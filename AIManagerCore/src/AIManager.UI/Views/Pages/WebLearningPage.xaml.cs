using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIManager.Core.AI;
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
    private readonly AutoLearningEngine? _autoLearningEngine;
    private readonly AIGuidedTeacher? _aiGuidedTeacher;
    private readonly HttpClient _httpClient = new();
    private bool _isRecording;
    private bool _isAutoLearningMode;
    private bool _isGuidedTeachingMode;
    private readonly ObservableCollection<RecordedStepViewModel> _recordedSteps = new();
    private readonly ObservableCollection<TeachingStepViewModel> _teachingSteps = new();
    private string? _currentSessionId;
    private WorkflowSuggestion? _currentSuggestion;
    private TeachingGuideline? _currentGuideline;
    private int _currentTeachingStepIndex;
    private string? _lastAiResponse;
    private List<WorkflowStep>? _suggestedWorkflowSteps;

    // Ollama configuration
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string OllamaModel = "llama3.2";

    // Initial URL and platform passed from WorkflowManagerPage
    private readonly string? _initialUrl;
    private readonly string? _platformName;

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

    public WebLearningPage(string? initialUrl = null, string? platformName = null)
    {
        InitializeComponent();

        _initialUrl = initialUrl;
        _platformName = platformName;

        // Try to get services from DI
        try
        {
            var services = App.Services;
            _logger = services?.GetService<ILogger<WebLearningPage>>();
            _workflowStorage = services?.GetService<WorkflowStorage>();
            _autoLearningEngine = services?.GetService<AutoLearningEngine>();
            _aiGuidedTeacher = services?.GetService<AIGuidedTeacher>();
        }
        catch
        {
            // DI not available
        }

        StepsItemsControl.ItemsSource = _recordedSteps;
        TeachingStepsControl.ItemsSource = _teachingSteps;

        // Set initial URL if provided
        if (!string.IsNullOrEmpty(_initialUrl))
        {
            UrlTextBox.Text = _initialUrl;
        }

        // Pre-select platform in combo box if provided
        if (!string.IsNullOrEmpty(_platformName))
        {
            SelectPlatformInComboBox(_platformName);
        }
    }

    /// <summary>
    /// Pre-select platform in combo box based on platform name
    /// </summary>
    private void SelectPlatformInComboBox(string platformName)
    {
        foreach (ComboBoxItem item in PlatformComboBox.Items)
        {
            if (item.Content?.ToString()?.Equals(platformName, StringComparison.OrdinalIgnoreCase) == true ||
                item.Tag?.ToString()?.Equals(platformName, StringComparison.OrdinalIgnoreCase) == true)
            {
                PlatformComboBox.SelectedItem = item;
                break;
            }
        }
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

            // Navigate to initial URL (from platform or URL textbox)
            var navigateUrl = !string.IsNullOrEmpty(_initialUrl) ? _initialUrl : UrlTextBox.Text;
            if (!string.IsNullOrEmpty(navigateUrl))
            {
                // Ensure URL has protocol
                if (!navigateUrl.StartsWith("http://") && !navigateUrl.StartsWith("https://"))
                {
                    navigateUrl = "https://" + navigateUrl;
                }
                UrlTextBox.Text = navigateUrl;
                LoadingIndicator.Visibility = Visibility.Visible;
                WebBrowser.CoreWebView2.Navigate(navigateUrl);

                _logger?.LogInformation("Navigating to platform URL: {Url} for platform: {Platform}",
                    navigateUrl, _platformName ?? "Unknown");
            }

            // Check Ollama status in background
            _ = CheckOllamaStatusAsync();
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
        var confidence = CalculateStepConfidence(step);
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
            OriginalStep = step,
            Confidence = confidence
        };

        _recordedSteps.Add(viewModel);
        UpdateStepCount();
        UpdateConfidence();

        // Clear redo stack when new step added
        _redoStack.Clear();
        UpdateUndoRedoButtons();

        // Validate against teaching guideline if in guided mode
        if (_isGuidedTeachingMode && _currentGuideline != null)
        {
            ValidateTeachingStep(step);
        }
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
        SaveStateForUndo();
        _recordedSteps.Clear();
        _currentTeachingStepIndex = 0;

        // Update UI
        StartRecordingButton.IsEnabled = false;
        StopRecordingButton.IsEnabled = true;
        RecordingIndicator.Visibility = Visibility.Visible;
        RecordingBorder.Visibility = Visibility.Visible;
        PlatformComboBox.IsEnabled = false;
        TaskTypeComboBox.IsEnabled = false;
        AiSuggestionPanel.Visibility = Visibility.Collapsed;

        // Reset confidence
        ConfidenceBar.Value = 0;
        ConfidenceText.Text = "0%";

        // Load AI Teaching Guidelines
        await LoadTeachingGuidelinesAsync();

        // Inject recording script
        await InjectRecordingScriptAsync();

        ShowStatus("เริ่มบันทึกการกระทำ กรุณาทำตาม AI Guide...", "info");
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
        _isGuidedTeachingMode = false;

        // Update UI
        StartRecordingButton.IsEnabled = true;
        StopRecordingButton.IsEnabled = false;
        RecordingIndicator.Visibility = Visibility.Collapsed;
        RecordingBorder.Visibility = Visibility.Collapsed;
        TeachingGuidelinesPanel.Visibility = Visibility.Collapsed;
        PlatformComboBox.IsEnabled = true;
        TaskTypeComboBox.IsEnabled = true;

        _logger?.LogInformation("Recording stopped - {Count} steps recorded", _recordedSteps.Count);

        // Show status and AI suggestions
        if (_recordedSteps.Count > 0)
        {
            var confidence = _recordedSteps.Average(s => s.Confidence);
            ShowStatus($"บันทึกเสร็จสิ้น! ได้ {_recordedSteps.Count} ขั้นตอน (Confidence: {confidence:P0})", "success");
            GenerateAiSuggestions();
        }
        else
        {
            ShowStatus("ยังไม่มีการกระทำที่บันทึกได้", "warning");
        }
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
            SaveStateForUndo();
            _recordedSteps.RemoveAt(index);
            ReindexSteps();
            UpdateConfidence();
            ShowStatus("ลบ Step แล้ว", "info");
        }
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index && index > 0 && index < _recordedSteps.Count)
        {
            SaveStateForUndo();
            var item = _recordedSteps[index];
            _recordedSteps.RemoveAt(index);
            _recordedSteps.Insert(index - 1, item);
            ReindexSteps();
        }
    }

    private void MoveDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index && index >= 0 && index < _recordedSteps.Count - 1)
        {
            SaveStateForUndo();
            var item = _recordedSteps[index];
            _recordedSteps.RemoveAt(index);
            _recordedSteps.Insert(index + 1, item);
            ReindexSteps();
        }
    }

    private void DuplicateStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int index && index >= 0 && index < _recordedSteps.Count)
        {
            SaveStateForUndo();
            var original = _recordedSteps[index];
            var clone = new RecordedStepViewModel
            {
                Index = index + 1,
                StepNumber = (index + 2).ToString(),
                Action = original.Action,
                ActionText = original.ActionText,
                ActionColor = original.ActionColor,
                ElementDescription = original.ElementDescription,
                Value = original.Value,
                ValueText = original.ValueText,
                HasValue = original.HasValue,
                OriginalStep = original.OriginalStep,
                Confidence = original.Confidence
            };
            _recordedSteps.Insert(index + 1, clone);
            ReindexSteps();
            ShowStatus($"คัดลอก Step #{original.StepNumber} แล้ว", "success");
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0) return;

        var currentState = _recordedSteps.ToList();
        _redoStack.Push(currentState);

        var previousState = _undoStack.Pop();
        _recordedSteps.Clear();
        foreach (var step in previousState)
        {
            _recordedSteps.Add(step);
        }

        UpdateUndoRedoButtons();
        UpdateStepCount();
        UpdateConfidence();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count == 0) return;

        var currentState = _recordedSteps.ToList();
        _undoStack.Push(currentState);

        var nextState = _redoStack.Pop();
        _recordedSteps.Clear();
        foreach (var step in nextState)
        {
            _recordedSteps.Add(step);
        }

        UpdateUndoRedoButtons();
        UpdateStepCount();
        UpdateConfidence();
    }

    private void DismissSuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        AiSuggestionPanel.Visibility = Visibility.Collapsed;
    }

    private void DismissStatusButton_Click(object sender, RoutedEventArgs e)
    {
        StatusBar.Visibility = Visibility.Collapsed;
    }

    #region Auto Learning Mode

    private void AutoLearningToggle_Changed(object sender, RoutedEventArgs e)
    {
        _isAutoLearningMode = AutoLearningToggle.IsChecked == true;

        if (_isAutoLearningMode)
        {
            // Show auto learning panel, hide suggestion panel
            AutoLearningPanel.Visibility = Visibility.Visible;
            AiSuggestionPanel.Visibility = Visibility.Collapsed;
            AiResponsePanel.Visibility = Visibility.Collapsed;

            // Check Ollama status when enabling
            _ = CheckOllamaStatusAsync();

            // Start page analysis
            _ = AnalyzeCurrentPageAsync();

            ShowStatus("เปิดโหมด AI Auto-Learning - พิมพ์คำสั่งให้ AI หรือเริ่มบันทึก", "success");
            _logger?.LogInformation("Auto-Learning mode enabled");
        }
        else
        {
            AutoLearningPanel.Visibility = Visibility.Collapsed;
            AiResponsePanel.Visibility = Visibility.Collapsed;
            ShowStatus("ปิดโหมด Auto-Learning แล้ว", "info");
            _logger?.LogInformation("Auto-Learning mode disabled");
        }
    }

    private async Task AnalyzeCurrentPageAsync()
    {
        if (WebBrowser.CoreWebView2 == null) return;

        try
        {
            AutoLearningStatusText.Text = "กำลังวิเคราะห์หน้าเว็บ...";
            AutoLearningProgress.IsIndeterminate = true;
            DetectedElementsPanel.Children.Clear();

            var url = WebBrowser.CoreWebView2.Source ?? "";
            var pageHtml = await WebBrowser.CoreWebView2.ExecuteScriptAsync(
                "document.documentElement.outerHTML");

            // Unescape JSON string
            pageHtml = System.Text.RegularExpressions.Regex.Unescape(
                pageHtml.Trim('"'));

            // Detect platform
            var detectedPlatform = DetectPlatformFromUrl(url);
            AutoLearningPlatformText.Text = detectedPlatform;

            // Update platform combobox if auto-detected
            foreach (ComboBoxItem item in PlatformComboBox.Items)
            {
                if (item.Tag?.ToString()?.Equals(detectedPlatform, StringComparison.OrdinalIgnoreCase) == true)
                {
                    PlatformComboBox.SelectedItem = item;
                    break;
                }
            }

            if (_autoLearningEngine != null)
            {
                // Use AutoLearningEngine to analyze
                _currentSuggestion = await _autoLearningEngine.AnalyzeAndSuggestAsync(
                    url, pageHtml, null, CancellationToken.None);

                UpdateAutoLearningUI(_currentSuggestion);
            }
            else
            {
                // Fallback: basic page analysis
                await PerformBasicPageAnalysisAsync(pageHtml);
            }

            AutoLearningProgress.IsIndeterminate = false;
            AutoLearningProgress.Value = 100;
            AutoLearningStatusText.Text = "วิเคราะห์เสร็จสิ้น";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to analyze page");
            AutoLearningStatusText.Text = "เกิดข้อผิดพลาดในการวิเคราะห์";
            AutoLearningProgress.IsIndeterminate = false;
        }
    }

    private void UpdateAutoLearningUI(WorkflowSuggestion suggestion)
    {
        if (suggestion == null) return;

        // Update status
        if (suggestion.Success)
        {
            AutoLearningStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            AutoLearningStatusText.Text = $"พบ {suggestion.ExistingWorkflows.Count} workflow ที่มีอยู่แล้ว";

            if (suggestion.NeedsHumanTeaching)
            {
                AutoLearningStatusText.Text += " (แนะนำให้สอน AI)";
                AutoLearningStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            }
        }
        else
        {
            AutoLearningStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            AutoLearningStatusText.Text = suggestion.Error ?? "ไม่สามารถวิเคราะห์ได้";
        }

        // Show detected elements
        DetectedElementsPanel.Children.Clear();

        if (!string.IsNullOrEmpty(suggestion.PageType))
        {
            AddDetectedElementTag($"Page: {suggestion.PageType}", "#06B6D4");
        }

        if (suggestion.ExistingWorkflows.Any())
        {
            AddDetectedElementTag($"{suggestion.ExistingWorkflows.Count} Workflows", "#10B981");
        }

        if (suggestion.SuggestedSteps.Any())
        {
            AddDetectedElementTag($"{suggestion.SuggestedSteps.Count} Steps", "#8B5CF6");
        }

        if (suggestion.Confidence >= 0.8)
        {
            AddDetectedElementTag($"High Confidence", "#10B981");
        }
        else if (suggestion.Confidence >= 0.5)
        {
            AddDetectedElementTag($"Medium Confidence", "#F59E0B");
        }
        else
        {
            AddDetectedElementTag($"Low Confidence", "#EF4444");
        }
    }

    private async Task PerformBasicPageAnalysisAsync(string pageHtml)
    {
        // Basic analysis without AutoLearningEngine
        DetectedElementsPanel.Children.Clear();

        // Count elements
        var buttonCount = System.Text.RegularExpressions.Regex.Matches(
            pageHtml, "<button", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        var inputCount = System.Text.RegularExpressions.Regex.Matches(
            pageHtml, "<input", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        var formCount = System.Text.RegularExpressions.Regex.Matches(
            pageHtml, "<form", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;

        if (buttonCount > 0) AddDetectedElementTag($"{buttonCount} Buttons", "#4CAF50");
        if (inputCount > 0) AddDetectedElementTag($"{inputCount} Inputs", "#2196F3");
        if (formCount > 0) AddDetectedElementTag($"{formCount} Forms", "#FF9800");

        // Detect page type
        var pageType = "Unknown";
        if (pageHtml.Contains("login", StringComparison.OrdinalIgnoreCase) ||
            pageHtml.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            pageType = "Login";
        }
        else if (pageHtml.Contains("compose", StringComparison.OrdinalIgnoreCase) ||
                 pageHtml.Contains("create post", StringComparison.OrdinalIgnoreCase))
        {
            pageType = "Compose";
        }
        else if (pageHtml.Contains("feed", StringComparison.OrdinalIgnoreCase) ||
                 pageHtml.Contains("timeline", StringComparison.OrdinalIgnoreCase))
        {
            pageType = "Feed";
        }

        AddDetectedElementTag($"Page: {pageType}", "#06B6D4");
        AutoLearningStatusText.Text = $"พบ {buttonCount} buttons, {inputCount} inputs";

        await Task.CompletedTask;
    }

    private void AddDetectedElementTag(string text, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 6)
        };

        var textBlock = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(color),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };

        border.Child = textBlock;
        DetectedElementsPanel.Children.Add(border);
    }

    private string DetectPlatformFromUrl(string url)
    {
        url = url.ToLowerInvariant();

        if (url.Contains("facebook.com") || url.Contains("fb.com")) return "Facebook";
        if (url.Contains("instagram.com")) return "Instagram";
        if (url.Contains("tiktok.com")) return "TikTok";
        if (url.Contains("twitter.com") || url.Contains("x.com")) return "Twitter";
        if (url.Contains("youtube.com") || url.Contains("youtu.be")) return "YouTube";
        if (url.Contains("line.me") || url.Contains("lineblog")) return "LINE";
        if (url.Contains("threads.net")) return "Threads";
        if (url.Contains("linkedin.com")) return "LinkedIn";
        if (url.Contains("pinterest.com")) return "Pinterest";

        return "Custom";
    }

    private async void GenerateWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_autoLearningEngine == null)
        {
            MessageBox.Show("AutoLearningEngine ไม่พร้อมใช้งาน", "แจ้งเตือน",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            GenerateWorkflowButton.IsEnabled = false;
            AutoLearningStatusText.Text = "กำลังสร้าง Workflow อัตโนมัติ...";
            AutoLearningProgress.IsIndeterminate = true;

            var url = WebBrowser.CoreWebView2?.Source ?? "";
            var pageHtml = await WebBrowser.CoreWebView2!.ExecuteScriptAsync(
                "document.documentElement.outerHTML");
            pageHtml = System.Text.RegularExpressions.Regex.Unescape(pageHtml.Trim('"'));

            var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
            var taskType = (TaskTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "post";

            var workflow = await _autoLearningEngine.GenerateWorkflowForNewPlatformAsync(
                platform, taskType, pageHtml, null, CancellationToken.None);

            if (workflow != null)
            {
                // Populate recorded steps from generated workflow
                _recordedSteps.Clear();
                foreach (var step in workflow.Steps)
                {
                    var viewModel = new RecordedStepViewModel
                    {
                        Index = step.Order,
                        StepNumber = (step.Order + 1).ToString(),
                        Action = step.Action.ToString().ToLower(),
                        ActionText = GetActionText(step.Action.ToString().ToLower()),
                        ActionColor = GetActionColor(step.Action.ToString().ToLower()),
                        ElementDescription = step.Description ?? step.Selector.AIDescription ?? "Unknown",
                        Value = step.InputValue,
                        ValueText = !string.IsNullOrEmpty(step.InputValue) ? $"Value: {step.InputValue}" : null,
                        HasValue = !string.IsNullOrEmpty(step.InputValue),
                        Confidence = step.ConfidenceScore
                    };
                    _recordedSteps.Add(viewModel);
                }

                UpdateStepCount();
                UpdateConfidence();

                // Set workflow name
                WorkflowNameTextBox.Text = $"{platform} {taskType} (Auto-generated)";
                WorkflowDescriptionTextBox.Text = $"AI-generated workflow for {platform}. " +
                    $"Confidence: {workflow.ConfidenceScore:P0}. Please review and test before saving.";

                ShowStatus($"สร้าง Workflow อัตโนมัติสำเร็จ! มี {workflow.Steps.Count} steps", "success");
                AutoLearningStatusText.Text = "สร้าง Workflow สำเร็จ - กรุณาตรวจสอบก่อนบันทึก";
            }
            else
            {
                ShowStatus("ไม่สามารถสร้าง Workflow ได้ ลองสอน AI ด้วยการบันทึกแทน", "warning");
                AutoLearningStatusText.Text = "ไม่พบ patterns ที่เพียงพอ";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate workflow");
            ShowStatus($"เกิดข้อผิดพลาด: {ex.Message}", "error");
            AutoLearningStatusText.Text = "เกิดข้อผิดพลาด";
        }
        finally
        {
            GenerateWorkflowButton.IsEnabled = true;
            AutoLearningProgress.IsIndeterminate = false;
        }
    }

    private async void TransferLearningButton_Click(object sender, RoutedEventArgs e)
    {
        if (_autoLearningEngine == null || _workflowStorage == null)
        {
            MessageBox.Show("Services ไม่พร้อมใช้งาน", "แจ้งเตือน",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Get existing workflows to transfer from
            var workflows = await _workflowStorage.GetAllWorkflowsAsync(CancellationToken.None);
            var humanTrainedWorkflows = workflows
                .Where(w => w.IsHumanTrained && w.IsActive)
                .OrderByDescending(w => w.GetSuccessRate())
                .Take(5)
                .ToList();

            if (!humanTrainedWorkflows.Any())
            {
                MessageBox.Show("ยังไม่มี Workflow ที่มนุษย์สอนไว้\n\n" +
                    "กรุณาบันทึก workflow อย่างน้อย 1 รายการก่อนใช้ Transfer Learning",
                    "แจ้งเตือน", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var workflowNames = string.Join("\n", humanTrainedWorkflows.Select(w =>
                $"• {w.Platform}: {w.Name} ({w.GetSuccessRate():P0} success)"));

            var result = MessageBox.Show(
                $"พบ {humanTrainedWorkflows.Count} workflows ที่สามารถ transfer ได้:\n\n" +
                $"{workflowNames}\n\n" +
                "ต้องการ transfer learning จาก workflow ที่ดีที่สุดหรือไม่?",
                "Transfer Learning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                TransferLearningButton.IsEnabled = false;
                AutoLearningStatusText.Text = "กำลัง Transfer Learning...";
                AutoLearningProgress.IsIndeterminate = true;

                var sourceWorkflow = humanTrainedWorkflows.First();
                var targetPlatform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";

                var pageHtml = await WebBrowser.CoreWebView2!.ExecuteScriptAsync(
                    "document.documentElement.outerHTML");
                pageHtml = System.Text.RegularExpressions.Regex.Unescape(pageHtml.Trim('"'));

                var transferredWorkflow = await _autoLearningEngine.TransferWorkflowAsync(
                    sourceWorkflow.Id, targetPlatform, pageHtml, CancellationToken.None);

                if (transferredWorkflow != null)
                {
                    // Show transferred workflow
                    _recordedSteps.Clear();
                    foreach (var step in transferredWorkflow.Steps)
                    {
                        var viewModel = new RecordedStepViewModel
                        {
                            Index = step.Order,
                            StepNumber = (step.Order + 1).ToString(),
                            Action = step.Action.ToString().ToLower(),
                            ActionText = GetActionText(step.Action.ToString().ToLower()),
                            ActionColor = GetActionColor(step.Action.ToString().ToLower()),
                            ElementDescription = step.Description ?? "Unknown",
                            Confidence = step.ConfidenceScore
                        };
                        _recordedSteps.Add(viewModel);
                    }

                    UpdateStepCount();
                    UpdateConfidence();

                    WorkflowNameTextBox.Text = $"{targetPlatform} {sourceWorkflow.Name} (Transferred)";
                    WorkflowDescriptionTextBox.Text = $"Transferred from {sourceWorkflow.Platform}. " +
                        $"Original confidence: {sourceWorkflow.ConfidenceScore:P0}";

                    ShowStatus($"Transfer Learning สำเร็จ! {transferredWorkflow.Steps.Count} steps", "success");
                }
                else
                {
                    ShowStatus("ไม่สามารถ transfer ได้ - platforms อาจแตกต่างกันเกินไป", "warning");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to transfer learning");
            ShowStatus($"เกิดข้อผิดพลาด: {ex.Message}", "error");
        }
        finally
        {
            TransferLearningButton.IsEnabled = true;
            AutoLearningProgress.IsIndeterminate = false;
            AutoLearningStatusText.Text = "พร้อมใช้งาน";
        }
    }

    #endregion

    #region AI Command Prompt (Ollama Integration)

    private async void AiPromptTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(AiPromptTextBox.Text))
        {
            await SendAiPromptAsync();
        }
    }

    private async void SendAiPromptButton_Click(object sender, RoutedEventArgs e)
    {
        await SendAiPromptAsync();
    }

    private async Task SendAiPromptAsync()
    {
        var prompt = AiPromptTextBox.Text.Trim();
        if (string.IsNullOrEmpty(prompt)) return;

        try
        {
            // Disable button and show loading
            SendAiPromptButton.IsEnabled = false;
            OllamaStatusText.Text = "กำลังประมวลผล...";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow

            // Get current page context
            var pageContext = await GetPageContextAsync();
            var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
            var taskType = (TaskTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";

            // Build the prompt with context
            var fullPrompt = BuildOllamaPrompt(prompt, platform, taskType, pageContext);

            // Send to Ollama
            var response = await SendToOllamaAsync(fullPrompt);

            if (!string.IsNullOrEmpty(response))
            {
                _lastAiResponse = response;
                ShowAiResponse(response);
                OllamaStatusText.Text = "Ollama Ready";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green

                // Clear prompt input
                AiPromptTextBox.Clear();

                _logger?.LogInformation("AI Prompt processed: {Prompt}", prompt);
            }
            else
            {
                ShowStatus("ไม่ได้รับคำตอบจาก AI กรุณาลองใหม่", "warning");
                OllamaStatusText.Text = "No response";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Failed to connect to Ollama");
            ShowStatus("ไม่สามารถเชื่อมต่อ Ollama ได้ กรุณาตรวจสอบว่า Ollama กำลังทำงานอยู่", "error");
            OllamaStatusText.Text = "Disconnected";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing AI prompt");
            ShowStatus($"เกิดข้อผิดพลาด: {ex.Message}", "error");
            OllamaStatusText.Text = "Error";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
        }
        finally
        {
            SendAiPromptButton.IsEnabled = true;
        }
    }

    private async Task<string> GetPageContextAsync()
    {
        if (WebBrowser.CoreWebView2 == null) return "";

        try
        {
            // Get basic page info
            var titleScript = "document.title";
            var title = await WebBrowser.CoreWebView2.ExecuteScriptAsync(titleScript);
            title = title.Trim('"');

            // Get interactive elements summary
            var elementsScript = @"
                (function() {
                    var buttons = document.querySelectorAll('button, [role=""button""]');
                    var inputs = document.querySelectorAll('input, textarea');
                    var links = document.querySelectorAll('a[href]');
                    return JSON.stringify({
                        buttonCount: buttons.length,
                        inputCount: inputs.length,
                        linkCount: links.length,
                        buttons: Array.from(buttons).slice(0, 10).map(b => b.textContent?.trim().substring(0, 50) || b.getAttribute('aria-label') || 'Unknown'),
                        inputs: Array.from(inputs).slice(0, 10).map(i => i.placeholder || i.name || i.type || 'Unknown')
                    });
                })();
            ";
            var elementsJson = await WebBrowser.CoreWebView2.ExecuteScriptAsync(elementsScript);
            elementsJson = System.Text.RegularExpressions.Regex.Unescape(elementsJson.Trim('"'));

            var url = WebBrowser.CoreWebView2.Source ?? "";

            return $"Page: {title}\nURL: {url}\nElements: {elementsJson}";
        }
        catch
        {
            return "";
        }
    }

    private string BuildOllamaPrompt(string userPrompt, string platform, string taskType, string pageContext)
    {
        // Use comprehensive SystemKnowledge for AI context
        var systemPrompt = SystemKnowledge.GetAISystemPrompt(platform, taskType, pageContext);

        return $@"{systemPrompt}
{userPrompt}

คำตอบ:";
    }

    private async Task<string> SendToOllamaAsync(string prompt)
    {
        var requestBody = new
        {
            model = OllamaModel,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                num_predict = 500
            }
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{OllamaBaseUrl}/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var responseObj = JsonConvert.DeserializeObject<OllamaResponse>(responseJson);

        return responseObj?.Response ?? "";
    }

    private void ShowAiResponse(string response)
    {
        AiResponseText.Text = response;
        AiResponsePanel.Visibility = Visibility.Visible;

        // Check if response contains actionable suggestions
        if (response.Contains("คลิก") || response.Contains("กรอก") || response.Contains("ขั้นตอน"))
        {
            AiResponseActions.Visibility = Visibility.Visible;
        }
        else
        {
            AiResponseActions.Visibility = Visibility.Collapsed;
        }
    }

    private void DismissAiResponseButton_Click(object sender, RoutedEventArgs e)
    {
        AiResponsePanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyAiSuggestionButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastAiResponse))
        {
            ShowStatus("ไม่มีคำแนะนำให้นำไปใช้", "warning");
            return;
        }

        // If auto learning is enabled, start recording based on AI suggestion
        if (_isAutoLearningMode && !_isRecording)
        {
            StartRecordingButton_Click(sender, e);
            ShowStatus("เริ่มบันทึกตาม AI แนะนำ - ทำตามขั้นตอนที่ AI บอก", "success");
        }
        else
        {
            ShowStatus("นำคำแนะนำ AI ไปใช้แล้ว - กรุณาทำตามขั้นตอน", "info");
        }

        // Parse AI response and potentially generate teaching steps
        _ = GenerateTeachingStepsFromAiResponseAsync(_lastAiResponse);

        AiResponsePanel.Visibility = Visibility.Collapsed;
    }

    private async void TeachMeButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastAiResponse)) return;

        // Ask AI for more detailed step-by-step instructions
        var followUpPrompt = "กรุณาอธิบายขั้นตอนโดยละเอียดมากขึ้น โดยระบุ:\n1. element ที่ต้องคลิกหรือกรอก\n2. selector ที่ใช้หาได้ (CSS หรือ XPath)\n3. ข้อความหรือค่าที่ต้องกรอก\n4. เวลาที่ต้องรอหลังแต่ละขั้นตอน";

        AiPromptTextBox.Text = followUpPrompt;
        await SendAiPromptAsync();
    }

    private void VoiceInputButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Voice Input จะพร้อมใช้งานในเวอร์ชันถัดไป",
            "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OllamaStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Show troubleshooting guide when status is clicked
        var statusText = OllamaStatusText.Text;
        if (statusText.Contains("Offline") || statusText.Contains("Error") || statusText.Contains("Timeout"))
        {
            var guide = SystemKnowledge.GetOllamaTroubleshootingGuide();
            MessageBox.Show(guide, "วิธีแก้ไข Ollama", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else if (statusText.Contains("No models"))
        {
            MessageBox.Show($"กรุณาติดตั้ง model โดยเปิด Terminal แล้วรัน:\n\nollama pull {OllamaModel}\n\nรอจนเสร็จแล้วกดปุ่ม Refresh",
                "ติดตั้ง Model", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Ollama พร้อมใช้งานแล้ว!\n\nModel: {OllamaModel}\nEndpoint: {OllamaBaseUrl}\n\nลองพิมพ์คำสั่งในช่อง AI Command เช่น:\n- 'สอนวิธีโพสต์รูปบน Facebook'\n- 'ช่วยหาปุ่ม Login'\n- 'อธิบายระบบ PostXAgent'",
                "Ollama Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void RefreshOllamaStatus_Click(object sender, RoutedEventArgs e)
    {
        OllamaStatusText.Text = "Checking...";
        OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow
        await CheckOllamaStatusAsync();
    }

    private async Task GenerateTeachingStepsFromAiResponseAsync(string response)
    {
        // Try to parse AI response into teaching steps
        _teachingSteps.Clear();

        // Simple parsing - look for numbered steps or keywords
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var stepNumber = 1;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            // Check if line looks like a step (starts with number or bullet)
            if (trimmedLine.StartsWith($"{stepNumber}.") ||
                trimmedLine.StartsWith($"{stepNumber})") ||
                trimmedLine.StartsWith("•") ||
                trimmedLine.StartsWith("-") ||
                trimmedLine.Contains("คลิก") ||
                trimmedLine.Contains("กรอก") ||
                trimmedLine.Contains("เลือก") ||
                trimmedLine.Contains("อัพโหลด"))
            {
                // Determine action type
                var action = "click";
                if (trimmedLine.Contains("กรอก") || trimmedLine.Contains("พิมพ์") || trimmedLine.Contains("ใส่"))
                    action = "type";
                else if (trimmedLine.Contains("อัพโหลด"))
                    action = "upload";
                else if (trimmedLine.Contains("เลือก"))
                    action = "select";

                _teachingSteps.Add(new TeachingStepViewModel
                {
                    StepNumber = stepNumber,
                    ActionType = action.ToUpper(),
                    ActionColor = GetActionColor(action),
                    DescriptionThai = trimmedLine.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ')', '•', '-', ' '),
                    Description = $"Step {stepNumber}",
                    IsCurrent = stepNumber == 1,
                    IsCompleted = false
                });

                stepNumber++;

                if (stepNumber > 10) break; // Limit to 10 steps
            }
        }

        if (_teachingSteps.Any())
        {
            _isGuidedTeachingMode = true;
            _currentTeachingStepIndex = 0;
            TeachingGuidelinesPanel.Visibility = Visibility.Visible;
            TeachingGuideSubtitle.Text = "ทำตามขั้นตอนที่ AI แนะนำ";
            UpdateTeachingProgress();
        }
    }

    private async Task CheckOllamaStatusAsync()
    {
        try
        {
            // Set timeout for quick check
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var response = await _httpClient.GetAsync($"{OllamaBaseUrl}/api/tags", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var hasModels = content.Contains("\"models\"") && !content.Contains("\"models\":[]");

                Dispatcher.Invoke(() =>
                {
                    if (hasModels)
                    {
                        OllamaStatusText.Text = $"Ollama Ready ({OllamaModel})";
                        OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                    }
                    else
                    {
                        OllamaStatusText.Text = "Ollama: No models";
                        OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow
                        ShowStatus($"กรุณาติดตั้ง model: ollama pull {OllamaModel}", "warning");
                    }
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    OllamaStatusText.Text = "Ollama Error";
                    OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                });
            }
        }
        catch (TaskCanceledException)
        {
            Dispatcher.Invoke(() =>
            {
                OllamaStatusText.Text = "Ollama Timeout";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                ShowStatus("Ollama ไม่ตอบสนอง - กรุณาตรวจสอบว่า Ollama กำลังทำงานอยู่", "error");
            });
        }
        catch (HttpRequestException)
        {
            Dispatcher.Invoke(() =>
            {
                OllamaStatusText.Text = "Ollama Offline";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                ShowStatus("ไม่สามารถเชื่อมต่อ Ollama ได้ - รัน 'ollama serve' ใน Terminal", "error");
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking Ollama status");
            Dispatcher.Invoke(() =>
            {
                OllamaStatusText.Text = "Ollama Error";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            });
        }
    }

    #endregion

    #region AI Guided Teaching

    private async Task LoadTeachingGuidelinesAsync()
    {
        var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";
        var taskType = (TaskTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "post";

        _teachingSteps.Clear();
        _currentTeachingStepIndex = 0;

        if (_aiGuidedTeacher != null)
        {
            try
            {
                var url = WebBrowser.CoreWebView2?.Source ?? "";
                var pageHtml = await WebBrowser.CoreWebView2!.ExecuteScriptAsync(
                    "document.documentElement.outerHTML");
                pageHtml = System.Text.RegularExpressions.Regex.Unescape(pageHtml.Trim('"'));

                _currentGuideline = await _aiGuidedTeacher.GenerateTeachingGuidelineAsync(
                    platform, taskType, pageHtml, url, null, CancellationToken.None);

                if (_currentGuideline != null && _currentGuideline.Steps.Any())
                {
                    _isGuidedTeachingMode = true;

                    // Populate teaching steps
                    foreach (var step in _currentGuideline.Steps)
                    {
                        _teachingSteps.Add(new TeachingStepViewModel
                        {
                            StepNumber = step.StepNumber,
                            ActionType = step.Action.ToUpper(),
                            ActionColor = GetActionColor(step.Action),
                            Description = step.Description,
                            DescriptionThai = step.DescriptionThai,
                            ElementHint = step.ElementHint,
                            InputHint = step.InputHint,
                            IsOptional = step.IsOptional,
                            IsOptionalText = "(ข้ามได้)",
                            IsCurrent = step.StepNumber == 1,
                            IsCompleted = false,
                            HasElementHint = !string.IsNullOrEmpty(step.ElementHint),
                            HasInputHint = !string.IsNullOrEmpty(step.InputHint)
                        });
                    }

                    // Update UI
                    TeachingGuidelinesPanel.Visibility = Visibility.Visible;
                    TeachingGuideSubtitle.Text = $"ทำตามขั้นตอนเพื่อสอน AI สร้าง {taskType} workflow";
                    UpdateTeachingProgress();

                    _logger?.LogInformation("Teaching guideline loaded: {Platform} {TaskType} with {Steps} steps",
                        platform, taskType, _currentGuideline.Steps.Count);
                }
                else
                {
                    // No guideline available - show basic mode message
                    _isGuidedTeachingMode = false;
                    TeachingGuidelinesPanel.Visibility = Visibility.Collapsed;
                    ShowStatus("ไม่พบ template สำหรับ platform นี้ - บันทึกอิสระ", "info");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load teaching guideline");
                _isGuidedTeachingMode = false;
                TeachingGuidelinesPanel.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            // AI Teacher not available - use fallback templates
            LoadFallbackTeachingGuideline(platform, taskType);
        }
    }

    private void LoadFallbackTeachingGuideline(string platform, string taskType)
    {
        // Basic fallback template for common task types
        var steps = new List<TeachingStepViewModel>();

        switch (taskType.ToLower())
        {
            case "post":
                steps.Add(CreateTeachingStep(1, "click", "คลิกปุ่มสร้างโพสต์", "Click create post button", "Look for 'Create Post', 'What's on your mind?' or '+' button"));
                steps.Add(CreateTeachingStep(2, "type", "พิมพ์เนื้อหาโพสต์", "Type post content", "Text input area", "[POST_CONTENT]"));
                steps.Add(CreateTeachingStep(3, "upload", "อัพโหลดรูปภาพ (ถ้ามี)", "Upload image (optional)", "Photo/Media button", isOptional: true));
                steps.Add(CreateTeachingStep(4, "click", "คลิกปุ่มโพสต์", "Click post/submit button", "Post, Share, or Submit button"));
                break;

            case "login":
                steps.Add(CreateTeachingStep(1, "type", "กรอกอีเมลหรือชื่อผู้ใช้", "Enter email or username", "Email/Username input", "[EMAIL]"));
                steps.Add(CreateTeachingStep(2, "type", "กรอกรหัสผ่าน", "Enter password", "Password input", "[PASSWORD]"));
                steps.Add(CreateTeachingStep(3, "click", "คลิกปุ่มเข้าสู่ระบบ", "Click login button", "Login, Sign in, or Submit button"));
                break;

            case "upload":
                steps.Add(CreateTeachingStep(1, "click", "คลิกปุ่มอัพโหลด", "Click upload button", "Upload, +, or Add media button"));
                steps.Add(CreateTeachingStep(2, "upload", "เลือกไฟล์ที่ต้องการอัพโหลด", "Select file to upload", "File input"));
                steps.Add(CreateTeachingStep(3, "type", "เพิ่มคำอธิบาย (ถ้ามี)", "Add description (optional)", "Caption or description input", isOptional: true));
                steps.Add(CreateTeachingStep(4, "click", "คลิกปุ่มยืนยัน", "Click confirm button", "Post, Share, or Upload button"));
                break;

            default:
                // Generic steps
                steps.Add(CreateTeachingStep(1, "click", "คลิก element แรก", "Click first element", "Target element"));
                steps.Add(CreateTeachingStep(2, "type", "กรอกข้อมูล (ถ้ามี)", "Enter data (if any)", "Input field", isOptional: true));
                steps.Add(CreateTeachingStep(3, "click", "คลิกปุ่มยืนยัน", "Click confirm", "Submit button"));
                break;
        }

        _teachingSteps.Clear();
        foreach (var step in steps)
        {
            _teachingSteps.Add(step);
        }

        if (steps.Any())
        {
            _isGuidedTeachingMode = true;
            _teachingSteps[0].IsCurrent = true;
            TeachingGuidelinesPanel.Visibility = Visibility.Visible;
            TeachingGuideSubtitle.Text = $"ทำตามขั้นตอนเพื่อสอน AI สร้าง {taskType} workflow";
            UpdateTeachingProgress();
        }
    }

    private TeachingStepViewModel CreateTeachingStep(int number, string action, string thaiDesc, string engDesc,
        string elementHint, string? inputHint = null, bool isOptional = false)
    {
        return new TeachingStepViewModel
        {
            StepNumber = number,
            ActionType = action.ToUpper(),
            ActionColor = GetActionColor(action),
            Description = engDesc,
            DescriptionThai = thaiDesc,
            ElementHint = elementHint,
            InputHint = inputHint,
            IsOptional = isOptional,
            IsOptionalText = "(ข้ามได้)",
            IsCurrent = false,
            IsCompleted = false,
            HasElementHint = !string.IsNullOrEmpty(elementHint),
            HasInputHint = !string.IsNullOrEmpty(inputHint)
        };
    }

    private void ValidateTeachingStep(RecordedStep recordedStep)
    {
        if (_currentTeachingStepIndex >= _teachingSteps.Count) return;

        var currentStep = _teachingSteps[_currentTeachingStepIndex];
        var expectedAction = currentStep.ActionType.ToLower();
        var actualAction = recordedStep.Action?.ToLower() ?? "";

        // Check if action matches (with some flexibility)
        var isMatch = actualAction == expectedAction ||
                      (expectedAction == "click" && (actualAction == "click" || actualAction == "submit")) ||
                      (expectedAction == "type" && actualAction == "type") ||
                      (expectedAction == "upload" && (actualAction == "upload" || actualAction == "change"));

        if (isMatch)
        {
            // Mark current step as completed
            currentStep.IsCompleted = true;
            currentStep.IsCurrent = false;

            // Show success feedback
            ShowValidationFeedback(true, $"ขั้นตอนที่ {currentStep.StepNumber} ถูกต้อง!");

            // Move to next step
            _currentTeachingStepIndex++;
            if (_currentTeachingStepIndex < _teachingSteps.Count)
            {
                _teachingSteps[_currentTeachingStepIndex].IsCurrent = true;
            }

            UpdateTeachingProgress();

            // Check if all steps completed
            if (_currentTeachingStepIndex >= _teachingSteps.Count)
            {
                ShowStatus("สอน AI เสร็จสมบูรณ์! กรุณาตั้งชื่อและบันทึก Workflow", "success");
            }
        }
        else if (!currentStep.IsOptional)
        {
            // Wrong action for non-optional step
            ShowValidationFeedback(false, $"คาดหวัง: {currentStep.ActionType} - แต่ได้: {actualAction.ToUpper()}");
        }
        // If optional step doesn't match, skip it silently
        else
        {
            // Skip optional step if action doesn't match
            currentStep.IsCurrent = false;
            _currentTeachingStepIndex++;
            if (_currentTeachingStepIndex < _teachingSteps.Count)
            {
                _teachingSteps[_currentTeachingStepIndex].IsCurrent = true;
            }
            UpdateTeachingProgress();

            // Re-validate against new current step
            ValidateTeachingStep(recordedStep);
        }

        // Refresh the items control
        TeachingStepsControl.Items.Refresh();
    }

    private void ShowValidationFeedback(bool success, string message)
    {
        ValidationFeedbackPanel.Visibility = Visibility.Visible;
        ValidationText.Text = message;

        if (success)
        {
            ValidationIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            ValidationIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else
        {
            ValidationIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
            ValidationIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            ValidationText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }

        // Auto-hide after 3 seconds
        Task.Delay(3000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                ValidationFeedbackPanel.Visibility = Visibility.Collapsed;
            });
        });
    }

    private void UpdateTeachingProgress()
    {
        var completedCount = _teachingSteps.Count(s => s.IsCompleted);
        var totalCount = _teachingSteps.Count;

        if (totalCount > 0)
        {
            var progress = (double)completedCount / totalCount * 100;
            TeachingProgressBar.Value = progress;
            TeachingProgressText.Text = $"Step {Math.Min(completedCount + 1, totalCount)}/{totalCount}";
        }
    }

    #endregion

    #region Helper Methods

    private readonly Stack<List<RecordedStepViewModel>> _undoStack = new();
    private readonly Stack<List<RecordedStepViewModel>> _redoStack = new();

    private void SaveStateForUndo()
    {
        _undoStack.Push(_recordedSteps.ToList());
        _redoStack.Clear();
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        UndoButton.IsEnabled = _undoStack.Count > 0;
        RedoButton.IsEnabled = _redoStack.Count > 0;
    }

    private void ReindexSteps()
    {
        for (int i = 0; i < _recordedSteps.Count; i++)
        {
            _recordedSteps[i].Index = i;
            _recordedSteps[i].StepNumber = (i + 1).ToString();
        }
        UpdateStepCount();
    }

    private void UpdateConfidence()
    {
        if (_recordedSteps.Count == 0)
        {
            ConfidenceBar.Value = 0;
            ConfidenceText.Text = "0%";
            return;
        }

        var avgConfidence = _recordedSteps.Average(s => s.Confidence);
        ConfidenceBar.Value = avgConfidence * 100;
        ConfidenceText.Text = $"{avgConfidence:P0}";

        // Update color based on confidence
        if (avgConfidence >= 0.8)
            ConfidenceText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        else if (avgConfidence >= 0.6)
            ConfidenceText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        else
            ConfidenceText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
    }

    private double CalculateStepConfidence(RecordedStep step)
    {
        if (step.Element == null) return 0.5;

        double confidence = 0.5;

        // ID that's not dynamic
        if (!string.IsNullOrEmpty(step.Element.Id) && !IsDynamicId(step.Element.Id))
            confidence += 0.3;

        // data-testid
        if (step.Element.Attributes?.ContainsKey("data-testid") == true)
            confidence += 0.3;

        // aria-label
        if (step.Element.Attributes?.ContainsKey("aria-label") == true)
            confidence += 0.2;

        // name attribute
        if (!string.IsNullOrEmpty(step.Element.Name))
            confidence += 0.15;

        return Math.Min(1.0, confidence);
    }

    private bool IsDynamicId(string id)
    {
        if (string.IsNullOrEmpty(id)) return true;
        if (id.Length > 30) return true;
        if (id.Count(char.IsDigit) > id.Length * 0.5) return true;
        var dynamicPrefixes = new[] { "ember", "react", "ng-", "_", "svelte", "vue-" };
        return dynamicPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private void ShowStatus(string message, string type)
    {
        StatusText.Text = message;
        StatusBar.Visibility = Visibility.Visible;

        // Set icon and color based on type
        switch (type)
        {
            case "success":
                StatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                break;
            case "warning":
                StatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Alert;
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                break;
            case "error":
                StatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                break;
            default:
                StatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Information;
                StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                break;
        }

        // Auto-hide after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            Dispatcher.Invoke(() =>
            {
                if (StatusText.Text == message)
                {
                    StatusBar.Visibility = Visibility.Collapsed;
                }
            });
        });
    }

    private void GenerateAiSuggestions()
    {
        var suggestions = new List<string>();

        // Check for quick succession actions
        for (int i = 1; i < _recordedSteps.Count; i++)
        {
            if (_recordedSteps[i].OriginalStep?.Timestamp != null &&
                _recordedSteps[i - 1].OriginalStep?.Timestamp != null)
            {
                var timeDiff = _recordedSteps[i].OriginalStep!.Timestamp -
                               _recordedSteps[i - 1].OriginalStep!.Timestamp;
                if (timeDiff.TotalMilliseconds < 200)
                {
                    suggestions.Add("พบการกระทำที่เร็วเกินไป แนะนำเพิ่ม Wait step");
                    break;
                }
            }
        }

        // Check for low confidence selectors
        var lowConfidenceSteps = _recordedSteps.Where(s => s.Confidence < 0.6).ToList();
        if (lowConfidenceSteps.Count > 0)
        {
            suggestions.Add($"มี {lowConfidenceSteps.Count} step ที่ selector อาจไม่เสถียร");
        }

        // Check for type actions without preceding click
        for (int i = 0; i < _recordedSteps.Count; i++)
        {
            if (_recordedSteps[i].Action == "type" &&
                (i == 0 || _recordedSteps[i - 1].Action != "click"))
            {
                suggestions.Add($"Step #{_recordedSteps[i].StepNumber} อาจต้อง click element ก่อนพิมพ์");
                break;
            }
        }

        if (suggestions.Count > 0)
        {
            AiSuggestionText.Text = string.Join("\n• ", new[] { "" }.Concat(suggestions));
            AiSuggestionPanel.Visibility = Visibility.Visible;
        }
    }

    #endregion

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
    public double Confidence { get; set; } = 0.8;
    public string ConfidenceDisplay => Confidence >= 0.8 ? "High" : Confidence >= 0.6 ? "Medium" : "Low";
    public Brush ConfidenceColor => Confidence >= 0.8
        ? new SolidColorBrush(Color.FromRgb(16, 185, 129))    // Green
        : Confidence >= 0.6
            ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) // Orange
            : new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
}

/// <summary>
/// ViewModel for teaching step display in AI Guided Teaching panel
/// </summary>
public class TeachingStepViewModel
{
    public int StepNumber { get; set; }
    public string ActionType { get; set; } = "";
    public Brush ActionColor { get; set; } = Brushes.Gray;
    public string Description { get; set; } = "";
    public string DescriptionThai { get; set; } = "";
    public string? ElementHint { get; set; }
    public string? InputHint { get; set; }
    public bool IsOptional { get; set; }
    public string IsOptionalText { get; set; } = "(optional)";
    public bool IsCurrent { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsNotCompleted => !IsCompleted;
    public bool HasElementHint { get; set; }
    public bool HasInputHint { get; set; }
}

public class WebViewMessage
{
    public string? Type { get; set; }
    public RecordedStep? Data { get; set; }
}

/// <summary>
/// Response model for Ollama API
/// </summary>
public class OllamaResponse
{
    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("response")]
    public string? Response { get; set; }

    [JsonProperty("done")]
    public bool Done { get; set; }

    [JsonProperty("total_duration")]
    public long TotalDuration { get; set; }

    [JsonProperty("eval_count")]
    public int EvalCount { get; set; }
}

#endregion
