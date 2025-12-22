using System.Collections.Concurrent;
using AIManager.Core.NodeEditor;
using AIManager.Core.NodeEditor.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.Services;

/// <summary>
/// Scheduled workflow configuration
/// </summary>
public class ScheduledWorkflow
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string WorkflowFilePath { get; set; } = "";
    public string? WorkflowJson { get; set; } // Inline workflow JSON (alternative to file)

    // Schedule settings
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Strategy;
    public long? StrategyId { get; set; } // Link to PostingStrategy
    public string? CronExpression { get; set; } // For custom cron scheduling
    public List<string> ScheduledTimes { get; set; } = new(); // Specific times like ["09:00", "12:00"]
    public int? IntervalMinutes { get; set; } // For interval-based scheduling

    // Execution settings
    public int MaxExecutionsPerDay { get; set; } = 10;
    public int CurrentExecutionsToday { get; set; }
    public DateTime? LastExecutedAt { get; set; }
    public DateTime? NextScheduledAt { get; set; }

    // Status
    public bool IsEnabled { get; set; } = true;
    public bool IsRunning { get; set; }
    public WorkflowExecutionStatus LastStatus { get; set; } = WorkflowExecutionStatus.Pending;
    public string? LastError { get; set; }

    // Variables to pass to workflow
    public Dictionary<string, object> Variables { get; set; } = new();

    // Metadata
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum ScheduleType
{
    Strategy,    // Follow posting strategy times
    Cron,        // Custom cron expression
    FixedTimes,  // Specific times each day
    Interval,    // Every X minutes
    Manual       // Only manual execution
}

public enum WorkflowExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Skipped
}

/// <summary>
/// Workflow execution history record
/// </summary>
public class WorkflowExecutionHistory
{
    public long Id { get; set; }
    public long ScheduledWorkflowId { get; set; }
    public string WorkflowName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public WorkflowExecutionStatus Status { get; set; }
    public string? Error { get; set; }
    public string? OutputsJson { get; set; }
    public double DurationSeconds { get; set; }
    public int NodesExecuted { get; set; }
}

/// <summary>
/// Service for scheduling and managing workflow executions
/// </summary>
public class WorkflowSchedulerService : IDisposable
{
    private readonly ILogger<WorkflowSchedulerService> _logger;
    private readonly WorkflowEngine _workflowEngine;
    private readonly AILearningDatabaseService _dbService;

    private readonly ConcurrentDictionary<long, ScheduledWorkflow> _scheduledWorkflows = new();
    private readonly ConcurrentDictionary<long, CancellationTokenSource> _runningWorkflows = new();
    private readonly List<WorkflowExecutionHistory> _executionHistory = new();

    private Timer? _schedulerTimer;
    private bool _isRunning;
    private readonly object _lock = new();

    // Events
    public event EventHandler<ScheduledWorkflow>? WorkflowScheduled;
    public event EventHandler<WorkflowExecutionHistory>? WorkflowStarted;
    public event EventHandler<WorkflowExecutionHistory>? WorkflowCompleted;
    public event EventHandler<string>? StatusChanged;

    public IReadOnlyCollection<ScheduledWorkflow> ScheduledWorkflows => _scheduledWorkflows.Values.ToList();
    public IReadOnlyCollection<WorkflowExecutionHistory> ExecutionHistory => _executionHistory.AsReadOnly();
    public bool IsRunning => _isRunning;

    public WorkflowSchedulerService(
        ILogger<WorkflowSchedulerService> logger,
        WorkflowEngine workflowEngine,
        AILearningDatabaseService dbService)
    {
        _logger = logger;
        _workflowEngine = workflowEngine;
        _dbService = dbService;
    }

    /// <summary>
    /// Start the scheduler
    /// </summary>
    public void Start()
    {
        if (_isRunning) return;

        _isRunning = true;
        _schedulerTimer = new Timer(CheckSchedules, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        StatusChanged?.Invoke(this, "Workflow scheduler started");
        _logger.LogInformation("Workflow scheduler started");
    }

    /// <summary>
    /// Stop the scheduler
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        _schedulerTimer?.Dispose();
        _schedulerTimer = null;

        // Cancel all running workflows
        foreach (var cts in _runningWorkflows.Values)
        {
            cts.Cancel();
        }
        _runningWorkflows.Clear();

        StatusChanged?.Invoke(this, "Workflow scheduler stopped");
        _logger.LogInformation("Workflow scheduler stopped");
    }

