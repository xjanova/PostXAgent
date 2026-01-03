using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

public partial class ImageGeneratorPage : Page, INotifyPropertyChanged
{
    private readonly ComfyUIService _comfyService;
    private readonly GpuPoolService? _gpuPoolService;
    private readonly ILogger<ImageGeneratorPage>? _logger;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _generateCts;
    private string? _currentOutputPath;
    private bool _isVideoMode;
    private bool _isParallelMode = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<GenerationHistoryItem> History { get; } = new();
    public ObservableCollection<GpuWorkerDisplayInfo> Workers { get; } = new();

    // Pool stats binding properties
    private int _workerCount;
    public int WorkerCount
    {
        get => _workerCount;
        set { _workerCount = value; OnPropertyChanged(); }
    }

    private double _totalVram;
    public double TotalVram
    {
        get => _totalVram;
        set { _totalVram = value; OnPropertyChanged(); }
    }

    private int _queueSize;
    public int QueueSize
    {
        get => _queueSize;
        set { _queueSize = value; OnPropertyChanged(); }
    }

    private int _completedCount;
    public int CompletedCount
    {
        get => _completedCount;
        set { _completedCount = value; OnPropertyChanged(); }
    }

    public ImageGeneratorPage()
    {
        InitializeComponent();
        DataContext = this;

        _comfyService = new ComfyUIService();
        _comfyService.ProgressChanged += ComfyService_OnProgress;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

        // Get services from DI
        try
        {
            var services = App.Services;
            _gpuPoolService = services?.GetService<GpuPoolService>();
            _logger = services?.GetService<ILogger<ImageGeneratorPage>>();

            // Subscribe to GpuPoolService events
            if (_gpuPoolService != null)
            {
                _gpuPoolService.WorkerStatusChanged += GpuPoolService_WorkerStatusChanged;
                _gpuPoolService.TaskCompleted += GpuPoolService_TaskCompleted;
            }
        }
        catch
        {
            // DI not available, will use direct HTTP
        }

        HistoryList.ItemsSource = History;
        WorkersList.ItemsSource = Workers;

        Loaded += async (s, e) => await InitializeAsync();
        Unloaded += (s, e) => Cleanup();
    }

    private void Cleanup()
    {
        if (_gpuPoolService != null)
        {
            _gpuPoolService.WorkerStatusChanged -= GpuPoolService_WorkerStatusChanged;
            _gpuPoolService.TaskCompleted -= GpuPoolService_TaskCompleted;
        }
    }

