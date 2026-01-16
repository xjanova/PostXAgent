using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.UI.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Visual Pipeline Page for Image & Video Generation
/// Shows the complete workflow: Input → Processor → Distributor → Output
/// Supports: Diffusers (HuggingFace), ComfyUI, GPU Pool
/// </summary>
public partial class GenerationPipelinePage : Page, INotifyPropertyChanged
{
    private readonly GpuPoolService? _gpuPoolService;
    private readonly LocalGpuService _localGpuService;
    private readonly DiffusersEngineManager _diffusersManager;
    private DiffusersGenerationEngine _diffusersEngine => _diffusersManager.Engine;
    private readonly ComfyUIService _comfyService;
    private readonly HuggingFaceModelService _modelService;
    private readonly AutoSetupService _autoSetupService;
    private readonly ILogger<GenerationPipelinePage>? _logger;
    private readonly DispatcherTimer _statusTimer;
    private readonly DispatcherTimer _vramTimer;
    private readonly ObservableCollection<WorkerDisplayItem> _activeWorkers = new();
    private readonly ObservableCollection<LogItem> _logItems = new();
    private bool _isSettingUpPython;
    private CancellationTokenSource? _setupCts;
    private DispatcherTimer? _setupAnimationTimer;
    private DispatcherTimer? _elapsedTimer;
    private DispatcherTimer? _spinnerTimer;
    private DateTime _generationStartTime;
    private GenerationStep _currentGenerationStep = GenerationStep.Idle;
    private int _animationFrame;

    private bool _isVideoMode;
    private bool _isGenerating;
    private CancellationTokenSource? _generateCts;
    private string? _currentOutputPath;
    private int _completedCount;
    private double _totalGenerationTime;
    private string? _currentModel;
    private GpuInfo? _localGpuInfo;

    // Model loading state
    private bool _isModelLoaded;
    private bool _isLoadingModel;
    private string? _loadedModelId;
    private CancellationTokenSource? _modelLoadCts;

    // Pipeline configuration with validation
    private PipelineConfiguration _pipelineConfig = new();
    private PipelineValidationResult? _lastValidation;

    public event PropertyChangedEventHandler? PropertyChanged;

    public GenerationPipelinePage()
    {
        InitializeComponent();
        DataContext = this;

        // Initialize services
        _modelService = new HuggingFaceModelService();
        _localGpuService = new LocalGpuService();
        _autoSetupService = new AutoSetupService(_localGpuService);
        _diffusersManager = DiffusersEngineManager.Instance; // Use singleton manager
        _comfyService = new ComfyUIService();

        // Subscribe to auto setup events
        _autoSetupService.ProgressChanged += AutoSetupService_ProgressChanged;

        // Subscribe to events from singleton manager (forwarded from engine)
        _diffusersManager.StatusChanged += DiffusersEngine_StatusChanged;
        _diffusersManager.ModelLoadProgressChanged += DiffusersEngine_ModelLoadProgressChanged;
        _diffusersEngine.ProgressChanged += DiffusersEngine_ProgressChanged;
        _diffusersEngine.GpuStatusChanged += DiffusersEngine_GpuStatusChanged;
        _diffusersEngine.SetupProgressChanged += DiffusersEngine_SetupProgressChanged;
        _comfyService.ProgressChanged += ComfyService_ProgressChanged;

        // Get services from DI
        try
        {
            var services = App.Services;
            _gpuPoolService = services?.GetService<GpuPoolService>();
            _logger = services?.GetService<ILogger<GenerationPipelinePage>>();

            if (_gpuPoolService != null)
            {
                _gpuPoolService.WorkerStatusChanged += GpuPoolService_WorkerStatusChanged;
                _gpuPoolService.TaskCompleted += GpuPoolService_TaskCompleted;
            }
        }
        catch
        {
            // DI not available
        }

        ActiveWorkersList.ItemsSource = _activeWorkers;
        LogMessages.ItemsSource = _logItems;

        // Status refresh timer
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _statusTimer.Tick += async (s, e) => await RefreshStatusAsync();

        // VRAM monitoring timer (more frequent)
        _vramTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _vramTimer.Tick += async (s, e) => await UpdateVramStatusAsync();

        // Subscribe to model activation events
        ModelManagerPage.ModelActivated += OnModelActivated;

        // Initialize with previously selected model
        if (!string.IsNullOrEmpty(ModelManagerPage.ActiveModelId))
        {
            _currentModel = ModelManagerPage.ActiveModelId;
        }

        Loaded += async (s, e) =>
        {
            // Reset setup UI in case previous session was interrupted
            // This ensures the overlay is hidden when returning to this page
            if (SetupOverlay.Visibility == Visibility.Visible && !_isSettingUpPython)
            {
                ResetSetupUI();
            }

            // Always sync with the latest active model from ModelManagerPage
            if (!string.IsNullOrEmpty(ModelManagerPage.ActiveModelId))
            {
                _currentModel = ModelManagerPage.ActiveModelId;
            }

            await InitializeGpuAsync();
            await RefreshStatusAsync();
            await CheckPythonStatusAsync();
            UpdateActiveModelDisplay();

            // Update block status indicators based on current state
            UpdateBlockStatusIndicators();

            _statusTimer.Start();
            _vramTimer.Start();

            // Auto-select ComfyUI if available (much faster than Diffusers)
            var comfyAvailable = await _comfyService.IsAvailableAsync();
            if (comfyAvailable)
            {
                Dispatcher.Invoke(() =>
                {
                    // Select ComfyUI as default processor
                    RbComfyUI.IsChecked = true;
                    TxtComfyStatus.Text = "127.0.0.1:8188 - พร้อมใช้งาน (แนะนำ)";
                });
                _logger?.LogInformation("ComfyUI detected and auto-selected as default processor");
            }
            // Note: Model will be loaded when Generate is clicked (not pre-loaded)
            // This avoids the long wait time that makes UI feel stuck
        };

        Unloaded += (s, e) =>
        {
            _statusTimer.Stop();
            _vramTimer.Stop();
            Cleanup();
        };
    }

    private async Task InitializeGpuAsync()
    {
        try
        {
            _localGpuInfo = await _localGpuService.DetectGpuAsync();
            UpdateLocalGpuDisplay();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detect local GPU");
        }
    }

    #region Python Auto Setup

    /// <summary>
    /// Check Python installation status and update UI (fast file-based check)
    /// </summary>
    private async Task CheckPythonStatusAsync()
    {
        try
        {
            // Use fast file-based verification instead of running Python processes
            var verification = QuickVerifyInstallation();
            UpdatePythonStatusUI(verification);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check Python status");
            UpdatePythonStatusUI(null);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Quick file-based verification without running Python processes
    /// Uses AutoSetupService.QuickVerifyInstallation for consistent checking
    /// </summary>
    private VerificationResult QuickVerifyInstallation()
    {
        var result = _autoSetupService.QuickVerifyInstallation();

        // Add GPU name to CUDA device if available
        if (result.HasCuda && _localGpuInfo != null)
        {
            result.CudaDevice = _localGpuInfo.Name;
        }

        return result;
    }

    private void UpdatePythonStatusUI(VerificationResult? verification)
    {
        Dispatcher.Invoke(() =>
        {
            if (verification == null || !verification.HasPython)
            {
                // Not installed
                PythonStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
                PythonStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TxtPythonStatus.Text = "Setup Required";
                PythonStatusBorder.ToolTip = "Click to install Python and AI packages automatically";
            }
            else if (!verification.IsValid)
            {
                // Partial installation
                PythonStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
                PythonStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                TxtPythonStatus.Text = "Incomplete";
                PythonStatusBorder.ToolTip = $"Missing: {string.Join(", ", verification.Errors)}\nClick to complete setup";
            }
            else
            {
                // Fully installed
                var cudaText = verification.HasCuda ? " + CUDA" : "";
                PythonStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x10, 0xB9, 0x81));
                PythonStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                TxtPythonStatus.Text = $"Ready{cudaText}";
                PythonStatusBorder.ToolTip = verification.GetSummary();
            }

            // Also update the other status indicators
            UpdateHeaderStatusIndicators();
        });
    }

