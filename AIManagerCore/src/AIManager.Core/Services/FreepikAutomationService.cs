using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// FreepikAutomationService - ระบบ automation สำหรับ Freepik Pikaso AI
/// สร้างรูปและวิดีโอผ่าน web automation โดยใช้เฉพาะโมเดล Unlimited (ไม่เสีย credits)
/// </summary>
public class FreepikAutomationService
{
    private readonly ILogger<FreepikAutomationService> _logger;
    private readonly BrowserController _browserController;
    private readonly WorkflowExecutor _workflowExecutor;
    private readonly WorkflowStorage _workflowStorage;
    private readonly string _downloadPath;

    // Freepik URLs
    private const string FREEPIK_HOME = "https://www.freepik.com";
    private const string FREEPIK_LOGIN = "https://www.freepik.com/log-in";
    private const string FREEPIK_IMAGE_GENERATOR = "https://www.freepik.com/pikaso/ai-image-generator";
    private const string FREEPIK_VIDEO_GENERATOR = "https://www.freepik.com/pikaso/ai-video";

    // รายชื่อโมเดลที่ Unlimited (ฟรี ไม่เสีย credits)
    private static readonly HashSet<string> UnlimitedModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Flux",
        "Flux Realism",
        "Mystic",
        "Anime",
        "Photo",
        "Digital Art",
        "3D Render"
    };

    // โมเดลที่ต้อง avoid (เสีย credits)
    private static readonly HashSet<string> PaidModels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Premium",
        "Pro",
        "Ultra"
    };

    public FreepikAutomationService(
        ILogger<FreepikAutomationService> logger,
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
            "PostXAgent", "Downloads", "Freepik");

        Directory.CreateDirectory(_downloadPath);
    }

    #region Public Methods

    /// <summary>
    /// สร้างรูปจาก prompt โดยใช้ Freepik Pikaso AI
    /// ใช้เฉพาะโมเดล Unlimited เท่านั้น
    /// </summary>
    public async Task<FreepikGenerationResult> GenerateImageAsync(
        string prompt,
        FreepikImageOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new FreepikGenerationResult
        {
            Type = MediaType.Image,
            Prompt = prompt,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting Freepik image generation: {Prompt}", prompt);

            // 1. ตรวจสอบว่า browser พร้อมหรือยัง
            if (!await EnsureBrowserReadyAsync(ct))
            {
                result.Success = false;
                result.Error = "Failed to initialize browser";
                return result;
            }

            // 2. ตรวจสอบว่า login แล้วหรือยัง
            if (!await EnsureLoggedInAsync(ct))
            {
                result.Success = false;
                result.Error = "Failed to login to Freepik";
                return result;
            }

            // 3. ไปที่หน้า AI Image Generator
            await _browserController.NavigateAsync(FREEPIK_IMAGE_GENERATOR, ct);
            await Task.Delay(3000, ct); // รอหน้าโหลด

            // 4. เลือกโมเดล Unlimited
            var modelSelected = await SelectUnlimitedModelAsync(options?.PreferredModel, ct);
            if (!modelSelected)
            {
                result.Success = false;
                result.Error = "Failed to select an unlimited model";
                return result;
            }

            // 5. พิมพ์ prompt
            await TypePromptAsync(prompt, ct);
            await Task.Delay(500, ct);

            // 6. กด Generate
            await ClickGenerateButtonAsync(ct);

            // 7. รอจนรูปเสร็จ
            var imageUrl = await WaitForGenerationCompleteAsync(TimeSpan.FromMinutes(2), ct);
            if (string.IsNullOrEmpty(imageUrl))
            {
                result.Success = false;
                result.Error = "Image generation timed out or failed";
                return result;
            }

            result.MediaUrl = imageUrl;

            // 8. ดาวน์โหลดรูป
            var localPath = await DownloadMediaAsync(imageUrl, "image", ct);
            if (!string.IsNullOrEmpty(localPath))
            {
                result.LocalPath = localPath;
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Image generated successfully: {Path}", result.LocalPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// สร้างวิดีโอจากรูป โดยใช้ Freepik Pikaso Video AI
    /// </summary>
    public async Task<FreepikGenerationResult> GenerateVideoFromImageAsync(
        string imagePath,
        string? motionPrompt = null,
        FreepikVideoOptions? options = null,
        CancellationToken ct = default)
    {
        var result = new FreepikGenerationResult
        {
            Type = MediaType.Video,
            Prompt = motionPrompt ?? "Smooth cinematic motion",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting Freepik video generation from image: {Path}", imagePath);

            // 1. ตรวจสอบว่าไฟล์รูปมีอยู่
            if (!File.Exists(imagePath))
            {
                result.Success = false;
                result.Error = $"Image file not found: {imagePath}";
                return result;
            }

            // 2. ตรวจสอบ browser และ login
            if (!await EnsureBrowserReadyAsync(ct) || !await EnsureLoggedInAsync(ct))
            {
                result.Success = false;
                result.Error = "Failed to initialize browser or login";
                return result;
            }

            // 3. ไปที่หน้า AI Video Generator
            await _browserController.NavigateAsync(FREEPIK_VIDEO_GENERATOR, ct);
            await Task.Delay(3000, ct);

            // 4. เลือก Image to Video tab
            await SelectImageToVideoTabAsync(ct);
            await Task.Delay(1000, ct);

            // 5. อัพโหลดรูป
            await UploadImageAsync(imagePath, ct);
            await Task.Delay(3000, ct); // รอ upload

            // 6. พิมพ์ motion prompt (ถ้ามี)
            if (!string.IsNullOrEmpty(motionPrompt))
            {
                await TypeMotionPromptAsync(motionPrompt, ct);
            }

            // 7. เลือก options (duration, style)
            if (options != null)
            {
                await SelectVideoOptionsAsync(options, ct);
            }

            // 8. กด Generate Video
            await ClickGenerateVideoButtonAsync(ct);

            // 9. รอจนวิดีโอเสร็จ (อาจใช้เวลา 2-5 นาที)
            var videoUrl = await WaitForVideoGenerationCompleteAsync(
                TimeSpan.FromMinutes(options?.MaxWaitMinutes ?? 5), ct);

            if (string.IsNullOrEmpty(videoUrl))
            {
                result.Success = false;
                result.Error = "Video generation timed out or failed";
                return result;
            }

            result.MediaUrl = videoUrl;

            // 10. ดาวน์โหลดวิดีโอ
            var localPath = await DownloadMediaAsync(videoUrl, "video", ct);
            if (!string.IsNullOrEmpty(localPath))
            {
                result.LocalPath = localPath;
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Video generated successfully: {Path}", result.LocalPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate video");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// สร้างรูปหลายรูปจาก prompts
    /// </summary>
    public async Task<List<FreepikGenerationResult>> GenerateMultipleImagesAsync(
        List<string> prompts,
        FreepikImageOptions? options = null,
        CancellationToken ct = default)
    {
        var results = new List<FreepikGenerationResult>();

        foreach (var prompt in prompts)
        {
            if (ct.IsCancellationRequested) break;

            var result = await GenerateImageAsync(prompt, options, ct);
            results.Add(result);

            // รอระหว่าง generations เพื่อไม่ให้ rate limit
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        return results;
    }

    /// <summary>
    /// ตรวจสอบว่าโมเดลเป็น Unlimited หรือไม่
    /// </summary>
    public bool IsUnlimitedModel(string modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return false;

        // ตรวจสอบว่าไม่อยู่ใน paid models
        if (PaidModels.Any(p => modelName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        // ตรวจสอบว่าอยู่ใน unlimited models
        return UnlimitedModels.Any(u => modelName.Contains(u, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ดึงรายชื่อโมเดล Unlimited ทั้งหมด
    /// </summary>
    public IReadOnlyCollection<string> GetUnlimitedModels() => UnlimitedModels;

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
            // ตรวจสอบว่า login แล้วหรือยังโดยดู URL หรือ element
            var currentUrl = _browserController.CurrentUrl ?? "";

            // ถ้าอยู่หน้า login ให้ทำการ login
            if (currentUrl.Contains("log-in") || currentUrl.Contains("login"))
            {
                // TODO: ใช้ learned workflow สำหรับ login
                _logger.LogWarning("Not logged in. Please login manually or use learned workflow.");
                return false;
            }

            // ไปที่หน้า home เพื่อตรวจสอบ
            await _browserController.NavigateAsync(FREEPIK_HOME, ct);
            await Task.Delay(2000, ct);

            // ตรวจสอบว่ามี user avatar หรือ profile icon
            var isLoggedIn = await CheckIfLoggedInAsync(ct);

            if (!isLoggedIn)
            {
                _logger.LogInformation("Not logged in, attempting login workflow...");

                // ลองใช้ learned workflow
                var loginWorkflow = await _workflowStorage.LoadWorkflowAsync("freepik_login", ct);
                if (loginWorkflow != null)
                {
                    var executeResult = await _workflowExecutor.ExecuteAsync(
                        loginWorkflow, new WebPostContent(), null, ct);
                    return executeResult.Success;
                }
            }

            return isLoggedIn;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure logged in");
            return false;
        }
    }

    private async Task<bool> CheckIfLoggedInAsync(CancellationToken ct)
    {
        try
        {
            // ตรวจสอบ element ที่บ่งบอกว่า login แล้ว
            var script = @"
                (function() {
                    // ลองหา avatar, profile icon, หรือ logout button
                    var avatar = document.querySelector('[data-cy=""user-avatar""], .user-avatar, .profile-icon');
                    var logoutBtn = document.querySelector('[data-cy=""logout""], a[href*=""logout""]');
                    var loginBtn = document.querySelector('[data-cy=""login""], a[href*=""log-in""]');

                    if (avatar || logoutBtn) return 'logged_in';
                    if (loginBtn) return 'logged_out';
                    return 'unknown';
                })();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);
            return result?.Contains("logged_in") == true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> SelectUnlimitedModelAsync(string? preferredModel, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Selecting unlimited model...");

            // JavaScript เพื่อหาและเลือกโมเดล Unlimited
            var script = $@"
                (function() {{
                    var unlimitedModels = {System.Text.Json.JsonSerializer.Serialize(UnlimitedModels)};
                    var paidKeywords = ['Premium', 'Pro', 'Ultra', 'credits'];

                    // หา model cards ทั้งหมด
                    var modelCards = document.querySelectorAll('[data-model], .model-card, .style-card');

                    for (var card of modelCards) {{
                        var text = card.textContent || '';
                        var hasUnlimitedBadge = card.querySelector('.unlimited, [class*=""unlimited""]') != null;
                        var hasPaidBadge = paidKeywords.some(k => text.includes(k));

                        // ถ้าเป็น unlimited และไม่ใช่ paid
                        if ((hasUnlimitedBadge || unlimitedModels.some(m => text.includes(m))) && !hasPaidBadge) {{
                            card.click();
                            return 'selected: ' + text.substring(0, 50);
                        }}
                    }}

                    return 'no_unlimited_found';
                }})();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);
            _logger.LogInformation("Model selection result: {Result}", result);

            return result?.StartsWith("selected") == true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select unlimited model");
            return false;
        }
    }

    private async Task TypePromptAsync(string prompt, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                var textarea = document.querySelector('textarea[placeholder*=""Describe""], textarea[placeholder*=""prompt""], .prompt-input textarea');
                if (textarea) {{
                    textarea.value = '';
                    textarea.focus();
                    return 'found';
                }}
                return 'not_found';
            }})();
        ";

        var result = await _browserController.ExecuteScriptAsync(script, ct);

        if (result == "found")
        {
            // พิมพ์ทีละตัว (human-like)
            await _browserController.ExecuteScriptAsync(
                $"document.activeElement.value = `{prompt.Replace("`", "\\`")}`;", ct);

            // Trigger input event
            await _browserController.ExecuteScriptAsync(
                "document.activeElement.dispatchEvent(new Event('input', { bubbles: true }));", ct);
        }
    }

    private async Task ClickGenerateButtonAsync(CancellationToken ct)
    {
        var script = @"
            (function() {
                var btn = document.querySelector('button[data-cy=""generate""], button:contains(""Generate""), .generate-button');
                if (!btn) {
                    // ลองหาด้วย text content
                    var buttons = document.querySelectorAll('button');
                    for (var b of buttons) {
                        if (b.textContent.includes('Generate') || b.textContent.includes('Create')) {
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

    private async Task<string?> WaitForGenerationCompleteAsync(TimeSpan timeout, CancellationToken ct)
    {
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(3000, ct); // ตรวจสอบทุก 3 วินาที

            var script = @"
                (function() {
                    // หารูปที่สร้างเสร็จ
                    var img = document.querySelector('.generated-image img, .result-image img, [data-generated] img');
                    if (img && img.src && !img.src.includes('loading') && !img.src.includes('placeholder')) {
                        return img.src;
                    }

                    // ตรวจสอบ loading state
                    var loading = document.querySelector('.loading, .spinner, [data-loading]');
                    if (loading) return 'still_loading';

                    // ตรวจสอบ error
                    var error = document.querySelector('.error, [data-error]');
                    if (error) return 'error: ' + error.textContent;

                    return 'waiting';
                })();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);

            if (result?.StartsWith("http") == true)
            {
                return result;
            }

            if (result?.StartsWith("error") == true)
            {
                _logger.LogWarning("Generation error: {Error}", result);
                return null;
            }

            _logger.LogDebug("Still waiting for generation... Status: {Status}", result);
        }

        return null;
    }

    private async Task SelectImageToVideoTabAsync(CancellationToken ct)
    {
        var script = @"
            (function() {
                var tab = document.querySelector('[data-tab=""image-to-video""], button:contains(""Image"")');
                if (tab) {
                    tab.click();
                    return 'clicked';
                }
                return 'not_found';
            })();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task UploadImageAsync(string imagePath, CancellationToken ct)
    {
        // หา file input element
        var script = @"
            (function() {
                var input = document.querySelector('input[type=""file""]');
                if (input) {
                    input.style.display = 'block';
                    return 'found';
                }
                return 'not_found';
            })();
        ";

        var result = await _browserController.ExecuteScriptAsync(script, ct);

        if (result == "found")
        {
            // ใช้ Playwright setInputFiles
            // Note: ต้องปรับ BrowserController ให้รองรับ
            _logger.LogInformation("Uploading image: {Path}", imagePath);

            // Fallback: ใช้ ExecuteScript กับ FileReader (สำหรับทดสอบ)
        }
    }

    private async Task TypeMotionPromptAsync(string motionPrompt, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                var input = document.querySelector('textarea[placeholder*=""motion""], textarea[placeholder*=""movement""], .motion-prompt');
                if (input) {{
                    input.value = `{motionPrompt.Replace("`", "\\`")}`;
                    input.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    return 'typed';
                }}
                return 'not_found';
            }})();
        ";
        await _browserController.ExecuteScriptAsync(script, ct);
    }

    private async Task SelectVideoOptionsAsync(FreepikVideoOptions options, CancellationToken ct)
    {
        // เลือก duration
        if (options.DurationSeconds > 0)
        {
            var durationScript = $@"
                (function() {{
                    var durationBtn = document.querySelector('[data-duration=""{options.DurationSeconds}""]');
                    if (durationBtn) durationBtn.click();
                }})();
            ";
            await _browserController.ExecuteScriptAsync(durationScript, ct);
        }
    }

    private async Task ClickGenerateVideoButtonAsync(CancellationToken ct)
    {
        var script = @"
            (function() {
                var btn = document.querySelector('button[data-cy=""generate-video""], button:contains(""Generate Video"")');
                if (!btn) {
                    var buttons = document.querySelectorAll('button');
                    for (var b of buttons) {
                        if (b.textContent.includes('Generate') && b.textContent.includes('Video')) {
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

    private async Task<string?> WaitForVideoGenerationCompleteAsync(TimeSpan timeout, CancellationToken ct)
    {
        var endTime = DateTime.UtcNow.Add(timeout);
        var checkInterval = TimeSpan.FromSeconds(10); // วิดีโอใช้เวลานาน

        while (DateTime.UtcNow < endTime && !ct.IsCancellationRequested)
        {
            await Task.Delay(checkInterval, ct);

            var script = @"
                (function() {
                    // หาวิดีโอที่สร้างเสร็จ
                    var video = document.querySelector('.generated-video video, .result-video video, video[src]');
                    if (video && video.src && !video.src.includes('loading')) {
                        return video.src;
                    }

                    // หา download link
                    var downloadLink = document.querySelector('a[download][href*=""video""], a[download][href*="".mp4""]');
                    if (downloadLink) return downloadLink.href;

                    // ตรวจสอบ progress
                    var progress = document.querySelector('.progress, [data-progress]');
                    if (progress) {
                        var pct = progress.textContent || progress.getAttribute('data-progress') || '0';
                        return 'progress: ' + pct;
                    }

                    return 'waiting';
                })();
            ";

            var result = await _browserController.ExecuteScriptAsync(script, ct);

            if (result?.StartsWith("http") == true)
            {
                return result;
            }

            _logger.LogDebug("Video generation status: {Status}", result);
        }

        return null;
    }

    private async Task<string?> DownloadMediaAsync(string url, string type, CancellationToken ct)
    {
        try
        {
            var extension = type == "video" ? ".mp4" : ".jpg";
            var filename = $"{type}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{extension}";
            var localPath = Path.Combine(_downloadPath, filename);

            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                await File.WriteAllBytesAsync(localPath, bytes, ct);
                return localPath;
            }

            _logger.LogWarning("Failed to download: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading media from {Url}", url);
            return null;
        }
    }

    #endregion
}

#region Models

/// <summary>
/// ผลลัพธ์จากการสร้างสื่อด้วย Freepik
/// </summary>
public class FreepikGenerationResult
{
    public bool Success { get; set; }
    public MediaType Type { get; set; }
    public string? Prompt { get; set; }
    public string? MediaUrl { get; set; }
    public string? LocalPath { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// ตัวเลือกสำหรับสร้างรูป
/// </summary>
public class FreepikImageOptions
{
    public string? PreferredModel { get; set; }
    public string? Style { get; set; }
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Square_1_1;
    public int NumberOfImages { get; set; } = 1;
}

/// <summary>
/// ตัวเลือกสำหรับสร้างวิดีโอ
/// </summary>
public class FreepikVideoOptions
{
    public int DurationSeconds { get; set; } = 5;
    public string? Style { get; set; }
    public int MaxWaitMinutes { get; set; } = 5;
}

public enum MediaType
{
    Image,
    Video,
    Audio
}

#endregion
