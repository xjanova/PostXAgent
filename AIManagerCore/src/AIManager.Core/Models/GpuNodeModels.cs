namespace AIManager.Core.Models;

/// <summary>
/// สถานะของ GPU Node
/// </summary>
public enum GpuNodeStatus
{
    /// <summary>กำลังทำงาน - ไฟเขียว</summary>
    Running,

    /// <summary>พร้อมใช้งาน - ไฟเขียวกระพริบ</summary>
    Ready,

    /// <summary>กำลังเริ่มต้น - ไฟเหลือง</summary>
    Starting,

    /// <summary>รอรอบถัดไป - ไฟฟ้า</summary>
    Queued,

    /// <summary>กำลัง Warmup/Prestart - ไฟส้ม</summary>
    Warming,

    /// <summary>กำลังรีบูต - ไฟส้มกระพริบ</summary>
    Rebooting,

    /// <summary>หยุดชั่วคราว - ไฟเทา</summary>
    Paused,

    /// <summary>หยุดใช้งาน - ไฟแดงเข้ม</summary>
    Stopped,

    /// <summary>เกิดข้อผิดพลาด - ไฟแดง</summary>
    Error,

    /// <summary>ถูกตัดการเชื่อมต่อ - ไฟแดงกระพริบ</summary>
    Disconnected,

    /// <summary>โหนดฉุกเฉิน (Standby) - ไฟม่วง</summary>
    Emergency,

    /// <summary>หมดโควต้า - ไฟชมพู</summary>
    QuotaExceeded,

    /// <summary>ไม่ทราบสถานะ - ไฟขาว</summary>
    Unknown
}

// Note: GpuProviderType is defined in ColabGpuModels.cs

/// <summary>
/// ประเภทของ GPU
/// </summary>
public enum GpuType
{
    Unknown,
    T4,
    P100,
    V100,
    A100,
    L4,
    A10G,
    RTX3090,
    RTX4090,
    H100
}

/// <summary>
/// ลำดับความสำคัญของ Node
/// </summary>
public enum NodePriority
{
    Emergency = 0,    // โหนดฉุกเฉิน - เริ่มได้เร็ว
    High = 1,         // สำคัญมาก
    Normal = 2,       // ปกติ
    Low = 3,          // ต่ำ
    Backup = 4        // สำรอง
}

