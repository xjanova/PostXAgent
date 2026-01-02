using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignThemes.Wpf;
using AIManager.Core.Models;
using AIManager.Core.Services;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// หน้าจัดการ Google Colab GPU Pool
/// </summary>
public partial class ColabGpuPage : Page
{
    private readonly ColabGpuPoolService _poolService;
    private readonly DispatcherTimer _refreshTimer;

    // Colors
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(16, 185, 129));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(245, 158, 11));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(239, 68, 68));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(156, 163, 175));
    private static readonly SolidColorBrush PurpleBrush = new(Color.FromRgb(139, 92, 246));
    private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(6, 182, 212));

    public ColabGpuPage()
    {
        InitializeComponent();

        _poolService = App.Services.GetRequiredService<ColabGpuPoolService>();

        // Subscribe to events
        _poolService.OnPoolEvent += OnPoolEvent;
        _poolService.OnAccountRotated += OnAccountRotated;

        // Timer to refresh every 30 seconds
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _refreshTimer.Tick += (s, e) => RefreshUI();
        _refreshTimer.Start();

        Loaded += (s, e) =>
        {
            LoadSettings();
            RefreshUI();
        };

        Unloaded += (s, e) =>
        {
            _refreshTimer.Stop();
        };
    }

    #region UI Refresh

    private void RefreshUI()
    {
        var status = _poolService.GetPoolStatus();
        var accounts = _poolService.GetAllAccounts();

        // Update stats
        TxtTotalAccounts.Text = $"{status.TotalAccounts} Accounts";
        TxtActiveAccounts.Text = $"{status.ActiveAccounts} พร้อมใช้";

        var hours = status.TotalRemainingQuotaMinutes / 60;
        var mins = status.TotalRemainingQuotaMinutes % 60;
        TxtRemainingQuota.Text = $"{hours}:{mins:D2} ชม. เหลือ";

        // Update pool status badge
        if (status.IsPoolAvailable)
        {
            PoolStatusBadge.Background = GreenBrush;
            TxtPoolStatus.Text = status.InUseAccounts > 0 ? "กำลังใช้งาน" : "พร้อมใช้งาน";
        }
        else
        {
            PoolStatusBadge.Background = RedBrush;
            TxtPoolStatus.Text = "ไม่พร้อม";
        }

        // Current session
        if (status.CurrentSession != null)
        {
            CurrentSessionPanel.Visibility = Visibility.Visible;
            var account = _poolService.GetAccount(status.CurrentSession.AccountId);
            TxtCurrentAccount.Text = account?.Email ?? "Unknown";
            TxtCurrentGpu.Text = $"{status.CurrentSession.GpuType} ({GetGpuMemory(status.CurrentSession.GpuType)}GB)";
            TxtSessionDuration.Text = status.CurrentSession.Duration.ToString(@"h\:mm\:ss");
        }
        else
        {
            CurrentSessionPanel.Visibility = Visibility.Collapsed;
        }

        // Account list
        RefreshAccountList(accounts);
    }

    private void RefreshAccountList(List<ColabGpuAccount> accounts)
    {
        // Clear existing cards (except empty state)
        var toRemove = AccountsPanel.Children.OfType<Border>()
            .Where(b => b != EmptyState).ToList();
        foreach (var item in toRemove)
        {
            AccountsPanel.Children.Remove(item);
        }

        EmptyState.Visibility = accounts.Any() ? Visibility.Collapsed : Visibility.Visible;

        foreach (var account in accounts.OrderBy(a => a.Priority))
        {
            var card = CreateAccountCard(account);
            AccountsPanel.Children.Add(card);
        }
    }

    private Border CreateAccountCard(ColabGpuAccount account)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            Tag = account.Id
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side - Account info
        var infoPanel = new StackPanel();

        // Header row
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal };

        // Status indicator
        var statusIndicator = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = GetStatusColor(account.Status),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        headerRow.Children.Add(statusIndicator);

        // Email
        var emailText = new TextBlock
        {
            Text = account.Email,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        };
        headerRow.Children.Add(emailText);

        // Tier badge
        var tierBadge = new Border
        {
            Background = GetTierColor(account.Tier),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        tierBadge.Child = new TextBlock
        {
            Text = account.Tier.ToString(),
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
        headerRow.Children.Add(tierBadge);

        // Priority
        var priorityBadge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        priorityBadge.Child = new TextBlock
        {
            Text = $"P{account.Priority}",
            FontSize = 10,
            Opacity = 0.7
        };
        headerRow.Children.Add(priorityBadge);

        infoPanel.Children.Add(headerRow);

        // Status text
        var statusText = new TextBlock
        {
            Text = GetStatusText(account),
            FontSize = 12,
            Opacity = 0.6,
            Margin = new Thickness(0, 6, 0, 0)
        };
        infoPanel.Children.Add(statusText);

        // Quota bar
        var quotaPanel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

        var quotaHeader = new Grid();
        quotaHeader.Children.Add(new TextBlock
        {
            Text = "โควต้า",
            FontSize = 11,
            Opacity = 0.6
        });
        quotaHeader.Children.Add(new TextBlock
        {
            Text = $"{account.RemainingQuotaMinutes / 60}:{account.RemainingQuotaMinutes % 60:D2} / {account.DailyQuotaLimitMinutes / 60} ชม.",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right
        });
        quotaPanel.Children.Add(quotaHeader);

        var quotaBarBg = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
            CornerRadius = new CornerRadius(3),
            Height = 6,
            Margin = new Thickness(0, 4, 0, 0)
        };
        var quotaBarFill = new Border
        {
            Background = GetQuotaColor(account.QuotaUsagePercent),
            CornerRadius = new CornerRadius(3),
            Height = 6,
            Width = Math.Max(0, (1 - account.QuotaUsagePercent / 100) * 200),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        quotaBarBg.Child = quotaBarFill;
        quotaPanel.Children.Add(quotaBarBg);

        infoPanel.Children.Add(quotaPanel);

        // Stats row
        var statsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 10, 0, 0)
        };

        statsRow.Children.Add(CreateStatBadge("Sessions", account.TotalSessions.ToString()));
        statsRow.Children.Add(CreateStatBadge("Success", $"{account.SuccessRate:F0}%"));
        if (account.LastUsedAt.HasValue)
        {
            var ago = DateTime.UtcNow - account.LastUsedAt.Value;
            var agoText = ago.TotalHours >= 24 ? $"{(int)ago.TotalDays}d" :
                          ago.TotalHours >= 1 ? $"{(int)ago.TotalHours}h" :
                          $"{(int)ago.TotalMinutes}m";
            statsRow.Children.Add(CreateStatBadge("Last used", agoText));
        }

        infoPanel.Children.Add(statsRow);

        Grid.SetColumn(infoPanel, 0);
        grid.Children.Add(infoPanel);

        // Right side - Actions
        var actionsPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        // Enable toggle
        var enableToggle = new ToggleButton
        {
            IsChecked = account.IsEnabled,
            Style = Application.Current.FindResource("MaterialDesignSwitchToggleButton") as Style,
            ToolTip = "เปิด/ปิดใช้งาน",
            Margin = new Thickness(0, 0, 0, 8)
        };
        enableToggle.Click += (s, e) => ToggleAccount(account.Id, enableToggle.IsChecked == true);
        actionsPanel.Children.Add(enableToggle);

        // Action buttons
        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

        if (account.Status == ColabAccountStatus.Suspended || account.Status == ColabAccountStatus.Error)
        {
            var recoverBtn = CreateIconButton(PackIconKind.Refresh, "กู้คืน", () => RecoverAccount(account.Id));
            recoverBtn.Foreground = GreenBrush;
            buttonPanel.Children.Add(recoverBtn);
        }

        var editBtn = CreateIconButton(PackIconKind.Pencil, "แก้ไข", () => EditAccount(account.Id));
        buttonPanel.Children.Add(editBtn);

        var deleteBtn = CreateIconButton(PackIconKind.Delete, "ลบ", () => DeleteAccount(account.Id));
        deleteBtn.Foreground = RedBrush;
        buttonPanel.Children.Add(deleteBtn);

        actionsPanel.Children.Add(buttonPanel);

        Grid.SetColumn(actionsPanel, 1);
        grid.Children.Add(actionsPanel);

        card.Child = grid;
        return card;
    }

    private static Border CreateStatBadge(string label, string value)
    {
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 11,
            Opacity = 0.6
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        });

        badge.Child = stack;
        return badge;
    }

    private static Button CreateIconButton(PackIconKind icon, string tooltip, Action onClick)
    {
        var btn = new Button
        {
            Style = Application.Current.FindResource("MaterialDesignIconButton") as Style,
            Width = 32,
            Height = 32,
            ToolTip = tooltip,
            Margin = new Thickness(4, 0, 0, 0)
        };
        btn.Content = new PackIcon { Kind = icon, Width = 16, Height = 16 };
        btn.Click += (s, e) => onClick();
        return btn;
    }

    #endregion

    #region Helpers

    private static SolidColorBrush GetStatusColor(ColabAccountStatus status) => status switch
    {
        ColabAccountStatus.Active => GreenBrush,
        ColabAccountStatus.InUse => CyanBrush,
        ColabAccountStatus.Cooldown => YellowBrush,
        ColabAccountStatus.QuotaExhausted => RedBrush,
        ColabAccountStatus.Suspended => RedBrush,
        ColabAccountStatus.NeedsReauth => YellowBrush,
        ColabAccountStatus.Error => RedBrush,
        _ => GrayBrush
    };

    private static SolidColorBrush GetTierColor(ColabTier tier) => tier switch
    {
        ColabTier.Free => new SolidColorBrush(Color.FromArgb(32, 16, 185, 129)),
        ColabTier.Pro => PurpleBrush,
        ColabTier.ProPlus => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        _ => GrayBrush
    };

    private static SolidColorBrush GetQuotaColor(double percent) => percent switch
    {
        >= 90 => RedBrush,
        >= 70 => YellowBrush,
        _ => GreenBrush
    };

    private static string GetStatusText(ColabGpuAccount account) => account.Status switch
    {
        ColabAccountStatus.Active => "พร้อมใช้งาน",
        ColabAccountStatus.InUse => $"กำลังใช้งาน - {account.CurrentGpuType}",
        ColabAccountStatus.Cooldown when account.CooldownUntil.HasValue =>
            $"Cooldown - เหลือ {(account.CooldownUntil.Value - DateTime.UtcNow).TotalMinutes:F0} นาที",
        ColabAccountStatus.QuotaExhausted => "หมดโควต้าแล้ว",
        ColabAccountStatus.Suspended => $"ถูกระงับ - {account.LastError}",
        ColabAccountStatus.NeedsReauth => "ต้องยืนยันตัวตนใหม่",
        ColabAccountStatus.Error => $"Error - {account.LastError}",
        _ => "Unknown"
    };

    private static int GetGpuMemory(ColabGpuType gpuType) => gpuType switch
    {
        ColabGpuType.T4 => 15,
        ColabGpuType.P100 => 16,
        ColabGpuType.V100 => 16,
        ColabGpuType.A100 => 40,
        ColabGpuType.L4 => 24,
        _ => 0
    };

    #endregion

    #region Settings

    private void LoadSettings()
    {
        var settings = _poolService.GetSettings();

        CmbRotationStrategy.SelectedIndex = (int)settings.RotationStrategy;
        TxtCooldownMinutes.Text = settings.CooldownMinutes.ToString();
        TxtQuotaThreshold.Text = settings.QuotaThresholdPercent.ToString();
        TglAutoFailover.IsChecked = settings.AutoFailover;
        TglAutoRotate.IsChecked = settings.AutoRotateOnQuotaLow;
    }

    private async void Settings_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;

        try
        {
            var settings = new ColabPoolSettings
            {
                RotationStrategy = (ColabRotationStrategy)CmbRotationStrategy.SelectedIndex,
                CooldownMinutes = int.TryParse(TxtCooldownMinutes.Text, out var cooldown) ? cooldown : 60,
                QuotaThresholdPercent = int.TryParse(TxtQuotaThreshold.Text, out var threshold) ? threshold : 90,
                AutoFailover = TglAutoFailover.IsChecked == true,
                AutoRotateOnQuotaLow = TglAutoRotate.IsChecked == true
            };

            await _poolService.UpdateSettingsAsync(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RotationStrategy_Changed(object sender, SelectionChangedEventArgs e)
    {
        Settings_Changed(sender, e);
    }

    #endregion

    #region Account Actions

    private async void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        // Show simple dialog to add account
        var dialog = new AddColabAccountDialog();
        if (dialog.ShowDialog() == true && dialog.Account != null)
        {
            try
            {
                await _poolService.AddAccountAsync(dialog.Account);
                RefreshUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add account: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void ToggleAccount(string accountId, bool enabled)
    {
        var account = _poolService.GetAccount(accountId);
        if (account != null)
        {
            account.IsEnabled = enabled;
            await _poolService.UpdateAccountAsync(account);
            RefreshUI();
        }
    }

    private void EditAccount(string accountId)
    {
        var account = _poolService.GetAccount(accountId);
        if (account != null)
        {
            var dialog = new AddColabAccountDialog(account);
            if (dialog.ShowDialog() == true && dialog.Account != null)
            {
                _ = _poolService.UpdateAccountAsync(dialog.Account);
                RefreshUI();
            }
        }
    }

    private async void DeleteAccount(string accountId)
    {
        var account = _poolService.GetAccount(accountId);
        if (account == null) return;

        var result = MessageBox.Show(
            $"ต้องการลบ account {account.Email} ออกจาก pool?",
            "ยืนยันการลบ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _poolService.RemoveAccountAsync(accountId);
                RefreshUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete account: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void RecoverAccount(string accountId)
    {
        try
        {
            await _poolService.RecoverAccountAsync(accountId);
            RefreshUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to recover account: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void EndSession_Click(object sender, RoutedEventArgs e)
    {
        await _poolService.EndSessionAsync();
        RefreshUI();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshUI();
    }

    private void OpenSetupWizard_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to the GPU Setup Wizard page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("GpuSetupWizard");
        }
    }

    private void OpenAIServicesInfo_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to the AI Services Info page
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.NavigateToPage("AIServicesInfo");
        }
    }

    #endregion

    #region Event Handlers

    private void OnPoolEvent(ColabPoolEvent evt)
    {
        Dispatcher.Invoke(() =>
        {
            // Could show notification here
            RefreshUI();
        });
    }

    private void OnAccountRotated(ColabGpuAccount account)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshUI();
            // Could show notification about rotation
        });
    }

    #endregion
}

