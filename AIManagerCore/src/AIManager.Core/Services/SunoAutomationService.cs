using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// SunoAutomationService - ระบบ automation สำหรับ Suno AI Music Generation
/// สร้างเพลงผ่าน web automation
/// </summary>
public class SunoAutomationService
{
    private readonly ILogger<SunoAutomationService> _logger;
    private readonly BrowserController _browserController;
    private readonly WorkflowExecutor _workflowExecutor;
    private readonly WorkflowStorage _workflowStorage;
    private readonly string _downloadPath;

    // Suno URLs
    private const string SUNO_HOME = "https://suno.com";
    private const string SUNO_CREATE = "https://suno.com/create";
    private const string SUNO_LIBRARY = "https://suno.com/library";

    // Music genres/styles ที่รองรับ
    public static readonly IReadOnlyList<string> SupportedGenres = new List<string>
    {
        "Pop", "Rock", "Hip Hop", "R&B", "Jazz", "Classical",
        "Electronic", "EDM", "House", "Lo-Fi", "Ambient",
        "Country", "Folk", "Reggae", "Latin", "K-Pop",
        "Thai Pop", "Thai Traditional", "Luk Thung", "Mor Lam",
        "Cinematic", "Orchestral", "Acoustic", "Indie"
    };

    // Moods ที่รองรับ
    public static readonly IReadOnlyList<string> SupportedMoods = new List<string>
    {
        "Happy", "Sad", "Energetic", "Calm", "Romantic",
        "Epic", "Dark", "Uplifting", "Nostalgic", "Peaceful",
        "Motivational", "Melancholic", "Dreamy", "Aggressive"
    };

    public SunoAutomationService(
        ILogger<SunoAutomationService> logger,
        BrowserController browserController,
        WorkflowExecutor workflowExecutor,
        WorkflowStorage workflowStorage,
        string? downloadPath = null)
    {
        _logger = logger;
        _browserController = browserController;
        _workflowExecutor = workflowExecutor;
        _workflowStorage = workflowStorage;
        _downloadPath = downloadPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PostXAgent", "Downloads", "Suno");

        Directory.CreateDirectory(_downloadPath);
    }

    #region Public Methods

