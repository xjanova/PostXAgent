using AIManager.Core.WebAutomation.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// AIGuidedTeacher - ระบบที่ AI เป็นผู้นำทาง ให้มนุษย์สอนตาม Step-by-Step
/// AI จะวิเคราะห์หน้าเว็บและสร้าง Guidelines ที่ชัดเจนให้มนุษย์ทำตาม
/// </summary>
public class AIGuidedTeacher
{
    private readonly ILogger<AIGuidedTeacher> _logger;
    private readonly ContentGeneratorService _aiService;
    private readonly AIElementAnalyzer _elementAnalyzer;

    // Platform-specific patterns สำหรับสร้าง guidelines
    private readonly Dictionary<string, TeachingPlatformPattern> _platformPatterns;

    public AIGuidedTeacher(
        ILogger<AIGuidedTeacher> logger,
        ContentGeneratorService aiService,
        AIElementAnalyzer elementAnalyzer)
    {
        _logger = logger;
        _aiService = aiService;
        _elementAnalyzer = elementAnalyzer;
        _platformPatterns = InitializePlatformPatterns();
    }

    #region Generate Teaching Guidelines

    /// <summary>
    /// สร้าง Step-by-Step Guidelines สำหรับการสอน workflow
    /// </summary>
    public async Task<TeachingGuideline> GenerateTeachingGuidelineAsync(
        string platform,
        string workflowType,
        string pageHtml,
        string currentUrl,
        string? screenshot = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Generating teaching guidelines for {Platform}/{Type}",
            platform, workflowType);

        var guideline = new TeachingGuideline
        {
            Platform = platform,
            WorkflowType = workflowType,
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            // 1. วิเคราะห์หน้าเว็บปัจจุบัน
            var pageAnalysis = await AnalyzePageForTeachingAsync(pageHtml, platform, ct);
            guideline.PageAnalysis = pageAnalysis;

            // 2. ตรวจสอบว่าอยู่หน้าที่ถูกต้องหรือไม่
            var pageValidation = ValidateCurrentPage(currentUrl, platform, workflowType);
            guideline.IsCorrectPage = pageValidation.IsCorrect;
            guideline.PageValidationMessage = pageValidation.Message;

            // 3. ถ้าหน้าไม่ถูก แนะนำให้ไปหน้าที่ถูกก่อน
            if (!pageValidation.IsCorrect)
            {
                guideline.PrerequisiteSteps = GenerateNavigationSteps(platform, workflowType);
                guideline.Steps = new List<TeachingStep>();
                return guideline;
            }

            // 4. สร้าง Teaching Steps จาก AI Analysis + Platform Patterns
            guideline.Steps = await GenerateTeachingStepsAsync(
                platform, workflowType, pageAnalysis, pageHtml, ct);

            // 5. เพิ่ม Tips และ Warnings
            guideline.Tips = GenerateTips(platform, workflowType);
            guideline.Warnings = GenerateWarnings(platform, workflowType, pageAnalysis);

            // 6. คำนวณ estimated time
            guideline.EstimatedTimeSeconds = guideline.Steps.Count * 3; // ~3 วินาทีต่อ step

            guideline.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate teaching guidelines");
            guideline.Success = false;
            guideline.Error = ex.Message;
        }

