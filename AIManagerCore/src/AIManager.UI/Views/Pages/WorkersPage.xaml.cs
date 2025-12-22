using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AIManager.UI.Views.Pages;

public partial class WorkersPage : Page
{
    private readonly HttpClient _httpClient;
    private readonly DispatcherTimer _updateTimer;
    private readonly string _apiBaseUrl;
    private bool _isInitialized = false;

    public WorkersPage()
    {
        InitializeComponent();

        _apiBaseUrl = "http://localhost:5000";
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _updateTimer.Tick += async (s, e) =>
        {
            if (_isInitialized)
                await RefreshDashboardAsync();
        };

        Loaded += async (s, e) =>
        {
            _isInitialized = true;
            await RefreshDashboardAsync();
            _updateTimer.Start();
        };

        Unloaded += (s, e) =>
        {
            _updateTimer.Stop();
            _isInitialized = false;
        };
    }

    #region Data Refresh

    private async Task RefreshDashboardAsync()
    {
        if (_httpClient == null || !_isInitialized)
            return;

        try
        {
            var response = await _httpClient.GetAsync($"{_apiBaseUrl}/api/workers/dashboard");

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<DashboardResponse>();
                if (result?.Success == true && result.Dashboard != null)
                {
                    UpdateUI(result.Dashboard);
                }
                else
                {
                    ShowDemoData();
                }
            }
            else
            {
                ShowDemoData();
            }
        }
        catch (Exception)
        {
            // API not available - show demo data
            ShowDemoData();
        }
    }

    private void UpdateUI(DashboardData data)
    {
        // System Stats
        TxtCpuUsage.Text = $"{data.SystemStats.CpuUsagePercent:F1}%";
        PbCpu.Value = data.SystemStats.CpuUsagePercent;
        TxtMemory.Text = $"{data.SystemStats.MemoryUsageMB} MB";
        TxtCores.Text = data.SystemStats.TotalCores.ToString();

        // Worker Stats
        TxtTotalWorkers.Text = data.WorkerStats.Total.ToString();
        TxtRunningWorkers.Text = data.WorkerStats.Running.ToString();
        TxtPausedWorkers.Text = data.WorkerStats.Paused.ToString();
        TxtStoppedWorkers.Text = data.WorkerStats.Stopped.ToString();
        TxtErrorWorkers.Text = data.WorkerStats.Error.ToString();

        // Calculate tasks per second (from success rate display)
        TxtTasksPerSec.Text = $"{data.WorkerStats.SuccessRate:F1}%";

        // Workers List
        var filteredWorkers = FilterWorkers(data.Workers);
        WorkersList.ItemsSource = filteredWorkers;

        // Recent Reports
        ReportsList.ItemsSource = data.RecentReports;
    }

    private void ShowDemoData()
    {
        TxtCpuUsage.Text = "0%";
        TxtMemory.Text = "0 MB";
        TxtCores.Text = Environment.ProcessorCount.ToString();
        TxtTasksPerSec.Text = "0";
        TxtTotalWorkers.Text = "0";
        TxtRunningWorkers.Text = "0";
        TxtPausedWorkers.Text = "0";
        TxtStoppedWorkers.Text = "0";
        TxtErrorWorkers.Text = "0";
    }

    private List<WorkerDto> FilterWorkers(List<WorkerDto> workers)
    {
        var filtered = workers.AsEnumerable();

        // Platform filter
        if (CbPlatformFilter.SelectedItem is ComboBoxItem platformItem &&
            platformItem.Content?.ToString() != "All Platforms")
        {
            var platform = platformItem.Content?.ToString();
            filtered = filtered.Where(w => w.Platform == platform);
        }

        // State filter
        if (CbStateFilter.SelectedItem is ComboBoxItem stateItem &&
            stateItem.Content?.ToString() != "All States")
        {
            var state = stateItem.Content?.ToString();
            filtered = filtered.Where(w => w.State == state);
        }

        return filtered.ToList();
    }

    #endregion

    #region Button Handlers

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDashboardAsync();
    }

    private async void BtnPauseAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _httpClient.PostAsync($"{_apiBaseUrl}/api/workers/pause-all", null);
            await RefreshDashboardAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to pause workers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnResumeAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _httpClient.PostAsync($"{_apiBaseUrl}/api/workers/resume-all", null);
            await RefreshDashboardAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to resume workers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnPauseWorker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string workerId)
        {
            try
            {
                await _httpClient.PostAsync($"{_apiBaseUrl}/api/workers/{workerId}/pause", null);
                await RefreshDashboardAsync();
            }
            catch { }
        }
    }

    private async void BtnResumeWorker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string workerId)
        {
            try
            {
                await _httpClient.PostAsync($"{_apiBaseUrl}/api/workers/{workerId}/resume", null);
                await RefreshDashboardAsync();
            }
            catch { }
        }
    }

    private async void BtnStopWorker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string workerId)
        {
            var result = MessageBox.Show(
                "Are you sure you want to stop this worker?",
                "Confirm Stop",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _httpClient.PostAsync($"{_apiBaseUrl}/api/workers/{workerId}/stop", null);
                    await RefreshDashboardAsync();
                }
                catch { }
            }
        }
    }

    private void BtnViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string workerId)
        {
            // Show worker details dialog
            var worker = (WorkersList.ItemsSource as IEnumerable<WorkerDto>)?
                .FirstOrDefault(w => w.Id == workerId);

            if (worker != null)
            {
                var details = $"Worker: {worker.Name}\n" +
                              $"Platform: {worker.Platform}\n" +
                              $"State: {worker.State}\n" +
                              $"Tasks Processed: {worker.TasksProcessed}\n" +
                              $"Success Rate: {worker.SuccessRate:F1}%\n" +
                              $"CPU Core: {worker.PreferredCore}\n" +
                              $"Current Task: {worker.CurrentTask ?? "None"}\n" +
                              $"Last Error: {worker.LastError ?? "None"}";

                MessageBox.Show(details, $"Worker Details - {worker.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void CbPlatformFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Trigger refresh with new filter
        _ = RefreshDashboardAsync();
    }

    private void CbStateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Trigger refresh with new filter
        _ = RefreshDashboardAsync();
    }

    private void WorkersList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Handle worker selection
    }

    #endregion
}

