using Newtonsoft.Json;

namespace AIManager.Core.Models;

/// <summary>
/// Represents a task to be processed by the system
/// </summary>
public class TaskItem
{
    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("type")]
    public TaskType Type { get; set; }

    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("user_id")]
    public int UserId { get; set; }

    [JsonProperty("brand_id")]
    public int BrandId { get; set; }

    [JsonProperty("payload")]
    public TaskPayload Payload { get; set; } = new();

    [JsonProperty("priority")]
    public int Priority { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("status")]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    [JsonProperty("retries")]
    public int Retries { get; set; }

    [JsonProperty("max_retries")]
    public int MaxRetries { get; set; } = 3;
}

/// <summary>
/// Task payload containing all necessary data
/// </summary>
public class TaskPayload
{
    // Content generation
    [JsonProperty("prompt")]
    public string? Prompt { get; set; }

    [JsonProperty("brand_info")]
    public BrandInfo? BrandInfo { get; set; }

    [JsonProperty("content_type")]
    public ContentType ContentType { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; } = "th";

    // Image generation
    [JsonProperty("style")]
    public string? Style { get; set; }

    [JsonProperty("size")]
    public string Size { get; set; } = "1024x1024";

    [JsonProperty("provider")]
    public string Provider { get; set; } = "auto";

    // Posting
    [JsonProperty("content")]
    public PostContent? Content { get; set; }

    [JsonProperty("credentials")]
    public PlatformCredentials? Credentials { get; set; }

    [JsonProperty("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    // Metrics
    [JsonProperty("post_ids")]
    public List<string>? PostIds { get; set; }

    // Comment operations
    [JsonProperty("comment_id")]
    public string? CommentId { get; set; }

    [JsonProperty("limit")]
    public int? Limit { get; set; }

    [JsonProperty("next_page_token")]
    public string? NextPageToken { get; set; }

    // Group operations
    [JsonProperty("group_id")]
    public string? GroupId { get; set; }

    [JsonProperty("group_ids")]
    public List<string>? GroupIds { get; set; }

    // Tone configuration for auto-reply
    [JsonProperty("tone_config")]
    public ToneConfig? ToneConfig { get; set; }

    // NEW: Video Generation Properties
    [JsonProperty("video_config")]
    public VideoGenerationConfig? VideoConfig { get; set; }

    // NEW: Music Generation Properties
    [JsonProperty("music_config")]
    public MusicGenerationConfig? MusicConfig { get; set; }

    // NEW: Media Processing Properties
    [JsonProperty("processing_config")]
    public MediaProcessingConfig? ProcessingConfig { get; set; }
}

/// <summary>
/// Tone configuration for comment replies
/// </summary>
public class ToneConfig
{
    [JsonProperty("name")]
    public string Name { get; set; } = "friendly";

    [JsonProperty("friendly")]
    public int Friendly { get; set; } = 70;

    [JsonProperty("formal")]
    public int Formal { get; set; } = 30;

    [JsonProperty("humor")]
    public int Humor { get; set; } = 40;

    [JsonProperty("emoji_usage")]
    public int EmojiUsage { get; set; } = 50;

    [JsonProperty("use_particles")]
    public bool UseParticles { get; set; } = true;

    [JsonProperty("custom_instructions")]
    public string? CustomInstructions { get; set; }
}

/// <summary>
/// Brand information for AI context
/// </summary>
public class BrandInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("industry")]
    public string? Industry { get; set; }

    [JsonProperty("target_audience")]
    public string? TargetAudience { get; set; }

    [JsonProperty("tone")]
    public string Tone { get; set; } = "professional";

    [JsonProperty("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonProperty("hashtags")]
    public List<string>? Hashtags { get; set; }
}

/// <summary>
/// Content to be posted
/// </summary>
public class PostContent
{
    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("images")]
    public List<string>? Images { get; set; }

    [JsonProperty("videos")]
    public List<string>? Videos { get; set; }

    [JsonProperty("hashtags")]
    public List<string>? Hashtags { get; set; }

    [JsonProperty("link")]
    public string? Link { get; set; }

    [JsonProperty("location")]
    public string? Location { get; set; }
}

/// <summary>
/// Platform API credentials
/// </summary>
public class PlatformCredentials
{
    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonProperty("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty("platform_user_id")]
    public string? PlatformUserId { get; set; }

    [JsonProperty("page_id")]
    public string? PageId { get; set; }

    [JsonProperty("channel_id")]
    public string? ChannelId { get; set; }

    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
