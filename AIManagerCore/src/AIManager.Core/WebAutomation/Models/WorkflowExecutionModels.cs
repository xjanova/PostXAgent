namespace AIManager.Core.WebAutomation.Models;

/// <summary>
/// ผลลัพธ์ของการ execute workflow
/// ใช้ร่วมกันทั้ง Playwright และ WebView2 executor
/// </summary>
public class WorkflowExecutionResult
{
    public string WorkflowId { get; set; } = "";
    public bool Success { get; set; }
    public int? FailedAtStep { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Provider ที่ใช้ execute (Playwright, WebView2, etc.)
    /// </summary>
    public string? ExecutorType { get; set; }

    /// <summary>
    /// จำนวน retry ที่ใช้
    /// </summary>
    public int RetryCount { get; set; }
}

/// <summary>
/// ผลลัพธ์ของการ execute แต่ละ step
/// </summary>
public class StepExecutionResult
{
    public string StepId { get; set; } = "";
    public string Action { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Screenshot ของ step (ถ้ามี)
    /// </summary>
    public byte[]? Screenshot { get; set; }

    /// <summary>
    /// Selector ที่ใช้จริง (อาจเป็น alternative)
    /// </summary>
    public string? UsedSelector { get; set; }
}

/// <summary>
/// Progress ของการ execute workflow
/// </summary>
public class WorkflowExecutionProgress
{
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public int CurrentStepIndex { get; set; }
    public int TotalSteps { get; set; }
    public string CurrentStepDescription { get; set; } = "";
    public double ProgressPercent => TotalSteps > 0 ? (CurrentStepIndex * 100.0 / TotalSteps) : 0;
    public string Status { get; set; } = "Running";
}

/// <summary>
/// Interface สำหรับ Workflow Executor
/// รองรับทั้ง Playwright และ WebView2
/// </summary>
public interface IWorkflowExecutor
{
    /// <summary>
    /// ชื่อของ executor type
    /// </summary>
    string ExecutorType { get; }

    /// <summary>
    /// กำลัง execute อยู่หรือไม่
    /// </summary>
    bool IsExecuting { get; }

    /// <summary>
    /// Execute workflow
    /// </summary>
    Task<WorkflowExecutionResult> ExecuteAsync(
        LearnedWorkflow workflow,
        Dictionary<string, string>? variables = null,
        IProgress<WorkflowExecutionProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Execute single step
    /// </summary>
    Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowStep step,
        Dictionary<string, string>? variables = null,
        CancellationToken ct = default);

    /// <summary>
    /// หยุดการ execute
    /// </summary>
    void Stop();

    /// <summary>
    /// ตรวจสอบว่า executor พร้อมใช้งาน
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}

/// <summary>
/// Event args สำหรับ step events
/// </summary>
public class WorkflowStepEventArgs : EventArgs
{
    public int StepIndex { get; set; }
    public WorkflowStep Step { get; set; } = null!;
    public int TotalSteps { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Event args สำหรับ workflow completion
/// </summary>
public class WorkflowCompletedEventArgs : EventArgs
{
    public LearnedWorkflow Workflow { get; set; } = null!;
    public WorkflowExecutionResult Result { get; set; } = null!;
}
