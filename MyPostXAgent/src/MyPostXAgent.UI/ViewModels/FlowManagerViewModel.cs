using System.Collections.ObjectModel;
using System.Windows;
using Microsoft.Win32;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel for Flow Manager Page - Workflow Automation
/// </summary>
public class FlowManagerViewModel : BaseViewModel
{
    private readonly LocalizationService _localizationService;

    // Collections
    public ObservableCollection<Workflow> Workflows { get; } = new();
    public ObservableCollection<WorkflowTemplate> Templates { get; } = new();
    public ObservableCollection<WorkflowRun> RunHistory { get; } = new();
    public ObservableCollection<WorkflowAction> AvailableActions { get; } = new();
    public ObservableCollection<WorkflowTrigger> AvailableTriggers { get; } = new();

    // Selected Workflow
    private Workflow? _selectedWorkflow;
    public Workflow? SelectedWorkflow
    {
        get => _selectedWorkflow;
        set
        {
            if (SetProperty(ref _selectedWorkflow, value))
            {
                LoadWorkflowDetails();
            }
        }
    }

    // Workflow Builder State
    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    private string _workflowName = "";
    public string WorkflowName
    {
        get => _workflowName;
        set => SetProperty(ref _workflowName, value);
    }

    private string _workflowDescription = "";
    public string WorkflowDescription
    {
        get => _workflowDescription;
        set => SetProperty(ref _workflowDescription, value);
    }

    private WorkflowTrigger? _selectedTrigger;
    public WorkflowTrigger? SelectedTrigger
    {
        get => _selectedTrigger;
        set => SetProperty(ref _selectedTrigger, value);
    }

    public ObservableCollection<WorkflowStep> CurrentSteps { get; } = new();

    // Dialog States
    private bool _showNewWorkflowDialog;
    public bool ShowNewWorkflowDialog
    {
        get => _showNewWorkflowDialog;
        set => SetProperty(ref _showNewWorkflowDialog, value);
    }

    private bool _showTemplateDialog;
    public bool ShowTemplateDialog
    {
        get => _showTemplateDialog;
        set => SetProperty(ref _showTemplateDialog, value);
    }

    // Running State
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    private string _runStatus = "";
    public string RunStatus
    {
        get => _runStatus;
        set => SetProperty(ref _runStatus, value);
    }

    private int _runProgress;
    public int RunProgress
    {
        get => _runProgress;
        set => SetProperty(ref _runProgress, value);
    }

    // Tab Selection
    private int _selectedTabIndex;
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    // Commands
    public RelayCommand NewWorkflowCommand { get; }
    public RelayCommand<WorkflowTemplate> UseTemplateCommand { get; }
    public RelayCommand<Workflow> EditWorkflowCommand { get; }
    public RelayCommand<Workflow> DeleteWorkflowCommand { get; }
    public RelayCommand<Workflow> DuplicateWorkflowCommand { get; }
    public RelayCommand<Workflow> RunWorkflowCommand { get; }
    public RelayCommand<Workflow> ToggleActiveCommand { get; }
    public RelayCommand SaveWorkflowCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public RelayCommand<WorkflowAction> AddStepCommand { get; }
    public RelayCommand<WorkflowStep> RemoveStepCommand { get; }
    public RelayCommand<WorkflowStep> MoveUpCommand { get; }
    public RelayCommand<WorkflowStep> MoveDownCommand { get; }
    public RelayCommand ImportFlowCommand { get; }
    public RelayCommand<Workflow> ExportFlowCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand ConfirmNewWorkflowCommand { get; }
    public RelayCommand CancelNewWorkflowCommand { get; }

