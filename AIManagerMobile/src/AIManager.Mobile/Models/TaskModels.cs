using System.Text.Json.Serialization;

namespace AIManager.Mobile.Models;

/// <summary>
/// Task status enum matching AI Manager Core
/// </summary>
public enum TaskStatus
{
    Pending,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Social media platforms
/// </summary>
public enum SocialPlatform
{
    Facebook,
    Instagram,
    TikTok,
    Twitter,
    Line,
    YouTube,
    Threads,
    LinkedIn,
    Pinterest
}

/// <summary>
/// Task types matching AI Manager Core
/// </summary>
public enum TaskType
{
    GenerateContent,
    GenerateImage,
    PostContent,
    SchedulePost,
    PostToGroup,
    PostToMultipleGroups,
    AnalyzeMetrics,
    MonitorEngagement,
    AnalyzeViralPotential,
    TrackTrendingKeywords,
    FetchComments,
    ReplyToComment,
    AutoReplyComments,
    AnalyzeCommentSentiment,
    DeletePost,
    RefreshToken,
    SearchGroups,
    JoinGroup,
    AnalyzeGroupActivity
}

/// <summary>
/// Task item for display in mobile app
/// </summary>
public class TaskItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public TaskType Type { get; set; }

    [JsonPropertyName("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("brand_id")]
    public int BrandId { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    [JsonPropertyName("retries")]
    public int Retries { get; set; }

    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; set; } = 3;

    // Display helpers
    public string StatusText => Status switch
    {
        TaskStatus.Pending => "รอดำเนินการ",
        TaskStatus.Queued => "อยู่ในคิว",
        TaskStatus.Running => "กำลังทำงาน",
        TaskStatus.Completed => "สำเร็จ",
        TaskStatus.Failed => "ล้มเหลว",
        TaskStatus.Cancelled => "ยกเลิก",
        _ => "ไม่ทราบ"
    };

    public Color StatusColor => Status switch
    {
        TaskStatus.Pending => Color.FromArgb("#FF9800"),
        TaskStatus.Queued => Color.FromArgb("#2196F3"),
        TaskStatus.Running => Color.FromArgb("#00BCD4"),
        TaskStatus.Completed => Color.FromArgb("#4CAF50"),
        TaskStatus.Failed => Color.FromArgb("#F44336"),
        TaskStatus.Cancelled => Color.FromArgb("#9E9E9E"),
        _ => Color.FromArgb("#9E9E9E")
    };

    public string TypeText => Type switch
    {
        TaskType.GenerateContent => "สร้างเนื้อหา",
        TaskType.GenerateImage => "สร้างรูปภาพ",
        TaskType.PostContent => "โพสต์",
        TaskType.SchedulePost => "ตั้งเวลาโพสต์",
        TaskType.PostToGroup => "โพสต์ในกลุ่ม",
        TaskType.AnalyzeMetrics => "วิเคราะห์ข้อมูล",
        TaskType.ReplyToComment => "ตอบคอมเมนต์",
        _ => Type.ToString()
    };

    public string PlatformIcon => Platform switch
    {
        SocialPlatform.Facebook => "\uE87C",
        SocialPlatform.Instagram => "\uE3B0",
        SocialPlatform.TikTok => "\uE04B",
        SocialPlatform.Twitter => "\uE0CA",
        SocialPlatform.Line => "\uE0C9",
        SocialPlatform.YouTube => "\uE063",
        _ => "\uE8D3"
    };
}

/// <summary>
/// Task result from AI Manager Core
/// </summary>
public class TaskResult
{
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("processed_at")]
    public DateTime ProcessedAt { get; set; }
}
