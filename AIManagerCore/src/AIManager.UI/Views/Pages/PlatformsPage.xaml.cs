using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Models;
using AIManager.Core.Services;

namespace AIManager.UI.Views.Pages;

public partial class PlatformsPage : Page
{
    private readonly PlatformSetupService? _setupService;
    private readonly AccountPoolManager? _poolManager;

    public PlatformsPage()
    {
        InitializeComponent();

        // Get services from DI
        _setupService = App.Services?.GetService<PlatformSetupService>();
        _poolManager = App.Services?.GetService<AccountPoolManager>();

        LoadPlatforms();
        LoadPoolFilters();
    }

    #region Tab Navigation

    private void Tab_Changed(object sender, RoutedEventArgs e)
    {
        if (PlatformsPanel == null || AccountPoolsPanel == null || HealthMonitorPanel == null) return;

        if (TabPlatforms?.IsChecked == true)
        {
            PlatformsPanel.Visibility = Visibility.Visible;
            AccountPoolsPanel.Visibility = Visibility.Collapsed;
            HealthMonitorPanel.Visibility = Visibility.Collapsed;
        }
        else if (TabAccountPools?.IsChecked == true)
        {
            PlatformsPanel.Visibility = Visibility.Collapsed;
            AccountPoolsPanel.Visibility = Visibility.Visible;
            HealthMonitorPanel.Visibility = Visibility.Collapsed;
            LoadPools();
        }
        else if (TabHealthMonitor?.IsChecked == true)
        {
            PlatformsPanel.Visibility = Visibility.Collapsed;
            AccountPoolsPanel.Visibility = Visibility.Collapsed;
            HealthMonitorPanel.Visibility = Visibility.Visible;
            LoadHealthData();
        }
    }

    #endregion

    #region Platforms Tab

    private void LoadPlatforms()
    {
        var platforms = new List<PlatformDisplayInfo>();

        foreach (SocialPlatform platform in Enum.GetValues(typeof(SocialPlatform)))
        {
            // Skip no specific platform (all values are valid)

            var setupInfo = _setupService?.GetSetupInfo(platform);
            var accountCount = _poolManager?.GetHealthReport(platform)?.Count ?? 0;

            platforms.Add(new PlatformDisplayInfo
            {
                Platform = platform,
                Name = platform.ToString(),
                Icon = GetPlatformIcon(platform),
                Color = GetPlatformColor(platform),
                AuthType = setupInfo?.AuthType.ToString() ?? "OAuth2",
                RequiredCredentialsText = setupInfo != null
                    ? $"Required: {string.Join(", ", setupInfo.RequiredCredentials)}"
                    : "Setup required",
                Status = accountCount > 0 ? "Configured" : "NotConfigured",
                StatusText = accountCount > 0 ? $"{accountCount} account(s)" : "Not configured",
                AccountCount = accountCount > 0 ? $"({accountCount} accounts)" : "",
                DocsUrl = setupInfo?.DocumentationUrl ?? ""
            });
        }

        PlatformsList.ItemsSource = platforms;
    }

