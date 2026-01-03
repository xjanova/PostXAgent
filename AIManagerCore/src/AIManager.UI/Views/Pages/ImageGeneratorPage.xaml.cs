using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIManager.Core.Services;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

public partial class ImageGeneratorPage : Page
{
    private readonly ComfyUIService _comfyService;
    private readonly HttpClient _httpClient;
    private CancellationTokenSource? _generateCts;
    private string? _currentOutputPath;
    private bool _isVideoMode;
    private int _completedCount;

    public ObservableCollection<GenerationHistoryItem> History { get; } = new();
    public ObservableCollection<GpuWorkerInfo> Workers { get; } = new();

    public ImageGeneratorPage()
    {
        InitializeComponent();

        _comfyService = new ComfyUIService();
        _comfyService.ProgressChanged += ComfyService_OnProgress;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

        HistoryList.ItemsSource = History;
        WorkersList.ItemsSource = Workers;

        Loaded += async (s, e) => await InitializeAsync();
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
        TxtWorkerCount.Text = Workers.Count.ToString();
        TxtTotalVram.Text = $"{Workers.Sum(w => w.TotalVramGb):F0} GB";
        TxtQueueSize.Text = "0";
        TxtCompleted.Text = _completedCount.ToString();
        NoWorkersPanel.Visibility = Workers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task CheckConnectionAsync()
    {
        // Check GPU Workers first
        await RefreshWorkersAsync();
    }

    private async Task RefreshWorkersAsync()
    {
        // This would connect to actual workers in production
        // For now, just update stats
        UpdatePoolStats();
        await Task.CompletedTask;
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

        // Check if we have workers
        if (Workers.Count == 0)
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

            if (Workers.Count > 0)
            {
                // Use GPU Pool
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
        // Get first available worker
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
            // Handle video response...
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

    private void AddWorker_Click(object sender, RoutedEventArgs e)
    {
        // Show dialog to add worker
        var dialog = new AddWorkerDialog();
        if (dialog.ShowDialog() == true)
        {
            var worker = new GpuWorkerInfo
            {
                Id = Guid.NewGuid().ToString(),
                Name = dialog.WorkerName,
                Url = dialog.WorkerUrl,
                GpuInfo = "Connecting...",
                IsOnline = false,
            };

            Workers.Add(worker);
            UpdatePoolStats();

            // Try to connect
            _ = ConnectToWorkerAsync(worker);
        }
    }

    private async Task ConnectToWorkerAsync(GpuWorkerInfo worker)
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

public class GpuWorkerInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string GpuInfo { get; set; } = "";
    public bool IsOnline { get; set; }
    public double TotalVramGb { get; set; }
    public double FreeVramGb { get; set; }
    public SolidColorBrush StatusColor { get; set; } = new(Color.FromRgb(107, 107, 138));
    public string VramText => $"{FreeVramGb:F0}/{TotalVramGb:F0} GB";
    public double VramPercent => TotalVramGb > 0 ? ((TotalVramGb - FreeVramGb) / TotalVramGb) * 100 : 0;
}
