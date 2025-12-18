using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AIManager.Core.Services;
using Newtonsoft.Json;

namespace AIManager.UI.Views.Pages;

public partial class StrategiesPage : Page
{
    private readonly AILearningDatabaseService _dbService;
    private List<PostingStrategy> _strategies = new();
    private List<ContentTemplate> _templates = new();
    private PostingStrategy? _currentStrategy;

    public StrategiesPage()
    {
        InitializeComponent();

        // Initialize database service
        var loggerFactory = App.Services?.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<AILearningDatabaseService>()
                     ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AILearningDatabaseService>.Instance;
        _dbService = new AILearningDatabaseService(logger);

        Loaded += async (s, e) =>
        {
            await InitializeDatabaseAsync();
            await LoadStrategiesAsync();
            await LoadTemplatesAsync();
        };
    }

    private async Task InitializeDatabaseAsync()
    {
        try
        {
            var config = new DatabaseConfig
            {
                Provider = DatabaseProvider.SQLite,
                SqliteFilePath = Environment.GetEnvironmentVariable("POSTX_DB_SQLITE_FILE") ?? "ai_learning.db"
            };

            _dbService.Configure(config);
            await _dbService.BuildDatabaseAsync();
        }
        catch (Exception ex)
        {
            // Database not yet initialized, use defaults
            System.Diagnostics.Debug.WriteLine($"Database not initialized: {ex.Message}");
        }
    }

    private async Task LoadStrategiesAsync()
    {
        try
        {
            _strategies = (await _dbService.GetPostingStrategiesAsync()).ToList();

            // Select default strategy
            if (_strategies.Count > 0)
            {
                _currentStrategy = _strategies.FirstOrDefault(s => s.StrategyType == "organic")
                                   ?? _strategies.First();
                ApplyStrategyToUI(_currentStrategy);
            }
        }
        catch
        {
            // Use default values
            _currentStrategy = CreateDefaultStrategy("organic");
            ApplyStrategyToUI(_currentStrategy);
        }
    }

    private async Task LoadTemplatesAsync()
    {
        try
        {
            _templates = (await _dbService.GetContentTemplatesAsync()).ToList();
            TemplatesPanel.ItemsSource = _templates;
            TxtNoTemplates.Visibility = _templates.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            TemplatesPanel.ItemsSource = null;
            TxtNoTemplates.Visibility = Visibility.Visible;
        }
    }

    private PostingStrategy CreateDefaultStrategy(string strategyType)
    {
        return strategyType switch
        {
            "organic" => new PostingStrategy
            {
                Name = "Organic Growth",
                Description = "Natural posting for quality engagement",
                StrategyType = "organic",
                PostsPerDay = 2,
                MinIntervalMinutes = 240,
                OptimalTimes = "[\"09:00\", \"12:00\", \"19:00\"]",
                ContentMix = "{\"promotional\": 20, \"educational\": 40, \"entertaining\": 30, \"engagement\": 10}",
                HashtagStrategy = "Use 5-10 relevant hashtags",
                EngagementScore = 0.7
            },
            "aggressive" => new PostingStrategy
            {
                Name = "Aggressive Marketing",
                Description = "High-frequency posting for maximum reach",
                StrategyType = "aggressive",
                PostsPerDay = 5,
                MinIntervalMinutes = 120,
                OptimalTimes = "[\"08:00\", \"10:00\", \"12:00\", \"15:00\", \"19:00\", \"21:00\"]",
                ContentMix = "{\"promotional\": 50, \"educational\": 20, \"entertaining\": 20, \"engagement\": 10}",
                HashtagStrategy = "Use 15-30 hashtags including trending",
                EngagementScore = 0.5
            },
            "brand_building" => new PostingStrategy
            {
                Name = "Brand Building",
                Description = "Long-term brand development with trust focus",
                StrategyType = "brand_building",
                PostsPerDay = 3,
                MinIntervalMinutes = 180,
                OptimalTimes = "[\"09:00\", \"13:00\", \"20:00\"]",
                ContentMix = "{\"promotional\": 30, \"educational\": 35, \"entertaining\": 25, \"engagement\": 10}",
                HashtagStrategy = "Use branded + niche hashtags",
                EngagementScore = 0.8
            },
            "product_launch" => new PostingStrategy
            {
                Name = "Product Launch",
                Description = "Campaign for product launches with hype",
                StrategyType = "product_launch",
                PostsPerDay = 4,
                MinIntervalMinutes = 90,
                OptimalTimes = "[\"07:00\", \"11:00\", \"15:00\", \"19:00\", \"22:00\"]",
                ContentMix = "{\"promotional\": 60, \"educational\": 15, \"entertaining\": 15, \"engagement\": 10}",
                HashtagStrategy = "Use campaign-specific + trending hashtags",
                EngagementScore = 0.6
            },
            "community" => new PostingStrategy
            {
                Name = "Community Focus",
                Description = "Community building with high engagement",
                StrategyType = "community",
                PostsPerDay = 3,
                MinIntervalMinutes = 180,
                OptimalTimes = "[\"10:00\", \"14:00\", \"20:00\"]",
                ContentMix = "{\"promotional\": 15, \"educational\": 30, \"entertaining\": 25, \"engagement\": 30}",
                HashtagStrategy = "Use community + local hashtags",
                EngagementScore = 0.85
            },
            _ => CreateDefaultStrategy("organic")
        };
    }

