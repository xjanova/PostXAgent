using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.VideoGeneration;

/// <summary>
/// Pika Labs Video Generation Service
/// https://pika.art - API documentation: https://docs.pika.art
///
/// Features:
/// - Text-to-Video generation
/// - Image-to-Video generation
/// - Video editing and modification
/// - 3-4 second video clips
/// - Creative and stylized output
/// </summary>
public class PikaLabsService : IVideoGenerationProvider
{
    private readonly ILogger<PikaLabsService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _downloadPath;

    private const string BaseUrl = "https://api.pika.art/v1";
    private const int DefaultTimeoutMinutes = 10;
    private const int PollIntervalSeconds = 5;

    public string ProviderName => "Pika Labs";
    public SocialPlatform Platform => SocialPlatform.PikaLabs;

    public PikaLabsService(
        ILogger<PikaLabsService> logger,
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
            "PostXAgent", "Downloads", "PikaLabs");

        Directory.CreateDirectory(_downloadPath);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return false;

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/user", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Pika Labs availability");
            return false;
        }
    }

    public async Task<ProviderCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/user/credits", ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<PikaCreditsResponse>(content);

                return new ProviderCreditsInfo
                {
                    HasCredits = (data?.Credits ?? 0) > 0,
                    RemainingCredits = data?.Credits,
                    DailyLimit = data?.DailyLimit,
                    UsedToday = data?.UsedToday,
                    PlanName = data?.Subscription,
                    IsUnlimited = data?.IsUnlimited ?? false
                };
            }

            return new ProviderCreditsInfo { HasCredits = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Pika Labs credits info");
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
            _logger.LogInformation("Starting Pika Labs text-to-video generation: {Prompt}", prompt);
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Initializing",
                Message = "Submitting generation request to Pika Labs"
            });

            var request = new PikaGenerationRequest
            {
                Prompt = prompt,
                NegativePrompt = config.NegativePrompt,
                AspectRatio = ConvertAspectRatio(config.AspectRatio),
                Style = config.Style,
                Motion = GetMotionLevel(config),
                Seed = config.Seed,
                Options = new PikaOptions
                {
                    Fps = config.Fps > 0 ? config.Fps : 24,
                    GuidanceScale = 12.0 // ค่าเริ่มต้นสำหรับความแม่นยำกับ prompt
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/generate", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to submit generation: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generationResponse = JsonSerializer.Deserialize<PikaGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.JobId;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned from Pika Labs";
                return result;
            }

            _logger.LogInformation("Pika Labs generation started with task ID: {TaskId}", result.TaskId);
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
            _logger.LogError(ex, "Error in Pika Labs text-to-video generation");
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
            _logger.LogInformation("Starting Pika Labs image-to-video generation");
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Preparing",
                Message = "Preparing image for Pika Labs"
            });

            // Upload image if local path
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

            var request = new PikaImageToVideoRequest
            {
                ImageUrl = imageUrl,
                Prompt = motionPrompt ?? "Subtle natural movement",
                NegativePrompt = config.NegativePrompt,
                Motion = GetMotionLevel(config),
                Seed = config.Seed,
                Options = new PikaOptions
                {
                    Fps = config.Fps > 0 ? config.Fps : 24,
                    GuidanceScale = 10.0
                }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/animate", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to submit animation: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generationResponse = JsonSerializer.Deserialize<PikaGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.JobId;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned from Pika Labs";
                return result;
            }

            _logger.LogInformation("Pika Labs animation started with task ID: {TaskId}", result.TaskId);
            return await PollForCompletionAsync(result.TaskId, progress, ct);
        }
        catch (OperationCanceledException)
        {
            result.Status = VideoGenerationStatus.Cancelled;
            result.Error = "Animation was cancelled";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Pika Labs image-to-video generation");
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
            var response = await _httpClient.GetAsync($"{BaseUrl}/job/{taskId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = $"Failed to get status: {response.StatusCode}";
                return result;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var statusResponse = JsonSerializer.Deserialize<PikaJobStatusResponse>(content);

            result.Status = MapStatus(statusResponse?.Status);
            result.ProgressPercent = statusResponse?.Progress;

            if (result.Status == VideoGenerationStatus.Completed)
            {
                result.Success = true;
                result.VideoUrl = statusResponse?.VideoUrl;
                result.ThumbnailUrl = statusResponse?.ThumbnailUrl;
                result.CompletedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(result.VideoUrl))
                {
                    result.LocalPath = await DownloadVideoAsync(result.VideoUrl, taskId, ct);
                }

                // Extract metadata if available
                if (statusResponse?.Metadata != null)
                {
                    result.Metadata = new VideoMetadata
                    {
                        Width = statusResponse.Metadata.Width ?? 0,
                        Height = statusResponse.Metadata.Height ?? 0,
                        Duration = statusResponse.Metadata.Duration ?? 0,
                        Fps = statusResponse.Metadata.Fps ?? 24,
                        Format = "mp4"
                    };
                }
            }
            else if (result.Status == VideoGenerationStatus.Failed)
            {
                result.Success = false;
                result.Error = statusResponse?.Error ?? "Generation failed";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Pika Labs generation status");
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
            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/job/{taskId}/cancel",
                null,
                ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Pika Labs generation");
            return false;
        }
    }

    public Task<IReadOnlyList<VideoModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = new List<VideoModelInfo>
        {
            new()
            {
                Id = "pika-1.0",
                Name = "Pika 1.0",
                Description = "Standard quality video generation",
                SupportsTextToVideo = true,
                SupportsImageToVideo = true,
                MaxDurationSeconds = 4,
                SupportedAspectRatios = new[] { AspectRatio.Landscape_16_9, AspectRatio.Portrait_9_16, AspectRatio.Square_1_1 },
                CreditsPerGeneration = 10,
                IsFree = false
            },
            new()
            {
                Id = "pika-1.5",
                Name = "Pika 1.5",
                Description = "Enhanced quality with better motion",
                SupportsTextToVideo = true,
                SupportsImageToVideo = true,
                MaxDurationSeconds = 4,
                SupportedAspectRatios = new[] { AspectRatio.Landscape_16_9, AspectRatio.Portrait_9_16, AspectRatio.Square_1_1, AspectRatio.Classic_4_3 },
                CreditsPerGeneration = 15,
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

        while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), ct);

            var status = await GetGenerationStatusAsync(taskId, ct);

            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = status.ProgressPercent ?? 0,
                Stage = status.Status.ToString(),
                Message = $"Processing... {status.ProgressPercent}%"
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
            content.Add(imageContent, "image", Path.GetFileName(localPath));

            var response = await _httpClient.PostAsync($"{BaseUrl}/upload", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(ct);
                var uploadResponse = JsonSerializer.Deserialize<PikaUploadResponse>(responseContent);
                return uploadResponse?.Url;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Pika Labs");
            return null;
        }
    }

    private async Task<string?> DownloadVideoAsync(string url, string taskId, CancellationToken ct)
    {
        try
        {
            var localPath = Path.Combine(_downloadPath, $"pika_{taskId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

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
            _logger.LogError(ex, "Error downloading video from Pika Labs");
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

    private static int GetMotionLevel(VideoGenerationConfig config)
    {
        // Pika uses motion level 1-4
        if (config.ProviderSpecific?.TryGetValue("motion", out var motion) == true)
        {
            return Convert.ToInt32(motion);
        }
        return 2; // Default medium motion
    }

    private static VideoGenerationStatus MapStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "pending" or "queued" or "waiting" => VideoGenerationStatus.Queued,
        "processing" or "running" or "generating" => VideoGenerationStatus.Processing,
        "completed" or "finished" or "done" => VideoGenerationStatus.Completed,
        "failed" or "error" => VideoGenerationStatus.Failed,
        "cancelled" or "canceled" => VideoGenerationStatus.Cancelled,
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

    private class PikaGenerationRequest
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("negative_prompt")]
        public string? NegativePrompt { get; set; }

        [JsonPropertyName("aspect_ratio")]
        public string AspectRatio { get; set; } = "16:9";

        [JsonPropertyName("style")]
        public string? Style { get; set; }

        [JsonPropertyName("motion")]
        public int Motion { get; set; } = 2;

        [JsonPropertyName("seed")]
        public int? Seed { get; set; }

        [JsonPropertyName("options")]
        public PikaOptions? Options { get; set; }
    }

    private class PikaImageToVideoRequest
    {
        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("negative_prompt")]
        public string? NegativePrompt { get; set; }

        [JsonPropertyName("motion")]
        public int Motion { get; set; } = 2;

        [JsonPropertyName("seed")]
        public int? Seed { get; set; }

        [JsonPropertyName("options")]
        public PikaOptions? Options { get; set; }
    }

    private class PikaOptions
    {
        [JsonPropertyName("fps")]
        public int Fps { get; set; } = 24;

        [JsonPropertyName("guidance_scale")]
        public double GuidanceScale { get; set; } = 12.0;
    }

    private class PikaGenerationResponse
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private class PikaJobStatusResponse
    {
        [JsonPropertyName("job_id")]
        public string? JobId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("progress")]
        public int? Progress { get; set; }

        [JsonPropertyName("video_url")]
        public string? VideoUrl { get; set; }

        [JsonPropertyName("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("metadata")]
        public PikaVideoMetadata? Metadata { get; set; }
    }

    private class PikaVideoMetadata
    {
        [JsonPropertyName("width")]
        public int? Width { get; set; }

        [JsonPropertyName("height")]
        public int? Height { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("fps")]
        public int? Fps { get; set; }
    }

    private class PikaCreditsResponse
    {
        [JsonPropertyName("credits")]
        public int? Credits { get; set; }

        [JsonPropertyName("daily_limit")]
        public int? DailyLimit { get; set; }

        [JsonPropertyName("used_today")]
        public int? UsedToday { get; set; }

        [JsonPropertyName("subscription")]
        public string? Subscription { get; set; }

        [JsonPropertyName("is_unlimited")]
        public bool? IsUnlimited { get; set; }
    }

    private class PikaUploadResponse
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    #endregion
}
