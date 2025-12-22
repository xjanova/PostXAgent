using System.Text.Json;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// Dynamic Code Executor - รัน JavaScript code ที่ AI generate มาใน WebView
/// รองรับการแทนที่ placeholders และ error handling
/// </summary>
public class DynamicCodeExecutor
{
    private readonly ILogger<DynamicCodeExecutor> _logger;
    private readonly BrowserController _browserController;

    // Execution history for learning
    private readonly List<CodeExecutionRecord> _executionHistory = new();

    public DynamicCodeExecutor(
        BrowserController browserController,
        ILogger<DynamicCodeExecutor>? logger = null)
    {
        _browserController = browserController;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DynamicCodeExecutor>();
    }

    #region Main Execution Methods

    /// <summary>
    /// Execute AI-generated JavaScript code in WebView
    /// </summary>
    public async Task<CodeExecutionResult> ExecuteAsync(
        string code,
        PostContent content,
        string pageUrl,
        CancellationToken ct = default)
    {
        var result = new CodeExecutionResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("🚀 Executing AI-generated code on {Url}", pageUrl);

            // Step 1: Replace placeholders with actual content
            var preparedCode = PrepareCodeWithContent(code, content);

            // Step 2: Navigate to page if needed
            var currentUrl = _browserController.CurrentUrl;
            if (string.IsNullOrEmpty(currentUrl) || !currentUrl.Contains(new Uri(pageUrl).Host))
            {
                await _browserController.NavigateAsync(pageUrl, ct);
                await Task.Delay(3000, ct); // Wait for page load
            }

            // Step 3: Inject helper functions
            await InjectHelperFunctionsAsync(ct);

            // Step 4: Execute the main code
            var executionResult = await _browserController.ExecuteScriptAsync(preparedCode, ct);

            // Step 5: Parse result
            result = ParseExecutionResult(executionResult, result);
            result.CompletedAt = DateTime.UtcNow;

            // Record execution
            RecordExecution(code, content, result);

            if (result.Success)
            {
                _logger.LogInformation("✅ Code execution successful. PostId: {PostId}", result.PostId);
            }
            else
            {
                _logger.LogWarning("❌ Code execution failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code execution failed with exception");
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Execute code with step-by-step monitoring
    /// </summary>
    public async Task<CodeExecutionResult> ExecuteWithMonitoringAsync(
        string code,
        PostContent content,
        string pageUrl,
        Action<CodeExecutionStep>? onStepComplete = null,
        CancellationToken ct = default)
    {
        var result = new CodeExecutionResult
        {
            StartedAt = DateTime.UtcNow,
            Steps = new List<CodeExecutionStep>()
        };

        try
        {
            _logger.LogInformation("🚀 Executing code with monitoring on {Url}", pageUrl);

            // Prepare code
            var preparedCode = PrepareCodeWithContent(code, content);

            // Navigate if needed
            var currentUrl = _browserController.CurrentUrl;
            if (string.IsNullOrEmpty(currentUrl) || !currentUrl.Contains(new Uri(pageUrl).Host))
            {
                result.Steps.Add(new CodeExecutionStep
                {
                    StepName = "Navigation",
                    Status = "started",
                    StartedAt = DateTime.UtcNow
                });

                await _browserController.NavigateAsync(pageUrl, ct);
                await Task.Delay(3000, ct); // Wait for page load

                result.Steps.Last().Status = "completed";
                result.Steps.Last().CompletedAt = DateTime.UtcNow;
                onStepComplete?.Invoke(result.Steps.Last());
            }

            // Inject helpers
            await InjectHelperFunctionsAsync(ct);

            // Wrap code with step reporting
            var monitoredCode = WrapCodeWithMonitoring(preparedCode);

            // Execute
            result.Steps.Add(new CodeExecutionStep
            {
                StepName = "Main Execution",
                Status = "started",
                StartedAt = DateTime.UtcNow
            });

            var executionResult = await _browserController.ExecuteScriptAsync(monitoredCode, ct);
            result = ParseExecutionResult(executionResult, result);

            result.Steps.Last().Status = result.Success ? "completed" : "failed";
            result.Steps.Last().CompletedAt = DateTime.UtcNow;
            result.Steps.Last().Error = result.Error;
            onStepComplete?.Invoke(result.Steps.Last());

            result.CompletedAt = DateTime.UtcNow;
            RecordExecution(code, content, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Monitored execution failed");
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Test code execution in sandbox mode (dry run)
    /// </summary>
    public async Task<CodeExecutionResult> TestExecuteAsync(
        string code,
        PostContent content,
        string pageUrl,
        CancellationToken ct = default)
    {
        var result = new CodeExecutionResult
        {
            StartedAt = DateTime.UtcNow,
            IsDryRun = true
        };

        try
        {
            _logger.LogInformation("🧪 Testing code execution (dry run) on {Url}", pageUrl);

            var preparedCode = PrepareCodeWithContent(code, content);

            // Navigate
            await _browserController.NavigateAsync(pageUrl, ct);
            await Task.Delay(3000, ct); // Wait for page load

            // Inject helpers
            await InjectHelperFunctionsAsync(ct);

            // Wrap code in test mode (no actual submissions)
            var testCode = WrapCodeInTestMode(preparedCode);

            var executionResult = await _browserController.ExecuteScriptAsync(testCode, ct);
            result = ParseExecutionResult(executionResult, result);
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("🧪 Test execution completed. Would succeed: {Success}", result.Success);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test execution failed");
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    #endregion

    #region Code Preparation

    /// <summary>
    /// Replace placeholders with actual content
    /// </summary>
    private string PrepareCodeWithContent(string code, PostContent content)
    {
        var prepared = code;

        // Replace content placeholders
        prepared = prepared.Replace("{{content.text}}", EscapeForJs(content.Text ?? ""));
        prepared = prepared.Replace("{{content.hashtags}}", EscapeForJs(FormatHashtags(content.Hashtags)));
        prepared = prepared.Replace("{{content.link}}", EscapeForJs(content.Link ?? ""));
        prepared = prepared.Replace("{{content.images}}", JsonSerializer.Serialize(content.Images ?? new List<string>()));
        prepared = prepared.Replace("{{content.title}}", EscapeForJs(content.Title ?? ""));
        prepared = prepared.Replace("{{content.location}}", EscapeForJs(content.Location ?? ""));

        // Handle full content object
        var contentJson = JsonSerializer.Serialize(content);
        prepared = prepared.Replace("{{content}}", contentJson);

        return prepared;
    }

    private string EscapeForJs(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        return text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private string FormatHashtags(List<string>? hashtags)
    {
        if (hashtags == null || hashtags.Count == 0) return "";
        return string.Join(" ", hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"));
    }

    /// <summary>
    /// Inject helper functions into the page
    /// </summary>
    private async Task InjectHelperFunctionsAsync(CancellationToken ct)
    {
        var helpers = @"
window.__postx = window.__postx || {};

// Wait for element with retry
window.__postx.waitForElement = async (selectors, timeout = 10000) => {
    const selectorList = Array.isArray(selectors) ? selectors : [selectors];
    const start = Date.now();

    while (Date.now() - start < timeout) {
        for (const selector of selectorList) {
            let element = null;

            // Try different selection methods
            if (selector.startsWith('//')) {
                // XPath
                const result = document.evaluate(selector, document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null);
                element = result.singleNodeValue;
            } else if (selector.startsWith('text:')) {
                // Text content
                const text = selector.substring(5);
                element = [...document.querySelectorAll('*')].find(el =>
                    el.textContent?.trim().toLowerCase().includes(text.toLowerCase())
                );
            } else if (selector.startsWith('aria:')) {
                // Aria label
                const label = selector.substring(5);
                element = document.querySelector(`[aria-label*=""${label}""]`);
            } else {
                // CSS selector
                element = document.querySelector(selector);
            }

            if (element && element.offsetParent !== null) {
                return element;
            }
        }

        await new Promise(r => setTimeout(r, 200));
    }

    throw new Error(`Element not found: ${selectorList.join(', ')}`);
};

// Safe click with retry
window.__postx.safeClick = async (element) => {
    if (!element) throw new Error('Element is null');

    // Scroll into view
    element.scrollIntoView({ behavior: 'smooth', block: 'center' });
    await new Promise(r => setTimeout(r, 300));

    // Try native click
    try {
        element.click();
        return true;
    } catch (e) {
        // Fallback to event dispatch
        const event = new MouseEvent('click', {
            bubbles: true,
            cancelable: true,
            view: window
        });
        element.dispatchEvent(event);
        return true;
    }
};

// Safe type with human-like delay
window.__postx.safeType = async (element, text, clearFirst = true) => {
    if (!element) throw new Error('Element is null');

    element.focus();
    await new Promise(r => setTimeout(r, 100));

    if (clearFirst) {
        element.value = '';
        element.textContent = '';
    }

    // Check if contenteditable
    if (element.contentEditable === 'true') {
        // Type character by character for contenteditable
        for (const char of text) {
            const inputEvent = new InputEvent('input', {
                bubbles: true,
                cancelable: true,
                inputType: 'insertText',
                data: char
            });
            element.textContent += char;
            element.dispatchEvent(inputEvent);
            await new Promise(r => setTimeout(r, Math.random() * 30 + 10));
        }
    } else {
        // For regular inputs
        element.value = text;
        element.dispatchEvent(new Event('input', { bubbles: true }));
        element.dispatchEvent(new Event('change', { bubbles: true }));
    }

    return true;
};

// Wait for navigation/page change
window.__postx.waitForNavigation = async (timeout = 10000) => {
    const start = Date.now();
    const initialUrl = window.location.href;

    while (Date.now() - start < timeout) {
        if (window.location.href !== initialUrl) {
            return true;
        }
        await new Promise(r => setTimeout(r, 100));
    }

    return false;
};

// Check for success indicators
window.__postx.checkSuccess = async (indicators) => {
    const indicatorList = Array.isArray(indicators) ? indicators : [indicators];

    for (const indicator of indicatorList) {
        if (typeof indicator === 'string') {
            // Text-based indicator
            if (document.body.textContent?.toLowerCase().includes(indicator.toLowerCase())) {
                return true;
            }
        } else if (indicator.selector) {
            // Element-based indicator
            try {
                await window.__postx.waitForElement(indicator.selector, indicator.timeout || 3000);
                return true;
            } catch (e) {
                // Continue to next indicator
            }
        }
    }

    return false;
};

// Delay helper
window.__postx.delay = (ms) => new Promise(r => setTimeout(r, ms));

console.log('PostX helpers injected');
";

        await _browserController.ExecuteScriptAsync(helpers, ct);
    }

    /// <summary>
    /// Wrap code with monitoring callbacks
    /// </summary>
    private string WrapCodeWithMonitoring(string code)
    {
        return $@"
(async () => {{
    const __steps = [];
    const __reportStep = (name, status, data) => {{
        __steps.push({{ name, status, data, timestamp: Date.now() }});
        console.log(`[PostX Step] ${{name}}: ${{status}}`);
    }};

    try {{
        __reportStep('init', 'started');

        {code.Replace("(async () => {", "").Replace("})();", "")}

    }} catch (error) {{
        __reportStep('error', 'failed', {{ error: error.message }});
        return {{ success: false, error: error.message, steps: __steps }};
    }}
}})();
";
    }

    /// <summary>
    /// Wrap code in test/dry-run mode
    /// </summary>
    private string WrapCodeInTestMode(string code)
    {
        // Override click on submit buttons to prevent actual submission
        var testWrapper = @"
// Override submit-like clicks
const originalClick = HTMLElement.prototype.click;
HTMLElement.prototype.click = function() {
    const isSubmit = this.type === 'submit' ||
                     this.textContent?.toLowerCase().includes('post') ||
                     this.textContent?.toLowerCase().includes('share') ||
                     this.textContent?.toLowerCase().includes('submit') ||
                     this.getAttribute('aria-label')?.toLowerCase().includes('post');

    if (isSubmit) {
        console.log('[DRY RUN] Would click submit:', this);
        return { dryRun: true, element: this.outerHTML };
    }

    return originalClick.apply(this, arguments);
};
";

        return testWrapper + "\n" + code;
    }

    #endregion

    #region Result Parsing

    /// <summary>
    /// Parse execution result from JavaScript
    /// </summary>
    private CodeExecutionResult ParseExecutionResult(string? jsResult, CodeExecutionResult result)
    {
        if (string.IsNullOrEmpty(jsResult))
        {
            result.Success = false;
            result.Error = "No result returned from code execution";
            return result;
        }

        try
        {
            // Try to parse as JSON
            var parsed = JsonSerializer.Deserialize<JsonElement>(jsResult);

            if (parsed.TryGetProperty("success", out var successProp))
            {
                result.Success = successProp.GetBoolean();
            }

            if (parsed.TryGetProperty("postId", out var postIdProp))
            {
                result.PostId = postIdProp.GetString();
            }

            if (parsed.TryGetProperty("error", out var errorProp))
            {
                result.Error = errorProp.GetString();
            }

            if (parsed.TryGetProperty("postUrl", out var urlProp))
            {
                result.PostUrl = urlProp.GetString();
            }

            if (parsed.TryGetProperty("dryRun", out var dryRunProp))
            {
                result.IsDryRun = dryRunProp.GetBoolean();
            }
        }
        catch (JsonException)
        {
            // Result is not JSON, check for simple success/failure
            var lower = jsResult.ToLower();
            result.Success = lower.Contains("true") || lower.Contains("success");
            if (!result.Success)
            {
                result.Error = jsResult;
            }
        }

        return result;
    }

    #endregion

    #region Execution History

    private void RecordExecution(string code, PostContent content, CodeExecutionResult result)
    {
        _executionHistory.Add(new CodeExecutionRecord
        {
            Code = code,
            Content = content,
            Result = result,
            ExecutedAt = DateTime.UtcNow
        });

        // Keep only last 100 records
        while (_executionHistory.Count > 100)
        {
            _executionHistory.RemoveAt(0);
        }
    }

    /// <summary>
    /// Get execution history for analysis
    /// </summary>
    public List<CodeExecutionRecord> GetExecutionHistory(int? limit = null)
    {
        var history = _executionHistory.OrderByDescending(h => h.ExecutedAt).ToList();
        return limit.HasValue ? history.Take(limit.Value).ToList() : history;
    }

    /// <summary>
    /// Get success rate for recent executions
    /// </summary>
    public double GetSuccessRate(int recentCount = 10)
    {
        var recent = _executionHistory.TakeLast(recentCount).ToList();
        if (recent.Count == 0) return 0;
        return (double)recent.Count(r => r.Result.Success) / recent.Count;
    }

    #endregion
}

#region Models

public class CodeExecutionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public bool IsDryRun { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public List<CodeExecutionStep>? Steps { get; set; }

    public TimeSpan Duration => (CompletedAt ?? DateTime.UtcNow) - StartedAt;
}

public class CodeExecutionStep
{
    public string StepName { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CodeExecutionRecord
{
    public string Code { get; set; } = "";
    public PostContent Content { get; set; } = new();
    public CodeExecutionResult Result { get; set; } = new();
    public DateTime ExecutedAt { get; set; }
}

public class PostContent
{
    public string? Text { get; set; }
    public string? Title { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? Link { get; set; }
    public List<string>? Images { get; set; }
    public string? Video { get; set; }
    public string? Location { get; set; }
}

#endregion
