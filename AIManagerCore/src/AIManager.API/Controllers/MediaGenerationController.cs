using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.Core.Workers;

namespace AIManager.API.Controllers;

/// <summary>
/// API Controller for AI Video and Music Generation
/// ตัวควบคุม API สำหรับการสร้างวีดีโอและเพลงด้วย AI
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MediaGenerationController : ControllerBase
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly WorkerFactory _workerFactory;
    private readonly ILogger<MediaGenerationController> _logger;

    public MediaGenerationController(
        ProcessOrchestrator orchestrator,
        WorkerFactory workerFactory,
        ILogger<MediaGenerationController> logger)
    {
        _orchestrator = orchestrator;
        _workerFactory = workerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Generate video using AI (Freepik Pikaso AI - PRIMARY)
    /// สร้างวีดีโอด้วย AI
    /// </summary>
    /// <param name="request">Video generation configuration</param>
    /// <returns>Task with video generation result</returns>
    [HttpPost("generate-video")]
    public async Task<ActionResult<TaskSubmitResponse>> GenerateVideo(
        [FromBody] VideoGenerationRequest request,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Generating video: {Prompt}", request.Config?.Prompt);

            // Create task item
            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = TaskType.GenerateVideo,
                Platform = SocialPlatform.Freepik, // Default to Freepik (PRIMARY)
                UserId = request.UserId,
                BrandId = request.BrandId,
                Payload = new TaskPayload
                {
                    VideoConfig = request.Config
                },
                Priority = request.Priority ?? 5,
                CreatedAt = DateTime.UtcNow,
                Status = AIManager.Core.Models.TaskStatus.Pending
            };

            // Submit task to orchestrator
            var taskId = await _orchestrator.SubmitTaskAsync(task);

            return Ok(new TaskSubmitResponse
            {
                Success = true,
                TaskId = taskId,
                Message = "Video generation task submitted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit video generation task");
            return BadRequest(new TaskSubmitResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Generate music using AI (Suno AI - PRIMARY)
    /// สร้างเพลงด้วย AI
    /// </summary>
    /// <param name="request">Music generation configuration</param>
    /// <returns>Task with music generation result</returns>
    [HttpPost("generate-music")]
    public async Task<ActionResult<TaskSubmitResponse>> GenerateMusic(
        [FromBody] MusicGenerationRequest request,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Generating music: {Prompt}", request.Config?.Prompt);

            // Create task item
            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = TaskType.GenerateMusic,
                Platform = SocialPlatform.SunoAI,
                UserId = request.UserId,
                BrandId = request.BrandId,
                Payload = new TaskPayload
                {
                    MusicConfig = request.Config
                },
                Priority = request.Priority ?? 5,
                CreatedAt = DateTime.UtcNow,
                Status = AIManager.Core.Models.TaskStatus.Pending
            };

            // Submit task to orchestrator
            var taskId = await _orchestrator.SubmitTaskAsync(task);

            return Ok(new TaskSubmitResponse
            {
                Success = true,
                TaskId = taskId,
                Message = "Music generation task submitted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit music generation task");
            return BadRequest(new TaskSubmitResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Process video (mix audio, concat, resize, etc.)
    /// ประมวลผลวีดีโอ (ผสมเสียง ต่อวีดีโอ ปรับขนาด ฯลฯ)
    /// </summary>
    /// <param name="request">Video processing configuration</param>
    /// <returns>Task with processed video result</returns>
    [HttpPost("process-video")]
    public async Task<ActionResult<TaskSubmitResponse>> ProcessVideo(
        [FromBody] VideoProcessingRequest request,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Processing video: {VideoPath}", request.Config?.VideoPath);

            var taskType = DetermineProcessingTaskType(request.Config);

            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = taskType,
                Platform = SocialPlatform.Freepik, // Use Freepik platform for consistency
                UserId = request.UserId,
                BrandId = request.BrandId,
                Payload = new TaskPayload
                {
                    ProcessingConfig = request.Config
                },
                Priority = request.Priority ?? 5,
                CreatedAt = DateTime.UtcNow,
                Status = AIManager.Core.Models.TaskStatus.Pending
            };

            var taskId = await _orchestrator.SubmitTaskAsync(task);

            return Ok(new TaskSubmitResponse
            {
                Success = true,
                TaskId = taskId,
                Message = "Video processing task submitted successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit video processing task");
            return BadRequest(new TaskSubmitResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get video generation task result
    /// ดึงผลลัพธ์การสร้างวีดีโอ
    /// </summary>
    [HttpGet("result/{taskId}")]
    public ActionResult<MediaGenerationResultResponse> GetResult(string taskId)
    {
        try
        {
            var task = _orchestrator.GetTask(taskId);

            if (task == null)
            {
                return NotFound(new MediaGenerationResultResponse
                {
                    Success = false,
                    Error = "Task not found"
                });
            }

            // Get task result (this would need to be implemented in ProcessOrchestrator)
            // For now, return task status
            return Ok(new MediaGenerationResultResponse
            {
                Success = task.Status == AIManager.Core.Models.TaskStatus.Completed,
                TaskId = taskId,
                Status = task.Status.ToString(),
                Message = task.Status == AIManager.Core.Models.TaskStatus.Completed
                    ? "Task completed successfully"
                    : $"Task status: {task.Status}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get task result");
            return BadRequest(new MediaGenerationResultResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Quick test endpoint for video generation
    /// ทดสอบการสร้างวีดีโออย่างง่าย
    /// </summary>
    [HttpPost("test/quick-video")]
    public async Task<ActionResult<TaskResult>> QuickVideoTest(
        [FromBody] QuickVideoTestRequest request,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Quick video test: {Prompt}", request.Prompt);

            // Get Freepik worker directly
            var worker = _workerFactory.GetWorker(SocialPlatform.Freepik) as FreepikWorker;

            if (worker == null)
            {
                return BadRequest(new { Error = "Freepik worker not available" });
            }

            // Create test task
            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = TaskType.GenerateVideo,
                Platform = SocialPlatform.Freepik,
                UserId = 1,
                BrandId = 1,
                Payload = new TaskPayload
                {
                    VideoConfig = new VideoGenerationConfig
                    {
                        Prompt = request.Prompt,
                        Duration = request.Duration ?? 5,
                        AspectRatio = request.AspectRatio ?? AspectRatio.Landscape_16_9,
                        Quality = VideoQuality.High_1080p
                    }
                }
            };

            // Execute directly (bypass queue for testing)
            var result = await worker.GenerateVideoAsync(task, ct);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick video test failed");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Quick test endpoint for music generation
    /// ทดสอบการสร้างเพลงอย่างง่าย
    /// </summary>
    [HttpPost("test/quick-music")]
    public async Task<ActionResult<TaskResult>> QuickMusicTest(
        [FromBody] QuickMusicTestRequest request,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Quick music test: {Prompt}", request.Prompt);

            // Get Suno AI worker directly
            var worker = _workerFactory.GetWorker(SocialPlatform.SunoAI) as SunoAIWorker;

            if (worker == null)
            {
                return BadRequest(new { Error = "Suno AI worker not available" });
            }

            // Create test task
            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = TaskType.GenerateMusic,
                Platform = SocialPlatform.SunoAI,
                UserId = 1,
                BrandId = 1,
                Payload = new TaskPayload
                {
                    MusicConfig = new MusicGenerationConfig
                    {
                        Prompt = request.Prompt,
                        Duration = request.Duration ?? 30,
                        Genre = request.Genre,
                        Mood = request.Mood,
                        Instrumental = request.Instrumental ?? false
                    }
                }
            };

            // Execute directly (bypass queue for testing)
            var result = await worker.GenerateMusicAsync(task, ct);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quick music test failed");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// Determine task type based on processing config
    /// </summary>
    private TaskType DetermineProcessingTaskType(MediaProcessingConfig? config)
    {
        if (config == null) return TaskType.ProcessVideo;

        if (config.MixAudio && !string.IsNullOrEmpty(config.AudioPath))
            return TaskType.MixVideoWithMusic;

        if (config.VideosToConcat?.Count > 1)
            return TaskType.ConcatenateVideos;

        if (!string.IsNullOrEmpty(config.OutputFormat) && config.OutputFormat != "mp4")
            return TaskType.ConvertVideoFormat;

        if (config.OutputQuality.HasValue)
            return TaskType.ResizeVideo;

        return TaskType.ProcessVideo;
    }
}

#region Request/Response Models

public class VideoGenerationRequest
{
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public VideoGenerationConfig? Config { get; set; }
    public int? Priority { get; set; }
}

public class MusicGenerationRequest
{
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public MusicGenerationConfig? Config { get; set; }
    public int? Priority { get; set; }
}

public class VideoProcessingRequest
{
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public MediaProcessingConfig? Config { get; set; }
    public int? Priority { get; set; }
}

public class MediaGenerationResultResponse
{
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public string? Status { get; set; }
    public MediaGenerationResult? Result { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class QuickVideoTestRequest
{
    public string Prompt { get; set; } = "";
    public int? Duration { get; set; }
    public AspectRatio? AspectRatio { get; set; }
}

public class QuickMusicTestRequest
{
    public string Prompt { get; set; } = "";
    public int? Duration { get; set; }
    public MusicGenre? Genre { get; set; }
    public MusicMood? Mood { get; set; }
    public bool? Instrumental { get; set; }
}

#endregion
