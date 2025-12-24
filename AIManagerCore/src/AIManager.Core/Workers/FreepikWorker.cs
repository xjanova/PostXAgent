using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Worker for Freepik Pikaso AI video generation
/// PRIMARY video generation provider
/// ตัวประมวลผลสำหรับการสร้างวีดีโอด้วย Freepik Pikaso AI
///
/// NOTE: Web Learning integration not suitable for this use case because:
/// - Freepik is a generative AI service, not a posting platform
/// - Need to extract generated content (video URLs), which Web Learning doesn't support
/// - Web Learning is designed for posting automation, not media generation
///
/// Future enhancement: Direct API integration when Freepik provides official API
/// </summary>
public class FreepikWorker : BasePlatformWorker
{
    private readonly VideoProcessor _videoProcessor;
    private readonly ILogger<FreepikWorker> _logger2;

    private const string FREEPIK_URL = "https://www.freepik.com/pikaso";

    public override SocialPlatform Platform => SocialPlatform.Freepik;

    /// <summary>
    /// Constructor
    /// </summary>
    public FreepikWorker(
        ILogger<FreepikWorker> logger,
        VideoProcessor videoProcessor)
    {
        _logger2 = logger;
        _videoProcessor = videoProcessor;
    }

    /// <summary>
    /// Generate video using Freepik Pikaso AI
    /// สร้างวีดีโอด้วย Freepik Pikaso AI
    ///
    /// Currently returns placeholder results until official API is available.
    /// </summary>
    public async Task<TaskResult> GenerateVideoAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var config = task.Payload.VideoConfig;
            if (config == null)
            {
                throw new ArgumentException("VideoConfig is required for video generation");
            }

            _logger2.LogInformation("Starting Freepik video generation: {Prompt}", config.Prompt);
            _logger2.LogInformation("URL: {Url}", FREEPIK_URL);

            // TODO: Integrate with Freepik API when available
            // For now, return placeholder result
            return await GeneratePlaceholder(task, config, sw, ct);
        }
        catch (Exception ex)
        {
            _logger2.LogError(ex, "Freepik video generation failed");

            return new TaskResult
            {
                TaskId = task.Id ?? "",
                WorkerId = 0,
                Platform = Platform.ToString(),
                Success = false,
                Error = ex.Message,
                ErrorType = ErrorType.PlatformError,
                ShouldRetry = true,
                RetryAfterSeconds = 60,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Generate placeholder result
    /// Returns simulated result until real API integration is available
    /// </summary>
    private Task<TaskResult> GeneratePlaceholder(
        TaskItem task,
        VideoGenerationConfig config,
        System.Diagnostics.Stopwatch sw,
        CancellationToken ct)
    {
        _logger2.LogWarning("Returning placeholder result");

        var result = new MediaGenerationResult
        {
            VideoUrl = $"https://placeholder.freepik.com/video/{Guid.NewGuid()}",
            VideoPath = null,
            ThumbnailUrl = null,
            ThumbnailPath = null,
            Metadata = new VideoMetadata
            {
                Width = GetWidthForAspectRatio(config.AspectRatio),
                Height = GetHeightForAspectRatio(config.AspectRatio),
                Duration = config.Duration,
                Fps = config.Fps,
                Format = "mp4",
                FileSize = 0,
                CreatedAt = DateTime.UtcNow
            }
        };

        return Task.FromResult(new TaskResult
        {
            TaskId = task.Id ?? "",
            WorkerId = 0,
            Platform = Platform.ToString(),
            Success = true,
            Data = new ResultData
            {
                MediaResult = result,
                Message = $"Placeholder video result for prompt: '{config.Prompt}'. Real Freepik Pikaso AI integration pending official API availability."
            },
            ProcessingTimeMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// PostContentAsync not applicable for video generation platform
    /// </summary>
    public override Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        return Task.FromResult(new TaskResult
        {
            Success = false,
            Error = "Freepik is a video generation platform, not a posting platform"
        });
    }

    /// <summary>
    /// AnalyzeMetricsAsync not applicable for video generation platform
    /// </summary>
    public override Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        return Task.FromResult(new TaskResult
        {
            Success = false,
            Error = "Freepik is a video generation platform, metrics analysis not applicable"
        });
    }

    private int GetWidthForAspectRatio(AspectRatio aspectRatio)
    {
        return aspectRatio switch
        {
            AspectRatio.Landscape_16_9 => 1920,
            AspectRatio.Portrait_9_16 => 1080,
            AspectRatio.Square_1_1 => 1080,
            AspectRatio.Classic_4_3 => 1440,
            AspectRatio.Ultrawide_21_9 => 2560,
            _ => 1920
        };
    }

    private int GetHeightForAspectRatio(AspectRatio aspectRatio)
    {
        return aspectRatio switch
        {
            AspectRatio.Landscape_16_9 => 1080,
            AspectRatio.Portrait_9_16 => 1920,
            AspectRatio.Square_1_1 => 1080,
            AspectRatio.Classic_4_3 => 1080,
            AspectRatio.Ultrawide_21_9 => 1080,
            _ => 1080
        };
    }
}
