using System.Text.Json;
using System.Text.RegularExpressions;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// บริการ AI-Guided Setup สำหรับสอนการสมัคร GPU Provider
/// วิเคราะห์หน้าเว็บและแนะนำขั้นตอนที่ต้องทำ
/// </summary>
public class GpuSetupGuideService
{
    private readonly ILogger<GpuSetupGuideService>? _logger;
    private readonly ContentGeneratorService? _contentGenerator;

    private GpuProviderType? _currentProvider;
    private ProviderSetupFlow? _currentFlow;
    private int _currentStepIndex;
    private SetupSession? _activeSession;

    // Events
    public event Action<SetupGuidanceMessage>? OnGuidanceMessage;
    public event Action<SetupStep>? OnStepCompleted;
    public event Action<GpuProviderType>? OnSetupCompleted;

    public GpuSetupGuideService(
        ILogger<GpuSetupGuideService>? logger = null,
        ContentGeneratorService? contentGenerator = null)
    {
        _logger = logger;
        _contentGenerator = contentGenerator;
    }

    #region Setup Flow Management

    /// <summary>
    /// เริ่ม Setup Flow สำหรับ Provider
    /// </summary>
    public SetupSession StartSetup(GpuProviderType provider)
    {
        _currentProvider = provider;
        _currentFlow = ProviderSetupFlow.GetSetupFlow(provider);
        _currentStepIndex = 0;

        _activeSession = new SetupSession
        {
            Provider = provider,
            Flow = _currentFlow,
            StartedAt = DateTime.UtcNow
        };

        _logger?.LogInformation("Started setup for {Provider}", provider);

        // ส่งข้อความต้อนรับ
        var providerInfo = GpuProviderInfo.GetAllProviders()
            .FirstOrDefault(p => p.Type == provider);

        SendGuidance(SetupGuidanceType.Welcome,
            $"เริ่มการตั้งค่า {_currentFlow.ProviderName}",
            $"ขั้นตอนนี้จะใช้เวลาประมาณ {_currentFlow.EstimatedTime}\n" +
            $"สิ่งที่ต้องเตรียม: {string.Join(", ", _currentFlow.Requirements)}",
            providerInfo?.SignupUrl);

        return _activeSession;
    }

    /// <summary>
    /// ดึง Step ปัจจุบัน
    /// </summary>
    public SetupStep? GetCurrentStep()
    {
        if (_currentFlow == null || _currentStepIndex >= _currentFlow.Steps.Count)
            return null;

        return _currentFlow.Steps[_currentStepIndex];
    }

    /// <summary>
    /// ไปยัง Step ถัดไป
    /// </summary>
    public SetupStep? MoveToNextStep()
    {
        if (_currentFlow == null)
            return null;

        var completedStep = GetCurrentStep();
        if (completedStep != null)
        {
            OnStepCompleted?.Invoke(completedStep);
            _activeSession?.CompletedSteps.Add(completedStep.StepNumber);
        }

        _currentStepIndex++;

        if (_currentStepIndex >= _currentFlow.Steps.Count)
        {
            // Setup เสร็จสิ้น
            CompleteSetup();
            return null;
        }

        var nextStep = GetCurrentStep();
        if (nextStep != null)
        {
            SendGuidance(SetupGuidanceType.NextStep,
                $"ขั้นตอนที่ {nextStep.StepNumber}: {nextStep.Title}",
                nextStep.Description,
                nextStep.Url);
        }

        return nextStep;
    }

    /// <summary>
    /// ข้าม Step (สำหรับ optional steps)
    /// </summary>
    public SetupStep? SkipCurrentStep()
    {
        var currentStep = GetCurrentStep();
        if (currentStep?.IsOptional == true)
        {
            _activeSession?.SkippedSteps.Add(currentStep.StepNumber);
            return MoveToNextStep();
        }

        SendGuidance(SetupGuidanceType.Warning,
            "ไม่สามารถข้ามขั้นตอนนี้ได้",
            "ขั้นตอนนี้จำเป็นต้องทำเพื่อให้สามารถใช้งาน GPU ได้");

        return currentStep;
    }

