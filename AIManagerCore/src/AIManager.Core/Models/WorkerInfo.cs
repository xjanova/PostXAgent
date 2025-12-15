namespace AIManager.Core.Models;

/// <summary>
/// Information about a worker thread
/// </summary>
public class WorkerInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public TaskItem? CurrentTask { get; set; }
    public int TasksProcessed { get; set; }
    public int TasksFailed { get; set; }
    public TimeSpan AverageProcessingTime { get; set; }
}

/// <summary>
/// Orchestrator configuration
/// </summary>
public class OrchestratorConfig
{
    /// <summary>
    /// Number of CPU cores to use (0 = auto-detect all)
    /// </summary>
    public int NumCores { get; set; } = 0;

    /// <summary>
    /// Maximum workers per platform
    /// </summary>
    public int MaxWorkersPerPlatform { get; set; } = 10;

    /// <summary>
    /// Task timeout in seconds
    /// </summary>
    public int TaskTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retries for failed tasks
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Redis connection string
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// PostgreSQL connection string
    /// </summary>
    public string DatabaseConnectionString { get; set; } = "";

    /// <summary>
    /// API server port
    /// </summary>
    public int ApiPort { get; set; } = 5000;

    /// <summary>
    /// WebSocket server port
    /// </summary>
    public int WebSocketPort { get; set; } = 5001;

    /// <summary>
    /// SignalR Hub port for real-time communication
    /// </summary>
    public int SignalRPort { get; set; } = 5002;
}

/// <summary>
/// Orchestrator statistics
/// </summary>
public class OrchestratorStats
{
    public DateTime StartTime { get; set; }
    public TimeSpan Uptime { get; set; }
    public int TotalWorkers { get; set; }
    public int ActiveWorkers { get; set; }
    public long TasksQueued { get; set; }
    public long TasksCompleted { get; set; }
    public long TasksFailed { get; set; }
    public double TasksPerSecond { get; set; }
    public long MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }

    public Dictionary<SocialPlatform, PlatformStats> PlatformStats { get; set; } = new();
}

/// <summary>
/// Per-platform statistics
/// </summary>
public class PlatformStats
{
    public SocialPlatform Platform { get; set; }
    public int ActiveWorkers { get; set; }
    public long TasksProcessed { get; set; }
    public long TasksFailed { get; set; }
    public double AverageProcessingTimeMs { get; set; }
}
