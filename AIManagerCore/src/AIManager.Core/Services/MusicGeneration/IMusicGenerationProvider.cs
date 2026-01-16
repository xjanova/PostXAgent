using AIManager.Core.Models;

namespace AIManager.Core.Services.MusicGeneration;

/// <summary>
/// Interface สำหรับ Music Generation Providers
/// รองรับ Suno AI และ providers อื่นๆ ในอนาคต
/// </summary>
public interface IMusicGenerationProvider
{
    /// <summary>
    /// ชื่อ provider
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// ตรวจสอบว่า provider พร้อมใช้งานหรือไม่
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// ตรวจสอบ credits ที่เหลือ
    /// </summary>
    Task<MusicCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// สร้างเพลงจาก description prompt
    /// </summary>
    Task<MusicGenerationResult> GenerateFromPromptAsync(
        string prompt,
        MusicGenerationOptions options,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// สร้างเพลงจากเนื้อเพลง (Custom mode)
    /// </summary>
    Task<MusicGenerationResult> GenerateFromLyricsAsync(
        string lyrics,
        string style,
        string? title = null,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// สร้างเพลง instrumental
    /// </summary>
    Task<MusicGenerationResult> GenerateInstrumentalAsync(
        string description,
        string style,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// ดึงสถานะของ generation task
    /// </summary>
    Task<MusicGenerationResult> GetGenerationStatusAsync(
        string taskId,
        CancellationToken ct = default);

    /// <summary>
    /// ดาวน์โหลดเพลง
    /// </summary>
    Task<string?> DownloadSongAsync(
        string songId,
        string? outputPath = null,
        CancellationToken ct = default);

    /// <summary>
    /// ดึงรายการ genres ที่รองรับ
    /// </summary>
    IReadOnlyList<string> GetSupportedGenres();

    /// <summary>
    /// ดึงรายการ moods ที่รองรับ
    /// </summary>
    IReadOnlyList<string> GetSupportedMoods();
}

/// <summary>
/// ผลลัพธ์จากการสร้างเพลง
/// </summary>
public class MusicGenerationResult
{
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public MusicGenerationStatus Status { get; set; }
    public List<GeneratedSong> Songs { get; set; } = new();
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public int? ProgressPercent { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? CreditsUsed { get; set; }
    public int? CreditsRemaining { get; set; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// ข้อมูลเพลงที่สร้าง
/// </summary>
public class GeneratedSong
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Style { get; set; }
    public string? AudioUrl { get; set; }
    public string? StreamUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? Lyrics { get; set; }
    public double DurationSeconds { get; set; }
    public string? ModelName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// สถานะการสร้างเพลง
/// </summary>
public enum MusicGenerationStatus
{
    Queued,
    Processing,
    Streaming,  // เพลงกำลัง stream (Suno)
    Completed,
    Failed,
    Cancelled,
    Timeout
}

/// <summary>
/// ความคืบหน้าในการสร้างเพลง
/// </summary>
public class MusicGenerationProgress
{
    public int PercentComplete { get; set; }
    public string? Stage { get; set; }
    public string? Message { get; set; }
    public int? CurrentSong { get; set; }
    public int? TotalSongs { get; set; }
}

/// <summary>
/// ตัวเลือกในการสร้างเพลง
/// </summary>
public class MusicGenerationOptions
{
    /// <summary>
    /// Genre/ประเภทเพลง
    /// </summary>
    public string? Genre { get; set; }

    /// <summary>
    /// อารมณ์ของเพลง
    /// </summary>
    public string? Mood { get; set; }

    /// <summary>
    /// สไตล์เพิ่มเติม (comma separated)
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// เป็นเพลง instrumental (ไม่มีเสียงร้อง)
    /// </summary>
    public bool Instrumental { get; set; } = false;

    /// <summary>
    /// ความยาวเพลง (วินาที)
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    /// จำนวนเพลงที่ต้องการสร้าง (Suno สร้าง 2 versions)
    /// </summary>
    public int NumberOfSongs { get; set; } = 2;

    /// <summary>
    /// ชื่อเพลง (สำหรับ custom mode)
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// ดาวน์โหลดทุกเพลงที่สร้าง
    /// </summary>
    public bool DownloadAll { get; set; } = true;

    /// <summary>
    /// Path สำหรับบันทึกไฟล์
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// เวลารอสูงสุด (วินาที)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// ใช้ model version ล่าสุด
    /// </summary>
    public bool UseLatestModel { get; set; } = true;
}

/// <summary>
/// ข้อมูล credits
/// </summary>
public class MusicCreditsInfo
{
    public bool HasCredits { get; set; }
    public int? RemainingCredits { get; set; }
    public int? TotalCredits { get; set; }
    public int? UsedToday { get; set; }
    public int? DailyLimit { get; set; }
    public DateTime? ResetsAt { get; set; }
    public string? PlanName { get; set; }
    public bool IsPro { get; set; }
    public bool IsUnlimited { get; set; }
}