    /// <summary>
    /// Setup เสร็จสิ้น
    /// </summary>
    private void CompleteSetup()
    {
        if (_currentProvider == null || _activeSession == null)
            return;

        _activeSession.CompletedAt = DateTime.UtcNow;
        _activeSession.IsCompleted = true;

        SendGuidance(SetupGuidanceType.Success,
            $"ตั้งค่า {_currentFlow?.ProviderName} สำเร็จ!",
            "สามารถเพิ่ม account เข้า GPU Pool ได้เลย");

        OnSetupCompleted?.Invoke(_currentProvider.Value);

        _logger?.LogInformation("Completed setup for {Provider}", _currentProvider);
    }

    /// <summary>
    /// ยกเลิก Setup
    /// </summary>
    public void CancelSetup()
    {
        if (_activeSession != null)
        {
            _activeSession.CancelledAt = DateTime.UtcNow;
        }

        _currentProvider = null;
        _currentFlow = null;
        _currentStepIndex = 0;
        _activeSession = null;

        _logger?.LogInformation("Setup cancelled");
    }

    #endregion

    #region Page Analysis (AI-Powered)

    /// <summary>
    /// วิเคราะห์หน้าเว็บและให้คำแนะนำ
    /// </summary>
    public async Task<PageAnalysisResult> AnalyzePageAsync(string url, string? pageContent = null)
    {
        var result = new PageAnalysisResult
        {
            Url = url,
            DetectedProvider = DetectProviderFromUrl(url)
        };

        // Pattern-based detection
        result.PageType = DetectPageType(url);
        result.CurrentStep = DetermineCurrentStep(url, pageContent);
        result.NextAction = GetRecommendedAction(url, pageContent);
        result.Tips = GetContextualTips(url, pageContent);
        result.IsLoggedIn = DetectLoginStatus(pageContent);

        // ถ้ามี AI service - ใช้ AI วิเคราะห์เพิ่มเติม
        if (_contentGenerator != null && !string.IsNullOrEmpty(pageContent))
        {
            try
            {
                var aiAnalysis = await AnalyzeWithAIAsync(url, pageContent);
                MergeAIAnalysis(result, aiAnalysis);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "AI analysis failed, using pattern-based analysis only");
            }
        }

        // ส่ง guidance message
        if (!string.IsNullOrEmpty(result.NextAction))
        {
            SendGuidance(SetupGuidanceType.Instruction,
                result.CurrentStep,
                result.NextAction,
                result.ActionTarget,
                result.Tips);
        }

