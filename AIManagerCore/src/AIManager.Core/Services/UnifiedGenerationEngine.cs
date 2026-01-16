using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Unified Generation Engine - รวมทุก provider ไว้ในที่เดียว
/// รองรับทั้ง Local GPU และ External APIs
///
/// Local GPU Providers:
/// - Diffusers (HuggingFace models) - FLUX, SDXL, SD1.5, CogVideoX, LTX-Video
/// - ComfyUI backend
/// - Ollama (for LLM-based image gen)
///
/// External API Providers:
/// - fal.ai (FLUX, SDXL Lightning)
/// - Replicate (any model)
/// - HuggingFace Inference API
/// - Together.ai
/// - Stability AI
/// </summary>
public class UnifiedGenerationEngine : IDisposable
{
    private readonly ILogger<UnifiedGenerationEngine>? _logger;
    private readonly HttpClient _httpClient;
    private readonly LocalGpuService _gpuService;
    private readonly HuggingFaceModelService _modelService;
    private readonly DiffusersGenerationEngine _diffusersEngine;
    private readonly ComfyUIService? _comfyService;
    private readonly GpuPoolService? _gpuPoolService;

    private UnifiedEngineConfig _config;
    private readonly string _configPath;
    private bool _disposed;

    // Provider status cache
    private readonly Dictionary<ProviderType, GenerationProviderStatus> _providerStatus = new();
    private DateTime _lastStatusCheck = DateTime.MinValue;
    private readonly TimeSpan _statusCacheExpiry = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Event for generation progress
    /// </summary>
    public event EventHandler<UnifiedProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Event for provider status changes
    /// </summary>
    public event EventHandler<GenerationProviderStatusEventArgs>? ProviderStatusChanged;

