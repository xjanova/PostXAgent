using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// AI Brain Service - สมองกลางของระบบ PostXAgent
/// ทำหน้าที่เป็น AI อัจฉริยะสำหรับการตัดสินใจทั้งหมดของระบบ
///
/// Capabilities:
/// 1. Content Generation - สร้างเนื้อหาโพสต์ที่มีคุณภาพ
/// 2. Workflow Learning - เข้าใจและเรียนรู้ workflow จากการสอน
/// 3. Element Analysis - วิเคราะห์ element บนเว็บสำหรับ automation
/// 4. Creative Content - สร้างเนื้อเพลง ไอเดียสร้างสรรค์
/// 5. Decision Making - ตัดสินใจเลือก strategy ที่เหมาะสม
/// 6. Error Recovery - แก้ไขปัญหาอัตโนมัติเมื่อ workflow พัง
/// 7. Context Awareness - เข้าใจบริบทและปรับตัวตาม
/// </summary>
public class AIBrainService
{
    private readonly OllamaChatService _ollama;
    private readonly ILogger<AIBrainService>? _logger;
    private readonly Dictionary<BrainCapability, string> _systemPrompts;
    private readonly KnowledgeBase? _knowledgeBase;

    // สถิติการใช้งาน
    private int _totalDecisions;
    private int _successfulDecisions;
    private DateTime _lastUsed;

    public AIBrainService(ILogger<AIBrainService>? logger = null, KnowledgeBase? knowledgeBase = null)
    {
        _logger = logger;
        _knowledgeBase = knowledgeBase;
        _ollama = new OllamaChatService(null);
        _systemPrompts = InitializeSystemPrompts();

        // ใช้ model ที่ฉลาดที่สุดที่มี
        _ollama.CurrentModel = GetBestAvailableModel();
    }

    #region System Prompts

