using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AIManager.UI.Views.Dialogs;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Pages
{
    public partial class GpuPoolManagerPage : Page, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        // View Models
        public ObservableCollection<GpuAccountViewModel> Accounts { get; } = new();
        public ObservableCollection<GpuPoolViewModel> Pools { get; } = new();
        public ObservableCollection<GpuInstanceViewModel> Instances { get; } = new();
        public ObservableCollection<GpuActivityLogViewModel> ActivityLogs { get; } = new();

        public GpuPoolManagerPage()
        {
            InitializeComponent();
            DataContext = this;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAllDataAsync();
        }

        private async Task LoadAllDataAsync()
        {
            try
            {
                await Task.WhenAll(
                    LoadAccountsAsync(),
                    LoadPoolsAsync(),
                    LoadInstancesAsync(),
                    LoadActivityLogsAsync()
                );

                UpdateSummaryStats();
            }
            catch (Exception ex)
            {
                ShowError($"Failed to load data: {ex.Message}");
            }
        }

        private void UpdateSummaryStats()
        {
            var totalCredits = Accounts.Sum(a => a.CreditsBalance);
            var activeInstances = Instances.Count(i => i.Status == "running");
            var costPerHour = Instances
                .Where(i => i.Status == "running")
                .Sum(i => i.PricePerHour);

            TxtTotalCredits.Text = $"${totalCredits:F2}";
            TxtActiveInstances.Text = activeInstances.ToString();
            TxtCostPerHour.Text = $"${costPerHour:F2}";

            // Update empty states
            AccountsEmptyState.Visibility = Accounts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            InstancesEmptyState.Visibility = Instances.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Data Loading

        private async Task LoadAccountsAsync()
        {
            // TODO: Call API via GpuProviderService
            // For now, show sample data
            await Task.Delay(100);

            Accounts.Clear();
            // Sample data - replace with actual API call
            /*
            var response = await _gpuService.ListAccountsAsync();
            foreach (var account in response.Data)
            {
                Accounts.Add(GpuAccountViewModel.FromApiModel(account));
            }
            */

            AccountsList.ItemsSource = Accounts;
        }

        private async Task LoadPoolsAsync()
        {
            await Task.Delay(100);
            Pools.Clear();
            PoolsList.ItemsSource = Pools;
        }

        private async Task LoadInstancesAsync()
        {
            await Task.Delay(100);
            Instances.Clear();
            InstancesList.ItemsSource = Instances;
        }

        private async Task LoadActivityLogsAsync()
        {
            await Task.Delay(100);
            ActivityLogs.Clear();
            ActivityLogsList.ItemsSource = ActivityLogs;
        }

        #endregion

        #region Account Actions

        private async void AddVastAiAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Dialogs.AddGpuAccountDialog("vast_ai");
                var result = await DialogHost.Show(dialog, "RootDialog");

                if (result is GpuAccountViewModel account)
                {
                    Accounts.Add(account);
                    UpdateSummaryStats();
                    ShowSuccess("Vast.ai account added successfully");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing dialog: {ex.Message}\n\n{ex.StackTrace}", "Dialog Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AddRunPodAccount_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Dialogs.AddGpuAccountDialog("runpod");
                var result = await DialogHost.Show(dialog, "RootDialog");

                if (result is GpuAccountViewModel account)
                {
                    Accounts.Add(account);
                    UpdateSummaryStats();
                    ShowSuccess("RunPod account added successfully");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing dialog: {ex.Message}\n\n{ex.StackTrace}", "Dialog Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshBalances_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Call API to refresh all balances
                await Task.Delay(500); // Simulate API call
                await LoadAccountsAsync();
                ShowSuccess("Balances refreshed");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to refresh balances: {ex.Message}");
            }
        }

        private async void RefreshAccountBalance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int accountId)
            {
                try
                {
                    // TODO: Call API to refresh specific account balance
                    await Task.Delay(300);
                    ShowSuccess("Balance refreshed");
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to refresh balance: {ex.Message}");
                }
            }
        }

        private async void EditAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int accountId)
            {
                var account = Accounts.FirstOrDefault(a => a.Id == accountId);
                if (account != null)
                {
                    var dialog = new Dialogs.EditGpuAccountDialog(account);
                    await DialogHost.Show(dialog, "RootDialog");
                }
            }
        }

        private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int accountId)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete this account?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // TODO: Call API to delete account
                        await Task.Delay(300);
                        var account = Accounts.FirstOrDefault(a => a.Id == accountId);
                        if (account != null)
                        {
                            Accounts.Remove(account);
                            UpdateSummaryStats();
                            ShowSuccess("Account deleted");
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Failed to delete account: {ex.Message}");
                    }
                }
            }
        }

        private void ProviderFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            FilterAccounts();
        }

        private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            FilterAccounts();
        }

        private void FilterAccounts()
        {
            // TODO: Implement filtering
        }

        #endregion

        #region Pool Actions

        private async void CreatePool_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.AddGpuPoolDialog();
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is GpuPoolViewModel pool)
            {
                Pools.Add(pool);
                ShowSuccess("Pool created successfully");
            }
        }

        private async void EditPool_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int poolId)
            {
                var pool = Pools.FirstOrDefault(p => p.Id == poolId);
                if (pool != null)
                {
                    var dialog = new Dialogs.EditGpuPoolDialog(pool);
                    await DialogHost.Show(dialog, "RootDialog");
                }
            }
        }

        #endregion

        #region Instance Actions

        private async void ProvisionInstance_Click(object sender, RoutedEventArgs e)
        {
            if (Pools.Count == 0 && Accounts.Count == 0)
            {
                ShowError("Please add an account or create a pool first");
                return;
            }

            var dialog = new Dialogs.ProvisionGpuInstanceDialog(Pools.ToList(), Accounts.ToList());
            var result = await DialogHost.Show(dialog, "RootDialog");

            if (result is GpuInstanceViewModel instance)
            {
                Instances.Add(instance);
                UpdateSummaryStats();
                ShowSuccess("Instance provisioning started");
            }
        }

        private async void StartInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int instanceId)
            {
                try
                {
                    // TODO: Call API to start instance
                    await Task.Delay(500);
                    var instance = Instances.FirstOrDefault(i => i.Id == instanceId);
                    if (instance != null)
                    {
                        instance.Status = "running";
                    }
                    UpdateSummaryStats();
                    ShowSuccess("Instance started");
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to start instance: {ex.Message}");
                }
            }
        }

        private async void StopInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int instanceId)
            {
                try
                {
                    // TODO: Call API to stop instance
                    await Task.Delay(500);
                    var instance = Instances.FirstOrDefault(i => i.Id == instanceId);
                    if (instance != null)
                    {
                        instance.Status = "stopped";
                    }
                    UpdateSummaryStats();
                    ShowSuccess("Instance stopped");
                }
                catch (Exception ex)
                {
                    ShowError($"Failed to stop instance: {ex.Message}");
                }
            }
        }

        private async void TerminateInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int instanceId)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to terminate this instance? This action cannot be undone.",
                    "Confirm Terminate",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // TODO: Call API to terminate instance
                        await Task.Delay(500);
                        var instance = Instances.FirstOrDefault(i => i.Id == instanceId);
                        if (instance != null)
                        {
                            Instances.Remove(instance);
                        }
                        UpdateSummaryStats();
                        ShowSuccess("Instance terminated");
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Failed to terminate instance: {ex.Message}");
                    }
                }
            }
        }

        private void CopySshCommand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string sshCommand)
            {
                Clipboard.SetText(sshCommand);
                ShowSuccess("SSH command copied to clipboard");
            }
        }

        private async void SyncAllInstances_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Call API to sync all instances
                await LoadInstancesAsync();
                ShowSuccess("Instances synced");
            }
            catch (Exception ex)
            {
                ShowError($"Failed to sync instances: {ex.Message}");
            }
        }

        private void InstanceStatusFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            // TODO: Implement filtering
        }

        #endregion

        #region Helper Methods

        private void ShowSuccess(string message)
        {
            // TODO: Show snackbar notification
            MessageBox.Show(message, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #region View Models

    public class GpuAccountViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Provider { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public decimal CreditsBalance { get; set; }
        public decimal CreditsUsedToday { get; set; }
        public string Status { get; set; } = "active";
        public int InstancesCreated { get; set; }
        public string LastUsed { get; set; } = "Never";

        public string ProviderIcon => Provider switch
        {
            "vast_ai" => "Server",
            "runpod" => "CloudOutline",
            _ => "HelpCircle"
        };

        public Brush ProviderColor => Provider switch
        {
            "vast_ai" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
            "runpod" => new SolidColorBrush(Color.FromRgb(139, 92, 246)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
        };

        public Brush StatusColor => Status switch
        {
            "active" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            "depleted" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            "error" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            "cooldown" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
        };

        public Brush BalanceColor => CreditsBalance switch
        {
            > 5 => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            > 1 => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            _ => new SolidColorBrush(Color.FromRgb(239, 68, 68))
        };
    }

    public class GpuPoolViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Provider { get; set; } = "";
        public string RotationStrategy { get; set; } = "most_credits";
        public int ActiveAccounts { get; set; }
        public decimal TotalCredits { get; set; }
        public int InstancesToday { get; set; }
        public List<GpuPoolMemberViewModel> Members { get; set; } = new();
    }

    public class GpuPoolMemberViewModel
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = "";
        public string Status { get; set; } = "active";
        public decimal Credits { get; set; }

        public Brush StatusColor => Status switch
        {
            "active" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            "depleted" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
        };
    }

    public class GpuInstanceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private string _status = "pending";

        public int Id { get; set; }
        public string Provider { get; set; } = "";
        public string InstanceId { get; set; } = "";
        public string GpuType { get; set; } = "";
        public int GpuCount { get; set; } = 1;
        public int CpuCores { get; set; }
        public int RamGb { get; set; }
        public int DiskGb { get; set; }
        public string Region { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public int SshPort { get; set; }
        public decimal PricePerHour { get; set; }
        public decimal CurrentCost { get; set; }
        public int RuntimeMinutes { get; set; }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusColor)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStart)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStop)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanConnect)));
            }
        }

        public string SshCommand => !string.IsNullOrEmpty(IpAddress) && SshPort > 0
            ? $"ssh -p {SshPort} root@{IpAddress}"
            : "";

        public Brush StatusColor => Status switch
        {
            "running" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            "starting" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            "pending" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            "stopped" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            "error" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))
        };

        public Visibility CanStart => Status is "stopped" or "error" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CanStop => Status is "running" or "starting" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility CanConnect => Status == "running" && !string.IsNullOrEmpty(IpAddress) ? Visibility.Visible : Visibility.Collapsed;
    }

    public class GpuActivityLogViewModel
    {
        public int Id { get; set; }
        public string EventType { get; set; } = "";
        public string Message { get; set; } = "";
        public string Details { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.Now - CreatedAt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                return $"{(int)diff.TotalDays}d ago";
            }
        }

        public string EventIcon => EventType switch
        {
            "instance_created" => "RocketLaunch",
            "instance_started" => "Play",
            "instance_stopped" => "Stop",
            "instance_terminated" => "DeleteForever",
            "balance_checked" => "Refresh",
            "balance_low" => "AlertCircle",
            "api_error" => "AlertOctagon",
            "failover_triggered" => "SwapHorizontal",
            _ => "Information"
        };

        public Brush EventColor => EventType switch
        {
            "instance_created" or "instance_started" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            "instance_stopped" or "instance_terminated" => new SolidColorBrush(Color.FromRgb(107, 114, 128)),
            "balance_low" or "api_error" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            "failover_triggered" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            _ => new SolidColorBrush(Color.FromRgb(99, 102, 241))
        };
    }

    #endregion

}
