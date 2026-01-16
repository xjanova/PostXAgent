using AIManager.Core.Models;

namespace AIManager.Core.Services.VideoGeneration;

/// <summary>
/// Interface สำหรับ Video Generation Providers ทั้งหมด
/// รองรับ Freepik, Runway ML, Pika Labs, Luma AI
/// </summary>
public interface IVideoGenerationProvider
{
    /// <summary>
    /// ชื่อ provider
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Platform enum
    /// </summary>
    SocialPlatform Platform { get; }

    /// <summary>
    /// ตรวจสอบว่า provider พร้อมใช้งานหรือไม่
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// ตรวจสอบ credits/quota ที่เหลือ
    /// </summary>
    Task<ProviderCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// สร้างวิดีโอจาก text prompt (Text-to-Video)
    /// </summary>
    Task<VideoGenerationResult> GenerateFromTextAsync(
        string prompt,
        VideoGenerationConfig config,
        IProgress<VideoGenerationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// สร้างวิดีโอจากรูปภาพ (Image-to-Video)
    /// </summary>
    Task<VideoGenerationResult> GenerateFromImageAsync(
        string imagePathOrUrl,
        string? motionPrompt,
        VideoGenerationConfig config,
        IProgress<VideoGenerationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// ดึงสถานะของ generation task
    /// </summary>
    Task<VideoGenerationResult> GetGenerationStatusAsync(
        string taskId,
        CancellationToken ct = default);

    /// <summary>
    /// ยกเลิก generation task
    /// </summary>
    Task<bool> CancelGenerationAsync(
        string taskId,
        CancellationToken ct = default);

    /// <summary>
    /// ดึงรายการ models ที่รองรับ
    /// </summary>
    Task<IReadOnlyList<VideoModelInfo>> GetAvailableModelsAsync(CancellationToken ct = default);
}

/// <summary>
/// ผลลัพธ์จากการสร้างวิดีโอ
/// </summary>
public class VideoGenerationResult
{
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public VideoGenerationStatus Status { get; set; }
    public string? VideoUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public int? ProgressPercent { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
    public VideoMetadata? Metadata { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public SocialPlatform Provider { get; set; }
    public Dictionary<string, object>? Extra { get; set; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// สถานะการสร้างวิดีโอ
/// </summary>
public enum VideoGenerationStatus
{
    Queued,
    Processing,
    Completed,
    Failed,
    Cancelled,
    Timeout
}

/// <summary>
/// ความคืบหน้าในการสร้างวิดีโอ
/// </summary>
public class VideoGenerationProgress
{
    public int PercentComplete { get; set; }
    public string? Stage { get; set; }
    public string? Message { get; set; }
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// ข้อมูล credits/quota ของ provider
/// </summary>
public class ProviderCreditsInfo
{
    public bool HasCredits { get; set; }
    public int? RemainingCredits { get; set; }
    public int? TotalCredits { get; set; }
    public int? UsedToday { get; set; }
    public int? DailyLimit { get; set; }
    public DateTime? ResetsAt { get; set; }
    public string? PlanName { get; set; }
    public bool IsUnlimited { get; set; }
}

/// <summary>
/// ข้อมูล model ที่รองรับ
/// </summary>
public class VideoModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool SupportsTextToVideo { get; set; }
    public bool SupportsImageToVideo { get; set; }
    public int? MaxDurationSeconds { get; set; }
    public IReadOnlyList<AspectRatio>? SupportedAspectRatios { get; set; }
    public IReadOnlyList<VideoQuality>? SupportedQualities { get; set; }
    public int? CreditsPerGeneration { get; set; }
    public bool IsFree { get; set; }
}
