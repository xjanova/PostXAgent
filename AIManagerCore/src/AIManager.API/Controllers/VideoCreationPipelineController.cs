using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;
using System.Collections.Concurrent;

namespace AIManager.API.Controllers;

/// <summary>
/// API Controller for Video Creation Pipeline
/// ตัวควบคุม API สำหรับ Pipeline การสร้างวิดีโอแบบครบวงจร
///
/// รองรับ:
/// - สร้างวิดีโอจาก concept (รูป + วิดีโอ + เพลง + ตัดต่อ + โพสต์)
/// - สร้าง slideshow จากรูป
/// - รวมวิดีโอกับเพลง
/// - ดูสถานะ pipeline ที่กำลังทำงาน
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class VideoCreationPipelineController : ControllerBase
{
    private readonly VideoCreationPipeline? _pipeline;
    private readonly FreepikAutomationService? _freepikService;
    private readonly SunoAutomationService? _sunoService;
    private readonly ILogger<VideoCreationPipelineController> _logger;

    // In-memory storage for running pipelines (consider using distributed cache in production)
    private static readonly ConcurrentDictionary<string, PipelineStatus> _runningPipelines = new();

    public VideoCreationPipelineController(
        VideoCreationPipeline? pipeline,
        FreepikAutomationService? freepikService,
        SunoAutomationService? sunoService,
        ILogger<VideoCreationPipelineController> logger)
    {
        _pipeline = pipeline;
        _freepikService = freepikService;
        _sunoService = sunoService;
        _logger = logger;
    }

    #region Pipeline Endpoints

    /// <summary>
    /// Create video from concept - Full pipeline
    /// สร้างวิดีโอแบบครบวงจรจาก concept
    ///
    /// Flow: Concept -> AI Plan -> Images -> Videos -> Music -> Compose -> Post
    /// </summary>
    [HttpPost("create-from-concept")]
    public async Task<ActionResult<PipelineSubmitResponse>> CreateFromConcept(
        [FromBody] VideoConceptApiRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_pipeline == null)
            {
                return ServiceUnavailable("VideoCreationPipeline not configured");
            }

            if (string.IsNullOrWhiteSpace(request.Concept))
            {
                return BadRequest(new PipelineSubmitResponse
                {
                    Success = false,
                    Error = "Concept is required"
                });
            }

            _logger.LogInformation("Starting video creation pipeline for concept: {Concept}", request.Concept);

            var conceptRequest = new VideoConceptRequest
            {
                Concept = request.Concept,
                Style = request.Style,
                TargetAudience = request.TargetAudience,
                TotalDurationSeconds = request.TotalDurationSeconds ?? 60,
                AspectRatio = request.AspectRatio ?? AspectRatio.Portrait_9_16,
                GenerateVideoFromImages = request.GenerateVideoFromImages ?? true,
                MaxVideosFromImages = request.MaxVideosFromImages ?? 3,
                VideoClipDurationSeconds = request.VideoClipDurationSeconds ?? 5,
                SlideshowDurationPerImage = request.SlideshowDurationPerImage ?? 3,
                GenerateMusic = request.GenerateMusic ?? true,
                MusicStyle = request.MusicStyle,
                InstrumentalOnly = request.InstrumentalOnly ?? true,
                TargetPlatforms = request.TargetPlatforms,
                AutoPost = request.AutoPost ?? false
            };

            var pipelineId = Guid.NewGuid().ToString();

            // Track pipeline status
            var status = new PipelineStatus
            {
                PipelineId = pipelineId,
                Concept = request.Concept,
                Stage = PipelineStage.Planning,
                StartedAt = DateTime.UtcNow
            };
            _runningPipelines[pipelineId] = status;

            // Run pipeline with progress tracking
            var progress = new Progress<PipelineProgress>(p =>
            {
                if (_runningPipelines.TryGetValue(pipelineId, out var s))
                {
                    s.CurrentStep = p.CurrentStep;
                    s.TotalSteps = p.TotalSteps;
                    s.Percentage = p.Percentage;
                    s.Message = p.Message;
                    s.Stage = p.Stage;
                }
            });

            // Start pipeline in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await _pipeline.CreateVideoFromConceptAsync(conceptRequest, progress, ct);

                    if (_runningPipelines.TryGetValue(pipelineId, out var s))
                    {
                        s.IsCompleted = true;
                        s.Success = result.Success;
                        s.Error = result.Error;
                        s.FinalVideoPath = result.FinalVideoPath;
                        s.CompletedAt = DateTime.UtcNow;
                        s.Result = result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Pipeline {PipelineId} failed", pipelineId);
                    if (_runningPipelines.TryGetValue(pipelineId, out var s))
                    {
                        s.IsCompleted = true;
                        s.Success = false;
                        s.Error = ex.Message;
                        s.CompletedAt = DateTime.UtcNow;
                    }
                }
            }, ct);

            return Ok(new PipelineSubmitResponse
            {
                Success = true,
                PipelineId = pipelineId,
                Message = "Video creation pipeline started",
                StatusUrl = $"/api/VideoCreationPipeline/status/{pipelineId}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start video creation pipeline");
            return BadRequest(new PipelineSubmitResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get pipeline status
    /// ดูสถานะ pipeline ที่กำลังทำงาน
    /// </summary>
    [HttpGet("status/{pipelineId}")]
    public ActionResult<PipelineStatusResponse> GetStatus(string pipelineId)
    {
        if (!_runningPipelines.TryGetValue(pipelineId, out var status))
        {
            return NotFound(new PipelineStatusResponse
            {
                Success = false,
                Error = "Pipeline not found"
            });
        }

        return Ok(new PipelineStatusResponse
        {
            Success = true,
            PipelineId = pipelineId,
            Concept = status.Concept,
            Stage = status.Stage.ToString(),
            CurrentStep = status.CurrentStep,
            TotalSteps = status.TotalSteps,
            Percentage = status.Percentage,
            Message = status.Message,
            IsCompleted = status.IsCompleted,
            PipelineSuccess = status.Success,
            Error = status.Error,
            FinalVideoPath = status.FinalVideoPath,
            StartedAt = status.StartedAt,
            CompletedAt = status.CompletedAt,
            Duration = status.CompletedAt.HasValue
                ? (status.CompletedAt.Value - status.StartedAt).TotalSeconds
                : (DateTime.UtcNow - status.StartedAt).TotalSeconds
        });
    }

    /// <summary>
    /// Get full pipeline result (after completion)
    /// ดึงผลลัพธ์ pipeline ทั้งหมด (หลังเสร็จสิ้น)
    /// </summary>
    [HttpGet("result/{pipelineId}")]
    public ActionResult<PipelineResultResponse> GetResult(string pipelineId)
    {
        if (!_runningPipelines.TryGetValue(pipelineId, out var status))
        {
            return NotFound(new PipelineResultResponse
            {
                Success = false,
                Error = "Pipeline not found"
            });
        }

        if (!status.IsCompleted)
        {
            return Ok(new PipelineResultResponse
            {
                Success = false,
                PipelineId = pipelineId,
                Error = "Pipeline still running",
                Status = "in_progress"
            });
        }

        return Ok(new PipelineResultResponse
        {
            Success = status.Success ?? false,
            PipelineId = pipelineId,
            Status = status.Success == true ? "completed" : "failed",
            Error = status.Error,
            Result = status.Result
        });
    }

    /// <summary>
    /// List all pipelines
    /// ดูรายการ pipeline ทั้งหมด
    /// </summary>
    [HttpGet("list")]
    public ActionResult<PipelineListResponse> ListPipelines(
        [FromQuery] int limit = 20,
        [FromQuery] bool includeCompleted = true)
    {
        var pipelines = _runningPipelines.Values
            .Where(p => includeCompleted || !p.IsCompleted)
            .OrderByDescending(p => p.StartedAt)
            .Take(limit)
            .Select(p => new PipelineSummary
            {
                PipelineId = p.PipelineId,
                Concept = p.Concept,
                Stage = p.Stage.ToString(),
                Percentage = p.Percentage,
                IsCompleted = p.IsCompleted,
                Success = p.Success,
                StartedAt = p.StartedAt,
                CompletedAt = p.CompletedAt
            })
            .ToList();

        return Ok(new PipelineListResponse
        {
            Success = true,
            TotalCount = _runningPipelines.Count,
            Pipelines = pipelines
        });
    }

    /// <summary>
    /// Cancel a running pipeline
    /// ยกเลิก pipeline ที่กำลังทำงาน
    /// </summary>
    [HttpPost("cancel/{pipelineId}")]
    public ActionResult CancelPipeline(string pipelineId)
    {
        if (!_runningPipelines.TryGetValue(pipelineId, out var status))
        {
            return NotFound(new { Error = "Pipeline not found" });
        }

        if (status.IsCompleted)
        {
            return BadRequest(new { Error = "Pipeline already completed" });
        }

        // Mark as cancelled
        status.IsCompleted = true;
        status.Success = false;
        status.Error = "Cancelled by user";
        status.CompletedAt = DateTime.UtcNow;

        return Ok(new { Success = true, Message = "Pipeline cancellation requested" });
    }

    #endregion

    #region Utility Endpoints

    /// <summary>
    /// Create slideshow from images (without AI generation)
    /// สร้าง slideshow จากรูปที่มีอยู่ (ไม่ใช้ AI)
    /// </summary>
    [HttpPost("slideshow")]
    public async Task<ActionResult<SlideshowResponse>> CreateSlideshow(
        [FromBody] SlideshowApiRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_pipeline == null)
            {
                return ServiceUnavailable("VideoCreationPipeline not configured");
            }

            if (request.ImagePaths == null || request.ImagePaths.Count == 0)
            {
                return BadRequest(new SlideshowResponse { Success = false, Error = "Image paths are required" });
            }

            _logger.LogInformation("Creating slideshow from {Count} images", request.ImagePaths.Count);

            var options = new SlideshowOptions
            {
                DurationPerImage = request.DurationPerImage ?? 3,
                Transition = request.Transition ?? "fade",
                TransitionDuration = request.TransitionDuration ?? 0.5
            };

            var result = await _pipeline.CreateSlideshowAsync(
                request.ImagePaths, request.AudioPath, options, ct);

            if (string.IsNullOrEmpty(result))
            {
                return BadRequest(new SlideshowResponse
                {
                    Success = false,
                    Error = "Failed to create slideshow"
                });
            }

            return Ok(new SlideshowResponse
            {
                Success = true,
                VideoPath = result,
                Message = $"Slideshow created from {request.ImagePaths.Count} images"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create slideshow");
            return BadRequest(new SlideshowResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Merge videos with music
    /// รวมวิดีโอหลายคลิปเข้าด้วยกันพร้อมเพลง
    /// </summary>
    [HttpPost("merge-with-music")]
    public async Task<ActionResult<MergeResponse>> MergeVideosWithMusic(
        [FromBody] MergeApiRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_pipeline == null)
            {
                return ServiceUnavailable("VideoCreationPipeline not configured");
            }

            if (request.VideoPaths == null || request.VideoPaths.Count == 0)
            {
                return BadRequest(new MergeResponse { Success = false, Error = "Video paths are required" });
            }

            if (string.IsNullOrEmpty(request.AudioPath))
            {
                return BadRequest(new MergeResponse { Success = false, Error = "Audio path is required" });
            }

            _logger.LogInformation("Merging {Count} videos with music", request.VideoPaths.Count);

            var result = await _pipeline.MergeVideosWithMusicAsync(
                request.VideoPaths, request.AudioPath, request.AudioVolume ?? 0.8, ct);

            if (string.IsNullOrEmpty(result))
            {
                return BadRequest(new MergeResponse
                {
                    Success = false,
                    Error = "Failed to merge videos"
                });
            }

            return Ok(new MergeResponse
            {
                Success = true,
                VideoPath = result,
                Message = $"Merged {request.VideoPaths.Count} videos with music"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge videos");
            return BadRequest(new MergeResponse { Success = false, Error = ex.Message });
        }
    }

    #endregion

    #region Freepik/Suno Info Endpoints

    /// <summary>
    /// Get Freepik unlimited models
    /// ดูรายชื่อโมเดลฟรี (Unlimited) ของ Freepik
    /// </summary>
    [HttpGet("freepik/unlimited-models")]
    public ActionResult<UnlimitedModelsResponse> GetFreepikUnlimitedModels()
    {
        var models = _freepikService?.GetUnlimitedModels() ?? Array.Empty<string>();

        return Ok(new UnlimitedModelsResponse
        {
            Success = true,
            Models = models.ToList(),
            Message = "These models are FREE and do not consume credits"
        });
    }

    /// <summary>
    /// Check if a Freepik model is unlimited (free)
    /// ตรวจสอบว่าโมเดลเป็น Unlimited (ฟรี) หรือไม่
    /// </summary>
    [HttpGet("freepik/is-unlimited/{modelName}")]
    public ActionResult<ModelCheckResponse> IsFreepikModelUnlimited(string modelName)
    {
        var isUnlimited = _freepikService?.IsUnlimitedModel(modelName) ?? false;

        return Ok(new ModelCheckResponse
        {
            Success = true,
            ModelName = modelName,
            IsUnlimited = isUnlimited,
            Message = isUnlimited
                ? "This model is FREE (Unlimited)"
                : "This model consumes credits - NOT recommended"
        });
    }

    /// <summary>
    /// Get Suno supported genres
    /// ดูรายการ genres ที่ Suno รองรับ
    /// </summary>
    [HttpGet("suno/genres")]
    public ActionResult<GenresResponse> GetSunoGenres()
    {
        var genres = SunoAutomationService.SupportedGenres;

        return Ok(new GenresResponse
        {
            Success = true,
            Genres = genres.ToList()
        });
    }

    /// <summary>
    /// Get Suno supported moods
    /// ดูรายการ moods ที่ Suno รองรับ
    /// </summary>
    [HttpGet("suno/moods")]
    public ActionResult<MoodsResponse> GetSunoMoods()
    {
        var moods = SunoAutomationService.SupportedMoods;

        return Ok(new MoodsResponse
        {
            Success = true,
            Moods = moods.ToList()
        });
    }

    /// <summary>
    /// Get Suno credits info
    /// ดูข้อมูล credits ของ Suno
    /// </summary>
    [HttpGet("suno/credits")]
    public async Task<ActionResult<SunoCreditsResponse>> GetSunoCredits(CancellationToken ct)
    {
        if (_sunoService == null)
        {
            return ServiceUnavailable("SunoAutomationService not configured");
        }

        try
        {
            var credits = await _sunoService.GetCreditsInfoAsync(ct);

            return Ok(new SunoCreditsResponse
            {
                Success = true,
                RemainingCredits = credits.Credits,
                TotalCredits = credits.IsPro ? 500 : 50, // Pro users get more credits
                IsUnlimited = credits.IsPro,
                ExpiresAt = null // Not tracked
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new SunoCreditsResponse { Success = false, Error = ex.Message });
        }
    }

    #endregion

    #region Quick Test Endpoints

    /// <summary>
    /// Quick test - Generate single image with Freepik
    /// ทดสอบสร้างรูปด้วย Freepik (ใช้เฉพาะโมเดล Unlimited)
    /// </summary>
    [HttpPost("test/freepik-image")]
    public async Task<ActionResult<QuickTestResponse>> TestFreepikImage(
        [FromBody] FreepikImageTestRequest request,
        CancellationToken ct)
    {
        if (_freepikService == null)
        {
            return ServiceUnavailable("FreepikAutomationService not configured");
        }

        try
        {
            _logger.LogInformation("Testing Freepik image generation: {Prompt}", request.Prompt);

            var result = await _freepikService.GenerateImageAsync(
                request.Prompt,
                new FreepikImageOptions
                {
                    AspectRatio = request.AspectRatio ?? AspectRatio.Square_1_1,
                    Style = request.Style
                },
                ct);

            return Ok(new QuickTestResponse
            {
                Success = result.Success,
                MediaUrl = result.MediaUrl,
                LocalPath = result.LocalPath,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new QuickTestResponse { Success = false, Error = ex.Message });
        }
    }

    /// <summary>
    /// Quick test - Generate music with Suno
    /// ทดสอบสร้างเพลงด้วย Suno
    /// </summary>
    [HttpPost("test/suno-music")]
    public async Task<ActionResult<QuickTestResponse>> TestSunoMusic(
        [FromBody] SunoMusicTestRequest request,
        CancellationToken ct)
    {
        if (_sunoService == null)
        {
            return ServiceUnavailable("SunoAutomationService not configured");
        }

        try
        {
            _logger.LogInformation("Testing Suno music generation: {Prompt}", request.Prompt);

            var result = await _sunoService.GenerateMusicAsync(
                request.Prompt,
                new SunoMusicOptions
                {
                    Style = request.Style,
                    IsInstrumental = request.IsInstrumental ?? true
                },
                ct);

            var firstSong = result.GeneratedSongs.FirstOrDefault();

            return Ok(new QuickTestResponse
            {
                Success = result.Success,
                MediaUrl = firstSong?.AudioUrl,
                LocalPath = firstSong?.LocalPath,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new QuickTestResponse { Success = false, Error = ex.Message });
        }
    }

    #endregion

    #region Helper Methods

    private ObjectResult ServiceUnavailable(string message)
    {
        return StatusCode(503, new { Success = false, Error = message });
    }

    #endregion
}

#region Internal Status Tracking

internal class PipelineStatus
{
    public string PipelineId { get; set; } = "";
    public string? Concept { get; set; }
    public PipelineStage Stage { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public double Percentage { get; set; }
    public string? Message { get; set; }
    public bool IsCompleted { get; set; }
    public bool? Success { get; set; }
    public string? Error { get; set; }
    public string? FinalVideoPath { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public PipelineResult? Result { get; set; }
}

#endregion

#region API Request/Response Models

public class VideoConceptApiRequest
{
    /// <summary>แนวคิด/หัวข้อหลักของวิดีโอ (ภาษาไทยหรืออังกฤษ)</summary>
    public string Concept { get; set; } = "";

    /// <summary>สไตล์ของวิดีโอ เช่น "Modern", "Vintage", "Corporate"</summary>
    public string? Style { get; set; }

    /// <summary>กลุ่มเป้าหมาย เช่น "Young adults", "Business professionals"</summary>
    public string? TargetAudience { get; set; }

    /// <summary>ความยาวรวม (วินาที) - default: 60</summary>
    public int? TotalDurationSeconds { get; set; }

    /// <summary>Aspect ratio - default: Portrait_9_16 (TikTok/Reels)</summary>
    public AspectRatio? AspectRatio { get; set; }

    /// <summary>สร้างวิดีโอจากรูปหรือไม่ - default: true</summary>
    public bool? GenerateVideoFromImages { get; set; }

    /// <summary>จำนวนวิดีโอสูงสุดที่จะสร้างจากรูป - default: 3</summary>
    public int? MaxVideosFromImages { get; set; }

    /// <summary>ความยาวของแต่ละ video clip (วินาที) - default: 5</summary>
    public int? VideoClipDurationSeconds { get; set; }

    /// <summary>ความยาวต่อรูปใน slideshow (วินาที) - default: 3</summary>
    public int? SlideshowDurationPerImage { get; set; }

    /// <summary>สร้างเพลงหรือไม่ - default: true</summary>
    public bool? GenerateMusic { get; set; }

    /// <summary>สไตล์เพลง เช่น "Pop", "Electronic", "Thai Pop"</summary>
    public string? MusicStyle { get; set; }

    /// <summary>เพลงแบบ instrumental (ไม่มีเนื้อร้อง) - default: true</summary>
    public bool? InstrumentalOnly { get; set; }

    /// <summary>Platforms ที่จะเผยแพร่</summary>
    public List<SocialPlatform>? TargetPlatforms { get; set; }

    /// <summary>โพสต์อัตโนมัติหลังสร้างเสร็จ - default: false</summary>
    public bool? AutoPost { get; set; }
}

public class PipelineSubmitResponse
{
    public bool Success { get; set; }
    public string? PipelineId { get; set; }
    public string? Message { get; set; }
    public string? StatusUrl { get; set; }
    public string? Error { get; set; }
}

public class PipelineStatusResponse
{
    public bool Success { get; set; }
    public string? PipelineId { get; set; }
    public string? Concept { get; set; }
    public string? Stage { get; set; }
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public double Percentage { get; set; }
    public string? Message { get; set; }
    public bool IsCompleted { get; set; }
    public bool? PipelineSuccess { get; set; }
    public string? Error { get; set; }
    public string? FinalVideoPath { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double Duration { get; set; }
}

public class PipelineResultResponse
{
    public bool Success { get; set; }
    public string? PipelineId { get; set; }
    public string? Status { get; set; }
    public string? Error { get; set; }
    public PipelineResult? Result { get; set; }
}

public class PipelineListResponse
{
    public bool Success { get; set; }
    public int TotalCount { get; set; }
    public List<PipelineSummary> Pipelines { get; set; } = new();
}

public class PipelineSummary
{
    public string PipelineId { get; set; } = "";
    public string? Concept { get; set; }
    public string? Stage { get; set; }
    public double Percentage { get; set; }
    public bool IsCompleted { get; set; }
    public bool? Success { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class SlideshowApiRequest
{
    public List<string> ImagePaths { get; set; } = new();
    public string? AudioPath { get; set; }
    public int? DurationPerImage { get; set; }
    public string? Transition { get; set; }
    public double? TransitionDuration { get; set; }
}

public class SlideshowResponse
{
    public bool Success { get; set; }
    public string? VideoPath { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class MergeApiRequest
{
    public List<string> VideoPaths { get; set; } = new();
    public string AudioPath { get; set; } = "";
    public double? AudioVolume { get; set; }
}

public class MergeResponse
{
    public bool Success { get; set; }
    public string? VideoPath { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class UnlimitedModelsResponse
{
    public bool Success { get; set; }
    public List<string> Models { get; set; } = new();
    public string? Message { get; set; }
}

public class ModelCheckResponse
{
    public bool Success { get; set; }
    public string? ModelName { get; set; }
    public bool IsUnlimited { get; set; }
    public string? Message { get; set; }
}

public class GenresResponse
{
    public bool Success { get; set; }
    public List<string> Genres { get; set; } = new();
}

public class MoodsResponse
{
    public bool Success { get; set; }
    public List<string> Moods { get; set; } = new();
}

public class SunoCreditsResponse
{
    public bool Success { get; set; }
    public int RemainingCredits { get; set; }
    public int TotalCredits { get; set; }
    public bool IsUnlimited { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Error { get; set; }
}

public class FreepikImageTestRequest
{
    public string Prompt { get; set; } = "";
    public AspectRatio? AspectRatio { get; set; }
    public string? Style { get; set; }
}

public class SunoMusicTestRequest
{
    public string Prompt { get; set; } = "";
    public string? Style { get; set; }
    public bool? IsInstrumental { get; set; }
}

public class QuickTestResponse
{
    public bool Success { get; set; }
    public string? MediaUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? Error { get; set; }
}

#endregion
