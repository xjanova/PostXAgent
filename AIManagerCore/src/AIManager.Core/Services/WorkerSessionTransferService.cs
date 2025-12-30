using System.Text.Json;
using Microsoft.Extensions.Logging;
using AIManager.Core.Workers;
using AIManager.Core.WebAutomation;
using BrowserCookie = AIManager.Core.Workers.BrowserCookie;

namespace AIManager.Core.Services;

/// <summary>
/// Service for transferring browser sessions between Playwright and WebView2
/// ใช้สำหรับการ sync session ระหว่าง headless browser (Playwright) และ embedded browser (WebView2)
/// </summary>
public class WorkerSessionTransferService
{
    private readonly ILogger<WorkerSessionTransferService> _logger;
    private readonly WorkerManager _workerManager;
    private readonly Dictionary<string, SessionState> _sessionStates = new();
    private readonly object _lock = new();

    public WorkerSessionTransferService(
        WorkerManager workerManager,
        ILogger<WorkerSessionTransferService>? logger = null)
    {
        _workerManager = workerManager;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<WorkerSessionTransferService>();
    }

    /// <summary>
    /// Capture session state from Playwright browser
    /// ดึงข้อมูล session จาก Playwright เพื่อเตรียมย้ายไป WebView2
    /// </summary>
    public async Task<SessionState?> CapturePlaywrightSessionAsync(
        string workerId,
        BrowserController browserController,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Capturing Playwright session for worker {WorkerId}", workerId);

            var state = new SessionState
            {
                WorkerId = workerId,
                CapturedAt = DateTime.UtcNow,
                CurrentUrl = browserController.CurrentUrl
            };

            // Capture cookies - use CurrentUrl as base domain for now
            // TODO: Implement GetCookiesAsync in BrowserController when available
            state.Cookies = new List<BrowserCookie>();
            _logger.LogDebug("Cookie capture not yet implemented for Playwright");

            // Capture localStorage and sessionStorage
            // TODO: Implement GetStorageDataAsync in BrowserController when available
            state.LocalStorage = new Dictionary<string, string>();
            state.SessionStorage = new Dictionary<string, string>();
            _logger.LogDebug("Storage capture not yet implemented for Playwright");

            // Store for later use
            lock (_lock)
            {
                _sessionStates[workerId] = state;
            }

            _logger.LogInformation("Captured session for worker {WorkerId}: {CookieCount} cookies, URL: {Url}",
                workerId, state.Cookies.Count, state.CurrentUrl);

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture Playwright session for worker {WorkerId}", workerId);
            return null;
        }
    }

    /// <summary>
    /// Transfer session to WebView2
    /// นำ session ที่ capture ไว้ไปใช้กับ WebView2
    /// </summary>
    public async Task<bool> TransferToWebView2Async(
        string workerId,
        IWebView2Controller webView2Controller,
        CancellationToken ct = default)
    {
        try
        {
            SessionState? state;
            lock (_lock)
            {
                if (!_sessionStates.TryGetValue(workerId, out state))
                {
                    _logger.LogWarning("No captured session found for worker {WorkerId}", workerId);
                    return false;
                }
            }

            _logger.LogInformation("Transferring session to WebView2 for worker {WorkerId}", workerId);

            // Set cookies
            if (state.Cookies?.Any() == true)
            {
                foreach (var cookie in state.Cookies)
                {
                    await webView2Controller.SetCookieAsync(
                        cookie.Name,
                        cookie.Value,
                        cookie.Domain,
                        cookie.Path,
                        cookie.Expires,
                        cookie.HttpOnly,
                        cookie.Secure,
                        ct);
                }
            }

            // Set localStorage
            if (state.LocalStorage?.Any() == true)
            {
                await webView2Controller.SetLocalStorageAsync(state.LocalStorage, ct);
            }

            // Set sessionStorage
            if (state.SessionStorage?.Any() == true)
            {
                await webView2Controller.SetSessionStorageAsync(state.SessionStorage, ct);
            }

            // Navigate to the URL
            if (!string.IsNullOrEmpty(state.CurrentUrl))
            {
                await webView2Controller.NavigateAsync(state.CurrentUrl, ct);
            }

            // Update worker view mode
            _workerManager.SetWorkerViewMode(workerId, WorkerViewMode.Viewing);

            _logger.LogInformation("Session transferred to WebView2 for worker {WorkerId}", workerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transfer session to WebView2 for worker {WorkerId}", workerId);
            return false;
        }
    }

    /// <summary>
    /// Capture session from WebView2 and prepare for transfer back to Playwright
    /// </summary>
    public async Task<SessionState?> CaptureWebView2SessionAsync(
        string workerId,
        IWebView2Controller webView2Controller,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Capturing WebView2 session for worker {WorkerId}", workerId);

            var state = new SessionState
            {
                WorkerId = workerId,
                CapturedAt = DateTime.UtcNow,
                CurrentUrl = await webView2Controller.GetCurrentUrlAsync(ct)
            };

            // Capture cookies
            state.Cookies = await webView2Controller.GetCookiesAsync(ct) ?? new List<BrowserCookie>();

            // Capture storage
            state.LocalStorage = await webView2Controller.GetLocalStorageAsync(ct) ?? new Dictionary<string, string>();
            state.SessionStorage = await webView2Controller.GetSessionStorageAsync(ct) ?? new Dictionary<string, string>();

            // Store for later use
            lock (_lock)
            {
                _sessionStates[workerId] = state;
            }

            return state;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture WebView2 session for worker {WorkerId}", workerId);
            return null;
        }
    }

    /// <summary>
    /// Transfer session back to Playwright
    /// นำ session จาก WebView2 กลับไปใช้กับ Playwright
    /// </summary>
    public async Task<bool> TransferToPlaywrightAsync(
        string workerId,
        BrowserController browserController,
        CancellationToken ct = default)
    {
        try
        {
            SessionState? state;
            lock (_lock)
            {
                if (!_sessionStates.TryGetValue(workerId, out state))
                {
                    _logger.LogWarning("No captured session found for worker {WorkerId}", workerId);
                    return false;
                }
            }

            _logger.LogInformation("Transferring session to Playwright for worker {WorkerId}", workerId);

            // Set cookies in Playwright
            // TODO: Implement SetCookiesAsync in BrowserController when available
            if (state.Cookies?.Any() == true)
            {
                _logger.LogDebug("Cookie transfer to Playwright not yet implemented ({CookieCount} cookies)",
                    state.Cookies.Count);
            }

            // Set storage
            // TODO: Implement SetStorageDataAsync in BrowserController when available
            if (state.LocalStorage?.Any() == true || state.SessionStorage?.Any() == true)
            {
                _logger.LogDebug("Storage transfer to Playwright not yet implemented");
            }

            // Navigate to the URL
            if (!string.IsNullOrEmpty(state.CurrentUrl))
            {
                await browserController.NavigateAsync(state.CurrentUrl, ct);
            }

            // Update worker view mode
            _workerManager.SetWorkerViewMode(workerId, WorkerViewMode.Headless);

            _logger.LogInformation("Session transferred to Playwright for worker {WorkerId}", workerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transfer session to Playwright for worker {WorkerId}", workerId);
            return false;
        }
    }

    /// <summary>
    /// Get stored session state
    /// </summary>
    public SessionState? GetSessionState(string workerId)
    {
        lock (_lock)
        {
            _sessionStates.TryGetValue(workerId, out var state);
            return state;
        }
    }

    /// <summary>
    /// Clear stored session state
    /// </summary>
    public void ClearSessionState(string workerId)
    {
        lock (_lock)
        {
            _sessionStates.Remove(workerId);
        }
    }

    /// <summary>
    /// Sync cookies only between browsers
    /// </summary>
    public async Task SyncCookiesAsync(
        string workerId,
        BrowserController playwright,
        IWebView2Controller webView2,
        SyncDirection direction,
        CancellationToken ct = default)
    {
        try
        {
            if (direction == SyncDirection.PlaywrightToWebView2)
            {
                // TODO: Implement GetCookiesAsync in BrowserController when available
                _logger.LogDebug("Playwright to WebView2 cookie sync not yet implemented");
            }
            else
            {
                // Get cookies from WebView2 and transfer to Playwright
                var cookies = await webView2.GetCookiesAsync(ct);
                if (cookies != null)
                {
                    // TODO: Implement SetCookiesAsync in BrowserController when available
                    _logger.LogDebug("WebView2 to Playwright cookie sync not yet implemented ({CookieCount} cookies)",
                        cookies.Count);
                }
            }

            _logger.LogDebug("Synced cookies for worker {WorkerId} ({Direction})", workerId, direction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync cookies for worker {WorkerId}", workerId);
        }
    }
}

/// <summary>
/// Session state for transfer between browsers
/// </summary>
public class SessionState
{
    public string WorkerId { get; set; } = "";
    public DateTime CapturedAt { get; set; }
    public string? CurrentUrl { get; set; }
    public List<BrowserCookie> Cookies { get; set; } = new();
    public Dictionary<string, string> LocalStorage { get; set; } = new();
    public Dictionary<string, string> SessionStorage { get; set; } = new();

    /// <summary>
    /// Serialize to JSON for storage
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>
    /// Deserialize from JSON
    /// </summary>
    public static SessionState? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SessionState>(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Direction for syncing data between browsers
/// </summary>
public enum SyncDirection
{
    PlaywrightToWebView2,
    WebView2ToPlaywright
}

/// <summary>
/// Interface for WebView2 controller
/// ใช้สำหรับ abstract WebView2 operations
/// </summary>
public interface IWebView2Controller
{
    Task NavigateAsync(string url, CancellationToken ct = default);
    Task<string?> GetCurrentUrlAsync(CancellationToken ct = default);
    Task SetCookieAsync(string name, string value, string domain, string path,
        DateTime? expires, bool httpOnly, bool secure, CancellationToken ct = default);
    Task<List<BrowserCookie>?> GetCookiesAsync(CancellationToken ct = default);
    Task SetLocalStorageAsync(Dictionary<string, string> data, CancellationToken ct = default);
    Task<Dictionary<string, string>?> GetLocalStorageAsync(CancellationToken ct = default);
    Task SetSessionStorageAsync(Dictionary<string, string> data, CancellationToken ct = default);
    Task<Dictionary<string, string>?> GetSessionStorageAsync(CancellationToken ct = default);
    Task<byte[]?> CaptureScreenshotAsync(CancellationToken ct = default);
    Task ExecuteScriptAsync(string script, CancellationToken ct = default);
    Task<string?> EvaluateScriptAsync(string script, CancellationToken ct = default);
}

/// <summary>
/// Cookie data for Playwright
/// </summary>
public class CookieData
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Path { get; set; } = "/";
    public DateTime? Expires { get; set; }
    public bool HttpOnly { get; set; }
    public bool Secure { get; set; }
    public string SameSite { get; set; } = "Lax";
}

/// <summary>
/// Storage data container
/// </summary>
public class StorageData
{
    public Dictionary<string, string>? LocalStorage { get; set; }
    public Dictionary<string, string>? SessionStorage { get; set; }
}
