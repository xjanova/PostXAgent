using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Platform Setup Service - Setup and validate platform credentials
/// Provides OAuth flows and credential validation for all 9 platforms
/// </summary>
public class PlatformSetupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PlatformSetupService>? _logger;

    public PlatformSetupService(ILogger<PlatformSetupService>? logger = null)
    {
        _httpClient = new HttpClient();
        _logger = logger;
    }

    /// <summary>
    /// Get setup information for a platform
    /// </summary>
    public PlatformSetupInfo GetSetupInfo(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "Facebook",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "pages_show_list", "pages_read_engagement", "pages_manage_posts" },
                SetupUrl = "https://developers.facebook.com/apps/",
                DocumentationUrl = "https://developers.facebook.com/docs/graph-api/",
                RequiredCredentials = new[] { "App ID", "App Secret", "Page Access Token" },
                SetupSteps = new[]
                {
                    "1. ไปที่ Facebook Developer Console",
                    "2. สร้าง App ใหม่หรือเลือก App ที่มีอยู่",
                    "3. เพิ่ม Facebook Login product",
                    "4. ตั้งค่า Permissions ที่ต้องการ",
                    "5. สร้าง Page Access Token"
                }
            },
            SocialPlatform.Instagram => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "Instagram",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "instagram_basic", "instagram_content_publish", "instagram_manage_insights" },
                SetupUrl = "https://developers.facebook.com/apps/",
                DocumentationUrl = "https://developers.facebook.com/docs/instagram-api/",
                RequiredCredentials = new[] { "Facebook App ID", "App Secret", "Instagram Business Account ID" },
                SetupSteps = new[]
                {
                    "1. ต้องมี Facebook App ก่อน",
                    "2. เชื่อมต่อ Instagram Business Account กับ Facebook Page",
                    "3. เพิ่ม Instagram Graph API product",
                    "4. ขอสิทธิ์ instagram_content_publish"
                }
            },
            SocialPlatform.TikTok => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "TikTok",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "user.info.basic", "video.list", "video.upload" },
                SetupUrl = "https://developers.tiktok.com/",
                DocumentationUrl = "https://developers.tiktok.com/doc/content-posting-api-get-started",
                RequiredCredentials = new[] { "Client Key", "Client Secret" },
                SetupSteps = new[]
                {
                    "1. สมัคร TikTok Developer Account",
                    "2. สร้าง App และรอ Approval",
                    "3. ขอ Content Posting API access",
                    "4. ตั้งค่า OAuth callback URL"
                }
            },
            SocialPlatform.Twitter => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "Twitter/X",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "tweet.read", "tweet.write", "users.read" },
                SetupUrl = "https://developer.twitter.com/en/portal/dashboard",
                DocumentationUrl = "https://developer.twitter.com/en/docs/twitter-api/v2",
                RequiredCredentials = new[] { "API Key", "API Secret", "Bearer Token" },
                SetupSteps = new[]
                {
                    "1. สมัคร Twitter Developer Account",
                    "2. สร้าง Project และ App",
                    "3. เลือก App permissions (Read and Write)",
                    "4. สร้าง Access Token และ Secret"
                }
            },
            SocialPlatform.Line => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "LINE Official Account",
                AuthType = AuthType.ChannelToken,
                RequiredScopes = new[] { "message:send", "message:broadcast" },
                SetupUrl = "https://developers.line.biz/console/",
                DocumentationUrl = "https://developers.line.biz/en/docs/messaging-api/",
                RequiredCredentials = new[] { "Channel ID", "Channel Secret", "Channel Access Token" },
                SetupSteps = new[]
                {
                    "1. สร้าง LINE Official Account",
                    "2. ไปที่ LINE Developers Console",
                    "3. สร้าง Messaging API Channel",
                    "4. สร้าง Long-lived Channel Access Token"
                }
            },
            SocialPlatform.YouTube => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "YouTube",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "https://www.googleapis.com/auth/youtube.upload", "https://www.googleapis.com/auth/youtube" },
                SetupUrl = "https://console.cloud.google.com/",
                DocumentationUrl = "https://developers.google.com/youtube/v3",
                RequiredCredentials = new[] { "Google Client ID", "Client Secret", "Refresh Token" },
                SetupSteps = new[]
                {
                    "1. ไปที่ Google Cloud Console",
                    "2. สร้าง Project และ Enable YouTube Data API v3",
                    "3. สร้าง OAuth 2.0 Credentials",
                    "4. ตั้งค่า Consent Screen"
                }
            },
            SocialPlatform.Threads => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "Threads",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "threads_basic", "threads_content_publish" },
                SetupUrl = "https://developers.facebook.com/apps/",
                DocumentationUrl = "https://developers.facebook.com/docs/threads/",
                RequiredCredentials = new[] { "App ID", "App Secret", "Threads User ID" },
                SetupSteps = new[]
                {
                    "1. ต้องมี Instagram Business Account ที่เชื่อมกับ Threads",
                    "2. ใช้ Facebook App เดียวกับ Instagram",
                    "3. เพิ่ม Threads API access",
                    "4. ขอ threads_content_publish permission"
                }
            },
            SocialPlatform.LinkedIn => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "LinkedIn",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "w_member_social", "r_liteprofile" },
                SetupUrl = "https://www.linkedin.com/developers/apps/",
                DocumentationUrl = "https://learn.microsoft.com/en-us/linkedin/marketing/",
                RequiredCredentials = new[] { "Client ID", "Client Secret" },
                SetupSteps = new[]
                {
                    "1. สร้าง LinkedIn App ที่ Developer Portal",
                    "2. ขอ Marketing Developer Platform access",
                    "3. เพิ่ม Products: Share on LinkedIn, Sign In with LinkedIn",
                    "4. Verify App และรอ Approval"
                }
            },
            SocialPlatform.Pinterest => new PlatformSetupInfo
            {
                Platform = platform,
                Name = "Pinterest",
                AuthType = AuthType.OAuth2,
                RequiredScopes = new[] { "boards:read", "boards:write", "pins:read", "pins:write" },
                SetupUrl = "https://developers.pinterest.com/apps/",
                DocumentationUrl = "https://developers.pinterest.com/docs/api/v5/",
                RequiredCredentials = new[] { "App ID", "App Secret" },
                SetupSteps = new[]
                {
                    "1. สมัคร Pinterest Business Account",
                    "2. สร้าง App ที่ Developer Portal",
                    "3. ขอ Standard Access (ต้องรอ Approval)",
                    "4. เลือก Board ที่จะโพสต์"
                }
            },
            _ => throw new ArgumentException($"Unknown platform: {platform}")
        };
    }

    /// <summary>
    /// Validate platform credentials
    /// </summary>
    public async Task<CredentialValidationResult> ValidateCredentialsAsync(
        SocialPlatform platform,
        PlatformCredentials credentials,
        CancellationToken ct = default)
    {
        try
        {
            return platform switch
            {
                SocialPlatform.Facebook => await ValidateFacebookAsync(credentials, ct),
                SocialPlatform.Instagram => await ValidateInstagramAsync(credentials, ct),
                SocialPlatform.TikTok => await ValidateTikTokAsync(credentials, ct),
                SocialPlatform.Twitter => await ValidateTwitterAsync(credentials, ct),
                SocialPlatform.Line => await ValidateLineAsync(credentials, ct),
                SocialPlatform.YouTube => await ValidateYouTubeAsync(credentials, ct),
                SocialPlatform.Threads => await ValidateThreadsAsync(credentials, ct),
                SocialPlatform.LinkedIn => await ValidateLinkedInAsync(credentials, ct),
                SocialPlatform.Pinterest => await ValidatePinterestAsync(credentials, ct),
                _ => new CredentialValidationResult { IsValid = false, Error = "Unknown platform" }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Credential validation failed for {Platform}", platform);
            return new CredentialValidationResult
            {
                IsValid = false,
                Error = ex.Message
            };
        }
    }

    #region Platform Validation Methods

    private async Task<CredentialValidationResult> ValidateFacebookAsync(PlatformCredentials creds, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(
            $"https://graph.facebook.com/v19.0/me?access_token={creds.AccessToken}",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = data.GetProperty("name").GetString(),
                AccountId = data.GetProperty("id").GetString()
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid access token" };
    }

    private async Task<CredentialValidationResult> ValidateInstagramAsync(PlatformCredentials creds, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(
            $"https://graph.facebook.com/v19.0/{creds.PlatformUserId}?fields=username,name&access_token={creds.AccessToken}",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = data.TryGetProperty("username", out var u) ? u.GetString() : null,
                AccountId = data.GetProperty("id").GetString()
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid Instagram credentials" };
    }

    private async Task<CredentialValidationResult> ValidateTikTokAsync(PlatformCredentials creds, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

        var response = await _httpClient.GetAsync(
            "https://open.tiktokapis.com/v2/user/info/?fields=display_name,username",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var userData = data.GetProperty("data").GetProperty("user");
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = userData.TryGetProperty("display_name", out var n) ? n.GetString() : null,
                AccountId = userData.TryGetProperty("username", out var u) ? u.GetString() : null
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid TikTok credentials" };
    }

    private async Task<CredentialValidationResult> ValidateTwitterAsync(PlatformCredentials creds, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

        var response = await _httpClient.GetAsync(
            "https://api.twitter.com/2/users/me",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var userData = data.GetProperty("data");
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = userData.GetProperty("name").GetString(),
                AccountId = userData.GetProperty("id").GetString()
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid Twitter credentials" };
    }

    private async Task<CredentialValidationResult> ValidateLineAsync(PlatformCredentials creds, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

        var response = await _httpClient.GetAsync(
            "https://api.line.me/v2/bot/info",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = data.TryGetProperty("displayName", out var n) ? n.GetString() : null,
                AccountId = data.TryGetProperty("userId", out var u) ? u.GetString() : null
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid LINE credentials" };
    }

    private async Task<CredentialValidationResult> ValidateYouTubeAsync(PlatformCredentials creds, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

        var response = await _httpClient.GetAsync(
            "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var items = data.GetProperty("items");
            if (items.GetArrayLength() > 0)
            {
                var channel = items[0];
                return new CredentialValidationResult
                {
                    IsValid = true,
                    AccountName = channel.GetProperty("snippet").GetProperty("title").GetString(),
                    AccountId = channel.GetProperty("id").GetString()
                };
            }
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid YouTube credentials" };
    }

    private async Task<CredentialValidationResult> ValidateThreadsAsync(PlatformCredentials creds, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync(
            $"https://graph.threads.net/v1.0/me?fields=username,name&access_token={creds.AccessToken}",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = data.TryGetProperty("username", out var u) ? u.GetString() : null,
                AccountId = data.GetProperty("id").GetString()
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid Threads credentials" };
    }

    private async Task<CredentialValidationResult> ValidateLinkedInAsync(PlatformCredentials creds, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

        var response = await _httpClient.GetAsync(
            "https://api.linkedin.com/v2/me",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var firstName = data.TryGetProperty("localizedFirstName", out var f) ? f.GetString() : "";
            var lastName = data.TryGetProperty("localizedLastName", out var l) ? l.GetString() : "";
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = $"{firstName} {lastName}".Trim(),
                AccountId = data.GetProperty("id").GetString()
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid LinkedIn credentials" };
    }

    private async Task<CredentialValidationResult> ValidatePinterestAsync(PlatformCredentials creds, CancellationToken ct)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

        var response = await _httpClient.GetAsync(
            "https://api.pinterest.com/v5/user_account",
            ct);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            return new CredentialValidationResult
            {
                IsValid = true,
                AccountName = data.TryGetProperty("username", out var u) ? u.GetString() : null,
                AccountId = data.TryGetProperty("id", out var i) ? i.GetString() : null
            };
        }

        return new CredentialValidationResult { IsValid = false, Error = "Invalid Pinterest credentials" };
    }

    #endregion

    /// <summary>
    /// Get OAuth URL for platform authentication
    /// </summary>
    public string GetOAuthUrl(SocialPlatform platform, string clientId, string redirectUri, string? state = null)
    {
        state ??= Guid.NewGuid().ToString("N");
        var info = GetSetupInfo(platform);

        return platform switch
        {
            SocialPlatform.Facebook or SocialPlatform.Instagram =>
                $"https://www.facebook.com/v19.0/dialog/oauth?" +
                $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={string.Join(",", info.RequiredScopes)}&state={state}",

            SocialPlatform.TikTok =>
                $"https://www.tiktok.com/v2/auth/authorize/?" +
                $"client_key={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={string.Join(",", info.RequiredScopes)}&response_type=code&state={state}",

            SocialPlatform.Twitter =>
                $"https://twitter.com/i/oauth2/authorize?" +
                $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={string.Join("%20", info.RequiredScopes)}&response_type=code&state={state}" +
                "&code_challenge=challenge&code_challenge_method=plain",

            SocialPlatform.YouTube =>
                $"https://accounts.google.com/o/oauth2/v2/auth?" +
                $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={Uri.EscapeDataString(string.Join(" ", info.RequiredScopes))}" +
                "&response_type=code&access_type=offline&state={state}",

            SocialPlatform.Threads =>
                $"https://www.threads.net/oauth/authorize?" +
                $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={string.Join(",", info.RequiredScopes)}&response_type=code&state={state}",

            SocialPlatform.LinkedIn =>
                $"https://www.linkedin.com/oauth/v2/authorization?" +
                $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={string.Join("%20", info.RequiredScopes)}&response_type=code&state={state}",

            SocialPlatform.Pinterest =>
                $"https://www.pinterest.com/oauth/?" +
                $"client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={string.Join(",", info.RequiredScopes)}&response_type=code&state={state}",

            SocialPlatform.Line =>
                // LINE uses Channel Access Token, not OAuth for Messaging API
                info.SetupUrl,

            _ => info.SetupUrl
        };
    }

    /// <summary>
    /// Open browser to platform setup page
    /// </summary>
    public void OpenSetupPage(SocialPlatform platform)
    {
        var info = GetSetupInfo(platform);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = info.SetupUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open browser for {Platform}", platform);
        }
    }
}

#region Setup Models

public class PlatformSetupInfo
{
    public SocialPlatform Platform { get; set; }
    public string Name { get; set; } = "";
    public AuthType AuthType { get; set; }
    public string[] RequiredScopes { get; set; } = Array.Empty<string>();
    public string SetupUrl { get; set; } = "";
    public string DocumentationUrl { get; set; } = "";
    public string[] RequiredCredentials { get; set; } = Array.Empty<string>();
    public string[] SetupSteps { get; set; } = Array.Empty<string>();
}

public enum AuthType
{
    OAuth2,
    OAuth1,
    ApiKey,
    ChannelToken
}

public class CredentialValidationResult
{
    public bool IsValid { get; set; }
    public string? AccountName { get; set; }
    public string? AccountId { get; set; }
    public string? Error { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

#endregion
