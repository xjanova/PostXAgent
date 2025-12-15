using System.Collections.ObjectModel;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Dashboard page ViewModel
/// </summary>
public class DashboardViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    private int _totalCores;
    public int TotalCores
    {
        get => _totalCores;
        set => SetProperty(ref _totalCores, value);
    }

    private int _activeWorkers;
    public int ActiveWorkers
    {
        get => _activeWorkers;
        set => SetProperty(ref _activeWorkers, value);
    }

    private long _tasksCompleted;
    public long TasksCompleted
    {
        get => _tasksCompleted;
        set => SetProperty(ref _tasksCompleted, value);
    }

    private long _tasksFailed;
    public long TasksFailed
    {
        get => _tasksFailed;
        set => SetProperty(ref _tasksFailed, value);
    }

    private long _tasksQueued;
    public long TasksQueued
    {
        get => _tasksQueued;
        set => SetProperty(ref _tasksQueued, value);
    }

    private double _tasksPerSecond;
    public double TasksPerSecond
    {
        get => _tasksPerSecond;
        set => SetProperty(ref _tasksPerSecond, value);
    }

    private string _uptime = "00:00:00";
    public string Uptime
    {
        get => _uptime;
        set => SetProperty(ref _uptime, value);
    }

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
