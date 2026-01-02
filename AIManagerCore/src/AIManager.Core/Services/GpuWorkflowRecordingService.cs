using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Service สำหรับบันทึกและเล่นซ้ำ Workflow การสมัคร GPU Provider
/// </summary>
public class GpuWorkflowRecordingService
{
    private readonly ILogger<GpuWorkflowRecordingService>? _logger;
    private readonly WorkflowStorage _workflowStorage;
    private readonly WorkflowLearningEngine _learningEngine;

    private TeachingSession? _activeSession;
    private bool _isRecording;
    private GpuProviderType? _currentProvider;

    // Events
    public event Action<RecordedStep>? OnStepRecorded;
    public event Action<LearnedWorkflow>? OnRecordingCompleted;
    public event Action<WebAutomation.Models.WorkflowStep, int, int>? OnReplayStepExecuted;
    public event Action<string>? OnReplayError;
    public event Action? OnReplayCompleted;

    public bool IsRecording => _isRecording;
    public int RecordedStepsCount => _activeSession?.RecordedSteps.Count ?? 0;
    public GpuProviderType? CurrentProvider => _currentProvider;

    public GpuWorkflowRecordingService(
        ILogger<GpuWorkflowRecordingService>? logger = null,
        WorkflowStorage? workflowStorage = null,
        WorkflowLearningEngine? learningEngine = null)
    {
        _logger = logger;

        // Create default instances if not provided
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        _workflowStorage = workflowStorage ?? new WorkflowStorage(
            loggerFactory.CreateLogger<WorkflowStorage>());

        var aiAnalyzer = new AIElementAnalyzer(
            loggerFactory.CreateLogger<AIElementAnalyzer>());

        _learningEngine = learningEngine ?? new WorkflowLearningEngine(
            loggerFactory.CreateLogger<WorkflowLearningEngine>(),
            _workflowStorage,
            aiAnalyzer);
    }

    #region Recording Control

    /// <summary>
    /// เริ่มบันทึก workflow สำหรับ provider ที่ระบุ
    /// </summary>
    public TeachingSession StartRecording(GpuProviderType provider)
    {
        if (_isRecording)
        {
            throw new InvalidOperationException("Recording is already in progress");
        }

        _currentProvider = provider;
        _isRecording = true;

        _activeSession = new TeachingSession
        {
            Platform = GetPlatformName(provider),
            WorkflowType = $"signup_{provider.ToString().ToLowerInvariant()}",
            Status = TeachingStatus.Recording
        };

        _logger?.LogInformation(
            "Started recording workflow for {Provider}",
            provider);

        return _activeSession;
    }

    /// <summary>
    /// หยุดบันทึกและบันทึก workflow
    /// </summary>
    public async Task<LearnedWorkflow?> StopRecordingAsync(CancellationToken ct = default)
    {
        if (!_isRecording || _activeSession == null)
        {
            _logger?.LogWarning("No active recording session to stop");
            return null;
        }

        _isRecording = false;
        _activeSession.Status = TeachingStatus.Completed;

        if (_activeSession.RecordedSteps.Count == 0)
        {
            _logger?.LogWarning("No steps recorded, skipping workflow creation");
            _activeSession = null;
            _currentProvider = null;
            return null;
        }

        try
        {
            // Convert to learned workflow
            var workflow = await _learningEngine.LearnFromTeachingSession(_activeSession, ct);

            _logger?.LogInformation(
                "Recording completed. Created workflow {WorkflowId} with {Steps} steps",
                workflow.Id, workflow.Steps.Count);

            OnRecordingCompleted?.Invoke(workflow);

            // Reset state
            _activeSession = null;
            _currentProvider = null;

            return workflow;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save recorded workflow");
            _activeSession = null;
            _currentProvider = null;
            throw;
        }
    }

    /// <summary>
    /// ยกเลิกการบันทึก
    /// </summary>
    public void CancelRecording()
    {
        if (_isRecording)
        {
            _logger?.LogInformation("Recording cancelled");
            _isRecording = false;
            _activeSession = null;
            _currentProvider = null;
        }
    }

    #endregion

    #region Recording Steps

    /// <summary>
    /// บันทึก step ใหม่จาก user action
    /// </summary>
    public void RecordStep(
        string action,
        string? value = null,
        RecordedElement? element = null,
        string? userInstruction = null,
        string? pageUrl = null)
    {
        if (!_isRecording || _activeSession == null)
        {
            _logger?.LogWarning("Cannot record step - no active session");
            return;
        }

        var step = new RecordedStep
        {
            Action = action,
            Value = value ?? string.Empty,
            Element = element,
            UserInstruction = userInstruction ?? $"{action} on element",
            PageUrl = pageUrl ?? string.Empty,
            Timestamp = DateTime.UtcNow
        };

        _activeSession.RecordedSteps.Add(step);

        _logger?.LogDebug(
            "Recorded step {StepNum}: {Action} - {Instruction}",
            _activeSession.RecordedSteps.Count, action, userInstruction);

        OnStepRecorded?.Invoke(step);
    }

