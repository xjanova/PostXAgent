using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Text.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// Browser Controller - ควบคุม Browser ผ่าน Playwright
/// สำหรับ Web Automation และ Recording
/// </summary>
public class BrowserController : IAsyncDisposable
{
    private readonly ILogger<BrowserController> _logger;
    private readonly BrowserConfig _config;

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool _isConnected;
    private bool _isInitialized;
    private bool _isPersistentContext;

    // Event handlers
    public event Func<string, Task>? OnPageNavigated;
    public event Func<RecordedStep, Task>? OnStepRecorded;

    public bool IsRecording { get; private set; }
    public string? CurrentUrl => _page?.Url;
    public string SessionId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Check if persistent context is active
    /// </summary>
    public bool IsPersistentContext => _isPersistentContext;

    /// <summary>
    /// Check if browser is launched and ready
    /// </summary>
    public bool IsLaunched => _isConnected && _page != null;

    // Recording state
    private readonly List<RecordedStep> _recordedSteps = new();
    private bool _recordingScriptInjected;

    public BrowserController(ILogger<BrowserController> logger, BrowserConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new BrowserConfig();
    }

    /// <summary>
    /// Initialize Playwright (call once at startup)
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        _logger.LogInformation("Initializing Playwright...");

        try
        {
            _playwright = await Playwright.CreateAsync();
            _isInitialized = true;
            _logger.LogInformation("Playwright initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Playwright. Run 'pwsh bin/Debug/net8.0/playwright.ps1 install' to install browsers");
            throw;
        }
    }

    /// <summary>
    /// เริ่มต้น Browser
    /// When UserDataDir is configured, uses persistent context (cookies/localStorage survive across sessions).
    /// Otherwise, creates a fresh browser each time (original behavior).
    /// </summary>
    public async Task<bool> LaunchAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Launching browser...");

        try
        {
            if (!_isInitialized)
            {
                await InitializeAsync(ct);
            }

            var usePersistent = !string.IsNullOrEmpty(_config.UserDataDir);

            if (usePersistent)
            {
                // Persistent context: cookies/localStorage/sessions survive across launches
                _logger.LogInformation("Using persistent browser context at {UserDataDir}", _config.UserDataDir);

                var userDataDir = _config.UserDataDir!;
                Directory.CreateDirectory(userDataDir);

                var persistentOptions = new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = _config.Headless,
                    SlowMo = _config.SlowMo,
                    Args = new[]
                    {
                        "--no-first-run",
                        "--no-default-browser-check",
                        $"--window-size={_config.WindowWidth},{_config.WindowHeight}"
                    },
                    ViewportSize = new ViewportSize
                    {
                        Width = _config.WindowWidth,
                        Height = _config.WindowHeight
                    },
                    UserAgent = _config.UserAgent,
                    Locale = "th-TH",
                    TimezoneId = "Asia/Bangkok"
                };

                if (!string.IsNullOrEmpty(_config.ProxyServer))
                {
                    persistentOptions.Proxy = new Proxy { Server = _config.ProxyServer };
                }

                // Persistent context = browser + context combined
                var browserType = _config.BrowserType.ToLower() switch
                {
                    "firefox" => _playwright!.Firefox,
                    "webkit" => _playwright!.Webkit,
                    _ => _playwright!.Chromium
                };

                _context = await browserType.LaunchPersistentContextAsync(userDataDir, persistentOptions);
                _browser = null; // No separate browser object for persistent context
                _isPersistentContext = true;

                // Use existing page or create new one
                _page = _context.Pages.Count > 0 ? _context.Pages[0] : await _context.NewPageAsync();
            }
            else
            {
                // Non-persistent: fresh browser each time (original behavior)
                var launchOptions = new BrowserTypeLaunchOptions
                {
                    Headless = _config.Headless,
                    SlowMo = _config.SlowMo,
                    Args = new[]
                    {
                        "--no-first-run",
                        "--no-default-browser-check",
                        $"--window-size={_config.WindowWidth},{_config.WindowHeight}"
                    }
                };

                // Choose browser type
                _browser = _config.BrowserType.ToLower() switch
                {
                    "firefox" => await _playwright!.Firefox.LaunchAsync(launchOptions),
                    "webkit" => await _playwright!.Webkit.LaunchAsync(launchOptions),
                    _ => await _playwright!.Chromium.LaunchAsync(launchOptions)
                };

                // Create context with custom settings
                var contextOptions = new BrowserNewContextOptions
                {
                    ViewportSize = new ViewportSize
                    {
                        Width = _config.WindowWidth,
                        Height = _config.WindowHeight
                    },
                    UserAgent = _config.UserAgent,
                    Locale = "th-TH",
                    TimezoneId = "Asia/Bangkok"
                };

                if (!string.IsNullOrEmpty(_config.ProxyServer))
                {
                    contextOptions.Proxy = new Proxy { Server = _config.ProxyServer };
                }

                // Load storage state if available (for non-persistent sessions)
                var storageStatePath = _config.StorageStatePath ?? GetDefaultStorageStatePath();
                if (File.Exists(storageStatePath))
                {
                    _logger.LogInformation("Loading storage state from {Path}", storageStatePath);
                    contextOptions.StorageStatePath = storageStatePath;
                }

                _context = await _browser.NewContextAsync(contextOptions);
                _page = await _context.NewPageAsync();
                _isPersistentContext = false;
            }

            // Set default timeout
            _page.SetDefaultTimeout(_config.DefaultTimeout);

            // Listen for navigation
            _page.FrameNavigated += async (_, frame) =>
            {
                if (frame == _page.MainFrame && OnPageNavigated != null)
                {
                    await OnPageNavigated.Invoke(frame.Url);
                }
            };

            _isConnected = true;
            _logger.LogInformation("Browser launched successfully - {BrowserType} (persistent={Persistent})",
                _config.BrowserType, _isPersistentContext);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch browser");
            return false;
        }
    }

    /// <summary>
    /// เริ่มต้น Browser ด้วย Persistent Context (convenience method)
    /// Sets UserDataDir on config and delegates to LaunchAsync
    /// </summary>
    public Task<bool> LaunchPersistentAsync(string userDataDir, CancellationToken ct = default)
    {
        _config.UserDataDir = userDataDir;
        return LaunchAsync(ct);
    }

    /// <summary>
    /// นำทางไปยัง URL
    /// </summary>
    public async Task<bool> NavigateAsync(string url, CancellationToken ct = default)
    {
        if (!_isConnected || _page == null)
        {
            _logger.LogWarning("Browser not connected");
            return false;
        }

        _logger.LogInformation("Navigating to {Url}", url);

        try
        {
            var response = await _page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = _config.DefaultTimeout
            });

            return response?.Ok ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed");
            return false;
        }
    }

    /// <summary>
    /// รัน JavaScript บนหน้าเว็บ
    /// </summary>
    public async Task<string?> ExecuteScriptAsync(string script, CancellationToken ct = default)
    {
        if (!_isConnected || _page == null)
        {
            return null;
        }

        try
        {
            var result = await _page.EvaluateAsync<JsonElement>(script);
            return result.ValueKind == JsonValueKind.Undefined ? null : result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed: {Script}", script.Substring(0, Math.Min(100, script.Length)));
            return null;
        }
    }

    /// <summary>
    /// รัน JavaScript และคืนค่า typed result
    /// </summary>
    public async Task<T?> ExecuteScriptAsync<T>(string script, CancellationToken ct = default)
    {
        if (!_isConnected || _page == null)
        {
            return default;
        }

        try
        {
            return await _page.EvaluateAsync<T>(script);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed");
            return default;
        }
    }

    /// <summary>
    /// เริ่มการ Recording
    /// </summary>
    public async Task StartRecordingAsync(CancellationToken ct = default)
    {
        if (IsRecording)
        {
            _logger.LogWarning("Already recording");
            return;
        }

        if (_page == null)
        {
            _logger.LogWarning("Browser not ready for recording");
            return;
        }

        _logger.LogInformation("Starting recording session {SessionId}", SessionId);

        _recordedSteps.Clear();
        IsRecording = true;

        if (!_recordingScriptInjected)
        {
            await _page.AddInitScriptAsync(GetRecordingScript());
            _recordingScriptInjected = true;
        }

        await ExecuteScriptAsync(GetRecordingScript(), ct);

        await _page.ExposeFunctionAsync("__postXAgentRecordStep", (string stepJson) =>
        {
            if (!string.IsNullOrEmpty(stepJson) && IsRecording)
            {
                try
                {
                    var step = JsonSerializer.Deserialize<RecordedStep>(stepJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (step != null)
                    {
                        _recordedSteps.Add(step);
                        _logger.LogDebug("Recorded step: {Action} on {Element}", step.Action, step.Element?.TagName);
                        OnStepRecorded?.Invoke(step);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse recorded step");
                }
            }
        });
    }

    /// <summary>
    /// หยุดการ Recording
    /// </summary>
    public Task<List<RecordedStep>> StopRecordingAsync(CancellationToken ct = default)
    {
        if (!IsRecording)
        {
            return Task.FromResult(new List<RecordedStep>());
        }

        _logger.LogInformation("Stopping recording session {SessionId}. Total steps: {Count}", SessionId, _recordedSteps.Count);
        IsRecording = false;

        return Task.FromResult(_recordedSteps.ToList());
    }

    /// <summary>
    /// คลิก Element
    /// </summary>
    public async Task<bool> SetInputFilesAsync(string selector, string filePath, CancellationToken ct = default)
    {
        if (_page == null) return false;
        _logger.LogDebug("Setting input files on: {Selector} with {File}", selector, filePath);
        try
        {
            var element = await _page.QuerySelectorAsync(selector);
            if (element != null)
            {
                await element.SetInputFilesAsync(filePath);
                return true;
            }
            _logger.LogWarning("File input not found: {Selector}", selector);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SetInputFiles failed: {Selector}", selector);
            return false;
        }
    }

    public async Task<bool> ClickAsync(ElementSelector selector, int timeout = 10000, CancellationToken ct = default)
    {
        if (_page == null) return false;
        _logger.LogDebug("Clicking element: {Selector}", selector.Value);
        try
        {
            var locator = GetLocator(selector);
            await locator.ClickAsync(new LocatorClickOptions { Timeout = timeout });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Click failed on selector: {Selector}", selector.Value);
            return false;
        }
    }

    /// <summary>
    /// พิมพ์ข้อความ
    /// </summary>
    public async Task<bool> TypeAsync(ElementSelector selector, string text, bool clear = true, int delay = 50, CancellationToken ct = default)
    {
        if (_page == null) return false;
        _logger.LogDebug("Typing into element: {Selector}", selector.Value);
        try
        {
            var locator = GetLocator(selector);
            if (clear) await locator.ClearAsync();
            await locator.FillAsync(text);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Type failed on selector: {Selector}", selector.Value);
            return false;
        }
    }

    /// <summary>
    /// พิมพ์ข้อความแบบ human-like (ทีละตัวอักษร)
    /// </summary>
    public async Task<bool> TypeHumanLikeAsync(ElementSelector selector, string text, int minDelay = 30, int maxDelay = 100, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try
        {
            var locator = GetLocator(selector);
            await locator.ClearAsync();
            var random = new Random();
            foreach (var c in text)
            {
                await locator.PressAsync(c.ToString());
                await Task.Delay(random.Next(minDelay, maxDelay), ct);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Human-like type failed");
            return false;
        }
    }

    public async Task<bool> UploadFileAsync(ElementSelector selector, string filePath, CancellationToken ct = default)
    {
        if (_page == null) return false;
        _logger.LogDebug("Uploading file: {Path}", filePath);
        try { var locator = GetLocator(selector); await locator.SetInputFilesAsync(filePath); return true; }
        catch (Exception ex) { _logger.LogWarning(ex, "File upload failed"); return false; }
    }

    public async Task<bool> UploadFilesAsync(ElementSelector selector, IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.SetInputFilesAsync(filePaths.ToArray()); return true; }
        catch (Exception ex) { _logger.LogWarning(ex, "Multiple file upload failed"); return false; }
    }

    public async Task<bool> WaitForElementAsync(ElementSelector selector, int timeout = 10000, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeout }); return true; }
        catch { return false; }
    }

    public async Task<bool> WaitForElementHiddenAsync(ElementSelector selector, int timeout = 10000, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden, Timeout = timeout }); return true; }
        catch { return false; }
    }

    public async Task<bool> SelectOptionAsync(ElementSelector selector, string value, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.SelectOptionAsync(value); return true; }
        catch (Exception ex) { _logger.LogWarning(ex, "Select option failed"); return false; }
    }

    public async Task<bool> SetCheckedAsync(ElementSelector selector, bool isChecked, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.SetCheckedAsync(isChecked); return true; }
        catch (Exception ex) { _logger.LogWarning(ex, "Set checked failed"); return false; }
    }

    public async Task<bool> HoverAsync(ElementSelector selector, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.HoverAsync(); return true; }
        catch (Exception ex) { _logger.LogWarning(ex, "Hover failed"); return false; }
    }

    public async Task<bool> ScrollToElementAsync(ElementSelector selector, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); await locator.ScrollIntoViewIfNeededAsync(); return true; }
        catch (Exception ex) { _logger.LogWarning(ex, "Scroll failed"); return false; }
    }

    public async Task<string?> TakeScreenshotAsync(CancellationToken ct = default)
    {
        if (_page == null) return null;
        try { var bytes = await _page.ScreenshotAsync(new PageScreenshotOptions { Type = ScreenshotType.Png, FullPage = false }); return Convert.ToBase64String(bytes); }
        catch (Exception ex) { _logger.LogError(ex, "Screenshot failed"); return null; }
    }

    public async Task<bool> SaveScreenshotAsync(string filePath, bool fullPage = false, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { await _page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, Type = ScreenshotType.Png, FullPage = fullPage }); return true; }
        catch (Exception ex) { _logger.LogError(ex, "Save screenshot failed"); return false; }
    }

    public async Task<string?> TakeElementScreenshotAsync(ElementSelector selector, CancellationToken ct = default)
    {
        if (_page == null) return null;
        try { var locator = GetLocator(selector); var bytes = await locator.ScreenshotAsync(); return Convert.ToBase64String(bytes); }
        catch (Exception ex) { _logger.LogError(ex, "Element screenshot failed"); return null; }
    }

    public async Task<string?> GetPageHtmlAsync(CancellationToken ct = default)
    {
        if (_page == null) return null;
        return await _page.ContentAsync();
    }

    public async Task<string?> GetTextAsync(ElementSelector selector, CancellationToken ct = default)
    {
        if (_page == null) return null;
        try { var locator = GetLocator(selector); return await locator.TextContentAsync(); }
        catch { return null; }
    }

    public async Task<string?> GetAttributeAsync(ElementSelector selector, string attributeName, CancellationToken ct = default)
    {
        if (_page == null) return null;
        try { var locator = GetLocator(selector); return await locator.GetAttributeAsync(attributeName); }
        catch { return null; }
    }

    public async Task<bool> IsElementVisibleAsync(ElementSelector selector, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { var locator = GetLocator(selector); return await locator.IsVisibleAsync(); }
        catch { return false; }
    }

    public async Task<RecordedElement?> GetElementInfoAsync(ElementSelector selector, CancellationToken ct = default)
    {
        if (_page == null) return null;
        try
        {
            var script = $@"
                (() => {{
                    const element = {GetSelectorScript(selector)};
                    if (!element) return null;
                    const rect = element.getBoundingClientRect();
                    const computed = window.getComputedStyle(element);
                    return {{
                        tagName: element.tagName.toLowerCase(),
                        id: element.id || null,
                        className: element.className || null,
                        name: element.name || null,
                        type: element.type || null,
                        placeholder: element.placeholder || null,
                        textContent: element.textContent?.trim().substring(0, 200) || null,
                        innerHtml: element.innerHTML?.substring(0, 500) || null,
                        attributes: Object.fromEntries([...element.attributes].map(a => [a.name, a.value])),
                        position: {{
                            x: Math.round(rect.x),
                            y: Math.round(rect.y),
                            width: Math.round(rect.width),
                            height: Math.round(rect.height)
                        }}
                    }};
                }})()";
            return await _page.EvaluateAsync<RecordedElement>(script);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Get element info failed"); return null; }
    }

    public async Task<List<BrowserCookie>> GetCookiesAsync(CancellationToken ct = default)
    {
        if (_context == null) return new List<BrowserCookie>();
        try
        {
            var cookies = await _context.CookiesAsync();
            return cookies.Select(c => new BrowserCookie
            {
                Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path,
                Expires = c.Expires > 0 ? DateTimeOffset.FromUnixTimeSeconds((long)c.Expires).DateTime : null,
                HttpOnly = c.HttpOnly, Secure = c.Secure, SameSite = c.SameSite.ToString()
            }).ToList();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Get cookies failed"); return new List<BrowserCookie>(); }
    }

    public async Task SetCookiesAsync(List<BrowserCookie> cookies, CancellationToken ct = default)
    {
        if (_context == null) return;
        try
        {
            var playwrightCookies = cookies.Select(c => new Cookie
            {
                Name = c.Name, Value = c.Value, Domain = c.Domain, Path = c.Path ?? "/",
                Expires = c.Expires.HasValue ? ((DateTimeOffset)c.Expires.Value).ToUnixTimeSeconds() : -1,
                HttpOnly = c.HttpOnly, Secure = c.Secure,
                SameSite = c.SameSite?.ToLower() switch { "strict" => SameSiteAttribute.Strict, "lax" => SameSiteAttribute.Lax, _ => SameSiteAttribute.None }
            }).ToArray();
            await _context.AddCookiesAsync(playwrightCookies);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Set cookies failed"); }
    }

    public async Task<WebCredentials> SaveSessionAsync(CancellationToken ct = default)
    {
        var cookies = await GetCookiesAsync(ct);
        var localStorageScript = @"(() => { const items = {}; for (let i = 0; i < localStorage.length; i++) { const key = localStorage.key(i); items[key] = localStorage.getItem(key); } return items; })()";
        var localStorage = await ExecuteScriptAsync<Dictionary<string, string>>(localStorageScript, ct) ?? new Dictionary<string, string>();
        return new WebCredentials { Cookies = cookies, LocalStorage = localStorage };
    }

    public async Task RestoreSessionAsync(WebCredentials credentials, CancellationToken ct = default)
    {
        if (credentials.Cookies != null && credentials.Cookies.Count > 0)
            await SetCookiesAsync(credentials.Cookies, ct);
        if (credentials.LocalStorage != null && credentials.LocalStorage.Count > 0)
        {
            foreach (var (key, value) in credentials.LocalStorage)
            {
                var escapedKey = key.Replace("'", "\\'");
                var escapedValue = value.Replace("'", "\\'");
                await ExecuteScriptAsync($"localStorage.setItem('{escapedKey}', '{escapedValue}')", ct);
            }
        }
    }

    /// <summary>
    /// บันทึก Storage State ไปยังไฟล์ที่กำหนด (สำหรับ reuse session)
    /// </summary>
    public async Task<string?> SaveStorageStateAsync(string filePath, CancellationToken ct = default)
    {
        if (_context == null) return null;
        try
        {
            var state = await _context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = filePath });
            return state;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Save storage state failed"); return null; }
    }

    /// <summary>
    /// บันทึก Storage State ไปยัง default path (หรือ StorageStatePath ที่กำหนดใน config)
    /// Saves cookies, localStorage for reuse across non-persistent sessions
    /// </summary>
    public async Task SaveDefaultStorageStateAsync(CancellationToken ct = default)
    {
        if (_context == null) return;
        var path = _config.StorageStatePath ?? GetDefaultStorageStatePath();
        var dir = Path.GetDirectoryName(path);
        if (dir != null) Directory.CreateDirectory(dir);
        try
        {
            var state = await _context.StorageStateAsync();
            await File.WriteAllTextAsync(path, state, ct);
            _logger.LogInformation("Storage state saved to {Path}", path);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Save storage state to default path failed"); }
    }

    private static string GetDefaultStorageStatePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "PostXAgent", "browser_state", "storage_state.json");
    }

    /// <summary>
    /// Get default user data directory for a named service (e.g. "facebook", "tiktok")
    /// </summary>
    public static string GetDefaultUserDataDir(string serviceName)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "PostXAgent", "browser_data", serviceName);
    }

    public async Task<bool> GoBackAsync(CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { await _page.GoBackAsync(); return true; } catch { return false; }
    }

    public async Task<bool> GoForwardAsync(CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { await _page.GoForwardAsync(); return true; } catch { return false; }
    }

    public async Task<bool> ReloadAsync(CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { await _page.ReloadAsync(); return true; } catch { return false; }
    }

    public async Task<bool> PressKeyAsync(string key, CancellationToken ct = default)
    {
        if (_page == null) return false;
        try { await _page.Keyboard.PressAsync(key); return true; } catch { return false; }
    }

    public IPage? GetPage() => _page;
    public IBrowserContext? GetContext() => _context;

    private ILocator GetLocator(ElementSelector selector)
    {
        return selector.Type switch
        {
            SelectorType.Id => _page!.Locator($"#{selector.Value}"),
            SelectorType.CSS => _page!.Locator(selector.Value),
            SelectorType.XPath => _page!.Locator($"xpath={selector.Value}"),
            SelectorType.Name => _page!.Locator($"[name=\"{selector.Value}\"]"),
            SelectorType.ClassName => _page!.Locator($".{selector.Value}"),
            SelectorType.TestId => _page!.Locator($"[data-testid=\"{selector.Value}\"]"),
            SelectorType.AriaLabel => _page!.Locator($"[aria-label=\"{selector.Value}\"]"),
            SelectorType.Placeholder => _page!.Locator($"[placeholder=\"{selector.Value}\"]"),
            SelectorType.Text => _page!.GetByText(selector.Value, new PageGetByTextOptions { Exact = selector.Confidence >= 0.9 }),
            SelectorType.Role => _page!.GetByRole(AriaRole.Button).Filter(new LocatorFilterOptions { HasText = selector.Value }),
            _ => _page!.Locator(selector.Value)
        };
    }

    private string GetSelectorScript(ElementSelector selector)
    {
        return selector.Type switch
        {
            SelectorType.Id => $"document.getElementById('{selector.Value}')",
            SelectorType.CSS => $"document.querySelector('{selector.Value}')",
            SelectorType.XPath => $"document.evaluate(\"{selector.Value}\", document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue",
            SelectorType.Name => $"document.querySelector('[name=\"{selector.Value}\"]')",
            SelectorType.ClassName => $"document.querySelector('.{selector.Value}')",
            SelectorType.TestId => $"document.querySelector('[data-testid=\"{selector.Value}\"]')",
            SelectorType.AriaLabel => $"document.querySelector('[aria-label=\"{selector.Value}\"]')",
            SelectorType.Placeholder => $"document.querySelector('[placeholder=\"{selector.Value}\"]')",
            SelectorType.Text => $"[...document.querySelectorAll('*')].find(e => e.textContent?.trim() === '{selector.Value}')",
            _ => "null"
        };
    }

    private string GetRecordingScript()
    {
        return @"
            (() => {
                if (window.__postXAgentRecorder) return;
                window.__postXAgentRecordedSteps = [];
                window.__postXAgentRecorder = true;
                function getElementInfo(element) {
                    const rect = element.getBoundingClientRect();
                    return { tagName: element.tagName.toLowerCase(), id: element.id || null, className: element.className || null, name: element.name || null, type: element.type || null, placeholder: element.placeholder || null, textContent: element.textContent?.trim().substring(0, 100) || null, xpath: getXPath(element), cssSelector: getCssSelector(element), attributes: Object.fromEntries([...element.attributes].map(a => [a.name, a.value])), position: { x: rect.x, y: rect.y, width: rect.width, height: rect.height } };
                }
                function getXPath(element) {
                    if (element.id) return '//*[@id=""' + element.id + '""]';
                    if (element === document.body) return '/html/body';
                    let ix = 0;
                    const siblings = element.parentNode?.childNodes || [];
                    for (let i = 0; i < siblings.length; i++) {
                        const sibling = siblings[i];
                        if (sibling === element) return getXPath(element.parentNode) + '/' + element.tagName.toLowerCase() + '[' + (ix + 1) + ']';
                        if (sibling.nodeType === 1 && sibling.tagName === element.tagName) ix++;
                    }
                }
                function getCssSelector(element) {
                    if (element.id) return '#' + element.id;
                    let path = [];
                    while (element && element.nodeType === Node.ELEMENT_NODE) {
                        let selector = element.tagName.toLowerCase();
                        if (element.className) selector += '.' + element.className.trim().split(/\s+/).join('.');
                        path.unshift(selector);
                        element = element.parentNode;
                    }
                    return path.join(' > ');
                }
                function recordStep(action, element, value) {
                    const step = { timestamp: new Date().toISOString(), action: action, element: element ? getElementInfo(element) : null, value: value || null, pageUrl: window.location.href, pageTitle: document.title };
                    window.__postXAgentRecordedSteps.push(step);
                    if (window.__postXAgentRecordStep) window.__postXAgentRecordStep(JSON.stringify(step));
                }
                document.addEventListener('click', function(e) { recordStep('click', e.target); }, true);
                document.addEventListener('input', function(e) { clearTimeout(e.target.__inputTimeout); e.target.__inputTimeout = setTimeout(() => { recordStep('type', e.target, e.target.value); }, 300); }, true);
                document.addEventListener('change', function(e) { if (e.target.type === 'file') { recordStep('upload', e.target, [...e.target.files].map(f => f.name).join(', ')); } else if (e.target.type === 'checkbox' || e.target.type === 'radio') { recordStep('check', e.target, e.target.checked); } else if (e.target.tagName === 'SELECT') { recordStep('select', e.target, e.target.value); } }, true);
                document.addEventListener('submit', function(e) { recordStep('submit', e.target); }, true);
                console.log('PostXAgent recorder initialized');
            })();
        ";
    }

    public async ValueTask DisposeAsync()
    {
        _isConnected = false;

        if (_isPersistentContext)
        {
            // Persistent context: closing the context also closes the browser
            if (_context != null)
                await _context.CloseAsync();
        }
        else
        {
            // Non-persistent: close page, context, then browser separately
            if (_page != null) await _page.CloseAsync();
            if (_context != null) await _context.CloseAsync();
            if (_browser != null) await _browser.CloseAsync();
        }

        _playwright?.Dispose();
    }
}

/// <summary>
/// Browser Configuration
/// </summary>
public class BrowserConfig
{
    public string BrowserType { get; set; } = "chromium";
    public bool Headless { get; set; } = false;
    public int DebugPort { get; set; } = 9222;
    public string? UserDataDir { get; set; }
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 800;
    public bool DisableGpu { get; set; } = false;
    public string? ProxyServer { get; set; }
    public int DefaultTimeout { get; set; } = 30000;
    public float SlowMo { get; set; } = 0;
    public string? UserAgent { get; set; }
    public string? StorageStatePath { get; set; }
}
