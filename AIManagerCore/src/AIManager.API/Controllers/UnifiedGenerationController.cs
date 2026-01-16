using System.ComponentModel.DataAnnotations;
using AIManager.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace AIManager.API.Controllers;

/// <summary>
/// Unified Generation API - รวมทุก provider ไว้ในที่เดียว
/// รองรับทั้ง Local GPU และ External APIs
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class UnifiedGenerationController : ControllerBase
{
    private readonly ILogger<UnifiedGenerationController> _logger;
    private readonly UnifiedGenerationEngine _engine;

    public UnifiedGenerationController(
        ILogger<UnifiedGenerationController> logger,
        UnifiedGenerationEngine engine)
    {
        _logger = logger;
        _engine = engine;
    }

    #region Provider Status

    /// <summary>
    /// Get status of all providers
    /// </summary>
    [HttpGet("providers")]
    public async Task<IActionResult> GetProvidersStatus(CancellationToken ct)
    {
        var status = await _engine.CheckAllProvidersAsync(ct);
        return Ok(new
        {
            success = true,
            data = status.Values.Select(s => new
            {
                provider = s.Provider.ToString(),
                available = s.IsAvailable,
                message = s.Message,
                free = s.IsFree,
                supportsImage = s.SupportsImage,
                supportsVideo = s.SupportsVideo,
                vram = s.TotalVramGb > 0 ? new { total = s.TotalVramGb, free = s.FreeVramGb } : null,
                speed = s.EstimatedSpeed
            })
        });
    }

    /// <summary>
    /// Get best available provider for image generation
    /// </summary>
    [HttpGet("providers/best-image")]
    public async Task<IActionResult> GetBestImageProvider([FromQuery] double? requiredVramGb, CancellationToken ct)
    {
        var provider = await _engine.GetBestImageProviderAsync(requiredVramGb, ct);
        if (provider == null)
        {
            return Ok(new { success = false, message = "No available provider for image generation" });
        }
        return Ok(new { success = true, provider = provider.ToString() });
    }

    /// <summary>
    /// Get best available provider for video generation
    /// </summary>
    [HttpGet("providers/best-video")]
    public async Task<IActionResult> GetBestVideoProvider(CancellationToken ct)
    {
        var provider = await _engine.GetBestVideoProviderAsync(ct);
        if (provider == null)
        {
            return Ok(new { success = false, message = "No available provider for video generation" });
        }
        return Ok(new { success = true, provider = provider.ToString() });
    }

    #endregion

    #region Configuration

    /// <summary>
    /// Get current configuration
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _engine.Config;
        return Ok(new
        {
            success = true,
            data = new
            {
                providerPriority = config.ProviderPriority.Select(p => p.ToString()),
                enableAutoFallback = config.EnableAutoFallback,
                preferFreeProviders = config.PreferFreeProviders,
                configuredProviders = config.ProviderSettings.Keys.Select(k => k.ToString())
            }
        });
    }

    /// <summary>
    /// Configure a provider API key
    /// </summary>
    [HttpPost("config/provider")]
    public IActionResult ConfigureProvider([FromBody] ConfigureProviderRequest request)
    {
        if (!Enum.TryParse<ProviderType>(request.Provider, out var provider))
        {
            return BadRequest(new { success = false, message = $"Invalid provider: {request.Provider}" });
        }

        _engine.ConfigureProvider(provider, request.ApiKey, request.BaseUrl);
        return Ok(new { success = true, message = $"Provider {provider} configured" });
    }

    /// <summary>
    /// Set provider priority order
    /// </summary>
    [HttpPost("config/priority")]
    public IActionResult SetPriority([FromBody] SetPriorityRequest request)
    {
        var priority = new List<ProviderType>();
        foreach (var p in request.Priority)
        {
            if (Enum.TryParse<ProviderType>(p, out var provider))
            {
                priority.Add(provider);
            }
        }

        if (priority.Count == 0)
        {
            return BadRequest(new { success = false, message = "No valid providers in priority list" });
        }

        _engine.SetProviderPriority(priority);
        return Ok(new { success = true, message = "Priority updated" });
    }

    /// <summary>
    /// Enable/disable a provider
    /// </summary>
    [HttpPost("config/provider/{provider}/enable")]
    public IActionResult EnableProvider(string provider, [FromBody] EnableProviderRequest request)
    {
        if (!Enum.TryParse<ProviderType>(provider, out var providerType))
        {
            return BadRequest(new { success = false, message = $"Invalid provider: {provider}" });
        }

        _engine.SetProviderEnabled(providerType, request.Enabled);
        return Ok(new { success = true, message = $"Provider {provider} {(request.Enabled ? "enabled" : "disabled")}" });
    }

    #endregion

    #region Image Generation

    /// <summary>
    /// Generate image with auto provider selection
    /// </summary>
    [HttpPost("generate/image")]
    public async Task<IActionResult> GenerateImage([FromBody] GenerateImageRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Image generation request: {Prompt}", request.Prompt);

        ProviderType? preferredProvider = null;
        if (!string.IsNullOrEmpty(request.Provider))
        {
            if (Enum.TryParse<ProviderType>(request.Provider, out var p))
            {
                preferredProvider = p;
            }
        }

        var imageRequest = new ImageRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width ?? 1024,
            Height = request.Height ?? 1024,
            Steps = request.Steps ?? 30,
            GuidanceScale = request.GuidanceScale ?? 7.5,
            Seed = request.Seed ?? -1,
            BatchSize = request.BatchSize ?? 1,
            ModelId = request.ModelId
        };

        var result = await _engine.GenerateImageAsync(imageRequest, preferredProvider, ct);

        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                error = result.Error,
                provider = result.Provider.ToString(),
                generationTimeMs = result.GenerationTimeMs
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                images = result.Images,
                seed = result.Seed,
                provider = result.Provider.ToString(),
                generationTimeMs = result.GenerationTimeMs
            }
        });
    }

    /// <summary>
    /// Quick test - generate a simple image
    /// </summary>
    [HttpPost("test/image")]
    public async Task<IActionResult> TestImage([FromBody] QuickTestRequest request, CancellationToken ct)
    {
        var prompt = request.Prompt ?? "A beautiful sunset over mountains, digital art";

        var imageRequest = new ImageRequest
        {
            Prompt = prompt,
            Width = 512,
            Height = 512,
            Steps = 20,
            BatchSize = 1
        };

        var result = await _engine.GenerateImageAsync(imageRequest, ct: ct);

        return Ok(new
        {
            success = result.Success,
            error = result.Error,
            provider = result.Provider.ToString(),
            generationTimeMs = result.GenerationTimeMs,
            hasImage = result.Images?.Count > 0
        });
    }

    #endregion

    #region Distributed Generation

    /// <summary>
    /// Generate images using distributed GPU pool
    /// Distributes batch across multiple GPU workers for faster generation
    /// </summary>
    [HttpPost("generate/distributed")]
    public async Task<IActionResult> GenerateDistributed([FromBody] DistributedImageRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Distributed generation request: {Prompt} x{BatchSize}", request.Prompt, request.BatchSize);

        var imageRequest = new ImageRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            Width = request.Width ?? 1024,
            Height = request.Height ?? 1024,
            Steps = request.Steps ?? 30,
            GuidanceScale = request.GuidanceScale ?? 7.5,
            Seed = request.Seed ?? -1,
            BatchSize = request.BatchSize ?? 4,
            ModelId = request.ModelId
        };

        // Parse distribution mode
        var mode = DistributionMode.Parallel;
        if (!string.IsNullOrEmpty(request.Mode))
        {
            Enum.TryParse<DistributionMode>(request.Mode, true, out mode);
        }

        var options = new DistributedGenerationOptions
        {
            MaxWorkers = request.MaxWorkers ?? 0,
            MinImagesPerWorker = request.MinImagesPerWorker ?? 1,
            FallbackToOtherProviders = request.FallbackToOtherProviders ?? true,
            Mode = mode
        };

        var result = await _engine.GenerateDistributedAsync(imageRequest, options, ct);

        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                error = result.Error,
                provider = result.Provider.ToString(),
                generationTimeMs = result.GenerationTimeMs
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                images = result.Images,
                imageCount = result.Images?.Count ?? 0,
                seed = result.Seed,
                provider = result.Provider.ToString(),
                generationTimeMs = result.GenerationTimeMs,
                workersUsed = result.WorkersUsed
            }
        });
    }

    /// <summary>
    /// Get GPU pool status and statistics
    /// </summary>
    [HttpGet("pool/status")]
    public async Task<IActionResult> GetPoolStatus(CancellationToken ct)
    {
        var providers = await _engine.CheckAllProvidersAsync(ct);

        if (!providers.TryGetValue(ProviderType.DistributedGpuPool, out var poolStatus))
        {
            return Ok(new
            {
                success = true,
                available = false,
                message = "GPU Pool not configured"
            });
        }

        return Ok(new
        {
            success = true,
            available = poolStatus.IsAvailable,
            message = poolStatus.Message,
            totalVramGb = poolStatus.TotalVramGb,
            freeVramGb = poolStatus.FreeVramGb,
            estimatedSpeed = poolStatus.EstimatedSpeed,
            supportsImage = poolStatus.SupportsImage,
            supportsVideo = poolStatus.SupportsVideo
        });
    }

    /// <summary>
    /// Get comprehensive system health status
    /// ตรวจสอบสถานะทุกระบบ: GPU Pool, Local GPU, External APIs
    /// </summary>
    [HttpGet("health")]
    public async Task<IActionResult> GetSystemHealth(CancellationToken ct)
    {
        var providers = await _engine.CheckAllProvidersAsync(ct);

        var health = new
        {
            success = true,
            timestamp = DateTime.UtcNow,
            overall = providers.Values.Any(p => p.IsAvailable) ? "healthy" : "degraded",

            // Local resources
            local = new
            {
                diffusers = providers.GetValueOrDefault(ProviderType.LocalDiffusers)?.IsAvailable ?? false,
                comfyui = providers.GetValueOrDefault(ProviderType.LocalComfyUI)?.IsAvailable ?? false,
                gpuPool = providers.GetValueOrDefault(ProviderType.DistributedGpuPool)?.IsAvailable ?? false,
                gpuPoolMessage = providers.GetValueOrDefault(ProviderType.DistributedGpuPool)?.Message ?? "Not configured"
            },

            // External APIs
            external = new
            {
                falAi = providers.GetValueOrDefault(ProviderType.FalAI)?.IsAvailable ?? false,
                replicate = providers.GetValueOrDefault(ProviderType.Replicate)?.IsAvailable ?? false,
                huggingface = providers.GetValueOrDefault(ProviderType.HuggingFaceInference)?.IsAvailable ?? false,
                togetherAi = providers.GetValueOrDefault(ProviderType.TogetherAI)?.IsAvailable ?? false,
                stabilityAi = providers.GetValueOrDefault(ProviderType.StabilityAI)?.IsAvailable ?? false
            },

            // Capabilities
            capabilities = new
            {
                canGenerateImage = providers.Values.Any(p => p.IsAvailable && p.SupportsImage),
                canGenerateVideo = providers.Values.Any(p => p.IsAvailable && p.SupportsVideo),
                hasFreeProvider = providers.Values.Any(p => p.IsAvailable && p.IsFree),
                availableProviders = providers.Values.Where(p => p.IsAvailable).Select(p => p.Provider.ToString()).ToList()
            },

            // Provider details
            providers = providers.Values.Select(p => new
            {
                provider = p.Provider.ToString(),
                available = p.IsAvailable,
                message = p.Message,
                free = p.IsFree,
                supportsImage = p.SupportsImage,
                supportsVideo = p.SupportsVideo,
                vramTotal = p.TotalVramGb > 0 ? p.TotalVramGb : (double?)null,
                vramFree = p.FreeVramGb > 0 ? p.FreeVramGb : (double?)null,
                speed = p.EstimatedSpeed
            })
        };

        return Ok(health);
    }

    /// <summary>
    /// Get available distribution modes
    /// </summary>
    [HttpGet("modes")]
    public IActionResult GetDistributionModes()
    {
        return Ok(new
        {
            success = true,
            modes = new[]
            {
                new { name = "Parallel", description = "แต่ละ GPU ทำงานพร้อมกัน รวมผลลัพธ์ - เหมาะสำหรับ batch generation" },
                new { name = "Combined", description = "รวมพลัง GPU หลายตัว - เหมาะสำหรับรูปใหญ่/video ยาว" },
                new { name = "RoundRobin", description = "หมุนเวียน GPU - เหมาะสำหรับ load balancing" },
                new { name = "Failover", description = "ใช้ตัวแรกที่ว่าง ถ้าล้มเหลวไปตัวถัดไป - เหมาะสำหรับ high availability" }
            }
        });
    }

    #endregion

    #region Video Generation

    /// <summary>
    /// Generate video with auto provider selection
    /// </summary>
    [HttpPost("generate/video")]
    public async Task<IActionResult> GenerateVideo([FromBody] GenerateVideoRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Video generation request: {Prompt}", request.Prompt);

        ProviderType? preferredProvider = null;
        if (!string.IsNullOrEmpty(request.Provider))
        {
            if (Enum.TryParse<ProviderType>(request.Provider, out var p))
            {
                preferredProvider = p;
            }
        }

        var videoRequest = new VideoRequest
        {
            Prompt = request.Prompt,
            NegativePrompt = request.NegativePrompt,
            SourceImageBase64 = request.SourceImage,
            Width = request.Width ?? 512,
            Height = request.Height ?? 512,
            NumFrames = request.NumFrames ?? 25,
            Fps = request.Fps ?? 8,
            Steps = request.Steps ?? 25,
            GuidanceScale = request.GuidanceScale ?? 6.0,
            Seed = request.Seed ?? -1,
            ModelId = request.ModelId
        };

        var result = await _engine.GenerateVideoAsync(videoRequest, preferredProvider, ct);

        if (!result.Success)
        {
            return Ok(new
            {
                success = false,
                error = result.Error,
                provider = result.Provider.ToString(),
                generationTimeMs = result.GenerationTimeMs
            });
        }

        return Ok(new
        {
            success = true,
            data = new
            {
                videoBase64 = result.VideoBase64,
                frames = result.VideoFrames,
                seed = result.Seed,
                provider = result.Provider.ToString(),
                generationTimeMs = result.GenerationTimeMs
            }
        });
    }

    /// <summary>
    /// Quick test - generate a simple video
    /// </summary>
    [HttpPost("test/video")]
    public async Task<IActionResult> TestVideo([FromBody] QuickTestRequest request, CancellationToken ct)
    {
        var prompt = request.Prompt ?? "A cat playing with a ball, smooth animation";

        var videoRequest = new VideoRequest
        {
            Prompt = prompt,
            Width = 512,
            Height = 512,
            NumFrames = 16,
            Fps = 8,
            Steps = 20
        };

        var result = await _engine.GenerateVideoAsync(videoRequest, ct: ct);

        return Ok(new
        {
            success = result.Success,
            error = result.Error,
            provider = result.Provider.ToString(),
            generationTimeMs = result.GenerationTimeMs,
            hasVideo = !string.IsNullOrEmpty(result.VideoBase64) || (result.VideoFrames?.Count > 0)
        });
    }

    #endregion
}

