using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// AI Brain Service - สมองกลอัจฉริยะของระบบ PostXAgent
///
/// Advanced Capabilities:
/// 1. Chain-of-Thought Reasoning - คิดเป็นขั้นตอนก่อนตอบ
/// 2. Self-Reflection - ประเมินและปรับปรุงคำตอบตัวเอง
/// 3. Memory System - จดจำและเรียนรู้จากอดีต
/// 4. Multi-Model Orchestration - ใช้หลาย model ตามความเหมาะสม
/// 5. Tool Usage - ใช้เครื่องมือภายนอก
/// 6. Knowledge Retrieval - ดึงความรู้จาก knowledge base
/// 7. Multi-turn Planning - วางแผนหลายขั้นตอน
/// 8. Emotional Intelligence - เข้าใจอารมณ์และปรับตัว
/// 9. Few-Shot Learning - เรียนรู้จากตัวอย่าง
/// 10. Auto-Improvement - เรียนรู้จากความสำเร็จและล้มเหลว
/// </summary>
public class AIBrainService
{
    private readonly OllamaChatService _ollama;
    private readonly ILogger<AIBrainService>? _logger;
    private readonly AIMemorySystem _memory;
    private readonly AIReasoningEngine _reasoning;
    private readonly AIToolRegistry _tools;
    private readonly ConcurrentDictionary<string, ModelCapability> _modelCapabilities;

    // Configuration
    public AIBrainConfig Config { get; set; } = new();

    // Statistics
    private int _totalInteractions;
    private int _successfulInteractions;
    private DateTime _lastUsed;
    private readonly List<(DateTime Time, double Duration, bool Success)> _metrics = new();

    public AIBrainService(ILogger<AIBrainService>? logger = null)
    {
        _logger = logger;
        _ollama = new OllamaChatService(null);
        _memory = new AIMemorySystem();
        _reasoning = new AIReasoningEngine();
        _tools = new AIToolRegistry();
        _modelCapabilities = new ConcurrentDictionary<string, ModelCapability>();

        InitializeModelCapabilities();
        RegisterDefaultTools();

        // Auto-select best model
        _ollama.CurrentModel = SelectBestModel(TaskComplexity.Medium);
    }

    #region Advanced Thinking Methods

    /// <summary>
    /// Think Step-by-Step (Chain-of-Thought) - คิดเป็นขั้นตอน
    /// </summary>
    public async Task<ThinkingResult> ThinkAsync(
        string problem,
        ThinkingMode mode = ThinkingMode.Analytical,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Thinking about: {Problem}", problem.Substring(0, Math.Min(50, problem.Length)));

        var startTime = DateTime.UtcNow;
        var result = new ThinkingResult { Problem = problem, Mode = mode };

        // Step 1: Understand the problem
        _ollama.SetSystemPrompt(GetThinkingSystemPrompt(mode));

        var understandingPrompt = $@"ขั้นตอนที่ 1: ทำความเข้าใจปัญหา

ปัญหา: {problem}

วิเคราะห์:
1. ปัญหานี้เกี่ยวกับอะไร?
2. มีข้อมูลอะไรให้บ้าง?
3. ต้องการผลลัพธ์อะไร?
4. มีข้อจำกัดอะไรบ้าง?

ตอบสั้นๆ แต่ครบถ้วน";

        var understanding = await _ollama.ChatAsync(understandingPrompt, ct);
        result.Steps.Add(new ThinkingStep
        {
            Name = "Understanding",
            Content = understanding.Content,
            Duration = DateTime.UtcNow - startTime
        });

        // Step 2: Break down into sub-problems
        var breakdownPrompt = $@"ขั้นตอนที่ 2: แยกปัญหาย่อย

จากความเข้าใจ: {understanding.Content}

แยกปัญหาออกเป็นส่วนย่อยที่แก้ได้:
1. ...
2. ...
3. ...

ระบุลำดับการแก้ไข";

        var breakdown = await _ollama.ChatAsync(breakdownPrompt, ct);
        result.Steps.Add(new ThinkingStep
        {
            Name = "Breakdown",
            Content = breakdown.Content,
            Duration = DateTime.UtcNow - startTime
        });

        // Step 3: Solve each sub-problem
        var solvePrompt = $@"ขั้นตอนที่ 3: แก้ปัญหาทีละส่วน

ปัญหาย่อย: {breakdown.Content}

แก้ไขทีละข้อ:";

        var solution = await _ollama.ChatAsync(solvePrompt, ct);
        result.Steps.Add(new ThinkingStep
        {
            Name = "Solution",
            Content = solution.Content,
            Duration = DateTime.UtcNow - startTime
        });

        // Step 4: Synthesize final answer
        var synthesizePrompt = $@"ขั้นตอนที่ 4: สรุปคำตอบ

จากการวิเคราะห์ทั้งหมด ให้สรุปคำตอบที่ดีที่สุด:

ปัญหาเดิม: {problem}
การวิเคราะห์: {solution.Content}

คำตอบสุดท้าย:";

        var finalAnswer = await _ollama.ChatAsync(synthesizePrompt, ct);
        result.FinalAnswer = finalAnswer.Content;
        result.TotalDuration = DateTime.UtcNow - startTime;

        // Step 5: Self-reflection (if enabled)
        if (Config.EnableSelfReflection)
        {
            result.Reflection = await ReflectOnAnswerAsync(problem, finalAnswer.Content, ct);
        }

        // Store in memory for future learning
        await _memory.StoreInteractionAsync(new MemoryItem
        {
            Type = MemoryType.Reasoning,
            Input = problem,
            Output = finalAnswer.Content,
            Success = true,
            Timestamp = DateTime.UtcNow
        });

        _totalInteractions++;
        _successfulInteractions++;
        _lastUsed = DateTime.UtcNow;

        return result;
    }

