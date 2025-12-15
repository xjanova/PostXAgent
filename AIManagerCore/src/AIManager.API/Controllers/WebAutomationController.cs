using Microsoft.AspNetCore.Mvc;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.API.Controllers;

/// <summary>
/// Web Automation API - จัดการ Teaching Mode และ Workflow Execution
/// </summary>
[ApiController]
[Route("api/automation")]
public class WebAutomationController : ControllerBase
{
    private readonly ILogger<WebAutomationController> _logger;
    private readonly WorkflowStorage _storage;
    private readonly WorkflowLearningEngine _learningEngine;

    // Active teaching sessions
    private static readonly Dictionary<string, TeachingSessionState> _teachingSessions = new();
    private static readonly object _sessionLock = new();

    public WebAutomationController(
        ILogger<WebAutomationController> logger,
        WorkflowStorage storage,
        WorkflowLearningEngine learningEngine)
    {
        _logger = logger;
        _storage = storage;
        _learningEngine = learningEngine;
    }

    #region Teaching Mode API

    /// <summary>
    /// เริ่ม Teaching Session ใหม่
    /// </summary>
    [HttpPost("teaching/start")]
    public async Task<ActionResult<TeachingSessionResponse>> StartTeachingSession(
        [FromBody] StartTeachingRequest request)
    {
        _logger.LogInformation("Starting teaching session for {Platform}/{Type}",
            request.Platform, request.WorkflowType);

        var session = new TeachingSession
        {
            Platform = request.Platform,
            WorkflowType = request.WorkflowType,
            Status = TeachingStatus.Started
        };

        // สร้าง browser session
        var state = new TeachingSessionState
        {
            Session = session,
            Browser = new BrowserController(
                LoggerFactory.Create(b => b.AddConsole()).CreateLogger<BrowserController>(),
                new BrowserConfig { Headless = false }
            )
        };

        var launched = await state.Browser.LaunchAsync();
        if (!launched)
        {
            return BadRequest(new { Error = "Failed to launch browser" });
        }

        // Navigate to platform
        if (!string.IsNullOrEmpty(request.StartUrl))
        {
            await state.Browser.NavigateAsync(request.StartUrl);
        }

        // Start recording
        await state.Browser.StartRecordingAsync();
        session.Status = TeachingStatus.Recording;
        session.BrowserSessionId = state.Browser.SessionId;

        lock (_sessionLock)
        {
            _teachingSessions[session.Id] = state;
        }

        return Ok(new TeachingSessionResponse
        {
            Success = true,
            SessionId = session.Id,
            Status = session.Status.ToString(),
            Message = "Teaching session started. Perform actions in the browser, then call /teaching/next-step to add instructions.",
            Tips = GetTeachingTips(request.WorkflowType, 0)
        });
    }

    /// <summary>
    /// เพิ่มคำอธิบายสำหรับ Step ปัจจุบัน
    /// </summary>
    [HttpPost("teaching/{sessionId}/add-step")]
    public async Task<ActionResult<TeachingStepResponse>> AddTeachingStep(
        string sessionId,
        [FromBody] AddStepRequest request)
    {
        TeachingSessionState? state;
        lock (_sessionLock)
        {
            _teachingSessions.TryGetValue(sessionId, out state);
        }

        if (state == null)
        {
            return NotFound(new { Error = "Session not found" });
        }

        // ดึง recorded steps ล่าสุด
        var recordedSteps = await state.Browser.StopRecordingAsync();
        await state.Browser.StartRecordingAsync(); // Start recording again

        if (recordedSteps.Count == 0)
        {
            return Ok(new TeachingStepResponse
            {
                Success = false,
                Message = "No actions recorded. Please perform an action in the browser first."
            });
        }

        // เพิ่ม user instruction
        var lastStep = recordedSteps.Last();
        lastStep.UserInstruction = request.Instruction;

        // เพิ่มเข้า session
        state.Session.RecordedSteps.AddRange(recordedSteps);
        state.Session.CurrentStep = state.Session.RecordedSteps.Count;

        // ถ่าย screenshot
        var screenshot = await state.Browser.TakeScreenshotAsync();
        if (screenshot != null)
        {
            lastStep.Screenshot = screenshot;
        }

        return Ok(new TeachingStepResponse
        {
            Success = true,
            StepNumber = state.Session.CurrentStep,
            RecordedAction = lastStep.Action,
            ElementInfo = lastStep.Element != null ? new ElementInfo
            {
                TagName = lastStep.Element.TagName,
                Id = lastStep.Element.Id,
                Text = lastStep.Element.TextContent?.Substring(0, Math.Min(50, lastStep.Element.TextContent?.Length ?? 0))
            } : null,
            Message = $"Step {state.Session.CurrentStep} recorded: {lastStep.Action}",
            Tips = GetTeachingTips(state.Session.WorkflowType, state.Session.CurrentStep)
        });
    }

