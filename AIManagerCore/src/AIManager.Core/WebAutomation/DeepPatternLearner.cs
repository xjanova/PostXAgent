using AIManager.Core.WebAutomation.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// Deep Pattern Learner - เรียนรู้และจดจำ Pattern ของแต่ละ Platform
/// ใช้ AI เพื่อเข้าใจ context และปรับตัวเมื่อ UI เปลี่ยนแปลง
/// </summary>
public class DeepPatternLearner
{
    private readonly ILogger<DeepPatternLearner> _logger;
    private readonly ContentGeneratorService _aiService;
    private readonly WorkflowStorage _storage;
    private readonly VisualElementRecognizer _visualRecognizer;

    private readonly Dictionary<string, PlatformKnowledge> _platformKnowledge = new();
    private readonly string _knowledgePath;

    public DeepPatternLearner(
        ILogger<DeepPatternLearner> logger,
        ContentGeneratorService aiService,
        WorkflowStorage storage,
        VisualElementRecognizer visualRecognizer)
    {
        _logger = logger;
        _aiService = aiService;
        _storage = storage;
        _visualRecognizer = visualRecognizer;

        _knowledgePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PostXAgent",
            "knowledge"
        );

        Directory.CreateDirectory(_knowledgePath);
        LoadKnowledgeBase();
    }

    /// <summary>
    /// เรียนรู้ Pattern ใหม่จากการสังเกตและ AI Analysis
    /// </summary>
    public async Task<LearningResult> LearnFromExecutionAsync(
        string platform,
        string workflowType,
        LearnedWorkflow workflow,
        WorkflowExecutionResult executionResult,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Learning from execution: {Platform}/{Type}, Success: {Success}",
            platform, workflowType, executionResult.Success);

        var result = new LearningResult
        {
            Platform = platform,
            WorkflowType = workflowType,
            LearnedAt = DateTime.UtcNow
        };

        try
        {
            // 1. วิเคราะห์ Page Structure
            var pageAnalysis = await AnalyzePageStructureAsync(pageHtml, ct);

            // 2. อัพเดท Platform Knowledge
            UpdatePlatformKnowledge(platform, pageAnalysis);

            // 3. เรียนรู้ Visual Patterns
            foreach (var step in workflow.Steps)
            {
                if (step.Selector?.AIDescription != null)
                {
                    var features = ExtractFeaturesFromStep(step, pageHtml);
                    if (features != null)
                    {
                        _visualRecognizer.LearnPattern(platform, step.Selector.AIDescription, features);
                        result.LearnedPatterns++;
                    }
                }
            }

            // 4. ถ้าล้มเหลว วิเคราะห์สาเหตุและเรียนรู้
            if (!executionResult.Success && executionResult.FailedAtStep.HasValue)
            {
                var failureAnalysis = await AnalyzeFailureAsync(
                    workflow, executionResult, pageHtml, screenshot, ct);

                result.FailureReason = failureAnalysis.Reason;
                result.SuggestedFix = failureAnalysis.SuggestedFix;
                result.Confidence = failureAnalysis.Confidence;

                // บันทึก failure pattern เพื่อหลีกเลี่ยงในอนาคต
                await RecordFailurePatternAsync(platform, workflowType, failureAnalysis, ct);
            }

            // 5. อัพเดท Workflow Weights
            if (executionResult.Success)
            {
                await UpdateWorkflowWeightsAsync(workflow, executionResult, ct);
                result.WeightsUpdated = true;
            }

            // 6. บันทึก Knowledge
            await SaveKnowledgeAsync(ct);

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Learning failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// ใช้ AI วิเคราะห์หน้าเว็บและหา Elements
    /// </summary>
    public async Task<AIPageAnalysis> AnalyzeWithAIAsync(
        string platform,
        string purpose,
        string pageHtml,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("AI analyzing page for {Platform}: {Purpose}", platform, purpose);

        var analysis = new AIPageAnalysis
        {
            Platform = platform,
            Purpose = purpose,
            AnalyzedAt = DateTime.UtcNow
        };

        try
        {
            // ใช้ AI วิเคราะห์ HTML
            var prompt = BuildAIAnalysisPrompt(platform, purpose, pageHtml);
            var aiResult = await _aiService.GenerateAsync(prompt, null, "general", "en", ct);

            // Parse AI response
            analysis = ParseAIAnalysisResponse(aiResult.Text, analysis);

            // Cross-reference with visual analysis
            var visualMap = await _visualRecognizer.AnalyzePageAsync(pageHtml, screenshot, ct);
            analysis.ElementMap = visualMap;

            // ใช้ Platform Knowledge เพิ่มเติม
            if (_platformKnowledge.TryGetValue(platform, out var knowledge))
            {
                EnhanceAnalysisWithKnowledge(analysis, knowledge);
            }

            analysis.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI analysis failed");
            analysis.Success = false;
            analysis.Error = ex.Message;
        }

        return analysis;
    }

    /// <summary>
    /// สร้าง Workflow อัตโนมัติจาก AI Analysis
    /// </summary>
    public async Task<LearnedWorkflow?> GenerateWorkflowFromAIAsync(
        string platform,
        string workflowType,
        AIPageAnalysis analysis,
        CancellationToken ct = default)
    {
        if (!analysis.Success || analysis.SuggestedSteps.Count == 0)
        {
            _logger.LogWarning("Cannot generate workflow: no steps found");
            return null;
        }

        var workflow = new LearnedWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            Platform = platform,
            Name = workflowType,
            Description = $"AI-generated workflow for {platform} {workflowType}",
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var steps = new List<WorkflowStep>();

        for (int i = 0; i < analysis.SuggestedSteps.Count; i++)
        {
            var suggestedStep = analysis.SuggestedSteps[i];

            // สร้าง Selector จาก AI suggestion
            var selector = await CreateSelectorFromSuggestionAsync(
                suggestedStep, analysis.ElementMap, ct);

            var step = new WorkflowStep
            {
                Id = Guid.NewGuid().ToString(),
                Order = i,
                Action = ParseStepAction(suggestedStep.Action),
                Description = suggestedStep.Description,
                Selector = selector,
                InputVariable = suggestedStep.InputVariable,
                IsOptional = suggestedStep.IsOptional,
                WaitBeforeMs = suggestedStep.WaitBeforeMs ?? 500,
                WaitAfterMs = suggestedStep.WaitAfterMs ?? 500,
                TimeoutMs = 10000,
                LearnedFrom = LearnedSource.AIObserved,
                ConfidenceScore = suggestedStep.Confidence
            };

            // สร้าง alternative selectors
            step.AlternativeSelectors = await CreateAlternativeSelectorsAsync(
                suggestedStep, analysis.ElementMap, ct);

            steps.Add(step);
        }

        workflow.Steps = steps;
        workflow.ConfidenceScore = analysis.SuggestedSteps.Average(s => s.Confidence);

        // บันทึก workflow
        await _storage.SaveWorkflowAsync(workflow, ct);

        _logger.LogInformation(
            "Generated AI workflow {Id} with {StepCount} steps (confidence: {Confidence:F2})",
            workflow.Id, steps.Count, workflow.ConfidenceScore);

        return workflow;
    }

    /// <summary>
    /// ปรับปรุง Workflow จากผลลัพธ์
    /// </summary>
    public async Task<LearnedWorkflow> OptimizeWorkflowAsync(
        LearnedWorkflow workflow,
        List<WorkflowExecutionResult> recentResults,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Optimizing workflow {Id}", workflow.Id);

        // วิเคราะห์ failure patterns
        var failedSteps = recentResults
            .Where(r => !r.Success && r.FailedAtStep.HasValue)
            .GroupBy(r => r.FailedAtStep!.Value)
            .Select(g => new { Step = g.Key, FailureCount = g.Count() })
            .OrderByDescending(x => x.FailureCount)
            .ToList();

        // ปรับปรุง steps ที่มีปัญหา
        foreach (var failedStep in failedSteps.Where(f => f.FailureCount >= 2))
        {
            var step = workflow.Steps[failedStep.Step];

            // ลด confidence
            step.ConfidenceScore = Math.Max(0.1, step.ConfidenceScore - 0.2);

            // ถ้ามี alternative selectors ลองสลับไปใช้
            if (step.AlternativeSelectors.Count > 0)
            {
                var bestAlternative = step.AlternativeSelectors
                    .OrderByDescending(s => s.Confidence)
                    .FirstOrDefault();

                if (bestAlternative != null && bestAlternative.Confidence > step.Selector.Confidence)
                {
                    // สลับ selector
                    step.AlternativeSelectors.Add(step.Selector);
                    step.Selector = bestAlternative;
                    step.AlternativeSelectors.Remove(bestAlternative);

                    _logger.LogInformation(
                        "Swapped selector for step {Step} in workflow {Id}",
                        failedStep.Step, workflow.Id);
                }
            }

            // เพิ่ม wait time
            step.WaitBeforeMs = Math.Min(step.WaitBeforeMs + 500, 3000);
        }

        // ปรับปรุง timing ตาม success rate
        var avgSuccessTime = recentResults
            .Where(r => r.Success)
            .Select(r => r.Duration.TotalMilliseconds)
            .DefaultIfEmpty(5000)
            .Average();

        // ถ้าเร็วกว่าเฉลี่ย อาจลด wait time ได้
        foreach (var step in workflow.Steps)
        {
            if (avgSuccessTime < 3000 && step.WaitAfterMs > 500)
            {
                step.WaitAfterMs = Math.Max(200, step.WaitAfterMs - 100);
            }
        }

        workflow.UpdatedAt = DateTime.UtcNow;
        await _storage.SaveWorkflowAsync(workflow, ct);

        return workflow;
    }

    /// <summary>
    /// ดึง Knowledge สำหรับ Platform
    /// </summary>
    public PlatformKnowledge? GetPlatformKnowledge(string platform)
    {
        return _platformKnowledge.GetValueOrDefault(platform);
    }

    /// <summary>
    /// สร้าง Workflow จาก Template
    /// </summary>
    public async Task<LearnedWorkflow?> CreateFromTemplateAsync(
        string platform,
        string workflowType,
        CancellationToken ct = default)
    {
        // ใช้ template ที่เรียนรู้มา
        var template = GetWorkflowTemplate(platform, workflowType);
        if (template == null)
        {
            return null;
        }

        var workflow = new LearnedWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            Platform = platform,
            Name = workflowType,
            Description = $"Created from {platform} template",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            Steps = template.Steps.Select(s => CloneStep(s)).ToList(),
            ConfidenceScore = template.ConfidenceScore * 0.8 // ลด confidence เล็กน้อย
        };

        await _storage.SaveWorkflowAsync(workflow, ct);

        return workflow;
    }

    #region Private Methods

    private void LoadKnowledgeBase()
    {
        try
        {
            var files = Directory.GetFiles(_knowledgePath, "*.json");
            foreach (var file in files)
            {
                var json = File.ReadAllText(file);
                var knowledge = JsonConvert.DeserializeObject<PlatformKnowledge>(json);
                if (knowledge != null)
                {
                    _platformKnowledge[knowledge.Platform] = knowledge;
                }
            }

            _logger.LogInformation("Loaded {Count} platform knowledge bases", _platformKnowledge.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load knowledge base");
        }
    }

    private async Task SaveKnowledgeAsync(CancellationToken ct)
    {
        foreach (var (platform, knowledge) in _platformKnowledge)
        {
            var filePath = Path.Combine(_knowledgePath, $"{platform}.json");
            var json = JsonConvert.SerializeObject(knowledge, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json, ct);
        }
    }

    private async Task<PageStructureAnalysis> AnalyzePageStructureAsync(
        string pageHtml,
        CancellationToken ct)
    {
        var analysis = new PageStructureAnalysis();

        await Task.Run(() =>
        {
            // Analyze common patterns
            analysis.HasNavigation = pageHtml.Contains("<nav") ||
                                     pageHtml.Contains("navigation") ||
                                     pageHtml.Contains("header");

            analysis.HasForm = pageHtml.Contains("<form") ||
                               pageHtml.Contains("type=\"submit\"");

            analysis.HasMediaUpload = pageHtml.Contains("type=\"file\"") ||
                                      pageHtml.Contains("upload");

            analysis.HasModal = pageHtml.Contains("modal") ||
                                pageHtml.Contains("dialog") ||
                                pageHtml.Contains("overlay");

            // Count interactive elements
            analysis.ButtonCount = CountPattern(pageHtml, @"<button");
            analysis.InputCount = CountPattern(pageHtml, @"<input");
            analysis.LinkCount = CountPattern(pageHtml, @"<a\s");

            // Detect framework
            analysis.DetectedFramework = DetectFramework(pageHtml);
        }, ct);

        return analysis;
    }

    private int CountPattern(string html, string pattern)
    {
        return System.Text.RegularExpressions.Regex.Matches(html, pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
    }

    private string DetectFramework(string pageHtml)
    {
        if (pageHtml.Contains("_next") || pageHtml.Contains("__NEXT"))
            return "Next.js";
        if (pageHtml.Contains("ng-") || pageHtml.Contains("_ngcontent"))
            return "Angular";
        if (pageHtml.Contains("data-v-") || pageHtml.Contains("vue"))
            return "Vue";
        if (pageHtml.Contains("data-reactroot") || pageHtml.Contains("__react"))
            return "React";

        return "Unknown";
    }

    private void UpdatePlatformKnowledge(string platform, PageStructureAnalysis analysis)
    {
        if (!_platformKnowledge.ContainsKey(platform))
        {
            _platformKnowledge[platform] = new PlatformKnowledge
            {
                Platform = platform,
                FirstLearnedAt = DateTime.UtcNow
            };
        }

        var knowledge = _platformKnowledge[platform];
        knowledge.LastUpdatedAt = DateTime.UtcNow;
        knowledge.AnalysisCount++;

        // Update framework detection
        if (!string.IsNullOrEmpty(analysis.DetectedFramework) &&
            analysis.DetectedFramework != "Unknown")
        {
            knowledge.DetectedFramework = analysis.DetectedFramework;
        }

        // Update patterns
        knowledge.HasMediaUpload = analysis.HasMediaUpload;
        knowledge.HasModal = analysis.HasModal;
    }

    private ElementFeatures? ExtractFeaturesFromStep(WorkflowStep step, string pageHtml)
    {
        // Simple feature extraction from step
        return new ElementFeatures
        {
            ElementId = step.Id,
            SemanticType = InferSemanticTypeFromAction(step.Action),
            Purpose = step.Selector?.AIDescription ?? step.Description ?? "unknown",
            Confidence = step.ConfidenceScore
        };
    }

    private string InferSemanticTypeFromAction(StepAction action)
    {
        return action switch
        {
            StepAction.Click => "button",
            StepAction.Type => "text-input",
            StepAction.Upload => "file-input",
            StepAction.Select => "dropdown",
            _ => "other"
        };
    }

    private async Task<FailureAnalysis> AnalyzeFailureAsync(
        LearnedWorkflow workflow,
        WorkflowExecutionResult result,
        string pageHtml,
        string? screenshot,
        CancellationToken ct)
    {
        var failedStep = workflow.Steps[result.FailedAtStep!.Value];
        var analysis = new FailureAnalysis
        {
            StepIndex = result.FailedAtStep.Value,
            OriginalError = result.Error
        };

        // ใช้ AI วิเคราะห์สาเหตุ
        var prompt = $@"Analyze why this web automation step failed:
Step Action: {failedStep.Action}
Step Description: {failedStep.Description}
Selector Type: {failedStep.Selector?.Type}
Selector Value: {failedStep.Selector?.Value}
Error Message: {result.Error}

Page HTML snippet (first 3000 chars):
{pageHtml.Substring(0, Math.Min(3000, pageHtml.Length))}

Respond with:
1. Most likely reason for failure (one line)
2. Suggested fix (one line)
3. Confidence level (0.0 to 1.0)";

        try
        {
            var aiResult = await _aiService.GenerateAsync(prompt, null, "general", "en", ct);
            ParseFailureAnalysisResponse(aiResult.Text, analysis);
        }
        catch
        {
            analysis.Reason = "Element not found or page structure changed";
            analysis.SuggestedFix = "Update selector or use alternative";
            analysis.Confidence = 0.5;
        }

        return analysis;
    }

    private void ParseFailureAnalysisResponse(string response, FailureAnalysis analysis)
    {
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length >= 1)
            analysis.Reason = lines[0].Replace("1.", "").Trim();

        if (lines.Length >= 2)
            analysis.SuggestedFix = lines[1].Replace("2.", "").Trim();

        if (lines.Length >= 3)
        {
            var confLine = lines[2].Replace("3.", "").Trim();
            if (double.TryParse(confLine, out var conf))
            {
                analysis.Confidence = conf;
            }
        }
    }

    private async Task RecordFailurePatternAsync(
        string platform,
        string workflowType,
        FailureAnalysis analysis,
        CancellationToken ct)
    {
        if (!_platformKnowledge.ContainsKey(platform))
        {
            _platformKnowledge[platform] = new PlatformKnowledge { Platform = platform };
        }

        var knowledge = _platformKnowledge[platform];
        knowledge.FailurePatterns.Add(new FailurePattern
        {
            WorkflowType = workflowType,
            Reason = analysis.Reason,
            RecordedAt = DateTime.UtcNow
        });

        // Keep only recent failure patterns
        if (knowledge.FailurePatterns.Count > 100)
        {
            knowledge.FailurePatterns = knowledge.FailurePatterns
                .OrderByDescending(f => f.RecordedAt)
                .Take(100)
                .ToList();
        }

        await Task.CompletedTask;
    }

    private async Task UpdateWorkflowWeightsAsync(
        LearnedWorkflow workflow,
        WorkflowExecutionResult result,
        CancellationToken ct)
    {
        // Increase confidence for successful steps
        foreach (var stepResult in result.StepResults.Where(s => s.Success))
        {
            var step = workflow.Steps.FirstOrDefault(s => s.Id == stepResult.StepId);
            if (step != null)
            {
                step.ConfidenceScore = Math.Min(1.0, step.ConfidenceScore + 0.02);
            }
        }

        workflow.ConfidenceScore = workflow.Steps.Average(s => s.ConfidenceScore);
        await _storage.SaveWorkflowAsync(workflow, ct);
    }

    private string BuildAIAnalysisPrompt(string platform, string purpose, string pageHtml)
    {
        return $@"Analyze this {platform} page for {purpose} automation.

HTML (first 5000 chars):
{pageHtml.Substring(0, Math.Min(5000, pageHtml.Length))}

Respond in JSON format:
{{
  ""pageType"": ""login|compose|feed|profile|settings|unknown"",
  ""suggestedSteps"": [
    {{
      ""order"": 1,
      ""action"": ""click|type|upload|wait|navigate"",
      ""description"": ""What this step does"",
      ""selectorHint"": ""CSS selector or description of element"",
      ""inputVariable"": ""{{{{content.text}}}} if this is a content input, null otherwise"",
      ""isOptional"": false,
      ""confidence"": 0.0-1.0
    }}
  ],
  ""confidence"": 0.0-1.0
}}";
    }

    private AIPageAnalysis ParseAIAnalysisResponse(string response, AIPageAnalysis analysis)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonConvert.DeserializeObject<AIAnalysisResponse>(json);

                if (parsed != null)
                {
                    analysis.PageType = parsed.PageType;
                    analysis.Confidence = parsed.Confidence;

                    analysis.SuggestedSteps = parsed.SuggestedSteps?.Select(s => new SuggestedStep
                    {
                        Order = s.Order,
                        Action = s.Action,
                        Description = s.Description,
                        SelectorHint = s.SelectorHint,
                        InputVariable = s.InputVariable,
                        IsOptional = s.IsOptional,
                        Confidence = s.Confidence
                    }).ToList() ?? new List<SuggestedStep>();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI analysis response");
        }

        return analysis;
    }

    private void EnhanceAnalysisWithKnowledge(AIPageAnalysis analysis, PlatformKnowledge knowledge)
    {
        // Add known patterns
        analysis.KnownPatterns = knowledge.FailurePatterns
            .Where(f => f.WorkflowType == analysis.Purpose)
            .Select(f => f.Reason)
            .Distinct()
            .ToList();
    }

    private async Task<ElementSelector> CreateSelectorFromSuggestionAsync(
        SuggestedStep suggestion,
        PageElementMap elementMap,
        CancellationToken ct)
    {
        // ถ้ามี selector hint ที่ชัดเจน ใช้เลย
        if (!string.IsNullOrEmpty(suggestion.SelectorHint) &&
            (suggestion.SelectorHint.StartsWith("#") ||
             suggestion.SelectorHint.StartsWith(".") ||
             suggestion.SelectorHint.StartsWith("[") ||
             suggestion.SelectorHint.StartsWith("//")))
        {
            return new ElementSelector
            {
                Type = suggestion.SelectorHint.StartsWith("//")
                    ? SelectorType.XPath : SelectorType.CSS,
                Value = suggestion.SelectorHint,
                Confidence = suggestion.Confidence,
                AIDescription = suggestion.Description
            };
        }

        // มิฉะนั้น ใช้ visual recognizer
        var features = new ElementFeatures
        {
            Purpose = suggestion.Description,
            SemanticType = InferSemanticTypeFromActionString(suggestion.Action)
        };

        return _visualRecognizer.CreateSmartSelector(features);
    }

    private async Task<List<ElementSelector>> CreateAlternativeSelectorsAsync(
        SuggestedStep suggestion,
        PageElementMap elementMap,
        CancellationToken ct)
    {
        var alternatives = new List<ElementSelector>();

        // Text-based alternative
        if (!string.IsNullOrEmpty(suggestion.Description))
        {
            var words = suggestion.Description.Split(' ').Take(3);
            foreach (var word in words.Where(w => w.Length >= 4))
            {
                alternatives.Add(new ElementSelector
                {
                    Type = SelectorType.XPath,
                    Value = $"//*[contains(text(), '{word}') or contains(@aria-label, '{word}')]",
                    Confidence = 0.5
                });
            }
        }

        // Semantic alternative
        var semanticType = InferSemanticTypeFromActionString(suggestion.Action);
        alternatives.Add(new ElementSelector
        {
            Type = SelectorType.XPath,
            Value = GetSemanticXPath(semanticType),
            Confidence = 0.4
        });

        return await Task.FromResult(alternatives);
    }

    private string InferSemanticTypeFromActionString(string action)
    {
        return action?.ToLowerInvariant() switch
        {
            "click" => "button",
            "type" => "text-input",
            "upload" => "file-input",
            "select" => "dropdown",
            _ => "other"
        };
    }

    private string GetSemanticXPath(string semanticType)
    {
        return semanticType switch
        {
            "button" => "//button | //input[@type='submit'] | //*[@role='button']",
            "text-input" => "//textarea | //input[@type='text'] | //*[@contenteditable='true']",
            "file-input" => "//input[@type='file']",
            "dropdown" => "//select",
            _ => "//*"
        };
    }

    private StepAction ParseStepAction(string action)
    {
        return action?.ToLowerInvariant() switch
        {
            "click" => StepAction.Click,
            "type" => StepAction.Type,
            "upload" => StepAction.Upload,
            "wait" => StepAction.Wait,
            "navigate" => StepAction.Navigate,
            "select" => StepAction.Select,
            "scroll" => StepAction.Scroll,
            _ => StepAction.Click
        };
    }

    private LearnedWorkflow? GetWorkflowTemplate(string platform, string workflowType)
    {
        // Load from storage if exists
        var workflows = _storage.GetAllWorkflowsAsync().GetAwaiter().GetResult();
        return workflows
            .Where(w => w.Platform == platform && w.Name == workflowType && w.IsActive)
            .OrderByDescending(w => w.ConfidenceScore)
            .ThenByDescending(w => w.SuccessCount)
            .FirstOrDefault();
    }

    private WorkflowStep CloneStep(WorkflowStep source)
    {
        var json = JsonConvert.SerializeObject(source);
        var clone = JsonConvert.DeserializeObject<WorkflowStep>(json)!;
        clone.Id = Guid.NewGuid().ToString();
        return clone;
    }

    #endregion
}

