using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.VideoGeneration;

/// <summary>
/// Factory สำหรับสร้างและจัดการ Video Generation Providers
/// รองรับการเลือก provider ตาม priority และ fallback อัตโนมัติ
/// </summary>
public class VideoProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly VideoProviderConfig _config;
    private readonly Dictionary<SocialPlatform, IVideoGenerationProvider> _providers = new();
    private readonly object _lock = new();

    /// <summary>
    /// ลำดับความสำคัญของ providers (PRIMARY ก่อน)
    /// </summary>
    public static readonly SocialPlatform[] DefaultPriorityOrder = new[]
    {
        SocialPlatform.Freepik,   // PRIMARY - Free unlimited models
        SocialPlatform.Runway,    // Fallback 1 - High quality
        SocialPlatform.PikaLabs,  // Fallback 2 - Creative styles
        SocialPlatform.LumaAI     // Fallback 3 - Realistic motion
    };

    public VideoProviderFactory(ILoggerFactory loggerFactory, VideoProviderConfig config)
    {
        _loggerFactory = loggerFactory;
        _config = config;
    }

    /// <summary>
    /// ดึง provider ตาม platform
    /// </summary>
    public IVideoGenerationProvider? GetProvider(SocialPlatform platform)
    {
        lock (_lock)
        {
            if (_providers.TryGetValue(platform, out var existing))
                return existing;

            var provider = CreateProvider(platform);
            if (provider != null)
            {
                _providers[platform] = provider;
            }
            return provider;
        }
    }

    /// <summary>
    /// ดึง provider ที่พร้อมใช้งาน (ตาม priority order)
    /// </summary>
    public async Task<IVideoGenerationProvider?> GetAvailableProviderAsync(
        SocialPlatform[]? priorityOrder = null,
        CancellationToken ct = default)
    {
        var order = priorityOrder ?? DefaultPriorityOrder;

        foreach (var platform in order)
        {
            var provider = GetProvider(platform);
            if (provider != null)
            {
                try
                {
                    if (await provider.IsAvailableAsync(ct))
                    {
                        return provider;
                    }
                }
                catch
                {
                    // Skip unavailable provider
                }
            }
        }

        return null;
    }

    /// <summary>
    /// ดึง provider ที่มี credits เหลือ
    /// </summary>
    public async Task<IVideoGenerationProvider?> GetProviderWithCreditsAsync(
        int minimumCredits = 1,
        SocialPlatform[]? priorityOrder = null,
        CancellationToken ct = default)
    {
        var order = priorityOrder ?? DefaultPriorityOrder;

        foreach (var platform in order)
        {
            var provider = GetProvider(platform);
            if (provider != null)
            {
                try
                {
                    var creditsInfo = await provider.GetCreditsInfoAsync(ct);
                    if (creditsInfo.IsUnlimited ||
                        (creditsInfo.HasCredits && (creditsInfo.RemainingCredits ?? 0) >= minimumCredits))
                    {
                        return provider;
                    }
                }
                catch
                {
                    // Skip provider with error
                }
            }
        }

        return null;
    }

    /// <summary>
    /// ดึง providers ทั้งหมดที่พร้อมใช้งาน
    /// </summary>
    public async Task<IReadOnlyList<IVideoGenerationProvider>> GetAllAvailableProvidersAsync(
        CancellationToken ct = default)
    {
        var available = new List<IVideoGenerationProvider>();

        foreach (var platform in DefaultPriorityOrder)
        {
            var provider = GetProvider(platform);
            if (provider != null)
            {
                try
                {
                    if (await provider.IsAvailableAsync(ct))
                    {
                        available.Add(provider);
                    }
                }
                catch
                {
                    // Skip
                }
            }
        }

        return available;
    }

    /// <summary>
    /// ดึงข้อมูล credits ของทุก providers
    /// </summary>
    public async Task<Dictionary<SocialPlatform, ProviderCreditsInfo>> GetAllCreditsInfoAsync(
        CancellationToken ct = default)
    {
        var result = new Dictionary<SocialPlatform, ProviderCreditsInfo>();

        foreach (var platform in DefaultPriorityOrder)
        {
            var provider = GetProvider(platform);
            if (provider != null)
            {
                try
                {
                    var credits = await provider.GetCreditsInfoAsync(ct);
                    result[platform] = credits;
                }
                catch
                {
                    result[platform] = new ProviderCreditsInfo { HasCredits = false };
                }
            }
        }

        return result;
    }

    /// <summary>
    /// สร้างวิดีโอโดยใช้ fallback providers อัตโนมัติ
    /// </summary>
    public async Task<VideoGenerationResult> GenerateWithFallbackAsync(
        string prompt,
        VideoGenerationConfig config,
        SocialPlatform[]? priorityOrder = null,
        IProgress<VideoGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var order = priorityOrder ?? DefaultPriorityOrder;
        var errors = new List<string>();

        foreach (var platform in order)
        {
            var provider = GetProvider(platform);
            if (provider == null) continue;

            try
            {
                // Check availability
                if (!await provider.IsAvailableAsync(ct))
                {
                    errors.Add($"{provider.ProviderName}: Not available");
                    continue;
                }

                // Check credits
                var credits = await provider.GetCreditsInfoAsync(ct);
                if (!credits.IsUnlimited && !credits.HasCredits)
                {
                    errors.Add($"{provider.ProviderName}: No credits remaining");
                    continue;
                }

                progress?.Report(new VideoGenerationProgress
                {
                    Stage = "Provider Selection",
                    Message = $"Using {provider.ProviderName}...",
                    PercentComplete = 5
                });

                // Generate
                var result = config.Mode == VideoGenerationMode.ImageToVideo
                    ? await provider.GenerateFromImageAsync(
                        config.SourceImageUrl ?? "",
                        prompt,
                        config,
                        progress,
                        ct)
                    : await provider.GenerateFromTextAsync(
                        prompt,
                        config,
                        progress,
                        ct);

                if (result.Success)
                {
                    return result;
                }

                errors.Add($"{provider.ProviderName}: {result.Error}");
            }
            catch (Exception ex)
            {
                errors.Add($"{provider.ProviderName}: {ex.Message}");
            }
        }

        // All providers failed
        return new VideoGenerationResult
        {
            Success = false,
            Status = VideoGenerationStatus.Failed,
            Error = $"All providers failed: {string.Join("; ", errors)}"
        };
    }

    #region Private Methods

    private IVideoGenerationProvider? CreateProvider(SocialPlatform platform)
    {
        var logger = _loggerFactory;

        return platform switch
        {
            SocialPlatform.Runway when !string.IsNullOrEmpty(_config.RunwayApiKey) =>
                new RunwayMLService(
                    logger.CreateLogger<RunwayMLService>(),
                    _config.RunwayApiKey,
                    null,
                    _config.DownloadPath),

            SocialPlatform.PikaLabs when !string.IsNullOrEmpty(_config.PikaLabsApiKey) =>
                new PikaLabsService(
                    logger.CreateLogger<PikaLabsService>(),
                    _config.PikaLabsApiKey,
                    null,
                    _config.DownloadPath),

            SocialPlatform.LumaAI when !string.IsNullOrEmpty(_config.LumaAIApiKey) =>
                new LumaAIService(
                    logger.CreateLogger<LumaAIService>(),
                    _config.LumaAIApiKey,
                    null,
                    _config.DownloadPath),

            // Freepik uses web automation, not API
            // SocialPlatform.Freepik => new FreepikAutomationService(...)

            _ => null
        };
    }

    #endregion
}

