using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Workers page ViewModel
/// </summary>
public class WorkersViewModel : BaseViewModel
{
    private readonly ProcessOrchestrator _orchestrator;

    private int _totalWorkers;
    public int TotalWorkers
    {
        get => _totalWorkers;
        set => SetProperty(ref _totalWorkers, value);
    }

    private int _activeWorkers;
    public int ActiveWorkers
    {
        get => _activeWorkers;
        set => SetProperty(ref _activeWorkers, value);
    }

    private int _idleWorkers;
    public int IdleWorkers
    {
        get => _idleWorkers;
        set => SetProperty(ref _idleWorkers, value);
    }

    public ObservableCollection<WorkerDisplayItem> Workers { get; } = new();

    public IRelayCommand RefreshWorkersCommand { get; }

    public WorkersViewModel(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        Title = "Workers";

        RefreshWorkersCommand = new RelayCommand(RefreshWorkers);

        _orchestrator.WorkerStatusChanged += OnWorkerStatusChanged;
        _orchestrator.StatsUpdated += OnStatsUpdated;

        RefreshWorkers();
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        TotalWorkers = e.Stats.TotalWorkers;
        ActiveWorkers = e.Stats.ActiveWorkers;
        IdleWorkers = TotalWorkers - ActiveWorkers;
    }

    private void OnWorkerStatusChanged(object? sender, WorkerEventArgs e)
    {
        RefreshWorkers();
    }

    private void RefreshWorkers()
    {
        Workers.Clear();

        foreach (var worker in _orchestrator.GetWorkers())
        {
            Workers.Add(new WorkerDisplayItem
            {
                Id = worker.Id,
                Name = worker.Name,
                Platform = worker.Platform.ToString(),
                IsActive = worker.IsActive,
                TasksProcessed = worker.TasksProcessed,
                CurrentTaskId = worker.CurrentTask?.Id ?? "-",
                StartedAt = worker.StartedAt
            });
        }

        TotalWorkers = Workers.Count;
        ActiveWorkers = Workers.Count(w => w.IsActive);
        IdleWorkers = TotalWorkers - ActiveWorkers;
    }
}

public class WorkerDisplayItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public bool IsActive { get; set; }
    public int TasksProcessed { get; set; }
    public string CurrentTaskId { get; set; } = "";
    public DateTime StartedAt { get; set; }
}
