using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIManager.Core.Services;

/// <summary>
/// Account Creator Service - สร้างบัญชี Social Media อัตโนมัติ
/// รองรับ 9 แพลตฟอร์ม: Facebook, Instagram, TikTok, Twitter, LINE, YouTube, Threads, LinkedIn, Pinterest
/// </summary>
public class AccountCreatorService
{
    private readonly ILogger<AccountCreatorService> _logger;
    private readonly BrowserController _browser;
    private readonly WorkflowExecutor _workflowExecutor;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly ImageGeneratorService _imageGenerator;

    // Platform signup URLs
    private static readonly Dictionary<SocialPlatform, string> SignupUrls = new()
    {
        [SocialPlatform.Facebook] = "https://www.facebook.com/r.php",
        [SocialPlatform.Instagram] = "https://www.instagram.com/accounts/emailsignup/",
        [SocialPlatform.TikTok] = "https://www.tiktok.com/signup",
        [SocialPlatform.Twitter] = "https://twitter.com/i/flow/signup",
        [SocialPlatform.Line] = "https://account.line.biz/signup",
        [SocialPlatform.YouTube] = "https://accounts.google.com/signup",
        [SocialPlatform.Threads] = "https://www.threads.net/login",
        [SocialPlatform.LinkedIn] = "https://www.linkedin.com/signup",
        [SocialPlatform.Pinterest] = "https://www.pinterest.com/signup/",
    };

    public AccountCreatorService(
        ILogger<AccountCreatorService> logger,
        BrowserController browser,
        WorkflowExecutor workflowExecutor,
        ContentGeneratorService contentGenerator,
        ImageGeneratorService imageGenerator)
    {
        _logger = logger;
        _browser = browser;
        _workflowExecutor = workflowExecutor;
        _contentGenerator = contentGenerator;
        _imageGenerator = imageGenerator;
    }

    /// <summary>
    /// สร้างบัญชีใหม่สำหรับแพลตฟอร์มที่กำหนด
    /// </summary>
    public async Task<AccountCreationResult> CreateAccountAsync(
        AccountCreationRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting account creation for {Platform}", request.Platform);

        var result = new AccountCreationResult
        {
            TaskId = request.TaskId,
            Platform = request.Platform,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Step 1: Launch browser with proxy
            await UpdateStepAsync(request, "open_browser", "กำลังเปิด Browser...");

            var browserConfig = new BrowserConfig
            {
                Headless = false, // Set to true in production
                ProxyServer = request.ProxyUrl,
                UserDataDir = Path.Combine(Path.GetTempPath(), $"postxagent_{request.TaskId}")
            };

            var launched = await _browser.LaunchAsync(ct);
            if (!launched)
            {
                throw new Exception("ไม่สามารถเปิด Browser ได้");
            }

            // Step 2: Navigate to signup page
            await UpdateStepAsync(request, "navigate_signup", "กำลังไปหน้าสมัคร...");

            var signupUrl = SignupUrls[request.Platform];
            await _browser.NavigateAsync(signupUrl, ct);
            await Task.Delay(2000, ct); // Wait for page load

            // Step 3: Fill registration form
            await UpdateStepAsync(request, "fill_form", "กำลังกรอกข้อมูล...");

            var fillResult = await FillRegistrationFormAsync(request, ct);
            if (!fillResult.Success)
            {
                throw new Exception($"ไม่สามารถกรอกฟอร์มได้: {fillResult.Error}");
            }

            // Step 4: Submit form
            await UpdateStepAsync(request, "submit_form", "กำลังส่งฟอร์ม...");

            var submitResult = await SubmitFormAsync(request.Platform, ct);
            if (!submitResult.Success)
            {
                throw new Exception($"ไม่สามารถส่งฟอร์มได้: {submitResult.Error}");
            }

            // Step 5: Wait for SMS/OTP if required
            if (request.RequiresPhoneVerification)
            {
                await UpdateStepAsync(request, "wait_sms", "รอรับ SMS...");

                var otpResult = await WaitForOtpAndEnterAsync(request, ct);
                if (!otpResult.Success)
                {
                    throw new Exception($"ไม่ได้รับ OTP: {otpResult.Error}");
                }
            }

            // Step 6: Complete profile
            await UpdateStepAsync(request, "complete_profile", "กำลังตั้งค่าโปรไฟล์...");

            var profileResult = await CompleteProfileAsync(request, ct);

            // Step 7: Save credentials
            await UpdateStepAsync(request, "save_credentials", "กำลังบันทึกข้อมูล...");

            var credentials = await SaveCredentialsAsync(request, ct);

            result.Success = true;
            result.Username = request.ProfileData.Username;
            result.PlatformUserId = credentials.PlatformUserId;
            result.ProfileUrl = credentials.ProfileUrl;
            result.AccessToken = credentials.AccessToken;
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Account created successfully: {Username}", result.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Account creation failed for {Platform}", request.Platform);

            result.Success = false;
            result.Error = ex.Message;
            result.Screenshot = await TakeErrorScreenshotAsync();
            result.CompletedAt = DateTime.UtcNow;
        }
        finally
        {
            await _browser.DisposeAsync();
        }