/// <summary>
/// Configuration สำหรับ Video Providers
/// </summary>
public class VideoProviderConfig
{
    /// <summary>
    /// Runway ML API Key
    /// </summary>
    public string? RunwayApiKey { get; set; }

    /// <summary>
    /// Pika Labs API Key
    /// </summary>
    public string? PikaLabsApiKey { get; set; }

    /// <summary>
    /// Luma AI API Key
    /// </summary>
    public string? LumaAIApiKey { get; set; }

    /// <summary>
    /// Path สำหรับบันทึกไฟล์ที่ดาวน์โหลด
    /// </summary>
    public string? DownloadPath { get; set; }

    /// <summary>
    /// ลำดับ priority ของ providers (ถ้าไม่กำหนดจะใช้ค่าเริ่มต้น)
    /// </summary>
    public SocialPlatform[]? PriorityOrder { get; set; }

    /// <summary>
    /// เปิดใช้ fallback อัตโนมัติเมื่อ provider หลักไม่พร้อม
    /// </summary>
    public bool EnableAutoFallback { get; set; } = true;

    /// <summary>
    /// จำนวน credits ขั้นต่ำก่อนเตือน
    /// </summary>
    public int LowCreditsWarningThreshold { get; set; } = 10;
}

/// <summary>
/// Extension methods สำหรับ DI registration
/// </summary>
public static class VideoProviderExtensions
{
    /// <summary>
    /// ตรวจสอบว่า platform เป็น video generation provider หรือไม่
    /// </summary>
    public static bool IsVideoProvider(this SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Freepik => true,
            SocialPlatform.Runway => true,
            SocialPlatform.PikaLabs => true,
            SocialPlatform.LumaAI => true,
            _ => false
        };
    }

    /// <summary>
    /// ดึงชื่อ provider
    /// </summary>
    public static string GetProviderDisplayName(this SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Freepik => "Freepik Pikaso AI",
            SocialPlatform.Runway => "Runway ML Gen-3",
            SocialPlatform.PikaLabs => "Pika Labs",
            SocialPlatform.LumaAI => "Luma AI Dream Machine",
            _ => platform.ToString()
        };
    }

    /// <summary>
    /// ดึง max duration ที่รองรับ (วินาที)
    /// </summary>
    public static int GetMaxDurationSeconds(this SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Freepik => 5,
            SocialPlatform.Runway => 10,
            SocialPlatform.PikaLabs => 4,
            SocialPlatform.LumaAI => 5,
            _ => 5
        };
    }
}