    public UnifiedGenerationEngine(
        HuggingFaceModelService modelService,
        LocalGpuService? gpuService = null,
        ComfyUIService? comfyService = null,
        GpuPoolService? gpuPoolService = null,
        ILogger<UnifiedGenerationEngine>? logger = null)
    {
        _logger = logger;
        _modelService = modelService;
        _gpuService = gpuService ?? new LocalGpuService();
        _comfyService = comfyService;
        _gpuPoolService = gpuPoolService;

        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _diffusersEngine = new DiffusersGenerationEngine(modelService, _gpuService);

        // Setup config
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _configPath = Path.Combine(appData, "PostXAgent", "unified_engine_config.json");
        _config = LoadConfig();

        // Subscribe to diffusers events
        _diffusersEngine.ProgressChanged += (s, e) =>
        {
            ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
            {
                Provider = ProviderType.LocalDiffusers,
                Step = e.Step,
                TotalSteps = e.TotalSteps,
                Message = $"Step {e.Step}/{e.TotalSteps}"
            });
        };
    }

    #region Configuration

    /// <summary>
    /// Get current configuration
    /// </summary>
    public UnifiedEngineConfig Config => _config;

    /// <summary>
    /// Configure API keys for providers
    /// </summary>
    public void ConfigureProvider(ProviderType provider, string apiKey, string? baseUrl = null)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(provider) ?? new ProviderSettings();
        settings.ApiKey = apiKey;
        if (baseUrl != null) settings.BaseUrl = baseUrl;
        settings.Enabled = true;

        _config.ProviderSettings[provider] = settings;
        SaveConfig();

        _logger?.LogInformation("Provider {Provider} configured", provider);
    }

    /// <summary>
    /// Set provider priority order
    /// </summary>
    public void SetProviderPriority(List<ProviderType> priorityOrder)
    {
        _config.ProviderPriority = priorityOrder;
        SaveConfig();
    }

    /// <summary>
    /// Enable/disable a provider
    /// </summary>
    public void SetProviderEnabled(ProviderType provider, bool enabled)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(provider) ?? new ProviderSettings();
        settings.Enabled = enabled;
        _config.ProviderSettings[provider] = settings;
        SaveConfig();
    }

    private UnifiedEngineConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<UnifiedEngineConfig>(json) ?? new UnifiedEngineConfig();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load unified engine config");
        }
        return new UnifiedEngineConfig();
    }

    private void SaveConfig()
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save unified engine config");
        }
    }

    #endregion

    #region Provider Status

    /// <summary>
    /// Check all provider availability
    /// </summary>
    public async Task<Dictionary<ProviderType, GenerationProviderStatus>> CheckAllProvidersAsync(CancellationToken ct = default)
    {
        if (DateTime.Now - _lastStatusCheck < _statusCacheExpiry && _providerStatus.Count > 0)
        {
            return _providerStatus;
        }

        var tasks = new List<Task>();

        // Check Local Diffusers
        tasks.Add(Task.Run(async () =>
        {
            var status = await CheckLocalDiffusersAsync(ct);
            lock (_providerStatus) _providerStatus[ProviderType.LocalDiffusers] = status;
        }, ct));

        // Check ComfyUI
        tasks.Add(Task.Run(async () =>
        {
            var status = await CheckComfyUIAsync(ct);
            lock (_providerStatus) _providerStatus[ProviderType.LocalComfyUI] = status;
        }, ct));

        // Check Distributed GPU Pool
        tasks.Add(Task.Run(() =>
        {
            var status = CheckDistributedGpuPool();
            lock (_providerStatus) _providerStatus[ProviderType.DistributedGpuPool] = status;
            return Task.CompletedTask;
        }, ct));

        // Check External APIs
        foreach (var provider in new[] { ProviderType.FalAI, ProviderType.Replicate, ProviderType.HuggingFaceInference, ProviderType.TogetherAI, ProviderType.StabilityAI })
        {
            tasks.Add(Task.Run(async () =>
            {
                var status = await CheckExternalProviderAsync(provider, ct);
                lock (_providerStatus) _providerStatus[provider] = status;
            }, ct));
        }

        await Task.WhenAll(tasks);
        _lastStatusCheck = DateTime.Now;

        return _providerStatus;
    }

    private async Task<GenerationProviderStatus> CheckLocalDiffusersAsync(CancellationToken ct)
    {
        try
        {
            var gpu = await _gpuService.DetectGpuAsync(ct: ct);
            var python = await _gpuService.CheckPythonEnvironmentAsync(ct);
            var models = await _modelService.GetDownloadedModelsAsync();

            return new GenerationProviderStatus
            {
                Provider = ProviderType.LocalDiffusers,
                IsAvailable = gpu.IsAvailable && python.IsReady && models.Count > 0,
                Message = gpu.IsAvailable
                    ? python.IsReady
                        ? models.Count > 0
                            ? $"{gpu.Name} - {models.Count} models ready"
                            : "No models downloaded"
                        : $"Python packages missing: {string.Join(", ", python.MissingPackages)}"
                    : "No GPU detected",
                TotalVramGb = gpu.TotalVramGb,
                FreeVramGb = gpu.FreeVramGb,
                IsFree = true,
                SupportsImage = true,
                SupportsVideo = true,
                EstimatedSpeed = gpu.IsAvailable ? "Fast (Local)" : "Slow (CPU)"
            };
        }
        catch (Exception ex)
        {
            return new GenerationProviderStatus
            {
                Provider = ProviderType.LocalDiffusers,
                IsAvailable = false,
                Message = ex.Message,
                IsFree = true
            };
        }
    }

    private async Task<GenerationProviderStatus> CheckComfyUIAsync(CancellationToken ct)
    {
        if (_comfyService == null)
        {
            return new GenerationProviderStatus
            {
                Provider = ProviderType.LocalComfyUI,
                IsAvailable = false,
                Message = "ComfyUI service not initialized",
                IsFree = true
            };
        }

        try
        {
            var isAvailable = await _comfyService.IsAvailableAsync(ct);
            return new GenerationProviderStatus
            {
                Provider = ProviderType.LocalComfyUI,
                IsAvailable = isAvailable,
                Message = isAvailable ? $"Online at {_comfyService.BaseUrl}" : "Offline",
                IsFree = true,
                SupportsImage = true,
                SupportsVideo = true,
                EstimatedSpeed = "Fast (Local)"
            };
        }
        catch (Exception ex)
        {
            return new GenerationProviderStatus
            {
                Provider = ProviderType.LocalComfyUI,
                IsAvailable = false,
                Message = ex.Message,
                IsFree = true
            };
        }
    }

    private GenerationProviderStatus CheckDistributedGpuPool()
    {
        if (_gpuPoolService == null)
        {
            return new GenerationProviderStatus
            {
                Provider = ProviderType.DistributedGpuPool,
                IsAvailable = false,
                Message = "GPU Pool service not initialized",
                IsFree = true
            };
        }

        var onlineWorkers = _gpuPoolService.OnlineWorkers;
        var totalVram = _gpuPoolService.TotalAvailableVram;
        var workerCount = onlineWorkers.Count;

        return new GenerationProviderStatus
        {
            Provider = ProviderType.DistributedGpuPool,
            IsAvailable = workerCount > 0,
            Message = workerCount > 0
                ? $"{workerCount} workers online - {totalVram:F1}GB VRAM - Mode: {_gpuPoolService.DistributionMode}"
                : "No online workers",
            TotalVramGb = _gpuPoolService.Workers.Sum(w => w.TotalVramGb),
            FreeVramGb = totalVram,
            IsFree = true,
            SupportsImage = true,
            SupportsVideo = true,
            EstimatedSpeed = workerCount > 1 ? $"Very Fast ({workerCount}x parallel)" : "Fast (Distributed)"
        };
    }

    private async Task<GenerationProviderStatus> CheckExternalProviderAsync(ProviderType provider, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(provider);
        if (settings == null || string.IsNullOrEmpty(settings.ApiKey) || !settings.Enabled)
        {
            return new GenerationProviderStatus
            {
                Provider = provider,
                IsAvailable = false,
                Message = settings?.Enabled == false ? "Disabled" : "Not configured - API key required",
                IsFree = false
            };
        }

        try
        {
            var (isAvailable, message) = provider switch
            {
                ProviderType.FalAI => await CheckFalAIAsync(settings.ApiKey, ct),
                ProviderType.Replicate => await CheckReplicateAsync(settings.ApiKey, ct),
                ProviderType.HuggingFaceInference => await CheckHuggingFaceAsync(settings.ApiKey, ct),
                ProviderType.TogetherAI => await CheckTogetherAIAsync(settings.ApiKey, ct),
                ProviderType.StabilityAI => await CheckStabilityAIAsync(settings.ApiKey, ct),
                _ => (false, "Unknown provider")
            };

            return new GenerationProviderStatus
            {
                Provider = provider,
                IsAvailable = isAvailable,
                Message = message,
                IsFree = false,
                SupportsImage = true,
                SupportsVideo = provider is ProviderType.FalAI or ProviderType.Replicate,
                EstimatedSpeed = "Fast (Cloud)"
            };
        }
        catch (Exception ex)
        {
            return new GenerationProviderStatus
            {
                Provider = provider,
                IsAvailable = false,
                Message = ex.Message,
                IsFree = false
            };
        }
    }

    private async Task<(bool, string)> CheckFalAIAsync(string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://fal.run/health");
        request.Headers.Add("Authorization", $"Key {apiKey}");
        var response = await _httpClient.SendAsync(request, ct);
        return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Ready" : "API key invalid");
    }

    private async Task<(bool, string)> CheckReplicateAsync(string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.replicate.com/v1/models");
        request.Headers.Add("Authorization", $"Token {apiKey}");
        var response = await _httpClient.SendAsync(request, ct);
        return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Ready" : "API key invalid");
    }

    private async Task<(bool, string)> CheckHuggingFaceAsync(string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://huggingface.co/api/whoami");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        var response = await _httpClient.SendAsync(request, ct);
        return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Ready" : "Token invalid");
    }

    private async Task<(bool, string)> CheckTogetherAIAsync(string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.together.xyz/v1/models");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        var response = await _httpClient.SendAsync(request, ct);
        return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Ready" : "API key invalid");
    }

    private async Task<(bool, string)> CheckStabilityAIAsync(string apiKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.stability.ai/v1/user/account");
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        var response = await _httpClient.SendAsync(request, ct);
        return (response.IsSuccessStatusCode, response.IsSuccessStatusCode ? "Ready" : "API key invalid");
    }

    /// <summary>
    /// Get best available provider for image generation
    /// </summary>
    public async Task<ProviderType?> GetBestImageProviderAsync(double? requiredVramGb = null, CancellationToken ct = default)
    {
        var status = await CheckAllProvidersAsync(ct);

        foreach (var provider in _config.ProviderPriority)
        {
            if (status.TryGetValue(provider, out var s) && s.IsAvailable && s.SupportsImage)
            {
                // Check VRAM requirement for local providers
                if (provider == ProviderType.LocalDiffusers && requiredVramGb.HasValue)
                {
                    if (s.FreeVramGb < requiredVramGb.Value) continue;
                }
                return provider;
            }
        }

        return null;
    }

    /// <summary>
    /// Get best available provider for video generation
    /// </summary>
    public async Task<ProviderType?> GetBestVideoProviderAsync(CancellationToken ct = default)
    {
        var status = await CheckAllProvidersAsync(ct);

        foreach (var provider in _config.ProviderPriority)
        {
            if (status.TryGetValue(provider, out var s) && s.IsAvailable && s.SupportsVideo)
            {
                return provider;
            }
        }

        return null;
    }

    #endregion

    #region Image Generation

    /// <summary>
    /// Generate image with auto provider selection
    /// </summary>
    public async Task<GenerationResult> GenerateImageAsync(
        ImageRequest request,
        ProviderType? preferredProvider = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Select provider
        var provider = preferredProvider ?? await GetBestImageProviderAsync(request.RequiredVramGb, ct);
        if (provider == null)
        {
            return new GenerationResult
            {
                Success = false,
                Error = "No available provider for image generation"
            };
        }

        _logger?.LogInformation("Generating image with provider: {Provider}", provider);
        ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
        {
            Provider = provider.Value,
            Message = $"Starting generation with {provider}"
        });

        try
        {
            var result = provider switch
            {
                ProviderType.LocalDiffusers => await GenerateWithDiffusersAsync(request, ct),
                ProviderType.LocalComfyUI => await GenerateWithComfyUIAsync(request, ct),
                ProviderType.DistributedGpuPool => await GenerateWithGpuPoolAsync(request, ct),
                ProviderType.FalAI => await GenerateWithFalAIAsync(request, ct),
                ProviderType.Replicate => await GenerateWithReplicateAsync(request, ct),
                ProviderType.HuggingFaceInference => await GenerateWithHuggingFaceAsync(request, ct),
                ProviderType.TogetherAI => await GenerateWithTogetherAIAsync(request, ct),
                ProviderType.StabilityAI => await GenerateWithStabilityAIAsync(request, ct),
                _ => new GenerationResult { Success = false, Error = "Unknown provider" }
            };

            result.Provider = provider.Value;
            result.GenerationTimeMs = sw.ElapsedMilliseconds;

            // Auto-fallback if failed
            if (!result.Success && _config.EnableAutoFallback && preferredProvider == null)
            {
                _logger?.LogWarning("Provider {Provider} failed, trying fallback", provider);

                var fallbackProvider = await GetFallbackProviderAsync(provider.Value, ct);
                if (fallbackProvider != null)
                {
                    return await GenerateImageAsync(request, fallbackProvider, ct);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image generation failed with provider: {Provider}", provider);

            // Auto-fallback on exception
            if (_config.EnableAutoFallback && preferredProvider == null)
            {
                var fallbackProvider = await GetFallbackProviderAsync(provider.Value, ct);
                if (fallbackProvider != null)
                {
                    return await GenerateImageAsync(request, fallbackProvider, ct);
                }
            }

            return new GenerationResult
            {
                Success = false,
                Error = ex.Message,
                Provider = provider.Value,
                GenerationTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<ProviderType?> GetFallbackProviderAsync(ProviderType failed, CancellationToken ct)
    {
        var status = await CheckAllProvidersAsync(ct);
        var idx = _config.ProviderPriority.IndexOf(failed);

        for (int i = idx + 1; i < _config.ProviderPriority.Count; i++)
        {
            var provider = _config.ProviderPriority[i];
            if (status.TryGetValue(provider, out var s) && s.IsAvailable && s.SupportsImage)
            {
                return provider;
            }
        }

        return null;
    }

    #endregion

    #region Provider Implementations - Image

    private async Task<GenerationResult> GenerateWithDiffusersAsync(ImageRequest request, CancellationToken ct)
    {
        // Start engine if needed
        if (!_diffusersEngine.IsRunning)
        {
            var startResult = await _diffusersEngine.StartAsync(ct: ct);
            if (!startResult.Success)
            {
                return new GenerationResult { Success = false, Error = startResult.Message };
            }
        }

        // Load model if needed
        var modelId = request.ModelId;
        if (string.IsNullOrEmpty(modelId))
        {
            var models = await _modelService.GetDownloadedModelsAsync(ModelType.TextToImage);
            modelId = models.FirstOrDefault()?.Id;
            if (modelId == null)
            {
                return new GenerationResult { Success = false, Error = "No models available" };
            }
        }

        var loadResult = await _diffusersEngine.LoadModelAsync(modelId, ModelType.TextToImage, ct: ct);
        if (!loadResult.Success)
        {
            return new GenerationResult { Success = false, Error = loadResult.Error };
        }

        // Generate
        var diffusersRequest = new DiffusersImageRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width,
            Height = request.Height,
            Steps = request.Steps,
            GuidanceScale = request.GuidanceScale,
            Seed = request.Seed,
            BatchSize = request.BatchSize
        };

        var result = await _diffusersEngine.GenerateImageAsync(diffusersRequest, ct);

        return new GenerationResult
        {
            Success = result.Success,
            Images = result.Images,
            Seed = result.Seed,
            Error = result.Error
        };
    }

    private async Task<GenerationResult> GenerateWithComfyUIAsync(ImageRequest request, CancellationToken ct)
    {
        if (_comfyService == null)
        {
            return new GenerationResult { Success = false, Error = "ComfyUI service not available" };
        }

        var comfyRequest = new ImageGenerationRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width,
            Height = request.Height,
            Steps = request.Steps,
            CfgScale = request.GuidanceScale,
            Seed = request.Seed
        };

        var result = await _comfyService.GenerateImageAsync(comfyRequest, ct);

        return new GenerationResult
        {
            Success = result.Images.Count > 0,
            Images = result.Images.Select(i => i.Base64Data).ToList(),
            Seed = request.Seed
        };
    }

    private async Task<GenerationResult> GenerateWithGpuPoolAsync(ImageRequest request, CancellationToken ct)
    {
        if (_gpuPoolService == null)
        {
            return new GenerationResult { Success = false, Error = "GPU Pool service not available" };
        }

        var onlineWorkers = _gpuPoolService.OnlineWorkers;
        if (onlineWorkers.Count == 0)
        {
            return new GenerationResult { Success = false, Error = "No online GPU workers" };
        }

        var gpuRequest = new GpuImageRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width,
            Height = request.Height,
            Steps = request.Steps,
            GuidanceScale = request.GuidanceScale,
            Seed = request.Seed,
            BatchSize = request.BatchSize,
            ModelId = request.ModelId ?? "stabilityai/stable-diffusion-xl-base-1.0",
            RequiredVramGb = request.RequiredVramGb ?? 8.0
        };

        // If batch size > 1 and multiple workers available, distribute
        if (request.BatchSize > 1 && onlineWorkers.Count > 1)
        {
            return await GenerateDistributedBatchAsync(gpuRequest, onlineWorkers.Count, ct);
        }

        // Single generation
        var result = await _gpuPoolService.GenerateImageAsync(gpuRequest, ct);

        return new GenerationResult
        {
            Success = result.Success,
            Images = result.Images,
            Seed = result.Seed,
            Error = result.Error
        };
    }

    /// <summary>
    /// Distribute batch generation across multiple GPU workers
    /// ความสามารถพิเศษ: แบ่งงานไปหลาย GPU แล้วรวมผลลัพธ์
    /// </summary>
    private async Task<GenerationResult> GenerateDistributedBatchAsync(
        GpuImageRequest request,
        int workerCount,
        CancellationToken ct)
    {
        if (_gpuPoolService == null)
        {
            return new GenerationResult { Success = false, Error = "GPU Pool not available" };
        }

        var totalBatch = request.BatchSize;
        var imagesPerWorker = Math.Max(1, totalBatch / workerCount);
        var remainder = totalBatch % workerCount;

        _logger?.LogInformation(
            "Distributing batch of {Total} images across {Workers} workers ({PerWorker} each)",
            totalBatch, workerCount, imagesPerWorker);

        ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
        {
            Provider = ProviderType.DistributedGpuPool,
            Message = $"Distributing {totalBatch} images across {workerCount} workers..."
        });

        // Create tasks for each worker
        var tasks = new List<Task<GpuTaskResult>>();
        var baseSeed = request.Seed >= 0 ? request.Seed : new Random().Next();

        for (int i = 0; i < workerCount; i++)
        {
            var workerBatchSize = imagesPerWorker + (i < remainder ? 1 : 0);
            if (workerBatchSize == 0) continue;

            var workerRequest = new GpuImageRequest
            {
                Prompt = request.Prompt,
                NegativePrompt = request.NegativePrompt,
                Width = request.Width,
                Height = request.Height,
                Steps = request.Steps,
                GuidanceScale = request.GuidanceScale,
                Seed = baseSeed + (i * 1000), // Different seed per worker
                BatchSize = workerBatchSize,
                ModelId = request.ModelId,
                RequiredVramGb = request.RequiredVramGb
            };

            tasks.Add(_gpuPoolService.GenerateImageAsync(workerRequest, ct));
        }

        // Wait for all workers
        var results = await Task.WhenAll(tasks);

        // Combine results
        var allImages = new List<string>();
        var errors = new List<string>();
        var finalSeed = baseSeed;

        foreach (var result in results)
        {
            if (result.Success)
            {
                allImages.AddRange(result.Images);
                if (result.Seed > 0) finalSeed = result.Seed;
            }
            else if (!string.IsNullOrEmpty(result.Error))
            {
                errors.Add(result.Error);
            }
        }

        ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
        {
            Provider = ProviderType.DistributedGpuPool,
            Message = $"Completed: {allImages.Count}/{totalBatch} images from {workerCount} workers"
        });

        return new GenerationResult
        {
            Success = allImages.Count > 0,
            Images = allImages,
            Seed = finalSeed,
            Error = errors.Count > 0 && allImages.Count == 0
                ? string.Join("; ", errors)
                : null
        };
    }

    /// <summary>
    /// Generate images with explicit distribution across all available GPU workers
    /// รองรับหลาย mode: Parallel, Combined, RoundRobin, Failover
    /// ถ้าไม่มี GPU Pool จะ fallback ไป providers อื่นโดยอัตโนมัติ
    /// </summary>
    public async Task<GenerationResult> GenerateDistributedAsync(
        ImageRequest request,
        DistributedGenerationOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new DistributedGenerationOptions();
        var sw = Stopwatch.StartNew();

        // ขั้นตอนที่ 1: ตรวจสอบ GPU Pool
        var onlineWorkers = _gpuPoolService?.OnlineWorkers ?? new List<GpuWorkerInfo>();

        if (onlineWorkers.Count == 0)
        {
            _logger?.LogInformation("No GPU Pool workers available, checking fallback options");

            // Fallback chain: GPU Pool -> Local GPU -> External API
            if (options.FallbackToOtherProviders)
            {
                return await GenerateWithFallbackChainAsync(request, ct);
            }
            return new GenerationResult
            {
                Success = false,
                Error = "No GPU workers available and fallback disabled",
                GenerationTimeMs = sw.ElapsedMilliseconds
            };
        }

        // ขั้นตอนที่ 2: เลือก mode การทำงาน
        var result = options.Mode switch
        {
            DistributionMode.Parallel => await GenerateParallelModeAsync(request, onlineWorkers, options, ct),
            DistributionMode.Combined => await GenerateCombinedModeAsync(request, onlineWorkers, options, ct),
            DistributionMode.RoundRobin => await GenerateRoundRobinModeAsync(request, onlineWorkers, options, ct),
            DistributionMode.Failover => await GenerateFailoverModeAsync(request, onlineWorkers, options, ct),
            _ => await GenerateParallelModeAsync(request, onlineWorkers, options, ct)
        };

        result.Provider = ProviderType.DistributedGpuPool;
        result.GenerationTimeMs = sw.ElapsedMilliseconds;
        result.WorkersUsed = onlineWorkers.Count;

        return result;
    }

    /// <summary>
    /// Fallback chain: ลองทุก provider ที่มีจนกว่าจะสำเร็จ
    /// GPU Pool -> Local Diffusers -> ComfyUI -> External APIs
    /// </summary>
    private async Task<GenerationResult> GenerateWithFallbackChainAsync(
        ImageRequest request,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var errors = new List<string>();

        // 1. ลอง Local Diffusers ก่อน (ฟรี)
        _logger?.LogInformation("Fallback: Trying Local Diffusers");
        try
        {
            var localGpu = await _gpuService.DetectGpuAsync(ct: ct);
            if (localGpu.IsAvailable)
            {
                var result = await GenerateWithDiffusersAsync(request, ct);
                if (result.Success)
                {
                    result.Provider = ProviderType.LocalDiffusers;
                    result.GenerationTimeMs = sw.ElapsedMilliseconds;
                    return result;
                }
                errors.Add($"Diffusers: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Diffusers: {ex.Message}");
        }

        // 2. ลอง ComfyUI (ถ้ามี)
        if (_comfyService != null)
        {
            _logger?.LogInformation("Fallback: Trying ComfyUI");
            try
            {
                var comfyAvailable = await _comfyService.IsAvailableAsync(ct);
                if (comfyAvailable)
                {
                    var result = await GenerateWithComfyUIAsync(request, ct);
                    if (result.Success)
                    {
                        result.Provider = ProviderType.LocalComfyUI;
                        result.GenerationTimeMs = sw.ElapsedMilliseconds;
                        return result;
                    }
                    errors.Add($"ComfyUI: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"ComfyUI: {ex.Message}");
            }
        }

        // 3. ลอง External APIs ตาม priority
        var externalProviders = new[]
        {
            ProviderType.FalAI,
            ProviderType.TogetherAI,
            ProviderType.HuggingFaceInference,
            ProviderType.Replicate,
            ProviderType.StabilityAI
        };

        foreach (var provider in externalProviders)
        {
            var settings = _config.ProviderSettings.GetValueOrDefault(provider);
            if (settings?.Enabled != true || string.IsNullOrEmpty(settings.ApiKey))
                continue;

            _logger?.LogInformation("Fallback: Trying {Provider}", provider);
            try
            {
                var result = await GenerateImageAsync(request, provider, ct);
                if (result.Success)
                {
                    result.GenerationTimeMs = sw.ElapsedMilliseconds;
                    return result;
                }
                errors.Add($"{provider}: {result.Error}");
            }
            catch (Exception ex)
            {
                errors.Add($"{provider}: {ex.Message}");
            }
        }

        // ไม่มี provider ไหนใช้ได้เลย
        return new GenerationResult
        {
            Success = false,
            Error = $"All providers failed: {string.Join("; ", errors)}",
            GenerationTimeMs = sw.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Parallel Mode: แต่ละ worker ทำงานพร้อมกัน แล้วรวมผลลัพธ์
    /// </summary>
    private async Task<GenerationResult> GenerateParallelModeAsync(
        ImageRequest request,
        IReadOnlyList<GpuWorkerInfo> workers,
        DistributedGenerationOptions options,
        CancellationToken ct)
    {
        var workersToUse = options.MaxWorkers > 0
            ? Math.Min(options.MaxWorkers, workers.Count)
            : workers.Count;

        var totalBatch = Math.Max(request.BatchSize, options.MinImagesPerWorker * workersToUse);

        var gpuRequest = new GpuImageRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width,
            Height = request.Height,
            Steps = request.Steps,
            GuidanceScale = request.GuidanceScale,
            Seed = request.Seed,
            BatchSize = totalBatch,
            ModelId = request.ModelId ?? "stabilityai/stable-diffusion-xl-base-1.0",
            RequiredVramGb = request.RequiredVramGb ?? 8.0
        };

        return await GenerateDistributedBatchAsync(gpuRequest, workersToUse, ct);
    }

    /// <summary>
    /// Combined Mode: รวมพลัง GPU หลายตัวทำงานเดียวกัน (เหมือน mining pool)
    /// ใช้สำหรับ task ที่ต้องการ VRAM มากกว่า GPU เดียว
    /// </summary>
    private async Task<GenerationResult> GenerateCombinedModeAsync(
        ImageRequest request,
        IReadOnlyList<GpuWorkerInfo> workers,
        DistributedGenerationOptions options,
        CancellationToken ct)
    {
        // Combined mode สำหรับ large generation
        // แบ่งการ render เป็นส่วนๆ แล้วรวมกัน (เช่น tile-based rendering)

        var totalVram = workers.Sum(w => w.FreeVramGb);
        _logger?.LogInformation(
            "Combined mode: {Workers} workers with {Vram:F1}GB total VRAM",
            workers.Count, totalVram);

        ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
        {
            Provider = ProviderType.DistributedGpuPool,
            Message = $"Combined mode: {workers.Count} GPUs ({totalVram:F1}GB VRAM)"
        });

        // สำหรับ Combined mode กับรูปใหญ่ - แบ่ง tile แล้วรวม
        if (request.Width > 1024 || request.Height > 1024)
        {
            return await GenerateTiledImageAsync(request, workers, ct);
        }

        // ถ้ารูปไม่ใหญ่ ใช้ parallel แทน
        return await GenerateParallelModeAsync(request, workers, options, ct);
    }

    /// <summary>
    /// Tiled Image Generation: แบ่งรูปใหญ่เป็น tiles แล้วให้แต่ละ GPU render
    /// </summary>
    private async Task<GenerationResult> GenerateTiledImageAsync(
        ImageRequest request,
        IReadOnlyList<GpuWorkerInfo> workers,
        CancellationToken ct)
    {
        // คำนวณ tiles
        var tileSize = 512;
        var tilesX = (int)Math.Ceiling(request.Width / (double)tileSize);
        var tilesY = (int)Math.Ceiling(request.Height / (double)tileSize);
        var totalTiles = tilesX * tilesY;

        _logger?.LogInformation(
            "Tiled generation: {Width}x{Height} -> {TilesX}x{TilesY} tiles ({Total} total)",
            request.Width, request.Height, tilesX, tilesY, totalTiles);

        // สร้าง tasks สำหรับแต่ละ tile
        var tasks = new List<Task<GpuTaskResult>>();
        var baseSeed = request.Seed >= 0 ? request.Seed : new Random().Next();

        for (int y = 0; y < tilesY; y++)
        {
            for (int x = 0; x < tilesX; x++)
            {
                var tileIndex = y * tilesX + x;
                var workerIndex = tileIndex % workers.Count;

                var tilePrompt = $"{request.Prompt} (region {x},{y} of {tilesX}x{tilesY} grid)";

                var tileRequest = new GpuImageRequest
                {
                    Prompt = tilePrompt,
                    NegativePrompt = request.NegativePrompt,
                    Width = tileSize,
                    Height = tileSize,
                    Steps = request.Steps,
                    GuidanceScale = request.GuidanceScale,
                    Seed = baseSeed + tileIndex,
                    BatchSize = 1,
                    ModelId = request.ModelId,
                    RequiredVramGb = request.RequiredVramGb ?? 4.0
                };

                if (_gpuPoolService != null)
                {
                    tasks.Add(_gpuPoolService.GenerateImageAsync(tileRequest, ct));
                }
            }
        }

        if (tasks.Count == 0)
        {
            return new GenerationResult { Success = false, Error = "No GPU pool available for tiled generation" };
        }

        // รอ tiles ทั้งหมด
        var results = await Task.WhenAll(tasks);

        // รวม tiles (ในความเป็นจริงต้อง stitch รูป แต่ตอนนี้คืน tiles ทั้งหมด)
        var allImages = results.Where(r => r.Success).SelectMany(r => r.Images).ToList();

        return new GenerationResult
        {
            Success = allImages.Count > 0,
            Images = allImages,
            Seed = baseSeed,
            WorkersUsed = workers.Count
        };
    }

    /// <summary>
    /// Round-Robin Mode: หมุนเวียนส่งงานให้ worker ทีละตัว
    /// </summary>
    private int _roundRobinIndex = 0;
    private async Task<GenerationResult> GenerateRoundRobinModeAsync(
        ImageRequest request,
        IReadOnlyList<GpuWorkerInfo> workers,
        DistributedGenerationOptions options,
        CancellationToken ct)
    {
        if (_gpuPoolService == null)
        {
            return new GenerationResult { Success = false, Error = "GPU Pool not available" };
        }

        // เลือก worker ถัดไปตาม round-robin
        _roundRobinIndex = _roundRobinIndex % workers.Count;
        var selectedWorker = workers[_roundRobinIndex];
        _roundRobinIndex = (_roundRobinIndex + 1) % workers.Count;

        _logger?.LogInformation(
            "Round-robin: Selected worker {Name} ({Index}/{Total})",
            selectedWorker.Name, _roundRobinIndex, workers.Count);

        ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
        {
            Provider = ProviderType.DistributedGpuPool,
            Message = $"Round-robin: Using {selectedWorker.Name}"
        });

        var gpuRequest = new GpuImageRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width,
            Height = request.Height,
            Steps = request.Steps,
            GuidanceScale = request.GuidanceScale,
            Seed = request.Seed,
            BatchSize = request.BatchSize,
            ModelId = request.ModelId ?? "stabilityai/stable-diffusion-xl-base-1.0",
            RequiredVramGb = request.RequiredVramGb ?? 8.0
        };

        var result = await _gpuPoolService.GenerateImageAsync(gpuRequest, ct);

        return new GenerationResult
        {
            Success = result.Success,
            Images = result.Images,
            Seed = result.Seed,
            Error = result.Error,
            WorkersUsed = 1,
            WorkerIds = new List<string> { selectedWorker.Id }
        };
    }

    /// <summary>
    /// Failover Mode: ใช้ตัวแรกที่ว่าง ถ้าล้มเหลวไปตัวถัดไป
    /// </summary>
    private async Task<GenerationResult> GenerateFailoverModeAsync(
        ImageRequest request,
        IReadOnlyList<GpuWorkerInfo> workers,
        DistributedGenerationOptions options,
        CancellationToken ct)
    {
        if (_gpuPoolService == null)
        {
            return new GenerationResult { Success = false, Error = "GPU Pool not available" };
        }

        var errors = new List<string>();

        // ลองแต่ละ worker จนกว่าจะสำเร็จ
        foreach (var worker in workers.Where(w => w.CurrentTask == null))
        {
            _logger?.LogInformation("Failover: Trying worker {Name}", worker.Name);

            ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
            {
                Provider = ProviderType.DistributedGpuPool,
                Message = $"Failover: Trying {worker.Name}..."
            });

            var gpuRequest = new GpuImageRequest
            {
                Prompt = request.Prompt,
                NegativePrompt = request.NegativePrompt,
                Width = request.Width,
                Height = request.Height,
                Steps = request.Steps,
                GuidanceScale = request.GuidanceScale,
                Seed = request.Seed,
                BatchSize = request.BatchSize,
                ModelId = request.ModelId ?? "stabilityai/stable-diffusion-xl-base-1.0",
                RequiredVramGb = request.RequiredVramGb ?? 8.0
            };

            try
            {
                var result = await _gpuPoolService.GenerateImageAsync(gpuRequest, ct);

                if (result.Success)
                {
                    return new GenerationResult
                    {
                        Success = true,
                        Images = result.Images,
                        Seed = result.Seed,
                        WorkersUsed = 1,
                        WorkerIds = new List<string> { worker.Id }
                    };
                }

                errors.Add($"{worker.Name}: {result.Error}");
            }
            catch (Exception ex)
            {
                errors.Add($"{worker.Name}: {ex.Message}");
            }
        }

        // Fallback ถ้าทุก worker ล้มเหลว
        if (options.FallbackToOtherProviders)
        {
            _logger?.LogWarning("All GPU workers failed, using fallback chain");
            return await GenerateWithFallbackChainAsync(request, ct);
        }

        return new GenerationResult
        {
            Success = false,
            Error = $"All workers failed: {string.Join("; ", errors)}"
        };
    }

    private async Task<GenerationResult> GenerateWithFalAIAsync(ImageRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.FalAI);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "fal.ai API key not configured" };
        }

        // Use FLUX schnell for speed, or specified model
        var model = request.ModelId ?? "fal-ai/flux/schnell";
        var url = $"https://fal.run/{model}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Authorization", $"Key {settings.ApiKey}");
        httpRequest.Content = JsonContent.Create(new
        {
            prompt = $"{request.Prompt}",
            image_size = new { width = request.Width, height = request.Height },
            num_inference_steps = request.Steps > 0 ? request.Steps : 4,
            num_images = request.BatchSize,
            enable_safety_checker = true,
            seed = request.Seed >= 0 ? request.Seed : (int?)null
        });

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"fal.ai error: {error}" };
        }

        var result = await response.Content.ReadFromJsonAsync<FalAIResponse>(ct);
        if (result?.Images == null || result.Images.Count == 0)
        {
            return new GenerationResult { Success = false, Error = "No images returned" };
        }

        // Download images and convert to base64
        var images = new List<string>();
        foreach (var img in result.Images)
        {
            var bytes = await _httpClient.GetByteArrayAsync(img.Url, ct);
            images.Add(Convert.ToBase64String(bytes));
        }

        return new GenerationResult
        {
            Success = true,
            Images = images,
            Seed = result.Seed ?? request.Seed
        };
    }

    private async Task<GenerationResult> GenerateWithReplicateAsync(ImageRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.Replicate);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "Replicate API key not configured" };
        }

        // Use SDXL-Lightning for speed
        var model = request.ModelId ?? "bytedance/sdxl-lightning-4step";

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.replicate.com/v1/predictions");
        createRequest.Headers.Add("Authorization", $"Token {settings.ApiKey}");
        createRequest.Content = JsonContent.Create(new
        {
            version = model.Contains(":") ? model.Split(':')[1] : null,
            model = model.Contains(":") ? null : model,
            input = new
            {
                prompt = request.Prompt,
                negative_prompt = request.NegativePrompt ?? "",
                width = request.Width,
                height = request.Height,
                num_outputs = request.BatchSize,
                seed = request.Seed >= 0 ? request.Seed : (int?)null
            }
        });

        var createResponse = await _httpClient.SendAsync(createRequest, ct);
        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"Replicate error: {error}" };
        }

        var prediction = await createResponse.Content.ReadFromJsonAsync<ReplicatePrediction>(ct);
        if (prediction == null)
        {
            return new GenerationResult { Success = false, Error = "Failed to create prediction" };
        }

        // Poll for result
        var pollUrl = prediction.Urls?.Get ?? $"https://api.replicate.com/v1/predictions/{prediction.Id}";
        for (int i = 0; i < 120; i++) // Max 2 minutes
        {
            await Task.Delay(1000, ct);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            pollRequest.Headers.Add("Authorization", $"Token {settings.ApiKey}");

            var pollResponse = await _httpClient.SendAsync(pollRequest, ct);
            var status = await pollResponse.Content.ReadFromJsonAsync<ReplicatePrediction>(ct);

            if (status?.Status == "succeeded" && status.Output != null)
            {
                var images = new List<string>();
                foreach (var url in status.Output)
                {
                    var bytes = await _httpClient.GetByteArrayAsync(url, ct);
                    images.Add(Convert.ToBase64String(bytes));
                }
                return new GenerationResult { Success = true, Images = images };
            }
            else if (status?.Status == "failed")
            {
                return new GenerationResult { Success = false, Error = status.Error ?? "Generation failed" };
            }
        }

        return new GenerationResult { Success = false, Error = "Generation timed out" };
    }

    private async Task<GenerationResult> GenerateWithHuggingFaceAsync(ImageRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.HuggingFaceInference);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "HuggingFace token not configured" };
        }

        var model = request.ModelId ?? "stabilityai/stable-diffusion-xl-base-1.0";
        var url = $"https://api-inference.huggingface.co/models/{model}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        httpRequest.Content = JsonContent.Create(new
        {
            inputs = request.Prompt,
            parameters = new
            {
                negative_prompt = request.NegativePrompt,
                width = Math.Min(request.Width, 1024),
                height = Math.Min(request.Height, 1024),
                num_inference_steps = request.Steps,
                guidance_scale = request.GuidanceScale
            }
        });

        var response = await _httpClient.SendAsync(httpRequest, ct);

        // Handle model loading
        if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
        {
            await Task.Delay(20000, ct);
            response = await _httpClient.SendAsync(httpRequest, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"HuggingFace error: {error}" };
        }

        var imageBytes = await response.Content.ReadAsByteArrayAsync(ct);
        return new GenerationResult
        {
            Success = true,
            Images = new List<string> { Convert.ToBase64String(imageBytes) }
        };
    }

    private async Task<GenerationResult> GenerateWithTogetherAIAsync(ImageRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.TogetherAI);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "Together.ai API key not configured" };
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.together.xyz/v1/images/generations");
        httpRequest.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        httpRequest.Content = JsonContent.Create(new
        {
            model = request.ModelId ?? "black-forest-labs/FLUX.1-schnell-Free",
            prompt = request.Prompt,
            negative_prompt = request.NegativePrompt,
            width = request.Width,
            height = request.Height,
            steps = request.Steps,
            n = request.BatchSize,
            seed = request.Seed >= 0 ? request.Seed : (int?)null
        });

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"Together.ai error: {error}" };
        }

        var result = await response.Content.ReadFromJsonAsync<TogetherAIResponse>(ct);
        if (result?.Data == null || result.Data.Count == 0)
        {
            return new GenerationResult { Success = false, Error = "No images returned" };
        }

        var images = new List<string>();
        foreach (var img in result.Data)
        {
            if (!string.IsNullOrEmpty(img.B64Json))
            {
                images.Add(img.B64Json);
            }
            else if (!string.IsNullOrEmpty(img.Url))
            {
                var bytes = await _httpClient.GetByteArrayAsync(img.Url, ct);
                images.Add(Convert.ToBase64String(bytes));
            }
        }

        return new GenerationResult { Success = true, Images = images };
    }

    private async Task<GenerationResult> GenerateWithStabilityAIAsync(ImageRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.StabilityAI);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "Stability AI API key not configured" };
        }

        var engine = request.ModelId ?? "stable-diffusion-xl-1024-v1-0";
        var url = $"https://api.stability.ai/v1/generation/{engine}/text-to-image";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Authorization", $"Bearer {settings.ApiKey}");
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(new
        {
            text_prompts = new[]
            {
                new { text = request.Prompt, weight = 1.0 },
                new { text = request.NegativePrompt ?? "", weight = -1.0 }
            },
            cfg_scale = request.GuidanceScale,
            width = request.Width,
            height = request.Height,
            steps = request.Steps,
            samples = request.BatchSize,
            seed = request.Seed >= 0 ? (uint)request.Seed : 0
        });

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"Stability AI error: {error}" };
        }

        var result = await response.Content.ReadFromJsonAsync<StabilityAIResponse>(ct);
        if (result?.Artifacts == null || result.Artifacts.Count == 0)
        {
            return new GenerationResult { Success = false, Error = "No images returned" };
        }

        return new GenerationResult
        {
            Success = true,
            Images = result.Artifacts.Select(a => a.Base64).ToList(),
            Seed = (int)(result.Artifacts.FirstOrDefault()?.Seed ?? 0)
        };
    }

    #endregion

    #region Video Generation

    /// <summary>
    /// Generate video with auto provider selection
    /// </summary>
    public async Task<GenerationResult> GenerateVideoAsync(
        VideoRequest request,
        ProviderType? preferredProvider = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var provider = preferredProvider ?? await GetBestVideoProviderAsync(ct);
        if (provider == null)
        {
            return new GenerationResult
            {
                Success = false,
                Error = "No available provider for video generation"
            };
        }

        _logger?.LogInformation("Generating video with provider: {Provider}", provider);

        try
        {
            var result = provider switch
            {
                ProviderType.LocalDiffusers => await GenerateVideoWithDiffusersAsync(request, ct),
                ProviderType.LocalComfyUI => await GenerateVideoWithComfyUIAsync(request, ct),
                ProviderType.FalAI => await GenerateVideoWithFalAIAsync(request, ct),
                ProviderType.Replicate => await GenerateVideoWithReplicateAsync(request, ct),
                _ => new GenerationResult { Success = false, Error = "Provider does not support video" }
            };

            result.Provider = provider.Value;
            result.GenerationTimeMs = sw.ElapsedMilliseconds;
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Video generation failed");
            return new GenerationResult
            {
                Success = false,
                Error = ex.Message,
                Provider = provider.Value,
                GenerationTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<GenerationResult> GenerateVideoWithDiffusersAsync(VideoRequest request, CancellationToken ct)
    {
        if (!_diffusersEngine.IsRunning)
        {
            var startResult = await _diffusersEngine.StartAsync(ct: ct);
            if (!startResult.Success)
            {
                return new GenerationResult { Success = false, Error = startResult.Message };
            }
        }

        // Load video model (SVD, CogVideoX, etc.)
        var modelId = request.ModelId;
        if (string.IsNullOrEmpty(modelId))
        {
            var models = await _modelService.GetDownloadedModelsAsync(ModelType.TextToVideo);
            modelId = models.FirstOrDefault()?.Id;
            if (modelId == null)
            {
                return new GenerationResult { Success = false, Error = "No video models available" };
            }
        }

        var loadResult = await _diffusersEngine.LoadModelAsync(modelId, ModelType.TextToVideo, ct: ct);
        if (!loadResult.Success)
        {
            return new GenerationResult { Success = false, Error = loadResult.Error };
        }

        var videoRequest = new DiffusersVideoRequest
        {
            Prompt = request.Prompt,
            Image = request.SourceImageBase64,
            NumFrames = request.NumFrames,
            Fps = request.Fps,
            Seed = request.Seed
        };

        var result = await _diffusersEngine.GenerateVideoAsync(videoRequest, ct);

        return new GenerationResult
        {
            Success = result.Success,
            VideoFrames = result.Frames,
            Seed = result.Seed,
            Error = result.Error
        };
    }

    private async Task<GenerationResult> GenerateVideoWithComfyUIAsync(VideoRequest request, CancellationToken ct)
    {
        if (_comfyService == null)
        {
            return new GenerationResult { Success = false, Error = "ComfyUI service not available" };
        }

        var comfyRequest = new VideoGenerationRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width,
            Height = request.Height,
            Frames = request.NumFrames,
            Fps = request.Fps,
            Steps = request.Steps,
            CfgScale = request.GuidanceScale,
            Method = VideoMethod.AnimateDiff
        };

        var result = await _comfyService.GenerateVideoAsync(comfyRequest, ct);

        if (result.Videos.Count > 0)
        {
            return new GenerationResult
            {
                Success = true,
                VideoBase64 = result.Videos[0].Base64Data
            };
        }

        return new GenerationResult { Success = false, Error = "No video generated" };
    }

    private async Task<GenerationResult> GenerateVideoWithFalAIAsync(VideoRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.FalAI);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "fal.ai API key not configured" };
        }

        // Use LTX-Video or Wan for video generation
        var model = request.ModelId ?? "fal-ai/ltx-video";
        var url = $"https://fal.run/{model}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Headers.Add("Authorization", $"Key {settings.ApiKey}");
        httpRequest.Content = JsonContent.Create(new
        {
            prompt = request.Prompt,
            negative_prompt = request.NegativePrompt,
            num_frames = request.NumFrames,
            fps = request.Fps,
            seed = request.Seed >= 0 ? request.Seed : (int?)null
        });

        var response = await _httpClient.SendAsync(httpRequest, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"fal.ai error: {error}" };
        }

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (result.TryGetProperty("video", out var video) && video.TryGetProperty("url", out var videoUrl))
        {
            var bytes = await _httpClient.GetByteArrayAsync(videoUrl.GetString()!, ct);
            return new GenerationResult
            {
                Success = true,
                VideoBase64 = Convert.ToBase64String(bytes)
            };
        }

        return new GenerationResult { Success = false, Error = "No video returned" };
    }

    private async Task<GenerationResult> GenerateVideoWithReplicateAsync(VideoRequest request, CancellationToken ct)
    {
        var settings = _config.ProviderSettings.GetValueOrDefault(ProviderType.Replicate);
        if (settings?.ApiKey == null)
        {
            return new GenerationResult { Success = false, Error = "Replicate API key not configured" };
        }

        // Use CogVideoX or Wan
        var model = request.ModelId ?? "tencent/hunyuan-video";

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.replicate.com/v1/predictions");
        createRequest.Headers.Add("Authorization", $"Token {settings.ApiKey}");
        createRequest.Content = JsonContent.Create(new
        {
            model = model,
            input = new
            {
                prompt = request.Prompt,
                negative_prompt = request.NegativePrompt ?? "",
                num_frames = request.NumFrames,
                fps = request.Fps,
                seed = request.Seed >= 0 ? request.Seed : (int?)null
            }
        });

        var createResponse = await _httpClient.SendAsync(createRequest, ct);
        if (!createResponse.IsSuccessStatusCode)
        {
            var error = await createResponse.Content.ReadAsStringAsync(ct);
            return new GenerationResult { Success = false, Error = $"Replicate error: {error}" };
        }

        var prediction = await createResponse.Content.ReadFromJsonAsync<ReplicatePrediction>(ct);
        if (prediction == null)
        {
            return new GenerationResult { Success = false, Error = "Failed to create prediction" };
        }

        // Poll for result (video takes longer)
        var pollUrl = prediction.Urls?.Get ?? $"https://api.replicate.com/v1/predictions/{prediction.Id}";
        for (int i = 0; i < 300; i++) // Max 5 minutes for video
        {
            await Task.Delay(1000, ct);

            using var pollRequest = new HttpRequestMessage(HttpMethod.Get, pollUrl);
            pollRequest.Headers.Add("Authorization", $"Token {settings.ApiKey}");

            var pollResponse = await _httpClient.SendAsync(pollRequest, ct);
            var status = await pollResponse.Content.ReadFromJsonAsync<ReplicatePrediction>(ct);

            if (status?.Status == "succeeded" && status.Output != null && status.Output.Count > 0)
            {
                var videoUrl = status.Output[0];
                var bytes = await _httpClient.GetByteArrayAsync(videoUrl, ct);
                return new GenerationResult
                {
                    Success = true,
                    VideoBase64 = Convert.ToBase64String(bytes)
                };
            }
            else if (status?.Status == "failed")
            {
                return new GenerationResult { Success = false, Error = status.Error ?? "Generation failed" };
            }

            // Report progress
            if (status?.Status == "processing")
            {
                ProgressChanged?.Invoke(this, new UnifiedProgressEventArgs
                {
                    Provider = ProviderType.Replicate,
                    Message = $"Processing... ({i}s)"
                });
            }
        }

        return new GenerationResult { Success = false, Error = "Generation timed out" };
    }

    #endregion

    #region Cleanup

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _diffusersEngine.Dispose();
        _httpClient.Dispose();
    }

    #endregion
}