        return result;
    }

    /// <summary>
    /// ตรวจจับ Provider จาก URL
    /// </summary>
    private GpuProviderType? DetectProviderFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return null;

        var urlLower = url.ToLowerInvariant();

        if (urlLower.Contains("colab.research.google") || urlLower.Contains("accounts.google.com"))
            return GpuProviderType.GoogleColab;

        if (urlLower.Contains("kaggle.com"))
            return GpuProviderType.Kaggle;

        if (urlLower.Contains("lightning.ai"))
            return GpuProviderType.LightningAI;

        if (urlLower.Contains("huggingface.co"))
            return GpuProviderType.HuggingFaceSpaces;

        if (urlLower.Contains("paperspace.com"))
            return GpuProviderType.PaperspaceGradient;

        if (urlLower.Contains("saturncloud.io") || urlLower.Contains("saturnenterprise.io"))
            return GpuProviderType.SaturnCloud;

        return null;
    }

    /// <summary>
    /// ตรวจจับประเภทหน้าเว็บ
    /// </summary>
    private string DetectPageType(string url)
    {
        var urlLower = url.ToLowerInvariant();

        // Login pages
        if (urlLower.Contains("/login") || urlLower.Contains("/signin") || urlLower.Contains("sign-in"))
            return "login";

        // Register pages
        if (urlLower.Contains("/register") || urlLower.Contains("/signup") || urlLower.Contains("sign-up") || urlLower.Contains("/join"))
            return "register";

        // Settings/Account pages
        if (urlLower.Contains("/settings") || urlLower.Contains("/account") || urlLower.Contains("/profile"))
            return "settings";

        // API Key pages
        if (urlLower.Contains("/api") || urlLower.Contains("/token") || urlLower.Contains("/credentials"))
            return "api_key";

        // Notebook/IDE pages
        if (urlLower.Contains("/notebook") || urlLower.Contains("/code") || urlLower.Contains("/studio"))
            return "notebook";

        // Dashboard
        if (urlLower.Contains("/dashboard") || urlLower.Contains("/home"))
            return "dashboard";

        return "unknown";
    }

    /// <summary>
    /// กำหนดขั้นตอนปัจจุบันจาก URL และ content
    /// </summary>
    private string DetermineCurrentStep(string url, string? content)
    {
        var pageType = DetectPageType(url);
        var provider = DetectProviderFromUrl(url);

        return pageType switch
        {
            "login" => "กำลังอยู่หน้า Login",
            "register" => "กำลังสมัครสมาชิก",
            "settings" => "กำลังอยู่หน้าตั้งค่า",
            "api_key" => "กำลังดึง API Key",
            "notebook" => "กำลังอยู่หน้า Notebook/IDE",
            "dashboard" => "กำลังอยู่หน้า Dashboard",
            _ => $"กำลังอยู่ที่ {provider?.ToString() ?? "หน้าเว็บ"}"
        };
    }

    /// <summary>
    /// ให้คำแนะนำการกระทำถัดไป
    /// </summary>
    private string GetRecommendedAction(string url, string? content)
    {
        var pageType = DetectPageType(url);
        var provider = DetectProviderFromUrl(url);

        // Google-specific recommendations
        if (url.Contains("accounts.google.com"))
        {
            if (url.Contains("/signin"))
                return "กรอก Email ของคุณแล้วกด 'ถัดไป'";
            if (url.Contains("/v3/signin/challenge"))
                return "กรอก Password แล้วกด 'ถัดไป'";
            if (url.Contains("/signup"))
                return "กรอกข้อมูลเพื่อสร้าง Google Account ใหม่";
        }

        // Kaggle-specific recommendations
        if (url.Contains("kaggle.com"))
        {
            if (pageType == "register")
                return "คลิก 'Register with Google' เพื่อสมัครด้วย Google Account หรือกรอกข้อมูลเพื่อสมัครใหม่";
            if (pageType == "settings")
                return "เลื่อนลงไปหา 'Phone Verification' เพื่อยืนยันเบอร์โทร และ 'API' เพื่อสร้าง Token";
        }

        // Lightning AI-specific
        if (url.Contains("lightning.ai"))
        {
            if (pageType == "register")
                return "คลิก 'Sign up with GitHub' หรือกรอก Email เพื่อสมัคร";
            if (pageType == "notebook" || url.Contains("/studios"))
                return "คลิก '+ New Studio' เพื่อสร้าง Studio ใหม่พร้อม GPU";
        }

        // General recommendations
        return pageType switch
        {
            "login" => "กรอก Email และ Password แล้วกด Sign In",
            "register" => "กรอกข้อมูลเพื่อสมัครสมาชิก หรือใช้ Social Login",
            "settings" => "ตรวจสอบการตั้งค่า Account และ API Keys",
            "api_key" => "คลิก 'Create New Token' หรือ 'Generate API Key' เพื่อสร้าง Key ใหม่",
            "notebook" => "คุณอยู่ในหน้า Notebook/IDE แล้ว - ลองรันโค้ดทดสอบ GPU",
            "dashboard" => "นี่คือหน้า Dashboard หลัก - ลองสร้าง Notebook ใหม่",
            _ => "สำรวจหน้าเว็บและดูตัวเลือกที่มี"
        };
    }

    /// <summary>
    /// ให้ tips ตาม context
    /// </summary>
    private List<string> GetContextualTips(string url, string? content)
    {
        var tips = new List<string>();
        var provider = DetectProviderFromUrl(url);
        var pageType = DetectPageType(url);

        // Provider-specific tips
        switch (provider)
        {
            case GpuProviderType.GoogleColab:
                tips.Add("🔋 Colab Free ให้ GPU ประมาณ 12 ชม./วัน");
                tips.Add("💡 ใช้ Runtime > Change runtime type เพื่อเปิด GPU");
                if (pageType == "notebook")
                    tips.Add("⚡ รัน !nvidia-smi เพื่อตรวจสอบ GPU");
                break;

            case GpuProviderType.Kaggle:
                tips.Add("📱 ต้องยืนยันเบอร์โทรก่อนใช้ GPU");
                tips.Add("⏰ GPU ใช้ได้ 30 ชม./สัปดาห์");
                tips.Add("🔑 API Key อยู่ที่ Settings > API > Create New Token");
                break;

            case GpuProviderType.LightningAI:
                tips.Add("⚡ A10G GPU มี VRAM 24GB");
                tips.Add("⏰ Free tier: 22 GPU-hours/เดือน");
                tips.Add("💡 สมัครด้วย GitHub สะดวกที่สุด");
                break;

            case GpuProviderType.HuggingFaceSpaces:
                tips.Add("🤗 ZeroGPU เป็น shared GPU - ไม่จำกัดเวลา");
                tips.Add("🔑 Token อยู่ที่ Settings > Access Tokens");
                break;
        }

        // General tips based on page type
        switch (pageType)
        {
            case "register":
                tips.Add("💡 ใช้ Social Login (Google/GitHub) จะเร็วกว่า");
                tips.Add("📧 ใช้ Email จริงเพราะต้องยืนยัน");
                break;

            case "api_key":
                tips.Add("🔐 เก็บ API Key ไว้ที่ปลอดภัย");
                tips.Add("❌ อย่าแชร์ Key กับใคร");
                break;
        }

        return tips;
    }

    /// <summary>
    /// ตรวจจับสถานะ Login จาก content
    /// </summary>
    private bool DetectLoginStatus(string? content)
    {
        if (string.IsNullOrEmpty(content)) return false;

        var loggedInPatterns = new[]
        {
            "sign out", "logout", "log out", "my account", "profile",
            "avatar", "user-menu", "account-menu"
        };

        var contentLower = content.ToLowerInvariant();
        return loggedInPatterns.Any(p => contentLower.Contains(p));
    }

    /// <summary>
    /// วิเคราะห์ด้วย AI (ถ้ามี ContentGeneratorService)
    /// </summary>
    private async Task<AIPageAnalysis?> AnalyzeWithAIAsync(string url, string pageContent)
    {
        if (_contentGenerator == null) return null;

        // ตัด content ให้สั้นลง (เอาแค่ส่วนสำคัญ)
        var truncatedContent = TruncateForAI(pageContent, 2000);

        var prompt = $@"
วิเคราะห์หน้าเว็บนี้และให้คำแนะนำเป็นภาษาไทย:

URL: {url}
Content (บางส่วน): {truncatedContent}

ตอบในรูปแบบ JSON:
{{
    ""page_type"": ""login|register|settings|api_key|notebook|dashboard|other"",
    ""is_logged_in"": true/false,
    ""current_step"": ""อธิบายสั้นๆ ว่าอยู่ขั้นตอนไหน"",
    ""next_action"": ""แนะนำการกระทำถัดไป"",
    ""action_target"": ""CSS selector หรือ text ของปุ่ม/ลิงก์ที่ควรคลิก (ถ้ามี)"",
    ""tips"": [""tip1"", ""tip2""]
}}
";

        try
        {
            // Use GenerateAsync instead of GenerateTextAsync
            var result = await _contentGenerator.GenerateAsync(
                prompt,
                null,  // No brand info
                "web", // Platform
                "th",  // Language
                CancellationToken.None);

            if (!string.IsNullOrEmpty(result?.Text))
            {
                // Parse JSON response
                var jsonMatch = Regex.Match(result.Text, @"\{[\s\S]*\}");
                if (jsonMatch.Success)
                {
                    return JsonSerializer.Deserialize<AIPageAnalysis>(jsonMatch.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse AI analysis response");
        }

        return null;
    }

    /// <summary>
    /// รวม AI analysis เข้ากับ result
    /// </summary>
    private void MergeAIAnalysis(PageAnalysisResult result, AIPageAnalysis? aiAnalysis)
    {
        if (aiAnalysis == null) return;

        // ใช้ AI analysis ถ้าดีกว่า pattern-based
        if (!string.IsNullOrEmpty(aiAnalysis.NextAction))
            result.NextAction = aiAnalysis.NextAction;

        if (!string.IsNullOrEmpty(aiAnalysis.CurrentStep))
            result.CurrentStep = aiAnalysis.CurrentStep;

        if (!string.IsNullOrEmpty(aiAnalysis.ActionTarget))
            result.ActionTarget = aiAnalysis.ActionTarget;

        if (aiAnalysis.Tips?.Any() == true)
            result.Tips.AddRange(aiAnalysis.Tips);

        result.IsLoggedIn = aiAnalysis.IsLoggedIn;
    }

    /// <summary>
    /// ตัด content ให้สั้นลงสำหรับ AI
    /// </summary>
    private string TruncateForAI(string content, int maxLength)
    {
        // ลบ script, style, comments
        content = Regex.Replace(content, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        content = Regex.Replace(content, @"<!--[\s\S]*?-->", "");

        // ลบ HTML tags
        content = Regex.Replace(content, @"<[^>]+>", " ");

        // ลบ whitespace ซ้ำ
        content = Regex.Replace(content, @"\s+", " ").Trim();

        if (content.Length > maxLength)
            content = content.Substring(0, maxLength) + "...";

        return content;
    }

    #endregion

    #region Guidance Messages

    /// <summary>
    /// ส่งข้อความแนะนำ
    /// </summary>
    private void SendGuidance(
        SetupGuidanceType type,
        string title,
        string message,
        string? actionUrl = null,
        List<string>? tips = null)
    {
        var guidance = new SetupGuidanceMessage
        {
            Type = type,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            Tips = tips ?? new(),
            Timestamp = DateTime.UtcNow
        };

        OnGuidanceMessage?.Invoke(guidance);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// ดึง Provider Info ทั้งหมด
    /// </summary>
    public List<GpuProviderInfo> GetAllProviders()
    {
        return GpuProviderInfo.GetAllProviders();
    }

    /// <summary>
    /// ดึง Setup Flow สำหรับ Provider
    /// </summary>
    public ProviderSetupFlow GetSetupFlow(GpuProviderType provider)
    {
        return ProviderSetupFlow.GetSetupFlow(provider);
    }

    /// <summary>
    /// ดึง Active Session
    /// </summary>
    public SetupSession? GetActiveSession() => _activeSession;

    /// <summary>
    /// ตรวจสอบว่ากำลัง Setup อยู่หรือไม่
    /// </summary>
    public bool IsSetupInProgress => _activeSession != null && !_activeSession.IsCompleted;

    #endregion

    #region AI Service Info (Ollama & Others)

    /// <summary>
    /// ดึงข้อมูล AI Service ทั้งหมดที่รองรับ
    /// </summary>
    public static List<AIServiceInfo> GetAllAIServices() => new()
    {
        new AIServiceInfo
        {
            Name = "Ollama",
            Description = "Local LLM - รันบนเครื่องของคุณ ฟรี ไม่จำกัด",
            Type = AIServiceType.Local,
            DefaultPort = 11434,
            BaseUrl = "http://localhost:11434",
            IsFree = true,
            RequiresApiKey = false,
            SetupUrl = "https://ollama.ai/download",
            DocumentationUrl = "https://github.com/ollama/ollama/blob/main/docs/api.md",
            Endpoints = new()
            {
                new AIEndpointInfo { Method = "GET", Path = "/", Description = "ตรวจสอบว่า Ollama ทำงานอยู่" },
                new AIEndpointInfo { Method = "GET", Path = "/api/tags", Description = "ดึงรายชื่อ models ที่ติดตั้ง" },
                new AIEndpointInfo { Method = "POST", Path = "/api/generate", Description = "สร้างข้อความ (completion)", RequestExample = @"{""model"": ""llama3.2"", ""prompt"": ""Hello""}" },
                new AIEndpointInfo { Method = "POST", Path = "/api/chat", Description = "Chat แบบ multi-turn", RequestExample = @"{""model"": ""llama3.2"", ""messages"": [{""role"": ""user"", ""content"": ""Hi""}]}" },
                new AIEndpointInfo { Method = "POST", Path = "/api/embeddings", Description = "สร้าง embeddings", RequestExample = @"{""model"": ""nomic-embed-text"", ""prompt"": ""Hello""}" },
                new AIEndpointInfo { Method = "POST", Path = "/api/pull", Description = "ดาวน์โหลด model", RequestExample = @"{""name"": ""llama3.2""}" },
                new AIEndpointInfo { Method = "DELETE", Path = "/api/delete", Description = "ลบ model", RequestExample = @"{""name"": ""llama3.2""}" },
                new AIEndpointInfo { Method = "POST", Path = "/api/show", Description = "ดูข้อมูล model", RequestExample = @"{""name"": ""llama3.2""}" },
                new AIEndpointInfo { Method = "GET", Path = "/api/ps", Description = "ดู models ที่กำลังรันอยู่" }
            },
            SetupSteps = new()
            {
                "1. ดาวน์โหลด Ollama จาก https://ollama.ai/download",
                "2. ติดตั้งและรัน Ollama",
                "3. เปิด terminal รัน: ollama pull llama3.2",
                "4. ทดสอบ: ollama run llama3.2",
                "5. API พร้อมใช้ที่ http://localhost:11434"
            },
            RecommendedModels = new()
            {
                new AIModelInfo { Name = "llama3.2", Size = "2GB", Description = "Meta Llama 3.2 - เร็ว ประหยัด RAM" },
                new AIModelInfo { Name = "llama3.2:3b", Size = "2GB", Description = "Llama 3.2 3B - balanced" },
                new AIModelInfo { Name = "mistral", Size = "4GB", Description = "Mistral 7B - ดีสำหรับ coding" },
                new AIModelInfo { Name = "codellama", Size = "4GB", Description = "Code Llama - เชี่ยวชาญ code" },
                new AIModelInfo { Name = "nomic-embed-text", Size = "274MB", Description = "สำหรับ embeddings" },
                new AIModelInfo { Name = "llava", Size = "4.7GB", Description = "Vision model - ดูรูปภาพได้" }
            }
        },
        new AIServiceInfo
        {
            Name = "Google Gemini",
            Description = "Google AI - มี Free tier 15 requests/min",
            Type = AIServiceType.Cloud,
            BaseUrl = "https://generativelanguage.googleapis.com",
            IsFree = true,
            RequiresApiKey = true,
            ApiKeyUrl = "https://aistudio.google.com/app/apikey",
            DocumentationUrl = "https://ai.google.dev/docs",
            Endpoints = new()
            {
                new AIEndpointInfo { Method = "POST", Path = "/v1beta/models/gemini-pro:generateContent", Description = "สร้างข้อความ" },
                new AIEndpointInfo { Method = "POST", Path = "/v1beta/models/gemini-pro-vision:generateContent", Description = "วิเคราะห์รูปภาพ" },
                new AIEndpointInfo { Method = "GET", Path = "/v1beta/models", Description = "ดึงรายชื่อ models" }
            },
            SetupSteps = new()
            {
                "1. ไปที่ https://aistudio.google.com/app/apikey",
                "2. Login ด้วย Google Account",
                "3. คลิก 'Create API Key'",
                "4. Copy key มาใส่ในการตั้งค่า"
            }
        },
        new AIServiceInfo
        {
            Name = "OpenAI",
            Description = "ChatGPT API - เสียเงิน แต่คุณภาพสูง",
            Type = AIServiceType.Cloud,
            BaseUrl = "https://api.openai.com",
            IsFree = false,
            RequiresApiKey = true,
            ApiKeyUrl = "https://platform.openai.com/api-keys",
            DocumentationUrl = "https://platform.openai.com/docs",
            Endpoints = new()
            {
                new AIEndpointInfo { Method = "POST", Path = "/v1/chat/completions", Description = "Chat completion" },
                new AIEndpointInfo { Method = "POST", Path = "/v1/embeddings", Description = "สร้าง embeddings" },
                new AIEndpointInfo { Method = "POST", Path = "/v1/images/generations", Description = "สร้างรูปภาพ (DALL-E)" },
                new AIEndpointInfo { Method = "GET", Path = "/v1/models", Description = "ดึงรายชื่อ models" }
            },
            SetupSteps = new()
            {
                "1. ไปที่ https://platform.openai.com/api-keys",
                "2. Login หรือสมัครสมาชิก",
                "3. คลิก 'Create new secret key'",
                "4. Copy key มาใส่ในการตั้งค่า",
                "5. เติมเงิน (ต้องใช้บัตรเครดิต)"
            }
        },
        new AIServiceInfo
        {
            Name = "Anthropic Claude",
            Description = "Claude API - เสียเงิน คุณภาพสูงมาก",
            Type = AIServiceType.Cloud,
            BaseUrl = "https://api.anthropic.com",
            IsFree = false,
            RequiresApiKey = true,
            ApiKeyUrl = "https://console.anthropic.com/settings/keys",
            DocumentationUrl = "https://docs.anthropic.com",
            Endpoints = new()
            {
                new AIEndpointInfo { Method = "POST", Path = "/v1/messages", Description = "Chat completion" }
            },
            SetupSteps = new()
            {
                "1. ไปที่ https://console.anthropic.com",
                "2. สมัครสมาชิกและยืนยัน email",
                "3. ไปที่ Settings > API Keys",
                "4. สร้าง key ใหม่และ copy มาใส่"
            }
        },
        new AIServiceInfo
        {
            Name = "HuggingFace Inference",
            Description = "Hosted models - มี Free tier",
            Type = AIServiceType.Cloud,
            BaseUrl = "https://api-inference.huggingface.co",
            IsFree = true,
            RequiresApiKey = true,
            ApiKeyUrl = "https://huggingface.co/settings/tokens",
            DocumentationUrl = "https://huggingface.co/docs/api-inference",
            Endpoints = new()
            {
                new AIEndpointInfo { Method = "POST", Path = "/models/{model_id}", Description = "Inference ตาม model" }
            },
            SetupSteps = new()
            {
                "1. ไปที่ https://huggingface.co/settings/tokens",
                "2. Login หรือสมัครสมาชิก",
                "3. สร้าง Access Token (Read)",
                "4. Copy token มาใส่ในการตั้งค่า"
            }
        }
    };

    /// <summary>
    /// ดึงข้อมูล Ollama API โดยเฉพาะ
    /// </summary>
    public static AIServiceInfo GetOllamaInfo()
    {
        return GetAllAIServices().First(s => s.Name == "Ollama");
    }

    /// <summary>
    /// ตรวจสอบสถานะ Ollama
    /// </summary>
    public async Task<OllamaConnectionStatus> CheckOllamaStatusAsync(string baseUrl = "http://localhost:11434")
    {
        var status = new OllamaConnectionStatus { BaseUrl = baseUrl };

        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            // Check if running
            var response = await client.GetAsync(baseUrl);
            status.IsRunning = response.IsSuccessStatusCode;

            if (status.IsRunning)
            {
                // Get installed models
                var tagsResponse = await client.GetAsync($"{baseUrl}/api/tags");
                if (tagsResponse.IsSuccessStatusCode)
                {
                    var json = await tagsResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var models))
                    {
                        status.InstalledModels = models.EnumerateArray()
                            .Select(m => m.GetProperty("name").GetString() ?? "")
                            .ToList();
                    }
                }

                // Get running models
                var psResponse = await client.GetAsync($"{baseUrl}/api/ps");
                if (psResponse.IsSuccessStatusCode)
                {
                    var json = await psResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("models", out var models))
                    {
                        status.RunningModels = models.EnumerateArray()
                            .Select(m => m.GetProperty("name").GetString() ?? "")
                            .ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            status.IsRunning = false;
            status.Error = ex.Message;
            _logger?.LogWarning(ex, "Failed to check Ollama status");
        }

        return status;
    }

    /// <summary>
    /// ดึงคำแนะนำการตั้งค่า Ollama
    /// </summary>
    public static string GetOllamaSetupGuide()
    {
        return @"
═══════════════════════════════════════════════════════════════
                    OLLAMA SETUP GUIDE
═══════════════════════════════════════════════════════════════

1. ติดตั้ง Ollama
   • Windows: ดาวน์โหลดจาก https://ollama.ai/download
   • Mac: brew install ollama
   • Linux: curl -fsSL https://ollama.ai/install.sh | sh

2. รัน Ollama Service
   • Windows: เปิด Ollama จาก Start Menu (จะรันเป็น tray icon)
   • Mac/Linux: ollama serve

3. ดาวน์โหลด Model
   • ollama pull llama3.2          (เร็ว, 2GB)
   • ollama pull mistral           (coding, 4GB)
   • ollama pull codellama         (code เฉพาะ, 4GB)
   • ollama pull nomic-embed-text  (embeddings, 274MB)

4. ทดสอบ
   • ollama run llama3.2
   • curl http://localhost:11434/api/tags

═══════════════════════════════════════════════════════════════
                      API ENDPOINTS
═══════════════════════════════════════════════════════════════

Base URL: http://localhost:11434

┌─────────┬─────────────────────┬──────────────────────────────┐
│ Method  │ Endpoint            │ Description                  │
├─────────┼─────────────────────┼──────────────────────────────┤
│ GET     │ /                   │ Check if running             │
│ GET     │ /api/tags           │ List installed models        │
│ GET     │ /api/ps             │ List running models          │
│ POST    │ /api/generate       │ Generate text (completion)   │
│ POST    │ /api/chat           │ Chat (multi-turn)            │
│ POST    │ /api/embeddings     │ Create embeddings            │
│ POST    │ /api/pull           │ Download model               │
│ DELETE  │ /api/delete         │ Delete model                 │
│ POST    │ /api/show           │ Show model info              │
└─────────┴─────────────────────┴──────────────────────────────┘

═══════════════════════════════════════════════════════════════
                     EXAMPLE REQUESTS
═══════════════════════════════════════════════════════════════

# Generate text
curl http://localhost:11434/api/generate -d '{
  ""model"": ""llama3.2"",
  ""prompt"": ""Why is the sky blue?"",
  ""stream"": false
}'

# Chat
curl http://localhost:11434/api/chat -d '{
  ""model"": ""llama3.2"",
  ""messages"": [
    {""role"": ""user"", ""content"": ""Hello!""}
  ],
  ""stream"": false
}'

# Embeddings
curl http://localhost:11434/api/embeddings -d '{
  ""model"": ""nomic-embed-text"",
  ""prompt"": ""Hello world""
}'

═══════════════════════════════════════════════════════════════
";
    }

    #endregion
}

