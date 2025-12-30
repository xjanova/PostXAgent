using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Workers;
using AIManager.Core.Services;

namespace AIManager.UI.ViewModels;

/// <summary>
/// ViewModel for WorkerWebViewPage
/// จัดการ state และ commands สำหรับหน้า Worker WebView
/// </summary>
public class WorkerWebViewViewModel : BaseViewModel
{
    private readonly WorkerManager _workerManager;
    private readonly WorkerSessionTransferService? _sessionTransferService;

    #region Observable Properties

    private ManagedWorker? _selectedWorker;
    public ManagedWorker? SelectedWorker
    {
        get => _selectedWorker;
        set => SetProperty(ref _selectedWorker, value);
    }

    private string _selectedWorkerName = "No worker selected";
    public string SelectedWorkerName
    {
        get => _selectedWorkerName;
        set => SetProperty(ref _selectedWorkerName, value);
    }

    private string _currentUrl = "";
    public string CurrentUrl
    {
        get => _currentUrl;
        set => SetProperty(ref _currentUrl, value);
    }

    private string _statusText = "Idle";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private WorkerViewMode _currentViewMode = WorkerViewMode.Headless;
    public WorkerViewMode CurrentViewMode
    {
        get => _currentViewMode;
        set => SetProperty(ref _currentViewMode, value);
    }

    private bool _isRecording;
    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    private bool _hasSelectedWorker;
    public bool HasSelectedWorker
    {
        get => _hasSelectedWorker;
        set => SetProperty(ref _hasSelectedWorker, value);
    }

    private bool _canViewOnly;
    public bool CanViewOnly
    {
        get => _canViewOnly;
        set => SetProperty(ref _canViewOnly, value);
    }

    private bool _canAssist;
    public bool CanAssist
    {
        get => _canAssist;
        set => SetProperty(ref _canAssist, value);
    }

    private bool _canTakeControl;
    public bool CanTakeControl
    {
        get => _canTakeControl;
        set => SetProperty(ref _canTakeControl, value);
    }

    private bool _showRecordingPanel;
    public bool ShowRecordingPanel
    {
        get => _showRecordingPanel;
        set => SetProperty(ref _showRecordingPanel, value);
    }

    private bool _showLearningOverlay;
    public bool ShowLearningOverlay
    {
        get => _showLearningOverlay;
        set => SetProperty(ref _showLearningOverlay, value);
    }

    private bool _showViewOnlyOverlay;
    public bool ShowViewOnlyOverlay
    {
        get => _showViewOnlyOverlay;
        set => SetProperty(ref _showViewOnlyOverlay, value);
    }

    private int _recordedStepsCount;
    public int RecordedStepsCount
    {
        get => _recordedStepsCount;
        set => SetProperty(ref _recordedStepsCount, value);
    }

    #endregion

    #region Collections

    public ObservableCollection<WorkerWebViewDisplayItem> WorkersNeedingHelp { get; } = new();
    public ObservableCollection<WorkerWebViewDisplayItem> ActiveWorkers { get; } = new();
    public ObservableCollection<RecordedStep> RecordedSteps { get; } = new();

    public bool HasWorkersNeedingHelp => WorkersNeedingHelp.Count > 0;

    #endregion

    #region Commands

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand<string> SelectWorkerCommand { get; }
    public IRelayCommand ViewOnlyCommand { get; }
    public IRelayCommand AssistCommand { get; }
    public IRelayCommand TakeControlCommand { get; }
    public IRelayCommand DoneTeachingCommand { get; }
    public IRelayCommand SaveWorkflowCommand { get; }
    public IRelayCommand CancelRecordingCommand { get; }

    #endregion

    public WorkerWebViewViewModel(WorkerManager workerManager, WorkerSessionTransferService? sessionTransferService = null)
    {
        _workerManager = workerManager;
        _sessionTransferService = sessionTransferService;

        Title = "Worker WebView";

        // Initialize commands
        RefreshCommand = new RelayCommand(RefreshWorkerList);
        SelectWorkerCommand = new RelayCommand<string>(SelectWorkerById);
        ViewOnlyCommand = new RelayCommand(SetViewOnlyMode, () => HasSelectedWorker);
        AssistCommand = new RelayCommand(StartAssisting, () => HasSelectedWorker);
        TakeControlCommand = new RelayCommand(TakeControl, () => HasSelectedWorker);
        DoneTeachingCommand = new RelayCommand(async () => await DoneTeachingAsync(), () => IsRecording);
        SaveWorkflowCommand = new RelayCommand(async () => await SaveWorkflowAsync(), () => RecordedSteps.Count > 0);
        CancelRecordingCommand = new RelayCommand(CancelRecording, () => IsRecording);

        // Subscribe to events
        _workerManager.WorkerHelpRequested += OnWorkerHelpRequested;
        _workerManager.WorkerViewModeChanged += OnWorkerViewModeChanged;
        _workerManager.WorkerStateChanged += OnWorkerStateChanged;

        // Initial load
        RefreshWorkerList();
    }

    #region Worker List Management