#region Request Models

public class ConfigureProviderRequest
{
    [Required]
    public string Provider { get; set; } = "";

    [Required]
    public string ApiKey { get; set; } = "";

    public string? BaseUrl { get; set; }
}

public class SetPriorityRequest
{
    [Required]
    public List<string> Priority { get; set; } = new();
}

public class EnableProviderRequest
{
    public bool Enabled { get; set; } = true;
}

public class GenerateImageRequest
{
    [Required]
    public string Prompt { get; set; } = "";

    public string? NegativePrompt { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Steps { get; set; }
    public double? GuidanceScale { get; set; }
    public int? Seed { get; set; }
    public int? BatchSize { get; set; }
    public string? ModelId { get; set; }
    public string? Provider { get; set; }
}

public class GenerateVideoRequest
{
    [Required]
    public string Prompt { get; set; } = "";

    public string? NegativePrompt { get; set; }
    public string? SourceImage { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? NumFrames { get; set; }
    public int? Fps { get; set; }
    public int? Steps { get; set; }
    public double? GuidanceScale { get; set; }
    public int? Seed { get; set; }
    public string? ModelId { get; set; }
    public string? Provider { get; set; }
}

public class DistributedImageRequest
{
    [Required]
    public string Prompt { get; set; } = "";

    public string? NegativePrompt { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Steps { get; set; }
    public double? GuidanceScale { get; set; }
    public int? Seed { get; set; }
    public int? BatchSize { get; set; }
    public string? ModelId { get; set; }

    // Distribution options
    public int? MaxWorkers { get; set; }
    public int? MinImagesPerWorker { get; set; }
    public bool? FallbackToOtherProviders { get; set; }

    /// <summary>
    /// Distribution mode: Parallel, Combined, RoundRobin, Failover
    /// </summary>
    public string? Mode { get; set; }
}

public class QuickTestRequest
{
    public string? Prompt { get; set; }
}

#endregion
