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
using AIManager.UI.Views.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Web Learning Page - Control Panel for teaching AI web automation
/// Manages WebViewWindow as a separate floating browser window
/// </summary>
public partial class WebLearningPage : Page
{
    private readonly ILogger<WebLearningPage>? _logger;
    private readonly WorkflowStorage? _workflowStorage;
    private readonly HttpClient _httpClient = new();
    private readonly ObservableCollection<RecordedStepViewModel> _recordedSteps = new();

    // WebView Window reference
    private WebViewWindow? _webViewWindow;
    private bool _isRecording;

    // Ollama configuration
    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string OllamaModel = "llama3.2";

    // Initial URL and platform passed from WorkflowManagerPage
    private readonly string? _initialUrl;
    private readonly string? _platformName;

    // Undo/Redo stacks
    private readonly Stack<List<RecordedStepViewModel>> _undoStack = new();
    private readonly Stack<List<RecordedStepViewModel>> _redoStack = new();

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
        }
        catch
        {
            // DI not available
        }

        StepsItemsControl.ItemsSource = _recordedSteps;

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

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Check Ollama status
        _ = CheckOllamaStatusAsync();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        // Close WebView window if open
        if (_webViewWindow != null)
        {
            _webViewWindow.Close();
            _webViewWindow = null;
        }
    }

    #region WebView Window Management

    private void OpenBrowserButton_Click(object sender, RoutedEventArgs e)
    {
        OpenOrFocusWebViewWindow();
    }

    private void OpenOrFocusWebViewWindow()
    {
        if (_webViewWindow == null || !_webViewWindow.IsLoaded)
        {
            // Create new WebView window
            var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
            _webViewWindow = new WebViewWindow(UrlTextBox.Text, platform);

            // Wire up events
            _webViewWindow.NavigationCompleted += WebViewWindow_NavigationCompleted;
            _webViewWindow.StepRecorded += WebViewWindow_StepRecorded;
            _webViewWindow.RecordingStarted += WebViewWindow_RecordingStarted;
            _webViewWindow.RecordingStopped += WebViewWindow_RecordingStopped;
            _webViewWindow.WindowClosing += WebViewWindow_Closing;

            _webViewWindow.Show();
            UpdateBrowserStatus(true);

            _logger?.LogInformation("WebView window opened for platform: {Platform}", platform);
        }
        else
        {
            // Focus existing window
            _webViewWindow.Activate();
            if (_webViewWindow.WindowState == WindowState.Minimized)
            {
                _webViewWindow.WindowState = WindowState.Normal;
            }
        }
    }

    private void WebViewWindow_NavigationCompleted(object? sender, string url)
    {
        Dispatcher.Invoke(() =>
        {
            TxtCurrentPageUrl.Text = url;
            TxtCurrentPageTitle.Text = _webViewWindow?.CurrentTitle ?? "Unknown";
            CurrentPageInfo.Visibility = Visibility.Visible;

            // Auto-detect platform from URL
            var detectedPlatform = DetectPlatformFromUrl(url);
            SelectPlatformInComboBox(detectedPlatform);
        });
    }

    private void WebViewWindow_StepRecorded(object? sender, WebViewStepEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            AddRecordedStep(e);
        });
    }

    private void WebViewWindow_RecordingStarted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isRecording = true;
            StartRecordingButton.IsEnabled = false;
            StopRecordingButton.IsEnabled = true;
        });
    }

    private void WebViewWindow_RecordingStopped(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isRecording = false;
            StartRecordingButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;
        });
    }

    private void WebViewWindow_Closing(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _webViewWindow = null;
            UpdateBrowserStatus(false);
            _isRecording = false;
            StartRecordingButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;
        });
    }

    private void UpdateBrowserStatus(bool isOpen)
    {
        if (isOpen)
        {
            BrowserStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            BrowserStatusText.Text = "Open";
            CurrentPageInfo.Visibility = Visibility.Visible;
        }
        else
        {
            BrowserStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
            BrowserStatusText.Text = "Not Open";
            CurrentPageInfo.Visibility = Visibility.Collapsed;
        }
    }

    private string DetectPlatformFromUrl(string url)
    {
        url = url.ToLowerInvariant();
        if (url.Contains("facebook.com") || url.Contains("fb.com")) return "Facebook";
        if (url.Contains("instagram.com")) return "Instagram";
        if (url.Contains("tiktok.com")) return "TikTok";
        if (url.Contains("twitter.com") || url.Contains("x.com")) return "Twitter";
        if (url.Contains("youtube.com") || url.Contains("youtu.be")) return "YouTube";
        if (url.Contains("line.me")) return "LINE";
        if (url.Contains("threads.net")) return "Threads";
        if (url.Contains("linkedin.com")) return "LinkedIn";
        if (url.Contains("pinterest.com")) return "Pinterest";
        return "Custom";
    }

    #endregion

    #region Recording Controls

    private async void StartRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewWindow == null || !_webViewWindow.IsLoaded)
        {
            OpenOrFocusWebViewWindow();
            await Task.Delay(1000); // Wait for window to load
        }

        if (_webViewWindow != null)
        {
            SaveStateForUndo();
            _recordedSteps.Clear();
            await _webViewWindow.StartRecordingAsync();
            _isRecording = true;
            StartRecordingButton.IsEnabled = false;
            StopRecordingButton.IsEnabled = true;
            _logger?.LogInformation("Recording started");
        }
    }

    private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
    {
        if (_webViewWindow != null && _webViewWindow.IsRecording)
        {
            _webViewWindow.StopRecording();
        }

        _isRecording = false;
        StartRecordingButton.IsEnabled = true;
        StopRecordingButton.IsEnabled = false;
        _logger?.LogInformation("Recording stopped - {Count} steps recorded", _recordedSteps.Count);
    }

    private void AddRecordedStep(WebViewStepEventArgs e)
    {
        var confidence = CalculateStepConfidence(e.Selector);
        var viewModel = new RecordedStepViewModel
        {
            Index = _recordedSteps.Count,
            StepNumber = (_recordedSteps.Count + 1).ToString(),
            Action = e.Action,
            ActionText = GetActionText(e.Action),
            ActionColor = GetActionColor(e.Action),
            ElementDescription = GetElementDescription(e),
            Value = e.Value,
            ValueText = !string.IsNullOrEmpty(e.Value) ? $"Value: {e.Value}" : null,
            HasValue = !string.IsNullOrEmpty(e.Value),
            Confidence = confidence,
            Selector = e.Selector,
            TagName = e.TagName,
            Text = e.Text
        };

        _recordedSteps.Add(viewModel);
        UpdateStepCount();
        _redoStack.Clear();
        UpdateUndoRedoButtons();

        _logger?.LogDebug("Step recorded: {Action} on {Element}", e.Action, e.Selector);
    }

    #endregion

    #region AI Command (Ollama)

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
            SendAiPromptButton.IsEnabled = false;
            OllamaStatusText.Text = "Processing...";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow

            var pageContext = _webViewWindow != null ? await _webViewWindow.GetPageContextAsync() : "";
            var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Custom";
            var taskType = (TaskTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";

            var fullPrompt = BuildOllamaPrompt(prompt, platform, taskType, pageContext);
            var response = await SendToOllamaAsync(fullPrompt);

            if (!string.IsNullOrEmpty(response))
            {
                ShowAiResponse(response);
                OllamaStatusText.Text = "Ollama Ready";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                AiPromptTextBox.Clear();
            }
            else
            {
                OllamaStatusText.Text = "No response";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }
        }
        catch (TimeoutException ex)
        {
            _logger?.LogWarning(ex, "Ollama timeout");
            OllamaStatusText.Text = "Timeout";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
        catch (HttpRequestException)
        {
            OllamaStatusText.Text = "Offline";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "AI prompt error");
            OllamaStatusText.Text = "Error";
            OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        finally
        {
            SendAiPromptButton.IsEnabled = true;
        }
    }

    private string BuildOllamaPrompt(string userPrompt, string platform, string taskType, string pageContext)
    {
        var systemPrompt = SystemKnowledge.GetCompactAISystemPrompt(platform, taskType, pageContext);
        return $@"{systemPrompt}

Question: {userPrompt}

Answer:";
    }

    private async Task<string> SendToOllamaAsync(string prompt)
    {
        var requestBody = new
        {
            model = OllamaModel,
            prompt = prompt,
            stream = false,
            options = new { temperature = 0.7, num_predict = 500 }
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        try
        {
            var response = await _httpClient.PostAsync($"{OllamaBaseUrl}/api/generate", content, cts.Token);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JsonConvert.DeserializeObject<OllamaResponse>(responseJson);
            return responseObj?.Response ?? "";
        }
        catch (TaskCanceledException)
        {
            throw new TimeoutException("Ollama timeout (60s)");
        }
    }

    private void ShowAiResponse(string response)
    {
        AiResponseText.Text = response;
        AiResponsePanel.Visibility = Visibility.Visible;
    }

    private void DismissAiResponseButton_Click(object sender, RoutedEventArgs e)
    {
        AiResponsePanel.Visibility = Visibility.Collapsed;
    }

    private void OllamaStatus_Click(object sender, MouseButtonEventArgs e)
    {
        var statusText = OllamaStatusText.Text;
        if (statusText.Contains("Offline") || statusText.Contains("Error") || statusText.Contains("Timeout"))
        {
            var guide = SystemKnowledge.GetOllamaTroubleshootingGuide();
            MessageBox.Show(guide, "Ollama Troubleshooting", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show($"Ollama is ready!\n\nModel: {OllamaModel}\nEndpoint: {OllamaBaseUrl}",
                "Ollama Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async Task CheckOllamaStatusAsync()
    {
        try
        {
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
                        OllamaStatusText.Text = $"Ollama Ready";
                        OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    }
                    else
                    {
                        OllamaStatusText.Text = "No models";
                        OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    }
                });
            }
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                OllamaStatusText.Text = "Offline";
                OllamaStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            });
        }
    }

    #endregion

    #region Steps Management

    private void UpdateStepCount()
    {
        StepCountText.Text = $"{_recordedSteps.Count} steps recorded";
    }

    private void ClearStepsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedSteps.Count == 0) return;

        if (MessageBox.Show("Clear all recorded steps?", "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            SaveStateForUndo();
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
        }
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0) return;

        _redoStack.Push(_recordedSteps.ToList());
        var previousState = _undoStack.Pop();

        _recordedSteps.Clear();
        foreach (var step in previousState)
        {
            _recordedSteps.Add(step);
        }

        UpdateUndoRedoButtons();
        UpdateStepCount();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count == 0) return;

        _undoStack.Push(_recordedSteps.ToList());
        var nextState = _redoStack.Pop();

        _recordedSteps.Clear();
        foreach (var step in nextState)
        {
            _recordedSteps.Add(step);
        }

        UpdateUndoRedoButtons();
        UpdateStepCount();
    }

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

    #endregion

    #region Workflow Save/Test

    private void TestWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedSteps.Count == 0)
        {
            MessageBox.Show("No steps recorded", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("Test Workflow feature will replay recorded steps in the browser.\n\n" +
            "Coming in full version with Playwright integration.",
            "Test Workflow", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void SaveWorkflowButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recordedSteps.Count == 0)
        {
            MessageBox.Show("No steps recorded", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var workflowName = WorkflowNameTextBox.Text.Trim();
        if (string.IsNullOrEmpty(workflowName))
        {
            MessageBox.Show("Please enter a workflow name", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            WorkflowNameTextBox.Focus();
            return;
        }

        try
        {
            var platform = (PlatformComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";
            var taskType = (TaskTypeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "custom";

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
                var step = ConvertToWorkflowStep(recordedStep, order++);
                if (step != null)
                {
                    workflow.Steps.Add(step);
                }
            }

            if (_workflowStorage != null)
            {
                await _workflowStorage.SaveWorkflowAsync(workflow);
                _logger?.LogInformation("Workflow saved: {Name} with {Count} steps", workflowName, workflow.Steps.Count);

                MessageBox.Show($"Workflow \"{workflowName}\" saved!\n\nSteps: {workflow.Steps.Count}",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                // Clear form
                _recordedSteps.Clear();
                WorkflowNameTextBox.Clear();
                WorkflowDescriptionTextBox.Clear();
                UpdateStepCount();
            }
            else
            {
                var json = JsonConvert.SerializeObject(workflow, Formatting.Indented);
                MessageBox.Show($"Workflow JSON:\n\n{json}", "Workflow Created", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save workflow");
            MessageBox.Show($"Error saving workflow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private WorkflowStep? ConvertToWorkflowStep(RecordedStepViewModel recorded, int order)
    {
        var step = new WorkflowStep
        {
            Id = Guid.NewGuid().ToString(),
            Order = order,
            Description = $"{recorded.Action} on {recorded.TagName ?? "element"}",
            InputValue = recorded.Value,
            WaitAfterMs = 500
        };

        step.Action = recorded.Action?.ToLower() switch
        {
            "click" => StepAction.Click,
            "input" => StepAction.Type,
            "type" => StepAction.Type,
            "upload" => StepAction.Upload,
            "select" => StepAction.Select,
            "submit" => StepAction.Click,
            _ => StepAction.Click
        };

        step.Selector = new ElementSelector
        {
            Type = SelectorType.CSS,
            Value = recorded.Selector ?? "",
            Confidence = recorded.Confidence
        };

        return step;
    }

    #endregion

    #region Helper Methods

    private string GetActionText(string action)
    {
        return action.ToLower() switch
        {
            "click" => "Click",
            "input" => "Type Text",
            "type" => "Type Text",
            "upload" => "Upload File",
            "select" => "Select Option",
            "submit" => "Submit Form",
            _ => action
        };
    }

    private Brush GetActionColor(string action)
    {
        return action.ToLower() switch
        {
            "click" => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            "input" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            "type" => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
            "upload" => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            "select" => new SolidColorBrush(Color.FromRgb(156, 39, 176)),
            "submit" => new SolidColorBrush(Color.FromRgb(0, 188, 212)),
            _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
        };
    }

    private string GetElementDescription(WebViewStepEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text) && e.Text.Length > 0)
        {
            var text = e.Text.Length > 30 ? e.Text.Substring(0, 30) + "..." : e.Text;
            return $"<{e.TagName ?? "element"}> \"{text}\"";
        }
        return e.Selector ?? "Unknown element";
    }

    private double CalculateStepConfidence(string? selector)
    {
        if (string.IsNullOrEmpty(selector)) return 0.5;

        if (selector.StartsWith("#") && !selector.Contains(" "))
            return 0.95; // ID selector

        if (selector.Contains("[data-testid="))
            return 0.9; // Test ID

        if (selector.Contains("[aria-label="))
            return 0.85; // Aria label

        if (selector.Contains("[name="))
            return 0.8; // Name attribute

        return 0.6; // Generic selector
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
    public double Confidence { get; set; } = 0.8;
    public string? Selector { get; set; }
    public string? TagName { get; set; }
    public string? Text { get; set; }

    public string ConfidenceDisplay => Confidence >= 0.8 ? "High" : Confidence >= 0.6 ? "Medium" : "Low";
    public Brush ConfidenceColor => Confidence >= 0.8
        ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
        : Confidence >= 0.6
            ? new SolidColorBrush(Color.FromRgb(245, 158, 11))
            : new SolidColorBrush(Color.FromRgb(239, 68, 68));
}

public class OllamaResponse
{
    [JsonProperty("model")]
    public string? Model { get; set; }

    [JsonProperty("response")]
    public string? Response { get; set; }

    [JsonProperty("done")]
    public bool Done { get; set; }
}

#endregion
