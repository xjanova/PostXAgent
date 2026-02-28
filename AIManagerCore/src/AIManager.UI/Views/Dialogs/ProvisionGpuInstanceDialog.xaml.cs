using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AIManager.UI.Views.Pages;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Dialogs
{
    /// <summary>
    /// Dialog for provisioning a new GPU instance
    /// </summary>
    public partial class ProvisionGpuInstanceDialog : UserControl
    {
        private readonly List<GpuPoolViewModel> _pools;
        private readonly List<GpuAccountViewModel> _accounts;
        private static int _nextId = 1;

        public ProvisionGpuInstanceDialog(List<GpuPoolViewModel> pools, List<GpuAccountViewModel> accounts)
        {
            InitializeComponent();
            _pools = pools;
            _accounts = accounts;

            // Setup source selection change event
            CmbSource.SelectionChanged += CmbSource_SelectionChanged;

            // Initial population
            PopulateSourceItems();
        }

        private void CmbSource_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateSourceItems();
        }

        private void PopulateSourceItems()
        {
            CmbSourceItem.Items.Clear();

            var selectedSource = (CmbSource.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "pool";

            if (selectedSource == "pool")
            {
                TxtSourceLabel.Text = "Select Pool";
                foreach (var pool in _pools)
                {
                    CmbSourceItem.Items.Add(new ComboBoxItem
                    {
                        Content = $"{pool.Name} ({pool.ActiveAccounts} accounts, ${pool.TotalCredits:F2})",
                        Tag = pool.Id
                    });
                }

                if (_pools.Count == 0)
                {
                    CmbSourceItem.Items.Add(new ComboBoxItem
                    {
                        Content = "No pools available",
                        IsEnabled = false
                    });
                }
            }
            else
            {
                TxtSourceLabel.Text = "Select Account";
                foreach (var account in _accounts)
                {
                    CmbSourceItem.Items.Add(new ComboBoxItem
                    {
                        Content = $"{account.Name} ({account.Provider}, ${account.CreditsBalance:F2})",
                        Tag = account.Id
                    });
                }

                if (_accounts.Count == 0)
                {
                    CmbSourceItem.Items.Add(new ComboBoxItem
                    {
                        Content = "No accounts available",
                        IsEnabled = false
                    });
                }
            }

            if (CmbSourceItem.Items.Count > 0)
            {
                CmbSourceItem.SelectedIndex = 0;
            }
        }

        private void Provision_Click(object sender, RoutedEventArgs e)
        {
            // Validate selections
            if (CmbSourceItem.SelectedItem == null ||
                (CmbSourceItem.SelectedItem as ComboBoxItem)?.IsEnabled == false)
            {
                MessageBox.Show("Please select a valid pool or account.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtDiskSize.Text, out var diskSize) || diskSize < 10)
            {
                MessageBox.Show("Please enter a valid disk size (minimum 10 GB).", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDiskSize.Focus();
                return;
            }

            var gpuType = (CmbGpuType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "RTX_4090";
            var gpuCount = int.Parse((CmbGpuCount.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1");
            var sourceType = (CmbSource.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "pool";
            var sourceId = (CmbSourceItem.SelectedItem as ComboBoxItem)?.Tag;

            // Create the instance view model
            var instance = new GpuInstanceViewModel
            {
                Id = _nextId++,
                Provider = sourceType == "pool" ? "pool" : GetProviderForAccount(sourceId),
                InstanceId = $"inst_{System.Guid.NewGuid().ToString().Substring(0, 8)}",
                GpuType = gpuType.Replace("_", " "),
                GpuCount = gpuCount,
                DiskGb = diskSize,
                Status = "pending",
                PricePerHour = EstimatePrice(gpuType, gpuCount)
            };

            DialogHost.CloseDialogCommand.Execute(instance, this);
        }

        private string GetProviderForAccount(object? accountId)
        {
            if (accountId is int id)
            {
                var account = _accounts.Find(a => a.Id == id);
                return account?.Provider ?? "unknown";
            }
            return "unknown";
        }

        private decimal EstimatePrice(string gpuType, int gpuCount)
        {
            // Rough price estimates per GPU per hour
            var basePrice = gpuType switch
            {
                "RTX_4090" => 0.50m,
                "RTX_4080" => 0.40m,
                "RTX_3090" => 0.35m,
                "RTX_3080" => 0.30m,
                "A100_80GB" => 2.50m,
                "A100_40GB" => 1.50m,
                "H100" => 4.00m,
                _ => 0.50m
            };

            return basePrice * gpuCount;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, this);
        }
    }
}
