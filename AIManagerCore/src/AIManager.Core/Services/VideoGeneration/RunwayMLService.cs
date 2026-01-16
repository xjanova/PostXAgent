using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.VideoGeneration;

/// <summary>
/// Runway ML Gen-3 Alpha Video Generation Service
/// https://runwayml.com - API documentation: https://docs.runwayml.com
///
/// Features:
/// - Text-to-Video generation
/// - Image-to-Video generation
/// - High quality cinematic output
/// - 5-10 second video clips
/// </summary>
public class RunwayMLService : IVideoGenerationProvider
{
    private readonly ILogger<RunwayMLService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _downloadPath;

    private const string BaseUrl = "https://api.runwayml.com/v1";
    private const int DefaultTimeoutMinutes = 10;
    private const int PollIntervalSeconds = 5;

    public string ProviderName => "Runway ML";
    public SocialPlatform Platform => SocialPlatform.Runway;

    public RunwayMLService(
        ILogger<RunwayMLService> logger,
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
            "PostXAgent", "Downloads", "Runway");

        Directory.CreateDirectory(_downloadPath);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return false;

        try
        {
            // ตรวจสอบ API key validity
            var response = await _httpClient.GetAsync($"{BaseUrl}/account", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Runway ML availability");
            return false;
        }
    }

    public async Task<ProviderCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/account/credits", ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<RunwayCreditsResponse>(content);