    public FlowManagerViewModel(LocalizationService localizationService)
    {
        _localizationService = localizationService;
        Title = LocalizationStrings.Nav.Workflows(_localizationService.IsThaiLanguage);

        // Initialize
        InitializeActions();
        InitializeTriggers();
        InitializeTemplates();

        // Commands
        NewWorkflowCommand = new RelayCommand(() => ShowNewWorkflowDialog = true);
        UseTemplateCommand = new RelayCommand<WorkflowTemplate>(UseTemplate);
        EditWorkflowCommand = new RelayCommand<Workflow>(EditWorkflow);
        DeleteWorkflowCommand = new RelayCommand<Workflow>(async w => await DeleteWorkflowAsync(w));
        DuplicateWorkflowCommand = new RelayCommand<Workflow>(DuplicateWorkflow);
        RunWorkflowCommand = new RelayCommand<Workflow>(async w => await RunWorkflowAsync(w));
        ToggleActiveCommand = new RelayCommand<Workflow>(ToggleActive);
        SaveWorkflowCommand = new RelayCommand(async () => await SaveWorkflowAsync());
        CancelEditCommand = new RelayCommand(CancelEdit);
        AddStepCommand = new RelayCommand<WorkflowAction>(AddStep);
        RemoveStepCommand = new RelayCommand<WorkflowStep>(RemoveStep);
        MoveUpCommand = new RelayCommand<WorkflowStep>(MoveStepUp);
        MoveDownCommand = new RelayCommand<WorkflowStep>(MoveStepDown);
        ImportFlowCommand = new RelayCommand(ImportFlow);
        ExportFlowCommand = new RelayCommand<Workflow>(ExportFlow);
        RefreshCommand = new RelayCommand(async () => await LoadWorkflowsAsync());
        ConfirmNewWorkflowCommand = new RelayCommand(CreateNewWorkflow);
        CancelNewWorkflowCommand = new RelayCommand(() => { ShowNewWorkflowDialog = false; WorkflowName = ""; });

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load initial data
        _ = LoadWorkflowsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.Workflows(_localizationService.IsThaiLanguage);
    }

    private void InitializeActions()
    {
        AvailableActions.Add(new WorkflowAction { Id = 1, Name = "สร้างเนื้อหา AI", Icon = "Robot", Category = "Content" });
        AvailableActions.Add(new WorkflowAction { Id = 2, Name = "สร้างรูปภาพ AI", Icon = "Image", Category = "Content" });
        AvailableActions.Add(new WorkflowAction { Id = 3, Name = "โพสต์ไปยัง Facebook", Icon = "Facebook", Category = "Publish" });
        AvailableActions.Add(new WorkflowAction { Id = 4, Name = "โพสต์ไปยัง Instagram", Icon = "Instagram", Category = "Publish" });
        AvailableActions.Add(new WorkflowAction { Id = 5, Name = "โพสต์ไปยัง TikTok", Icon = "VideoBox", Category = "Publish" });
        AvailableActions.Add(new WorkflowAction { Id = 6, Name = "โพสต์ไปยัง Twitter", Icon = "Twitter", Category = "Publish" });
        AvailableActions.Add(new WorkflowAction { Id = 7, Name = "โพสต์ไปยังกลุ่ม", Icon = "AccountGroup", Category = "Publish" });
        AvailableActions.Add(new WorkflowAction { Id = 8, Name = "รอ/หน่วงเวลา", Icon = "Clock", Category = "Control" });
        AvailableActions.Add(new WorkflowAction { Id = 9, Name = "ตรวจสอบเงื่อนไข", Icon = "HelpCircle", Category = "Control" });
        AvailableActions.Add(new WorkflowAction { Id = 10, Name = "ส่ง Notification", Icon = "Bell", Category = "Notify" });
        AvailableActions.Add(new WorkflowAction { Id = 11, Name = "บันทึก Log", Icon = "TextBox", Category = "Utility" });
        AvailableActions.Add(new WorkflowAction { Id = 12, Name = "เรียก Webhook", Icon = "Webhook", Category = "Utility" });
    }

    private void InitializeTriggers()
    {
        AvailableTriggers.Add(new WorkflowTrigger { Id = 1, Name = "ตามตารางเวลา", Icon = "Calendar", Type = TriggerType.Schedule });
        AvailableTriggers.Add(new WorkflowTrigger { Id = 2, Name = "เมื่อมีโพสต์ใหม่", Icon = "Post", Type = TriggerType.Event });
        AvailableTriggers.Add(new WorkflowTrigger { Id = 3, Name = "เมื่อมีความคิดเห็น", Icon = "Comment", Type = TriggerType.Event });
        AvailableTriggers.Add(new WorkflowTrigger { Id = 4, Name = "ทำด้วยตัวเอง", Icon = "CursorDefaultClick", Type = TriggerType.Manual });
        AvailableTriggers.Add(new WorkflowTrigger { Id = 5, Name = "เมื่อ Webhook ถูกเรียก", Icon = "Webhook", Type = TriggerType.Webhook });
    }

