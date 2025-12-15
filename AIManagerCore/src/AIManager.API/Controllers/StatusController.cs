using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Orchestrator;
using AIManager.Core.Models;

namespace AIManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatusController : ControllerBase
{
    private readonly ProcessOrchestrator _orchestrator;

    public StatusController(ProcessOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    /// <summary>
    /// Get server health status
    /// </summary>
    [HttpGet("health")]
    public ActionResult GetHealth()
    {
        return Ok(new
        {
            Status = _orchestrator.IsRunning ? "healthy" : "stopped",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get detailed server statistics
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<OrchestratorStats> GetStats()
    {
        return Ok(_orchestrator.Stats);
    }

    /// <summary>
    /// Get all workers information
    /// </summary>
    [HttpGet("workers")]
    public ActionResult<IEnumerable<WorkerInfo>> GetWorkers()
    {
        return Ok(_orchestrator.GetWorkers());
    }

    /// <summary>
    /// Get system information
    /// </summary>
    [HttpGet("system")]
    public ActionResult GetSystemInfo()
    {
        return Ok(new
        {
            MachineName = Environment.MachineName,
            ProcessorCount = Environment.ProcessorCount,
            OSVersion = Environment.OSVersion.ToString(),
            Is64Bit = Environment.Is64BitOperatingSystem,
            WorkingSet = Environment.WorkingSet / 1024 / 1024,  // MB
            DotNetVersion = Environment.Version.ToString()
        });
    }

    /// <summary>
    /// Start the orchestrator
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult> Start()
    {
        if (_orchestrator.IsRunning)
            return BadRequest(new { Error = "Already running" });

        await _orchestrator.StartAsync();
        return Ok(new { Message = "Started successfully" });
    }

    /// <summary>
    /// Stop the orchestrator
    /// </summary>
    [HttpPost("stop")]
    public async Task<ActionResult> Stop()
    {
        if (!_orchestrator.IsRunning)
            return BadRequest(new { Error = "Not running" });

        await _orchestrator.StopAsync();
        return Ok(new { Message = "Stopped successfully" });
    }
}
