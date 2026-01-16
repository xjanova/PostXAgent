using AIManager.Core.Models;

namespace AIManager.Core.Configuration;

/// <summary>
/// Configuration settings สำหรับ Video Generation
/// </summary>
public class VideoGenerationSettings
{
    public const string SectionName = "VideoGeneration";

    /// <summary>
    /// Provider configurations
    /// </summary>
    public VideoProvidersSettings Providers { get; set; } = new();

    /// <summary>
    /// Path สำหรับบันทึกไฟล์ที่ดาวน์โหลด
    /// </summary>
    public string? DownloadPath { get; set; }

    /// <summary>
    /// เปิดใช้ fallback อัตโนมัติ
    /// </summary>
    public bool EnableAutoFallback { get; set; } = true;

    /// <summary>
    /// Provider เริ่มต้น
    /// </summary>
    public string DefaultProvider { get; set; } = "Freepik";

    /// <summary>
    /// ลำดับ priority ของ providers
    /// </summary>
    public string[] PriorityOrder { get; set; } = { "Freepik", "Runway", "PikaLabs", "LumaAI" };

    /// <summary>
    /// จำนวน credits ขั้นต่ำก่อนเตือน
    /// </summary>
    public int LowCreditsWarningThreshold { get; set; } = 10;

    /// <summary>
    /// แปลง PriorityOrder เป็น SocialPlatform array
    /// </summary>
    public SocialPlatform[] GetPriorityPlatforms()
    {
        var platforms = new List<SocialPlatform>();
        foreach (var name in PriorityOrder)
        {
            if (Enum.TryParse<SocialPlatform>(name, true, out var platform))
            {
                platforms.Add(platform);
            }
        }
        return platforms.ToArray();
    }
}

/// <summary>
/// Provider settings รวม
/// </summary>
public class VideoProvidersSettings
{
    public RunwayProviderSettings Runway { get; set; } = new();
    public PikaLabsProviderSettings PikaLabs { get; set; } = new();
    public LumaAIProviderSettings LumaAI { get; set; } = new();
    public FreepikProviderSettings Freepik { get; set; } = new();
}

/// <summary>
/// Runway ML settings
/// </summary>
public class RunwayProviderSettings
{
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Pika Labs settings
/// </summary>
public class PikaLabsProviderSettings
{
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Luma AI settings
/// </summary>
public class LumaAIProviderSettings
{
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Freepik settings
/// </summary>
public class FreepikProviderSettings
{
    public bool Enabled { get; set; } = true;
    public bool UseWebAutomation { get; set; } = true;
    public bool PreferUnlimitedModels { get; set; } = true;
}

/// <summary>
/// Configuration settings สำหรับ Music Generation
/// </summary>
public class MusicGenerationSettings
{
    public const string SectionName = "MusicGeneration";

    /// <summary>
    /// Provider configurations
    /// </summary>
    public MusicProvidersSettings Providers { get; set; } = new();

    /// <summary>
    /// Path สำหรับบันทึกไฟล์ที่ดาวน์โหลด
    /// </summary>
    public string? DownloadPath { get; set; }

    /// <summary>
    /// Genre เริ่มต้น
    /// </summary>
    public string DefaultGenre { get; set; } = "Pop";

    /// <summary>
    /// Mood เริ่มต้น
    /// </summary>
    public string DefaultMood { get; set; } = "Energetic";
}

/// <summary>
/// Music provider settings
/// </summary>
public class MusicProvidersSettings
{
    public SunoProviderSettings Suno { get; set; } = new();
}

/// <summary>
/// Suno AI settings
/// </summary>
public class SunoProviderSettings
{
    public bool Enabled { get; set; } = true;
    public bool UseWebAutomation { get; set; } = true;
}
