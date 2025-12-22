using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// COMMENT MONITOR SERVICE - Fetch, analyze, and track comments from social media
// ═══════════════════════════════════════════════════════════════════════════════

#region Models

public class SocialComment
{
    public string Id { get; set; } = "";
    public string PlatformCommentId { get; set; } = "";
    public string Platform { get; set; } = "";
    public long PostId { get; set; }
    public string? ParentCommentId { get; set; }

    // Author
    public string AuthorName { get; set; } = "";
    public string? AuthorId { get; set; }
    public string? AuthorAvatarUrl { get; set; }

    // Content
    public string ContentText { get; set; } = "";
    public string? MediaUrl { get; set; }

    // Analysis
    public string Sentiment { get; set; } = "neutral";
    public double SentimentScore { get; set; }
    public bool IsQuestion { get; set; }
    public bool RequiresReply { get; set; } = true;
    public int Priority { get; set; }

    // Engagement
    public int LikesCount { get; set; }
    public int RepliesCount { get; set; }

    // Timestamps
    public DateTime CommentedAt { get; set; }
    public DateTime? RepliedAt { get; set; }
    public string ReplyStatus { get; set; } = "pending";
    public string? ReplyContent { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }
}

public class CommentAnalysis
{
    public string Sentiment { get; set; } = "neutral";
    public double SentimentScore { get; set; }
    public bool IsQuestion { get; set; }
    public bool RequiresReply { get; set; }
    public int Priority { get; set; }
    public List<string> Keywords { get; set; } = new();
    public string? Intent { get; set; }
    public string? Emotion { get; set; }
}

public class CommentFetchResult
{
    public bool Success { get; set; }
    public List<SocialComment> Comments { get; set; } = new();
    public int TotalCount { get; set; }
    public string? NextPageToken { get; set; }
    public string? Error { get; set; }
}

public class SentimentAnalysisResult
{
    public string Sentiment { get; set; } = "neutral";
    public double Score { get; set; }
    public bool IsQuestion { get; set; }
    public int Priority { get; set; }
}

#endregion

public class CommentMonitorService
{
    private readonly ILogger<CommentMonitorService> _logger;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly HttpClient _httpClient;

    private const string FacebookGraphUrl = "https://graph.facebook.com/v19.0";

    public event EventHandler<SocialComment>? CommentReceived;
    public event EventHandler<(SocialComment Comment, CommentAnalysis Analysis)>? CommentAnalyzed;

