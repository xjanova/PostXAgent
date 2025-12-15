using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ProcessOrchestrator orchestrator, ILogger<TasksController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Submit a new task for processing
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TaskSubmitResponse>> SubmitTask([FromBody] TaskItem task)
    {
        try
        {
            var taskId = await _orchestrator.SubmitTaskAsync(task);

            return Ok(new TaskSubmitResponse
            {
                Success = true,
                TaskId = taskId,
                Message = "Task submitted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit task");
            return BadRequest(new TaskSubmitResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get task status
    /// </summary>
    [HttpGet("{taskId}")]
    public ActionResult<TaskItem> GetTask(string taskId)
    {
        var task = _orchestrator.GetTask(taskId);

        if (task == null)
            return NotFound(new { Error = "Task not found" });

        return Ok(task);
    }

    /// <summary>
    /// Cancel a pending task
    /// </summary>
    [HttpDelete("{taskId}")]
    public ActionResult CancelTask(string taskId)
    {
        var result = _orchestrator.CancelTask(taskId);

        if (!result)
            return NotFound(new { Error = "Task not found or cannot be cancelled" });

        return Ok(new { Message = "Task cancelled" });
    }

    /// <summary>
    /// Generate content using AI
    /// </summary>
    [HttpPost("generate-content")]
    public async Task<ActionResult<TaskSubmitResponse>> GenerateContent([FromBody] ContentGenerationRequest request)
    {
        var task = new TaskItem
        {
            Type = TaskType.GenerateContent,
            Platform = request.Platform,
            UserId = request.UserId,
            BrandId = request.BrandId,
            Payload = new TaskPayload
            {
                Prompt = request.Prompt,
                BrandInfo = request.BrandInfo,
                ContentType = request.ContentType,
                Language = request.Language ?? "th"
            }
        };

        var taskId = await _orchestrator.SubmitTaskAsync(task);

        return Ok(new TaskSubmitResponse
        {
            Success = true,
            TaskId = taskId
        });
    }

    /// <summary>
    /// Generate image using AI
    /// </summary>
    [HttpPost("generate-image")]
    public async Task<ActionResult<TaskSubmitResponse>> GenerateImage([FromBody] ImageGenerationRequest request)
    {
        var task = new TaskItem
        {
            Type = TaskType.GenerateImage,
            Platform = SocialPlatform.Facebook, // Default
            UserId = request.UserId,
            BrandId = request.BrandId,
            Payload = new TaskPayload
            {
                Prompt = request.Prompt,
                Style = request.Style,
                Size = request.Size ?? "1024x1024",
                Provider = request.Provider ?? "auto"
            }
        };

        var taskId = await _orchestrator.SubmitTaskAsync(task);

        return Ok(new TaskSubmitResponse
        {
            Success = true,
            TaskId = taskId
        });
    }

    /// <summary>
    /// Post content to a platform
    /// </summary>
    [HttpPost("post")]
    public async Task<ActionResult<TaskSubmitResponse>> PostContent([FromBody] PostContentRequest request)
    {
        var task = new TaskItem
        {
            Type = TaskType.PostContent,
            Platform = request.Platform,
            UserId = request.UserId,
            BrandId = request.BrandId,
            Payload = new TaskPayload
            {
                Content = request.Content,
                Credentials = request.Credentials,
                ScheduledAt = request.ScheduledAt
            }
        };

        var taskId = await _orchestrator.SubmitTaskAsync(task);

        return Ok(new TaskSubmitResponse
        {
            Success = true,
            TaskId = taskId
        });
    }
}

// Request/Response models
public class TaskSubmitResponse
{
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class ContentGenerationRequest
{
    public SocialPlatform Platform { get; set; }
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public string Prompt { get; set; } = "";
    public BrandInfo? BrandInfo { get; set; }
    public ContentType ContentType { get; set; }
    public string? Language { get; set; }
}

public class ImageGenerationRequest
{
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public string Prompt { get; set; } = "";
    public string? Style { get; set; }
    public string? Size { get; set; }
    public string? Provider { get; set; }
}

public class PostContentRequest
{
    public SocialPlatform Platform { get; set; }
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public PostContent Content { get; set; } = new();
    public PlatformCredentials? Credentials { get; set; }
    public DateTime? ScheduledAt { get; set; }
}
