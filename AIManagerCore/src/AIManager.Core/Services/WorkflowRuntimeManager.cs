using System.Collections.Concurrent;
using System.Text.Json;
using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Workflow Runtime Manager
/// จัดการ workflow versions และ active jobs แบบ hot-reload
/// - งานที่กำลังรันใช้ workflow version เดิม
/// - งานใหม่โหลด workflow version ล่าสุดเสมอ
/// </summary>
public class WorkflowRuntimeManager
{
    private readonly ILogger<WorkflowRuntimeManager>? _logger;
    private readonly WorkflowStorage _workflowStorage;
    private readonly string _workflowsPath;

    // Workflow Registry - เก็บ workflow ทุก version
    private readonly ConcurrentDictionary<string, WorkflowDefinition> _workflowRegistry = new();

    // Active Jobs - งานที่กำลังรันอยู่
    private readonly ConcurrentDictionary<string, ActiveJob> _activeJobs = new();

    // Job → Workflow Version binding
    private readonly ConcurrentDictionary<string, int> _jobWorkflowVersions = new();

    // Events
    public event EventHandler<WorkflowUpdatedEventArgs>? OnWorkflowUpdated;
    public event EventHandler<JobStatusChangedEventArgs>? OnJobStatusChanged;
    public event EventHandler<JobProgressEventArgs>? OnJobProgress;

    public WorkflowRuntimeManager(
        WorkflowStorage workflowStorage,
        ILogger<WorkflowRuntimeManager>? logger = null)
    {
        _workflowStorage = workflowStorage;
        _logger = logger;
        _workflowsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIManager", "workflows");

        Directory.CreateDirectory(_workflowsPath);
    }

    #region Workflow Registry

    /// <summary>
    /// โหลด workflow ทั้งหมดจาก storage
    /// </summary>
    public async Task LoadAllWorkflowsAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Loading all workflows from storage...");

        var workflows = await _workflowStorage.GetAllWorkflowsAsync(ct);

        foreach (var workflow in workflows)
        {
            var definition = new WorkflowDefinition
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Platform = workflow.Platform,
                TaskType = workflow.TaskType,
                CurrentVersion = workflow.Version,
                Versions = new Dictionary<int, LearnedWorkflow>
                {
                    [workflow.Version] = workflow
                },
                IsActive = true,
                LastModified = workflow.CreatedAt,
                CreatedAt = workflow.CreatedAt
            };

