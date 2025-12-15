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
                    ErrorType = ErrorType.ValidationError,
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
                ErrorType = ErrorType.Unknown,
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