    /// <summary>
    /// บันทึก Navigate action
    /// </summary>
    public void RecordNavigate(string url)
    {
        RecordStep(
            action: "navigate",
            value: url,
            userInstruction: $"Navigate to {url}",
            pageUrl: url);
    }

    /// <summary>
    /// บันทึก Click action
    /// </summary>
    public void RecordClick(RecordedElement element, string pageUrl)
    {
        var description = !string.IsNullOrEmpty(element.TextContent)
            ? element.TextContent.Trim()
            : element.TagName;

        RecordStep(
            action: "click",
            element: element,
            userInstruction: $"Click on '{description}'",
            pageUrl: pageUrl);
    }

    /// <summary>
    /// บันทึก Type/Input action
    /// </summary>
    public void RecordType(RecordedElement element, string value, string pageUrl)
    {
        var placeholder = element.Placeholder ?? element.Name ?? "field";

        RecordStep(
            action: "type",
            value: value,
            element: element,
            userInstruction: $"Enter text in '{placeholder}'",
            pageUrl: pageUrl);
    }

    /// <summary>
    /// บันทึก Upload action
    /// </summary>
    public void RecordUpload(RecordedElement element, string filePath, string pageUrl)
    {
        RecordStep(
            action: "upload",
            value: filePath,
            element: element,
            userInstruction: $"Upload file: {Path.GetFileName(filePath)}",
            pageUrl: pageUrl);
    }

    /// <summary>
    /// บันทึก Select action
    /// </summary>
    public void RecordSelect(RecordedElement element, string value, string pageUrl)
    {
        RecordStep(
            action: "select",
            value: value,
            element: element,
            userInstruction: $"Select option: {value}",
            pageUrl: pageUrl);
    }

    #endregion

    #region Replay Workflow

    /// <summary>
    /// เล่นซ้ำ workflow ที่บันทึกไว้
    /// </summary>
    public async Task<bool> ReplayWorkflowAsync(
        string workflowId,
        Func<WebAutomation.Models.WorkflowStep, Task<bool>> executeStep,
        CancellationToken ct = default)
    {
        var workflow = await _workflowStorage.LoadWorkflowAsync(workflowId, ct);

        if (workflow == null)
        {
            _logger?.LogWarning("Workflow {WorkflowId} not found", workflowId);
            OnReplayError?.Invoke($"Workflow {workflowId} not found");
            return false;
        }

        return await ReplayWorkflowAsync(workflow, executeStep, ct);
    }

    /// <summary>
    /// เล่นซ้ำ workflow object
    /// </summary>
    public async Task<bool> ReplayWorkflowAsync(
        LearnedWorkflow workflow,
        Func<WebAutomation.Models.WorkflowStep, Task<bool>> executeStep,
        CancellationToken ct = default)
    {
        _logger?.LogInformation(
            "Starting replay of workflow {WorkflowId}: {Name}",
            workflow.Id, workflow.Name);

        var success = true;
        var failedStep = -1;

        for (int i = 0; i < workflow.Steps.Count; i++)
        {
            if (ct.IsCancellationRequested)
            {
                _logger?.LogInformation("Replay cancelled at step {Step}", i);
                return false;
            }

            var step = workflow.Steps[i];

            try
            {
                OnReplayStepExecuted?.Invoke(step, i + 1, workflow.Steps.Count);

                var stepSuccess = await executeStep(step);

                if (!stepSuccess)
                {
                    _logger?.LogWarning(
                        "Step {StepNum} failed in workflow {WorkflowId}",
                        i, workflow.Id);
                    success = false;
                    failedStep = i;
                    break;
                }

                // Small delay between steps
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing step {StepNum}", i);
                OnReplayError?.Invoke($"Step {i + 1} failed: {ex.Message}");
                success = false;
                failedStep = i;
                break;
            }
        }

        // Update workflow stats
        await _learningEngine.UpdateWorkflowFromResult(
            workflow, success, failedStep, null, ct);

        if (success)
        {
            _logger?.LogInformation("Workflow replay completed successfully");
            OnReplayCompleted?.Invoke();
        }

        return success;
    }

    #endregion

    #region Workflow Management

    /// <summary>
    /// ค้นหา workflow สำหรับ provider
    /// </summary>
    public async Task<LearnedWorkflow?> FindWorkflowForProviderAsync(
        GpuProviderType provider,
        CancellationToken ct = default)
    {
        var platform = GetPlatformName(provider);
        var workflowType = $"signup_{provider.ToString().ToLowerInvariant()}";

        return await _workflowStorage.FindWorkflowAsync(platform, workflowType, ct);
    }

