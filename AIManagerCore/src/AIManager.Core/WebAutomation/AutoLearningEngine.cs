using AIManager.Core.WebAutomation.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// AutoLearningEngine - ระบบเรียนรู้อัตโนมัติที่สามารถ:
/// 1. เรียนรู้จากการสอนของมนุษย์ (Human Teaching)
/// 2. ต่อยอดเรียนรู้เองจาก patterns ที่พบ
/// 3. สร้าง workflow ใหม่สำหรับ platforms ที่คล้ายกัน
/// 4. ปรับปรุง workflow อัตโนมัติจากผลลัพธ์
/// </summary>
public class AutoLearningEngine
{
    private readonly ILogger<AutoLearningEngine> _logger;
    private readonly ContentGeneratorService _aiService;
    private readonly WorkflowStorage _storage;
    private readonly DeepPatternLearner _patternLearner;
    private readonly WorkflowLearningEngine _teachingEngine;

    // Platform similarity matrix สำหรับ transfer learning
    private readonly Dictionary<string, List<string>> _platformSimilarities = new()
    {
        // Social Media Platforms
        ["Facebook"] = new() { "Threads", "LinkedIn" },
        ["Instagram"] = new() { "TikTok", "Pinterest" },
        ["Twitter"] = new() { "Threads", "Facebook" },
        ["TikTok"] = new() { "Instagram", "YouTube" },
        ["YouTube"] = new() { "TikTok", "Facebook" },
        ["LINE"] = new() { "Facebook" },
        ["Threads"] = new() { "Twitter", "Facebook" },
        ["LinkedIn"] = new() { "Facebook", "Twitter" },
        ["Pinterest"] = new() { "Instagram" },

        // AI Generation Platforms - คล้ายกันในแง่ของ workflow pattern
        ["Freepik"] = new() { "Leonardo", "Midjourney", "Canva" },
        ["Suno"] = new() { "Udio", "Mubert" },

        // Cross-category similarities (AI Gen → Social for publishing)
        ["Leonardo"] = new() { "Freepik", "Canva" },
        ["Midjourney"] = new() { "Freepik", "Leonardo" },
        ["Udio"] = new() { "Suno" }
    };

    // Standard workflow templates สำหรับ platforms ที่ยังไม่มี workflow
    private readonly Dictionary<string, WorkflowTemplate> _standardTemplates = new();

    private readonly string _learningDataPath;

    public AutoLearningEngine(
        ILogger<AutoLearningEngine> logger,
        ContentGeneratorService aiService,
        WorkflowStorage storage,
        DeepPatternLearner patternLearner,
        WorkflowLearningEngine teachingEngine)
    {
        _logger = logger;
        _aiService = aiService;
        _storage = storage;
        _patternLearner = patternLearner;
        _teachingEngine = teachingEngine;

        _learningDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PostXAgent",
            "auto_learning"
        );

