using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// รัน Workflow ที่เรียนรู้มา
/// </summary>
public class WorkflowExecutor
{
    private readonly ILogger<WorkflowExecutor> _logger;
    private readonly BrowserController _browser;
    private readonly WorkflowLearningEngine _learningEngine;
    private readonly AIElementAnalyzer _aiAnalyzer;

    // Events
    public event Func<int, WorkflowStep, Task>? OnStepStarted;
    public event Func<int, WorkflowStep, bool, Task>? OnStepCompleted;
    public event Func<int, WorkflowStep, string, Task>? OnStepFailed;
    public event Func<string, Task>? OnWorkflowCompleted;
    public event Func<string, int, string, Task>? OnWorkflowFailed;

    public WorkflowExecutor(
        ILogger<WorkflowExecutor> logger,
        BrowserController browser,
        WorkflowLearningEngine learningEngine,
        AIElementAnalyzer aiAnalyzer)
    {
        _logger = logger;
        _browser = browser;
        _learningEngine = learningEngine;
        _aiAnalyzer = aiAnalyzer;
    }

    /// <summary>
    /// รัน Workflow พร้อม Content
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteAsync(
        LearnedWorkflow workflow,
        WebPostContent content,
        WebCredentials? credentials = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Executing workflow {Id}: {Name}", workflow.Id, workflow.Name);

        var result = new WorkflowExecutionResult
        {
            WorkflowId = workflow.Id,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Restore session if credentials provided
            if (credentials != null)
            {
                await _browser.RestoreSessionAsync(credentials, ct);
            }

            // Execute each step
            for (int i = 0; i < workflow.Steps.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var step = workflow.Steps[i];
                await OnStepStarted?.Invoke(i, step)!;

                var stepResult = await ExecuteStepAsync(step, content, ct);
                result.StepResults.Add(stepResult);

                if (stepResult.Success)
                {
                    await OnStepCompleted?.Invoke(i, step, true)!;
                    _logger.LogDebug("Step {Index} completed: {Action}", i, step.Action);
                }
                else
                {
                    // ถ้า step ไม่ optional และล้มเหลว
                    if (!step.IsOptional)
                    {
                        await OnStepFailed?.Invoke(i, step, stepResult.Error ?? "Unknown error")!;

                        // พยายามซ่อมอัตโนมัติ
                        var repaired = await TryAutoRepairAndRetry(workflow, i, content, ct);

                        if (!repaired)
                        {
                            result.Success = false;
                            result.FailedAtStep = i;
                            result.Error = stepResult.Error;

                            // อัพเดท workflow
                            await _learningEngine.UpdateWorkflowFromResult(
                                workflow, false, i, stepResult.Error, ct);

                            await OnWorkflowFailed?.Invoke(workflow.Id, i, stepResult.Error ?? "Unknown")!;

                            break;
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Optional step {Index} failed, continuing", i);
                        await OnStepCompleted?.Invoke(i, step, false)!;
                    }
                }
            }

            // ถ้าไม่มี error = success
            if (!result.FailedAtStep.HasValue)
            {
                result.Success = true;
                await _learningEngine.UpdateWorkflowFromResult(workflow, true, ct: ct);
                await OnWorkflowCompleted?.Invoke(workflow.Id)!;
            }

            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Error = "Operation cancelled";
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed");
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;

            await _learningEngine.UpdateWorkflowFromResult(
                workflow, false, null, ex.Message, ct);

            return result;
        }
    }

    /// <summary>
    /// รันทีละ Step (สำหรับ Debug/Teaching)
    /// </summary>
    public async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowStep step,
        WebPostContent content,
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

            // Execute action
            var success = await ExecuteActionAsync(step, content, ct);

            if (!success && step.AlternativeSelectors.Count > 0)
            {
                // Try alternative selectors
                foreach (var altSelector in step.AlternativeSelectors.OrderByDescending(s => s.Confidence))
                {
                    var originalSelector = step.Selector;
                    step.Selector = altSelector;

                    success = await ExecuteActionAsync(step, content, ct);
                    step.Selector = originalSelector;

                    if (success)
                    {
                        _logger.LogDebug("Used alternative selector for step {Id}", step.Id);
                        break;
                    }
                }
            }

            result.Success = success;

            // Wait after
            if (step.WaitAfterMs > 0)
            {
                await Task.Delay(step.WaitAfterMs, ct);
            }

            // Check success condition
            if (step.SuccessCondition != null && success)
            {
                result.Success = await CheckSuccessConditionAsync(step.SuccessCondition, ct);
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
        WorkflowStep step,
        WebPostContent content,
        CancellationToken ct)
    {
        var value = ResolveInputValue(step, content);

        return step.Action switch
        {
            StepAction.Navigate => await _browser.NavigateAsync(value ?? step.Selector.Value, ct),

            StepAction.Click => await _browser.ClickAsync(step.Selector, step.TimeoutMs, ct),

            StepAction.Type => await _browser.TypeAsync(step.Selector, value ?? "", true, 50, ct),

            StepAction.Upload => await _browser.UploadFileAsync(step.Selector, value ?? "", ct),

            StepAction.Wait => await WaitAsync(step.TimeoutMs, ct),

            StepAction.WaitForElement => await _browser.WaitForElementAsync(step.Selector, step.TimeoutMs, ct),

            StepAction.Clear => await ClearElementAsync(step.Selector, ct),

            StepAction.Select => await SelectOptionAsync(step.Selector, value ?? "", ct),

            StepAction.Scroll => await ScrollAsync(step.Selector, ct),

            StepAction.Hover => await HoverAsync(step.Selector, ct),

            StepAction.PressKey => await PressKeyAsync(value ?? "Enter", ct),

            StepAction.ExecuteScript => await ExecuteScriptAsync(value ?? "", ct),

            StepAction.Screenshot => await TakeScreenshotAsync(ct),

            _ => true
        };
    }

    private string? ResolveInputValue(WorkflowStep step, WebPostContent content)
    {
        // ถ้ามี static value ใช้เลย
        if (!string.IsNullOrEmpty(step.InputValue) && string.IsNullOrEmpty(step.InputVariable))
        {
            return step.InputValue;
        }

        // ถ้ามี variable แทนที่ด้วย content
        if (!string.IsNullOrEmpty(step.InputVariable))
        {
            return step.InputVariable switch
            {
                "{{content.text}}" => content.Text,
                "{{content.hashtags}}" => content.Hashtags != null
                    ? string.Join(" ", content.Hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"))
                    : "",
                "{{content.link}}" => content.Link ?? "",
                "{{content.location}}" => content.Location ?? "",
                "{{content.image}}" => content.ImagePaths?.FirstOrDefault() ?? "",
                "{{content.video}}" => content.VideoPaths?.FirstOrDefault() ?? "",
                _ => step.InputValue
            };
        }

        return step.InputValue;
    }

    private async Task<bool> CheckSuccessConditionAsync(
        SuccessCondition condition,
        CancellationToken ct)
    {
        return condition.Type switch
        {
            ConditionType.ElementVisible =>
                condition.Selector != null &&
                await _browser.WaitForElementAsync(condition.Selector, 5000, ct),

            ConditionType.UrlContains =>
                !string.IsNullOrEmpty(condition.ExpectedUrl) &&
                (_browser.CurrentUrl?.Contains(condition.ExpectedUrl) ?? false),

            ConditionType.UrlEquals =>
                _browser.CurrentUrl == condition.ExpectedUrl,

            _ => true
        };
    }

    private async Task<bool> TryAutoRepairAndRetry(
        LearnedWorkflow workflow,
        int failedStepIndex,
        WebPostContent content,
        CancellationToken ct)
    {
        _logger.LogInformation("Attempting auto-repair at step {Index}", failedStepIndex);

        // ดึง HTML ปัจจุบัน
        var pageHtml = await _browser.GetPageHtmlAsync(ct);
        var screenshot = await _browser.TakeScreenshotAsync(ct);

        if (string.IsNullOrEmpty(pageHtml))
        {
            return false;
        }

        // ให้ AI ซ่อม
        var repairedWorkflow = await _learningEngine.TryAutoRepairWorkflow(
            workflow, failedStepIndex, pageHtml, screenshot, ct);

        if (repairedWorkflow == null)
        {
            return false;
        }

        // ลอง step ใหม่
        var step = repairedWorkflow.Steps[failedStepIndex];
        var result = await ExecuteStepAsync(step, content, ct);

        if (result.Success)
        {
            _logger.LogInformation("Auto-repair successful at step {Index}", failedStepIndex);
            // อัพเดท original workflow
            workflow.Steps[failedStepIndex] = step;
            return true;
        }

        return false;
    }

    // Helper methods
    private async Task<bool> WaitAsync(int ms, CancellationToken ct)
    {
        await Task.Delay(ms, ct);
        return true;
    }

    private async Task<bool> ClearElementAsync(ElementSelector selector, CancellationToken ct)
    {
        return await _browser.TypeAsync(selector, "", true, 0, ct);
    }

    private async Task<bool> SelectOptionAsync(ElementSelector selector, string value, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                const select = document.querySelector('{selector.Value}');
                if (select) {{
                    select.value = '{value}';
                    select.dispatchEvent(new Event('change', {{ bubbles: true }}));
                    return true;
                }}
                return false;
            }})()";

