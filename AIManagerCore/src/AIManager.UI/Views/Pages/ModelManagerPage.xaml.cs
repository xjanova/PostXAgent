using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

    // Static property for sharing active model with other pages
    public static string? ActiveModelId { get; private set; }
    public static ModelType? ActiveModelType { get; private set; }
    public static event EventHandler<ModelActivatedEventArgs>? ModelActivated;

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
        UpdateActiveModelDisplay();
    }

    private void UpdateActiveModelDisplay()
    {
        if (!string.IsNullOrEmpty(ActiveModelId))
        {
            ActiveModelBanner.Visibility = Visibility.Visible;
            ActiveModelName.Text = ActiveModelId.Split('/').LastOrDefault() ?? ActiveModelId;
        }
        else
        {
            ActiveModelBanner.Visibility = Visibility.Collapsed;
        }
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
                    SizeBytes = model.SizeBytes,
                    IsActive = model.Id == ActiveModelId
                });
            }

            DownloadedModelsList.ItemsSource = _downloadedModels;
            ModelCountText.Text = $"{_downloadedModels.Count} models";
            EmptyDownloadedState.Visibility = _downloadedModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load models: {ex.Message}", "Error",
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
            Width = 300,
            Margin = new Thickness(0, 0, 16, 16),
            CornerRadius = new CornerRadius(12)
        };

        // Gradient background
        var gradientBrush = new LinearGradientBrush(
            Color.FromRgb(0x1E, 0x1E, 0x2E),
            Color.FromRgb(0x1A, 0x1A, 0x28),
            45);
        card.Background = gradientBrush;
        card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x48));
        card.BorderThickness = new Thickness(1);

        var mainStack = new StackPanel();

        // Content area
        var contentStack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };

        // Name
        var nameText = new TextBlock
        {
            Text = model.Name,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        contentStack.Children.Add(nameText);

        // Model ID
        var idText = new TextBlock
        {
            Text = model.Id,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x8A)),
            Margin = new Thickness(0, 4, 0, 0)
        };
        contentStack.Children.Add(idText);

        // Description
        var descText = new TextBlock
        {
            Text = model.Description,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        contentStack.Children.Add(descText);

        // VRAM badge
        var vramBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x30, 0x7C, 0x4D, 0xFF)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var vramText = new TextBlock
        {
            Text = $"VRAM: {model.RequiredVramGb} GB",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xFF))
        };
        vramBorder.Child = vramText;
        contentStack.Children.Add(vramBorder);

        mainStack.Children.Add(contentStack);

        // Action bar
        var actionBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1F)),
            CornerRadius = new CornerRadius(0, 0, 12, 12),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var downloadBtn = new Button
        {
            Cursor = Cursors.Hand,
            Tag = model.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        downloadBtn.Click += DownloadRecommended_Click;

        // Custom button template
        var btnBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(0, 10, 0, 10)
        };
        btnBorder.Background = new LinearGradientBrush(
            Color.FromRgb(0x10, 0xB9, 0x81),
            Color.FromRgb(0x05, 0x96, 0x69),
            45);

        var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        btnStack.Children.Add(new PackIcon { Kind = PackIconKind.Download, Width = 18, Height = 18, Foreground = Brushes.White });
        btnStack.Children.Add(new TextBlock
        {
            Text = "Download",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            Margin = new Thickness(8, 0, 0, 0)
        });

        btnBorder.Child = btnStack;
        downloadBtn.Content = btnBorder;
        downloadBtn.Template = CreateSimpleButtonTemplate();

        actionBar.Child = downloadBtn;
        mainStack.Children.Add(actionBar);

        card.Child = mainStack;
        return card;
    }

    private ControlTemplate CreateSimpleButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        template.VisualTree = presenter;
        return template;
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
        if (FilterTypeCombo == null || SearchDownloadedBox == null || DownloadedModelsList == null || ModelCountText == null)
            return;

        var typeFilter = (FilterTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Types";
        var searchText = SearchDownloadedBox.Text?.ToLower() ?? "";

        var filtered = _downloadedModels.Where(m =>
        {
            bool matchesType = typeFilter == "All Types" || m.TypeName == typeFilter.Replace(" ", "");
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                m.Name.ToLower().Contains(searchText) ||
                m.ModelId.ToLower().Contains(searchText);
            return matchesType && matchesSearch;
        }).ToList();

        DownloadedModelsList.ItemsSource = filtered;
        ModelCountText.Text = $"{filtered.Count} models";
    }

    private void UseModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            // Find the model to get its type
            var model = _downloadedModels.FirstOrDefault(m => m.ModelId == modelId);
            if (model == null) return;

            // Set as active model
            ActiveModelId = modelId;
            ActiveModelType = Enum.TryParse<ModelType>(model.TypeName, out var type) ? type : ModelType.TextToImage;

            // Update UI
            UpdateActiveModelDisplay();

            // Refresh list to show active badge
            foreach (var m in _downloadedModels)
            {
                m.IsActive = m.ModelId == modelId;
            }
            DownloadedModelsList.Items.Refresh();

            // Notify other pages
            ModelActivated?.Invoke(this, new ModelActivatedEventArgs(modelId, ActiveModelType.Value));

            // Show confirmation
            MessageBox.Show($"Model activated: {model.Name}\n\n" +
                "This model is now selected for image/video generation.\n" +
                "Go to Generation Pipeline to start creating!",
                "Model Activated", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            var result = MessageBox.Show($"Delete model: {modelId}?\n\n" +
                "This action cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _modelService.DeleteModelAsync(modelId);

                    // Clear active model if deleted
                    if (ActiveModelId == modelId)
                    {
                        ActiveModelId = null;
                        ActiveModelType = null;
                        UpdateActiveModelDisplay();
                    }

                    await LoadDownloadedModelsAsync();
                    await UpdateStorageUsageAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete model: {ex.Message}", "Error",
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
            MessageBox.Show($"Search failed: {ex.Message}", "Error",
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
            DownloadModelName.Text = modelId;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "0%";
            DownloadSpeedText.Text = "";
            DownloadProgressOverlay.Visibility = Visibility.Visible;

            var result = await _modelService.DownloadModelAsync(modelId, type, ct: _downloadCts.Token);

            if (result != null)
            {
                MessageBox.Show($"Download complete: {result.Name}", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadDownloadedModelsAsync();
                await UpdateStorageUsageAsync();
            }
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("Download cancelled", "Cancelled",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed: {ex.Message}", "Error",
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
        var dialog = new ModelManagerSettingsDialog(_modelService);
        dialog.Owner = Window.GetWindow(this);
        dialog.ShowDialog();
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
        ModelType.TextToVideo => "#06B6D4",
        ModelType.LoRA => "#F59E0B",
        ModelType.ControlNet => "#10B981",
        ModelType.VAE => "#E91E63",
        _ => "#6B6B8A"
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
    public string TypeColor { get; set; } = "#6B6B8A";
    public string SizeFormatted { get; set; } = "";
    public long SizeBytes { get; set; }
    public bool IsActive { get; set; }
}

public class SearchResultViewModel
{
    public string ModelId { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadsFormatted { get; set; } = "0";
    public string LikesFormatted { get; set; } = "0";
}

public class ModelActivatedEventArgs : EventArgs
{
    public string ModelId { get; }
    public ModelType ModelType { get; }

    public ModelActivatedEventArgs(string modelId, ModelType modelType)
    {
        ModelId = modelId;
        ModelType = modelType;
    }
}

#endregion

#region Settings Dialog

public class ModelManagerSettingsDialog : Window
{
    private readonly HuggingFaceModelService _modelService;
    private readonly TextBox _apiTokenBox;
    private readonly TextBox _modelsPathBox;
    private readonly TextBox _cacheSizeBox;

    public ModelManagerSettingsDialog(HuggingFaceModelService modelService)
    {
        _modelService = modelService;

        Title = "Model Manager Settings";
        Width = 550;
        Height = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x12, 0x1A));
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;

        var mainBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
            CornerRadius = new CornerRadius(16),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x50)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(8)
        };

        // Add drop shadow
        mainBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 20,
            ShadowDepth = 0,
            Color = Colors.Black,
            Opacity = 0.5
        };

        var mainStack = new StackPanel { Margin = new Thickness(28) };

        // Header with close button
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerIcon = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 16, 0)
        };
        headerIcon.Background = new LinearGradientBrush(
            Color.FromRgb(0xF5, 0x9E, 0x0B),
            Color.FromRgb(0xEF, 0x44, 0x44),
            45);
        headerIcon.Child = new PackIcon
        {
            Kind = PackIconKind.Key,
            Width = 24,
            Height = 24,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerIcon, 0);
        headerGrid.Children.Add(headerIcon);

        var headerTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        headerTextStack.Children.Add(new TextBlock
        {
            Text = "API Settings",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        });
        headerTextStack.Children.Add(new TextBlock
        {
            Text = "Configure HuggingFace API access",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x8A)),
            Margin = new Thickness(0, 4, 0, 0)
        });
        Grid.SetColumn(headerTextStack, 1);
        headerGrid.Children.Add(headerTextStack);

        // Close button
        var closeBtn = new Button { Cursor = Cursors.Hand, Width = 32, Height = 32 };
        closeBtn.Template = CreateCloseButtonTemplate();
        closeBtn.Click += (s, e) => Close();
        Grid.SetColumn(closeBtn, 2);
        headerGrid.Children.Add(closeBtn);

        mainStack.Children.Add(headerGrid);

        // API Token Section
        mainStack.Children.Add(CreateSectionHeader("HuggingFace API Token", "Required for downloading private/gated models"));

        var tokenBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1F)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 8)
        };
        _apiTokenBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontSize = 14,
            Text = GetSavedApiToken()
        };
        HintAssist.SetHint(_apiTokenBox, "hf_xxxxxxxxxxxxxxxxxxxx");
        tokenBorder.Child = _apiTokenBox;
        mainStack.Children.Add(tokenBorder);

        // Get Token Link
        var getTokenLink = new TextBlock
        {
            FontSize = 12,
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 24)
        };
        getTokenLink.Inlines.Add(new System.Windows.Documents.Run("Get your token: ") { Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x8A)) });
        getTokenLink.Inlines.Add(new System.Windows.Documents.Run("huggingface.co/settings/tokens") { Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xFF)), TextDecorations = TextDecorations.Underline });
        getTokenLink.MouseDown += (s, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://huggingface.co/settings/tokens",
                    UseShellExecute = true
                });
            }
            catch { }
        };
        mainStack.Children.Add(getTokenLink);

        // Models Directory
        mainStack.Children.Add(CreateSectionHeader("Models Directory", "Location where models are stored"));

        var pathGrid = new Grid { Margin = new Thickness(0, 10, 0, 20) };
        pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var pathBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1F)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12)
        };
        _modelsPathBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontSize = 13,
            Text = GetModelsPath(),
            IsReadOnly = true
        };
        pathBorder.Child = _modelsPathBox;
        Grid.SetColumn(pathBorder, 0);
        pathGrid.Children.Add(pathBorder);

        var browseBtn = new Button { Cursor = Cursors.Hand, Margin = new Thickness(10, 0, 0, 0) };
        browseBtn.Template = CreateBrowseButtonTemplate();
        browseBtn.Click += BrowseModelsPath_Click;
        Grid.SetColumn(browseBtn, 1);
        pathGrid.Children.Add(browseBtn);
        mainStack.Children.Add(pathGrid);

        // Cache Settings
        mainStack.Children.Add(CreateSectionHeader("Cache Size Limit (GB)", "Maximum disk space for model cache"));

        var cacheBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1F)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 10, 0, 0),
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _cacheSizeBox = new TextBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            FontSize = 14,
            Text = "50",
            TextAlignment = TextAlignment.Center
        };
        cacheBorder.Child = _cacheSizeBox;
        mainStack.Children.Add(cacheBorder);

        // Buttons
        var buttonStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 32, 0, 0)
        };

        var cancelBtn = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 12, 0) };
        cancelBtn.Template = CreateCancelButtonTemplate();
        cancelBtn.Click += (s, e) => Close();
        buttonStack.Children.Add(cancelBtn);

        var saveBtn = new Button { Cursor = Cursors.Hand };
        saveBtn.Template = CreateSaveButtonTemplate();
        saveBtn.Click += SaveSettings_Click;
        buttonStack.Children.Add(saveBtn);

        mainStack.Children.Add(buttonStack);

        mainBorder.Child = mainStack;
        Content = mainBorder;

        // Enable dragging
        MouseLeftButtonDown += (s, e) =>
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        };
    }

    private StackPanel CreateSectionHeader(string title, string subtitle)
    {
        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });
        stack.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x8A)),
            Margin = new Thickness(0, 2, 0, 0)
        });
        return stack;
    }

    private ControlTemplate CreateCloseButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x25, 0x1A, 0x1A)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.Name = "border";

        var icon = new FrameworkElementFactory(typeof(PackIcon));
        icon.SetValue(PackIcon.KindProperty, PackIconKind.Close);
        icon.SetValue(PackIcon.WidthProperty, 18.0);
        icon.SetValue(PackIcon.HeightProperty, 18.0);
        icon.SetValue(PackIcon.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)));
        icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        border.AppendChild(icon);
        template.VisualTree = border;

        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x3D, 0x20, 0x20)), "border"));
        template.Triggers.Add(trigger);

        return template;
    }

    private ControlTemplate CreateBrowseButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x40)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.PaddingProperty, new Thickness(14, 10, 14, 10));
        border.Name = "border";

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icon = new FrameworkElementFactory(typeof(PackIcon));
        icon.SetValue(PackIcon.KindProperty, PackIconKind.FolderOpen);
        icon.SetValue(PackIcon.WidthProperty, 16.0);
        icon.SetValue(PackIcon.HeightProperty, 16.0);
        icon.SetValue(PackIcon.ForegroundProperty, Brushes.White);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
        stack.AppendChild(icon);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, "Browse");
        text.SetValue(TextBlock.FontSizeProperty, 13.0);
        text.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        stack.AppendChild(text);

        border.AppendChild(stack);
        template.VisualTree = border;

        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x48)), "border"));
        template.Triggers.Add(trigger);

        return template;
    }

    private ControlTemplate CreateCancelButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x40)));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.PaddingProperty, new Thickness(24, 12, 24, 12));
        border.Name = "border";

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, "Cancel");
        text.SetValue(TextBlock.FontSizeProperty, 13.0);
        text.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)));
        border.AppendChild(text);

        template.VisualTree = border;

        var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        trigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x48)), "border"));
        template.Triggers.Add(trigger);

        return template;
    }

    private ControlTemplate CreateSaveButtonTemplate()
    {
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        border.SetValue(Border.PaddingProperty, new Thickness(24, 12, 24, 12));
        border.Name = "border";

        var gradientBrush = new LinearGradientBrush(
            Color.FromRgb(0x10, 0xB9, 0x81),
            Color.FromRgb(0x05, 0x96, 0x69),
            45);
        border.SetValue(Border.BackgroundProperty, gradientBrush);

        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

        var icon = new FrameworkElementFactory(typeof(PackIcon));
        icon.SetValue(PackIcon.KindProperty, PackIconKind.ContentSave);
        icon.SetValue(PackIcon.WidthProperty, 16.0);
        icon.SetValue(PackIcon.HeightProperty, 16.0);
        icon.SetValue(PackIcon.ForegroundProperty, Brushes.White);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));
        stack.AppendChild(icon);

        var text = new FrameworkElementFactory(typeof(TextBlock));
        text.SetValue(TextBlock.TextProperty, "Save Settings");
        text.SetValue(TextBlock.FontSizeProperty, 13.0);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        text.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        stack.AppendChild(text);

        border.AppendChild(stack);
        template.VisualTree = border;

        return template;
    }

    private void BrowseModelsPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Models Directory",
            SelectedPath = _modelsPathBox.Text
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _modelsPathBox.Text = dialog.SelectedPath;
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Save API Token
            var token = _apiTokenBox.Text?.Trim();
            if (!string.IsNullOrEmpty(token))
            {
                Environment.SetEnvironmentVariable("HF_TOKEN", token, EnvironmentVariableTarget.User);
            }

            // Save models path
            var modelsPath = _modelsPathBox.Text?.Trim();
            if (!string.IsNullOrEmpty(modelsPath))
            {
                Environment.SetEnvironmentVariable("HF_HOME", modelsPath, EnvironmentVariableTarget.User);
            }

            MessageBox.Show("Settings saved successfully!\n\nNote: You may need to restart the application for changes to take effect.",
                "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetSavedApiToken()
    {
        return Environment.GetEnvironmentVariable("HF_TOKEN", EnvironmentVariableTarget.User) ?? "";
    }

    private string GetModelsPath()
    {
        var hfHome = Environment.GetEnvironmentVariable("HF_HOME", EnvironmentVariableTarget.User);
        if (!string.IsNullOrEmpty(hfHome)) return hfHome;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cache", "huggingface");
    }
}

#endregion
