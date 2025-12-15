using System.Windows.Controls;
using System.Windows.Threading;
using AIManager.Core.Orchestrator;

namespace AIManager.UI.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly DispatcherTimer _updateTimer;

    public DashboardPage(ProcessOrchestrator orchestrator)
    {
        InitializeComponent();
        _orchestrator = orchestrator;

        // Subscribe to stats updates
        _orchestrator.StatsUpdated += OnStatsUpdated;

        // Update timer for real-time display
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) => UpdateDisplay();
        _updateTimer.Start();

        // Initial display
        UpdateDisplay();
        LoadSampleTasks();
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var stats = e.Stats;
            TxtCpuCores.Text = stats.TotalWorkers.ToString();
            TxtActiveWorkers.Text = stats.ActiveWorkers.ToString();
            TxtTasksCompleted.Text = stats.TasksCompleted.ToString("N0");
            TxtTasksPerSecond.Text = $"{stats.TasksPerSecond:F1}/sec";
            TxtUptime.Text = FormatUptime(stats.Uptime);
        });
    }

    private void UpdateDisplay()
    {
        if (_orchestrator.IsRunning)
        {
            var stats = _orchestrator.Stats;
            TxtCpuCores.Text = _orchestrator.TotalCores.ToString();
            TxtActiveWorkers.Text = _orchestrator.ActiveWorkers.ToString();
            TxtTasksCompleted.Text = stats.TasksCompleted.ToString("N0");
            TxtTasksPerSecond.Text = $"{stats.TasksPerSecond:F1}/sec";
            TxtUptime.Text = FormatUptime(stats.Uptime);
        }
    }

    private void LoadSampleTasks()
    {
        var tasks = new[]
        {
            new TaskViewModel
            {
                TaskType = "Generate Content",
                Description = "Creating promotional post for Brand A",
                Platform = "Facebook",
                PlatformColor = "#1877F2",
                PlatformIcon = "Facebook",
                Status = "Completed",
                StatusColor = "#4CAF50",
                TimeAgo = "2s ago"
            },
            new TaskViewModel
            {
                TaskType = "Post Content",
                Description = "Publishing to Instagram feed",
                Platform = "Instagram",
                PlatformColor = "#DD2A7B",
                PlatformIcon = "Instagram",
                Status = "Running",
                StatusColor = "#2196F3",
                TimeAgo = "5s ago"
            },
            new TaskViewModel
            {
                TaskType = "Generate Image",
                Description = "Creating product image with DALL-E",
                Platform = "TikTok",
                PlatformColor = "#00F2EA",
                PlatformIcon = "Video",
                Status = "Queued",
                StatusColor = "#FF9800",
                TimeAgo = "10s ago"
            }
        };

        TasksList.ItemsSource = tasks;
    }

    private string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}";
        return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }
}

public class TaskViewModel
{
    public string TaskType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Platform { get; set; } = "";
    public string PlatformColor { get; set; } = "";
    public string PlatformIcon { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public string TimeAgo { get; set; } = "";
}
