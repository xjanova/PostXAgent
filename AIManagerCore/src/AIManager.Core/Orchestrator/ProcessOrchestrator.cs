using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;
using AIManager.Core.Workers;

namespace AIManager.Core.Orchestrator;

/// <summary>
/// High-performance multi-threaded task orchestrator
/// Utilizes all available CPU cores (40+) for maximum parallelism
/// </summary>
public class ProcessOrchestrator : IDisposable
{
    private readonly ILogger<ProcessOrchestrator> _logger;
    private readonly OrchestratorConfig _config;
    private readonly CancellationTokenSource _cts;

    // Task channels for each platform (high-performance producer-consumer)
    private readonly Dictionary<SocialPlatform, Channel<TaskItem>> _taskChannels;
    private readonly Channel<TaskResult> _resultChannel;

    // Worker pools
    private readonly ConcurrentDictionary<string, WorkerInfo> _workers;
    private readonly List<Task> _workerTasks;

    // Statistics
    private readonly OrchestratorStats _stats;
    private readonly object _statsLock = new();

    // Events for UI updates
    public event EventHandler<TaskEventArgs>? TaskReceived;
    public event EventHandler<TaskEventArgs>? TaskCompleted;
    public event EventHandler<TaskEventArgs>? TaskFailed;
    public event EventHandler<WorkerEventArgs>? WorkerStatusChanged;
    public event EventHandler<StatsEventArgs>? StatsUpdated;

    public int TotalCores { get; }
    public int ActiveWorkers => _workers.Count(w => w.Value.IsActive);
    public bool IsRunning { get; private set; }
    public OrchestratorStats Stats => _stats;

    public ProcessOrchestrator(ILogger<ProcessOrchestrator> logger, OrchestratorConfig config)
    {
        _logger = logger;
        _config = config;
        _cts = new CancellationTokenSource();

        TotalCores = config.NumCores > 0 ? config.NumCores : Environment.ProcessorCount;
        _logger.LogInformation("Initializing ProcessOrchestrator with {Cores} CPU cores", TotalCores);

        // Initialize channels
        _taskChannels = new Dictionary<SocialPlatform, Channel<TaskItem>>();
        foreach (SocialPlatform platform in Enum.GetValues<SocialPlatform>())
        {
            _taskChannels[platform] = Channel.CreateBounded<TaskItem>(
                new BoundedChannelOptions(1000)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = false
                });
        }

