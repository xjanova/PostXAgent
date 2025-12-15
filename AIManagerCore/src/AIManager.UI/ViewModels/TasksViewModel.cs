using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Tasks page ViewModel
/// </summary>
public partial class TasksViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    [ObservableProperty]
    private long _totalTasks;

    [ObservableProperty]
    private long _completedTasks;

    [ObservableProperty]
    private long _failedTasks;

    [ObservableProperty]
    private long _queuedTasks;

    [ObservableProperty]
    private string _filterPlatform = "All";

    [ObservableProperty]
    private string _filterStatus = "All";

    public ObservableCollection<TaskListItem> Tasks { get; } = new();
    public ObservableCollection<string> Platforms { get; } = new() { "All", "Facebook", "Instagram", "TikTok", "Twitter", "LINE", "YouTube", "Threads", "LinkedIn", "Pinterest" };
    public ObservableCollection<string> Statuses { get; } = new() { "All", "Queued", "Running", "Completed", "Failed" };

    public TasksViewModel(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        Title = "Tasks";

        _orchestrator.TaskReceived += OnTaskReceived;
        _orchestrator.TaskCompleted += OnTaskCompleted;
        _orchestrator.TaskFailed += OnTaskFailed;
        _orchestrator.StatsUpdated += OnStatsUpdated;
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        var stats = e.Stats;
        TotalTasks = stats.TasksQueued + stats.TasksCompleted + stats.TasksFailed;
        CompletedTasks = stats.TasksCompleted;
        FailedTasks = stats.TasksFailed;
        QueuedTasks = stats.TasksQueued;
    }

    private void OnTaskReceived(object? sender, TaskEventArgs e)
    {
        AddTask(e.Task);
    }

    private void OnTaskCompleted(object? sender, TaskEventArgs e)
    {
        UpdateTask(e.Task);
    }

    private void OnTaskFailed(object? sender, TaskEventArgs e)
    {
        UpdateTask(e.Task);
    }

    private void AddTask(TaskItem task)
    {
        var item = new TaskListItem
        {
            Id = task.Id ?? "",
            Type = task.Type.ToString(),
            Platform = task.Platform.ToString(),
            Status = task.Status.ToString(),
            CreatedAt = task.CreatedAt,
            Prompt = task.Payload?.Prompt ?? ""
        };

        Tasks.Insert(0, item);

        // Keep only last 200 items
        while (Tasks.Count > 200)
        {
            Tasks.RemoveAt(Tasks.Count - 1);
        }
    }

    private void UpdateTask(TaskItem task)
    {
        var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
        if (existing != null)
        {
            existing.Status = task.Status.ToString();
            existing.CompletedAt = task.CompletedAt;
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        FilterPlatform = "All";
        FilterStatus = "All";
    }

    [RelayCommand]
    private async Task SubmitTestTaskAsync()
    {
        var task = new TaskItem
        {
            Type = TaskType.GenerateContent,
            Platform = SocialPlatform.Facebook,
            Payload = new TaskPayload
            {
                Prompt = "Test promotional content",
                BrandInfo = new BrandInfo
                {
                    Name = "Test Brand",
                    Industry = "Technology"
                }
            }
        };

        await _orchestrator.SubmitTaskAsync(task);
    }
}

public class TaskListItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Status { get; set; } = "";
    public string Prompt { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