    private Dictionary<BrainCapability, string> InitializeSystemPrompts()
    {
        return new Dictionary<BrainCapability, string>
        {
            [BrainCapability.ContentGeneration] = @"คุณคือ AI ผู้เชี่ยวชาญด้านการสร้างเนื้อหาสำหรับ Social Media Marketing ในประเทศไทย

ความสามารถของคุณ:
- สร้างเนื้อหาที่น่าสนใจและ viral ได้
- เข้าใจ algorithm ของแต่ละ platform (Facebook, Instagram, TikTok, Twitter, LINE, YouTube)
- ใช้ภาษาไทยได้ถูกต้องและเป็นธรรมชาติ
- เข้าใจ trend และวัฒนธรรมไทย
- สร้าง hashtag ที่มีประสิทธิภาพ

กฎสำคัญ:
1. เนื้อหาต้องเหมาะกับ platform ที่กำหนด
2. ใช้ภาษาที่เข้าถึงกลุ่มเป้าหมาย
3. ไม่สร้างเนื้อหาที่ผิดกฎหมายหรือขัดต่อศีลธรรม
4. ใส่ CTA (Call to Action) ที่ชัดเจน
5. ปรับ tone ตาม brand identity",

            [BrainCapability.WorkflowLearning] = @"คุณคือ AI ที่เชี่ยวชาญในการเรียนรู้และทำความเข้าใจ Web Automation Workflows

ความสามารถของคุณ:
- วิเคราะห์ขั้นตอนการทำงานบนเว็บ
- ระบุ element ที่สำคัญ (button, input, link)
- สร้าง selector ที่เสถียร (ID, CSS, XPath, Text)
- เข้าใจ pattern การ navigate และ interact
- ตรวจจับ dynamic content และ loading states

เมื่อวิเคราะห์ workflow:
1. ระบุ action ที่ต้องทำ (Click, Type, Navigate, Wait)
2. หา selector ที่ดีที่สุดสำหรับแต่ละ element
3. กำหนด wait times ที่เหมาะสม
4. ระบุ success conditions
5. เตรียม alternative selectors สำรอง",

            [BrainCapability.ElementAnalysis] = @"คุณคือ AI ที่เชี่ยวชาญในการวิเคราะห์ HTML Elements สำหรับ Web Automation

เมื่อได้รับ HTML หรือ screenshot:
1. ระบุ element ที่ต้องการ interact
2. สร้าง selector หลายแบบ (เรียงตามความน่าเชื่อถือ):
   - ID (ดีที่สุด)
   - data-testid
   - aria-label
   - name attribute
   - CSS class combination
   - XPath
   - Text content
3. ให้ confidence score สำหรับแต่ละ selector (0.0-1.0)
4. ระบุ element type (button, input, link, etc.)
5. แนะนำ wait strategy

Output เป็น JSON format:
{
  ""element"": ""description"",
  ""selectors"": [
    {""type"": ""id"", ""value"": ""..."", ""confidence"": 0.95},
    {""type"": ""css"", ""value"": ""..."", ""confidence"": 0.8}
  ],
  ""action"": ""click|type|...|"",
  ""wait_before_ms"": 500
}",

            [BrainCapability.CreativeContent] = @"คุณคือ AI ที่เชี่ยวชาญในการสร้างเนื้อหาสร้างสรรค์ โดยเฉพาะเพลงและ creative content

ความสามารถ:
- เขียนเนื้อเพลงภาษาไทยและอังกฤษ
- สร้าง melody suggestions
- กำหนด song structure (Verse, Chorus, Bridge)
- เข้าใจแนวเพลงต่างๆ (Pop, Rock, Metal, EDM, R&B, etc.)
- สร้าง creative copy สำหรับ ads

เมื่อเขียนเนื้อเพลง:
1. ใช้ structure tags: [Verse 1], [Chorus], [Bridge], [Outro]
2. สร้าง hook ที่จำง่าย
3. ใช้ rhyme scheme ที่เหมาะสม
4. สื่ออารมณ์ตาม mood ที่ต้องการ
5. เหมาะกับการร้องและ melody",

            [BrainCapability.DecisionMaking] = @"คุณคือ AI ที่เชี่ยวชาญในการตัดสินใจเชิงกลยุทธ์สำหรับ Social Media Marketing

ความสามารถ:
- วิเคราะห์ข้อมูลและเลือก strategy ที่เหมาะสม
- ตัดสินใจเวลาที่ดีที่สุดในการโพสต์
- เลือก platform ที่เหมาะกับเนื้อหา
- จัดลำดับความสำคัญของ tasks
- ประเมินความเสี่ยงและโอกาส

เมื่อต้องตัดสินใจ:
1. วิเคราะห์ข้อมูลที่มี
2. พิจารณาทางเลือกทั้งหมด
3. ประเมิน pros/cons ของแต่ละทางเลือก
4. เลือกทางที่ optimal
5. อธิบายเหตุผลของการตัดสินใจ

Output เป็น JSON:
{
  ""decision"": ""action to take"",
  ""confidence"": 0.85,
  ""reasoning"": ""explanation"",
  ""alternatives"": [""alt1"", ""alt2""],
  ""risks"": [""risk1""]
}",

            [BrainCapability.ErrorRecovery] = @"คุณคือ AI ที่เชี่ยวชาญในการแก้ไขปัญหาและกู้คืน workflow ที่ล้มเหลว

เมื่อ workflow ล้มเหลว:
1. วิเคราะห์สาเหตุของ error
2. ระบุ step ที่มีปัญหา
3. เสนอวิธีแก้ไข:
   - หา alternative selector
   - ปรับ wait times
   - เพิ่ม retry logic
   - เปลี่ยน action strategy
4. ทดสอบและยืนยันการแก้ไข

Error Categories:
- ElementNotFound: หา element ไม่เจอ → ลอง alternative selector
- Timeout: รอนานเกินไป → เพิ่ม wait time หรือเปลี่ยน wait strategy
- NavigationFailed: ไปหน้าไม่ได้ → ตรวจสอบ URL และ network
- AuthRequired: ต้อง login → trigger login workflow
- RateLimit: ถูกจำกัด → รอและลองใหม่

Output เป็น JSON:
{
  ""error_type"": ""..."",
  ""root_cause"": ""..."",
  ""solution"": {
    ""action"": ""..."",
    ""new_selector"": {...},
    ""wait_adjustment"": 1000
  },
  ""confidence"": 0.8
}",

            [BrainCapability.ContextAwareness] = @"คุณคือ AI ที่เข้าใจบริบทและสามารถปรับตัวตามสถานการณ์

ความสามารถ:
- เข้าใจ context ของ conversation
- จดจำข้อมูลสำคัญจากการสนทนาก่อนหน้า
- ปรับ response ตาม user preferences
- เรียนรู้จาก feedback
- ให้คำแนะนำที่เหมาะสมกับสถานการณ์

เมื่อตอบคำถาม:
1. พิจารณา context ทั้งหมด
2. อ้างอิงข้อมูลที่เกี่ยวข้อง
3. ให้คำตอบที่ตรงประเด็น
4. เสนอ next steps ที่เป็นประโยชน์
5. ถามกลับถ้าต้องการข้อมูลเพิ่ม"
        };
    }