        _resultChannel = Channel.CreateUnbounded<TaskResult>();
        _workers = new ConcurrentDictionary<string, WorkerInfo>();
        _workerTasks = new List<Task>();
        _stats = new OrchestratorStats();
    }

    /// <summary>
    /// Start the orchestrator and all worker threads
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning) return;

        _logger.LogInformation("Starting ProcessOrchestrator...");
        IsRunning = true;
        _stats.StartTime = DateTime.UtcNow;

        // Calculate workers per platform
        int platformCount = Enum.GetValues<SocialPlatform>().Length;
        int workersPerPlatform = Math.Max(2, TotalCores / platformCount);

        _logger.LogInformation("Allocating {Workers} workers per platform", workersPerPlatform);

        // Create worker threads for each platform
        int workerId = 0;
        foreach (SocialPlatform platform in Enum.GetValues<SocialPlatform>())
        {
            for (int i = 0; i < workersPerPlatform; i++)
            {
                var worker = CreateWorker(workerId++, platform);
                var task = Task.Run(() => WorkerLoopAsync(worker, _cts.Token));
                _workerTasks.Add(task);
            }
        }

        // Allocate remaining cores to high-traffic platforms
        var highTrafficPlatforms = new[]
        {
            SocialPlatform.Facebook,
            SocialPlatform.Instagram,
            SocialPlatform.TikTok,
            SocialPlatform.Line
        };

        int remainingCores = TotalCores - workerId;
        foreach (var platform in highTrafficPlatforms)
        {
            if (remainingCores <= 0) break;
            var worker = CreateWorker(workerId++, platform);
            var task = Task.Run(() => WorkerLoopAsync(worker, _cts.Token));
            _workerTasks.Add(task);
            remainingCores--;
        }

        // Start result processor
        _ = Task.Run(() => ProcessResultsAsync(_cts.Token));

        // Start stats reporter
        _ = Task.Run(() => ReportStatsAsync(_cts.Token));

        _logger.LogInformation("ProcessOrchestrator started with {Workers} workers", _workers.Count);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stop the orchestrator gracefully
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning) return;

        _logger.LogInformation("Stopping ProcessOrchestrator...");
        IsRunning = false;

        _cts.Cancel();

        try
        {
            await Task.WhenAll(_workerTasks.Where(t => !t.IsCompleted))
                .WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Some workers did not stop gracefully");
        }

        _logger.LogInformation("ProcessOrchestrator stopped");
    }

    /// <summary>
    /// Submit a task for processing
    /// </summary>
    public async Task<string> SubmitTaskAsync(TaskItem task)
    {
        task.Id ??= Guid.NewGuid().ToString();
        task.CreatedAt = DateTime.UtcNow;
        task.Status = Models.TaskStatus.Queued;

        var channel = _taskChannels[task.Platform];
        await channel.Writer.WriteAsync(task);

        lock (_statsLock)
        {
            _stats.TasksQueued++;
        }

        TaskReceived?.Invoke(this, new TaskEventArgs(task));
        _logger.LogDebug("Task {TaskId} submitted for platform {Platform}", task.Id, task.Platform);

        return task.Id;
    }

    /// <summary>
    /// Get task status
    /// </summary>
    public TaskItem? GetTask(string taskId)
    {
        // This would be enhanced with a proper task store
        return null;
    }

    /// <summary>
    /// Cancel a pending task
    /// </summary>
    public bool CancelTask(string taskId)
    {
        // Implementation for task cancellation
        return false;
    }

    private WorkerInfo CreateWorker(int workerId, SocialPlatform platform)
    {
        var worker = new WorkerInfo
        {
            Id = workerId,
            Name = $"{platform}_{workerId}",
            Platform = platform,
            IsActive = true,
            StartedAt = DateTime.UtcNow
        };

        _workers.TryAdd(worker.Name, worker);
        WorkerStatusChanged?.Invoke(this, new WorkerEventArgs(worker, "Started"));

        return worker;
    }

    private async Task WorkerLoopAsync(WorkerInfo worker, CancellationToken ct)
    {
        var channel = _taskChannels[worker.Platform];
        _logger.LogDebug("Worker {WorkerName} started", worker.Name);

        try
        {
            await foreach (var task in channel.Reader.ReadAllAsync(ct))
            {
                worker.CurrentTask = task;
                worker.TasksProcessed++;
                task.Status = Models.TaskStatus.Running;
                task.StartedAt = DateTime.UtcNow;

                try
                {
                    var result = await ProcessTaskAsync(worker, task, ct);
                    await _resultChannel.Writer.WriteAsync(result, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Worker {WorkerName} error processing task {TaskId}",
                        worker.Name, task.Id);

                    await _resultChannel.Writer.WriteAsync(new TaskResult
                    {
                        TaskId = task.Id!,
                        WorkerId = worker.Id,
                        Success = false,
                        Error = ex.Message
                    }, ct);
                }
                finally
                {
                    worker.CurrentTask = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        finally
        {
            worker.IsActive = false;
            WorkerStatusChanged?.Invoke(this, new WorkerEventArgs(worker, "Stopped"));
            _logger.LogDebug("Worker {WorkerName} stopped", worker.Name);
        }
    }

    private async Task<TaskResult> ProcessTaskAsync(WorkerInfo worker, TaskItem task, CancellationToken ct)
    {
        _logger.LogDebug("Worker {WorkerName} processing task {TaskId} of type {TaskType}",
            worker.Name, task.Id, task.Type);

        var result = new TaskResult
        {
            TaskId = task.Id!,
            WorkerId = worker.Id,
            Platform = task.Platform.ToString()
        };

        try
        {
            // Get the appropriate worker for the platform
            var platformWorker = WorkerFactory.CreateWorker(task.Platform);

            result = task.Type switch
            {
                TaskType.GenerateContent => await platformWorker.GenerateContentAsync(task, ct),
                TaskType.GenerateImage => await platformWorker.GenerateImageAsync(task, ct),
                TaskType.PostContent => await platformWorker.PostContentAsync(task, ct),
                TaskType.SchedulePost => await platformWorker.SchedulePostAsync(task, ct),
                TaskType.AnalyzeMetrics => await platformWorker.AnalyzeMetricsAsync(task, ct),
                TaskType.DeletePost => await platformWorker.DeletePostAsync(task, ct),
                _ => throw new ArgumentException($"Unknown task type: {task.Type}")
            };

            result.TaskId = task.Id!;
            result.WorkerId = worker.Id;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task ProcessResultsAsync(CancellationToken ct)
    {
        _logger.LogDebug("Result processor started");

        try
        {
            await foreach (var result in _resultChannel.Reader.ReadAllAsync(ct))
            {
                lock (_statsLock)
                {
                    if (result.Success)
                    {
                        _stats.TasksCompleted++;
                        TaskCompleted?.Invoke(this, new TaskEventArgs(new TaskItem
                        {
                            Id = result.TaskId,
                            Status = Models.TaskStatus.Completed
                        }));
                    }
                    else
                    {
                        _stats.TasksFailed++;
                        TaskFailed?.Invoke(this, new TaskEventArgs(new TaskItem
                        {
                            Id = result.TaskId,
                            Status = Models.TaskStatus.Failed
                        }));
                    }
                }

                // Send result to Laravel via callback or message queue
                await NotifyLaravelAsync(result, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
    }

    private async Task NotifyLaravelAsync(TaskResult result, CancellationToken ct)
    {
        // This will be implemented via SignalR or Redis
        await Task.CompletedTask;
    }

    private async Task ReportStatsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                lock (_statsLock)
                {
                    _stats.ActiveWorkers = _workers.Count(w => w.Value.IsActive);
                    _stats.TotalWorkers = _workers.Count;
                    _stats.Uptime = DateTime.UtcNow - _stats.StartTime;
                    _stats.TasksPerSecond = _stats.TasksCompleted / Math.Max(1, _stats.Uptime.TotalSeconds);
                }

                StatsUpdated?.Invoke(this, new StatsEventArgs(_stats));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public IEnumerable<WorkerInfo> GetWorkers() => _workers.Values;

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        foreach (var channel in _taskChannels.Values)
        {
            channel.Writer.Complete();
        }
        _resultChannel.Writer.Complete();
    }
}

// Event Args
public class TaskEventArgs : EventArgs
{
    public TaskItem Task { get; }
    public TaskEventArgs(TaskItem task) => Task = task;
}

public class WorkerEventArgs : EventArgs
{
    public WorkerInfo Worker { get; }
    public string Status { get; }
    public WorkerEventArgs(WorkerInfo worker, string status)
    {
        Worker = worker;
        Status = status;
    }
}

public class StatsEventArgs : EventArgs
{
    public OrchestratorStats Stats { get; }
    public StatsEventArgs(OrchestratorStats stats) => Stats = stats;
}
