using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Models;
using AIManager.Core.Workers;

namespace AIManager.API.Controllers;

/// <summary>
/// Worker Manager API - จัดการและตรวจสอบสถานะ Workers ทั้งหมด
/// </summary>
[ApiController]
[Route("api/workers")]
public class WorkerManagerController : ControllerBase
{
    private readonly ILogger<WorkerManagerController> _logger;
    private readonly WorkerManager _workerManager;

    public WorkerManagerController(
        ILogger<WorkerManagerController> logger,
        WorkerManager workerManager)
    {
        _logger = logger;
        _workerManager = workerManager;
    }

    #region Worker Status APIs

    /// <summary>
    /// ดึงข้อมูล Workers ทั้งหมด
    /// </summary>
    [HttpGet]
    public ActionResult<WorkersResponse> GetAllWorkers(
        [FromQuery] string? platform = null,
        [FromQuery] string? state = null)
    {
        var workers = _workerManager.GetAllWorkers();

        // Filter by platform
        if (!string.IsNullOrEmpty(platform) &&
            Enum.TryParse<SocialPlatform>(platform, true, out var platformEnum))
        {
            workers = workers.Where(w => w.Platform == platformEnum);
        }

        // Filter by state
        if (!string.IsNullOrEmpty(state) &&
            Enum.TryParse<WorkerState>(state, true, out var stateEnum))
        {
            workers = workers.Where(w => w.State == stateEnum);
        }

        var workerList = workers.Select(MapWorkerToDto).ToList();

        return Ok(new WorkersResponse
        {
            Success = true,
            Workers = workerList,
            Stats = GetStatsDto()
        });
    }

    /// <summary>
    /// ดึงข้อมูล Worker ตาม ID
    /// </summary>
    [HttpGet("{workerId}")]
    public ActionResult<WorkerDetailResponse> GetWorker(string workerId)
    {
        var worker = _workerManager.GetWorker(workerId);

        if (worker == null)
        {
            return NotFound(new { success = false, error = "Worker not found" });
        }

        return Ok(new WorkerDetailResponse
        {
            Success = true,
            Worker = MapWorkerToDetailDto(worker)
        });
    }

    /// <summary>
    /// ดึงสถิติรวมของ Workers
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<StatsResponse> GetStats()
    {
        return Ok(new StatsResponse
        {
            Success = true,
            Stats = GetStatsDto()
        });
    }

    /// <summary>
    /// ดึงประวัติรายงานของ Workers
    /// </summary>
    [HttpGet("reports")]
    public ActionResult<ReportsResponse> GetReports(
        [FromQuery] int limit = 100,
        [FromQuery] string? workerId = null)
    {
        var reports = _workerManager.GetReportHistory(limit);

        if (!string.IsNullOrEmpty(workerId))
        {
            reports = reports.Where(r => r.WorkerId == workerId);
        }

        return Ok(new ReportsResponse
        {
            Success = true,
            Reports = reports.Select(MapReportToDto).ToList()
        });
    }

    #endregion

    #region Worker Control APIs

