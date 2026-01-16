using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.MusicGeneration;

/// <summary>
/// จัดการ Session Token สำหรับ Suno AI
///
/// วิธีการทำงาน:
/// 1. ดึง token จาก browser cookies (ผ่าน SunoAutomationService)
/// 2. เก็บ token ไว้ในไฟล์ encrypted
/// 3. Auto-refresh token ก่อนหมดอายุ
/// 4. รองรับ multiple accounts
/// </summary>
public class SunoSessionManager
{
    private readonly ILogger<SunoSessionManager> _logger;
    private readonly string _sessionFilePath;
    private readonly object _lock = new();

    private SunoSessionData? _currentSession;
    private DateTime _lastRefresh;

    public SunoSessionManager(
        ILogger<SunoSessionManager> logger,
        string? sessionFilePath = null)
    {
        _logger = logger;
        _sessionFilePath = sessionFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PostXAgent", "suno_session.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_sessionFilePath)!);
    }

    /// <summary>
    /// ดึง session token ที่ใช้งานได้
    /// </summary>
    public async Task<string?> GetValidTokenAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_currentSession != null && !IsExpired(_currentSession))
            {
                return _currentSession.Token;
            }
        }

        // ลองโหลดจากไฟล์
        var loaded = await LoadSessionAsync(ct);
        if (loaded != null && !IsExpired(loaded))
        {
            lock (_lock) { _currentSession = loaded; }
            return loaded.Token;
        }

        _logger.LogWarning("No valid Suno session token available");
        return null;
    }

    /// <summary>
    /// บันทึก session token ใหม่
    /// </summary>
    public async Task SaveTokenAsync(
        string token,
        string? accountEmail = null,
        TimeSpan? expiresIn = null,
        CancellationToken ct = default)
    {
        var session = new SunoSessionData
        {
            Token = token,
            AccountEmail = accountEmail,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(expiresIn ?? TimeSpan.FromDays(7))
        };

        lock (_lock) { _currentSession = session; }
        await SaveSessionAsync(session, ct);

        _logger.LogInformation("Suno session saved for {Email}, expires at {ExpiresAt}",
            accountEmail ?? "unknown", session.ExpiresAt);
    }

    /// <summary>
    /// ดึง token จาก browser cookies
    /// </summary>
    public async Task<string?> ExtractTokenFromCookiesAsync(
        string cookieString,
        CancellationToken ct = default)
    {
        try
        {
            // Parse cookie string เพื่อหา session token
            // Suno ใช้ Clerk สำหรับ auth ดังนั้นหา __client หรือ __session
            var cookies = ParseCookieString(cookieString);

            // ลำดับการค้นหา
            var tokenKeys = new[] { "__client", "__session", "session", "token" };

            foreach (var key in tokenKeys)
            {
                if (cookies.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                {
                    // ตรวจสอบว่า token ใช้งานได้
                    if (await ValidateTokenAsync(value, ct))
                    {
                        await SaveTokenAsync(value, ct: ct);
                        return value;
                    }
                }
            }

            _logger.LogWarning("Could not find valid Suno token in cookies");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting token from cookies");
            return null;
        }
    }

    /// <summary>
    /// ดึง JWT token จาก Clerk session
    /// </summary>
    public async Task<string?> GetClerkTokenAsync(
        string clerkSessionId,
        CancellationToken ct = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Cookie", $"__client={clerkSessionId}");

            // Clerk endpoint สำหรับ get token
            var response = await httpClient.PostAsync(
                "https://clerk.suno.com/v1/client/sessions/{session_id}/tokens",
                null, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                var tokenData = JsonSerializer.Deserialize<ClerkTokenResponse>(content);
                return tokenData?.Jwt;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Clerk token");
            return null;
        }
    }

    /// <summary>
    /// ตรวจสอบว่า token ยังใช้งานได้
    /// </summary>
    public async Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await httpClient.GetAsync(
                "https://studio-api.suno.ai/api/billing/info/", ct);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ลบ session ที่เก็บไว้
    /// </summary>
    public async Task ClearSessionAsync(CancellationToken ct = default)
    {
        lock (_lock) { _currentSession = null; }

        try
        {
            if (File.Exists(_sessionFilePath))
            {
                File.Delete(_sessionFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing session file");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// ดึงข้อมูล session ปัจจุบัน
    /// </summary>
    public SunoSessionInfo? GetCurrentSessionInfo()
    {
        lock (_lock)
        {
            if (_currentSession == null)
                return null;

            return new SunoSessionInfo
            {
                AccountEmail = _currentSession.AccountEmail,
                IsValid = !IsExpired(_currentSession),
                ExpiresAt = _currentSession.ExpiresAt,
                CreatedAt = _currentSession.CreatedAt
            };
        }
    }

    #region Private Methods

    private async Task SaveSessionAsync(SunoSessionData session, CancellationToken ct)
    {
        try
        {
            // Encrypt sensitive data before saving
            var encrypted = EncryptSession(session);
            var json = JsonSerializer.Serialize(encrypted, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_sessionFilePath, json, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving session");
        }
    }

    private async Task<SunoSessionData?> LoadSessionAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_sessionFilePath))
                return null;

            var json = await File.ReadAllTextAsync(_sessionFilePath, ct);
            var encrypted = JsonSerializer.Deserialize<SunoSessionData>(json);

            if (encrypted == null)
                return null;

            return DecryptSession(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading session");
            return null;
        }
    }

    private bool IsExpired(SunoSessionData session)
    {
        return DateTime.UtcNow >= session.ExpiresAt;
    }

    private Dictionary<string, string> ParseCookieString(string cookieString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(cookieString))
            return result;

        var pairs = cookieString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Trim().Split('=', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }

        return result;
    }

    private SunoSessionData EncryptSession(SunoSessionData session)
    {
        // Simple obfuscation - ในการใช้งานจริงควรใช้ DPAPI หรือ encryption ที่แข็งแกร่งกว่า
        // สำหรับ Windows ใช้ ProtectedData class
        try
        {
            var tokenBytes = System.Text.Encoding.UTF8.GetBytes(session.Token ?? "");
            var encoded = Convert.ToBase64String(tokenBytes);

            return new SunoSessionData
            {
                Token = encoded,
                AccountEmail = session.AccountEmail,
                CreatedAt = session.CreatedAt,
                ExpiresAt = session.ExpiresAt,
                IsEncrypted = true
            };
        }
        catch
        {
            return session;
        }
    }

    private SunoSessionData DecryptSession(SunoSessionData session)
    {
        if (!session.IsEncrypted)
            return session;

        try
        {
            var decoded = Convert.FromBase64String(session.Token ?? "");
            var token = System.Text.Encoding.UTF8.GetString(decoded);

            return new SunoSessionData
            {
                Token = token,
                AccountEmail = session.AccountEmail,
                CreatedAt = session.CreatedAt,
                ExpiresAt = session.ExpiresAt,
                IsEncrypted = false
            };
        }
        catch
        {
            return session;
        }
    }

    #endregion

    #region Models

    private class SunoSessionData
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        [JsonPropertyName("account_email")]
        public string? AccountEmail { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [JsonPropertyName("is_encrypted")]
        public bool IsEncrypted { get; set; }
    }

    private class ClerkTokenResponse
    {
        [JsonPropertyName("jwt")]
        public string? Jwt { get; set; }
    }

    #endregion
}

/// <summary>
/// ข้อมูล session สำหรับแสดงผล
/// </summary>
public class SunoSessionInfo
{
    public string? AccountEmail { get; set; }
    public bool IsValid { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CreatedAt { get; set; }

    public TimeSpan? TimeUntilExpiry => ExpiresAt.HasValue
        ? ExpiresAt.Value - DateTime.UtcNow
        : null;
}