        return guideline;
    }

    /// <summary>
    /// สร้าง Quick Guidelines โดยไม่ต้องใช้ AI (เร็วกว่า)
    /// </summary>
    public TeachingGuideline GenerateQuickGuideline(
        string platform,
        string workflowType)
    {
        _logger.LogInformation(
            "Generating quick guidelines for {Platform}/{Type}",
            platform, workflowType);

        if (!_platformPatterns.TryGetValue(platform.ToLower(), out var pattern))
        {
            pattern = _platformPatterns["default"];
        }

        if (!pattern.WorkflowSteps.TryGetValue(workflowType.ToLower(), out var steps))
        {
            steps = pattern.WorkflowSteps.GetValueOrDefault("post") ?? new List<TeachingPatternStep>();
        }

        return new TeachingGuideline
        {
            Platform = platform,
            WorkflowType = workflowType,
            GeneratedAt = DateTime.UtcNow,
            Success = true,
            IsCorrectPage = true, // Assume correct for quick mode
            Steps = steps.Select((s, i) => new TeachingStep
            {
                StepNumber = i + 1,
                Action = s.Action,
                Description = s.Description,
                DescriptionThai = s.DescriptionThai,
                TargetElement = s.TargetElement,
                ElementHint = s.ElementHint,
                InputHint = s.InputHint,
                WaitAfterMs = s.WaitAfterMs,
                IsOptional = s.IsOptional,
                ValidationHint = s.ValidationHint
            }).ToList(),
            Tips = GenerateTips(platform, workflowType),
            EstimatedTimeSeconds = steps.Count * 3
        };
    }

    #endregion

    #region Validate Teaching Progress

    /// <summary>
    /// ตรวจสอบว่า step ที่มนุษย์ทำถูกต้องตาม guideline หรือไม่
    /// </summary>
    public StepValidationResult ValidateStep(
        TeachingStep expectedStep,
        RecordedStep actualStep)
    {
        var result = new StepValidationResult
        {
            StepNumber = expectedStep.StepNumber,
            ExpectedAction = expectedStep.Action,
            ActualAction = actualStep.Action ?? "unknown"
        };

        // 1. ตรวจสอบ action type
        if (!IsActionMatch(expectedStep.Action, actualStep.Action))
        {
            result.IsCorrect = false;
            result.Feedback = $"คาดหวัง '{expectedStep.Action}' แต่ได้ '{actualStep.Action}'";
            result.FeedbackThai = $"ควรทำ {GetActionNameThai(expectedStep.Action)} แต่ทำ {GetActionNameThai(actualStep.Action)}";
            result.Suggestion = $"ลองทำใหม่: {expectedStep.DescriptionThai}";
            return result;
        }

        // 2. ตรวจสอบ element ที่ click/type (ถ้ามี hint)
        if (!string.IsNullOrEmpty(expectedStep.ElementHint))
        {
            var elementMatch = IsElementMatch(expectedStep, actualStep);
            if (!elementMatch.IsMatch)
            {
                result.IsCorrect = false;
                result.Feedback = elementMatch.Message;
                result.FeedbackThai = elementMatch.MessageThai;
                result.Suggestion = $"ลองหา element ที่มี: {expectedStep.ElementHint}";
                result.ConfidenceScore = elementMatch.Confidence;
                return result;
            }
        }

        // 3. ตรวจสอบ input value (สำหรับ type action)
        if (expectedStep.Action.Equals("type", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(actualStep.Value))
            {
                result.IsCorrect = false;
                result.Feedback = "No text was typed";
                result.FeedbackThai = "ยังไม่ได้พิมพ์ข้อความ";
                result.Suggestion = expectedStep.InputHint ?? "กรุณาพิมพ์ข้อความ";
                return result;
            }
        }

        // 4. ผ่านทุกการตรวจสอบ
        result.IsCorrect = true;
        result.Feedback = "Step completed correctly!";
        result.FeedbackThai = "ทำถูกต้อง!";
        result.ConfidenceScore = 1.0;

        return result;
    }

    /// <summary>
    /// ตรวจสอบความคืบหน้าทั้งหมด
    /// </summary>
    public TeachingProgressReport GetProgressReport(
        TeachingGuideline guideline,
        List<RecordedStep> completedSteps)
    {
        var report = new TeachingProgressReport
        {
            TotalSteps = guideline.Steps.Count,
            CompletedSteps = 0,
            CurrentStep = 1,
            StepResults = new List<StepValidationResult>()
        };

        for (int i = 0; i < Math.Min(completedSteps.Count, guideline.Steps.Count); i++)
        {
            var validation = ValidateStep(guideline.Steps[i], completedSteps[i]);
            report.StepResults.Add(validation);

            if (validation.IsCorrect)
            {
                report.CompletedSteps++;
            }
        }

        report.CurrentStep = report.CompletedSteps + 1;
        report.ProgressPercentage = (double)report.CompletedSteps / report.TotalSteps * 100;

        // Get next step info
        if (report.CurrentStep <= guideline.Steps.Count)
        {
            var nextStep = guideline.Steps[report.CurrentStep - 1];
            report.NextStepDescription = nextStep.Description;
            report.NextStepDescriptionThai = nextStep.DescriptionThai;
            report.NextStepHint = nextStep.ElementHint;
        }
        else
        {
            report.IsComplete = true;
            report.NextStepDescription = "All steps completed!";
            report.NextStepDescriptionThai = "ทำครบทุกขั้นตอนแล้ว!";
        }

        // Calculate overall confidence
        if (report.StepResults.Any())
        {
            report.OverallConfidence = report.StepResults.Average(r => r.ConfidenceScore);
        }

        return report;
    }

    #endregion

    #region Private Methods - Analysis

    private async Task<PageTeachingAnalysis> AnalyzePageForTeachingAsync(
        string pageHtml,
        string platform,
        CancellationToken ct)
    {
        var analysis = new PageTeachingAnalysis();

        try
        {
            // ใช้ AI วิเคราะห์หน้า
            var prompt = $@"Analyze this {platform} webpage HTML and identify interactive elements for a workflow.

Return JSON:
{{
  ""pageType"": ""login|compose|feed|profile|settings|upload|unknown"",
  ""mainActions"": [
    {{
      ""action"": ""click|type|upload|select"",
      ""elementDescription"": ""description of element"",
      ""purpose"": ""what this action does"",
      ""selectorHint"": ""CSS selector or identifier hint"",
      ""isRequired"": true/false
    }}
  ],
  ""inputFields"": [
    {{
      ""name"": ""field name"",
      ""type"": ""text|password|textarea|file"",
      ""placeholder"": ""placeholder text if any"",
      ""isRequired"": true/false
    }}
  ],
  ""buttons"": [
    {{
      ""text"": ""button text"",
      ""type"": ""submit|action|navigation"",
      ""selectorHint"": ""selector hint""
    }}
  ]
}}

HTML (first 5000 chars):
{pageHtml.Substring(0, Math.Min(5000, pageHtml.Length))}";

            var result = await _aiService.GenerateAsync(prompt, null, "analysis", "en", ct);
            var parsed = ParsePageAnalysis(result.Text);

            analysis.PageType = parsed.PageType;
            analysis.MainActions = parsed.MainActions;
            analysis.InputFields = parsed.InputFields;
            analysis.Buttons = parsed.Buttons;
            analysis.AnalyzedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis failed, using basic analysis");
            analysis = PerformBasicPageAnalysis(pageHtml);
        }

        return analysis;
    }

    private PageTeachingAnalysis ParsePageAnalysis(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonConvert.DeserializeObject<PageTeachingAnalysis>(json)
                    ?? new PageTeachingAnalysis();
            }
        }
        catch { }

        return new PageTeachingAnalysis();
    }

    private PageTeachingAnalysis PerformBasicPageAnalysis(string pageHtml)
    {
        var analysis = new PageTeachingAnalysis
        {
            AnalyzedAt = DateTime.UtcNow
        };

        // Detect page type
        var htmlLower = pageHtml.ToLower();
        if (htmlLower.Contains("login") || htmlLower.Contains("password"))
            analysis.PageType = "login";
        else if (htmlLower.Contains("compose") || htmlLower.Contains("create post") || htmlLower.Contains("what's on your mind"))
            analysis.PageType = "compose";
        else if (htmlLower.Contains("feed") || htmlLower.Contains("timeline"))
            analysis.PageType = "feed";
        else
            analysis.PageType = "unknown";

        return analysis;
    }

    private (bool IsCorrect, string Message) ValidateCurrentPage(
        string currentUrl,
        string platform,
        string workflowType)
    {
        var urlLower = currentUrl.ToLower();
        var platformLower = platform.ToLower();

        // ตรวจสอบว่า URL ตรงกับ platform
        var platformUrls = new Dictionary<string, string[]>
        {
            ["facebook"] = new[] { "facebook.com", "fb.com" },
            ["instagram"] = new[] { "instagram.com" },
            ["tiktok"] = new[] { "tiktok.com" },
            ["twitter"] = new[] { "twitter.com", "x.com" },
            ["youtube"] = new[] { "youtube.com", "youtu.be" },
            ["linkedin"] = new[] { "linkedin.com" },
            ["threads"] = new[] { "threads.net" },
            ["pinterest"] = new[] { "pinterest.com" },
            ["line"] = new[] { "line.me" },
            // AI Generation Platforms
            ["freepik"] = new[] { "freepik.com" },
            ["suno"] = new[] { "suno.com", "suno.ai" }
        };

        if (platformUrls.TryGetValue(platformLower, out var urls))
        {
            if (!urls.Any(u => urlLower.Contains(u)))
            {
                return (false, $"กรุณาไปที่เว็บไซต์ {platform} ก่อน");
            }
        }

        // ตรวจสอบว่าอยู่หน้าที่เหมาะสมกับ workflow type
        var workflowTypeLower = workflowType.ToLower();

        if (workflowTypeLower == "post" || workflowTypeLower == "compose")
        {
            // ควรอยู่หน้า compose หรือ home ที่มี composer
            if (urlLower.Contains("login") || urlLower.Contains("signin"))
            {
                return (false, "ดูเหมือนว่าคุณยังไม่ได้ login กรุณา login ก่อน");
            }
        }

        if (workflowTypeLower == "login")
        {
            if (!urlLower.Contains("login") && !urlLower.Contains("signin"))
            {
                return (false, "กรุณาไปที่หน้า Login ก่อน");
            }
        }

        return (true, "อยู่หน้าที่ถูกต้องแล้ว");
    }

    private List<TeachingStep> GenerateNavigationSteps(string platform, string workflowType)
    {
        var steps = new List<TeachingStep>();

        // สร้าง steps เพื่อไปหน้าที่ถูกต้อง
        if (_platformPatterns.TryGetValue(platform.ToLower(), out var pattern))
        {
            var startUrl = workflowType.ToLower() switch
            {
                "login" => pattern.LoginUrl,
                "post" => pattern.ComposeUrl ?? pattern.HomeUrl,
                "upload" => pattern.UploadUrl ?? pattern.ComposeUrl ?? pattern.HomeUrl,
                _ => pattern.HomeUrl
            };

            steps.Add(new TeachingStep
            {
                StepNumber = 0,
                Action = "navigate",
                Description = $"Go to {platform} {workflowType} page",
                DescriptionThai = $"ไปที่หน้า {workflowType} ของ {platform}",
                TargetElement = startUrl,
                IsPrerequisite = true
            });
        }

        return steps;
    }

    private async Task<List<TeachingStep>> GenerateTeachingStepsAsync(
        string platform,
        string workflowType,
        PageTeachingAnalysis pageAnalysis,
        string pageHtml,
        CancellationToken ct)
    {
        var steps = new List<TeachingStep>();

        // 1. ใช้ Platform Pattern เป็น base
        if (_platformPatterns.TryGetValue(platform.ToLower(), out var pattern) &&
            pattern.WorkflowSteps.TryGetValue(workflowType.ToLower(), out var patternSteps))
        {
            int stepNum = 1;
            foreach (var ps in patternSteps)
            {
                var step = new TeachingStep
                {
                    StepNumber = stepNum++,
                    Action = ps.Action,
                    Description = ps.Description,
                    DescriptionThai = ps.DescriptionThai,
                    TargetElement = ps.TargetElement,
                    ElementHint = ps.ElementHint,
                    InputHint = ps.InputHint,
                    WaitAfterMs = ps.WaitAfterMs,
                    IsOptional = ps.IsOptional,
                    ValidationHint = ps.ValidationHint
                };

                // 2. Enhance with AI analysis ถ้ามี
                if (pageAnalysis.MainActions?.Any() == true)
                {
                    var matchingAction = pageAnalysis.MainActions
                        .FirstOrDefault(a => a.Purpose?.Contains(ps.Description, StringComparison.OrdinalIgnoreCase) == true);

                    if (matchingAction != null)
                    {
                        step.AIEnhancedHint = matchingAction.SelectorHint;
                        step.AIConfidence = 0.8;
                    }
                }

                steps.Add(step);
            }
        }
        else
        {
            // 3. Fallback: ใช้ AI สร้าง steps ใหม่ทั้งหมด
            steps = await GenerateStepsFromAIAsync(platform, workflowType, pageAnalysis, ct);
        }

        return steps;
    }

    private async Task<List<TeachingStep>> GenerateStepsFromAIAsync(
        string platform,
        string workflowType,
        PageTeachingAnalysis pageAnalysis,
        CancellationToken ct)
    {
        var prompt = $@"Create step-by-step teaching instructions for performing '{workflowType}' workflow on {platform}.

Current page analysis:
- Page type: {pageAnalysis.PageType}
- Available buttons: {string.Join(", ", pageAnalysis.Buttons?.Select(b => b.Text) ?? Array.Empty<string>())}
- Input fields: {string.Join(", ", pageAnalysis.InputFields?.Select(f => f.Name) ?? Array.Empty<string>())}

Return JSON array:
[
  {{
    ""stepNumber"": 1,
    ""action"": ""click|type|upload|select|wait"",
    ""description"": ""What to do in English"",
    ""descriptionThai"": ""คำอธิบายภาษาไทย"",
    ""elementHint"": ""How to find the element"",
    ""inputHint"": ""What to type (if type action)"",
    ""isOptional"": false,
    ""waitAfterMs"": 500
  }}
]";

        try
        {
            var result = await _aiService.GenerateAsync(prompt, null, "teaching", "en", ct);
            return ParseTeachingSteps(result.Text);
        }
        catch
        {
            return new List<TeachingStep>();
        }
    }

    private List<TeachingStep> ParseTeachingSteps(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonConvert.DeserializeObject<List<TeachingStep>>(json)
                    ?? new List<TeachingStep>();
            }
        }
        catch { }

        return new List<TeachingStep>();
    }

    #endregion

    #region Private Methods - Validation

    private bool IsActionMatch(string expected, string? actual)
    {
        if (string.IsNullOrEmpty(actual)) return false;

        expected = expected.ToLower();
        actual = actual.ToLower();

        // Direct match
        if (expected == actual) return true;

        // Synonyms
        var synonyms = new Dictionary<string, string[]>
        {
            ["click"] = new[] { "click", "tap", "press" },
            ["type"] = new[] { "type", "input", "enter", "write" },
            ["upload"] = new[] { "upload", "attach", "file" },
            ["select"] = new[] { "select", "choose", "pick" },
            ["submit"] = new[] { "submit", "click", "post", "send" }
        };

        if (synonyms.TryGetValue(expected, out var syns))
        {
            return syns.Contains(actual);
        }

        return false;
    }

    private (bool IsMatch, double Confidence, string Message, string MessageThai) IsElementMatch(
        TeachingStep expected,
        RecordedStep actual)
    {
        if (actual.Element == null)
        {
            return (false, 0, "No element found", "ไม่พบ element");
        }

        double confidence = 0.5;
        var hints = expected.ElementHint?.ToLower() ?? "";

        // Check various element properties
        if (!string.IsNullOrEmpty(actual.Element.Id))
        {
            if (hints.Contains(actual.Element.Id.ToLower()))
            {
                confidence = 0.95;
            }
        }

        if (!string.IsNullOrEmpty(actual.Element.Placeholder))
        {
            if (hints.Contains(actual.Element.Placeholder.ToLower()))
            {
                confidence = 0.9;
            }
        }

        if (!string.IsNullOrEmpty(actual.Element.TextContent))
        {
            var text = actual.Element.TextContent.ToLower();
            if (hints.Split(' ').Any(h => text.Contains(h)))
            {
                confidence = Math.Max(confidence, 0.7);
            }
        }

        if (actual.Element.Attributes != null)
        {
            var ariaLabel = actual.Element.Attributes.GetValueOrDefault("aria-label")?.ToLower();
            if (!string.IsNullOrEmpty(ariaLabel) && hints.Contains(ariaLabel))
            {
                confidence = 0.9;
            }
        }

        if (confidence >= 0.6)
        {
            return (true, confidence, "Element matches", "ตรงกัน");
        }

        return (false, confidence,
            $"Element may not match. Expected: {expected.ElementHint}",
            $"อาจไม่ใช่ element ที่ต้องการ ควรหา: {expected.ElementHint}");
    }

    private string GetActionNameThai(string action)
    {
        return action?.ToLower() switch
        {
            "click" => "คลิก",
            "type" => "พิมพ์",
            "upload" => "อัพโหลด",
            "select" => "เลือก",
            "submit" => "ส่ง",
            "wait" => "รอ",
            "navigate" => "ไปที่",
            _ => action ?? "ไม่ทราบ"
        };
    }

    #endregion

    #region Private Methods - Tips & Warnings

    private List<string> GenerateTips(string platform, string workflowType)
    {
        var tips = new List<string>
        {
            "รอให้หน้าโหลดเสร็จก่อนทำแต่ละ step",
            "ถ้า element หาไม่เจอ ลอง scroll หน้าจอดู"
        };

        if (workflowType.Equals("post", StringComparison.OrdinalIgnoreCase))
        {
            tips.Add("ควรเตรียมเนื้อหาและรูปภาพไว้ก่อน");
            tips.Add("ตรวจสอบให้แน่ใจว่า login แล้ว");
        }

        if (workflowType.Equals("login", StringComparison.OrdinalIgnoreCase))
        {
            tips.Add("เตรียม username และ password ไว้");
            tips.Add("ระวังการพิมพ์ผิด");
        }

        return tips;
    }

    private List<string> GenerateWarnings(
        string platform,
        string workflowType,
        PageTeachingAnalysis analysis)
    {
        var warnings = new List<string>();

        if (analysis.PageType == "unknown")
        {
            warnings.Add("ไม่แน่ใจว่าอยู่หน้าที่ถูกต้อง กรุณาตรวจสอบ");
        }

        if (workflowType.Equals("post", StringComparison.OrdinalIgnoreCase) &&
            analysis.PageType == "login")
        {
            warnings.Add("ดูเหมือนว่าอยู่หน้า login กรุณา login ก่อนทำ post");
        }

        return warnings;
    }

    #endregion

    #region Platform Patterns

    private Dictionary<string, TeachingPlatformPattern> InitializePlatformPatterns()
    {
        return new Dictionary<string, TeachingPlatformPattern>
        {
            ["facebook"] = new TeachingPlatformPattern
            {
                Name = "Facebook",
                HomeUrl = "https://www.facebook.com",
                LoginUrl = "https://www.facebook.com/login",
                ComposeUrl = "https://www.facebook.com",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["post"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on 'What's on your mind?' input",
                            DescriptionThai = "คลิกที่ช่อง 'คุณกำลังคิดอะไรอยู่'",
                            ElementHint = "What's on your mind, composer, create post",
                            TargetElement = "[aria-label*='Create a post'], [data-testid='create-post']",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Type your post content",
                            DescriptionThai = "พิมพ์เนื้อหาโพสต์",
                            ElementHint = "contenteditable, post composer, textarea",
                            InputHint = "พิมพ์ข้อความที่ต้องการโพสต์",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "upload",
                            Description = "Add photo/video (optional)",
                            DescriptionThai = "เพิ่มรูปภาพ/วิดีโอ (ถ้าต้องการ)",
                            ElementHint = "Photo/Video, add media, attachment",
                            IsOptional = true,
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Post' button",
                            DescriptionThai = "คลิกปุ่ม 'โพสต์'",
                            ElementHint = "Post button, submit, publish",
                            TargetElement = "[data-testid='react-composer-post-button'], button[type='submit']",
                            ValidationHint = "ตรวจสอบว่าปุ่มโพสต์สีเข้มและกดได้",
                            WaitAfterMs = 3000
                        }
                    },
                    ["login"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "type",
                            Description = "Enter email or phone number",
                            DescriptionThai = "กรอกอีเมลหรือเบอร์โทรศัพท์",
                            ElementHint = "email, phone, username",
                            TargetElement = "#email, input[name='email']",
                            InputHint = "{{credentials.username}}",
                            WaitAfterMs = 300
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Enter password",
                            DescriptionThai = "กรอกรหัสผ่าน",
                            ElementHint = "password",
                            TargetElement = "#pass, input[name='pass']",
                            InputHint = "{{credentials.password}}",
                            WaitAfterMs = 300
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Log In' button",
                            DescriptionThai = "คลิกปุ่ม 'เข้าสู่ระบบ'",
                            ElementHint = "Log In, Login, Sign In",
                            TargetElement = "button[name='login'], input[type='submit']",
                            WaitAfterMs = 3000
                        }
                    }
                }
            },

            ["instagram"] = new TeachingPlatformPattern
            {
                Name = "Instagram",
                HomeUrl = "https://www.instagram.com",
                LoginUrl = "https://www.instagram.com/accounts/login",
                ComposeUrl = "https://www.instagram.com",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["post"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Create' or '+' button",
                            DescriptionThai = "คลิกปุ่ม 'สร้าง' หรือ '+'",
                            ElementHint = "Create, New Post, plus icon",
                            TargetElement = "[aria-label='New post'], svg[aria-label='New post']",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "upload",
                            Description = "Select photo or video from computer",
                            DescriptionThai = "เลือกรูปภาพหรือวิดีโอจากเครื่อง",
                            ElementHint = "Select from computer, file input",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Next' to proceed",
                            DescriptionThai = "คลิก 'ถัดไป' เพื่อดำเนินการต่อ",
                            ElementHint = "Next",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Write a caption",
                            DescriptionThai = "เขียนคำบรรยาย",
                            ElementHint = "Write a caption, textarea",
                            InputHint = "พิมพ์คำบรรยายรูปภาพ",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Share' to post",
                            DescriptionThai = "คลิก 'แชร์' เพื่อโพสต์",
                            ElementHint = "Share",
                            WaitAfterMs = 3000
                        }
                    }
                }
            },

            ["tiktok"] = new TeachingPlatformPattern
            {
                Name = "TikTok",
                HomeUrl = "https://www.tiktok.com",
                LoginUrl = "https://www.tiktok.com/login",
                UploadUrl = "https://www.tiktok.com/upload",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["post"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click Upload button",
                            DescriptionThai = "คลิกปุ่ม Upload",
                            ElementHint = "Upload, Create",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "upload",
                            Description = "Select video file",
                            DescriptionThai = "เลือกไฟล์วิดีโอ",
                            ElementHint = "Select file, file input",
                            WaitAfterMs = 5000
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Add caption with hashtags",
                            DescriptionThai = "เพิ่มคำบรรยายพร้อม hashtags",
                            ElementHint = "caption, description, editable",
                            InputHint = "พิมพ์คำบรรยายและ #hashtags",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Post' button",
                            DescriptionThai = "คลิกปุ่ม 'Post'",
                            ElementHint = "Post, Publish",
                            WaitAfterMs = 5000
                        }
                    }
                }
            },

            ["twitter"] = new TeachingPlatformPattern
            {
                Name = "Twitter/X",
                HomeUrl = "https://twitter.com",
                LoginUrl = "https://twitter.com/login",
                ComposeUrl = "https://twitter.com/compose/tweet",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["post"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click compose tweet button",
                            DescriptionThai = "คลิกปุ่มเขียนทวีต",
                            ElementHint = "Tweet, compose, What's happening",
                            TargetElement = "[data-testid='tweetButtonInline'], [aria-label='Post']",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Type your tweet",
                            DescriptionThai = "พิมพ์ข้อความทวีต",
                            ElementHint = "What's happening, contenteditable",
                            InputHint = "พิมพ์ข้อความทวีต (ไม่เกิน 280 ตัวอักษร)",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "upload",
                            Description = "Add media (optional)",
                            DescriptionThai = "เพิ่มรูปภาพ/วิดีโอ (ถ้าต้องการ)",
                            ElementHint = "Add media, image, GIF",
                            IsOptional = true,
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Post' button",
                            DescriptionThai = "คลิกปุ่ม 'Post'",
                            ElementHint = "Post, Tweet",
                            TargetElement = "[data-testid='tweetButton']",
                            WaitAfterMs = 3000
                        }
                    }
                }
            },

            // ============================================
            // FREEPIK - AI Image & Video Generation
            // ============================================
            ["freepik"] = new TeachingPlatformPattern
            {
                Name = "Freepik",
                HomeUrl = "https://www.freepik.com",
                LoginUrl = "https://www.freepik.com/log-in",
                ComposeUrl = "https://www.freepik.com/pikaso/ai-image-generator",
                UploadUrl = "https://www.freepik.com/pikaso/ai-video",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["login"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "navigate",
                            Description = "Go to Freepik login page",
                            DescriptionThai = "ไปที่หน้า Login ของ Freepik",
                            TargetElement = "https://www.freepik.com/log-in",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Continue with Google' or email login",
                            DescriptionThai = "คลิก 'Continue with Google' หรือ login ด้วยอีเมล",
                            ElementHint = "Continue with Google, Google, Sign in with Google, email",
                            TargetElement = "button[data-cy='google-login'], .google-login-button",
                            WaitAfterMs = 3000
                        },
                        new()
                        {
                            Action = "wait",
                            Description = "Complete Google authentication in popup",
                            DescriptionThai = "เข้าสู่ระบบ Google ใน popup ที่เปิดขึ้นมา",
                            ElementHint = "รอจนกว่าจะ login สำเร็จและกลับมาหน้า Freepik",
                            WaitAfterMs = 10000
                        }
                    },
                    ["generate_image"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "navigate",
                            Description = "Go to Freepik AI Image Generator (Pikaso)",
                            DescriptionThai = "ไปที่หน้า AI Image Generator ของ Freepik",
                            TargetElement = "https://www.freepik.com/pikaso/ai-image-generator",
                            WaitAfterMs = 3000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Select an UNLIMITED model (Flux, Mystic, etc.) - CHECK FOR 'Unlimited' BADGE",
                            DescriptionThai = "เลือกโมเดลที่มีป้าย 'Unlimited' (ฟรี) เช่น Flux, Mystic - ห้ามเลือกโมเดลที่เสีย credits!",
                            ElementHint = "Unlimited, Flux, Mystic, model selector, style selector",
                            TargetElement = "[data-model='flux'], .model-unlimited, .style-card:has(.unlimited-badge)",
                            ValidationHint = "⚠️ ต้องเลือกเฉพาะโมเดลที่มีป้าย 'Unlimited' เท่านั้น!",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Enter image generation prompt",
                            DescriptionThai = "พิมพ์ prompt สำหรับสร้างรูป",
                            ElementHint = "prompt, describe, textarea, input",
                            TargetElement = "textarea[placeholder*='Describe'], input[placeholder*='prompt'], .prompt-input",
                            InputHint = "{{content.prompt}} - อธิบายรูปที่ต้องการสร้าง",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Generate' button",
                            DescriptionThai = "คลิกปุ่ม 'Generate' เพื่อสร้างรูป",
                            ElementHint = "Generate, Create, สร้าง",
                            TargetElement = "button[data-cy='generate'], .generate-button, button:contains('Generate')",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "wait",
                            Description = "Wait for image generation to complete (may take 10-30 seconds)",
                            DescriptionThai = "รอการสร้างรูปเสร็จ (อาจใช้เวลา 10-30 วินาที)",
                            ElementHint = "รอจนเห็นรูปที่สร้างเสร็จ หรือเห็นปุ่ม Download",
                            ValidationHint = "ถ้าเห็น loading spinner ให้รอต่อ",
                            WaitAfterMs = 30000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click on generated image to select",
                            DescriptionThai = "คลิกที่รูปที่สร้างเสร็จเพื่อเลือก",
                            ElementHint = "generated image, result image, thumbnail",
                            TargetElement = ".generated-image, .result-image, .image-result",
                            WaitAfterMs = 500
                        }
                    },
                    ["download_image"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Download' button",
                            DescriptionThai = "คลิกปุ่ม 'Download' เพื่อดาวน์โหลดรูป",
                            ElementHint = "Download, ดาวน์โหลด, save",
                            TargetElement = "button[data-cy='download'], .download-button, a[download]",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Select download format (JPG recommended)",
                            DescriptionThai = "เลือกรูปแบบไฟล์ (แนะนำ JPG หรือ PNG)",
                            ElementHint = "JPG, PNG, format",
                            IsOptional = true,
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Confirm download",
                            DescriptionThai = "ยืนยันการดาวน์โหลด",
                            ElementHint = "Download, Confirm, ดาวน์โหลด",
                            WaitAfterMs = 3000
                        }
                    },
                    ["image_to_video"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "navigate",
                            Description = "Go to Freepik AI Video Generator (Pikaso Video)",
                            DescriptionThai = "ไปที่หน้า AI Video Generator ของ Freepik",
                            TargetElement = "https://www.freepik.com/pikaso/ai-video",
                            WaitAfterMs = 3000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Image to Video' option",
                            DescriptionThai = "เลือกตัวเลือก 'Image to Video' หรือ 'Upload Image'",
                            ElementHint = "Image to Video, Upload, อัพโหลดรูป",
                            TargetElement = "[data-tab='image-to-video'], .image-to-video-tab",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "upload",
                            Description = "Upload the generated image",
                            DescriptionThai = "อัพโหลดรูปที่สร้างมา",
                            ElementHint = "upload, file input, drop zone, browse",
                            TargetElement = "input[type='file'], .upload-zone, .dropzone",
                            InputHint = "{{content.imagePath}} - เลือกไฟล์รูปที่ดาวน์โหลดมา",
                            WaitAfterMs = 3000
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Enter motion/animation prompt (optional)",
                            DescriptionThai = "พิมพ์ prompt สำหรับการเคลื่อนไหว (ไม่บังคับ)",
                            ElementHint = "motion, prompt, describe movement",
                            InputHint = "{{content.motionPrompt}} - อธิบายการเคลื่อนไหวที่ต้องการ",
                            IsOptional = true,
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Select video style/duration",
                            DescriptionThai = "เลือก style และความยาววิดีโอ",
                            ElementHint = "style, duration, 5s, 10s",
                            IsOptional = true,
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Generate Video' button",
                            DescriptionThai = "คลิกปุ่ม 'Generate Video' เพื่อสร้างวิดีโอ",
                            ElementHint = "Generate, Create Video, สร้างวิดีโอ",
                            TargetElement = "button[data-cy='generate-video'], .generate-video-button",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "wait",
                            Description = "Wait for video generation to complete (may take 2-5 minutes)",
                            DescriptionThai = "รอการสร้างวิดีโอเสร็จ (อาจใช้เวลา 2-5 นาที)",
                            ElementHint = "รอจนเห็นวิดีโอที่สร้างเสร็จ หรือเห็นปุ่ม Download",
                            ValidationHint = "วิดีโอใช้เวลานานกว่ารูป อดทนรอ!",
                            WaitAfterMs = 300000 // 5 minutes max
                        }
                    },
                    ["download_video"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on generated video to select",
                            DescriptionThai = "คลิกที่วิดีโอที่สร้างเสร็จเพื่อเลือก",
                            ElementHint = "video, result, thumbnail",
                            TargetElement = ".generated-video, .video-result, video",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Download' button",
                            DescriptionThai = "คลิกปุ่ม 'Download' เพื่อดาวน์โหลดวิดีโอ",
                            ElementHint = "Download, ดาวน์โหลด, save video",
                            TargetElement = "button[data-cy='download-video'], .download-button, a[download]",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Confirm download (MP4 format)",
                            DescriptionThai = "ยืนยันการดาวน์โหลด (รูปแบบ MP4)",
                            ElementHint = "MP4, Download, Confirm",
                            WaitAfterMs = 5000
                        }
                    },
                    ["check_credits"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on profile/account icon",
                            DescriptionThai = "คลิกที่ไอคอนโปรไฟล์หรือบัญชี",
                            ElementHint = "profile, account, avatar, user",
                            TargetElement = ".user-avatar, .profile-icon, [data-cy='user-menu']",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "extract",
                            Description = "Extract remaining credits information",
                            DescriptionThai = "ดึงข้อมูลจำนวน credits ที่เหลือ",
                            ElementHint = "credits, remaining, จำนวนที่เหลือ",
                            TargetElement = ".credits-count, .remaining-credits, [data-credits]",
                            WaitAfterMs = 500
                        }
                    }
                }
            },

            // ============================================
            // SUNO - AI Music Generation
            // ============================================
            ["suno"] = new TeachingPlatformPattern
            {
                Name = "Suno AI",
                HomeUrl = "https://suno.com",
                LoginUrl = "https://suno.com",
                ComposeUrl = "https://suno.com/create",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["login"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "navigate",
                            Description = "Go to Suno homepage",
                            DescriptionThai = "ไปที่หน้าหลักของ Suno",
                            TargetElement = "https://suno.com",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Sign In' or 'Make a song' button",
                            DescriptionThai = "คลิกปุ่ม 'Sign In' หรือ 'Make a song'",
                            ElementHint = "Sign In, Login, Make a song, Create",
                            TargetElement = "button:contains('Sign In'), a[href*='login'], .login-button",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Select login method (Google, Discord, or Microsoft)",
                            DescriptionThai = "เลือกวิธี login (Google, Discord, หรือ Microsoft)",
                            ElementHint = "Google, Discord, Microsoft, Continue with",
                            TargetElement = "button[data-provider='google'], .google-login, button:contains('Google')",
                            WaitAfterMs = 3000
                        },
                        new()
                        {
                            Action = "wait",
                            Description = "Complete authentication in popup",
                            DescriptionThai = "เข้าสู่ระบบใน popup ที่เปิดขึ้นมา",
                            ElementHint = "รอจนกว่าจะ login สำเร็จและกลับมาหน้า Suno",
                            WaitAfterMs = 10000
                        }
                    },
                    ["generate_music"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "navigate",
                            Description = "Go to Suno Create page",
                            DescriptionThai = "ไปที่หน้า Create ของ Suno",
                            TargetElement = "https://suno.com/create",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Create' or 'Make a song' button",
                            DescriptionThai = "คลิกปุ่ม 'Create' หรือ 'Make a song'",
                            ElementHint = "Create, Make a song, สร้าง",
                            TargetElement = "button:contains('Create'), .create-button, [data-cy='create-song']",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Toggle 'Custom' mode for more control (optional)",
                            DescriptionThai = "เปิดโหมด 'Custom' เพื่อควบคุมเพิ่มเติม (ไม่บังคับ)",
                            ElementHint = "Custom, Advanced, Custom Mode",
                            TargetElement = "[data-cy='custom-toggle'], .custom-mode-toggle",
                            IsOptional = true,
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Enter song description or lyrics",
                            DescriptionThai = "พิมพ์คำอธิบายเพลงหรือเนื้อเพลง",
                            ElementHint = "lyrics, description, prompt, textarea",
                            TargetElement = "textarea[placeholder*='lyrics'], textarea[placeholder*='describe'], .lyrics-input",
                            InputHint = "{{content.musicPrompt}} - อธิบายเพลงที่ต้องการ หรือใส่เนื้อเพลง",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Enter music style/genre (if in custom mode)",
                            DescriptionThai = "ระบุ style หรือ genre ของเพลง (ถ้าเปิดโหมด Custom)",
                            ElementHint = "style, genre, style of music",
                            TargetElement = "input[placeholder*='style'], input[placeholder*='genre'], .style-input",
                            InputHint = "{{content.musicStyle}} - เช่น Pop, Rock, Jazz, Thai Traditional",
                            IsOptional = true,
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Enter song title (if in custom mode)",
                            DescriptionThai = "ตั้งชื่อเพลง (ถ้าเปิดโหมด Custom)",
                            ElementHint = "title, song name, ชื่อเพลง",
                            TargetElement = "input[placeholder*='title'], input[placeholder*='name'], .title-input",
                            InputHint = "{{content.songTitle}}",
                            IsOptional = true,
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Create' to generate music",
                            DescriptionThai = "คลิกปุ่ม 'Create' เพื่อสร้างเพลง",
                            ElementHint = "Create, Generate, สร้าง",
                            TargetElement = "button[type='submit'], button:contains('Create'), .submit-button",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "wait",
                            Description = "Wait for music generation to complete (usually 1-3 minutes)",
                            DescriptionThai = "รอการสร้างเพลงเสร็จ (ปกติใช้เวลา 1-3 นาที)",
                            ElementHint = "รอจนเห็นเพลงที่สร้างเสร็จ (Suno จะสร้าง 2 เวอร์ชัน)",
                            ValidationHint = "Suno จะสร้างเพลง 2 เวอร์ชันให้เลือก",
                            WaitAfterMs = 180000 // 3 minutes max
                        }
                    },
                    ["download_music"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on the song you want to download",
                            DescriptionThai = "คลิกที่เพลงที่ต้องการดาวน์โหลด",
                            ElementHint = "song, track, audio, เพลง",
                            TargetElement = ".song-card, .track-item, [data-song-id]",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click the three-dot menu (...) or more options",
                            DescriptionThai = "คลิกเมนู 3 จุด (...) หรือ ตัวเลือกเพิ่มเติม",
                            ElementHint = "more, options, menu, three dots, ...",
                            TargetElement = "[data-cy='song-menu'], .more-options, button[aria-label='More']",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Download' option",
                            DescriptionThai = "คลิกตัวเลือก 'Download'",
                            ElementHint = "Download, ดาวน์โหลด",
                            TargetElement = "[data-cy='download'], .download-option, button:contains('Download')",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Select download format (Audio - MP3 recommended)",
                            DescriptionThai = "เลือกรูปแบบดาวน์โหลด (แนะนำ Audio - MP3)",
                            ElementHint = "Audio, MP3, Video",
                            TargetElement = "button:contains('Audio'), [data-format='mp3']",
                            WaitAfterMs = 3000
                        }
                    },
                    ["check_credits"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on profile/account section",
                            DescriptionThai = "คลิกที่ส่วนโปรไฟล์หรือบัญชี",
                            ElementHint = "profile, account, credits, user",
                            TargetElement = ".user-menu, .profile-section, [data-cy='credits']",
                            WaitAfterMs = 1000
                        },
                        new()
                        {
                            Action = "extract",
                            Description = "Extract remaining credits",
                            DescriptionThai = "ดึงข้อมูลจำนวน credits ที่เหลือ",
                            ElementHint = "credits remaining, จำนวนที่เหลือ",
                            TargetElement = ".credits-display, [data-credits], span:contains('credits')",
                            WaitAfterMs = 500
                        }
                    },
                    ["extend_song"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on the song to extend",
                            DescriptionThai = "คลิกที่เพลงที่ต้องการต่อเพิ่ม",
                            ElementHint = "song, track",
                            TargetElement = ".song-card, .track-item",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Extend' or 'Continue' option",
                            DescriptionThai = "คลิกตัวเลือก 'Extend' หรือ 'Continue'",
                            ElementHint = "Extend, Continue, ต่อเพลง",
                            TargetElement = "[data-cy='extend'], button:contains('Extend')",
                            WaitAfterMs = 500
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click 'Create' to generate extension",
                            DescriptionThai = "คลิก 'Create' เพื่อสร้างส่วนต่อ",
                            ElementHint = "Create, Generate",
                            TargetElement = "button[type='submit'], button:contains('Create')",
                            WaitAfterMs = 2000
                        },
                        new()
                        {
                            Action = "wait",
                            Description = "Wait for extension to complete",
                            DescriptionThai = "รอการสร้างส่วนต่อเสร็จ",
                            ElementHint = "รอจนเห็นเพลงที่ต่อเสร็จ",
                            WaitAfterMs = 180000
                        }
                    }
                }
            },

            ["default"] = new TeachingPlatformPattern
            {
                Name = "Default",
                HomeUrl = "",
                WorkflowSteps = new Dictionary<string, List<TeachingPatternStep>>
                {
                    ["post"] = new List<TeachingPatternStep>
                    {
                        new()
                        {
                            Action = "click",
                            Description = "Click on compose/create button",
                            DescriptionThai = "คลิกปุ่มสร้างโพสต์",
                            ElementHint = "compose, create, new, post, write"
                        },
                        new()
                        {
                            Action = "type",
                            Description = "Type your content",
                            DescriptionThai = "พิมพ์เนื้อหา",
                            ElementHint = "textarea, contenteditable, input"
                        },
                        new()
                        {
                            Action = "click",
                            Description = "Click submit/post button",
                            DescriptionThai = "คลิกปุ่มโพสต์",
                            ElementHint = "post, submit, publish, send"
                        }
                    }
                }
            }
        };
    }

    #endregion
}

