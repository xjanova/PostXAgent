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