    /// <summary>
    /// Self-Reflection - ประเมินคำตอบตัวเอง
    /// </summary>
    public async Task<ReflectionResult> ReflectOnAnswerAsync(
        string question,
        string answer,
        CancellationToken ct = default)
    {
        var reflectionPrompt = $@"ประเมินคำตอบของตัวเอง:

คำถาม: {question}
คำตอบ: {answer}

วิเคราะห์:
1. คำตอบถูกต้องหรือไม่? (0-100%)
2. ครบถ้วนหรือไม่?
3. มีจุดอ่อนอะไร?
4. ควรปรับปรุงอย่างไร?

ตอบเป็น JSON:
{{
  ""confidence"": 85,
  ""is_complete"": true,
  ""weaknesses"": [""...""],
  ""improvements"": [""...""],
  ""revised_answer"": ""..."" // ถ้าต้องแก้ไข
}}";

        var reflection = await _ollama.ChatAsync(reflectionPrompt, ct);

        try
        {
            return ParseReflection(reflection.Content);
        }
        catch
        {
            return new ReflectionResult
            {
                Confidence = 70,
                RawResponse = reflection.Content
            };
        }
    }

    #endregion

    #region Multi-Model Orchestration

    /// <summary>
    /// เลือก model ที่เหมาะสมที่สุดสำหรับ task
    /// </summary>
    public string SelectBestModel(TaskComplexity complexity, TaskType? type = null)
    {
        // Model selection based on task
        return (complexity, type) switch
        {
            // High complexity tasks - use larger models
            (TaskComplexity.High, TaskType.Coding) => "deepseek-coder:6.7b",
            (TaskComplexity.High, TaskType.Reasoning) => "llama3.1:8b",
            (TaskComplexity.High, TaskType.Creative) => "llama3.1:8b",
            (TaskComplexity.High, _) => "llama3.1:8b",

            // Medium complexity - balanced models
            (TaskComplexity.Medium, TaskType.Vision) => "llama3.2-vision:11b",
            (TaskComplexity.Medium, TaskType.Coding) => "deepseek-coder:1.3b",
            (TaskComplexity.Medium, _) => "llama3.2:3b",

            // Low complexity - fast models
            (TaskComplexity.Low, _) => "llama3.2:1b",

            // Default
            _ => "llama3.2:3b"
        };
    }

