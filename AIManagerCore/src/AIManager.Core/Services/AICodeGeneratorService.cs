using System.Text;
using System.Net.Http.Json;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.Core.Services;

/// <summary>
/// AI Code Generator Service - ใช้ Local AI เขียน prompt แล้วส่งให้ External AI เขียน JS Code
/// สำหรับรันใน WebView เพื่อโพสต์บน Social Media
/// </summary>
public class AICodeGeneratorService
{
    private readonly ILogger<AICodeGeneratorService> _logger;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly HttpClient _httpClient;
    private readonly AICodeGenerationConfig _config;

    // Code generation stats
    private readonly Dictionary<string, CodeGenerationStats> _stats = new();

    public AICodeGeneratorService(
        ContentGeneratorService contentGenerator,
        AICodeGenerationConfig? config = null,
        ILogger<AICodeGeneratorService>? logger = null)
    {
        _contentGenerator = contentGenerator;
        _config = config ?? new AICodeGenerationConfig();
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<AICodeGeneratorService>();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(_config.CodeGenerationTimeoutSeconds) };

        // Allow environment variables to override config
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")))
            _config.OllamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL")!;
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("EXTERNAL_AI_PROVIDER")))
            _config.ExternalAIProvider = Environment.GetEnvironmentVariable("EXTERNAL_AI_PROVIDER")!;
    }

    /// <summary>
    /// Get current configuration (for API inspection)
    /// </summary>
    public AICodeGenerationConfig GetConfiguration() => _config;

    #region Main Code Generation Flow

    /// <summary>
    /// Generate JavaScript code to fix posting issue
    /// Flow: Error → Local AI (Prompt) → External AI (Code) → Executable JS
    /// </summary>
    public async Task<CodeGenerationResult> GeneratePostingCodeAsync(
        SocialPlatform platform,
        string taskType,
        string errorMessage,
        string currentPageHtml,
        string? currentPageUrl = null,
        LearnedWorkflow? failedWorkflow = null,
        CancellationToken ct = default)
    {
        var result = new CodeGenerationResult
        {
            Platform = platform,
            TaskType = taskType,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("🤖 Starting AI code generation for {Platform}/{TaskType}", platform, taskType);

            // Step 1: Local AI analyzes error and creates prompt
            var analysisPrompt = await AnalyzeErrorWithLocalAIAsync(
                platform, taskType, errorMessage, currentPageHtml, failedWorkflow, ct);

            if (string.IsNullOrEmpty(analysisPrompt))
            {
                result.Success = false;
                result.Error = "Local AI failed to analyze error";
                return result;
            }

            result.AnalysisPrompt = analysisPrompt;

            // Step 2: External AI generates JavaScript code
            var generatedCode = await GenerateCodeWithExternalAIAsync(
                platform, taskType, analysisPrompt, currentPageUrl, ct);

            if (string.IsNullOrEmpty(generatedCode))
            {
                result.Success = false;
                result.Error = "External AI failed to generate code";
                return result;
            }

            // Step 3: Validate and sanitize the generated code
            var validatedCode = ValidateAndSanitizeCode(generatedCode, platform);

            if (!validatedCode.IsValid)
            {
                result.Success = false;
                result.Error = $"Generated code validation failed: {validatedCode.Error}";
                return result;
            }

            result.Success = true;
            result.GeneratedCode = validatedCode.Code;
            result.CodeType = validatedCode.CodeType;
            result.EstimatedSteps = validatedCode.EstimatedSteps;
            result.CompletedAt = DateTime.UtcNow;

            // Update stats
            UpdateStats(platform, taskType, true);

            _logger.LogInformation("✅ AI code generation successful for {Platform}/{TaskType}", platform, taskType);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI code generation failed for {Platform}/{TaskType}", platform, taskType);
            result.Success = false;
            result.Error = ex.Message;
            UpdateStats(platform, taskType, false);
            return result;
        }
    }

    /// <summary>
    /// Check if should escalate to human training
    /// </summary>
    public bool ShouldEscalateToHuman(SocialPlatform platform, string taskType)
    {
        var key = $"{platform}_{taskType}";
        if (_stats.TryGetValue(key, out var stats))
        {
            return stats.ConsecutiveFailures >= _config.MaxConsecutiveFailuresBeforeEscalation;
        }
        return false;
    }

    /// <summary>
    /// Reset failure count after successful execution
    /// </summary>
    public void ResetFailureCount(SocialPlatform platform, string taskType)
    {
        var key = $"{platform}_{taskType}";
        if (_stats.TryGetValue(key, out var stats))
        {
            stats.ConsecutiveFailures = 0;
            stats.LastSuccessAt = DateTime.UtcNow;
        }
    }

    #endregion

    #region Local AI - Error Analysis & Prompt Generation

    /// <summary>
    /// Use Local AI (Ollama) to analyze error and generate prompt for External AI
    /// </summary>
    private async Task<string?> AnalyzeErrorWithLocalAIAsync(
        SocialPlatform platform,
        string taskType,
        string errorMessage,
        string currentPageHtml,
        LearnedWorkflow? failedWorkflow,
        CancellationToken ct)
    {
        _logger.LogDebug("📝 Local AI analyzing error...");

        // Extract key elements from HTML (simplified)
        var pageElements = ExtractKeyElements(currentPageHtml);
        var failedSteps = failedWorkflow?.Steps.Select(s => new
        {
            s.Order,
            Action = s.Action.ToString(),
            s.Description,
            Selector = s.Selector?.Value
        }).ToList();

        var analysisRequest = $@"
คุณเป็น AI Assistant ที่เชี่ยวชาญในการวิเคราะห์ปัญหาการโพสต์บน Social Media

## สถานการณ์
- Platform: {platform}
- Task Type: {taskType}
- Error Message: {errorMessage}
- Current Page URL Elements: {string.Join(", ", pageElements.Take(20))}

## Workflow ที่ล้มเหลว (ถ้ามี)
{(failedSteps != null ? JsonConvert.SerializeObject(failedSteps, Formatting.Indented) : "ไม่มี workflow ก่อนหน้า")}

## งานของคุณ
วิเคราะห์ปัญหาและเขียน prompt สำหรับ AI ที่จะเขียน JavaScript code เพื่อ:
1. Navigate และค้นหา element ที่ต้องการบนหน้าเว็บ
2. กรอกข้อมูลและโพสต์เนื้อหา
3. รองรับ dynamic elements และ async loading

## ผลลัพธ์
เขียน prompt ที่ละเอียดและชัดเจนสำหรับ AI ที่จะเขียน JavaScript code
ระบุ:
- Element selectors ที่ควรลอง (id, class, aria-label, text content)
- ขั้นตอนการทำงานที่ชัดเจน
- Error handling ที่ต้องมี
- Variable placeholders สำหรับ content ({{{{content.text}}}}, {{{{content.hashtags}}}})
";

        try
        {
            var response = await CallOllamaAsync(analysisRequest, _config.OllamaModel, ct);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local AI analysis failed, using fallback prompt");
            return GenerateFallbackPrompt(platform, taskType, errorMessage, pageElements);
        }
    }

    /// <summary>
    /// Fallback prompt when Local AI fails
    /// </summary>
    private string GenerateFallbackPrompt(
        SocialPlatform platform,
        string taskType,
        string errorMessage,
        List<string> pageElements)
    {
        return $@"
Generate JavaScript code to post content on {platform}.

Error encountered: {errorMessage}

Available elements on page:
{string.Join("\n", pageElements.Take(15))}

Requirements:
1. Find the post/message input field
2. Enter the content text (use placeholder {{{{content.text}}}})
3. Add hashtags if available (use placeholder {{{{content.hashtags}}}})
4. Click the post/submit button
5. Wait for confirmation
6. Handle any popups or confirmations

The code must:
- Use async/await pattern
- Include retry logic for element finding
- Have proper error handling
- Return success/failure status
";
    }

    #endregion

    #region External AI - Code Generation

    /// <summary>
    /// Use External AI (GPT-4/Claude/Gemini) to generate executable JavaScript code
    /// </summary>
    private async Task<string?> GenerateCodeWithExternalAIAsync(
        SocialPlatform platform,
        string taskType,
        string analysisPrompt,
        string? pageUrl,
        CancellationToken ct)
    {
        _logger.LogDebug("💻 External AI generating code...");

        var codeGenerationPrompt = $@"
{analysisPrompt}

## IMPORTANT REQUIREMENTS

Generate a complete, executable JavaScript code block that can run in a WebView/browser context.

The code MUST:
1. Be wrapped in an async IIFE: (async () => {{ ... }})()
2. Use modern JavaScript (ES2020+)
3. Include helper functions for element finding with retries
4. Use these placeholders that will be replaced at runtime:
   - {{{{content.text}}}} - Main post content
   - {{{{content.hashtags}}}} - Hashtags to add
   - {{{{content.images}}}} - Image URLs (if any)
   - {{{{content.link}}}} - Link to share (if any)
5. Return a result object: {{ success: boolean, postId?: string, error?: string }}
6. Handle dynamic loading with proper waits
7. Have comprehensive error handling

Platform-specific notes for {platform}:
{GetPlatformSpecificNotes(platform)}

Page URL: {pageUrl ?? "unknown"}

## OUTPUT FORMAT
Return ONLY the JavaScript code, no explanations. The code must be immediately executable.
Start with: (async () => {{
End with: }})();
";

        try
        {
            string? code = _config.ExternalAIProvider.ToLower() switch
            {
                "openai" => await CallOpenAIAsync(codeGenerationPrompt, ct),
                "anthropic" => await CallAnthropicAsync(codeGenerationPrompt, ct),
                "google" => await CallGoogleAIAsync(codeGenerationPrompt, ct),
                _ => await CallOpenAIAsync(codeGenerationPrompt, ct)
            };

            return ExtractCodeFromResponse(code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "External AI code generation failed");
            return null;
        }
    }

    /// <summary>
    /// Get platform-specific notes for code generation
    /// </summary>
    private string GetPlatformSpecificNotes(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => @"
- Post box often has aria-label=""What's on your mind"" or role=""textbox""
- Submit button may have aria-label=""Post"" or contain text ""Post""
- Handle photo upload dialogs if images are provided
- Watch for ""Share to News Feed"" vs ""Share to Story"" options
- Facebook uses React, elements may have data-testid attributes",

            SocialPlatform.Instagram => @"
- New post button often has aria-label=""New post""
- Caption field is a textarea or contenteditable div
- Share button typically has text ""Share""
- Handle the multi-step posting process (select photo, edit, caption, share)
- Instagram web uses React components",

            SocialPlatform.Twitter => @"
- Compose tweet button has aria-label=""Compose tweet"" or similar
- Tweet text area is a div with role=""textbox"" and contenteditable
- Post button may have data-testid=""tweetButtonInline""
- Character count is important (280 limit)
- Handle media upload if images provided",

            SocialPlatform.TikTok => @"
- Upload button for videos
- Caption field for description
- Handle hashtag suggestions
- Privacy settings may need to be set",

            SocialPlatform.LinkedIn => @"
- Start a post button
- Text area for post content
- Post button to submit
- Handle document/image attachments
- Watch for LinkedIn's content editor",

            SocialPlatform.Line => @"
- LINE official account posting interface
- Message composer for different message types
- Handle rich messages and flex messages",

            _ => "Follow standard web posting patterns. Look for input fields, textareas, and submit buttons."
        };
    }

    #endregion

    #region AI Provider Implementations

    private async Task<string?> CallOllamaAsync(string prompt, string model, CancellationToken ct)
    {
        var request = new
        {
            model = model,
            prompt = prompt,
            stream = false,
            options = new { temperature = 0.3 }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_config.OllamaBaseUrl}/api/generate",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>(ct);
        return result?.Response;
    }

    private async Task<string?> CallOpenAIAsync(string prompt, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key not configured");
            return null;
        }

        var request = new
        {
            model = _config.OpenAIModel,
            messages = new[]
            {
                new { role = "system", content = "You are an expert JavaScript developer specializing in browser automation and web scraping. Generate clean, efficient, and robust code." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 4000
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("OpenAI API error: {Error}", error);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>(ct);
        return result?.Choices?.FirstOrDefault()?.Message?.Content;
    }

    private async Task<string?> CallAnthropicAsync(string prompt, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Anthropic API key not configured");
            return null;
        }

        var request = new
        {
            model = _config.AnthropicModel,
            max_tokens = 4000,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(ct);
        return result?.Content?.FirstOrDefault()?.Text;
    }

    private async Task<string?> CallGoogleAIAsync(string prompt, CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Google API key not configured");
            return null;
        }

        var request = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 4000
            }
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"https://generativelanguage.googleapis.com/v1beta/models/{_config.GoogleModel}:generateContent?key={apiKey}",
            request,
            ct);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<GoogleAIResponse>(ct);
        return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
    }

    #endregion

    #region Code Validation & Sanitization

    /// <summary>
    /// Validate and sanitize generated code
    /// </summary>
    private CodeValidationResult ValidateAndSanitizeCode(string code, SocialPlatform platform)
    {
        var result = new CodeValidationResult();

        // Remove markdown code blocks if present
        code = code.Trim();
        if (code.StartsWith("```javascript") || code.StartsWith("```js"))
        {
            code = code.Substring(code.IndexOf('\n') + 1);
        }
        if (code.StartsWith("```"))
        {
            code = code.Substring(3);
        }
        if (code.EndsWith("```"))
        {
            code = code.Substring(0, code.Length - 3);
        }
        code = code.Trim();

        // Check for dangerous patterns (from config + hardcoded security patterns)
        var dangerousPatterns = _config.BlockedCodePatterns.Concat(new[]
        {
            "localStorage.clear",
            "sessionStorage.clear",
            "window.location =",
            "fetch('http://",  // Only allow HTTPS
            "XMLHttpRequest",
            "navigator.credentials",
            "crypto.subtle"
        }).Distinct();

        foreach (var pattern in dangerousPatterns)
        {
            if (code.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.Error = $"Dangerous pattern detected: {pattern}";
                return result;
            }
        }

        // Check for required structure
        if (!code.Contains("async") || !code.Contains("await"))
        {
            result.IsValid = false;
            result.Error = "Code must use async/await pattern";
            return result;
        }

        // Check for result return
        if (!code.Contains("success") || !code.Contains("return"))
        {
            result.IsValid = false;
            result.Error = "Code must return a result object with success property";
            return result;
        }

        // Ensure it's wrapped in async IIFE
        if (!code.StartsWith("(async"))
        {
            code = $"(async () => {{\n{code}\n}})();";
        }

        // Determine code type
        result.CodeType = DetermineCodeType(code);
        result.EstimatedSteps = CountEstimatedSteps(code);
        result.Code = code;
        result.IsValid = true;

        return result;
    }

    private string DetermineCodeType(string code)
    {
        if (code.Contains("puppeteer") || code.Contains("playwright"))
            return "automation";
        if (code.Contains("fetch") || code.Contains("axios"))
            return "api";
        return "dom_manipulation";
    }

    private int CountEstimatedSteps(string code)
    {
        var stepIndicators = new[] { "click", "type", "fill", "select", "wait", "navigate" };
        return stepIndicators.Sum(s =>
            System.Text.RegularExpressions.Regex.Matches(code, s,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count);
    }

    #endregion

    #region Helper Methods

    private List<string> ExtractKeyElements(string html)
    {
        var elements = new List<string>();

        // Simple extraction of key attributes
        var patterns = new[]
        {
            @"id=""([^""]+)""",
            @"class=""([^""]+)""",
            @"aria-label=""([^""]+)""",
            @"data-testid=""([^""]+)""",
            @"placeholder=""([^""]+)""",
            @"name=""([^""]+)"""
        };

        foreach (var pattern in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    elements.Add(match.Groups[1].Value);
                }
            }
        }

        return elements.Distinct().Take(50).ToList();
    }

    private string? ExtractCodeFromResponse(string? response)
    {
        if (string.IsNullOrEmpty(response))
            return null;

        // Try to find code block
        var codeBlockStart = response.IndexOf("```");
        if (codeBlockStart >= 0)
        {
            var codeStart = response.IndexOf('\n', codeBlockStart) + 1;
            var codeEnd = response.IndexOf("```", codeStart);
            if (codeEnd > codeStart)
            {
                return response.Substring(codeStart, codeEnd - codeStart).Trim();
            }
        }

        // Try to find async IIFE
        var asyncStart = response.IndexOf("(async");
        if (asyncStart >= 0)
        {
            // Find matching closing
            var depth = 0;
            var inString = false;
            var stringChar = ' ';

            for (int i = asyncStart; i < response.Length; i++)
            {
                var c = response[i];

                if (!inString && (c == '"' || c == '\'' || c == '`'))
                {
                    inString = true;
                    stringChar = c;
                }
                else if (inString && c == stringChar && response[i - 1] != '\\')
                {
                    inString = false;
                }
                else if (!inString)
                {
                    if (c == '(') depth++;
                    else if (c == ')') depth--;

                    if (depth == 0 && i > asyncStart + 10)
                    {
                        // Check for ();
                        var remaining = response.Substring(i + 1).TrimStart();
                        if (remaining.StartsWith(";") || remaining.StartsWith("()"))
                        {
                            var endIndex = response.IndexOf(';', i);
                            if (endIndex < 0) endIndex = i + 3;
                            return response.Substring(asyncStart, endIndex - asyncStart + 1);
                        }
                    }
                }
            }
        }

        return response;
    }

    private void UpdateStats(SocialPlatform platform, string taskType, bool success)
    {
        var key = $"{platform}_{taskType}";
        if (!_stats.TryGetValue(key, out var stats))
        {
            stats = new CodeGenerationStats { Platform = platform, TaskType = taskType };
            _stats[key] = stats;
        }

        stats.TotalAttempts++;
        if (success)
        {
            stats.SuccessCount++;
            stats.ConsecutiveFailures = 0;
            stats.LastSuccessAt = DateTime.UtcNow;
        }
        else
        {
            stats.FailureCount++;
            stats.ConsecutiveFailures++;
            stats.LastFailureAt = DateTime.UtcNow;
        }
    }

    #endregion
}

