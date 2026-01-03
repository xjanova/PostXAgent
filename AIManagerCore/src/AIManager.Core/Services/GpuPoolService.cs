using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Service for managing distributed GPU workers in a mining pool-like architecture
/// </summary>
public class GpuPoolService : IDisposable
{
    private readonly ILogger<GpuPoolService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentDictionary<string, GpuWorkerInfo> _workers = new();
    private readonly ConcurrentDictionary<string, GpuTaskInfo> _activeTasks = new();
    private readonly SemaphoreSlim _taskLock = new(1, 1);
    private Timer? _healthCheckTimer;
    private bool _disposed;

    /// <summary>
    /// Distribution mode for task allocation
    /// </summary>
    public GpuDistributionMode DistributionMode { get; set; } = GpuDistributionMode.Parallel;

    /// <summary>
    /// Event raised when worker status changes
    /// </summary>
    public event EventHandler<GpuWorkerEventArgs>? WorkerStatusChanged;

    /// <summary>
    /// Event raised when a task completes
    /// </summary>
    public event EventHandler<GpuTaskEventArgs>? TaskCompleted;

    public GpuPoolService(ILogger<GpuPoolService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        // Start health check timer (every 30 seconds)
        _healthCheckTimer = new Timer(
            async _ => await CheckAllWorkersHealthAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Gets all registered workers
    /// </summary>
    public IReadOnlyList<GpuWorkerInfo> Workers => _workers.Values.ToList();

    /// <summary>
    /// Gets online workers only
    /// </summary>
    public IReadOnlyList<GpuWorkerInfo> OnlineWorkers =>
        _workers.Values.Where(w => w.Status == GpuWorkerStatus.Online).ToList();

    /// <summary>
    /// Gets total available VRAM across all online workers
    /// </summary>
    public double TotalAvailableVram =>
        OnlineWorkers.Sum(w => w.FreeVramGb);

    /// <summary>
    /// Gets number of queued tasks
    /// </summary>
    public int QueuedTaskCount => _activeTasks.Count(t => t.Value.Status == "pending");

    /// <summary>
    /// Gets number of completed tasks (session)
    /// </summary>
    public int CompletedTaskCount { get; private set; }

    /// <summary>
    /// Add a new GPU worker to the pool
    /// </summary>
    public async Task<GpuWorkerInfo?> AddWorkerAsync(string name, string url)
    {
        try
        {
            var workerId = Guid.NewGuid().ToString("N")[..8];
            var worker = new GpuWorkerInfo
            {
                Id = workerId,
                Name = name,
                Url = url.TrimEnd('/'),
                Status = GpuWorkerStatus.Connecting,
                AddedAt = DateTime.UtcNow
            };

            // Try to connect and get status
            var status = await GetWorkerStatusAsync(worker.Url);
            if (status != null)
            {
                worker.Status = GpuWorkerStatus.Online;
                worker.GpuName = status.GpuName;
                worker.TotalVramGb = status.TotalVramGb;
                worker.FreeVramGb = status.FreeVramGb;
                worker.CurrentModel = status.CurrentModel;
                worker.LastSeen = DateTime.UtcNow;

                _workers[workerId] = worker;
                _logger.LogInformation("GPU Worker added: {Name} ({Gpu}) - {Vram}GB",
                    name, worker.GpuName, worker.TotalVramGb);

                WorkerStatusChanged?.Invoke(this, new GpuWorkerEventArgs(worker, "added"));
                return worker;
            }

            worker.Status = GpuWorkerStatus.Offline;
            worker.LastError = "Failed to connect";
            _workers[workerId] = worker;

            _logger.LogWarning("GPU Worker added but offline: {Name} at {Url}", name, url);
            WorkerStatusChanged?.Invoke(this, new GpuWorkerEventArgs(worker, "added"));
            return worker;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add worker: {Name} at {Url}", name, url);
            return null;
        }
    }

    /// <summary>
    /// Remove a worker from the pool
    /// </summary>
    public bool RemoveWorker(string workerId)
    {
        if (_workers.TryRemove(workerId, out var worker))
        {
            _logger.LogInformation("GPU Worker removed: {Name}", worker.Name);
            WorkerStatusChanged?.Invoke(this, new GpuWorkerEventArgs(worker, "removed"));
            return true;
        }
        return false;
    }

    /// <summary>
    /// Submit an image generation task to the pool
    /// </summary>
    public async Task<GpuTaskResult> GenerateImageAsync(
        GpuImageRequest request,
        CancellationToken ct = default)
    {
        await _taskLock.WaitAsync(ct);
        try
        {
            var worker = SelectBestWorker(request.ModelId, request.RequiredVramGb);
            if (worker == null)
            {
                return new GpuTaskResult
                {
                    Success = false,
                    Error = "No available GPU workers"
                };
            }

            var taskId = Guid.NewGuid().ToString();
            var task = new GpuTaskInfo
            {
                TaskId = taskId,
                WorkerId = worker.Id,
                Type = "image",
                Status = "processing",
                StartedAt = DateTime.UtcNow
            };
            _activeTasks[taskId] = task;
            worker.CurrentTask = taskId;

            try
            {
                var result = await SendImageRequestAsync(worker, request, ct);

                task.Status = result.Success ? "completed" : "failed";
                task.CompletedAt = DateTime.UtcNow;
                worker.CurrentTask = null;

                if (result.Success) CompletedTaskCount++;

                TaskCompleted?.Invoke(this, new GpuTaskEventArgs(task, result));
                return result;
            }
            catch (Exception ex)
            {
                task.Status = "failed";
                task.Error = ex.Message;
                worker.CurrentTask = null;
                return new GpuTaskResult { Success = false, Error = ex.Message };
            }
            finally
            {
                _activeTasks.TryRemove(taskId, out _);
            }
        }
        finally
        {
            _taskLock.Release();
        }
    }

    /// <summary>
    /// Select the best available worker based on requirements
    /// </summary>
    private GpuWorkerInfo? SelectBestWorker(string? modelId, double requiredVram)
    {
        var candidates = OnlineWorkers
            .Where(w => w.CurrentTask == null)
            .Where(w => w.FreeVramGb >= requiredVram)
            .ToList();

        if (!candidates.Any())
            return null;

        // Prefer workers with the model already loaded
        if (!string.IsNullOrEmpty(modelId))
        {
            var withModel = candidates.FirstOrDefault(w => w.CurrentModel == modelId);
            if (withModel != null)
                return withModel;
        }

        // Otherwise, select worker with most free VRAM
        return candidates.OrderByDescending(w => w.FreeVramGb).First();
    }

    /// <summary>
    /// Send image generation request to a worker
    /// </summary>
    private async Task<GpuTaskResult> SendImageRequestAsync(
        GpuWorkerInfo worker,
        GpuImageRequest request,
        CancellationToken ct)
    {
        var requestBody = new
        {
            prompt = request.Prompt,
            negative_prompt = request.NegativePrompt,
            width = request.Width,
            height = request.Height,
            steps = request.Steps,
            guidance_scale = request.GuidanceScale,
            seed = request.Seed,
            batch_size = request.BatchSize,
            model_id = request.ModelId
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{worker.Url}/generate/image", content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            var result = JsonSerializer.Deserialize<GpuImageResponse>(responseJson);
            return new GpuTaskResult
            {
                Success = true,
                TaskId = result?.TaskId,
                Images = result?.Result?.Images ?? new List<string>(),
                Seed = result?.Result?.Seed ?? -1,
                GenerationTime = result?.Result?.GenerationTime ?? 0
            };
        }

        return new GpuTaskResult
        {
            Success = false,
            Error = $"Worker error: {response.StatusCode}"
        };
    }

    /// <summary>
    /// Get worker status from API
    /// </summary>
    private async Task<GpuWorkerStatusResponse?> GetWorkerStatusAsync(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{url}/status");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GpuWorkerStatusResponse>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to get worker status from {Url}: {Error}", url, ex.Message);
        }
        return null;
    }