#region Models

public class LearningResult
{
    public string Platform { get; set; } = "";
    public string WorkflowType { get; set; } = "";
    public bool Success { get; set; }
    public int LearnedPatterns { get; set; }
    public bool WeightsUpdated { get; set; }
    public string? FailureReason { get; set; }
    public string? SuggestedFix { get; set; }
    public double Confidence { get; set; }
    public string? Error { get; set; }
    public DateTime LearnedAt { get; set; }
}

public class AIPageAnalysis
{
    public string Platform { get; set; } = "";
    public string Purpose { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string PageType { get; set; } = "";
    public double Confidence { get; set; }
    public List<SuggestedStep> SuggestedSteps { get; set; } = new();
    public PageElementMap? ElementMap { get; set; }
    public List<string> KnownPatterns { get; set; } = new();
    public DateTime AnalyzedAt { get; set; }
}

public class SuggestedStep
{
    public int Order { get; set; }
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string? SelectorHint { get; set; }
    public string? InputVariable { get; set; }
    public bool IsOptional { get; set; }
    public int? WaitBeforeMs { get; set; }
    public int? WaitAfterMs { get; set; }
    public double Confidence { get; set; }
}

public class PageStructureAnalysis
{
    public bool HasNavigation { get; set; }
    public bool HasForm { get; set; }
    public bool HasMediaUpload { get; set; }
    public bool HasModal { get; set; }
    public int ButtonCount { get; set; }
    public int InputCount { get; set; }
    public int LinkCount { get; set; }
    public string DetectedFramework { get; set; } = "";
}

public class FailureAnalysis
{
    public int StepIndex { get; set; }
    public string? OriginalError { get; set; }
    public string Reason { get; set; } = "";
    public string SuggestedFix { get; set; } = "";
    public double Confidence { get; set; }
}

public class PlatformKnowledge
{
    public string Platform { get; set; } = "";
    public DateTime FirstLearnedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public int AnalysisCount { get; set; }
    public string? DetectedFramework { get; set; }
    public bool HasMediaUpload { get; set; }
    public bool HasModal { get; set; }
    public List<FailurePattern> FailurePatterns { get; set; } = new();
    public Dictionary<string, List<string>> CommonSelectors { get; set; } = new();
}

public class FailurePattern
{
    public string WorkflowType { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime RecordedAt { get; set; }
}

public class AIAnalysisResponse
{
    public string PageType { get; set; } = "";
    public double Confidence { get; set; }
    public List<AIStepResponse>? SuggestedSteps { get; set; }
}

public class AIStepResponse
{
    public int Order { get; set; }
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string? SelectorHint { get; set; }
    public string? InputVariable { get; set; }
    public bool IsOptional { get; set; }
    public double Confidence { get; set; }
}

#endregion
