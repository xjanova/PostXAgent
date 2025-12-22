using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// AI Content Generation Service
/// Supports multiple providers: Ollama (Local), Google Gemini (Free), OpenAI, Anthropic, HuggingFace
/// </summary>
public class ContentGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly AIConfig _config;
    private readonly OllamaCpuOptimizer _cpuOptimizer;
    private readonly OllamaCpuOptimizer.CpuConfiguration _cpuConfig;
    private static bool _cpuOptimizationApplied = false;

    public ContentGeneratorService()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(10); // เพิ่ม timeout สำหรับ CPU inference
        _config = AIConfig.Load();
        _cpuOptimizer = new OllamaCpuOptimizer();
        _cpuConfig = _cpuOptimizer.DetectAndOptimize();

        // Apply CPU optimization เมื่อสร้าง service ครั้งแรก
        if (!_cpuOptimizationApplied)
        {
            _cpuOptimizer.ApplyToCurrentProcess(_cpuConfig);
            _cpuOptimizationApplied = true;
        }
    }

    public async Task<GeneratedContent> GenerateAsync(
        string prompt,
        BrandInfo? brandInfo,
        string platform,
        string language,
        CancellationToken ct)
    {
        // Try providers in order of preference (free first)
        var providers = new (string name, Func<string, BrandInfo?, string, string, CancellationToken, Task<GeneratedContent?>> generator)[]
        {
            ("ollama", GenerateWithOllamaAsync),
            ("google", GenerateWithGeminiAsync),
            ("huggingface", GenerateWithHuggingFaceAsync),
            ("openai", GenerateWithOpenAIAsync),
            ("anthropic", GenerateWithClaudeAsync),
        };

        foreach (var (name, generator) in providers)
        {
            try
            {
                var result = await generator(prompt, brandInfo, platform, language, ct);
                if (result != null)
                {
                    result.Provider = name;
                    return result;
                }
            }
            catch
            {
                // Try next provider
            }
        }

        throw new Exception("All AI providers failed. Please configure at least one provider.");
    }

    /// <summary>
    /// ตรวจสอบสถานะ providers ทั้งหมด
    /// </summary>
    public async Task<List<ProviderStatus>> CheckProvidersAsync(CancellationToken ct = default)
    {
        var results = new List<ProviderStatus>();

        // Check Ollama
        try
        {
            var response = await _httpClient.GetAsync($"{_config.OllamaBaseUrl}/api/tags", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var models = json.GetProperty("models");
                var modelCount = models.GetArrayLength();
                results.Add(new ProviderStatus
                {
                    Name = "Ollama",
                    IsAvailable = true,
                    IsFree = true,
                    Note = $"{modelCount} models installed"
                });
            }
            else
            {
                results.Add(new ProviderStatus { Name = "Ollama", IsAvailable = false, IsFree = true, Note = "Not running" });
            }
        }
        catch
        {
            results.Add(new ProviderStatus { Name = "Ollama", IsAvailable = false, IsFree = true, Note = "Not installed" });
        }

        // Check Google Gemini
        results.Add(new ProviderStatus
        {
            Name = "Google Gemini",
            IsAvailable = !string.IsNullOrEmpty(_config.GoogleApiKey),
            IsFree = true,
            Note = string.IsNullOrEmpty(_config.GoogleApiKey) ? "Set GOOGLE_API_KEY" : "Ready"
        });

        // Check HuggingFace
        var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
        results.Add(new ProviderStatus
        {
            Name = "HuggingFace",
            IsAvailable = !string.IsNullOrEmpty(hfToken),
            IsFree = true,
            Note = string.IsNullOrEmpty(hfToken) ? "Set HF_TOKEN" : "Ready"
        });

        // Check OpenAI
        results.Add(new ProviderStatus
        {
            Name = "OpenAI GPT-4o",
            IsAvailable = !string.IsNullOrEmpty(_config.OpenAIApiKey),
            IsFree = false,
            Note = string.IsNullOrEmpty(_config.OpenAIApiKey) ? "Set OPENAI_API_KEY" : "Ready"
        });

        // Check Anthropic
        results.Add(new ProviderStatus
        {
            Name = "Anthropic Claude",
            IsAvailable = !string.IsNullOrEmpty(_config.AnthropicApiKey),
            IsFree = false,
            Note = string.IsNullOrEmpty(_config.AnthropicApiKey) ? "Set ANTHROPIC_API_KEY" : "Ready"
        });

        return results;
    }

    /// <summary>
    /// รับรายชื่อ providers ที่พร้อมใช้งาน
    /// </summary>
    public async Task<List<string>> GetAvailableProvidersAsync(CancellationToken ct = default)
    {
        var statuses = await CheckProvidersAsync(ct);
        return statuses.Where(s => s.IsAvailable).Select(s => s.Name).ToList();
    }

    private async Task<GeneratedContent?> GenerateWithOpenAIAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.OpenAIApiKey)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);

        // ใช้ GPT-4o (ใหม่สุด ถูกกว่า) หรือ GPT-4o-mini (ถูกมาก) เป็น fallback
        var models = new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo" };

        foreach (var model in models)
        {
            try
            {
                var request = new
                {
                    model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7,
                    max_tokens = 1000
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.OpenAIApiKey}");

                var response = await _httpClient.PostAsJsonAsync(
                    "https://api.openai.com/v1/chat/completions",
                    request, ct);

                if (!response.IsSuccessStatusCode) continue;

                var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

                if (result.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0)
                {
                    var content = choices[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return ParseContent(content ?? "");
                }
            }
            catch
            {
                // Try next model
            }
        }

        return null;
    }

    private async Task<GeneratedContent?> GenerateWithClaudeAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.AnthropicApiKey)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);

        // ใช้ Claude 3.5 Sonnet (คุ้มค่าที่สุด) หรือ Claude 3 Haiku (ถูกที่สุด) เป็น fallback
        var models = new[]
        {
            "claude-sonnet-4-20250514",      // Claude Sonnet 4 (ใหม่สุด)
            "claude-3-5-sonnet-20241022",    // Claude 3.5 Sonnet (คุ้มค่า)
            "claude-3-5-haiku-20241022",     // Claude 3.5 Haiku (ถูก)
            "claude-3-haiku-20240307"        // Claude 3 Haiku (ถูกที่สุด)
        };

        foreach (var model in models)
        {
            try
            {
                var request = new
                {
                    model,
                    max_tokens = 1000,
                    system = systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _config.AnthropicApiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

                var response = await _httpClient.PostAsJsonAsync(
                    "https://api.anthropic.com/v1/messages",
                    request, ct);

                if (!response.IsSuccessStatusCode) continue;

                var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

                if (result.TryGetProperty("content", out var contentArray) &&
                    contentArray.GetArrayLength() > 0)
                {
                    var content = contentArray[0]
                        .GetProperty("text")
                        .GetString();

                    return ParseContent(content ?? "");
                }
            }
            catch
            {
                // Try next model
            }
        }

        return null;
    }

    private async Task<GeneratedContent?> GenerateWithGeminiAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.GoogleApiKey)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);

        // ใช้ Gemini 2.0 Flash (ใหม่สุด ฟรี) หรือ 1.5 Flash เป็น fallback
        var models = new[] { "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro", "gemini-pro" };

        foreach (var model in models)
        {
            try
            {
                var request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[]
                            {
                                new { text = $"{systemPrompt}\n\n{prompt}" }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.7,
                        maxOutputTokens = 1000
                    }
                };

                _httpClient.DefaultRequestHeaders.Clear();

                var response = await _httpClient.PostAsJsonAsync(
                    $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_config.GoogleApiKey}",
                    request, ct);

                if (!response.IsSuccessStatusCode) continue;

                var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

                if (result.TryGetProperty("candidates", out var candidates) &&
                    candidates.GetArrayLength() > 0)
                {
                    var content = candidates[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return ParseContent(content ?? "");
                }
            }
            catch
            {
                // Try next model
            }
        }

        return null;
    }

    /// <summary>
    /// สร้างเนื้อหาด้วย HuggingFace Inference API (FREE)
    /// </summary>
    private async Task<GeneratedContent?> GenerateWithHuggingFaceAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
        if (string.IsNullOrEmpty(hfToken)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);
        var fullPrompt = $"{systemPrompt}\n\nUser: {prompt}\n\nAssistant:";

        // ใช้ Mistral หรือ Llama models ฟรีบน HuggingFace
        var models = new[]
        {
            "mistralai/Mistral-7B-Instruct-v0.3",
            "meta-llama/Meta-Llama-3-8B-Instruct",
            "HuggingFaceH4/zephyr-7b-beta"
        };

        foreach (var modelId in models)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api-inference.huggingface.co/models/{modelId}");

                request.Headers.Add("Authorization", $"Bearer {hfToken}");
                request.Content = JsonContent.Create(new
                {
                    inputs = fullPrompt,
                    parameters = new
                    {
                        max_new_tokens = 500,
                        temperature = 0.7,
                        do_sample = true,
                        return_full_text = false
                    }
                });

                var response = await _httpClient.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(ct);
                    // Model loading - wait and retry
                    if (error.Contains("loading"))
                    {
                        await Task.Delay(10000, ct);
                        response = await _httpClient.SendAsync(request, ct);
                        if (!response.IsSuccessStatusCode) continue;
                    }
                    else
                    {
                        continue;
                    }
                }

                var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

                string? generatedText = null;
                if (result.ValueKind == JsonValueKind.Array && result.GetArrayLength() > 0)
                {
                    generatedText = result[0].GetProperty("generated_text").GetString();
                }
                else if (result.TryGetProperty("generated_text", out var textProp))
                {
                    generatedText = textProp.GetString();
                }

                if (!string.IsNullOrEmpty(generatedText))
                {
                    return ParseContent(generatedText);
                }
            }
            catch
            {
                // Try next model
            }
        }

        return null;
    }

    private async Task<GeneratedContent?> GenerateWithOllamaAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt(brand, platform, language);
        var effectiveModel = _config.GetEffectiveModel();

        // สร้าง options จาก CPU optimization
        var cpuOptions = _cpuOptimizer.GetModelfileOptions(_cpuConfig);

        // สำหรับ qwen3 models ต้องปิด thinking mode โดยเพิ่ม /no_think
        var isQwenModel = effectiveModel.Contains("qwen", StringComparison.OrdinalIgnoreCase);
        var finalPrompt = isQwenModel ? prompt + " /no_think" : prompt;
        var finalSystemPrompt = isQwenModel ? systemPrompt + " /no_think" : systemPrompt;

        var request = new
        {
            model = effectiveModel,
            messages = new[]
            {
                new { role = "system", content = finalSystemPrompt },
                new { role = "user", content = finalPrompt }
            },
            stream = false,
            // CPU Optimization Options
            options = new
            {
                num_thread = cpuOptions["num_thread"],
                num_ctx = cpuOptions["num_ctx"],
                num_batch = cpuOptions["num_batch"],
                num_gpu = cpuOptions["num_gpu"],
                use_mmap = cpuOptions["use_mmap"],
                use_mlock = cpuOptions["use_mlock"]
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.OllamaBaseUrl}/api/chat",
                request, ct);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            // Get content from response
            var content = result.GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // For qwen3, if content is empty, try to get from thinking field
            if (string.IsNullOrWhiteSpace(content) && isQwenModel)
            {
                if (result.GetProperty("message").TryGetProperty("thinking", out var thinkingProp))
                {
                    var thinking = thinkingProp.GetString() ?? "";
                    // If there's thinking but no content, the model failed to produce output
                    // Try fallback to llama model
                    return await GenerateWithFallbackModelAsync(prompt, brand, platform, language, ct);
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return await GenerateWithFallbackModelAsync(prompt, brand, platform, language, ct);
            }

            return ParseContent(content);
        }
        catch
        {
            return null; // Ollama not available
        }
    }

    /// <summary>
    /// Fallback to llama3.2:3b when primary model fails
    /// </summary>
    private async Task<GeneratedContent?> GenerateWithFallbackModelAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt(brand, platform, language);
        var cpuOptions = _cpuOptimizer.GetModelfileOptions(_cpuConfig);

        // Try llama3.2:3b as fallback
        var request = new
        {
            model = "llama3.2:3b",
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = prompt }
            },
            stream = false,
            options = new
            {
                num_thread = cpuOptions["num_thread"],
                num_ctx = cpuOptions["num_ctx"],
                num_batch = cpuOptions["num_batch"],
                num_gpu = cpuOptions["num_gpu"],
                use_mmap = cpuOptions["use_mmap"],
                use_mlock = cpuOptions["use_mlock"]
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_config.OllamaBaseUrl}/api/chat",
                request, ct);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var content = result.GetProperty("message")
                .GetProperty("content")
                .GetString();

            return ParseContent(content ?? "");
        }
        catch
        {
            return null;
        }
    }

    private string BuildSystemPrompt(BrandInfo? brand, string platform, string language)
    {
        if (language == "th")
        {
            return $@"คุณคือผู้เชี่ยวชาญด้านการสร้างเนื้อหา Social Media สำหรับ {platform}
สร้างเนื้อหาโปรโมทที่น่าสนใจและเหมาะกับแพลตฟอร์ม

แบรนด์: {brand?.Name ?? "ไม่ระบุ"}
ธุรกิจ: {brand?.Industry ?? "ทั่วไป"}
กลุ่มเป้าหมาย: {brand?.TargetAudience ?? "ทั่วไป"}
โทน: {brand?.Tone ?? "Professional"}

กฎ:
1. ตอบเป็นภาษาไทยเท่านั้น
2. สร้างเนื้อหาสั้นกระชับ เหมาะกับ social media
3. ใส่ hashtag ที่เกี่ยวข้องท้ายโพสต์
4. ห้ามใช้ markdown formatting
5. ตอบเฉพาะเนื้อหาโพสต์ ไม่ต้องอธิบายเพิ่มเติม";
        }

        return $@"You are an expert social media content creator for {platform}.
Create engaging promotional content optimized for the platform.

Brand: {brand?.Name ?? "N/A"}
Industry: {brand?.Industry ?? "N/A"}
Target Audience: {brand?.TargetAudience ?? "General"}
Tone: {brand?.Tone ?? "Professional"}

Rules:
1. Respond in {language} only
2. Create concise content suitable for social media
3. Include relevant hashtags at the end
4. Do not use markdown formatting
5. Only output the post content, no explanations";
    }

    private GeneratedContent ParseContent(string content)
    {
        var lines = content.Split('\n');
        var textLines = new List<string>();
        var hashtags = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("#") && trimmed.Length < 50)
            {
                // Extract hashtags
                var tags = System.Text.RegularExpressions.Regex.Matches(trimmed, @"#(\w+)");
                foreach (System.Text.RegularExpressions.Match tag in tags)
                {
                    hashtags.Add(tag.Groups[1].Value);
                }
            }
            else
            {
                textLines.Add(line);
            }
        }

        return new GeneratedContent
        {
            Text = string.Join("\n", textLines).Trim(),
            Hashtags = hashtags.Distinct().ToList()
        };
    }
}