    /// <summary>
    /// Update all header status indicators (Model, GPU, VRAM)
    /// </summary>
    private void UpdateHeaderStatusIndicators()
    {
        var greenColor = Color.FromRgb(16, 185, 129);   // #10B981
        var redColor = Color.FromRgb(239, 68, 68);      // #EF4444
        var yellowColor = Color.FromRgb(245, 158, 11);  // #F59E0B
        var grayColor = Color.FromRgb(107, 114, 128);   // #6B7280

        // Sync with ModelManagerPage active model
        if (string.IsNullOrEmpty(_currentModel) && !string.IsNullOrEmpty(ModelManagerPage.ActiveModelId))
        {
            _currentModel = ModelManagerPage.ActiveModelId;
        }

        // Model Status
        if (!string.IsNullOrEmpty(_currentModel))
        {
            var modelName = _currentModel.Split('/').LastOrDefault() ?? _currentModel;
            if (modelName.Length > 15) modelName = modelName.Substring(0, 12) + "...";

            ModelStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x10, 0xB9, 0x81));
            ModelStatusIcon.Foreground = new SolidColorBrush(greenColor);
            TxtModelStatus.Text = modelName;
            ModelStatusBorder.ToolTip = $"Active Model: {_currentModel}\nClick to change model";
        }
        else
        {
            ModelStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xEF, 0x44, 0x44));
            ModelStatusIcon.Foreground = new SolidColorBrush(redColor);
            TxtModelStatus.Text = "ไม่มีโมเดล";
            ModelStatusBorder.ToolTip = "ยังไม่ได้เลือกโมเดล - คลิกเพื่อเลือก";
        }

        // GPU Status
        if (_localGpuInfo?.IsAvailable == true)
        {
            var gpuName = _localGpuInfo.Name;
            if (gpuName.Length > 18) gpuName = gpuName.Substring(0, 15) + "...";

            GpuStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x10, 0xB9, 0x81));
            GpuStatusIcon.Foreground = new SolidColorBrush(greenColor);
            TxtGpuStatus.Text = gpuName;
        }
        else
        {
            GpuStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            GpuStatusIcon.Foreground = new SolidColorBrush(yellowColor);
            TxtGpuStatus.Text = "CPU Only";
        }

        // VRAM Status
        if (_localGpuInfo?.IsAvailable == true && _localGpuInfo.TotalVramGb > 0)
        {
            var usedVram = _localGpuInfo.TotalVramGb - _localGpuInfo.FreeVramGb;
            var usagePercent = _localGpuInfo.UsagePercent;

            VramStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20,
                usagePercent > 90 ? (byte)0xEF : usagePercent > 70 ? (byte)0xF5 : (byte)0x10,
                usagePercent > 90 ? (byte)0x44 : usagePercent > 70 ? (byte)0x9E : (byte)0xB9,
                usagePercent > 90 ? (byte)0x44 : usagePercent > 70 ? (byte)0x0B : (byte)0x81));

            var vramColor = usagePercent > 90 ? redColor : usagePercent > 70 ? yellowColor : greenColor;
            ((Border)VramStatusBorder).Child.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
            if (VramStatusBorder.Child is StackPanel sp && sp.Children.Count >= 1)
            {
                if (sp.Children[0] is MaterialDesignThemes.Wpf.PackIcon icon)
                    icon.Foreground = new SolidColorBrush(vramColor);
            }

            TxtVramStatus.Text = $"{usedVram:F1}/{_localGpuInfo.TotalVramGb:F0} GB";
        }
        else
        {
            VramStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x6B, 0x72, 0x80));
            TxtVramStatus.Text = "-- GB";
        }
    }

    private void ModelStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Navigate to Model Manager page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("ModelManager");
        }
    }

    private void AutoSetupService_ProgressChanged(object? sender, SetupProgressEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateSetupProgress(e));
    }

    private async void PythonStatus_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isSettingUpPython) return;

        // Use fast file-based check instead of running Python processes
        var verification = QuickVerifyInstallation();
        if (verification.IsValid)
        {
            // Already fully installed - show info
            MessageBox.Show(
                verification.GetSummary(),
                "Python Environment",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Ask user to confirm setup
        var result = MessageBox.Show(
            "ต้องการติดตั้ง Python และ AI packages โดยอัตโนมัติหรือไม่?\n\n" +
            "ระบบจะติดตั้ง:\n" +
            "• Python 3.11 (Embedded)\n" +
            "• PyTorch (with CUDA support)\n" +
            "• Diffusers, Transformers, Accelerate\n\n" +
            "ใช้เวลาประมาณ 5-15 นาที ขึ้นอยู่กับความเร็วอินเทอร์เน็ต",
            "Auto Setup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        await StartAutoSetupAsync();
    }

    private async Task StartAutoSetupAsync()
    {
        _isSettingUpPython = true;
        _setupCts = new CancellationTokenSource();
        SetupOverlay.Visibility = Visibility.Visible;

        // Start animation timer for RGB glow effect
        StartSetupAnimation();

        try
        {
            var setupResult = await _autoSetupService.PerformFullSetupAsync(_setupCts.Token);

            if (setupResult.Success)
            {
                MessageBox.Show(
                    "การติดตั้งเสร็จสมบูรณ์!\n\n" +
                    (setupResult.VerificationResult?.GetSummary() ?? "Ready to generate images."),
                    "Setup Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await CheckPythonStatusAsync();
            }
            else
            {
                MessageBox.Show(
                    $"การติดตั้งล้มเหลว:\n{setupResult.Message}",
                    "Setup Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show(
                "การติดตั้งถูกยกเลิก\n\nคุณสามารถเริ่มใหม่ได้โดยคลิกที่ปุ่ม Python Status",
                "Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Auto setup failed");
            MessageBox.Show(
                $"เกิดข้อผิดพลาดระหว่างติดตั้ง:\n{ex.Message}",
                "Setup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            StopSetupAnimation();
            _isSettingUpPython = false;
            _setupCts?.Dispose();
            _setupCts = null;
            SetupOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private async void CancelSetup_Click(object sender, RoutedEventArgs e)
    {
        // Ask for confirmation
        var result = MessageBox.Show(
            "ยกเลิกการติดตั้ง?\n\n" +
            "ไฟล์ที่ดาวน์โหลดไปแล้วจะถูกลบเพื่อคืนพื้นที่\n" +
            "คุณสามารถเริ่มใหม่ได้ทุกเมื่อ",
            "ยืนยันการยกเลิก",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        // Cancel the setup operation
        if (_setupCts != null && !_setupCts.IsCancellationRequested)
        {
            _setupCts.Cancel();
            _logger?.LogInformation("User cancelled setup");
        }

        // Update UI to show cleanup in progress
        SetupStatusText.Text = "กำลังยกเลิกและลบไฟล์...";
        BtnCancelSetup.IsEnabled = false;

        try
        {
            // Cleanup installed files
            if (_autoSetupService != null)
            {
                var progress = new Progress<string>(msg =>
                {
                    Dispatcher.Invoke(() => SetupStatusText.Text = msg);
                });

                var cleanupResult = await _autoSetupService.CleanupInstallationAsync(progress);

                if (cleanupResult.Success)
                {
                    _logger?.LogInformation("Cleanup completed: {Message}", cleanupResult.Message);

                    // Show result
                    MessageBox.Show(
                        $"ยกเลิกการติดตั้งเรียบร้อย\n\n{cleanupResult.Message}",
                        "ยกเลิกสำเร็จ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    _logger?.LogWarning("Cleanup failed: {Message}", cleanupResult.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during cleanup");
        }

        // Reset UI state
        ResetSetupUI();

        // Refresh Python status display
        await CheckPythonStatusAsync();
    }

    /// <summary>
    /// Reset setup UI to initial state
    /// </summary>
    private void ResetSetupUI()
    {
        Dispatcher.Invoke(() =>
        {
            // Stop animation
            StopSetupAnimation();

            // Hide overlay
            SetupOverlay.Visibility = Visibility.Collapsed;

            // Reset progress
            SetupProgressBar.Value = 0;
            SetupProgressPercent.Text = "0%";
            SetupStatusText.Text = "";

            // Reset step indicators
            var grayBrush = new SolidColorBrush(Color.FromRgb(107, 107, 138));
            Step1Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step1Icon.Foreground = grayBrush;
            Step1Status.Text = "Pending";
            Step1Status.Foreground = grayBrush;

            Step2Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step2Icon.Foreground = grayBrush;
            Step2Status.Text = "Pending";
            Step2Status.Foreground = grayBrush;

            Step3Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step3Icon.Foreground = grayBrush;
            Step3Status.Text = "Pending";
            Step3Status.Foreground = grayBrush;

            Step4Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step4Icon.Foreground = grayBrush;
            Step4Status.Text = "Pending";
            Step4Status.Foreground = grayBrush;

            // Re-enable cancel button for next time
            BtnCancelSetup.IsEnabled = true;

            // Reset flags
            _isSettingUpPython = false;

            _logger?.LogInformation("Setup UI reset to initial state");
        });
    }

    private void StartSetupAnimation()
    {
        _animationFrame = 0;
        _setupAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _setupAnimationTimer.Tick += SetupAnimation_Tick;
        _setupAnimationTimer.Start();
    }

    private void StopSetupAnimation()
    {
        _setupAnimationTimer?.Stop();
        _setupAnimationTimer = null;
    }

    private void SetupAnimation_Tick(object? sender, EventArgs e)
    {
        _animationFrame++;

        try
        {
            // Get container width - use a default if not yet rendered
            var containerWidth = ProgressBarContainer.ActualWidth;
            if (containerWidth <= 0) containerWidth = 400; // Default width

            var progressPercent = SetupProgressBar.Value / 100.0;
            var progressWidth = progressPercent * containerWidth;

            // Animate gradient colors (RGB cycling for the fill)
            var offset = (_animationFrame % 100) / 100.0;
            var hue1 = (offset * 360) % 360;
            var hue2 = ((offset + 0.5) * 360) % 360;

            if (SetupProgressBar.Foreground is LinearGradientBrush gradient && gradient.GradientStops.Count >= 3)
            {
                gradient.GradientStops[0].Color = HsvToColor(hue1, 0.7, 0.95);
                gradient.GradientStops[1].Color = HsvToColor(hue2, 0.7, 0.95);
                gradient.GradientStops[2].Color = HsvToColor(hue1, 0.7, 0.95);
            }

            // Running light animation - always visible and moving
            var lightWidth = 60.0;
            var maxX = Math.Max(containerWidth - lightWidth, lightWidth);

            // Continuous left-to-right animation using modulo
            var cycleTime = 40.0; // Complete cycle every 40 frames (2 seconds at 50ms)
            var normalizedPosition = (_animationFrame % cycleTime) / cycleTime;

            // Ping-pong motion (0->1->0)
            var pingPong = normalizedPosition < 0.5
                ? normalizedPosition * 2
                : 2 - normalizedPosition * 2;

            // Move within the progress bar area (or full width if progress is low)
            var moveRange = progressWidth > lightWidth ? progressWidth - lightWidth : maxX;
            var lightX = pingPong * moveRange;

            RunningLightTransform.X = lightX;

            // Pulsing opacity for the light
            RunningLightBorder.Opacity = 0.5 + 0.4 * Math.Sin(_animationFrame * 0.2);

            // Update outer glow border width to match progress
            ProgressGlowBorder.Width = Math.Max(progressWidth, 10);

            // Pulse glow opacity
            ProgressGlowBorder.Opacity = 0.3 + 0.2 * Math.Sin(_animationFrame * 0.1);
        }
        catch { }
    }

    private static Color HsvToColor(double hue, double saturation, double value)
    {
        int hi = (int)(hue / 60) % 6;
        double f = hue / 60 - Math.Floor(hue / 60);

        value *= 255;
        byte v = (byte)value;
        byte p = (byte)(value * (1 - saturation));
        byte q = (byte)(value * (1 - f * saturation));
        byte t = (byte)(value * (1 - (1 - f) * saturation));

        return hi switch
        {
            0 => Color.FromRgb(v, t, p),
            1 => Color.FromRgb(q, v, p),
            2 => Color.FromRgb(p, v, t),
            3 => Color.FromRgb(p, q, v),
            4 => Color.FromRgb(t, p, v),
            _ => Color.FromRgb(v, p, q)
        };
    }

    #endregion

    private void DiffusersEngine_GpuStatusChanged(object? sender, LocalGpuStatusEventArgs e)
    {
        _localGpuInfo = e.GpuInfo;
        Dispatcher.Invoke(UpdateLocalGpuDisplay);
    }

    private void DiffusersEngine_ModelLoadProgressChanged(object? sender, ModelLoadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Update status text with progress percentage
            var modelName = e.ModelId.Split('/').LastOrDefault() ?? e.ModelId;
            if (modelName.Length > 20) modelName = modelName.Substring(0, 17) + "...";

            TxtDiffusersStatus.Text = $"โหลด {modelName}: {e.Progress}%";
            DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow

            // If complete, update display
            if (e.Stage == "Complete")
            {
                _isModelLoaded = true;
                _loadedModelId = e.ModelId;
                _isLoadingModel = false;
                UpdateActiveModelDisplay();
            }
        });
    }

    private void DiffusersEngine_StatusChanged(object? sender, EngineStatusEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Check if model was just loaded (message contains "Loaded:")
            if (e.Status == EngineStatus.Running && e.Message.StartsWith("Loaded:"))
            {
                // Model loaded successfully - update our tracking
                var loadedModelId = _diffusersEngine.CurrentModel;
                if (!string.IsNullOrEmpty(loadedModelId))
                {
                    _isModelLoaded = true;
                    _loadedModelId = loadedModelId;
                    _isLoadingModel = false;
                }
                UpdateActiveModelDisplay();
                return;
            }

            // For Loading status, only update if we're actually loading a model
            if (e.Status == EngineStatus.Loading && _isLoadingModel)
            {
                var modelName = _currentModel?.Split('/').LastOrDefault() ?? "Model";
                TxtDiffusersStatus.Text = $"กำลังโหลดเข้า VRAM: {modelName}...";
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow
                return;
            }

            // For Generating status
            if (e.Status == EngineStatus.Generating)
            {
                TxtDiffusersStatus.Text = e.Message;
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(167, 139, 250)); // Purple
                return;
            }

            // For Error status
            if (e.Status == EngineStatus.Error)
            {
                TxtDiffusersStatus.Text = $"Error: {e.Message}";
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
                return;
            }

            // For other Running status (not model loaded), use our display
            if (e.Status == EngineStatus.Running)
            {
                UpdateActiveModelDisplay();
            }
        });
    }

    private void UpdateLocalGpuDisplay()
    {
        if (_localGpuInfo == null || !_localGpuInfo.IsAvailable)
        {
            TxtLocalGpuName.Text = "No GPU - CPU Only";
            TxtLocalGpuVram.Text = "N/A";
            TxtLocalGpuTemp.Text = "N/A";
            LocalGpuVramBar.Value = 0;
        }
        else
        {
            TxtLocalGpuName.Text = _localGpuInfo.Name;
            TxtLocalGpuVram.Text = $"{_localGpuInfo.FreeVramGb:F1} / {_localGpuInfo.TotalVramGb:F1} GB";
            TxtLocalGpuTemp.Text = _localGpuInfo.Temperature > 0 ? $"{_localGpuInfo.Temperature}°C" : "N/A";
            LocalGpuVramBar.Value = _localGpuInfo.UsagePercent;
            LocalGpuVramBar.Foreground = new SolidColorBrush(
                _localGpuInfo.UsagePercent > 90 ? Color.FromRgb(239, 68, 68) :
                _localGpuInfo.UsagePercent > 70 ? Color.FromRgb(245, 158, 11) :
                Color.FromRgb(16, 185, 129));

            // Update compatible models indicator
            var compatibleModels = _diffusersEngine.GetCompatibleModelTypes().ToList();
            TxtCompatibleModels.Text = string.Join(", ", compatibleModels);
        }

        // Update header status indicators
        UpdateHeaderStatusIndicators();
    }

    private async Task UpdateVramStatusAsync()
    {
        try
        {
            var vram = await _localGpuService.GetVramUsageAsync();

            // Update local GPU info (only mutable properties)
            if (_localGpuInfo != null)
            {
                _localGpuInfo.FreeVramGb = vram.FreeMb / 1024.0;
                _localGpuInfo.TotalVramGb = vram.TotalMb / 1024.0;
                // Note: UsagePercent is calculated from Free/Total, so we don't need to set it
            }

            Dispatcher.Invoke(() =>
            {
                TxtLocalGpuVram.Text = $"{vram.FreeMb / 1024.0:F1} / {vram.TotalMb / 1024.0:F1} GB";
                LocalGpuVramBar.Value = vram.UsagePercent;
                LocalGpuVramBar.Foreground = new SolidColorBrush(
                    vram.UsagePercent > 90 ? Color.FromRgb(239, 68, 68) :
                    vram.UsagePercent > 70 ? Color.FromRgb(245, 158, 11) :
                    Color.FromRgb(16, 185, 129));

                // Update header VRAM indicator
                UpdateHeaderStatusIndicators();
            });
        }
        catch
        {
            // Ignore VRAM update errors
        }
    }

    private void OnModelActivated(object? sender, ModelActivatedEventArgs e)
    {
        _currentModel = e.ModelId;
        _isModelLoaded = false; // Reset - will load when Generate is clicked
        _loadedModelId = null;

        Dispatcher.Invoke(() =>
        {
            UpdateActiveModelDisplay();
            UpdateHeaderStatusIndicators();
            UpdateBlockStatusIndicators();
        });

        // Note: Model will be loaded automatically when Generate is clicked
        // This avoids the long wait time for pre-loading
    }

    private void UpdateActiveModelDisplay()
    {
        if (!string.IsNullOrEmpty(_currentModel))
        {
            var modelName = _currentModel.Split('/').LastOrDefault() ?? _currentModel;

            // Sync with engine state - check if model is actually loaded in VRAM
            var engineLoadedModel = _diffusersEngine.CurrentModel;
            var isActuallyLoaded = !string.IsNullOrEmpty(engineLoadedModel) && engineLoadedModel == _currentModel;

            // Update our tracking state to match engine
            if (isActuallyLoaded && !_isModelLoaded)
            {
                _isModelLoaded = true;
                _loadedModelId = _currentModel;
            }
            else if (!isActuallyLoaded && _isModelLoaded && _loadedModelId == _currentModel)
            {
                _isModelLoaded = false;
            }

            // Show loading or ready status
            if (_isLoadingModel)
            {
                TxtDiffusersStatus.Text = $"กำลังโหลดเข้า VRAM: {modelName}...";
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)); // Yellow
            }
            else if (_isModelLoaded && _loadedModelId == _currentModel)
            {
                TxtDiffusersStatus.Text = $"พร้อมใช้ (ใน VRAM): {modelName}";
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
            }
            else
            {
                // Model selected - will load when Generate is clicked
                TxtDiffusersStatus.Text = $"เลือก: {modelName} (พร้อม Generate)";
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green - ready to use
            }
        }
        else
        {
            TxtDiffusersStatus.Text = "ยังไม่ได้เลือก Model";
            DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray
        }
    }

    /// <summary>
    /// Pre-load model in background when selected
    /// โหลดโมเดลล่วงหน้าเมื่อเลือก เพื่อลดเวลารอตอน Generate
    /// </summary>
    private async Task PreloadModelAsync(string modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return;

        // Cancel previous loading if any
        _modelLoadCts?.Cancel();
        _modelLoadCts = new CancellationTokenSource();
        var ct = _modelLoadCts.Token;

        // Skip if already loaded
        if (_isModelLoaded && _loadedModelId == modelId)
        {
            _logger?.LogInformation("Model already loaded: {ModelId}", modelId);
            return;
        }

        _isLoadingModel = true;
        _isModelLoaded = false;

        Dispatcher.Invoke(() =>
        {
            UpdateActiveModelDisplay();
            UpdateModelLoadingUI(true, "กำลังเตรียม Engine...", 0);
        });

        try
        {
            // Check Python installation first
            var verification = QuickVerifyInstallation();
            if (!verification.IsValid)
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateModelLoadingUI(false, "ต้องติดตั้ง Python ก่อน", 0);
                    TxtDiffusersStatus.Text = "ต้องติดตั้ง Python";
                    DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                });
                return;
            }

            // Start engine if not running (uses singleton manager - fast if already running)
            Dispatcher.Invoke(() => UpdateModelLoadingUI(true, "กำลังเริ่ม AI Engine...", 10));

            if (!await _diffusersManager.EnsureRunningAsync(ct))
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateModelLoadingUI(false, "ไม่สามารถเริ่ม Engine", 0);
                    TxtDiffusersStatus.Text = "Engine Error";
                    DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                });
                _logger?.LogError("Failed to start engine for preload");
                return;
            }

            ct.ThrowIfCancellationRequested();

            // Load model (cached - won't reload if same model)
            Dispatcher.Invoke(() => UpdateModelLoadingUI(true, "กำลังโหลดโมเดล...", 30));

            var modelType = _isVideoMode ? ModelType.TextToVideo : ModelType.TextToImage;
            var loadResult = await _diffusersManager.LoadModelAsync(modelId, modelType, ct);

            if (loadResult.Success)
            {
                _isModelLoaded = true;
                _loadedModelId = modelId;

                Dispatcher.Invoke(() =>
                {
                    UpdateModelLoadingUI(false, "โมเดลพร้อมใช้งาน!", 100);
                    UpdateActiveModelDisplay();

                    // Update header to show green status
                    var modelName = modelId.Split('/').LastOrDefault() ?? modelId;
                    if (modelName.Length > 15) modelName = modelName.Substring(0, 12) + "...";

                    ModelStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0x10, 0xB9, 0x81));
                    ModelStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    TxtModelStatus.Text = $"✓ {modelName}";
                    ModelStatusBorder.ToolTip = $"โมเดลพร้อมใช้: {modelId}\n(Pre-loaded)";
                });

                _logger?.LogInformation("Model pre-loaded successfully: {ModelId}", modelId);
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateModelLoadingUI(false, $"โหลดไม่สำเร็จ: {loadResult.Error}", 0);
                    TxtDiffusersStatus.Text = "โหลดโมเดลไม่สำเร็จ";
                    DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                });
                _logger?.LogWarning("Failed to preload model: {Error}", loadResult.Error);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Model preload cancelled");
            Dispatcher.Invoke(() => UpdateModelLoadingUI(false, "ยกเลิก", 0));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error preloading model");
            Dispatcher.Invoke(() =>
            {
                UpdateModelLoadingUI(false, $"Error: {ex.Message}", 0);
                TxtDiffusersStatus.Text = "Error";
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            });
        }
        finally
        {
            _isLoadingModel = false;
            Dispatcher.Invoke(UpdateActiveModelDisplay);
        }
    }

    /// <summary>
    /// Update model loading UI in header
    /// </summary>
    private void UpdateModelLoadingUI(bool isLoading, string message, int progress)
    {
        if (isLoading)
        {
            // Show loading state in Model Status border
            ModelStatusBorder.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xF5, 0x9E, 0x0B));
            ModelStatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));

            var shortMessage = message.Length > 20 ? message.Substring(0, 17) + "..." : message;
            TxtModelStatus.Text = shortMessage;
            ModelStatusBorder.ToolTip = $"กำลังโหลด: {message}\nProgress: {progress}%";
        }
        else if (progress == 100)
        {
            // Complete - will be updated by UpdateActiveModelDisplay
        }
        else
        {
            // Error or cancelled
            ModelStatusBorder.ToolTip = message;
        }
    }

    private void Cleanup()
    {
        if (_gpuPoolService != null)
        {
            _gpuPoolService.WorkerStatusChanged -= GpuPoolService_WorkerStatusChanged;
            _gpuPoolService.TaskCompleted -= GpuPoolService_TaskCompleted;
        }
        ModelManagerPage.ModelActivated -= OnModelActivated;
        _generateCts?.Cancel();
        _modelLoadCts?.Cancel();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #region Status Updates

    private async Task RefreshStatusAsync()
    {
        // Check Diffusers Engine - use UpdateActiveModelDisplay for proper state tracking
        var downloadedModels = await _modelService.GetDownloadedModelsAsync();
        Dispatcher.Invoke(() =>
        {
            // If no models downloaded at all, show red
            if (downloadedModels.Count == 0)
            {
                DiffusersStatusDot.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TxtDiffusersStatus.Text = "ยังไม่มี Model - คลิกเพื่อดาวน์โหลด";
            }
            else
            {
                // Use UpdateActiveModelDisplay to show proper loading/ready state
                UpdateActiveModelDisplay();
            }
        });

        // Check ComfyUI
        var comfyAvailable = await _comfyService.IsAvailableAsync();
        Dispatcher.Invoke(() =>
        {
            ComfyStatusDot.Fill = new SolidColorBrush(
                comfyAvailable ? Color.FromRgb(16, 185, 129) : Color.FromRgb(239, 68, 68));
            TxtComfyStatus.Text = comfyAvailable ? "127.0.0.1:8188 - Online" : "127.0.0.1:8188 - Offline";
        });

        // Check GPU Pool
        if (_gpuPoolService != null)
        {
            await _gpuPoolService.RefreshAllWorkersAsync();
            UpdatePoolStatus();
        }

        UpdatePipelineStats();
    }

    private void UpdatePoolStatus()
    {
        if (_gpuPoolService == null) return;

        Dispatcher.Invoke(() =>
        {
            var onlineCount = _gpuPoolService.OnlineWorkers.Count;
            var totalVram = _gpuPoolService.OnlineWorkers.Sum(w => w.TotalVramGb);

            TxtPoolStatus.Text = $"{onlineCount} workers online ({totalVram:F0} GB)";
            PoolStatusDot.Fill = new SolidColorBrush(
                onlineCount > 0 ? Color.FromRgb(16, 185, 129) : Color.FromRgb(239, 68, 68));

            // Update combined status
            var comfyOnline = ComfyStatusDot.Fill is SolidColorBrush b && b.Color == Color.FromRgb(16, 185, 129);
            var hasAnyProcessor = comfyOnline || onlineCount > 0;
            CombinedStatusDot.Fill = new SolidColorBrush(
                hasAnyProcessor ? Color.FromRgb(245, 158, 11) : Color.FromRgb(107, 114, 128));
            TxtCombinedStatus.Text = hasAnyProcessor
                ? $"ComfyUI + {onlineCount} GPU Workers"
                : "No processors available";

            // Update active workers list
            _activeWorkers.Clear();
            foreach (var worker in _gpuPoolService.OnlineWorkers)
            {
                _activeWorkers.Add(new WorkerDisplayItem
                {
                    Name = worker.Name,
                    GpuInfo = $"{worker.GpuName} ({worker.TotalVramGb:F0}GB)",
                    StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129))
                });
            }
        });
    }

    private void UpdatePipelineStats()
    {
        Dispatcher.Invoke(() =>
        {
            TxtQueueCount.Text = (_gpuPoolService?.QueuedTaskCount ?? 0).ToString();
            TxtActiveCount.Text = _activeWorkers.Count(w => w.IsBusy).ToString();
            TxtCompletedCount.Text = _completedCount.ToString();

            if (_completedCount > 0 && _totalGenerationTime > 0)
            {
                var avgTime = _totalGenerationTime / _completedCount;
                TxtAvgTime.Text = $"{avgTime:F1}s";
            }
        });
    }

    private void GpuPoolService_WorkerStatusChanged(object? sender, GpuWorkerEventArgs e)
    {
        Dispatcher.Invoke(() => UpdatePoolStatus());
    }

    private void GpuPoolService_TaskCompleted(object? sender, GpuTaskEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.Result.Success)
            {
                _completedCount++;
                _totalGenerationTime += e.Result.GenerationTime;
            }
            UpdatePipelineStats();
        });
    }

    private void ComfyService_ProgressChanged(object? sender, GenerationProgressEventArgs e)
    {
        // Could show progress in UI if needed
    }

    private void DiffusersEngine_ProgressChanged(object? sender, DiffusersProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Update status text with progress
            TxtDiffusersStatus.Text = $"Step {e.Step}/{e.TotalSteps} ({e.Progress:F0}%)";

            // Update log panel if generating
            if (_isGenerating && _currentGenerationStep == GenerationStep.Generating)
            {
                TxtCurrentStep.Text = $"สร้าง Step {e.Step}/{e.TotalSteps} ({e.Progress:F0}%)";
            }
        });
    }

    private void DiffusersEngine_SetupProgressChanged(object? sender, SetupProgressEventArgs e)
    {
        Dispatcher.Invoke(() => UpdateSetupProgress(e));
    }

    private void UpdateSetupProgress(SetupProgressEventArgs e)
    {
        // Show/hide overlay based on phase
        if (e.Phase == SetupPhase.Complete || e.Phase == SetupPhase.Error)
        {
            // Hide overlay after a brief delay to show completion
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                SetupOverlay.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }
        else if (e.Phase != SetupPhase.Starting || e.Percentage > 0)
        {
            SetupOverlay.Visibility = Visibility.Visible;
        }

        // Update progress bar and percentage
        SetupProgressBar.Value = Math.Max(0, e.Percentage);
        SetupProgressPercent.Text = $"{Math.Max(0, e.Percentage)}%";
        SetupStatusText.Text = e.Message;

        // Update phase icon and text
        var (icon, text, color) = e.Phase switch
        {
            SetupPhase.DetectingGpu => (MaterialDesignThemes.Wpf.PackIconKind.Gpu, "Detecting GPU", "#F59E0B"),
            SetupPhase.InstallingPython => (MaterialDesignThemes.Wpf.PackIconKind.Language, "Installing Python", "#C084FC"),
            SetupPhase.InstallingPip => (MaterialDesignThemes.Wpf.PackIconKind.Package, "Installing pip", "#C084FC"),
            SetupPhase.InstallingPyTorch => (MaterialDesignThemes.Wpf.PackIconKind.Fire, "Installing PyTorch", "#EC4899"),
            SetupPhase.InstallingPackages => (MaterialDesignThemes.Wpf.PackIconKind.Puzzle, "Installing AI Packages", "#06B6D4"),
            SetupPhase.Verifying => (MaterialDesignThemes.Wpf.PackIconKind.CheckDecagram, "Verifying Installation", "#10B981"),
            SetupPhase.Complete => (MaterialDesignThemes.Wpf.PackIconKind.Check, "Setup Complete!", "#10B981"),
            SetupPhase.Error => (MaterialDesignThemes.Wpf.PackIconKind.AlertCircle, "Setup Failed", "#EF4444"),
            _ => (MaterialDesignThemes.Wpf.PackIconKind.CloudDownload, "Preparing", "#80FFFFFF")
        };

        SetupPhaseIcon.Kind = icon;
        SetupPhaseText.Text = text;
        SetupPhaseText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        SetupPhaseIcon.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));

        // Update step indicators
        UpdateStepIndicator(e.Phase);
    }

    private void UpdateStepIndicator(SetupPhase phase)
    {
        var greenBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        var purpleBrush = new SolidColorBrush(Color.FromRgb(167, 139, 250));
        var grayBrush = new SolidColorBrush(Color.FromRgb(107, 107, 138));

        // Step 1: Python
        if (phase > SetupPhase.InstallingPip)
        {
            Step1Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            Step1Icon.Foreground = greenBrush;
            Step1Status.Text = "Done";
            Step1Status.Foreground = greenBrush;
        }
        else if (phase == SetupPhase.InstallingPython || phase == SetupPhase.InstallingPip)
        {
            Step1Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.ProgressDownload;
            Step1Icon.Foreground = purpleBrush;
            Step1Status.Text = "Installing...";
            Step1Status.Foreground = purpleBrush;
        }
        else
        {
            Step1Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step1Icon.Foreground = grayBrush;
            Step1Status.Text = "Pending";
            Step1Status.Foreground = grayBrush;
        }

        // Step 2: PyTorch
        if (phase > SetupPhase.InstallingPyTorch)
        {
            Step2Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            Step2Icon.Foreground = greenBrush;
            Step2Status.Text = "Done";
            Step2Status.Foreground = greenBrush;
        }
        else if (phase == SetupPhase.InstallingPyTorch)
        {
            Step2Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.ProgressDownload;
            Step2Icon.Foreground = purpleBrush;
            Step2Status.Text = "Installing...";
            Step2Status.Foreground = purpleBrush;
        }
        else
        {
            Step2Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step2Icon.Foreground = grayBrush;
            Step2Status.Text = "Pending";
            Step2Status.Foreground = grayBrush;
        }

        // Step 3: AI Packages
        if (phase > SetupPhase.InstallingPackages)
        {
            Step3Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            Step3Icon.Foreground = greenBrush;
            Step3Status.Text = "Done";
            Step3Status.Foreground = greenBrush;
        }
        else if (phase == SetupPhase.InstallingPackages)
        {
            Step3Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.ProgressDownload;
            Step3Icon.Foreground = purpleBrush;
            Step3Status.Text = "Installing...";
            Step3Status.Foreground = purpleBrush;
        }
        else
        {
            Step3Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step3Icon.Foreground = grayBrush;
            Step3Status.Text = "Pending";
            Step3Status.Foreground = grayBrush;
        }

        // Step 4: Verification
        if (phase == SetupPhase.Complete)
        {
            Step4Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
            Step4Icon.Foreground = greenBrush;
            Step4Status.Text = "Done";
            Step4Status.Foreground = greenBrush;
        }
        else if (phase == SetupPhase.Verifying)
        {
            Step4Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.ProgressDownload;
            Step4Icon.Foreground = purpleBrush;
            Step4Status.Text = "Verifying...";
            Step4Status.Foreground = purpleBrush;
        }
        else if (phase == SetupPhase.Error)
        {
            Step4Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
            Step4Icon.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
            Step4Status.Text = "Failed";
            Step4Status.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
        }
        else
        {
            Step4Icon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CircleOutline;
            Step4Icon.Foreground = grayBrush;
            Step4Status.Text = "Pending";
            Step4Status.Foreground = grayBrush;
        }
    }

    #endregion

    #region Log Panel Methods

    /// <summary>
    /// Show log panel and start timers
    /// </summary>
    private void ShowLogPanel()
    {
        Dispatcher.Invoke(() =>
        {
            _logItems.Clear();
            LogPanel.Visibility = Visibility.Visible;
            _generationStartTime = DateTime.Now;

            // Start elapsed time timer
            _elapsedTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _elapsedTimer.Tick += ElapsedTimer_Tick;
            _elapsedTimer.Start();

            // Start spinner animation
            _spinnerTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _spinnerTimer.Tick += SpinnerTimer_Tick;
            _spinnerTimer.Start();

            // Reset step indicators
            ResetLogStepIndicators();

            // Reset current step display
            TxtCurrentStep.Text = "เตรียมพร้อม...";
            TxtCurrentStep.Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252)); // #C084FC
            CurrentStepIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Loading;
            CurrentStepIcon.Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            TxtElapsedTime.Text = "0.0s";
        });
    }

    /// <summary>
    /// Hide log panel and stop timers
    /// </summary>
    private void HideLogPanel()
    {
        Dispatcher.Invoke(() =>
        {
            _elapsedTimer?.Stop();
            _elapsedTimer = null;
            _spinnerTimer?.Stop();
            _spinnerTimer = null;

            // Keep log panel visible for a moment to show final status
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                // Don't hide the log panel - let user see the results
            };
            hideTimer.Start();
        });
    }

    private void ElapsedTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _generationStartTime;
        TxtElapsedTime.Text = elapsed.TotalMinutes >= 1
            ? $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}"
            : $"{elapsed.Seconds}.{elapsed.Milliseconds / 100}s";
    }

    private void SpinnerTimer_Tick(object? sender, EventArgs e)
    {
        var currentAngle = SpinnerRotation.Angle;
        SpinnerRotation.Angle = (currentAngle + 15) % 360;
    }

    /// <summary>
    /// Reset all log step indicators to pending state
    /// </summary>
    private void ResetLogStepIndicators()
    {
        var grayBrush = new SolidColorBrush(Color.FromRgb(107, 107, 138)); // #6B6B8A
        var grayBg = new SolidColorBrush(Color.FromRgb(37, 37, 64));       // #252540

        LogStep1.Background = grayBg;
        LogStep1Dot.Fill = grayBrush;
        LogStep2.Background = grayBg;
        LogStep2Dot.Fill = grayBrush;
        LogStep3.Background = grayBg;
        LogStep3Dot.Fill = grayBrush;
        LogStep4.Background = grayBg;
        LogStep4Dot.Fill = grayBrush;
    }

    /// <summary>
    /// Update the current generation step display
    /// </summary>
    private void UpdateGenerationStep(GenerationStep step, string stepText)
    {
        Dispatcher.Invoke(() =>
        {
            _currentGenerationStep = step;
            TxtCurrentStep.Text = stepText;

            var purpleBrush = new SolidColorBrush(Color.FromRgb(192, 132, 252));   // #C084FC
            var greenBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));     // #10B981
            var yellowBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));    // #F59E0B
            var grayBrush = new SolidColorBrush(Color.FromRgb(107, 107, 138));     // #6B6B8A

            var activeBg = new SolidColorBrush(Color.FromRgb(48, 40, 80));         // Active purple bg
            var completeBg = new SolidColorBrush(Color.FromRgb(32, 60, 45));       // Complete green bg
            var grayBg = new SolidColorBrush(Color.FromRgb(37, 37, 64));           // Pending gray bg

            // Update step indicators based on current step
            switch (step)
            {
                case GenerationStep.ValidatingInput:
                    LogStep1.Background = activeBg;
                    LogStep1Dot.Fill = purpleBrush;
                    break;

                case GenerationStep.StartingEngine:
                case GenerationStep.LoadingModel:
                    LogStep1.Background = completeBg;
                    LogStep1Dot.Fill = greenBrush;
                    LogStep2.Background = activeBg;
                    LogStep2Dot.Fill = purpleBrush;
                    break;

                case GenerationStep.Generating:
                    LogStep1.Background = completeBg;
                    LogStep1Dot.Fill = greenBrush;
                    LogStep2.Background = completeBg;
                    LogStep2Dot.Fill = greenBrush;
                    LogStep3.Background = activeBg;
                    LogStep3Dot.Fill = purpleBrush;
                    break;

                case GenerationStep.PostProcessing:
                case GenerationStep.SavingOutput:
                    LogStep1.Background = completeBg;
                    LogStep1Dot.Fill = greenBrush;
                    LogStep2.Background = completeBg;
                    LogStep2Dot.Fill = greenBrush;
                    LogStep3.Background = completeBg;
                    LogStep3Dot.Fill = greenBrush;
                    LogStep4.Background = activeBg;
                    LogStep4Dot.Fill = purpleBrush;
                    break;

                case GenerationStep.Complete:
                    LogStep1.Background = completeBg;
                    LogStep1Dot.Fill = greenBrush;
                    LogStep2.Background = completeBg;
                    LogStep2Dot.Fill = greenBrush;
                    LogStep3.Background = completeBg;
                    LogStep3Dot.Fill = greenBrush;
                    LogStep4.Background = completeBg;
                    LogStep4Dot.Fill = greenBrush;
                    TxtCurrentStep.Text = "เสร็จสิ้น!";
                    TxtCurrentStep.Foreground = greenBrush;
                    CurrentStepIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.CheckCircle;
                    CurrentStepIcon.Foreground = greenBrush;
                    break;

                case GenerationStep.Error:
                    var errorBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // #EF4444
                    TxtCurrentStep.Foreground = errorBrush;
                    CurrentStepIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.AlertCircle;
                    CurrentStepIcon.Foreground = errorBrush;
                    break;
            }
        });
    }

    /// <summary>
    /// Add a log message to the panel
    /// </summary>
    private void AddLog(LogItem log)
    {
        Dispatcher.Invoke(() =>
        {
            _logItems.Add(log);

            // Auto-scroll to bottom
            if (LogScrollViewer != null)
            {
                LogScrollViewer.ScrollToEnd();
            }
        });
    }

    /// <summary>
    /// Add an info log message
    /// </summary>
    private void LogInfo(string message)
    {
        AddLog(LogItem.Info(message));
    }

    /// <summary>
    /// Add a step log message
    /// </summary>
    private void LogStep(string message)
    {
        AddLog(LogItem.Step(message));
    }

    /// <summary>
    /// Add a success log message
    /// </summary>
    private void LogSuccess(string message)
    {
        AddLog(LogItem.Success(message));
    }

    /// <summary>
    /// Add a warning log message
    /// </summary>
    private void LogWarning(string message)
    {
        AddLog(LogItem.Warning(message));
    }

    /// <summary>
    /// Add an error log message
    /// </summary>
    private void LogError(string message)
    {
        AddLog(LogItem.Error(message));
    }

    #endregion

    #region UI Event Handlers

    private void GenType_Changed(object sender, RoutedEventArgs e)
    {
        // Prevent NullReferenceException during initialization
        if (RbVideo == null) return;

        _isVideoMode = RbVideo.IsChecked == true;

        // Update output node appearance
        if (OutputIcon != null)
        {
            OutputIcon.Kind = _isVideoMode
                ? MaterialDesignThemes.Wpf.PackIconKind.Video
                : MaterialDesignThemes.Wpf.PackIconKind.Image;
        }

        if (TxtOutputType != null)
        {
            TxtOutputType.Text = _isVideoMode ? "Video Result" : "Image Result";
        }

        if (PlaceholderIcon != null)
        {
            PlaceholderIcon.Kind = _isVideoMode
                ? MaterialDesignThemes.Wpf.PackIconKind.VideoBox
                : MaterialDesignThemes.Wpf.PackIconKind.ImageArea;
        }
    }

    private void Processor_Changed(object sender, RoutedEventArgs e)
    {
        // Prevent null during initialization
        if (TxtPipelineMode == null) return;

        // Update UI based on selected processor
        if (RbDiffusers?.IsChecked == true)
        {
            TxtPipelineMode.Text = "DIFFUSERS MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
        else if (RbCombined?.IsChecked == true)
        {
            TxtPipelineMode.Text = "COMBINED MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));
        }
        else if (RbGpuPool?.IsChecked == true)
        {
            TxtPipelineMode.Text = "GPU POOL MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else if (RbComfyUI?.IsChecked == true)
        {
            TxtPipelineMode.Text = "COMFYUI MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(6, 182, 212));
        }
    }

    private void ToggleAutoMode_Click(object sender, RoutedEventArgs e)
    {
        var isAuto = ToggleAutoMode.IsChecked == true;
        TxtSelectionMode.Text = isAuto ? "Auto - Best Available" : "Manual Selection";
        TxtSelectionMode.Foreground = new SolidColorBrush(
            isAuto ? Color.FromRgb(16, 185, 129) : Color.FromRgb(167, 139, 250));

        // Disable manual selection when auto mode is on
        RbDiffusers.IsEnabled = !isAuto;
        RbComfyUI.IsEnabled = !isAuto;
        RbGpuPool.IsEnabled = !isAuto;
        RbCombined.IsEnabled = !isAuto;

        if (isAuto)
        {
            TxtPipelineMode.Text = "AUTO MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            AutoSelectBestProcessor();
        }
    }

    private void AutoSelectBestProcessor()
    {
        // Check availability of each processor
        var diffusersAvailable = DiffusersStatusDot.Fill is SolidColorBrush d && d.Color == Color.FromRgb(16, 185, 129);
        var comfyOnline = ComfyStatusDot.Fill is SolidColorBrush b && b.Color == Color.FromRgb(16, 185, 129);
        var poolOnline = _gpuPoolService?.OnlineWorkers.Count > 0;

        // Priority: Diffusers > GPU Pool > ComfyUI > Combined
        if (diffusersAvailable)
        {
            RbDiffusers.IsChecked = true;
        }
        else if (poolOnline)
        {
            RbGpuPool.IsChecked = true;
        }
        else if (comfyOnline)
        {
            RbComfyUI.IsChecked = true;
        }
        else if (poolOnline && comfyOnline)
        {
            RbCombined.IsChecked = true;
        }
    }

    private async void RefreshProcessors_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private void GpuSetup_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to GPU Setup Wizard page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("GpuSetupWizard");
        }
    }

    private void ConfigureDiffusers_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Model Manager page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("ModelManager");
        }
    }

    private void ConfigureComfyUI_Click(object sender, RoutedEventArgs e)
    {
        // Open ComfyUI settings dialog
        var dialog = new ComfyUISettingsDialog(_comfyService.BaseUrl);
        if (dialog.ShowDialog() == true)
        {
            _comfyService.BaseUrl = dialog.ComfyUIUrl;
            _ = RefreshStatusAsync();
        }
    }

    private void ConfigureGpuPool_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Workers page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("Workers");
        }
    }

    private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
    {
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "PostXAgent");

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        Process.Start("explorer.exe", outputDir);
    }

    private void SaveOutput_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentOutputPath) || !File.Exists(_currentOutputPath))
            return;

        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "PostXAgent");
        Directory.CreateDirectory(outputDir);

        var ext = Path.GetExtension(_currentOutputPath);
        var destPath = Path.Combine(outputDir, $"generation_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        File.Copy(_currentOutputPath, destPath, true);

        MessageBox.Show($"Saved to:\n{destPath}", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Generation

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_isGenerating)
        {
            // Cancel
            _generateCts?.Cancel();
            return;
        }

        var prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Update pipeline configuration from UI
        UpdatePipelineConfigFromUI();

        // Validate pipeline before generation
        _lastValidation = _pipelineConfig.Validate();
        UpdateBlockStatusIndicators();

        if (!_lastValidation.IsValid)
        {
            var errorMessage = $"Pipeline configuration is invalid.\n\n" +
                              $"First invalid block: {_lastValidation.FirstInvalidBlock}\n\n" +
                              $"Errors:\n" + string.Join("\n", _lastValidation.Errors.Select(e => $"• {e}"));

            if (_lastValidation.Warnings.Count > 0)
            {
                errorMessage += $"\n\nWarnings:\n" + string.Join("\n", _lastValidation.Warnings.Select(w => $"• {w}"));
            }

            MessageBox.Show(errorMessage, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show warnings if any
        if (_lastValidation.Warnings.Count > 0)
        {
            var warningMessage = "The following warnings were found:\n\n" +
                                string.Join("\n", _lastValidation.Warnings.Select(w => $"• {w}")) +
                                "\n\nContinue anyway?";

            if (MessageBox.Show(warningMessage, "Warnings", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
        }

        _isGenerating = true;
        _generateCts = new CancellationTokenSource();
        UpdateGenerateButton(true);

        // Show log panel and start logging
        ShowLogPanel();
        LogInfo("เริ่มต้น Generation Pipeline");

        try
        {
            // Step 1: Validate Input
            UpdateGenerationStep(GenerationStep.ValidatingInput, "กำลังตรวจสอบข้อมูล...");
            LogStep("ตรวจสอบ Prompt และการตั้งค่า");

            // Auto-select processor if in auto mode
            if (ToggleAutoMode.IsChecked == true)
            {
                AutoSelectBestProcessor();
                LogInfo("Auto-select: เลือก processor ที่ดีที่สุดอัตโนมัติ");
            }

            // Get generation settings
            var negativePrompt = TxtNegative.Text.Trim();

            // Determine which processor to use
            ProcessorType processor;
            if (RbDiffusers.IsChecked == true)
                processor = ProcessorType.Diffusers;
            else if (RbCombined.IsChecked == true)
                processor = ProcessorType.Combined;
            else if (RbGpuPool.IsChecked == true)
                processor = ProcessorType.GpuPool;
            else
                processor = ProcessorType.ComfyUI;

            // Get distribution strategy
            DistributionStrategy strategy;
            if (RbLeastLoad.IsChecked == true)
                strategy = DistributionStrategy.LeastLoaded;
            else if (RbPriority.IsChecked == true)
                strategy = DistributionStrategy.Priority;
            else
                strategy = DistributionStrategy.RoundRobin;

            _logger?.LogInformation("Starting generation: {Mode}, Processor: {Processor}, Strategy: {Strategy}",
                _isVideoMode ? "Video" : "Image", processor, strategy);

            LogSuccess($"Input validated: {(_isVideoMode ? "Video" : "Image")} mode, Processor: {processor}");
            LogInfo($"Prompt: {prompt.Substring(0, Math.Min(50, prompt.Length))}...");

            if (_isVideoMode)
            {
                await GenerateVideoAsync(prompt, negativePrompt, processor, strategy);
            }
            else
            {
                await GenerateImageAsync(prompt, negativePrompt, processor, strategy);
            }

            // Generation complete
            UpdateGenerationStep(GenerationStep.Complete, "เสร็จสิ้น!");
            LogSuccess("Generation เสร็จสมบูรณ์!");
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Generation cancelled");
            UpdateGenerationStep(GenerationStep.Error, "ยกเลิก");
            LogWarning("ยกเลิกการ Generate โดยผู้ใช้");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Generation failed");
            UpdateGenerationStep(GenerationStep.Error, "เกิดข้อผิดพลาด");
            LogError($"Generation failed: {ex.Message}");
            MessageBox.Show($"Generation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isGenerating = false;
            UpdateGenerateButton(false);
            HideLogPanel();
            _generateCts?.Dispose();
            _generateCts = null;
        }
    }

    private void UpdateGenerateButton(bool isGenerating)
    {
        // Find the elements in the button template
        if (BtnGenerate.Template.FindName("GenIcon", BtnGenerate) is MaterialDesignThemes.Wpf.PackIcon icon)
        {
            icon.Kind = isGenerating
                ? MaterialDesignThemes.Wpf.PackIconKind.Stop
                : MaterialDesignThemes.Wpf.PackIconKind.Creation;
        }

        if (BtnGenerate.Template.FindName("GenText", BtnGenerate) is TextBlock text)
        {
            text.Text = isGenerating ? "Cancel" : "Generate";
        }
    }

    private async Task GenerateImageAsync(string prompt, string negativePrompt,
        ProcessorType processor, DistributionStrategy strategy)
    {
        // Show progress
        OutputPlaceholder.Visibility = Visibility.Visible;

        if (processor == ProcessorType.Diffusers)
        {
            // Use Diffusers (HuggingFace models - Recommended)
            await GenerateWithDiffusersAsync(prompt, negativePrompt, isVideo: false);
        }
        else if (processor == ProcessorType.GpuPool || processor == ProcessorType.Combined)
        {
            // Use GPU Pool
            if (_gpuPoolService != null && _gpuPoolService.OnlineWorkers.Count > 0)
            {
                var request = new GpuImageRequest
                {
                    Prompt = prompt,
                    NegativePrompt = negativePrompt,
                    Width = 1024,
                    Height = 1024,
                    Steps = 30,
                    GuidanceScale = 7.5,
                    Seed = -1,
                    BatchSize = 1,
                    RequiredVramGb = 8.0
                };

                var result = await _gpuPoolService.GenerateImageAsync(request, _generateCts!.Token);

                if (result.Success && result.Images.Count > 0)
                {
                    // Decode and display
                    var imageBytes = Convert.FromBase64String(result.Images[0]);
                    var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.png");
                    await File.WriteAllBytesAsync(tempPath, imageBytes);

                    _currentOutputPath = tempPath;
                    ShowOutput(tempPath, false);

                    _completedCount++;
                    _totalGenerationTime += result.GenerationTime;
                    UpdatePipelineStats();
                }
                else
                {
                    throw new Exception(result.Error ?? "Generation failed");
                }
            }
        }
        else
        {
            // Use ComfyUI
            var request = new ImageGenerationRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Width = 1024,
                Height = 1024,
                Steps = 30,
                CfgScale = 7.5
            };

            var result = await _comfyService.GenerateImageAsync(request, _generateCts!.Token);

            if (result.Images.Count > 0)
            {
                var imageBytes = Convert.FromBase64String(result.Images[0].Base64Data);
                var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, imageBytes);

                _currentOutputPath = tempPath;
                ShowOutput(tempPath, false);

                _completedCount++;
                UpdatePipelineStats();
            }
        }
    }

    private async Task GenerateVideoAsync(string prompt, string negativePrompt,
        ProcessorType processor, DistributionStrategy strategy)
    {
        OutputPlaceholder.Visibility = Visibility.Visible;

        if (processor == ProcessorType.Diffusers)
        {
            // Use Diffusers for video (SVD, AnimateDiff)
            await GenerateWithDiffusersAsync(prompt, negativePrompt, isVideo: true);
        }
        else if (processor == ProcessorType.ComfyUI || processor == ProcessorType.Combined)
        {
            // Use ComfyUI with AnimateDiff
            var request = new VideoGenerationRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Width = 512,
                Height = 512,
                Frames = 16,
                Fps = 8,
                Steps = 25,
                CfgScale = 7.0,
                Method = VideoMethod.AnimateDiff
            };

            var result = await _comfyService.GenerateVideoAsync(request, _generateCts!.Token);

            if (result.Videos.Count > 0)
            {
                var videoBytes = Convert.FromBase64String(result.Videos[0].Base64Data);
                var ext = result.Videos[0].Filename.EndsWith(".mp4") ? ".mp4" : ".gif";
                var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}{ext}");
                await File.WriteAllBytesAsync(tempPath, videoBytes);

                _currentOutputPath = tempPath;
                ShowOutput(tempPath, true);

                _completedCount++;
                UpdatePipelineStats();
            }
        }
        else
        {
            // GPU Pool video generation (if supported by workers)
            throw new NotSupportedException("Video generation via GPU Pool requires Diffusers or ComfyUI");
        }
    }

    /// <summary>
    /// Generate image or video using Diffusers engine (HuggingFace models)
    /// </summary>
    private async Task GenerateWithDiffusersAsync(string prompt, string negativePrompt, bool isVideo)
    {
        // Step 2: Starting Engine
        UpdateGenerationStep(GenerationStep.StartingEngine, "กำลังเริ่ม AI Engine...");
        LogStep("เริ่ม Diffusers Engine");

        // Ensure engine is running (uses singleton manager - instant if already running)
        if (!_diffusersManager.IsReady)
        {
            LogInfo("Engine ยังไม่ทำงาน - กำลังเริ่มต้น...");
            var ready = await _diffusersManager.EnsureRunningAsync(_generateCts!.Token);
            if (!ready)
            {
                var errorMessage = "Failed to start AI generation engine.\n\n";
                errorMessage += "💡 วิธีแก้ไข:\n";
                errorMessage += "• คลิกปุ่ม 'GPU Setup' ในส่วน DISTRIBUTOR เพื่อติดตั้งอัตโนมัติ\n";
                errorMessage += "• หรือคลิกปุ่ม 'Auto Install' ในส่วน GPU & Python Status";

                LogError("ไม่สามารถเริ่ม Engine ได้");
                throw new InvalidOperationException(errorMessage);
            }
        }
        else
        {
            LogSuccess("Engine พร้อมทำงาน (Running)");
        }

        LogSuccess("Engine พร้อมทำงาน");

        // Step 3: Loading Model
        UpdateGenerationStep(GenerationStep.LoadingModel, "กำลังโหลด Model...");
        LogStep("โหลด AI Model");

        // Get first available model or use default
        var models = await _modelService.GetDownloadedModelsAsync();
        var modelId = _currentModel;

        // Check if we have models downloaded first
        if (models == null || models.Count == 0)
        {
            throw new InvalidOperationException(
                "ยังไม่มี Model ที่ดาวน์โหลด\n\n" +
                "วิธีแก้ไข:\n" +
                "1. ไปที่หน้า Model Manager\n" +
                "2. ค้นหาและดาวน์โหลด Model ที่ต้องการ\n" +
                "3. Model แนะนำสำหรับเริ่มต้น:\n" +
                "   • SDXL Base (8GB VRAM)\n" +
                "   • Stable Diffusion 1.5 (4GB VRAM)\n\n" +
                "No models available. Please download a model from Model Manager.");
        }

        if (string.IsNullOrEmpty(modelId))
        {
            // Select appropriate model type
            var modelType = isVideo ? ModelType.TextToVideo : ModelType.TextToImage;
            var model = models.FirstOrDefault(m => m.Type == modelType)
                     ?? models.FirstOrDefault();

            if (model == null)
            {
                LogError("ไม่มี Model ที่เหมาะสมกับงานนี้");
                throw new InvalidOperationException(
                    "ไม่มี Model ที่เหมาะสมกับงานนี้\n\n" +
                    $"ต้องการ Model ประเภท: {(isVideo ? "Text-to-Video" : "Text-to-Image")}\n\n" +
                    "กรุณาดาวน์โหลด Model จากหน้า Model Manager");
            }

            modelId = model.Id;
        }

        var modelName = modelId.Split('/').LastOrDefault() ?? modelId;
        LogInfo($"เลือก Model: {modelName}");

        // Check if model is already loaded in manager (cached in VRAM)
        if (_diffusersManager.CurrentModel == modelId)
        {
            LogSuccess($"Model พร้อมใช้งาน (Cached in VRAM): {modelName}");
            _isModelLoaded = true;
            _loadedModelId = modelId;
        }
        else
        {
            // Load model via manager (will cache for future use)
            var loadModelType = isVideo ? ModelType.TextToVideo : ModelType.TextToImage;
            LogInfo("โหลด Model เข้า VRAM...");
            var loadResult = await _diffusersManager.LoadModelAsync(modelId, loadModelType, _generateCts!.Token);

            if (!loadResult.Success)
            {
                var errorMessage = loadResult.Error ?? "Failed to load model";
                if (loadResult.VramCheck != null && loadResult.VramCheck.Recommendations.Count > 0)
                {
                    errorMessage += "\n\nRecommendations:\n- " + string.Join("\n- ", loadResult.VramCheck.Recommendations);
                }
                LogError($"โหลด Model ล้มเหลว: {loadResult.Error}");
                throw new InvalidOperationException(errorMessage);
            }

            // Update preload state
            _isModelLoaded = true;
            _loadedModelId = modelId;
            LogSuccess($"Model พร้อมใช้งาน: {modelName}");
        }

        // Step 4: Generating
        UpdateGenerationStep(GenerationStep.Generating, "กำลังสร้าง...");

        if (isVideo)
        {
            LogStep("เริ่ม Video Generation");
            var request = new DiffusersVideoRequest
            {
                Prompt = prompt,
                NumFrames = 16
            };

            LogInfo($"สร้าง Video: {request.NumFrames} frames");
            var result = await _diffusersManager.GenerateVideoAsync(request, _generateCts!.Token);

            if (result.Success && result.Frames?.Count > 0)
            {
                // Step 5: Saving Output
                UpdateGenerationStep(GenerationStep.SavingOutput, "กำลังบันทึก...");
                LogStep("บันทึกผลลัพธ์");

                // Save frames as video/gif
                var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.gif");
                // For now, save first frame as image
                // Strip data URI prefix if present (e.g., "data:image/png;base64,")
                var frameData = result.Frames[0];
                if (frameData.Contains(","))
                {
                    frameData = frameData.Split(',')[1];
                }
                var firstFrame = Convert.FromBase64String(frameData);
                await File.WriteAllBytesAsync(tempPath, firstFrame);

                _currentOutputPath = tempPath;
                ShowOutput(tempPath, true);

                _completedCount++;
                _totalGenerationTime += result.GenerationTime;
                UpdatePipelineStats();

                LogSuccess($"สร้าง Video สำเร็จ ({result.GenerationTime:F1}s)");
            }
            else
            {
                LogError(result.Error ?? "Video generation failed");
                throw new Exception(result.Error ?? "Video generation failed");
            }
        }
        else
        {
            LogStep("เริ่ม Image Generation");

            // Use settings from _pipelineConfig (Advanced Model Settings)
            var request = new DiffusersImageRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Width = _pipelineConfig.Sampler.Width,
                Height = _pipelineConfig.Sampler.Height,
                Steps = _pipelineConfig.Sampler.Steps,
                GuidanceScale = _pipelineConfig.Sampler.CfgScale,
                Seed = _pipelineConfig.Sampler.Seed,
                Sampler = _pipelineConfig.Sampler.Sampler.ToString(),
                Scheduler = _pipelineConfig.Sampler.Scheduler.ToString()
            };

            LogInfo($"สร้างภาพ: {request.Width}x{request.Height}, {request.Steps} steps, CFG={request.GuidanceScale}");
            var result = await _diffusersManager.GenerateImageAsync(request, _generateCts!.Token);

            if (result.Success && result.Images?.Count > 0)
            {
                // Step 5: Saving Output
                UpdateGenerationStep(GenerationStep.SavingOutput, "กำลังบันทึก...");
                LogStep("บันทึกผลลัพธ์");

                // Decode base64 image and save to temp file
                // Strip data URI prefix if present (e.g., "data:image/png;base64,")
                var base64Data = result.Images[0];
                if (base64Data.Contains(","))
                {
                    base64Data = base64Data.Split(',')[1];
                }
                var imageBytes = Convert.FromBase64String(base64Data);
                var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, imageBytes);

                _currentOutputPath = tempPath;
                ShowOutput(tempPath, false);

                _completedCount++;
                _totalGenerationTime += result.GenerationTime;
                UpdatePipelineStats();

                LogSuccess($"สร้างภาพสำเร็จ ({result.GenerationTime:F1}s)");
            }
            else
            {
                LogError(result.Error ?? "Image generation failed");
                throw new Exception(result.Error ?? "Image generation failed");
            }
        }
    }

    private void ShowOutput(string path, bool isVideo)
    {
        Dispatcher.Invoke(() =>
        {
            OutputPlaceholder.Visibility = Visibility.Collapsed;

            if (isVideo)
            {
                OutputImage.Visibility = Visibility.Collapsed;
                OutputVideo.Visibility = Visibility.Visible;
                OutputVideo.Source = new Uri(path);
                OutputVideo.Play();
            }
            else
            {
                OutputVideo.Visibility = Visibility.Collapsed;
                OutputImage.Visibility = Visibility.Visible;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                OutputImage.Source = bitmap;
            }

            BtnSaveOutput.Visibility = Visibility.Visible;
        });
    }

    #endregion

    #region Pipeline Configuration

    /// <summary>
    /// Update pipeline configuration from UI controls
    /// </summary>
    private void UpdatePipelineConfigFromUI()
    {
        // Input block
        _pipelineConfig.Input.Prompt = TxtPrompt.Text.Trim();
        _pipelineConfig.Input.NegativePrompt = TxtNegative.Text.Trim();
        _pipelineConfig.Input.GenerationType = _isVideoMode ? GenerationType.Video : GenerationType.Image;

        // Model block - ALWAYS sync with ModelManagerPage.ActiveModelId first
        // This fixes the issue where model was selected in ComboBox but not recognized
        if (string.IsNullOrEmpty(_currentModel) && !string.IsNullOrEmpty(ModelManagerPage.ActiveModelId))
        {
            _currentModel = ModelManagerPage.ActiveModelId;
        }

        // Model block - use current model or from advanced settings
        if (!string.IsNullOrEmpty(_currentModel))
        {
            _pipelineConfig.Model.CheckpointId = _currentModel;
        }

        // Sampler defaults (can be overridden by advanced settings)
        if (_pipelineConfig.Sampler.Steps == 0)
        {
            _pipelineConfig.Sampler.Steps = 30;
            _pipelineConfig.Sampler.CfgScale = 7.5;
            _pipelineConfig.Sampler.Width = 1024;
            _pipelineConfig.Sampler.Height = 1024;
        }

        // Processor - mark as available for validation (actual check happens later)
        _pipelineConfig.Processor.IsAvailable = true;
        _pipelineConfig.Processor.StatusMessage = "Ready";

        // Output block
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "PostXAgent");
        _pipelineConfig.Output.OutputDirectory = outputDir;
    }

    /// <summary>
    /// Update visual indicators for block validation status
    /// </summary>
    private void UpdateBlockStatusIndicators()
    {
        var greenColor = Color.FromRgb(16, 185, 129);   // #10B981 - Valid
        var redColor = Color.FromRgb(239, 68, 68);      // #EF4444 - Invalid
        var yellowColor = Color.FromRgb(245, 158, 11);  // #F59E0B - Warning
        var grayColor = Color.FromRgb(107, 114, 128);   // #6B7280 - Disabled

        // Check 1: Is Python/Diffusers installed?
        var pythonVerification = QuickVerifyInstallation();
        var isPythonReady = pythonVerification.IsValid;

        // Check 2: Is there an active model?
        var hasActiveModel = !string.IsNullOrEmpty(_currentModel) ||
                             !string.IsNullOrEmpty(ModelManagerPage.ActiveModelId);

        // Check 3: Is GPU available?
        var hasGpu = _localGpuInfo?.IsAvailable == true;

        if (!isPythonReady)
        {
            // Python not ready - all blocks should be red/gray
            InputStatusDot.Fill = new SolidColorBrush(grayColor);
            DiffusersStatusDot.Fill = new SolidColorBrush(redColor);
            TxtDiffusersStatus.Text = "ต้องติดตั้ง Python ก่อน";
            DistributorStatusDot.Fill = new SolidColorBrush(grayColor);
            OutputStatusDot.Fill = new SolidColorBrush(grayColor);
            return;
        }

        if (!hasActiveModel)
        {
            // No model selected
            InputStatusDot.Fill = new SolidColorBrush(grayColor);
            DiffusersStatusDot.Fill = new SolidColorBrush(redColor);
            TxtDiffusersStatus.Text = "ยังไม่ได้เลือกโมเดล";
            DistributorStatusDot.Fill = new SolidColorBrush(grayColor);
            OutputStatusDot.Fill = new SolidColorBrush(grayColor);
            return;
        }

        // Both Python and Model ready - check validation
        if (_lastValidation == null)
        {
            // No validation yet but we have Python + Model
            InputStatusDot.Fill = new SolidColorBrush(yellowColor);
            DiffusersStatusDot.Fill = new SolidColorBrush(greenColor);
            var modelName = (_currentModel ?? ModelManagerPage.ActiveModelId)?.Split('/').LastOrDefault() ?? "Ready";
            TxtDiffusersStatus.Text = $"Active: {modelName}";
            DistributorStatusDot.Fill = hasGpu ? new SolidColorBrush(greenColor) : new SolidColorBrush(yellowColor);
            OutputStatusDot.Fill = new SolidColorBrush(greenColor);
            return;
        }

        // Determine validity of each block in sequence
        var firstInvalid = _lastValidation.FirstInvalidBlock;
        var inputValid = firstInvalid != "Input";
        var modelValid = inputValid && firstInvalid != "Model";
        var samplerValid = modelValid && firstInvalid != "Sampler";
        var outputValid = samplerValid && firstInvalid != "Output";

        // Update INPUT block status
        if (!inputValid)
        {
            InputStatusDot.Fill = new SolidColorBrush(redColor);
        }
        else if (_lastValidation.Warnings.Any(w => w.Contains("Prompt", StringComparison.OrdinalIgnoreCase)))
        {
            InputStatusDot.Fill = new SolidColorBrush(yellowColor);
        }
        else
        {
            InputStatusDot.Fill = new SolidColorBrush(greenColor);
        }

        // Update MODEL/PROCESSOR block status (Diffusers is primary)
        if (!inputValid)
        {
            // Previous block invalid - disable this block
            DiffusersStatusDot.Fill = new SolidColorBrush(grayColor);
            TxtDiffusersStatus.Text = "กรอก Prompt ก่อน";
        }
        else if (!modelValid)
        {
            DiffusersStatusDot.Fill = new SolidColorBrush(redColor);
            TxtDiffusersStatus.Text = "Model not configured";
        }
        else if (_lastValidation.Warnings.Any(w => w.Contains("Model", StringComparison.OrdinalIgnoreCase) ||
                                                   w.Contains("LoRA", StringComparison.OrdinalIgnoreCase) ||
                                                   w.Contains("VAE", StringComparison.OrdinalIgnoreCase)))
        {
            DiffusersStatusDot.Fill = new SolidColorBrush(yellowColor);
            TxtDiffusersStatus.Text = "Configured with warnings";
        }
        else
        {
            DiffusersStatusDot.Fill = new SolidColorBrush(greenColor);
            var modelName = _pipelineConfig.Model.CheckpointId?.Split('/').LastOrDefault() ?? "Ready";
            TxtDiffusersStatus.Text = $"Active: {modelName}";
        }

        // Update DISTRIBUTOR block status
        if (!modelValid)
        {
            DistributorStatusDot.Fill = new SolidColorBrush(grayColor);
        }
        else if (!samplerValid)
        {
            DistributorStatusDot.Fill = new SolidColorBrush(redColor);
        }
        else
        {
            DistributorStatusDot.Fill = new SolidColorBrush(greenColor);
        }

        // Update OUTPUT block status
        if (!samplerValid)
        {
            OutputStatusDot.Fill = new SolidColorBrush(grayColor);
        }
        else if (!outputValid)
        {
            OutputStatusDot.Fill = new SolidColorBrush(redColor);
        }
        else
        {
            OutputStatusDot.Fill = new SolidColorBrush(greenColor);
        }
    }

    /// <summary>
    /// Open Advanced Model Settings dialog
    /// </summary>
    private void OpenAdvancedModelSettings_Click(object sender, RoutedEventArgs e)
    {
        // Sync current model selection to pipeline config before opening dialog
        if (!string.IsNullOrEmpty(_currentModel))
        {
            _pipelineConfig.Model.CheckpointId = _currentModel;
        }

        var dialog = new AdvancedModelSettingsDialog(_pipelineConfig)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true && dialog.Applied)
        {
            _pipelineConfig = dialog.Configuration;

            // Update UI with new settings
            _currentModel = _pipelineConfig.Model.CheckpointId;
            UpdateActiveModelDisplay();

            // Re-validate and update indicators
            _lastValidation = _pipelineConfig.Validate();
            UpdateBlockStatusIndicators();

            _logger?.LogInformation("Advanced settings applied: Model={Model}, Steps={Steps}, CFG={CFG}",
                _pipelineConfig.Model.CheckpointId,
                _pipelineConfig.Sampler.Steps,
                _pipelineConfig.Sampler.CfgScale);
        }
    }

    /// <summary>
    /// Get pipeline configuration for external use
    /// </summary>
    public PipelineConfiguration GetCurrentConfiguration() => _pipelineConfig;

    #endregion
}

