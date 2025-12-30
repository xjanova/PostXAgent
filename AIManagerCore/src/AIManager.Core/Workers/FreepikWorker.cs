using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Worker for Freepik Pikaso AI image and video generation
/// PRIMARY image/video generation provider
/// ตัวประมวลผลสำหรับการสร้างรูปภาพและวีดีโอด้วย Freepik Pikaso AI
///
/// ใช้ Web Automation ผ่าน FreepikAutomationService สำหรับ:
/// - สร้างรูปจาก prompt (ใช้เฉพาะโมเดล Unlimited ที่ไม่เสีย credits)
/// - แปลงรูปเป็นวิดีโอ (Image to Video)
/// - ดาวน์โหลดไฟล์ที่สร้าง
/// </summary>
public class FreepikWorker : BasePlatformWorker
{
    private readonly VideoProcessor _videoProcessor;
    private readonly FreepikAutomationService? _automationService;
    private readonly ILogger<FreepikWorker> _logger2;

    private const string FREEPIK_URL = "https://www.freepik.com/pikaso";

    public override SocialPlatform Platform => SocialPlatform.Freepik;

    /// <summary>
    /// Constructor with automation service
    /// </summary>
    public FreepikWorker(
        ILogger<FreepikWorker> logger,
        VideoProcessor videoProcessor,
        FreepikAutomationService? automationService = null)
    {
        _logger2 = logger;
        _videoProcessor = videoProcessor;
        _automationService = automationService;
    }

    /// <summary>
    /// Generate image using Freepik Pikaso AI
    /// สร้างรูปด้วย Freepik Pikaso AI (ใช้เฉพาะโมเดล Unlimited)
    /// </summary>
    public override async Task<TaskResult> GenerateImageAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // ใช้ prompt จาก payload โดยตรง
            var prompt = task.Payload.Prompt;
            if (string.IsNullOrEmpty(prompt))
            {
                throw new ArgumentException("Prompt is required for image generation");
            }

            var style = task.Payload.Style;

            _logger2.LogInformation("Starting Freepik image generation: {Prompt}", prompt);

            // ใช้ Web Automation ถ้ามี
            if (_automationService != null)
            {
                var result = await _automationService.GenerateImageAsync(
                    prompt,
                    new FreepikImageOptions
                    {
                        AspectRatio = AspectRatio.Square_1_1,
                        Style = style
                    },
                    ct);

                if (result.Success)
                {
                    return new TaskResult
                    {
                        TaskId = task.Id ?? "",
                        WorkerId = 0,
                        Platform = Platform.ToString(),
                        Success = true,
                        Data = new ResultData
                        {
                            MediaResult = new MediaGenerationResult
                            {
                                VideoUrl = result.MediaUrl, // ใช้ VideoUrl เพื่อความเข้ากันได้
                                VideoPath = result.LocalPath,
                                Metadata = new VideoMetadata
                                {
                                    Format = "jpg",
                                    CreatedAt = DateTime.UtcNow
                                }
                            },
                            Message = $"Image generated successfully for prompt: '{prompt}'"
                        },
                        ProcessingTimeMs = sw.ElapsedMilliseconds
                    };
                }
                else
                {
                    throw new Exception(result.Error ?? "Image generation failed");
                }
            }

