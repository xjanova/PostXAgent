namespace AIManager.Core.Models;

/// <summary>
/// ประเภท Platform สำหรับ Workflow Manager
/// </summary>
public enum WorkflowPlatformType
{
    // Social Media
    Facebook,
    Google,
    TikTok,
    Instagram,
    Twitter,
    YouTube,
    LinkedIn,
    Pinterest,
    Threads,
    Line,

    // AI Services - Video
    Freepik,
    Runway,
    PikaLabs,
    LumaAI,

    // AI Services - Music
    SunoAI,
    StableAudio,
    AudioCraft,

    // AI Services - Image
    StableDiffusion,
    Leonardo,
    MidJourney,
    DallE,

    // GPU Providers
    GoogleColab,
    Kaggle,
    PaperSpace,
    LightningAI,
    HuggingFace,
    SaturnCloud,

    // Custom
    Custom
}

/// <summary>
/// ประเภท Workflow
/// </summary>
public enum WorkflowActionType
{
    Signup,
    Login,
    CreateVideo,
    CreateMusic,
    CreateImage,
    Post,
    Download,
    Upload,
    Custom
}

/// <summary>
/// โหมดการกำหนด Keywords
/// </summary>
public enum KeywordMode
{
    Preset,         // ใช้ค่าที่กำหนดไว้
    AskDuringRun,   // ถามระหว่างรัน
    OllamaGenerate  // ให้ Ollama สร้าง
}

/// <summary>
/// ข้อมูล Platform พร้อม Workflow (Core model - no WPF dependencies)
/// </summary>
public class PlatformWorkflowInfo
{
    public WorkflowPlatformType PlatformType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Icon properties (string-based for serialization)
    public string IconKind { get; set; } = "Web";
    public string IconColorHex { get; set; } = "#FFFFFF";
    public string IconBackgroundHex { get; set; } = "#30FFFFFF";
    public string StatusBackgroundHex { get; set; } = "#4010B981";

    // Workflow counts
    public int WorkflowCount { get; set; }
    public bool HasSignupWorkflow { get; set; }
    public bool HasLoginWorkflow { get; set; }
    public bool HasPrimaryWorkflow { get; set; }
    public bool HasAnyWorkflow => WorkflowCount > 0;

    // Primary action for this platform
    public string PrimaryAction { get; set; } = "Create";
    public string PrimaryActionTag { get; set; } = "Create";
    public bool ShowPrimaryAction { get; set; } = true;

    // Features
    public bool SupportsOllama { get; set; }
    public string StartUrl { get; set; } = string.Empty;

    // Stored workflows
    public List<string> WorkflowIds { get; set; } = new();
}

/// <summary>
/// Options สำหรับรัน Workflow
/// </summary>
public class WorkflowRunOptions
{
    public WorkflowPlatformType Platform { get; set; }
    public string WorkflowType { get; set; } = "Signup";
    public KeywordMode KeywordMode { get; set; } = KeywordMode.Preset;
    public string Prompt { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    // Ollama settings
    public string OllamaModel { get; set; } = "llama3.2:latest";
    public string OllamaContext { get; set; } = string.Empty;

    // Options
    public bool AutoDownload { get; set; } = true;
    public bool SaveToPool { get; set; } = true;
    public bool NotifyOnComplete { get; set; } = true;
    public string DownloadFolder { get; set; } = string.Empty;

    // Credentials (for signup/login)
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Username { get; set; }
}

/// <summary>
/// ผลลัพธ์จากการรัน Workflow
/// </summary>
public class WorkflowRunResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? WorkflowId { get; set; }
    public string? OutputPath { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public List<string> Logs { get; set; } = new();
}

/// <summary>
/// Template สำหรับ Export/Import Workflow Manager
/// </summary>
public class WorkflowExportTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkflowPlatformType Platform { get; set; }
    public WorkflowActionType ActionType { get; set; }
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Steps data (serialized workflow)
    public string WorkflowJson { get; set; } = string.Empty;

    // Metadata
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    // Compatibility
    public bool IsCompatibleWithMyPostXAgent { get; set; } = true;
}

/// <summary>
/// การตั้งค่า Download Folder
/// </summary>
public class DownloadFolderConfig
{
    public string BasePath { get; set; } = string.Empty;
    public string VideoFolder { get; set; } = "Videos";
    public string MusicFolder { get; set; } = "Music";
    public string ImageFolder { get; set; } = "Images";
    public bool AutoOrganize { get; set; } = true;
    public bool AutoCleanup { get; set; } = false;
    public int CleanupAfterDays { get; set; } = 30;
}

/// <summary>
/// สถิติ Workflow
/// </summary>
public class WorkflowStats
{
    public int TotalWorkflows { get; set; }
    public int TotalSuccessRuns { get; set; }
    public int TotalFailedRuns { get; set; }
    public double AverageSuccessRate { get; set; }
    public Dictionary<WorkflowPlatformType, int> WorkflowsByPlatform { get; set; } = new();
    public Dictionary<WorkflowActionType, int> WorkflowsByAction { get; set; } = new();
    public DateTime LastRunAt { get; set; }
}