    /// <summary>
    /// Add or update a scheduled workflow
    /// </summary>
    public async Task<ScheduledWorkflow> ScheduleWorkflowAsync(ScheduledWorkflow workflow)
    {
        workflow.UpdatedAt = DateTime.UtcNow;
        if (workflow.Id == 0)
        {
            workflow.Id = DateTime.UtcNow.Ticks;
            workflow.CreatedAt = DateTime.UtcNow;
        }

        // Calculate next execution time
        workflow.NextScheduledAt = CalculateNextExecutionTime(workflow);

        _scheduledWorkflows[workflow.Id] = workflow;

        // Save to database
        await SaveScheduledWorkflowToDbAsync(workflow);

        WorkflowScheduled?.Invoke(this, workflow);
        _logger.LogInformation("Workflow scheduled: {Name}, Next: {NextTime}",
            workflow.Name, workflow.NextScheduledAt);

        return workflow;
    }

    /// <summary>
    /// Schedule a workflow based on a posting strategy
    /// </summary>
    public async Task<ScheduledWorkflow> ScheduleFromStrategyAsync(
        string workflowPath,
        PostingStrategy strategy,
        Dictionary<string, object>? variables = null)
    {
        var times = new List<string>();
        if (!string.IsNullOrEmpty(strategy.OptimalTimes))
        {
            times = JsonConvert.DeserializeObject<List<string>>(strategy.OptimalTimes) ?? new();
        }

        var workflow = new ScheduledWorkflow
        {
            Name = $"{strategy.Name} - Workflow",
            Description = $"Scheduled from strategy: {strategy.Description}",
            WorkflowFilePath = workflowPath,
            ScheduleType = ScheduleType.Strategy,
            StrategyId = strategy.Id,
            ScheduledTimes = times,
            MaxExecutionsPerDay = strategy.PostsPerDay,
            Variables = variables ?? new()
        };

        return await ScheduleWorkflowAsync(workflow);
    }

    /// <summary>
    /// Remove a scheduled workflow
    /// </summary>
    public void RemoveSchedule(long workflowId)
    {
        if (_scheduledWorkflows.TryRemove(workflowId, out var workflow))
        {
            _logger.LogInformation("Workflow schedule removed: {Name}", workflow.Name);
        }

        // Cancel if running
        if (_runningWorkflows.TryRemove(workflowId, out var cts))
        {
            cts.Cancel();
        }
    }

    /// <summary>
    /// Enable or disable a scheduled workflow
    /// </summary>
    public void SetEnabled(long workflowId, bool enabled)
    {
        if (_scheduledWorkflows.TryGetValue(workflowId, out var workflow))
        {
            workflow.IsEnabled = enabled;
            workflow.UpdatedAt = DateTime.UtcNow;

            if (enabled)
            {
                workflow.NextScheduledAt = CalculateNextExecutionTime(workflow);
            }

            _logger.LogInformation("Workflow {Name} {Status}",
                workflow.Name, enabled ? "enabled" : "disabled");
        }
    }

    /// <summary>
    /// Execute a workflow immediately
    /// </summary>
    public async Task<WorkflowExecutionHistory> ExecuteNowAsync(long workflowId)
    {
        if (!_scheduledWorkflows.TryGetValue(workflowId, out var scheduled))
        {
            throw new ArgumentException($"Workflow {workflowId} not found");
        }

        return await ExecuteWorkflowAsync(scheduled);
    }

