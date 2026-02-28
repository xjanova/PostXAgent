using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AIManager.Core.Services;
using AIManager.Core.NodeEditor;
using AIManager.Core.NodeEditor.Models;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Log entry with color and icon support
/// </summary>
public class LogEntry
{
    public string Timestamp { get; set; } = "";
    public string Message { get; set; } = "";
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Icon { get; set; } = "InformationOutline";
    public string IconColor { get; set; } = "#3B82F6";
    public string TextColor { get; set; } = "#E0E0E0";

    public enum LogLevel
    {
        Debug,
        Info,
        Success,
        Warning,
        Error,
        Step
    }

    public static LogEntry Create(string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Message = message
        };

        // Detect log level from message content
        var msgLower = message.ToLowerInvariant();

        // Check for step indicators [1/7], [Step 1], etc.
        if (System.Text.RegularExpressions.Regex.IsMatch(message, @"\[\d+/\d+\]"))
        {
            entry.Level = LogLevel.Step;
            entry.Icon = "CircleOutline";
            entry.IconColor = "#8B5CF6";  // Purple
            entry.TextColor = "#C4B5FD";  // Light purple
        }
        // Success indicators
        else if (msgLower.Contains("success") || msgLower.Contains("completed") ||
                 msgLower.Contains("ready") || msgLower.Contains("[ok]"))
        {
            entry.Level = LogLevel.Success;
            entry.Icon = "CheckCircle";
            entry.IconColor = "#10B981";  // Green
            entry.TextColor = "#34D399";  // Light green
        }
        // Error indicators
        else if (msgLower.Contains("error") || msgLower.Contains("fail") ||
                 msgLower.Contains("exception") || msgLower.Contains("[x]"))
        {
            entry.Level = LogLevel.Error;
            entry.Icon = "AlertCircle";
            entry.IconColor = "#EF4444";  // Red
            entry.TextColor = "#F87171";  // Light red
        }
        // Warning indicators
        else if (msgLower.Contains("warning") || msgLower.Contains("warn") ||
                 msgLower.Contains("caution") || msgLower.Contains("[warn]") ||
                 msgLower.Contains("[!]"))
        {
            entry.Level = LogLevel.Warning;
            entry.Icon = "AlertOutline";
            entry.IconColor = "#F59E0B";  // Yellow/Orange
            entry.TextColor = "#FBBF24";  // Light yellow
        }
        // Starting/Loading indicators
        else if (msgLower.Contains("starting") || msgLower.Contains("loading") ||
                 msgLower.Contains("connecting") || msgLower.Contains("initializing") ||
                 msgLower.Contains("..."))
        {
            entry.Level = LogLevel.Info;
            entry.Icon = "Loading";
            entry.IconColor = "#3B82F6";  // Blue
            entry.TextColor = "#60A5FA";  // Light blue
        }
        // Validation section headers
        else if (message.StartsWith("[Validation]"))
        {
            entry.Level = LogLevel.Info;
            entry.Icon = "ShieldCheck";
            entry.IconColor = "#06B6D4";  // Cyan
            entry.TextColor = "#22D3EE";  // Light cyan
        }
        // Separator lines
        else if (message.StartsWith("─"))
        {
            entry.Level = LogLevel.Debug;
            entry.Icon = "Minus";
            entry.IconColor = "#4B5563";  // Gray
            entry.TextColor = "#6B7280";  // Light gray
        }
        // Debug/verbose
        else if (msgLower.Contains("debug") || msgLower.Contains("verbose"))
        {
            entry.Level = LogLevel.Debug;
            entry.Icon = "BugOutline";
            entry.IconColor = "#6B7280";  // Gray
            entry.TextColor = "#9CA3AF";  // Light gray
        }
        // Default info
        else
        {
            entry.Level = LogLevel.Info;
            entry.Icon = "InformationOutline";
            entry.IconColor = "#3B82F6";  // Blue
            entry.TextColor = "#E0E0E0";  // White/gray
        }

        return entry;
    }

    public string DisplayText => $"[{Timestamp}] {Message}";
}

public partial class DiffusersManagerPage : Page
{
    private readonly DiffusersEngineManager? _manager;
    private readonly LocalGpuService _gpuService;
    private readonly ComfyUIService _comfyService;
    private readonly WorkflowEngine? _workflowEngine;
    private readonly ObservableCollection<LogEntry> _logEntries = new();
    private readonly DispatcherTimer _updateTimer;
    private DateTime? _startTime;
    private int _requestCount;
    private CancellationTokenSource? _workflowCts;
    private CancellationTokenSource? _engineStartCts;
    private bool _isEngineStarting;

