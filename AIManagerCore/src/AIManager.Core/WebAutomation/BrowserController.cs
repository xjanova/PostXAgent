using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// Browser Controller - ควบคุม Browser ผ่าน Playwright หรือ Puppeteer
/// สำหรับ Web Automation และ Recording
/// </summary>
public class BrowserController : IAsyncDisposable
{
    private readonly ILogger<BrowserController> _logger;
    private readonly BrowserConfig _config;

    private Process? _browserProcess;
    private HttpClient? _cdpClient;
    private bool _isConnected;

    // Event handlers
    public event Func<string, Task>? OnPageNavigated;

    public bool IsRecording { get; private set; }
    public string? CurrentUrl { get; private set; }
    public string SessionId { get; } = Guid.NewGuid().ToString();

    public BrowserController(ILogger<BrowserController> logger, BrowserConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new BrowserConfig();
    }

    /// <summary>
    /// เริ่มต้น Browser
    /// </summary>
    public async Task<bool> LaunchAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Launching browser...");

        try
        {
            var browserPath = FindBrowserPath();
            if (string.IsNullOrEmpty(browserPath))
            {
                _logger.LogError("Browser not found");
                return false;
            }

            // เปิด browser ในโหมด debug
            var port = _config.DebugPort;
            var args = new List<string>
            {
                $"--remote-debugging-port={port}",
                "--no-first-run",
                "--no-default-browser-check"
            };

            if (_config.Headless)
            {
                args.Add("--headless=new");
            }

            if (!string.IsNullOrEmpty(_config.UserDataDir))
            {
                args.Add($"--user-data-dir={_config.UserDataDir}");
            }

            if (_config.DisableGpu)
            {
                args.Add("--disable-gpu");
            }

            // Window size
            args.Add($"--window-size={_config.WindowWidth},{_config.WindowHeight}");

            _browserProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = browserPath,
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    CreateNoWindow = _config.Headless
                }
            };

            _browserProcess.Start();

            // รอให้ browser พร้อม
            await Task.Delay(2000, ct);

            // เชื่อมต่อผ่าน CDP
            _cdpClient = new HttpClient();
            var response = await _cdpClient.GetStringAsync($"http://localhost:{port}/json/version", ct);

            _isConnected = true;
            _logger.LogInformation("Browser launched successfully");

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch browser");
            return false;
        }
    }

    /// <summary>
    /// นำทางไปยัง URL
    /// </summary>
    public async Task<bool> NavigateAsync(string url, CancellationToken ct = default)
    {
        if (!_isConnected)
        {
            _logger.LogWarning("Browser not connected");
            return false;
        }

        _logger.LogInformation("Navigating to {Url}", url);

        try
        {
            // ใช้ CDP command เพื่อ navigate
            // ในการใช้งานจริงควรใช้ Playwright API
            CurrentUrl = url;
            await OnPageNavigated?.Invoke(url)!;

            return true;
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
        if (!_isConnected)
        {
            return null;
        }

        try
        {
            // Execute script via CDP
            // In real implementation, use Playwright
            _logger.LogDebug("Executing script: {Script}", script.Substring(0, Math.Min(100, script.Length)));

            return null; // Return result from CDP
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script execution failed");
            return null;
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

        _logger.LogInformation("Starting recording session {SessionId}", SessionId);

        // Inject recording script
        var recordingScript = GetRecordingScript();
        await ExecuteScriptAsync(recordingScript, ct);

        IsRecording = true;
    }

    /// <summary>
    /// หยุดการ Recording
    /// </summary>
    public async Task<List<RecordedStep>> StopRecordingAsync(CancellationToken ct = default)
    {
        if (!IsRecording)
        {
            return new List<RecordedStep>();
        }

        _logger.LogInformation("Stopping recording session {SessionId}", SessionId);

        // Get recorded steps from page
        var stepsJson = await ExecuteScriptAsync("return window.__postXAgentRecordedSteps || []", ct);

        IsRecording = false;

        // Parse steps
        if (!string.IsNullOrEmpty(stepsJson))
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<List<RecordedStep>>(stepsJson)
                       ?? new List<RecordedStep>();
            }
            catch
            {
                return new List<RecordedStep>();
            }
        }

        return new List<RecordedStep>();
    }

    /// <summary>
    /// คลิก Element
    /// </summary>
    public async Task<bool> ClickAsync(
        ElementSelector selector,
        int timeout = 10000,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Clicking element: {Selector}", selector.Value);

        var script = $@"
            (function() {{
                const element = {GetSelectorScript(selector)};
                if (element) {{
                    element.click();
                    return true;
                }}
                return false;
            }})()";

        var result = await ExecuteScriptAsync(script, ct);
        return result == "true";
    }

    /// <summary>
    /// พิมพ์ข้อความ
    /// </summary>
    public async Task<bool> TypeAsync(
        ElementSelector selector,
        string text,
        bool clear = true,
        int delay = 50,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Typing into element: {Selector}", selector.Value);

        var escapedText = text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n");

        var script = $@"
            (function() {{
                const element = {GetSelectorScript(selector)};
                if (element) {{
                    element.focus();
                    {(clear ? "element.value = '';" : "")}
                    element.value = '{escapedText}';
                    element.dispatchEvent(new Event('input', {{ bubbles: true }}));
                    element.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    return true;
                }}
                return false;
            }})()";

        var result = await ExecuteScriptAsync(script, ct);
        return result == "true";
    }

    /// <summary>
    /// อัพโหลดไฟล์
    /// </summary>
    public async Task<bool> UploadFileAsync(
        ElementSelector selector,
        string filePath,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Uploading file: {Path}", filePath);

        // File upload requires special handling via CDP
        // In real implementation, use Playwright's setInputFiles

        return false; // Placeholder
    }

    /// <summary>
    /// รอ Element ปรากฏ
    /// </summary>
    public async Task<bool> WaitForElementAsync(
        ElementSelector selector,
        int timeout = 10000,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var interval = 100;

        while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeout)
        {
            ct.ThrowIfCancellationRequested();

            var script = $@"
                (function() {{
                    const element = {GetSelectorScript(selector)};
                    return element !== null;
                }})()";

            var result = await ExecuteScriptAsync(script, ct);
            if (result == "true")
            {
                return true;
            }

            await Task.Delay(interval, ct);
        }

        return false;
    }

    /// <summary>
    /// ถ่าย Screenshot
    /// </summary>
    public async Task<string?> TakeScreenshotAsync(CancellationToken ct = default)
    {
        try
        {
            // Use CDP to capture screenshot
            // Returns base64 encoded image
            return null; // Placeholder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Screenshot failed");
            return null;
        }
    }

    /// <summary>
    /// ดึง HTML ของหน้า
    /// </summary>
    public async Task<string?> GetPageHtmlAsync(CancellationToken ct = default)
    {
        return await ExecuteScriptAsync("return document.documentElement.outerHTML", ct);
    }

    /// <summary>
    /// ดึงข้อมูล Element
    /// </summary>
    public async Task<RecordedElement?> GetElementInfoAsync(
        ElementSelector selector,
        CancellationToken ct = default)
    {
        var script = $@"
            (function() {{
                const element = {GetSelectorScript(selector)};
                if (!element) return null;

                const rect = element.getBoundingClientRect();
                const computed = window.getComputedStyle(element);

                return JSON.stringify({{
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
                }});
            }})()";

        var result = await ExecuteScriptAsync(script, ct);
        if (!string.IsNullOrEmpty(result))
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<RecordedElement>(result);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// บันทึก Cookies
    /// </summary>
    public async Task<List<BrowserCookie>> GetCookiesAsync(CancellationToken ct = default)
    {
        // Get cookies via CDP
        return new List<BrowserCookie>();
    }

    /// <summary>
    /// โหลด Cookies
    /// </summary>
    public async Task SetCookiesAsync(List<BrowserCookie> cookies, CancellationToken ct = default)
    {
        // Set cookies via CDP
    }

    /// <summary>
    /// บันทึก Session (cookies + localStorage)
    /// </summary>
    public async Task<WebCredentials> SaveSessionAsync(CancellationToken ct = default)
    {
        var cookies = await GetCookiesAsync(ct);

        var localStorageScript = @"
            (function() {
                const items = {};
                for (let i = 0; i < localStorage.length; i++) {
                    const key = localStorage.key(i);
                    items[key] = localStorage.getItem(key);
                }
                return JSON.stringify(items);
            })()";

        var localStorageJson = await ExecuteScriptAsync(localStorageScript, ct);
        var localStorage = string.IsNullOrEmpty(localStorageJson)
            ? new Dictionary<string, string>()
            : Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(localStorageJson);

        return new WebCredentials
        {
            Cookies = cookies,
            LocalStorage = localStorage
        };
    }

    /// <summary>
    /// โหลด Session
    /// </summary>
    public async Task RestoreSessionAsync(WebCredentials credentials, CancellationToken ct = default)
    {
        if (credentials.Cookies != null)
        {
            await SetCookiesAsync(credentials.Cookies, ct);
        }

        if (credentials.LocalStorage != null && credentials.LocalStorage.Count > 0)
        {
            foreach (var (key, value) in credentials.LocalStorage)
            {
                var script = $"localStorage.setItem('{key}', '{value}')";
                await ExecuteScriptAsync(script, ct);
            }
        }
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
            (function() {
                if (window.__postXAgentRecorder) return;

                window.__postXAgentRecordedSteps = [];
                window.__postXAgentRecorder = true;

                function getElementInfo(element) {
                    const rect = element.getBoundingClientRect();
                    return {
                        tagName: element.tagName.toLowerCase(),
                        id: element.id || null,
                        className: element.className || null,
                        name: element.name || null,
                        type: element.type || null,
                        placeholder: element.placeholder || null,
                        textContent: element.textContent?.trim().substring(0, 100) || null,
                        xpath: getXPath(element),
                        cssSelector: getCssSelector(element),
                        attributes: Object.fromEntries([...element.attributes].map(a => [a.name, a.value])),
                        position: { x: rect.x, y: rect.y, width: rect.width, height: rect.height }
                    };
                }

                function getXPath(element) {
                    if (element.id) return '//*[@id=""' + element.id + '""]';
                    if (element === document.body) return '/html/body';

                    let ix = 0;
                    const siblings = element.parentNode?.childNodes || [];
                    for (let i = 0; i < siblings.length; i++) {
                        const sibling = siblings[i];
                        if (sibling === element) {
                            return getXPath(element.parentNode) + '/' + element.tagName.toLowerCase() + '[' + (ix + 1) + ']';
                        }
                        if (sibling.nodeType === 1 && sibling.tagName === element.tagName) {
                            ix++;
                        }
                    }
                }

                function getCssSelector(element) {
                    if (element.id) return '#' + element.id;
                    let path = [];
                    while (element && element.nodeType === Node.ELEMENT_NODE) {
                        let selector = element.tagName.toLowerCase();
                        if (element.className) {
                            selector += '.' + element.className.trim().split(/\s+/).join('.');
                        }
                        path.unshift(selector);
                        element = element.parentNode;
                    }
                    return path.join(' > ');
                }

                function recordStep(action, element, value) {
                    window.__postXAgentRecordedSteps.push({
                        timestamp: new Date().toISOString(),
                        action: action,
                        element: element ? getElementInfo(element) : null,
                        value: value || null,
                        pageUrl: window.location.href,
                        pageTitle: document.title
                    });
                }

                // Listen for clicks
                document.addEventListener('click', function(e) {
                    recordStep('click', e.target);
                }, true);

                // Listen for input
                document.addEventListener('input', function(e) {
                    recordStep('type', e.target, e.target.value);
                }, true);

                // Listen for change (select, checkbox, etc)
                document.addEventListener('change', function(e) {
                    recordStep('change', e.target, e.target.value);
                }, true);

                // Listen for file uploads
                document.addEventListener('change', function(e) {
                    if (e.target.type === 'file' && e.target.files.length > 0) {
                        recordStep('upload', e.target, [...e.target.files].map(f => f.name).join(', '));
                    }
                }, true);

                console.log('PostXAgent recorder initialized');
            })();
        ";
    }

    private string FindBrowserPath()
    {
        var possiblePaths = new[]
        {
            // Windows Chrome
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            Environment.ExpandEnvironmentVariables(@"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe"),

            // Windows Edge
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",

            // Linux Chrome
            "/usr/bin/google-chrome",
            "/usr/bin/chromium-browser",

            // macOS Chrome
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null!;
    }

    public async ValueTask DisposeAsync()
    {
        _cdpClient?.Dispose();

        if (_browserProcess != null && !_browserProcess.HasExited)
        {
            _browserProcess.Kill();
            _browserProcess.Dispose();
        }
    }
}

/// <summary>
/// Browser Configuration
/// </summary>
public class BrowserConfig
{
    public bool Headless { get; set; } = false;
    public int DebugPort { get; set; } = 9222;
    public string? UserDataDir { get; set; }
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 800;
    public bool DisableGpu { get; set; } = false;
    public string? ProxyServer { get; set; }
    public int DefaultTimeout { get; set; } = 30000;
}
