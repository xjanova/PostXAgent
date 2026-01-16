using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.MusicGeneration;

/// <summary>
/// Factory สำหรับจัดการ Music Generation Providers
/// </summary>
public class MusicProviderFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MusicProviderConfig _config;
    private readonly SunoSessionManager _sessionManager;
    private SunoApiService? _sunoService;
    private readonly object _lock = new();

    public MusicProviderFactory(
        ILoggerFactory loggerFactory,
        MusicProviderConfig config,
        SunoSessionManager? sessionManager = null)
    {
        _loggerFactory = loggerFactory;
        _config = config;
        _sessionManager = sessionManager ?? new SunoSessionManager(
            loggerFactory.CreateLogger<SunoSessionManager>());
    }

    /// <summary>
    /// ดึง Suno provider
    /// </summary>
    public async Task<IMusicGenerationProvider?> GetSunoProviderAsync(
        CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_sunoService != null)
                return _sunoService;
        }

        // ดึง session token
        var token = await _sessionManager.GetValidTokenAsync(ct);

        if (string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(_config.SunoSessionToken))
        {
            token = _config.SunoSessionToken;
        }

        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        lock (_lock)
        {
            _sunoService = new SunoApiService(
                _loggerFactory.CreateLogger<SunoApiService>(),
                token,
                null,
                _config.DownloadPath);

            return _sunoService;
        }
    }

    /// <summary>
    /// ดึง provider ที่พร้อมใช้งาน
    /// </summary>
    public async Task<IMusicGenerationProvider?> GetAvailableProviderAsync(
        CancellationToken ct = default)
    {
        // ตอนนี้มีแค่ Suno แต่ในอนาคตอาจมี provider อื่น
        var suno = await GetSunoProviderAsync(ct);
        if (suno != null && await suno.IsAvailableAsync(ct))
        {
            return suno;
        }

        return null;
    }

    /// <summary>
    /// ตั้งค่า Suno session token
    /// </summary>
    public async Task SetSunoSessionAsync(
        string token,
        string? email = null,
        CancellationToken ct = default)
    {
        await _sessionManager.SaveTokenAsync(token, email, ct: ct);

        // Reset cached service
        lock (_lock) { _sunoService = null; }
    }

    /// <summary>
    /// ดึงข้อมูล session ปัจจุบัน
    /// </summary>
    public SunoSessionInfo? GetCurrentSessionInfo()
    {
        return _sessionManager.GetCurrentSessionInfo();
    }

    /// <summary>
    /// ตรวจสอบสถานะ providers ทั้งหมด
    /// </summary>
    public async Task<List<MusicProviderStatusInfo>> GetAllProvidersStatusAsync(
        CancellationToken ct = default)
    {
        var statuses = new List<MusicProviderStatusInfo>();

        // Check Suno
        var suno = await GetSunoProviderAsync(ct);
        var sunoStatus = new MusicProviderStatusInfo
        {
            ProviderName = "Suno AI",
            IsConfigured = suno != null,
            IsAvailable = suno != null && await suno.IsAvailableAsync(ct)
        };

        if (sunoStatus.IsAvailable && suno != null)
        {
            var credits = await suno.GetCreditsInfoAsync(ct);
            sunoStatus.CreditsRemaining = credits.RemainingCredits;
            sunoStatus.IsPro = credits.IsPro;
        }

        statuses.Add(sunoStatus);

        return statuses;
    }

    /// <summary>
    /// สร้างเพลงโดยใช้ provider ที่พร้อมใช้งาน
    /// </summary>
    public async Task<MusicGenerationResult> GenerateMusicAsync(
        string prompt,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        options ??= new MusicGenerationOptions();

        var provider = await GetAvailableProviderAsync(ct);
        if (provider == null)
        {
            return new MusicGenerationResult
            {
                Success = false,
                Status = MusicGenerationStatus.Failed,
                Error = "No music provider available. Please configure Suno session token."
            };
        }

        return await provider.GenerateFromPromptAsync(prompt, options, progress, ct);
    }

    /// <summary>
    /// สร้างเพลงจากเนื้อเพลง
    /// </summary>
    public async Task<MusicGenerationResult> GenerateMusicWithLyricsAsync(
        string lyrics,
        string style,
        string? title = null,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var provider = await GetAvailableProviderAsync(ct);
        if (provider == null)
        {
            return new MusicGenerationResult
            {
                Success = false,
                Status = MusicGenerationStatus.Failed,
                Error = "No music provider available"
            };
        }

        return await provider.GenerateFromLyricsAsync(lyrics, style, title, options, progress, ct);
    }

    /// <summary>
    /// สร้างเพลง instrumental
    /// </summary>
    public async Task<MusicGenerationResult> GenerateInstrumentalAsync(
        string description,
        string style,
        MusicGenerationOptions? options = null,
        IProgress<MusicGenerationProgress>? progress = null,
        CancellationToken ct = default)
    {
        var provider = await GetAvailableProviderAsync(ct);
        if (provider == null)
        {
            return new MusicGenerationResult
            {
                Success = false,
                Status = MusicGenerationStatus.Failed,
                Error = "No music provider available"
            };
        }

        return await provider.GenerateInstrumentalAsync(description, style, options, progress, ct);
    }
}

/// <summary>
/// Configuration สำหรับ Music Providers
/// </summary>
public class MusicProviderConfig
{
    /// <summary>
    /// Suno session token (จาก browser cookies)
    /// </summary>
    public string? SunoSessionToken { get; set; }

    /// <summary>
    /// Path สำหรับบันทึกไฟล์เพลง
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

    /// <summary>
    /// ดาวน์โหลดทุกเพลงที่สร้าง
    /// </summary>
    public bool DownloadAllSongs { get; set; } = true;

    /// <summary>
    /// เวลารอสูงสุด (วินาที)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;
}

/// <summary>
/// สถานะของ Music Provider
/// </summary>
public class MusicProviderStatusInfo
{
    public string ProviderName { get; set; } = "";
    public bool IsConfigured { get; set; }
    public bool IsAvailable { get; set; }
    public int? CreditsRemaining { get; set; }
    public bool IsPro { get; set; }
    public string? Error { get; set; }
}