#region Supporting Classes

#region AI Service Models

/// <summary>
/// ประเภท AI Service
/// </summary>
public enum AIServiceType
{
    Local,  // รันบนเครื่อง (Ollama)
    Cloud   // Cloud API (OpenAI, Gemini, etc.)
}

/// <summary>
/// ข้อมูล AI Service
/// </summary>
public class AIServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AIServiceType Type { get; set; }
    public int? DefaultPort { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public bool IsFree { get; set; }
    public bool RequiresApiKey { get; set; }
    public string? ApiKeyUrl { get; set; }
    public string? SetupUrl { get; set; }
    public string? DocumentationUrl { get; set; }
    public List<AIEndpointInfo> Endpoints { get; set; } = new();
    public List<string> SetupSteps { get; set; } = new();
    public List<AIModelInfo> RecommendedModels { get; set; } = new();
}

/// <summary>
/// ข้อมูล API Endpoint
/// </summary>
public class AIEndpointInfo
{
    public string Method { get; set; } = "GET";
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? RequestExample { get; set; }
    public string? ResponseExample { get; set; }
}

/// <summary>
/// ข้อมูล AI Model
/// </summary>
public class AIModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Capabilities { get; set; } = new();
}

/// <summary>
/// สถานะการเชื่อมต่อ Ollama (แยกจาก OllamaStatus enum ใน OllamaServiceManager)
/// </summary>
public class OllamaConnectionStatus
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public bool IsRunning { get; set; }
    public List<string> InstalledModels { get; set; } = new();
    public List<string> RunningModels { get; set; } = new();
    public string? Error { get; set; }
}

#endregion

/// <summary>
/// Session การ Setup
/// </summary>
public class SetupSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public GpuProviderType Provider { get; set; }
    public ProviderSetupFlow? Flow { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public bool IsCompleted { get; set; }
    public List<int> CompletedSteps { get; set; } = new();
    public List<int> SkippedSteps { get; set; } = new();
}

/// <summary>
/// ข้อความแนะนำจาก Guide
/// </summary>
public class SetupGuidanceMessage
{
    public SetupGuidanceType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public List<string> Tips { get; set; } = new();
    public string? HighlightSelector { get; set; }
    public DateTime Timestamp { get; set; }
}

public enum SetupGuidanceType
{
    Welcome,
    Instruction,
    NextStep,
    Success,
    Warning,
    Error,
    Tip
}

/// <summary>
/// AI Page Analysis Result (for JSON deserialization)
/// </summary>
internal class AIPageAnalysis
{
    [System.Text.Json.Serialization.JsonPropertyName("page_type")]
    public string? PageType { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("is_logged_in")]
    public bool IsLoggedIn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("current_step")]
    public string? CurrentStep { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("next_action")]
    public string? NextAction { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("action_target")]
    public string? ActionTarget { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("tips")]
    public List<string>? Tips { get; set; }
}

#endregion