#region DTOs

public class DashboardResponse
{
    public bool Success { get; set; }
    public DashboardData? Dashboard { get; set; }
}

public class DashboardData
{
    public SystemStatsDto SystemStats { get; set; } = new();
    public WorkerStatsDto WorkerStats { get; set; } = new();
    public List<PlatformStatsDto> PlatformStats { get; set; } = new();
    public List<WorkerDto> Workers { get; set; } = new();
    public List<ReportDto> RecentReports { get; set; } = new();
}

public class SystemStatsDto
{
    public int TotalCores { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long UptimeSeconds { get; set; }
}

public class WorkerStatsDto
{
    public int Total { get; set; }
    public int Running { get; set; }
    public int Paused { get; set; }
    public int Stopped { get; set; }
    public int Error { get; set; }
    public long TasksProcessed { get; set; }
    public double SuccessRate { get; set; }
}

public class PlatformStatsDto
{
    public string Platform { get; set; } = "";
    public int WorkerCount { get; set; }
    public int ActiveCount { get; set; }
    public long TasksProcessed { get; set; }
}

public class WorkerDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string State { get; set; } = "";
    public string StateEmoji { get; set; } = "";
    public string? CurrentTask { get; set; }
    public double ProgressPercent { get; set; }
    public string? ProgressDetails { get; set; }
    public int TasksProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public int PreferredCore { get; set; }
    public long UptimeSeconds { get; set; }
    public DateTime LastActivityAt { get; set; }
    public string? LastError { get; set; }
}

public class ReportDto
{
    public string Id { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string WorkerName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public long ProcessingTimeMs { get; set; }
    public DateTime ReportedAt { get; set; }
}

#endregion
