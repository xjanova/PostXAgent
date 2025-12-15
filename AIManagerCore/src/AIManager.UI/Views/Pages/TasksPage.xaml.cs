using System.Collections.ObjectModel;
using System.Windows.Controls;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.UI.Views.Pages;

public partial class TasksPage : Page
{
    private readonly ProcessOrchestrator _orchestrator;
    public ObservableCollection<TaskDisplayItem> Tasks { get; } = new();

    public TasksPage(ProcessOrchestrator orchestrator)
    {
        InitializeComponent();
        _orchestrator = orchestrator;
        DataContext = this;

        _orchestrator.TaskReceived += OnTaskReceived;
        _orchestrator.TaskCompleted += OnTaskCompleted;
        _orchestrator.TaskFailed += OnTaskFailed;
    }

    private void OnTaskReceived(object? sender, TaskEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Tasks.Insert(0, new TaskDisplayItem
            {
                Id = e.Task.Id ?? "",
                Type = e.Task.Type.ToString(),
                Platform = e.Task.Platform.ToString(),
                Status = "Queued",
                CreatedAt = e.Task.CreatedAt.ToString("HH:mm:ss")
            });

            while (Tasks.Count > 100)
                Tasks.RemoveAt(Tasks.Count - 1);
        });
    }

    private void OnTaskCompleted(object? sender, TaskEventArgs e)
    {
        UpdateTaskStatus(e.Task.Id, "Completed");
    }

    private void OnTaskFailed(object? sender, TaskEventArgs e)
    {
        UpdateTaskStatus(e.Task.Id, "Failed");
    }

    private void UpdateTaskStatus(string? taskId, string status)
    {
        if (taskId == null) return;

        Dispatcher.Invoke(() =>
        {
            var task = Tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                task.Status = status;
            }
        });
    }

    public class TaskDisplayItem
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Platform { get; set; } = "";
        public string Status { get; set; } = "";
        public string CreatedAt { get; set; } = "";
    }
}
