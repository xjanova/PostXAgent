using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Ollama AI Content Generator (Free, Local)
/// </summary>
public class OllamaContentGenerator : IAIContentGenerator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaContentGenerator>? _logger;
    private readonly string _baseUrl;
    private readonly string _model;

    public AIProvider Provider => AIProvider.Ollama;

    public OllamaContentGenerator(
        HttpClient httpClient,
        string baseUrl = "http://localhost:11434",
        string model = "llama3.2:3b",
        ILogger<OllamaContentGenerator>? logger = null)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
        _logger = logger;
    }

    public async Task<AIProviderStatus> CheckStatusAsync()
    {
        try
        {
            // Step 1: Check if Ollama is running
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags",
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            if (!response.IsSuccessStatusCode)
            {
                return new AIProviderStatus
                {
                    Provider = Provider,
                    IsAvailable = false,
                    IsConfigured = true,
                    Message = "Ollama is not running",
                    LastChecked = DateTime.UtcNow
                };
            }

            // Step 2: Check if the model is installed
            var tagsJson = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
            var hasModel = tagsJson?.Models?.Any(m => m.Name?.Contains(_model) == true) ?? false;

            if (!hasModel)
            {
                return new AIProviderStatus
                {
                    Provider = Provider,
                    IsAvailable = false,
                    IsConfigured = true,
                    Message = $"Model '{_model}' not installed. Run: ollama pull {_model}",
                    LastChecked = DateTime.UtcNow
                };
            }

            // Step 3: All checks passed
            return new AIProviderStatus
            {
                Provider = Provider,
                IsAvailable = true,
                IsConfigured = true,
                Message = $"Ollama ready ({_model})",
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
                Message = "Ollama timeout - not running",
                LastChecked = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check Ollama status");
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
            var prompt = BuildPrompt(request);
            var content = await GenerateAsync(prompt, cancellationToken);

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
                Content = content.Trim(),
                Hashtags = hashtags.Trim(),
                Provider = Provider,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger?.LogError(ex, "Failed to generate content with Ollama");

            return new ContentGenerationResult
            {
                Success = false,
                ErrorMessage = $"Ollama error: {ex.Message}",
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

            var result = await GenerateAsync(prompt, cancellationToken);
            return result.Trim();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate hashtags with Ollama");
            return string.Empty;
        }
    }

    private string BuildPrompt(ContentGenerationRequest request)
    {
        var platformsText = request.TargetPlatforms.Count > 0
            ? string.Join(", ", request.TargetPlatforms)
            : "Social Media";

        var prompt = $@"You are a professional Thai social media content creator.

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

        return prompt;
    }

    private async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _model,
            prompt = prompt,
            stream = false,
            options = new
            {
                temperature = 0.7,
                top_p = 0.9,
                num_predict = 500
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(
            $"{_baseUrl}/api/generate",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadFromJsonAsync<OllamaResponse>(
            cancellationToken: cancellationToken);

        return responseJson?.Response ?? string.Empty;
    }

    private class OllamaResponse
    {
        public string? Model { get; set; }
        public string? Response { get; set; }
        public bool Done { get; set; }
    }

    private class OllamaTagsResponse
    {
        public List<OllamaModel>? Models { get; set; }
    }

    private class OllamaModel
    {
        public string? Name { get; set; }
        public long? Size { get; set; }
    }
}