    /// <summary>
    /// Pause Worker
    /// </summary>
    [HttpPost("{workerId}/pause")]
    public ActionResult<WorkerActionResponse> PauseWorker(string workerId)
    {
        try
        {
            _workerManager.PauseWorker(workerId);
            _logger.LogInformation("Paused worker {WorkerId}", workerId);

            return Ok(new WorkerActionResponse
            {
                Success = true,
                Message = "Worker paused successfully",
                Worker = MapWorkerToDto(_workerManager.GetWorker(workerId)!)
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new WorkerActionResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Resume Worker
    /// </summary>
    [HttpPost("{workerId}/resume")]
    public ActionResult<WorkerActionResponse> ResumeWorker(string workerId)
    {
        try
        {
            _workerManager.ResumeWorker(workerId);
            _logger.LogInformation("Resumed worker {WorkerId}", workerId);

            return Ok(new WorkerActionResponse
            {
                Success = true,
                Message = "Worker resumed successfully",
                Worker = MapWorkerToDto(_workerManager.GetWorker(workerId)!)
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new WorkerActionResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Stop Worker
    /// </summary>
    [HttpPost("{workerId}/stop")]
    public async Task<ActionResult<WorkerActionResponse>> StopWorker(
        string workerId,
        [FromQuery] bool graceful = true)
    {
        try
        {
            await _workerManager.StopWorkerAsync(workerId, graceful);
            _logger.LogInformation("Stopped worker {WorkerId} (graceful: {Graceful})",
                workerId, graceful);

            return Ok(new WorkerActionResponse
            {
                Success = true,
                Message = "Worker stopped successfully",
                Worker = MapWorkerToDto(_workerManager.GetWorker(workerId)!)
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new WorkerActionResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }

    /// <summary>
    /// Pause ทุก Workers
    /// </summary>
    [HttpPost("pause-all")]
    public ActionResult<BulkActionResponse> PauseAllWorkers()
    {
        _workerManager.PauseAllWorkers();
        _logger.LogInformation("Paused all workers");

        return Ok(new BulkActionResponse
        {
            Success = true,
            Message = "All workers paused",
            AffectedCount = _workerManager.PausedWorkers
        });
    }

    /// <summary>
    /// Resume ทุก Workers
    /// </summary>
    [HttpPost("resume-all")]
    public ActionResult<BulkActionResponse> ResumeAllWorkers()
    {
        _workerManager.ResumeAllWorkers();
        _logger.LogInformation("Resumed all workers");

        return Ok(new BulkActionResponse
        {
            Success = true,
            Message = "All workers resumed",
            AffectedCount = _workerManager.ActiveWorkers
        });
    }

    /// <summary>
    /// Stop ทุก Workers
    /// </summary>
    [HttpPost("stop-all")]
    public async Task<ActionResult<BulkActionResponse>> StopAllWorkers()
    {
        var count = _workerManager.ActiveWorkers + _workerManager.PausedWorkers;
        await _workerManager.StopAllWorkersAsync();
        _logger.LogInformation("Stopped all workers");

        return Ok(new BulkActionResponse
        {
            Success = true,
            Message = "All workers stopped",
            AffectedCount = count
        });
    }

    #endregion

    #region Dashboard Data API

    /// <summary>
    /// ดึงข้อมูลสำหรับ Dashboard (real-time)
    /// </summary>
    [HttpGet("dashboard")]
    public ActionResult<DashboardResponse> GetDashboard()
    {
        var stats = _workerManager.GetStats();
        var workers = _workerManager.GetAllWorkers()
            .OrderBy(w => w.Platform)
            .ThenBy(w => w.State)
            .Select(MapWorkerToDto)
            .ToList();

        var recentReports = _workerManager.GetReportHistory(20)
            .OrderByDescending(r => r.ReportedAt)
            .Select(MapReportToDto)
            .ToList();

        return Ok(new DashboardResponse
        {
            Success = true,
            Dashboard = new DashboardData
            {
                SystemStats = new SystemStatsDto
                {
                    TotalCores = stats.TotalCores,
                    CpuUsagePercent = Math.Round(stats.CpuUsagePercent, 1),
                    MemoryUsageMB = stats.MemoryUsageMB,
                    UptimeSeconds = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds
                },
                WorkerStats = new WorkerStatsDto
                {
                    Total = stats.TotalWorkers,
                    Running = stats.ActiveWorkers,
                    Paused = stats.PausedWorkers,
                    Stopped = stats.StoppedWorkers,
                    Error = stats.ErrorWorkers,
                    TasksProcessed = stats.TotalTasksProcessed,
                    SuccessRate = stats.TotalTasksProcessed > 0
                        ? Math.Round((double)stats.TotalSuccesses / stats.TotalTasksProcessed * 100, 1)
                        : 0
                },
                PlatformStats = stats.PlatformStats.Select(kv => new PlatformStatsDto
                {
                    Platform = kv.Key.ToString(),
                    WorkerCount = kv.Value.WorkerCount,
                    ActiveCount = kv.Value.ActiveCount,
                    TasksProcessed = kv.Value.TasksProcessed
                }).ToList(),
                Workers = workers,
                RecentReports = recentReports
            }
        });
    }

    #endregion

    #region Mapping Helpers

    private WorkerDto MapWorkerToDto(ManagedWorker worker)
    {
        return new WorkerDto
        {
            Id = worker.Id,
            Name = worker.Name,
            Platform = worker.Platform.ToString(),
            State = worker.State.ToString(),
            StateEmoji = worker.StateEmoji,
            CurrentTask = worker.CurrentTaskDescription,
            ProgressPercent = Math.Round(worker.ProgressPercent, 1),
            ProgressDetails = worker.ProgressDetails,
            TasksProcessed = worker.TasksProcessed,
            SuccessCount = worker.SuccessCount,
            FailureCount = worker.FailureCount,
            SuccessRate = Math.Round(worker.SuccessRate, 1),
            PreferredCore = worker.PreferredCore,
            UptimeSeconds = (long)worker.Uptime.TotalSeconds,
            LastActivityAt = worker.LastActivityAt,
            LastError = worker.LastError
        };
    }

    private WorkerDetailDto MapWorkerToDetailDto(ManagedWorker worker)
    {
        return new WorkerDetailDto
        {
            Id = worker.Id,
            Name = worker.Name,
            Platform = worker.Platform.ToString(),
            State = worker.State.ToString(),
            StateEmoji = worker.StateEmoji,
            CurrentTask = worker.CurrentTaskDescription,
            ProgressPercent = Math.Round(worker.ProgressPercent, 1),
            ProgressDetails = worker.ProgressDetails,
            TasksProcessed = worker.TasksProcessed,
            SuccessCount = worker.SuccessCount,
            FailureCount = worker.FailureCount,
            ErrorCount = worker.ErrorCount,
            SuccessRate = Math.Round(worker.SuccessRate, 1),
            PreferredCore = worker.PreferredCore,
            CreatedAt = worker.CreatedAt,
            StartedAt = worker.StartedAt,
            PausedAt = worker.PausedAt,
            ResumedAt = worker.ResumedAt,
            StoppedAt = worker.StoppedAt,
            LastActivityAt = worker.LastActivityAt,
            TotalProcessingTimeMs = (long)worker.TotalProcessingTime.TotalMilliseconds,
            LastError = worker.LastError
        };
    }

    private ReportDto MapReportToDto(WorkerReport report)
    {
        return new ReportDto
        {
            Id = report.Id,
            WorkerId = report.WorkerId,
            WorkerName = report.WorkerName,
            Platform = report.Platform.ToString(),
            TaskType = report.TaskType,
            Success = report.Success,
            Message = report.Message,
            ErrorDetails = report.ErrorDetails,
            ProcessingTimeMs = (long)report.ProcessingTime.TotalMilliseconds,
            Metadata = report.Metadata,
            ReportedAt = report.ReportedAt
        };
    }

    private WorkerManagerStatsDto GetStatsDto()
    {
        var stats = _workerManager.GetStats();
        return new WorkerManagerStatsDto
        {
            TotalWorkers = stats.TotalWorkers,
            ActiveWorkers = stats.ActiveWorkers,
            PausedWorkers = stats.PausedWorkers,
            StoppedWorkers = stats.StoppedWorkers,
            ErrorWorkers = stats.ErrorWorkers,
            TotalTasksProcessed = stats.TotalTasksProcessed,
            TotalSuccesses = stats.TotalSuccesses,
            TotalFailures = stats.TotalFailures,
            SuccessRate = stats.TotalTasksProcessed > 0
                ? Math.Round((double)stats.TotalSuccesses / stats.TotalTasksProcessed * 100, 1)
                : 0,
            CpuUsagePercent = Math.Round(stats.CpuUsagePercent, 1),
            MemoryUsageMB = stats.MemoryUsageMB,
            TotalCores = stats.TotalCores
        };
    }

    #endregion
}

#region Response Models

public class WorkersResponse
{
    public bool Success { get; set; }
    public List<WorkerDto> Workers { get; set; } = new();
    public WorkerManagerStatsDto? Stats { get; set; }
}

public class WorkerDetailResponse
{
    public bool Success { get; set; }
    public WorkerDetailDto? Worker { get; set; }
}

public class WorkerActionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public WorkerDto? Worker { get; set; }
}

public class BulkActionResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int AffectedCount { get; set; }
}

public class StatsResponse
{
    public bool Success { get; set; }
    public WorkerManagerStatsDto? Stats { get; set; }
}

public class ReportsResponse
{
    public bool Success { get; set; }
    public List<ReportDto> Reports { get; set; } = new();
}

public class DashboardResponse
{
    public bool Success { get; set; }
    public DashboardData? Dashboard { get; set; }
}

public class DashboardData
{
    public SystemStatsDto SystemStats { get; set; } = new();
    public WorkerStatsDto WorkerStats { get; set; } = new();
    public List<PlatformStatsDto> PlatformStats { get; set; } = new();
    public List<WorkerDto> Workers { get; set; } = new();
    public List<ReportDto> RecentReports { get; set; } = new();
}

public class SystemStatsDto
{
    public int TotalCores { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public long UptimeSeconds { get; set; }
}

public class WorkerStatsDto
{
    public int Total { get; set; }
    public int Running { get; set; }
    public int Paused { get; set; }
    public int Stopped { get; set; }
    public int Error { get; set; }
    public long TasksProcessed { get; set; }
    public double SuccessRate { get; set; }
}

public class PlatformStatsDto
{
    public string Platform { get; set; } = "";
    public int WorkerCount { get; set; }
    public int ActiveCount { get; set; }
    public long TasksProcessed { get; set; }
}

public class WorkerDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";
    public string State { get; set; } = "";
    public string StateEmoji { get; set; } = "";
    public string? CurrentTask { get; set; }
    public double ProgressPercent { get; set; }
    public string? ProgressDetails { get; set; }
    public int TasksProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public int PreferredCore { get; set; }
    public long UptimeSeconds { get; set; }
    public DateTime LastActivityAt { get; set; }
    public string? LastError { get; set; }
}

public class WorkerDetailDto : WorkerDto
{
    public int ErrorCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public long TotalProcessingTimeMs { get; set; }
}

public class WorkerManagerStatsDto
{
    public int TotalWorkers { get; set; }
    public int ActiveWorkers { get; set; }
    public int PausedWorkers { get; set; }
    public int StoppedWorkers { get; set; }
    public int ErrorWorkers { get; set; }
    public long TotalTasksProcessed { get; set; }
    public long TotalSuccesses { get; set; }
    public long TotalFailures { get; set; }
    public double SuccessRate { get; set; }
    public double CpuUsagePercent { get; set; }
    public long MemoryUsageMB { get; set; }
    public int TotalCores { get; set; }
}

public class ReportDto
{
    public string Id { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string WorkerName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public long ProcessingTimeMs { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime ReportedAt { get; set; }
}

#endregion
