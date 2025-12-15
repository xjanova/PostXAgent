using System.Windows.Controls;
using System.Windows.Threading;
using AIManager.Core.Orchestrator;

namespace AIManager.UI.Views.Pages;

public partial class WorkersPage : Page
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly DispatcherTimer _updateTimer;

    public WorkersPage(ProcessOrchestrator orchestrator)
    {
        InitializeComponent();
        _orchestrator = orchestrator;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _updateTimer.Tick += (s, e) => RefreshWorkers();
        _updateTimer.Start();

        RefreshWorkers();
    }

    private void RefreshWorkers()
    {
        var workers = _orchestrator.GetWorkers().Select(w => new WorkerDisplayItem
        {
            Id = w.Id,
            Name = w.Name,
            Platform = w.Platform.ToString(),
            IsActive = w.IsActive,
            TasksProcessed = w.TasksProcessed,
            CurrentTask = w.CurrentTask?.Id ?? "-"
        }).ToList();

        WorkersList.ItemsSource = workers;
        TxtTotalWorkers.Text = workers.Count.ToString();
        TxtActiveWorkers.Text = workers.Count(w => w.IsActive).ToString();
    }

    private class WorkerDisplayItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Platform { get; set; } = "";
        public bool IsActive { get; set; }
        public int TasksProcessed { get; set; }
        public string CurrentTask { get; set; } = "";
    }
}