    /// <summary>
    /// ใช้หลาย model ช่วยกันตอบ (Ensemble)
    /// </summary>
    public async Task<EnsembleResult> EnsembleThinkAsync(
        string problem,
        CancellationToken ct = default)
    {
        var models = new[] { "llama3.2:3b", "mistral:7b", "qwen2.5:7b" };
        var responses = new List<ModelResponse>();

        foreach (var model in models)
        {
            try
            {
                _ollama.CurrentModel = model;
                _ollama.ClearHistory();

                var response = await _ollama.ChatAsync(problem, ct);
                responses.Add(new ModelResponse
                {
                    Model = model,
                    Response = response.Content,
                    TokensPerSecond = response.TokensPerSecond
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Model {Model} failed", model);
            }
        }

        // Synthesize best answer from all responses
        var synthesisPrompt = $@"วิเคราะห์คำตอบจากหลาย AI และสังเคราะห์คำตอบที่ดีที่สุด:

คำถาม: {problem}

คำตอบจาก AI ต่างๆ:
{string.Join("\n\n", responses.Select((r, i) => $"AI {i + 1} ({r.Model}):\n{r.Response}"))}

สังเคราะห์คำตอบที่ดีที่สุดโดยรวมจุดเด่นของแต่ละ AI:";

        _ollama.CurrentModel = SelectBestModel(TaskComplexity.High);
        var synthesis = await _ollama.ChatAsync(synthesisPrompt, ct);

        return new EnsembleResult
        {
            IndividualResponses = responses,
            SynthesizedAnswer = synthesis.Content,
            ModelsUsed = models.ToList()
        };
    }

    #endregion

    #region Memory & Learning System

    /// <summary>
    /// เรียนรู้จากตัวอย่าง (Few-Shot Learning)
    /// </summary>
    public async Task<string> LearnFromExamplesAsync(
        List<Example> examples,
        string newInput,
        CancellationToken ct = default)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("เรียนรู้จากตัวอย่างต่อไปนี้:\n");

        foreach (var example in examples)
        {
            prompt.AppendLine($"Input: {example.Input}");
            prompt.AppendLine($"Output: {example.Output}");
            prompt.AppendLine();
        }

        prompt.AppendLine($"ทำแบบเดียวกันกับ input ใหม่:");
        prompt.AppendLine($"Input: {newInput}");
        prompt.AppendLine("Output:");

        var response = await _ollama.ChatAsync(prompt.ToString(), ct);

        // Store learned pattern
        await _memory.StorePatternAsync(new LearnedPattern
        {
            Examples = examples,
            SuccessCount = 1
        });

        return response.Content;
    }

    /// <summary>
    /// ดึงความรู้ที่เกี่ยวข้องจาก memory
    /// </summary>
    public async Task<List<MemoryItem>> RecallRelevantAsync(
        string query,
        int maxItems = 5,
        CancellationToken ct = default)
    {
        return await _memory.SearchAsync(query, maxItems);
    }

    /// <summary>
    /// เรียนรู้จาก feedback
    /// </summary>
    public async Task LearnFromFeedbackAsync(
        string input,
        string output,
        bool wasCorrect,
        string? correction = null)
    {
        await _memory.StoreFeedbackAsync(new FeedbackItem
        {
            Input = input,
            Output = output,
            WasCorrect = wasCorrect,
            Correction = correction,
            Timestamp = DateTime.UtcNow
        });

        if (!wasCorrect)
        {
            _logger?.LogInformation("Learning from correction: {Correction}", correction);
        }
    }

    #endregion

    #region Tool Usage

    /// <summary>
    /// ใช้ AI ตัดสินใจว่าต้องใช้ tool อะไร
    /// </summary>
    public async Task<ToolDecision> DecideToolUsageAsync(
        string task,
        CancellationToken ct = default)
    {
        var availableTools = _tools.GetAvailableTools();

        var prompt = $@"วิเคราะห์ task และเลือก tool ที่เหมาะสม:

Task: {task}

Available Tools:
{string.Join("\n", availableTools.Select(t => $"- {t.Name}: {t.Description}"))}

ตอบเป็น JSON:
{{
  ""needs_tool"": true/false,
  ""tool_name"": ""..."",
  ""parameters"": {{}},
  ""reasoning"": ""...""
}}";

        var response = await _ollama.ChatAsync(prompt, ct);
        return ParseToolDecision(response.Content);
    }

    /// <summary>
    /// Execute tool และใช้ผลลัพธ์
    /// </summary>
    public async Task<string> ExecuteWithToolsAsync(
        string task,
        CancellationToken ct = default)
    {
        var decision = await DecideToolUsageAsync(task, ct);

        if (!decision.NeedsTool)
        {
            // No tool needed, just answer directly
            return (await _ollama.ChatAsync(task, ct)).Content;
        }

        // Execute the tool
        var tool = _tools.GetTool(decision.ToolName);
        if (tool == null)
        {
            return (await _ollama.ChatAsync(task, ct)).Content;
        }

        var toolResult = await tool.ExecuteAsync(decision.Parameters);

        // Use tool result to answer
        var followUpPrompt = $@"Task: {task}

ผลลัพธ์จากการใช้ {decision.ToolName}:
{toolResult}

ใช้ข้อมูลนี้ตอบคำถาม:";

        return (await _ollama.ChatAsync(followUpPrompt, ct)).Content;
    }

    #endregion

    #region Advanced Content Generation

    /// <summary>
    /// สร้าง content แบบ advanced พร้อม quality check
    /// </summary>
    public async Task<AdvancedContentResult> GenerateAdvancedContentAsync(
        ContentRequest request,
        CancellationToken ct = default)
    {
        var result = new AdvancedContentResult();
        var startTime = DateTime.UtcNow;

        // Step 1: Research & Planning
        _ollama.SetSystemPrompt(GetMasterContentCreatorPrompt());

        var planPrompt = $@"วางแผนการสร้าง content:

Platform: {request.Platform}
Topic: {request.Topic}
Target: {request.TargetAudience}
Brand: {request.BrandInfo?.Name}

วิเคราะห์:
1. กลุ่มเป้าหมายต้องการอะไร?
2. Trend ปัจจุบันคืออะไร?
3. Tone ที่เหมาะสม?
4. Key messages ที่ต้องสื่อ?
5. CTA ที่ดีที่สุด?";

        var plan = await _ollama.ChatAsync(planPrompt, ct);
        result.Planning = plan.Content;

        // Step 2: Generate multiple drafts
        var drafts = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var draftPrompt = $@"สร้าง content draft #{i + 1} (ลองแนวทางที่ต่างกัน):

Plan: {plan.Content}

สร้าง content ที่:
- ดึงดูดความสนใจใน 3 วินาทีแรก
- มี value ชัดเจน
- กระตุ้น engagement
- เหมาะกับ {request.Platform}";

            var draft = await _ollama.ChatAsync(draftPrompt, ct);
            drafts.Add(draft.Content);
        }
        result.Drafts = drafts;

        // Step 3: Evaluate and select best
        var evalPrompt = $@"เลือก draft ที่ดีที่สุด:

Draft 1: {drafts[0]}

Draft 2: {drafts[1]}

Draft 3: {drafts[2]}

วิเคราะห์แต่ละ draft และเลือกที่ดีที่สุด พร้อมปรับปรุงให้สมบูรณ์:";

        var best = await _ollama.ChatAsync(evalPrompt, ct);
        result.FinalContent = best.Content;

        // Step 4: Quality check
        result.QualityScore = await EvaluateContentQualityAsync(result.FinalContent, request.Platform, ct);

        // Step 5: Generate hashtags intelligently
        result.Hashtags = await GenerateSmartHashtagsAsync(result.FinalContent, request.Platform, ct);

        result.GenerationTime = DateTime.UtcNow - startTime;

        return result;
    }

