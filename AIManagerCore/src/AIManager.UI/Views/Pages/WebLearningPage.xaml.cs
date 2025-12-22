using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Web Learning Page - สอนระบบทำงานบนเว็บไซต์
/// </summary>
public partial class WebLearningPage : Page
{
    private readonly ILogger<WebLearningPage>? _logger;
    private readonly WorkflowStorage? _workflowStorage;
    private bool _isRecording;
    private readonly ObservableCollection<RecordedStepViewModel> _recordedSteps = new();
    private string? _currentSessionId;

    // Injected Recording Script
    private const string RecordingScript = @"
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
                    position: { x: Math.round(rect.x), y: Math.round(rect.y), width: Math.round(rect.width), height: Math.round(rect.height) }
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
                return '';
            }

            function getCssSelector(element) {
                if (element.id) return '#' + element.id;
                let path = [];
                while (element && element.nodeType === Node.ELEMENT_NODE) {
                    let selector = element.tagName.toLowerCase();
                    if (element.id) {
                        path.unshift('#' + element.id);
                        break;
                    }
                    if (element.className) {
                        const classes = element.className.trim().split(/\s+/).filter(c => c && !c.includes(':')).slice(0, 2);
                        if (classes.length > 0) {
                            selector += '.' + classes.join('.');
                        }
                    }
                    path.unshift(selector);
                    element = element.parentElement;
                }
                return path.join(' > ');
            }

            function recordStep(action, element, value) {
                const step = {
                    timestamp: new Date().toISOString(),
                    action: action,
                    element: element ? getElementInfo(element) : null,
                    value: value || null,
                    pageUrl: window.location.href,
                    pageTitle: document.title
                };

                window.__postXAgentRecordedSteps.push(step);

                // Send to C# via postMessage
                window.chrome.webview.postMessage(JSON.stringify({
                    type: 'recordedStep',
                    data: step
                }));
            }

            // Listen for clicks
            document.addEventListener('click', function(e) {
                recordStep('click', e.target);
            }, true);

            // Listen for input with debounce
            let inputTimeout;
            document.addEventListener('input', function(e) {
                clearTimeout(inputTimeout);
                inputTimeout = setTimeout(() => {
                    recordStep('type', e.target, e.target.value);
                }, 500);
            }, true);

            // Listen for change (select, checkbox, etc)
            document.addEventListener('change', function(e) {
                if (e.target.type === 'file') {
                    recordStep('upload', e.target, [...e.target.files].map(f => f.name).join(', '));
                } else if (e.target.type === 'checkbox' || e.target.type === 'radio') {
                    recordStep('check', e.target, e.target.checked);
                } else if (e.target.tagName === 'SELECT') {
                    recordStep('select', e.target, e.target.value);
                }
            }, true);

            // Listen for form submission
            document.addEventListener('submit', function(e) {
                recordStep('submit', e.target);
            }, true);

            console.log('PostXAgent recorder initialized');
        })();
    ";

    public WebLearningPage()
    {
        InitializeComponent();

        // Try to get services from DI
        try
        {
            var services = App.Services;
            _logger = services?.GetService<ILogger<WebLearningPage>>();
            _workflowStorage = services?.GetService<WorkflowStorage>();
        }
        catch
        {
            // DI not available
        }

        StepsItemsControl.ItemsSource = _recordedSteps;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Initialize WebView2
            var env = await CoreWebView2Environment.CreateAsync();
            await WebBrowser.EnsureCoreWebView2Async(env);

            // Set up message handler for recording
            WebBrowser.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            // Navigate to default URL
            WebBrowser.CoreWebView2.Navigate(UrlTextBox.Text);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize WebView2");
            MessageBox.Show($"ไม่สามารถเปิด Browser ได้: {ex.Message}\n\nกรุณาติดตั้ง WebView2 Runtime",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // Stop recording if active
        if (_isRecording)
        {
            StopRecording();
        }

        WebBrowser?.Dispose();
    }

    private void WebBrowser_CoreWebView2InitializationCompleted(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (!e.IsSuccess)
        {
            _logger?.LogError(e.InitializationException, "WebView2 initialization failed");
            return;
        }

        // Inject recording script on every page load
        WebBrowser.CoreWebView2.NavigationCompleted += async (s, args) =>
        {
            if (_isRecording && args.IsSuccess)
            {
                await InjectRecordingScriptAsync();
            }
        };
    }

    private void WebBrowser_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UrlTextBox.Text = WebBrowser.CoreWebView2?.Source ?? "";
            LoadingIndicator.Visibility = Visibility.Collapsed;
        });
    }

    private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!_isRecording) return;

        try
        {
            var message = JsonConvert.DeserializeObject<WebViewMessage>(e.WebMessageAsJson);
            if (message?.Type == "recordedStep" && message.Data != null)
            {
                Dispatcher.Invoke(() =>
                {
                    AddRecordedStep(message.Data);
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse web message");
        }
    }

    private void AddRecordedStep(RecordedStep step)
    {
        var viewModel = new RecordedStepViewModel
        {
            Index = _recordedSteps.Count,
            StepNumber = (_recordedSteps.Count + 1).ToString(),
            Action = step.Action ?? "unknown",
            ActionText = GetActionText(step.Action ?? "unknown"),
            ActionColor = GetActionColor(step.Action ?? "unknown"),
            ElementDescription = GetElementDescription(step.Element),
            Value = step.Value,
            ValueText = !string.IsNullOrEmpty(step.Value) ? $"Value: {step.Value}" : null,
            HasValue = !string.IsNullOrEmpty(step.Value),
            OriginalStep = step
        };

        _recordedSteps.Add(viewModel);
        UpdateStepCount();
    }

    private string GetActionText(string action)
    {
        return action.ToLower() switch
        {
            "click" => "Click",
            "type" => "Type Text",
            "upload" => "Upload File",
            "select" => "Select Option",
            "check" => "Check/Uncheck",
            "submit" => "Submit Form",
            "navigate" => "Navigate",
            _ => action
        };
    }

    private Brush GetActionColor(string action)
    {
        return action.ToLower() switch
        {
            "click" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),     // Green
            "type" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),     // Blue
            "upload" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),    // Orange
            "select" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),   // Purple
            "check" => new SolidColorBrush(Color.FromRgb(233, 30, 99)),     // Pink
            "submit" => new SolidColorBrush(Color.FromRgb(0, 188, 212)),    // Cyan
            "navigate" => new SolidColorBrush(Color.FromRgb(0, 188, 212)),  // Cyan
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))          // Gray
        };
    }

    private string GetElementDescription(RecordedElement? element)
    {
        if (element == null) return "Unknown element";

        var parts = new List<string>();

        if (!string.IsNullOrEmpty(element.Id))
        {
            parts.Add($"#{element.Id}");
        }
        else if (!string.IsNullOrEmpty(element.Name))
        {
            parts.Add($"[name={element.Name}]");
        }
        else if (!string.IsNullOrEmpty(element.Placeholder))
        {
            parts.Add($"\"{element.Placeholder}\"");
        }

        if (!string.IsNullOrEmpty(element.TagName))
        {
            parts.Insert(0, $"<{element.TagName}>");
        }

        if (parts.Count == 0 && !string.IsNullOrEmpty(element.TextContent))
        {
            var text = element.TextContent.Length > 30
                ? element.TextContent.Substring(0, 30) + "..."
                : element.TextContent;
            parts.Add($"\"{text}\"");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : element.CssSelector ?? "Unknown";
    }

    private void UpdateStepCount()
    {
        StepCountText.Text = $"{_recordedSteps.Count} steps recorded";
    }

    private async Task InjectRecordingScriptAsync()
    {
        if (WebBrowser.CoreWebView2 == null) return;

        try
        {
            await WebBrowser.CoreWebView2.ExecuteScriptAsync(RecordingScript);
            _logger?.LogDebug("Recording script injected");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to inject recording script");
        }
    }

    #region Button Handlers

    private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecording) return;

        _isRecording = true;
        _currentSessionId = Guid.NewGuid().ToString();
        _recordedSteps.Clear();

        // Update UI
        StartRecordingButton.IsEnabled = false;
        StopRecordingButton.IsEnabled = true;
        RecordingIndicator.Visibility = Visibility.Visible;
        PlatformComboBox.IsEnabled = false;
        TaskTypeComboBox.IsEnabled = false;

        // Inject recording script
        await InjectRecordingScriptAsync();

        _logger?.LogInformation("Recording started - Session: {SessionId}", _currentSessionId);
    }

    private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
    }

    private void StopRecording()
    {
        if (!_isRecording) return;

        _isRecording = false;

        // Update UI
        StartRecordingButton.IsEnabled = true;
        StopRecordingButton.IsEnabled = false;
        RecordingIndicator.Visibility = Visibility.Collapsed;
        PlatformComboBox.IsEnabled = true;
        TaskTypeComboBox.IsEnabled = true;

        _logger?.LogInformation("Recording stopped - {Count} steps recorded", _recordedSteps.Count);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebBrowser.CoreWebView2?.CanGoBack == true)
        {
            WebBrowser.CoreWebView2.GoBack();
        }
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        if (WebBrowser.CoreWebView2?.CanGoForward == true)
        {
            WebBrowser.CoreWebView2.GoForward();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        WebBrowser.CoreWebView2?.Reload();
    }

    private void GoButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToUrl();
    }

    private void UrlTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToUrl();
        }
    }

    private void NavigateToUrl()
    {
        var url = UrlTextBox.Text.Trim();
        if (string.IsNullOrEmpty(url)) return;

        // Add http if missing
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = "https://" + url;
            UrlTextBox.Text = url;
        }

        LoadingIndicator.Visibility = Visibility.Visible;
        WebBrowser.CoreWebView2?.Navigate(url);

        // Add navigation step if recording
        if (_isRecording)
        {
            AddRecordedStep(new RecordedStep
            {
                Timestamp = DateTime.UtcNow,
                Action = "navigate",
                Value = url,
                PageUrl = url
            });
        }
    }

    private void ClearStepsButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("ต้องการล้าง Steps ทั้งหมดหรือไม่?", "ยืนยัน",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _recordedSteps.Clear();
            UpdateStepCount();
        }
    }

    private void DeleteStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index && index >= 0 && index < _recordedSteps.Count)
        {
            _recordedSteps.RemoveAt(index);

            // Re-index remaining steps
            for (int i = 0; i < _recordedSteps.Count; i++)
            {
                _recordedSteps[i].Index = i;
                _recordedSteps[i].StepNumber = (i + 1).ToString();
            }

            UpdateStepCount();
        }
    }

    private async void TestWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedSteps.Count == 0)
        {
            MessageBox.Show("ยังไม่มี Steps ที่บันทึกไว้", "แจ้งเตือน",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Stop recording first
        if (_isRecording)
        {
            StopRecording();
        }

        MessageBox.Show("ฟีเจอร์ทดสอบ Workflow จะเปิด Browser ใหม่และรัน Steps ที่บันทึกไว้\n\n" +
            "ในเวอร์ชันเต็ม จะใช้ Playwright Browser ในการทดสอบ",
            "Test Workflow", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void SaveWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedSteps.Count == 0)
        {
            MessageBox.Show("ยังไม่มี Steps ที่บันทึกไว้", "แจ้งเตือน",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var workflowName = WorkflowNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(workflowName))
        {
            MessageBox.Show("กรุณากรอกชื่อ Workflow", "แจ้งเตือน",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            WorkflowNameTextBox.Focus();
            return;
        }

        // Stop recording first
        if (_isRecording)
        {
            StopRecording();
        }

        try
        {
            var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";
            var taskType = (TaskTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";

            // Create workflow
            var workflow = new LearnedWorkflow
            {
                Id = Guid.NewGuid().ToString(),
                Name = workflowName,
                Platform = platform,
                TaskType = taskType,
                Description = WorkflowDescriptionTextBox.Text.Trim(),
                IsHumanTrained = true,
                CreatedAt = DateTime.UtcNow
            };

            // Convert recorded steps to workflow steps
            var order = 0;
            foreach (var recordedStep in _recordedSteps)
            {
                var step = ConvertToWorkflowStep(recordedStep.OriginalStep, order++);
                if (step != null)
                {
                    workflow.Steps.Add(step);
                }
            }

            // Save workflow
            if (_workflowStorage != null)
            {
                await _workflowStorage.SaveWorkflowAsync(workflow);
                _logger?.LogInformation("Workflow saved: {Name} with {Count} steps", workflowName, workflow.Steps.Count);

                MessageBox.Show($"บันทึก Workflow \"{workflowName}\" สำเร็จ!\n\nจำนวน Steps: {workflow.Steps.Count}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear form
                _recordedSteps.Clear();
                WorkflowNameTextBox.Clear();
                WorkflowDescriptionTextBox.Clear();
                UpdateStepCount();
            }
            else
            {
                // Fallback: show JSON
                var json = JsonConvert.SerializeObject(workflow, Formatting.Indented);
                MessageBox.Show($"Workflow JSON (copy นี้ไปบันทึก):\n\n{json}",
                    "Workflow Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save workflow");
            MessageBox.Show($"เกิดข้อผิดพลาดในการบันทึก: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private WorkflowStep? ConvertToWorkflowStep(RecordedStep? recorded, int order)
    {
        if (recorded == null) return null;

        var step = new WorkflowStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = order,
            Description = $"{recorded.Action} on {recorded.Element?.TagName ?? "element"}",
            InputValue = recorded.Value,
            WaitAfterMs = 500 // Default wait after each step
        };

        // Set action
        step.Action = recorded.Action?.ToLower() switch
        {
            "click" => StepAction.Click,
            "type" => StepAction.Type,
            "upload" => StepAction.Upload,
            "select" => StepAction.Select,
            "check" => StepAction.Click, // Treat as click
            "submit" => StepAction.Click,
            "navigate" => StepAction.Navigate,
            _ => StepAction.Click
        };

        // Set selector
        if (recorded.Element != null)
        {
            step.Selector = new ElementSelector
            {
                Type = DetermineBestSelectorType(recorded.Element),
                Value = GetBestSelectorValue(recorded.Element),
                Confidence = 0.9
            };

            // Add alternative selectors
            AddAlternativeSelectors(step, recorded.Element);
        }
        else if (step.Action == StepAction.Navigate)
        {
            step.Selector = new ElementSelector
            {
                Type = SelectorType.CSS,
                Value = recorded.Value ?? "",
                Confidence = 1.0
            };
        }

        return step;
    }

    private SelectorType DetermineBestSelectorType(RecordedElement element)
    {
        if (!string.IsNullOrEmpty(element.Id))
            return SelectorType.Id;
        if (!string.IsNullOrEmpty(element.Name))
            return SelectorType.Name;
        if (!string.IsNullOrEmpty(element.Attributes?.GetValueOrDefault("data-testid")))
            return SelectorType.TestId;
        if (!string.IsNullOrEmpty(element.Attributes?.GetValueOrDefault("aria-label")))
            return SelectorType.AriaLabel;
        if (!string.IsNullOrEmpty(element.Placeholder))
            return SelectorType.Placeholder;
        if (!string.IsNullOrEmpty(element.CssSelector))
            return SelectorType.CSS;
        if (!string.IsNullOrEmpty(element.XPath))
            return SelectorType.XPath;

        return SelectorType.CSS;
    }

    private string GetBestSelectorValue(RecordedElement element)
    {
        if (!string.IsNullOrEmpty(element.Id))
            return element.Id;
        if (!string.IsNullOrEmpty(element.Name))
            return element.Name;
        if (!string.IsNullOrEmpty(element.Attributes?.GetValueOrDefault("data-testid")))
            return element.Attributes["data-testid"];
        if (!string.IsNullOrEmpty(element.Attributes?.GetValueOrDefault("aria-label")))
            return element.Attributes["aria-label"];
        if (!string.IsNullOrEmpty(element.Placeholder))
            return element.Placeholder;
        if (!string.IsNullOrEmpty(element.CssSelector))
            return element.CssSelector;

        return element.XPath ?? "";
    }

    private void AddAlternativeSelectors(WorkflowStep step, RecordedElement element)
    {
        // Add XPath as alternative
        if (!string.IsNullOrEmpty(element.XPath) && step.Selector.Type != SelectorType.XPath)
        {
            step.AlternativeSelectors.Add(new ElementSelector
            {
                Type = SelectorType.XPath,
                Value = element.XPath,
                Confidence = 0.7
            });
        }

        // Add CSS selector as alternative
        if (!string.IsNullOrEmpty(element.CssSelector) && step.Selector.Type != SelectorType.CSS)
        {
            step.AlternativeSelectors.Add(new ElementSelector
            {
                Type = SelectorType.CSS,
                Value = element.CssSelector,
                Confidence = 0.8
            });
        }
    }

    #endregion
}

#region ViewModels

public class RecordedStepViewModel
{
    public int Index { get; set; }
    public string StepNumber { get; set; } = "";
    public string Action { get; set; } = "";
    public string ActionText { get; set; } = "";
    public Brush ActionColor { get; set; } = Brushes.Gray;
    public string ElementDescription { get; set; } = "";
    public string? Value { get; set; }
    public string? ValueText { get; set; }
    public bool HasValue { get; set; }
    public RecordedStep? OriginalStep { get; set; }
}

public class WebViewMessage
{
    public string? Type { get; set; }
    public RecordedStep? Data { get; set; }
}

#endregion