#region Models

public enum ProcessorType
{
    Diffusers,
    ComfyUI,
    GpuPool,
    Combined
}

public enum DistributionStrategy
{
    RoundRobin,
    LeastLoaded,
    Priority
}

public class WorkerDisplayItem : INotifyPropertyChanged
{
    private bool _isBusy;

    public string Name { get; set; } = "";
    public string GpuInfo { get; set; } = "";
    public SolidColorBrush StatusColor { get; set; } = new(Color.FromRgb(107, 114, 128));

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// Log item for displaying generation progress
/// </summary>
public class LogItem
{
    public string Time { get; set; } = "";
    public string Type { get; set; } = "INFO";
    public string Message { get; set; } = "";
    public SolidColorBrush TypeBackground { get; set; } = new(Color.FromRgb(45, 45, 74));
    public SolidColorBrush TypeColor { get; set; } = new(Color.FromRgb(192, 132, 252));

    public static LogItem Info(string message) => new()
    {
        Time = DateTime.Now.ToString("HH:mm:ss"),
        Type = "INFO",
        Message = message,
        TypeBackground = new SolidColorBrush(Color.FromRgb(32, 45, 74)),
        TypeColor = new SolidColorBrush(Color.FromRgb(96, 165, 250))
    };

    public static LogItem Step(string message) => new()
    {
        Time = DateTime.Now.ToString("HH:mm:ss"),
        Type = "STEP",
        Message = message,
        TypeBackground = new SolidColorBrush(Color.FromRgb(48, 45, 74)),
        TypeColor = new SolidColorBrush(Color.FromRgb(192, 132, 252))
    };

