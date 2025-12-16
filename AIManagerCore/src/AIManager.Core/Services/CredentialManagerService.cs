using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Service for managing platform credentials with encryption and persistent storage
/// </summary>
public class CredentialManagerService
{
    private readonly ILogger<CredentialManagerService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly string _credentialsPath;
    private readonly byte[]? _encryptionKey;
    private AllPlatformCredentials _credentials;

    private static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AIManager", "credentials.enc");

    public CredentialManagerService() : this(null, null)
    {
    }

    public CredentialManagerService(ILogger<CredentialManagerService>? logger, string? credentialsPath = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _credentialsPath = credentialsPath ?? DefaultPath;
        _encryptionKey = GetOrCreateEncryptionKey();
        _credentials = LoadCredentials();
    }

    #region Credential Storage

    private byte[] GetOrCreateEncryptionKey()
    {
        var keyPath = Path.Combine(Path.GetDirectoryName(_credentialsPath)!, ".key");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_credentialsPath)!);

            // Check if running on Windows for DPAPI support
            if (OperatingSystem.IsWindows())
            {
                if (File.Exists(keyPath))
                {
                    // Use DPAPI to decrypt the stored key (Windows only)
                    var encryptedKey = File.ReadAllBytes(keyPath);
                    return ProtectedData.Unprotect(encryptedKey, null, DataProtectionScope.CurrentUser);
                }

                // Generate new key and protect it with DPAPI
                var key = new byte[32];
                RandomNumberGenerator.Fill(key);
                var protectedKey = ProtectedData.Protect(key, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(keyPath, protectedKey);

                _logger?.LogInformation("Created new encryption key using DPAPI");
                return key;
            }

            // Non-Windows platform - use machine-specific key derivation
            return DeriveKeyFromMachine();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to create encryption key, using derived key");
            return DeriveKeyFromMachine();
        }
    }

    private static byte[] DeriveKeyFromMachine()
    {
        var machineId = Environment.MachineName + Environment.UserName + Environment.OSVersion.VersionString;
        return SHA256.HashData(Encoding.UTF8.GetBytes(machineId));
    }

    private AllPlatformCredentials LoadCredentials()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
            {
                _logger?.LogInformation("No credentials file found, creating new");
                return new AllPlatformCredentials();
            }

            var encryptedData = File.ReadAllBytes(_credentialsPath);
            var json = Decrypt(encryptedData);
            var creds = JsonSerializer.Deserialize<AllPlatformCredentials>(json);

            _logger?.LogInformation("Loaded credentials from {Path}", _credentialsPath);
            return creds ?? new AllPlatformCredentials();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load credentials");
            return new AllPlatformCredentials();
        }
    }

    public void SaveCredentials()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_credentialsPath)!);

            var json = JsonSerializer.Serialize(_credentials, new JsonSerializerOptions
            {
                WriteIndented = false
            });
            var encryptedData = Encrypt(json);
            File.WriteAllBytes(_credentialsPath, encryptedData);

            _logger?.LogInformation("Saved credentials to {Path}", _credentialsPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save credentials");
            throw;
        }
    }

    private byte[] Encrypt(string plainText)
    {
        if (_encryptionKey == null)
            return Encoding.UTF8.GetBytes(plainText);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return result;
    }

    private string Decrypt(byte[] encryptedData)
    {
        if (_encryptionKey == null || encryptedData.Length < 16)
            return Encoding.UTF8.GetString(encryptedData);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        // Extract IV from beginning
        var iv = new byte[16];
        Buffer.BlockCopy(encryptedData, 0, iv, 0, 16);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(
            encryptedData, 16, encryptedData.Length - 16);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    #endregion

    #region Credential Management

    public AllPlatformCredentials GetAllCredentials() => _credentials;

    public void UpdateCredentials(FacebookCredentials credentials)
    {
        _credentials.Facebook = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(InstagramCredentials credentials)
    {
        _credentials.Instagram = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(TikTokCredentials credentials)
    {
        _credentials.TikTok = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(TwitterCredentials credentials)
    {
        _credentials.Twitter = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(LineCredentials credentials)
    {
        _credentials.Line = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(YouTubeCredentials credentials)
    {
        _credentials.YouTube = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(ThreadsCredentials credentials)
    {
        _credentials.Threads = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(LinkedInCredentials credentials)
    {
        _credentials.LinkedIn = credentials;
        SaveCredentials();
    }

    public void UpdateCredentials(PinterestCredentials credentials)
    {
        _credentials.Pinterest = credentials;
        SaveCredentials();
    }

    public List<(SocialPlatform Platform, bool IsConfigured, string Status)> GetPlatformStatusSummary()
    {
        return new List<(SocialPlatform, bool, string)>
        {
            (SocialPlatform.Facebook, _credentials.Facebook.IsConfigured, GetStatus(_credentials.Facebook)),
            (SocialPlatform.Instagram, _credentials.Instagram.IsConfigured, GetStatus(_credentials.Instagram)),
            (SocialPlatform.TikTok, _credentials.TikTok.IsConfigured, GetStatus(_credentials.TikTok)),
            (SocialPlatform.Twitter, _credentials.Twitter.IsConfigured, GetStatus(_credentials.Twitter)),
            (SocialPlatform.Line, _credentials.Line.IsConfigured, GetStatus(_credentials.Line)),
            (SocialPlatform.YouTube, _credentials.YouTube.IsConfigured, GetStatus(_credentials.YouTube)),
            (SocialPlatform.Threads, _credentials.Threads.IsConfigured, GetStatus(_credentials.Threads)),
            (SocialPlatform.LinkedIn, _credentials.LinkedIn.IsConfigured, GetStatus(_credentials.LinkedIn)),
            (SocialPlatform.Pinterest, _credentials.Pinterest.IsConfigured, GetStatus(_credentials.Pinterest))
        };
    }

    private static string GetStatus(BasePlatformCredentials creds)
    {
        if (!creds.IsConfigured) return "Not configured";
        if (creds.TokenExpiresAt.HasValue && creds.TokenExpiresAt.Value < DateTime.UtcNow)
            return "Token expired";
        if (creds.LastTestSuccess.HasValue)
            return creds.LastTestSuccess.Value ? "Connected" : "Connection failed";
        return "Configured";
    }

    #endregion

    #region Connection Testing

    public async Task<(bool Success, string Message)> TestConnectionAsync(SocialPlatform platform, CancellationToken ct = default)
    {
        try
        {
            var result = platform switch
            {
                SocialPlatform.Facebook => await TestFacebookAsync(ct),
                SocialPlatform.Instagram => await TestInstagramAsync(ct),
                SocialPlatform.TikTok => await TestTikTokAsync(ct),
                SocialPlatform.Twitter => await TestTwitterAsync(ct),
                SocialPlatform.Line => await TestLineAsync(ct),
                SocialPlatform.YouTube => await TestYouTubeAsync(ct),
                SocialPlatform.Threads => await TestThreadsAsync(ct),
                SocialPlatform.LinkedIn => await TestLinkedInAsync(ct),
                SocialPlatform.Pinterest => await TestPinterestAsync(ct),
                _ => (false, "Unknown platform")
            };

            // Update test result
            UpdateTestResult(platform, result.Item1);

            _logger?.LogInformation("Connection test for {Platform}: {Success} - {Message}",
                platform, result.Item1, result.Item2);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Connection test failed for {Platform}", platform);
            UpdateTestResult(platform, false);
            return (false, $"Error: {ex.Message}");
        }
    }

    private void UpdateTestResult(SocialPlatform platform, bool success)
    {
        var now = DateTime.UtcNow;
        switch (platform)
        {
            case SocialPlatform.Facebook:
                _credentials.Facebook.LastTestSuccess = success;
                _credentials.Facebook.LastTestedAt = now;
                break;
            case SocialPlatform.Instagram:
                _credentials.Instagram.LastTestSuccess = success;
                _credentials.Instagram.LastTestedAt = now;
                break;
            case SocialPlatform.TikTok:
                _credentials.TikTok.LastTestSuccess = success;
                _credentials.TikTok.LastTestedAt = now;
                break;
            case SocialPlatform.Twitter:
                _credentials.Twitter.LastTestSuccess = success;
                _credentials.Twitter.LastTestedAt = now;
                break;
            case SocialPlatform.Line:
                _credentials.Line.LastTestSuccess = success;
                _credentials.Line.LastTestedAt = now;
                break;
            case SocialPlatform.YouTube:
                _credentials.YouTube.LastTestSuccess = success;
                _credentials.YouTube.LastTestedAt = now;
                break;
            case SocialPlatform.Threads:
                _credentials.Threads.LastTestSuccess = success;
                _credentials.Threads.LastTestedAt = now;
                break;
            case SocialPlatform.LinkedIn:
                _credentials.LinkedIn.LastTestSuccess = success;
                _credentials.LinkedIn.LastTestedAt = now;
                break;
            case SocialPlatform.Pinterest:
                _credentials.Pinterest.LastTestSuccess = success;
                _credentials.Pinterest.LastTestedAt = now;
                break;
        }
        SaveCredentials();
    }

    private async Task<(bool Success, string Message)> TestFacebookAsync(CancellationToken ct)
    {
        var creds = _credentials.Facebook;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            // Test by getting user/page info
            var url = $"https://graph.facebook.com/v18.0/me?access_token={creds.AccessToken}";
            var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var name = data.TryGetProperty("name", out var n) ? n.GetString() : "Unknown";
                return (true, $"Connected as {name}");
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            if (error.Contains("expired"))
                return (false, "Access token expired");

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestInstagramAsync(CancellationToken ct)
    {
        var creds = _credentials.Instagram;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            // Instagram uses Facebook Graph API
            var url = $"https://graph.facebook.com/v18.0/me?fields=id,username&access_token={creds.AccessToken}";
            var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var username = data.TryGetProperty("username", out var u) ? u.GetString() : "Unknown";
                return (true, $"Connected as @{username}");
            }

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestTikTokAsync(CancellationToken ct)
    {
        var creds = _credentials.TikTok;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

            var response = await _httpClient.GetAsync(
                "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (data.TryGetProperty("data", out var userData) &&
                    userData.TryGetProperty("user", out var user))
                {
                    var name = user.TryGetProperty("display_name", out var n) ? n.GetString() : "Unknown";
                    return (true, $"Connected as {name}");
                }
                return (true, "Connected");
            }

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestTwitterAsync(CancellationToken ct)
    {
        var creds = _credentials.Twitter;
        if (string.IsNullOrEmpty(creds.BearerToken))
            return (false, "Bearer token not configured");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.BearerToken}");

            var response = await _httpClient.GetAsync(
                "https://api.twitter.com/2/users/me", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (data.TryGetProperty("data", out var userData))
                {
                    var username = userData.TryGetProperty("username", out var u) ? u.GetString() : "Unknown";
                    return (true, $"Connected as @{username}");
                }
                return (true, "Connected");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Invalid or expired bearer token");

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestLineAsync(CancellationToken ct)
    {
        var creds = _credentials.Line;
        if (string.IsNullOrEmpty(creds.ChannelAccessToken))
            return (false, "Channel access token not configured");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.ChannelAccessToken}");

            var response = await _httpClient.GetAsync(
                "https://api.line.me/v2/bot/info", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var name = data.TryGetProperty("displayName", out var n) ? n.GetString() : "Unknown";
                return (true, $"Connected as {name}");
            }

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestYouTubeAsync(CancellationToken ct)
    {
        var creds = _credentials.YouTube;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

            var response = await _httpClient.GetAsync(
                "https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (data.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                {
                    var channel = items[0];
                    if (channel.TryGetProperty("snippet", out var snippet))
                    {
                        var title = snippet.TryGetProperty("title", out var t) ? t.GetString() : "Unknown";
                        return (true, $"Connected as {title}");
                    }
                }
                return (true, "Connected");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Access token expired - refresh required");

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestThreadsAsync(CancellationToken ct)
    {
        var creds = _credentials.Threads;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            // Threads uses Instagram Graph API
            var url = $"https://graph.threads.net/v1.0/me?fields=id,username&access_token={creds.AccessToken}";
            var response = await _httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var username = data.TryGetProperty("username", out var u) ? u.GetString() : "Unknown";
                return (true, $"Connected as @{username}");
            }

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestLinkedInAsync(CancellationToken ct)
    {
        var creds = _credentials.LinkedIn;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

            var response = await _httpClient.GetAsync(
                "https://api.linkedin.com/v2/userinfo", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var name = data.TryGetProperty("name", out var n) ? n.GetString() : "Unknown";
                return (true, $"Connected as {name}");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                return (false, "Access token expired");

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    private async Task<(bool Success, string Message)> TestPinterestAsync(CancellationToken ct)
    {
        var creds = _credentials.Pinterest;
        if (string.IsNullOrEmpty(creds.AccessToken))
            return (false, "Access token not configured");

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {creds.AccessToken}");

            var response = await _httpClient.GetAsync(
                "https://api.pinterest.com/v5/user_account", ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var username = data.TryGetProperty("username", out var u) ? u.GetString() : "Unknown";
                return (true, $"Connected as @{username}");
            }

            return (false, $"API error: {response.StatusCode}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Network error: {ex.Message}");
        }
    }

    #endregion

    #region Token Refresh

    public async Task<bool> RefreshTokenAsync(SocialPlatform platform, CancellationToken ct = default)
    {
        try
        {
            var success = platform switch
            {
                SocialPlatform.Facebook => await RefreshFacebookTokenAsync(ct),
                SocialPlatform.YouTube => await RefreshYouTubeTokenAsync(ct),
                _ => false // Most platforms require re-authorization
            };

            if (success)
            {
                _logger?.LogInformation("Token refreshed for {Platform}", platform);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Token refresh failed for {Platform}", platform);
            return false;
        }
    }

    private async Task<bool> RefreshFacebookTokenAsync(CancellationToken ct)
    {
        var creds = _credentials.Facebook;
        if (string.IsNullOrEmpty(creds.AccessToken) ||
            string.IsNullOrEmpty(creds.AppId) ||
            string.IsNullOrEmpty(creds.AppSecret))
            return false;

        try
        {
            var url = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                     $"?grant_type=fb_exchange_token" +
                     $"&client_id={creds.AppId}" +
                     $"&client_secret={creds.AppSecret}" +
                     $"&fb_exchange_token={creds.AccessToken}";

            var response = await _httpClient.GetAsync(url, ct);
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (data.TryGetProperty("access_token", out var newToken))
                {
                    creds.AccessToken = newToken.GetString() ?? creds.AccessToken;
                    if (data.TryGetProperty("expires_in", out var expires))
                    {
                        creds.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expires.GetInt64());
                    }
                    SaveCredentials();
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> RefreshYouTubeTokenAsync(CancellationToken ct)
    {
        var creds = _credentials.YouTube;
        if (string.IsNullOrEmpty(creds.RefreshToken) ||
            string.IsNullOrEmpty(creds.ClientId) ||
            string.IsNullOrEmpty(creds.ClientSecret))
            return false;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = creds.ClientId,
                ["client_secret"] = creds.ClientSecret,
                ["refresh_token"] = creds.RefreshToken,
                ["grant_type"] = "refresh_token"
            });

            var response = await _httpClient.PostAsync(
                "https://oauth2.googleapis.com/token", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (data.TryGetProperty("access_token", out var newToken))
                {
                    creds.AccessToken = newToken.GetString() ?? creds.AccessToken;
                    if (data.TryGetProperty("expires_in", out var expires))
                    {
                        creds.TokenExpiresAt = DateTime.UtcNow.AddSeconds(expires.GetInt64());
                    }
                    SaveCredentials();
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

#region Platform Credential Models

public abstract class BasePlatformCredentials
{
    public bool IsConfigured { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public DateTime? LastTestedAt { get; set; }
    public bool? LastTestSuccess { get; set; }
}

public class AllPlatformCredentials
{
    public FacebookCredentials Facebook { get; set; } = new();
    public InstagramCredentials Instagram { get; set; } = new();
    public TikTokCredentials TikTok { get; set; } = new();
    public TwitterCredentials Twitter { get; set; } = new();
    public LineCredentials Line { get; set; } = new();
    public YouTubeCredentials YouTube { get; set; } = new();
    public ThreadsCredentials Threads { get; set; } = new();
    public LinkedInCredentials LinkedIn { get; set; } = new();
    public PinterestCredentials Pinterest { get; set; } = new();
}

public class FacebookCredentials : BasePlatformCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string PageId { get; set; } = "";
    public string PageAccessToken { get; set; } = "";
}

public class InstagramCredentials : BasePlatformCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string BusinessAccountId { get; set; } = "";
}

public class TikTokCredentials : BasePlatformCredentials
{
    public string ClientKey { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string OpenId { get; set; } = "";
}

public class TwitterCredentials : BasePlatformCredentials
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AccessTokenSecret { get; set; } = "";
    public string BearerToken { get; set; } = "";
}

public class LineCredentials : BasePlatformCredentials
{
    public string ChannelId { get; set; } = "";
    public string ChannelSecret { get; set; } = "";
    public string ChannelAccessToken { get; set; } = "";
}

public class YouTubeCredentials : BasePlatformCredentials
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string ChannelId { get; set; } = "";
}

public class ThreadsCredentials : BasePlatformCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string UserId { get; set; } = "";
}

public class LinkedInCredentials : BasePlatformCredentials
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string OrganizationId { get; set; } = "";
}

public class PinterestCredentials : BasePlatformCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string BoardId { get; set; } = "";
}

#endregion
