using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// Engine สำหรับเรียนรู้และปรับปรุง Workflow อัตโนมัติ
/// </summary>
public class WorkflowLearningEngine
{
    private readonly ILogger<WorkflowLearningEngine> _logger;
    private readonly WorkflowStorage _storage;
    private readonly AIElementAnalyzer _aiAnalyzer;

    // Threshold สำหรับการตัดสินใจ
    private const double MinConfidenceThreshold = 0.7;
    private const int MinSuccessCountForTrust = 5;
    private const double FailureRatioForRelearn = 0.3;

    public WorkflowLearningEngine(
        ILogger<WorkflowLearningEngine> logger,
        WorkflowStorage storage,
        AIElementAnalyzer aiAnalyzer)
    {
        _logger = logger;
        _storage = storage;
        _aiAnalyzer = aiAnalyzer;
    }

    /// <summary>
    /// เรียนรู้ Workflow ใหม่จากการสอน
    /// </summary>
    public async Task<LearnedWorkflow> LearnFromTeachingSession(
        TeachingSession session,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Learning workflow from teaching session {SessionId}", session.Id);

        var workflow = new LearnedWorkflow
        {
            Platform = session.Platform,
            Name = session.WorkflowType,
            Description = $"Learned from teaching session on {session.StartedAt:yyyy-MM-dd HH:mm}"
        };

        var steps = new List<WorkflowStep>();

        for (int i = 0; i < session.RecordedSteps.Count; i++)
        {
            var recorded = session.RecordedSteps[i];
            var step = await ConvertRecordedStepToWorkflowStep(recorded, i, ct);
            steps.Add(step);
        }

        workflow.Steps = steps;

        // บันทึก workflow
        await _storage.SaveWorkflowAsync(workflow, ct);

        _logger.LogInformation("Created workflow with {StepCount} steps", steps.Count);

        return workflow;
    }

    /// <summary>
    /// แปลง RecordedStep เป็น WorkflowStep พร้อม AI วิเคราะห์
    /// </summary>
    private async Task<WorkflowStep> ConvertRecordedStepToWorkflowStep(
        RecordedStep recorded,
        int order,
        CancellationToken ct)
    {
        var step = new WorkflowStep
        {
            Order = order,
            Action = ParseAction(recorded.Action),
            Description = recorded.UserInstruction,
            LearnedFrom = LearnedSource.Manual
        };

        if (recorded.Element != null)
        {
            // สร้าง selector หลัก
            step.Selector = CreateBestSelector(recorded.Element);

            // สร้าง alternative selectors
            step.AlternativeSelectors = CreateAlternativeSelectors(recorded.Element);

            // ใช้ AI วิเคราะห์เพิ่มเติม
            var aiAnalysis = await _aiAnalyzer.AnalyzeElement(recorded.Element, ct);
            if (aiAnalysis != null)
            {
                step.Selector.AIDescription = aiAnalysis.Description;
                step.ConfidenceScore = aiAnalysis.Confidence;
            }
        }

        if (!string.IsNullOrEmpty(recorded.Value))
        {
            // ตรวจสอบว่าเป็น variable หรือ static value
            step.InputValue = recorded.Value;

            // AI พยายามจับคู่กับ variable
            var detectedVariable = DetectInputVariable(recorded.Value, recorded.Element);
            if (detectedVariable != null)
            {
                step.InputVariable = detectedVariable;
            }
        }

        return step;
    }

    /// <summary>
    /// สร้าง selector ที่ดีที่สุดสำหรับ element
    /// </summary>
    private ElementSelector CreateBestSelector(RecordedElement element)
    {
        // ลำดับความสำคัญของ selector
        // 1. ID (ถ้ามีและไม่ใช่ dynamic)
        if (!string.IsNullOrEmpty(element.Id) && !IsDynamicId(element.Id))
        {
            return new ElementSelector
            {
                Type = SelectorType.Id,
                Value = element.Id,
                Confidence = 0.95
            };
        }

        // 2. data-testid
        if (element.Attributes.TryGetValue("data-testid", out var testId))
        {
            return new ElementSelector
            {
                Type = SelectorType.TestId,
                Value = testId,
                Confidence = 0.95
            };
        }

        // 3. aria-label
        if (element.Attributes.TryGetValue("aria-label", out var ariaLabel))
        {
            return new ElementSelector
            {
                Type = SelectorType.AriaLabel,
                Value = ariaLabel,
                Confidence = 0.9
            };
        }

        // 4. name attribute
        if (!string.IsNullOrEmpty(element.Name))
        {
            return new ElementSelector
            {
                Type = SelectorType.Name,
                Value = element.Name,
                Confidence = 0.85
            };
        }

        // 5. placeholder (สำหรับ input)
        if (!string.IsNullOrEmpty(element.Placeholder))
        {
            return new ElementSelector
            {
                Type = SelectorType.Placeholder,
                Value = element.Placeholder,
                Confidence = 0.8
            };
        }

        // 6. CSS Selector
        if (!string.IsNullOrEmpty(element.CssSelector))
        {
            return new ElementSelector
            {
                Type = SelectorType.CSS,
                Value = element.CssSelector,
                Confidence = 0.75
            };
        }

        // 7. XPath (fallback)
        if (!string.IsNullOrEmpty(element.XPath))
        {
            return new ElementSelector
            {
                Type = SelectorType.XPath,
                Value = element.XPath,
                Confidence = 0.6
            };
        }

        // 8. Text content
        if (!string.IsNullOrEmpty(element.TextContent))
        {
            return new ElementSelector
            {
                Type = SelectorType.Text,
                Value = element.TextContent.Trim(),
                TextContent = element.TextContent,
                Confidence = 0.7
            };
        }

        // Fallback: Smart selector ให้ AI หา
        return new ElementSelector
        {
            Type = SelectorType.Smart,
            Value = "",
            Position = element.Position,
            Confidence = 0.5
        };
    }