    /// <summary>
    /// สร้างเพลงจาก prompt/description
    /// </summary>
    public async Task<SunoGenerationResult> GenerateMusicAsync(
        string prompt,
        SunoMusicOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new SunoGenerationResult
        {
            Prompt = prompt,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting Suno music generation: {Prompt}", prompt);
            options ??= new SunoMusicOptions();

            // 1. ตรวจสอบ browser และ login
            if (!await EnsureBrowserReadyAsync(ct))
            {
                result.Success = false;
                result.Error = "Failed to initialize browser";
                return result;
            }

            if (!await EnsureLoggedInAsync(ct))
            {
                result.Success = false;
                result.Error = "Failed to login to Suno";
                return result;
            }

            // 2. ไปที่หน้า Create
            await _browserController.NavigateAsync(SUNO_CREATE, ct);
            await Task.Delay(3000, ct);

            // 3. เปิดโหมด Custom ถ้าต้องการควบคุมมากขึ้น
            if (options.UseCustomMode)
            {
                await EnableCustomModeAsync(ct);
                await Task.Delay(1000, ct);
            }

            // 4. พิมพ์ prompt/lyrics
            await TypeSongPromptAsync(prompt, options.UseCustomMode, ct);
            await Task.Delay(500, ct);

            // 5. ระบุ style (ถ้าอยู่ใน custom mode)
            if (options.UseCustomMode && !string.IsNullOrEmpty(options.Style))
            {
                await TypeStyleAsync(options.Style, ct);
            }

            // 6. ตั้งชื่อเพลง (ถ้าต้องการ)
            if (options.UseCustomMode && !string.IsNullOrEmpty(options.Title))
            {
                await TypeTitleAsync(options.Title, ct);
            }

            // 7. กด Create
            await ClickCreateButtonAsync(ct);

            // 8. รอจนเพลงเสร็จ (Suno สร้าง 2 versions)
            var songs = await WaitForMusicGenerationCompleteAsync(
                TimeSpan.FromMinutes(options.MaxWaitMinutes), ct);

            if (songs.Count == 0)
            {
                result.Success = false;
                result.Error = "Music generation timed out or failed";
                return result;
            }

            result.GeneratedSongs = songs;

            // 9. ดาวน์โหลดเพลงที่ดีที่สุด (หรือทั้งหมด)
            foreach (var song in songs)
            {
                if (options.DownloadAll || song == songs[0])
                {
                    var localPath = await DownloadSongAsync(song.AudioUrl, song.Title, ct);
                    if (!string.IsNullOrEmpty(localPath))
                    {
                        song.LocalPath = localPath;
                    }
                }
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Music generated successfully: {Count} songs", songs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate music");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// สร้างเพลงด้วยเนื้อเพลง (lyrics)
    /// </summary>
    public async Task<SunoGenerationResult> GenerateMusicWithLyricsAsync(
        string lyrics,
        string style,
        string? title = null,
        CancellationToken ct = default)
    {
        var options = new SunoMusicOptions
        {
            UseCustomMode = true,
            Style = style,
            Title = title ?? $"Song_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        return await GenerateMusicAsync(lyrics, options, ct);
    }

    /// <summary>
    /// สร้างเพลงแบบ instrumental (ไม่มีเนื้อร้อง)
    /// </summary>
    public async Task<SunoGenerationResult> GenerateInstrumentalAsync(
        string description,
        string style,
        CancellationToken ct = default)
    {
        var options = new SunoMusicOptions
        {
            UseCustomMode = true,
            Style = $"{style}, instrumental",
            IsInstrumental = true
        };

        return await GenerateMusicAsync(description, options, ct);
    }

    /// <summary>
    /// ดึงรายการเพลงจาก library
    /// </summary>
    public async Task<List<SunoSong>> GetLibrarySongsAsync(int limit = 20, CancellationToken ct = default)
    {
        var songs = new List<SunoSong>();

        try
        {
            if (!await EnsureBrowserReadyAsync(ct) || !await EnsureLoggedInAsync(ct))
            {
                return songs;
            }

            await _browserController.NavigateAsync(SUNO_LIBRARY, ct);
            await Task.Delay(3000, ct);

            var script = $@"
                (function() {{
                    var songs = [];
                    var songElements = document.querySelectorAll('[data-song-id], .song-card, .track-item');

                    for (var i = 0; i < Math.min(songElements.length, {limit}); i++) {{
                        var el = songElements[i];
                        var song = {{
                            id: el.getAttribute('data-song-id') || i.toString(),
                            title: el.querySelector('.title, .song-title')?.textContent?.trim() || 'Unknown',
                            duration: el.querySelector('.duration')?.textContent?.trim() || '',
                            imageUrl: el.querySelector('img')?.src || ''
                        }};
                        songs.push(song);
                    }}

                    return JSON.stringify(songs);
                }})();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);
            if (!string.IsNullOrEmpty(result))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<SunoSong>>(result);
                if (parsed != null)
                {
                    songs.AddRange(parsed);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get library songs");
        }

        return songs;
    }

    /// <summary>
    /// ตรวจสอบจำนวน credits ที่เหลือ
    /// </summary>
    public async Task<SunoCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default)
    {
        var info = new SunoCreditsInfo();

        try
        {
            if (!await EnsureBrowserReadyAsync(ct) || !await EnsureLoggedInAsync(ct))
            {
                info.Error = "Not logged in";
                return info;
            }

            var script = @"
                (function() {
                    // หา credits display
                    var creditsEl = document.querySelector('[data-credits], .credits-display, span:contains(""credits"")');
                    if (creditsEl) {
                        var text = creditsEl.textContent || '';
                        var match = text.match(/(\d+)/);
                        if (match) {
                            return JSON.stringify({
                                credits: parseInt(match[1]),
                                isPro: text.toLowerCase().includes('pro'),
                                raw: text
                            });
                        }
                    }

                    // ลองหาจาก profile menu
                    var profileMenu = document.querySelector('.user-menu, .profile-section');
                    if (profileMenu) {
                        return JSON.stringify({
                            credits: -1,
                            message: 'Click profile to see credits'
                        });
                    }

                    return JSON.stringify({ credits: -1, message: 'Credits not visible' });
                })();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);
            if (!string.IsNullOrEmpty(result))
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<SunoCreditsInfo>(result);
                if (parsed != null)
                {
                    info = parsed;
                    info.CheckedAt = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get credits info");
            info.Error = ex.Message;
        }

        return info;
    }

    /// <summary>
    /// Extend เพลงที่มีอยู่
    /// </summary>
    public async Task<SunoGenerationResult> ExtendSongAsync(
        string songId,
        string? additionalLyrics = null,
        CancellationToken ct = default)
    {
        var result = new SunoGenerationResult
        {
            Prompt = $"Extend song: {songId}",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Extending song: {SongId}", songId);

            // 1. ไปที่ library และเลือกเพลง
            await _browserController.NavigateAsync($"{SUNO_HOME}/song/{songId}", ct);
            await Task.Delay(3000, ct);

            // 2. คลิก Extend
            await ClickExtendButtonAsync(ct);
            await Task.Delay(1000, ct);

            // 3. เพิ่ม lyrics ถ้ามี
            if (!string.IsNullOrEmpty(additionalLyrics))
            {
                await TypeSongPromptAsync(additionalLyrics, true, ct);
            }

            // 4. กด Create
            await ClickCreateButtonAsync(ct);

            // 5. รอจนเสร็จ
            var songs = await WaitForMusicGenerationCompleteAsync(TimeSpan.FromMinutes(3), ct);

            if (songs.Count > 0)
            {
                result.GeneratedSongs = songs;
                result.Success = true;
            }
            else
            {
                result.Success = false;
                result.Error = "Extension failed";
            }

            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extend song");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    #endregion

    #region Private Methods - Browser Actions

    private async Task<bool> EnsureBrowserReadyAsync(CancellationToken ct)
    {
        try
        {
            if (_browserController.CurrentUrl == null)
            {
                await _browserController.InitializeAsync(ct);
                await _browserController.LaunchAsync(ct);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure browser ready");
            return false;
        }
    }

    private async Task<bool> EnsureLoggedInAsync(CancellationToken ct)
    {
        try
        {
            await _browserController.NavigateAsync(SUNO_HOME, ct);
            await Task.Delay(2000, ct);

            var script = @"
                (function() {
                    // ตรวจสอบว่า login แล้วหรือยัง
                    var createBtn = document.querySelector('a[href*=""create""], button:contains(""Create"")');
                    var profileIcon = document.querySelector('.user-avatar, .profile-icon, [data-user]');
                    var signInBtn = document.querySelector('button:contains(""Sign""), a[href*=""login""]');

                    if (profileIcon || (createBtn && !signInBtn)) return 'logged_in';
                    if (signInBtn) return 'logged_out';
                    return 'unknown';
                })();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);

            if (result == "logged_in")
            {
                return true;
            }

            _logger.LogWarning("Not logged in to Suno. Attempting login workflow...");

            // ลองใช้ learned workflow
            var loginWorkflow = await _workflowStorage.LoadWorkflowAsync("suno_login", ct);
            if (loginWorkflow != null)
            {
                var executeResult = await _workflowExecutor.ExecuteAsync(
                    loginWorkflow, new WebPostContent(), null, ct);
                return executeResult.Success;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure logged in");
            return false;
        }
    }

    private async Task EnableCustomModeAsync(CancellationToken ct)
    {
        var script = @"
            (function() {
                var customToggle = document.querySelector('[data-cy=""custom-toggle""], .custom-mode-toggle, label:contains(""Custom"")');
                if (customToggle) {
                    customToggle.click();
                    return 'toggled';
                }

                // ลองหา checkbox
                var checkbox = document.querySelector('input[type=""checkbox""][name*=""custom""]');
                if (checkbox && !checkbox.checked) {
                    checkbox.click();
                    return 'checked';
                }

                return 'not_found';
            })();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task TypeSongPromptAsync(string prompt, bool isCustomMode, CancellationToken ct)
    {
        var selector = isCustomMode
            ? "textarea[placeholder*='lyrics'], textarea[placeholder*='Lyrics'], .lyrics-input"
            : "textarea[placeholder*='describe'], textarea[placeholder*='Song description'], .prompt-input";

        var script = $@"
            (function() {{
                var textarea = document.querySelector('{selector}');
                if (!textarea) {{
                    // Fallback: หา textarea แรกที่เจอ
                    textarea = document.querySelector('textarea');
                }}

                if (textarea) {{
                    textarea.value = '';
                    textarea.focus();
                    textarea.value = `{prompt.Replace("`", "\\`").Replace("\n", "\\n")}`;
                    textarea.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    return 'typed';
                }}
                return 'not_found';
            }})();
        ";

        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task TypeStyleAsync(string style, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                var styleInput = document.querySelector('input[placeholder*=""style""], input[placeholder*=""genre""], .style-input');
                if (styleInput) {{
                    styleInput.value = `{style}`;
                    styleInput.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    return 'typed';
                }}
                return 'not_found';
            }})();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task TypeTitleAsync(string title, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                var titleInput = document.querySelector('input[placeholder*=""title""], input[placeholder*=""Title""], .title-input');
                if (titleInput) {{
                    titleInput.value = `{title}`;
                    titleInput.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    return 'typed';
                }}
                return 'not_found';
            }})();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task ClickCreateButtonAsync(CancellationToken ct)
    {
        var script = @"
            (function() {
                var btn = document.querySelector('button[type=""submit""], button:contains(""Create""), .create-button');
                if (!btn) {
                    var buttons = document.querySelectorAll('button');
                    for (var b of buttons) {
                        var text = b.textContent.toLowerCase();
                        if (text.includes('create') || text.includes('generate')) {
                            btn = b;
                            break;
                        }
                    }
                }
                if (btn) {
                    btn.click();
                    return 'clicked';
                }
                return 'not_found';
            })();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task ClickExtendButtonAsync(CancellationToken ct)
    {
        var script = @"
            (function() {
                var btn = document.querySelector('[data-cy=""extend""], button:contains(""Extend""), .extend-button');
                if (btn) {
                    btn.click();
                    return 'clicked';
                }
                return 'not_found';
            })();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task<List<SunoSong>> WaitForMusicGenerationCompleteAsync(
        TimeSpan timeout,
        CancellationToken ct)
    {
        var songs = new List<SunoSong>();
        var endTime = DateTime.UtcNow.Add(timeout);
        var checkInterval = TimeSpan.FromSeconds(5);

        while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, ct);

            var script = @"
                (function() {
                    var songs = [];

                    // หาเพลงที่สร้างเสร็จ (Suno จะสร้าง 2 versions)
                    var songCards = document.querySelectorAll('.song-card, .generated-song, [data-song-id]');

                    for (var card of songCards) {
                        // ตรวจสอบว่าเสร็จแล้วหรือยัง (ไม่มี loading)
                        var loading = card.querySelector('.loading, .spinner');
                        if (loading) continue;

                        var audio = card.querySelector('audio');
                        var playBtn = card.querySelector('[data-play], .play-button');

                        if (audio?.src || playBtn) {
                            var song = {
                                id: card.getAttribute('data-song-id') || '',
                                title: card.querySelector('.title, .song-title')?.textContent?.trim() || 'Untitled',
                                audioUrl: audio?.src || '',
                                imageUrl: card.querySelector('img')?.src || '',
                                duration: card.querySelector('.duration')?.textContent?.trim() || ''
                            };
                            songs.push(song);
                        }
                    }

                    if (songs.length >= 2) {
                        return JSON.stringify({ status: 'complete', songs: songs });
                    }

                    // ตรวจสอบ loading state
                    var globalLoading = document.querySelector('[data-generating], .generating-indicator');
                    if (globalLoading) {
                        return JSON.stringify({ status: 'generating' });
                    }

                    // ตรวจสอบ error
                    var error = document.querySelector('.error, [data-error]');
                    if (error) {
                        return JSON.stringify({ status: 'error', message: error.textContent });
                    }

                    return JSON.stringify({ status: 'waiting', foundSongs: songs.length });
                })();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);

            if (!string.IsNullOrEmpty(result))
            {
                var parsed = System.Text.Json.JsonDocument.Parse(result);
                var status = parsed.RootElement.GetProperty("status").GetString();

                if (status == "complete")
                {
                    var songsJson = parsed.RootElement.GetProperty("songs");
                    songs = System.Text.Json.JsonSerializer.Deserialize<List<SunoSong>>(songsJson.GetRawText())
                            ?? new List<SunoSong>();
                    break;
                }

                if (status == "error")
                {
                    _logger.LogWarning("Generation error: {Error}",
                        parsed.RootElement.GetProperty("message").GetString());
                    break;
                }

                _logger.LogDebug("Music generation status: {Status}", status);
            }
        }

        return songs;
    }

    private async Task<string?> DownloadSongAsync(string? audioUrl, string? title, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(audioUrl))
            {
                // ลองใช้ workflow download
                var downloadScript = @"
                    (function() {
                        // หา download button
                        var downloadBtn = document.querySelector('[data-cy=""download""], button:contains(""Download""), .download-button');
                        if (downloadBtn) {
                            downloadBtn.click();
                            return 'clicked';
                        }

                        // หา menu options
                        var menuBtn = document.querySelector('[data-cy=""song-menu""], .more-options');
                        if (menuBtn) {
                            menuBtn.click();
                            return 'menu_opened';
                        }

                        return 'not_found';
                    })();
                ";

                await _browserController.ExecuteScriptAsync(downloadScript, ct);
                await Task.Delay(1000, ct);

                // คลิก download option
                await _browserController.ExecuteScriptAsync(@"
                    (function() {
                        var downloadOption = document.querySelector('[data-cy=""download""], button:contains(""Download"")');
                        if (downloadOption) downloadOption.click();
                    })();
                ", ct);

                await Task.Delay(2000, ct);
                return null; // Browser จะ download ไปที่ default downloads folder
            }

            // Download directly ถ้ามี URL
            var safeTitle = string.IsNullOrEmpty(title)
                ? $"song_{DateTime.Now:yyyyMMdd_HHmmss}"
                : System.Text.RegularExpressions.Regex.Replace(title, @"[^\w\s-]", "");

            var filename = $"{safeTitle}_{Guid.NewGuid():N}.mp3";
            var localPath = Path.Combine(_downloadPath, filename);

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(audioUrl, ct);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                await File.WriteAllBytesAsync(localPath, bytes, ct);
                return localPath;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading song");
            return null;
        }
    }

    #endregion
}

#region Models

/// <summary>
/// ผลลัพธ์จากการสร้างเพลงด้วย Suno
/// </summary>
public class SunoGenerationResult
{
    public bool Success { get; set; }
    public string? Prompt { get; set; }
    public List<SunoSong> GeneratedSongs { get; set; } = new();
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// ข้อมูลเพลงที่สร้างจาก Suno
/// </summary>
public class SunoSong
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? Duration { get; set; }
    public string? Style { get; set; }
    public string? Lyrics { get; set; }
}

/// <summary>
/// ตัวเลือกสำหรับสร้างเพลง
/// </summary>
public class SunoMusicOptions
{
    /// <summary>ใช้ Custom mode เพื่อควบคุม style และ title</summary>
    public bool UseCustomMode { get; set; } = false;

    /// <summary>Style/Genre ของเพลง (ใช้ได้ใน Custom mode)</summary>
    public string? Style { get; set; }

    /// <summary>ชื่อเพลง (ใช้ได้ใน Custom mode)</summary>
    public string? Title { get; set; }

    /// <summary>เป็นเพลง instrumental (ไม่มีเนื้อร้อง)</summary>
    public bool IsInstrumental { get; set; } = false;

    /// <summary>เวลารอสูงสุด (นาที)</summary>
    public int MaxWaitMinutes { get; set; } = 3;

    /// <summary>ดาวน์โหลดทุก version ที่สร้าง</summary>
    public bool DownloadAll { get; set; } = false;
}

/// <summary>
/// ข้อมูล credits ของ Suno
/// </summary>
public class SunoCreditsInfo
{
    public int Credits { get; set; }
    public bool IsPro { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public DateTime? CheckedAt { get; set; }
}

#endregion