    private void Strategy_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string strategyType)
        {
            _currentStrategy = _strategies.FirstOrDefault(s => s.StrategyType == strategyType)
                               ?? CreateDefaultStrategy(strategyType);
            ApplyStrategyToUI(_currentStrategy);
        }
    }

    private void ApplyStrategyToUI(PostingStrategy strategy)
    {
        TxtStrategyName.Text = strategy.Name;
        TxtStrategyDescription.Text = strategy.Description;

        SliderPostsPerDay.Value = strategy.PostsPerDay;
        TxtPostsPerDay.Text = strategy.PostsPerDay.ToString();

        SliderMinInterval.Value = strategy.MinIntervalMinutes;
        TxtMinInterval.Text = strategy.MinIntervalMinutes.ToString();

        TxtHashtagStrategy.Text = strategy.HashtagStrategy ?? "";

        // Parse optimal times
        ClearTimeCheckboxes();
        if (!string.IsNullOrEmpty(strategy.OptimalTimes))
        {
            try
            {
                var times = JsonConvert.DeserializeObject<List<string>>(strategy.OptimalTimes);
                if (times != null)
                {
                    foreach (var time in times)
                    {
                        SetTimeCheckbox(time, true);
                    }
                }
            }
            catch { }
        }

        // Parse content mix
        if (!string.IsNullOrEmpty(strategy.ContentMix))
        {
            try
            {
                var mix = JsonConvert.DeserializeObject<Dictionary<string, int>>(strategy.ContentMix);
                if (mix != null)
                {
                    SliderPromotional.Value = mix.GetValueOrDefault("promotional", 25);
                    SliderEducational.Value = mix.GetValueOrDefault("educational", 25);
                    SliderEntertaining.Value = mix.GetValueOrDefault("entertaining", 25);
                    SliderEngagement.Value = mix.GetValueOrDefault("engagement", 25);
                    UpdateContentMixDisplay();
                }
            }
            catch { }
        }
    }

    private void ClearTimeCheckboxes()
    {
        Time06.IsChecked = false; Time07.IsChecked = false;
        Time08.IsChecked = false; Time09.IsChecked = false;
        Time10.IsChecked = false; Time11.IsChecked = false;
        Time12.IsChecked = false; Time13.IsChecked = false;
        Time14.IsChecked = false; Time15.IsChecked = false;
        Time16.IsChecked = false; Time17.IsChecked = false;
        Time18.IsChecked = false; Time19.IsChecked = false;
        Time20.IsChecked = false; Time21.IsChecked = false;
        Time22.IsChecked = false;
    }

    private void SetTimeCheckbox(string time, bool isChecked)
    {
        var checkbox = time switch
        {
            "06:00" => Time06, "07:00" => Time07, "08:00" => Time08,
            "09:00" => Time09, "10:00" => Time10, "11:00" => Time11,
            "12:00" => Time12, "13:00" => Time13, "14:00" => Time14,
            "15:00" => Time15, "16:00" => Time16, "17:00" => Time17,
            "18:00" => Time18, "19:00" => Time19, "20:00" => Time20,
            "21:00" => Time21, "22:00" => Time22,
            _ => null
        };

        if (checkbox != null)
            checkbox.IsChecked = isChecked;
    }

    private List<string> GetSelectedTimes()
    {
        var times = new List<string>();

        if (Time06.IsChecked == true) times.Add("06:00");
        if (Time07.IsChecked == true) times.Add("07:00");
        if (Time08.IsChecked == true) times.Add("08:00");
        if (Time09.IsChecked == true) times.Add("09:00");
        if (Time10.IsChecked == true) times.Add("10:00");
        if (Time11.IsChecked == true) times.Add("11:00");
        if (Time12.IsChecked == true) times.Add("12:00");
        if (Time13.IsChecked == true) times.Add("13:00");
        if (Time14.IsChecked == true) times.Add("14:00");
        if (Time15.IsChecked == true) times.Add("15:00");
        if (Time16.IsChecked == true) times.Add("16:00");
        if (Time17.IsChecked == true) times.Add("17:00");
        if (Time18.IsChecked == true) times.Add("18:00");
        if (Time19.IsChecked == true) times.Add("19:00");
        if (Time20.IsChecked == true) times.Add("20:00");
        if (Time21.IsChecked == true) times.Add("21:00");
        if (Time22.IsChecked == true) times.Add("22:00");

        return times;
    }

    private void SliderPostsPerDay_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtPostsPerDay != null && SliderPostsPerDay != null)
            TxtPostsPerDay.Text = ((int)SliderPostsPerDay.Value).ToString();
    }

    private void SliderMinInterval_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMinInterval != null && SliderMinInterval != null)
            TxtMinInterval.Text = ((int)SliderMinInterval.Value).ToString();
    }

    private void ContentMix_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateContentMixDisplay();
    }

    private void UpdateContentMixDisplay()
    {
        // Comprehensive null checks - all controls must be initialized
        if (TxtPromotional == null || TxtEducational == null ||
            TxtEntertaining == null || TxtEngagement == null ||
            TxtContentMixTotal == null ||
            SliderPromotional == null || SliderEducational == null ||
            SliderEntertaining == null || SliderEngagement == null)
            return;

        TxtPromotional.Text = $"{(int)SliderPromotional.Value}%";
        TxtEducational.Text = $"{(int)SliderEducational.Value}%";
        TxtEntertaining.Text = $"{(int)SliderEntertaining.Value}%";
        TxtEngagement.Text = $"{(int)SliderEngagement.Value}%";

        var total = (int)SliderPromotional.Value + (int)SliderEducational.Value +
                    (int)SliderEntertaining.Value + (int)SliderEngagement.Value;

        TxtContentMixTotal.Text = $"Total: {total}%";

        // Color indicator
        if (total == 100)
        {
            TxtContentMixTotal.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
        }
        else if (total > 100)
        {
            TxtContentMixTotal.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
        }
        else
        {
            TxtContentMixTotal.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
        }
    }

    private async void SaveStrategy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStrategy == null) return;

        try
        {
            _currentStrategy.PostsPerDay = (int)SliderPostsPerDay.Value;
            _currentStrategy.MinIntervalMinutes = (int)SliderMinInterval.Value;
            _currentStrategy.OptimalTimes = JsonConvert.SerializeObject(GetSelectedTimes());
            _currentStrategy.HashtagStrategy = TxtHashtagStrategy.Text;

            var contentMix = new Dictionary<string, int>
            {
                ["promotional"] = (int)SliderPromotional.Value,
                ["educational"] = (int)SliderEducational.Value,
                ["entertaining"] = (int)SliderEntertaining.Value,
                ["engagement"] = (int)SliderEngagement.Value
            };
            _currentStrategy.ContentMix = JsonConvert.SerializeObject(contentMix);

            await _dbService.SavePostingStrategyAsync(_currentStrategy);

            MessageBox.Show(
                $"Strategy '{_currentStrategy.Name}' saved successfully!",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to save strategy: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ResetStrategy_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStrategy == null) return;

        var defaultStrategy = CreateDefaultStrategy(_currentStrategy.StrategyType);
        _currentStrategy.PostsPerDay = defaultStrategy.PostsPerDay;
        _currentStrategy.MinIntervalMinutes = defaultStrategy.MinIntervalMinutes;
        _currentStrategy.OptimalTimes = defaultStrategy.OptimalTimes;
        _currentStrategy.ContentMix = defaultStrategy.ContentMix;
        _currentStrategy.HashtagStrategy = defaultStrategy.HashtagStrategy;

        ApplyStrategyToUI(_currentStrategy);

        MessageBox.Show(
            "Strategy reset to default values.",
            "Reset",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void AddTemplate_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddTemplateDialog();
        if (dialog.ShowDialog() == true)
        {
            SaveTemplateAsync(dialog.NewTemplate);
        }
    }

    private async void SaveTemplateAsync(ContentTemplate template)
    {
        try
        {
            await _dbService.SaveContentTemplateAsync(template);
            await LoadTemplatesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long templateId)
        {
            var template = _templates.FirstOrDefault(t => t.Id == templateId);
            if (template != null)
            {
                var dialog = new AddTemplateDialog(template);
                if (dialog.ShowDialog() == true)
                {
                    SaveTemplateAsync(dialog.NewTemplate);
                }
            }
        }
    }

    private async void DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long templateId)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this template?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var template = _templates.FirstOrDefault(t => t.Id == templateId);
                if (template != null)
                {
                    template.IsActive = false;
                    await _dbService.SaveContentTemplateAsync(template);
                    await LoadTemplatesAsync();
                }
            }
        }
    }
}

