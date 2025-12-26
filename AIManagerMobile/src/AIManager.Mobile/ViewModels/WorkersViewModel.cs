using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// Workers ViewModel - Worker management
/// </summary>
public partial class WorkersViewModel : BaseViewModel
{
    private readonly IAIManagerApiService _apiService;
    private readonly ISignalRService _signalRService;

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _idleCount;

    [ObservableProperty]
    private int _errorCount;

    public ObservableCollection<WorkerInfo> Workers { get; } = new();

    public WorkersViewModel(IAIManagerApiService apiService, ISignalRService signalRService)
    {
        _apiService = apiService;
        _signalRService = signalRService;

        Title = "Workers";

        _signalRService.WorkerUpdated += OnWorkerUpdated;
    }

    [RelayCommand]
    private async Task LoadWorkersAsync()
    {
        await ExecuteAsync(async () =>
        {
            var response = await _apiService.GetWorkersAsync();
            if (response.Success && response.Data != null)
            {
                Workers.Clear();
                foreach (var worker in response.Data)
                {
                    Workers.Add(worker);
                }

                UpdateCounts();
            }
        }, "ไม่สามารถโหลดรายการ Workers ได้");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadWorkersAsync();
    }

    [RelayCommand]
    private async Task StartWorkerAsync(WorkerInfo worker)
    {
        await ExecuteAsync(async () =>
        {
            var response = await _apiService.StartWorkerAsync(worker.Id);
            if (response.Success)
            {
                worker.Status = WorkerStatus.Working;
                await LoadWorkersAsync();
            }
            else
            {
                ShowError(response.Message ?? "ไม่สามารถเริ่ม Worker ได้");
            }
        });
    }

    [RelayCommand]
    private async Task PauseWorkerAsync(WorkerInfo worker)
    {
        await ExecuteAsync(async () =>
        {
            var response = await _apiService.PauseWorkerAsync(worker.Id);
            if (response.Success)
            {
                worker.Status = WorkerStatus.Paused;
                await LoadWorkersAsync();
            }
            else
            {
                ShowError(response.Message ?? "ไม่สามารถหยุด Worker ชั่วคราวได้");
            }
        });
    }

    [RelayCommand]
    private async Task StopWorkerAsync(WorkerInfo worker)
    {
        var confirmed = await ConfirmAsync("ยืนยันการหยุด", $"คุณต้องการหยุด {worker.Name} หรือไม่?");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var response = await _apiService.StopWorkerAsync(worker.Id);
            if (response.Success)
            {
                worker.Status = WorkerStatus.Offline;
                await LoadWorkersAsync();
            }
            else
            {
                ShowError(response.Message ?? "ไม่สามารถหยุด Worker ได้");
            }
        });
    }

    [RelayCommand]
    private async Task StartAllWorkersAsync()
    {
        var confirmed = await ConfirmAsync("เริ่ม Workers ทั้งหมด", "คุณต้องการเริ่ม Workers ทั้งหมดหรือไม่?");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            foreach (var worker in Workers.Where(w => w.Status != WorkerStatus.Working))
            {
                await _apiService.StartWorkerAsync(worker.Id);
            }
            await LoadWorkersAsync();
        });
    }

    [RelayCommand]
    private async Task StopAllWorkersAsync()
    {
        var confirmed = await ConfirmAsync("หยุด Workers ทั้งหมด", "คุณต้องการหยุด Workers ทั้งหมดหรือไม่?");
        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            foreach (var worker in Workers.Where(w => w.Status == WorkerStatus.Working))
            {
                await _apiService.StopWorkerAsync(worker.Id);
            }
            await LoadWorkersAsync();
        });
    }

    private void UpdateCounts()
    {
        ActiveCount = Workers.Count(w => w.Status == WorkerStatus.Working);
        IdleCount = Workers.Count(w => w.Status == WorkerStatus.Idle);
        ErrorCount = Workers.Count(w => w.Status == WorkerStatus.Error);
    }

    private void OnWorkerUpdated(object? sender, WorkerInfo worker)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = Workers.FirstOrDefault(w => w.Id == worker.Id);
            if (existing != null)
            {
                var index = Workers.IndexOf(existing);
                Workers[index] = worker;
            }
            else
            {
                Workers.Add(worker);
            }
            UpdateCounts();
        });
    }
}
