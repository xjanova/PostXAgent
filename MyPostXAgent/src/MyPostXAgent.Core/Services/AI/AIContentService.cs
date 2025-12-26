using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Main AI Content Generation Service
/// Manages multiple AI providers with fallback support
/// </summary>
public class AIContentService
{
    private readonly DatabaseService _database;
    private readonly ILogger<AIContentService>? _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<AIProvider, IAIContentGenerator> _generators = new();

    public AIContentService(
        DatabaseService database,
        IHttpClientFactory httpClientFactory,
        ILogger<AIContentService>? logger = null)
    {
        _database = database;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Initialize all AI providers based on settings
    /// </summary>
    public async Task InitializeProvidersAsync()
    {
        _generators.Clear();

        try
        {
            // Ollama - Always available (local)
            var ollamaUrl = await _database.GetSettingAsync("ollama_base_url") ?? "http://localhost:11434";
            _generators[AIProvider.Ollama] = new OllamaContentGenerator(
                _httpClientFactory.CreateClient(),
                ollamaUrl,
                "llama3.2",
                _logger as ILogger<OllamaContentGenerator>);

            // OpenAI
            var openAiKey = await _database.GetSettingAsync("openai_api_key");
            if (!string.IsNullOrWhiteSpace(openAiKey))
            {
                _generators[AIProvider.OpenAI] = new OpenAIContentGenerator(
                    _httpClientFactory.CreateClient(),
                    openAiKey,
                    "gpt-4o-mini",
                    _logger as ILogger<OpenAIContentGenerator>);
            }

            // Claude
            var claudeKey = await _database.GetSettingAsync("anthropic_api_key");
            if (!string.IsNullOrWhiteSpace(claudeKey))
            {
                _generators[AIProvider.Claude] = new ClaudeContentGenerator(
                    _httpClientFactory.CreateClient(),
                    claudeKey,
                    "claude-3-5-haiku-20241022",
                    _logger as ILogger<ClaudeContentGenerator>);
            }

            // Gemini
            var geminiKey = await _database.GetSettingAsync("google_api_key");
            if (!string.IsNullOrWhiteSpace(geminiKey))
            {
                _generators[AIProvider.Gemini] = new GeminiContentGenerator(
                    _httpClientFactory.CreateClient(),
                    geminiKey,
                    "gemini-2.0-flash-exp",
                    _logger as ILogger<GeminiContentGenerator>);
            }

            _logger?.LogInformation("Initialized {Count} AI providers", _generators.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing AI providers");
        }
    }

    /// <summary>
    /// Get status of all AI providers
    /// </summary>
    public async Task<List<AIProviderStatus>> GetAllProvidersStatusAsync()
    {
        var statuses = new List<AIProviderStatus>();

        foreach (var (provider, generator) in _generators)
        {
            try
            {
                var status = await generator.CheckStatusAsync();
                statuses.Add(status);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error checking status for {Provider}", provider);
                statuses.Add(new AIProviderStatus
                {
                    Provider = provider,
                    IsAvailable = false,
                    IsConfigured = false,
                    Message = $"Error: {ex.Message}",
                    LastChecked = DateTime.UtcNow
                });
            }
        }

        return statuses;
    }

    /// <summary>
    /// Generate content using specified provider (with fallback)
    /// </summary>
    public async Task<ContentGenerationResult> GenerateContentAsync(
        ContentGenerationRequest request,
        AIProvider preferredProvider,
        bool useFallback = true,
        CancellationToken cancellationToken = default)
    {
        string? firstError = null;
        var errors = new List<string>();

        // Try preferred provider first
        if (_generators.TryGetValue(preferredProvider, out var generator))
        {
            _logger?.LogInformation("Generating content with {Provider}", preferredProvider);
            var result = await generator.GenerateContentAsync(request, cancellationToken);

            if (result.Success)
            {
                return result;
            }

            firstError = result.ErrorMessage;
            errors.Add($"{preferredProvider}: {result.ErrorMessage}");
            _logger?.LogWarning("Failed with {Provider}: {Error}", preferredProvider, result.ErrorMessage);
        }
        else
        {
            firstError = $"Provider {preferredProvider} not initialized";
            errors.Add(firstError);
            _logger?.LogWarning("Provider {Provider} not found in generators", preferredProvider);
        }

        // Fallback to other providers if enabled
        if (useFallback)
        {
            _logger?.LogInformation("Attempting fallback providers");

            // Try in priority order: Ollama -> Gemini -> OpenAI -> Claude
            var fallbackOrder = new[]
            {
                AIProvider.Ollama,
                AIProvider.Gemini,
                AIProvider.OpenAI,
                AIProvider.Claude
            }.Where(p => p != preferredProvider); // Skip the one we already tried

            foreach (var provider in fallbackOrder)
            {
                if (_generators.TryGetValue(provider, out var fallbackGenerator))
                {
                    _logger?.LogInformation("Trying fallback: {Provider}", provider);

                    var result = await fallbackGenerator.GenerateContentAsync(request, cancellationToken);
                    if (result.Success)
                    {
                        _logger?.LogInformation("Successfully generated with fallback: {Provider}", provider);
                        return result;
                    }

                    errors.Add($"{provider}: {result.ErrorMessage}");
                    _logger?.LogWarning("Fallback {Provider} failed: {Error}", provider, result.ErrorMessage);
                }
            }
        }

        // All failed - return the first error (most relevant)
        return new ContentGenerationResult
        {
            Success = false,
            ErrorMessage = firstError ?? "No AI providers available",
            Provider = preferredProvider
        };
    }

    /// <summary>
    /// Check if a specific provider is available
    /// </summary>
    public bool IsProviderAvailable(AIProvider provider)
    {
        return _generators.ContainsKey(provider);
    }

    /// <summary>
    /// Get the first available provider
    /// </summary>
    public AIProvider? GetFirstAvailableProvider()
    {
        // Priority order
        var order = new[] { AIProvider.Ollama, AIProvider.Gemini, AIProvider.OpenAI, AIProvider.Claude };

        foreach (var provider in order)
        {
            if (_generators.ContainsKey(provider))
                return provider;
        }

        return null;
    }

    /// <summary>
    /// Get list of available providers
    /// </summary>
    public List<AIProvider> GetAvailableProviders()
    {
        return _generators.Keys.ToList();
    }
}