#region Models

/// <summary>
/// Provider types
/// </summary>
public enum ProviderType
{
    // Local providers (FREE)
    LocalDiffusers,
    LocalComfyUI,
    LocalOllama,

    // Distributed GPU Pool (FREE - uses multiple GPU workers)
    DistributedGpuPool,

    // External APIs
    FalAI,
    Replicate,
    HuggingFaceInference,
    TogetherAI,
    StabilityAI
}

/// <summary>
/// Generation provider status information
/// </summary>
public class GenerationProviderStatus
{
    public ProviderType Provider { get; set; }
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = "";
    public bool IsFree { get; set; }
    public bool SupportsImage { get; set; }
    public bool SupportsVideo { get; set; }
    public double TotalVramGb { get; set; }
    public double FreeVramGb { get; set; }
    public string EstimatedSpeed { get; set; } = "";
}

/// <summary>
/// Provider settings
/// </summary>
public class ProviderSettings
{
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Unified engine configuration
/// </summary>
public class UnifiedEngineConfig
{
    public Dictionary<ProviderType, ProviderSettings> ProviderSettings { get; set; } = new();

    /// <summary>
    /// Provider priority order (first = highest priority)
    /// </summary>
    public List<ProviderType> ProviderPriority { get; set; } = new()
    {
        ProviderType.LocalDiffusers,  // Local GPU first (FREE)
        ProviderType.LocalComfyUI,    // ComfyUI second (FREE)
        ProviderType.FalAI,           // Fast cloud API
        ProviderType.TogetherAI,      // Has free tier
        ProviderType.HuggingFaceInference,
        ProviderType.Replicate,
        ProviderType.StabilityAI
    };

