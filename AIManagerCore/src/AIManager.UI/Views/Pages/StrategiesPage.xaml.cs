using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AIManager.Core.Services;
using AIManager.Core.NodeEditor;
using Newtonsoft.Json;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// ViewModel for linked workflow display
/// </summary>
public class LinkedWorkflowViewModel
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string WorkflowFilePath { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public DateTime? NextScheduledAt { get; set; }
    public string NextScheduledDisplay => NextScheduledAt?.ToLocalTime().ToString("dd/MM HH:mm") ?? "Not scheduled";
}

/// <summary>
/// ViewModel for available workflow display
/// </summary>
public class AvailableWorkflowViewModel
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
}

public partial class StrategiesPage : Page
{
    private readonly AILearningDatabaseService _dbService;
    private WorkflowSchedulerService? _schedulerService;
    private List<PostingStrategy> _strategies = new();
    private List<ContentTemplate> _templates = new();
    private List<LinkedWorkflowViewModel> _linkedWorkflows = new();
    private List<AvailableWorkflowViewModel> _availableWorkflows = new();
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
            await LoadAvailableWorkflowsAsync();
            await LoadLinkedWorkflowsAsync();
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    private async Task LoadAvailableWorkflowsAsync()
    {
        try
        {
            _availableWorkflows.Clear();

            var workflowsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PostXAgent", "Workflows");

            if (Directory.Exists(workflowsDir))
            {
                var files = Directory.GetFiles(workflowsDir, "*.workflow")
                    .Concat(Directory.GetFiles(workflowsDir, "*.json"));

                foreach (var file in files)
                {
                    _availableWorkflows.Add(new AvailableWorkflowViewModel
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        FilePath = file
                    });
                }
            }

            CboAvailableWorkflows.ItemsSource = _availableWorkflows;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load workflows: {ex.Message}");
        }
    }

    private async Task LoadLinkedWorkflowsAsync()
    {
        try
        {
            if (_currentStrategy == null)
            {
                _linkedWorkflows.Clear();
                LinkedWorkflowsPanel.ItemsSource = null;
                TxtNoLinkedWorkflows.Visibility = Visibility.Visible;
                return;
            }

            // Load from learning records
            var records = await _dbService.GetLearningRecordsByCategoryAsync($"strategy_workflows_{_currentStrategy.Id}");

            _linkedWorkflows = records.Select(r =>
            {
                try
                {
                    return JsonConvert.DeserializeObject<LinkedWorkflowViewModel>(r.Value);
                }
                catch
                {
                    return null;
                }
            }).Where(w => w != null).Cast<LinkedWorkflowViewModel>().ToList();

            LinkedWorkflowsPanel.ItemsSource = _linkedWorkflows;
            TxtNoLinkedWorkflows.Visibility = _linkedWorkflows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            _linkedWorkflows.Clear();
            LinkedWorkflowsPanel.ItemsSource = null;
            TxtNoLinkedWorkflows.Visibility = Visibility.Visible;
        }
    }

    private async void LinkWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (CboAvailableWorkflows.SelectedItem is not AvailableWorkflowViewModel selected)
        {
            MessageBox.Show("Please select a workflow to link.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_currentStrategy == null)
        {
            MessageBox.Show("Please select a strategy first.", "No Strategy", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check if already linked
        if (_linkedWorkflows.Any(w => w.WorkflowFilePath == selected.FilePath))
        {
            MessageBox.Show("This workflow is already linked to this strategy.", "Already Linked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Parse strategy times for scheduling
        DateTime? nextScheduled = null;
        if (!string.IsNullOrEmpty(_currentStrategy.OptimalTimes))
        {
            try
            {
                var times = JsonConvert.DeserializeObject<List<string>>(_currentStrategy.OptimalTimes);
                if (times != null && times.Count > 0)
                {
                    var now = DateTime.Now;
                    foreach (var timeStr in times.OrderBy(t => t))
                    {
                        if (TimeSpan.TryParse(timeStr, out var time))
                        {
                            var scheduled = now.Date.Add(time);
                            if (scheduled > now)
                            {
                                nextScheduled = scheduled;
                                break;
                            }
                        }
                    }

                    if (!nextScheduled.HasValue && TimeSpan.TryParse(times.OrderBy(t => t).First(), out var firstTime))
                    {
                        nextScheduled = now.Date.AddDays(1).Add(firstTime);
                    }
                }
            }
            catch { }
        }

        var linkedWorkflow = new LinkedWorkflowViewModel
        {
            Id = DateTime.UtcNow.Ticks,
            Name = selected.Name,
            WorkflowFilePath = selected.FilePath,
            IsEnabled = true,
            NextScheduledAt = nextScheduled
        };

        // Save to database
        try
        {
            var record = new LearningRecord
            {
                Category = $"strategy_workflows_{_currentStrategy.Id}",
                Key = linkedWorkflow.Id.ToString(),
                Value = JsonConvert.SerializeObject(linkedWorkflow),
                Metadata = linkedWorkflow.Name
            };

            await _dbService.SaveLearningRecordAsync(record);
            await LoadLinkedWorkflowsAsync();

            MessageBox.Show($"Workflow '{selected.Name}' linked to strategy successfully!",
                "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to link workflow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RemoveLinkedWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is long workflowId && _currentStrategy != null)
        {
            var result = MessageBox.Show("Remove this workflow from the strategy?",
                "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Mark as inactive in database
                    var record = new LearningRecord
                    {
                        Category = $"strategy_workflows_{_currentStrategy.Id}",
                        Key = workflowId.ToString(),
                        Value = "", // Empty = deleted
                        Confidence = 0
                    };

                    // Just reload - the empty value will be filtered out
                    _linkedWorkflows.RemoveAll(w => w.Id == workflowId);
                    await LoadLinkedWorkflowsAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to remove workflow: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenWorkflowEditor_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Workflow Editor page
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("WorkflowEditor");
        }
    }

    private async void RunAllWorkflows_Click(object sender, RoutedEventArgs e)
    {
        if (_linkedWorkflows.Count == 0)
        {
            MessageBox.Show("No workflows linked to run.", "No Workflows", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var enabledWorkflows = _linkedWorkflows.Where(w => w.IsEnabled).ToList();
        if (enabledWorkflows.Count == 0)
        {
            MessageBox.Show("All linked workflows are disabled.", "No Enabled Workflows", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Run {enabledWorkflows.Count} workflow(s) now?\n\nThis will execute all enabled workflows linked to this strategy.",
            "Confirm Run",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Initialize scheduler service if needed
            if (_schedulerService == null)
            {
                try
                {
                    var loggerFactory = App.Services?.GetService<ILoggerFactory>();
                    var contentGen = App.Services?.GetService<ContentGeneratorService>();
                    var imageGen = App.Services?.GetService<ImageGeneratorService>();

                    if (loggerFactory != null && contentGen != null && imageGen != null)
                    {
                        var nodeRegistry = new NodeRegistry();
                        var workflowEngine = new WorkflowEngine(
                            loggerFactory.CreateLogger<WorkflowEngine>(),
                            nodeRegistry, contentGen, imageGen);

                        _schedulerService = new WorkflowSchedulerService(
                            loggerFactory.CreateLogger<WorkflowSchedulerService>(),
                            workflowEngine, _dbService);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to initialize workflow engine: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            // Execute workflows
            var successCount = 0;
            var failCount = 0;

            foreach (var workflow in enabledWorkflows)
            {
                try
                {
                    var history = await _schedulerService!.ExecuteWorkflowByPathAsync(workflow.WorkflowFilePath);

                    if (history.Status == WorkflowExecutionStatus.Completed)
                        successCount++;
                    else
                        failCount++;
                }
                catch
                {
                    failCount++;
                }
            }

            MessageBox.Show(
                $"Execution complete!\n\nSuccess: {successCount}\nFailed: {failCount}",
                "Results",
                MessageBoxButton.OK,
                successCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
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

    private async void Strategy_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string strategyType)
        {
            _currentStrategy = _strategies.FirstOrDefault(s => s.StrategyType == strategyType)
                               ?? CreateDefaultStrategy(strategyType);
            ApplyStrategyToUI(_currentStrategy);

            // Reload linked workflows for the new strategy
            await LoadLinkedWorkflowsAsync();
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