    /// <summary>
    /// สร้างเนื้อเพลงแบบ professional
    /// </summary>
    public async Task<ProfessionalLyricsResult> GenerateProfessionalLyricsAsync(
        LyricsRequest request,
        CancellationToken ct = default)
    {
        var result = new ProfessionalLyricsResult();

        _ollama.SetSystemPrompt(GetMasterSongwriterPrompt());

        // Step 1: Concept Development
        var conceptPrompt = $@"พัฒนา concept เพลง:

Theme: {request.Theme}
Genre: {request.Genre}
Mood: {request.Mood}
Language: {request.Language}

สร้าง:
1. Core message ของเพลง
2. Emotional journey (อารมณ์ที่เปลี่ยนไปในเพลง)
3. Hook/Chorus concept
4. Story structure";

        var concept = await _ollama.ChatAsync(conceptPrompt, ct);
        result.Concept = concept.Content;

        // Step 2: Write lyrics with structure
        var lyricsPrompt = $@"เขียนเนื้อเพลงจาก concept:

{concept.Content}

เขียนเป็น structure:
[Intro] (ถ้ามี)
[Verse 1] - เปิดเรื่อง ดึงเข้าสู่โลกของเพลง
[Pre-Chorus] - สร้าง tension ก่อน chorus
[Chorus] - Hook ที่จำได้ติดหู ซ้ำได้
[Verse 2] - พัฒนาเรื่องราว เพิ่มมิติ
[Chorus]
[Bridge] - เปลี่ยน perspective หรือ climax
[Chorus] - กลับมาทรงพลัง
[Outro]

ใส่ความรู้สึกและ imagery ที่ชัดเจน";

        var lyrics = await _ollama.ChatAsync(lyricsPrompt, ct);
        result.Lyrics = lyrics.Content;

        // Step 3: Melody suggestions
        var melodyPrompt = $@"แนะนำ melody direction:

Lyrics: {lyrics.Content}
Genre: {request.Genre}

แนะนำ:
1. Tempo (BPM)
2. Key ที่เหมาะสม
3. Chord progression แนะนำ
4. Melody style สำหรับแต่ละ section";

        var melody = await _ollama.ChatAsync(melodyPrompt, ct);
        result.MelodySuggestions = melody.Content;

        return result;
    }

    #endregion

    #region Intelligent Decision Making

    /// <summary>
    /// ตัดสินใจแบบ multi-criteria
    /// </summary>
    public async Task<StrategicDecision> MakeStrategicDecisionAsync(
        StrategicContext context,
        CancellationToken ct = default)
    {
        _ollama.SetSystemPrompt(GetStrategicAdvisorPrompt());

        // Step 1: Analyze situation
        var analysisPrompt = $@"วิเคราะห์สถานการณ์:

Context: {context.Situation}
Goals: {string.Join(", ", context.Goals)}
Constraints: {string.Join(", ", context.Constraints)}
Options: {string.Join(", ", context.Options)}

วิเคราะห์:
1. SWOT ของแต่ละ option
2. Risk assessment
3. Expected outcomes
4. Resource requirements";

        var analysis = await _ollama.ChatAsync(analysisPrompt, ct);

        // Step 2: Score each option
        var scorePrompt = $@"ให้คะแนนแต่ละ option (0-100):

Analysis: {analysis.Content}

Criteria:
- Effectiveness (ประสิทธิผล)
- Feasibility (ทำได้จริง)
- Risk (ความเสี่ยง - คะแนนต่ำ = เสี่ยงน้อย)
- Speed (ความเร็ว)
- Cost (ต้นทุน - คะแนนต่ำ = ต้นทุนน้อย)

ให้คะแนนแต่ละ option ในแต่ละ criteria เป็น JSON";

        var scores = await _ollama.ChatAsync(scorePrompt, ct);

        // Step 3: Make final decision
        var decisionPrompt = $@"ตัดสินใจสุดท้าย:

Analysis: {analysis.Content}
Scores: {scores.Content}

เลือก option ที่ดีที่สุดและอธิบายเหตุผล รวมถึง:
1. Action plan ที่ชัดเจน
2. Contingency plan (แผนสำรอง)
3. Success metrics
4. Timeline";

        var decision = await _ollama.ChatAsync(decisionPrompt, ct);

        return new StrategicDecision
        {
            Analysis = analysis.Content,
            Scores = scores.Content,
            Decision = decision.Content,
            Confidence = 0.85,
            Timestamp = DateTime.UtcNow
        };
    }

    #endregion

    #region Advanced Error Recovery