    /// <summary>
    /// Execute a workflow by path immediately
    /// </summary>
    public async Task<WorkflowExecutionHistory> ExecuteWorkflowByPathAsync(
        string workflowPath,
        Dictionary<string, object>? variables = null)
    {
        var scheduled = new ScheduledWorkflow
        {
            Id = DateTime.UtcNow.Ticks,
            Name = Path.GetFileNameWithoutExtension(workflowPath),
            WorkflowFilePath = workflowPath,
            ScheduleType = ScheduleType.Manual,
            Variables = variables ?? new(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return await ExecuteWorkflowAsync(scheduled);
    }

    /// <summary>
    /// Get execution history for a workflow
    /// </summary>
    public IEnumerable<WorkflowExecutionHistory> GetHistory(long? workflowId = null, int limit = 100)
    {
        var query = _executionHistory.AsEnumerable();

        if (workflowId.HasValue)
        {
            query = query.Where(h => h.ScheduledWorkflowId == workflowId.Value);
        }

        return query.OrderByDescending(h => h.StartedAt).Take(limit);
    }

    /// <summary>
    /// Get workflows due for execution
    /// </summary>
    public IEnumerable<ScheduledWorkflow> GetDueWorkflows()
    {
        var now = DateTime.UtcNow;

        return _scheduledWorkflows.Values
            .Where(w => w.IsEnabled && !w.IsRunning)
            .Where(w => w.NextScheduledAt.HasValue && w.NextScheduledAt <= now)
            .Where(w => w.CurrentExecutionsToday < w.MaxExecutionsPerDay)
            .OrderBy(w => w.NextScheduledAt);
    }

    /// <summary>
    /// Load saved workflows from file
    /// </summary>
    public async Task LoadSavedWorkflowsAsync()
    {
        var path = GetWorkflowsConfigPath();
        if (!File.Exists(path)) return;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            var workflows = JsonConvert.DeserializeObject<List<ScheduledWorkflow>>(json);

            if (workflows != null)
            {
                foreach (var wf in workflows)
                {
                    wf.NextScheduledAt = CalculateNextExecutionTime(wf);
                    wf.IsRunning = false;
                    wf.CurrentExecutionsToday = 0; // Reset daily counter
                    _scheduledWorkflows[wf.Id] = wf;
                }

                _logger.LogInformation("Loaded {Count} scheduled workflows", workflows.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load saved workflows");
        }
    }

    /// <summary>
    /// Save workflows to file
    /// </summary>
    public async Task SaveWorkflowsAsync()
    {
        var path = GetWorkflowsConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var json = JsonConvert.SerializeObject(_scheduledWorkflows.Values.ToList(), Formatting.Indented);
        await File.WriteAllTextAsync(path, json);

        _logger.LogInformation("Saved {Count} scheduled workflows", _scheduledWorkflows.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private void CheckSchedules(object? state)
    {
        if (!_isRunning) return;

        // Reset daily counters at midnight
        var now = DateTime.UtcNow;
        if (now.Hour == 0 && now.Minute < 2)
        {
            foreach (var wf in _scheduledWorkflows.Values)
            {
                wf.CurrentExecutionsToday = 0;
            }
        }

        // Find due workflows
        var dueWorkflows = GetDueWorkflows().ToList();

        foreach (var workflow in dueWorkflows)
        {
            // Execute in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteWorkflowAsync(workflow);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to execute scheduled workflow: {Name}", workflow.Name);
                }
            });
        }
    }

    private async Task<WorkflowExecutionHistory> ExecuteWorkflowAsync(ScheduledWorkflow scheduled)
    {
        var history = new WorkflowExecutionHistory
        {
            Id = DateTime.UtcNow.Ticks,
            ScheduledWorkflowId = scheduled.Id,
            WorkflowName = scheduled.Name,
            StartedAt = DateTime.UtcNow,
            Status = WorkflowExecutionStatus.Running
        };

        var cts = new CancellationTokenSource();
        _runningWorkflows[scheduled.Id] = cts;

        scheduled.IsRunning = true;
        scheduled.LastStatus = WorkflowExecutionStatus.Running;

        WorkflowStarted?.Invoke(this, history);
        _logger.LogInformation("Executing workflow: {Name}", scheduled.Name);

        try
        {
            // Load workflow
            WorkflowGraph? workflow = null;

            if (!string.IsNullOrEmpty(scheduled.WorkflowJson))
            {
                workflow = JsonConvert.DeserializeObject<WorkflowGraph>(scheduled.WorkflowJson);
            }
            else if (!string.IsNullOrEmpty(scheduled.WorkflowFilePath) && File.Exists(scheduled.WorkflowFilePath))
            {
                var json = await File.ReadAllTextAsync(scheduled.WorkflowFilePath, cts.Token);
                workflow = JsonConvert.DeserializeObject<WorkflowGraph>(json);
            }

            if (workflow == null)
            {
                throw new FileNotFoundException($"Workflow not found: {scheduled.WorkflowFilePath}");
            }

            // Add scheduled variables
            foreach (var (key, value) in scheduled.Variables)
            {
                workflow.Variables[key] = value;
            }

            // Execute
            var result = await _workflowEngine.ExecuteAsync(workflow, ct: cts.Token);

            // Update history
            history.CompletedAt = DateTime.UtcNow;
            history.DurationSeconds = (history.CompletedAt.Value - history.StartedAt).TotalSeconds;
            history.NodesExecuted = result.NodeResults.Count;

            if (result.Success)
            {
                history.Status = WorkflowExecutionStatus.Completed;
                history.OutputsJson = JsonConvert.SerializeObject(result.FinalOutputs);

                scheduled.LastStatus = WorkflowExecutionStatus.Completed;
                scheduled.LastError = null;
            }
            else
            {
                history.Status = WorkflowExecutionStatus.Failed;
                history.Error = result.Error;

                scheduled.LastStatus = WorkflowExecutionStatus.Failed;
                scheduled.LastError = result.Error;
            }
        }
        catch (OperationCanceledException)
        {
            history.Status = WorkflowExecutionStatus.Cancelled;
            history.CompletedAt = DateTime.UtcNow;
            scheduled.LastStatus = WorkflowExecutionStatus.Cancelled;
        }
        catch (Exception ex)
        {
            history.Status = WorkflowExecutionStatus.Failed;
            history.Error = ex.Message;
            history.CompletedAt = DateTime.UtcNow;

            scheduled.LastStatus = WorkflowExecutionStatus.Failed;
            scheduled.LastError = ex.Message;

            _logger.LogError(ex, "Workflow execution failed: {Name}", scheduled.Name);
        }
        finally
        {
            scheduled.IsRunning = false;
            scheduled.LastExecutedAt = DateTime.UtcNow;
            scheduled.CurrentExecutionsToday++;
            scheduled.NextScheduledAt = CalculateNextExecutionTime(scheduled);

            _runningWorkflows.TryRemove(scheduled.Id, out _);

            // Add to history
            lock (_lock)
            {
                _executionHistory.Insert(0, history);

                // Keep only last 1000 records
                while (_executionHistory.Count > 1000)
                {
                    _executionHistory.RemoveAt(_executionHistory.Count - 1);
                }
            }
        }

        WorkflowCompleted?.Invoke(this, history);
        return history;
    }

    private DateTime? CalculateNextExecutionTime(ScheduledWorkflow workflow)
    {
        if (!workflow.IsEnabled) return null;

        var now = DateTime.Now; // Use local time for scheduling

        return workflow.ScheduleType switch
        {
            ScheduleType.Strategy or ScheduleType.FixedTimes =>
                GetNextTimeFromList(workflow.ScheduledTimes, now),

            ScheduleType.Interval when workflow.IntervalMinutes.HasValue =>
                workflow.LastExecutedAt?.AddMinutes(workflow.IntervalMinutes.Value)
                ?? now.AddMinutes(workflow.IntervalMinutes.Value),

            ScheduleType.Cron when !string.IsNullOrEmpty(workflow.CronExpression) =>
                ParseCronNextTime(workflow.CronExpression, now),

            ScheduleType.Manual => null,

            _ => null
        };
    }

    private DateTime? GetNextTimeFromList(List<string> times, DateTime now)
    {
        if (times.Count == 0) return null;

        var today = now.Date;

        // Find next time today
        foreach (var timeStr in times.OrderBy(t => t))
        {
            if (TimeSpan.TryParse(timeStr, out var time))
            {
                var scheduled = today.Add(time);
                if (scheduled > now)
                {
                    return scheduled.ToUniversalTime();
                }
            }
        }

        // No more times today, get first time tomorrow
        if (TimeSpan.TryParse(times.OrderBy(t => t).First(), out var firstTime))
        {
            return today.AddDays(1).Add(firstTime).ToUniversalTime();
        }

        return null;
    }

    private DateTime? ParseCronNextTime(string cronExpression, DateTime now)
    {
        // Simple cron parser for common patterns
        // Format: minute hour day month dayOfWeek
        // Example: "0 9 * * *" = every day at 9:00

        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length < 5) return null;

            var minute = parts[0] == "*" ? 0 : int.Parse(parts[0]);
            var hour = parts[1] == "*" ? 0 : int.Parse(parts[1]);

            var today = now.Date;
            var scheduled = today.AddHours(hour).AddMinutes(minute);

            if (scheduled <= now)
            {
                scheduled = scheduled.AddDays(1);
            }

            return scheduled.ToUniversalTime();
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveScheduledWorkflowToDbAsync(ScheduledWorkflow workflow)
    {
        try
        {
            // Save as learning record for persistence
            var record = new LearningRecord
            {
                Category = "scheduled_workflows",
                Key = workflow.Id.ToString(),
                Value = JsonConvert.SerializeObject(workflow),
                Metadata = workflow.Name
            };

            await _dbService.SaveLearningRecordAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save workflow to database, will save to file");
            await SaveWorkflowsAsync();
        }
    }

    private string GetWorkflowsConfigPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent", "scheduled_workflows.json");
    }

    public void Dispose()
    {
        Stop();
        _schedulerTimer?.Dispose();
    }
}