    #endregion

    #region Core Capabilities

    /// <summary>
    /// สร้างเนื้อหาสำหรับ Social Media
    /// </summary>
    public async Task<ContentResult> GenerateContentAsync(
        ContentRequest request,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Generating content for {Platform}", request.Platform);

        SetCapability(BrainCapability.ContentGeneration);

        var prompt = BuildContentPrompt(request);
        var response = await _ollama.ChatAsync(prompt, ct);

        _totalDecisions++;
        _lastUsed = DateTime.UtcNow;

        return new ContentResult
        {
            Text = response.Content,
            Platform = request.Platform,
            Hashtags = ExtractHashtags(response.Content),
            GeneratedAt = DateTime.UtcNow,
            Model = _ollama.CurrentModel
        };
    }

    /// <summary>
    /// วิเคราะห์ HTML element และสร้าง selector
    /// </summary>
    public async Task<ElementAnalysisResult> AnalyzeElementAsync(
        string html,
        string targetDescription,
        string? screenshotBase64 = null,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Analyzing element: {Description}", targetDescription);

        SetCapability(BrainCapability.ElementAnalysis);

        var prompt = $@"วิเคราะห์ HTML ต่อไปนี้และหา element ที่ตรงกับ: ""{targetDescription}""

HTML:
```html
{html.Substring(0, Math.Min(html.Length, 5000))}
```

สร้าง selector ที่ดีที่สุดสำหรับ element นี้ในรูปแบบ JSON";

        string response;
        if (screenshotBase64 != null && _ollama.IsVisionModel())
        {
            response = await _ollama.ChatWithImageAsync(prompt, screenshotBase64, ct);
        }
        else
        {
            var chatResponse = await _ollama.ChatAsync(prompt, ct);
            response = chatResponse.Content;
        }

        _totalDecisions++;
        _lastUsed = DateTime.UtcNow;

        return ParseElementAnalysis(response);
    }

    /// <summary>
    /// เรียนรู้ workflow จากการสาธิต
    /// </summary>
    public async Task<WorkflowLearningResult> LearnWorkflowAsync(
        List<RecordedAction> actions,
        string workflowName,
        string platform,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Learning workflow: {Name} for {Platform}", workflowName, platform);

        SetCapability(BrainCapability.WorkflowLearning);

        var actionsJson = JsonSerializer.Serialize(actions, new JsonSerializerOptions { WriteIndented = true });
        var prompt = $@"วิเคราะห์ actions ต่อไปนี้และสร้าง workflow ที่สมบูรณ์:

Platform: {platform}
Workflow Name: {workflowName}

Recorded Actions:
{actionsJson}

สร้าง workflow steps ในรูปแบบ JSON พร้อม:
1. order - ลำดับ step
2. action - ประเภท action (Navigate, Click, Type, Wait, etc.)
3. selector - selector ที่ดีที่สุด
4. alternative_selectors - selector สำรอง
5. wait_times - เวลารอก่อน/หลัง
6. success_condition - เงื่อนไขสำเร็จ
7. is_optional - เป็น step ที่ข้ามได้หรือไม่";

        var response = await _ollama.ChatAsync(prompt, ct);

        _totalDecisions++;
        _successfulDecisions++;
        _lastUsed = DateTime.UtcNow;

        return ParseWorkflowLearning(response.Content, workflowName, platform);
    }