    /// <summary>
    /// Check health of all workers
    /// </summary>
    private async Task CheckAllWorkersHealthAsync()
    {
        foreach (var worker in _workers.Values.ToList())
        {
            try
            {
                var status = await GetWorkerStatusAsync(worker.Url);
                var previousStatus = worker.Status;

                if (status != null)
                {
                    worker.Status = GpuWorkerStatus.Online;
                    worker.GpuName = status.GpuName;
                    worker.TotalVramGb = status.TotalVramGb;
                    worker.FreeVramGb = status.FreeVramGb;
                    worker.CurrentModel = status.CurrentModel;
                    worker.LastSeen = DateTime.UtcNow;
                    worker.LastError = null;
                }
                else
                {
                    worker.Status = GpuWorkerStatus.Offline;
                    worker.LastError = "Connection failed";
                }

                if (previousStatus != worker.Status)
                {
                    WorkerStatusChanged?.Invoke(this,
                        new GpuWorkerEventArgs(worker, worker.Status == GpuWorkerStatus.Online ? "online" : "offline"));
                }
            }
            catch (Exception ex)
            {
                worker.Status = GpuWorkerStatus.Offline;
                worker.LastError = ex.Message;
                _logger.LogDebug("Health check failed for {Name}: {Error}", worker.Name, ex.Message);
            }
        }
    }