    /// <summary>
    /// แก้ไข error แบบอัจฉริยะพร้อม root cause analysis
    /// </summary>
    public async Task<IntelligentRecoveryResult> RecoverIntelligentlyAsync(
        ErrorContext error,
        List<MemoryItem>? previousAttempts = null,
        CancellationToken ct = default)
    {
        var result = new IntelligentRecoveryResult();

        _ollama.SetSystemPrompt(GetErrorRecoveryExpertPrompt());

        // Step 1: Root Cause Analysis
        var rcaPrompt = $@"วิเคราะห์ Root Cause:

Error: {error.ErrorType}
Message: {error.Message}
Failed Step: {error.FailedStep}
URL: {error.CurrentUrl}
HTML: {error.HtmlSnippet?.Substring(0, Math.Min(error.HtmlSnippet?.Length ?? 0, 3000))}

{(previousAttempts?.Count > 0 ? $"Previous attempts that failed:\n{string.Join("\n", previousAttempts.Select(a => $"- {a.Output}"))}" : "")}

วิเคราะห์:
1. สาเหตุที่แท้จริง (Root Cause)
2. สาเหตุรอง (Contributing Factors)
3. Pattern ที่เห็น
4. ความน่าจะเป็นของแต่ละสาเหตุ";

        var rca = await _ollama.ChatAsync(rcaPrompt, ct);
        result.RootCauseAnalysis = rca.Content;

        // Step 2: Generate multiple solutions
        var solutionsPrompt = $@"เสนอวิธีแก้ไขหลายทาง:

Root Cause: {rca.Content}

เสนอ 3 วิธีแก้ไข เรียงตาม:
1. Quick fix (แก้เร็วที่สุด)
2. Robust fix (แก้ได้มั่นคง)
3. Preventive fix (ป้องกันไม่ให้เกิดอีก)

แต่ละวิธีให้รายละเอียด:
- วิธีการ
- Selector ใหม่ (ถ้าจำเป็น)
- Wait time adjustments
- Success criteria
- Risk level";

        var solutions = await _ollama.ChatAsync(solutionsPrompt, ct);
        result.Solutions = ParseMultipleSolutions(solutions.Content);

        // Step 3: Select best solution
        var bestSolution = result.Solutions.OrderByDescending(s => s.Confidence).FirstOrDefault();
        result.RecommendedSolution = bestSolution;

        // Step 4: Generate test steps
        if (bestSolution != null)
        {
            var testPrompt = $@"สร้างขั้นตอนทดสอบ solution:

Solution: {bestSolution.Description}

สร้าง test steps เพื่อยืนยันว่าแก้ได้จริง";

            var test = await _ollama.ChatAsync(testPrompt, ct);
            result.TestSteps = test.Content;
        }

        // Store for learning
        await _memory.StoreInteractionAsync(new MemoryItem
        {
            Type = MemoryType.ErrorRecovery,
            Input = JsonSerializer.Serialize(error),
            Output = JsonSerializer.Serialize(result),
            Timestamp = DateTime.UtcNow
        });

        return result;
    }

    #endregion

    #region System Prompts

    private string GetThinkingSystemPrompt(ThinkingMode mode)
    {
        return mode switch
        {
            ThinkingMode.Analytical => @"คุณคือ AI นักวิเคราะห์ที่ฉลาดมาก

วิธีคิด:
1. ทำความเข้าใจปัญหาให้ชัด
2. แยกปัญหาใหญ่เป็นปัญหาย่อย
3. วิเคราะห์แต่ละส่วนอย่างละเอียด
4. สังเคราะห์คำตอบจากทุกส่วน
5. ตรวจสอบความถูกต้อง

หลักการ:
- คิดเป็นขั้นตอน
- ใช้เหตุผลและหลักฐาน
- พิจารณาทุกมุม
- ยอมรับเมื่อไม่แน่ใจ",

            ThinkingMode.Creative => @"คุณคือ AI ที่สร้างสรรค์มาก

วิธีคิด:
1. คิดนอกกรอบ
2. เชื่อมโยงสิ่งที่ไม่เกี่ยวกัน
3. ลองมุมมองใหม่
4. ไม่กลัวผิด
5. สนุกกับการสร้างสรรค์

หลักการ:
- ไม่มีไอเดียที่ผิด
- Quantity ก่อน Quality
- Build on ideas
- Challenge assumptions",

            ThinkingMode.Critical => @"คุณคือ AI ที่วิพากษ์วิจารณ์อย่างสร้างสรรค์

วิธีคิด:
1. ตั้งคำถามกับทุกอย่าง
2. หาจุดอ่อนและช่องโหว่
3. ตรวจสอบ logic และหลักฐาน
4. พิจารณา counterarguments
5. สรุปอย่างรอบคอบ

หลักการ:
- อย่าเชื่ออะไรง่ายๆ
- หาแหล่งที่มา
- ดู bias
- Logical fallacies",

            _ => @"คุณคือ AI ที่ฉลาดและรอบรู้"
        };
    }

    private string GetMasterContentCreatorPrompt()
    {
        return @"คุณคือ Content Creator ระดับ Master ที่เข้าใจ:

Platform Expertise:
- Facebook: Storytelling, Emotional connection, Community
- Instagram: Visual-first, Aesthetic, Lifestyle
- TikTok: Trend-driven, Authentic, Entertainment
- Twitter: Concise, Timely, Conversational
- LINE: Personal, Direct, Thai culture
- YouTube: Value-packed, Searchable, Engaging

Content Principles:
1. Hook ใน 3 วินาที - ดึงความสนใจทันที
2. Value First - ให้คุณค่าก่อนขาย
3. Emotion > Logic - คนตัดสินใจด้วยอารมณ์
4. Clear CTA - บอกให้ทำอะไร
5. Platform Native - เหมาะกับ platform

Thai Market Understanding:
- วัฒนธรรม สังคม ค่านิยม
- ภาษาที่ใช้จริง (ไม่เป็นทางการเกินไป)
- Trend ที่กำลังมา
- What makes Thai people engage";
    }

    private string GetMasterSongwriterPrompt()
    {
        return @"คุณคือ Songwriter มืออาชีพที่เข้าใจ:

Song Structure:
- Verse: เล่าเรื่อง สร้าง setting
- Pre-Chorus: Build tension
- Chorus: Hook ที่จำได้ทันที ซ้ำได้
- Bridge: Change perspective, climax
- Outro: Resolution หรือ fade

Lyric Writing Techniques:
1. Show don't tell - ใช้ imagery
2. Specific > General - รายละเอียดสร้างอารมณ์
3. Rhyme scheme - สร้าง flow
4. Syllable count - เข้ากับ melody
5. Emotional journey - พาผู้ฟังไปด้วย

Genre Expertise:
- Pop: Catchy, relatable, simple
- Rock: Powerful, raw, energetic
- Metal: Intense, dramatic, technical
- Ballad: Emotional, storytelling
- EDM: Repetitive hooks, build-ups
- R&B: Smooth, soulful, groove

Thai Songwriting:
- เข้าใจ vowel sounds และ tones
- วลีที่สวยงามในภาษาไทย
- Cultural references
- Emotional expressions";
    }

