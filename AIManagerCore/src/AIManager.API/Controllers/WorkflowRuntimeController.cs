using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.API.Controllers;

/// <summary>
/// Workflow Runtime Controller
/// API สำหรับจัดการ workflows และ jobs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkflowRuntimeController : ControllerBase
{
    private readonly WorkflowRuntimeManager _runtimeManager;
    private readonly ILogger<WorkflowRuntimeController> _logger;

    public WorkflowRuntimeController(
        WorkflowRuntimeManager runtimeManager,
        ILogger<WorkflowRuntimeController> logger)
    {
        _runtimeManager = runtimeManager;
        _logger = logger;
    }

    #region Workflows

    /// <summary>
    /// ดึง workflows ทั้งหมด
    /// </summary>
    [HttpGet("workflows")]
    public ActionResult<object> GetAllWorkflows()
    {
        var workflows = _runtimeManager.GetAllWorkflows();

        return Ok(new
        {
            success = true,
            data = workflows.Select(w => new
            {
                id = w.Id,
                name = w.Name,
                platform = w.Platform,
                taskType = w.TaskType,
                currentVersion = w.CurrentVersion,
                isActive = w.IsActive,
                createdAt = w.CreatedAt,
                lastModified = w.LastModified,
                totalVersions = w.Versions.Count
            })
        });
    }

    /// <summary>
    /// ดึง workflow ตาม ID
    /// </summary>
    [HttpGet("workflows/{id}")]
    public ActionResult<object> GetWorkflow(string id, [FromQuery] int? version = null)
    {
        var definition = _runtimeManager.GetLatestWorkflow(id);
        if (definition == null)
        {
            return NotFound(new { success = false, error = "Workflow not found" });
        }

        LearnedWorkflow? workflow;
        if (version.HasValue)
        {
            workflow = _runtimeManager.GetWorkflowVersion(id, version.Value);
            if (workflow == null)
            {
                return NotFound(new { success = false, error = $"Version {version} not found" });
            }
        }
        else
        {
            workflow = definition.CurrentWorkflow;
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                id = definition.Id,
                name = definition.Name,
                platform = definition.Platform,
                taskType = definition.TaskType,
                currentVersion = definition.CurrentVersion,
                requestedVersion = version ?? definition.CurrentVersion,
                isActive = definition.IsActive,
                createdAt = definition.CreatedAt,
                lastModified = definition.LastModified,
                versions = definition.Versions.Keys.OrderByDescending(v => v),
                workflow = workflow != null ? new
                {
                    id = workflow.Id,
                    name = workflow.Name,
                    platform = workflow.Platform,
                    taskType = workflow.TaskType,
                    version = workflow.Version,
                    isHumanTrained = workflow.IsHumanTrained,
                    steps = workflow.Steps.Select(s => new
                    {
                        order = s.Order,
                        action = s.Action.ToString(),
                        description = s.Description,
                        selector = s.Selector != null ? new
                        {
                            type = s.Selector.Type.ToString(),
                            value = s.Selector.Value,
                            confidence = s.Selector.Confidence
                        } : null,
                        inputValue = s.InputValue,
                        waitAfterMs = s.WaitAfterMs
                    })
                } : null
            }
        });
    }

    /// <summary>
    /// ดึง workflows ตาม platform
    /// </summary>
    [HttpGet("workflows/platform/{platform}")]
    public ActionResult<object> GetWorkflowsByPlatform(string platform, [FromQuery] string? taskType = null)
    {
        var workflows = _runtimeManager.GetWorkflowsByPlatform(platform, taskType);

        return Ok(new
        {
            success = true,
            data = workflows.Select(w => new
            {
                id = w.Id,
                name = w.Name,
                platform = w.Platform,
                taskType = w.TaskType,
                currentVersion = w.CurrentVersion,
                isActive = w.IsActive
            })
        });
    }

    /// <summary>
    /// บันทึก/อัพเดต workflow
    /// </summary>
    [HttpPost("workflows")]
    public async Task<ActionResult<object>> SaveWorkflow([FromBody] SaveWorkflowRequest request)
    {
        try
        {
            var workflow = new LearnedWorkflow
            {
                Id = request.Id ?? Guid.NewGuid().ToString(),
                Name = request.Name,
                Platform = request.Platform,
                TaskType = request.TaskType,
                IsHumanTrained = request.IsHumanTrained,
                CreatedAt = DateTime.UtcNow
            };

            // Parse steps
            foreach (var stepReq in request.Steps)
            {
                var step = new WorkflowStep
                {
                    Order = stepReq.Order,
                    Action = Enum.TryParse<StepAction>(stepReq.Action, true, out var action)
                        ? action : StepAction.Click,
                    Description = stepReq.Description,
                    InputValue = stepReq.InputValue,
                    WaitAfterMs = stepReq.WaitAfterMs
                };

                if (stepReq.Selector != null)
                {
                    step.Selector = new ElementSelector
                    {
                        Type = Enum.TryParse<SelectorType>(stepReq.Selector.Type, true, out var sType)
                            ? sType : SelectorType.CSS,
                        Value = stepReq.Selector.Value,
                        Confidence = stepReq.Selector.Confidence
                    };
                }

                workflow.Steps.Add(step);
            }

            var definition = await _runtimeManager.SaveWorkflowAsync(workflow);

            return Ok(new
            {
                success = true,
                data = new
                {
                    id = definition.Id,
                    name = definition.Name,
                    currentVersion = definition.CurrentVersion,
                    message = request.Id == null
                        ? "Workflow created successfully"
                        : $"Workflow updated to version {definition.CurrentVersion}"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save workflow");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// ลบ workflow
    /// </summary>
    [HttpDelete("workflows/{id}")]
    public async Task<ActionResult<object>> DeleteWorkflow(string id)
    {
        var result = await _runtimeManager.DeleteWorkflowAsync(id);

        if (!result)
        {
            return BadRequest(new
            {
                success = false,
                error = "Cannot delete workflow - either not found or has active jobs"
            });
        }

        return Ok(new { success = true, message = "Workflow deleted" });
    }

    /// <summary>
    /// Toggle workflow active status
    /// </summary>
    [HttpPost("workflows/{id}/toggle")]
    public ActionResult<object> ToggleWorkflow(string id, [FromQuery] bool active)
    {
        var definition = _runtimeManager.GetLatestWorkflow(id);
        if (definition == null)
        {
            return NotFound(new { success = false, error = "Workflow not found" });
        }

        _runtimeManager.SetWorkflowActive(id, active);

        return Ok(new
        {
            success = true,
            data = new { id, isActive = active }
        });
    }

    #endregion

    #region Jobs

    /// <summary>
    /// ดึง jobs ที่กำลังทำงาน
    /// </summary>
    [HttpGet("jobs/active")]
    public ActionResult<object> GetActiveJobs()
    {
        var jobs = _runtimeManager.GetActiveJobs();

        return Ok(new
        {
            success = true,
            data = jobs.Select(j => MapJobToResponse(j))
        });
    }

    /// <summary>
    /// ดึง jobs ทั้งหมด
    /// </summary>
    [HttpGet("jobs")]
    public ActionResult<object> GetAllJobs([FromQuery] int limit = 100, [FromQuery] string? status = null)
    {
        List<ActiveJob> jobs;

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<JobStatus>(status, true, out var jobStatus))
        {
            jobs = _runtimeManager.GetJobsByStatus(jobStatus);
        }
        else
        {
            jobs = _runtimeManager.GetAllJobs(limit);
        }

        return Ok(new
        {
            success = true,
            data = jobs.Select(j => MapJobToResponse(j))
        });
    }

    /// <summary>
    /// ดึง job ตาม ID
    /// </summary>
    [HttpGet("jobs/{id}")]
    public ActionResult<object> GetJob(string id)
    {
        var jobs = _runtimeManager.GetAllJobs(int.MaxValue);
        var job = jobs.FirstOrDefault(j => j.Id == id);

        if (job == null)
        {
            return NotFound(new { success = false, error = "Job not found" });
        }

        return Ok(new
        {
            success = true,
            data = MapJobToResponse(job, includeLogs: true)
        });
    }

    /// <summary>
    /// เริ่มงานใหม่
    /// </summary>
    [HttpPost("jobs/start")]
    public ActionResult<object> StartJob([FromBody] StartJobRequest request)
    {
        var job = _runtimeManager.StartJob(request.WorkflowId, request.UserId, request.Parameters);

        if (job == null)
        {
            return BadRequest(new
            {
                success = false,
                error = "Cannot start job - workflow not found or inactive"
            });
        }

        return Ok(new
        {
            success = true,
            data = MapJobToResponse(job)
        });
    }

    /// <summary>
    /// Cancel job
    /// </summary>
    [HttpPost("jobs/{id}/cancel")]
    public ActionResult<object> CancelJob(string id)
    {
        var result = _runtimeManager.CancelJob(id);

        if (!result)
        {
            return BadRequest(new { success = false, error = "Cannot cancel job" });
        }

        return Ok(new { success = true, message = "Job cancelled" });
    }

    /// <summary>
    /// Pause job
    /// </summary>
    [HttpPost("jobs/{id}/pause")]
    public ActionResult<object> PauseJob(string id)
    {
        var result = _runtimeManager.PauseJob(id);

        if (!result)
        {
            return BadRequest(new { success = false, error = "Cannot pause job" });
        }

        return Ok(new { success = true, message = "Job paused" });
    }

    /// <summary>
    /// Resume job
    /// </summary>
    [HttpPost("jobs/{id}/resume")]
    public ActionResult<object> ResumeJob(string id)
    {
        var result = _runtimeManager.ResumeJob(id);

        if (!result)
        {
            return BadRequest(new { success = false, error = "Cannot resume job" });
        }

        return Ok(new { success = true, message = "Job resumed" });
    }

    /// <summary>
    /// Cleanup completed jobs
    /// </summary>
    [HttpPost("jobs/cleanup")]
    public ActionResult<object> CleanupJobs([FromQuery] int hoursOld = 24)
    {
        var count = _runtimeManager.CleanupCompletedJobs(TimeSpan.FromHours(hoursOld));

        return Ok(new
        {
            success = true,
            data = new { removedCount = count }
        });
    }

    #endregion

    #region Statistics

    /// <summary>
    /// ดึงสถิติของระบบ
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<object> GetStatistics()
    {
        var stats = _runtimeManager.GetStatistics();

        return Ok(new
        {
            success = true,
            data = new
            {
                workflows = new
                {
                    total = stats.TotalWorkflows,
                    active = stats.ActiveWorkflows
                },
                jobs = new
                {
                    total = stats.TotalJobs,
                    running = stats.RunningJobs,
                    pending = stats.PendingJobs,
                    paused = stats.PausedJobs,
                    completed = stats.CompletedJobs,
                    failed = stats.FailedJobs,
                    cancelled = stats.CancelledJobs
                },
                lastUpdated = stats.LastUpdated
            }
        });
    }

    #endregion

    #region Helpers

    private object MapJobToResponse(ActiveJob job, bool includeLogs = false)
    {
        var response = new Dictionary<string, object?>
        {
            ["id"] = job.Id,
            ["workflowId"] = job.WorkflowId,
            ["workflowName"] = job.WorkflowName,
            ["workflowVersion"] = job.WorkflowVersion,
            ["userId"] = job.UserId,
            ["status"] = job.Status.ToString(),
            ["progress"] = new
            {
                percentage = job.Progress.Percentage,
                currentStep = job.Progress.CurrentStep,
                currentStepIndex = job.Progress.CurrentStepIndex,
                totalSteps = job.Progress.TotalSteps,
                lastUpdate = job.Progress.LastUpdate
            },
            ["createdAt"] = job.CreatedAt,
            ["startedAt"] = job.StartedAt,
            ["completedAt"] = job.CompletedAt,
            ["duration"] = job.Duration?.TotalSeconds,
            ["result"] = job.Result,
            ["error"] = job.Error
        };

        if (includeLogs)
        {
            response["logs"] = job.Progress.Logs.Select(l => new
            {
                timestamp = l.Timestamp,
                message = l.Message,
                level = l.Level
            });
        }

        return response;
    }

    #endregion
}

#region Request Models

public class SaveWorkflowRequest
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public bool IsHumanTrained { get; set; }
    public List<WorkflowStepRequest> Steps { get; set; } = new();
}

public class WorkflowStepRequest
{
    public int Order { get; set; }
    public string Action { get; set; } = "Click";
    public string? Description { get; set; }
    public SelectorRequest? Selector { get; set; }
    public string? InputValue { get; set; }
    public int WaitAfterMs { get; set; }
}

public class SelectorRequest
{
    public string Type { get; set; } = "CSS";
    public string Value { get; set; } = "";
    public double Confidence { get; set; } = 0.9;
}

public class StartJobRequest
{
    public string WorkflowId { get; set; } = "";
    public string? UserId { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

#endregion
