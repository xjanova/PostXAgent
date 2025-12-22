using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;

namespace AIManager.Core.Workers;

/// <summary>
/// Advanced Worker Manager - จัดการ workers ทั้งหมดแบบ real-time
/// รองรับ Pause/Resume/Stop และรายงานสถานะทุก worker
/// </summary>
public class WorkerManager : IDisposable
{
    private readonly ILogger<WorkerManager> _logger;
    private readonly ConcurrentDictionary<string, ManagedWorker> _workers = new();
    private readonly ConcurrentDictionary<string, WorkerReport> _reports = new();
    private readonly ConcurrentQueue<WorkerReport> _reportHistory = new();
    private readonly int _maxHistorySize = 1000;

    private readonly CancellationTokenSource _globalCts = new();
    private readonly Process _currentProcess;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuCheck = DateTime.UtcNow;

    // Events for UI binding
    public event EventHandler<WorkerStateChangedEventArgs>? WorkerStateChanged;
    public event EventHandler<WorkerProgressEventArgs>? WorkerProgress;
    public event EventHandler<WorkerReportEventArgs>? WorkerReported;
    public event EventHandler<SystemStatsEventArgs>? SystemStatsUpdated;

    // System-wide stats
    public int TotalCores { get; }
    public int ActiveWorkers => _workers.Count(w => w.Value.State == WorkerState.Running);
    public int PausedWorkers => _workers.Count(w => w.Value.State == WorkerState.Paused);
    public int TotalWorkers => _workers.Count;
    public double CpuUsage { get; private set; }
    public long MemoryUsageMB { get; private set; }

    public WorkerManager(ILogger<WorkerManager>? logger = null)
    {
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<WorkerManager>();
        _currentProcess = Process.GetCurrentProcess();
        TotalCores = Environment.ProcessorCount;
        _lastCpuTime = _currentProcess.TotalProcessorTime;
        _lastCpuCheck = DateTime.UtcNow;

        // Start system stats monitor
        _ = Task.Run(MonitorSystemStatsAsync);

        _logger.LogInformation("WorkerManager initialized with {Cores} CPU cores", TotalCores);
    }

    #region Worker Creation & Management

    /// <summary>
    /// สร้าง Worker ใหม่พร้อม CPU affinity optimization
    /// </summary>
    public ManagedWorker CreateWorker(
        string name,
        SocialPlatform platform,
        Func<ManagedWorker, CancellationToken, Task> workFunc,
        int? preferredCore = null)
    {
        var workerId = $"{platform}_{name}_{Guid.NewGuid():N}".Substring(0, 32);

        var worker = new ManagedWorker
        {
            Id = workerId,
            Name = name,
            Platform = platform,
            State = WorkerState.Created,
            CreatedAt = DateTime.UtcNow,
            PreferredCore = preferredCore ?? AssignOptimalCore(platform),
            WorkFunction = workFunc
        };

        _workers.TryAdd(workerId, worker);

        OnWorkerStateChanged(worker, WorkerState.Created, "Worker created");
        _logger.LogInformation("Created worker {WorkerId} for {Platform} on core {Core}",
            workerId, platform, worker.PreferredCore);

        return worker;
    }

    /// <summary>
    /// เริ่มการทำงานของ Worker
    /// </summary>
    public async Task StartWorkerAsync(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            throw new ArgumentException($"Worker {workerId} not found");
        }

        if (worker.State == WorkerState.Running)
        {
            return;
        }