        return result;
    }

    /// <summary>
    /// สร้าง Avatar ด้วย AI
    /// </summary>
    public async Task<string?> GenerateAvatarAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var imageResult = await _imageGenerator.GenerateAsync(
                prompt,
                new ImageGenerationOptions
                {
                    Style = "realistic portrait",
                    Width = 512,
                    Height = 512,
                    Provider = "stable_diffusion"
                },
                ct);

            return !string.IsNullOrEmpty(imageResult.Url)
                ? imageResult.Url
                : (!string.IsNullOrEmpty(imageResult.Base64Data) ? $"data:image/png;base64,{imageResult.Base64Data}" : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate avatar");
            return null;
        }
    }

    // Platform-specific registration methods

    private async Task<OperationResult> FillRegistrationFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        return request.Platform switch
        {
            SocialPlatform.Facebook => await FillFacebookFormAsync(request, ct),
            SocialPlatform.Instagram => await FillInstagramFormAsync(request, ct),
            SocialPlatform.TikTok => await FillTikTokFormAsync(request, ct),
            SocialPlatform.Twitter => await FillTwitterFormAsync(request, ct),
            SocialPlatform.Line => await FillLineFormAsync(request, ct),
            SocialPlatform.YouTube => await FillGoogleFormAsync(request, ct),
            SocialPlatform.LinkedIn => await FillLinkedInFormAsync(request, ct),
            SocialPlatform.Pinterest => await FillPinterestFormAsync(request, ct),
            _ => new OperationResult { Success = false, Error = "Platform not supported" }
        };
    }

    private async Task<OperationResult> FillFacebookFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // Fill first name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "firstname" },
            profile.FirstName, true, 50, ct);

        // Fill last name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "lastname" },
            profile.LastName, true, 50, ct);

        // Fill email or phone
        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "reg_email__" },
                request.PhoneNumber, true, 50, ct);
        }
        else if (!string.IsNullOrEmpty(request.Email))
        {
            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "reg_email__" },
                request.Email, true, 50, ct);
        }

        // Fill password
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "reg_passwd__" },
            profile.Password, true, 50, ct);

        // Fill birthday
        if (DateTime.TryParse(profile.Birthday, out var birthday))
        {
            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "birthday_day" },
                5000, ct);
            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.CSS, Value = $"option[value='{birthday.Day}']" },
                5000, ct);

            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "birthday_month" },
                5000, ct);
            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.CSS, Value = $"option[value='{birthday.Month}']" },
                5000, ct);

            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "birthday_year" },
                5000, ct);
            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.CSS, Value = $"option[value='{birthday.Year}']" },
                5000, ct);
        }

        // Select gender
        var genderValue = profile.Gender?.ToLower() switch
        {
            "male" => "2",
            "female" => "1",
            _ => "2"
        };
        await _browser.ClickAsync(
            new ElementSelector { Type = SelectorType.CSS, Value = $"input[name='sex'][value='{genderValue}']" },
            5000, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillInstagramFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // Fill email or phone
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "emailOrPhone" },
            request.Email ?? request.PhoneNumber ?? "", true, 50, ct);

        // Fill full name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "fullName" },
            $"{profile.FirstName} {profile.LastName}", true, 50, ct);

        // Fill username
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "username" },
            profile.Username, true, 50, ct);

        // Fill password
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "password" },
            profile.Password, true, 50, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillTikTokFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // TikTok typically requires phone number
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "phone" },
            request.PhoneNumber ?? "", true, 50, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillTwitterFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // Fill name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "name" },
            $"{profile.FirstName} {profile.LastName}", true, 50, ct);

        // Click next
        await _browser.ClickAsync(
            new ElementSelector { Type = SelectorType.CSS, Value = "[data-testid='ocfEnterTextNextButton']" },
            5000, ct);

        await Task.Delay(1000, ct);

        // Fill email or phone
        if (!string.IsNullOrEmpty(request.PhoneNumber))
        {
            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.CSS, Value = "[data-testid='phoneMode']" },
                5000, ct);
            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "phone" },
                request.PhoneNumber, true, 50, ct);
        }
        else
        {
            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "email" },
                request.Email ?? "", true, 50, ct);
        }

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillLineFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // LINE Official Account signup
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "email" },
            request.Email ?? "", true, 50, ct);

        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "password" },
            profile.Password, true, 50, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillGoogleFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // First name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "firstName" },
            profile.FirstName, true, 50, ct);

        // Last name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Name, Value = "lastName" },
            profile.LastName, true, 50, ct);

        // Click next
        await _browser.ClickAsync(
            new ElementSelector { Type = SelectorType.CSS, Value = "#accountDetailsNext button" },
            5000, ct);

        await Task.Delay(1500, ct);

        // Birthday
        if (DateTime.TryParse(profile.Birthday, out var birthday))
        {
            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "day" },
                birthday.Day.ToString(), true, 50, ct);

            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.Id, Value = "month" },
                5000, ct);
            await _browser.ClickAsync(
                new ElementSelector { Type = SelectorType.CSS, Value = $"[data-value='{birthday.Month}']" },
                5000, ct);

            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.Name, Value = "year" },
                birthday.Year.ToString(), true, 50, ct);
        }

        // Gender
        await _browser.ClickAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "gender" },
            5000, ct);
        var genderOption = profile.Gender?.ToLower() == "female" ? "2" : "1";
        await _browser.ClickAsync(
            new ElementSelector { Type = SelectorType.CSS, Value = $"[data-value='{genderOption}']" },
            5000, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillLinkedInFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // Email
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "email-address" },
            request.Email ?? "", true, 50, ct);

        // Password
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "password" },
            profile.Password, true, 50, ct);

        // Submit first step
        await _browser.ClickAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "join-form-submit" },
            5000, ct);

        await Task.Delay(1500, ct);

        // First name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "first-name" },
            profile.FirstName, true, 50, ct);

        // Last name
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "last-name" },
            profile.LastName, true, 50, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> FillPinterestFormAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // Email
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "email" },
            request.Email ?? "", true, 50, ct);

        // Password
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "password" },
            profile.Password, true, 50, ct);

        // Age
        await _browser.TypeAsync(
            new ElementSelector { Type = SelectorType.Id, Value = "age" },
            "25", true, 50, ct);

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> SubmitFormAsync(SocialPlatform platform, CancellationToken ct)
    {
        var submitSelector = platform switch
        {
            SocialPlatform.Facebook => new ElementSelector { Type = SelectorType.Name, Value = "websubmit" },
            SocialPlatform.Instagram => new ElementSelector { Type = SelectorType.CSS, Value = "button[type='submit']" },
            SocialPlatform.TikTok => new ElementSelector { Type = SelectorType.CSS, Value = "button[type='submit']" },
            SocialPlatform.Twitter => new ElementSelector { Type = SelectorType.CSS, Value = "[data-testid='ocfEnterTextNextButton']" },
            SocialPlatform.LinkedIn => new ElementSelector { Type = SelectorType.CSS, Value = "#join-form-submit" },
            SocialPlatform.Pinterest => new ElementSelector { Type = SelectorType.CSS, Value = "button[type='submit']" },
            _ => new ElementSelector { Type = SelectorType.CSS, Value = "button[type='submit']" }
        };

        await _browser.ClickAsync(submitSelector, 10000, ct);
        await Task.Delay(3000, ct); // Wait for submission

        return new OperationResult { Success = true };
    }

    private async Task<OperationResult> WaitForOtpAndEnterAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var maxWaitTime = TimeSpan.FromMinutes(2);
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < maxWaitTime)
        {
            ct.ThrowIfCancellationRequested();

            // Poll for OTP from Laravel backend
            var otp = await GetOtpFromBackendAsync(request.PhoneNumberId, ct);

            if (!string.IsNullOrEmpty(otp))
            {
                // Enter OTP
                await _browser.TypeAsync(
                    new ElementSelector { Type = SelectorType.CSS, Value = "input[type='text'], input[type='number']" },
                    otp, true, 100, ct);

                // Submit OTP
                await _browser.ClickAsync(
                    new ElementSelector { Type = SelectorType.CSS, Value = "button[type='submit']" },
                    5000, ct);

                await Task.Delay(2000, ct);

                return new OperationResult { Success = true };
            }

            await Task.Delay(5000, ct); // Check every 5 seconds
        }

        return new OperationResult { Success = false, Error = "OTP timeout" };
    }

    private async Task<string?> GetOtpFromBackendAsync(int phoneNumberId, CancellationToken ct)
    {
        // This would call the Laravel API to get the latest OTP
        // For now, return null to simulate waiting
        await Task.Delay(100, ct);
        return null;
    }

    private async Task<OperationResult> CompleteProfileAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        var profile = request.ProfileData;

        // Try to set bio
        try
        {
            await _browser.TypeAsync(
                new ElementSelector { Type = SelectorType.CSS, Value = "textarea[name='bio'], textarea[aria-label*='bio']" },
                profile.Bio ?? "", true, 50, ct);
        }
        catch
        {
            // Bio field may not exist
        }

        // Try to upload avatar if we have one
        if (!string.IsNullOrEmpty(profile.AvatarUrl))
        {
            try
            {
                // Download avatar and upload
                // This is simplified - real implementation would handle file download
            }
            catch
            {
                // Avatar upload is optional
            }
        }

        return new OperationResult { Success = true };
    }

    private async Task<SavedCredentials> SaveCredentialsAsync(
        AccountCreationRequest request,
        CancellationToken ct)
    {
        // Save session cookies and get profile info
        var session = await _browser.SaveSessionAsync(ct);
        var pageUrl = _browser.CurrentUrl;

        // Extract user ID from URL or page content
        var platformUserId = await ExtractPlatformUserIdAsync(request.Platform, ct);

        return new SavedCredentials
        {
            PlatformUserId = platformUserId,
            ProfileUrl = pageUrl,
            Cookies = session.Cookies,
            LocalStorage = session.LocalStorage
        };
    }

    private async Task<string?> ExtractPlatformUserIdAsync(SocialPlatform platform, CancellationToken ct)
    {
        // Try to extract user ID from page
        var script = platform switch
        {
            SocialPlatform.Facebook => "return document.body.innerHTML.match(/\"USER_ID\":\"(\\d+)\"/)?.[1]",
            SocialPlatform.Instagram => "return window._sharedData?.config?.viewer?.id",
            _ => "return null"
        };

        return await _browser.ExecuteScriptAsync(script, ct);
    }

    private async Task<string?> TakeErrorScreenshotAsync()
    {
        try
        {
            return await _browser.TakeScreenshotAsync();
        }
        catch
        {
            return null;
        }
    }

    private async Task UpdateStepAsync(AccountCreationRequest request, string step, string message)
    {
        _logger.LogInformation("Step: {Step} - {Message}", step, message);
        // This would call back to Laravel to update the task step
    }
}

#region Models

public class AccountCreationRequest
{
    public int TaskId { get; set; }
    public SocialPlatform Platform { get; set; }
    public ProfileData ProfileData { get; set; } = new();
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int PhoneNumberId { get; set; }
    public string? ProxyUrl { get; set; }
    public bool RequiresPhoneVerification { get; set; }
}

public class ProfileData
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? Birthday { get; set; }
    public string? Gender { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}

public class AccountCreationResult
{
    public int TaskId { get; set; }
    public SocialPlatform Platform { get; set; }
    public bool Success { get; set; }
    public string? Username { get; set; }
    public string? PlatformUserId { get; set; }
    public string? ProfileUrl { get; set; }
    public string? AccessToken { get; set; }
    public string? Error { get; set; }
    public string? Screenshot { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class OperationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class SavedCredentials
{
    public string? PlatformUserId { get; set; }
    public string? ProfileUrl { get; set; }
    public string? AccessToken { get; set; }
    public List<BrowserCookie>? Cookies { get; set; }
    public Dictionary<string, string>? LocalStorage { get; set; }
}

#endregion