        return await _browser.ExecuteScriptAsync(script, ct) == "true";
    }

    private async Task<bool> ScrollAsync(ElementSelector selector, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                const element = document.querySelector('{selector.Value}');
                if (element) {{
                    element.scrollIntoView({{ behavior: 'smooth', block: 'center' }});
                    return true;
                }}
                window.scrollBy(0, 300);
                return true;
            }})()";

        return await _browser.ExecuteScriptAsync(script, ct) == "true";
    }

    private async Task<bool> HoverAsync(ElementSelector selector, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                const element = document.querySelector('{selector.Value}');
                if (element) {{
                    element.dispatchEvent(new MouseEvent('mouseenter', {{ bubbles: true }}));
                    element.dispatchEvent(new MouseEvent('mouseover', {{ bubbles: true }}));
                    return true;
                }}
                return false;
            }})()";

        return await _browser.ExecuteScriptAsync(script, ct) == "true";
    }

    private async Task<bool> PressKeyAsync(string key, CancellationToken ct)
    {
        var script = $@"
            (function() {{
                document.activeElement.dispatchEvent(new KeyboardEvent('keydown', {{ key: '{key}' }}));
                document.activeElement.dispatchEvent(new KeyboardEvent('keyup', {{ key: '{key}' }}));
                return true;
            }})()";

        return await _browser.ExecuteScriptAsync(script, ct) == "true";
    }

    private async Task<bool> ExecuteScriptAsync(string script, CancellationToken ct)
    {
        var result = await _browser.ExecuteScriptAsync(script, ct);
        return result != null;
    }

    private async Task<bool> TakeScreenshotAsync(CancellationToken ct)
    {
        var screenshot = await _browser.TakeScreenshotAsync(ct);
        return screenshot != null;
    }
}

/// <summary>
/// ผลการรัน Workflow
/// </summary>
public class WorkflowExecutionResult
{
    public string WorkflowId { get; set; } = "";
    public bool Success { get; set; }
    public int? FailedAtStep { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();

    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// ผลการรัน Step
/// </summary>
public class StepExecutionResult
{
    public string StepId { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public string? Screenshot { get; set; }
}