    /// <summary>
    /// Auto fallback to next provider if current fails
    /// </summary>
    public bool EnableAutoFallback { get; set; } = true;

    /// <summary>
    /// Prefer free providers
    /// </summary>
    public bool PreferFreeProviders { get; set; } = true;
}

/// <summary>
/// Image generation request
/// </summary>
public class ImageRequest
{
    public string Prompt { get; set; } = "";
    public string? NegativePrompt { get; set; }
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int Steps { get; set; } = 30;
    public double GuidanceScale { get; set; } = 7.5;
    public int Seed { get; set; } = -1;
    public int BatchSize { get; set; } = 1;
    public string? ModelId { get; set; }
    public double? RequiredVramGb { get; set; }
}

/// <summary>
/// Video generation request
/// </summary>
public class VideoRequest
{
    public string Prompt { get; set; } = "";
    public string? NegativePrompt { get; set; }
    public string? SourceImageBase64 { get; set; }
    public int Width { get; set; } = 512;
    public int Height { get; set; } = 512;
    public int NumFrames { get; set; } = 25;
    public int Fps { get; set; } = 8;
    public int Steps { get; set; } = 25;
    public double GuidanceScale { get; set; } = 7.0;
    public int Seed { get; set; } = -1;
    public string? ModelId { get; set; }
}

/// <summary>
/// Generation result
/// </summary>
public class GenerationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public ProviderType Provider { get; set; }
    public long GenerationTimeMs { get; set; }