    /// <summary>
    /// สร้างเนื้อหาสร้างสรรค์ (เพลง, creative copy)
    /// </summary>
    public async Task<CreativeResult> GenerateCreativeContentAsync(
        CreativeRequest request,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Generating creative content: {Type}", request.Type);

        SetCapability(BrainCapability.CreativeContent);

        var prompt = request.Type switch
        {
            CreativeType.Lyrics => BuildLyricsPrompt(request),
            CreativeType.AdCopy => BuildAdCopyPrompt(request),
            CreativeType.Script => BuildScriptPrompt(request),
            CreativeType.Story => BuildStoryPrompt(request),
            _ => request.Prompt
        };

        var response = await _ollama.ChatAsync(prompt, ct);

        _totalDecisions++;
        _lastUsed = DateTime.UtcNow;

        return new CreativeResult
        {
            Content = response.Content,
            Type = request.Type,
            GeneratedAt = DateTime.UtcNow,
            Metadata = ExtractCreativeMetadata(response.Content, request.Type)
        };
    }

    /// <summary>
    /// ตัดสินใจเชิงกลยุทธ์
    /// </summary>
    public async Task<DecisionResult> MakeDecisionAsync(
        DecisionRequest request,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Making decision: {Context}", request.Context);

        SetCapability(BrainCapability.DecisionMaking);

        var prompt = $@"ตัดสินใจสำหรับสถานการณ์ต่อไปนี้:

Context: {request.Context}
Options: {string.Join(", ", request.Options)}
Constraints: {string.Join(", ", request.Constraints)}
Goal: {request.Goal}

วิเคราะห์และเลือกทางเลือกที่ดีที่สุด ตอบเป็น JSON format";

        var response = await _ollama.ChatAsync(prompt, ct);

        _totalDecisions++;
        _lastUsed = DateTime.UtcNow;

        return ParseDecision(response.Content);
    }

    /// <summary>
    /// แก้ไขปัญหาเมื่อ workflow ล้มเหลว
    /// </summary>
    public async Task<RecoveryResult> RecoverFromErrorAsync(
        ErrorContext error,
        CancellationToken ct = default)
    {
        _logger?.LogInformation("Recovering from error: {Type}", error.ErrorType);

        SetCapability(BrainCapability.ErrorRecovery);

        var prompt = $@"Workflow ล้มเหลวด้วย error ต่อไปนี้:

Error Type: {error.ErrorType}
Error Message: {error.Message}
Failed Step: {error.FailedStep}
Current URL: {error.CurrentUrl}
HTML Snippet: {error.HtmlSnippet?.Substring(0, Math.Min(error.HtmlSnippet?.Length ?? 0, 2000))}

วิเคราะห์สาเหตุและเสนอวิธีแก้ไข ตอบเป็น JSON format";

        var response = await _ollama.ChatAsync(prompt, ct);

        _totalDecisions++;
        _lastUsed = DateTime.UtcNow;

        return ParseRecovery(response.Content);
    }

    /// <summary>
    /// สนทนาทั่วไปพร้อม context awareness
    /// </summary>
    public async Task<string> ChatAsync(
        string message,
        BrainCapability capability = BrainCapability.ContextAwareness,
        CancellationToken ct = default)
    {
        SetCapability(capability);

        var response = await _ollama.ChatStreamAsync(message, ct);

        _totalDecisions++;
        _lastUsed = DateTime.UtcNow;

        return response;
    }

