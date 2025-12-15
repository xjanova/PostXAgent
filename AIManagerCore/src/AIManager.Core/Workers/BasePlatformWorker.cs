using System.Net.Http.Json;
using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Base class for platform-specific workers
/// </summary>
public abstract class BasePlatformWorker : IPlatformWorker
{
    protected readonly HttpClient _httpClient;
    protected readonly ILogger _logger;

    public abstract SocialPlatform Platform { get; }

    protected BasePlatformWorker()
    {
        _httpClient = new HttpClient();
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger(GetType());
    }

    public virtual async Task<TaskResult> GenerateContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var generator = new ContentGeneratorService();
            var content = await generator.GenerateAsync(
                task.Payload.Prompt ?? "",
                task.Payload.BrandInfo,
                Platform.ToString(),
                task.Payload.Language,
                ct
            );

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Content = content.Text,
                    Hashtags = content.Hashtags,
                    AIProvider = content.Provider
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Content generation failed");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public virtual async Task<TaskResult> GenerateImageAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var generator = new ImageGeneratorService();
            var image = await generator.GenerateAsync(
                task.Payload.Prompt ?? "",
                task.Payload.Style ?? "modern",
                task.Payload.Size,
                task.Payload.Provider,
                ct
            );

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    ImageUrl = image.Url,
                    ImageBase64 = image.Base64Data,
                    AIProvider = image.Provider
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image generation failed");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public abstract Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct);

    public virtual async Task<TaskResult> SchedulePostAsync(TaskItem task, CancellationToken ct)
    {
        // Default: store for later processing
        return new TaskResult
        {
            Success = true,
            Data = new ResultData
            {
                PostId = $"scheduled_{Guid.NewGuid()}"
            }
        };
    }

    public abstract Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct);

    public virtual async Task<TaskResult> DeletePostAsync(TaskItem task, CancellationToken ct)
    {
        return new TaskResult
        {
            Success = false,
            Error = "Delete not implemented for this platform"
        };
    }

    protected string FormatHashtags(List<string>? hashtags)
    {
        if (hashtags == null || hashtags.Count == 0) return "";
        return string.Join(" ", hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
    }

    protected PostContent OptimizeContent(PostContent content)
    {
        // Override in subclasses for platform-specific optimization
        return content;
    }
}