    public CommentMonitorService(
        ILogger<CommentMonitorService> logger,
        ContentGeneratorService contentGenerator)
    {
        _logger = logger;
        _contentGenerator = contentGenerator;
        _httpClient = new HttpClient();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FETCH COMMENTS FROM PLATFORMS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fetch comments for a post from any platform using credentials object
    /// </summary>
    public async Task<List<SocialComment>> FetchCommentsAsync(
        Models.SocialPlatform platform,
        string platformPostId,
        Models.PlatformCredentials credentials,
        int limit = 50,
        CancellationToken ct = default)
    {
        var result = await FetchCommentsAsync(
            platform.ToString().ToLower(),
            platformPostId,
            credentials.AccessToken,
            limit,
            null,
            ct);

        return result.Comments;
    }

    /// <summary>
    /// Analyze sentiment for a comment
    /// </summary>
    public async Task<SentimentAnalysisResult> AnalyzeSentimentAsync(
        string content,
        string context,
        CancellationToken ct = default)
    {
        var analysis = await AnalyzeCommentAsync(content, context, ct);
        return new SentimentAnalysisResult
        {
            Sentiment = analysis.Sentiment,
            Score = analysis.SentimentScore,
            IsQuestion = analysis.IsQuestion,
            Priority = analysis.Priority
        };
    }

    /// <summary>
    /// Fetch comments for a post from any platform
    /// </summary>
    public async Task<CommentFetchResult> FetchCommentsAsync(
        string platform,
        string platformPostId,
        string accessToken,
        int limit = 50,
        string? nextPageToken = null,
        CancellationToken ct = default)
    {
        return platform.ToLower() switch
        {
            "facebook" => await FetchFacebookCommentsAsync(platformPostId, accessToken, limit, nextPageToken, ct),
            "instagram" => await FetchInstagramCommentsAsync(platformPostId, accessToken, limit, nextPageToken, ct),
            "twitter" or "x" => await FetchTwitterCommentsAsync(platformPostId, accessToken, limit, nextPageToken, ct),
            "youtube" => await FetchYouTubeCommentsAsync(platformPostId, accessToken, limit, nextPageToken, ct),
            _ => new CommentFetchResult { Success = false, Error = $"Platform {platform} not supported" }
        };
    }

    /// <summary>
    /// Fetch comments from Facebook
    /// </summary>
    private async Task<CommentFetchResult> FetchFacebookCommentsAsync(
        string postId,
        string accessToken,
        int limit,
        string? nextPageToken,
        CancellationToken ct)
    {
        try
        {
            var url = $"{FacebookGraphUrl}/{postId}/comments" +
                      $"?fields=id,message,from{{id,name,picture}},created_time,like_count,comment_count" +
                      $"&limit={limit}" +
                      $"&access_token={accessToken}";

            if (!string.IsNullOrEmpty(nextPageToken))
            {
                url += $"&after={nextPageToken}";
            }

            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<FacebookCommentsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Data == null)
            {
                return new CommentFetchResult
                {
                    Success = false,
                    Error = "No data in response"
                };
            }

            var comments = data.Data.Select(c => new SocialComment
            {
                PlatformCommentId = c.Id,
                Platform = "facebook",
                AuthorName = c.From?.Name ?? "Unknown",
                AuthorId = c.From?.Id,
                AuthorAvatarUrl = c.From?.Picture?.Data?.Url,
                ContentText = c.Message ?? "",
                LikesCount = c.LikeCount ?? 0,
                RepliesCount = c.CommentCount ?? 0,
                CommentedAt = DateTime.TryParse(c.CreatedTime, out var dt) ? dt : DateTime.UtcNow
            }).ToList();

            foreach (var comment in comments)
            {
                CommentReceived?.Invoke(this, comment);
            }

            return new CommentFetchResult
            {
                Success = true,
                Comments = comments,
                TotalCount = comments.Count,
                NextPageToken = data.Paging?.Cursors?.After
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Facebook comments for post {PostId}", postId);
            return new CommentFetchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Fetch comments from Instagram
    /// </summary>
    private async Task<CommentFetchResult> FetchInstagramCommentsAsync(
        string mediaId,
        string accessToken,
        int limit,
        string? nextPageToken,
        CancellationToken ct)
    {
        try
        {
            var url = $"{FacebookGraphUrl}/{mediaId}/comments" +
                      $"?fields=id,text,username,timestamp,like_count,replies{{id,text,username,timestamp}}" +
                      $"&limit={limit}" +
                      $"&access_token={accessToken}";

            if (!string.IsNullOrEmpty(nextPageToken))
            {
                url += $"&after={nextPageToken}";
            }

            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<InstagramCommentsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Data == null)
            {
                return new CommentFetchResult
                {
                    Success = false,
                    Error = "No data in response"
                };
            }

            var comments = data.Data.Select(c => new SocialComment
            {
                PlatformCommentId = c.Id,
                Platform = "instagram",
                AuthorName = c.Username ?? "Unknown",
                ContentText = c.Text ?? "",
                LikesCount = c.LikeCount ?? 0,
                RepliesCount = c.Replies?.Data?.Count ?? 0,
                CommentedAt = DateTime.TryParse(c.Timestamp, out var dt) ? dt : DateTime.UtcNow
            }).ToList();

            return new CommentFetchResult
            {
                Success = true,
                Comments = comments,
                TotalCount = comments.Count,
                NextPageToken = data.Paging?.Cursors?.After
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Instagram comments for media {MediaId}", mediaId);
            return new CommentFetchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Fetch comments from Twitter/X
    /// </summary>
    private async Task<CommentFetchResult> FetchTwitterCommentsAsync(
        string tweetId,
        string bearerToken,
        int limit,
        string? nextPageToken,
        CancellationToken ct)
    {
        try
        {
            // Twitter API v2 - Search for replies
            var url = $"https://api.twitter.com/2/tweets/search/recent" +
                      $"?query=conversation_id:{tweetId}" +
                      $"&tweet.fields=created_at,public_metrics,author_id" +
                      $"&user.fields=name,username,profile_image_url" +
                      $"&expansions=author_id" +
                      $"&max_results={Math.Min(limit, 100)}";

            if (!string.IsNullOrEmpty(nextPageToken))
            {
                url += $"&pagination_token={nextPageToken}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {bearerToken}");

            var response = await _httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<TwitterSearchResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Data == null)
            {
                return new CommentFetchResult
                {
                    Success = true,
                    Comments = new List<SocialComment>(),
                    TotalCount = 0
                };
            }

            // Build user lookup dictionary
            var users = data.Includes?.Users?.ToDictionary(u => u.Id, u => u) ?? new();

            var comments = data.Data.Select(t =>
            {
                users.TryGetValue(t.AuthorId ?? "", out var author);
                return new SocialComment
                {
                    PlatformCommentId = t.Id,
                    Platform = "twitter",
                    AuthorName = author?.Name ?? "Unknown",
                    AuthorId = t.AuthorId,
                    AuthorAvatarUrl = author?.ProfileImageUrl,
                    ContentText = t.Text ?? "",
                    LikesCount = t.PublicMetrics?.LikeCount ?? 0,
                    RepliesCount = t.PublicMetrics?.ReplyCount ?? 0,
                    CommentedAt = DateTime.TryParse(t.CreatedAt, out var dt) ? dt : DateTime.UtcNow
                };
            }).ToList();

            return new CommentFetchResult
            {
                Success = true,
                Comments = comments,
                TotalCount = data.Meta?.ResultCount ?? comments.Count,
                NextPageToken = data.Meta?.NextToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Twitter replies for tweet {TweetId}", tweetId);
            return new CommentFetchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Fetch comments from YouTube
    /// </summary>
    private async Task<CommentFetchResult> FetchYouTubeCommentsAsync(
        string videoId,
        string apiKey,
        int limit,
        string? nextPageToken,
        CancellationToken ct)
    {
        try
        {
            var url = $"https://www.googleapis.com/youtube/v3/commentThreads" +
                      $"?part=snippet,replies" +
                      $"&videoId={videoId}" +
                      $"&maxResults={Math.Min(limit, 100)}" +
                      $"&key={apiKey}";

            if (!string.IsNullOrEmpty(nextPageToken))
            {
                url += $"&pageToken={nextPageToken}";
            }

            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<YouTubeCommentsResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data?.Items == null)
            {
                return new CommentFetchResult
                {
                    Success = false,
                    Error = "No items in response"
                };
            }

            var comments = data.Items.Select(item =>
            {
                var snippet = item.Snippet?.TopLevelComment?.Snippet;
                return new SocialComment
                {
                    PlatformCommentId = item.Id,
                    Platform = "youtube",
                    AuthorName = snippet?.AuthorDisplayName ?? "Unknown",
                    AuthorId = snippet?.AuthorChannelId?.Value,
                    AuthorAvatarUrl = snippet?.AuthorProfileImageUrl,
                    ContentText = snippet?.TextDisplay ?? "",
                    LikesCount = snippet?.LikeCount ?? 0,
                    RepliesCount = item.Snippet?.TotalReplyCount ?? 0,
                    CommentedAt = DateTime.TryParse(snippet?.PublishedAt, out var dt) ? dt : DateTime.UtcNow
                };
            }).ToList();

            return new CommentFetchResult
            {
                Success = true,
                Comments = comments,
                TotalCount = data.PageInfo?.TotalResults ?? comments.Count,
                NextPageToken = data.NextPageToken
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch YouTube comments for video {VideoId}", videoId);
            return new CommentFetchResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // COMMENT ANALYSIS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Analyze a comment using AI
    /// </summary>
    public async Task<CommentAnalysis> AnalyzeCommentAsync(
        string commentText,
        string? postContext = null,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = $@"วิเคราะห์คอมเมนต์นี้และตอบเป็น JSON เท่านั้น:

คอมเมนต์: ""{commentText}""
{(postContext != null ? $"บริบทโพสต์: {postContext}" : "")}

วิเคราะห์:
1. sentiment: positive/negative/neutral
2. sentiment_score: คะแนน -1.0 ถึง 1.0
3. is_question: true/false (เป็นคำถามหรือไม่)
4. requires_reply: true/false (ต้องตอบหรือไม่)
5. priority: 0-20 (ความเร่งด่วน)
6. intent: inquiry/complaint/praise/feedback/spam/greeting (จุดประสงค์)
7. emotion: happy/angry/sad/curious/neutral (อารมณ์)
8. keywords: คำสำคัญ

ตอบเฉพาะ JSON:";

            var result = await _contentGenerator.GenerateAsync(prompt, null, "general", "th", ct);

            if (result?.Text == null)
            {
                return CreateDefaultAnalysis(commentText);
            }

            // Parse JSON from response
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                result.Text,
                @"\{[\s\S]*\}",
                System.Text.RegularExpressions.RegexOptions.Multiline
            );

            if (jsonMatch.Success)
            {
                var analysis = JsonSerializer.Deserialize<CommentAnalysisJson>(jsonMatch.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (analysis != null)
                {
                    return new CommentAnalysis
                    {
                        Sentiment = analysis.Sentiment ?? "neutral",
                        SentimentScore = analysis.SentimentScore ?? 0,
                        IsQuestion = analysis.IsQuestion ?? DetectQuestion(commentText),
                        RequiresReply = analysis.RequiresReply ?? true,
                        Priority = analysis.Priority ?? CalculateBasePriority(commentText),
                        Intent = analysis.Intent,
                        Emotion = analysis.Emotion,
                        Keywords = analysis.Keywords ?? new List<string>()
                    };
                }
            }

            return CreateDefaultAnalysis(commentText);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis failed, using rule-based analysis");
            return CreateDefaultAnalysis(commentText);
        }
    }

    /// <summary>
    /// Create default analysis using simple rules
    /// </summary>
    private CommentAnalysis CreateDefaultAnalysis(string text)
    {
        var isQuestion = DetectQuestion(text);
        var sentiment = DetectSentimentSimple(text);
        var priority = CalculateBasePriority(text);

        return new CommentAnalysis
        {
            Sentiment = sentiment,
            SentimentScore = sentiment == "positive" ? 0.5 : sentiment == "negative" ? -0.5 : 0,
            IsQuestion = isQuestion,
            RequiresReply = isQuestion || sentiment == "negative",
            Priority = priority
        };
    }

    /// <summary>
    /// Simple question detection
    /// </summary>
    private bool DetectQuestion(string text)
    {
        var questionIndicators = new[]
        {
            "?", "ไหม", "อะไร", "ที่ไหน", "เมื่อไหร่", "อย่างไร", "ยังไง",
            "ทำไม", "กี่", "เท่าไหร่", "หรือเปล่า", "รึเปล่า", "มั้ย",
            "what", "where", "when", "how", "why", "which", "who"
        };

        var lowerText = text.ToLower();
        return questionIndicators.Any(q => lowerText.Contains(q));
    }

    /// <summary>
    /// Simple sentiment detection
    /// </summary>
    private string DetectSentimentSimple(string text)
    {
        var positiveWords = new[]
        {
            "ดี", "เยี่ยม", "สุดยอด", "ชอบ", "รัก", "ขอบคุณ", "น่ารัก", "สวย", "เก่ง", "ประทับใจ",
            "good", "great", "love", "thanks", "amazing", "awesome", "beautiful", "excellent"
        };

        var negativeWords = new[]
        {
            "แย่", "ห่วย", "เลว", "ไม่ดี", "โกง", "เกลียด", "ผิดหวัง", "แพง", "ช้า", "หลอก",
            "bad", "terrible", "hate", "disappointed", "scam", "fake", "worst", "horrible"
        };

        var lowerText = text.ToLower();
        var positiveCount = positiveWords.Count(w => lowerText.Contains(w));
        var negativeCount = negativeWords.Count(w => lowerText.Contains(w));

        if (positiveCount > negativeCount) return "positive";
        if (negativeCount > positiveCount) return "negative";
        return "neutral";
    }

    /// <summary>
    /// Calculate base priority
    /// </summary>
    private int CalculateBasePriority(string text)
    {
        var priority = 5; // Base priority

        // Questions get higher priority
        if (DetectQuestion(text)) priority += 3;

        // Negative sentiment gets higher priority
        if (DetectSentimentSimple(text) == "negative") priority += 5;

        // Urgent words
        var urgentWords = new[] { "ด่วน", "urgent", "asap", "รีบ", "เร็ว", "help", "ช่วย" };
        if (urgentWords.Any(w => text.ToLower().Contains(w))) priority += 5;

        return Math.Min(priority, 20);
    }

    /// <summary>
    /// Analyze and update a comment
    /// </summary>
    public async Task<SocialComment> AnalyzeAndUpdateCommentAsync(
        SocialComment comment,
        string? postContext = null,
        CancellationToken ct = default)
    {
        var analysis = await AnalyzeCommentAsync(comment.ContentText, postContext, ct);

        comment.Sentiment = analysis.Sentiment;
        comment.SentimentScore = analysis.SentimentScore;
        comment.IsQuestion = analysis.IsQuestion;
        comment.RequiresReply = analysis.RequiresReply;
        comment.Priority = analysis.Priority;
        comment.Metadata = new Dictionary<string, object>
        {
            ["intent"] = analysis.Intent ?? "",
            ["emotion"] = analysis.Emotion ?? "",
            ["keywords"] = analysis.Keywords,
            ["analyzed_at"] = DateTime.UtcNow.ToString("o")
        };

        CommentAnalyzed?.Invoke(this, (comment, analysis));

        return comment;
    }

    /// <summary>
    /// Batch analyze comments
    /// </summary>
    public async Task<List<SocialComment>> AnalyzeCommentsAsync(
        List<SocialComment> comments,
        string? postContext = null,
        CancellationToken ct = default)
    {
        var analyzed = new List<SocialComment>();

        foreach (var comment in comments)
        {
            if (ct.IsCancellationRequested) break;

            var result = await AnalyzeAndUpdateCommentAsync(comment, postContext, ct);
            analyzed.Add(result);

            // Small delay to avoid rate limits
            await Task.Delay(100, ct);
        }

        return analyzed.OrderByDescending(c => c.Priority).ToList();
    }
}

#region API Response Models

internal class FacebookCommentsResponse
{
    public List<FacebookComment>? Data { get; set; }
    public FacebookPaging? Paging { get; set; }
}

internal class FacebookComment
{
    public string Id { get; set; } = "";
    public string? Message { get; set; }
    public FacebookFrom? From { get; set; }
    public string? CreatedTime { get; set; }
    public int? LikeCount { get; set; }
    public int? CommentCount { get; set; }
}

internal class FacebookFrom
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public FacebookPicture? Picture { get; set; }
}

internal class FacebookPicture
{
    public FacebookPictureData? Data { get; set; }
}

internal class FacebookPictureData
{
    public string? Url { get; set; }
}

internal class FacebookPaging
{
    public FacebookCursors? Cursors { get; set; }
}

internal class FacebookCursors
{
    public string? Before { get; set; }
    public string? After { get; set; }
}

internal class InstagramCommentsResponse
{
    public List<InstagramComment>? Data { get; set; }
    public FacebookPaging? Paging { get; set; }
}

internal class InstagramComment
{
    public string Id { get; set; } = "";
    public string? Text { get; set; }
    public string? Username { get; set; }
    public string? Timestamp { get; set; }
    public int? LikeCount { get; set; }
    public InstagramReplies? Replies { get; set; }
}

internal class InstagramReplies
{
    public List<InstagramComment>? Data { get; set; }
}

internal class TwitterSearchResponse
{
    public List<TwitterTweet>? Data { get; set; }
    public TwitterIncludes? Includes { get; set; }
    public TwitterMeta? Meta { get; set; }
}

internal class TwitterTweet
{
    public string Id { get; set; } = "";
    public string? Text { get; set; }
    public string? AuthorId { get; set; }
    public string? CreatedAt { get; set; }
    public TwitterPublicMetrics? PublicMetrics { get; set; }
}

internal class TwitterPublicMetrics
{
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public int RetweetCount { get; set; }
}

internal class TwitterIncludes
{
    public List<TwitterUser>? Users { get; set; }
}

internal class TwitterUser
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Username { get; set; }
    public string? ProfileImageUrl { get; set; }
}

internal class TwitterMeta
{
    public int ResultCount { get; set; }
    public string? NextToken { get; set; }
}

internal class YouTubeCommentsResponse
{
    public List<YouTubeCommentThread>? Items { get; set; }
    public YouTubePageInfo? PageInfo { get; set; }
    public string? NextPageToken { get; set; }
}

internal class YouTubeCommentThread
{
    public string Id { get; set; } = "";
    public YouTubeCommentSnippet? Snippet { get; set; }
}

internal class YouTubeCommentSnippet
{
    public YouTubeTopLevelComment? TopLevelComment { get; set; }
    public int TotalReplyCount { get; set; }
}

internal class YouTubeTopLevelComment
{
    public YouTubeCommentDetail? Snippet { get; set; }
}

internal class YouTubeCommentDetail
{
    public string? TextDisplay { get; set; }
    public string? AuthorDisplayName { get; set; }
    public YouTubeAuthorChannelId? AuthorChannelId { get; set; }
    public string? AuthorProfileImageUrl { get; set; }
    public int LikeCount { get; set; }
    public string? PublishedAt { get; set; }
}

internal class YouTubeAuthorChannelId
{
    public string? Value { get; set; }
}

internal class YouTubePageInfo
{
    public int TotalResults { get; set; }
}

internal class CommentAnalysisJson
{
    public string? Sentiment { get; set; }
    public double? SentimentScore { get; set; }
    public bool? IsQuestion { get; set; }
    public bool? RequiresReply { get; set; }
    public int? Priority { get; set; }
    public string? Intent { get; set; }
    public string? Emotion { get; set; }
    public List<string>? Keywords { get; set; }
}

#endregion
