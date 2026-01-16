using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.MusicGeneration;

/// <summary>
/// Suno AI Music Generation Service
/// ใช้ unofficial API ของ Suno (ผ่าน session token)
///
/// วิธีการใช้งาน:
/// 1. Login ที่ suno.com ผ่าน browser
/// 2. เปิด DevTools → Application → Cookies
/// 3. Copy ค่า cookie ชื่อ "__client" หรือ session token
/// 4. ใส่ใน configuration
///
/// หรือใช้ร่วมกับ SunoAutomationService ที่จะจัดการ session อัตโนมัติ
/// </summary>
public class SunoApiService : IMusicGenerationProvider
{
    private readonly ILogger<SunoApiService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _downloadPath;
    private string? _sessionToken;
    private DateTime _tokenExpiresAt;

    // Suno API endpoints (unofficial - reverse engineered)
    private const string BaseUrl = "https://studio-api.suno.ai";
    private const string ClerkBaseUrl = "https://clerk.suno.com";

    private const int DefaultTimeoutSeconds = 180;
    private const int PollIntervalSeconds = 5;

    public string ProviderName => "Suno AI";

    // Supported genres
    private static readonly List<string> _genres = new()
    {
        "Pop", "Rock", "Hip Hop", "R&B", "Jazz", "Classical",
        "Electronic", "EDM", "House", "Techno", "Trance",
        "Lo-Fi", "Ambient", "Chillout", "Downtempo",
        "Country", "Folk", "Bluegrass", "Americana",
        "Reggae", "Ska", "Dancehall",
        "Latin", "Salsa", "Bossa Nova", "Reggaeton",
        "K-Pop", "J-Pop", "C-Pop",
        "Thai Pop", "Thai Traditional", "Luk Thung", "Mor Lam",
        "Cinematic", "Orchestral", "Epic", "Trailer",
        "Acoustic", "Singer-Songwriter", "Indie",
        "Metal", "Punk", "Alternative", "Grunge",
        "Soul", "Funk", "Disco", "Gospel",
        "Blues", "Swing", "Big Band"
    };

    // Supported moods
    private static readonly List<string> _moods = new()
    {
        "Happy", "Sad", "Energetic", "Calm", "Romantic",
        "Epic", "Dark", "Uplifting", "Nostalgic", "Peaceful",
        "Motivational", "Melancholic", "Dreamy", "Aggressive",
        "Mysterious", "Playful", "Intense", "Relaxing",
        "Triumphant", "Hopeful", "Bittersweet", "Haunting"
    };

    public SunoApiService(
        ILogger<SunoApiService> logger,
        string? sessionToken = null,
        HttpClient? httpClient = null,
        string? downloadPath = null)
    {
        _logger = logger;
        _sessionToken = sessionToken;
        _httpClient = httpClient ?? new HttpClient();

        _downloadPath = downloadPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PostXAgent", "Downloads", "Suno");

        Directory.CreateDirectory(_downloadPath);

        ConfigureHttpClient();
    }

