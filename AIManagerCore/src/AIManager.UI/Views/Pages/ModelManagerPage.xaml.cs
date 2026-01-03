using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIManager.Core.Services;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Pages;

public partial class ModelManagerPage : Page
{
    private readonly HuggingFaceModelService _modelService;
    private CancellationTokenSource? _downloadCts;
    private CancellationTokenSource? _searchCts;

    private ObservableCollection<DownloadedModelViewModel> _downloadedModels = new();
    private ObservableCollection<SearchResultViewModel> _searchResults = new();

    public ModelManagerPage()
    {
        InitializeComponent();

        _modelService = new HuggingFaceModelService(null);
        _modelService.DownloadProgressChanged += OnDownloadProgressChanged;

        Loaded += ModelManagerPage_Loaded;
    }

    private async void ModelManagerPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadDownloadedModelsAsync();
        await UpdateStorageUsageAsync();
        LoadRecommendedModels();
    }

    private async Task LoadDownloadedModelsAsync()
    {
        try
        {
            var models = await _modelService.GetDownloadedModelsAsync();
            _downloadedModels.Clear();

            foreach (var model in models)
            {
                _downloadedModels.Add(new DownloadedModelViewModel
                {
                    ModelId = model.Id,
                    Name = model.Name,
                    TypeName = model.Type.ToString(),
                    TypeIcon = GetTypeIcon(model.Type),
                    TypeColor = GetTypeColor(model.Type),
                    SizeFormatted = FormatSize(model.SizeBytes),
                    SizeBytes = model.SizeBytes
                });
            }

            DownloadedModelsList.ItemsSource = _downloadedModels;
            EmptyDownloadedState.Visibility = _downloadedModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ไม่สามารถโหลดรายการโมเดลได้: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UpdateStorageUsageAsync()
    {
        try
        {
            var usage = await _modelService.GetStorageUsageAsync();
            StorageUsedText.Text = $"{usage.UsedBytes / 1024.0 / 1024.0 / 1024.0:F1} GB used";
            StorageProgressBar.Value = usage.UsagePercent;
        }
        catch
        {
            StorageUsedText.Text = "-- GB used";
        }
    }

    private void LoadRecommendedModels()
    {
        var recommended = _modelService.GetRecommendedModels();

        // Text to Image
        if (recommended.TryGetValue(ModelType.TextToImage, out var t2iModels))
        {
            TextToImageList.Children.Clear();
            foreach (var model in t2iModels)
            {
                TextToImageList.Children.Add(CreateRecommendedCard(model));
            }
        }

        // Text to Video
        if (recommended.TryGetValue(ModelType.TextToVideo, out var t2vModels))
        {
            TextToVideoList.Children.Clear();
            foreach (var model in t2vModels)
            {
                TextToVideoList.Children.Add(CreateRecommendedCard(model));
            }
        }

        // LoRA
        if (recommended.TryGetValue(ModelType.LoRA, out var loraModels))
        {
            LoraList.Children.Clear();
            foreach (var model in loraModels)
            {
                LoraList.Children.Add(CreateRecommendedCard(model));
            }
        }

        // ControlNet
        if (recommended.TryGetValue(ModelType.ControlNet, out var cnModels))
        {
            ControlNetList.Children.Clear();
            foreach (var model in cnModels)
            {
                ControlNetList.Children.Add(CreateRecommendedCard(model));
            }
        }
    }

    private Border CreateRecommendedCard(RecommendedModel model)
    {
        var card = new Border
        {
            Width = 280,
            Margin = new Thickness(0, 0, 16, 16),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16)
        };

        var stack = new StackPanel();

        // Name
        var nameText = new TextBlock
        {
            Text = model.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        stack.Children.Add(nameText);

        // Model ID
        var idText = new TextBlock
        {
            Text = model.Id,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        stack.Children.Add(idText);

        // Description
        var descText = new TextBlock
        {
            Text = model.Description,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        stack.Children.Add(descText);

        // VRAM requirement
        var vramText = new TextBlock
        {
            Text = $"VRAM: {model.RequiredVramGb} GB",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xFF)),
            Margin = new Thickness(0, 8, 0, 0)
        };
        stack.Children.Add(vramText);

        // Download button
        var downloadBtn = new Button
        {
            Content = "ดาวน์โหลด",
            Margin = new Thickness(0, 12, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xFF)),
            Foreground = Brushes.White,
            Tag = model.Id
        };
        downloadBtn.Click += DownloadRecommended_Click;
        stack.Children.Add(downloadBtn);

        card.Child = stack;
        return card;
    }

    #region Tab Navigation

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        // Prevent NullReferenceException during initialization
        if (TabDownloaded == null || DownloadedPanel == null || BrowsePanel == null || RecommendedPanel == null)
            return;

        DownloadedPanel.Visibility = TabDownloaded.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BrowsePanel.Visibility = TabBrowse.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        RecommendedPanel.Visibility = TabRecommended.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GotoBrowse_Click(object sender, RoutedEventArgs e)
    {
        TabBrowse.IsChecked = true;
    }

    #endregion

    #region Downloaded Models

    private void FilterType_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyDownloadedFilter();
    }

    private void SearchDownloaded_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyDownloadedFilter();
    }

    private void ApplyDownloadedFilter()
    {
        // Prevent NullReferenceException during initialization
        if (FilterTypeCombo == null || SearchDownloadedBox == null || DownloadedModelsList == null)
            return;

        var typeFilter = (FilterTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ทั้งหมด";
        var searchText = SearchDownloadedBox.Text?.ToLower() ?? "";

        var filtered = _downloadedModels.Where(m =>
        {
            bool matchesType = typeFilter == "ทั้งหมด" || m.TypeName == typeFilter;
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                m.Name.ToLower().Contains(searchText) ||
                m.ModelId.ToLower().Contains(searchText);
            return matchesType && matchesSearch;
        }).ToList();

        DownloadedModelsList.ItemsSource = filtered;
    }

    private void UseModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            MessageBox.Show($"เลือกใช้โมเดล: {modelId}\n\n" +
                "โมเดลนี้จะถูกใช้ในการสร้างภาพ/วิดีโอครั้งถัดไป",
                "ใช้งานโมเดล", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            var result = MessageBox.Show($"ต้องการลบโมเดล {modelId}?\n\n" +
                "การลบจะไม่สามารถกู้คืนได้",
                "ยืนยันการลบ", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _modelService.DeleteModelAsync(modelId);
                    await LoadDownloadedModelsAsync();
                    await UpdateStorageUsageAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"ไม่สามารถลบโมเดลได้: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    #endregion

    #region Search HuggingFace

    private void SearchHuggingFace_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SearchHuggingFace_Click(sender, e);
        }
    }

    private async void SearchHuggingFace_Click(object sender, RoutedEventArgs e)
    {
        var query = SearchHuggingFaceBox.Text?.Trim();
        if (string.IsNullOrEmpty(query)) return;

        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();

        try
        {
            SearchLoadingState.Visibility = Visibility.Visible;
            SearchResultsList.Visibility = Visibility.Collapsed;

            var results = await _modelService.SearchModelsAsync(query, ct: _searchCts.Token);
            _searchResults.Clear();

            foreach (var model in results)
            {
                _searchResults.Add(new SearchResultViewModel
                {
                    ModelId = model.ModelId,
                    Description = model.PipelineTag ?? "",
                    DownloadsFormatted = FormatNumber((int)model.Downloads),
                    LikesFormatted = FormatNumber(model.Likes)
                });
            }

            SearchResultsList.ItemsSource = _searchResults;
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ค้นหาล้มเหลว: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SearchLoadingState.Visibility = Visibility.Collapsed;
            SearchResultsList.Visibility = Visibility.Visible;
        }
    }

    #endregion

    #region Download

    private async void DownloadModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            await StartDownloadAsync(modelId, ModelType.TextToImage);
        }
    }

    private async void DownloadRecommended_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            // Determine type from recommended models
            var recommended = _modelService.GetRecommendedModels();
            var modelType = ModelType.TextToImage;

            foreach (var kvp in recommended)
            {
                if (kvp.Value.Any(m => m.Id == modelId))
                {
                    modelType = kvp.Key;
                    break;
                }
            }

            await StartDownloadAsync(modelId, modelType);
        }
    }

    private async Task StartDownloadAsync(string modelId, ModelType type)
    {
        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();

        try
        {
            DownloadModelName.Text = $"กำลังดาวน์โหลด: {modelId}";
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "0%";
            DownloadSpeedText.Text = "";
            DownloadProgressOverlay.Visibility = Visibility.Visible;

            var result = await _modelService.DownloadModelAsync(modelId, type, ct: _downloadCts.Token);

            if (result != null)
            {
                MessageBox.Show($"ดาวน์โหลดสำเร็จ: {result.Name}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadDownloadedModelsAsync();
                await UpdateStorageUsageAsync();
            }
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("การดาวน์โหลดถูกยกเลิก", "Cancelled",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ดาวน์โหลดล้มเหลว: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DownloadProgressOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        _downloadCts?.Cancel();
    }

    private void OnDownloadProgressChanged(object? sender, ModelDownloadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var progress = e.Progress;
            DownloadProgressBar.Value = progress.Percentage;
            DownloadProgressText.Text = $"{progress.Percentage:F0}%";
            DownloadSpeedText.Text = $"{progress.Speed:F1} MB/s";
        });
    }

    #endregion

    #region Header Actions

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadDownloadedModelsAsync();
        await UpdateStorageUsageAsync();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Open settings dialog for:
        // - API token
        // - Models directory
        // - Cache settings
        MessageBox.Show("ตั้งค่า Model Manager\n\n" +
            "- API Token: สำหรับดาวน์โหลดโมเดล private\n" +
            "- โฟลเดอร์: เปลี่ยนที่เก็บโมเดล\n" +
            "- Cache: จัดการ cache",
            "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Helpers

    private static string GetTypeIcon(ModelType type) => type switch
    {
        ModelType.TextToImage => "Image",
        ModelType.TextToVideo => "Video",
        ModelType.LoRA => "LayersTriple",
        ModelType.ControlNet => "DrawingBox",
        ModelType.VAE => "Cube",
        _ => "Help"
    };

    private static string GetTypeColor(ModelType type) => type switch
    {
        ModelType.TextToImage => "#7C4DFF",
        ModelType.TextToVideo => "#00BCD4",
        ModelType.LoRA => "#FF9800",
        ModelType.ControlNet => "#4CAF50",
        ModelType.VAE => "#E91E63",
        _ => "#888888"
    };

    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }

    private static string FormatNumber(int number)
    {
        if (number >= 1_000_000)
            return $"{number / 1_000_000.0:F1}M";
        if (number >= 1_000)
            return $"{number / 1_000.0:F1}K";
        return number.ToString();
    }

    #endregion
}

#region ViewModels

public class DownloadedModelViewModel
{
    public string ModelId { get; set; } = "";
    public string Name { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string TypeIcon { get; set; } = "Help";
    public string TypeColor { get; set; } = "#888888";
    public string SizeFormatted { get; set; } = "";
    public long SizeBytes { get; set; }
}

public class SearchResultViewModel
{
    public string ModelId { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadsFormatted { get; set; } = "0";
    public string LikesFormatted { get; set; } = "0";
}

#endregion