    // Image results
    public List<string>? Images { get; set; }

    // Video results
    public string? VideoBase64 { get; set; }
    public List<string>? VideoFrames { get; set; }

    public int Seed { get; set; }

    // Distributed generation info
    public int? WorkersUsed { get; set; }
    public List<string>? WorkerIds { get; set; }
}

/// <summary>
/// Options for distributed generation across GPU workers
/// </summary>
public class DistributedGenerationOptions
{
    /// <summary>
    /// Maximum number of workers to use (0 = all available)
    /// </summary>
    public int MaxWorkers { get; set; } = 0;

    /// <summary>
    /// Minimum images per worker (to ensure efficient distribution)
    /// </summary>
    public int MinImagesPerWorker { get; set; } = 1;

    /// <summary>
    /// Fall back to other providers if no GPU workers available
    /// </summary>
    public bool FallbackToOtherProviders { get; set; } = true;

    /// <summary>
    /// Wait for all workers or return partial results
    /// </summary>
    public bool WaitForAll { get; set; } = true;

    /// <summary>
    /// Timeout per worker in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Distribution mode: Parallel (each worker does separate task) or Combined (workers share one task)
    /// </summary>
    public DistributionMode Mode { get; set; } = DistributionMode.Parallel;
}

/// <summary>
/// Distribution mode for multi-GPU generation
/// </summary>
public enum DistributionMode
{
    /// <summary>
    /// Parallel/Round-Robin: แต่ละ worker ทำงานคนละ task แยกกัน แล้วรวมผลลัพธ์
    /// เหมาะสำหรับ: batch generation, ต้องการรูปหลายรูปพร้อมกัน
    /// </summary>
    Parallel,

