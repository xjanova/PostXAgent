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
/// Anthropic Claude Content Generator
/// </summary>
public class ClaudeContentGenerator : IAIContentGenerator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeContentGenerator>? _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public AIProvider Provider => AIProvider.Claude;

    public ClaudeContentGenerator(
        HttpClient httpClient,
        string apiKey,
        string model = "claude-3-5-haiku-20241022",
        ILogger<ClaudeContentGenerator>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
            // Step 2: Test API key with minimal request (with 2-second timeout)
            var testRequest = new
            {
                model = _model,
                max_tokens = 10,
                messages = new[]
                {
                    new { role = "user", content = "Hi" }
                }
            };

            var json = JsonSerializer.Serialize(testRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.anthropic.com/v1/messages",
                content,
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var errorMessage = statusCode switch
                {
                    401 => "Invalid API key",
                    403 => "API access forbidden - check your account",
                    429 => "Rate limit exceeded",
                    500 => "Anthropic server error",
                    503 => "Anthropic service unavailable",
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

            // Step 3: All checks passed
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = true,
                IsConfigured = true,
                Message = $"Claude ready ({_model})",
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
                Message = "Claude timeout - cannot connect",
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
            _logger?.LogWarning(ex, "Failed to check Claude status");
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
            System.Diagnostics.Debug.WriteLine($"[Claude] Generating content for topic: {request.Topic}");

            var prompt = BuildPrompt(request);
            var response = await GenerateAsync(prompt, cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[Claude] ✅ Content generated successfully ({response.TokensUsed} tokens)");

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
            System.Diagnostics.Debug.WriteLine($"[Claude] ❌ {errorMsg}");
            _logger?.LogError(ex, "Failed to generate content with Claude");

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
            var errorMsg = "Request timeout - Claude took too long to respond";
            System.Diagnostics.Debug.WriteLine($"[Claude] ❌ {errorMsg}");

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

            // Parse Claude API errors
            if (errorMsg.Contains("401"))
                errorMsg = "Invalid API key";
            else if (errorMsg.Contains("429"))
                errorMsg = "Rate limit exceeded - please try again later";
            else if (errorMsg.Contains("500") || errorMsg.Contains("503"))
                errorMsg = "Anthropic service unavailable - please try again later";
            else if (errorMsg.Contains("overloaded"))
                errorMsg = "Claude is overloaded - please try again in a moment";

            System.Diagnostics.Debug.WriteLine($"[Claude] ❌ {errorMsg}");
            _logger?.LogError(ex, "Failed to generate content with Claude");

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
            _logger?.LogError(ex, "Failed to generate hashtags with Claude");
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
            model = _model,
            max_tokens = 1024,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        System.Diagnostics.Debug.WriteLine($"[Claude] Sending request to /v1/messages with model: {_model}");

        var response = await _httpClient.PostAsync(
            "https://api.anthropic.com/v1/messages",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            System.Diagnostics.Debug.WriteLine($"[Claude] HTTP {statusCode} Error: {errorBody}");

            var errorMsg = statusCode switch
            {
                401 => "Invalid API key",
                403 => "API access forbidden",
                404 => $"Model '{_model}' not found or endpoint incorrect",
                429 => "Rate limit exceeded",
                529 => "Claude is overloaded - try again later",
                _ => $"HTTP {statusCode}: {errorBody}"
            };

            throw new HttpRequestException(errorMsg);
        }

        var responseData = await response.Content.ReadFromJsonAsync<ClaudeResponse>(
            cancellationToken: cancellationToken);

        var contentText = responseData?.Content?[0]?.Text ?? string.Empty;
        var tokensUsed = (responseData?.Usage?.InputTokens ?? 0) + (responseData?.Usage?.OutputTokens ?? 0);

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

    private class ClaudeResponse
    {
        [JsonPropertyName("content")]
        public List<ContentBlock>? Content { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private class ContentBlock
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }
}