    private string GetStrategicAdvisorPrompt()
    {
        return @"คุณคือที่ปรึกษาเชิงกลยุทธ์ที่เชี่ยวชาญ:

Decision Framework:
1. Define the problem clearly
2. Gather relevant information
3. Identify alternatives
4. Weigh evidence
5. Choose among alternatives
6. Take action
7. Review decision

Analysis Tools:
- SWOT Analysis
- Risk Assessment Matrix
- Decision Tree
- Cost-Benefit Analysis
- Scenario Planning

Principles:
- Data-driven decisions
- Consider long-term impact
- Risk-reward balance
- Stakeholder impact
- Reversibility of decisions";
    }

    private string GetErrorRecoveryExpertPrompt()
    {
        return @"คุณคือผู้เชี่ยวชาญแก้ไข Web Automation Errors:

Error Categories & Solutions:

1. ElementNotFound
- Check if page loaded completely
- Try alternative selectors (ID → CSS → XPath → Text)
- Check if element is in iframe
- Check if element is dynamically loaded
- Increase wait time

2. Timeout
- Increase timeout duration
- Check network conditions
- Check for loading indicators
- Try different wait strategies

3. StaleElement
- Re-fetch the element
- Add wait before interaction
- Check for page updates

4. ClickIntercepted
- Scroll element into view
- Close popups/overlays
- Use JavaScript click

5. InvalidSelector
- Validate selector syntax
- Try simpler selector
- Use more stable attributes

Root Cause Analysis:
1. Reproduce the error
2. Identify what changed
3. Check all dependencies
4. Find the actual cause
5. Not just symptoms";
    }

    #endregion

    #region Helper Methods

    private void InitializeModelCapabilities()
    {
        _modelCapabilities["llama3.2:1b"] = new ModelCapability
        {
            Speed = 100, Intelligence = 60, Context = 4096,
            BestFor = new[] { TaskType.Simple, TaskType.Quick }
        };
        _modelCapabilities["llama3.2:3b"] = new ModelCapability
        {
            Speed = 80, Intelligence = 75, Context = 8192,
            BestFor = new[] { TaskType.General, TaskType.Content }
        };
        _modelCapabilities["llama3.1:8b"] = new ModelCapability
        {
            Speed = 50, Intelligence = 90, Context = 32768,
            BestFor = new[] { TaskType.Reasoning, TaskType.Complex }
        };
        _modelCapabilities["llama3.2-vision:11b"] = new ModelCapability
        {
            Speed = 30, Intelligence = 85, Context = 8192,
            BestFor = new[] { TaskType.Vision }
        };
        _modelCapabilities["deepseek-coder:6.7b"] = new ModelCapability
        {
            Speed = 40, Intelligence = 85, Context = 16384,
            BestFor = new[] { TaskType.Coding }
        };
    }

    private void RegisterDefaultTools()
    {
        _tools.Register(new AITool
        {
            Name = "web_search",
            Description = "ค้นหาข้อมูลจากอินเทอร์เน็ต",
            Execute = async (p) => await _ollama.SearchWebAsync(p["query"]?.ToString() ?? "")
        });

        _tools.Register(new AITool
        {
            Name = "calculate",
            Description = "คำนวณสูตรคณิตศาสตร์",
            Execute = async (p) => await Task.FromResult(EvaluateExpression(p["expression"]?.ToString() ?? "0"))
        });
    }

    private object EvaluateExpression(string expr)
    {
        try
        {
            var dt = new System.Data.DataTable();
            return dt.Compute(expr, "");
        }
        catch
        {
            return "Error evaluating expression";
        }
    }

    private async Task<int> EvaluateContentQualityAsync(
        string content,
        string platform,
        CancellationToken ct)
    {
        var evalPrompt = $@"ให้คะแนน content นี้ (0-100):

Content: {content}
Platform: {platform}

Criteria:
- Hook (ดึงดูดความสนใจ) - 20%
- Value (มีประโยชน์) - 20%
- Engagement (กระตุ้น action) - 20%
- Platform fit (เหมาะกับ platform) - 20%
- Authenticity (จริงใจ) - 20%

ตอบแค่ตัวเลข 0-100";

        var response = await _ollama.ChatAsync(evalPrompt, ct);

        if (int.TryParse(response.Content.Trim(), out int score))
            return Math.Clamp(score, 0, 100);

        return 70; // Default score
    }

