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
using AIManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Visual Pipeline Page for Image & Video Generation
/// Shows the complete workflow: Input → Processor → Distributor → Output
/// </summary>
public partial class GenerationPipelinePage : Page, INotifyPropertyChanged
{
    private readonly GpuPoolService? _gpuPoolService;
    private readonly ComfyUIService _comfyService;
    private readonly ILogger<GenerationPipelinePage>? _logger;
    private readonly DispatcherTimer _statusTimer;
    private readonly ObservableCollection<WorkerDisplayItem> _activeWorkers = new();

    private bool _isVideoMode;
    private bool _isGenerating;
    private CancellationTokenSource? _generateCts;
    private string? _currentOutputPath;
    private int _completedCount;
    private double _totalGenerationTime;

    public event PropertyChangedEventHandler? PropertyChanged;

    public GenerationPipelinePage()
    {
        InitializeComponent();
        DataContext = this;

        _comfyService = new ComfyUIService();
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

        // Status refresh timer
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _statusTimer.Tick += async (s, e) => await RefreshStatusAsync();

        Loaded += async (s, e) =>
        {
            await RefreshStatusAsync();
            _statusTimer.Start();
        };

        Unloaded += (s, e) =>
        {
            _statusTimer.Stop();
            Cleanup();
        };
    }

    private void Cleanup()
    {
        if (_gpuPoolService != null)
        {
            _gpuPoolService.WorkerStatusChanged -= GpuPoolService_WorkerStatusChanged;
            _gpuPoolService.TaskCompleted -= GpuPoolService_TaskCompleted;
        }
        _generateCts?.Cancel();
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #region Status Updates

    private async Task RefreshStatusAsync()
    {
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

            TxtPoolStatus.Text = $"{onlineCount} workers online";
            PoolStatusDot.Fill = new SolidColorBrush(
                onlineCount > 0 ? Color.FromRgb(16, 185, 129) : Color.FromRgb(239, 68, 68));

            TxtOnlineGpus.Text = $"{onlineCount} GPUs";
            TxtTotalVram.Text = $"{totalVram:F0} GB";

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

    #endregion

    #region UI Event Handlers

    private void GenType_Changed(object sender, RoutedEventArgs e)
    {
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
        // Update UI based on selected processor
        if (RbCombined.IsChecked == true)
        {
            TxtPipelineMode.Text = "COMBINED MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
        }
        else if (RbGpuPool.IsChecked == true)
        {
            TxtPipelineMode.Text = "GPU POOL MODE";
            TxtPipelineMode.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        }
        else
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
            isAuto ? Color.FromRgb(16, 185, 129) : Color.FromRgb(139, 92, 246));

        // Disable manual selection when auto mode is on
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
        // Logic to auto-select best processor
        var comfyOnline = ComfyStatusDot.Fill is SolidColorBrush b && b.Color == Color.FromRgb(16, 185, 129);
        var poolOnline = _gpuPoolService?.OnlineWorkers.Count > 0;

        if (poolOnline && comfyOnline)
        {
            // Both available - use combined for video, pool for image
            RbCombined.IsChecked = _isVideoMode;
            RbGpuPool.IsChecked = !_isVideoMode;
        }
        else if (poolOnline)
        {
            RbGpuPool.IsChecked = true;
        }
        else if (comfyOnline)
        {
            RbComfyUI.IsChecked = true;
        }
    }

    private async void RefreshProcessors_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
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

        _isGenerating = true;
        _generateCts = new CancellationTokenSource();
        UpdateGenerateButton(true);

        try
        {
            // Auto-select processor if in auto mode
            if (ToggleAutoMode.IsChecked == true)
            {
                AutoSelectBestProcessor();
            }

            // Get generation settings
            var negativePrompt = TxtNegative.Text.Trim();

            // Determine which processor to use
            ProcessorType processor;
            if (RbCombined.IsChecked == true)
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

            if (_isVideoMode)
            {
                await GenerateVideoAsync(prompt, negativePrompt, processor, strategy);
            }
            else
            {
                await GenerateImageAsync(prompt, negativePrompt, processor, strategy);
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Generation cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Generation failed");
            MessageBox.Show($"Generation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _isGenerating = false;
            UpdateGenerateButton(false);
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

        if (processor == ProcessorType.GpuPool || processor == ProcessorType.Combined)
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

        // For video, prefer ComfyUI with AnimateDiff or use GPU Pool
        if (processor == ProcessorType.ComfyUI || processor == ProcessorType.Combined)
        {
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
            throw new NotSupportedException("Video generation via GPU Pool requires ComfyUI workers with AnimateDiff support");
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
}

#region Models

public enum ProcessorType
{
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
            Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
            Foreground = Brushes.White
        };
        okButton.Click += (s, e) => DialogResult = true;
        buttonPanel.Children.Add(okButton);

        grid.Children.Add(buttonPanel);
        Content = grid;
    }
}

#endregion