public class GeneratedContent
{
    public string Text { get; set; } = "";
    public List<string> Hashtags { get; set; } = new();
    public string Provider { get; set; } = "";
}

/// <summary>
/// สถานะของ AI Provider
/// </summary>
public class ProviderStatus
{
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set; }
    public bool IsFree { get; set; }
    public string Note { get; set; } = "";
}

public class AIConfig
{
    public string OpenAIApiKey { get; set; } = "";
    public string AnthropicApiKey { get; set; } = "";
    public string GoogleApiKey { get; set; } = "";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "auto";
    public bool AutoSelectModel { get; set; } = true;

    // CPU Optimization Settings
    public int OllamaNumThreads { get; set; } = 0;  // 0 = auto
    public int OllamaNumParallel { get; set; } = 0; // 0 = auto
    public int OllamaNumBatch { get; set; } = 0;    // 0 = auto
    public int OllamaNumCtx { get; set; } = 0;      // 0 = auto
    public int OllamaNumGpu { get; set; } = -1;     // -1 = auto, 0 = CPU only
    public bool OllamaUseMmap { get; set; } = true;
    public bool OllamaUseMlock { get; set; } = false;

    // Cached auto-selected model
    private static string? _cachedAutoModel;
    private static DateTime _cacheTime = DateTime.MinValue;

