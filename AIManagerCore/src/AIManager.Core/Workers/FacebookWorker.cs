using System.Net.Http.Json;
using AIManager.Core.Models;
using AIManager.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Facebook Platform Worker
/// Supports: Pages, Groups, Marketplace
/// </summary>
public class FacebookWorker : BasePlatformWorker
{
    private const string GraphApiUrl = "https://graph.facebook.com/v19.0";

    public override SocialPlatform Platform => SocialPlatform.Facebook;

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;

            if (credentials == null || content == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Missing credentials or content",
                    ErrorType = Models.ErrorType.ValidationError,
                    ShouldRetry = false
                };
            }

            var pageId = credentials.PageId ?? credentials.PlatformUserId;
            var accessToken = credentials.AccessToken;

            // Post to Facebook
            var postData = new Dictionary<string, string>
            {
                ["message"] = content.Text + "\n\n" + FormatHashtags(content.Hashtags),
                ["access_token"] = accessToken
            };

            if (!string.IsNullOrEmpty(content.Link))
            {
                postData["link"] = content.Link;
            }

            var response = await _httpClient.PostAsync(
                $"{GraphApiUrl}/{pageId}/feed",
                new FormUrlEncodedContent(postData),
                ct
            );

            var result = await response.Content.ReadFromJsonAsync<FacebookPostResponse>(ct);

            if (result?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = result.Id,
                        PlatformUrl = $"https://facebook.com/{result.Id}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Error occurred - classify it for account rotation
            var errorCode = result?.Error?.Code.ToString();
            var errorMessage = result?.Error?.Message ?? "Unknown error";

            return ErrorClassifier.CreateErrorResult(
                Platform,
                errorCode,
                errorMessage,
                (int)response.StatusCode,
                sw.ElapsedMilliseconds
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Facebook post failed - network error");
            return ErrorClassifier.CreateErrorResult(
                Platform,
                null,
                ex.Message,
                null,
                sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook post failed");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ErrorType = Models.ErrorType.Unknown,
                ShouldRetry = true,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public override async Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var postIds = task.Payload.PostIds;

            if (credentials == null || postIds == null || postIds.Count == 0)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Missing credentials or post IDs"
                };
            }

            var postId = postIds[0];
            var accessToken = credentials.AccessToken;

            var response = await _httpClient.GetAsync(
                $"{GraphApiUrl}/{postId}?fields=likes.summary(true),comments.summary(true),shares&access_token={accessToken}",
                ct
            );

            var data = await response.Content.ReadFromJsonAsync<FacebookInsightsResponse>(ct);

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Metrics = new EngagementMetrics
                    {
                        Likes = data?.Likes?.Summary?.TotalCount ?? 0,
                        Comments = data?.Comments?.Summary?.TotalCount ?? 0,
                        Shares = data?.Shares?.Count ?? 0
                    }
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook metrics failed");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    public override async Task<TaskResult> DeletePostAsync(TaskItem task, CancellationToken ct)
    {
        try
        {
            var credentials = task.Payload.Credentials;
            var postIds = task.Payload.PostIds;

            if (credentials == null || postIds == null || postIds.Count == 0)
            {
                return new TaskResult { Success = false, Error = "Missing data" };
            }

            var postId = postIds[0];
            var response = await _httpClient.DeleteAsync(
                $"{GraphApiUrl}/{postId}?access_token={credentials.AccessToken}",
                ct
            );

            return new TaskResult { Success = response.IsSuccessStatusCode };
        }
        catch (Exception ex)
        {
            return new TaskResult { Success = false, Error = ex.Message };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMENT MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fetch comments for a post
    /// </summary>
    public async Task<TaskResult> FetchCommentsAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var postIds = task.Payload.PostIds;

            if (credentials == null || postIds == null || postIds.Count == 0)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Missing credentials or post IDs"
                };
            }

            var postId = postIds[0];
            var accessToken = credentials.AccessToken;
            var limit = task.Payload.Limit ?? 50;

            var url = $"{GraphApiUrl}/{postId}/comments" +
                      $"?fields=id,message,from{{id,name,picture}},created_time,like_count,comment_count" +
                      $"&limit={limit}" +
                      $"&access_token={accessToken}";

            var response = await _httpClient.GetAsync(url, ct);
            var data = await response.Content.ReadFromJsonAsync<FacebookCommentsListResponse>(ct);

            if (data?.Data == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "No data in response",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            var comments = data.Data.Select(c => new CommentData
            {
                PlatformCommentId = c.Id ?? "",
                AuthorName = c.From?.Name ?? "Unknown",
                AuthorId = c.From?.Id,
                AuthorAvatarUrl = c.From?.Picture?.Data?.Url,
                ContentText = c.Message ?? "",
                LikesCount = c.LikeCount ?? 0,
                RepliesCount = c.CommentCount ?? 0,
                CommentedAt = DateTime.TryParse(c.CreatedTime, out var dt) ? dt : DateTime.UtcNow
            }).ToList();

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Comments = comments,
                    NextPageToken = data.Paging?.Cursors?.After
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Facebook comments");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Reply to a comment
    /// </summary>
    public async Task<TaskResult> ReplyToCommentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var commentId = task.Payload.CommentId;
            var replyContent = task.Payload.Content?.Text;

            if (credentials == null || string.IsNullOrEmpty(commentId) || string.IsNullOrEmpty(replyContent))
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Missing credentials, comment ID, or reply content"
                };
            }

            var postData = new Dictionary<string, string>
            {
                ["message"] = replyContent,
                ["access_token"] = credentials.AccessToken
            };

            var response = await _httpClient.PostAsync(
                $"{GraphApiUrl}/{commentId}/comments",
                new FormUrlEncodedContent(postData),
                ct
            );

            var result = await response.Content.ReadFromJsonAsync<FacebookPostResponse>(ct);

            if (result?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        ReplyId = result.Id
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = result?.Error?.Message ?? "Unknown error",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reply to Facebook comment");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Post to a Facebook Group (requires group admin access or user token with publish_to_groups permission)
    /// </summary>
    public async Task<TaskResult> PostToGroupAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var credentials = task.Payload.Credentials;
            var content = task.Payload.Content;
            var groupId = task.Payload.GroupId;

            if (credentials == null || content == null || string.IsNullOrEmpty(groupId))
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Missing credentials, content, or group ID",
                    ErrorType = Models.ErrorType.ValidationError,
                    ShouldRetry = false
                };
            }

            var accessToken = credentials.AccessToken;

            // Post to group
            var postData = new Dictionary<string, string>
            {
                ["message"] = content.Text + "\n\n" + FormatHashtags(content.Hashtags),
                ["access_token"] = accessToken
            };

            if (!string.IsNullOrEmpty(content.Link))
            {
                postData["link"] = content.Link;
            }

            var response = await _httpClient.PostAsync(
                $"{GraphApiUrl}/{groupId}/feed",
                new FormUrlEncodedContent(postData),
                ct
            );

            var result = await response.Content.ReadFromJsonAsync<FacebookPostResponse>(ct);

            if (result?.Id != null)
            {
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        PostId = result.Id,
                        PlatformUrl = $"https://facebook.com/groups/{groupId}/posts/{result.Id.Split('_').LastOrDefault()}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            // Error occurred
            var errorCode = result?.Error?.Code.ToString();
            var errorMessage = result?.Error?.Message ?? "Unknown error";

            return ErrorClassifier.CreateErrorResult(
                Platform,
                errorCode,
                errorMessage,
                (int)response.StatusCode,
                sw.ElapsedMilliseconds
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Facebook group post failed - network error");
            return ErrorClassifier.CreateErrorResult(
                Platform,
                null,
                ex.Message,
                null,
                sw.ElapsedMilliseconds
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Facebook group post failed");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ErrorType = Models.ErrorType.Unknown,
                ShouldRetry = true,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }
}

// Facebook API response models
internal class FacebookPostResponse
{
    public string? Id { get; set; }
    public FacebookError? Error { get; set; }
}

internal class FacebookError
{
    public string? Message { get; set; }
    public int Code { get; set; }
}

internal class FacebookInsightsResponse
{
    public FacebookLikes? Likes { get; set; }
    public FacebookComments? Comments { get; set; }
    public FacebookShares? Shares { get; set; }
}

internal class FacebookLikes
{
    public FacebookSummary? Summary { get; set; }
}

internal class FacebookComments
{
    public FacebookSummary? Summary { get; set; }
}

internal class FacebookShares
{
    public int Count { get; set; }
}

internal class FacebookSummary
{
    public int TotalCount { get; set; }
}

// Extended response models for comments
internal class FacebookCommentsListResponse
{
    public List<FacebookCommentItem>? Data { get; set; }
    public FacebookPagingInfo? Paging { get; set; }
}

internal class FacebookCommentItem
{
    public string? Id { get; set; }
    public string? Message { get; set; }
    public FacebookFromInfo? From { get; set; }
    public string? CreatedTime { get; set; }
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
}

internal class FacebookFromInfo
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public FacebookPictureInfo? Picture { get; set; }
}

internal class FacebookPictureInfo
{
    public FacebookPictureData? Data { get; set; }
}

internal class FacebookPictureData
{
    public string? Url { get; set; }
}

internal class FacebookPagingInfo
{
    public FacebookCursorsInfo? Cursors { get; set; }
}

internal class FacebookCursorsInfo
{
    public string? Before { get; set; }
    public string? After { get; set; }
}