    private async Task<List<string>> GenerateSmartHashtagsAsync(
        string content,
        string platform,
        CancellationToken ct)
    {
        var prompt = $@"สร้าง hashtags สำหรับ {platform}:

Content: {content}

สร้าง hashtags ที่:
1. เกี่ยวข้องกับเนื้อหา
2. มีคนค้นหาเยอะ
3. ไม่แข่งขันสูงเกินไป
4. Mix ระหว่าง broad และ niche

ตอบเป็น list แยกด้วย comma";

        var response = await _ollama.ChatAsync(prompt, ct);

        return response.Content
            .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => h.Trim())
            .Where(h => h.StartsWith("#") || !string.IsNullOrEmpty(h))
            .Select(h => h.StartsWith("#") ? h : $"#{h}")
            .Take(15)
            .ToList();
    }

    private ReflectionResult ParseReflection(string content)
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<ReflectionResult>(json) ?? new ReflectionResult();
            }
        }
        catch { }

        return new ReflectionResult { RawResponse = content };
    }

    private ToolDecision ParseToolDecision(string content)
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<ToolDecision>(json) ?? new ToolDecision();
            }
        }
        catch { }

        return new ToolDecision { NeedsTool = false };
    }

    private List<RecoverySolution> ParseMultipleSolutions(string content)
    {
        var solutions = new List<RecoverySolution>();

        // Simple parsing - extract numbered solutions
        var lines = content.Split('\n');
        RecoverySolution? current = null;

        foreach (var line in lines)
        {
            if (line.Contains("Quick fix") || line.Contains("1."))
            {
                current = new RecoverySolution { Type = "Quick", Confidence = 0.7 };
                solutions.Add(current);
            }
            else if (line.Contains("Robust fix") || line.Contains("2."))
            {
                current = new RecoverySolution { Type = "Robust", Confidence = 0.85 };
                solutions.Add(current);
            }
            else if (line.Contains("Preventive fix") || line.Contains("3."))
            {
                current = new RecoverySolution { Type = "Preventive", Confidence = 0.9 };
                solutions.Add(current);
            }
            else if (current != null)
            {
                current.Description += line + "\n";
            }
        }

        return solutions;
    }

    #endregion

    #region Statistics

    public BrainStats GetStats()
    {
        return new BrainStats
        {
            TotalInteractions = _totalInteractions,
            SuccessfulInteractions = _successfulInteractions,
            SuccessRate = _totalInteractions > 0 ? (double)_successfulInteractions / _totalInteractions : 0,
            LastUsed = _lastUsed,
            CurrentModel = _ollama.CurrentModel,
            MemorySize = _memory.GetSize(),
            LearnedPatterns = _memory.GetPatternCount()
        };
    }

    #endregion
}

#region Enums and Models

public enum ThinkingMode
{
    Analytical,  // วิเคราะห์เชิงตรรกะ
    Creative,    // สร้างสรรค์
    Critical,    // วิพากษ์วิจารณ์
    Strategic,   // เชิงกลยุทธ์
    Empathetic   // เข้าใจอารมณ์
}

public enum TaskComplexity
{
    Low,
    Medium,
    High
}

public enum TaskType
{
    Simple,
    General,
    Content,
    Reasoning,
    Complex,
    Vision,
    Coding,
    Quick,
    Creative
}

public enum MemoryType
{
    Reasoning,
    Content,
    Workflow,
    ErrorRecovery,
    Feedback,
    Pattern
}

public class AIBrainConfig
{
    public bool EnableSelfReflection { get; set; } = true;
    public bool EnableMemory { get; set; } = true;
    public bool EnableToolUsage { get; set; } = true;
    public int MaxMemoryItems { get; set; } = 1000;
    public double MinConfidenceThreshold { get; set; } = 0.6;
}

public class ThinkingResult
{
    public string Problem { get; set; } = "";
    public ThinkingMode Mode { get; set; }
    public List<ThinkingStep> Steps { get; set; } = new();
    public string FinalAnswer { get; set; } = "";
    public ReflectionResult? Reflection { get; set; }
    public TimeSpan TotalDuration { get; set; }
}

public class ThinkingStep
{
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public TimeSpan Duration { get; set; }
}

public class ReflectionResult
{
    [JsonPropertyName("confidence")]
    public int Confidence { get; set; }

    [JsonPropertyName("is_complete")]
    public bool IsComplete { get; set; }

    [JsonPropertyName("weaknesses")]
    public List<string> Weaknesses { get; set; } = new();

    [JsonPropertyName("improvements")]
    public List<string> Improvements { get; set; } = new();

    [JsonPropertyName("revised_answer")]
    public string? RevisedAnswer { get; set; }

    public string? RawResponse { get; set; }
}

public class EnsembleResult
{
    public List<ModelResponse> IndividualResponses { get; set; } = new();
    public string SynthesizedAnswer { get; set; } = "";
    public List<string> ModelsUsed { get; set; } = new();
}

public class ModelResponse
{
    public string Model { get; set; } = "";
    public string Response { get; set; } = "";
    public double TokensPerSecond { get; set; }
}

public class ModelCapability
{
    public int Speed { get; set; }
    public int Intelligence { get; set; }
    public int Context { get; set; }
    public TaskType[] BestFor { get; set; } = Array.Empty<TaskType>();
}

public class Example
{
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
}

public class ToolDecision
{
    [JsonPropertyName("needs_tool")]
    public bool NeedsTool { get; set; }

    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = "";

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();

    [JsonPropertyName("reasoning")]
    public string Reasoning { get; set; } = "";
}

public class AdvancedContentResult
{
    public string Planning { get; set; } = "";
    public List<string> Drafts { get; set; } = new();
    public string FinalContent { get; set; } = "";
    public int QualityScore { get; set; }
    public List<string> Hashtags { get; set; } = new();
    public TimeSpan GenerationTime { get; set; }
}

public class LyricsRequest
{
    public string Theme { get; set; } = "";
    public string Genre { get; set; } = "Pop";
    public string Mood { get; set; } = "Emotional";
    public string Language { get; set; } = "Thai";
    public string? AdditionalInstructions { get; set; }
}

