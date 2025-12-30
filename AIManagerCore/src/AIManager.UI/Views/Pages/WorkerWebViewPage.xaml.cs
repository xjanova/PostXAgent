using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Web.WebView2.Core;
using AIManager.Core.Workers;
using AIManager.Core.Services;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// WorkerWebViewPage - หน้าสำหรับดู/ควบคุม Worker browsers
/// Features:
/// - View-only mode: ดู browser ของ worker แต่ไม่สามารถควบคุมได้
/// - NeedsHelp mode: Worker ต้องการความช่วยเหลือ
/// - HumanControl mode: มนุษย์ควบคุมและบันทึก steps
/// </summary>
public partial class WorkerWebViewPage : Page
{
    private readonly WorkerManager? _workerManager;
    private readonly WorkerSessionTransferService? _sessionTransferService;
    private ManagedWorker? _selectedWorker;
    private bool _isRecording;
    private bool _webViewInitialized;

    // Observable collections for UI binding
    public ObservableCollection<WorkerWebViewDisplayItem> WorkersNeedingHelp { get; } = new();
    public ObservableCollection<WorkerWebViewDisplayItem> ActiveWorkers { get; } = new();
    public ObservableCollection<RecordedStep> RecordedSteps { get; } = new();
    public bool HasWorkersNeedingHelp => WorkersNeedingHelp.Count > 0;

    public WorkerWebViewPage()
    {
        InitializeComponent();
        DataContext = this;

        try
        {
            _workerManager = App.Services.GetService<WorkerManager>();
            _sessionTransferService = App.Services.GetService<WorkerSessionTransferService>();

            if (_workerManager != null)
            {
                // Subscribe to events
                _workerManager.WorkerHelpRequested += OnWorkerHelpRequested;
                _workerManager.WorkerViewModeChanged += OnWorkerViewModeChanged;
                _workerManager.WorkerStateChanged += OnWorkerStateChanged;
            }

            // Initialize WebView2
            InitializeWebView2Async();

            // Load initial worker list
            RefreshWorkerList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WorkerWebViewPage init error: {ex.Message}");
        }
    }

    #region WebView2 Initialization

    private async void InitializeWebView2Async()
    {
        try
        {
            await WorkerWebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;

            // Configure WebView2 settings
            var settings = WorkerWebView.CoreWebView2.Settings;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultContextMenusEnabled = true;
            settings.IsWebMessageEnabled = true;

            // Inject recording script
            await InjectRecordingScriptAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 init error: {ex.Message}");
        }
    }

    private async Task InjectRecordingScriptAsync()
    {
        if (!_webViewInitialized || WorkerWebView.CoreWebView2 == null) return;

        var recordingScript = @"
            (function() {
                if (window.__workerRecorder) return;

                window.__workerRecorder = {
                    recording: false,
                    steps: [],

                    start: function() {
                        this.recording = true;
                        this.steps = [];
                        this.attachListeners();
                    },

                    stop: function() {
                        this.recording = false;
                        this.detachListeners();
                    },

                    attachListeners: function() {
                        document.addEventListener('click', this.handleClick.bind(this), true);
                        document.addEventListener('input', this.handleInput.bind(this), true);
                        document.addEventListener('change', this.handleChange.bind(this), true);
                    },

                    detachListeners: function() {
                        document.removeEventListener('click', this.handleClick.bind(this), true);
                        document.removeEventListener('input', this.handleInput.bind(this), true);
                        document.removeEventListener('change', this.handleChange.bind(this), true);
                    },

                    handleClick: function(e) {
                        if (!this.recording) return;
                        this.recordStep('Click', e.target, null);
                    },

                    handleInput: function(e) {
                        if (!this.recording) return;
                        var self = this;
                        var target = e.target;
                        clearTimeout(this._inputTimeout);
                        this._inputTimeout = setTimeout(function() {
                            self.recordStep('Type', target, target.value);
                        }, 500);
                    },

                    handleChange: function(e) {
                        if (!this.recording) return;
                        if (e.target.type === 'checkbox') {
                            this.recordStep(e.target.checked ? 'Check' : 'Uncheck', e.target, null);
                        } else if (e.target.tagName === 'SELECT') {
                            this.recordStep('Select', e.target, e.target.value);
                        }
                    },

                    recordStep: function(actionType, element, value) {
                        var step = {
                            actionType: actionType,
                            selector: this.getSelector(element),
                            value: value,
                            description: this.getDescription(element, actionType),
                            url: window.location.href,
                            elementInfo: {
                                tagName: element.tagName,
                                id: element.id,
                                className: element.className,
                                text: element.innerText?.substring(0, 50),
                                ariaLabel: element.getAttribute('aria-label'),
                                x: element.offsetLeft,
                                y: element.offsetTop,
                                width: element.offsetWidth,
                                height: element.offsetHeight
                            }
                        };

                        this.steps.push(step);
                        window.chrome.webview.postMessage(JSON.stringify({type: 'step', data: step}));
                    },

                    getSelector: function(el) {
                        if (el.id) return '#' + el.id;
                        if (el.name) return '[name=""' + el.name + '""]';

                        var path = [];
                        while (el && el.nodeType === 1) {
                            var selector = el.tagName.toLowerCase();
                            if (el.className) {
                                selector += '.' + el.className.split(' ').filter(function(c) { return c; }).join('.');
                            }
                            path.unshift(selector);
                            if (path.length > 3) break;
                            el = el.parentNode;
                        }
                        return path.join(' > ');
                    },

                    getDescription: function(el, actionType) {
                        var text = (el.innerText && el.innerText.substring(0, 30)) || el.placeholder || el.name || el.id || el.tagName;
                        return actionType + ' on ' + text;
                    }
                };
            })();
        ";

        await WorkerWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(recordingScript);
    }