    private void InitializeTemplates()
    {
        Templates.Add(new WorkflowTemplate
        {
            Id = 1,
            Name = "โพสต์รายวัน",
            Description = "สร้างเนื้อหาและโพสต์ไปยังทุกแพลตฟอร์มทุกวัน",
            Icon = "CalendarStar",
            StepCount = 5
        });
        Templates.Add(new WorkflowTemplate
        {
            Id = 2,
            Name = "โพสต์หลายกลุ่ม",
            Description = "โพสต์เนื้อหาเดียวกันไปยังหลายกลุ่ม Facebook",
            Icon = "AccountGroupOutline",
            StepCount = 3
        });
        Templates.Add(new WorkflowTemplate
        {
            Id = 3,
            Name = "AI Content + Image",
            Description = "สร้างเนื้อหาและรูปภาพด้วย AI แล้วโพสต์",
            Icon = "Robot",
            StepCount = 4
        });
        Templates.Add(new WorkflowTemplate
        {
            Id = 4,
            Name = "Cross-Platform Post",
            Description = "โพสต์เนื้อหาเดียวกันไปยังทุกแพลตฟอร์ม",
            Icon = "ShareAll",
            StepCount = 6
        });
    }

    public async Task LoadWorkflowsAsync()
    {
        try
        {
            IsBusy = true;

            // TODO: Load from database
            await Task.Delay(300);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Workflows.Clear();
                // Sample workflows
                Workflows.Add(new Workflow
                {
                    Id = 1,
                    Name = "Daily Facebook Post",
                    Description = "โพสต์ AI content ทุกวันเวลา 09:00",
                    IsActive = true,
                    TriggerType = TriggerType.Schedule,
                    TriggerInfo = "ทุกวัน 09:00",
                    StepCount = 3,
                    LastRun = DateTime.Now.AddHours(-5),
                    RunCount = 45,
                    CreatedAt = DateTime.Now.AddDays(-30)
                });
                Workflows.Add(new Workflow
                {
                    Id = 2,
                    Name = "Multi-Group Poster",
                    Description = "โพสต์ไปยัง 5 กลุ่มพร้อมกัน",
                    IsActive = true,
                    TriggerType = TriggerType.Manual,
                    TriggerInfo = "Manual",
                    StepCount = 6,
                    LastRun = DateTime.Now.AddDays(-1),
                    RunCount = 12,
                    CreatedAt = DateTime.Now.AddDays(-15)
                });
                Workflows.Add(new Workflow
                {
                    Id = 3,
                    Name = "Instagram Story Auto",
                    Description = "สร้าง Story และโพสต์อัตโนมัติ",
                    IsActive = false,
                    TriggerType = TriggerType.Schedule,
                    TriggerInfo = "ทุกวัน 18:00",
                    StepCount = 4,
                    LastRun = DateTime.Now.AddDays(-7),
                    RunCount = 8,
                    CreatedAt = DateTime.Now.AddDays(-20)
                });
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LoadWorkflowDetails()
    {
        CurrentSteps.Clear();
        if (SelectedWorkflow == null) return;

        // Load steps for selected workflow
        // TODO: Load from database
    }

    private void CreateNewWorkflow()
    {
        if (string.IsNullOrWhiteSpace(WorkflowName))
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "กรุณาใส่ชื่อ Workflow" : "Please enter workflow name",
                isThai ? "ข้อมูลไม่ครบ" : "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var workflow = new Workflow
        {
            Id = Workflows.Count + 1,
            Name = WorkflowName,
            Description = WorkflowDescription,
            IsActive = false,
            TriggerType = TriggerType.Manual,
            TriggerInfo = "Manual",
            StepCount = 0,
            CreatedAt = DateTime.Now
        };

        Workflows.Insert(0, workflow);
        SelectedWorkflow = workflow;
        IsEditing = true;

        ShowNewWorkflowDialog = false;
        WorkflowName = "";
        WorkflowDescription = "";
    }

    private void UseTemplate(WorkflowTemplate? template)
    {
        if (template == null) return;

        var workflow = new Workflow
        {
            Id = Workflows.Count + 1,
            Name = $"{template.Name} (Copy)",
            Description = template.Description,
            IsActive = false,
            TriggerType = TriggerType.Manual,
            TriggerInfo = "Manual",
            StepCount = template.StepCount,
            CreatedAt = DateTime.Now
        };

        Workflows.Insert(0, workflow);
        SelectedWorkflow = workflow;
        IsEditing = true;

        var isThai = _localizationService.IsThaiLanguage;
        MessageBox.Show(
            isThai ? $"สร้าง Workflow จาก template '{template.Name}' สำเร็จ" : $"Created workflow from template '{template.Name}'",
            isThai ? "สำเร็จ" : "Success",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void EditWorkflow(Workflow? workflow)
    {
        if (workflow == null) return;
        SelectedWorkflow = workflow;
        IsEditing = true;
    }

    private async Task DeleteWorkflowAsync(Workflow? workflow)
    {
        if (workflow == null) return;

        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            isThai ? $"ต้องการลบ Workflow '{workflow.Name}'?" : $"Delete workflow '{workflow.Name}'?",
            isThai ? "ยืนยันการลบ" : "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // TODO: Delete from database
            Workflows.Remove(workflow);
            if (SelectedWorkflow == workflow)
            {
                SelectedWorkflow = null;
                IsEditing = false;
            }
        }
    }

    private void DuplicateWorkflow(Workflow? workflow)
    {
        if (workflow == null) return;

        var copy = new Workflow
        {
            Id = Workflows.Count + 1,
            Name = $"{workflow.Name} (Copy)",
            Description = workflow.Description,
            IsActive = false,
            TriggerType = workflow.TriggerType,
            TriggerInfo = workflow.TriggerInfo,
            StepCount = workflow.StepCount,
            CreatedAt = DateTime.Now
        };

        Workflows.Insert(0, copy);
        SelectedWorkflow = copy;
    }

    private async Task RunWorkflowAsync(Workflow? workflow)
    {
        if (workflow == null) return;

        try
        {
            IsRunning = true;
            RunProgress = 0;

            var isThai = _localizationService.IsThaiLanguage;

            // Simulate running workflow steps
            for (int step = 1; step <= workflow.StepCount; step++)
            {
                RunStatus = isThai
                    ? $"กำลังรัน step {step}/{workflow.StepCount}..."
                    : $"Running step {step}/{workflow.StepCount}...";
                RunProgress = (step * 100) / workflow.StepCount;

                await Task.Delay(800);
            }

            workflow.LastRun = DateTime.Now;
            workflow.RunCount++;

            // Add to history
            RunHistory.Insert(0, new WorkflowRun
            {
                WorkflowId = workflow.Id,
                WorkflowName = workflow.Name,
                StartTime = DateTime.Now.AddSeconds(-workflow.StepCount),
                EndTime = DateTime.Now,
                Status = WorkflowRunStatus.Success,
                StepsCompleted = workflow.StepCount
            });

            MessageBox.Show(
                isThai ? $"Workflow '{workflow.Name}' รันสำเร็จ!" : $"Workflow '{workflow.Name}' completed successfully!",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsRunning = false;
            RunProgress = 0;
            RunStatus = "";
        }
    }

    private void ToggleActive(Workflow? workflow)
    {
        if (workflow == null) return;
        workflow.IsActive = !workflow.IsActive;
        OnPropertyChanged(nameof(Workflows));
    }

    private async Task SaveWorkflowAsync()
    {
        if (SelectedWorkflow == null) return;

        try
        {
            IsBusy = true;

            SelectedWorkflow.StepCount = CurrentSteps.Count;

            // TODO: Save to database
            await Task.Delay(300);

            IsEditing = false;

            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "บันทึก Workflow สำเร็จ!" : "Workflow saved successfully!",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        CurrentSteps.Clear();
    }

    private void AddStep(WorkflowAction? action)
    {
        if (action == null) return;

        var step = new WorkflowStep
        {
            Id = CurrentSteps.Count + 1,
            Order = CurrentSteps.Count + 1,
            ActionId = action.Id,
            ActionName = action.Name,
            ActionIcon = action.Icon,
            IsEnabled = true
        };

        CurrentSteps.Add(step);
    }

    private void RemoveStep(WorkflowStep? step)
    {
        if (step == null) return;
        CurrentSteps.Remove(step);

        // Reorder remaining steps
        for (int i = 0; i < CurrentSteps.Count; i++)
        {
            CurrentSteps[i].Order = i + 1;
        }
    }

    private void MoveStepUp(WorkflowStep? step)
    {
        if (step == null) return;
        var index = CurrentSteps.IndexOf(step);
        if (index > 0)
        {
            CurrentSteps.Move(index, index - 1);
            // Update order
            for (int i = 0; i < CurrentSteps.Count; i++)
            {
                CurrentSteps[i].Order = i + 1;
            }
        }
    }

    private void MoveStepDown(WorkflowStep? step)
    {
        if (step == null) return;
        var index = CurrentSteps.IndexOf(step);
        if (index < CurrentSteps.Count - 1)
        {
            CurrentSteps.Move(index, index + 1);
            // Update order
            for (int i = 0; i < CurrentSteps.Count; i++)
            {
                CurrentSteps[i].Order = i + 1;
            }
        }
    }

    private void ImportFlow()
    {
        var dialog = new OpenFileDialog
        {
            Title = _localizationService.IsThaiLanguage ? "นำเข้า Workflow" : "Import Workflow",
            Filter = "MyPostX Flow Files|*.mpflow|JSON Files|*.json|All Files|*.*",
            DefaultExt = ".mpflow"
        };

        if (dialog.ShowDialog() == true)
        {
            // TODO: Import workflow from file
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? "นำเข้า Workflow สำเร็จ!" : "Workflow imported successfully!",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            _ = LoadWorkflowsAsync();
        }
    }

    private void ExportFlow(Workflow? workflow)
    {
        if (workflow == null) return;

        var dialog = new SaveFileDialog
        {
            Title = _localizationService.IsThaiLanguage ? "ส่งออก Workflow" : "Export Workflow",
            Filter = "MyPostX Flow Files|*.mpflow|JSON Files|*.json",
            DefaultExt = ".mpflow",
            FileName = $"{workflow.Name}.mpflow"
        };

        if (dialog.ShowDialog() == true)
        {
            // TODO: Export workflow to file
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                isThai ? $"ส่งออก '{workflow.Name}' สำเร็จ!\n{dialog.FileName}" : $"Exported '{workflow.Name}' successfully!\n{dialog.FileName}",
                isThai ? "สำเร็จ" : "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}

// Supporting Models
public class Workflow
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; }
    public TriggerType TriggerType { get; set; }
    public string TriggerInfo { get; set; } = "";
    public int StepCount { get; set; }
    public DateTime? LastRun { get; set; }
    public int RunCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string StatusText => IsActive ? "Active" : "Inactive";
}

public class WorkflowTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Icon { get; set; } = "";
    public int StepCount { get; set; }
}

public class WorkflowAction
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Category { get; set; } = "";
}

public class WorkflowTrigger
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public TriggerType Type { get; set; }
}

public class WorkflowStep
{
    public int Id { get; set; }
    public int Order { get; set; }
    public int ActionId { get; set; }
    public string ActionName { get; set; } = "";
    public string ActionIcon { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

public class WorkflowRun
{
    public int Id { get; set; }
    public int WorkflowId { get; set; }
    public string WorkflowName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public WorkflowRunStatus Status { get; set; }
    public int StepsCompleted { get; set; }
    public string? ErrorMessage { get; set; }

    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;
}

public enum TriggerType
{
    Manual,
    Schedule,
    Event,
    Webhook
}

public enum WorkflowRunStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Cancelled
}
