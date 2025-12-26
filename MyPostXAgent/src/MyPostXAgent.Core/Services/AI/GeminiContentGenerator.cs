using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Google Gemini Content Generator
/// </summary>
public class GeminiContentGenerator : IAIContentGenerator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiContentGenerator>? _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public AIProvider Provider => AIProvider.Gemini;

    public GeminiContentGenerator(
        HttpClient httpClient,
        string apiKey,
        string model = "gemini-1.5-flash",
        ILogger<GeminiContentGenerator>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
    }

    public async Task<AIProviderStatus> CheckStatusAsync()
    {
        // Step 1: Check if API key is configured
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = false,
                IsConfigured = false,
                Message = "API key not configured",
                LastChecked = DateTime.UtcNow
            };
        }

        try
        {
            // Step 2: Check if Gemini API is accessible (with 2-second timeout)
            var response = await _httpClient.GetAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models?key={_apiKey}",
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var errorMessage = statusCode switch
                {
                    400 => "Invalid API key format",
                    403 => "API key not authorized - check your Google Cloud project",
                    429 => "Rate limit exceeded",
                    500 => "Google server error",
                    503 => "Gemini service unavailable",
                    _ => $"HTTP {statusCode} error"
                };

                return new AIProviderStatus
                {
                    Provider = Provider,
                    IsAvailable = false,
                    IsConfigured = true,
                    Message = errorMessage,
                    LastChecked = DateTime.UtcNow
                };
            }

            // Step 3: Verify model availability
            var modelsResponse = await response.Content.ReadFromJsonAsync<GeminiModelsResponse>();
            var hasModel = modelsResponse?.Models?.Any(m => m.Name?.Contains(_model) == true) ?? false;

            if (!hasModel)
            {
                return new AIProviderStatus
                {
                    Provider = Provider,
                    IsAvailable = false,
                    IsConfigured = true,
                    Message = $"Model '{_model}' not available - check model name",
                    LastChecked = DateTime.UtcNow
                };
            }

            // Step 4: All checks passed
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = true,
                IsConfigured = true,
                Message = $"Gemini ready ({_model})",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException)
        {
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = false,
                IsConfigured = true,
                Message = "Gemini timeout - cannot connect",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (HttpRequestException ex)
        {
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = false,
                IsConfigured = true,
                Message = $"Network error: {ex.Message}",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check Gemini status");
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = false,
                IsConfigured = true,
                Message = $"Cannot connect: {ex.Message}",
                LastChecked = DateTime.UtcNow
            };
        }
    }

    public async Task<ContentGenerationResult> GenerateContentAsync(
        ContentGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            System.Diagnostics.Debug.WriteLine($"[Gemini] Generating content for topic: {request.Topic}");

            var prompt = BuildPrompt(request);
            var response = await GenerateAsync(prompt, cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[Gemini] ✅ Content generated successfully ({response.TokensUsed} tokens)");

            // Generate hashtags separately
            var hashtags = string.Empty;
            if (!string.IsNullOrWhiteSpace(request.Keywords))
            {
                hashtags = await GenerateHashtagsAsync(request.Topic, request.Keywords, cancellationToken);
            }

            stopwatch.Stop();

            return new ContentGenerationResult
            {
                Success = true,
                Content = response.Content.Trim(),
                Hashtags = hashtags.Trim(),
                Provider = Provider,
                TokensUsed = response.TokensUsed,
                Duration = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var errorMsg = $"Network error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[Gemini] ❌ {errorMsg}");
            _logger?.LogError(ex, "Failed to generate content with Gemini");

            return new ContentGenerationResult
            {
                Success = false,
                ErrorMessage = errorMsg,
                Provider = Provider,
                Duration = stopwatch.Elapsed
            };
        }
        catch (TaskCanceledException)
        {
            stopwatch.Stop();
            var errorMsg = "Request timeout - Gemini took too long to respond";
            System.Diagnostics.Debug.WriteLine($"[Gemini] ❌ {errorMsg}");

            return new ContentGenerationResult
            {
                Success = false,
                ErrorMessage = errorMsg,
                Provider = Provider,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMsg = ex.Message;

            // Parse Gemini API errors
            if (errorMsg.Contains("400"))
                errorMsg = "Invalid API key or request format";
            else if (errorMsg.Contains("403"))
                errorMsg = "API key not authorized - check your Google Cloud project";
            else if (errorMsg.Contains("429"))
                errorMsg = "Rate limit exceeded - please try again later";
            else if (errorMsg.Contains("500") || errorMsg.Contains("503"))
                errorMsg = "Google service unavailable - please try again later";
            else if (errorMsg.Contains("SAFETY"))
                errorMsg = "Content blocked by safety filters - try different wording";

            System.Diagnostics.Debug.WriteLine($"[Gemini] ❌ {errorMsg}");
            _logger?.LogError(ex, "Failed to generate content with Gemini");

            return new ContentGenerationResult
            {
                Success = false,
                ErrorMessage = errorMsg,
                Provider = Provider,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<string> GenerateHashtagsAsync(
        string topic,
        string keywords,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var prompt = $@"Generate 5-8 relevant Thai hashtags for this topic.
Topic: {topic}
Keywords: {keywords}

Return ONLY hashtags separated by spaces, with # prefix.
Example format: #แฮชแท็ก1 #แฮชแท็ก2 #แฮชแท็ก3

Hashtags:";

            var response = await GenerateAsync(prompt, cancellationToken);
            return response.Content.Trim();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate hashtags with Gemini");
            return string.Empty;
        }
    }

    private string BuildPrompt(ContentGenerationRequest request)
    {
        var platformsText = request.TargetPlatforms.Count > 0
            ? string.Join(", ", request.TargetPlatforms)
            : "Social Media";

        return $@"You are a professional Thai social media content creator.

Create engaging {request.Language} social media content with these specifications:

**Topic**: {request.Topic}
**Content Type**: {request.ContentType}
**Tone**: {request.Tone}
**Length**: {request.Length}
**Target Platforms**: {platformsText}
**Keywords to include**: {request.Keywords}
**Use Emojis**: {(request.IncludeEmojis ? "Yes" : "No")}
**Include Call-to-Action**: {(request.IncludeCTA ? "Yes" : "No")}

IMPORTANT RULES:
1. Write ONLY in {request.Language} language
2. Make it natural, engaging, and platform-appropriate
3. {(request.IncludeEmojis ? "Use relevant emojis throughout" : "Do not use emojis")}
4. {(request.IncludeCTA ? "End with a clear call-to-action" : "No call-to-action needed")}
5. Match the specified tone: {request.Tone}
6. Keep the length as: {request.Length}
7. DO NOT include hashtags in the content (they will be added separately)
8. Return ONLY the content text, no explanations or meta-commentary

Content:";
    }

    private async Task<GenerateResponse> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.7,
                maxOutputTokens = 500,
                topP = 0.9
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        System.Diagnostics.Debug.WriteLine($"[Gemini] Request URL: {url.Replace(_apiKey, "***KEY***")}");

        var response = await _httpClient.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            System.Diagnostics.Debug.WriteLine($"[Gemini] HTTP {statusCode} Error: {errorBody}");

            var errorMsg = statusCode switch
            {
                404 => $"Model '{_model}' not found. Try 'gemini-pro' or 'gemini-1.5-flash'",
                400 => "Invalid request format or API key",
                403 => "API key not authorized",
                _ => $"HTTP {statusCode}: {errorBody}"
            };

            throw new HttpRequestException(errorMsg);
        }

        var responseData = await response.Content.ReadFromJsonAsync<GeminiResponse>(
            cancellationToken: cancellationToken);

        var contentText = responseData?.Candidates?[0]?.Content?.Parts?[0]?.Text ?? string.Empty;
        var tokensUsed = (responseData?.UsageMetadata?.PromptTokenCount ?? 0) +
                        (responseData?.UsageMetadata?.CandidatesTokenCount ?? 0);

        return new GenerateResponse
        {
            Content = contentText,
            TokensUsed = tokensUsed
        };
    }

    private class GenerateResponse
    {
        public string Content { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
    }

    private class GeminiResponse
    {
        [JsonPropertyName("candidates")]
        public List<Candidate>? Candidates { get; set; }

        [JsonPropertyName("usageMetadata")]
        public UsageMetadata? UsageMetadata { get; set; }
    }

    private class Candidate
    {
        [JsonPropertyName("content")]
        public Content? Content { get; set; }
    }

    private class Content
    {
        [JsonPropertyName("parts")]
        public List<Part>? Parts { get; set; }
    }

    private class Part
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class UsageMetadata
    {
        [JsonPropertyName("promptTokenCount")]
        public int PromptTokenCount { get; set; }

        [JsonPropertyName("candidatesTokenCount")]
        public int CandidatesTokenCount { get; set; }
    }

    private class GeminiModelsResponse
    {
        [JsonPropertyName("models")]
        public List<GeminiModel>? Models { get; set; }
    }

    private class GeminiModel
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