    /// <summary>
    /// สร้าง alternative selectors สำรอง
    /// </summary>
    private List<ElementSelector> CreateAlternativeSelectors(RecordedElement element)
    {
        var alternatives = new List<ElementSelector>();

        // เพิ่มทุก selector ที่เป็นไปได้
        if (!string.IsNullOrEmpty(element.Id))
        {
            alternatives.Add(new ElementSelector
            {
                Type = SelectorType.CSS,
                Value = $"#{element.Id}",
                Confidence = 0.9
            });
        }

        if (!string.IsNullOrEmpty(element.ClassName))
        {
            var classes = element.ClassName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var cls in classes.Take(3)) // เอาแค่ 3 class แรก
            {
                alternatives.Add(new ElementSelector
                {
                    Type = SelectorType.ClassName,
                    Value = cls,
                    Confidence = 0.5
                });
            }
        }

        if (!string.IsNullOrEmpty(element.XPath))
        {
            alternatives.Add(new ElementSelector
            {
                Type = SelectorType.XPath,
                Value = element.XPath,
                Confidence = 0.6
            });
        }

        // Visual matching ถ้ามี position
        if (element.Position != null)
        {
            alternatives.Add(new ElementSelector
            {
                Type = SelectorType.Visual,
                Position = element.Position,
                Confidence = 0.4
            });
        }

