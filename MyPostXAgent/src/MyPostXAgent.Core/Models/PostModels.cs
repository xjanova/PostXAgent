using Newtonsoft.Json;

namespace MyPostXAgent.Core.Models;

/// <summary>
/// Post - โพสต์สำหรับ Social Media
/// </summary>
public class Post
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("account_id")]
    public int? AccountId { get; set; }

    [JsonProperty("pool_id")]
    public int? PoolId { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; } = "";

    [JsonProperty("content_type")]
    public ContentType ContentType { get; set; } = ContentType.Text;

    [JsonProperty("media_paths")]
    public List<string> MediaPaths { get; set; } = new();

    [JsonProperty("hashtags")]
    public List<string> Hashtags { get; set; } = new();

    [JsonProperty("link")]
    public string? Link { get; set; }

    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("visibility")]
    public string Visibility { get; set; } = "public";

    [JsonProperty("status")]
    public PostStatus Status { get; set; } = PostStatus.Draft;

    [JsonProperty("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [JsonProperty("posted_at")]
    public DateTime? PostedAt { get; set; }

    [JsonProperty("post_url")]
    public string? PostUrl { get; set; }

    [JsonProperty("platform_post_id")]
    public string? PlatformPostId { get; set; }

    [JsonProperty("retry_count")]
    public int RetryCount { get; set; }

    [JsonProperty("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [JsonProperty("last_error")]
    public string? LastError { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public bool CanRetry => RetryCount < MaxRetries;

    public string FormattedContent => string.IsNullOrEmpty(Content)
        ? ""
        : Hashtags.Count > 0
            ? $"{Content}\n\n{string.Join(" ", Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"))}"
            : Content;
}

/// <summary>
/// Scheduled Task - งานที่ตั้งเวลาไว้
/// </summary>
public class ScheduledTask
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("task_type")]
    public TaskType TaskType { get; set; }

    [JsonProperty("target_id")]
    public int? TargetId { get; set; }

    [JsonProperty("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    [JsonProperty("status")]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    [JsonProperty("retry_count")]
    public int RetryCount { get; set; }

    [JsonProperty("max_retries")]
    public int MaxRetries { get; set; } = 3;

    [JsonProperty("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("metadata")]
    public string? MetadataJson { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDue => ScheduledAt <= DateTime.UtcNow;

    public bool CanRetry => RetryCount < MaxRetries;
}

/// <summary>
/// Comment - ความคิดเห็น
/// </summary>
public class Comment
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("post_id")]
    public int PostId { get; set; }

    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("platform_comment_id")]
    public string? PlatformCommentId { get; set; }

    [JsonProperty("author_name")]
    public string? AuthorName { get; set; }

    [JsonProperty("author_id")]
    public string? AuthorId { get; set; }

    [JsonProperty("author_avatar")]
    public string? AuthorAvatar { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; } = "";

    [JsonProperty("sentiment")]
    public CommentSentiment Sentiment { get; set; } = CommentSentiment.Neutral;

    [JsonProperty("priority")]
    public CommentPriority Priority { get; set; } = CommentPriority.Normal;

    [JsonProperty("replied")]
    public bool Replied { get; set; }

    [JsonProperty("reply_content")]
    public string? ReplyContent { get; set; }

    [JsonProperty("replied_at")]
    public DateTime? RepliedAt { get; set; }

    [JsonProperty("fetched_at")]
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("created_at")]
    public DateTime? CreatedAt { get; set; }
}

public enum CommentSentiment
{
    Positive,
    Neutral,
    Negative,
    Question
}

public enum CommentPriority
{
    Low,
    Normal,
    High,
    Urgent
}

/// <summary>
/// Discovered Group - กลุ่มที่ค้นพบ
/// </summary>
public class DiscoveredGroup
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("group_id")]
    public string GroupId { get; set; } = "";

    [JsonProperty("group_name")]
    public string GroupName { get; set; } = "";

    [JsonProperty("group_url")]
    public string? GroupUrl { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("member_count")]
    public int? MemberCount { get; set; }

    [JsonProperty("post_frequency")]
    public string? PostFrequency { get; set; }

    [JsonProperty("is_joined")]
    public bool IsJoined { get; set; }

    [JsonProperty("is_approved")]
    public bool IsApproved { get; set; }

    [JsonProperty("is_private")]
    public bool IsPrivate { get; set; }

    [JsonProperty("discovered_at")]
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonProperty("relevance_score")]
    public double RelevanceScore { get; set; }

    [JsonProperty("last_post_at")]
    public DateTime? LastPostAt { get; set; }
}

/// <summary>
/// AI Generation Record - ประวัติการสร้าง Content
/// </summary>
public class AIGenerationRecord
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("generation_type")]
    public string GenerationType { get; set; } = ""; // text, image, video, music

    [JsonProperty("provider")]
    public string Provider { get; set; } = "";

    [JsonProperty("prompt")]
    public string Prompt { get; set; } = "";

    [JsonProperty("result_path")]
    public string? ResultPath { get; set; }

    [JsonProperty("result_text")]
    public string? ResultText { get; set; }

    [JsonProperty("settings")]
    public string? SettingsJson { get; set; }

    [JsonProperty("duration_ms")]
    public int DurationMs { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; } = true;

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Project - โปรเจคสำหรับจัดการ Session งาน
/// </summary>
public class Project
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("folder_path")]
    public string FolderPath { get; set; } = "";

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonProperty("media_files")]
    public List<ProjectMediaFile> MediaFiles { get; set; } = new();
}

public class ProjectMediaFile
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("file_path")]
    public string FilePath { get; set; } = "";

    [JsonProperty("file_type")]
    public string FileType { get; set; } = ""; // video, audio, image

    [JsonProperty("file_size")]
    public long FileSize { get; set; }

    [JsonProperty("duration_seconds")]
    public double? DurationSeconds { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
