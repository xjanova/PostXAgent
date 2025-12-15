using Microsoft.AspNetCore.SignalR;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.API.Hubs;

/// <summary>
/// SignalR Hub for real-time communication with Laravel/Frontend
/// </summary>
public class AIManagerHub : Hub
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly ILogger<AIManagerHub> _logger;

    public AIManagerHub(ProcessOrchestrator orchestrator, ILogger<AIManagerHub> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;

        // Subscribe to events and forward to clients
        _orchestrator.TaskReceived += async (s, e) =>
        {
            await BroadcastTaskUpdate("TaskReceived", e.Task);
        };

        _orchestrator.TaskCompleted += async (s, e) =>
        {
            await BroadcastTaskUpdate("TaskCompleted", e.Task);
        };

        _orchestrator.TaskFailed += async (s, e) =>
        {
            await BroadcastTaskUpdate("TaskFailed", e.Task);
        };

        _orchestrator.StatsUpdated += async (s, e) =>
        {
            await BroadcastStats(e.Stats);
        };

        _orchestrator.WorkerStatusChanged += async (s, e) =>
        {
            await BroadcastWorkerStatus(e.Worker, e.Status);
        };
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);

        // Send current state to new client
        await Clients.Caller.SendAsync("Connected", new
        {
            Stats = _orchestrator.Stats,
            Workers = _orchestrator.GetWorkers(),
            IsRunning = _orchestrator.IsRunning
        });

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Submit a task from client
    /// </summary>
    public async Task<string> SubmitTask(TaskItem task)
    {
        return await _orchestrator.SubmitTaskAsync(task);
    }

    /// <summary>
    /// Get current stats
    /// </summary>
    public Task<OrchestratorStats> GetStats()
    {
        return Task.FromResult(_orchestrator.Stats);
    }

    /// <summary>
    /// Get all workers
    /// </summary>
    public Task<IEnumerable<WorkerInfo>> GetWorkers()
    {
        return Task.FromResult(_orchestrator.GetWorkers());
    }

    /// <summary>
    /// Subscribe to specific platform updates
    /// </summary>
    public async Task SubscribeToPlatform(string platform)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"platform_{platform}");
        _logger.LogDebug("Client {ConnectionId} subscribed to platform {Platform}",
            Context.ConnectionId, platform);
    }

    /// <summary>
    /// Unsubscribe from platform updates
    /// </summary>
    public async Task UnsubscribeFromPlatform(string platform)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"platform_{platform}");
    }

    // Broadcast methods
    private async Task BroadcastTaskUpdate(string eventName, TaskItem task)
    {
        await Clients.All.SendAsync(eventName, task);
        await Clients.Group($"platform_{task.Platform}").SendAsync($"Platform{eventName}", task);
    }

    private async Task BroadcastStats(OrchestratorStats stats)
    {
        await Clients.All.SendAsync("StatsUpdated", stats);
    }

    private async Task BroadcastWorkerStatus(WorkerInfo worker, string status)
    {
        await Clients.All.SendAsync("WorkerStatusChanged", new { Worker = worker, Status = status });
    }
}
