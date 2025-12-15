using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Tasks page ViewModel
/// </summary>
public class TasksViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    private long _totalTasks;
    public long TotalTasks
    {
        get => _totalTasks;
        set => SetProperty(ref _totalTasks, value);
    }

    private long _completedTasks;
    public long CompletedTasks
    {
        get => _completedTasks;
        set => SetProperty(ref _completedTasks, value);
    }

    private long _failedTasks;
    public long FailedTasks
    {
        get => _failedTasks;
        set => SetProperty(ref _failedTasks, value);
    }

    private long _queuedTasks;
    public long QueuedTasks
    {
        get => _queuedTasks;
        set => SetProperty(ref _queuedTasks, value);
    }

    private string _filterPlatform = "All";
    public string FilterPlatform
    {
        get => _filterPlatform;
        set => SetProperty(ref _filterPlatform, value);
    }

    private string _filterStatus = "All";
    public string FilterStatus
    {
        get => _filterStatus;
        set => SetProperty(ref _filterStatus, value);
    }

    public ObservableCollection<TaskListItem> Tasks { get; } = new();
    public ObservableCollection<string> Platforms { get; } = new() { "All", "Facebook", "Instagram", "TikTok", "Twitter", "LINE", "YouTube", "Threads", "LinkedIn", "Pinterest" };
    public ObservableCollection<string> Statuses { get; } = new() { "All", "Queued", "Running", "Completed", "Failed" };

    public IRelayCommand ClearFiltersCommand { get; }
    public IAsyncRelayCommand SubmitTestTaskCommand { get; }

    public TasksViewModel(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        Title = "Tasks";

        ClearFiltersCommand = new RelayCommand(ClearFilters);
        SubmitTestTaskCommand = new AsyncRelayCommand(SubmitTestTaskAsync);

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

    private void ClearFilters()
    {
        FilterPlatform = "All";
        FilterStatus = "All";
    }

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