    #endregion

    #region Worker List Management

    private void RefreshWorkerList()
    {
        if (_workerManager == null) return;

        Dispatcher.Invoke(() =>
        {
            WorkersNeedingHelp.Clear();
            ActiveWorkers.Clear();

            foreach (var worker in _workerManager.GetAllWorkers())
            {
                var displayItem = new WorkerWebViewDisplayItem
                {
                    Id = worker.Id,
                    Name = worker.Name,
                    Platform = worker.Platform,
                    ViewMode = worker.ViewMode,
                    State = worker.State,
                    CurrentUrl = worker.ViewContext?.CurrentUrl,
                    HelpReason = worker.ViewContext?.HelpReason,
                    HelpRequestedAt = worker.ViewContext?.HelpRequestedAt,
                    RecordedStepsCount = worker.ViewContext?.RecordedSteps.Count ?? 0,
                    LastActivityAt = worker.LastActivityAt
                };

                if (worker.ViewMode == WorkerViewMode.NeedsHelp)
                {
                    WorkersNeedingHelp.Add(displayItem);
                }
                else if (worker.State == WorkerState.Running || worker.State == WorkerState.Paused)
                {
                    ActiveWorkers.Add(displayItem);
                }
            }

            // Notify property changed for HasWorkersNeedingHelp
            OnPropertyChanged(nameof(HasWorkersNeedingHelp));
        });
    }

    private void OnPropertyChanged(string propertyName)
    {
        // Simple property changed notification
    }

    #endregion

    #region Event Handlers