    public static LogItem Success(string message) => new()
    {
        Time = DateTime.Now.ToString("HH:mm:ss"),
        Type = "OK",
        Message = message,
        TypeBackground = new SolidColorBrush(Color.FromRgb(32, 74, 45)),
        TypeColor = new SolidColorBrush(Color.FromRgb(16, 185, 129))
    };

    public static LogItem Warning(string message) => new()
    {
        Time = DateTime.Now.ToString("HH:mm:ss"),
        Type = "WARN",
        Message = message,
        TypeBackground = new SolidColorBrush(Color.FromRgb(74, 60, 32)),
        TypeColor = new SolidColorBrush(Color.FromRgb(245, 158, 11))
    };

    public static LogItem Error(string message) => new()
    {
        Time = DateTime.Now.ToString("HH:mm:ss"),
        Type = "ERR",
        Message = message,
        TypeBackground = new SolidColorBrush(Color.FromRgb(74, 32, 32)),
        TypeColor = new SolidColorBrush(Color.FromRgb(239, 68, 68))
    };
}

/// <summary>
/// Generation step enum for tracking progress
/// </summary>
public enum GenerationStep
{
    Idle,
    ValidatingInput,
    StartingEngine,
    LoadingModel,
    Generating,
    PostProcessing,
    SavingOutput,
    Complete,
    Error
}

#endregion

#region Dialogs

/// <summary>
/// Simple dialog for ComfyUI settings
/// </summary>
public class ComfyUISettingsDialog : Window
{
    private readonly TextBox _urlTextBox;

    public string ComfyUIUrl => _urlTextBox.Text;

    public ComfyUISettingsDialog(string currentUrl)
    {
        Title = "ComfyUI Settings";
        Width = 400;
        Height = 180;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(26, 26, 46));

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = "ComfyUI URL:",
            Foreground = Brushes.White,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        _urlTextBox = new TextBox
        {
            Text = currentUrl,
            Background = new SolidColorBrush(Color.FromRgb(45, 45, 74)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(12, 10, 12, 10),
            FontSize = 13
        };
        Grid.SetRow(_urlTextBox, 1);
        grid.Children.Add(_urlTextBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        Grid.SetRow(buttonPanel, 2);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(20, 8, 20, 8),
            Margin = new Thickness(0, 0, 10, 0)
        };
        cancelButton.Click += (s, e) => DialogResult = false;
        buttonPanel.Children.Add(cancelButton);

        var okButton = new Button
        {
            Content = "Save",
            Padding = new Thickness(20, 8, 20, 8),
            Background = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
            Foreground = Brushes.White
        };
        okButton.Click += (s, e) => DialogResult = true;
        buttonPanel.Children.Add(okButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }
}

#endregion