        worker.State = WorkerState.Running;
        worker.StartedAt = DateTime.UtcNow;
        worker.WorkerCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token);

        // Set CPU affinity if on Windows
        SetCpuAffinity(worker.PreferredCore);

        OnWorkerStateChanged(worker, WorkerState.Running, "Worker started");

        // Run the worker
        worker.WorkerTask = Task.Run(async () =>
        {
            try
            {
                await worker.WorkFunction!(worker, worker.WorkerCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                worker.LastError = ex.Message;
                worker.ErrorCount++;
                OnWorkerStateChanged(worker, WorkerState.Error, ex.Message);
            }
            finally
            {
                if (worker.State != WorkerState.Paused)
                {
                    worker.State = WorkerState.Stopped;
                    worker.StoppedAt = DateTime.UtcNow;
                    OnWorkerStateChanged(worker, WorkerState.Stopped, "Worker finished");
                }
            }
        });

        await Task.CompletedTask;
    }

    /// <summary>
    /// หยุด Worker ชั่วคราว (Pause)
    /// </summary>
    public void PauseWorker(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            throw new ArgumentException($"Worker {workerId} not found");
        }

        if (worker.State != WorkerState.Running)
        {
            return;
        }

        worker.PauseRequested = true;
        worker.State = WorkerState.Paused;
        worker.PausedAt = DateTime.UtcNow;

        OnWorkerStateChanged(worker, WorkerState.Paused, "Worker paused");
        _logger.LogInformation("Paused worker {WorkerId}", workerId);
    }

    /// <summary>
    /// กลับมาทำงานต่อจาก Pause
    /// </summary>
    public void ResumeWorker(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            throw new ArgumentException($"Worker {workerId} not found");
        }

        if (worker.State != WorkerState.Paused)
        {
            return;
        }

        worker.PauseRequested = false;
        worker.State = WorkerState.Running;
        worker.ResumedAt = DateTime.UtcNow;

        OnWorkerStateChanged(worker, WorkerState.Running, "Worker resumed");
        _logger.LogInformation("Resumed worker {WorkerId}", workerId);
    }

    /// <summary>
    /// หยุด Worker ถาวร
    /// </summary>
    public async Task StopWorkerAsync(string workerId, bool graceful = true)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            throw new ArgumentException($"Worker {workerId} not found");
        }

        worker.StopRequested = true;
        worker.WorkerCts?.Cancel();

        if (graceful && worker.WorkerTask != null)
        {
            try
            {
                await worker.WorkerTask.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Worker {WorkerId} did not stop gracefully", workerId);
            }
        }

        worker.State = WorkerState.Stopped;
        worker.StoppedAt = DateTime.UtcNow;

        // Generate final report
        GenerateWorkerReport(worker, "Worker stopped");

        OnWorkerStateChanged(worker, WorkerState.Stopped, "Worker stopped");
        _logger.LogInformation("Stopped worker {WorkerId}", workerId);
    }

    /// <summary>
    /// หยุดทุก Workers
    /// </summary>
    public async Task StopAllWorkersAsync()
    {
        _logger.LogInformation("Stopping all workers...");

        var tasks = _workers.Values
            .Where(w => w.State == WorkerState.Running || w.State == WorkerState.Paused)
            .Select(w => StopWorkerAsync(w.Id))
            .ToList();

        await Task.WhenAll(tasks);

        _logger.LogInformation("All workers stopped");
    }

    /// <summary>
    /// Pause ทุก Workers
    /// </summary>
    public void PauseAllWorkers()
    {
        foreach (var worker in _workers.Values.Where(w => w.State == WorkerState.Running))
        {
            PauseWorker(worker.Id);
        }
    }

    /// <summary>
    /// Resume ทุก Workers
    /// </summary>
    public void ResumeAllWorkers()
    {
        foreach (var worker in _workers.Values.Where(w => w.State == WorkerState.Paused))
        {
            ResumeWorker(worker.Id);
        }
    }

    #endregion

    #region Worker Control Helpers

    /// <summary>
    /// ให้ Worker เรียกเพื่อตรวจสอบและรอถ้าถูก Pause
    /// Worker ควรเรียก method นี้บ่อยๆ ระหว่างทำงาน
    /// </summary>
    public async Task CheckPauseAndWaitAsync(string workerId, CancellationToken ct)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            return;
        }

        // ถ้า worker ถูก pause ให้รอจนกว่าจะ resume หรือถูก cancel
        while (worker.PauseRequested && !ct.IsCancellationRequested)
        {
            await Task.Delay(100, ct); // Check every 100ms
        }
    }

    /// <summary>
    /// ตรวจสอบว่า Worker ควรหยุดทำงานหรือไม่
    /// </summary>
    public bool ShouldStop(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            return true;
        }
        return worker.StopRequested;
    }

    /// <summary>
    /// ตรวจสอบว่า Worker ถูก Pause หรือไม่
    /// </summary>
    public bool IsPaused(string workerId)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            return false;
        }
        return worker.PauseRequested;
    }

    /// <summary>
    /// Helper method สำหรับ Worker - ตรวจสอบสถานะและ Delay
    /// รวม pause check และ delay ในที่เดียว
    /// </summary>
    public async Task DelayWithPauseCheckAsync(string workerId, int milliseconds, CancellationToken ct)
    {
        var elapsed = 0;
        var checkInterval = 100; // Check every 100ms

        while (elapsed < milliseconds && !ct.IsCancellationRequested)
        {
            // Check for pause
            await CheckPauseAndWaitAsync(workerId, ct);

            // Check for stop
            if (ShouldStop(workerId))
            {
                ct.ThrowIfCancellationRequested();
                return;
            }

            var delay = Math.Min(checkInterval, milliseconds - elapsed);
            await Task.Delay(delay, ct);
            elapsed += delay;
        }
    }

    #endregion

    #region Worker Progress & Reporting

    /// <summary>
    /// อัพเดท progress ของ Worker (เรียกจากภายใน worker)
    /// </summary>
    public void UpdateWorkerProgress(string workerId, string currentTask, double progressPercent, string? details = null)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            return;
        }

        worker.CurrentTaskDescription = currentTask;
        worker.ProgressPercent = progressPercent;
        worker.ProgressDetails = details;
        worker.LastActivityAt = DateTime.UtcNow;
        worker.TasksProcessed++;

        WorkerProgress?.Invoke(this, new WorkerProgressEventArgs(worker, currentTask, progressPercent, details));
    }

    /// <summary>
    /// รายงานผลการทำงานของ Worker
    /// </summary>
    public void ReportWorkResult(string workerId, WorkerReport report)
    {
        if (!_workers.TryGetValue(workerId, out var worker))
        {
            return;
        }

        report.WorkerId = workerId;
        report.WorkerName = worker.Name;
        report.Platform = worker.Platform;
        report.ReportedAt = DateTime.UtcNow;

        // Update worker stats
        if (report.Success)
        {
            worker.SuccessCount++;
        }
        else
        {
            worker.FailureCount++;
        }
        worker.TotalProcessingTime += report.ProcessingTime;

        // Store report
        _reports.TryAdd(report.Id, report);
        _reportHistory.Enqueue(report);

        // Trim history
        while (_reportHistory.Count > _maxHistorySize)
        {
            _reportHistory.TryDequeue(out _);
        }

        WorkerReported?.Invoke(this, new WorkerReportEventArgs(report));
        _logger.LogDebug("Worker {WorkerId} reported: {TaskType} - {Success}",
            workerId, report.TaskType, report.Success ? "Success" : "Failed");
    }

    /// <summary>
    /// สร้างรายงานสรุปของ Worker
    /// </summary>
    private void GenerateWorkerReport(ManagedWorker worker, string reason)
    {
        var report = new WorkerReport
        {
            Id = Guid.NewGuid().ToString(),
            WorkerId = worker.Id,
            WorkerName = worker.Name,
            Platform = worker.Platform,
            TaskType = "WorkerLifecycle",
            Success = worker.ErrorCount == 0,
            Message = reason,
            ProcessingTime = worker.StoppedAt.HasValue && worker.StartedAt.HasValue
                ? worker.StoppedAt.Value - worker.StartedAt.Value
                : TimeSpan.Zero,
            Metadata = new Dictionary<string, object>
            {
                ["TasksProcessed"] = worker.TasksProcessed,
                ["SuccessCount"] = worker.SuccessCount,
                ["FailureCount"] = worker.FailureCount,
                ["ErrorCount"] = worker.ErrorCount,
                ["AverageProcessingTime"] = worker.TasksProcessed > 0
                    ? worker.TotalProcessingTime.TotalMilliseconds / worker.TasksProcessed
                    : 0
            },
            ReportedAt = DateTime.UtcNow
        };

        _reports.TryAdd(report.Id, report);
        _reportHistory.Enqueue(report);

        WorkerReported?.Invoke(this, new WorkerReportEventArgs(report));
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// ดึงข้อมูล Worker ทั้งหมด
    /// </summary>
    public IEnumerable<ManagedWorker> GetAllWorkers() => _workers.Values;

    /// <summary>
    /// ดึงข้อมูล Worker ตาม Platform
    /// </summary>
    public IEnumerable<ManagedWorker> GetWorkersByPlatform(SocialPlatform platform)
        => _workers.Values.Where(w => w.Platform == platform);

    /// <summary>
    /// ดึงข้อมูล Worker ตาม State
    /// </summary>
    public IEnumerable<ManagedWorker> GetWorkersByState(WorkerState state)
        => _workers.Values.Where(w => w.State == state);

    /// <summary>
    /// ดึง Worker ตาม ID
    /// </summary>
    public ManagedWorker? GetWorker(string workerId)
    {
        _workers.TryGetValue(workerId, out var worker);
        return worker;
    }

    /// <summary>
    /// ดึงประวัติ Reports
    /// </summary>
    public IEnumerable<WorkerReport> GetReportHistory(int limit = 100)
        => _reportHistory.TakeLast(limit);

    /// <summary>
    /// ดึงสถิติรวม
    /// </summary>
    public WorkerManagerStats GetStats()
    {
        var workers = _workers.Values.ToList();

        return new WorkerManagerStats
        {
            TotalWorkers = workers.Count,
            ActiveWorkers = workers.Count(w => w.State == WorkerState.Running),
            PausedWorkers = workers.Count(w => w.State == WorkerState.Paused),
            StoppedWorkers = workers.Count(w => w.State == WorkerState.Stopped),
            ErrorWorkers = workers.Count(w => w.State == WorkerState.Error),
            TotalTasksProcessed = workers.Sum(w => w.TasksProcessed),
            TotalSuccesses = workers.Sum(w => w.SuccessCount),
            TotalFailures = workers.Sum(w => w.FailureCount),
            CpuUsagePercent = CpuUsage,
            MemoryUsageMB = MemoryUsageMB,
            TotalCores = TotalCores,
            PlatformStats = Enum.GetValues<SocialPlatform>()
                .ToDictionary(p => p, p => new PlatformWorkerStats
                {
                    Platform = p,
                    WorkerCount = workers.Count(w => w.Platform == p),
                    ActiveCount = workers.Count(w => w.Platform == p && w.State == WorkerState.Running),
                    TasksProcessed = workers.Where(w => w.Platform == p).Sum(w => w.TasksProcessed)
                })
        };
    }

    #endregion

    #region CPU Optimization

    private int _nextCoreIndex = 0;
    private readonly object _coreLock = new();

    /// <summary>
    /// กระจาย Worker ไปยัง CPU cores อย่างเท่าเทียม
    /// </summary>
    private int AssignOptimalCore(SocialPlatform platform)
    {
        lock (_coreLock)
        {
            var core = _nextCoreIndex;
            _nextCoreIndex = (_nextCoreIndex + 1) % TotalCores;
            return core;
        }
    }

    /// <summary>
    /// Set CPU affinity for current thread (Windows only)
    /// </summary>
    private void SetCpuAffinity(int coreIndex)
    {
        if (!OperatingSystem.IsWindows()) return;

        try
        {
            // Set thread affinity mask
            var affinityMask = (IntPtr)(1L << coreIndex);
            // Note: This would require P/Invoke to SetThreadAffinityMask
            // For now, we rely on the OS scheduler
        }
        catch
        {
            // Ignore - affinity setting is optional optimization
        }
    }

    #endregion

    #region System Monitoring

    private async Task MonitorSystemStatsAsync()
    {
        while (!_globalCts.Token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, _globalCts.Token);

                // Update CPU usage using process CPU time calculation
                _currentProcess.Refresh();
                var currentCpuTime = _currentProcess.TotalProcessorTime;
                var currentTime = DateTime.UtcNow;
                var timeDelta = (currentTime - _lastCpuCheck).TotalMilliseconds;

                if (timeDelta > 0)
                {
                    var cpuTimeDelta = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
                    CpuUsage = (cpuTimeDelta / (timeDelta * TotalCores)) * 100;
                    CpuUsage = Math.Min(100, Math.Max(0, CpuUsage)); // Clamp to 0-100
                }

                _lastCpuTime = currentCpuTime;
                _lastCpuCheck = currentTime;

                // Update memory usage
                MemoryUsageMB = _currentProcess.WorkingSet64 / (1024 * 1024);

                // Fire event
                SystemStatsUpdated?.Invoke(this, new SystemStatsEventArgs(
                    CpuUsage, MemoryUsageMB, TotalCores, ActiveWorkers));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Continue monitoring
            }
        }
    }

    #endregion

    #region Platform Worker Integration

    /// <summary>
    /// สร้าง long-running worker สำหรับ Platform Worker ที่รับ tasks จาก queue
    /// </summary>
    public ManagedWorker CreatePlatformWorker(
        string name,
        SocialPlatform platform,
        IPlatformWorker platformWorker,
        Func<CancellationToken, Task<TaskItem?>> getNextTask,
        Func<TaskItem, TaskResult, Task> onTaskComplete)
    {
        return CreateWorker(name, platform, async (worker, ct) =>
        {
            _logger.LogInformation("Platform worker {Name} started for {Platform}", name, platform);

            while (!ct.IsCancellationRequested && !worker.StopRequested)
            {
                try
                {
                    // Check for pause
                    await CheckPauseAndWaitAsync(worker.Id, ct);

                    if (worker.StopRequested) break;

                    // Get next task
                    var task = await getNextTask(ct);

                    if (task == null)
                    {
                        // No task available, wait a bit
                        await DelayWithPauseCheckAsync(worker.Id, 1000, ct);
                        continue;
                    }

                    // Update progress
                    UpdateWorkerProgress(worker.Id, $"Processing: {task.Type}", 0, task.Id);

                    // Execute task based on type
                    TaskResult result;
                    switch (task.Type)
                    {
                        case TaskType.GenerateContent:
                            result = await platformWorker.GenerateContentAsync(task, ct);
                            break;
                        case TaskType.GenerateImage:
                            result = await platformWorker.GenerateImageAsync(task, ct);
                            break;
                        case TaskType.PostContent:
                            result = await platformWorker.PostContentAsync(task, ct);
                            break;
                        case TaskType.AnalyzeMetrics:
                            result = await platformWorker.AnalyzeMetricsAsync(task, ct);
                            break;
                        case TaskType.DeletePost:
                            result = await platformWorker.DeletePostAsync(task, ct);
                            break;
                        case TaskType.SchedulePost:
                            result = await platformWorker.SchedulePostAsync(task, ct);
                            break;
                        default:
                            result = new TaskResult { Success = false, Error = $"Unknown task type: {task.Type}" };
                            break;
                    }

                    // Report result
                    ReportWorkResult(worker.Id, new WorkerReport
                    {
                        TaskType = task.Type.ToString(),
                        Success = result.Success,
                        Message = result.Success ? "Completed" : result.Error,
                        ProcessingTime = TimeSpan.FromMilliseconds(result.ProcessingTimeMs),
                        Metadata = new Dictionary<string, object>
                        {
                            ["TaskId"] = task.Id,
                            ["Platform"] = platform.ToString()
                        }
                    });

                    // Callback
                    await onTaskComplete(task, result);

                    // Update progress
                    UpdateWorkerProgress(worker.Id, "Idle", 100, "Waiting for next task");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in platform worker {Name}", name);
                    worker.LastError = ex.Message;
                    worker.ErrorCount++;

                    // Wait before retry
                    await DelayWithPauseCheckAsync(worker.Id, 5000, ct);
                }
            }

            _logger.LogInformation("Platform worker {Name} stopped", name);
        });
    }

    /// <summary>
    /// สร้าง worker สำหรับ Seek and Post
    /// </summary>
    public ManagedWorker CreateSeekAndPostWorker(
        string name,
        SocialPlatform platform,
        Func<ManagedWorker, CancellationToken, Task> seekAndPostLoop)
    {
        return CreateWorker(name, platform, async (worker, ct) =>
        {
            _logger.LogInformation("SeekAndPost worker {Name} started for {Platform}", name, platform);

            while (!ct.IsCancellationRequested && !worker.StopRequested)
            {
                try
                {
                    // Check for pause
                    await CheckPauseAndWaitAsync(worker.Id, ct);

                    if (worker.StopRequested) break;

                    // Run the seek and post logic
                    await seekAndPostLoop(worker, ct);

                    // Delay between iterations
                    await DelayWithPauseCheckAsync(worker.Id, 30000, ct); // 30 seconds between searches
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in SeekAndPost worker {Name}", name);
                    worker.LastError = ex.Message;
                    worker.ErrorCount++;

                    await DelayWithPauseCheckAsync(worker.Id, 60000, ct); // Wait 1 min on error
                }
            }

            _logger.LogInformation("SeekAndPost worker {Name} stopped", name);
        });
    }

    #endregion

    #region Event Handlers

    private void OnWorkerStateChanged(ManagedWorker worker, WorkerState newState, string reason)
    {
        WorkerStateChanged?.Invoke(this, new WorkerStateChangedEventArgs(worker, newState, reason));
    }

    #endregion

    public void Dispose()
    {
        _globalCts.Cancel();
        _globalCts.Dispose();
        _currentProcess?.Dispose();
    }
}