    private void OnWorkerHelpRequested(object? sender, WorkerHelpRequestedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshWorkerList();

            // Auto-select the worker that needs help
            SelectWorker(e.Worker);

            // Show notification
            System.Diagnostics.Debug.WriteLine($"Worker {e.Worker.Name} needs help: {e.Reason}");
        });
    }

    private void OnWorkerViewModeChanged(object? sender, WorkerViewModeChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshWorkerList();

            // Update UI if this is the selected worker
            if (_selectedWorker?.Id == e.WorkerId)
            {
                UpdateControlsForViewMode(e.NewMode);
            }
        });
    }

    private void OnWorkerStateChanged(object? sender, WorkerStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshWorkerList();
        });
    }

    #endregion

    #region Worker Selection

    private void WorkerCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is string workerId)
        {
            var worker = _workerManager?.GetWorker(workerId);
            if (worker != null)
            {
                SelectWorker(worker);
            }
        }
    }

    private async void SelectWorker(ManagedWorker worker)
    {
        _selectedWorker = worker;

        // Update UI
        TxtSelectedWorker.Text = worker.Name;
        TxtCurrentUrl.Text = worker.ViewContext?.CurrentUrl ?? "No URL";
        StatusBadge.Visibility = Visibility.Visible;
        NoWorkerPlaceholder.Visibility = Visibility.Collapsed;

        UpdateControlsForViewMode(worker.ViewMode);

        // Load WebView with worker's session
        if (_webViewInitialized && worker.ViewContext?.CurrentUrl != null)
        {
            try
            {
                WorkerWebView.Visibility = Visibility.Visible;
                WorkerWebView.CoreWebView2.Navigate(worker.ViewContext.CurrentUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Navigate error: {ex.Message}");
            }
        }
    }

    private void UpdateControlsForViewMode(WorkerViewMode mode)
    {
        Dispatcher.Invoke(() =>
        {
            // Reset all controls
            BtnViewOnly.IsEnabled = true;
            BtnAssist.IsEnabled = true;
            BtnTakeControl.IsEnabled = true;
            BtnDoneTeaching.Visibility = Visibility.Collapsed;
            RecordingPanel.Visibility = Visibility.Collapsed;
            ViewOnlyOverlay.Visibility = Visibility.Collapsed;
            LearningOverlay.Visibility = Visibility.Collapsed;

            switch (mode)
            {
                case WorkerViewMode.Headless:
                    TxtStatus.Text = "Headless";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(128, 128, 128));
                    ViewOnlyOverlay.Visibility = Visibility.Visible;
                    break;

                case WorkerViewMode.Viewing:
                    TxtStatus.Text = "View Only";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(6, 182, 212));
                    ViewOnlyOverlay.Visibility = Visibility.Visible;
                    break;

                case WorkerViewMode.NeedsHelp:
                    TxtStatus.Text = "Needs Help";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    ViewOnlyOverlay.Visibility = Visibility.Visible;
                    break;

                case WorkerViewMode.HumanControl:
                    TxtStatus.Text = "Recording";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    BtnDoneTeaching.Visibility = Visibility.Visible;
                    RecordingPanel.Visibility = Visibility.Visible;
                    ViewOnlyOverlay.Visibility = Visibility.Collapsed;
                    break;

                case WorkerViewMode.Learning:
                    TxtStatus.Text = "Learning";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(139, 92, 246));
                    LearningOverlay.Visibility = Visibility.Visible;
                    BtnViewOnly.IsEnabled = false;
                    BtnAssist.IsEnabled = false;
                    BtnTakeControl.IsEnabled = false;
                    break;

                case WorkerViewMode.Resuming:
                    TxtStatus.Text = "Resuming";
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    LearningOverlay.Visibility = Visibility.Visible;
                    break;
            }
        });
    }

    #endregion

    #region Control Button Handlers

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshWorkerList();
    }

    private void BtnViewOnly_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorker == null || _workerManager == null) return;

        _workerManager.SetWorkerViewMode(_selectedWorker.Id, WorkerViewMode.Viewing);
    }

    private void BtnAssist_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorker == null || _workerManager == null) return;

        // Start human control with recording
        StartRecording();
    }

    private void BtnTakeControl_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorker == null || _workerManager == null) return;

        // Emergency take control
        var result = MessageBox.Show(
            "This will pause the worker and give you full control. Continue?",
            "Take Control",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            StartRecording();
        }
    }

    private async void BtnDoneTeaching_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorker == null || _workerManager == null) return;

        StopRecording();

        // Process the recorded steps
        TxtLearningProgress.Text = $"{RecordedSteps.Count} steps recorded";
        LearningOverlay.Visibility = Visibility.Visible;

        try
        {
            // Resume worker with learned workflow
            await _workerManager.ResumeWorkerFromHelpAsync(_selectedWorker.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resume error: {ex.Message}");
        }
    }

    private void BtnSaveWorkflow_Click(object sender, RoutedEventArgs e)
    {
        BtnDoneTeaching_Click(sender, e);
    }

    private void BtnCancelRecording_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedWorker == null || _workerManager == null) return;

        StopRecording();
        RecordedSteps.Clear();

        // Return to viewing mode
        _workerManager.SetWorkerViewMode(_selectedWorker.Id, WorkerViewMode.Viewing);
    }

    #endregion

    #region Recording

    private async void StartRecording()
    {
        if (_selectedWorker == null || _workerManager == null) return;

        _isRecording = true;
        RecordedSteps.Clear();

        // Set to human control mode
        _workerManager.StartRecording(_selectedWorker.Id);

        // Start recording in WebView
        if (_webViewInitialized)
        {
            try
            {
                await WorkerWebView.CoreWebView2.ExecuteScriptAsync("window.__workerRecorder.start()");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Start recording error: {ex.Message}");
            }
        }
    }

    private async void StopRecording()
    {
        _isRecording = false;

        // Stop recording in WebView
        if (_webViewInitialized)
        {
            try
            {
                await WorkerWebView.CoreWebView2.ExecuteScriptAsync("window.__workerRecorder.stop()");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop recording error: {ex.Message}");
            }
        }
    }

    #endregion

    #region WebView Events

    private void WorkerWebView_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_selectedWorker != null)
        {
            TxtCurrentUrl.Text = WorkerWebView.Source?.ToString() ?? "";

            // Update worker context
            if (_selectedWorker.ViewContext != null)
            {
                _selectedWorker.ViewContext.CurrentUrl = WorkerWebView.Source?.ToString();
            }
        }
    }

    private void WorkerWebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var message = e.WebMessageAsJson;
            var data = System.Text.Json.JsonDocument.Parse(message);

            if (data.RootElement.GetProperty("type").GetString() == "step")
            {
                var stepData = data.RootElement.GetProperty("data");

                Dispatcher.Invoke(() =>
                {
                    var step = new RecordedStep
                    {
                        StepNumber = RecordedSteps.Count + 1,
                        ActionType = Enum.Parse<RecordedActionType>(stepData.GetProperty("actionType").GetString() ?? "Click"),
                        Selector = stepData.GetProperty("selector").GetString(),
                        Value = stepData.TryGetProperty("value", out var val) ? val.GetString() : null,
                        Description = stepData.GetProperty("description").GetString(),
                        Url = stepData.GetProperty("url").GetString()
                    };

                    RecordedSteps.Add(step);

                    // Also add to worker manager
                    if (_selectedWorker != null)
                    {
                        _workerManager?.AddRecordedStep(_selectedWorker.Id, step);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebMessage error: {ex.Message}");
        }
    }

    #endregion
}
