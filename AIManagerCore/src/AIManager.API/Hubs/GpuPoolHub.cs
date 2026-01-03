using Microsoft.AspNetCore.SignalR;
using AIManager.Core.Services;

namespace AIManager.API.Hubs;

/// <summary>
/// SignalR Hub for real-time GPU Pool updates
/// </summary>
public class GpuPoolHub : Hub
{
    private readonly GpuPoolService _gpuPoolService;
    private readonly ILogger<GpuPoolHub> _logger;

    public GpuPoolHub(GpuPoolService gpuPoolService, ILogger<GpuPoolHub> logger)
    {
        _gpuPoolService = gpuPoolService;
        _logger = logger;

        // Subscribe to GpuPoolService events
        _gpuPoolService.WorkerStatusChanged += async (s, e) =>
        {
            await BroadcastWorkerUpdate(e.Worker, e.EventType);
        };

        _gpuPoolService.TaskCompleted += async (s, e) =>
        {
            await BroadcastTaskCompleted(e.Task, e.Result);
        };
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("GPU Pool client connected: {ConnectionId}", Context.ConnectionId);

        // Send current state to new client
        await Clients.Caller.SendAsync("Connected", new
        {
            Workers = _gpuPoolService.Workers,
            Stats = GetPoolStats(),
            DistributionMode = _gpuPoolService.DistributionMode.ToString()
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("GPU Pool client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Get all workers
    /// </summary>
    public Task<IReadOnlyList<GpuWorkerInfo>> GetWorkers()
    {
        return Task.FromResult(_gpuPoolService.Workers);
    }

    /// <summary>
    /// Get pool statistics
    /// </summary>
    public Task<object> GetStats()
    {
        return Task.FromResult(GetPoolStats());
    }

    /// <summary>
    /// Add a new worker
    /// </summary>
    public async Task<GpuWorkerInfo?> AddWorker(string name, string url)
    {
        var worker = await _gpuPoolService.AddWorkerAsync(name, url);
        if (worker != null)
        {
            await Clients.All.SendAsync("WorkerAdded", worker);
            await Clients.All.SendAsync("StatsUpdated", GetPoolStats());
        }
        return worker;
    }

    /// <summary>
    /// Remove a worker
    /// </summary>
    public async Task<bool> RemoveWorker(string workerId)
    {
        var result = _gpuPoolService.RemoveWorker(workerId);
        if (result)
        {
            await Clients.All.SendAsync("WorkerRemoved", workerId);
            await Clients.All.SendAsync("StatsUpdated", GetPoolStats());
        }
        return result;
    }

    /// <summary>
    /// Refresh all workers
    /// </summary>
    public async Task RefreshWorkers()
    {
        await _gpuPoolService.RefreshAllWorkersAsync();
        await Clients.All.SendAsync("WorkersRefreshed", _gpuPoolService.Workers);
        await Clients.All.SendAsync("StatsUpdated", GetPoolStats());
    }

    /// <summary>
    /// Set distribution mode
    /// </summary>
    public async Task SetDistributionMode(string mode)
    {
        if (Enum.TryParse<GpuDistributionMode>(mode, true, out var distributionMode))
        {
            _gpuPoolService.DistributionMode = distributionMode;
            await Clients.All.SendAsync("ModeChanged", distributionMode.ToString());
            _logger.LogInformation("Distribution mode changed to {Mode}", distributionMode);
        }
    }

    /// <summary>
    /// Generate image via SignalR (with real-time progress)
    /// </summary>
    public async Task<GpuTaskResult> GenerateImage(GpuImageRequest request)
    {
        // Notify all clients that generation started
        await Clients.Caller.SendAsync("GenerationStarted", new
        {
            Prompt = request.Prompt,
            Model = request.ModelId,
            Size = $"{request.Width}x{request.Height}"
        });

        var result = await _gpuPoolService.GenerateImageAsync(request);

        if (result.Success)
        {
            await Clients.Caller.SendAsync("GenerationCompleted", result);
        }
        else
        {
            await Clients.Caller.SendAsync("GenerationFailed", result.Error);
        }

        return result;
    }

    // Helper methods
    private object GetPoolStats()
    {
        return new
        {
            TotalWorkers = _gpuPoolService.Workers.Count,
            OnlineWorkers = _gpuPoolService.OnlineWorkers.Count,
            TotalVramGb = _gpuPoolService.Workers.Sum(w => w.TotalVramGb),
            AvailableVramGb = _gpuPoolService.TotalAvailableVram,
            QueuedTasks = _gpuPoolService.QueuedTaskCount,
            CompletedTasks = _gpuPoolService.CompletedTaskCount,
            DistributionMode = _gpuPoolService.DistributionMode.ToString()
        };
    }

    private async Task BroadcastWorkerUpdate(GpuWorkerInfo worker, string eventType)
    {
        await Clients.All.SendAsync("WorkerStatusChanged", new
        {
            Worker = worker,
            EventType = eventType
        });
        await Clients.All.SendAsync("StatsUpdated", GetPoolStats());
    }

    private async Task BroadcastTaskCompleted(GpuTaskInfo task, GpuTaskResult result)
    {
        await Clients.All.SendAsync("TaskCompleted", new
        {
            Task = task,
            Result = new
            {
                result.Success,
                result.TaskId,
                result.Seed,
                result.GenerationTime,
                ImageCount = result.Images.Count
            }
        });
        await Clients.All.SendAsync("StatsUpdated", GetPoolStats());
    }
}
