using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Dialogs
{
    /// <summary>
    /// Dialog for viewing GPU instance details
    /// </summary>
    public partial class GpuInstanceDetailsDialog : UserControl
    {
        /// <summary>
        /// Gets or sets whether the instance was terminated
        /// </summary>
        public bool WasTerminated { get; private set; }

        /// <summary>
        /// Instance data model
        /// </summary>
        public GpuInstanceData? Instance { get; private set; }

        public GpuInstanceDetailsDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Load instance data into the dialog
        /// </summary>
        public void LoadInstance(GpuInstanceData instance)
        {
            Instance = instance;

            TxtTitle.Text = instance.Name ?? "GPU Instance";
            TxtStatus.Text = instance.Status;
            TxtInstanceId.Text = $"Instance ID: {instance.InstanceId}";

            // Set status badge color
            StatusBadge.Background = instance.Status switch
            {
                "running" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129)),  // Green
                "starting" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)), // Yellow
                "stopping" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 115, 22)), // Orange
                "stopped" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)), // Gray
                "error" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),     // Red
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 165, 250))           // Blue
            };

            // Provider info
            TxtProvider.Text = instance.Provider;
            TxtAccount.Text = instance.AccountName;
            TxtProviderInstanceId.Text = instance.ProviderInstanceId;

            // Hardware info
            TxtGpuModel.Text = instance.GpuModel ?? "Unknown";
            TxtGpuMemory.Text = instance.GpuMemoryGb > 0 ? $"{instance.GpuMemoryGb} GB" : "Unknown";
            TxtCpuCores.Text = instance.CpuCores > 0 ? $"{instance.CpuCores} cores" : "Unknown";
            TxtRam.Text = instance.RamGb > 0 ? $"{instance.RamGb} GB" : "Unknown";
            TxtDisk.Text = instance.DiskGb > 0 ? $"{instance.DiskGb} GB" : "Unknown";

            // Connection info
            TxtIpAddress.Text = instance.IpAddress ?? "N/A";
            TxtSshPort.Text = instance.SshPort > 0 ? instance.SshPort.ToString() : "22";

            // Cost info
            TxtHourlyRate.Text = $"${instance.HourlyRate:F2}/hr";
            TxtTotalCost.Text = $"${instance.TotalCost:F2}";
            TxtStartedAt.Text = instance.StartedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";

            if (instance.StartedAt.HasValue)
            {
                var runtime = DateTime.UtcNow - instance.StartedAt.Value;
                TxtRuntime.Text = $"{(int)runtime.TotalHours}h {runtime.Minutes}m";
            }
            else
            {
                TxtRuntime.Text = "N/A";
            }

            // Disable terminate button if not running
            BtnTerminate.IsEnabled = instance.Status == "running" || instance.Status == "starting";
        }

        private void CopyIpAddress_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Instance?.IpAddress))
            {
                try
                {
                    Clipboard.SetText(Instance.IpAddress);
                    MessageBox.Show("IP address copied to clipboard.", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SshConnect_Click(object sender, RoutedEventArgs e)
        {
            if (Instance == null || string.IsNullOrEmpty(Instance.IpAddress))
            {
                MessageBox.Show("No IP address available for SSH connection.", "SSH Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Try to open SSH connection in Windows Terminal or default terminal
                var sshCommand = $"ssh root@{Instance.IpAddress} -p {Instance.SshPort}";

                // Try Windows Terminal first, fallback to cmd
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "wt.exe",
                        Arguments = $"new-tab cmd /k {sshCommand}",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // Fallback to cmd
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/k {sshCommand}",
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open SSH: {ex.Message}\n\nSSH command: ssh root@{Instance.IpAddress} -p {Instance.SshPort}",
                    "SSH Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Terminate_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to terminate this instance?\n\nThis action cannot be undone and you will be charged for any usage up to this point.",
                "Confirm Termination",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                BtnTerminate.IsEnabled = false;

                // TODO: Call actual API to terminate instance
                await System.Threading.Tasks.Task.Delay(2000);

                WasTerminated = true;
                MessageBox.Show("Instance terminated successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogHost.CloseDialogCommand.Execute(WasTerminated, this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to terminate instance: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                BtnTerminate.IsEnabled = true;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(WasTerminated, this);
        }
    }

    /// <summary>
    /// Data model for GPU instance
    /// </summary>
    public class GpuInstanceData
    {
        public string? InstanceId { get; set; }
        public string? Name { get; set; }
        public string Status { get; set; } = "unknown";
        public string Provider { get; set; } = "unknown";
        public string? AccountName { get; set; }
        public string? ProviderInstanceId { get; set; }
        public string? GpuModel { get; set; }
        public int GpuMemoryGb { get; set; }
        public int CpuCores { get; set; }
        public int RamGb { get; set; }
        public int DiskGb { get; set; }
        public string? IpAddress { get; set; }
        public int SshPort { get; set; } = 22;
        public decimal HourlyRate { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime? StartedAt { get; set; }
    }
}