/// <summary>
/// Dialog สำหรับเพิ่ม/แก้ไข Colab Account
/// </summary>
public class AddColabAccountDialog : Window
{
    public ColabGpuAccount? Account { get; private set; }

    private readonly TextBox _emailBox;
    private readonly TextBox _displayNameBox;
    private readonly ComboBox _tierBox;
    private readonly TextBox _priorityBox;
    private readonly TextBox _quotaLimitBox;

    public AddColabAccountDialog(ColabGpuAccount? existing = null)
    {
        Title = existing == null ? "เพิ่ม Colab Account" : "แก้ไข Colab Account";
        Width = 400;
        Height = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(30, 30, 40));
        Foreground = Brushes.White;

        var mainPanel = new StackPanel { Margin = new Thickness(20) };

        // Email
        mainPanel.Children.Add(new TextBlock { Text = "Email", Margin = new Thickness(0, 0, 0, 4) });
        _emailBox = new TextBox
        {
            Text = existing?.Email ?? "",
            Margin = new Thickness(0, 0, 0, 12)
        };
        mainPanel.Children.Add(_emailBox);

        // Display Name
        mainPanel.Children.Add(new TextBlock { Text = "Display Name", Margin = new Thickness(0, 0, 0, 4) });
        _displayNameBox = new TextBox
        {
            Text = existing?.DisplayName ?? "",
            Margin = new Thickness(0, 0, 0, 12)
        };
        mainPanel.Children.Add(_displayNameBox);