/// <summary>
/// ข้อมูล GPU Node
/// </summary>
public class GpuNodeInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public GpuProviderType Provider { get; set; }
    public GpuType GpuType { get; set; }
    public GpuNodeStatus Status { get; set; } = GpuNodeStatus.Unknown;
    public NodePriority Priority { get; set; } = NodePriority.Normal;

    // Connection info
    public string Url { get; set; } = string.Empty;
    public string ApiEndpoint { get; set; } = string.Empty;
    public string NotebookUrl { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime? LastConnectedAt { get; set; }
    public DateTime? LastDisconnectedAt { get; set; }

    // Quota & Time
    public TimeSpan DailyQuota { get; set; } = TimeSpan.FromHours(12);
    public TimeSpan UsedQuota { get; set; } = TimeSpan.Zero;
    public TimeSpan RemainingQuota => DailyQuota - UsedQuota;
    public double QuotaPercentUsed => DailyQuota.TotalSeconds > 0
        ? (UsedQuota.TotalSeconds / DailyQuota.TotalSeconds) * 100
        : 0;
    public DateTime? QuotaResetTime { get; set; }
    public DateTime? SessionStartTime { get; set; }
    public TimeSpan SessionDuration => SessionStartTime.HasValue
        ? DateTime.UtcNow - SessionStartTime.Value
        : TimeSpan.Zero;
    public TimeSpan MaxSessionTime { get; set; } = TimeSpan.FromHours(12);
    public TimeSpan RemainingSessionTime => MaxSessionTime - SessionDuration;

    // Performance
    public double GpuMemoryTotal { get; set; } // GB
    public double GpuMemoryUsed { get; set; }  // GB
    public double GpuMemoryFree => GpuMemoryTotal - GpuMemoryUsed;
    public double GpuUtilization { get; set; } // 0-100%
    public double Temperature { get; set; }    // Celsius
    public int CurrentTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }

    // Smart scheduling
    public bool IsEmergencyNode { get; set; }
    public bool CanQuickStart { get; set; }
    public TimeSpan EstimatedStartTime { get; set; } = TimeSpan.FromMinutes(2);
    public bool AutoPrestart { get; set; } = true;
    public TimeSpan PrestartBefore { get; set; } = TimeSpan.FromMinutes(5);
    public DateTime? ScheduledStartTime { get; set; }
    public DateTime? ScheduledStopTime { get; set; }

    // Credentials (encrypted)
    public string? CredentialId { get; set; }
    public string? AccountEmail { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? LastError { get; set; }
    public int ErrorCount { get; set; }
    public int RebootCount { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();

    // UI Display helpers
    public string StatusIcon => Status switch
    {
        GpuNodeStatus.Running => "CheckCircle",
        GpuNodeStatus.Ready => "CheckCircleOutline",
        GpuNodeStatus.Starting => "Rocket",
        GpuNodeStatus.Queued => "Clock",
        GpuNodeStatus.Warming => "Fire",
        GpuNodeStatus.Rebooting => "Restart",
        GpuNodeStatus.Paused => "Pause",
        GpuNodeStatus.Stopped => "Stop",
        GpuNodeStatus.Error => "AlertCircle",
        GpuNodeStatus.Disconnected => "WifiOff",
        GpuNodeStatus.Emergency => "Lightning",
        GpuNodeStatus.QuotaExceeded => "TimerOff",
        _ => "HelpCircle"
    };

    public string StatusColorHex => Status switch
    {
        GpuNodeStatus.Running => "#10B981",      // Green
        GpuNodeStatus.Ready => "#34D399",        // Light Green
        GpuNodeStatus.Starting => "#FBBF24",     // Yellow
        GpuNodeStatus.Queued => "#3B82F6",       // Blue
        GpuNodeStatus.Warming => "#F97316",      // Orange
        GpuNodeStatus.Rebooting => "#FB923C",    // Light Orange
        GpuNodeStatus.Paused => "#6B7280",       // Gray
        GpuNodeStatus.Stopped => "#991B1B",      // Dark Red
        GpuNodeStatus.Error => "#EF4444",        // Red
        GpuNodeStatus.Disconnected => "#DC2626", // Bright Red
        GpuNodeStatus.Emergency => "#8B5CF6",    // Purple
        GpuNodeStatus.QuotaExceeded => "#EC4899",// Pink
        _ => "#9CA3AF"                           // Light Gray
    };

    public string StatusText => Status switch
    {
        GpuNodeStatus.Running => "กำลังทำงาน",
        GpuNodeStatus.Ready => "พร้อมใช้งาน",
        GpuNodeStatus.Starting => "กำลังเริ่มต้น",
        GpuNodeStatus.Queued => "รอรอบถัดไป",
        GpuNodeStatus.Warming => "กำลัง Warmup",
        GpuNodeStatus.Rebooting => "กำลังรีบูต",
        GpuNodeStatus.Paused => "หยุดชั่วคราว",
        GpuNodeStatus.Stopped => "หยุดใช้งาน",
        GpuNodeStatus.Error => "เกิดข้อผิดพลาด",
        GpuNodeStatus.Disconnected => "ขาดการเชื่อมต่อ",
        GpuNodeStatus.Emergency => "โหนดฉุกเฉิน",
        GpuNodeStatus.QuotaExceeded => "หมดโควต้า",
        _ => "ไม่ทราบสถานะ"
    };

    public string ProviderIcon => Provider switch
    {
        GpuProviderType.GoogleColab => "Google",
        GpuProviderType.Kaggle => "Alpha",
        GpuProviderType.PaperSpace => "Cloud",
        GpuProviderType.LightningAI => "Lightning",
        GpuProviderType.HuggingFace => "Robot",
        GpuProviderType.SaturnCloud => "Saturn",
        GpuProviderType.RunPod => "Server",
        GpuProviderType.Vast => "ServerNetwork",
        GpuProviderType.Lambda => "Lambda",
        _ => "Chip"
    };
}

/// <summary>
/// เหตุการณ์ของ GPU Node
/// </summary>
public class GpuNodeEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public GpuNodeEventType EventType { get; set; }
    public GpuNodeStatus? OldStatus { get; set; }
    public GpuNodeStatus? NewStatus { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum GpuNodeEventType
{
    StatusChanged,
    Connected,
    Disconnected,
    TaskStarted,
    TaskCompleted,
    TaskFailed,
    QuotaWarning,
    QuotaExceeded,
    QuotaReset,
    SessionStarted,
    SessionEnded,
    Rebooting,
    Error,
    EmergencyActivated,
    PrestartTriggered,
    ScheduleUpdated
}

/// <summary>
/// สถิติรวมของ GPU Nodes
/// </summary>
public class GpuNodeStats
{
    public int TotalNodes { get; set; }
    public int RunningNodes { get; set; }
    public int ReadyNodes { get; set; }
    public int ErrorNodes { get; set; }
    public int EmergencyNodes { get; set; }
    public TimeSpan TotalQuotaRemaining { get; set; }
    public TimeSpan TotalQuotaUsed { get; set; }
    public double AverageGpuUtilization { get; set; }
    public int TotalTasksCompleted { get; set; }
    public int TotalTasksFailed { get; set; }
    public GpuNodeInfo? ActiveNode { get; set; }
    public GpuNodeInfo? NextNode { get; set; }
    public DateTime? NextSwitchTime { get; set; }
}

/// <summary>
/// การตั้งค่า Smart Scheduling
/// </summary>
public class GpuSchedulingConfig
{
    public bool EnableSmartScheduling { get; set; } = true;
    public bool EnableAutoPrestart { get; set; } = true;
    public TimeSpan DefaultPrestartTime { get; set; } = TimeSpan.FromMinutes(5);
    public bool EnableEmergencyNodes { get; set; } = true;
    public int MaxConcurrentNodes { get; set; } = 2;
    public double QuotaWarningThreshold { get; set; } = 0.2; // 20% remaining
    public TimeSpan SessionWarningTime { get; set; } = TimeSpan.FromMinutes(10);
    public bool AutoSwitchOnQuotaLow { get; set; } = true;
    public bool AutoSwitchOnError { get; set; } = true;
    public NodeSelectionStrategy SelectionStrategy { get; set; } = NodeSelectionStrategy.SmartBalance;
}

public enum NodeSelectionStrategy
{
    /// <summary>ใช้ Node ตามลำดับที่กำหนด</summary>
    Sequential,

    /// <summary>ใช้ Node ที่มีโควต้าเหลือมากที่สุด</summary>
    MaxQuota,

    /// <summary>ใช้ Node ที่เริ่มได้เร็วที่สุด</summary>
    FastestStart,

    /// <summary>สมดุลระหว่างโควต้าและความเร็ว</summary>
    SmartBalance,

    /// <summary>Round Robin</summary>
    RoundRobin
}