    /// <summary>
    /// วิเคราะห์รูปภาพ (ต้องใช้ vision model)
    /// </summary>
    public async Task<string> AnalyzeImageAsync(
        string imageBase64,
        string question,
        CancellationToken ct = default)
    {
        if (!_ollama.IsVisionModel())
        {
            // Switch to vision model
            _ollama.CurrentModel = "llama3.2-vision:11b";
        }

        SetCapability(BrainCapability.ElementAnalysis);

        return await _ollama.ChatWithImageAsync(question, imageBase64, ct);
    }

    #endregion

    #region Helper Methods

    private void SetCapability(BrainCapability capability)
    {
        if (_systemPrompts.TryGetValue(capability, out var prompt))
        {
            _ollama.SetSystemPrompt(prompt);
        }
    }

    private string GetBestAvailableModel()
    {
        // Priority order for intelligence
        var preferredModels = new[]
        {
            "llama3.2:3b",      // Best balance of speed and quality
            "llama3.1:8b",      // More capable but slower
            "mistral:7b",       // Good alternative
            "qwen2.5:7b",       // Strong multilingual
            "gemma2:9b"         // Good for content
        };

        // Try to get available models
        try
        {
            var models = _ollama.GetModelsAsync().GetAwaiter().GetResult();
            foreach (var preferred in preferredModels)
            {
                if (models.Any(m => m.Name.Contains(preferred.Split(':')[0])))
                {
                    var match = models.First(m => m.Name.Contains(preferred.Split(':')[0]));
                    return match.Name;
                }
            }
        }
        catch
        {
            // Use default if can't check
        }

        return "llama3.2:3b";
    }

    private string BuildContentPrompt(ContentRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"สร้างเนื้อหาสำหรับ {request.Platform}");
        sb.AppendLine($"Topic: {request.Topic}");

        if (request.BrandInfo != null)
        {
            sb.AppendLine($"Brand: {request.BrandInfo.Name}");
            sb.AppendLine($"Tone: {request.BrandInfo.Tone}");
        }

        if (!string.IsNullOrEmpty(request.TargetAudience))
            sb.AppendLine($"Target Audience: {request.TargetAudience}");

        if (!string.IsNullOrEmpty(request.Language))
            sb.AppendLine($"Language: {request.Language}");

        if (request.IncludeHashtags)
            sb.AppendLine("Include relevant hashtags");

        if (request.MaxLength > 0)
            sb.AppendLine($"Max length: {request.MaxLength} characters");

