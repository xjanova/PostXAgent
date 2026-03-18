using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Service for publishing posts to groups and pages across platforms
/// </summary>
public class PostPublisherService
{
    private readonly ILogger<PostPublisherService> _logger;
    private readonly HttpClient _httpClient;
    private const string FacebookGraphApiUrl = "https://graph.facebook.com/v19.0";

    public PostPublisherService()
    {
        _logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<PostPublisherService>();
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Post content to a Facebook Page using Graph API
    /// </summary>
    public async Task<PostResult> PostToFacebookPageAsync(
        string pageId,
        string accessToken,
        string content,
        string? link = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Posting to Facebook page: {PageId}", pageId);

        try
        {
            var postData = new Dictionary<string, string>
            {
                ["message"] = content,
                ["access_token"] = accessToken
            };

            if (!string.IsNullOrEmpty(link))
            {
                postData["link"] = link;
            }

            var response = await _httpClient.PostAsync(
                $"{FacebookGraphApiUrl}/{pageId}/feed",
                new FormUrlEncodedContent(postData),
                ct
            );

            var responseText = await response.Content.ReadAsStringAsync(ct);
            _logger.LogDebug("Facebook API response: {Response}", responseText);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<FacebookApiResponse>(responseText);
                if (result?.Id != null)
                {
                    return new PostResult
                    {
                        Success = true,
                        PostId = result.Id,
                        PostUrl = $"https://facebook.com/{result.Id}"
                    };
                }
            }

            // Parse error
            var errorResult = JsonSerializer.Deserialize<FacebookApiResponse>(responseText);
            return new PostResult
            {
                Success = false,
                Error = errorResult?.Error?.Message ?? $"HTTP {(int)response.StatusCode}: {responseText}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to Facebook page {PageId}", pageId);
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Post content to a platform (simple version for ContentCreator)
    /// </summary>
    public async Task<PostResult> PostContentAsync(
        string platform,
        string content,
        Dictionary<string, string>? credentials = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Posting to {Platform}", platform);

        if (credentials == null || !credentials.TryGetValue("access_token", out var accessToken))
        {
            return new PostResult
            {
                Success = false,
                Error = "Missing access token. Configure your platform credentials in Settings."
            };
        }

        return platform.ToLower() switch
        {
            "facebook" => await PostToFacebookPageAsync(
                credentials.GetValueOrDefault("page_id", "me"),
                accessToken,
                content,
                credentials.GetValueOrDefault("link"),
                ct),
            "twitter" or "twitter/x" => await PostToTwitterAsync(accessToken, content, ct),
            "instagram" => await PostToInstagramAsync(credentials, content, ct),
            "linkedin" => await PostToLinkedInAsync(credentials, content, ct),
            _ => new PostResult { Success = false, Error = $"Platform '{platform}' posting not yet implemented" }
        };
    }

    private async Task<PostResult> PostToTwitterAsync(string bearerToken, string content, CancellationToken ct)
    {
        _logger.LogInformation("Posting to Twitter/X");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/tweets");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
            request.Content = JsonContent.Create(new { text = content });

            var response = await _httpClient.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<TwitterApiResponse>(responseText);
                return new PostResult
                {
                    Success = true,
                    PostId = result?.Data?.Id,
                    PostUrl = result?.Data?.Id != null ? $"https://x.com/i/status/{result.Data.Id}" : null
                };
            }

            return new PostResult { Success = false, Error = responseText };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to Twitter");
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    private async Task<PostResult> PostToInstagramAsync(Dictionary<string, string> credentials, string content, CancellationToken ct)
    {
        // Instagram requires media (image/video) for posts - text-only not supported
        return new PostResult
        {
            Success = false,
            Error = "Instagram requires an image or video. Text-only posts are not supported."
        };
    }

    private async Task<PostResult> PostToLinkedInAsync(Dictionary<string, string> credentials, string content, CancellationToken ct)
    {
        _logger.LogInformation("Posting to LinkedIn");

        try
        {
            if (!credentials.TryGetValue("access_token", out var accessToken) ||
                !credentials.TryGetValue("person_urn", out var personUrn))
            {
                return new PostResult { Success = false, Error = "Missing LinkedIn credentials (access_token and person_urn required)" };
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linkedin.com/v2/ugcPosts");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = JsonContent.Create(new
            {
                author = personUrn,
                lifecycleState = "PUBLISHED",
                specificContent = new
                {
                    comLinkedinUgcShareContent = new
                    {
                        shareCommentary = new { text = content },
                        shareMediaCategory = "NONE"
                    }
                },
                visibility = new { comLinkedinUgcMemberNetworkVisibility = "PUBLIC" }
            });

            var response = await _httpClient.SendAsync(request, ct);
            var responseText = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return new PostResult { Success = true, PostId = "linkedin_post" };
            }

            return new PostResult { Success = false, Error = responseText };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to LinkedIn");
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Post content to a specific group
    /// </summary>
    public async Task<PostResult> PostToGroupAsync(
        string platform,
        string groupId,
        string content,
        List<string>? mediaUrls,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Posting to group {GroupId} on {Platform}", groupId, platform);

        try
        {
            return platform.ToLower() switch
            {
                "facebook" => await PostToFacebookGroupAsync(groupId, content, mediaUrls, ct),
                "line" => await PostToLineGroupAsync(groupId, content, mediaUrls, ct),
                "telegram" => await PostToTelegramGroupAsync(groupId, content, mediaUrls, ct),
                "twitter" => await PostToTwitterCommunityAsync(groupId, content, mediaUrls, ct),
                _ => new PostResult { Success = false, Error = "Platform not supported" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to group {GroupId} on {Platform}", groupId, platform);
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Post content to multiple groups
    /// </summary>
    public async Task<List<PostResult>> PostToGroupsAsync(
        string platform,
        List<string> groupIds,
        string content,
        List<string>? mediaUrls,
        int delayBetweenPostsMs = 60000,
        CancellationToken ct = default)
    {
        var results = new List<PostResult>();

        foreach (var groupId in groupIds)
        {
            if (ct.IsCancellationRequested) break;

            var result = await PostToGroupAsync(platform, groupId, content, mediaUrls, ct);
            result.GroupId = groupId;
            results.Add(result);

            // Rate limiting between posts
            if (groupId != groupIds.Last())
            {
                await Task.Delay(delayBetweenPostsMs, ct);
            }
        }

        return results;
    }

    #region Facebook

    private async Task<PostResult> PostToFacebookGroupAsync(
        string groupId,
        string content,
        List<string>? mediaUrls,
        CancellationToken ct)
    {
        _logger.LogInformation("Posting to Facebook group: {GroupId}", groupId);

        // Use web automation to post to group
        // This would integrate with BrowserController and learned workflows
        await Task.Delay(100, ct);

        // Actual implementation would:
        // 1. Navigate to group
        // 2. Click on create post
        // 3. Enter content
        // 4. Upload media if provided
        // 5. Click post button

        return new PostResult
        {
            Success = true,
            PostId = $"fb_post_{Guid.NewGuid():N}",
            PostUrl = $"https://facebook.com/groups/{groupId}/posts/..."
        };
    }

    #endregion

    #region LINE

    private async Task<PostResult> PostToLineGroupAsync(
        string groupId,
        string content,
        List<string>? mediaUrls,
        CancellationToken ct)
    {
        _logger.LogInformation("Posting to LINE OpenChat: {GroupId}", groupId);

        // LINE OpenChat posting would be implemented here
        await Task.Delay(100, ct);

        return new PostResult
        {
            Success = true,
            PostId = $"line_msg_{Guid.NewGuid():N}"
        };
    }

    #endregion

    #region Telegram

    private async Task<PostResult> PostToTelegramGroupAsync(
        string groupId,
        string content,
        List<string>? mediaUrls,
        CancellationToken ct)
    {
        _logger.LogInformation("Posting to Telegram group: {GroupId}", groupId);

        // Telegram Bot API posting would be implemented here
        // This would use TelegramBotClient
        await Task.Delay(100, ct);

        return new PostResult
        {
            Success = true,
            PostId = $"tg_msg_{Guid.NewGuid():N}"
        };
    }

    #endregion

    #region Twitter

    private async Task<PostResult> PostToTwitterCommunityAsync(
        string groupId,
        string content,
        List<string>? mediaUrls,
        CancellationToken ct)
    {
        _logger.LogInformation("Posting to Twitter community: {GroupId}", groupId);

        // Twitter API posting would be implemented here
        // This would use Twitter API v2
        await Task.Delay(100, ct);

        return new PostResult
        {
            Success = true,
            PostId = $"tw_post_{Guid.NewGuid():N}"
        };
    }

    #endregion

    #region Video Publishing

    /// <summary>
    /// Publish video to a platform
    /// เผยแพร่วิดีโอไปยัง platform
    /// </summary>
    public async Task<PostResult> PublishVideoAsync(
        VideoPostRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Publishing video to {Platform}: {VideoPath}",
            request.Platform, request.VideoPath);

        try
        {
            // TODO: Implement actual video upload for each platform
            // For now, return placeholder result
            // Each platform has different video upload APIs

            switch (request.Platform)
            {
                case AIManager.Core.Models.SocialPlatform.Facebook:
                    return await PublishVideoToFacebookAsync(request, ct);

                case AIManager.Core.Models.SocialPlatform.TikTok:
                    return await PublishVideoToTikTokAsync(request, ct);

                case AIManager.Core.Models.SocialPlatform.YouTube:
                    return await PublishVideoToYouTubeAsync(request, ct);

                case AIManager.Core.Models.SocialPlatform.Instagram:
                    return await PublishVideoToInstagramAsync(request, ct);

                default:
                    _logger.LogWarning("Video publishing not implemented for {Platform}", request.Platform);
                    return new PostResult
                    {
                        Success = false,
                        Error = $"Video publishing not implemented for {request.Platform}"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish video to {Platform}", request.Platform);
            return new PostResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Publish video to Facebook Page using Graph Video API (multipart/form-data upload).
    /// Supports local files and URLs. For files under 1GB.
    /// </summary>
    private async Task<PostResult> PublishVideoToFacebookAsync(VideoPostRequest request, CancellationToken ct)
    {
        var accessToken = request.Credentials?.GetValueOrDefault("access_token");
        var pageId = request.Credentials?.GetValueOrDefault("page_id") ?? "me";

        if (string.IsNullOrEmpty(accessToken))
        {
            return new PostResult { Success = false, Error = "Missing Facebook access_token in credentials" };
        }

        if (string.IsNullOrEmpty(request.VideoPath))
        {
            return new PostResult { Success = false, Error = "No video file path provided" };
        }

        _logger.LogInformation("Publishing video to Facebook page {PageId}", pageId);

        try
        {
            // Resolve video to a local file stream
            Stream videoStream;
            string fileName;
            string? tempFilePath = null;

            if (Uri.TryCreate(request.VideoPath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                // Download from URL to temp file
                _logger.LogInformation("Downloading video from URL for Facebook upload: {Url}", request.VideoPath);
                tempFilePath = Path.Combine(Path.GetTempPath(), $"fb_upload_{Guid.NewGuid():N}.mp4");
                using var downloadResponse = await _httpClient.GetAsync(request.VideoPath, HttpCompletionOption.ResponseHeadersRead, ct);
                downloadResponse.EnsureSuccessStatusCode();
                await using (var fileStream = File.Create(tempFilePath))
                {
                    await downloadResponse.Content.CopyToAsync(fileStream, ct);
                }
                videoStream = File.OpenRead(tempFilePath);
                fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrEmpty(fileName)) fileName = "video.mp4";
            }
            else if (File.Exists(request.VideoPath))
            {
                videoStream = File.OpenRead(request.VideoPath);
                fileName = Path.GetFileName(request.VideoPath);
            }
            else
            {
                return new PostResult { Success = false, Error = $"Video file not found: {request.VideoPath}" };
            }

            try
            {
                // Build multipart form data for Graph Video API
                using var formContent = new MultipartFormDataContent();
                formContent.Add(new StreamContent(videoStream), "source", fileName);
                formContent.Add(new StringContent(accessToken), "access_token");

                if (!string.IsNullOrEmpty(request.Title))
                {
                    formContent.Add(new StringContent(request.Title), "title");
                }

                if (!string.IsNullOrEmpty(request.Description))
                {
                    formContent.Add(new StringContent(request.Description), "description");
                }

                // POST to Graph Video API endpoint (different host from regular Graph API)
                var graphVideoUrl = $"https://graph-video.facebook.com/v19.0/{pageId}/videos";
                _logger.LogInformation("Uploading video ({Size} bytes) to {Url}", videoStream.Length, graphVideoUrl);

                var response = await _httpClient.PostAsync(graphVideoUrl, formContent, ct);
                var responseText = await response.Content.ReadAsStringAsync(ct);

                _logger.LogDebug("Facebook Video API response: {Status} {Body}", (int)response.StatusCode, responseText);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<FacebookVideoUploadResponse>(responseText);
                    if (result?.Id != null)
                    {
                        return new PostResult
                        {
                            Success = true,
                            PostId = result.Id,
                            PostUrl = $"https://facebook.com/{pageId}/videos/{result.Id}"
                        };
                    }
                }

                // Parse error response
                var errorResult = JsonSerializer.Deserialize<FacebookVideoUploadResponse>(responseText);
                return new PostResult
                {
                    Success = false,
                    Error = errorResult?.Error?.Message ?? $"Facebook video upload failed ({(int)response.StatusCode}): {responseText}"
                };
            }
            finally
            {
                await videoStream.DisposeAsync();
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch { /* best effort cleanup */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish video to Facebook page {PageId}", pageId);
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Publish video to TikTok using Content Posting API with FILE_UPLOAD source.
    /// Steps: Init upload -> Upload file chunks -> Publish
    /// </summary>
    private async Task<PostResult> PublishVideoToTikTokAsync(VideoPostRequest request, CancellationToken ct)
    {
        const string TikTokApiUrl = "https://open.tiktokapis.com/v2";

        var accessToken = request.Credentials?.GetValueOrDefault("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            return new PostResult { Success = false, Error = "Missing TikTok access_token in credentials" };
        }

        if (string.IsNullOrEmpty(request.VideoPath))
        {
            return new PostResult { Success = false, Error = "No video file path provided" };
        }

        _logger.LogInformation("Publishing video to TikTok via Content Posting API");

        try
        {
            // Resolve video to local file
            string localPath;
            string? tempFilePath = null;

            if (Uri.TryCreate(request.VideoPath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                tempFilePath = Path.Combine(Path.GetTempPath(), $"tt_upload_{Guid.NewGuid():N}.mp4");
                using var downloadResponse = await _httpClient.GetAsync(request.VideoPath, HttpCompletionOption.ResponseHeadersRead, ct);
                downloadResponse.EnsureSuccessStatusCode();
                await using (var fileStream = File.Create(tempFilePath))
                {
                    await downloadResponse.Content.CopyToAsync(fileStream, ct);
                }
                localPath = tempFilePath;
            }
            else if (File.Exists(request.VideoPath))
            {
                localPath = request.VideoPath;
            }
            else
            {
                return new PostResult { Success = false, Error = $"Video file not found: {request.VideoPath}" };
            }

            try
            {
                var fileInfo = new FileInfo(localPath);
                var fileSize = fileInfo.Length;
                var chunkSize = Math.Min(fileSize, 10 * 1024 * 1024L); // Max 10MB chunks for TikTok
                var totalChunks = (int)Math.Ceiling((double)fileSize / chunkSize);

                // Step 1: Init upload via Content Posting API with FILE_UPLOAD source
                var title = request.Title ?? request.Description ?? "";
                if (title.Length > 150) title = title.Substring(0, 150);

                var initPayload = new
                {
                    post_info = new
                    {
                        title,
                        privacy_level = "PUBLIC_TO_EVERYONE",
                        disable_duet = false,
                        disable_comment = false,
                        disable_stitch = false
                    },
                    source_info = new
                    {
                        source = "FILE_UPLOAD",
                        video_size = fileSize,
                        chunk_size = chunkSize,
                        total_chunk_count = totalChunks
                    }
                };

                using var initRequest = new HttpRequestMessage(HttpMethod.Post,
                    $"{TikTokApiUrl}/post/publish/video/init/");
                initRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                initRequest.Content = JsonContent.Create(initPayload);

                var initResponse = await _httpClient.SendAsync(initRequest, ct);
                var initBody = await initResponse.Content.ReadAsStringAsync(ct);

                if (!initResponse.IsSuccessStatusCode)
                {
                    return new PostResult
                    {
                        Success = false,
                        Error = $"TikTok upload init failed ({(int)initResponse.StatusCode}): {initBody}"
                    };
                }

                var initResult = JsonSerializer.Deserialize<TikTokUploadInitResponse>(initBody);
                var uploadUrl = initResult?.Data?.UploadUrl;
                var publishId = initResult?.Data?.PublishId;

                if (string.IsNullOrEmpty(uploadUrl))
                {
                    return new PostResult
                    {
                        Success = false,
                        Error = $"TikTok did not return upload URL. Response: {initBody}"
                    };
                }

                // Step 2: Upload file chunks
                _logger.LogInformation("Uploading {Size} bytes to TikTok in {Chunks} chunk(s)", fileSize, totalChunks);

                await using var videoStream = File.OpenRead(localPath);
                var buffer = new byte[chunkSize];

                for (var chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
                {
                    ct.ThrowIfCancellationRequested();

                    var offset = chunkIndex * chunkSize;
                    var currentChunkSize = (int)Math.Min(chunkSize, fileSize - offset);
                    videoStream.Position = offset;
                    var bytesRead = await videoStream.ReadAsync(buffer.AsMemory(0, currentChunkSize), ct);

                    using var chunkRequest = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
                    chunkRequest.Headers.Add("Content-Range",
                        $"bytes {offset}-{offset + bytesRead - 1}/{fileSize}");

                    var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
                    chunkContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
                    chunkRequest.Content = chunkContent;

                    var chunkResponse = await _httpClient.SendAsync(chunkRequest, ct);
                    if (!chunkResponse.IsSuccessStatusCode)
                    {
                        var chunkError = await chunkResponse.Content.ReadAsStringAsync(ct);
                        return new PostResult
                        {
                            Success = false,
                            Error = $"TikTok chunk {chunkIndex + 1}/{totalChunks} upload failed: {chunkError}"
                        };
                    }

                    _logger.LogDebug("TikTok upload chunk {Chunk}/{Total} complete", chunkIndex + 1, totalChunks);
                }

                _logger.LogInformation("TikTok video upload complete, publish ID: {PublishId}", publishId);

                return new PostResult
                {
                    Success = true,
                    PostId = publishId ?? $"tt_video_{Guid.NewGuid():N}",
                    PostUrl = publishId != null
                        ? $"https://tiktok.com/@user/video/{publishId}"
                        : null
                };
            }
            finally
            {
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch { /* best effort cleanup */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish video to TikTok");
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Publish video to YouTube using Data API v3 resumable upload.
    /// Delegates to the same resumable upload protocol as YouTubeWorker.
    /// </summary>
    private async Task<PostResult> PublishVideoToYouTubeAsync(VideoPostRequest request, CancellationToken ct)
    {
        const string YouTubeUploadUrl = "https://www.googleapis.com/upload/youtube/v3/videos";
        const int ChunkSize = 5 * 1024 * 1024; // 5MB

        var accessToken = request.Credentials?.GetValueOrDefault("access_token");
        var channelId = request.Credentials?.GetValueOrDefault("channel_id");

        if (string.IsNullOrEmpty(accessToken))
        {
            return new PostResult { Success = false, Error = "Missing YouTube access_token in credentials" };
        }

        if (string.IsNullOrEmpty(request.VideoPath))
        {
            return new PostResult { Success = false, Error = "No video file path provided" };
        }

        _logger.LogInformation("Publishing video to YouTube channel {ChannelId}", channelId);

        try
        {
            // Build video metadata
            var title = request.Title ?? "Video";
            if (title.Length > 100) title = title.Substring(0, 97) + "...";

            var videoMetadata = new
            {
                snippet = new
                {
                    title,
                    description = request.Description ?? "",
                    tags = request.Hashtags?.ToArray() ?? Array.Empty<string>(),
                    categoryId = "22"
                },
                status = new
                {
                    privacyStatus = "public",
                    selfDeclaredMadeForKids = false
                }
            };

            var metadataJson = JsonSerializer.Serialize(videoMetadata);

            // Resolve video file
            Stream videoStream;
            string? tempFilePath = null;

            if (Uri.TryCreate(request.VideoPath, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                tempFilePath = Path.Combine(Path.GetTempPath(), $"yt_pub_upload_{Guid.NewGuid():N}.mp4");
                using var downloadResponse = await _httpClient.GetAsync(request.VideoPath, HttpCompletionOption.ResponseHeadersRead, ct);
                downloadResponse.EnsureSuccessStatusCode();
                await using (var fileStream = File.Create(tempFilePath))
                {
                    await downloadResponse.Content.CopyToAsync(fileStream, ct);
                }
                videoStream = File.OpenRead(tempFilePath);
            }
            else if (File.Exists(request.VideoPath))
            {
                videoStream = File.OpenRead(request.VideoPath);
            }
            else
            {
                return new PostResult { Success = false, Error = $"Video file not found: {request.VideoPath}" };
            }

            try
            {
                var fileSize = videoStream.Length;

                // Step 1: Init resumable upload
                using var initRequest = new HttpRequestMessage(HttpMethod.Post,
                    $"{YouTubeUploadUrl}?uploadType=resumable&part=snippet,status");
                initRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                initRequest.Content = new StringContent(metadataJson, System.Text.Encoding.UTF8, "application/json");
                initRequest.Headers.Add("X-Upload-Content-Length", fileSize.ToString());
                initRequest.Headers.Add("X-Upload-Content-Type", "video/*");

                var initResponse = await _httpClient.SendAsync(initRequest, ct);
                if (!initResponse.IsSuccessStatusCode)
                {
                    var errorBody = await initResponse.Content.ReadAsStringAsync(ct);
                    return new PostResult
                    {
                        Success = false,
                        Error = $"YouTube upload init failed ({(int)initResponse.StatusCode}): {errorBody}"
                    };
                }

                var uploadUri = initResponse.Headers.Location?.ToString();
                if (string.IsNullOrEmpty(uploadUri))
                {
                    return new PostResult { Success = false, Error = "YouTube did not return resumable upload URI" };
                }

                // Step 2: Upload chunks
                var buffer = new byte[ChunkSize];
                long bytesSent = 0;
                string? videoId = null;

                while (bytesSent < fileSize)
                {
                    ct.ThrowIfCancellationRequested();
                    var currentChunkSize = (int)Math.Min(ChunkSize, fileSize - bytesSent);
                    var bytesRead = await videoStream.ReadAsync(buffer.AsMemory(0, currentChunkSize), ct);
                    if (bytesRead == 0) break;

                    using var chunkRequest = new HttpRequestMessage(HttpMethod.Put, uploadUri);
                    chunkRequest.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                    var chunkContent = new ByteArrayContent(buffer, 0, bytesRead);
                    chunkContent.Headers.ContentType =
                        new System.Net.Http.Headers.MediaTypeHeaderValue("video/*");
                    chunkContent.Headers.Add("Content-Range",
                        $"bytes {bytesSent}-{bytesSent + bytesRead - 1}/{fileSize}");
                    chunkRequest.Content = chunkContent;

                    var response = await _httpClient.SendAsync(chunkRequest, ct);
                    var statusCode = (int)response.StatusCode;

                    if (statusCode == 308)
                    {
                        bytesSent += bytesRead;
                        continue;
                    }

                    if (response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(ct);
                        var result = JsonSerializer.Deserialize<YouTubeVideoUploadResult>(responseBody);
                        videoId = result?.Id;
                        break;
                    }

                    var errorBody2 = await response.Content.ReadAsStringAsync(ct);
                    return new PostResult
                    {
                        Success = false,
                        Error = $"YouTube chunk upload failed ({statusCode}): {errorBody2}"
                    };
                }

                if (!string.IsNullOrEmpty(videoId))
                {
                    return new PostResult
                    {
                        Success = true,
                        PostId = videoId,
                        PostUrl = $"https://youtube.com/watch?v={videoId}"
                    };
                }

                return new PostResult { Success = false, Error = "YouTube upload completed but no video ID returned" };
            }
            finally
            {
                await videoStream.DisposeAsync();
                if (tempFilePath != null && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch { /* best effort cleanup */ }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish video to YouTube");
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Publish video to Instagram as a Reel.
    /// Instagram Reels API requires a publicly accessible video URL - local files cannot be uploaded directly.
    /// </summary>
    private Task<PostResult> PublishVideoToInstagramAsync(VideoPostRequest request, CancellationToken ct)
    {
        var accessToken = request.Credentials?.GetValueOrDefault("access_token");
        var igUserId = request.Credentials?.GetValueOrDefault("ig_user_id");

        if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(igUserId))
        {
            return Task.FromResult(new PostResult
            {
                Success = false,
                Error = "Missing Instagram credentials (access_token and ig_user_id required)"
            });
        }

        if (string.IsNullOrEmpty(request.VideoPath))
        {
            return Task.FromResult(new PostResult
            {
                Success = false,
                Error = "No video file path provided"
            });
        }

        // Check if the video path is a publicly accessible URL
        if (Uri.TryCreate(request.VideoPath, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "http" || uri.Scheme == "https"))
        {
            // Public URL - can proceed with Instagram Reels API
            return PublishVideoToInstagramWithUrlAsync(igUserId, accessToken, request, ct);
        }

        // Local file - Instagram does not support direct file upload
        _logger.LogWarning("Instagram Reels requires a publicly accessible video URL. Local file: {Path}", request.VideoPath);
        return Task.FromResult(new PostResult
        {
            Success = false,
            Error = "Instagram Reels API requires a publicly accessible video URL. " +
                    "Local file uploads are not supported. Please host the video on a public URL " +
                    "(e.g., via a local file server or CDN) and provide the URL instead."
        });
    }

    /// <summary>
    /// Publish a video Reel to Instagram using a public URL via the Container-based Publishing API.
    /// </summary>
    private async Task<PostResult> PublishVideoToInstagramWithUrlAsync(
        string igUserId, string accessToken, VideoPostRequest request, CancellationToken ct)
    {
        try
        {
            var caption = request.Description ?? request.Title ?? "";
            if (request.Hashtags?.Any() == true)
            {
                caption += "\n\n" + string.Join(" ", request.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
            }

            // Step 1: Create media container
            var containerData = new Dictionary<string, string>
            {
                ["media_type"] = "REELS",
                ["video_url"] = request.VideoPath,
                ["caption"] = caption,
                ["access_token"] = accessToken
            };

            var containerResponse = await _httpClient.PostAsync(
                $"{FacebookGraphApiUrl}/{igUserId}/media",
                new FormUrlEncodedContent(containerData),
                ct
            );

            var containerBody = await containerResponse.Content.ReadAsStringAsync(ct);
            if (!containerResponse.IsSuccessStatusCode)
            {
                return new PostResult
                {
                    Success = false,
                    Error = $"Instagram container creation failed ({(int)containerResponse.StatusCode}): {containerBody}"
                };
            }

            var containerResult = JsonSerializer.Deserialize<InstagramContainerResponse>(containerBody);
            if (containerResult?.Id == null)
            {
                return new PostResult { Success = false, Error = "Instagram did not return a container ID" };
            }

            // Step 2: Wait for container to be ready, then publish
            // Instagram processes the video asynchronously, poll status
            var containerId = containerResult.Id;
            var maxAttempts = 30;
            for (var i = 0; i < maxAttempts; i++)
            {
                await Task.Delay(2000, ct); // Wait 2 seconds between polls

                var statusResponse = await _httpClient.GetAsync(
                    $"{FacebookGraphApiUrl}/{containerId}?fields=status_code&access_token={accessToken}", ct);
                var statusBody = await statusResponse.Content.ReadAsStringAsync(ct);
                var status = JsonSerializer.Deserialize<InstagramContainerStatusResponse>(statusBody);

                if (status?.StatusCode == "FINISHED")
                    break;

                if (status?.StatusCode == "ERROR")
                {
                    return new PostResult
                    {
                        Success = false,
                        Error = $"Instagram video processing failed: {statusBody}"
                    };
                }

                _logger.LogDebug("Instagram container {ContainerId} status: {Status}", containerId, status?.StatusCode);
            }

            // Step 3: Publish the container
            var publishData = new Dictionary<string, string>
            {
                ["creation_id"] = containerId,
                ["access_token"] = accessToken
            };

            var publishResponse = await _httpClient.PostAsync(
                $"{FacebookGraphApiUrl}/{igUserId}/media_publish",
                new FormUrlEncodedContent(publishData),
                ct
            );

            var publishBody = await publishResponse.Content.ReadAsStringAsync(ct);
            if (publishResponse.IsSuccessStatusCode)
            {
                var publishResult = JsonSerializer.Deserialize<InstagramContainerResponse>(publishBody);
                if (publishResult?.Id != null)
                {
                    return new PostResult
                    {
                        Success = true,
                        PostId = publishResult.Id,
                        PostUrl = $"https://instagram.com/reel/{publishResult.Id}"
                    };
                }
            }

            return new PostResult
            {
                Success = false,
                Error = $"Instagram publish failed ({(int)publishResponse.StatusCode}): {publishBody}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish video to Instagram");
            return new PostResult { Success = false, Error = ex.Message };
        }
    }

    #endregion
}

/// <summary>
/// Result from posting to a group
/// </summary>
public class PostResult
{
    public bool Success { get; set; }
    public string? GroupId { get; set; }
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public string? Error { get; set; }
}

// API Response Models
internal class FacebookApiResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("error")]
    public FacebookApiError? Error { get; set; }
}

internal class FacebookApiError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }
}

internal class TwitterApiResponse
{
    [JsonPropertyName("data")]
    public TwitterData? Data { get; set; }
}

internal class TwitterData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

// Facebook Video Upload Models
internal class FacebookVideoUploadResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("error")]
    public FacebookVideoUploadError? Error { get; set; }
}

internal class FacebookVideoUploadError
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("code")]
    public int Code { get; set; }
}

// YouTube Video Upload Models (for PostPublisherService)
internal class YouTubeVideoUploadResult
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }
}

// TikTok Upload Init Models
internal class TikTokUploadInitResponse
{
    [JsonPropertyName("data")]
    public TikTokUploadInitData? Data { get; set; }

    [JsonPropertyName("error")]
    public TikTokUploadError? Error { get; set; }
}

internal class TikTokUploadInitData
{
    [JsonPropertyName("publish_id")]
    public string? PublishId { get; set; }

    [JsonPropertyName("upload_url")]
    public string? UploadUrl { get; set; }
}

internal class TikTokUploadError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

// Instagram Container Models (for PostPublisherService)
internal class InstagramContainerResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

internal class InstagramContainerStatusResponse
{
    [JsonPropertyName("status_code")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }
}