        return alternatives;
    }

    /// <summary>
    /// ปรับปรุง Workflow จากผลการทำงาน
    /// </summary>
    public async Task UpdateWorkflowFromResult(
        LearnedWorkflow workflow,
        bool success,
        int? failedAtStep = null,
        string? errorMessage = null,
        CancellationToken ct = default)
    {
        if (success)
        {
            workflow.SuccessCount++;
            workflow.LastSuccessAt = DateTime.UtcNow;
            workflow.ConfidenceScore = Math.Min(1.0, workflow.ConfidenceScore + 0.05);
        }
        else
        {
            workflow.FailureCount++;
            workflow.ConfidenceScore = Math.Max(0.1, workflow.ConfidenceScore - 0.1);

            // ถ้าล้มเหลวที่ step ไหน ลด confidence ของ step นั้น
            if (failedAtStep.HasValue && failedAtStep.Value < workflow.Steps.Count)
            {
                var step = workflow.Steps[failedAtStep.Value];
                step.ConfidenceScore = Math.Max(0.1, step.ConfidenceScore - 0.2);

                _logger.LogWarning(
                    "Step {StepOrder} failed in workflow {WorkflowId}: {Error}",
                    failedAtStep.Value, workflow.Id, errorMessage);
            }

            // ถ้า failure rate สูงเกินไป ให้ mark ว่าต้อง relearn
            if (workflow.GetSuccessRate() < (1 - FailureRatioForRelearn) &&
                workflow.SuccessCount + workflow.FailureCount >= MinSuccessCountForTrust)
            {
                _logger.LogWarning(
                    "Workflow {WorkflowId} needs relearning, success rate: {Rate:P}",
                    workflow.Id, workflow.GetSuccessRate());

                workflow.IsActive = false;
            }
        }

        workflow.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveWorkflowAsync(workflow, ct);
    }

    /// <summary>
    /// AI พยายามซ่อม Workflow ที่พังเอง
    /// </summary>
    public async Task<LearnedWorkflow?> TryAutoRepairWorkflow(
        LearnedWorkflow workflow,
        int failedStepIndex,
        string pageHtml,
        string? screenshot,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Attempting to auto-repair workflow {WorkflowId} at step {Step}",
            workflow.Id, failedStepIndex);

        var failedStep = workflow.Steps[failedStepIndex];

        // ใช้ AI หา element ใหม่
        var newSelector = await _aiAnalyzer.FindSimilarElement(
            failedStep.Selector,
            pageHtml,
            screenshot,
            ct);

        if (newSelector != null && newSelector.Confidence >= MinConfidenceThreshold)
        {
            // สร้าง workflow version ใหม่
            var newWorkflow = CloneWorkflow(workflow);
            newWorkflow.Version++;
            newWorkflow.Steps[failedStepIndex].Selector = newSelector;
            newWorkflow.Steps[failedStepIndex].LearnedFrom = LearnedSource.AutoRecovered;
            newWorkflow.SuccessCount = 0;
            newWorkflow.FailureCount = 0;
            newWorkflow.ConfidenceScore = newSelector.Confidence;

            await _storage.SaveWorkflowAsync(newWorkflow, ct);

            _logger.LogInformation(
                "Auto-repaired workflow {WorkflowId} with new selector (confidence: {Confidence})",
                newWorkflow.Id, newSelector.Confidence);

            return newWorkflow;
        }

        _logger.LogWarning("Could not auto-repair workflow {WorkflowId}", workflow.Id);
        return null;
    }

    /// <summary>
    /// AI เรียนรู้ Workflow ใหม่จากการสังเกต
    /// </summary>
    public async Task<LearnedWorkflow?> LearnFromObservation(
        string platform,
        string workflowType,
        List<RecordedStep> observedSteps,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Learning new workflow from observation: {Platform}/{Type}",
            platform, workflowType);

        // ตรวจสอบว่ามี workflow ที่คล้ายกันอยู่แล้วหรือไม่
        var existingWorkflow = await _storage.FindSimilarWorkflowAsync(platform, workflowType, ct);

        if (existingWorkflow != null && existingWorkflow.ConfidenceScore >= MinConfidenceThreshold)
        {
            // Merge กับ workflow ที่มีอยู่
            return await MergeWorkflows(existingWorkflow, observedSteps, ct);
        }

        // สร้าง workflow ใหม่
        var session = new TeachingSession
        {
            Platform = platform,
            WorkflowType = workflowType,
            RecordedSteps = observedSteps,
            Status = TeachingStatus.Completed
        };

        var newWorkflow = await LearnFromTeachingSession(session, ct);
        newWorkflow.Steps.ForEach(s => s.LearnedFrom = LearnedSource.AIObserved);

        return newWorkflow;
    }

    /// <summary>
    /// รวม workflow ที่มีอยู่กับ steps ที่สังเกตได้ใหม่
    /// </summary>
    private async Task<LearnedWorkflow> MergeWorkflows(
        LearnedWorkflow existing,
        List<RecordedStep> newSteps,
        CancellationToken ct)
    {
        // เพิ่ม alternative selectors จาก observation ใหม่
        for (int i = 0; i < Math.Min(existing.Steps.Count, newSteps.Count); i++)
        {
            var existingStep = existing.Steps[i];
            var newStep = newSteps[i];

            if (newStep.Element != null)
            {
                var newSelector = CreateBestSelector(newStep.Element);
                if (!existingStep.AlternativeSelectors.Any(s => s.Value == newSelector.Value))
                {
                    existingStep.AlternativeSelectors.Add(newSelector);
                }
            }
        }

        existing.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveWorkflowAsync(existing, ct);

        return existing;
    }

    private StepAction ParseAction(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "click" => StepAction.Click,
            "type" or "input" => StepAction.Type,
            "navigate" or "goto" => StepAction.Navigate,
            "upload" => StepAction.Upload,
            "select" => StepAction.Select,
            "scroll" => StepAction.Scroll,
            "hover" => StepAction.Hover,
            "wait" => StepAction.Wait,
            "press" or "keypress" => StepAction.PressKey,
            "doubleclick" => StepAction.DoubleClick,
            "rightclick" => StepAction.RightClick,
            _ => StepAction.Click
        };
    }

    private bool IsDynamicId(string id)
    {
        // ตรวจสอบว่า ID ดูเหมือน dynamic หรือไม่
        // เช่น มีตัวเลขยาวๆ หรือ GUID
        if (id.Length > 20 && id.Any(char.IsDigit))
            return true;

        if (Guid.TryParse(id, out _))
            return true;

        // Pattern ที่มักเป็น dynamic
        var dynamicPatterns = new[] { "react", "ember", "ng-", "_ngcontent", "svelte" };
        return dynamicPatterns.Any(p => id.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private string? DetectInputVariable(string value, RecordedElement? element)
    {
        // พยายามจับคู่ว่า input นี้ควรเป็น variable อะไร
        var placeholder = element?.Placeholder?.ToLowerInvariant() ?? "";
        var name = element?.Name?.ToLowerInvariant() ?? "";
        var label = element?.Attributes?.GetValueOrDefault("aria-label", "")?.ToLowerInvariant() ?? "";

        if (placeholder.Contains("message") || name.Contains("content") || label.Contains("post"))
            return "{{content.text}}";

        if (placeholder.Contains("hashtag") || name.Contains("tag"))
            return "{{content.hashtags}}";

        if (placeholder.Contains("link") || placeholder.Contains("url"))
            return "{{content.link}}";

        if (placeholder.Contains("location") || name.Contains("place"))
            return "{{content.location}}";

        return null;
    }

    private LearnedWorkflow CloneWorkflow(LearnedWorkflow source)
    {
        var json = JsonConvert.SerializeObject(source);
        var clone = JsonConvert.DeserializeObject<LearnedWorkflow>(json)!;
        clone.Id = Guid.NewGuid().ToString();
        return clone;
    }
}