    /// <summary>
    /// ดึงรายการ workflow ทั้งหมดสำหรับ GPU signup
    /// </summary>
    public async Task<List<LearnedWorkflow>> GetAllGpuWorkflowsAsync(
        CancellationToken ct = default)
    {
        var all = await _workflowStorage.GetAllWorkflowsAsync(ct);

        return all
            .Where(w => w.Platform.Contains("GPU", StringComparison.OrdinalIgnoreCase) ||
                        w.Name.Contains("signup", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(w => w.SuccessCount)
            .ThenByDescending(w => w.ConfidenceScore)
            .ToList();
    }

    /// <summary>
    /// ลบ workflow
    /// </summary>
    public async Task DeleteWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        await _workflowStorage.DeleteWorkflowAsync(workflowId, ct);
        _logger?.LogInformation("Deleted workflow {WorkflowId}", workflowId);
    }

    /// <summary>
    /// Export workflow เป็น JSON
    /// </summary>
    public async Task<string> ExportWorkflowAsync(string workflowId, CancellationToken ct = default)
    {
        return await _workflowStorage.ExportWorkflowAsync(workflowId, ct);
    }

    /// <summary>
    /// Import workflow จาก JSON
    /// </summary>
    public async Task<LearnedWorkflow> ImportWorkflowAsync(string json, CancellationToken ct = default)
    {
        return await _workflowStorage.ImportWorkflowAsync(json, ct);
    }

    /// <summary>
    /// ดึงสถิติ workflows
    /// </summary>
    public async Task<GpuWorkflowStats> GetStatsAsync(CancellationToken ct = default)
    {
        var workflows = await GetAllGpuWorkflowsAsync(ct);

        var stats = new GpuWorkflowStats
        {
            TotalWorkflows = workflows.Count,
            TotalSuccessfulRuns = workflows.Sum(w => w.SuccessCount),
            TotalFailedRuns = workflows.Sum(w => w.FailureCount),
            ByProvider = new Dictionary<string, GpuProviderWorkflowInfo>()
        };

        foreach (var provider in Enum.GetValues<GpuProviderType>())
        {
            var providerWorkflows = workflows
                .Where(w => w.Name.Contains(provider.ToString(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (providerWorkflows.Any())
            {
                var best = providerWorkflows
                    .OrderByDescending(w => w.SuccessCount)
                    .First();

                stats.ByProvider[provider.ToString()] = new GpuProviderWorkflowInfo
                {
                    Provider = provider,
                    WorkflowCount = providerWorkflows.Count,
                    BestWorkflowId = best.Id,
                    BestWorkflowName = best.Name,
                    SuccessCount = best.SuccessCount,
                    FailureCount = best.FailureCount,
                    ConfidenceScore = best.ConfidenceScore
                };
            }
        }

        return stats;
    }

    #endregion

    #region Helpers

    private static string GetPlatformName(GpuProviderType provider) => provider switch
    {
        GpuProviderType.GoogleColab => "GPU_Colab",
        GpuProviderType.Kaggle => "GPU_Kaggle",
        GpuProviderType.PaperspaceGradient => "GPU_PaperSpace",
        GpuProviderType.LightningAI => "GPU_LightningAI",
        GpuProviderType.HuggingFaceSpaces => "GPU_HuggingFace",
        GpuProviderType.SaturnCloud => "GPU_SaturnCloud",
        GpuProviderType.Gradient => "GPU_Gradient",
        GpuProviderType.Local => "GPU_Local",
        _ => $"GPU_{provider}"
    };

    #endregion
}

#region Stats Models

/// <summary>
/// สถิติ GPU Workflows
/// </summary>
public class GpuWorkflowStats
{
    public int TotalWorkflows { get; set; }
    public int TotalSuccessfulRuns { get; set; }
    public int TotalFailedRuns { get; set; }
    public Dictionary<string, GpuProviderWorkflowInfo> ByProvider { get; set; } = new();

    public double OverallSuccessRate => TotalSuccessfulRuns + TotalFailedRuns > 0
        ? (double)TotalSuccessfulRuns / (TotalSuccessfulRuns + TotalFailedRuns)
        : 0;
}

/// <summary>
/// ข้อมูล workflow ต่อ provider
/// </summary>
public class GpuProviderWorkflowInfo
{
    public GpuProviderType Provider { get; set; }
    public int WorkflowCount { get; set; }
    public string BestWorkflowId { get; set; } = string.Empty;
    public string BestWorkflowName { get; set; } = string.Empty;
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double ConfidenceScore { get; set; }

    public double SuccessRate => SuccessCount + FailureCount > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount)
        : 0;
}

#endregion