    /// <summary>
    /// Available Ollama models with Llama 4 variants
    /// </summary>
    public static readonly string[] RecommendedModels = new[]
    {
        "auto",              // Auto-select based on system resources
        "llama4:scout",      // Llama 4 Scout - Fast, 17B active params
        "llama4:maverick",   // Llama 4 Maverick - Best quality
        "llama4",            // Llama 4 default
        "llama3.3:70b",      // Llama 3.3 70B
        "llama3.2:3b",       // Llama 3.2 lightweight
        "llama3.1:8b",       // Llama 3.1 balanced
        "llama2",            // Legacy
        "mistral",           // Alternative
        "qwen2.5:7b"         // Multilingual
    };

    /// <summary>
    /// รับชื่อ Model ที่จะใช้งานจริง (resolve auto ถ้าจำเป็น)
    /// </summary>
    public string GetEffectiveModel()
    {
        if (OllamaModel != "auto" && !AutoSelectModel)
        {
            return OllamaModel;
        }

        // Use cached value if recent (5 minutes)
        if (_cachedAutoModel != null && (DateTime.UtcNow - _cacheTime).TotalMinutes < 5)
        {
            return _cachedAutoModel;
        }

        // Auto-select based on system resources
        var detector = new SystemResourceDetector();
        var recommendation = detector.GetBestModel();
        _cachedAutoModel = recommendation.ModelName;
        _cacheTime = DateTime.UtcNow;

        return _cachedAutoModel;
    }

