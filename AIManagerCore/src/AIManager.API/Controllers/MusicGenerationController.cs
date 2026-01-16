using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services.MusicGeneration;

namespace AIManager.API.Controllers;

/// <summary>
/// API Controller สำหรับการสร้างเพลงด้วย AI (Suno AI)
/// รองรับทั้ง API mode และ Web Automation mode
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MusicGenerationController : ControllerBase
{
    private readonly ILogger<MusicGenerationController> _logger;
    private readonly MusicProviderFactory? _musicFactory;
    private readonly SunoSessionManager? _sessionManager;

    public MusicGenerationController(
        ILogger<MusicGenerationController> logger,
        MusicProviderFactory? musicFactory = null,
        SunoSessionManager? sessionManager = null)
    {
        _logger = logger;
        _musicFactory = musicFactory;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// สร้างเพลงจาก description prompt
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult<MusicGenerationResponse>> GenerateMusic(
        [FromBody] GenerateMusicRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_musicFactory == null)
            {
                return BadRequest(new MusicGenerationResponse
                {
                    Success = false,
                    Error = "Music generation service not configured"
                });
            }

            _logger.LogInformation("Generating music: {Prompt}", request.Prompt);

            var options = new MusicGenerationOptions
            {
                Genre = request.Genre,
                Mood = request.Mood,
                Style = request.Style,
                Instrumental = request.Instrumental,
                DurationSeconds = request.DurationSeconds,
                Title = request.Title,
                DownloadAll = request.DownloadAll,
                TimeoutSeconds = request.TimeoutSeconds ?? 180
            };

            var result = await _musicFactory.GenerateMusicAsync(
                request.Prompt,
                options,
                null,
                ct);

            return Ok(new MusicGenerationResponse
            {
                Success = result.Success,
                TaskId = result.TaskId,
                Status = result.Status.ToString(),
                Songs = result.Songs.Select(s => new GeneratedSongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Style = s.Style,
                    AudioUrl = s.AudioUrl,
                    ImageUrl = s.ImageUrl,
                    LocalPath = s.LocalPath,
                    DurationSeconds = s.DurationSeconds
                }).ToList(),
                CreditsUsed = result.CreditsUsed,
                CreditsRemaining = result.CreditsRemaining,
                Error = result.Error,
                DurationMs = result.Duration?.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music generation failed");
            return BadRequest(new MusicGenerationResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// สร้างเพลงจากเนื้อเพลง (Custom mode)
    /// </summary>
    [HttpPost("generate-with-lyrics")]
    public async Task<ActionResult<MusicGenerationResponse>> GenerateWithLyrics(
        [FromBody] GenerateWithLyricsRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_musicFactory == null)
            {
                return BadRequest(new MusicGenerationResponse
                {
                    Success = false,
                    Error = "Music generation service not configured"
                });
            }

            _logger.LogInformation("Generating music with lyrics, style: {Style}", request.Style);

            var options = new MusicGenerationOptions
            {
                DownloadAll = request.DownloadAll,
                TimeoutSeconds = request.TimeoutSeconds ?? 180
            };

            var result = await _musicFactory.GenerateMusicWithLyricsAsync(
                request.Lyrics,
                request.Style,
                request.Title,
                options,
                null,
                ct);

            return Ok(new MusicGenerationResponse
            {
                Success = result.Success,
                TaskId = result.TaskId,
                Status = result.Status.ToString(),
                Songs = result.Songs.Select(s => new GeneratedSongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Style = s.Style,
                    AudioUrl = s.AudioUrl,
                    ImageUrl = s.ImageUrl,
                    LocalPath = s.LocalPath,
                    DurationSeconds = s.DurationSeconds,
                    Lyrics = s.Lyrics
                }).ToList(),
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Music generation with lyrics failed");
            return BadRequest(new MusicGenerationResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// สร้างเพลง instrumental
    /// </summary>
    [HttpPost("generate-instrumental")]
    public async Task<ActionResult<MusicGenerationResponse>> GenerateInstrumental(
        [FromBody] GenerateInstrumentalRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_musicFactory == null)
            {
                return BadRequest(new MusicGenerationResponse
                {
                    Success = false,
                    Error = "Music generation service not configured"
                });
            }

            _logger.LogInformation("Generating instrumental: {Description}", request.Description);

            var result = await _musicFactory.GenerateInstrumentalAsync(
                request.Description,
                request.Style,
                null,
                null,
                ct);

            return Ok(new MusicGenerationResponse
            {
                Success = result.Success,
                TaskId = result.TaskId,
                Status = result.Status.ToString(),
                Songs = result.Songs.Select(s => new GeneratedSongDto
                {
                    Id = s.Id,
                    Title = s.Title,
                    Style = s.Style,
                    AudioUrl = s.AudioUrl,
                    ImageUrl = s.ImageUrl,
                    LocalPath = s.LocalPath,
                    DurationSeconds = s.DurationSeconds
                }).ToList(),
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instrumental generation failed");
            return BadRequest(new MusicGenerationResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ดึงสถานะ providers
    /// </summary>
    [HttpGet("providers/status")]
    public async Task<ActionResult<ProvidersStatusResponse>> GetProvidersStatus(CancellationToken ct)
    {
        try
        {
            if (_musicFactory == null)
            {
                return Ok(new ProvidersStatusResponse
                {
                    Providers = new List<ProviderStatusDto>
                    {
                        new()
                        {
                            Name = "Suno AI",
                            IsConfigured = false,
                            IsAvailable = false,
                            Message = "Music generation service not configured"
                        }
                    }
                });
            }

            var statuses = await _musicFactory.GetAllProvidersStatusAsync(ct);

            return Ok(new ProvidersStatusResponse
            {
                Providers = statuses.Select(s => new ProviderStatusDto
                {
                    Name = s.ProviderName,
                    IsConfigured = s.IsConfigured,
                    IsAvailable = s.IsAvailable,
                    CreditsRemaining = s.CreditsRemaining,
                    IsPro = s.IsPro,
                    Error = s.Error
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get providers status");
            return BadRequest(new { Error = ex.Message });
        }
    }

    /// <summary>
    /// ตั้งค่า Suno session token
    /// </summary>
    [HttpPost("suno/set-session")]
    public async Task<ActionResult<SetSessionResponse>> SetSunoSession(
        [FromBody] SetSessionRequest request,
        CancellationToken ct)
    {
        try
        {
            if (_musicFactory == null)
            {
                return BadRequest(new SetSessionResponse
                {
                    Success = false,
                    Error = "Music generation service not configured"
                });
            }

            await _musicFactory.SetSunoSessionAsync(request.Token, request.Email, ct);

            // ตรวจสอบว่า token ใช้งานได้
            var provider = await _musicFactory.GetSunoProviderAsync(ct);
            var isValid = provider != null && await provider.IsAvailableAsync(ct);

            if (isValid && provider != null)
            {
                var credits = await provider.GetCreditsInfoAsync(ct);
                return Ok(new SetSessionResponse
                {
                    Success = true,
                    Message = "Session token set successfully",
                    CreditsRemaining = credits.RemainingCredits,
                    IsPro = credits.IsPro,
                    PlanName = credits.PlanName
                });
            }
            else
            {
                return BadRequest(new SetSessionResponse
                {
                    Success = false,
                    Error = "Session token is invalid or expired"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set Suno session");
            return BadRequest(new SetSessionResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ดึงข้อมูล session ปัจจุบัน
    /// </summary>
    [HttpGet("suno/session")]
    public ActionResult<SessionInfoResponse> GetSunoSession()
    {
        if (_musicFactory == null)
        {
            return Ok(new SessionInfoResponse
            {
                IsValid = false,
                Message = "Music generation service not configured"
            });
        }

        var info = _musicFactory.GetCurrentSessionInfo();

        if (info == null)
        {
            return Ok(new SessionInfoResponse
            {
                IsValid = false,
                Message = "No session configured"
            });
        }

        return Ok(new SessionInfoResponse
        {
            IsValid = info.IsValid,
            AccountEmail = info.AccountEmail,
            ExpiresAt = info.ExpiresAt,
            TimeUntilExpiryMinutes = info.TimeUntilExpiry?.TotalMinutes
        });
    }

    /// <summary>
    /// ดึงรายการ genres ที่รองรับ
    /// </summary>
    [HttpGet("genres")]
    public async Task<ActionResult<List<string>>> GetSupportedGenres(CancellationToken ct)
    {
        if (_musicFactory == null)
        {
            return Ok(SunoApiService._defaultGenres);
        }

        var provider = await _musicFactory.GetSunoProviderAsync(ct);
        if (provider != null)
        {
            return Ok(provider.GetSupportedGenres());
        }

        return Ok(SunoApiService._defaultGenres);
    }

    /// <summary>
    /// ดึงรายการ moods ที่รองรับ
    /// </summary>
    [HttpGet("moods")]
    public async Task<ActionResult<List<string>>> GetSupportedMoods(CancellationToken ct)
    {
        if (_musicFactory == null)
        {
            return Ok(SunoApiService._defaultMoods);
        }

        var provider = await _musicFactory.GetSunoProviderAsync(ct);
        if (provider != null)
        {
            return Ok(provider.GetSupportedMoods());
        }

        return Ok(SunoApiService._defaultMoods);
    }

    /// <summary>
    /// ดาวน์โหลดเพลง
    /// </summary>
    [HttpPost("download/{songId}")]
    public async Task<ActionResult<DownloadResponse>> DownloadSong(
        string songId,
        CancellationToken ct)
    {
        try
        {
            if (_musicFactory == null)
            {
                return BadRequest(new DownloadResponse
                {
                    Success = false,
                    Error = "Music generation service not configured"
                });
            }

            var provider = await _musicFactory.GetSunoProviderAsync(ct);
            if (provider == null)
            {
                return BadRequest(new DownloadResponse
                {
                    Success = false,
                    Error = "Suno provider not available"
                });
            }

            var localPath = await provider.DownloadSongAsync(songId, null, ct);

            if (string.IsNullOrEmpty(localPath))
            {
                return BadRequest(new DownloadResponse
                {
                    Success = false,
                    Error = "Failed to download song"
                });
            }

            return Ok(new DownloadResponse
            {
                Success = true,
                LocalPath = localPath,
                FileName = Path.GetFileName(localPath)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download song: {SongId}", songId);
            return BadRequest(new DownloadResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}

#region Static Helper
public partial class SunoApiService
{
    public static readonly List<string> _defaultGenres = new()
    {
        "Pop", "Rock", "Hip Hop", "R&B", "Jazz", "Classical",
        "Electronic", "EDM", "Lo-Fi", "Ambient", "Country",
        "Folk", "Reggae", "Latin", "K-Pop", "Thai Pop",
        "Cinematic", "Orchestral", "Acoustic", "Indie"
    };

    public static readonly List<string> _defaultMoods = new()
    {
        "Happy", "Sad", "Energetic", "Calm", "Romantic",
        "Epic", "Dark", "Uplifting", "Nostalgic", "Peaceful",
        "Motivational", "Melancholic", "Dreamy", "Aggressive"
    };
}
#endregion

#region Request/Response Models

public class GenerateMusicRequest
{
    public string Prompt { get; set; } = "";
    public string? Genre { get; set; }
    public string? Mood { get; set; }
    public string? Style { get; set; }
    public bool Instrumental { get; set; }
    public int? DurationSeconds { get; set; }
    public string? Title { get; set; }
    public bool DownloadAll { get; set; } = true;
    public int? TimeoutSeconds { get; set; }
}

public class GenerateWithLyricsRequest
{
    public string Lyrics { get; set; } = "";
    public string Style { get; set; } = "";
    public string? Title { get; set; }
    public bool DownloadAll { get; set; } = true;
    public int? TimeoutSeconds { get; set; }
}

public class GenerateInstrumentalRequest
{
    public string Description { get; set; } = "";
    public string Style { get; set; } = "";
}

public class MusicGenerationResponse
{
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public string? Status { get; set; }
    public List<GeneratedSongDto> Songs { get; set; } = new();
    public int? CreditsUsed { get; set; }
    public int? CreditsRemaining { get; set; }
    public string? Error { get; set; }
    public double? DurationMs { get; set; }
}

public class GeneratedSongDto
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Style { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? Lyrics { get; set; }
    public double DurationSeconds { get; set; }
}

public class ProvidersStatusResponse
{
    public List<ProviderStatusDto> Providers { get; set; } = new();
}

public class ProviderStatusDto
{
    public string Name { get; set; } = "";
    public bool IsConfigured { get; set; }
    public bool IsAvailable { get; set; }
    public int? CreditsRemaining { get; set; }
    public bool IsPro { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class SetSessionRequest
{
    public string Token { get; set; } = "";
    public string? Email { get; set; }
}

public class SetSessionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? CreditsRemaining { get; set; }
    public bool IsPro { get; set; }
    public string? PlanName { get; set; }
    public string? Error { get; set; }
}

public class SessionInfoResponse
{
    public bool IsValid { get; set; }
    public string? AccountEmail { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double? TimeUntilExpiryMinutes { get; set; }
    public string? Message { get; set; }
}

public class DownloadResponse
{
    public bool Success { get; set; }
    public string? LocalPath { get; set; }
    public string? FileName { get; set; }
    public string? Error { get; set; }
}

#endregion