#region Models

/// <summary>
/// Worker State
/// </summary>
public enum WorkerState
{
    Created,
    Running,
    Paused,
    Stopped,
    Error
}

/// <summary>
/// Managed Worker with full state tracking
/// </summary>
public class ManagedWorker
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public WorkerState State { get; set; }

    // Timing
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public DateTime LastActivityAt { get; set; }

    // Current work
    public string? CurrentTaskDescription { get; set; }
    public double ProgressPercent { get; set; }
    public string? ProgressDetails { get; set; }

    // Statistics
    public int TasksProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan TotalProcessingTime { get; set; }

    // Errors
    public string? LastError { get; set; }

    // Control flags
    public bool PauseRequested { get; set; }
    public bool StopRequested { get; set; }

    // CPU assignment
    public int PreferredCore { get; set; }

    // Internal
    internal Func<ManagedWorker, CancellationToken, Task>? WorkFunction { get; set; }
    internal Task? WorkerTask { get; set; }
    internal CancellationTokenSource? WorkerCts { get; set; }

    // Helper properties
    public double SuccessRate => TasksProcessed > 0
        ? (double)SuccessCount / TasksProcessed * 100
        : 0;

    public TimeSpan Uptime => State == WorkerState.Running && StartedAt.HasValue
        ? DateTime.UtcNow - StartedAt.Value
        : TimeSpan.Zero;

    public string StateEmoji => State switch
    {
        WorkerState.Created => "⚪",   // White - created
        WorkerState.Running => "🟢",  // Green - running
        WorkerState.Paused => "🟡",   // Yellow - paused
        WorkerState.Stopped => "⚫",  // Black - stopped
        WorkerState.Error => "🔴",    // Red - error
        _ => "⚪"
    };
}

