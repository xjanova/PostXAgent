using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// Tasks ViewModel - Task management
/// </summary>
public partial class TasksViewModel : BaseViewModel
{
    private readonly IAIManagerApiService _apiService;
    private readonly ISignalRService _signalRService;

    [ObservableProperty]
    private TaskItem? _selectedTask;

    [ObservableProperty]
    private string _filterStatus = "ทั้งหมด";

    public ObservableCollection<TaskItem> Tasks { get; } = new();
    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "ทั้งหมด",
        "รอดำเนินการ",
        "กำลังทำงาน",
        "สำเร็จ",
        "ล้มเหลว"
    };

    public TasksViewModel(IAIManagerApiService apiService, ISignalRService signalRService)
    {
        _apiService = apiService;
        _signalRService = signalRService;

        Title = "งานทั้งหมด";

        _signalRService.TaskUpdated += OnTaskUpdated;
    }

    [RelayCommand]
    private async Task LoadTasksAsync()
    {
        await ExecuteAsync(async () =>
        {
            var response = await _apiService.GetTasksAsync(1, 100);
            if (response.Success && response.Data != null)
            {
                Tasks.Clear();
                var filtered = FilterTasks(response.Data);
                foreach (var task in filtered)
                {
                    Tasks.Add(task);
                }
            }
        }, "ไม่สามารถโหลดรายการงานได้");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadTasksAsync();
    }

    [RelayCommand]
    private async Task CancelTaskAsync(TaskItem task)
    {
        if (task.Status != Models.TaskStatus.Pending && task.Status != Models.TaskStatus.Running)
        {
            ShowMessage("ไม่สามารถยกเลิกได้", "สามารถยกเลิกได้เฉพาะงานที่รอดำเนินการหรือกำลังทำงานเท่านั้น");
            return;
        }

        var confirmed = await ConfirmAsync("ยืนยันการยกเลิก", $"คุณต้องการยกเลิกงาน {task.TypeText} หรือไม่?");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var response = await _apiService.CancelTaskAsync(task.Id!);
            if (response.Success)
            {
                task.Status = Models.TaskStatus.Cancelled;
                await LoadTasksAsync();
            }
            else
            {
                ShowError(response.Message ?? "ไม่สามารถยกเลิกงานได้");
            }
        });
    }

    [RelayCommand]
    private async Task ViewTaskDetailsAsync(TaskItem task)
    {
        SelectedTask = task;

        var details = $"ประเภท: {task.TypeText}\n" +
                     $"แพลตฟอร์ม: {task.Platform}\n" +
                     $"สถานะ: {task.StatusText}\n" +
                     $"สร้างเมื่อ: {task.CreatedAt:dd/MM/yyyy HH:mm}\n";

        if (task.StartedAt.HasValue)
            details += $"เริ่มเมื่อ: {task.StartedAt:dd/MM/yyyy HH:mm}\n";

        if (task.CompletedAt.HasValue)
            details += $"เสร็จเมื่อ: {task.CompletedAt:dd/MM/yyyy HH:mm}\n";

        details += $"ลองใหม่: {task.Retries}/{task.MaxRetries}";

        ShowMessage("รายละเอียดงาน", details);
    }

    partial void OnFilterStatusChanged(string value)
    {
        _ = LoadTasksAsync();
    }

    private IEnumerable<TaskItem> FilterTasks(List<TaskItem> tasks)
    {
        return FilterStatus switch
        {
            "รอดำเนินการ" => tasks.Where(t => t.Status == Models.TaskStatus.Pending || t.Status == Models.TaskStatus.Queued),
            "กำลังทำงาน" => tasks.Where(t => t.Status == Models.TaskStatus.Running),
            "สำเร็จ" => tasks.Where(t => t.Status == Models.TaskStatus.Completed),
            "ล้มเหลว" => tasks.Where(t => t.Status == Models.TaskStatus.Failed || t.Status == Models.TaskStatus.Cancelled),
            _ => tasks
        };
    }

    private void OnTaskUpdated(object? sender, TaskItem task)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = Tasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                var index = Tasks.IndexOf(existing);
                Tasks[index] = task;
            }
            else
            {
                Tasks.Insert(0, task);
            }
        });
    }
}