        // Tier
        mainPanel.Children.Add(new TextBlock { Text = "Tier", Margin = new Thickness(0, 0, 0, 4) });
        _tierBox = new ComboBox
        {
            Margin = new Thickness(0, 0, 0, 12)
        };
        _tierBox.Items.Add(new ComboBoxItem { Content = "Free", Tag = ColabTier.Free });
        _tierBox.Items.Add(new ComboBoxItem { Content = "Pro ($9.99/mo)", Tag = ColabTier.Pro });
        _tierBox.Items.Add(new ComboBoxItem { Content = "Pro+ ($49.99/mo)", Tag = ColabTier.ProPlus });
        _tierBox.SelectedIndex = existing != null ? (int)existing.Tier : 0;
        mainPanel.Children.Add(_tierBox);

        // Priority
        mainPanel.Children.Add(new TextBlock { Text = "Priority (1-100, ต่ำ = ใช้ก่อน)", Margin = new Thickness(0, 0, 0, 4) });
        _priorityBox = new TextBox
        {
            Text = existing?.Priority.ToString() ?? "100",
            Margin = new Thickness(0, 0, 0, 12)
        };
        mainPanel.Children.Add(_priorityBox);

        // Daily Quota Limit
        mainPanel.Children.Add(new TextBlock { Text = "Quota Limit (นาที/วัน)", Margin = new Thickness(0, 0, 0, 4) });
        _quotaLimitBox = new TextBox
        {
            Text = existing?.DailyQuotaLimitMinutes.ToString() ?? "720",
            Margin = new Thickness(0, 0, 0, 12)
        };
        mainPanel.Children.Add(_quotaLimitBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var cancelBtn = new Button
        {
            Content = "ยกเลิก",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0)
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };
        buttonPanel.Children.Add(cancelBtn);

