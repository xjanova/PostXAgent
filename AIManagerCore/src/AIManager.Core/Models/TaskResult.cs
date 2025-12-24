using Newtonsoft.Json;

namespace AIManager.Core.Models;

/// <summary>
/// Result from task processing
/// </summary>
public class TaskResult
{
    [JsonProperty("task_id")]
    public string TaskId { get; set; } = "";

    [JsonProperty("worker_id")]
    public int WorkerId { get; set; }

    [JsonProperty("platform")]
    public string Platform { get; set; } = "";

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("error_code")]
    public string? ErrorCode { get; set; }

    [JsonProperty("error_type")]
    public ErrorType ErrorType { get; set; } = ErrorType.Unknown;

    [JsonProperty("account_id")]
    public int? AccountId { get; set; }

    [JsonProperty("should_retry")]
    public bool ShouldRetry { get; set; }

    [JsonProperty("retry_after_seconds")]
    public int? RetryAfterSeconds { get; set; }

    [JsonProperty("data")]
    public ResultData? Data { get; set; }

    [JsonProperty("processing_time_ms")]
    public long ProcessingTimeMs { get; set; }

    [JsonProperty("completed_at")]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Error types for account rotation handling
/// </summary>
public enum ErrorType
{
    Unknown = 0,
    NetworkError = 1,
    AuthenticationError = 2,
    RateLimited = 3,
    AccountBanned = 4,
    AccountSuspended = 5,
    ContentRejected = 6,
    PlatformError = 7,
    ValidationError = 8,
    TokenExpired = 9
}

/// <summary>
/// Result data from various task types
/// </summary>
public class ResultData
{
    // Content generation result
    [JsonProperty("content")]
    public string? Content { get; set; }

    [JsonProperty("hashtags")]
    public List<string>? Hashtags { get; set; }

    [JsonProperty("tokens_used")]
    public int TokensUsed { get; set; }

    [JsonProperty("ai_provider")]
    public string? AIProvider { get; set; }

    // Image generation result
    [JsonProperty("image_url")]
    public string? ImageUrl { get; set; }

    [JsonProperty("image_base64")]
    public string? ImageBase64 { get; set; }

    // Posting result
    [JsonProperty("post_id")]
    public string? PostId { get; set; }

    [JsonProperty("platform_url")]
    public string? PlatformUrl { get; set; }

    // Metrics result
    [JsonProperty("metrics")]
    public EngagementMetrics? Metrics { get; set; }

    // Comment results
    [JsonProperty("comments")]
    public List<CommentData>? Comments { get; set; }

    [JsonProperty("reply_id")]
    public string? ReplyId { get; set; }

    [JsonProperty("next_page_token")]
    public string? NextPageToken { get; set; }

    // Viral analysis results
    [JsonProperty("viral_score")]
    public double? ViralScore { get; set; }

    [JsonProperty("viral_factors")]
    public Dictionary<string, object>? ViralFactors { get; set; }

    [JsonProperty("trending_keywords")]
    public List<string>? TrendingKeywords { get; set; }

    // Comment reply results
    [JsonProperty("reply_content")]
    public string? ReplyContent { get; set; }

    [JsonProperty("replies")]
    public List<object>? Replies { get; set; }

    [JsonProperty("sentiment")]
    public string? Sentiment { get; set; }

    [JsonProperty("sentiment_score")]
    public double? SentimentScore { get; set; }

    [JsonProperty("is_question")]
    public bool? IsQuestion { get; set; }

    [JsonProperty("priority_score")]
    public int? PriorityScore { get; set; }

    // Keyword tracking results
    [JsonProperty("keywords")]
    public List<object>? Keywords { get; set; }

    [JsonProperty("keyword_updates")]
    public List<object>? KeywordUpdates { get; set; }

    [JsonProperty("optimal_times")]
    public object? OptimalTimes { get; set; }

    [JsonProperty("emotions")]
    public object? Emotions { get; set; }

    // Emotional analysis results
    [JsonProperty("emotional_score")]
    public double? EmotionalScore { get; set; }

    [JsonProperty("emotional_triggers")]
    public List<string>? EmotionalTriggers { get; set; }

    [JsonProperty("recommendations")]
    public List<string>? Recommendations { get; set; }

    // Message field for responses
    [JsonProperty("message")]
    public string? Message { get; set; }

    // Group posting results
    [JsonProperty("groups")]
    public List<object>? Groups { get; set; }

    [JsonProperty("post_url")]
    public string? PostUrl { get; set; }

    [JsonProperty("post_results")]
    public List<PostResultData>? PostResults { get; set; }

    [JsonProperty("task_id")]
    public string? TaskId { get; set; }

    [JsonProperty("active_loops")]
    public List<string>? ActiveLoops { get; set; }

    [JsonProperty("generated_content")]
    public string? GeneratedContent { get; set; }

    // NEW: Media generation results (Video/Music)
    [JsonProperty("media_result")]
    public MediaGenerationResult? MediaResult { get; set; }
}

/// <summary>
/// Comment data from social platforms
/// </summary>
public class CommentData
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("platform_comment_id")]
    public string PlatformCommentId { get; set; } = "";

    [JsonProperty("content")]
    public string Content { get; set; } = "";

    [JsonProperty("author_name")]
    public string AuthorName { get; set; } = "";

    [JsonProperty("author_id")]
    public string? AuthorId { get; set; }

    [JsonProperty("author_avatar")]
    public string? AuthorAvatar { get; set; }

    [JsonProperty("author_avatar_url")]
    public string? AuthorAvatarUrl { get; set; }

    [JsonProperty("content_text")]
    public string ContentText { get; set; } = "";

    [JsonProperty("media_url")]
    public string? MediaUrl { get; set; }

    [JsonProperty("sentiment")]
    public string Sentiment { get; set; } = "neutral";

    [JsonProperty("sentiment_score")]
    public double SentimentScore { get; set; }

    [JsonProperty("is_question")]
    public bool IsQuestion { get; set; }

    [JsonProperty("priority_score")]
    public int PriorityScore { get; set; }

    [JsonProperty("requires_reply")]
    public bool RequiresReply { get; set; } = true;

    [JsonProperty("priority")]
    public int Priority { get; set; }

    [JsonProperty("likes_count")]
    public int LikesCount { get; set; }

    [JsonProperty("replies_count")]
    public int RepliesCount { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("commented_at")]
    public DateTime CommentedAt { get; set; }

    [JsonProperty("parent_comment_id")]
    public string? ParentCommentId { get; set; }
}

/// <summary>
/// Post result data for batch posting
/// </summary>
public class PostResultData
{
    [JsonProperty("group_id")]
    public string GroupId { get; set; } = "";

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("post_id")]
    public string? PostId { get; set; }

    [JsonProperty("post_url")]
    public string? PostUrl { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Engagement metrics from social platforms
/// </summary>
public class EngagementMetrics
{
    [JsonProperty("likes")]
    public int Likes { get; set; }

    [JsonProperty("comments")]
    public int Comments { get; set; }

    [JsonProperty("shares")]
    public int Shares { get; set; }

    [JsonProperty("views")]
    public int Views { get; set; }

    [JsonProperty("clicks")]
    public int Clicks { get; set; }

    [JsonProperty("saves")]
    public int Saves { get; set; }

    [JsonProperty("reach")]
    public int Reach { get; set; }

    [JsonProperty("impressions")]
    public int Impressions { get; set; }

    [JsonProperty("engagement_rate")]
    public double EngagementRate { get; set; }

    [JsonProperty("retrieved_at")]
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
}
