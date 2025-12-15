using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Dashboard page ViewModel
/// </summary>
public partial class DashboardViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    [ObservableProperty]
    private int _totalCores;

    [ObservableProperty]
    private int _activeWorkers;

    [ObservableProperty]
    private long _tasksCompleted;

    [ObservableProperty]
    private long _tasksFailed;

    [ObservableProperty]
    private long _tasksQueued;

    [ObservableProperty]
    private double _tasksPerSecond;

    [ObservableProperty]
    private string _uptime = "00:00:00";

    public ObservableCollection<TaskDisplayItem> RecentTasks { get; } = new();

    public DashboardViewModel(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        Title = "Dashboard";

        TotalCores = _orchestrator.TotalCores;

        _orchestrator.StatsUpdated += OnStatsUpdated;
        _orchestrator.TaskCompleted += OnTaskCompleted;
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        var stats = e.Stats;
        TotalCores = stats.TotalWorkers;
        ActiveWorkers = stats.ActiveWorkers;
        TasksCompleted = stats.TasksCompleted;
        TasksFailed = stats.TasksFailed;
        TasksQueued = stats.TasksQueued;
        TasksPerSecond = stats.TasksPerSecond;
        Uptime = FormatUptime(stats.Uptime);
    }

    private void OnTaskCompleted(object? sender, TaskEventArgs e)
    {
        var task = e.Task;
        RecentTasks.Insert(0, new TaskDisplayItem
        {
            Id = task.Id ?? "",
            Type = task.Type.ToString(),
            Platform = task.Platform.ToString(),
            Status = task.Status.ToString(),
            CompletedAt = DateTime.Now
        });

        // Keep only last 50 items
        while (RecentTasks.Count > 50)
        {
            RecentTasks.RemoveAt(RecentTasks.Count - 1);
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}";
        return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }
}

public class TaskDisplayItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CompletedAt { get; set; }
}
