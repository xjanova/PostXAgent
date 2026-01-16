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
using System.Windows.Threading;
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

    // Track current/last download for resume functionality
    private string? _currentDownloadModelId;
    private ModelType _currentDownloadModelType;
    private long _downloadedBytesBeforeCancel;
    private bool _downloadWasCancelled;

    // Animation for progress bar
    private DispatcherTimer? _downloadAnimationTimer;
    private int _downloadAnimationFrame;

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

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        // Navigate back to Generation Pipeline
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("GenerationPipeline");
        }
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
                    IsActive = model.Id == ActiveModelId,
                    ThumbnailUrl = model.ThumbnailUrl
                });
            }

            DownloadedModelsList.ItemsSource = _downloadedModels;
            ModelCountText.Text = $"{_downloadedModels.Count} models";
            EmptyDownloadedState.Visibility = _downloadedModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            // Load thumbnails in background for models without saved thumbnails
            _ = LoadThumbnailsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load models: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task LoadThumbnailsAsync()
    {
        var modelsWithoutThumbnails = _downloadedModels.Where(m => !m.HasThumbnail).ToList();

        // Process in parallel with limit
        var semaphore = new SemaphoreSlim(3); // Max 3 concurrent fetches
        var tasks = modelsWithoutThumbnails.Select(async model =>
        {
            await semaphore.WaitAsync();
            try
            {
                var thumbnailUrl = await _modelService.GetModelThumbnailUrlAsync(model.ModelId);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    model.ThumbnailUrl = thumbnailUrl;

                    // Save to metadata for persistence
                    await _modelService.UpdateModelThumbnailAsync(model.ModelId, thumbnailUrl);

                    // Refresh UI on dispatcher thread
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var index = _downloadedModels.IndexOf(model);
                        if (index >= 0)
                        {
                            DownloadedModelsList.Items.Refresh();
                        }
                    });
                }
            }
            catch
            {
                // Ignore thumbnail fetch errors - not critical
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async Task LoadSearchThumbnailsAsync(CancellationToken ct)
    {
        foreach (var model in _searchResults.Where(m => !m.HasThumbnail))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var thumbnailUrl = await _modelService.GetModelThumbnailUrlAsync(model.ModelId, ct);
                if (!string.IsNullOrEmpty(thumbnailUrl))
                {
                    model.ThumbnailUrl = thumbnailUrl;
                    // Refresh UI on dispatcher thread
                    await Dispatcher.InvokeAsync(() => SearchResultsList.Items.Refresh());
                }
            }
            catch
            {
                // Ignore thumbnail fetch errors - not critical
            }
        }
    }

    private async Task UpdateStorageUsageAsync()
    {
        try
        {
            var usage = await _modelService.GetStorageUsageAsync();
            var usedGb = usage.UsedBytes / 1024.0 / 1024.0 / 1024.0;
            var freeGb = usage.AvailableBytes / 1024.0 / 1024.0 / 1024.0;

            StorageUsedText.Text = $"{usedGb:F1} GB used";
            StorageFreeText.Text = $"{freeGb:F1} GB free";
            StorageProgressBar.Value = usage.UsagePercent;

            // Show storage path (truncated if too long)
            var path = _modelService.ModelsDirectory;
            if (path.Length > 35)
            {
                path = "..." + path.Substring(path.Length - 32);
            }
            StoragePathText.Text = path;
        }
        catch
        {
            StorageUsedText.Text = "-- GB used";
            StorageFreeText.Text = "-- GB free";
            StoragePathText.Text = "";
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

        // Load thumbnails with fallback in background
        _ = LoadRecommendedThumbnailsAsync(recommended.Values.SelectMany(m => m).ToList());
    }

    /// <summary>
    /// Load thumbnails for recommended models with fallback to web search
    /// </summary>
    private async Task LoadRecommendedThumbnailsAsync(List<RecommendedModel> models)
    {
        var semaphore = new SemaphoreSlim(3); // Max 3 concurrent requests

        var tasks = models.Select(async model =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Check if original thumbnail URL works
                if (!string.IsNullOrEmpty(model.ThumbnailUrl))
                {
                    var isValid = await CheckUrlValidAsync(model.ThumbnailUrl);
                    if (isValid) return; // Original URL works, no need to fallback
                }

                // Fallback: Get thumbnail from web search
                var fallbackUrl = await _modelService.GetModelThumbnailUrlAsync(model.Id);
                if (!string.IsNullOrEmpty(fallbackUrl) && fallbackUrl != model.ThumbnailUrl)
                {
                    model.ThumbnailUrl = fallbackUrl;

                    // Refresh UI - find and update the card
                    await Dispatcher.InvokeAsync(() => RefreshRecommendedCardThumbnail(model));
                }
            }
            catch
            {
                // Ignore errors - thumbnail is not critical
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Check if a URL is valid by sending HEAD request
    /// </summary>
    private async Task<bool> CheckUrlValidAsync(string url)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url);
            using var response = await httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Refresh thumbnail for a recommended model card
    /// </summary>
    private void RefreshRecommendedCardThumbnail(RecommendedModel model)
    {
        // Find which list contains this model and reload the card
        var recommended = _modelService.GetRecommendedModels();

        foreach (var kvp in recommended)
        {
            Panel? targetList = kvp.Key switch
            {
                ModelType.TextToImage => TextToImageList,
                ModelType.TextToVideo => TextToVideoList,
                ModelType.LoRA => LoraList,
                ModelType.ControlNet => ControlNetList,
                _ => null
            };

            if (targetList == null) continue;

            // Find and update the matching model
            var matchingModel = kvp.Value.FirstOrDefault(m => m.Id == model.Id);
            if (matchingModel != null)
            {
                matchingModel.ThumbnailUrl = model.ThumbnailUrl;

                // Find the card index and replace it
                for (int i = 0; i < targetList.Children.Count && i < kvp.Value.Count; i++)
                {
                    if (kvp.Value[i].Id == model.Id)
                    {
                        targetList.Children.RemoveAt(i);
                        targetList.Children.Insert(i, CreateRecommendedCard(matchingModel));
                        break;
                    }
                }
                return;
            }
        }
    }

    private Border CreateRecommendedCard(RecommendedModel model)
    {
        // Check if model is already downloaded
        var isDownloaded = _downloadedModels.Any(m =>
            m.ModelId.Equals(model.Id, StringComparison.OrdinalIgnoreCase));

        var card = new Border
        {
            Width = 280,
            Height = 340, // Fixed height for consistent cards
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

        // Use DockPanel for proper layout - button always at bottom
        var mainDock = new DockPanel { LastChildFill = true };

        // Action bar - DOCK AT BOTTOM FIRST
        var actionBar = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x15, 0x15, 0x1F)),
            CornerRadius = new CornerRadius(0, 0, 12, 12),
            Padding = new Thickness(14, 10, 14, 10)
        };
        DockPanel.SetDock(actionBar, Dock.Bottom);

        var actionBtn = new Button
        {
            Cursor = Cursors.Hand,
            Tag = model.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Improved button with better proportions
        var btnBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var btnStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isDownloaded)
        {
            // Remove button for downloaded models
            actionBtn.Click += RemoveRecommended_Click;
            btnBorder.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x1A, 0x1A));
            btnBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
            btnBorder.BorderThickness = new Thickness(1);

            btnStack.Children.Add(new PackIcon
            {
                Kind = PackIconKind.Delete,
                Width = 18,
                Height = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                VerticalAlignment = VerticalAlignment.Center
            });
            btnStack.Children.Add(new TextBlock
            {
                Text = "ลบออก",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else
        {
            // Download button for non-downloaded models
            actionBtn.Click += DownloadRecommended_Click;
            btnBorder.Background = new LinearGradientBrush(
                Color.FromRgb(0x10, 0xB9, 0x81),
                Color.FromRgb(0x05, 0x96, 0x69),
                90);

            btnStack.Children.Add(new PackIcon
            {
                Kind = PackIconKind.Download,
                Width = 18,
                Height = 18,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });
            btnStack.Children.Add(new TextBlock
            {
                Text = "ดาวน์โหลด",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        btnBorder.Child = btnStack;
        actionBtn.Content = btnBorder;
        actionBtn.Template = CreateSimpleButtonTemplate();
        actionBar.Child = actionBtn;
        mainDock.Children.Add(actionBar);

        // Content wrapper (fills remaining space)
        var contentWrapper = new StackPanel();

        // Thumbnail area - ALWAYS show (use placeholder if no URL)
        var thumbnailBorder = new Border
        {
            Height = 130,
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            ClipToBounds = true
        };

        var thumbnailGrid = new Grid();

        // Get thumbnail URL or generate placeholder
        var thumbnailUrl = model.ThumbnailUrl;
        if (string.IsNullOrEmpty(thumbnailUrl))
        {
            thumbnailUrl = GeneratePlaceholderUrl(model);
        }

        // Placeholder/loading background with gradient
        var placeholderBorder = new Border
        {
            CornerRadius = new CornerRadius(12, 12, 0, 0)
        };
        var placeholderGradient = GetModelTypeGradient(model.Name);
        placeholderBorder.Background = placeholderGradient;

        // Model initials as fallback
        var initialsText = new TextBlock
        {
            Text = GetModelInitials(model.Name),
            FontSize = 36,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        placeholderBorder.Child = initialsText;
        thumbnailGrid.Children.Add(placeholderBorder);

        // Async loaded image (on top of placeholder)
        var thumbnailImage = new Image
        {
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        thumbnailImage.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);

        // Use async loader
        Helpers.AsyncImageLoader.SetSourceUrl(thumbnailImage, thumbnailUrl);

        thumbnailGrid.Children.Add(thumbnailImage);
        thumbnailBorder.Child = thumbnailGrid;
        contentWrapper.Children.Add(thumbnailBorder);

        // Content area
        var contentStack = new StackPanel { Margin = new Thickness(14, 12, 14, 8) };

        // Name
        var nameText = new TextBlock
        {
            Text = model.Name,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        contentStack.Children.Add(nameText);

        // Model ID
        var idText = new TextBlock
        {
            Text = model.Id,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x6B, 0x8A)),
            Margin = new Thickness(0, 2, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        contentStack.Children.Add(idText);

        // Description
        var descText = new TextBlock
        {
            Text = model.Description,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA8)),
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 34, // Limit to ~2 lines
            Margin = new Thickness(0, 6, 0, 0)
        };
        contentStack.Children.Add(descText);

        // Info badges row
        var badgesStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0)
        };

        // VRAM badge
        var vramBorder = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x30, 0x7C, 0x4D, 0xFF)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            Margin = new Thickness(0, 0, 6, 0)
        };
        var vramText = new TextBlock
        {
            Text = $"🎮 {model.RequiredVramGb}GB",
            FontSize = 10,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromRgb(0x7C, 0x4D, 0xFF))
        };
        vramBorder.Child = vramText;
        badgesStack.Children.Add(vramBorder);

        // Size badge
        if (model.SizeGb > 0)
        {
            var sizeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x30, 0x06, 0xB6, 0xD4)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3)
            };
            var sizeText = new TextBlock
            {
                Text = $"💾 {model.SizeGb:F1}GB",
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(0x06, 0xB6, 0xD4))
            };
            sizeBorder.Child = sizeText;
            badgesStack.Children.Add(sizeBorder);
        }

        contentStack.Children.Add(badgesStack);
        contentWrapper.Children.Add(contentStack);

        mainDock.Children.Add(contentWrapper);
        card.Child = mainDock;
        return card;
    }

    /// <summary>
    /// Generate placeholder URL for model without thumbnail
    /// </summary>
    private string GeneratePlaceholderUrl(RecommendedModel model)
    {
        var initials = GetModelInitials(model.Name);
        var bgColor = GetModelTypeColor(model.Name);
        return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(initials)}&background={bgColor}&color=fff&size=200&bold=true&format=png";
    }

    /// <summary>
    /// Get initials from model name
    /// </summary>
    private string GetModelInitials(string name)
    {
        var words = name.Split(new[] { '-', '_', ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2)
        {
            return $"{words[0][0]}{words[1][0]}".ToUpperInvariant();
        }
        return name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant();
    }

    /// <summary>
    /// Get background color based on model type
    /// </summary>
    private string GetModelTypeColor(string name)
    {
        var lowerName = name.ToLowerInvariant();
        if (lowerName.Contains("flux")) return "7C4DFF";
        if (lowerName.Contains("sdxl") || lowerName.Contains("xl")) return "E91E63";
        if (lowerName.Contains("lora") || lowerName.Contains("lcm")) return "F59E0B";
        if (lowerName.Contains("control")) return "10B981";
        if (lowerName.Contains("video") || lowerName.Contains("animate") || lowerName.Contains("svd")) return "06B6D4";
        if (lowerName.Contains("vae")) return "8B5CF6";
        if (lowerName.Contains("realistic") || lowerName.Contains("dream")) return "EC4899";
        return "6366F1"; // Default indigo
    }

    /// <summary>
    /// Get gradient brush based on model type
    /// </summary>
    private LinearGradientBrush GetModelTypeGradient(string name)
    {
        var lowerName = name.ToLowerInvariant();

        if (lowerName.Contains("flux"))
            return new LinearGradientBrush(Color.FromRgb(0x7C, 0x3A, 0xED), Color.FromRgb(0x4F, 0x46, 0xE5), 135);

        if (lowerName.Contains("sdxl") || lowerName.Contains("xl"))
            return new LinearGradientBrush(Color.FromRgb(0xE9, 0x1E, 0x63), Color.FromRgb(0xBE, 0x18, 0x5D), 135);

        if (lowerName.Contains("lora") || lowerName.Contains("lcm") || lowerName.Contains("lightning"))
            return new LinearGradientBrush(Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0xD9, 0x77, 0x06), 135);

        if (lowerName.Contains("control"))
            return new LinearGradientBrush(Color.FromRgb(0x10, 0xB9, 0x81), Color.FromRgb(0x05, 0x96, 0x69), 135);

        if (lowerName.Contains("video") || lowerName.Contains("animate") || lowerName.Contains("svd"))
            return new LinearGradientBrush(Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x08, 0x91, 0xB2), 135);

        if (lowerName.Contains("vae"))
            return new LinearGradientBrush(Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x7C, 0x3A, 0xED), 135);

        if (lowerName.Contains("realistic"))
            return new LinearGradientBrush(Color.FromRgb(0xEC, 0x48, 0x99), Color.FromRgb(0xDB, 0x27, 0x77), 135);

        if (lowerName.Contains("dream"))
            return new LinearGradientBrush(Color.FromRgb(0xA8, 0x55, 0xF7), Color.FromRgb(0x93, 0x33, 0xEA), 135);

        // Default gradient
        return new LinearGradientBrush(Color.FromRgb(0x63, 0x66, 0xF1), Color.FromRgb(0x4F, 0x46, 0xE5), 135);
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
            // Show loading state, hide results and empty state
            SearchLoadingState.Visibility = Visibility.Visible;
            SearchEmptyState.Visibility = Visibility.Collapsed;
            SearchResultsGridView.Visibility = Visibility.Collapsed;
            SearchResultsListView.Visibility = Visibility.Collapsed;

            var results = await _modelService.SearchModelsAsync(query, ct: _searchCts.Token);
            _searchResults.Clear();

            // Get downloaded model IDs for checking
            var downloadedIds = _downloadedModels.Select(m => m.ModelId).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var model in results)
            {
                _searchResults.Add(new SearchResultViewModel
                {
                    ModelId = model.ModelId,
                    Description = model.PipelineTag ?? "",
                    DownloadsFormatted = FormatNumber((int)model.Downloads),
                    LikesFormatted = FormatNumber(model.Likes),
                    IsDownloaded = downloadedIds.Contains(model.ModelId)
                });
            }

            SearchResultsList.ItemsSource = _searchResults;
            SearchResultsListMode.ItemsSource = _searchResults;

            // Update result count and show clear button
            SearchResultCount.Text = $"พบ {_searchResults.Count} โมเดล";
            ClearSearchButton.Visibility = Visibility.Visible;

            // Load thumbnails in background from multiple sources
            _ = LoadSearchThumbnailsAsync(_searchCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ค้นหาล้มเหลว: {ex.Message}", "ผิดพลาด",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SearchLoadingState.Visibility = Visibility.Collapsed;

            // Show appropriate view based on results
            if (_searchResults.Count > 0)
            {
                // Check which view is currently selected (grid is default)
                var isListView = ViewListBtn.Background is SolidColorBrush brush &&
                                 brush.Color.A > 0x20; // Has highlight color

                SearchResultsGridView.Visibility = !isListView ? Visibility.Visible : Visibility.Collapsed;
                SearchResultsListView.Visibility = isListView ? Visibility.Visible : Visibility.Collapsed;
                SearchEmptyState.Visibility = Visibility.Collapsed;
            }
            else
            {
                SearchEmptyState.Visibility = Visibility.Visible;
            }
        }
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchHuggingFaceBox.Text = "";
        _searchResults.Clear();
        ClearSearchButton.Visibility = Visibility.Collapsed;
        SearchEmptyState.Visibility = Visibility.Visible;
        SearchResultsGridView.Visibility = Visibility.Collapsed;
        SearchResultsListView.Visibility = Visibility.Collapsed;
        SearchResultCount.Text = "";
    }

    private void FilterCategory_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border clickedBorder || clickedBorder.Tag is not string category)
            return;

        // Update visual states for all filter chips
        var filterChips = new[] { FilterAll, FilterCheckpoint, FilterLora, FilterSdxl, FilterFlux, FilterAnime };
        foreach (var chip in filterChips)
        {
            var isSelected = chip == clickedBorder;
            chip.Background = new SolidColorBrush(isSelected
                ? Color.FromArgb(0x30, 0x7C, 0x4D, 0xFF)
                : Color.FromRgb(0x20, 0x20, 0x30));

            // Update text and icon colors
            if (chip.Child is StackPanel stack)
            {
                foreach (var child in stack.Children)
                {
                    if (child is MaterialDesignThemes.Wpf.PackIcon icon)
                    {
                        icon.Foreground = new SolidColorBrush(isSelected
                            ? Color.FromRgb(0x7C, 0x4D, 0xFF)
                            : Color.FromRgb(0x6B, 0x6B, 0x8A));
                    }
                    else if (child is TextBlock text)
                    {
                        text.Foreground = new SolidColorBrush(isSelected
                            ? Color.FromRgb(0x7C, 0x4D, 0xFF)
                            : Color.FromRgb(0x6B, 0x6B, 0x8A));
                        if (isSelected) text.FontWeight = FontWeights.SemiBold;
                        else text.FontWeight = FontWeights.Normal;
                    }
                }
            }
        }

        // Trigger search with the selected category
        if (category == "all")
        {
            // Keep current search or show popular models
            if (!string.IsNullOrWhiteSpace(SearchHuggingFaceBox.Text))
            {
                SearchHuggingFace_Click(sender, new RoutedEventArgs());
            }
        }
        else
        {
            // Set the search box to the category and trigger search
            SearchHuggingFaceBox.Text = category;
            SearchHuggingFace_Click(sender, new RoutedEventArgs());
        }
    }

    private void ViewToggle_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border clickedBorder || clickedBorder.Tag is not string view)
            return;

        var isGridView = view == "grid";

        // Update visual states
        ViewGridBtn.Background = new SolidColorBrush(isGridView
            ? Color.FromArgb(0x30, 0x7C, 0x4D, 0xFF)
            : Color.FromRgb(0x20, 0x20, 0x30));
        ViewListBtn.Background = new SolidColorBrush(!isGridView
            ? Color.FromArgb(0x30, 0x7C, 0x4D, 0xFF)
            : Color.FromRgb(0x20, 0x20, 0x30));

        // Update icon colors
        if (ViewGridBtn.Child is MaterialDesignThemes.Wpf.PackIcon gridIcon)
        {
            gridIcon.Foreground = new SolidColorBrush(isGridView
                ? Color.FromRgb(0x7C, 0x4D, 0xFF)
                : Color.FromRgb(0x6B, 0x6B, 0x8A));
        }
        if (ViewListBtn.Child is MaterialDesignThemes.Wpf.PackIcon listIcon)
        {
            listIcon.Foreground = new SolidColorBrush(!isGridView
                ? Color.FromRgb(0x7C, 0x4D, 0xFF)
                : Color.FromRgb(0x6B, 0x6B, 0x8A));
        }

        // Toggle view visibility
        if (_searchResults.Count > 0)
        {
            SearchResultsGridView.Visibility = isGridView ? Visibility.Visible : Visibility.Collapsed;
            SearchResultsListView.Visibility = !isGridView ? Visibility.Visible : Visibility.Collapsed;

            // Bind list view to the same data source
            if (!isGridView && SearchResultsListMode.ItemsSource == null)
            {
                SearchResultsListMode.ItemsSource = _searchResults;
            }
        }
    }

    #endregion

    #region Download

    private async void DownloadModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            await StartDownloadAsync(modelId, ModelType.TextToImage);

            // Update search results to reflect download
            var searchModel = _searchResults.FirstOrDefault(m => m.ModelId == modelId);
            if (searchModel != null)
            {
                searchModel.IsDownloaded = true;
            }
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

            // Reload recommended models to update button state
            LoadRecommendedModels();
        }
    }

    private async void RemoveRecommended_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            var result = MessageBox.Show($"ลบโมเดล: {modelId}?\n\n" +
                "ไฟล์โมเดลที่ดาวน์โหลดจะถูกลบออก\n" +
                "การดำเนินการนี้ไม่สามารถยกเลิกได้",
                "ยืนยันการลบ", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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

                    // Reload recommended models to update button state
                    LoadRecommendedModels();

                    MessageBox.Show("ลบโมเดลเรียบร้อยแล้ว", "สำเร็จ",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"ไม่สามารถลบโมเดลได้: {ex.Message}", "ผิดพลาด",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private async Task StartDownloadAsync(string modelId, ModelType type, bool isResume = false)
    {
        _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();

        // Track current download for resume
        _currentDownloadModelId = modelId;
        _currentDownloadModelType = type;
        _downloadWasCancelled = false;
        _downloadedBytesBeforeCancel = 0;

        try
        {
            // Setup UI
            DownloadModelName.Text = modelId;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "0%";
            DownloadSizeText.Text = "Preparing...";
            DownloadSpeedText.Text = "-- MB/s";
            DownloadEtaText.Text = "ETA: --";

            // Reset status to downloading
            UpdateDownloadStatus("Downloading", "#2563EB", "#302563EB");

            // Hide resume/delete/close sections, show cancel button
            ResumeSection.Visibility = Visibility.Collapsed;
            DeleteDownloadButton.Visibility = Visibility.Collapsed;
            CloseDownloadButton.Visibility = Visibility.Collapsed;
            CancelDownloadButton.Visibility = Visibility.Visible;

            DownloadProgressOverlay.Visibility = Visibility.Visible;

            // Start beautiful animation
            StartDownloadAnimation();

            var result = await _modelService.DownloadModelAsync(modelId, type, ct: _downloadCts.Token);

            if (result != null)
            {
                // Success!
                StopDownloadAnimation();
                UpdateDownloadStatus("Complete", "#10B981", "#3010B981");
                DownloadProgressBar.Value = 100;
                DownloadProgressText.Text = "100%";

                await Task.Delay(500); // Brief pause to show completion

                MessageBox.Show($"Download complete: {result.Name}\n\n" +
                    $"Size: {result.SizeDisplay}\n" +
                    "You can now use this model for image generation.",
                    "Download Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadDownloadedModelsAsync();
                await UpdateStorageUsageAsync();

                DownloadProgressOverlay.Visibility = Visibility.Collapsed;
            }
        }
        catch (OperationCanceledException)
        {
            // UI was already updated in CancelDownload_Click, just ensure cleanup
            if (!_downloadWasCancelled)
            {
                _downloadWasCancelled = true;
                StopDownloadAnimation();

                // Show cancelled state with resume option
                UpdateDownloadStatus("ยกเลิกแล้ว", "#F59E0B", "#30F59E0B");

                // Show resume section if there was progress
                if (_downloadedBytesBeforeCancel > 0)
                {
                    ResumeInfoText.Text = $"ดาวน์โหลดไปแล้ว {FormatSize(_downloadedBytesBeforeCancel)}";
                    ResumeSection.Visibility = Visibility.Visible;
                }

                // Show close and delete buttons, hide cancel button
                CloseDownloadButton.Visibility = Visibility.Visible;
                DeleteDownloadButton.Visibility = Visibility.Visible;
                CancelDownloadButton.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            // Show error state
            StopDownloadAnimation();
            UpdateDownloadStatus("ล้มเหลว", "#EF4444", "#30EF4444");

            // Show resume option for network errors
            if (_downloadedBytesBeforeCancel > 0)
            {
                ResumeInfoText.Text = $"ดาวน์โหลดไปแล้ว {FormatSize(_downloadedBytesBeforeCancel)} ก่อนเกิดข้อผิดพลาด";
                ResumeSection.Visibility = Visibility.Visible;
            }

            // Show close and delete buttons, hide cancel button
            CloseDownloadButton.Visibility = Visibility.Visible;
            DeleteDownloadButton.Visibility = Visibility.Visible;
            CancelDownloadButton.Visibility = Visibility.Collapsed;

            MessageBox.Show($"ดาวน์โหลดล้มเหลว: {ex.Message}\n\n" +
                "คุณสามารถลองดาวน์โหลดต่อ หรือลบไฟล์ที่ดาวน์โหลดไปแล้ว",
                "ผิดพลาด",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateDownloadStatus(string text, string foregroundColor, string backgroundColor)
    {
        DownloadStatusText.Text = text;
        DownloadStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(foregroundColor));
        DownloadStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(backgroundColor));
    }

    #region Download Progress Animation

    private void StartDownloadAnimation()
    {
        _downloadAnimationFrame = 0;
        _downloadAnimationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _downloadAnimationTimer.Tick += DownloadAnimation_Tick;
        _downloadAnimationTimer.Start();
        DownloadRunningLight.Visibility = Visibility.Visible;
    }

    private void StopDownloadAnimation()
    {
        _downloadAnimationTimer?.Stop();
        _downloadAnimationTimer = null;
        DownloadRunningLight.Visibility = Visibility.Collapsed;
    }

    private void DownloadAnimation_Tick(object? sender, EventArgs e)
    {
        _downloadAnimationFrame++;

        // Running light effect - move across the progress bar
        var containerWidth = DownloadProgressContainer.ActualWidth;
        var progressWidth = (DownloadProgressBar.Value / 100.0) * containerWidth;

        if (progressWidth > 0)
        {
            var lightWidth = 60.0;
            var maxX = progressWidth - lightWidth;
            if (maxX > 0)
            {
                // Bounce back and forth within the progress
                var cycleLength = maxX * 2;
                var position = (_downloadAnimationFrame * 3) % cycleLength;
                var x = position < maxX ? position : cycleLength - position;
                DownloadLightTransform.X = x;
            }
        }

        // Update glow width to match progress
        DownloadProgressGlow.Width = progressWidth;

        // Subtle gradient color animation
        var phase = (_downloadAnimationFrame % 100) / 100.0;
        var hueShift = Math.Sin(phase * Math.PI * 2) * 0.1;

        // Create subtle color variations
        var color1 = Color.FromRgb(
            (byte)(124 + hueShift * 20),
            (byte)(77 + hueShift * 10),
            255);
        var color2 = Color.FromRgb(
            (byte)(167 + hueShift * 15),
            (byte)(139 + hueShift * 10),
            250);
        var color3 = Color.FromRgb(
            (byte)(16 + hueShift * 5),
            (byte)(185 + hueShift * 10),
            (byte)(129 + hueShift * 5));

        DownloadGradStop1.Color = color1;
        DownloadGradStop2.Color = color2;
        DownloadGradStop3.Color = color3;
    }

    #endregion

    private void CancelDownload_Click(object sender, RoutedEventArgs e)
    {
        // Confirm cancellation
        var result = MessageBox.Show(
            "ยกเลิกการดาวน์โหลด?\n\n" +
            "คุณสามารถดาวน์โหลดต่อได้ภายหลัง หรือลบไฟล์ที่ดาวน์โหลดไปแล้ว",
            "ยืนยันการยกเลิก",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Cancel the download
            _downloadCts?.Cancel();

            // Immediately update UI to show cancelled state
            // (don't wait for catch block which may not fire if download already finished)
            _downloadWasCancelled = true;
            StopDownloadAnimation();

            // Update status to cancelled
            UpdateDownloadStatus("ยกเลิกแล้ว", "#F59E0B", "#30F59E0B");

            // Show resume section if there was progress
            if (_downloadedBytesBeforeCancel > 0)
            {
                ResumeInfoText.Text = $"ดาวน์โหลดไปแล้ว {FormatSize(_downloadedBytesBeforeCancel)}";
                ResumeSection.Visibility = Visibility.Visible;
            }

            // Show close and delete buttons, hide cancel button
            CloseDownloadButton.Visibility = Visibility.Visible;
            DeleteDownloadButton.Visibility = Visibility.Visible;
            CancelDownloadButton.Visibility = Visibility.Collapsed;
        }
    }

    private async void ResumeDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentDownloadModelId))
        {
            // Hide resume section
            ResumeSection.Visibility = Visibility.Collapsed;
            DeleteDownloadButton.Visibility = Visibility.Collapsed;

            // Restart download (TODO: implement actual resume with byte range)
            await StartDownloadAsync(_currentDownloadModelId, _currentDownloadModelType, isResume: true);
        }
    }

    private void CloseDownload_Click(object sender, RoutedEventArgs e)
    {
        // Simply close the overlay
        DownloadProgressOverlay.Visibility = Visibility.Collapsed;
        CloseDownloadButton.Visibility = Visibility.Collapsed;
        _currentDownloadModelId = null;
    }

    private async void DeleteDownload_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentDownloadModelId))
        {
            DownloadProgressOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        var result = MessageBox.Show(
            "Delete partial download files?\n\n" +
            "This will free up disk space but you'll need to restart the download from scratch.",
            "Delete Partial Download",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                // Delete partial model files
                var modelPath = _modelService.GetModelPath(_currentDownloadModelId, _currentDownloadModelType);
                if (Directory.Exists(modelPath))
                {
                    Directory.Delete(modelPath, recursive: true);
                }

                MessageBox.Show("Partial download files deleted.",
                    "Cleanup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete files: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // Close overlay
            DownloadProgressOverlay.Visibility = Visibility.Collapsed;
            _currentDownloadModelId = null;

            await UpdateStorageUsageAsync();
        }
    }

    private void OnDownloadProgressChanged(object? sender, ModelDownloadProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var progress = e.Progress;

            // Track bytes for resume functionality
            _downloadedBytesBeforeCancel = progress.DownloadedBytes;

            DownloadProgressBar.Value = progress.Percentage;
            DownloadProgressText.Text = $"{progress.Percentage:F0}%";
            DownloadSizeText.Text = progress.DownloadedDisplay;
            DownloadSpeedText.Text = progress.SpeedDisplay;
            DownloadEtaText.Text = progress.EtaDisplay;

            // Update status color based on progress
            if (progress.Percentage > 0 && progress.Status == DownloadStatus.Downloading)
            {
                UpdateDownloadStatus("Downloading", "#10B981", "#3010B981");
            }
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

    private async void ChangeStoragePath_Click(object sender, RoutedEventArgs e)
    {
        // Use WPF OpenFolderDialog (Windows 10+) or fallback to input dialog
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select folder to store AI models",
            InitialDirectory = _modelService.ModelsDirectory,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            var oldPath = _modelService.ModelsDirectory;
            var newPath = dialog.FolderName;

            // Check if same path
            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                // Check if there are existing models to move
                var existingModels = await _modelService.GetDownloadedModelsAsync();
                var hasModels = existingModels.Any();

                if (hasModels)
                {
                    var totalSize = existingModels.Sum(m => m.SizeBytes);
                    var sizeText = FormatSize(totalSize);
                    var modelCount = existingModels.Count;

                    var moveResult = MessageBox.Show(
                        $"คุณมีโมเดลที่ดาวน์โหลดไว้ {modelCount} รายการ (รวม {sizeText})\n\n" +
                        "ต้องการย้ายโมเดลไปยังโฟลเดอร์ใหม่หรือไม่?\n\n" +
                        "• Yes = ย้ายโมเดลทั้งหมด (อาจใช้เวลาสักครู่)\n" +
                        "• No = ไม่ย้าย (ต้องดาวน์โหลดใหม่)\n" +
                        "• Cancel = ยกเลิก",
                        "ย้ายโมเดล?",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (moveResult == MessageBoxResult.Cancel)
                    {
                        return;
                    }

                    if (moveResult == MessageBoxResult.Yes)
                    {
                        // Move models to new location
                        await MoveModelsToNewLocationAsync(oldPath, newPath, existingModels);
                    }
                }

                // Set the new directory
                _modelService.SetModelsDirectory(newPath);
                await UpdateStorageUsageAsync();
                await LoadDownloadedModelsAsync();

                MessageBox.Show($"เปลี่ยนตำแหน่งเก็บโมเดลเป็น:\n{newPath}",
                    "เสร็จสิ้น", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task MoveModelsToNewLocationAsync(string oldPath, string newPath, IEnumerable<ModelInfo> models)
    {
        var progressWindow = new Window
        {
            Title = "กำลังย้ายโมเดล...",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Background = new SolidColorBrush(Color.FromRgb(26, 26, 46))
        };

        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Height = 20,
            Margin = new Thickness(20, 10, 20, 10)
        };

        var statusText = new TextBlock
        {
            Text = "กำลังเตรียม...",
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var stackPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(statusText);
        stackPanel.Children.Add(progressBar);
        progressWindow.Content = stackPanel;

        progressWindow.Show();

        try
        {
            var modelList = models.ToList();
            var totalModels = modelList.Count;
            var movedCount = 0;
            var errors = new List<string>();

            foreach (var model in modelList)
            {
                var modelName = model.Id.Replace('/', '_');
                var sourcePath = Path.Combine(oldPath, modelName);
                var destPath = Path.Combine(newPath, modelName);

                statusText.Text = $"กำลังย้าย: {model.Name} ({movedCount + 1}/{totalModels})";
                progressBar.Value = (double)movedCount / totalModels * 100;

                // Force UI update
                await Task.Delay(10);
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

                try
                {
                    if (Directory.Exists(sourcePath))
                    {
                        // Create destination directory if not exists
                        Directory.CreateDirectory(destPath);

                        // Move all files
                        await Task.Run(() =>
                        {
                            foreach (var file in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
                            {
                                var relativePath = Path.GetRelativePath(sourcePath, file);
                                var destFile = Path.Combine(destPath, relativePath);
                                var destDir = Path.GetDirectoryName(destFile);
                                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                                {
                                    Directory.CreateDirectory(destDir);
                                }
                                File.Move(file, destFile, overwrite: true);
                            }

                            // Remove old directory
                            Directory.Delete(sourcePath, recursive: true);
                        });
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"{model.Name}: {ex.Message}");
                }

                movedCount++;
            }

            progressBar.Value = 100;
            statusText.Text = "เสร็จสิ้น!";
            await Task.Delay(500);

            if (errors.Count > 0)
            {
                MessageBox.Show($"ย้ายโมเดลเสร็จสิ้น แต่มีบางโมเดลที่ย้ายไม่สำเร็จ:\n\n" +
                    string.Join("\n", errors.Take(5)) +
                    (errors.Count > 5 ? $"\n...และอีก {errors.Count - 5} รายการ" : ""),
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            progressWindow.Close();
        }
    }

    private async void RemoveModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string modelId)
        {
            var result = MessageBox.Show($"Remove model: {modelId}?\n\n" +
                "This will delete the downloaded model files.\n" +
                "This action cannot be undone.",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Warning);

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

                    // Update search results to reflect removal
                    var searchModel = _searchResults.FirstOrDefault(m => m.ModelId == modelId);
                    if (searchModel != null)
                    {
                        searchModel.IsDownloaded = false;
                    }

                    await LoadDownloadedModelsAsync();
                    await UpdateStorageUsageAsync();

                    MessageBox.Show("Model removed successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to remove model: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
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
        ModelType.TextToImage => "#C084FC",
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
    public string? ThumbnailUrl { get; set; }
    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);
}

public class SearchResultViewModel : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isDownloaded;
    private string? _thumbnailUrl;
    private long _sizeBytes;

    public string ModelId { get; set; } = "";
    public string Description { get; set; } = "";
    public string DownloadsFormatted { get; set; } = "0";
    public string LikesFormatted { get; set; } = "0";

    /// <summary>
    /// Author extracted from ModelId (part before /)
    /// </summary>
    public string Author => ModelId.Contains('/') ? ModelId.Split('/')[0] : "Unknown";

    /// <summary>
    /// Display name for model type based on description/pipeline tag
    /// </summary>
    public string ModelTypeDisplay
    {
        get
        {
            var desc = Description?.ToLowerInvariant() ?? "";
            if (desc.Contains("text-to-image")) return "Text-to-Image";
            if (desc.Contains("text-to-video")) return "Text-to-Video";
            if (desc.Contains("lora")) return "LoRA";
            if (desc.Contains("controlnet")) return "ControlNet";
            if (desc.Contains("vae")) return "VAE";
            if (desc.Contains("stable-diffusion")) return "Stable Diffusion";
            if (desc.Contains("flux")) return "Flux";
            return "Model";
        }
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            _sizeBytes = value;
            OnPropertyChanged(nameof(SizeBytes));
            OnPropertyChanged(nameof(HasSize));
            OnPropertyChanged(nameof(SizeFormatted));
        }
    }

    public bool HasSize => _sizeBytes > 0;

    public string SizeFormatted
    {
        get
        {
            if (_sizeBytes <= 0) return "";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = _sizeBytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F1} {sizes[order]}";
        }
    }

    public string? ThumbnailUrl
    {
        get => _thumbnailUrl;
        set { _thumbnailUrl = value; OnPropertyChanged(nameof(ThumbnailUrl)); OnPropertyChanged(nameof(HasThumbnail)); }
    }

    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailUrl);

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set { _isDownloaded = value; OnPropertyChanged(nameof(IsDownloaded)); }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
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
        // Use Microsoft.Win32.OpenFolderDialog for WPF
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Models Directory",
            InitialDirectory = _modelsPathBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            _modelsPathBox.Text = dialog.FolderName;
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