    private void SetupPlatform_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SocialPlatform platform)
        {
            _setupService?.OpenSetupPage(platform);
        }
    }

    private void ManageAccounts_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SocialPlatform platform)
        {
            // Switch to Account Pools tab and filter by platform
            TabAccountPools.IsChecked = true;
            CmbPoolPlatformFilter.SelectedItem = platform.ToString();
        }
    }

    private void OpenDocs_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string url && !string.IsNullOrEmpty(url))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open browser: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    #endregion

    #region Account Pools Tab

    private void LoadPoolFilters()
    {
        var platforms = new List<string> { "All Platforms" };
        foreach (SocialPlatform platform in Enum.GetValues(typeof(SocialPlatform)))
        {
            platforms.Add(platform.ToString());
        }
        CmbPoolPlatformFilter.ItemsSource = platforms;
        CmbPoolPlatformFilter.SelectedIndex = 0;
    }

    private void LoadPools(SocialPlatform? filterPlatform = null)
    {
        if (_poolManager == null)
        {
            EmptyPoolsState.Visibility = Visibility.Visible;
            return;
        }

        var pools = _poolManager.GetPools(filterPlatform);

        if (!pools.Any())
        {
            PoolsList.ItemsSource = null;
            EmptyPoolsState.Visibility = Visibility.Visible;
            return;
        }

        EmptyPoolsState.Visibility = Visibility.Collapsed;

        var displayPools = pools.Select(p => new PoolDisplayInfo
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description ?? "",
            Platform = p.Platform,
            PlatformName = p.Platform.ToString(),
            PlatformColor = GetPlatformColor(p.Platform),
            IsEnabled = p.IsEnabled,
            AccountCount = p.Accounts.Count,
            AvailableCount = p.Accounts.Count(a => a.IsActive && (_poolManager?.IsAccountAvailable(a.Id) ?? false)),
            PostsToday = p.Accounts.Sum(a => _poolManager?.GetAccountStatus(a.Id)?.DailyPostCount ?? 0)
        }).ToList();

        PoolsList.ItemsSource = displayPools;
    }

    private void PoolFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (CmbPoolPlatformFilter.SelectedItem is string selected)
        {
            if (selected == "All Platforms")
            {
                LoadPools();
            }
            else if (Enum.TryParse<SocialPlatform>(selected, out var platform))
            {
                LoadPools(platform);
            }
        }
    }

    private void CreatePool_Click(object sender, RoutedEventArgs e)
    {
        // Show create pool dialog
        var dialog = new CreatePoolDialog();
        if (dialog.ShowDialog() == true && _poolManager != null)
        {
            _poolManager.CreatePool(dialog.SelectedPlatform, dialog.PoolName);
            LoadPools();
            MessageBox.Show($"Pool '{dialog.PoolName}' created successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ManagePoolAccounts_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string poolId)
        {
            // Show manage accounts dialog
            var dialog = new ManageAccountsDialog(poolId, _poolManager);
            dialog.ShowDialog();
            LoadPools();
        }
    }

    private void DeletePool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string poolId)
        {
            var result = MessageBox.Show(
                "Are you sure you want to delete this pool? All accounts in the pool will be removed.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes && _poolManager != null)
            {
                _poolManager.DeletePool(poolId);
                LoadPools();
            }
        }
    }

    #endregion

    #region Health Monitor Tab

    private void LoadHealthData()
    {
        if (_poolManager == null) return;

        var healthReports = _poolManager.GetHealthReport();
        var alerts = _poolManager.CheckHealth();

        // Update summary cards
        TxtTotalAccounts.Text = healthReports.Count.ToString();
        TxtActiveAccounts.Text = healthReports.Count(r => r.State == AccountState.Active && r.IsAvailable).ToString();
        TxtCooldownAccounts.Text = healthReports.Count(r => r.State == AccountState.Cooldown).ToString();
        TxtBannedAccounts.Text = healthReports.Count(r => r.State == AccountState.Banned).ToString();

        var totalPosts = healthReports.Sum(r => r.DailyUsage);
        var totalLimit = healthReports.Sum(r => r.DailyLimit > 0 ? r.DailyLimit : 50);
        var successRateSum = healthReports.Where(r => r.SuccessRate > 0).Sum(r => r.SuccessRate);
        var successRateCount = healthReports.Count(r => r.SuccessRate > 0);
        var avgSuccessRate = successRateCount > 0 ? successRateSum / successRateCount : 0;
        TxtSuccessRate.Text = $"{avgSuccessRate:F1}%";

        // Update alerts
        AlertsList.ItemsSource = alerts.Select(a => new AlertDisplayInfo
        {
            AccountName = a.AccountName,
            Message = a.Message,
            Severity = a.Severity.ToString()
        }).ToList();

        // Update health grid
        HealthGrid.ItemsSource = healthReports.Select(r => new HealthDisplayInfo
        {
            AccountName = r.AccountName,
            PoolName = r.PoolName,
            Platform = r.Platform.ToString(),
            State = r.State.ToString(),
            SuccessRateText = $"{r.SuccessRate:F1}%",
            DailyUsageText = $"{r.DailyUsage}/{(r.DailyLimit > 0 ? r.DailyLimit : 50)}",
            LastUsedText = r.LastUsed?.ToString("HH:mm dd/MM") ?? "Never",
            LastError = r.LastError ?? "-"
        }).ToList();
    }

    #endregion

    #region Helpers

    private static string GetPlatformIcon(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => "Facebook",
            SocialPlatform.Instagram => "Instagram",
            SocialPlatform.TikTok => "Video",
            SocialPlatform.Twitter => "Twitter",
            SocialPlatform.Line => "Chat",
            SocialPlatform.YouTube => "Youtube",
            SocialPlatform.Threads => "At",
            SocialPlatform.LinkedIn => "Linkedin",
            SocialPlatform.Pinterest => "Pinterest",
            _ => "Web"
        };
    }

    private static string GetPlatformColor(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => "#1877F2",
            SocialPlatform.Instagram => "#DD2A7B",
            SocialPlatform.TikTok => "#00F2EA",
            SocialPlatform.Twitter => "#1DA1F2",
            SocialPlatform.Line => "#00C300",
            SocialPlatform.YouTube => "#FF0000",
            SocialPlatform.Threads => "#000000",
            SocialPlatform.LinkedIn => "#0A66C2",
            SocialPlatform.Pinterest => "#E60023",
            _ => "#9E9E9E"
        };
    }

    #endregion

    #region Display Models

    public class PlatformDisplayInfo
    {
        public SocialPlatform Platform { get; set; }
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Color { get; set; } = "";
        public string AuthType { get; set; } = "";
        public string RequiredCredentialsText { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string AccountCount { get; set; } = "";
        public string DocsUrl { get; set; } = "";
    }

    public class PoolDisplayInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public SocialPlatform Platform { get; set; }
        public string PlatformName { get; set; } = "";
        public string PlatformColor { get; set; } = "";
        public bool IsEnabled { get; set; }
        public int AccountCount { get; set; }
        public int AvailableCount { get; set; }
        public int PostsToday { get; set; }
    }

    public class AlertDisplayInfo
    {
        public string AccountName { get; set; } = "";
        public string Message { get; set; } = "";
        public string Severity { get; set; } = "";
    }

    public class HealthDisplayInfo
    {
        public string AccountName { get; set; } = "";
        public string PoolName { get; set; } = "";
        public string Platform { get; set; } = "";
        public string State { get; set; } = "";
        public string SuccessRateText { get; set; } = "";
        public string DailyUsageText { get; set; } = "";
        public string LastUsedText { get; set; } = "";
        public string LastError { get; set; } = "";
    }

    #endregion
}

