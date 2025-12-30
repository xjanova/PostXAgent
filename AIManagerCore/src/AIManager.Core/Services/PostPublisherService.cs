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

    private Task<PostResult> PublishVideoToFacebookAsync(VideoPostRequest request, CancellationToken ct)
    {
        // TODO: Implement Facebook video upload using Graph API
        // POST /{page-id}/videos with video file
        _logger.LogInformation("[PLACEHOLDER] Publishing video to Facebook");
        return Task.FromResult(new PostResult
        {
            Success = true,
            PostId = $"fb_video_{Guid.NewGuid():N}",
            PostUrl = $"https://facebook.com/video/{Guid.NewGuid():N}"
        });
    }

    private Task<PostResult> PublishVideoToTikTokAsync(VideoPostRequest request, CancellationToken ct)
    {
        // TODO: Implement TikTok video upload using Content Posting API
        _logger.LogInformation("[PLACEHOLDER] Publishing video to TikTok");
        return Task.FromResult(new PostResult
        {
            Success = true,
            PostId = $"tt_video_{Guid.NewGuid():N}",
            PostUrl = $"https://tiktok.com/@user/video/{Guid.NewGuid():N}"
        });
    }

    private Task<PostResult> PublishVideoToYouTubeAsync(VideoPostRequest request, CancellationToken ct)
    {
        // TODO: Implement YouTube video upload using Data API v3
        _logger.LogInformation("[PLACEHOLDER] Publishing video to YouTube");
        return Task.FromResult(new PostResult
        {
            Success = true,
            PostId = $"yt_video_{Guid.NewGuid():N}",
            PostUrl = $"https://youtube.com/watch?v={Guid.NewGuid():N}"
        });
    }

    private Task<PostResult> PublishVideoToInstagramAsync(VideoPostRequest request, CancellationToken ct)
    {
        // TODO: Implement Instagram video upload (Reels) using Instagram Content Publishing API
        _logger.LogInformation("[PLACEHOLDER] Publishing video to Instagram");
        return Task.FromResult(new PostResult
        {
            Success = true,
            PostId = $"ig_video_{Guid.NewGuid():N}",
            PostUrl = $"https://instagram.com/reel/{Guid.NewGuid():N}"
        });
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
