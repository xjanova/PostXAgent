using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

#region Instagram Worker
/// <summary>
/// Instagram Platform Worker
/// Uses Meta Graph API (same as Facebook)
/// Supports: Feed posts, Stories, Reels
/// </summary>
public class InstagramWorker : BasePlatformWorker
{
    private const string GraphApiUrl = "https://graph.facebook.com/v19.0";

    /// <summary>
    /// Optional file server for serving local files as temporary URLs.
    /// Set this before calling PostContentAsync when media paths are local files.
    /// </summary>
    public LocalFileServerService? FileServer { get; set; }

    public override SocialPlatform Platform => SocialPlatform.Instagram;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            var igUserId = credentials.PlatformUserId;
            var accessToken = credentials.AccessToken;

            // Step 1: Create media container
            var caption = content.Text + "\n\n" + FormatHashtags(content.Hashtags);
            var containerData = new Dictionary<string, string>
            {
                ["caption"] = caption,
                ["access_token"] = accessToken
            };

            // Add image or video URL (convert local paths to temporary URLs)
            if (content.Videos?.Count > 0)
            {
                containerData["media_type"] = "REELS";
                var videoPath = content.Videos.First();
                containerData["video_url"] = ResolveMediaUrl(videoPath);
            }
            else if (content.Images?.Count > 0)
            {
                var imagePath = content.Images.First();
                containerData["image_url"] = ResolveMediaUrl(imagePath);
            }

            var containerResponse = await _httpClient.PostAsync(
                $"{GraphApiUrl}/{igUserId}/media",
                new FormUrlEncodedContent(containerData),
                ct
            );

            var containerResult = await containerResponse.Content.ReadFromJsonAsync<InstagramMediaResponse>(ct);