    /// <summary>
    /// ดูตัวอย่าง Workflow ที่กำลังสอน
    /// </summary>
    [HttpGet("teaching/{sessionId}/preview")]
    public ActionResult<TeachingPreviewResponse> PreviewTeachingSession(string sessionId)
    {
        TeachingSessionState? state;
        lock (_sessionLock)
        {
            _teachingSessions.TryGetValue(sessionId, out state);
        }

        if (state == null)
        {
            return NotFound(new { Error = "Session not found" });
        }

        return Ok(new TeachingPreviewResponse
        {
            SessionId = sessionId,
            Platform = state.Session.Platform,
            WorkflowType = state.Session.WorkflowType,
            StepCount = state.Session.RecordedSteps.Count,
            Steps = state.Session.RecordedSteps.Select((s, i) => new StepPreview
            {
                Index = i,
                Action = s.Action,
                Instruction = s.UserInstruction,
                ElementTag = s.Element?.TagName,
                Value = s.Value?.Substring(0, Math.Min(50, s.Value?.Length ?? 0))
            }).ToList()
        });
    }

    /// <summary>
    /// จบการสอนและบันทึก Workflow
    /// </summary>
    [HttpPost("teaching/{sessionId}/complete")]
    public async Task<ActionResult<WorkflowSavedResponse>> CompleteTeachingSession(
        string sessionId,
        [FromBody] CompleteTeachingRequest? request = null)
    {
        TeachingSessionState? state;
        lock (_sessionLock)
        {
            _teachingSessions.TryGetValue(sessionId, out state);
        }

        if (state == null)
        {
            return NotFound(new { Error = "Session not found" });
        }

        // ดึง steps ที่ยังไม่ได้บันทึก
        var finalSteps = await state.Browser.StopRecordingAsync();
        state.Session.RecordedSteps.AddRange(finalSteps);

        if (state.Session.RecordedSteps.Count == 0)
        {
            return BadRequest(new { Error = "No steps recorded" });
        }

        // เรียนรู้ Workflow
        state.Session.Status = TeachingStatus.Completed;
        state.Session.CompletedAt = DateTime.UtcNow;

        var workflow = await _learningEngine.LearnFromTeachingSession(state.Session);

        // Override name/description if provided
        if (!string.IsNullOrEmpty(request?.WorkflowName))
        {
            workflow.Name = request.WorkflowName;
        }
        if (!string.IsNullOrEmpty(request?.Description))
        {
            workflow.Description = request.Description;
        }

        await _storage.SaveWorkflowAsync(workflow);

        // Cleanup
        await state.Browser.DisposeAsync();
        lock (_sessionLock)
        {
            _teachingSessions.Remove(sessionId);
        }

        return Ok(new WorkflowSavedResponse
        {
            Success = true,
            WorkflowId = workflow.Id,
            Name = workflow.Name,
            StepCount = workflow.Steps.Count,
            Message = $"Workflow '{workflow.Name}' saved with {workflow.Steps.Count} steps"
        });
    }

    /// <summary>
    /// ยกเลิก Teaching Session
    /// </summary>
    [HttpPost("teaching/{sessionId}/cancel")]
    public async Task<ActionResult> CancelTeachingSession(string sessionId)
    {
        TeachingSessionState? state;
        lock (_sessionLock)
        {
            _teachingSessions.TryGetValue(sessionId, out state);
        }

        if (state != null)
        {
            await state.Browser.DisposeAsync();
            lock (_sessionLock)
            {
                _teachingSessions.Remove(sessionId);
            }
        }

        return Ok(new { Message = "Session cancelled" });
    }

    #endregion

    #region Workflow Management API