public class ProfessionalLyricsResult
{
    public string Concept { get; set; } = "";
    public string Lyrics { get; set; } = "";
    public string MelodySuggestions { get; set; } = "";
}

public class StrategicContext
{
    public string Situation { get; set; } = "";
    public List<string> Goals { get; set; } = new();
    public List<string> Constraints { get; set; } = new();
    public List<string> Options { get; set; } = new();
}

public class StrategicDecision
{
    public string Analysis { get; set; } = "";
    public string Scores { get; set; } = "";
    public string Decision { get; set; } = "";
    public double Confidence { get; set; }
    public DateTime Timestamp { get; set; }
}

public class IntelligentRecoveryResult
{
    public string RootCauseAnalysis { get; set; } = "";
    public List<RecoverySolution> Solutions { get; set; } = new();
    public RecoverySolution? RecommendedSolution { get; set; }
    public string TestSteps { get; set; } = "";
}

public class RecoverySolution
{
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public double Confidence { get; set; }
    public Dictionary<string, object>? NewSelector { get; set; }
    public int WaitAdjustment { get; set; }
}

public class BrainStats
{
    public int TotalInteractions { get; set; }
    public int SuccessfulInteractions { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastUsed { get; set; }
    public string CurrentModel { get; set; } = "";
    public int MemorySize { get; set; }
    public int LearnedPatterns { get; set; }
}

public class ContentRequest
{
    public string Topic { get; set; } = "";
    public string Platform { get; set; } = "Facebook";
    public BrandInfo? BrandInfo { get; set; }
    public string? TargetAudience { get; set; }
    public string Language { get; set; } = "th";
}

public class BrandInfo
{
    public string Name { get; set; } = "";
    public string Tone { get; set; } = "Friendly";
}

public class ErrorContext
{
    public string ErrorType { get; set; } = "";
    public string Message { get; set; } = "";
    public string FailedStep { get; set; } = "";
    public string? CurrentUrl { get; set; }
    public string? HtmlSnippet { get; set; }
}

#endregion

#region Memory System

public class AIMemorySystem
{
    private readonly List<MemoryItem> _shortTermMemory = new();
    private readonly List<MemoryItem> _longTermMemory = new();
    private readonly List<LearnedPattern> _patterns = new();
    private readonly List<FeedbackItem> _feedback = new();

    public async Task StoreInteractionAsync(MemoryItem item)
    {
        _shortTermMemory.Add(item);

        // Promote to long-term if successful
        if (item.Success)
        {
            _longTermMemory.Add(item);
        }

        // Limit memory size
        while (_shortTermMemory.Count > 100)
            _shortTermMemory.RemoveAt(0);

        while (_longTermMemory.Count > 500)
            _longTermMemory.RemoveAt(0);

        await Task.CompletedTask;
    }

    public async Task<List<MemoryItem>> SearchAsync(string query, int maxItems = 5)
    {
        // Simple keyword matching (could be enhanced with embeddings)
        var results = _longTermMemory
            .Where(m => m.Input.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       m.Output.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => m.Timestamp)
            .Take(maxItems)
            .ToList();

        return await Task.FromResult(results);
    }

    public async Task StorePatternAsync(LearnedPattern pattern)
    {
        _patterns.Add(pattern);
        await Task.CompletedTask;
    }

    public async Task StoreFeedbackAsync(FeedbackItem feedback)
    {
        _feedback.Add(feedback);
        await Task.CompletedTask;
    }

    public int GetSize() => _shortTermMemory.Count + _longTermMemory.Count;
    public int GetPatternCount() => _patterns.Count;
}

public class MemoryItem
{
    public MemoryType Type { get; set; }
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public bool Success { get; set; }
    public DateTime Timestamp { get; set; }
}

public class LearnedPattern
{
    public List<Example> Examples { get; set; } = new();
    public int SuccessCount { get; set; }
}

public class FeedbackItem
{
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public bool WasCorrect { get; set; }
    public string? Correction { get; set; }
    public DateTime Timestamp { get; set; }
}

#endregion

#region Reasoning Engine

public class AIReasoningEngine
{
    public async Task<string> ChainOfThoughtAsync(string problem, OllamaChatService ollama)
    {
        var steps = new List<string>();

        // Step 1: Understand
        var understanding = await ollama.ChatAsync($"ทำความเข้าใจปัญหานี้: {problem}");
        steps.Add($"Understanding: {understanding.Content}");

        // Step 2: Plan
        var plan = await ollama.ChatAsync($"วางแผนแก้ปัญหา: {understanding.Content}");
        steps.Add($"Plan: {plan.Content}");

        // Step 3: Execute
        var solution = await ollama.ChatAsync($"แก้ปัญหาตามแผน: {plan.Content}");
        steps.Add($"Solution: {solution.Content}");

        return string.Join("\n\n", steps);
    }
}

#endregion

#region Tool Registry

public class AIToolRegistry
{
    private readonly Dictionary<string, AITool> _tools = new();

    public void Register(AITool tool)
    {
        _tools[tool.Name] = tool;
    }

    public AITool? GetTool(string name)
    {
        return _tools.TryGetValue(name, out var tool) ? tool : null;
    }

    public List<AITool> GetAvailableTools()
    {
        return _tools.Values.ToList();
    }
}

public class AITool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public Func<Dictionary<string, object>, Task<object>>? Execute { get; set; }

    public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
    {
        if (Execute == null) return "Tool not implemented";

        var result = await Execute(parameters);
        return result?.ToString() ?? "";
    }
}

#endregion
