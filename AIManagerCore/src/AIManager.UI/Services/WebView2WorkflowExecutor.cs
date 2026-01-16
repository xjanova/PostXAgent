using System.Text.Json;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace AIManager.UI.Services;

/// <summary>
/// Workflow Executor ที่ทำงานผ่าน WebView2
/// ใช้สำหรับ replay workflow ใน WebViewWindow
/// </summary>
public class WebView2WorkflowExecutor
{
    private readonly ILogger<WebView2WorkflowExecutor>? _logger;
    private WebView2? _webView;

    // Events
    public event EventHandler<WorkflowStepEventArgs>? StepStarted;
    public event EventHandler<WorkflowStepEventArgs>? StepCompleted;
    public event EventHandler<WorkflowStepEventArgs>? StepFailed;
    public event EventHandler<WorkflowCompletedEventArgs>? WorkflowCompleted;

    public bool IsExecuting { get; private set; }
    public int CurrentStepIndex { get; private set; }
    public LearnedWorkflow? CurrentWorkflow { get; private set; }

    public WebView2WorkflowExecutor(ILogger<WebView2WorkflowExecutor>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// ตั้งค่า WebView2 ที่จะใช้
    /// </summary>
    public void SetWebView(WebView2 webView)
    {
        _webView = webView;
    }

    /// <summary>
    /// Execute workflow
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        LearnedWorkflow workflow,
        Dictionary<string, string>? variables = null,
        CancellationToken ct = default)
    {
        if (_webView?.CoreWebView2 == null)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Error = "WebView2 not initialized"
            };
        }

        if (IsExecuting)
        {
            return new WorkflowExecutionResult
            {
                Success = false,
                Error = "Another workflow is already executing"
            };
        }

        IsExecuting = true;
        CurrentWorkflow = workflow;
        CurrentStepIndex = 0;

        var result = new WorkflowExecutionResult
        {
            WorkflowId = workflow.Id,
            StartedAt = DateTime.UtcNow
        };

        _logger?.LogInformation("Executing workflow {Id}: {Name} with {Count} steps",
            workflow.Id, workflow.Name, workflow.Steps.Count);

        try
        {
            for (int i = 0; i < workflow.Steps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var step = workflow.Steps[i];
                CurrentStepIndex = i;

                StepStarted?.Invoke(this, new WorkflowStepEventArgs
                {
                    StepIndex = i,
                    Step = step,
                    TotalSteps = workflow.Steps.Count
                });

                var stepResult = await ExecuteStepAsync(step, variables, ct);
                result.StepResults.Add(stepResult);

                if (stepResult.Success)
                {
                    StepCompleted?.Invoke(this, new WorkflowStepEventArgs
                    {
                        StepIndex = i,
                        Step = step,
                        TotalSteps = workflow.Steps.Count,
                        Success = true
                    });

                    _logger?.LogDebug("Step {Index}/{Total} completed: {Action}",
                        i + 1, workflow.Steps.Count, step.Action);
                }
                else
                {
                    if (!step.IsOptional)
                    {
                        StepFailed?.Invoke(this, new WorkflowStepEventArgs
                        {
                            StepIndex = i,
                            Step = step,
                            TotalSteps = workflow.Steps.Count,
                            Error = stepResult.Error
                        });

                        // Try alternative selectors
                        var recovered = await TryAlternativeSelectorsAsync(step, variables, ct);

                        if (!recovered)
                        {
                            result.Success = false;
                            result.FailedAtStep = i;
                            result.Error = stepResult.Error;

                            _logger?.LogWarning("Workflow failed at step {Index}: {Error}",
                                i, stepResult.Error);
                            break;
                        }
                    }
                    else
                    {
                        _logger?.LogDebug("Optional step {Index} skipped: {Error}",
                            i, stepResult.Error);
                    }
                }
            }

            if (!result.FailedAtStep.HasValue)
            {
                result.Success = true;
                _logger?.LogInformation("Workflow {Id} completed successfully", workflow.Id);
            }

            result.CompletedAt = DateTime.UtcNow;

            WorkflowCompleted?.Invoke(this, new WorkflowCompletedEventArgs
            {
                Workflow = workflow,
                Result = result
            });

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Error = "Workflow cancelled";
            result.CompletedAt = DateTime.UtcNow;
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;

            _logger?.LogError(ex, "Workflow execution failed");
            return result;
        }
        finally
        {
            IsExecuting = false;
            CurrentWorkflow = null;
        }
    }

    /// <summary>
    /// Execute single step
    /// </summary>
    public async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowStep step,
        Dictionary<string, string>? variables = null,
        CancellationToken ct = default)
    {
        var result = new StepExecutionResult
        {
            StepId = step.Id,
            Action = step.Action.ToString(),
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Wait before
            if (step.WaitBeforeMs > 0)
            {
                await Task.Delay(step.WaitBeforeMs, ct);
            }

            // Resolve input value
            var value = ResolveValue(step.InputValue ?? step.InputVariable, variables);

            // Execute action
            result.Success = await ExecuteActionAsync(step.Action, step.Selector, value, step.TimeoutMs, ct);

            if (!result.Success)
            {
                result.Error = "Action failed";
            }

            // Wait after
            if (step.WaitAfterMs > 0)
            {
                await Task.Delay(step.WaitAfterMs, ct);
            }

            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    private async Task<bool> ExecuteActionAsync(
        StepAction action,
        ElementSelector selector,
        string? value,
        int timeout,
        CancellationToken ct)
    {
        var selectorJs = GetSelectorScript(selector);
        var timeoutMs = timeout > 0 ? timeout : 10000;

        var script = action switch
        {
            StepAction.Navigate => $@"
                window.location.href = '{EscapeJs(value ?? "")}';
                true;",

            StepAction.Click => $@"
                (function() {{
                    const element = {selectorJs};
                    if (element) {{
                        element.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
                        element.click();
                        return true;
                    }}
                    return false;
                }})();",

            StepAction.Type => $@"
                (function() {{
                    const element = {selectorJs};
                    if (element) {{
                        element.focus();
                        element.value = '';
                        element.value = '{EscapeJs(value ?? "")}';
                        element.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        element.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        return true;
                    }}
                    return false;
                }})();",

            StepAction.Clear => $@"
                (function() {{
                    const element = {selectorJs};
                    if (element) {{
                        element.value = '';
                        element.dispatchEvent(new Event('input', {{ bubbles: true }}));
                        return true;
                    }}
                    return false;
                }})();",

            StepAction.Select => $@"
                (function() {{
                    const element = {selectorJs};
                    if (element && element.tagName === 'SELECT') {{
                        element.value = '{EscapeJs(value ?? "")}';
                        element.dispatchEvent(new Event('change', {{ bubbles: true }}));
                        return true;
                    }}
                    return false;
                }})();",

            StepAction.Scroll => $@"
                (function() {{
                    const element = {selectorJs};
                    if (element) {{
                        element.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
                        return true;
                    }}
                    return false;
                }})();",

            StepAction.Hover => $@"
                (function() {{
                    const element = {selectorJs};
                    if (element) {{
                        element.dispatchEvent(new MouseEvent('mouseenter', {{ bubbles: true }}));
                        element.dispatchEvent(new MouseEvent('mouseover', {{ bubbles: true }}));
                        return true;
                    }}
                    return false;
                }})();",

            StepAction.Wait => $@"
                true;",

            StepAction.WaitForElement => $@"
                (function() {{
                    const element = {selectorJs};
                    return element !== null;
                }})();",

            StepAction.PressKey => $@"
                (function() {{
                    document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', {{ key: '{EscapeJs(value ?? "Enter")}', bubbles: true }}));
                    document.activeElement?.dispatchEvent(new KeyboardEvent('keyup', {{ key: '{EscapeJs(value ?? "Enter")}', bubbles: true }}));
                    return true;
                }})();",

            StepAction.ExecuteScript => value ?? "true;",

            StepAction.Screenshot => "true;",

            _ => "true;"
        };

        try
        {
            // For Wait action, just delay
            if (action == StepAction.Wait)
            {
                await Task.Delay(timeoutMs, ct);
                return true;
            }

            // For WaitForElement, poll until element is found
            if (action == StepAction.WaitForElement)
            {
                var startTime = DateTime.UtcNow;
                while ((DateTime.UtcNow - startTime).TotalMilliseconds < timeoutMs)
                {
                    var found = await ExecuteScriptAsync<bool>(script, ct);
                    if (found) return true;
                    await Task.Delay(100, ct);
                }
                return false;
            }

            var result = await ExecuteScriptAsync<bool>(script, ct);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Action {Action} failed", action);
            return false;
        }
    }

    private async Task<bool> TryAlternativeSelectorsAsync(
        WorkflowStep step,
        Dictionary<string, string>? variables,
        CancellationToken ct)
    {
        if (step.AlternativeSelectors.Count == 0)
            return false;

        foreach (var altSelector in step.AlternativeSelectors.OrderByDescending(s => s.Confidence))
        {
            _logger?.LogDebug("Trying alternative selector: {Selector}", altSelector.Value);

            var altStep = new WorkflowStep
            {
                Id = step.Id,
                Action = step.Action,
                Selector = altSelector,
                InputValue = step.InputValue,
                InputVariable = step.InputVariable,
                TimeoutMs = step.TimeoutMs
            };

            var result = await ExecuteStepAsync(altStep, variables, ct);

            if (result.Success)
            {
                _logger?.LogInformation("Alternative selector worked: {Selector}", altSelector.Value);
                return true;
            }
        }

        return false;
    }

    private string? ResolveValue(string? template, Dictionary<string, string>? variables)
    {
        if (string.IsNullOrEmpty(template) || variables == null)
            return template;

        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }

    private string GetSelectorScript(ElementSelector selector)
    {
        return selector.Type switch
        {
            SelectorType.Id => $"document.getElementById('{EscapeJs(selector.Value)}')",
            SelectorType.CSS => $"document.querySelector('{EscapeJs(selector.Value)}')",
            SelectorType.XPath => $"document.evaluate(\"{EscapeJs(selector.Value)}\", document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue",
            SelectorType.Name => $"document.querySelector('[name=\"{EscapeJs(selector.Value)}\"]')",
            SelectorType.ClassName => $"document.querySelector('.{EscapeJs(selector.Value)}')",
            SelectorType.TestId => $"document.querySelector('[data-testid=\"{EscapeJs(selector.Value)}\"]')",
            SelectorType.AriaLabel => $"document.querySelector('[aria-label=\"{EscapeJs(selector.Value)}\"]')",
            SelectorType.Placeholder => $"document.querySelector('[placeholder=\"{EscapeJs(selector.Value)}\"]')",
            SelectorType.Text => $"[...document.querySelectorAll('*')].find(e => e.textContent?.trim() === '{EscapeJs(selector.Value)}')",
            _ => $"document.querySelector('{EscapeJs(selector.Value)}')"
        };
    }

    private async Task<T?> ExecuteScriptAsync<T>(string script, CancellationToken ct)
    {
        if (_webView?.CoreWebView2 == null)
            return default;

        try
        {
            var resultJson = await _webView.CoreWebView2.ExecuteScriptAsync(script);

            if (string.IsNullOrEmpty(resultJson) || resultJson == "null" || resultJson == "undefined")
                return default;

            // Handle primitive types
            if (typeof(T) == typeof(bool))
            {
                if (bool.TryParse(resultJson.Trim('"'), out var boolResult))
                    return (T)(object)boolResult;
                return (T)(object)(resultJson == "true");
            }

            if (typeof(T) == typeof(string))
                return (T)(object)resultJson.Trim('"');

            return JsonSerializer.Deserialize<T>(resultJson);
        }
        catch
        {
            return default;
        }
    }

    private static string EscapeJs(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    /// <summary>
    /// Stop current execution
    /// </summary>
    public void Stop()
    {
        IsExecuting = false;
    }

    public string ExecutorType => "WebView2";
}

// Note: WorkflowStepEventArgs, WorkflowCompletedEventArgs, WorkflowExecutionResult, StepExecutionResult
// are now defined in AIManager.Core.WebAutomation.Models.WorkflowExecutionModels