            _workflowRegistry[workflow.Id] = definition;
        }

        _logger?.LogInformation("Loaded {Count} workflows", workflows.Count);
    }

    /// <summary>
    /// ดึง workflow version ล่าสุด (สำหรับงานใหม่)
    /// </summary>
    public WorkflowDefinition? GetLatestWorkflow(string workflowId)
    {
        return _workflowRegistry.TryGetValue(workflowId, out var definition) ? definition : null;
    }

    /// <summary>
    /// ดึง workflow ตาม version (สำหรับงานที่กำลังรัน)
    /// </summary>
    public LearnedWorkflow? GetWorkflowVersion(string workflowId, int version)
    {
        if (_workflowRegistry.TryGetValue(workflowId, out var definition))
        {
            return definition.Versions.TryGetValue(version, out var workflow) ? workflow : null;
        }
        return null;
    }

    /// <summary>
    /// ดึง workflow ทั้งหมด
    /// </summary>
    public List<WorkflowDefinition> GetAllWorkflows()
    {
        return _workflowRegistry.Values.ToList();
    }

    /// <summary>
    /// ดึง workflow ตาม platform และ task type
    /// </summary>
    public List<WorkflowDefinition> GetWorkflowsByPlatform(string platform, string? taskType = null)
    {
        return _workflowRegistry.Values
            .Where(w => w.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                       (taskType == null || w.TaskType.Equals(taskType, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// บันทึก workflow ใหม่หรืออัพเดต (สร้าง version ใหม่)
    /// </summary>
    public async Task<WorkflowDefinition> SaveWorkflowAsync(
        LearnedWorkflow workflow,
        CancellationToken ct = default)
    {
        WorkflowDefinition definition;

        if (_workflowRegistry.TryGetValue(workflow.Id, out var existing))
        {
            // สร้าง version ใหม่
            var newVersion = existing.CurrentVersion + 1;
            workflow.Version = newVersion;

            existing.Versions[newVersion] = workflow;
            existing.CurrentVersion = newVersion;
            existing.LastModified = DateTime.UtcNow;

            definition = existing;

            _logger?.LogInformation("Updated workflow {Id} to version {Version}",
                workflow.Id, newVersion);
        }
        else
        {
            // Workflow ใหม่
            workflow.Version = 1;

            definition = new WorkflowDefinition
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Platform = workflow.Platform,
                TaskType = workflow.TaskType,
                CurrentVersion = 1,
                Versions = new Dictionary<int, LearnedWorkflow> { [1] = workflow },
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };

            _workflowRegistry[workflow.Id] = definition;

            _logger?.LogInformation("Created new workflow {Id}", workflow.Id);
        }

        // บันทึกลง storage
        await _workflowStorage.SaveWorkflowAsync(workflow, ct);

        // Notify
        OnWorkflowUpdated?.Invoke(this, new WorkflowUpdatedEventArgs(definition, workflow.Version));

        return definition;
    }

    /// <summary>
    /// ลบ workflow
    /// </summary>
    public async Task<bool> DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        // ตรวจสอบว่ามีงานกำลังใช้อยู่หรือไม่
        var activeJobsUsingWorkflow = _activeJobs.Values
            .Where(j => j.WorkflowId == workflowId && j.Status == JobStatus.Running)
            .ToList();

        if (activeJobsUsingWorkflow.Any())
        {
            _logger?.LogWarning("Cannot delete workflow {Id} - {Count} active jobs using it",
                workflowId, activeJobsUsingWorkflow.Count);
            return false;
        }

        if (_workflowRegistry.TryRemove(workflowId, out var removed))
        {
            await _workflowStorage.DeleteWorkflowAsync(workflowId, ct);
            _logger?.LogInformation("Deleted workflow {Id}", workflowId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Toggle workflow active status
    /// </summary>
    public void SetWorkflowActive(string workflowId, bool isActive)
    {
        if (_workflowRegistry.TryGetValue(workflowId, out var definition))
        {
            definition.IsActive = isActive;
            _logger?.LogInformation("Workflow {Id} active status set to {Status}",
                workflowId, isActive);
        }
    }

    #endregion

    #region Active Jobs Management

    /// <summary>
    /// สร้างและเริ่มงานใหม่ - โหลด workflow version ล่าสุด
    /// </summary>
    public ActiveJob? StartJob(
        string workflowId,
        string? userId = null,
        Dictionary<string, object>? parameters = null)
    {
        var definition = GetLatestWorkflow(workflowId);
        if (definition == null || !definition.IsActive)
        {
            _logger?.LogWarning("Cannot start job - workflow {Id} not found or inactive", workflowId);
            return null;
        }

        var job = new ActiveJob
        {
            Id = Guid.NewGuid().ToString(),
            WorkflowId = workflowId,
            WorkflowName = definition.Name,
            WorkflowVersion = definition.CurrentVersion, // Snapshot version
            UserId = userId,
            Parameters = parameters ?? new(),
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Progress = new JobProgress()
        };

        // Bind job → workflow version
        _jobWorkflowVersions[job.Id] = definition.CurrentVersion;
        _activeJobs[job.Id] = job;

        _logger?.LogInformation("Created job {JobId} using workflow {WorkflowId} v{Version}",
            job.Id, workflowId, definition.CurrentVersion);

        // Start the job
        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.UtcNow;

        OnJobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs(job));

        return job;
    }

    /// <summary>
    /// ดึง workflow ที่ job ใช้ (version ที่ snapshot ไว้ตอนเริ่มงาน)
    /// </summary>
    public LearnedWorkflow? GetJobWorkflow(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job) &&
            _jobWorkflowVersions.TryGetValue(jobId, out var version))
        {
            return GetWorkflowVersion(job.WorkflowId, version);
        }
        return null;
    }

    /// <summary>
    /// อัพเดต progress ของ job
    /// </summary>
    public void UpdateJobProgress(string jobId, int percentage, string currentStep, string? message = null)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.Progress.Percentage = percentage;
            job.Progress.CurrentStep = currentStep;
            job.Progress.LastUpdate = DateTime.UtcNow;

            if (message != null)
            {
                job.Progress.Logs.Add(new JobLog
                {
                    Timestamp = DateTime.UtcNow,
                    Message = message,
                    Level = "info"
                });
            }

            OnJobProgress?.Invoke(this, new JobProgressEventArgs(job));
        }
    }

    /// <summary>
    /// Complete job
    /// </summary>
    public void CompleteJob(string jobId, bool success, string? result = null, string? error = null)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            job.Status = success ? JobStatus.Completed : JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.Result = result;
            job.Error = error;
            job.Progress.Percentage = success ? 100 : job.Progress.Percentage;

            _logger?.LogInformation("Job {JobId} completed with status {Status}",
                jobId, job.Status);

            OnJobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs(job));
        }
    }

    /// <summary>
    /// Cancel job
    /// </summary>
    public bool CancelJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == JobStatus.Running || job.Status == JobStatus.Pending)
            {
                job.Status = JobStatus.Cancelled;
                job.CompletedAt = DateTime.UtcNow;

                _logger?.LogInformation("Job {JobId} cancelled", jobId);
                OnJobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs(job));

                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Pause job
    /// </summary>
    public bool PauseJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Paused;
                _logger?.LogInformation("Job {JobId} paused", jobId);
                OnJobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs(job));
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Resume job
    /// </summary>
    public bool ResumeJob(string jobId)
    {
        if (_activeJobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == JobStatus.Paused)
            {
                job.Status = JobStatus.Running;
                _logger?.LogInformation("Job {JobId} resumed", jobId);
                OnJobStatusChanged?.Invoke(this, new JobStatusChangedEventArgs(job));
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ดึงงานที่กำลังรัน
    /// </summary>
    public List<ActiveJob> GetActiveJobs()
    {
        return _activeJobs.Values
            .Where(j => j.Status == JobStatus.Running || j.Status == JobStatus.Pending || j.Status == JobStatus.Paused)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// ดึงงานทั้งหมด (รวม completed)
    /// </summary>
    public List<ActiveJob> GetAllJobs(int limit = 100)
    {
        return _activeJobs.Values
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// ดึงงานตาม workflow
    /// </summary>
    public List<ActiveJob> GetJobsByWorkflow(string workflowId)
    {
        return _activeJobs.Values
            .Where(j => j.WorkflowId == workflowId)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// ดึงงานตาม status
    /// </summary>
    public List<ActiveJob> GetJobsByStatus(JobStatus status)
    {
        return _activeJobs.Values
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// ลบ job ที่เสร็จแล้ว (cleanup)
    /// </summary>
    public int CleanupCompletedJobs(TimeSpan olderThan)
    {
        var cutoff = DateTime.UtcNow - olderThan;
        var toRemove = _activeJobs.Values
            .Where(j => (j.Status == JobStatus.Completed || j.Status == JobStatus.Failed || j.Status == JobStatus.Cancelled) &&
                       j.CompletedAt.HasValue && j.CompletedAt.Value < cutoff)
            .Select(j => j.Id)
            .ToList();

        foreach (var id in toRemove)
        {
            _activeJobs.TryRemove(id, out _);
            _jobWorkflowVersions.TryRemove(id, out _);
        }

        _logger?.LogInformation("Cleaned up {Count} completed jobs", toRemove.Count);
        return toRemove.Count;
    }

    #endregion

    #region Statistics

    /// <summary>
    /// ดึงสถิติของระบบ
    /// </summary>
    public RuntimeStatistics GetStatistics()
    {
        var jobs = _activeJobs.Values.ToList();

        return new RuntimeStatistics
        {
            TotalWorkflows = _workflowRegistry.Count,
            ActiveWorkflows = _workflowRegistry.Values.Count(w => w.IsActive),
            TotalJobs = jobs.Count,
            RunningJobs = jobs.Count(j => j.Status == JobStatus.Running),
            PendingJobs = jobs.Count(j => j.Status == JobStatus.Pending),
            PausedJobs = jobs.Count(j => j.Status == JobStatus.Paused),
            CompletedJobs = jobs.Count(j => j.Status == JobStatus.Completed),
            FailedJobs = jobs.Count(j => j.Status == JobStatus.Failed),
            CancelledJobs = jobs.Count(j => j.Status == JobStatus.Cancelled),
            LastUpdated = DateTime.UtcNow
        };
    }

    #endregion
}

#region Models

/// <summary>
/// Workflow Definition with versions
/// </summary>
public class WorkflowDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public int CurrentVersion { get; set; } = 1;
    public Dictionary<int, LearnedWorkflow> Versions { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime LastModified { get; set; }

    public LearnedWorkflow? CurrentWorkflow =>
        Versions.TryGetValue(CurrentVersion, out var wf) ? wf : null;
}

/// <summary>
/// Active Job
/// </summary>
public class ActiveJob
{
    public string Id { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public int WorkflowVersion { get; set; }
    public string? UserId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public JobStatus Status { get; set; }
    public JobProgress Progress { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Result { get; set; }
    public string? Error { get; set; }

    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : (StartedAt.HasValue ? DateTime.UtcNow - StartedAt.Value : null);
}

/// <summary>
/// Job Status
/// </summary>
public enum JobStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Job Progress
/// </summary>
public class JobProgress
{
    public int Percentage { get; set; }
    public string CurrentStep { get; set; } = "";
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
    public List<JobLog> Logs { get; set; } = new();
}

/// <summary>
/// Job Log Entry
/// </summary>
public class JobLog
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
    public string Level { get; set; } = "info"; // info, warning, error
}

/// <summary>
/// Runtime Statistics
/// </summary>
public class RuntimeStatistics
{
    public int TotalWorkflows { get; set; }
    public int ActiveWorkflows { get; set; }
    public int TotalJobs { get; set; }
    public int RunningJobs { get; set; }
    public int PendingJobs { get; set; }
    public int PausedJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int CancelledJobs { get; set; }
    public DateTime LastUpdated { get; set; }
}

#endregion

#region Event Args

public class WorkflowUpdatedEventArgs : EventArgs
{
    public WorkflowDefinition Workflow { get; }
    public int NewVersion { get; }

    public WorkflowUpdatedEventArgs(WorkflowDefinition workflow, int newVersion)
    {
        Workflow = workflow;
        NewVersion = newVersion;
    }
}

public class JobStatusChangedEventArgs : EventArgs
{
    public ActiveJob Job { get; }

    public JobStatusChangedEventArgs(ActiveJob job)
    {
        Job = job;
    }
}

public class JobProgressEventArgs : EventArgs
{
    public ActiveJob Job { get; }

    public JobProgressEventArgs(ActiveJob job)
    {
        Job = job;
    }
}

#endregion