    /// <summary>
    /// Combined/Mining Pool: GPU หลายตัวรวมพลังทำงานเดียวกัน
    /// เหมาะสำหรับ: รูปให���่มาก, video ยาว, task ที่ต้องการ VRAM มาก
    /// </summary>
    Combined,

    /// <summary>
    /// Round-Robin: หมุนเวียนส่งงานให้ worker ทีละตัว
    /// เหมาะสำหรับ: load balancing, ใช้งานต่อเนื่อง
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Failover: ใช้ตัวแรกที่ว่าง ถ้าไม่ว่างก็ไปตัวถัดไป
    /// เหมาะสำหรับ: high availability
    /// </summary>
    Failover
}

/// <summary>
/// Progress event args
/// </summary>
public class UnifiedProgressEventArgs : EventArgs
{
    public ProviderType Provider { get; set; }
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public string Message { get; set; } = "";
    public double Progress => TotalSteps > 0 ? (double)Step / TotalSteps * 100 : 0;
}

/// <summary>
/// Provider status event args
/// </summary>
public class GenerationProviderStatusEventArgs : EventArgs
{
    public ProviderType Provider { get; set; }
    public GenerationProviderStatus Status { get; set; } = new();
}

#endregion

#region API Response Models

internal class FalAIResponse
{
    [JsonPropertyName("images")]
    public List<FalAIImage>? Images { get; set; }

    [JsonPropertyName("seed")]
    public int? Seed { get; set; }
}

internal class FalAIImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

internal class ReplicatePrediction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("output")]
    public List<string>? Output { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("urls")]
    public ReplicateUrls? Urls { get; set; }
}

internal class ReplicateUrls
{
    [JsonPropertyName("get")]
    public string? Get { get; set; }
}

internal class TogetherAIResponse
{
    [JsonPropertyName("data")]
    public List<TogetherAIImage>? Data { get; set; }
}

internal class TogetherAIImage
{
    [JsonPropertyName("b64_json")]
    public string? B64Json { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

internal class StabilityAIResponse
{
    [JsonPropertyName("artifacts")]
    public List<StabilityAIArtifact>? Artifacts { get; set; }
}

internal class StabilityAIArtifact
{
    [JsonPropertyName("base64")]
    public string Base64 { get; set; } = "";

    [JsonPropertyName("seed")]
    public uint Seed { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

#endregion