    private void RefreshWorkerList()
    {
        WorkersNeedingHelp.Clear();
        ActiveWorkers.Clear();

        foreach (var worker in _workerManager.GetAllWorkers())
        {
            var displayItem = new WorkerWebViewDisplayItem
            {
                Id = worker.Id,
                Name = worker.Name,
                Platform = worker.Platform,
                ViewMode = worker.ViewMode,
                State = worker.State,
                CurrentUrl = worker.ViewContext?.CurrentUrl,
                HelpReason = worker.ViewContext?.HelpReason,
                HelpRequestedAt = worker.ViewContext?.HelpRequestedAt,
                RecordedStepsCount = worker.ViewContext?.RecordedSteps.Count ?? 0,
                LastActivityAt = worker.LastActivityAt
            };

            if (worker.ViewMode == WorkerViewMode.NeedsHelp)
            {
                WorkersNeedingHelp.Add(displayItem);
            }
            else if (worker.State == WorkerState.Running || worker.State == WorkerState.Paused)
            {
                ActiveWorkers.Add(displayItem);
            }
        }

        OnPropertyChanged(nameof(HasWorkersNeedingHelp));
    }

    #endregion

    #region Worker Selection

    private void SelectWorkerById(string? workerId)
    {
        if (string.IsNullOrEmpty(workerId)) return;

        var worker = _workerManager.GetWorker(workerId);
        if (worker != null)
        {
            SelectWorker(worker);
        }
    }

    public void SelectWorker(ManagedWorker worker)
    {
        SelectedWorker = worker;
        SelectedWorkerName = worker.Name;
        CurrentUrl = worker.ViewContext?.CurrentUrl ?? "";
        CurrentViewMode = worker.ViewMode;
        HasSelectedWorker = true;

        UpdateUIForViewMode(worker.ViewMode);
    }

    private void UpdateUIForViewMode(WorkerViewMode mode)
    {
        CurrentViewMode = mode;

        // Reset states
        ShowRecordingPanel = false;
        ShowLearningOverlay = false;
        ShowViewOnlyOverlay = true;
        CanViewOnly = true;
        CanAssist = true;
        CanTakeControl = true;

        switch (mode)
        {
            case WorkerViewMode.Headless:
                StatusText = "Headless";
                break;

            case WorkerViewMode.Viewing:
                StatusText = "View Only";
                break;

            case WorkerViewMode.NeedsHelp:
                StatusText = "Needs Help";
                break;

            case WorkerViewMode.HumanControl:
                StatusText = "Recording";
                ShowRecordingPanel = true;
                ShowViewOnlyOverlay = false;
                IsRecording = true;
                break;

            case WorkerViewMode.Learning:
                StatusText = "Learning";
                ShowLearningOverlay = true;
                CanViewOnly = false;
                CanAssist = false;
                CanTakeControl = false;
                break;

            case WorkerViewMode.Resuming:
                StatusText = "Resuming";
                ShowLearningOverlay = true;
                break;
        }
    }

    #endregion

    #region Control Actions

    private void SetViewOnlyMode()
    {
        if (SelectedWorker == null) return;
        _workerManager.SetWorkerViewMode(SelectedWorker.Id, WorkerViewMode.Viewing);
    }

    private void StartAssisting()
    {
        if (SelectedWorker == null) return;
        StartRecording();
    }

    private void TakeControl()
    {
        if (SelectedWorker == null) return;
        StartRecording();
    }

    private void StartRecording()
    {
        if (SelectedWorker == null) return;

        IsRecording = true;
        RecordedSteps.Clear();
        RecordedStepsCount = 0;

        _workerManager.StartRecording(SelectedWorker.Id);
    }

    private async Task DoneTeachingAsync()
    {
        if (SelectedWorker == null) return;

        StopRecording();

        try
        {
            await _workerManager.ResumeWorkerFromHelpAsync(SelectedWorker.Id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Resume error: {ex.Message}");
        }
    }

    private async Task SaveWorkflowAsync()
    {
        await DoneTeachingAsync();
    }

    private void CancelRecording()
    {
        if (SelectedWorker == null) return;

        StopRecording();
        RecordedSteps.Clear();
        RecordedStepsCount = 0;

        _workerManager.SetWorkerViewMode(SelectedWorker.Id, WorkerViewMode.Viewing);
    }

    private void StopRecording()
    {
        IsRecording = false;
    }

    #endregion

    #region Event Handlers

    private void OnWorkerHelpRequested(object? sender, WorkerHelpRequestedEventArgs e)
    {
        RefreshWorkerList();

        // Auto-select worker that needs help
        SelectWorker(e.Worker);
    }

    private void OnWorkerViewModeChanged(object? sender, WorkerViewModeChangedEventArgs e)
    {
        RefreshWorkerList();

        if (SelectedWorker?.Id == e.WorkerId)
        {
            UpdateUIForViewMode(e.NewMode);
        }
    }

    private void OnWorkerStateChanged(object? sender, WorkerStateChangedEventArgs e)
    {
        RefreshWorkerList();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Add a recorded step (called from code-behind when receiving WebView messages)
    /// </summary>
    public void AddRecordedStep(RecordedStep step)
    {
        step.StepNumber = RecordedSteps.Count + 1;
        RecordedSteps.Add(step);
        RecordedStepsCount = RecordedSteps.Count;

        if (SelectedWorker != null)
        {
            _workerManager.AddRecordedStep(SelectedWorker.Id, step);
        }
    }

    /// <summary>
    /// Update current URL from navigation
    /// </summary>
    public void UpdateCurrentUrl(string url)
    {
        CurrentUrl = url;

        if (SelectedWorker?.ViewContext != null)
        {
            SelectedWorker.ViewContext.CurrentUrl = url;
        }
    }

    #endregion
}