    // Colors
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(245, 158, 11));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(107, 114, 128));

    // Processing Module
    public enum ProcessingModule
    {
        Diffusers,
        ComfyUI,
        CloudGPU
    }

    private ProcessingModule _currentModule = ProcessingModule.Diffusers;

    public DiffusersManagerPage()
    {
        InitializeComponent();

        try
        {
            _manager = DiffusersEngineManager.Instance;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get DiffusersEngineManager: {ex.Message}");
            _manager = null!;
        }

        _gpuService = new LocalGpuService();
        _comfyService = new ComfyUIService();

        // WorkflowEngine requires DI - will be set later if available
        try
        {
            var nodeRegistry = new NodeRegistry();
            var contentGen = new ContentGeneratorService();
            var diffusersEngine = _manager?.Engine;
            var imageGen = new ImageGeneratorService(diffusersEngine: diffusersEngine);
            _workflowEngine = new WorkflowEngine(null!, nodeRegistry, contentGen, imageGen, diffusersEngine);

            // Subscribe to workflow engine events
            _workflowEngine.NodeStateChanged += WorkflowEngine_NodeStateChanged;
            _workflowEngine.LogMessage += (msg) => Dispatcher.Invoke(() => AddLogEntry(msg));
        }
        catch
        {
            _workflowEngine = null;
        }

        LogItems.ItemsSource = _logEntries;

        // Subscribe to manager events
        if (_manager != null)
        {
            _manager.StatusChanged += Manager_StatusChanged;
            _manager.LogMessage += Manager_LogMessage;
            _manager.HuggingFaceTokenChanged += Manager_HuggingFaceTokenChanged;
        }

        // Update timer for uptime and stats
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += DiffusersManagerPage_Loaded;
        Unloaded += DiffusersManagerPage_Unloaded;
    }

    private async void DiffusersManagerPage_Loaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Start();
        await UpdateStatusAsync();
        await LoadSystemInfoAsync();
        await UpdateModuleStatusAsync();
        UpdateHfTokenStatus();
        AddLogEntry("Diffusers Manager page loaded");
    }

    private void DiffusersManagerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        if (_manager != null)
        {
            _manager.StatusChanged -= Manager_StatusChanged;
            _manager.LogMessage -= Manager_LogMessage;
            _manager.HuggingFaceTokenChanged -= Manager_HuggingFaceTokenChanged;
        }
        _workflowCts?.Cancel();
    }

    #region Engine Status

    private void Manager_StatusChanged(object? sender, EngineStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var status = MapEngineStatus(e.Status);
            UpdateStatusUI(status);
            AddLogEntry($"[{e.Status}] {e.Message}");
        });
    }

    private static DiffusersEngineStatus MapEngineStatus(EngineStatus status)
    {
        return status switch
        {
            EngineStatus.Running => DiffusersEngineStatus.Running,
            EngineStatus.Starting => DiffusersEngineStatus.Starting,
            EngineStatus.Error => DiffusersEngineStatus.Error,
            EngineStatus.Loading => DiffusersEngineStatus.Starting,
            EngineStatus.Generating => DiffusersEngineStatus.Running,
            _ => DiffusersEngineStatus.Stopped
        };
    }

    private void Manager_LogMessage(object? sender, string message)
    {
        Dispatcher.Invoke(() => AddLogEntry(message));
    }

    private void Manager_HuggingFaceTokenChanged(object? sender, bool hasToken)
    {
        Dispatcher.Invoke(() => UpdateHfTokenStatus());
    }

    private async Task UpdateStatusAsync()
    {
        var isRunning = _manager?.IsReady ?? false;
        var status = isRunning ? DiffusersEngineStatus.Running : DiffusersEngineStatus.Stopped;
        UpdateStatusUI(status);

        if (isRunning && _startTime == null)
        {
            _startTime = DateTime.Now.AddSeconds(-10);
        }

        // Update loaded model - use DisplayModel which shows default model even before engine starts
        var displayModel = _manager?.DisplayModel;
        // Always have a model to display (defaults to SDXL)
        var modelToShow = !string.IsNullOrEmpty(displayModel) ? displayModel : RecommendedModels.DefaultModel;
        var modelName = modelToShow.Split('/').LastOrDefault() ?? modelToShow;
        // Show status: (loaded) if actually in VRAM, (default) if just configured
        var isActuallyLoaded = !string.IsNullOrEmpty(_manager?.CurrentModel);
        TxtLoadedModel.Text = isActuallyLoaded ? modelName : $"{modelName} (default)";

        await Task.CompletedTask;
    }

    private void UpdateStatusUI(DiffusersEngineStatus status)
    {
        switch (status)
        {
            case DiffusersEngineStatus.Running:
                StatusDot.Fill = GreenBrush;
                TxtStatus.Text = "Running";
                StatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x10, 0xB9, 0x81));
                BtnStartStop.Tag = "Running";
                if (_startTime == null) _startTime = DateTime.Now;
                break;

            case DiffusersEngineStatus.Starting:
                StatusDot.Fill = YellowBrush;
                TxtStatus.Text = "Starting...";
                StatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xF5, 0x9E, 0x0B));
                // Keep button enabled and change to "Stop" so user can cancel
                BtnStartStop.Tag = "Starting";
                BtnStartStop.IsEnabled = true;
                break;

            case DiffusersEngineStatus.Stopping:
                StatusDot.Fill = YellowBrush;
                TxtStatus.Text = "Stopping...";
                StatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xF5, 0x9E, 0x0B));
                BtnStartStop.Tag = "Stopping";
                BtnStartStop.IsEnabled = false;
                break;

            case DiffusersEngineStatus.Error:
                StatusDot.Fill = RedBrush;
                TxtStatus.Text = "Error";
                StatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xEF, 0x44, 0x44));
                BtnStartStop.Tag = null;
                BtnStartStop.IsEnabled = true;
                _startTime = null;
                break;

            default:
                StatusDot.Fill = GrayBrush;
                TxtStatus.Text = "Stopped";
                StatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x6B, 0x72, 0x80));
                BtnStartStop.Tag = null;
                BtnStartStop.IsEnabled = true;
                _startTime = null;
                break;
        }
    }

    private async Task LoadSystemInfoAsync()
    {
        // GPU Info
        var gpuInfo = await _gpuService.DetectGpuAsync();
        if (gpuInfo?.IsAvailable == true)
        {
            TxtGpu.Text = gpuInfo.Name;
            TxtVram.Text = $"{gpuInfo.FreeVramGb:F1} / {gpuInfo.TotalVramGb:F1} GB";
        }
        else
        {
            TxtGpu.Text = "Not detected";
            TxtVram.Text = "-";
        }

        // Python Info
        var pythonPath = Environment.GetEnvironmentVariable("DIFFUSERS_PYTHON_PATH");
        if (!string.IsNullOrEmpty(pythonPath) && System.IO.File.Exists(pythonPath))
        {
            TxtPython.Text = "Embedded";
        }
        else
        {
            TxtPython.Text = "System";
        }

        // Port
        TxtPort.Text = DiffusersGenerationEngine.DEFAULT_PORT.ToString();
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        // Update uptime
        if (_startTime.HasValue && (_manager?.IsReady ?? false))
        {
            var uptime = DateTime.Now - _startTime.Value;
            TxtUptime.Text = FormatUptime(uptime);
        }
        else
        {
            TxtUptime.Text = "-";
        }

        // Update PID
        TxtPid.Text = _manager?.ProcessId?.ToString() ?? "-";

        // Update request count
        TxtRequests.Text = _requestCount.ToString();

        // Update log count
        TxtLogCount.Text = $"({_logEntries.Count})";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
        if (uptime.TotalMinutes >= 1)
            return $"{uptime.Minutes}m {uptime.Seconds}s";
        return $"{uptime.Seconds}s";
    }

    #endregion

    #region Control Panel Actions

    private async void BtnStartStop_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null)
        {
            AddLogEntry("Manager not available");
            return;
        }

        // Handle cancel during starting
        if (_isEngineStarting)
        {
            AddLogEntry("Cancelling engine startup...");
            _engineStartCts?.Cancel();
            _isEngineStarting = false;
            UpdateStatusUI(DiffusersEngineStatus.Stopped);
            return;
        }

        if (_manager.IsReady)
        {
            // Stop engine
            AddLogEntry("Stopping engine...");
            UpdateStatusUI(DiffusersEngineStatus.Stopping);

            try
            {
                await _manager.ShutdownAsync();
                AddLogEntry("Engine stopped successfully");
                _startTime = null;
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error stopping engine: {ex.Message}");
            }

            UpdateStatusUI(DiffusersEngineStatus.Stopped);
        }
        else
        {
            // Start engine
            _isEngineStarting = true;
            _engineStartCts = new CancellationTokenSource();
            AddLogEntry("Starting engine...");
            UpdateStatusUI(DiffusersEngineStatus.Starting);

            try
            {
                // First do preflight checks to show detailed status
                AddLogEntry("Running pre-flight checks...");
                var preflightResult = await _manager.Engine.PerformPreflightChecksAsync(autoInstall: false, ct: _engineStartCts.Token);

                if (preflightResult.HasGpu)
                {
                    AddLogEntry($"GPU detected: {preflightResult.GpuInfo?.Name ?? "Unknown"}");
                    AddLogEntry($"VRAM: {preflightResult.GpuInfo?.TotalVramGb:F1} GB (Free: {preflightResult.GpuInfo?.FreeVramGb:F1} GB)");
                }
                else
                {
                    AddLogEntry("WARNING: No GPU detected - will use CPU (very slow)");
                }

                if (preflightResult.HasPython)
                {
                    AddLogEntry($"Python: {preflightResult.PythonEnv?.PythonVersion ?? "Unknown"}");
                    if (preflightResult.PythonReady)
                    {
                        AddLogEntry("PyTorch and Diffusers: Ready");
                    }
                    else
                    {
                        AddLogEntry("WARNING: PyTorch/Diffusers not installed");
                        if (preflightResult.PythonEnv?.MissingPackages?.Count > 0)
                        {
                            AddLogEntry($"Missing packages: {string.Join(", ", preflightResult.PythonEnv.MissingPackages)}");
                        }
                    }
                }
                else
                {
                    AddLogEntry("WARNING: Python not found");
                }

                // Show any warnings
                foreach (var warning in preflightResult.Warnings)
                {
                    AddLogEntry($"WARNING: {warning}");
                }

                // Show any errors
                foreach (var error in preflightResult.Errors)
                {
                    AddLogEntry($"ERROR: {error}");
                }

                if (!preflightResult.IsReady)
                {
                    AddLogEntry("Pre-flight checks failed. Attempting auto-install...");
                }

                // Now try to initialize (which will auto-install if needed)
                var success = await _manager.InitializeAsync(ct: _engineStartCts.Token);
                if (success)
                {
                    AddLogEntry("Engine started successfully");
                    _startTime = DateTime.Now;
                    UpdateStatusUI(DiffusersEngineStatus.Running);

                    // Check startup validation status
                    await CheckStartupValidationAsync();

                    // Update WorkflowEngine with the running engine
                    if (_workflowEngine != null && _manager.Engine != null)
                    {
                        _workflowEngine.SetDiffusersEngine(_manager.Engine);
                        AddLogEntry("WorkflowEngine connected to Diffusers Engine");
                    }
                }
                else
                {
                    AddLogEntry("Failed to start engine");
                    AddLogEntry("TIP: Go to 'Generation Pipeline' page and click 'GPU Setup' in DISTRIBUTOR section");
                    AddLogEntry("Or go to 'GPU Setup Wizard' page for step-by-step installation guide");
                    UpdateStatusUI(DiffusersEngineStatus.Error);

                    // Offer to open GPU Setup Wizard
                    var result = MessageBox.Show(
                        "Cannot start Engine.\n\n" +
                        "Would you like to go to GPU Setup Wizard to install Python and dependencies?",
                        "Engine Start Failed",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (Window.GetWindow(this) is MainWindow mainWindow)
                        {
                            mainWindow.NavigateToPage("GpuSetupWizard");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AddLogEntry("Engine startup cancelled by user");
                UpdateStatusUI(DiffusersEngineStatus.Stopped);
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error starting engine: {ex.Message}");
                UpdateStatusUI(DiffusersEngineStatus.Error);
            }
            finally
            {
                _isEngineStarting = false;
                _engineStartCts?.Dispose();
                _engineStartCts = null;
            }
        }

        BtnStartStop.IsEnabled = true;
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        AddLogEntry("Log cleared");
    }

    private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_logEntries.Count == 0)
            {
                MessageBox.Show("No log entries to copy.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var logText = string.Join(Environment.NewLine, _logEntries.Select(e => e.DisplayText));
            Clipboard.SetText(logText);
            AddLogEntry("Log copied to clipboard");
            MessageBox.Show($"Copied {_logEntries.Count} log entries to clipboard.", "Log Copied",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to copy log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("GenerationPipeline");
        }
    }

    private void BtnHfToken_Click(object sender, RoutedEventArgs e)
    {
        if (_manager == null)
        {
            MessageBox.Show("Manager not available", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var dialog = new Views.Dialogs.HuggingFaceTokenDialog(_manager.HasHuggingFaceToken);
        dialog.Owner = Window.GetWindow(this);

        if (dialog.ShowDialog() == true)
        {
            var token = dialog.Token;
            _manager.SetHuggingFaceToken(string.IsNullOrWhiteSpace(token) ? null : token);
            UpdateHfTokenStatus();

            if (!string.IsNullOrWhiteSpace(token))
            {
                AddLogEntry("[Settings] HuggingFace token configured - restart engine to apply");
                MessageBox.Show(
                    "HuggingFace token has been saved.\n\n" +
                    "If the engine is running, please restart it to apply the new token.\n" +
                    "This token allows access to gated models like FLUX.1-dev.",
                    "Token Saved",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                AddLogEntry("[Settings] HuggingFace token cleared");
            }
        }
    }

    private void UpdateHfTokenStatus()
    {
        if (_manager != null && BtnHfToken != null)
        {
            BtnHfToken.Tag = _manager.HasHuggingFaceToken ? "Configured" : null;
        }
    }

    /// <summary>
    /// Check startup validation status and show warnings if needed
    /// </summary>
    private async Task CheckStartupValidationAsync()
    {
        if (_manager?.Engine == null) return;

        try
        {
            var status = await _manager.Engine.GetStartupStatusAsync();
            if (status == null)
            {
                AddLogEntry("[Validation] Cannot check status");
                return;
            }

            // Log validation steps
            AddLogEntry("─────────────────────────────────────────");
            AddLogEntry("[Validation] System validation results:");

            foreach (var step in status.Steps)
            {
                var icon = step.Status == "ok" ? "[OK]" : step.Status == "error" ? "[X]" : "[!]";
                var msg = $"  [{step.StepNumber}/7] {icon} {step.Name}";
                if (!string.IsNullOrEmpty(step.Message))
                    msg += $": {step.Message}";
                AddLogEntry(msg);
            }

            // Show warnings/missing features
            if (status.MissingFeatures.Count > 0)
            {
                AddLogEntry("─────────────────────────────────────────");
                AddLogEntry($"[Validation] [WARN] Missing {status.MissingFeatures.Count} feature(s):");
                foreach (var feature in status.MissingFeatures)
                {
                    AddLogEntry($"  • {feature}");
                }

                // Show dialog with option to continue
                var warningMsg = "System can run but some features are missing:\n\n";
                foreach (var feature in status.MissingFeatures)
                {
                    warningMsg += $"• {feature}\n";
                }
                warningMsg += "\nDo you want to continue?";

                var result = MessageBox.Show(
                    warningMsg,
                    "Missing Features",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    AddLogEntry("[Validation] User cancelled operation");
                    await _manager.ShutdownAsync();
                    UpdateStatusUI(DiffusersEngineStatus.Stopped);
                    return;
                }

                AddLogEntry("[Validation] User chose to continue (missing some features)");
            }

            // Final status
            AddLogEntry("─────────────────────────────────────────");
            if (status.HasGpu)
            {
                AddLogEntry($"[Validation] [OK] GPU: {status.GpuName}");
            }
            else
            {
                AddLogEntry("[Validation] [WARN] No GPU found - will use CPU (very slow)");
            }

            AddLogEntry($"[Validation] ControlNet: {(status.ControlNetAvailable ? "[OK] Ready" : "[X] Not available")}");
            AddLogEntry($"[Validation] IP-Adapter: {(status.IpAdapterAvailable ? "[OK] Ready" : "[X] Not available")}");
            AddLogEntry("─────────────────────────────────────────");

            if (status.Success && status.MissingFeatures.Count == 0)
            {
                AddLogEntry("[Validation] [OK] System ready with all features");
            }
            else if (status.CanContinue)
            {
                AddLogEntry("[Validation] [OK] System ready (missing some features)");
            }
        }
        catch (Exception ex)
        {
            AddLogEntry($"[Validation] Error: {ex.Message}");
        }
    }

    #endregion

    #region Processing Module

    private async void ProcessingModule_Changed(object sender, RoutedEventArgs e)
    {
        if (RbDiffusers.IsChecked == true)
            _currentModule = ProcessingModule.Diffusers;
        else if (RbComfyUI.IsChecked == true)
            _currentModule = ProcessingModule.ComfyUI;
        else if (RbCloudGpu.IsChecked == true)
            _currentModule = ProcessingModule.CloudGPU;

        await UpdateModuleStatusAsync();
        AddLogEntry($"Processing module changed to: {_currentModule}");
    }

    private async Task UpdateModuleStatusAsync()
    {
        try
        {
            // Update Diffusers status
            var diffusersRunning = _manager?.IsReady ?? false;
            if (DiffusersStatusDot != null)
                DiffusersStatusDot.Fill = diffusersRunning ? GreenBrush : GrayBrush;

            // Update ComfyUI status
            var comfyAvailable = _comfyService != null && await _comfyService.IsAvailableAsync();
            if (ComfyStatusDot != null)
                ComfyStatusDot.Fill = comfyAvailable ? GreenBrush : GrayBrush;

            // Update Cloud status (placeholder)
            if (CloudStatusDot != null)
                CloudStatusDot.Fill = GrayBrush;

            // Update status text
            if (TxtModuleStatus != null)
            {
                TxtModuleStatus.Text = _currentModule switch
                {
                    ProcessingModule.Diffusers => diffusersRunning ? "Diffusers: Running" : "Diffusers: Stopped",
                    ProcessingModule.ComfyUI => comfyAvailable ? "ComfyUI: Connected" : "ComfyUI: Offline",
                    ProcessingModule.CloudGPU => "Cloud: Not configured",
                    _ => "Unknown"
                };

                TxtModuleStatus.Foreground = _currentModule switch
                {
                    ProcessingModule.Diffusers when diffusersRunning => GreenBrush,
                    ProcessingModule.ComfyUI when comfyAvailable => GreenBrush,
                    _ => GrayBrush
                };
            }
        }
        catch (Exception ex)
        {
            AddLogEntry($"Error updating module status: {ex.Message}");
        }
    }

    #endregion

    #region Configuration Actions

    private void ManageLoRAs_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open LoRA manager dialog
        AddLogEntry("LoRA manager not implemented yet");
        MessageBox.Show("LoRA manager will be available in a future update.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Output Directory"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtOutputDir.Text = dialog.FolderName;
        }
    }

    private void SaveConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Build configuration from UI
            var config = new
            {
                Input = new
                {
                    NegativePrompt = TxtDefaultNegative.Text,
                    UseClipSkip = ChkClipSkip.IsChecked ?? false,
                    ClipSkipLayers = int.TryParse(TxtClipSkip.Text, out var cs) ? cs : 1,
                    UsePromptWeighting = ChkPromptWeighting.IsChecked ?? true
                },
                Model = new
                {
                    Precision = (CmbPrecision.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "FP16",
                    UseCustomVae = ChkCustomVae.IsChecked ?? false,
                    TiledVae = ChkTiledVae.IsChecked ?? false
                },
                Sampler = new
                {
                    Type = (CmbSampler.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "DPM++ 2M Karras",
                    Scheduler = (CmbScheduler.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Normal",
                    Steps = int.TryParse(TxtSteps.Text, out var steps) ? steps : 30,
                    CfgScale = double.TryParse(TxtCfgScale.Text, out var cfg) ? cfg : 7.5,
                    Seed = long.TryParse(TxtSeed.Text, out var seed) ? seed : -1
                },
                Processor = new
                {
                    AttentionSlicing = ChkAttentionSlicing.IsChecked ?? true,
                    VaeSlicing = ChkVaeSlicing.IsChecked ?? true,
                    UseXformers = ChkXformers.IsChecked ?? true,
                    UseSafetensors = ChkSafetensors.IsChecked ?? true,
                    TimeoutSeconds = int.TryParse(TxtTimeout.Text, out var timeout) ? timeout : 300
                },
                PostProcess = new
                {
                    EnableUpscale = ChkUpscale.IsChecked ?? false,
                    Upscaler = (CmbUpscaler.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "RealESRGAN 4x",
                    EnableFaceRestore = ChkFaceRestore.IsChecked ?? false
                },
                Output = new
                {
                    Directory = TxtOutputDir.Text,
                    FilenamePattern = TxtFilenamePattern.Text,
                    ImageFormat = (CmbImageFormat.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "PNG",
                    SaveMetadata = ChkSaveMetadata.IsChecked ?? true
                }
            };

            // Save to settings file
            var configPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PostXAgent", "diffusers_config.json");

            var dir = System.IO.Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            System.IO.File.WriteAllText(configPath, json);

            AddLogEntry("Configuration saved successfully");
            MessageBox.Show("Configuration saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AddLogEntry($"Error saving configuration: {ex.Message}");
            MessageBox.Show($"Error saving configuration: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Workflow Actions

    private void NewWorkflow_Click(object sender, RoutedEventArgs e)
    {
        WorkflowCanvas.ClearWorkflow();
        AddLogEntry("New workflow created");
    }

    private void OpenWorkflow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Workflow files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Open Workflow"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = System.IO.File.ReadAllText(dialog.FileName);
                var workflow = JsonConvert.DeserializeObject<WorkflowGraph>(json);
                if (workflow != null)
                {
                    WorkflowCanvas.LoadWorkflow(workflow);
                    AddLogEntry($"Workflow loaded: {dialog.FileName}");
                }
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading workflow: {ex.Message}");
                MessageBox.Show($"Error loading workflow: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveWorkflow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Workflow files (*.json)|*.json|All files (*.*)|*.*",
            Title = "Save Workflow"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var workflow = WorkflowCanvas.GetWorkflow();
                var json = JsonConvert.SerializeObject(workflow, Formatting.Indented);
                System.IO.File.WriteAllText(dialog.FileName, json);
                AddLogEntry($"Workflow saved: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error saving workflow: {ex.Message}");
                MessageBox.Show($"Error saving workflow: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void RunWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (_workflowEngine == null)
        {
            MessageBox.Show("Workflow engine is not available.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var workflow = WorkflowCanvas.GetWorkflow();
        if (workflow == null || !workflow.Nodes.Any())
        {
            MessageBox.Show("No workflow to execute. Add some nodes first.", "Empty Workflow",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _workflowCts = new CancellationTokenSource();
            AddLogEntry("Starting workflow execution...");

            var result = await _workflowEngine.ExecuteAsync(workflow, ct: _workflowCts.Token);

            if (result.Success)
            {
                AddLogEntry($"Workflow completed successfully in {result.Duration.TotalSeconds:F1}s");
            }
            else
            {
                AddLogEntry($"Workflow failed: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            AddLogEntry("Workflow execution cancelled");
        }
        catch (Exception ex)
        {
            AddLogEntry($"Workflow error: {ex.Message}");
        }
    }

    private void StopWorkflow_Click(object sender, RoutedEventArgs e)
    {
        _workflowCts?.Cancel();
        AddLogEntry("Stopping workflow...");
    }

    private void WorkflowEngine_NodeStateChanged(string nodeId, NodeExecutionState state)
    {
        Dispatcher.Invoke(() =>
        {
            WorkflowCanvas.UpdateNodeState(nodeId, state);
            if (state == NodeExecutionState.Running)
                AddLogEntry($"Executing node: {nodeId}");
        });
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        WorkflowCanvas.ZoomIn();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        WorkflowCanvas.ZoomOut();
    }

    private void ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        WorkflowCanvas.ResetZoom();
    }

    #endregion

    #region Logging

    private void AddLogEntry(string message)
    {
        var entry = LogEntry.Create(message);
        _logEntries.Add(entry);

        // Auto-scroll to bottom
        if (LogScrollViewer != null)
        {
            LogScrollViewer.ScrollToBottom();
        }

        // Limit log entries to 1000
        while (_logEntries.Count > 1000)
        {
            _logEntries.RemoveAt(0);
        }
    }

    /// <summary>
    /// Increment request counter (called from generation pipeline)
    /// </summary>
    public void IncrementRequestCount()
    {
        _requestCount++;
    }

    #endregion
}

/// <summary>
/// Engine status enum
/// </summary>
public enum DiffusersEngineStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Error
}