    private void GpuPoolService_WorkerStatusChanged(object? sender, GpuWorkerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            SyncWorkersFromService();
            UpdatePoolStats();
        });
    }

    private void GpuPoolService_TaskCompleted(object? sender, GpuTaskEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            CompletedCount = _gpuPoolService?.CompletedTaskCount ?? 0;
            UpdatePoolStats();
        });
    }

    private void SyncWorkersFromService()
    {
        if (_gpuPoolService == null) return;

        Workers.Clear();
        foreach (var worker in _gpuPoolService.Workers)
        {
            Workers.Add(new GpuWorkerDisplayInfo
            {
                Id = worker.Id,
                Name = worker.Name,
                Url = worker.Url,
                GpuInfo = worker.GpuName ?? "Unknown GPU",
                IsOnline = worker.IsOnline,
                TotalVramGb = worker.TotalVramGb,
                FreeVramGb = worker.FreeVramGb,
                CurrentModel = worker.CurrentModel,
                IsBusy = worker.IsBusy,
                StatusColor = worker.IsOnline
                    ? new SolidColorBrush(Color.FromRgb(16, 185, 129))
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68))
            });
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private async Task InitializeAsync()
    {
        LoadDefaultModels();
        UpdatePoolStats();
        await CheckConnectionAsync();
    }

    private void LoadDefaultModels()
    {
        CboModel.Items.Clear();
        CboModel.Items.Add(new ComboBoxItem { Content = "SDXL 1.0", Tag = "stabilityai/stable-diffusion-xl-base-1.0" });
        CboModel.Items.Add(new ComboBoxItem { Content = "SDXL Turbo", Tag = "stabilityai/sdxl-turbo" });
        CboModel.Items.Add(new ComboBoxItem { Content = "SD 1.5", Tag = "runwayml/stable-diffusion-v1-5" });
        CboModel.Items.Add(new ComboBoxItem { Content = "FLUX Schnell", Tag = "black-forest-labs/FLUX.1-schnell" });
        CboModel.Items.Add(new ComboBoxItem { Content = "Realistic Vision", Tag = "SG161222/Realistic_Vision_V5.1_noVAE" });
        CboModel.SelectedIndex = 0;
        UpdateModelInfo();
    }

    private void UpdateModelInfo()
    {
        var modelTag = (CboModel.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
        var vramMap = new Dictionary<string, string>
        {
            { "stabilityai/stable-diffusion-xl-base-1.0", "SDXL 1.0 - 8GB VRAM" },
            { "stabilityai/sdxl-turbo", "SDXL Turbo - 8GB VRAM (Fast)" },
            { "runwayml/stable-diffusion-v1-5", "SD 1.5 - 4GB VRAM" },
            { "black-forest-labs/FLUX.1-schnell", "FLUX Schnell - 12GB VRAM" },
            { "SG161222/Realistic_Vision_V5.1_noVAE", "Realistic Vision - 4GB VRAM" },
        };
        TxtModelInfo.Text = vramMap.GetValueOrDefault(modelTag, "Select a model");
    }

    private void UpdatePoolStats()
    {
        if (_gpuPoolService != null)
        {
            WorkerCount = _gpuPoolService.Workers.Count;
            TotalVram = _gpuPoolService.Workers.Sum(w => w.TotalVramGb);
            QueueSize = _gpuPoolService.QueuedTaskCount;
            CompletedCount = _gpuPoolService.CompletedTaskCount;
        }
        else
        {
            WorkerCount = Workers.Count;
            TotalVram = Workers.Sum(w => w.TotalVramGb);
        }

        TxtWorkerCount.Text = WorkerCount.ToString();
        TxtTotalVram.Text = $"{TotalVram:F0} GB";
        TxtQueueSize.Text = QueueSize.ToString();
        TxtCompleted.Text = CompletedCount.ToString();
        NoWorkersPanel.Visibility = WorkerCount == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task CheckConnectionAsync()
    {
        // Check GPU Workers first
        await RefreshWorkersAsync();

        // Load saved workers from storage
        await LoadSavedWorkersAsync();
    }

    private async Task RefreshWorkersAsync()
    {
        if (_gpuPoolService != null)
        {
            await _gpuPoolService.RefreshAllWorkersAsync();
            SyncWorkersFromService();
        }
        UpdatePoolStats();
    }

    private async Task LoadSavedWorkersAsync()
    {
        // Load workers from local storage (JSON file)
        try
        {
            var workersFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PostXAgent", "gpu_workers.json");

            if (File.Exists(workersFile))
            {
                var json = await File.ReadAllTextAsync(workersFile);
                var savedWorkers = JsonSerializer.Deserialize<List<SavedWorkerInfo>>(json);

                if (savedWorkers != null && _gpuPoolService != null)
                {
                    foreach (var saved in savedWorkers)
                    {
                        await _gpuPoolService.AddWorkerAsync(saved.Name, saved.Url);
                    }
                    SyncWorkersFromService();
                    UpdatePoolStats();
                    _logger?.LogInformation("Loaded {Count} saved GPU workers", savedWorkers.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load saved workers");
        }
    }

    private async Task SaveWorkersAsync()
    {
        try
        {
            var workersDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PostXAgent");
            Directory.CreateDirectory(workersDir);

            var workersFile = Path.Combine(workersDir, "gpu_workers.json");
            var workersToSave = (_gpuPoolService?.Workers ?? Workers.Select(w => new Core.Services.GpuWorkerInfo
            {
                Name = w.Name,
                Url = w.Url
            })).Select(w => new SavedWorkerInfo { Name = w.Name, Url = w.Url }).ToList();

            var json = JsonSerializer.Serialize(workersToSave, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(workersFile, json);

            _logger?.LogInformation("Saved {Count} GPU workers", workersToSave.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save workers");
        }
    }

    private void ComfyService_OnProgress(object? sender, GenerationProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            TxtProgressStatus.Text = $"Generating... {e.Percentage:F0}%";
            TxtProgressDetail.Text = $"Step {e.CurrentStep} / {e.TotalSteps}";

            if (e.TotalSteps > 0)
            {
                GenerationProgress.IsIndeterminate = false;
                GenerationProgress.Maximum = e.TotalSteps;
                GenerationProgress.Value = e.CurrentStep;
            }
        });
    }

    private (int width, int height) GetDimensionsFromRatio()
    {
        if (Ratio1_1.IsChecked == true) return (1024, 1024);
        if (Ratio16_9.IsChecked == true) return (1344, 768);
        if (Ratio9_16.IsChecked == true) return (768, 1344);
        if (Ratio4_3.IsChecked == true) return (1152, 896);
        return (1024, 1024);
    }

    private int GetSelectedFps()
    {
        if (Fps8.IsChecked == true) return 8;
        if (Fps12.IsChecked == true) return 12;
        if (Fps24.IsChecked == true) return 24;
        return 8;
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        var prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Check if we have workers (from GpuPoolService or local list)
        var hasOnlineWorkers = _gpuPoolService?.OnlineWorkers.Count > 0 || Workers.Any(w => w.IsOnline);
        if (!hasOnlineWorkers)
        {
            // Try GPU Pool first, fall back to ComfyUI
            if (!await _comfyService.IsAvailableAsync())
            {
                MessageBox.Show("No GPU workers connected and ComfyUI is not available.\n\nPlease add a GPU worker or start ComfyUI.",
                    "No GPU Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Prepare UI
        BtnGenerate.Visibility = Visibility.Collapsed;
        BtnCancel.Visibility = Visibility.Visible;
        ProgressOverlay.Visibility = Visibility.Visible;
        GenerationProgress.IsIndeterminate = true;
        TxtProgressStatus.Text = "Starting...";
        TxtProgressDetail.Text = "";

        _generateCts = new CancellationTokenSource();

        try
        {
            var selectedModel = (CboModel.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "stabilityai/stable-diffusion-xl-base-1.0";
            var negativePrompt = TxtNegativePrompt.Text.Trim();
            var (width, height) = GetDimensionsFromRatio();
            var steps = (int)SliderSteps.Value;
            var cfg = SliderCfg.Value;
            var seed = int.TryParse(TxtSeed.Text, out var s) ? s : -1;

            if (hasOnlineWorkers)
            {
                // Use GPU Pool Service
                await GenerateWithPoolAsync(prompt, negativePrompt, selectedModel, width, height, steps, cfg, seed);
            }
            else
            {
                // Fallback to ComfyUI
                await GenerateWithComfyUIAsync(prompt, negativePrompt, selectedModel, width, height, steps, cfg, seed);
            }

            PlaceholderPanel.Visibility = Visibility.Collapsed;
            BtnSave.Visibility = Visibility.Visible;
            BtnOpenFolder.Visibility = Visibility.Visible;
            _completedCount++;
            UpdatePoolStats();
        }
        catch (OperationCanceledException)
        {
            TxtProgressStatus.Text = "Cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Generation failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnGenerate.Visibility = Visibility.Visible;
            BtnCancel.Visibility = Visibility.Collapsed;
            ProgressOverlay.Visibility = Visibility.Collapsed;
            _generateCts?.Dispose();
            _generateCts = null;
        }
    }

    private async Task GenerateWithPoolAsync(string prompt, string negativePrompt, string modelId,
        int width, int height, int steps, double cfg, int seed)
    {
        // Use GpuPoolService if available
        if (_gpuPoolService != null && _gpuPoolService.OnlineWorkers.Count > 0)
        {
            var workerInfo = _gpuPoolService.OnlineWorkers.First();
            TxtProgressStatus.Text = $"Sending to {workerInfo.Name}...";
            TxtProgressDetail.Text = $"Model: {modelId.Split('/').Last()}";

            var request = new GpuImageRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                ModelId = modelId,
                Width = width,
                Height = height,
                Steps = steps,
                GuidanceScale = cfg,
                Seed = seed,
                BatchSize = 1,
                RequiredVramGb = modelId.Contains("xl") ? 8.0 : 4.0
            };

            GenerationProgress.IsIndeterminate = true;
            TxtProgressStatus.Text = "Generating on GPU Pool...";

            var result = await _gpuPoolService.GenerateImageAsync(request, _generateCts!.Token);

            if (result.Success && result.Images.Count > 0)
            {
                // Decode base64 image
                var imageBytes = Convert.FromBase64String(result.Images[0]);
                var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.png");
                await File.WriteAllBytesAsync(tempPath, imageBytes);

                _currentOutputPath = tempPath;
                ShowImage(tempPath);
                AddToHistory(tempPath, prompt);

                TxtProgressStatus.Text = $"Done! Seed: {result.Seed}";
                TxtProgressDetail.Text = $"Time: {result.GenerationTime:F1}s";
                _logger?.LogInformation("Image generated via GPU Pool in {Time:F1}s", result.GenerationTime);
            }
            else
            {
                throw new Exception(result.Error ?? "Generation failed");
            }
        }
        else
        {
            // Fallback to direct HTTP call
            var worker = Workers.FirstOrDefault(w => w.IsOnline);
            if (worker == null)
                throw new Exception("No online workers available");

            TxtProgressStatus.Text = $"Sending to {worker.Name}...";

            var request = new
            {
                prompt,
                negative_prompt = negativePrompt,
                width,
                height,
                steps,
                guidance_scale = cfg,
                seed,
                model_id = modelId,
                batch_size = 1
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

            if (_isVideoMode)
            {
                var response = await _httpClient.PostAsync($"{worker.Url}/generate/video", content, _generateCts!.Token);
                response.EnsureSuccessStatusCode();

                var result = await JsonSerializer.DeserializeAsync<GenerateVideoResponse>(
                    await response.Content.ReadAsStreamAsync());

                if (result?.result?.video_base64 != null)
                {
                    var videoBytes = Convert.FromBase64String(result.result.video_base64);
                    var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.mp4");
                    await File.WriteAllBytesAsync(tempPath, videoBytes);

                    _currentOutputPath = tempPath;
                    PreviewVideo.Source = new Uri(tempPath);
                    PreviewVideo.Play();
                    PreviewVideo.Visibility = Visibility.Visible;
                    PreviewImage.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                var response = await _httpClient.PostAsync($"{worker.Url}/generate/image", content, _generateCts!.Token);
                response.EnsureSuccessStatusCode();

                var result = await JsonSerializer.DeserializeAsync<GenerateImageResponse>(
                    await response.Content.ReadAsStreamAsync());

                if (result?.result?.images?.Count > 0)
                {
                    // Decode base64 image
                    var imageBytes = Convert.FromBase64String(result.result.images[0]);
                    var tempPath = Path.Combine(Path.GetTempPath(), $"postx_{Guid.NewGuid()}.png");
                    await File.WriteAllBytesAsync(tempPath, imageBytes);

                    _currentOutputPath = tempPath;
                    ShowImage(tempPath);
                    AddToHistory(tempPath, prompt);
                }
            }
        }
    }

    private async Task GenerateWithComfyUIAsync(string prompt, string negativePrompt, string modelId,
        int width, int height, int steps, double cfg, int seed)
    {
        if (_isVideoMode)
        {
            var frames = (int)SliderFrames.Value;
            var fps = GetSelectedFps();

            var request = new VideoGenerationRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Checkpoint = modelId,
                Frames = frames,
                Fps = fps,
                Width = width,
                Height = height,
                Seed = seed
            };

            var videoResult = await _comfyService.GenerateVideoAsync(request, _generateCts!.Token);

            if (videoResult.Videos.Count > 0)
            {
                _currentOutputPath = videoResult.Videos[0].Url;
                PreviewVideo.Source = new Uri(_currentOutputPath);
                PreviewVideo.Play();
                PreviewVideo.Visibility = Visibility.Visible;
                PreviewImage.Visibility = Visibility.Collapsed;
            }
            else
            {
                throw new Exception("No video generated");
            }
        }
        else
        {
            var request = new ImageGenerationRequest
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                Checkpoint = modelId,
                Width = width,
                Height = height,
                Steps = steps,
                CfgScale = cfg,
                Seed = seed
            };

            var imageResult = await _comfyService.GenerateImageAsync(request, _generateCts!.Token);

            if (imageResult.Images.Count > 0)
            {
                _currentOutputPath = imageResult.Images[0].Url;
                ShowImage(_currentOutputPath);
                AddToHistory(_currentOutputPath, prompt);
            }
            else
            {
                throw new Exception("No image generated");
            }
        }
    }

    private void ShowImage(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();

        PreviewImage.Source = bitmap;
        PreviewImage.Visibility = Visibility.Visible;
        PreviewVideo.Visibility = Visibility.Collapsed;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _generateCts?.Cancel();
    }

    private void AddToHistory(string path, string prompt)
    {
        try
        {
            var thumbnail = new BitmapImage();
            thumbnail.BeginInit();
            thumbnail.CacheOption = BitmapCacheOption.OnLoad;
            thumbnail.DecodePixelWidth = 60;
            thumbnail.UriSource = new Uri(path);
            thumbnail.EndInit();

            History.Insert(0, new GenerationHistoryItem
            {
                Path = path,
                Prompt = prompt,
                Thumbnail = thumbnail,
                CreatedAt = DateTime.Now
            });

            while (History.Count > 20)
            {
                History.RemoveAt(History.Count - 1);
            }
        }
        catch
        {
            // Ignore thumbnail errors
        }
    }

    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is GenerationHistoryItem item)
        {
            if (File.Exists(item.Path))
            {
                ShowImage(item.Path);
                _currentOutputPath = item.Path;
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentOutputPath) || !File.Exists(_currentOutputPath))
        {
            MessageBox.Show("No image to save.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = _isVideoMode ? "MP4 Video|*.mp4" : "PNG Image|*.png|JPEG Image|*.jpg",
            FileName = Path.GetFileName(_currentOutputPath)
        };

        if (dialog.ShowDialog() == true)
        {
            File.Copy(_currentOutputPath, dialog.FileName, true);
            MessageBox.Show($"Saved to: {dialog.FileName}", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentOutputPath) && File.Exists(_currentOutputPath))
        {
            var folder = Path.GetDirectoryName(_currentOutputPath);
            if (!string.IsNullOrEmpty(folder))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_currentOutputPath}\"");
            }
        }
    }

    private async void RefreshConnection_Click(object sender, RoutedEventArgs e)
    {
        await RefreshWorkersAsync();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        if (RbVideo == null) return;

        _isVideoMode = RbVideo.IsChecked == true;

        if (ImageSettings != null && VideoSettings != null)
        {
            ImageSettings.Visibility = _isVideoMode ? Visibility.Collapsed : Visibility.Visible;
            VideoSettings.Visibility = _isVideoMode ? Visibility.Visible : Visibility.Collapsed;
        }

        // Update duration text
        if (TxtDuration != null && SliderFrames != null)
        {
            var frames = (int)SliderFrames.Value;
            var fps = GetSelectedFps();
            var duration = frames / (double)fps;
            TxtDuration.Text = $"{duration:F1} seconds";
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtStepsValue != null)
            TxtStepsValue.Text = SliderSteps?.Value.ToString("F0") ?? "30";

        if (TxtCfgValue != null)
            TxtCfgValue.Text = SliderCfg?.Value.ToString("F1") ?? "7.5";

        if (TxtFramesValue != null)
            TxtFramesValue.Text = SliderFrames?.Value.ToString("F0") ?? "16";

        // Update duration
        if (TxtDuration != null && SliderFrames != null)
        {
            var frames = (int)SliderFrames.Value;
            var fps = GetSelectedFps();
            var duration = frames / (double)fps;
            TxtDuration.Text = $"{duration:F1} seconds";
        }
    }

    private async void AddWorker_Click(object sender, RoutedEventArgs e)
    {
        // Show dialog to add worker
        var dialog = new AddWorkerDialog();
        if (dialog.ShowDialog() == true)
        {
            if (_gpuPoolService != null)
            {
                // Use GpuPoolService
                var worker = await _gpuPoolService.AddWorkerAsync(dialog.WorkerName, dialog.WorkerUrl);
                if (worker != null)
                {
                    SyncWorkersFromService();
                    await SaveWorkersAsync();
                    _logger?.LogInformation("Worker added: {Name} at {Url}", dialog.WorkerName, dialog.WorkerUrl);
                }
            }
            else
            {
                // Fallback to local management
                var worker = new GpuWorkerDisplayInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = dialog.WorkerName,
                    Url = dialog.WorkerUrl,
                    GpuInfo = "Connecting...",
                    IsOnline = false,
                };

                Workers.Add(worker);
                await ConnectToWorkerAsync(worker);
                await SaveWorkersAsync();
            }

            UpdatePoolStats();
        }
    }

    private async void RemoveWorker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string workerId)
        {
            if (_gpuPoolService != null)
            {
                _gpuPoolService.RemoveWorker(workerId);
                SyncWorkersFromService();
            }
            else
            {
                var worker = Workers.FirstOrDefault(w => w.Id == workerId);
                if (worker != null)
                {
                    Workers.Remove(worker);
                }
            }

            await SaveWorkersAsync();
            UpdatePoolStats();
        }
    }

    private async Task ConnectToWorkerAsync(GpuWorkerDisplayInfo worker)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{worker.Url}/status");
            if (response.IsSuccessStatusCode)
            {
                var status = await JsonSerializer.DeserializeAsync<WorkerStatusResponse>(
                    await response.Content.ReadAsStreamAsync());

                if (status != null)
                {
                    worker.IsOnline = true;
                    worker.GpuInfo = status.gpus?.FirstOrDefault()?.name ?? "GPU";
                    worker.TotalVramGb = status.total_vram_gb;
                    worker.FreeVramGb = status.free_vram_gb;
                    worker.StatusColor = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                }
            }
        }
        catch
        {
            worker.IsOnline = false;
            worker.GpuInfo = "Offline";
            worker.StatusColor = new SolidColorBrush(Color.FromRgb(239, 68, 68)); // Red
        }

        Dispatcher.Invoke(() =>
        {
            WorkersList.Items.Refresh();
            UpdatePoolStats();
        });
    }

    private void ColabSetup_Click(object sender, RoutedEventArgs e)
    {
        // Open Colab notebook instructions
        var result = MessageBox.Show(
            "To setup a Colab GPU Worker:\n\n" +
            "1. Open the PostX_GPU_Worker.ipynb notebook in Colab\n" +
            "2. Enable GPU Runtime (Runtime > Change runtime type)\n" +
            "3. Run all cells\n" +
            "4. Copy the ngrok URL and add as worker\n\n" +
            "Open Colab now?",
            "Setup Colab Worker",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://colab.research.google.com",
                UseShellExecute = true
            });
        }
    }
}