        var saveBtn = new Button
        {
            Content = "บันทึก",
            Width = 80,
            Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)),
            Foreground = Brushes.White
        };
        saveBtn.Click += SaveBtn_Click;
        buttonPanel.Children.Add(saveBtn);

        mainPanel.Children.Add(buttonPanel);

        Content = mainPanel;

        if (existing != null)
        {
            Account = existing;
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_emailBox.Text))
        {
            MessageBox.Show("กรุณาใส่ Email", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Account ??= new ColabGpuAccount();

        Account.Email = _emailBox.Text.Trim();
        Account.DisplayName = _displayNameBox.Text.Trim();
        Account.Tier = (ColabTier)((_tierBox.SelectedItem as ComboBoxItem)?.Tag ?? ColabTier.Free);
        Account.Priority = int.TryParse(_priorityBox.Text, out var p) ? Math.Clamp(p, 1, 100) : 100;
        Account.DailyQuotaLimitMinutes = int.TryParse(_quotaLimitBox.Text, out var q) ? q : 720;

        // Set quota limit based on tier if using default
        if (Account.DailyQuotaLimitMinutes == 720)
        {
            Account.DailyQuotaLimitMinutes = Account.Tier switch
            {
                ColabTier.Free => 720,    // 12 hours
                ColabTier.Pro => 1440,    // 24 hours
                ColabTier.ProPlus => 2880, // 48 hours
                _ => 720
            };
        }

        DialogResult = true;
        Close();
    }
}