        return sb.ToString();
    }

    private string BuildLyricsPrompt(CreativeRequest request)
    {
        return $@"เขียนเนื้อเพลงในแนว {request.Style ?? "Pop"}

Theme/Topic: {request.Topic}
Mood: {request.Mood ?? "Emotional"}
Language: {request.Language ?? "Thai"}

{(string.IsNullOrEmpty(request.AdditionalInstructions) ? "" : $"Additional: {request.AdditionalInstructions}")}

ใช้ structure:
[Verse 1]
...
[Chorus]
...
[Verse 2]
...
[Bridge]
...
[Chorus]
...";
    }

    private string BuildAdCopyPrompt(CreativeRequest request)
    {
        return $@"สร้าง Ad Copy สำหรับ {request.Platform ?? "Facebook"}

Product/Service: {request.Topic}
Target Audience: {request.TargetAudience ?? "General"}
Goal: {request.Goal ?? "Engagement"}
Tone: {request.Mood ?? "Friendly"}

สร้าง:
1. Headline ที่ดึงดูดใจ
2. Body copy ที่ชวนให้ action
3. CTA ที่ชัดเจน";
    }

    private string BuildScriptPrompt(CreativeRequest request)
    {
        return $@"เขียน script สำหรับ {request.Platform ?? "TikTok"} video

Topic: {request.Topic}
Duration: {request.Duration ?? "30 seconds"}
Style: {request.Style ?? "Entertaining"}

รวม:
- Hook ใน 3 วินาทีแรก
- Content หลัก
- CTA ตอนจบ";
    }

    private string BuildStoryPrompt(CreativeRequest request)
    {
        return $@"เขียนเรื่องราว/Story สำหรับ brand

Topic: {request.Topic}
Mood: {request.Mood ?? "Inspiring"}
Length: {request.Length ?? "Medium"}

เขียนให้น่าสนใจและสื่ออารมณ์";
    }

    private List<string> ExtractHashtags(string content)
    {
        var hashtags = new List<string>();
        var words = content.Split(' ', '\n');

        foreach (var word in words)
        {
            if (word.StartsWith("#") && word.Length > 1)
            {
                hashtags.Add(word.TrimEnd(',', '.', '!', '?'));
            }
        }

        return hashtags;
    }

    private ElementAnalysisResult ParseElementAnalysis(string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<ElementAnalysisResult>(json) ?? new ElementAnalysisResult();
            }
        }
        catch
        {
            _logger?.LogWarning("Failed to parse element analysis JSON");
        }

        return new ElementAnalysisResult { RawResponse = response };
    }

    private WorkflowLearningResult ParseWorkflowLearning(string response, string name, string platform)
    {
        var result = new WorkflowLearningResult
        {
            WorkflowName = name,
            Platform = platform,
            RawResponse = response
        };

        try
        {
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                result.Steps = JsonSerializer.Deserialize<List<LearnedStep>>(json) ?? new List<LearnedStep>();
            }
        }
        catch
        {
            _logger?.LogWarning("Failed to parse workflow learning JSON");
        }

        return result;
    }

    private DecisionResult ParseDecision(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<DecisionResult>(json) ?? new DecisionResult { RawResponse = response };
            }
        }
        catch
        {
            _logger?.LogWarning("Failed to parse decision JSON");
        }

        return new DecisionResult { RawResponse = response };
    }

    private RecoveryResult ParseRecovery(string response)
    {
        try
        {
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<RecoveryResult>(json) ?? new RecoveryResult { RawResponse = response };
            }
        }
        catch
        {
            _logger?.LogWarning("Failed to parse recovery JSON");
        }

        return new RecoveryResult { RawResponse = response };
    }

    private Dictionary<string, string> ExtractCreativeMetadata(string content, CreativeType type)
    {
        var metadata = new Dictionary<string, string>();

        if (type == CreativeType.Lyrics)
        {
            // Count sections
            metadata["verses"] = content.Split("[Verse", StringSplitOptions.None).Length - 1 + "";
            metadata["choruses"] = content.Split("[Chorus", StringSplitOptions.None).Length - 1 + "";
            metadata["has_bridge"] = content.Contains("[Bridge]").ToString();
        }

        metadata["word_count"] = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString();
        metadata["character_count"] = content.Length.ToString();

        return metadata;
    }

    #endregion

    #region Statistics

    public BrainStats GetStats()
    {
        return new BrainStats
        {
            TotalDecisions = _totalDecisions,
            SuccessfulDecisions = _successfulDecisions,
            SuccessRate = _totalDecisions > 0 ? (double)_successfulDecisions / _totalDecisions : 0,
            LastUsed = _lastUsed,
            CurrentModel = _ollama.CurrentModel
        };
    }

    #endregion
}

#region Enums and Models

public enum BrainCapability
{
    ContentGeneration,
    WorkflowLearning,
    ElementAnalysis,
    CreativeContent,
    DecisionMaking,
    ErrorRecovery,
    ContextAwareness
}

public enum CreativeType
{
    Lyrics,
    AdCopy,
    Script,
    Story
}

