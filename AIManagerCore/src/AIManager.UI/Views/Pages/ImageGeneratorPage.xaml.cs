using System.Collections.ObjectModel;
using System.IO;
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
    private CancellationTokenSource? _generateCts;
    private string? _currentOutputPath;
    private bool _isVideoMode;

    public ObservableCollection<GenerationHistoryItem> History { get; } = new();

    public ImageGeneratorPage()
    {
        InitializeComponent();

        _comfyService = new ComfyUIService();
        _comfyService.ProgressChanged += ComfyService_OnProgress;

        HistoryList.ItemsSource = History;

        Loaded += async (s, e) => await CheckConnectionAsync();
    }

    private async Task CheckConnectionAsync()
    {
        try
        {
            TxtConnectionStatus.Text = "Connecting...";
            ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow

            var isAvailable = await _comfyService.IsAvailableAsync();

            if (isAvailable)
            {
                TxtConnectionStatus.Text = "Connected";
                TxtConnectionUrl.Text = $"ComfyUI: {_comfyService.BaseUrl}";
                ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green

                await LoadModelsAsync();
            }
            else
            {
                TxtConnectionStatus.Text = "Disconnected";
                TxtConnectionUrl.Text = $"ComfyUI not available at {_comfyService.BaseUrl}";
                ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
        }
        catch (Exception ex)
        {
            TxtConnectionStatus.Text = "Error";
            TxtConnectionUrl.Text = ex.Message;
            ConnectionIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        }
    }

    private async Task LoadModelsAsync()
    {
        try
        {
            var models = await _comfyService.GetAvailableModelsAsync();

            CboModel.Items.Clear();

            if (models.Checkpoints.Count > 0)
            {
                foreach (var model in models.Checkpoints)
                {
                    CboModel.Items.Add(new ComboBoxItem { Content = model, Tag = model });
                }
                CboModel.SelectedIndex = 0;
            }
            else
            {
                CboModel.Items.Add(new ComboBoxItem { Content = "No models found", IsEnabled = false });
            }
        }
        catch (Exception ex)
        {
            CboModel.Items.Clear();
            CboModel.Items.Add(new ComboBoxItem { Content = $"Error: {ex.Message}", IsEnabled = false });
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

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        var prompt = TxtPrompt.Text.Trim();
        if (string.IsNullOrEmpty(prompt))
        {
            MessageBox.Show("Please enter a prompt.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Check connection
        if (!await _comfyService.IsAvailableAsync())
        {
            MessageBox.Show("ComfyUI is not connected. Please start ComfyUI first.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
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
            var selectedModel = (CboModel.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
            var negativePrompt = TxtNegativePrompt.Text.Trim();
            var width = int.Parse((CboWidth.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1024");
            var height = int.Parse((CboHeight.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1024");
            var steps = (int)SliderSteps.Value;
            var cfg = SliderCfg.Value;
            var seed = int.TryParse(TxtSeed.Text, out var s) ? s : -1;

            if (_isVideoMode)
            {
                // Video generation
                var frames = (int)SliderFrames.Value;
                var fps = int.Parse((CboFps.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "8");

                var request = new VideoGenerationRequest
                {
                    Prompt = prompt,
                    NegativePrompt = negativePrompt,
                    Checkpoint = selectedModel,
                    Frames = frames,
                    Fps = fps,
                    Width = width,
                    Height = height,
                    Seed = seed
                };

                var videoResult = await _comfyService.GenerateVideoAsync(request, _generateCts.Token);

                // Get first video from result
                if (videoResult.Videos.Count > 0)
                {
                    _currentOutputPath = videoResult.Videos[0].Url;

                    // Show video
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
                // Image generation
                var request = new ImageGenerationRequest
                {
                    Prompt = prompt,
                    NegativePrompt = negativePrompt,
                    Checkpoint = selectedModel,
                    Width = width,
                    Height = height,
                    Steps = steps,
                    CfgScale = cfg,
                    Seed = seed
                };

                var imageResult = await _comfyService.GenerateImageAsync(request, _generateCts.Token);

                // Get first image from result
                if (imageResult.Images.Count > 0)
                {
                    _currentOutputPath = imageResult.Images[0].Url;

                    // Show image
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource = new Uri(_currentOutputPath);
                    bitmap.EndInit();

                    PreviewImage.Source = bitmap;
                    PreviewImage.Visibility = Visibility.Visible;
                    PreviewVideo.Visibility = Visibility.Collapsed;

                    // Add to history
                    AddToHistory(_currentOutputPath, prompt);
                }
                else
                {
                    throw new Exception("No image generated");
                }
            }

            PlaceholderPanel.Visibility = Visibility.Collapsed;
            BtnSave.Visibility = Visibility.Visible;
            BtnOpenFolder.Visibility = Visibility.Visible;
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

            // Keep only last 20
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
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(item.Path);
                bitmap.EndInit();

                PreviewImage.Source = bitmap;
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
        await CheckConnectionAsync();
    }

    private void Mode_Changed(object sender, RoutedEventArgs e)
    {
        // Guard against null during initialization
        if (RbVideo == null) return;

        _isVideoMode = RbVideo.IsChecked == true;

        if (ImageSettings != null && VideoSettings != null)
        {
            ImageSettings.Visibility = _isVideoMode ? Visibility.Collapsed : Visibility.Visible;
            VideoSettings.Visibility = _isVideoMode ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Null check for XAML initialization
        if (CboProvider == null || TxtModelInfo == null) return;

        // TODO: Switch between ComfyUI and Custom Engine
        var provider = (CboProvider.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        if (provider == "custom")
        {
            TxtModelInfo.Text = "Custom engine uses built-in workflow nodes";
        }
        else
        {
            TxtModelInfo.Text = "";
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtStepsValue != null)
            TxtStepsValue.Text = SliderSteps?.Value.ToString("F0") ?? "20";

        if (TxtCfgValue != null)
            TxtCfgValue.Text = SliderCfg?.Value.ToString("F1") ?? "7.0";

        if (TxtFramesValue != null)
            TxtFramesValue.Text = SliderFrames?.Value.ToString("F0") ?? "16";
    }
}

public class GenerationHistoryItem
{
    public string Path { get; set; } = "";
    public string Prompt { get; set; } = "";
    public BitmapImage? Thumbnail { get; set; }
    public DateTime CreatedAt { get; set; }
}
