using AIManager.Core.Models;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.Core.Workers;

/// <summary>
/// Worker View Mode - สถานะการแสดงผลของ Worker
/// </summary>
public enum WorkerViewMode
{
    /// <summary>
    /// Worker รันใน Playwright (headless) - ไม่แสดงผล UI
    /// </summary>
    Headless,

    /// <summary>
    /// แสดงใน WebView2 แต่ยังควบคุมอัตโนมัติ (view-only)
    /// </summary>
    Viewing,

    /// <summary>
    /// Worker หยุด รอความช่วยเหลือจากมนุษย์
    /// </summary>
    NeedsHelp,

    /// <summary>
    /// มนุษย์ควบคุมเต็มที่ พร้อม Recording
    /// </summary>
    HumanControl,

    /// <summary>
    /// กำลังเรียนรู้จาก steps ที่บันทึก
    /// </summary>
    Learning,

    /// <summary>
    /// กำลังส่งกลับไปทำงานอัตโนมัติ
    /// </summary>
    Resuming
}

/// <summary>
/// Context for Worker View - ข้อมูลสถานะปัจจุบันของ worker ในโหมด view
/// </summary>
public class WorkerViewContext
{
    public string WorkerId { get; set; } = "";
    public WorkerViewMode Mode { get; set; } = WorkerViewMode.Headless;
    public DateTime? HelpRequestedAt { get; set; }
    public string? HelpReason { get; set; }
    public string? CurrentUrl { get; set; }
    public string? SessionData { get; set; }

    /// <summary>
    /// Cookies ที่ต้องการ sync ระหว่าง browsers
    /// </summary>
    public List<BrowserCookie>? Cookies { get; set; }

    /// <summary>
    /// LocalStorage data
    /// </summary>
    public Dictionary<string, string>? LocalStorage { get; set; }

    /// <summary>
    /// SessionStorage data
    /// </summary>
    public Dictionary<string, string>? SessionStorage { get; set; }

    /// <summary>
    /// Recorded steps during human control mode
    /// </summary>
    public List<RecordedStep> RecordedSteps { get; set; } = new();

    /// <summary>
    /// Last screenshot for display purposes
    /// </summary>
    public byte[]? LastScreenshot { get; set; }

    /// <summary>
    /// Timestamp of last activity
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Browser Cookie for session transfer
/// </summary>
public class BrowserCookie
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
    public string Domain { get; set; } = "";
    public string Path { get; set; } = "/";
    public DateTime? Expires { get; set; }
    public bool HttpOnly { get; set; }
    public bool Secure { get; set; }
    public string SameSite { get; set; } = "Lax";
}

/// <summary>
/// Recorded step during human control mode
/// </summary>
public class RecordedStep
{
    public int StepNumber { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public RecordedActionType ActionType { get; set; }
    public string? Selector { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public string? Url { get; set; }
    public byte[]? Screenshot { get; set; }

    /// <summary>
    /// Element info at the time of action
    /// </summary>
    public RecordedElementInfo? ElementInfo { get; set; }
}

/// <summary>
/// Type of recorded action
/// </summary>
public enum RecordedActionType
{
    Click,
    Type,
    Clear,
    Navigate,
    Select,
    Check,
    Uncheck,
    Upload,
    Scroll,
    Wait,
    KeyPress,
    Hover,
    Focus,
    Custom
}

/// <summary>
/// Information about the element that was interacted with
/// </summary>
public class RecordedElementInfo
{
    public string? TagName { get; set; }
    public string? Id { get; set; }
    public string? ClassName { get; set; }
    public string? Text { get; set; }
    public string? XPath { get; set; }
    public string? CssSelector { get; set; }
    public string? AriaLabel { get; set; }
    public Dictionary<string, string>? Attributes { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// Event args for when a worker requests help
/// </summary>
public class WorkerHelpRequestedEventArgs : EventArgs
{
    public ManagedWorker Worker { get; }
    public string Reason { get; }
    public string? CurrentUrl { get; }
    public DateTime RequestedAt { get; } = DateTime.UtcNow;

    public WorkerHelpRequestedEventArgs(ManagedWorker worker, string reason, string? currentUrl = null)
    {
        Worker = worker;
        Reason = reason;
        CurrentUrl = currentUrl;
    }
}

/// <summary>
/// Event args for when worker view mode changes
/// </summary>
public class WorkerViewModeChangedEventArgs : EventArgs
{
    public string WorkerId { get; }
    public WorkerViewMode OldMode { get; }
    public WorkerViewMode NewMode { get; }
    public DateTime ChangedAt { get; } = DateTime.UtcNow;

    public WorkerViewModeChangedEventArgs(string workerId, WorkerViewMode oldMode, WorkerViewMode newMode)
    {
        WorkerId = workerId;
        OldMode = oldMode;
        NewMode = newMode;
    }
}

/// <summary>
/// Event args for when human intervention is complete
/// </summary>
public class HumanInterventionCompleteEventArgs : EventArgs
{
    public string WorkerId { get; }
    public List<RecordedStep> RecordedSteps { get; }
    public LearnedWorkflow? GeneratedWorkflow { get; set; }
    public bool Success { get; }
    public string? Message { get; set; }

    public HumanInterventionCompleteEventArgs(string workerId, List<RecordedStep> steps, bool success)
    {
        WorkerId = workerId;
        RecordedSteps = steps;
        Success = success;
    }
}

/// <summary>
/// Display model for workers in the WebView page
/// </summary>
public class WorkerWebViewDisplayItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public string PlatformName => Platform.ToString();
    public WorkerViewMode ViewMode { get; set; }
    public WorkerState State { get; set; }
    public string? CurrentUrl { get; set; }
    public string? HelpReason { get; set; }
    public DateTime? HelpRequestedAt { get; set; }
    public int RecordedStepsCount { get; set; }
    public DateTime LastActivityAt { get; set; }

    /// <summary>
    /// Emoji representing the current state
    /// </summary>
    public string StateEmoji => State switch
    {
        WorkerState.Created => "⚪",
        WorkerState.Running => "🟢",
        WorkerState.Paused => "🟡",
        WorkerState.Stopped => "⚫",
        WorkerState.Error => "🔴",
        _ => "⚪"
    };

    /// <summary>
    /// Color for view mode indicator
    /// </summary>
    public string ViewModeColor => ViewMode switch
    {
        WorkerViewMode.Headless => "#808080",      // Gray
        WorkerViewMode.Viewing => "#06B6D4",       // Cyan
        WorkerViewMode.NeedsHelp => "#F59E0B",     // Yellow/Orange
        WorkerViewMode.HumanControl => "#EF4444", // Red
        WorkerViewMode.Learning => "#8B5CF6",      // Purple
        WorkerViewMode.Resuming => "#10B981",      // Green
        _ => "#808080"
    };

    /// <summary>
    /// Needs help indicator
    /// </summary>
    public bool NeedsHelp => ViewMode == WorkerViewMode.NeedsHelp;

    /// <summary>
    /// Is being controlled by human
    /// </summary>
    public bool IsHumanControlled => ViewMode == WorkerViewMode.HumanControl;

    /// <summary>
    /// Is visible in WebView
    /// </summary>
    public bool IsVisible => ViewMode != WorkerViewMode.Headless;
}