    /// <summary>
    /// รายการ Workflows ทั้งหมด
    /// </summary>
    [HttpGet("workflows")]
    public async Task<ActionResult<List<WorkflowSummary>>> GetWorkflows(
        [FromQuery] string? platform = null)
    {
        var workflows = string.IsNullOrEmpty(platform)
            ? await _storage.GetAllWorkflowsAsync()
            : await _storage.GetWorkflowsByPlatformAsync(platform);

        return Ok(workflows.Select(w => new WorkflowSummary
        {
            Id = w.Id,
            Platform = w.Platform,
            Name = w.Name,
            Description = w.Description,
            StepCount = w.Steps.Count,
            ConfidenceScore = w.ConfidenceScore,
            SuccessCount = w.SuccessCount,
            FailureCount = w.FailureCount,
            IsActive = w.IsActive,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        }).ToList());
    }

    /// <summary>
    /// ดูรายละเอียด Workflow
    /// </summary>
    [HttpGet("workflows/{id}")]
    public async Task<ActionResult<LearnedWorkflow>> GetWorkflow(string id)
    {
        var workflow = await _storage.LoadWorkflowAsync(id);
        if (workflow == null)
        {
            return NotFound();
        }
        return Ok(workflow);
    }

    /// <summary>
    /// ลบ Workflow
    /// </summary>
    [HttpDelete("workflows/{id}")]
    public async Task<ActionResult> DeleteWorkflow(string id)
    {
        await _storage.DeleteWorkflowAsync(id);
        return Ok(new { Message = "Workflow deleted" });
    }