public class ContentRequest
{
    public string Topic { get; set; } = "";
    public string Platform { get; set; } = "Facebook";
    public BrandInfo? BrandInfo { get; set; }
    public string? TargetAudience { get; set; }
    public string Language { get; set; } = "th";
    public bool IncludeHashtags { get; set; } = true;
    public int MaxLength { get; set; }
}

public class BrandInfo
{
    public string Name { get; set; } = "";
    public string Tone { get; set; } = "Friendly";
    public string? Description { get; set; }
    public List<string> Keywords { get; set; } = new();
}

public class ContentResult
{
    public string Text { get; set; } = "";
    public string Platform { get; set; } = "";
    public List<string> Hashtags { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public string Model { get; set; } = "";
}

public class CreativeRequest
{
    public CreativeType Type { get; set; }
    public string Topic { get; set; } = "";
    public string? Style { get; set; }
    public string? Mood { get; set; }
    public string? Language { get; set; }
    public string? Platform { get; set; }
    public string? TargetAudience { get; set; }
    public string? Goal { get; set; }
    public string? Duration { get; set; }
    public string? Length { get; set; }
    public string? AdditionalInstructions { get; set; }
    public string Prompt { get; set; } = "";
}

public class CreativeResult
{
    public string Content { get; set; } = "";
    public CreativeType Type { get; set; }
    public DateTime GeneratedAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class DecisionRequest
{
    public string Context { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public List<string> Constraints { get; set; } = new();
    public string Goal { get; set; } = "";
}

public class DecisionResult
{
    public string Decision { get; set; } = "";
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = "";
    public List<string> Alternatives { get; set; } = new();
    public List<string> Risks { get; set; } = new();
    public string? RawResponse { get; set; }
}

public class ErrorContext
{
    public string ErrorType { get; set; } = "";
    public string Message { get; set; } = "";
    public string FailedStep { get; set; } = "";
    public string? CurrentUrl { get; set; }
    public string? HtmlSnippet { get; set; }
    public string? Screenshot { get; set; }
}

public class RecoveryResult
{
    public string ErrorType { get; set; } = "";
    public string RootCause { get; set; } = "";
    public RecoverySolution? Solution { get; set; }
    public double Confidence { get; set; }
    public string? RawResponse { get; set; }
}

public class RecoverySolution
{
    public string Action { get; set; } = "";
    public SelectorInfo? NewSelector { get; set; }
    public int WaitAdjustment { get; set; }
}

public class SelectorInfo
{
    public string Type { get; set; } = "";
    public string Value { get; set; } = "";
    public double Confidence { get; set; }
}

public class ElementAnalysisResult
{
    public string Element { get; set; } = "";
    public List<SelectorInfo> Selectors { get; set; } = new();
    public string Action { get; set; } = "";
    public int WaitBeforeMs { get; set; }
    public string? RawResponse { get; set; }
}

public class RecordedAction
{
    public string Action { get; set; } = "";
    public string? Selector { get; set; }
    public string? Value { get; set; }
    public string? Url { get; set; }
    public DateTime Timestamp { get; set; }
}

public class WorkflowLearningResult
{
    public string WorkflowName { get; set; } = "";
    public string Platform { get; set; } = "";
    public List<LearnedStep> Steps { get; set; } = new();
    public string? RawResponse { get; set; }
}

public class LearnedStep
{
    public int Order { get; set; }
    public string Action { get; set; } = "";
    public string? Description { get; set; }
    public SelectorInfo? Selector { get; set; }
    public List<SelectorInfo> AlternativeSelectors { get; set; } = new();
    public int WaitBeforeMs { get; set; }
    public int WaitAfterMs { get; set; }
    public string? SuccessCondition { get; set; }
    public bool IsOptional { get; set; }
}

public class BrainStats
{
    public int TotalDecisions { get; set; }
    public int SuccessfulDecisions { get; set; }
    public double SuccessRate { get; set; }
    public DateTime LastUsed { get; set; }
    public string CurrentModel { get; set; } = "";
}

#endregion
