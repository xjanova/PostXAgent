using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Main AI Image Service - orchestrates multiple image generators with fallback
/// </summary>
public class AIImageService
{
    private readonly DatabaseService _database;
    private readonly Dictionary<ImageAIProvider, IImageGenerator> _generators = new();
    
    private StableDiffusionImageGenerator? _sdGenerator;
    private DallEImageGenerator? _dalleGenerator;
    private LeonardoImageGenerator? _leonardoGenerator;

    public AIImageService(DatabaseService database)
    {
        _database = database;
    }

    /// <summary>
    /// Initialize image generation providers with API keys from settings
    /// </summary>
    public async Task InitializeProvidersAsync(CancellationToken ct = default)
    {
        _generators.Clear();

        // Stable Diffusion (Local - always try to add)
        var sdUrl = await _database.GetSettingAsync("sd_api_url", ct) ?? "http://127.0.0.1:7860";
        _sdGenerator = new StableDiffusionImageGenerator(sdUrl);
        _generators[ImageAIProvider.StableDiffusion] = _sdGenerator;

        // DALL-E (OpenAI)
        var openAiKey = await _database.GetSettingAsync("openai_api_key", ct);
        if (!string.IsNullOrEmpty(openAiKey))
        {
            _dalleGenerator = new DallEImageGenerator(openAiKey);
            _generators[ImageAIProvider.DallE] = _dalleGenerator;
        }

        // Leonardo.AI
        var leonardoKey = await _database.GetSettingAsync("leonardo_api_key", ct);
        if (!string.IsNullOrEmpty(leonardoKey))
        {
            _leonardoGenerator = new LeonardoImageGenerator(leonardoKey);
            _generators[ImageAIProvider.Leonardo] = _leonardoGenerator;
        }
    }

    /// <summary>
    /// Generate image using specified provider or best available
    /// </summary>
    public async Task<ImageGenerationResult> GenerateImageAsync(
        ImageGenerationRequest request,
        ImageAIProvider? preferredProvider = null,
        bool useFallback = true,
        CancellationToken ct = default)
    {
        // Get ordered list of providers to try
        var providersToTry = GetProvidersToTry(preferredProvider);

        foreach (var provider in providersToTry)
        {
            if (!_generators.TryGetValue(provider, out var generator))
                continue;

            var result = await generator.GenerateImageAsync(request, ct);
            
            if (result.Success)
            {
                return result;
            }

            if (!useFallback)
            {
                return result;
            }

            // Log failure and try next
            System.Diagnostics.Debug.WriteLine($"Image generation failed with {provider}: {result.ErrorMessage}");
        }

        return new ImageGenerationResult
        {
            Success = false,
            ErrorMessage = "All image generation providers failed"
        };
    }

    /// <summary>
    /// Get status of all configured providers
    /// </summary>
    public async Task<List<ImageProviderStatus>> GetAllProvidersStatusAsync(CancellationToken ct = default)
    {
        var statuses = new List<ImageProviderStatus>();

        foreach (var (provider, generator) in _generators)
        {
            var status = new ImageProviderStatus
            {
                Provider = provider,
                IsConfigured = true,
                LastChecked = DateTime.UtcNow
            };

            try
            {
                if (generator is StableDiffusionImageGenerator sd)
                {
                    status.IsAvailable = await sd.IsAvailableAsync(ct);
                    status.Message = status.IsAvailable ? "Running" : "Not running";
                }
                else if (generator is DallEImageGenerator dalle)
                {
                    status.IsAvailable = await dalle.IsAvailableAsync(ct);
                    status.Message = status.IsAvailable ? "API Ready" : "API Key invalid";
                }
                else if (generator is LeonardoImageGenerator leo)
                {
                    status.IsAvailable = await leo.IsAvailableAsync(ct);
                    status.Message = status.IsAvailable ? "API Ready" : "API Key invalid";
                }
            }
            catch (Exception ex)
            {
                status.IsAvailable = false;
                status.Message = ex.Message;
            }

            statuses.Add(status);
        }

        // Add unconfigured providers
        foreach (ImageAIProvider provider in Enum.GetValues(typeof(ImageAIProvider)))
        {
            if (!_generators.ContainsKey(provider))
            {
                statuses.Add(new ImageProviderStatus
                {
                    Provider = provider,
                    IsConfigured = false,
                    IsAvailable = false,
                    Message = "Not configured",
                    LastChecked = DateTime.UtcNow
                });
            }
        }

        return statuses;
    }

    /// <summary>
    /// Check if any image provider is available
    /// </summary>
    public async Task<bool> IsAnyProviderAvailableAsync(CancellationToken ct = default)
    {
        var statuses = await GetAllProvidersStatusAsync(ct);
        return statuses.Any(s => s.IsAvailable);
    }

    /// <summary>
    /// Get first available provider
    /// </summary>
    public async Task<ImageAIProvider?> GetFirstAvailableProviderAsync(CancellationToken ct = default)
    {
        // Priority order: StableDiffusion (free local) > Leonardo (free tier) > DALL-E (paid)
        var priority = new[] 
        { 
            ImageAIProvider.StableDiffusion, 
            ImageAIProvider.Leonardo, 
            ImageAIProvider.DallE 
        };

        foreach (var provider in priority)
        {
            if (_generators.TryGetValue(provider, out var generator))
            {
                bool isAvailable = provider switch
                {
                    ImageAIProvider.StableDiffusion => await ((StableDiffusionImageGenerator)generator).IsAvailableAsync(ct),
                    ImageAIProvider.DallE => await ((DallEImageGenerator)generator).IsAvailableAsync(ct),
                    ImageAIProvider.Leonardo => await ((LeonardoImageGenerator)generator).IsAvailableAsync(ct),
                    _ => false
                };

                if (isAvailable) return provider;
            }
        }

        return null;
    }

    private List<ImageAIProvider> GetProvidersToTry(ImageAIProvider? preferred)
    {
        var providers = new List<ImageAIProvider>();

        if (preferred.HasValue)
        {
            providers.Add(preferred.Value);
        }

        // Add in priority order (free first)
        var priority = new[] 
        { 
            ImageAIProvider.StableDiffusion,
            ImageAIProvider.Leonardo, 
            ImageAIProvider.DallE 
        };

        foreach (var p in priority)
        {
            if (!providers.Contains(p))
            {
                providers.Add(p);
            }
        }

        return providers;
    }
}
