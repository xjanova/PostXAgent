using System.Text.Json.Serialization;

namespace AIManager.Mobile.Models;

/// <summary>
/// Worker status information
/// </summary>
public class WorkerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonPropertyName("status")]
    public WorkerStatus Status { get; set; }

    [JsonPropertyName("current_task")]
    public string? CurrentTask { get; set; }

    [JsonPropertyName("tasks_completed")]
    public int TasksCompleted { get; set; }

    [JsonPropertyName("tasks_failed")]
    public int TasksFailed { get; set; }

    [JsonPropertyName("last_activity")]
    public DateTime? LastActivity { get; set; }

    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    // Display helpers
    public string StatusText => Status switch
    {
        WorkerStatus.Idle => "ว่าง",
        WorkerStatus.Working => "กำลังทำงาน",
        WorkerStatus.Paused => "หยุดชั่วคราว",
        WorkerStatus.Error => "มีข้อผิดพลาด",
        WorkerStatus.Offline => "ออฟไลน์",
        _ => "ไม่ทราบ"
    };

    public Color StatusColor => Status switch
    {
        WorkerStatus.Idle => Color.FromArgb("#4CAF50"),
        WorkerStatus.Working => Color.FromArgb("#00BCD4"),
        WorkerStatus.Paused => Color.FromArgb("#FF9800"),
        WorkerStatus.Error => Color.FromArgb("#F44336"),
        WorkerStatus.Offline => Color.FromArgb("#9E9E9E"),
        _ => Color.FromArgb("#9E9E9E")
    };

    public string PlatformName => Platform switch
    {
        SocialPlatform.Facebook => "Facebook",
        SocialPlatform.Instagram => "Instagram",
        SocialPlatform.TikTok => "TikTok",
        SocialPlatform.Twitter => "Twitter/X",
        SocialPlatform.Line => "LINE",
        SocialPlatform.YouTube => "YouTube",
        SocialPlatform.Threads => "Threads",
        SocialPlatform.LinkedIn => "LinkedIn",
        SocialPlatform.Pinterest => "Pinterest",
        _ => "Unknown"
    };

    public Color PlatformColor => Platform switch
    {
        SocialPlatform.Facebook => Color.FromArgb("#1877F2"),
        SocialPlatform.Instagram => Color.FromArgb("#DD2A7B"),
        SocialPlatform.TikTok => Color.FromArgb("#00F2EA"),
        SocialPlatform.Twitter => Color.FromArgb("#1DA1F2"),
        SocialPlatform.Line => Color.FromArgb("#00B900"),
        SocialPlatform.YouTube => Color.FromArgb("#FF0000"),
        SocialPlatform.Threads => Color.FromArgb("#000000"),
        SocialPlatform.LinkedIn => Color.FromArgb("#0A66C2"),
        SocialPlatform.Pinterest => Color.FromArgb("#E60023"),
        _ => Color.FromArgb("#9E9E9E")
    };
}

/// <summary>
/// Worker status enum
/// </summary>
public enum WorkerStatus
{
    Idle,
    Working,
    Paused,
    Error,
    Offline
}