                return new ProviderCreditsInfo
                {
                    HasCredits = (data?.RemainingCredits ?? 0) > 0,
                    RemainingCredits = data?.RemainingCredits,
                    TotalCredits = data?.TotalCredits,
                    PlanName = data?.PlanName,
                    IsUnlimited = data?.IsUnlimited ?? false
                };
            }

            return new ProviderCreditsInfo { HasCredits = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Runway ML credits info");
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
            _logger.LogInformation("Starting Runway ML text-to-video generation: {Prompt}", prompt);
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Initializing",
                Message = "Submitting generation request to Runway ML"
            });

            // สร้าง request payload
            var request = new RunwayGenerationRequest
            {
                PromptText = prompt,
                Model = "gen3a_turbo", // Gen-3 Alpha Turbo
                Duration = Math.Min(config.Duration, 10), // Max 10 seconds
                AspectRatio = ConvertAspectRatio(config.AspectRatio),
                Seed = config.Seed
            };

            // Submit generation task
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
            var generationResponse = JsonSerializer.Deserialize<RunwayGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.Id;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned from Runway ML";
                return result;
            }

            _logger.LogInformation("Runway ML generation started with task ID: {TaskId}", result.TaskId);
            result.Status = VideoGenerationStatus.Processing;

            // Poll for completion
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
            _logger.LogError(ex, "Error in Runway ML text-to-video generation");
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
            _logger.LogInformation("Starting Runway ML image-to-video generation");
            progress?.Report(new VideoGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Preparing",
                Message = "Preparing image for Runway ML"
            });

            // ถ้าเป็น local path ต้อง upload ก่อน หรือแปลงเป็น base64
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

            var request = new RunwayGenerationRequest
            {
                PromptImage = imageUrl,
                PromptText = motionPrompt ?? "Smooth cinematic motion",
                Model = "gen3a_turbo",
                Duration = Math.Min(config.Duration, 10),
                AspectRatio = ConvertAspectRatio(config.AspectRatio),
                Seed = config.Seed
            };

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
            var generationResponse = JsonSerializer.Deserialize<RunwayGenerationResponse>(responseContent);
            result.TaskId = generationResponse?.Id;

            if (string.IsNullOrEmpty(result.TaskId))
            {
                result.Success = false;
                result.Status = VideoGenerationStatus.Failed;
                result.Error = "No task ID returned from Runway ML";
                return result;
            }

            _logger.LogInformation("Runway ML image-to-video started with task ID: {TaskId}", result.TaskId);
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
            _logger.LogError(ex, "Error in Runway ML image-to-video generation");
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
            var statusResponse = JsonSerializer.Deserialize<RunwayStatusResponse>(content);

            result.Status = MapStatus(statusResponse?.Status);
            result.ProgressPercent = statusResponse?.Progress;

            if (result.Status == VideoGenerationStatus.Completed)
            {
                result.Success = true;
                result.VideoUrl = statusResponse?.Output?.FirstOrDefault();
                result.CompletedAt = DateTime.UtcNow;

                // Download video locally
                if (!string.IsNullOrEmpty(result.VideoUrl))
                {
                    result.LocalPath = await DownloadVideoAsync(result.VideoUrl, taskId, ct);
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
            _logger.LogError(ex, "Error getting Runway ML generation status");
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
            _logger.LogError(ex, "Error cancelling Runway ML generation");
            return false;
        }
    }

    public Task<IReadOnlyList<VideoModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default)
    {
        var models = new List<VideoModelInfo>
        {
            new()
            {
                Id = "gen3a_turbo",
                Name = "Gen-3 Alpha Turbo",
                Description = "Fastest generation, great for iterations",
                SupportsTextToVideo = true,
                SupportsImageToVideo = true,
                MaxDurationSeconds = 10,
                SupportedAspectRatios = new[] { AspectRatio.Landscape_16_9, AspectRatio.Portrait_9_16, AspectRatio.Square_1_1 },
                CreditsPerGeneration = 5,
                IsFree = false
            },
            new()
            {
                Id = "gen3a",
                Name = "Gen-3 Alpha",
                Description = "Highest quality cinematic output",
                SupportsTextToVideo = true,
                SupportsImageToVideo = true,
                MaxDurationSeconds = 10,
                SupportedAspectRatios = new[] { AspectRatio.Landscape_16_9, AspectRatio.Portrait_9_16, AspectRatio.Square_1_1 },
                CreditsPerGeneration = 10,
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
            content.Add(imageContent, "file", Path.GetFileName(localPath));

            var response = await _httpClient.PostAsync($"{BaseUrl}/uploads", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync(ct);
                var uploadResponse = JsonSerializer.Deserialize<RunwayUploadResponse>(responseContent);
                return uploadResponse?.Url;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image to Runway ML");
            return null;
        }
    }

    private async Task<string?> DownloadVideoAsync(string url, string taskId, CancellationToken ct)
    {
        try
        {
            var localPath = Path.Combine(_downloadPath, $"runway_{taskId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

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
            _logger.LogError(ex, "Error downloading video from Runway ML");
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

    private static VideoGenerationStatus MapStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "pending" or "queued" => VideoGenerationStatus.Queued,
        "processing" or "running" => VideoGenerationStatus.Processing,
        "completed" or "succeeded" => VideoGenerationStatus.Completed,
        "failed" or "error" => VideoGenerationStatus.Failed,
        "cancelled" => VideoGenerationStatus.Cancelled,
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

    private class RunwayGenerationRequest
    {
        [JsonPropertyName("promptText")]
        public string? PromptText { get; set; }

        [JsonPropertyName("promptImage")]
        public string? PromptImage { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = "gen3a_turbo";

        [JsonPropertyName("duration")]
        public int Duration { get; set; } = 5;

        [JsonPropertyName("aspectRatio")]
        public string AspectRatio { get; set; } = "16:9";

        [JsonPropertyName("seed")]
        public int? Seed { get; set; }
    }

    private class RunwayGenerationResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private class RunwayStatusResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("progress")]
        public int? Progress { get; set; }

        [JsonPropertyName("output")]
        public List<string>? Output { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private class RunwayCreditsResponse
    {
        [JsonPropertyName("remaining_credits")]
        public int? RemainingCredits { get; set; }

        [JsonPropertyName("total_credits")]
        public int? TotalCredits { get; set; }

        [JsonPropertyName("plan_name")]
        public string? PlanName { get; set; }

        [JsonPropertyName("is_unlimited")]
        public bool? IsUnlimited { get; set; }
    }

    private class RunwayUploadResponse
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    #endregion
}