            // Fallback: placeholder ถ้าไม่มี automation service
            var imageConfig = new ImageGenerationConfig { Prompt = prompt, Style = style };
            return await GenerateImagePlaceholder(task, imageConfig, sw, ct);
        }
        catch (Exception ex)
        {
            _logger2.LogError(ex, "Freepik image generation failed");

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
    /// Generate video using Freepik Pikaso AI
    /// สร้างวีดีโอด้วย Freepik Pikaso AI
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

            // ใช้ Web Automation ถ้ามี (ใช้ SourceImageUrl สำหรับ local path ด้วย)
            if (_automationService != null && !string.IsNullOrEmpty(config.SourceImageUrl))
            {
                var result = await _automationService.GenerateVideoFromImageAsync(
                    config.SourceImageUrl,
                    config.Prompt,
                    new FreepikVideoOptions
                    {
                        DurationSeconds = (int)config.Duration,
                        MaxWaitMinutes = 5
                    },
                    ct);

                if (result.Success)
                {
                    return new TaskResult
                    {
                        TaskId = task.Id ?? "",
                        WorkerId = 0,
                        Platform = Platform.ToString(),
                        Success = true,
                        Data = new ResultData
                        {
                            MediaResult = new MediaGenerationResult
                            {
                                VideoUrl = result.MediaUrl,
                                VideoPath = result.LocalPath,
                                Metadata = new VideoMetadata
                                {
                                    Width = GetWidthForAspectRatio(config.AspectRatio),
                                    Height = GetHeightForAspectRatio(config.AspectRatio),
                                    Duration = config.Duration,
                                    Fps = config.Fps,
                                    Format = "mp4",
                                    CreatedAt = DateTime.UtcNow
                                }
                            },
                            Message = $"Video generated successfully for prompt: '{config.Prompt}'"
                        },
                        ProcessingTimeMs = sw.ElapsedMilliseconds
                    };
                }
                else
                {
                    throw new Exception(result.Error ?? "Video generation failed");
                }
            }

            // Fallback: placeholder
            return await GenerateVideoPlaceholder(task, config, sw, ct);
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
    /// Generate multiple images for slideshow
    /// สร้างหลายรูปสำหรับทำ slideshow
    /// </summary>
    public async Task<TaskResult> GenerateMultipleImagesAsync(
        List<string> prompts,
        AspectRatio aspectRatio,
        CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var generatedPaths = new List<string>();

        try
        {
            if (_automationService == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Automation service not available",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            var results = await _automationService.GenerateMultipleImagesAsync(
                prompts,
                new FreepikImageOptions { AspectRatio = aspectRatio },
                ct);

            generatedPaths = results
                .Where(r => r.Success && !string.IsNullOrEmpty(r.LocalPath))
                .Select(r => r.LocalPath!)
                .ToList();

            return new TaskResult
            {
                Success = generatedPaths.Count > 0,
                Data = new ResultData
                {
                    GeneratedFiles = generatedPaths,
                    Message = $"Generated {generatedPaths.Count}/{prompts.Count} images"
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger2.LogError(ex, "Failed to generate multiple images");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Check if a model name is unlimited (free)
    /// ตรวจสอบว่าโมเดลเป็น Unlimited (ฟรี) หรือไม่
    /// </summary>
    public bool IsUnlimitedModel(string modelName)
    {
        return _automationService?.IsUnlimitedModel(modelName) ?? false;
    }

    /// <summary>
    /// Get list of available unlimited models
    /// ดึงรายชื่อโมเดล Unlimited ที่ใช้ได้
    /// </summary>
    public IReadOnlyCollection<string> GetUnlimitedModels()
    {
        return _automationService?.GetUnlimitedModels() ?? Array.Empty<string>();
    }

    #region Placeholder Methods (used when automation not available)

    private Task<TaskResult> GenerateImagePlaceholder(
        TaskItem task,
        ImageGenerationConfig config,
        System.Diagnostics.Stopwatch sw,
        CancellationToken ct)
    {
        _logger2.LogWarning("Returning image placeholder result - automation service not available");

        var result = new MediaGenerationResult
        {
            VideoUrl = $"https://placeholder.freepik.com/image/{Guid.NewGuid()}",
            Metadata = new VideoMetadata
            {
                Width = GetWidthForAspectRatio(config.AspectRatio),
                Height = GetHeightForAspectRatio(config.AspectRatio),
                Format = "jpg",
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
                Message = $"[PLACEHOLDER] Image for prompt: '{config.Prompt}'. Use web automation for real generation."
            },
            ProcessingTimeMs = sw.ElapsedMilliseconds
        });
    }

    private Task<TaskResult> GenerateVideoPlaceholder(
        TaskItem task,
        VideoGenerationConfig config,
        System.Diagnostics.Stopwatch sw,
        CancellationToken ct)
    {
        _logger2.LogWarning("Returning video placeholder result - automation service not available");

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
                Message = $"[PLACEHOLDER] Video for prompt: '{config.Prompt}'. Use web automation for real generation."
            },
            ProcessingTimeMs = sw.ElapsedMilliseconds
        });
    }

    #endregion

    #region Platform Worker Overrides

    /// <summary>
    /// PostContentAsync not applicable for video generation platform
    /// </summary>
    public override Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        return Task.FromResult(new TaskResult
        {
            Success = false,
            Error = "Freepik is a media generation platform, not a posting platform. Use other platform workers for posting."
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
            Error = "Freepik is a media generation platform, metrics analysis not applicable"
        });
    }

    #endregion

    #region Helper Methods

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

    #endregion
}

/// <summary>
/// Image generation config
/// </summary>
public class ImageGenerationConfig
{
    public string Prompt { get; set; } = "";
    public string? Style { get; set; }
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Square_1_1;
    public int NumberOfImages { get; set; } = 1;
}