#region Models

public class TeachingGuideline
{
    public string Platform { get; set; } = "";
    public string WorkflowType { get; set; } = "";
    public DateTime GeneratedAt { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    public bool IsCorrectPage { get; set; }
    public string? PageValidationMessage { get; set; }

    public PageTeachingAnalysis? PageAnalysis { get; set; }
    public List<TeachingStep> PrerequisiteSteps { get; set; } = new();
    public List<TeachingStep> Steps { get; set; } = new();
    public List<string> Tips { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public int EstimatedTimeSeconds { get; set; }
}

public class TeachingStep
{
    [JsonProperty("stepNumber")]
    public int StepNumber { get; set; }

    [JsonProperty("action")]
    public string Action { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("descriptionThai")]
    public string DescriptionThai { get; set; } = "";

    public string? TargetElement { get; set; }

    [JsonProperty("elementHint")]
    public string? ElementHint { get; set; }

    [JsonProperty("inputHint")]
    public string? InputHint { get; set; }

    [JsonProperty("waitAfterMs")]
    public int WaitAfterMs { get; set; } = 500;

    [JsonProperty("isOptional")]
    public bool IsOptional { get; set; }

    public string? ValidationHint { get; set; }
    public string? AIEnhancedHint { get; set; }
    public double AIConfidence { get; set; }
    public bool IsPrerequisite { get; set; }
}

public class PageTeachingAnalysis
{
    [JsonProperty("pageType")]
    public string PageType { get; set; } = "unknown";

    [JsonProperty("mainActions")]
    public List<PageAction>? MainActions { get; set; }

    [JsonProperty("inputFields")]
    public List<PageInputField>? InputFields { get; set; }

    [JsonProperty("buttons")]
    public List<PageButton>? Buttons { get; set; }

    public DateTime AnalyzedAt { get; set; }
}

public class PageAction
{
    [JsonProperty("action")]
    public string Action { get; set; } = "";

    [JsonProperty("elementDescription")]
    public string ElementDescription { get; set; } = "";

    [JsonProperty("purpose")]
    public string Purpose { get; set; } = "";

    [JsonProperty("selectorHint")]
    public string? SelectorHint { get; set; }

    [JsonProperty("isRequired")]
    public bool IsRequired { get; set; }
}

public class PageInputField
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [JsonProperty("placeholder")]
    public string? Placeholder { get; set; }

    [JsonProperty("isRequired")]
    public bool IsRequired { get; set; }
}

public class PageButton
{
    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("type")]
    public string Type { get; set; } = "";

