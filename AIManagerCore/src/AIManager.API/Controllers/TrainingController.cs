using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.Core.Workers;
using AIManager.Core.WebAutomation;

namespace AIManager.API.Controllers;

/// <summary>
/// Training Controller - API สำหรับสอน Worker ใหม่
/// รองรับ Human-in-the-Loop Training
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TrainingController : ControllerBase
{
    private readonly HumanTrainingService _trainingService;
    private readonly KnowledgeBase _knowledgeBase;
    private readonly WorkflowStorage _workflowStorage;
    private readonly ILogger<TrainingController> _logger;

    public TrainingController(
        HumanTrainingService trainingService,
        KnowledgeBase knowledgeBase,
        WorkflowStorage workflowStorage,
        ILogger<TrainingController> logger)
    {
        _trainingService = trainingService;
        _knowledgeBase = knowledgeBase;
        _workflowStorage = workflowStorage;
        _logger = logger;
    }

    #region Training Sessions

    /// <summary>
    /// เริ่ม Training Session ใหม่
    /// </summary>
    [HttpPost("sessions/start")]
    public async Task<IActionResult> StartTrainingSession([FromBody] StartTrainingRequest request)
    {
        try
        {
            if (!Enum.TryParse<SocialPlatform>(request.Platform, true, out var platform))
            {
                return BadRequest(new { error = $"Invalid platform: {request.Platform}" });
            }

            var session = await _trainingService.StartTrainingSessionAsync(
                platform,
                request.TaskType,
                request.AssistanceRequestId
            );

            return Ok(new
            {
                success = true,
                sessionId = session.Id,
                platform = session.Platform.ToString(),
                taskType = session.TaskType,
                startedAt = session.StartedAt,
                message = "Training session started. Begin recording your actions."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start training session");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// บันทึก Training Step
    /// </summary>
    [HttpPost("sessions/{sessionId}/steps")]
    public IActionResult RecordStep(string sessionId, [FromBody] RecordStepRequest request)
    {
        try
        {
            var step = new TrainingStep
            {
                Action = request.Action,
                ElementSelector = request.ElementSelector,
                SelectorType = request.SelectorType ?? "css",
                ElementId = request.ElementId,
                ElementXPath = request.ElementXPath,
                ElementText = request.ElementText,
                ElementDescription = request.ElementDescription,
                Description = request.Description,
                InputValue = request.InputValue,
                Url = request.Url,
                WaitForElement = request.WaitForElement,
                WaitTimeoutMs = request.WaitTimeoutMs,
                WaitMs = request.WaitMs,
                Screenshot = request.Screenshot
            };

            _trainingService.RecordTrainingStep(sessionId, step);

            return Ok(new
            {
                success = true,
                message = "Step recorded",
                stepNumber = step.StepNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record step");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// จบ Training Session และสร้าง Workflow
    /// </summary>
    [HttpPost("sessions/{sessionId}/complete")]
    public async Task<IActionResult> CompleteSession(string sessionId, [FromBody] CompleteSessionRequest? request = null)
    {
        try
        {
            var workflow = await _trainingService.CompleteTrainingSessionAsync(sessionId, request?.Notes);

            if (workflow == null)
            {
                return BadRequest(new { error = "Failed to generate workflow from training session" });
            }

            return Ok(new
            {
                success = true,
                message = "Training completed successfully",
                workflow = new
                {
                    id = workflow.Id,
                    name = workflow.Name,
                    platform = workflow.Platform,
                    taskType = workflow.TaskType,
                    stepsCount = workflow.Steps.Count,
                    createdAt = workflow.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete session");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ยกเลิก Training Session
    /// </summary>
    [HttpPost("sessions/{sessionId}/cancel")]
    public IActionResult CancelSession(string sessionId)
    {
        _trainingService.CancelTrainingSession(sessionId);
        return Ok(new { success = true, message = "Session cancelled" });
    }

    /// <summary>
    /// ดึงข้อมูล Training Session
    /// </summary>
    [HttpGet("sessions/{sessionId}")]
    public IActionResult GetSession(string sessionId)
    {
        var session = _trainingService.GetTrainingSession(sessionId);
        if (session == null)
        {
            return NotFound(new { error = "Session not found" });
        }

        return Ok(new
        {
            id = session.Id,
            platform = session.Platform.ToString(),
            taskType = session.TaskType,
            status = session.Status.ToString(),
            startedAt = session.StartedAt,
            completedAt = session.CompletedAt,
            stepsCount = session.Steps.Count,
            steps = session.Steps.Select(s => new
            {
                stepNumber = s.StepNumber,
                action = s.Action,
                description = s.Description ?? s.ElementDescription,
                selector = s.ElementSelector,
                recordedAt = s.RecordedAt
            })
        });
    }

    /// <summary>
    /// ดึง Active Training Sessions ทั้งหมด
    /// </summary>
    [HttpGet("sessions/active")]
    public IActionResult GetActiveSessions()
    {
        var sessions = _trainingService.GetActiveTrainingSessions();

        return Ok(new
        {
            count = sessions.Count,
            sessions = sessions.Select(s => new
            {
                id = s.Id,
                platform = s.Platform.ToString(),
                taskType = s.TaskType,
                status = s.Status.ToString(),
                stepsCount = s.Steps.Count,
                startedAt = s.StartedAt
            })
        });
    }

    #endregion

    #region Quick Training

    /// <summary>
    /// Quick Training - สอนด้วย steps อย่างง่าย
    /// </summary>
    [HttpPost("quick")]
    public async Task<IActionResult> QuickTrain([FromBody] QuickTrainRequest request)
    {
        try
        {
            if (!Enum.TryParse<SocialPlatform>(request.Platform, true, out var platform))
            {
                return BadRequest(new { error = $"Invalid platform: {request.Platform}" });
            }

            var steps = request.Steps.Select(s => new QuickTrainingStep
            {
                Action = s.Action,
                Selector = s.Selector,
                Description = s.Description,
                Value = s.Value,
                WaitMs = s.WaitMs,
                WaitForElement = s.WaitForElement
            }).ToList();

            var workflow = await _trainingService.QuickTrainAsync(
                platform,
                request.TaskType,
                request.StartUrl,
                steps
            );

            if (workflow == null)
            {
                return BadRequest(new { error = "Failed to create workflow" });
            }

            return Ok(new
            {
                success = true,
                message = "Quick training completed",
                workflow = new
                {
                    id = workflow.Id,
                    name = workflow.Name,
                    stepsCount = workflow.Steps.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick training failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Import Workflow จาก JSON
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportWorkflow([FromBody] TrainingImportWorkflowRequest request)
    {
        try
        {
            if (!Enum.TryParse<SocialPlatform>(request.Platform, true, out var platform))
            {
                return BadRequest(new { error = $"Invalid platform: {request.Platform}" });
            }

            var workflow = await _trainingService.ImportWorkflowAsync(
                platform,
                request.TaskType,
                request.WorkflowJson
            );

            if (workflow == null)
            {
                return BadRequest(new { error = "Failed to import workflow" });
            }

            return Ok(new
            {
                success = true,
                message = "Workflow imported successfully",
                workflowId = workflow.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Assistance Requests

    /// <summary>
    /// ดึงคำขอความช่วยเหลือที่รอดำเนินการ
    /// </summary>
    [HttpGet("assistance/pending")]
    public async Task<IActionResult> GetPendingAssistance()
    {
        try
        {
            var requests = await _knowledgeBase.GetPendingAssistanceRequestsAsync();

            return Ok(new
            {
                count = requests.Count,
                requests = requests.Select(r => new
                {
                    id = r.Id,
                    workerId = r.WorkerId,
                    workerName = r.WorkerName,
                    platform = r.Platform.ToString(),
                    taskType = r.TaskType,
                    errorMessage = r.ErrorMessage,
                    requestedAt = r.RequestedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending assistance");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// เริ่ม Training Session จาก Assistance Request
    /// </summary>
    [HttpPost("assistance/{requestId}/train")]
    public async Task<IActionResult> StartTrainingFromAssistance(string requestId)
    {
        try
        {
            var requests = await _knowledgeBase.GetPendingAssistanceRequestsAsync();
            var request = requests.FirstOrDefault(r => r.Id == requestId);

            if (request == null)
            {
                return NotFound(new { error = "Assistance request not found" });
            }

            var session = await _trainingService.StartTrainingSessionAsync(
                request.Platform,
                request.TaskType,
                requestId
            );

            return Ok(new
            {
                success = true,
                sessionId = session.Id,
                message = "Training session started for assistance request"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start training from assistance");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Knowledge Base

    /// <summary>
    /// ค้นหาความรู้ที่มีอยู่
    /// </summary>
    [HttpGet("knowledge/search")]
    public async Task<IActionResult> SearchKnowledge(
        [FromQuery] string? platform = null,
        [FromQuery] string? error = null)
    {
        try
        {
            if (string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(error))
            {
                return BadRequest(new { error = "platform and error are required" });
            }

            if (!Enum.TryParse<SocialPlatform>(platform, true, out var platformEnum))
            {
                return BadRequest(new { error = $"Invalid platform: {platform}" });
            }

            var knowledge = await _knowledgeBase.FindSolutionAsync(platformEnum, error);

            if (knowledge == null)
            {
                return Ok(new { found = false, message = "No solution found" });
            }

            return Ok(new
            {
                found = true,
                knowledge = new
                {
                    id = knowledge.Id,
                    platform = knowledge.Platform.ToString(),
                    errorPattern = knowledge.ErrorPattern,
                    solution = knowledge.Solution,
                    successCount = knowledge.SuccessCount,
                    failureCount = knowledge.FailureCount,
                    createdAt = knowledge.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Knowledge search failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ดึง error patterns ที่พบบ่อย
    /// </summary>
    [HttpGet("knowledge/errors/{platform}")]
    public IActionResult GetFrequentErrors(string platform)
    {
        if (!Enum.TryParse<SocialPlatform>(platform, true, out var platformEnum))
        {
            return BadRequest(new { error = $"Invalid platform: {platform}" });
        }

        var errors = _knowledgeBase.GetFrequentErrors(platformEnum);

        return Ok(new
        {
            platform = platform,
            errors = errors.Select(kv => new { pattern = kv.Key, count = kv.Value })
        });
    }

    #endregion

    #region Workflows

    /// <summary>
    /// ดึง Workflows ทั้งหมด
    /// </summary>
    [HttpGet("workflows")]
    public async Task<IActionResult> GetWorkflows([FromQuery] string? platform = null)
    {
        try
        {
            var workflows = await _workflowStorage.GetAllWorkflowsAsync();

            if (!string.IsNullOrEmpty(platform))
            {
                workflows = workflows.Where(w => w.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(new
            {
                count = workflows.Count,
                workflows = workflows.Select(w => new
                {
                    id = w.Id,
                    name = w.Name,
                    platform = w.Platform,
                    taskType = w.TaskType,
                    stepsCount = w.Steps.Count,
                    isHumanTrained = w.IsHumanTrained,
                    version = w.Version,
                    createdAt = w.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflows");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ดึง Workflow เฉพาะตัว
    /// </summary>
    [HttpGet("workflows/{id}")]
    public async Task<IActionResult> GetWorkflow(string id)
    {
        try
        {
            var workflows = await _workflowStorage.GetAllWorkflowsAsync();
            var workflow = workflows.FirstOrDefault(w => w.Id == id);

            if (workflow == null)
            {
                return NotFound(new { error = "Workflow not found" });
            }

            return Ok(workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Export Workflow เป็น JSON
    /// </summary>
    [HttpGet("workflows/{id}/export")]
    public async Task<IActionResult> ExportWorkflow(string id)
    {
        try
        {
            var workflows = await _workflowStorage.GetAllWorkflowsAsync();
            var workflow = workflows.FirstOrDefault(w => w.Id == id);

            if (workflow == null)
            {
                return NotFound(new { error = "Workflow not found" });
            }

            return Ok(new
            {
                workflowJson = workflow.ToJson()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export workflow");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion
}

#region Request Models

public class StartTrainingRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string? AssistanceRequestId { get; set; }
}

public class RecordStepRequest
{
    public string Action { get; set; } = "";
    public string? ElementSelector { get; set; }
    public string? SelectorType { get; set; }
    public string? ElementId { get; set; }
    public string? ElementXPath { get; set; }
    public string? ElementText { get; set; }
    public string? ElementDescription { get; set; }
    public string? Description { get; set; }
    public string? InputValue { get; set; }
    public string? Url { get; set; }
    public bool WaitForElement { get; set; }
    public int? WaitTimeoutMs { get; set; }
    public int WaitMs { get; set; }
    public string? Screenshot { get; set; }
}

public class CompleteSessionRequest
{
    public string? Notes { get; set; }
}

public class QuickTrainRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string StartUrl { get; set; } = "";
    public List<QuickStepRequest> Steps { get; set; } = new();
}

public class QuickStepRequest
{
    public string Action { get; set; } = "";
    public string Selector { get; set; } = "";
    public string? Description { get; set; }
    public string? Value { get; set; }
    public int WaitMs { get; set; }
    public bool WaitForElement { get; set; }
}

public class TrainingImportWorkflowRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string WorkflowJson { get; set; } = "";
}

#endregion