    /// <summary>
    /// ตั้งค่า session token (จาก cookie หรือ login)
    /// </summary>
    public void SetSessionToken(string token, TimeSpan? expiresIn = null)
    {
        _sessionToken = token;
        _tokenExpiresAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromHours(24));
        ConfigureHttpClient();
        _logger.LogInformation("Suno session token updated, expires at {ExpiresAt}", _tokenExpiresAt);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_sessionToken))
        {
            _logger.LogWarning("Suno session token not set");
            return false;
        }

        if (DateTime.UtcNow >= _tokenExpiresAt)
        {
            _logger.LogWarning("Suno session token expired");
            return false;
        }

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/billing/info/", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check Suno availability");
            return false;
        }
    }

    public async Task<MusicCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default)
    {
        var info = new MusicCreditsInfo();

        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/billing/info/", ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var data = JsonSerializer.Deserialize<SunoBillingInfo>(content);

                if (data != null)
                {
                    info.HasCredits = data.TotalCreditsLeft > 0;
                    info.RemainingCredits = data.TotalCreditsLeft;
                    info.DailyLimit = data.MonthlyLimit;
                    info.UsedToday = data.MonthlyUsage;
                    info.IsPro = data.SubscriptionType?.ToLower() != "free";
                    info.PlanName = data.SubscriptionType;
                    info.IsUnlimited = data.SubscriptionType?.ToLower() == "pro_unlimited";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Suno credits info");
        }

        return info;
    }

    public async Task<MusicGenerationResult> GenerateFromPromptAsync(
        string prompt,
        MusicGenerationOptions options,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new MusicGenerationResult
        {
            StartedAt = DateTime.UtcNow,
            Status = MusicGenerationStatus.Queued
        };

        try
        {
            _logger.LogInformation("Starting Suno music generation: {Prompt}", prompt);

            progress?.Report(new MusicGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Submitting",
                Message = "Sending request to Suno AI..."
            });

            // Build style description
            var styleDescription = BuildStyleDescription(options);

            // Submit generation request
            var request = new SunoGenerateRequest
            {
                Prompt = prompt,
                Tags = styleDescription,
                Title = options.Title ?? "",
                MakeInstrumental = options.Instrumental,
                Model = options.UseLatestModel ? "chirp-v3-5" : "chirp-v3-0",
                WaitAudio = false // ไม่รอ เราจะ poll เอง
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/generate/v2/", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = MusicGenerationStatus.Failed;
                result.Error = $"Failed to submit: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generateResponse = JsonSerializer.Deserialize<SunoGenerateResponse>(responseContent);

            if (generateResponse?.Clips == null || generateResponse.Clips.Count == 0)
            {
                result.Success = false;
                result.Status = MusicGenerationStatus.Failed;
                result.Error = "No clips returned from Suno";
                return result;
            }

            // Store task IDs
            var clipIds = generateResponse.Clips.Select(c => c.Id).ToList();
            result.TaskId = string.Join(",", clipIds);

            _logger.LogInformation("Suno generation started with {Count} clips: {Ids}",
                clipIds.Count, result.TaskId);

            // Poll for completion
            result = await PollForCompletionAsync(clipIds, options, progress, ct);

            // Download songs if successful
            if (result.Success && result.Songs.Count > 0)
            {
                await DownloadSongsAsync(result.Songs, options, progress, ct);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Status = MusicGenerationStatus.Cancelled;
            result.Error = "Generation was cancelled";
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Suno music generation");
            result.Success = false;
            result.Status = MusicGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<MusicGenerationResult> GenerateFromLyricsAsync(
        string lyrics,
        string style,
        string? title = null,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new MusicGenerationResult
        {
            StartedAt = DateTime.UtcNow,
            Status = MusicGenerationStatus.Queued
        };

        try
        {
            _logger.LogInformation("Starting Suno custom generation with lyrics");

            options ??= new MusicGenerationOptions();

            progress?.Report(new MusicGenerationProgress
            {
                PercentComplete = 0,
                Stage = "Submitting",
                Message = "Generating song with custom lyrics..."
            });

            var request = new SunoCustomGenerateRequest
            {
                Prompt = lyrics,
                Tags = style,
                Title = title ?? $"Song_{DateTime.Now:yyyyMMdd_HHmmss}",
                MakeInstrumental = false,
                Model = options.UseLatestModel ? "chirp-v3-5" : "chirp-v3-0",
                WaitAudio = false
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{BaseUrl}/api/custom_generate/", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                result.Success = false;
                result.Status = MusicGenerationStatus.Failed;
                result.Error = $"Failed to submit: {response.StatusCode} - {errorContent}";
                return result;
            }

            var responseContent = await response.Content.ReadAsStringAsync(ct);
            var generateResponse = JsonSerializer.Deserialize<SunoGenerateResponse>(responseContent);

            if (generateResponse?.Clips == null || generateResponse.Clips.Count == 0)
            {
                result.Success = false;
                result.Status = MusicGenerationStatus.Failed;
                result.Error = "No clips returned";
                return result;
            }

            var clipIds = generateResponse.Clips.Select(c => c.Id).ToList();
            result.TaskId = string.Join(",", clipIds);

            result = await PollForCompletionAsync(clipIds, options, progress, ct);

            if (result.Success && result.Songs.Count > 0)
            {
                await DownloadSongsAsync(result.Songs, options, progress, ct);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Suno custom generation");
            result.Success = false;
            result.Status = MusicGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<MusicGenerationResult> GenerateInstrumentalAsync(
        string description,
        string style,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        options ??= new MusicGenerationOptions();
        options.Instrumental = true;
        options.Style = style;

        return await GenerateFromPromptAsync(description, options, progress, ct);
    }

    public async Task<MusicGenerationResult> GetGenerationStatusAsync(
        string taskId,
        CancellationToken ct = default)
    {
        var result = new MusicGenerationResult
        {
            TaskId = taskId
        };

        try
        {
            var clipIds = taskId.Split(',').ToList();
            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/api/feed/?ids={string.Join(",", clipIds)}", ct);

            if (!response.IsSuccessStatusCode)
            {
                result.Success = false;
                result.Status = MusicGenerationStatus.Failed;
                result.Error = $"Failed to get status: {response.StatusCode}";
                return result;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var clips = JsonSerializer.Deserialize<List<SunoClip>>(content);

            if (clips == null || clips.Count == 0)
            {
                result.Status = MusicGenerationStatus.Processing;
                return result;
            }

            // Check if all clips are complete
            var allComplete = clips.All(c =>
                c.Status == "complete" || c.Status == "error");

            var anyError = clips.Any(c => c.Status == "error");

            if (allComplete)
            {
                if (anyError && clips.All(c => c.Status == "error"))
                {
                    result.Success = false;
                    result.Status = MusicGenerationStatus.Failed;
                    result.Error = "All songs failed to generate";
                }
                else
                {
                    result.Success = true;
                    result.Status = MusicGenerationStatus.Completed;
                    result.CompletedAt = DateTime.UtcNow;

                    foreach (var clip in clips.Where(c => c.Status == "complete"))
                    {
                        result.Songs.Add(new GeneratedSong
                        {
                            Id = clip.Id,
                            Title = clip.Title,
                            Style = clip.Metadata?.Tags,
                            AudioUrl = clip.AudioUrl,
                            StreamUrl = clip.AudioUrl,
                            ImageUrl = clip.ImageUrl,
                            VideoUrl = clip.VideoUrl,
                            Lyrics = clip.Metadata?.Prompt,
                            DurationSeconds = clip.Metadata?.Duration ?? 0,
                            ModelName = clip.ModelName,
                            CreatedAt = clip.CreatedAt ?? DateTime.UtcNow
                        });
                    }
                }
            }
            else
            {
                // Still processing
                var streaming = clips.Any(c => c.Status == "streaming");
                result.Status = streaming ? MusicGenerationStatus.Streaming : MusicGenerationStatus.Processing;

                // Calculate progress
                var completedCount = clips.Count(c => c.Status == "complete");
                result.ProgressPercent = (completedCount * 100) / clips.Count;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting generation status");
            result.Status = MusicGenerationStatus.Failed;
            result.Error = ex.Message;
            return result;
        }
    }

    public async Task<string?> DownloadSongAsync(
        string songId,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            // Get song info first
            var response = await _httpClient.GetAsync(
                $"{BaseUrl}/api/feed/?ids={songId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get song info for download: {SongId}", songId);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var clips = JsonSerializer.Deserialize<List<SunoClip>>(content);
            var clip = clips?.FirstOrDefault();

            if (clip == null || string.IsNullOrEmpty(clip.AudioUrl))
            {
                _logger.LogWarning("No audio URL for song: {SongId}", songId);
                return null;
            }

            // Download the audio
            var audioResponse = await _httpClient.GetAsync(clip.AudioUrl, ct);

            if (!audioResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download audio: {StatusCode}", audioResponse.StatusCode);
                return null;
            }

            // Determine filename
            var title = clip.Title ?? songId;
            var safeTitle = SanitizeFileName(title);
            var extension = clip.AudioUrl.Contains(".mp3") ? ".mp3" : ".wav";
            var filename = $"{safeTitle}_{songId[..8]}{extension}";
            var localPath = Path.Combine(outputPath ?? _downloadPath, filename);

            // Save file
            var bytes = await audioResponse.Content.ReadAsByteArrayAsync(ct);
            await File.WriteAllBytesAsync(localPath, bytes, ct);

            _logger.LogInformation("Downloaded song to: {Path}", localPath);
            return localPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading song: {SongId}", songId);
            return null;
        }
    }

    public IReadOnlyList<string> GetSupportedGenres() => _genres;
    public IReadOnlyList<string> GetSupportedMoods() => _moods;

    #region Private Methods

    private void ConfigureHttpClient()
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        if (!string.IsNullOrEmpty(_sessionToken))
        {
            // Suno uses bearer token from Clerk
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _sessionToken);
        }
    }

    private string BuildStyleDescription(MusicGenerationOptions options)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(options.Genre))
            parts.Add(options.Genre);

        if (!string.IsNullOrEmpty(options.Mood))
            parts.Add(options.Mood);

        if (!string.IsNullOrEmpty(options.Style))
            parts.Add(options.Style);

        if (options.Instrumental)
            parts.Add("instrumental");

        return string.Join(", ", parts);
    }

    private async Task<MusicGenerationResult> PollForCompletionAsync(
        List<string?> clipIds,
        MusicGenerationOptions options,
        IProgress<MusicGenerationProgress>? progress,
        CancellationToken ct)
    {
        var timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        var endTime = DateTime.UtcNow.Add(timeout);
        var taskId = string.Join(",", clipIds.Where(id => id != null));

        while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), ct);

            var status = await GetGenerationStatusAsync(taskId, ct);

            progress?.Report(new MusicGenerationProgress
            {
                PercentComplete = status.ProgressPercent ?? 0,
                Stage = status.Status.ToString(),
                Message = status.Status switch
                {
                    MusicGenerationStatus.Queued => "Waiting in queue...",
                    MusicGenerationStatus.Processing => "Generating music...",
                    MusicGenerationStatus.Streaming => "Streaming audio...",
                    MusicGenerationStatus.Completed => "Complete!",
                    _ => "Processing..."
                },
                CurrentSong = status.Songs.Count,
                TotalSongs = clipIds.Count
            });

            if (status.Status == MusicGenerationStatus.Completed ||
                status.Status == MusicGenerationStatus.Failed ||
                status.Status == MusicGenerationStatus.Cancelled)
            {
                return status;
            }
        }

        return new MusicGenerationResult
        {
            TaskId = taskId,
            Success = false,
            Status = MusicGenerationStatus.Timeout,
            Error = $"Generation timed out after {timeout.TotalSeconds} seconds"
        };
    }

    private async Task DownloadSongsAsync(
        List<GeneratedSong> songs,
        MusicGenerationOptions options,
        IProgress<MusicGenerationProgress>? progress,
        CancellationToken ct)
    {
        var outputPath = options.OutputPath ?? _downloadPath;

        for (int i = 0; i < songs.Count; i++)
        {
            var song = songs[i];

            if (!options.DownloadAll && i > 0)
                break;

            progress?.Report(new MusicGenerationProgress
            {
                PercentComplete = 90 + (i * 10 / songs.Count),
                Stage = "Downloading",
                Message = $"Downloading song {i + 1}/{songs.Count}...",
                CurrentSong = i + 1,
                TotalSongs = songs.Count
            });

            if (!string.IsNullOrEmpty(song.Id))
            {
                var localPath = await DownloadSongAsync(song.Id, outputPath, ct);
                if (!string.IsNullOrEmpty(localPath))
                {
                    song.LocalPath = localPath;
                }
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }

    #endregion

    #region API Models

    private class SunoGenerateRequest
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("make_instrumental")]
        public bool MakeInstrumental { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = "chirp-v3-5";

        [JsonPropertyName("wait_audio")]
        public bool WaitAudio { get; set; }
    }

    private class SunoCustomGenerateRequest
    {
        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = "";

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("make_instrumental")]
        public bool MakeInstrumental { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; } = "chirp-v3-5";

        [JsonPropertyName("wait_audio")]
        public bool WaitAudio { get; set; }
    }

    private class SunoGenerateResponse
    {
        [JsonPropertyName("clips")]
        public List<SunoClip>? Clips { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private class SunoClip
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("audio_url")]
        public string? AudioUrl { get; set; }

        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("video_url")]
        public string? VideoUrl { get; set; }

        [JsonPropertyName("model_name")]
        public string? ModelName { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("metadata")]
        public SunoClipMetadata? Metadata { get; set; }
    }

    private class SunoClipMetadata
    {
        [JsonPropertyName("prompt")]
        public string? Prompt { get; set; }

        [JsonPropertyName("tags")]
        public string? Tags { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }
    }

    private class SunoBillingInfo
    {
        [JsonPropertyName("total_credits_left")]
        public int TotalCreditsLeft { get; set; }

        [JsonPropertyName("monthly_limit")]
        public int MonthlyLimit { get; set; }

        [JsonPropertyName("monthly_usage")]
        public int MonthlyUsage { get; set; }

        [JsonPropertyName("subscription_type")]
        public string? SubscriptionType { get; set; }
    }

    #endregion
}
