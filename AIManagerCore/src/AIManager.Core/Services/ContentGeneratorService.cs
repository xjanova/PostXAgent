using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// AI Content Generation Service
/// Supports OpenAI, Anthropic, Google Gemini, and Ollama
/// </summary>
public class ContentGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly AIConfig _config;

    public ContentGeneratorService()
    {
        _httpClient = new HttpClient();
        _config = AIConfig.Load();
    }

    public async Task<GeneratedContent> GenerateAsync(
        string prompt,
        BrandInfo? brandInfo,
        string platform,
        string language,
        CancellationToken ct)
    {
        // Try providers in order of preference (free first)
        var providers = new[]
        {
            ("ollama", GenerateWithOllamaAsync),
            ("google", GenerateWithGeminiAsync),
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

        throw new Exception("All AI providers failed");
    }

    private async Task<GeneratedContent?> GenerateWithOpenAIAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.OpenAIApiKey)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);

        var request = new
        {
            model = "gpt-4-turbo-preview",
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

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = result.GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return ParseContent(content ?? "");
    }

    private async Task<GeneratedContent?> GenerateWithClaudeAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.AnthropicApiKey)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);

        var request = new
        {
            model = "claude-3-opus-20240229",
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

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = result.GetProperty("content")[0]
            .GetProperty("text")
            .GetString();

        return ParseContent(content ?? "");
    }

    private async Task<GeneratedContent?> GenerateWithGeminiAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.GoogleApiKey)) return null;

        var systemPrompt = BuildSystemPrompt(brand, platform, language);
        var fullPrompt = $"{systemPrompt}\n\n{prompt}";

        var request = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = fullPrompt } } }
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-pro:generateContent?key={_config.GoogleApiKey}",
            request, ct);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var content = result.GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        return ParseContent(content ?? "");
    }

    private async Task<GeneratedContent?> GenerateWithOllamaAsync(
        string prompt, BrandInfo? brand, string platform, string language, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt(brand, platform, language);

        var request = new
        {
            model = _config.OllamaModel,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = prompt }
            },
            stream = false
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
            return null; // Ollama not available
        }
    }

    private string BuildSystemPrompt(BrandInfo? brand, string platform, string language)
    {
        var langInstruction = language == "th" ? "ตอบเป็นภาษาไทย" : $"Respond in {language}";

        return $@"You are an expert social media content creator for {platform}.
Create engaging promotional content optimized for the platform.

Brand: {brand?.Name ?? "N/A"}
Industry: {brand?.Industry ?? "N/A"}
Target Audience: {brand?.TargetAudience ?? "General"}
Tone: {brand?.Tone ?? "Professional"}

{langInstruction}

Include relevant hashtags at the end.";
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

public class AIConfig
{
    public string OpenAIApiKey { get; set; } = "";
    public string AnthropicApiKey { get; set; } = "";
    public string GoogleApiKey { get; set; } = "";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama2";

    public static AIConfig Load()
    {
        return new AIConfig
        {
            OpenAIApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
            AnthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "",
            GoogleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "",
            OllamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434",
            OllamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama2"
        };
    }
}