#region Models

public class CodeGenerationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public SocialPlatform Platform { get; set; }
    public string TaskType { get; set; } = "";
    public string? AnalysisPrompt { get; set; }
    public string? GeneratedCode { get; set; }
    public string? CodeType { get; set; }
    public int EstimatedSteps { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class CodeValidationResult
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string Code { get; set; } = "";
    public string CodeType { get; set; } = "";
    public int EstimatedSteps { get; set; }
}

public class CodeGenerationStats
{
    public SocialPlatform Platform { get; set; }
    public string TaskType { get; set; } = "";
    public int TotalAttempts { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
}

// API Response Models
internal class OllamaResponse
{
    public string? Response { get; set; }
}

internal class OpenAIResponse
{
    public List<OpenAIChoice>? Choices { get; set; }
}

internal class OpenAIChoice
{
    public OpenAIMessage? Message { get; set; }
}

internal class OpenAIMessage
{
    public string? Content { get; set; }
}

internal class AnthropicResponse
{
    public List<AnthropicContent>? Content { get; set; }
}

internal class AnthropicContent
{
    public string? Text { get; set; }
}

internal class GoogleAIResponse
{
    public List<GoogleAICandidate>? Candidates { get; set; }
}

internal class GoogleAICandidate
{
    public GoogleAIContent? Content { get; set; }
}

internal class GoogleAIContent
{
    public List<GoogleAIPart>? Parts { get; set; }
}

internal class GoogleAIPart
{
    public string? Text { get; set; }
}

#endregion
