using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.VideoGeneration;

/// <summary>
/// Luma AI Dream Machine Video Generation Service
/// https://lumalabs.ai - API documentation: https://docs.lumalabs.ai
///
/// Features:
/// - Text-to-Video generation (Dream Machine)
/// - Image-to-Video generation
/// - Keyframe-based animation
/// - High quality realistic output
/// - 5-second video clips
/// </summary>
public class LumaAIService : IVideoGenerationProvider
{
    private readonly ILogger<LumaAIService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _downloadPath;

    private const string BaseUrl = "https://api.lumalabs.ai/dream-machine/v1";
    private const int DefaultTimeoutMinutes = 15;
    private const int PollIntervalSeconds = 5;

    public string ProviderName => "Luma AI";
    public SocialPlatform Platform => SocialPlatform.LumaAI;

    public LumaAIService(
        ILogger<LumaAIService> logger,
        string apiKey,
        HttpClient? httpClient = null,
        string? downloadPath = null)
    {
        _logger = logger;
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        _downloadPath = downloadPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PostXAgent", "Downloads", "LumaAI");

        Directory.CreateDirectory(_downloadPath);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return false;

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/ping", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Luma AI availability");
            return false;
        }
    }

    public async Task<ProviderCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/credits", ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<LumaCreditsResponse>(content);

                return new ProviderCreditsInfo
                {
                    HasCredits = (data?.CreditBalance ?? 0) > 0,
                    RemainingCredits = data?.CreditBalance,
                    PlanName = data?.SubscriptionLevel,
                    IsUnlimited = data?.SubscriptionLevel?.ToLowerInvariant() == "unlimited"
                };
            }

            return new ProviderCreditsInfo { HasCredits = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Luma AI credits info");
            return new ProviderCreditsInfo { HasCredits = false };
        }
    }

    public async Task<VideoGenerationResult> GenerateFromTextAsync(
        string prompt,
        VideoGenerationConfig config,
        IProgress<VideoGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new VideoGenerationResult
        {
            Provider = Platform,
            StartedAt = DateTime.UtcNow,
            Status = VideoGenerationStatus.Queued
        };

        try
        {
            _logger.LogInformation("Starting Luma AI text-to-video generation: {Prompt}", prompt);
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Initializing",
                Message = "Submitting generation request to Luma AI Dream Machine"
            });

            var request = new LumaGenerationRequest
            {
                Prompt = prompt,
                AspectRatio = ConvertAspectRatio(config.AspectRatio),
                Loop = false, // Default non-looping
                Keyframes = null // Text-to-video doesn't need keyframes
            };

            // Apply provider-specific options
            if (config.ProviderSpecific?.TryGetValue("loop", out var loop) == true)
            {
                request.Loop = Convert.ToBoolean(loop);
            }

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/generations", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to submit generation: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generationResponse = JsonSerializer.Deserialize<LumaGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.Id;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned from Luma AI";
                return result;
            }

            _logger.LogInformation("Luma AI generation started with task ID: {TaskId}", result.TaskId);
            result.Status = VideoGenerationStatus.Processing;

            return await PollForCompletionAsync(result.TaskId, progress, ct);
        }
        catch (OperationCanceledException)
        {
            result.Status = VideoGenerationStatus.Cancelled;
            result.Error = "Generation was cancelled";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Luma AI text-to-video generation");
            result.Success = false;
            result.Status = VideoGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<VideoGenerationResult> GenerateFromImageAsync(
        string imagePathOrUrl,
        string? motionPrompt,
        VideoGenerationConfig config,
        IProgress<VideoGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new VideoGenerationResult
        {
            Provider = Platform,
            StartedAt = DateTime.UtcNow,
            Status = VideoGenerationStatus.Queued
        };

        try
        {
            _logger.LogInformation("Starting Luma AI image-to-video generation");
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Preparing",
                Message = "Preparing image for Luma AI Dream Machine"
            });

            // Handle local file - upload or convert to data URI
            string imageUrl = imagePathOrUrl;
            if (File.Exists(imagePathOrUrl))
            {
                imageUrl = await UploadImageAsync(imagePathOrUrl, ct);
                if (string.IsNullOrEmpty(imageUrl))
                {
                    result.Success = false;
                    result.Status = VideoGenerationStatus.Failed;
                    result.Error = "Failed to upload image";
                    return result;
                }
            }

            // Luma AI uses keyframes for image-to-video
            var request = new LumaGenerationRequest
            {
                Prompt = motionPrompt ?? "Smooth natural motion with cinematic quality",
                AspectRatio = ConvertAspectRatio(config.AspectRatio),
                Loop = false,
                Keyframes = new LumaKeyframes
                {
                    Frame0 = new LumaKeyframe
                    {
                        Type = "image",
                        Url = imageUrl
                    }
                }
            };

            // Optionally add end frame for more control
            if (!string.IsNullOrEmpty(config.SourceVideoUrl))
            {
                request.Keyframes.Frame1 = new LumaKeyframe
                {
                    Type = "image",
                    Url = config.SourceVideoUrl
                };
            }

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/generations", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to submit generation: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generationResponse = JsonSerializer.Deserialize<LumaGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.Id;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned from Luma AI";
                return result;
            }

            _logger.LogInformation("Luma AI image-to-video started with task ID: {TaskId}", result.TaskId);
            return await PollForCompletionAsync(result.TaskId, progress, ct);
        }
        catch (OperationCanceledException)
        {
            result.Status = VideoGenerationStatus.Cancelled;
            result.Error = "Generation was cancelled";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Luma AI image-to-video generation");
            result.Success = false;
            result.Status = VideoGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Extend an existing video (Luma AI specific feature)
    /// </summary>
    public async Task<VideoGenerationResult> ExtendVideoAsync(
        string generationId,
        string? prompt = null,
        bool reverse = false,
        CancellationToken ct = default)
    {
        var result = new VideoGenerationResult
        {
            Provider = Platform,
            StartedAt = DateTime.UtcNow,
            Status = VideoGenerationStatus.Queued
        };

        try
        {
            _logger.LogInformation("Extending Luma AI video: {GenerationId}", generationId);

            var request = new LumaExtendRequest
            {
                Prompt = prompt,
                Keyframes = new LumaKeyframes
                {
                    Frame0 = new LumaKeyframe
                    {
                        Type = "generation",
                        Id = generationId
                    }
                },
                Loop = false
            };

            // If reverse, the source is the end frame
            if (reverse)
            {
                request.Keyframes = new LumaKeyframes
                {
                    Frame1 = new LumaKeyframe
                    {
                        Type = "generation",
                        Id = generationId
                    }
                };
            }

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/generations", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to extend video: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generationResponse = JsonSerializer.Deserialize<LumaGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.Id;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned";
                return result;
            }

            return await PollForCompletionAsync(result.TaskId, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending Luma AI video");
            result.Success = false;
            result.Status = VideoGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<VideoGenerationResult> GetGenerationStatusAsync(
        string taskId,
        CancellationToken ct = default)
    {
        var result = new VideoGenerationResult
        {
            TaskId = taskId,
            Provider = Platform
        };

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/generations/{taskId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to get status: {response.StatusCode}";
                return result;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var statusResponse = JsonSerializer.Deserialize<LumaGenerationStatusResponse>(content);

            result.Status = MapStatus(statusResponse?.State);

            if (result.Status == VideoGenerationStatus.Completed)
            {
                result.Success = true;
                result.VideoUrl = statusResponse?.Assets?.Video;
                result.ThumbnailUrl = statusResponse?.Assets?.Thumbnail;
                result.CompletedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(result.VideoUrl))
                {
                    result.LocalPath = await DownloadVideoAsync(result.VideoUrl, taskId, ct);
                }
            }
            else if (result.Status == VideoGenerationStatus.Failed)
            {
                result.Success = false;
                result.Error = statusResponse?.FailureReason ?? "Generation failed";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Luma AI generation status");
            result.Success = false;
            result.Status = VideoGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<bool> CancelGenerationAsync(string taskId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{BaseUrl}/generations/{taskId}", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Luma AI generation");
            return false;
        }
    }

    public Task<IReadOnlyList<VideoModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = new List<VideoModelInfo>
        {
            new()
            {
                Id = "dream-machine",
                Name = "Dream Machine",
                Description = "Luma AI's flagship video generation model - realistic and cinematic",
                SupportsTextToVideo = true,
                SupportsImageToVideo = true,
                MaxDurationSeconds = 5,
                SupportedAspectRatios = new[] { AspectRatio.Landscape_16_9, AspectRatio.Portrait_9_16, AspectRatio.Square_1_1 },
                CreditsPerGeneration = 30,
                IsFree = false
            },
            new()
            {
                Id = "dream-machine-1.5",
                Name = "Dream Machine 1.5",
                Description = "Enhanced version with better physics and motion consistency",
                SupportsTextToVideo = true,
                SupportsImageToVideo = true,
                MaxDurationSeconds = 5,
                SupportedAspectRatios = new[]
                {
                    AspectRatio.Landscape_16_9,
                    AspectRatio.Portrait_9_16,
                    AspectRatio.Square_1_1,
                    AspectRatio.Classic_4_3,
                    AspectRatio.Ultrawide_21_9
                },
                CreditsPerGeneration = 40,
                IsFree = false
            }
        };

        return Task.FromResult<IReadOnlyList<VideoModelInfo>>(models);
    }

    #region Private Methods

    private async Task<VideoGenerationResult> PollForCompletionAsync(
        string taskId,
        IProgress<VideoGenerationProgress>? progress,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromMinutes(DefaultTimeoutMinutes);
        var endTime = DateTime.UtcNow.Add(timeout);
        var pollCount = 0;

        while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), ct);
            pollCount++;

            var status = await GetGenerationStatusAsync(taskId, ct);

            // Estimate progress based on typical generation time (~2-3 minutes)
            var estimatedProgress = Math.Min(95, pollCount * 5);
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = status.Status == VideoGenerationStatus.Completed ? 100 : estimatedProgress,
                Stage = status.Status.ToString(),
                Message = status.Status switch
                {
                    VideoGenerationStatus.Queued => "Waiting in queue...",
                    VideoGenerationStatus.Processing => "Generating video with Dream Machine...",
                    VideoGenerationStatus.Completed => "Video ready!",
                    _ => "Processing..."
                }
            });

            if (status.Status == VideoGenerationStatus.Completed ||
                status.Status == VideoGenerationStatus.Failed ||
                status.Status == VideoGenerationStatus.Cancelled)
            {
                return status;
            }
        }

        return new VideoGenerationResult
        {
            TaskId = taskId,
            Provider = Platform,
            Success = false,
            Status = VideoGenerationStatus.Timeout,
            Error = $"Generation timed out after {timeout.TotalMinutes} minutes"
        };
    }

    private async Task<string?> UploadImageAsync(string localPath, CancellationToken ct)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var imageBytes = await File.ReadAllBytesAsync(localPath, ct);
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(localPath));
            content.Add(imageContent, "file", Path.GetFileName(localPath));

            var response = await _httpClient.PostAsync($"{BaseUrl}/uploads", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(ct);
                var uploadResponse = JsonSerializer.Deserialize<LumaUploadResponse>(responseContent);
                return uploadResponse?.PresignedUrl;
            }

            _logger.LogWarning("Failed to upload image: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Luma AI");
            return null;
        }
    }

    private async Task<string?> DownloadVideoAsync(string url, string taskId, CancellationToken ct)
    {
        try
        {
            var localPath = Path.Combine(_downloadPath, $"luma_{taskId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                await File.WriteAllBytesAsync(localPath, bytes, ct);
                return localPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading video from Luma AI");
            return null;
        }
    }

    private static string ConvertAspectRatio(AspectRatio ratio) => ratio switch
    {
        AspectRatio.Portrait_9_16 => "9:16",
        AspectRatio.Square_1_1 => "1:1",
        AspectRatio.Classic_4_3 => "4:3",
        AspectRatio.Ultrawide_21_9 => "21:9",
        _ => "16:9"
    };

    private static VideoGenerationStatus MapStatus(string? state) => state?.ToLowerInvariant() switch
    {
        "queued" or "pending" => VideoGenerationStatus.Queued,
        "dreaming" or "processing" => VideoGenerationStatus.Processing,
        "completed" => VideoGenerationStatus.Completed,
        "failed" => VideoGenerationStatus.Failed,
        _ => VideoGenerationStatus.Processing
    };

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
    }

    #endregion

    #region API Models

    private class LumaGenerationRequest
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("aspect_ratio")]
        public string AspectRatio { get; set; } = "16:9";

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }

        [JsonPropertyName("keyframes")]
        public LumaKeyframes? Keyframes { get; set; }
    }

    private class LumaExtendRequest
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("keyframes")]
        public LumaKeyframes? Keyframes { get; set; }

        [JsonPropertyName("loop")]
        public bool Loop { get; set; }
    }

    private class LumaKeyframes
    {
        [JsonPropertyName("frame0")]
        public LumaKeyframe? Frame0 { get; set; }

        [JsonPropertyName("frame1")]
        public LumaKeyframe? Frame1 { get; set; }
    }

    private class LumaKeyframe
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "image"; // "image" or "generation"

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class LumaGenerationResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }
    }

    private class LumaGenerationStatusResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("state")]
        public string? State { get; set; }

        [JsonPropertyName("failure_reason")]
        public string? FailureReason { get; set; }

        [JsonPropertyName("assets")]
        public LumaAssets? Assets { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }

    private class LumaAssets
    {
        [JsonPropertyName("video")]
        public string? Video { get; set; }

        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }
    }

    private class LumaCreditsResponse
    {
        [JsonPropertyName("credit_balance")]
        public int? CreditBalance { get; set; }

        [JsonPropertyName("subscription_level")]
        public string? SubscriptionLevel { get; set; }
    }

    private class LumaUploadResponse
    {
        [JsonPropertyName("presigned_url")]
        public string? PresignedUrl { get; set; }
    }

    #endregion
}