/// <summary>
/// Dialog for creating a new account pool
/// </summary>
public class CreatePoolDialog : Window
{
    private readonly ComboBox _platformCombo;
    private readonly TextBox _nameTextBox;

    public SocialPlatform SelectedPlatform { get; private set; }
    public string PoolName { get; private set; } = "";

    public CreatePoolDialog()
    {
        Title = "Create Account Pool";
        Width = 400;
        Height = 200;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Platform selection
        grid.Children.Add(new TextBlock { Text = "Platform:", Margin = new Thickness(0, 0, 0, 5) });
        _platformCombo = new ComboBox { Margin = new Thickness(0, 0, 0, 15) };
        foreach (SocialPlatform platform in Enum.GetValues(typeof(SocialPlatform)))
        {
            _platformCombo.Items.Add(platform);
        }
        _platformCombo.SelectedIndex = 0;
        Grid.SetRow(_platformCombo, 1);
        grid.Children.Add(_platformCombo);

        // Pool name
        var nameLabel = new TextBlock { Text = "Pool Name:", Margin = new Thickness(0, 0, 0, 5) };
        Grid.SetRow(nameLabel, 2);
        grid.Children.Add(nameLabel);

        _nameTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 20) };
        Grid.SetRow(_nameTextBox, 3);
        grid.Children.Add(_nameTextBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button { Content = "Cancel", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
        cancelButton.Click += (_, _) => DialogResult = false;
        buttonPanel.Children.Add(cancelButton);

        var createButton = new Button { Content = "Create", Width = 80 };
        createButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show("Please enter a pool name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SelectedPlatform = (SocialPlatform)_platformCombo.SelectedItem;
            PoolName = _nameTextBox.Text.Trim();
            DialogResult = true;
        };
        buttonPanel.Children.Add(createButton);

        Grid.SetRow(buttonPanel, 4);
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(buttonPanel, 4);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }
}