    [JsonProperty("selectorHint")]
    public string? SelectorHint { get; set; }
}

public class StepValidationResult
{
    public int StepNumber { get; set; }
    public string ExpectedAction { get; set; } = "";
    public string ActualAction { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string Feedback { get; set; } = "";
    public string FeedbackThai { get; set; } = "";
    public string? Suggestion { get; set; }
    public double ConfidenceScore { get; set; } = 1.0;
}

public class TeachingProgressReport
{
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public int CurrentStep { get; set; }
    public double ProgressPercentage { get; set; }
    public bool IsComplete { get; set; }
    public string? NextStepDescription { get; set; }
    public string? NextStepDescriptionThai { get; set; }
    public string? NextStepHint { get; set; }
    public double OverallConfidence { get; set; }
    public List<StepValidationResult> StepResults { get; set; } = new();
}

/// <summary>
/// Platform-specific teaching pattern with workflow steps
/// </summary>
public class TeachingPlatformPattern
{
    public string Name { get; set; } = "";
    public string HomeUrl { get; set; } = "";
    public string? LoginUrl { get; set; }
    public string? ComposeUrl { get; set; }
    public string? UploadUrl { get; set; }
    public Dictionary<string, List<TeachingPatternStep>> WorkflowSteps { get; set; } = new();
}

/// <summary>
/// Single step in a teaching pattern workflow
/// </summary>
public class TeachingPatternStep
{
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string DescriptionThai { get; set; } = "";
    public string? TargetElement { get; set; }
    public string? ElementHint { get; set; }
    public string? InputHint { get; set; }
    public int WaitAfterMs { get; set; } = 500;
    public bool IsOptional { get; set; }
    public string? ValidationHint { get; set; }
}

#endregion