    /// <summary>
    /// Export Workflow
    /// </summary>
    [HttpGet("workflows/{id}/export")]
    public async Task<ActionResult> ExportWorkflow(string id)
    {
        try
        {
            var json = await _storage.ExportWorkflowAsync(id);
            return Content(json, "application/json");
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Import Workflow
    /// </summary>
    [HttpPost("workflows/import")]
    public async Task<ActionResult<WorkflowSavedResponse>> ImportWorkflow([FromBody] ImportWorkflowRequest request)
    {
        try
        {
            var workflow = await _storage.ImportWorkflowAsync(request.Json);
            return Ok(new WorkflowSavedResponse
            {
                Success = true,
                WorkflowId = workflow.Id,
                Name = workflow.Name,
                StepCount = workflow.Steps.Count
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// สถิติ Workflows
    /// </summary>
    [HttpGet("workflows/statistics")]
    public async Task<ActionResult<WorkflowStatistics>> GetStatistics()
    {
        return Ok(await _storage.GetStatisticsAsync());
    }

    #endregion

    #region Execution API

    /// <summary>
    /// รัน Workflow
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<ExecutionResponse>> ExecuteWorkflow(
        [FromBody] ExecuteWorkflowRequest request,
        CancellationToken ct)
    {
        var workflow = await _storage.LoadWorkflowAsync(request.WorkflowId);
        if (workflow == null)
        {
            return NotFound(new { Error = "Workflow not found" });
        }

        if (!workflow.IsActive)
        {
            return BadRequest(new { Error = "Workflow is inactive" });
        }

        _logger.LogInformation("Executing workflow {Id}: {Name}", workflow.Id, workflow.Name);

        // สร้าง browser และ executor
        var browserConfig = new BrowserConfig
        {
            Headless = request.Headless,
            WindowWidth = request.WindowWidth ?? 1280,
            WindowHeight = request.WindowHeight ?? 800
        };

        await using var browser = new BrowserController(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<BrowserController>(),
            browserConfig
        );

        var launched = await browser.LaunchAsync(ct);
        if (!launched)
        {
            return BadRequest(new { Error = "Failed to launch browser" });
        }

        var executor = new WorkflowExecutor(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<WorkflowExecutor>(),
            browser,
            _learningEngine,
            new AIElementAnalyzer(LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AIElementAnalyzer>())
        );

        var result = await executor.ExecuteAsync(workflow, request.Content, request.Credentials, ct);

        return Ok(new ExecutionResponse
        {
            Success = result.Success,
            WorkflowId = result.WorkflowId,
            Duration = result.Duration.TotalSeconds,
            StepsExecuted = result.StepResults.Count,
            FailedAtStep = result.FailedAtStep,
            Error = result.Error,
            StepResults = result.StepResults.Select(s => new StepResultSummary
            {
                StepId = s.StepId,
                Action = s.Action,
                Success = s.Success,
                Error = s.Error
            }).ToList()
        });
    }

    /// <summary>
    /// ทดสอบ Workflow (Dry Run)
    /// </summary>
    [HttpPost("test")]
    public async Task<ActionResult<ExecutionResponse>> TestWorkflow(
        [FromBody] ExecuteWorkflowRequest request,
        CancellationToken ct)
    {
        request.Headless = true;
        return await ExecuteWorkflow(request, ct);
    }

    #endregion

    private string[] GetTeachingTips(string workflowType, int stepNumber)
    {
        var tips = new List<string>();

        if (workflowType.Contains("post", StringComparison.OrdinalIgnoreCase))
        {
            tips.AddRange(stepNumber switch
            {
                0 => new[] {
                    "เริ่มต้นที่หน้า Create Post หรือ Home",
                    "คลิกที่ช่องพิมพ์ข้อความโพส"
                },
                1 => new[] {
                    "พิมพ์ข้อความทดสอบ (AI จะจำตำแหน่งช่องพิมพ์)",
                    "ถ้าต้องการเพิ่มรูปภาพ ให้คลิกปุ่ม Photo/Image"
                },
                2 => new[] {
                    "เลือกไฟล์รูปภาพ (ถ้าต้องการ)",
                    "คลิกปุ่ม Post/Share เพื่อโพส"
                },
                _ => new[] {
                    "ดำเนินการต่อหรือกด Complete เพื่อจบการสอน"
                }
            });
        }
        else if (workflowType.Contains("login", StringComparison.OrdinalIgnoreCase))
        {
            tips.AddRange(new[]
            {
                "กรอก Username/Email",
                "กรอก Password",
                "คลิกปุ่ม Login"
            });
        }

        return tips.ToArray();
    }
}

// Session state
internal class TeachingSessionState
{
    public TeachingSession Session { get; set; } = new();
    public BrowserController Browser { get; set; } = null!;
}

// Request/Response Models
public class StartTeachingRequest
{
    public string Platform { get; set; } = "";
    public string WorkflowType { get; set; } = "";
    public string? StartUrl { get; set; }
}

public class AddStepRequest
{
    public string Instruction { get; set; } = "";
}

public class CompleteTeachingRequest
{
    public string? WorkflowName { get; set; }
    public string? Description { get; set; }
}

public class ImportWorkflowRequest
{
    public string Json { get; set; } = "";
}

public class ExecuteWorkflowRequest
{
    public string WorkflowId { get; set; } = "";
    public WebPostContent Content { get; set; } = new();
    public WebCredentials? Credentials { get; set; }
    public bool Headless { get; set; } = true;
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
}

public class TeachingSessionResponse
{
    public bool Success { get; set; }
    public string SessionId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string[] Tips { get; set; } = Array.Empty<string>();
}

public class TeachingStepResponse
{
    public bool Success { get; set; }
    public int StepNumber { get; set; }
    public string? RecordedAction { get; set; }
    public ElementInfo? ElementInfo { get; set; }
    public string Message { get; set; } = "";
    public string[] Tips { get; set; } = Array.Empty<string>();
}

public class ElementInfo
{
    public string TagName { get; set; } = "";
    public string? Id { get; set; }
    public string? Text { get; set; }
}

public class TeachingPreviewResponse
{
    public string SessionId { get; set; } = "";
    public string Platform { get; set; } = "";
    public string WorkflowType { get; set; } = "";
    public int StepCount { get; set; }
    public List<StepPreview> Steps { get; set; } = new();
}

public class StepPreview
{
    public int Index { get; set; }
    public string Action { get; set; } = "";
    public string? Instruction { get; set; }
    public string? ElementTag { get; set; }
    public string? Value { get; set; }
}

public class WorkflowSavedResponse
{
    public bool Success { get; set; }
    public string WorkflowId { get; set; } = "";
    public string Name { get; set; } = "";
    public int StepCount { get; set; }
    public string? Message { get; set; }
}

public class WorkflowSummary
{
    public string Id { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int StepCount { get; set; }
    public double ConfidenceScore { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ExecutionResponse
{
    public bool Success { get; set; }
    public string WorkflowId { get; set; } = "";
    public double Duration { get; set; }
    public int StepsExecuted { get; set; }
    public int? FailedAtStep { get; set; }
    public string? Error { get; set; }
    public List<StepResultSummary> StepResults { get; set; } = new();
}

public class StepResultSummary
{
    public string StepId { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
}