    /// <summary>
    /// รับข้อมูล Model ที่แนะนำพร้อมเหตุผล
    /// </summary>
    public static (string model, string reason) GetRecommendedModel()
    {
        var detector = new SystemResourceDetector();
        var recommendation = detector.GetBestModel();
        return (recommendation.ModelName, recommendation.Reason);
    }

    /// <summary>
    /// รับข้อมูลทรัพยากรระบบ
    /// </summary>
    public static SystemResourceDetector.SystemResources GetSystemResources()
    {
        var detector = new SystemResourceDetector();
        return detector.DetectResources();
    }

    public static AIConfig Load()
    {
        var modelSetting = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "auto";
        var autoSelect = modelSetting == "auto" ||
                         string.Equals(Environment.GetEnvironmentVariable("OLLAMA_AUTO_SELECT"), "true", StringComparison.OrdinalIgnoreCase);

        return new AIConfig
        {
            OpenAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "",
            GoogleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "",
            OllamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434",
            OllamaModel = modelSetting,
            AutoSelectModel = autoSelect,

            // CPU Optimization - อ่านจาก environment variables
            OllamaNumThreads = ParseInt(Environment.GetEnvironmentVariable("OLLAMA_NUM_THREADS"), 0),
            OllamaNumParallel = ParseInt(Environment.GetEnvironmentVariable("OLLAMA_NUM_PARALLEL"), 0),
            OllamaNumBatch = ParseInt(Environment.GetEnvironmentVariable("OLLAMA_NUM_BATCH"), 0),
            OllamaNumCtx = ParseInt(Environment.GetEnvironmentVariable("OLLAMA_NUM_CTX"), 0),
            OllamaNumGpu = ParseInt(Environment.GetEnvironmentVariable("OLLAMA_NUM_GPU"), -1),
            OllamaUseMmap = !string.Equals(Environment.GetEnvironmentVariable("OLLAMA_USE_MMAP"), "false", StringComparison.OrdinalIgnoreCase),
            OllamaUseMlock = string.Equals(Environment.GetEnvironmentVariable("OLLAMA_USE_MLOCK"), "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static int ParseInt(string? value, int defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// รับ CPU Optimization summary
    /// </summary>
    public static string GetCpuOptimizationSummary()
    {
        var optimizer = new OllamaCpuOptimizer();
        var config = optimizer.DetectAndOptimize();
        return config.OptimizationReason;
    }
}