/// <summary>
/// Dialog for adding/editing content templates
/// </summary>
public partial class AddTemplateDialog : Window
{
    public ContentTemplate NewTemplate { get; private set; }

    public AddTemplateDialog(ContentTemplate? existingTemplate = null)
    {
        Width = 500;
        Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = existingTemplate == null ? "Add Content Template" : "Edit Content Template";
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 46));

        NewTemplate = existingTemplate ?? new ContentTemplate();

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Name
        var txtName = new TextBox
        {
            Text = NewTemplate.Name,
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 58)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 120))
        };
        txtName.SetValue(MaterialDesignThemes.Wpf.HintAssist.HintProperty, "Template Name");
        Grid.SetRow(txtName, 0);
        grid.Children.Add(txtName);

        // Type
        var cboType = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 58)),
            Foreground = Brushes.White
        };
        cboType.Items.Add("post");
        cboType.Items.Add("caption");
        cboType.Items.Add("hashtag");
        cboType.Items.Add("story");
        cboType.Items.Add("reel");
        cboType.SelectedItem = string.IsNullOrEmpty(NewTemplate.TemplateType) ? "post" : NewTemplate.TemplateType;
        Grid.SetRow(cboType, 1);
        grid.Children.Add(cboType);

        // Platform
        var cboPlatform = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 58)),
            Foreground = Brushes.White
        };
        cboPlatform.Items.Add("all");
        cboPlatform.Items.Add("facebook");
        cboPlatform.Items.Add("instagram");
        cboPlatform.Items.Add("tiktok");
        cboPlatform.Items.Add("twitter");
        cboPlatform.Items.Add("line");
        cboPlatform.SelectedItem = string.IsNullOrEmpty(NewTemplate.Platform) ? "all" : NewTemplate.Platform;
        Grid.SetRow(cboPlatform, 2);
        grid.Children.Add(cboPlatform);

        // Content
        var txtContent = new TextBox
        {
            Text = NewTemplate.Content,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(42, 42, 58)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 120))
        };
        txtContent.SetValue(MaterialDesignThemes.Wpf.HintAssist.HintProperty, "Template Content (use {variable} for placeholders)");
        Grid.SetRow(txtContent, 3);
        grid.Children.Add(txtContent);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var btnCancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(20, 8, 20, 8),
            Margin = new Thickness(0, 0, 10, 0)
        };
        btnCancel.Click += (s, e) => DialogResult = false;
        buttonPanel.Children.Add(btnCancel);

        var btnSave = new Button
        {
            Content = "Save",
            Padding = new Thickness(20, 8, 20, 8),
            Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
            Foreground = Brushes.White
        };
        btnSave.Click += (s, e) =>
        {
            NewTemplate.Name = txtName.Text;
            NewTemplate.TemplateType = cboType.SelectedItem?.ToString() ?? "post";
            NewTemplate.Platform = cboPlatform.SelectedItem?.ToString() ?? "all";
            NewTemplate.Content = txtContent.Text;
            DialogResult = true;
        };
        buttonPanel.Children.Add(btnSave);

        Grid.SetRow(buttonPanel, 4);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }
}