    /// <summary>
    /// Refresh all workers immediately
    /// </summary>
    public async Task RefreshAllWorkersAsync()
    {
        await CheckAllWorkersHealthAsync();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _healthCheckTimer?.Dispose();
        _httpClient.Dispose();
        _taskLock.Dispose();
    }
}

#region Models

/// <summary>
/// GPU worker distribution mode
/// </summary>
public enum GpuDistributionMode
{
    /// <summary>
    /// Each worker handles one task at a time (parallel processing)
    /// </summary>
    Parallel,

    /// <summary>
    /// All workers combine to handle one large task (like mining pool)
    /// </summary>
    Combined
}

/// <summary>
/// GPU worker status
/// </summary>
public enum GpuWorkerStatus
{
    Offline,
    Connecting,
    Online,
    Busy
}

/// <summary>
/// GPU worker information
/// </summary>
public class GpuWorkerInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public GpuWorkerStatus Status { get; set; } = GpuWorkerStatus.Offline;
    public string? GpuName { get; set; }
    public double TotalVramGb { get; set; }
    public double FreeVramGb { get; set; }
    public string? CurrentModel { get; set; }
    public string? CurrentTask { get; set; }
    public DateTime AddedAt { get; set; }
    public DateTime LastSeen { get; set; }
    public string? LastError { get; set; }

    public bool IsOnline => Status == GpuWorkerStatus.Online;
    public bool IsBusy => !string.IsNullOrEmpty(CurrentTask);
}

/// <summary>
/// GPU task information
/// </summary>
public class GpuTaskInfo
{
    public string TaskId { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Image generation request
/// </summary>
public class GpuImageRequest
{
    public string Prompt { get; set; } = "";
    public string? NegativePrompt { get; set; }
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int Steps { get; set; } = 30;
    public double GuidanceScale { get; set; } = 7.5;
    public int Seed { get; set; } = -1;
    public int BatchSize { get; set; } = 1;
    public string ModelId { get; set; } = "stabilityai/stable-diffusion-xl-base-1.0";
    public double RequiredVramGb { get; set; } = 8.0;
}

/// <summary>
/// Task result
/// </summary>
public class GpuTaskResult
{
    public bool Success { get; set; }
    public string? TaskId { get; set; }
    public string? Error { get; set; }
    public List<string> Images { get; set; } = new();
    public int Seed { get; set; }
    public double GenerationTime { get; set; }
}

/// <summary>
/// Worker status response from API
/// </summary>
public class GpuWorkerStatusResponse
{
    public string? Status { get; set; }
    public int GpuCount { get; set; }
    public List<GpuInfo>? Gpus { get; set; }
    public double TotalVramGb { get; set; }
    public double FreeVramGb { get; set; }
    public string? CurrentModel { get; set; }

    public string? GpuName => Gpus?.FirstOrDefault()?.Name;

    public class GpuInfo
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public double MemoryTotalGb { get; set; }
        public double MemoryFreeGb { get; set; }
    }
}

/// <summary>
/// Image generation response from worker
/// </summary>
public class GpuImageResponse
{
    public string? TaskId { get; set; }
    public string? Status { get; set; }
    public GpuImageResult? Result { get; set; }

    public class GpuImageResult
    {
        public List<string>? Images { get; set; }
        public int Seed { get; set; }
        public double GenerationTime { get; set; }
    }
}

/// <summary>
/// Event args for worker status changes
/// </summary>
public class GpuWorkerEventArgs : EventArgs
{
    public GpuWorkerInfo Worker { get; }
    public string EventType { get; }

    public GpuWorkerEventArgs(GpuWorkerInfo worker, string eventType)
    {
        Worker = worker;
        EventType = eventType;
    }
}

/// <summary>
/// Event args for task completion
/// </summary>
public class GpuTaskEventArgs : EventArgs
{
    public GpuTaskInfo Task { get; }
    public GpuTaskResult Result { get; }

    public GpuTaskEventArgs(GpuTaskInfo task, GpuTaskResult result)
    {
        Task = task;
        Result = result;
    }
}

#endregion