// Response models
public class GenerateImageResponse
{
    public string? task_id { get; set; }
    public string? status { get; set; }
    public GenerateResult? result { get; set; }
}

public class GenerateVideoResponse
{
    public string? task_id { get; set; }
    public string? status { get; set; }
    public GenerateVideoResult? result { get; set; }
}

public class GenerateVideoResult
{
    public string? video_base64 { get; set; }
    public int frames { get; set; }
    public int fps { get; set; }
    public double generation_time { get; set; }
}

public class GenerateResult
{
    public List<string>? images { get; set; }
    public int seed { get; set; }
    public double generation_time { get; set; }
}

public class WorkerStatusResponse
{
    public string? worker_id { get; set; }
    public string? status { get; set; }
    public int gpu_count { get; set; }
    public double total_vram_gb { get; set; }
    public double free_vram_gb { get; set; }
    public List<GpuInfoResponse>? gpus { get; set; }
}

public class GpuInfoResponse
{
    public int id { get; set; }
    public string? name { get; set; }
    public double memory_total_gb { get; set; }
    public double memory_free_gb { get; set; }
}

public class GenerationHistoryItem
{
    public string Path { get; set; } = "";
    public string Prompt { get; set; } = "";
    public BitmapImage? Thumbnail { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Worker info for persistence (saving/loading)
/// </summary>
public class SavedWorkerInfo
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

/// <summary>
/// Display model for GPU Worker in UI
/// </summary>
public class GpuWorkerDisplayInfo : INotifyPropertyChanged
{
    private bool _isOnline;
    private bool _isBusy;
    private double _freeVramGb;
    private string _gpuInfo = "";
    private SolidColorBrush _statusColor = new(Color.FromRgb(107, 107, 138));

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";

    public string GpuInfo
    {
        get => _gpuInfo;
        set { _gpuInfo = value; OnPropertyChanged(); }
    }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public double TotalVramGb { get; set; }

    public double FreeVramGb
    {
        get => _freeVramGb;
        set { _freeVramGb = value; OnPropertyChanged(); OnPropertyChanged(nameof(VramText)); OnPropertyChanged(nameof(VramPercent)); }
    }

    public string? CurrentModel { get; set; }

    public SolidColorBrush StatusColor
    {
        get => _statusColor;
        set { _statusColor = value; OnPropertyChanged(); }
    }

    public string VramText => $"{FreeVramGb:F0}/{TotalVramGb:F0} GB";
    public double VramPercent => TotalVramGb > 0 ? ((TotalVramGb - FreeVramGb) / TotalVramGb) * 100 : 0;
    public string StatusText => IsBusy ? "Working" : (IsOnline ? "Online" : "Offline");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
