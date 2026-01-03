using AIManager.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIManager.API.Controllers;

/// <summary>
/// API Controller for GPU Pool management
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GpuPoolController : ControllerBase
{
    private readonly GpuPoolService _gpuPoolService;
    private readonly ILogger<GpuPoolController> _logger;

    public GpuPoolController(GpuPoolService gpuPoolService, ILogger<GpuPoolController> logger)
    {
        _gpuPoolService = gpuPoolService;
        _logger = logger;
    }

    /// <summary>
    /// Get all workers in the pool
    /// </summary>
    [HttpGet("workers")]
    public ActionResult<IEnumerable<GpuWorkerInfo>> GetWorkers()
    {
        return Ok(_gpuPoolService.Workers);
    }

    /// <summary>
    /// Get pool statistics
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<GpuPoolStats> GetStats()
    {
        var stats = new GpuPoolStats
        {
            TotalWorkers = _gpuPoolService.Workers.Count,
            OnlineWorkers = _gpuPoolService.OnlineWorkers.Count,
            TotalVramGb = _gpuPoolService.Workers.Sum(w => w.TotalVramGb),
            AvailableVramGb = _gpuPoolService.TotalAvailableVram,
            QueuedTasks = _gpuPoolService.QueuedTaskCount,
            CompletedTasks = _gpuPoolService.CompletedTaskCount,
            DistributionMode = _gpuPoolService.DistributionMode.ToString()
        };
        return Ok(stats);
    }

    /// <summary>
    /// Add a new worker to the pool
    /// </summary>
    [HttpPost("workers")]
    public async Task<ActionResult<GpuWorkerInfo>> AddWorker([FromBody] AddWorkerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Url))
        {
            return BadRequest("Name and URL are required");
        }

        var worker = await _gpuPoolService.AddWorkerAsync(request.Name, request.Url);
        if (worker == null)
        {
            return StatusCode(500, "Failed to add worker");
        }

        _logger.LogInformation("Worker added via API: {Name} at {Url}", request.Name, request.Url);
        return Ok(worker);
    }

    /// <summary>
    /// Remove a worker from the pool
    /// </summary>
    [HttpDelete("workers/{workerId}")]
    public ActionResult RemoveWorker(string workerId)
    {
        if (_gpuPoolService.RemoveWorker(workerId))
        {
            _logger.LogInformation("Worker removed via API: {WorkerId}", workerId);
            return Ok(new { message = "Worker removed" });
        }
        return NotFound("Worker not found");
    }

    /// <summary>
    /// Refresh all workers (health check)
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult> RefreshWorkers()
    {
        await _gpuPoolService.RefreshAllWorkersAsync();
        return Ok(new { message = "Workers refreshed", count = _gpuPoolService.Workers.Count });
    }

    /// <summary>
    /// Set distribution mode
    /// </summary>
    [HttpPost("mode")]
    public ActionResult SetMode([FromBody] SetModeRequest request)
    {
        if (Enum.TryParse<GpuDistributionMode>(request.Mode, true, out var mode))
        {
            _gpuPoolService.DistributionMode = mode;
            _logger.LogInformation("Distribution mode changed to: {Mode}", mode);
            return Ok(new { message = $"Mode set to {mode}" });
        }
        return BadRequest("Invalid mode. Use 'Parallel' or 'Combined'");
    }

    /// <summary>
    /// Generate an image using the GPU pool
    /// </summary>
    [HttpPost("generate/image")]
    public async Task<ActionResult<GpuTaskResult>> GenerateImage([FromBody] GpuImageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required");
        }

        if (_gpuPoolService.OnlineWorkers.Count == 0)
        {
            return StatusCode(503, "No online GPU workers available");
        }

        var result = await _gpuPoolService.GenerateImageAsync(request, ct);
        if (result.Success)
        {
            return Ok(result);
        }
        return StatusCode(500, result.Error);
    }
}

#region Request/Response Models

public class AddWorkerRequest
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
}

public class SetModeRequest
{
    public string Mode { get; set; } = "Parallel";
}

public class GpuPoolStats
{
    public int TotalWorkers { get; set; }
    public int OnlineWorkers { get; set; }
    public double TotalVramGb { get; set; }
    public double AvailableVramGb { get; set; }
    public int QueuedTasks { get; set; }
    public int CompletedTasks { get; set; }
    public string DistributionMode { get; set; } = "";
}

#endregion
