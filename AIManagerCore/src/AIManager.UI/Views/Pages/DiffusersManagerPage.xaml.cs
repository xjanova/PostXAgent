using System.Collections.ObjectModel;
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

public partial class DiffusersManagerPage : Page
{
    private readonly DiffusersEngineManager? _manager;
    private readonly LocalGpuService _gpuService;
    private readonly ComfyUIService _comfyService;
    private readonly WorkflowEngine? _workflowEngine;
    private readonly ObservableCollection<string> _logEntries = new();
    private readonly DispatcherTimer _updateTimer;
    private DateTime? _startTime;
    private int _requestCount;
    private CancellationTokenSource? _workflowCts;

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
        AddLogEntry("Diffusers Manager page loaded");
    }

    private void DiffusersManagerPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        if (_manager != null)
        {
            _manager.StatusChanged -= Manager_StatusChanged;
            _manager.LogMessage -= Manager_LogMessage;
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

    private async Task UpdateStatusAsync()
    {
        var isRunning = _manager?.IsReady ?? false;
        var status = isRunning ? DiffusersEngineStatus.Running : DiffusersEngineStatus.Stopped;
        UpdateStatusUI(status);

        if (isRunning && _startTime == null)
        {
            _startTime = DateTime.Now.AddSeconds(-10);
        }

        // Update loaded model
        var currentModel = _manager?.CurrentModel;
        if (!string.IsNullOrEmpty(currentModel))
        {
            var modelName = currentModel.Split('/').LastOrDefault() ?? currentModel;
            TxtLoadedModel.Text = modelName;
        }
        else
        {
            TxtLoadedModel.Text = "None";
        }

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
                BtnStartStop.IsEnabled = false;
                break;

            case DiffusersEngineStatus.Stopping:
                StatusDot.Fill = YellowBrush;
                TxtStatus.Text = "Stopping...";
                StatusBadge.Background = new SolidColorBrush(Color.FromArgb(0x30, 0xF5, 0x9E, 0x0B));
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
            AddLogEntry("Starting engine...");
            UpdateStatusUI(DiffusersEngineStatus.Starting);

            try
            {
                // First do preflight checks to show detailed status
                AddLogEntry("Running pre-flight checks...");
                var preflightResult = await _manager.Engine.PerformPreflightChecksAsync(autoInstall: false);

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
                var success = await _manager.InitializeAsync();
                if (success)
                {
                    AddLogEntry("Engine started successfully");
                    _startTime = DateTime.Now;
                    UpdateStatusUI(DiffusersEngineStatus.Running);

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
                        "ไม่สามารถเริ่ม Engine ได้\n\n" +
                        "ต้องการไปที่ GPU Setup Wizard เพื่อติดตั้ง Python และ dependencies หรือไม่?",
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
            catch (Exception ex)
            {
                AddLogEntry($"Error starting engine: {ex.Message}");
                UpdateStatusUI(DiffusersEngineStatus.Error);
            }
        }

        BtnStartStop.IsEnabled = true;
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logEntries.Clear();
        AddLogEntry("Log cleared");
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("GenerationPipeline");
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
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";
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