/// <summary>
/// Dialog for managing accounts in a pool
/// </summary>
public class ManageAccountsDialog : Window
{
    private readonly string _poolId;
    private readonly AccountPoolManager? _poolManager;
    private readonly ListBox _accountsList;

    public ManageAccountsDialog(string poolId, AccountPoolManager? poolManager)
    {
        _poolId = poolId;
        _poolManager = poolManager;

        Title = "Manage Pool Accounts";
        Width = 500;
        Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Accounts list
        _accountsList = new ListBox { Margin = new Thickness(0, 0, 0, 15) };
        grid.Children.Add(_accountsList);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var addButton = new Button { Content = "Add Account", Width = 100, Margin = new Thickness(0, 0, 10, 0) };
        addButton.Click += AddAccount_Click;
        buttonPanel.Children.Add(addButton);

        var removeButton = new Button { Content = "Remove", Width = 80, Margin = new Thickness(0, 0, 10, 0) };
        removeButton.Click += RemoveAccount_Click;
        buttonPanel.Children.Add(removeButton);

        var closeButton = new Button { Content = "Close", Width = 80 };
        closeButton.Click += (_, _) => Close();
        buttonPanel.Children.Add(closeButton);

        Grid.SetRow(buttonPanel, 1);
        grid.Children.Add(buttonPanel);

        Content = grid;

        LoadAccounts();
    }

    private void LoadAccounts()
    {
        _accountsList.Items.Clear();
        var accounts = _poolManager?.GetAccounts(_poolId) ?? new List<PoolAccount>();
        foreach (var account in accounts)
        {
            var status = _poolManager?.GetAccountStatus(account.Id);
            var statusText = status?.State.ToString() ?? "Unknown";
            _accountsList.Items.Add(new ListBoxItem
            {
                Content = $"{account.Name} - {statusText}",
                Tag = account.Id
            });
        }
    }

    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        // Show input dialog for account name
        var dialog = new InputDialog("Add Account", "Enter account name:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputValue) && _poolManager != null)
        {
            _poolManager.AddAccount(_poolId, dialog.InputValue, new PlatformCredentials());
            LoadAccounts();
        }
    }

    private void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if (_accountsList.SelectedItem is ListBoxItem item && item.Tag is string accountId)
        {
            _poolManager?.RemoveAccount(_poolId, accountId);
            LoadAccounts();
        }
    }
}

/// <summary>
/// Simple input dialog for WPF
/// </summary>
public class InputDialog : Window
{
    private readonly TextBox _inputTextBox;

    public string InputValue { get; private set; } = "";

    public InputDialog(string title, string prompt)
    {
        Title = title;
        Width = 350;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Prompt
        var promptLabel = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) };
        grid.Children.Add(promptLabel);

        // Input
        _inputTextBox = new TextBox { Margin = new Thickness(0, 0, 0, 15) };
        Grid.SetRow(_inputTextBox, 1);
        grid.Children.Add(_inputTextBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancelButton = new Button { Content = "Cancel", Width = 75, Margin = new Thickness(0, 0, 10, 0) };
        cancelButton.Click += (_, _) => DialogResult = false;
        buttonPanel.Children.Add(cancelButton);

        var okButton = new Button { Content = "OK", Width = 75 };
        okButton.Click += (_, _) =>
        {
            InputValue = _inputTextBox.Text.Trim();
            DialogResult = true;
        };
        buttonPanel.Children.Add(okButton);

        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        Content = grid;

        Loaded += (_, _) => _inputTextBox.Focus();
    }
}
