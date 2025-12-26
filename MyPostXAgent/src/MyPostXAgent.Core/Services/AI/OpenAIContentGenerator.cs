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
/// OpenAI GPT Content Generator
/// </summary>
public class OpenAIContentGenerator : IAIContentGenerator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIContentGenerator>? _logger;
    private readonly string _apiKey;
    private readonly string _model;

    public AIProvider Provider => AIProvider.OpenAI;

    public OpenAIContentGenerator(
        HttpClient httpClient,
        string apiKey,
        string model = "gpt-4o-mini",
        ILogger<OpenAIContentGenerator>? logger = null)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
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
            // Step 2: Check if OpenAI API is accessible (with 2-second timeout)
            var response = await _httpClient.GetAsync("https://api.openai.com/v1/models",
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            if (!response.IsSuccessStatusCode)
            {
                var statusCode = (int)response.StatusCode;
                var errorMessage = statusCode switch
                {
                    401 => "Invalid API key",
                    403 => "API access forbidden - check your account",
                    429 => "Rate limit exceeded",
                    500 => "OpenAI server error",
                    503 => "OpenAI service unavailable",
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
            var modelsResponse = await response.Content.ReadFromJsonAsync<OpenAIModelsResponse>();
            var hasModel = modelsResponse?.Data?.Any(m => m.Id?.Contains(_model.Split('-')[0]) == true) ?? false;

            if (!hasModel)
            {
                return new AIProviderStatus
                {
                    Provider = Provider,
                    IsAvailable = false,
                    IsConfigured = true,
                    Message = $"Model '{_model}' not available in your account",
                    LastChecked = DateTime.UtcNow
                };
            }

            // Step 4: All checks passed
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = true,
                IsConfigured = true,
                Message = $"OpenAI ready ({_model})",
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
                Message = "OpenAI timeout - cannot connect",
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
            _logger?.LogWarning(ex, "Failed to check OpenAI status");
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
            System.Diagnostics.Debug.WriteLine($"[OpenAI] Generating content for topic: {request.Topic}");

            var prompt = BuildPrompt(request);
            var response = await GenerateAsync(prompt, cancellationToken);

            System.Diagnostics.Debug.WriteLine($"[OpenAI] ✅ Content generated successfully ({response.TokensUsed} tokens)");

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
            System.Diagnostics.Debug.WriteLine($"[OpenAI] ❌ {errorMsg}");
            _logger?.LogError(ex, "Failed to generate content with OpenAI");

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
            var errorMsg = "Request timeout - OpenAI took too long to respond";
            System.Diagnostics.Debug.WriteLine($"[OpenAI] ❌ {errorMsg}");

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

            // Parse OpenAI API errors
            if (errorMsg.Contains("401"))
                errorMsg = "Invalid API key";
            else if (errorMsg.Contains("429"))
                errorMsg = "Rate limit exceeded - please try again later";
            else if (errorMsg.Contains("500") || errorMsg.Contains("503"))
                errorMsg = "OpenAI service unavailable - please try again later";

            System.Diagnostics.Debug.WriteLine($"[OpenAI] ❌ {errorMsg}");
            _logger?.LogError(ex, "Failed to generate content with OpenAI");

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
            _logger?.LogError(ex, "Failed to generate hashtags with OpenAI");
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
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 500
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        System.Diagnostics.Debug.WriteLine($"[OpenAI] Sending request to /v1/chat/completions with model: {_model}");

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            content,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var statusCode = (int)response.StatusCode;

            System.Diagnostics.Debug.WriteLine($"[OpenAI] HTTP {statusCode} Error: {errorBody}");

            var errorMsg = statusCode switch
            {
                401 => "Invalid API key",
                403 => "API access forbidden",
                404 => $"Model '{_model}' not found or not available",
                429 => "Rate limit exceeded",
                _ => $"HTTP {statusCode}: {errorBody}"
            };

            throw new HttpRequestException(errorMsg);
        }

        var responseData = await response.Content.ReadFromJsonAsync<OpenAIResponse>(
            cancellationToken: cancellationToken);

        return new GenerateResponse
        {
            Content = responseData?.Choices?[0]?.Message?.Content ?? string.Empty,
            TokensUsed = responseData?.Usage?.TotalTokens ?? 0
        };
    }

    private class GenerateResponse
    {
        public string Content { get; set; } = string.Empty;
        public int TokensUsed { get; set; }
    }

    private class OpenAIResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }

        [JsonPropertyName("usage")]
        public Usage? Usage { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class Usage
    {
        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    private class OpenAIModelsResponse
    {
        [JsonPropertyName("data")]
        public List<OpenAIModel>? Data { get; set; }
    }

    private class OpenAIModel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