        Directory.CreateDirectory(_learningDataPath);
        InitializeStandardTemplates();
    }

    #region Human Teaching Integration

    /// <summary>
    /// เรียนรู้จากการสอนของมนุษย์และบันทึกเป็น foundation
    /// </summary>
    public async Task<AutoLearningResult> LearnFromHumanTeachingAsync(
        TeachingSession session,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Learning from human teaching: {Platform}/{Type}",
            session.Platform, session.WorkflowType);

        var result = new AutoLearningResult
        {
            Platform = session.Platform,
            WorkflowType = session.WorkflowType,
            LearnedAt = DateTime.UtcNow,
            Source = LearningSource.HumanTeaching
        };

        try
        {
            // 1. ใช้ Teaching Engine สร้าง base workflow
            var baseWorkflow = await _teachingEngine.LearnFromTeachingSession(session, ct);
            baseWorkflow.IsHumanTrained = true;

            result.WorkflowId = baseWorkflow.Id;
            result.StepsLearned = baseWorkflow.Steps.Count;

            // 2. Extract patterns จาก workflow นี้
            var patterns = await ExtractPatternsFromWorkflowAsync(baseWorkflow, ct);
            result.PatternsExtracted = patterns.Count;

            // 3. บันทึก patterns สำหรับ transfer learning
            await SavePlatformPatternsAsync(session.Platform, session.WorkflowType, patterns, ct);

            // 4. ลองสร้าง workflow สำหรับ platforms ที่คล้ายกัน
            if (_platformSimilarities.TryGetValue(session.Platform, out var similarPlatforms))
            {
                foreach (var similarPlatform in similarPlatforms)
                {
                    var transferResult = await TryTransferWorkflowAsync(
                        baseWorkflow, similarPlatform, ct);

                    if (transferResult != null)
                    {
                        result.TransferredWorkflows.Add(transferResult);
                    }
                }
            }

            // 5. อัพเดท platform knowledge
            await UpdatePlatformKnowledgeFromTeachingAsync(session, baseWorkflow, ct);

            result.Success = true;
            result.Message = $"Successfully learned workflow with {baseWorkflow.Steps.Count} steps. " +
                            $"Extracted {patterns.Count} patterns. " +
                            $"Created {result.TransferredWorkflows.Count} transferred workflows.";

            _logger.LogInformation(result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to learn from human teaching");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// ต่อยอดจาก workflow ที่มนุษย์สอนด้วยการเรียนรู้เพิ่มเติม
    /// </summary>
    public async Task<AutoLearningResult> ContinueLearningAsync(
        string workflowId,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Continuing learning for workflow {WorkflowId}", workflowId);

        var result = new AutoLearningResult
        {
            WorkflowId = workflowId,
            LearnedAt = DateTime.UtcNow,
            Source = LearningSource.AutoContinued
        };

        try
        {
            // 1. โหลด workflow เดิม
            var workflow = await _storage.LoadWorkflowAsync(workflowId, ct);
            if (workflow == null)
            {
                result.Success = false;
                result.Error = "Workflow not found";
                return result;
            }

            result.Platform = workflow.Platform;
            result.WorkflowType = workflow.Name;

            // 2. วิเคราะห์หน้าปัจจุบัน
            var pageAnalysis = await _patternLearner.AnalyzeWithAIAsync(
                workflow.Platform,
                workflow.Name,
                pageHtml,
                screenshot,
                ct);

            // 3. เปรียบเทียบกับ workflow ที่มี
            var differences = await ComparePagesWithWorkflowAsync(workflow, pageAnalysis, ct);

            // 4. อัพเดท selectors ที่อาจเปลี่ยนแปลง
            if (differences.ChangedElements.Count > 0)
            {
                await UpdateSelectorsFromPageAsync(workflow, differences, pageHtml, ct);
                result.SelectorsUpdated = differences.ChangedElements.Count;
            }

            // 5. เพิ่ม steps ใหม่ถ้าพบ
            if (differences.NewElements.Count > 0)
            {
                var newSteps = await SuggestNewStepsAsync(workflow, differences.NewElements, ct);
                result.NewStepsSuggested = newSteps.Count;
            }

            // 6. บันทึก workflow ที่อัพเดท
            workflow.UpdatedAt = DateTime.UtcNow;
            await _storage.SaveWorkflowAsync(workflow, ct);

            result.Success = true;
            result.Message = $"Updated {result.SelectorsUpdated} selectors, " +
                            $"suggested {result.NewStepsSuggested} new steps.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to continue learning");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    #endregion

    #region Automatic Workflow Generation

    /// <summary>
    /// สร้าง workflow อัตโนมัติสำหรับ platform ใหม่โดยใช้ knowledge ที่เรียนรู้มา
    /// </summary>
    public async Task<LearnedWorkflow?> GenerateWorkflowForNewPlatformAsync(
        string platform,
        string workflowType,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating workflow for new platform: {Platform}/{Type}",
            platform, workflowType);

        try
        {
            // 1. ตรวจสอบว่ามี similar platform ที่มี workflow อยู่แล้วหรือไม่
            var sourceWorkflow = await FindBestSourceWorkflowAsync(platform, workflowType, ct);

            LearnedWorkflow? newWorkflow;

            if (sourceWorkflow != null)
            {
                // 2a. ใช้ transfer learning จาก platform ที่คล้าย
                newWorkflow = await TransferWorkflowWithAdaptationAsync(
                    sourceWorkflow, platform, pageHtml, screenshot, ct);
            }
            else
            {
                // 2b. สร้างจาก AI analysis โดยไม่มี reference
                newWorkflow = await GenerateFromAIAnalysisAsync(
                    platform, workflowType, pageHtml, screenshot, ct);
            }

            if (newWorkflow != null)
            {
                // 3. Mark ว่าเป็น AI generated
                newWorkflow.Steps.ForEach(s => s.LearnedFrom = LearnedSource.AIGenerated);
                await _storage.SaveWorkflowAsync(newWorkflow, ct);

                _logger.LogInformation(
                    "Generated workflow {Id} with {StepCount} steps (confidence: {Confidence:F2})",
                    newWorkflow.Id, newWorkflow.Steps.Count, newWorkflow.ConfidenceScore);
            }

            return newWorkflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate workflow for {Platform}", platform);
            return null;
        }
    }

    /// <summary>
    /// วิเคราะห์หน้าเว็บและแนะนำ workflow ที่เหมาะสม
    /// </summary>
    public async Task<WorkflowSuggestion> AnalyzeAndSuggestAsync(
        string url,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Analyzing page for workflow suggestions: {Url}", url);

        var suggestion = new WorkflowSuggestion
        {
            Url = url,
            AnalyzedAt = DateTime.UtcNow
        };

        try
        {
            // 1. ตรวจจับ platform จาก URL
            suggestion.DetectedPlatform = DetectPlatformFromUrl(url);

            // 2. วิเคราะห์ page type
            var pageAnalysis = await AnalyzePageTypeAsync(pageHtml, ct);
            suggestion.PageType = pageAnalysis.PageType;
            suggestion.Confidence = pageAnalysis.Confidence;

            // 3. หา existing workflows ที่อาจใช้ได้
            var existingWorkflows = await FindApplicableWorkflowsAsync(
                suggestion.DetectedPlatform, pageAnalysis.PageType, ct);
            suggestion.ExistingWorkflows = existingWorkflows;

            // 4. ถ้าไม่มี ให้แนะนำ steps
            if (!existingWorkflows.Any())
            {
                suggestion.SuggestedSteps = await SuggestWorkflowStepsAsync(
                    suggestion.DetectedPlatform,
                    pageAnalysis.PageType,
                    pageHtml,
                    ct);
            }

            // 5. ระบุว่าต้องการ human teaching หรือไม่
            suggestion.NeedsHumanTeaching = suggestion.Confidence < 0.7 ||
                                            !existingWorkflows.Any();

            suggestion.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze page");
            suggestion.Success = false;
            suggestion.Error = ex.Message;
        }

        return suggestion;
    }

    #endregion

    #region Cross-Platform Learning

    /// <summary>
    /// Transfer workflow จาก platform หนึ่งไปอีก platform
    /// </summary>
    public async Task<LearnedWorkflow?> TransferWorkflowAsync(
        string sourceWorkflowId,
        string targetPlatform,
        string targetPageHtml,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Transferring workflow {WorkflowId} to {Platform}",
            sourceWorkflowId, targetPlatform);

        try
        {
            var sourceWorkflow = await _storage.LoadWorkflowAsync(sourceWorkflowId, ct);
            if (sourceWorkflow == null)
            {
                _logger.LogWarning("Source workflow not found: {Id}", sourceWorkflowId);
                return null;
            }

            return await TransferWorkflowWithAdaptationAsync(
                sourceWorkflow, targetPlatform, targetPageHtml, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transfer workflow");
            return null;
        }
    }

    /// <summary>
    /// รวม patterns จากหลาย workflows เพื่อสร้าง best practice workflow
    /// </summary>
    public async Task<LearnedWorkflow?> MergeBestPracticesAsync(
        string workflowType,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Merging best practices for {Type}", workflowType);

        try
        {
            // 1. โหลดทุก workflows ที่เป็น type เดียวกัน
            var allWorkflows = await _storage.GetAllWorkflowsAsync(ct);
            var relevantWorkflows = allWorkflows
                .Where(w => w.Name == workflowType && w.IsActive)
                .OrderByDescending(w => w.GetSuccessRate())
                .ThenByDescending(w => w.SuccessCount)
                .Take(5)
                .ToList();

            if (relevantWorkflows.Count == 0)
            {
                return null;
            }

            // 2. วิเคราะห์ common patterns
            var commonPatterns = await AnalyzeCommonPatternsAsync(relevantWorkflows, ct);

            // 3. สร้าง merged workflow
            var mergedWorkflow = new LearnedWorkflow
            {
                Id = Guid.NewGuid().ToString(),
                Platform = "Universal", // ใช้ได้กับทุก platform
                Name = workflowType,
                Description = $"Best practice workflow for {workflowType} (merged from {relevantWorkflows.Count} workflows)",
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Steps = CreateMergedSteps(commonPatterns)
            };

            mergedWorkflow.ConfidenceScore = mergedWorkflow.Steps.Average(s => s.ConfidenceScore);

            await _storage.SaveWorkflowAsync(mergedWorkflow, ct);

            _logger.LogInformation(
                "Created merged workflow with {StepCount} steps",
                mergedWorkflow.Steps.Count);

            return mergedWorkflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to merge best practices");
            return null;
        }
    }

    #endregion

    #region Continuous Learning

    /// <summary>
    /// เรียนรู้จากผลการ execute workflow
    /// </summary>
    public async Task LearnFromExecutionAsync(
        string workflowId,
        WorkflowExecutionResult executionResult,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        try
        {
            var workflow = await _storage.LoadWorkflowAsync(workflowId, ct);
            if (workflow == null) return;

            // 1. ใช้ DeepPatternLearner เรียนรู้
            var learningResult = await _patternLearner.LearnFromExecutionAsync(
                workflow.Platform,
                workflow.Name,
                workflow,
                executionResult,
                pageHtml,
                screenshot,
                ct);

            // 2. ถ้าล้มเหลว ลอง auto-repair
            if (!executionResult.Success && executionResult.FailedAtStep.HasValue)
            {
                await TryAutoRepairAsync(workflow, executionResult, pageHtml, screenshot, ct);
            }

            // 3. ถ้าสำเร็จ ปรับปรุง confidence ของ selectors ที่ใช้
            if (executionResult.Success)
            {
                await ReinforceLearningAsync(workflow, executionResult, ct);
            }

            // 4. Share learning กับ similar platforms
            await ShareLearningWithSimilarPlatformsAsync(workflow, learningResult, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to learn from execution");
        }
    }

    /// <summary>
    /// Background learning task - รันเป็น periodic job
    /// </summary>
    public async Task RunPeriodicLearningAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Running periodic learning tasks");

        try
        {
            // 1. Optimize workflows ที่มี failure rate สูง
            await OptimizeFailingWorkflowsAsync(ct);

            // 2. Merge similar patterns
            await ConsolidatePatternsAsync(ct);

            // 3. Update platform knowledge
            await RefreshPlatformKnowledgeAsync(ct);

            // 4. Clean up old data
            await CleanupOldLearningDataAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Periodic learning failed");
        }
    }

    #endregion

    #region Private Methods

    private void InitializeStandardTemplates()
    {
        // Template สำหรับ Post workflow
        _standardTemplates["post"] = new WorkflowTemplate
        {
            Name = "post",
            Description = "Standard post creation workflow",
            Steps = new List<TemplateStep>
            {
                new() { Action = StepAction.Navigate, Purpose = "Go to compose page" },
                new() { Action = StepAction.Click, Purpose = "Click compose button", Semantic = "compose-button" },
                new() { Action = StepAction.Type, Purpose = "Enter post content", Semantic = "content-input", Variable = "{{content.text}}" },
                new() { Action = StepAction.Upload, Purpose = "Upload media", Semantic = "media-upload", IsOptional = true },
                new() { Action = StepAction.Click, Purpose = "Click post button", Semantic = "submit-button" },
                new() { Action = StepAction.Wait, Purpose = "Wait for confirmation" }
            }
        };

        // Template สำหรับ Login workflow
        _standardTemplates["login"] = new WorkflowTemplate
        {
            Name = "login",
            Description = "Standard login workflow",
            Steps = new List<TemplateStep>
            {
                new() { Action = StepAction.Navigate, Purpose = "Go to login page" },
                new() { Action = StepAction.Type, Purpose = "Enter username/email", Semantic = "username-input", Variable = "{{credentials.username}}" },
                new() { Action = StepAction.Type, Purpose = "Enter password", Semantic = "password-input", Variable = "{{credentials.password}}" },
                new() { Action = StepAction.Click, Purpose = "Click login button", Semantic = "submit-button" },
                new() { Action = StepAction.WaitForNavigation, Purpose = "Wait for redirect" }
            }
        };

        // Template สำหรับ Upload workflow
        _standardTemplates["upload"] = new WorkflowTemplate
        {
            Name = "upload",
            Description = "Standard media upload workflow",
            Steps = new List<TemplateStep>
            {
                new() { Action = StepAction.Navigate, Purpose = "Go to upload page" },
                new() { Action = StepAction.Click, Purpose = "Click upload area", Semantic = "upload-trigger" },
                new() { Action = StepAction.Upload, Purpose = "Select file", Semantic = "file-input", Variable = "{{content.media}}" },
                new() { Action = StepAction.Wait, Purpose = "Wait for upload" },
                new() { Action = StepAction.Type, Purpose = "Enter description", Semantic = "description-input", Variable = "{{content.text}}", IsOptional = true },
                new() { Action = StepAction.Click, Purpose = "Click publish", Semantic = "submit-button" }
            }
        };
    }

    private async Task<List<LearnedPattern>> ExtractPatternsFromWorkflowAsync(
        LearnedWorkflow workflow,
        CancellationToken ct)
    {
        var patterns = new List<LearnedPattern>();

        foreach (var step in workflow.Steps)
        {
            var pattern = new LearnedPattern
            {
                Id = Guid.NewGuid().ToString(),
                Platform = workflow.Platform,
                WorkflowType = workflow.Name,
                StepOrder = step.Order,
                Action = step.Action,
                SelectorType = step.Selector.Type,
                SelectorPattern = ExtractSelectorPattern(step.Selector),
                SemanticPurpose = step.Description ?? InferSemanticPurpose(step),
                Confidence = step.ConfidenceScore,
                LearnedAt = DateTime.UtcNow
            };

            // Extract input variable pattern
            if (!string.IsNullOrEmpty(step.InputVariable))
            {
                pattern.InputVariable = step.InputVariable;
            }

            patterns.Add(pattern);
        }

        return await Task.FromResult(patterns);
    }

    private string ExtractSelectorPattern(ElementSelector selector)
    {
        // สร้าง pattern จาก selector ที่สามารถ transfer ได้
        return selector.Type switch
        {
            SelectorType.AriaLabel => $"aria-label:*{selector.Value}*",
            SelectorType.Role => $"role:{selector.Value}",
            SelectorType.Placeholder => $"placeholder:*{selector.Value}*",
            SelectorType.Text => $"text:*{selector.Value}*",
            SelectorType.TestId => $"testid:*{GetTestIdPattern(selector.Value)}*",
            _ => $"semantic:{selector.AIDescription ?? "unknown"}"
        };
    }

    private string GetTestIdPattern(string testId)
    {
        // Extract common pattern from testid
        // e.g., "fb-post-button" -> "post-button"
        var parts = testId.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join("-", parts.Skip(1)) : testId;
    }

    private string InferSemanticPurpose(WorkflowStep step)
    {
        return step.Action switch
        {
            StepAction.Click when step.Selector.Value.Contains("submit", StringComparison.OrdinalIgnoreCase) => "submit-action",
            StepAction.Click when step.Selector.Value.Contains("post", StringComparison.OrdinalIgnoreCase) => "post-action",
            StepAction.Click when step.Selector.Value.Contains("login", StringComparison.OrdinalIgnoreCase) => "login-action",
            StepAction.Type when step.InputVariable?.Contains("content") == true => "content-input",
            StepAction.Type when step.InputVariable?.Contains("password") == true => "password-input",
            StepAction.Upload => "file-upload",
            _ => "unknown"
        };
    }

    private async Task SavePlatformPatternsAsync(
        string platform,
        string workflowType,
        List<LearnedPattern> patterns,
        CancellationToken ct)
    {
        var filePath = Path.Combine(_learningDataPath, $"{platform}_{workflowType}_patterns.json");
        var json = JsonConvert.SerializeObject(patterns, Formatting.Indented);
        await File.WriteAllTextAsync(filePath, json, ct);
    }

    private async Task<TransferredWorkflow?> TryTransferWorkflowAsync(
        LearnedWorkflow sourceWorkflow,
        string targetPlatform,
        CancellationToken ct)
    {
        try
        {
            // สร้าง workflow ใหม่โดย adapt selectors
            var transferredWorkflow = new LearnedWorkflow
            {
                Id = Guid.NewGuid().ToString(),
                Platform = targetPlatform,
                Name = sourceWorkflow.Name,
                Description = $"Transferred from {sourceWorkflow.Platform}",
                CreatedAt = DateTime.UtcNow,
                IsActive = false, // ต้อง validate ก่อน
                ConfidenceScore = sourceWorkflow.ConfidenceScore * 0.6 // ลด confidence
            };

            // Adapt steps
            var adaptedSteps = new List<WorkflowStep>();
            foreach (var step in sourceWorkflow.Steps)
            {
                var adaptedStep = AdaptStepForPlatform(step, targetPlatform);
                adaptedSteps.Add(adaptedStep);
            }

            transferredWorkflow.Steps = adaptedSteps;
            await _storage.SaveWorkflowAsync(transferredWorkflow, ct);

            return new TransferredWorkflow
            {
                WorkflowId = transferredWorkflow.Id,
                TargetPlatform = targetPlatform,
                StepsTransferred = adaptedSteps.Count,
                Confidence = transferredWorkflow.ConfidenceScore
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to transfer to {Platform}", targetPlatform);
            return null;
        }
    }

    private WorkflowStep AdaptStepForPlatform(WorkflowStep sourceStep, string targetPlatform)
    {
        var step = CloneStep(sourceStep);
        step.Id = Guid.NewGuid().ToString();
        step.LearnedFrom = LearnedSource.AIGenerated;
        step.ConfidenceScore *= 0.6; // ลด confidence เพราะยังไม่ได้ validate

        // ปรับ selector ให้เป็น semantic-based แทน
        step.Selector = new ElementSelector
        {
            Type = SelectorType.Smart,
            Value = "",
            AIDescription = sourceStep.Selector.AIDescription ?? sourceStep.Description,
            Confidence = step.ConfidenceScore
        };

        return step;
    }

    private async Task UpdatePlatformKnowledgeFromTeachingAsync(
        TeachingSession session,
        LearnedWorkflow workflow,
        CancellationToken ct)
    {
        var knowledge = _patternLearner.GetPlatformKnowledge(session.Platform);
        if (knowledge == null)
        {
            // DeepPatternLearner จะสร้างให้เมื่อเรียก API
            return;
        }

        // Update common selectors
        foreach (var step in workflow.Steps)
        {
            var key = step.Description ?? step.Action.ToString();
            if (!knowledge.CommonSelectors.ContainsKey(key))
            {
                knowledge.CommonSelectors[key] = new List<string>();
            }
            knowledge.CommonSelectors[key].Add(step.Selector.Value);
        }

        await Task.CompletedTask;
    }

    private async Task<LearnedWorkflow?> FindBestSourceWorkflowAsync(
        string targetPlatform,
        string workflowType,
        CancellationToken ct)
    {
        var allWorkflows = await _storage.GetAllWorkflowsAsync(ct);

        // 1. ลองหาจาก similar platforms ก่อน
        if (_platformSimilarities.TryGetValue(targetPlatform, out var similarPlatforms))
        {
            foreach (var similar in similarPlatforms)
            {
                var workflow = allWorkflows
                    .Where(w => w.Platform == similar &&
                               w.Name == workflowType &&
                               w.IsActive &&
                               w.IsHumanTrained)
                    .OrderByDescending(w => w.GetSuccessRate())
                    .FirstOrDefault();

                if (workflow != null)
                {
                    return workflow;
                }
            }
        }

        // 2. หา workflow ที่ดีที่สุดจากทุก platform
        return allWorkflows
            .Where(w => w.Name == workflowType &&
                       w.IsActive &&
                       w.GetSuccessRate() > 0.7)
            .OrderByDescending(w => w.GetSuccessRate())
            .ThenByDescending(w => w.SuccessCount)
            .FirstOrDefault();
    }

    private async Task<LearnedWorkflow?> TransferWorkflowWithAdaptationAsync(
        LearnedWorkflow source,
        string targetPlatform,
        string pageHtml,
        string? screenshot,
        CancellationToken ct)
    {
        // 1. วิเคราะห์หน้า target
        var pageAnalysis = await _patternLearner.AnalyzeWithAIAsync(
            targetPlatform, source.Name, pageHtml, screenshot, ct);

        if (!pageAnalysis.Success)
        {
            return null;
        }

        // 2. สร้าง workflow ใหม่
        var newWorkflow = new LearnedWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            Platform = targetPlatform,
            Name = source.Name,
            Description = $"Adapted from {source.Platform} (confidence: {pageAnalysis.Confidence:F2})",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            ConfidenceScore = Math.Min(source.ConfidenceScore, pageAnalysis.Confidence)
        };

        // 3. Map steps จาก source ไปยัง target
        var steps = new List<WorkflowStep>();
        var elementMap = pageAnalysis.ElementMap;

        for (int i = 0; i < source.Steps.Count; i++)
        {
            var sourceStep = source.Steps[i];

            // หา matching element ใน target page
            var matchedSelector = await FindMatchingSelectorAsync(
                sourceStep, pageHtml, pageAnalysis, ct);

            var newStep = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString(),
                Order = i,
                Action = sourceStep.Action,
                Description = sourceStep.Description,
                Selector = matchedSelector ?? CreateSmartSelector(sourceStep),
                InputVariable = sourceStep.InputVariable,
                IsOptional = sourceStep.IsOptional,
                WaitBeforeMs = sourceStep.WaitBeforeMs,
                WaitAfterMs = sourceStep.WaitAfterMs,
                TimeoutMs = sourceStep.TimeoutMs,
                LearnedFrom = LearnedSource.AIGenerated,
                ConfidenceScore = matchedSelector?.Confidence ?? 0.5
            };

            steps.Add(newStep);
        }

        newWorkflow.Steps = steps;
        newWorkflow.ConfidenceScore = steps.Average(s => s.ConfidenceScore);

        return newWorkflow;
    }

    private async Task<LearnedWorkflow?> GenerateFromAIAnalysisAsync(
        string platform,
        string workflowType,
        string pageHtml,
        string? screenshot,
        CancellationToken ct)
    {
        // 1. ใช้ AI วิเคราะห์หน้า
        var pageAnalysis = await _patternLearner.AnalyzeWithAIAsync(
            platform, workflowType, pageHtml, screenshot, ct);

        if (!pageAnalysis.Success || pageAnalysis.SuggestedSteps.Count == 0)
        {
            // ลองใช้ standard template
            return await CreateFromStandardTemplateAsync(platform, workflowType, pageHtml, ct);
        }

        // 2. ใช้ DeepPatternLearner สร้าง workflow
        return await _patternLearner.GenerateWorkflowFromAIAsync(
            platform, workflowType, pageAnalysis, ct);
    }

    private async Task<LearnedWorkflow?> CreateFromStandardTemplateAsync(
        string platform,
        string workflowType,
        string pageHtml,
        CancellationToken ct)
    {
        if (!_standardTemplates.TryGetValue(workflowType.ToLowerInvariant(), out var template))
        {
            return null;
        }

        var workflow = new LearnedWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            Platform = platform,
            Name = workflowType,
            Description = $"Created from standard template: {template.Description}",
            CreatedAt = DateTime.UtcNow,
            IsActive = false, // ต้อง validate ก่อน
            ConfidenceScore = 0.4 // Low confidence เพราะใช้ template
        };

        var steps = new List<WorkflowStep>();
        for (int i = 0; i < template.Steps.Count; i++)
        {
            var templateStep = template.Steps[i];
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString(),
                Order = i,
                Action = templateStep.Action,
                Description = templateStep.Purpose,
                Selector = new ElementSelector
                {
                    Type = SelectorType.Smart,
                    Value = "",
                    AIDescription = templateStep.Semantic ?? templateStep.Purpose,
                    Confidence = 0.4
                },
                InputVariable = templateStep.Variable,
                IsOptional = templateStep.IsOptional,
                LearnedFrom = LearnedSource.AIGenerated,
                ConfidenceScore = 0.4
            };
            steps.Add(step);
        }

        workflow.Steps = steps;
        await _storage.SaveWorkflowAsync(workflow, ct);

        return workflow;
    }

    private string DetectPlatformFromUrl(string url)
    {
        url = url.ToLowerInvariant();

        if (url.Contains("facebook.com") || url.Contains("fb.com")) return "Facebook";
        if (url.Contains("instagram.com")) return "Instagram";
        if (url.Contains("tiktok.com")) return "TikTok";
        if (url.Contains("twitter.com") || url.Contains("x.com")) return "Twitter";
        if (url.Contains("youtube.com") || url.Contains("youtu.be")) return "YouTube";
        if (url.Contains("line.me") || url.Contains("lineblog")) return "LINE";
        if (url.Contains("threads.net")) return "Threads";
        if (url.Contains("linkedin.com")) return "LinkedIn";
        if (url.Contains("pinterest.com")) return "Pinterest";

        return "Custom";
    }

    private async Task<PageTypeAnalysis> AnalyzePageTypeAsync(string pageHtml, CancellationToken ct)
    {
        var prompt = @"Analyze this webpage HTML and determine its type.

Respond in JSON:
{
  ""pageType"": ""login|compose|feed|profile|settings|upload|unknown"",
  ""confidence"": 0.0-1.0,
  ""detectedElements"": [""list of key elements found""]
}

HTML (first 3000 chars):
" + pageHtml.Substring(0, Math.Min(3000, pageHtml.Length));

        try
        {
            var result = await _aiService.GenerateAsync(prompt, null, "general", "en", ct);
            return ParsePageTypeAnalysis(result.Text);
        }
        catch
        {
            return new PageTypeAnalysis { PageType = "unknown", Confidence = 0.3 };
        }
    }

    private PageTypeAnalysis ParsePageTypeAnalysis(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonConvert.DeserializeObject<PageTypeAnalysis>(json) ?? new PageTypeAnalysis();
            }
        }
        catch { }

        return new PageTypeAnalysis { PageType = "unknown", Confidence = 0.3 };
    }

    private async Task<List<WorkflowReference>> FindApplicableWorkflowsAsync(
        string platform,
        string pageType,
        CancellationToken ct)
    {
        var workflows = await _storage.GetAllWorkflowsAsync(ct);
        return workflows
            .Where(w => w.Platform == platform && w.IsActive)
            .Select(w => new WorkflowReference
            {
                WorkflowId = w.Id,
                Name = w.Name,
                SuccessRate = w.GetSuccessRate(),
                IsHumanTrained = w.IsHumanTrained
            })
            .OrderByDescending(w => w.SuccessRate)
            .ToList();
    }

    private async Task<List<SuggestedStep>> SuggestWorkflowStepsAsync(
        string platform,
        string pageType,
        string pageHtml,
        CancellationToken ct)
    {
        // ใช้ standard template ถ้ามี
        var workflowType = pageType switch
        {
            "compose" => "post",
            "login" => "login",
            "upload" => "upload",
            _ => pageType
        };

        if (_standardTemplates.TryGetValue(workflowType, out var template))
        {
            return template.Steps.Select((s, i) => new SuggestedStep
            {
                Order = i,
                Action = s.Action.ToString().ToLower(),
                Description = s.Purpose,
                InputVariable = s.Variable,
                IsOptional = s.IsOptional,
                Confidence = 0.5
            }).ToList();
        }

        return new List<SuggestedStep>();
    }

    private async Task<PageDifferences> ComparePagesWithWorkflowAsync(
        LearnedWorkflow workflow,
        AIPageAnalysis pageAnalysis,
        CancellationToken ct)
    {
        var differences = new PageDifferences();

        // เปรียบเทียบ workflow steps กับ page elements
        foreach (var step in workflow.Steps)
        {
            bool found = false;
            foreach (var suggested in pageAnalysis.SuggestedSteps)
            {
                if (IsSimilarStep(step, suggested))
                {
                    found = true;
                    if (suggested.SelectorHint != step.Selector.Value)
                    {
                        differences.ChangedElements.Add(new ElementChange
                        {
                            StepId = step.Id,
                            OldSelector = step.Selector.Value,
                            NewSelector = suggested.SelectorHint ?? ""
                        });
                    }
                    break;
                }
            }

            if (!found)
            {
                differences.MissingElements.Add(step.Id);
            }
        }

        // หา elements ใหม่
        foreach (var suggested in pageAnalysis.SuggestedSteps)
        {
            bool exists = workflow.Steps.Any(s => IsSimilarStep(s, suggested));
            if (!exists)
            {
                differences.NewElements.Add(new NewElement
                {
                    Description = suggested.Description,
                    Action = suggested.Action,
                    Selector = suggested.SelectorHint ?? ""
                });
            }
        }

        return await Task.FromResult(differences);
    }

    private bool IsSimilarStep(WorkflowStep step, SuggestedStep suggested)
    {
        // เปรียบเทียบ action และ description
        return step.Action.ToString().Equals(suggested.Action, StringComparison.OrdinalIgnoreCase) &&
               (step.Description?.Contains(suggested.Description, StringComparison.OrdinalIgnoreCase) == true ||
                suggested.Description?.Contains(step.Description ?? "", StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task UpdateSelectorsFromPageAsync(
        LearnedWorkflow workflow,
        PageDifferences differences,
        string pageHtml,
        CancellationToken ct)
    {
        foreach (var change in differences.ChangedElements)
        {
            var step = workflow.Steps.FirstOrDefault(s => s.Id == change.StepId);
            if (step != null && !string.IsNullOrEmpty(change.NewSelector))
            {
                // Keep old selector as alternative
                step.AlternativeSelectors.Add(step.Selector);

                // Update with new selector
                step.Selector = new ElementSelector
                {
                    Type = DetermineSelecrorType(change.NewSelector),
                    Value = change.NewSelector,
                    Confidence = 0.7, // Medium confidence for auto-updated
                    AIDescription = step.Selector.AIDescription
                };

                step.LearnedFrom = LearnedSource.AutoRecovered;
            }
        }
    }

    private SelectorType DetermineSelecrorType(string selector)
    {
        if (selector.StartsWith("#")) return SelectorType.CSS;
        if (selector.StartsWith(".")) return SelectorType.CSS;
        if (selector.StartsWith("//")) return SelectorType.XPath;
        if (selector.StartsWith("[")) return SelectorType.CSS;
        return SelectorType.Smart;
    }

    private async Task<List<WorkflowStep>> SuggestNewStepsAsync(
        LearnedWorkflow workflow,
        List<NewElement> newElements,
        CancellationToken ct)
    {
        var suggestions = new List<WorkflowStep>();

        foreach (var element in newElements)
        {
            var step = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString(),
                Order = workflow.Steps.Count + suggestions.Count,
                Action = ParseAction(element.Action),
                Description = element.Description,
                Selector = new ElementSelector
                {
                    Type = DetermineSelecrorType(element.Selector),
                    Value = element.Selector,
                    Confidence = 0.5
                },
                IsOptional = true,
                LearnedFrom = LearnedSource.AIObserved,
                ConfidenceScore = 0.5
            };

            suggestions.Add(step);
        }

        return await Task.FromResult(suggestions);
    }

    private StepAction ParseAction(string action)
    {
        return action?.ToLowerInvariant() switch
        {
            "click" => StepAction.Click,
            "type" => StepAction.Type,
            "upload" => StepAction.Upload,
            "wait" => StepAction.Wait,
            "navigate" => StepAction.Navigate,
            "select" => StepAction.Select,
            _ => StepAction.Click
        };
    }

    private async Task<List<CommonPattern>> AnalyzeCommonPatternsAsync(
        List<LearnedWorkflow> workflows,
        CancellationToken ct)
    {
        var patterns = new List<CommonPattern>();
        var stepGroups = new Dictionary<string, List<WorkflowStep>>();

        // Group steps by similar description/action
        foreach (var workflow in workflows)
        {
            foreach (var step in workflow.Steps)
            {
                var key = $"{step.Action}:{step.Description ?? ""}";
                if (!stepGroups.ContainsKey(key))
                {
                    stepGroups[key] = new List<WorkflowStep>();
                }
                stepGroups[key].Add(step);
            }
        }

        // Create patterns from groups
        foreach (var (key, steps) in stepGroups)
        {
            if (steps.Count >= 2) // At least 2 occurrences
            {
                patterns.Add(new CommonPattern
                {
                    Key = key,
                    Steps = steps,
                    Frequency = steps.Count,
                    AverageConfidence = steps.Average(s => s.ConfidenceScore)
                });
            }
        }

        return await Task.FromResult(patterns.OrderByDescending(p => p.Frequency).ToList());
    }

    private List<WorkflowStep> CreateMergedSteps(List<CommonPattern> patterns)
    {
        var steps = new List<WorkflowStep>();
        int order = 0;

        foreach (var pattern in patterns.Take(10)) // Limit to 10 most common
        {
            var bestStep = pattern.Steps
                .OrderByDescending(s => s.ConfidenceScore)
                .First();

            var mergedStep = CloneStep(bestStep);
            mergedStep.Id = Guid.NewGuid().ToString();
            mergedStep.Order = order++;
            mergedStep.ConfidenceScore = pattern.AverageConfidence;
            mergedStep.LearnedFrom = LearnedSource.AIGenerated;

            // Collect all alternative selectors
            mergedStep.AlternativeSelectors = pattern.Steps
                .Select(s => s.Selector)
                .Distinct()
                .ToList();

            steps.Add(mergedStep);
        }

        return steps;
    }

    private async Task TryAutoRepairAsync(
        LearnedWorkflow workflow,
        WorkflowExecutionResult result,
        string pageHtml,
        string? screenshot,
        CancellationToken ct)
    {
        var repairedWorkflow = await _teachingEngine.TryAutoRepairWorkflow(
            workflow,
            result.FailedAtStep!.Value,
            pageHtml,
            screenshot,
            ct);

        if (repairedWorkflow != null)
        {
            _logger.LogInformation(
                "Auto-repaired workflow {Id} at step {Step}",
                workflow.Id, result.FailedAtStep);
        }
    }

    private async Task ReinforceLearningAsync(
        LearnedWorkflow workflow,
        WorkflowExecutionResult result,
        CancellationToken ct)
    {
        // Increase confidence for successful selectors
        foreach (var stepResult in result.StepResults.Where(s => s.Success))
        {
            var step = workflow.Steps.FirstOrDefault(s => s.Id == stepResult.StepId);
            if (step != null)
            {
                step.Selector.Confidence = Math.Min(1.0, step.Selector.Confidence + 0.02);
                step.ConfidenceScore = Math.Min(1.0, step.ConfidenceScore + 0.01);
            }
        }

        workflow.ConfidenceScore = workflow.Steps.Average(s => s.ConfidenceScore);
        await _storage.SaveWorkflowAsync(workflow, ct);
    }

    private async Task ShareLearningWithSimilarPlatformsAsync(
        LearnedWorkflow workflow,
        LearningResult learningResult,
        CancellationToken ct)
    {
        if (!learningResult.Success) return;

        if (_platformSimilarities.TryGetValue(workflow.Platform, out var similarPlatforms))
        {
            foreach (var platform in similarPlatforms)
            {
                var existingWorkflow = await _storage.FindSimilarWorkflowAsync(
                    platform, workflow.Name, ct);

                if (existingWorkflow != null)
                {
                    // Update with learned patterns
                    await ApplyLearnedPatternsAsync(existingWorkflow, workflow, ct);
                }
            }
        }
    }

    private async Task ApplyLearnedPatternsAsync(
        LearnedWorkflow target,
        LearnedWorkflow source,
        CancellationToken ct)
    {
        // Add successful selectors as alternatives
        for (int i = 0; i < Math.Min(target.Steps.Count, source.Steps.Count); i++)
        {
            var sourceStep = source.Steps[i];
            var targetStep = target.Steps[i];

            if (sourceStep.Selector.Confidence > targetStep.Selector.Confidence)
            {
                // Add current selector as alternative
                if (!targetStep.AlternativeSelectors.Any(s => s.Value == targetStep.Selector.Value))
                {
                    targetStep.AlternativeSelectors.Add(targetStep.Selector);
                }

                // Don't replace selector directly, just add as alternative
                if (!targetStep.AlternativeSelectors.Any(s => s.AIDescription == sourceStep.Selector.AIDescription))
                {
                    targetStep.AlternativeSelectors.Add(new ElementSelector
                    {
                        Type = SelectorType.Smart,
                        AIDescription = sourceStep.Selector.AIDescription,
                        Confidence = sourceStep.Selector.Confidence * 0.5
                    });
                }
            }
        }

        target.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveWorkflowAsync(target, ct);
    }

    private async Task OptimizeFailingWorkflowsAsync(CancellationToken ct)
    {
        var workflows = await _storage.GetAllWorkflowsAsync(ct);
        var failing = workflows.Where(w => w.IsActive && w.GetSuccessRate() < 0.5 && w.FailureCount > 3);

        foreach (var workflow in failing)
        {
            _logger.LogInformation("Optimizing failing workflow: {Id}", workflow.Id);

            // Reduce confidence and mark for review
            workflow.ConfidenceScore *= 0.8;
            workflow.UpdatedAt = DateTime.UtcNow;

            // If still failing after optimization, deactivate
            if (workflow.GetSuccessRate() < 0.3)
            {
                workflow.IsActive = false;
            }

            await _storage.SaveWorkflowAsync(workflow, ct);
        }
    }

    private async Task ConsolidatePatternsAsync(CancellationToken ct)
    {
        // Merge similar workflows periodically
        var workflows = await _storage.GetAllWorkflowsAsync(ct);
        var workflowTypes = workflows.Select(w => w.Name).Distinct();

        foreach (var type in workflowTypes)
        {
            var typeWorkflows = workflows.Where(w => w.Name == type && w.IsActive).ToList();
            if (typeWorkflows.Count >= 3)
            {
                // Check if we should merge
                await MergeBestPracticesAsync(type, ct);
            }
        }
    }

    private async Task RefreshPlatformKnowledgeAsync(CancellationToken ct)
    {
        // Refresh knowledge files are handled by DeepPatternLearner
        await Task.CompletedTask;
    }

    private async Task CleanupOldLearningDataAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        try
        {
            var files = Directory.GetFiles(_learningDataPath, "*.json");
            foreach (var file in files)
            {
                var lastWrite = File.GetLastWriteTimeUtc(file);
                if (lastWrite < cutoff)
                {
                    File.Delete(file);
                    _logger.LogDebug("Deleted old learning data: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old learning data");
        }

        await Task.CompletedTask;
    }

    private async Task<ElementSelector?> FindMatchingSelectorAsync(
        WorkflowStep sourceStep,
        string pageHtml,
        AIPageAnalysis analysis,
        CancellationToken ct)
    {
        // Try to find matching element in page
        foreach (var suggested in analysis.SuggestedSteps)
        {
            if (IsSimilarStep(sourceStep, suggested) && !string.IsNullOrEmpty(suggested.SelectorHint))
            {
                return new ElementSelector
                {
                    Type = DetermineSelecrorType(suggested.SelectorHint),
                    Value = suggested.SelectorHint,
                    AIDescription = sourceStep.Selector.AIDescription,
                    Confidence = suggested.Confidence
                };
            }
        }

        return await Task.FromResult<ElementSelector?>(null);
    }

    private ElementSelector CreateSmartSelector(WorkflowStep step)
    {
        return new ElementSelector
        {
            Type = SelectorType.Smart,
            Value = "",
            AIDescription = step.Selector.AIDescription ?? step.Description,
            Confidence = 0.4
        };
    }

    private WorkflowStep CloneStep(WorkflowStep source)
    {
        var json = JsonConvert.SerializeObject(source);
        return JsonConvert.DeserializeObject<WorkflowStep>(json)!;
    }

    #endregion
}

#region Models

public class AutoLearningResult
{
    public bool Success { get; set; }
    public string? WorkflowId { get; set; }
    public string Platform { get; set; } = "";
    public string WorkflowType { get; set; } = "";
    public LearningSource Source { get; set; }
    public int StepsLearned { get; set; }
    public int PatternsExtracted { get; set; }
    public int SelectorsUpdated { get; set; }
    public int NewStepsSuggested { get; set; }
    public List<TransferredWorkflow> TransferredWorkflows { get; set; } = new();
    public string? Message { get; set; }
    public string? Error { get; set; }
    public DateTime LearnedAt { get; set; }
}

public enum LearningSource
{
    HumanTeaching,
    AutoContinued,
    TransferLearning,
    TemplateGenerated,
    AIGenerated
}

public class TransferredWorkflow
{
    public string WorkflowId { get; set; } = "";
    public string TargetPlatform { get; set; } = "";
    public int StepsTransferred { get; set; }
    public double Confidence { get; set; }
}

public class LearnedPattern
{
    public string Id { get; set; } = "";
    public string Platform { get; set; } = "";
    public string WorkflowType { get; set; } = "";
    public int StepOrder { get; set; }
    public StepAction Action { get; set; }
    public SelectorType SelectorType { get; set; }
    public string SelectorPattern { get; set; } = "";
    public string SemanticPurpose { get; set; } = "";
    public string? InputVariable { get; set; }
    public double Confidence { get; set; }
    public DateTime LearnedAt { get; set; }
}

public class WorkflowSuggestion
{
    public string Url { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string DetectedPlatform { get; set; } = "";
    public string PageType { get; set; } = "";
    public double Confidence { get; set; }
    public List<WorkflowReference> ExistingWorkflows { get; set; } = new();
    public List<SuggestedStep> SuggestedSteps { get; set; } = new();
    public bool NeedsHumanTeaching { get; set; }
    public DateTime AnalyzedAt { get; set; }
}

public class WorkflowReference
{
    public string WorkflowId { get; set; } = "";
    public string Name { get; set; } = "";
    public double SuccessRate { get; set; }
    public bool IsHumanTrained { get; set; }
}

public class WorkflowTemplate
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<TemplateStep> Steps { get; set; } = new();
}

public class TemplateStep
{
    public StepAction Action { get; set; }
    public string Purpose { get; set; } = "";
    public string? Semantic { get; set; }
    public string? Variable { get; set; }
    public bool IsOptional { get; set; }
}

public class PageDifferences
{
    public List<ElementChange> ChangedElements { get; set; } = new();
    public List<string> MissingElements { get; set; } = new();
    public List<NewElement> NewElements { get; set; } = new();
}

public class ElementChange
{
    public string StepId { get; set; } = "";
    public string OldSelector { get; set; } = "";
    public string NewSelector { get; set; } = "";
}

public class NewElement
{
    public string Description { get; set; } = "";
    public string Action { get; set; } = "";
    public string Selector { get; set; } = "";
}

public class PageTypeAnalysis
{
    public string PageType { get; set; } = "";
    public double Confidence { get; set; }
    public List<string> DetectedElements { get; set; } = new();
}

public class CommonPattern
{
    public string Key { get; set; } = "";
    public List<WorkflowStep> Steps { get; set; } = new();
    public int Frequency { get; set; }
    public double AverageConfidence { get; set; }
}

#endregion