/// <summary>
/// Worker Report - รายงานผลการทำงาน
/// </summary>
public class WorkerReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkerId { get; set; } = "";
    public string WorkerName { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public string TaskType { get; set; } = "";
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime ReportedAt { get; set; }
}

/// <summary>
/// Worker Manager Statistics
/// </summary>
public class WorkerManagerStats
{
    public int TotalWorkers { get; set; }
    public int ActiveWorkers { get; set; }
    public int PausedWorkers { get; set; }
    public int StoppedWorkers { get; set; }
    public int ErrorWorkers { get; set; }
    public long TotalTasksProcessed { get; set; }
    public long TotalSuccesses { get; set; }
    public long TotalFailures { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public int TotalCores { get; set; }
    public Dictionary<SocialPlatform, PlatformWorkerStats> PlatformStats { get; set; } = new();
}

/// <summary>
/// Platform Worker Statistics
/// </summary>
public class PlatformWorkerStats
{
    public SocialPlatform Platform { get; set; }
    public int WorkerCount { get; set; }
    public int ActiveCount { get; set; }
    public long TasksProcessed { get; set; }
}

#endregion

#region Event Args

public class WorkerStateChangedEventArgs : EventArgs
{
    public ManagedWorker Worker { get; }
    public WorkerState NewState { get; }
    public string Reason { get; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    public WorkerStateChangedEventArgs(ManagedWorker worker, WorkerState newState, string reason)
    {
        Worker = worker;
        NewState = newState;
        Reason = reason;
    }
}

public class WorkerProgressEventArgs : EventArgs
{
    public ManagedWorker Worker { get; }
    public string CurrentTask { get; }
    public double ProgressPercent { get; }
    public string? Details { get; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    public WorkerProgressEventArgs(ManagedWorker worker, string currentTask, double progressPercent, string? details)
    {
        Worker = worker;
        CurrentTask = currentTask;
        ProgressPercent = progressPercent;
        Details = details;
    }
}

public class WorkerReportEventArgs : EventArgs
{
    public WorkerReport Report { get; }

    public WorkerReportEventArgs(WorkerReport report)
    {
        Report = report;
    }
}

public class SystemStatsEventArgs : EventArgs
{
    public double CpuUsagePercent { get; }
    public long MemoryUsageMB { get; }
    public int TotalCores { get; }
    public int ActiveWorkers { get; }

    public SystemStatsEventArgs(double cpuUsage, long memoryMB, int cores, int activeWorkers)
    {
        CpuUsagePercent = cpuUsage;
        MemoryUsageMB = memoryMB;
        TotalCores = cores;
        ActiveWorkers = activeWorkers;
    }
}

#endregion