            if (containerResult?.Id == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = containerResult?.Error?.Message ?? "Failed to create media container",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Step 2: Publish the container
            var publishData = new Dictionary<string, string>
            {
                ["creation_id"] = containerResult.Id,
                ["access_token"] = accessToken
            };

            var publishResponse = await _httpClient.PostAsync(
                $"{GraphApiUrl}/{igUserId}/media_publish",
                new FormUrlEncodedContent(publishData),
                ct
            );

            var publishResult = await publishResponse.Content.ReadFromJsonAsync<InstagramMediaResponse>(ct);

            if (publishResult?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = publishResult.Id,
                        PlatformUrl = $"https://instagram.com/p/{publishResult.Id}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = publishResult?.Error?.Message ?? "Failed to publish",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Resolve a media path to a URL. If the path is already a URL, return as-is.
    /// If it's a local file path, register it with the file server to get a temporary URL.
    /// </summary>
    private string ResolveMediaUrl(string pathOrUrl)
    {
        // Already a URL
        if (pathOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            pathOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return pathOrUrl;
        }

        // Local file path - use file server
        if (FileServer == null)
        {
            _logger.LogWarning("Local file path provided but no FileServer configured. Using path as-is: {Path}", pathOrUrl);
            return pathOrUrl;
        }

        _logger.LogInformation("Converting local file to temporary URL: {Path}", pathOrUrl);
        return FileServer.RegisterFile(pathOrUrl, TimeSpan.FromHours(2));
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var postId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(postId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or post ID" };
            }

            var response = await _httpClient.GetAsync(
                $"{GraphApiUrl}/{postId}/insights?metric=impressions,reach,likes,comments,shares,saved&access_token={credentials.AccessToken}",
                ct
            );

            var data = await response.Content.ReadFromJsonAsync<InstagramInsightsResponse>(ct);

            var metrics = new EngagementMetrics();
            if (data?.Data != null)
            {
                foreach (var item in data.Data)
                {
                    switch (item.Name)
                    {
                        case "likes": metrics.Likes = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "comments": metrics.Comments = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "shares": metrics.Shares = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "saved": metrics.Saves = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "impressions": metrics.Impressions = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "reach": metrics.Reach = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                    }
                }
            }

            return new TaskResult
            {
                Success = true,
                Data = new ResultData { Metrics = metrics },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instagram metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region TikTok Worker
/// <summary>
/// TikTok Platform Worker
/// Uses TikTok Content Posting API
/// </summary>
public class TikTokWorker : BasePlatformWorker
{
    private const string TikTokApiUrl = "https://open.tiktokapis.com/v2";
    private const int ChunkSize = 10 * 1024 * 1024; // 10 MB chunks

    /// <summary>
    /// Optional file server for serving local files as temporary URLs.
    /// Set this before calling PostContentAsync when video paths are local files.
    /// </summary>
    public LocalFileServerService? FileServer { get; set; }

    public override SocialPlatform Platform => SocialPlatform.TikTok;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            // TikTok requires video content - use Content Posting API
            var videoPath = content.Videos?.FirstOrDefault();
            if (string.IsNullOrEmpty(videoPath))
            {
                return new TaskResult { Success = false, Error = "TikTok requires video content" };
            }

            var isLocalFile = !videoPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                              !videoPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            TikTokPostResponse? result;

            if (isLocalFile && File.Exists(videoPath))
            {
                // Local file: use FILE_UPLOAD source type with chunked upload
                result = await PostWithFileUploadAsync(videoPath, content, credentials, ct);
            }
            else
            {
                // Remote URL: use PULL_FROM_URL
                result = await PostWithPullFromUrlAsync(videoPath, content, credentials, ct);
            }

            if (result?.Data?.PublishId != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = result.Data.PublishId,
                        PlatformUrl = $"https://tiktok.com/@{credentials.PlatformUserId}/video/{result.Data.PublishId}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = result?.Error?.Message ?? "Failed to post to TikTok",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TikTok post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Post video using PULL_FROM_URL source type (for remote URLs).
    /// </summary>
    private async Task<TikTokPostResponse?> PostWithPullFromUrlAsync(
        string videoUrl, PostContent content, PlatformCredentials credentials, CancellationToken ct)
    {
        var initRequest = new
        {
            post_info = new
            {
                title = content.Text?.Substring(0, Math.Min(content.Text.Length, 150)) ?? "",
                privacy_level = "PUBLIC_TO_EVERYONE",
                disable_duet = false,
                disable_comment = false,
                disable_stitch = false
            },
            source_info = new
            {
                source = "PULL_FROM_URL",
                video_url = videoUrl
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{TikTokApiUrl}/post/publish/video/init/",
            initRequest,
            ct
        );

        return await response.Content.ReadFromJsonAsync<TikTokPostResponse>(ct);
    }

    /// <summary>
    /// Post video using FILE_UPLOAD source type with chunked upload (for local files).
    /// Flow: init upload -> upload chunks -> publish
    /// </summary>
    private async Task<TikTokPostResponse?> PostWithFileUploadAsync(
        string localPath, PostContent content, PlatformCredentials credentials, CancellationToken ct)
    {
        var fileInfo = new FileInfo(localPath);
        var fileSize = fileInfo.Length;
        var totalChunks = (int)Math.Ceiling((double)fileSize / ChunkSize);

        _logger.LogInformation("TikTok FILE_UPLOAD: {Path} ({Size} bytes, {Chunks} chunks)",
            localPath, fileSize, totalChunks);

        // Step 1: Initialize file upload
        var initRequest = new
        {
            post_info = new
            {
                title = content.Text?.Substring(0, Math.Min(content.Text.Length, 150)) ?? "",
                privacy_level = "PUBLIC_TO_EVERYONE",
                disable_duet = false,
                disable_comment = false,
                disable_stitch = false
            },
            source_info = new
            {
                source = "FILE_UPLOAD",
                video_size = fileSize,
                chunk_size = ChunkSize,
                total_chunk_count = totalChunks
            }
        };

        var initResponse = await _httpClient.PostAsJsonAsync(
            $"{TikTokApiUrl}/post/publish/video/init/",
            initRequest,
            ct
        );

        var initResult = await initResponse.Content.ReadFromJsonAsync<TikTokUploadInitResponse>(ct);

        if (initResult?.Data?.UploadUrl == null)
        {
            _logger.LogError("TikTok FILE_UPLOAD init failed: {Error}", initResult?.Error?.Message);
            return new TikTokPostResponse { Error = initResult?.Error ?? new TikTokError { Message = "Upload init failed" } };
        }

        // Step 2: Upload chunks
        var uploadUrl = initResult.Data.UploadUrl;
        using var fileStream = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var buffer = new byte[ChunkSize];

        for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
        {
            var bytesRead = await fileStream.ReadAsync(buffer, ct);
            var chunkStart = (long)chunkIndex * ChunkSize;
            var chunkEnd = chunkStart + bytesRead - 1;

            using var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
            chunkContent.Headers.Add("Content-Range", $"bytes {chunkStart}-{chunkEnd}/{fileSize}");
            chunkContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");

            var uploadResponse = await _httpClient.PutAsync(uploadUrl, chunkContent, ct);

            if (!uploadResponse.IsSuccessStatusCode)
            {
                var errorBody = await uploadResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("TikTok chunk upload failed (chunk {Index}/{Total}): {Error}",
                    chunkIndex + 1, totalChunks, errorBody);
                return new TikTokPostResponse { Error = new TikTokError { Message = $"Chunk upload failed: {errorBody}" } };
            }

            _logger.LogDebug("TikTok chunk {Index}/{Total} uploaded", chunkIndex + 1, totalChunks);
        }

        // Step 3: Publish (the init response already contains publish_id for FILE_UPLOAD)
        _logger.LogInformation("TikTok FILE_UPLOAD completed, publish_id: {PublishId}", initResult.Data.PublishId);

        return new TikTokPostResponse
        {
            Data = new TikTokPostData { PublishId = initResult.Data.PublishId }
        };
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var videoId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(videoId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or video ID" };
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var request = new { filters = new { video_ids = new[] { videoId } } };
            var response = await _httpClient.PostAsJsonAsync(
                $"{TikTokApiUrl}/video/query/?fields=like_count,comment_count,share_count,view_count",
                request,
                ct
            );

            var data = await response.Content.ReadFromJsonAsync<TikTokVideoQueryResponse>(ct);
            var video = data?.Data?.Videos?.FirstOrDefault();

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Likes = video?.LikeCount ?? 0,
                        Comments = video?.CommentCount ?? 0,
                        Shares = video?.ShareCount ?? 0,
                        Views = video?.ViewCount ?? 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TikTok metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region Twitter/X Worker
/// <summary>
/// Twitter/X Platform Worker
/// Uses Twitter API v2
/// </summary>
public class TwitterWorker : BasePlatformWorker
{
    private const string TwitterApiUrl = "https://api.twitter.com/2";

    public override SocialPlatform Platform => SocialPlatform.Twitter;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            // Prepare tweet text (max 280 chars)
            var tweetText = content.Text + " " + FormatHashtags(content.Hashtags);
            if (tweetText.Length > 280)
            {
                tweetText = tweetText.Substring(0, 277) + "...";
            }

            var tweetData = new { text = tweetText };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.PostAsJsonAsync(
                $"{TwitterApiUrl}/tweets",
                tweetData,
                ct
            );

            var result = await response.Content.ReadFromJsonAsync<TwitterPostResponse>(ct);

            if (result?.Data?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = result.Data.Id,
                        PlatformUrl = $"https://twitter.com/i/web/status/{result.Data.Id}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = result?.Errors?.FirstOrDefault()?.Message ?? "Failed to post tweet",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twitter post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var tweetId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(tweetId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or tweet ID" };
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.GetAsync(
                $"{TwitterApiUrl}/tweets/{tweetId}?tweet.fields=public_metrics",
                ct
            );

            var result = await response.Content.ReadFromJsonAsync<TwitterTweetResponse>(ct);
            var metrics = result?.Data?.PublicMetrics;

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Likes = metrics?.LikeCount ?? 0,
                        Comments = metrics?.ReplyCount ?? 0,
                        Shares = metrics?.RetweetCount ?? 0,
                        Views = metrics?.ImpressionCount ?? 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Twitter metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    public override async Task<TaskResult> DeletePostAsync(TaskItem task, CancellationToken ct)
    {
        try
        {
            var credentials = task.Payload.Credentials;
            var tweetId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(tweetId))
            {
                return new TaskResult { Success = false, Error = "Missing data" };
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.DeleteAsync($"{TwitterApiUrl}/tweets/{tweetId}", ct);
            return new TaskResult { Success = response.IsSuccessStatusCode };
        }
        catch (Exception ex)
        {
            return new TaskResult { Success = false, Error = ex.Message };
        }
    }
}
#endregion

#region LINE Worker
/// <summary>
/// LINE Platform Worker
/// Uses LINE Messaging API for Official Accounts
/// </summary>
public class LineWorker : BasePlatformWorker
{
    private const string LineApiUrl = "https://api.line.me/v2";

    public override SocialPlatform Platform => SocialPlatform.Line;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            // LINE broadcasts to all followers
            var messages = new List<object>();

            // Text message
            if (!string.IsNullOrEmpty(content.Text))
            {
                messages.Add(new { type = "text", text = content.Text + "\n\n" + FormatHashtags(content.Hashtags) });
            }

            // Image message
            if (content.Images?.Any() == true)
            {
                var imageUrl = content.Images.First();
                messages.Add(new
                {
                    type = "image",
                    originalContentUrl = imageUrl,
                    previewImageUrl = imageUrl
                });
            }

            var broadcastData = new { messages };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.PostAsJsonAsync(
                $"{LineApiUrl}/bot/message/broadcast",
                broadcastData,
                ct
            );

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.GetValues("X-Line-Request-Id").FirstOrDefault() ?? Guid.NewGuid().ToString();
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = messageId,
                        PlatformUrl = $"line://msg/{messageId}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            return new TaskResult
            {
                Success = false,
                Error = error,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials?.AccessToken}");

            // Get follower count and message stats
            var response = await _httpClient.GetAsync($"{LineApiUrl}/bot/insight/followers", ct);
            var data = await response.Content.ReadFromJsonAsync<LineInsightResponse>(ct);

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Reach = data?.Followers ?? 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LINE metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region YouTube Worker
/// <summary>
/// YouTube Platform Worker
/// Uses YouTube Data API v3
/// Supports: Video uploads, Community posts
/// </summary>
public class YouTubeWorker : BasePlatformWorker
{
    private const string YouTubeApiUrl = "https://www.googleapis.com/youtube/v3";
    private const string YouTubeUploadUrl = "https://www.googleapis.com/upload/youtube/v3/videos";
    private const int UploadChunkSize = 5 * 1024 * 1024; // 5MB chunks for reliability

    public override SocialPlatform Platform => SocialPlatform.YouTube;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            // For community posts (text-only or image-only, no video)
            if ((content.Videos == null || content.Videos.Count == 0) &&
                (content.Images == null || content.Images.Count == 0))
            {
                // Community posts require YouTube Partner Program - not available via standard API
                return new TaskResult
                {
                    Success = false,
                    Error = "YouTube community posts require the YouTube Partner Program and are not available via the Data API. Please upload a video instead.",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Get the video file path/URL
            var videoSource = content.Videos?.FirstOrDefault() ?? content.Images?.FirstOrDefault();
            if (string.IsNullOrEmpty(videoSource))
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "No video file path or URL provided",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Build video metadata (title max 100 chars for YouTube)
            var title = content.Text?.Length > 100
                ? content.Text.Substring(0, 97) + "..."
                : content.Text ?? "Video";
            var description = content.Text + "\n\n" + FormatHashtags(content.Hashtags);
            var tags = content.Hashtags?.ToArray() ?? Array.Empty<string>();

            var videoMetadata = new
            {
                snippet = new
                {
                    title,
                    description,
                    tags,
                    categoryId = "22" // People & Blogs
                },
                status = new
                {
                    privacyStatus = "public",
                    selfDeclaredMadeForKids = false
                }
            };

            var metadataJson = JsonSerializer.Serialize(videoMetadata);

            // Step 1: Initiate resumable upload session
            _logger.LogInformation("Initiating YouTube resumable upload for: {VideoSource}", videoSource);

            using var initRequest = new HttpRequestMessage(HttpMethod.Post,
                $"{YouTubeUploadUrl}?uploadType=resumable&part=snippet,status");
            initRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", credentials.AccessToken);
            initRequest.Content = new StringContent(metadataJson, Encoding.UTF8, "application/json");

            // Resolve video source to a stream (download URL or open local file)
            Stream videoStream;
            string? tempFilePath = null;

            if (Uri.TryCreate(videoSource, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                _logger.LogInformation("Downloading video from URL before upload: {Url}", videoSource);
                tempFilePath = Path.Combine(Path.GetTempPath(), $"yt_upload_{Guid.NewGuid():N}.mp4");
                using var downloadResponse = await _httpClient.GetAsync(videoSource, HttpCompletionOption.ResponseHeadersRead, ct);
                downloadResponse.EnsureSuccessStatusCode();
                await using (var fileStream = File.Create(tempFilePath))
                {
                    await downloadResponse.Content.CopyToAsync(fileStream, ct);
                }
                videoStream = File.OpenRead(tempFilePath);
            }
            else if (File.Exists(videoSource))
            {
                videoStream = File.OpenRead(videoSource);
            }
            else
            {
                return new TaskResult
                {
                    Success = false,
                    Error = $"Video file not found: {videoSource}",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            try
            {
                var fileSize = videoStream.Length;
                initRequest.Headers.Add("X-Upload-Content-Length", fileSize.ToString());
                initRequest.Headers.Add("X-Upload-Content-Type", "video/*");

                var initResponse = await _httpClient.SendAsync(initRequest, ct);
                if (!initResponse.IsSuccessStatusCode)
                {
                    var errorBody = await initResponse.Content.ReadAsStringAsync(ct);
                    _logger.LogError("YouTube upload init failed: {Status} {Error}",
                        (int)initResponse.StatusCode, errorBody);
                    return new TaskResult
                    {
                        Success = false,
                        Error = $"YouTube upload init failed ({(int)initResponse.StatusCode}): {errorBody}",
                        ProcessingTimeMs = sw.ElapsedMilliseconds
                    };
                }

                // Get the resumable upload URI from Location header
                var uploadUri = initResponse.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(uploadUri))
                {
                    return new TaskResult
                    {
                        Success = false,
                        Error = "YouTube did not return a resumable upload URI",
                        ProcessingTimeMs = sw.ElapsedMilliseconds
                    };
                }

                _logger.LogInformation("Got resumable upload URI, uploading {Size} bytes in {ChunkSize}B chunks",
                    fileSize, UploadChunkSize);

                // Step 2: Upload video in chunks with 308 Resume Incomplete handling
                var videoId = await UploadVideoChunkedAsync(
                    uploadUri, videoStream, fileSize, credentials.AccessToken, ct);

                if (string.IsNullOrEmpty(videoId))
                {
                    return new TaskResult
                    {
                        Success = false,
                        Error = "Failed to complete video upload - no video ID returned",
                        ProcessingTimeMs = sw.ElapsedMilliseconds
                    };
                }

                _logger.LogInformation("YouTube video uploaded successfully: {VideoId}", videoId);

                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = videoId,
                        PlatformUrl = $"https://youtube.com/watch?v={videoId}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }
            finally
            {
                await videoStream.DisposeAsync();
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch { /* best effort temp file cleanup */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    /// <summary>
    /// Upload video to YouTube using chunked resumable upload protocol.
    /// Handles HTTP 308 Resume Incomplete responses for progress tracking.
    /// </summary>
    private async Task<string?> UploadVideoChunkedAsync(
        string uploadUri, Stream videoStream, long totalSize, string accessToken, CancellationToken ct)
    {
        var buffer = new byte[UploadChunkSize];
        long bytesSent = 0;

        while (bytesSent < totalSize)
        {
            ct.ThrowIfCancellationRequested();

            var bytesRemaining = totalSize - bytesSent;
            var currentChunkSize = (int)Math.Min(UploadChunkSize, bytesRemaining);
            var bytesRead = await videoStream.ReadAsync(buffer.AsMemory(0, currentChunkSize), ct);

            if (bytesRead == 0) break;

            using var chunkRequest = new HttpRequestMessage(HttpMethod.Put, uploadUri);
            chunkRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
            chunkContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue("video/*");
            chunkContent.Headers.ContentLength = bytesRead;
            chunkContent.Headers.Add("Content-Range",
                $"bytes {bytesSent}-{bytesSent + bytesRead - 1}/{totalSize}");
            chunkRequest.Content = chunkContent;

            var response = await _httpClient.SendAsync(chunkRequest, ct);
            var statusCode = (int)response.StatusCode;

            if (statusCode == 308)
            {
                // Resume Incomplete - chunk accepted, continue uploading next chunk
                bytesSent += bytesRead;
                var progress = (double)bytesSent / totalSize * 100;
                _logger.LogDebug("YouTube upload progress: {Progress:F1}% ({Sent}/{Total} bytes)",
                    progress, bytesSent, totalSize);
                continue;
            }

            if (response.IsSuccessStatusCode)
            {
                // Upload complete (200 OK) - parse response for video ID
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                var uploadResult = JsonSerializer.Deserialize<YouTubeUploadResponse>(responseBody);
                return uploadResult?.Id;
            }

            // Error during chunk upload
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("YouTube chunk upload failed at {BytesSent}/{Total}: {Status} {Error}",
                bytesSent, totalSize, statusCode, errorBody);
            throw new InvalidOperationException(
                $"YouTube upload failed at byte {bytesSent} ({statusCode}): {errorBody}");
        }

        return null;
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var videoId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(videoId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or video ID" };
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            // Use YouTube Data API v3 to get real video statistics
            var response = await _httpClient.GetAsync(
                $"{YouTubeApiUrl}/videos?part=statistics&id={videoId}",
                ct
            );

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("YouTube metrics API returned {Status}: {Error}",
                    (int)response.StatusCode, errorBody);
                return new TaskResult
                {
                    Success = false,
                    Error = $"YouTube API error ({(int)response.StatusCode}): {errorBody}",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            var data = await response.Content.ReadFromJsonAsync<YouTubeVideoResponse>(ct);
            var stats = data?.Items?.FirstOrDefault()?.Statistics;

            if (stats == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = $"Video not found: {videoId}",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Views = long.TryParse(stats.ViewCount, out var views) ? (int)Math.Min(views, int.MaxValue) : 0,
                        Likes = long.TryParse(stats.LikeCount, out var likes) ? (int)Math.Min(likes, int.MaxValue) : 0,
                        Comments = long.TryParse(stats.CommentCount, out var comments) ? (int)Math.Min(comments, int.MaxValue) : 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region Threads Worker
/// <summary>
/// Threads Platform Worker
/// Uses Threads API (Meta)
/// </summary>
public class ThreadsWorker : BasePlatformWorker
{
    private const string ThreadsApiUrl = "https://graph.threads.net/v1.0";

    public override SocialPlatform Platform => SocialPlatform.Threads;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            var userId = credentials.PlatformUserId;

            // Step 1: Create media container
            var containerData = new Dictionary<string, string>
            {
                ["media_type"] = "TEXT",
                ["text"] = content.Text + "\n\n" + FormatHashtags(content.Hashtags),
                ["access_token"] = credentials.AccessToken
            };

            if (content.Images?.Any() == true)
            {
                containerData["media_type"] = "IMAGE";
                containerData["image_url"] = content.Images.First();
            }

            var containerResponse = await _httpClient.PostAsync(
                $"{ThreadsApiUrl}/{userId}/threads",
                new FormUrlEncodedContent(containerData),
                ct
            );

            var containerResult = await containerResponse.Content.ReadFromJsonAsync<ThreadsMediaResponse>(ct);

            if (containerResult?.Id == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Failed to create Threads container",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Step 2: Publish
            var publishData = new Dictionary<string, string>
            {
                ["creation_id"] = containerResult.Id,
                ["access_token"] = credentials.AccessToken
            };

            var publishResponse = await _httpClient.PostAsync(
                $"{ThreadsApiUrl}/{userId}/threads_publish",
                new FormUrlEncodedContent(publishData),
                ct
            );

            var publishResult = await publishResponse.Content.ReadFromJsonAsync<ThreadsMediaResponse>(ct);

            if (publishResult?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = publishResult.Id,
                        PlatformUrl = $"https://threads.net/t/{publishResult.Id}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = "Failed to publish to Threads",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Threads post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var threadId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(threadId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or thread ID" };
            }

            var response = await _httpClient.GetAsync(
                $"{ThreadsApiUrl}/{threadId}/insights?metric=views,likes,replies,reposts&access_token={credentials.AccessToken}",
                ct
            );

            var data = await response.Content.ReadFromJsonAsync<ThreadsInsightsResponse>(ct);

            var metrics = new EngagementMetrics();
            if (data?.Data != null)
            {
                foreach (var item in data.Data)
                {
                    switch (item.Name)
                    {
                        case "views": metrics.Views = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "likes": metrics.Likes = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "replies": metrics.Comments = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                        case "reposts": metrics.Shares = item.Values?.FirstOrDefault()?.Value ?? 0; break;
                    }
                }
            }

            return new TaskResult
            {
                Success = true,
                Data = new ResultData { Metrics = metrics },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Threads metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region LinkedIn Worker
/// <summary>
/// LinkedIn Platform Worker
/// Uses LinkedIn Marketing API
/// Supports: Personal posts, Company pages
/// </summary>
public class LinkedInWorker : BasePlatformWorker
{
    private const string LinkedInApiUrl = "https://api.linkedin.com/v2";

    public override SocialPlatform Platform => SocialPlatform.LinkedIn;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            var authorUrn = credentials.PageId != null
                ? $"urn:li:organization:{credentials.PageId}"
                : $"urn:li:person:{credentials.PlatformUserId}";

            var postData = new
            {
                author = authorUrn,
                lifecycleState = "PUBLISHED",
                specificContent = new
                {
                    comLinkedInUgcShareContent = new
                    {
                        shareCommentary = new
                        {
                            text = content.Text + "\n\n" + FormatHashtags(content.Hashtags)
                        },
                        shareMediaCategory = content.Images?.Any() == true ? "IMAGE" : "NONE",
                        media = content.Images?.Select(url => new
                        {
                            status = "READY",
                            originalUrl = url
                        }).ToArray()
                    }
                },
                visibility = new
                {
                    comLinkedInUgcMemberNetworkVisibility = "PUBLIC"
                }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");
            _httpClient.DefaultRequestHeaders.Add("X-Restli-Protocol-Version", "2.0.0");

            var response = await _httpClient.PostAsJsonAsync($"{LinkedInApiUrl}/ugcPosts", postData, ct);
            var responseContent = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                // Extract post ID from response headers or body
                var postId = response.Headers.GetValues("X-RestLi-Id").FirstOrDefault() ?? Guid.NewGuid().ToString();
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = postId,
                        PlatformUrl = $"https://linkedin.com/feed/update/{postId}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = responseContent,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LinkedIn post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var postId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(postId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or post ID" };
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.GetAsync(
                $"{LinkedInApiUrl}/socialActions/{postId}?count=1000",
                ct
            );

            var data = await response.Content.ReadFromJsonAsync<LinkedInSocialActionsResponse>(ct);

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Likes = data?.LikesCount ?? 0,
                        Comments = data?.CommentsCount ?? 0,
                        Shares = data?.SharesCount ?? 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LinkedIn metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region Pinterest Worker
/// <summary>
/// Pinterest Platform Worker
/// Uses Pinterest API v5
/// Supports: Pins, Boards
/// </summary>
public class PinterestWorker : BasePlatformWorker
{
    private const string PinterestApiUrl = "https://api.pinterest.com/v5";

    public override SocialPlatform Platform => SocialPlatform.Pinterest;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult { Success = false, Error = "Missing credentials or content" };
            }

            // Pinterest requires an image
            var imageUrl = content.Images?.FirstOrDefault();
            if (string.IsNullOrEmpty(imageUrl))
            {
                return new TaskResult { Success = false, Error = "Pinterest requires an image" };
            }

            var pinData = new
            {
                board_id = credentials.Metadata?.GetValueOrDefault("board_id") ?? credentials.PageId,
                title = content.Text?.Substring(0, Math.Min(content.Text?.Length ?? 0, 100)),
                description = content.Text + "\n\n" + FormatHashtags(content.Hashtags),
                media_source = new
                {
                    source_type = "image_url",
                    url = imageUrl
                },
                link = content.Link
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.PostAsJsonAsync($"{PinterestApiUrl}/pins", pinData, ct);
            var result = await response.Content.ReadFromJsonAsync<PinterestPinResponse>(ct);

            if (result?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = result.Id,
                        PlatformUrl = $"https://pinterest.com/pin/{result.Id}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = result?.Message ?? "Failed to create pin",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pinterest post failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var pinId = task.Payload.PostIds?.FirstOrDefault();

            if (credentials == null || string.IsNullOrEmpty(pinId))
            {
                return new TaskResult { Success = false, Error = "Missing credentials or pin ID" };
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var response = await _httpClient.GetAsync(
                $"{PinterestApiUrl}/pins/{pinId}/analytics?metric_types=IMPRESSION,SAVE,PIN_CLICK,OUTBOUND_CLICK",
                ct
            );

            var data = await response.Content.ReadFromJsonAsync<PinterestAnalyticsResponse>(ct);

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Impressions = data?.All?.Lifetime?.Impression ?? 0,
                        Saves = data?.All?.Lifetime?.Save ?? 0,
                        Clicks = data?.All?.Lifetime?.PinClick ?? 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pinterest metrics failed");
            return new TaskResult { Success = false, Error = ex.Message, ProcessingTimeMs = sw.ElapsedMilliseconds };
        }
    }
}
#endregion

#region API Response Models

// Instagram Models
internal class InstagramMediaResponse
{
    public string? Id { get; set; }
    public InstagramError? Error { get; set; }
}

internal class InstagramError
{
    public string? Message { get; set; }
    public int Code { get; set; }
}

internal class InstagramInsightsResponse
{
    public List<InstagramInsightItem>? Data { get; set; }
}

internal class InstagramInsightItem
{
    public string? Name { get; set; }
    public List<InstagramInsightValue>? Values { get; set; }
}

internal class InstagramInsightValue
{
    public int Value { get; set; }
}

// TikTok Models
internal class TikTokPostResponse
{
    public TikTokPostData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

internal class TikTokPostData
{
    public string? PublishId { get; set; }
}

internal class TikTokError
{
    public string? Message { get; set; }
    public string? Code { get; set; }
}

internal class TikTokUploadInitResponse
{
    public TikTokUploadInitData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

internal class TikTokUploadInitData
{
    public string? PublishId { get; set; }
    public string? UploadUrl { get; set; }
}

internal class TikTokVideoQueryResponse
{
    public TikTokVideoData? Data { get; set; }
}

internal class TikTokVideoData
{
    public List<TikTokVideo>? Videos { get; set; }
}

internal class TikTokVideo
{
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }
    public int ShareCount { get; set; }
    public int ViewCount { get; set; }
}

// Twitter Models
internal class TwitterPostResponse
{
    public TwitterPostData? Data { get; set; }
    public List<TwitterError>? Errors { get; set; }
}

internal class TwitterPostData
{
    public string? Id { get; set; }
    public string? Text { get; set; }
}

internal class TwitterError
{
    public string? Message { get; set; }
}

internal class TwitterTweetResponse
{
    public TwitterTweetData? Data { get; set; }
}

internal class TwitterTweetData
{
    public string? Id { get; set; }
    public TwitterPublicMetrics? PublicMetrics { get; set; }
}

internal class TwitterPublicMetrics
{
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public int RetweetCount { get; set; }
    public int ImpressionCount { get; set; }
}

// LINE Models
internal class LineInsightResponse
{
    public int Followers { get; set; }
}

// YouTube Models
internal class YouTubeVideoResponse
{
    public List<YouTubeVideoItem>? Items { get; set; }
}

internal class YouTubeVideoItem
{
    public YouTubeStatistics? Statistics { get; set; }
}

internal class YouTubeStatistics
{
    public string? ViewCount { get; set; }
    public string? LikeCount { get; set; }
    public string? CommentCount { get; set; }
}

/// <summary>
/// Response from YouTube resumable upload completion (200 OK)
/// </summary>
internal class YouTubeUploadResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string? Id { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("snippet")]
    public YouTubeUploadSnippet? Snippet { get; set; }
}

internal class YouTubeUploadSnippet
{
    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string? Title { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("channelId")]
    public string? ChannelId { get; set; }
}

// Threads Models
internal class ThreadsMediaResponse
{
    public string? Id { get; set; }
}

internal class ThreadsInsightsResponse
{
    public List<ThreadsInsightItem>? Data { get; set; }
}

internal class ThreadsInsightItem
{
    public string? Name { get; set; }
    public List<ThreadsInsightValue>? Values { get; set; }
}

internal class ThreadsInsightValue
{
    public int Value { get; set; }
}

// LinkedIn Models
internal class LinkedInSocialActionsResponse
{
    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }
    public int SharesCount { get; set; }
}

// Pinterest Models
internal class PinterestPinResponse
{
    public string? Id { get; set; }
    public string? Message { get; set; }
}

internal class PinterestAnalyticsResponse
{
    public PinterestAllMetrics? All { get; set; }
}

internal class PinterestAllMetrics
{
    public PinterestLifetimeMetrics? Lifetime { get; set; }
}

internal class PinterestLifetimeMetrics
{
    public int Impression { get; set; }
    public int Save { get; set; }
    public int PinClick { get; set; }
}

#endregion
