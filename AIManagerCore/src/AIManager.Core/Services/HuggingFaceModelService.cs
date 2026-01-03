using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Service for managing HuggingFace models - Download, Cache, and Configuration
/// Replaces ComfyUI with direct HuggingFace integration
/// </summary>
public class HuggingFaceModelService : IDisposable
{
    private readonly ILogger<HuggingFaceModelService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory;
    private readonly string _configFile;
    private readonly ConcurrentDictionary<string, ModelInfo> _loadedModels = new();
    private readonly ConcurrentDictionary<string, DownloadProgress> _activeDownloads = new();
    private HuggingFaceConfig _config;
    private bool _disposed;

    public const string HF_API_BASE = "https://huggingface.co/api";
    public const string HF_DOWNLOAD_BASE = "https://huggingface.co";

    /// <summary>
    /// Event raised when download progress changes
    /// </summary>
    public event EventHandler<ModelDownloadProgressEventArgs>? DownloadProgressChanged;

    /// <summary>
    /// Event raised when a model is loaded/unloaded
    /// </summary>
    public event EventHandler<ModelStatusEventArgs>? ModelStatusChanged;

    public HuggingFaceModelService(ILogger<HuggingFaceModelService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostXAgent/1.0");

        // Setup directories
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var baseDir = Path.Combine(appData, "PostXAgent");
        _modelsDirectory = Path.Combine(baseDir, "models");
        _configFile = Path.Combine(baseDir, "hf_config.json");

        Directory.CreateDirectory(_modelsDirectory);
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "checkpoints"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "loras"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "vae"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "controlnet"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "embeddings"));

        _config = LoadConfig();
        _logger?.LogInformation("HuggingFace Model Service initialized. Models directory: {Dir}", _modelsDirectory);
    }

    #region Configuration

    /// <summary>
    /// Gets current configuration
    /// </summary>
    public HuggingFaceConfig Config => _config;

    /// <summary>
    /// Sets HuggingFace API token for private models
    /// </summary>
    public void SetApiToken(string token)
    {
        _config.ApiToken = token;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        SaveConfig();
        _logger?.LogInformation("HuggingFace API token configured");
    }

    /// <summary>
    /// Sets the models directory
    /// </summary>
    public void SetModelsDirectory(string path)
    {
        _config.ModelsDirectory = path;
        SaveConfig();
        _logger?.LogInformation("Models directory changed to: {Path}", path);
    }

    /// <summary>
    /// Gets the current models directory
    /// </summary>
    public string ModelsDirectory =>
        string.IsNullOrEmpty(_config.ModelsDirectory) ? _modelsDirectory : _config.ModelsDirectory;

    private HuggingFaceConfig LoadConfig()
    {
        try
        {
            if (File.Exists(_configFile))
            {
                var json = File.ReadAllText(_configFile);
                var config = JsonSerializer.Deserialize<HuggingFaceConfig>(json);
                if (config != null)
                {
                    if (!string.IsNullOrEmpty(config.ApiToken))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", config.ApiToken);
                    }
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load HuggingFace config");
        }
        return new HuggingFaceConfig();
    }

    private void SaveConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFile, json);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save HuggingFace config");
        }
    }

    #endregion

    #region Model Discovery

    /// <summary>
    /// Search models on HuggingFace
    /// </summary>
    public async Task<List<HuggingFaceModelInfo>> SearchModelsAsync(
        string query,
        ModelType? type = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        try
        {
            var filter = type switch
            {
                ModelType.TextToImage => "text-to-image",
                ModelType.ImageToImage => "image-to-image",
                ModelType.TextToVideo => "text-to-video",
                ModelType.LoRA => "lora",
                _ => "diffusers"
            };

            var url = $"{HF_API_BASE}/models?search={Uri.EscapeDataString(query)}&filter={filter}&limit={limit}&sort=downloads&direction=-1";

            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var models = await response.Content.ReadFromJsonAsync<List<HuggingFaceModelInfo>>(ct);
            return models ?? new List<HuggingFaceModelInfo>();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search models: {Query}", query);
            return new List<HuggingFaceModelInfo>();
        }
    }

    /// <summary>
    /// Get model info from HuggingFace
    /// </summary>
    public async Task<HuggingFaceModelInfo?> GetModelInfoAsync(string modelId, CancellationToken ct = default)
    {
        try
        {
            var url = $"{HF_API_BASE}/models/{modelId}";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
                return null;

            return await response.Content.ReadFromJsonAsync<HuggingFaceModelInfo>(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get model info: {ModelId}", modelId);
            return null;
        }
    }

    /// <summary>
    /// Get popular/recommended models for each category
    /// </summary>
    public Dictionary<ModelType, List<RecommendedModel>> GetRecommendedModels()
    {
        return new Dictionary<ModelType, List<RecommendedModel>>
        {
            [ModelType.TextToImage] = new()
            {
                new RecommendedModel
                {
                    Id = "stabilityai/stable-diffusion-xl-base-1.0",
                    Name = "SDXL 1.0",
                    Description = "Stable Diffusion XL - High quality 1024x1024 images",
                    RequiredVramGb = 8,
                    SizeGb = 6.5
                },
                new RecommendedModel
                {
                    Id = "runwayml/stable-diffusion-v1-5",
                    Name = "SD 1.5",
                    Description = "Classic Stable Diffusion - Good for most uses",
                    RequiredVramGb = 4,
                    SizeGb = 4.0
                },
                new RecommendedModel
                {
                    Id = "black-forest-labs/FLUX.1-schnell",
                    Name = "FLUX Schnell",
                    Description = "Fast high-quality generation",
                    RequiredVramGb = 12,
                    SizeGb = 23.0
                },
                new RecommendedModel
                {
                    Id = "black-forest-labs/FLUX.1-dev",
                    Name = "FLUX Dev",
                    Description = "Best quality FLUX model",
                    RequiredVramGb = 16,
                    SizeGb = 23.0
                },
                new RecommendedModel
                {
                    Id = "SG161222/Realistic_Vision_V5.1_noVAE",
                    Name = "Realistic Vision V5.1",
                    Description = "Photorealistic image generation",
                    RequiredVramGb = 4,
                    SizeGb = 2.0
                },
                new RecommendedModel
                {
                    Id = "Lykon/dreamshaper-8",
                    Name = "DreamShaper 8",
                    Description = "Artistic and creative generations",
                    RequiredVramGb = 4,
                    SizeGb = 2.0
                }
            },
            [ModelType.TextToVideo] = new()
            {
                new RecommendedModel
                {
                    Id = "stabilityai/stable-video-diffusion-img2vid-xt",
                    Name = "SVD-XT",
                    Description = "Image to video generation",
                    RequiredVramGb = 16,
                    SizeGb = 9.0
                },
                new RecommendedModel
                {
                    Id = "guoyww/animatediff-motion-adapter-v1-5-2",
                    Name = "AnimateDiff v1.5.2",
                    Description = "Text to video animation",
                    RequiredVramGb = 8,
                    SizeGb = 1.5
                },
                new RecommendedModel
                {
                    Id = "Wan-AI/Wan2.1-T2V-14B",
                    Name = "Wan2.1 T2V",
                    Description = "Latest text-to-video model",
                    RequiredVramGb = 24,
                    SizeGb = 28.0
                }
            },
            [ModelType.LoRA] = new()
            {
                new RecommendedModel
                {
                    Id = "latent-consistency/lcm-lora-sdxl",
                    Name = "LCM LoRA SDXL",
                    Description = "Speed up SDXL generation (4-8 steps)",
                    RequiredVramGb = 0,
                    SizeGb = 0.4
                },
                new RecommendedModel
                {
                    Id = "ByteDance/SDXL-Lightning",
                    Name = "SDXL Lightning",
                    Description = "Ultra-fast SDXL (1-4 steps)",
                    RequiredVramGb = 0,
                    SizeGb = 0.8
                }
            },
            [ModelType.ControlNet] = new()
            {
                new RecommendedModel
                {
                    Id = "diffusers/controlnet-canny-sdxl-1.0",
                    Name = "ControlNet Canny SDXL",
                    Description = "Edge detection control",
                    RequiredVramGb = 2,
                    SizeGb = 2.5
                },
                new RecommendedModel
                {
                    Id = "diffusers/controlnet-depth-sdxl-1.0",
                    Name = "ControlNet Depth SDXL",
                    Description = "Depth map control",
                    RequiredVramGb = 2,
                    SizeGb = 2.5
                }
            },
            [ModelType.VAE] = new()
            {
                new RecommendedModel
                {
                    Id = "stabilityai/sdxl-vae",
                    Name = "SDXL VAE",
                    Description = "Official SDXL VAE",
                    RequiredVramGb = 1,
                    SizeGb = 0.3
                },
                new RecommendedModel
                {
                    Id = "madebyollin/sdxl-vae-fp16-fix",
                    Name = "SDXL VAE FP16 Fix",
                    Description = "Fixed VAE for FP16",
                    RequiredVramGb = 1,
                    SizeGb = 0.3
                }
            }
        };
    }

    #endregion

    #region Model Download

    /// <summary>
    /// Download a model from HuggingFace
    /// </summary>
    public async Task<ModelInfo?> DownloadModelAsync(
        string modelId,
        ModelType type,
        string? revision = null,
        CancellationToken ct = default)
    {
        var downloadId = Guid.NewGuid().ToString("N")[..8];
        var progress = new DownloadProgress
        {
            ModelId = modelId,
            Status = DownloadStatus.Preparing
        };
        _activeDownloads[downloadId] = progress;

        try
        {
            _logger?.LogInformation("Starting download: {ModelId}", modelId);

            // Get model files
            var filesUrl = $"{HF_API_BASE}/models/{modelId}/tree/{revision ?? "main"}";
            var response = await _httpClient.GetAsync(filesUrl, ct);
            response.EnsureSuccessStatusCode();

            var files = await response.Content.ReadFromJsonAsync<List<HuggingFaceFile>>(ct);
            if (files == null || files.Count == 0)
            {
                throw new Exception("No files found in model repository");
            }

            // Determine which files to download based on model type
            var filesToDownload = GetFilesToDownload(files, type);
            var totalSize = filesToDownload.Sum(f => f.Size);

            progress.TotalBytes = totalSize;
            progress.Status = DownloadStatus.Downloading;

            // Create model directory
            var modelDir = GetModelPath(modelId, type);
            Directory.CreateDirectory(modelDir);

            // Download each file
            long downloadedBytes = 0;
            foreach (var file in filesToDownload)
            {
                ct.ThrowIfCancellationRequested();
                progress.CurrentFile = file.Path;

                var fileUrl = $"{HF_DOWNLOAD_BASE}/{modelId}/resolve/{revision ?? "main"}/{file.Path}";
                var destPath = Path.Combine(modelDir, file.Path.Replace("/", Path.DirectorySeparatorChar.ToString()));

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                await DownloadFileWithProgressAsync(fileUrl, destPath, file.Size,
                    downloaded =>
                    {
                        progress.DownloadedBytes = downloadedBytes + downloaded;
                        progress.Speed = CalculateSpeed(progress);
                        DownloadProgressChanged?.Invoke(this, new ModelDownloadProgressEventArgs(progress));
                    }, ct);

                downloadedBytes += file.Size;
            }

            // Create model info
            var modelInfo = new ModelInfo
            {
                Id = modelId,
                Name = modelId.Split('/').Last(),
                Type = type,
                LocalPath = modelDir,
                SizeBytes = totalSize,
                DownloadedAt = DateTime.UtcNow,
                Revision = revision ?? "main"
            };

            // Save model metadata
            await SaveModelMetadataAsync(modelInfo);

            progress.Status = DownloadStatus.Completed;
            _logger?.LogInformation("Download completed: {ModelId}", modelId);

            return modelInfo;
        }
        catch (OperationCanceledException)
        {
            progress.Status = DownloadStatus.Cancelled;
            _logger?.LogInformation("Download cancelled: {ModelId}", modelId);
            return null;
        }
        catch (Exception ex)
        {
            progress.Status = DownloadStatus.Failed;
            progress.Error = ex.Message;
            _logger?.LogError(ex, "Download failed: {ModelId}", modelId);
            throw;
        }
        finally
        {
            _activeDownloads.TryRemove(downloadId, out _);
        }
    }

    private List<HuggingFaceFile> GetFilesToDownload(List<HuggingFaceFile> files, ModelType type)
    {
        // Filter files based on model type
        var extensions = type switch
        {
            ModelType.LoRA => new[] { ".safetensors", ".bin", ".pt" },
            ModelType.VAE => new[] { ".safetensors", ".bin", ".pt" },
            ModelType.Embedding => new[] { ".safetensors", ".bin", ".pt" },
            _ => new[] { ".safetensors", ".bin", ".pt", ".json", ".txt", ".yaml", ".yml" }
        };

        return files
            .Where(f => f.Type == "file")
            .Where(f => extensions.Any(ext => f.Path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private async Task DownloadFileWithProgressAsync(
        string url,
        string destPath,
        long expectedSize,
        Action<long> progressCallback,
        CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;
            progressCallback(totalRead);
        }
    }

    private double CalculateSpeed(DownloadProgress progress)
    {
        // Simple speed calculation (would be more accurate with time tracking)
        return progress.DownloadedBytes / 1024.0 / 1024.0; // MB
    }

    /// <summary>
    /// Cancel an active download
    /// </summary>
    public void CancelDownload(string downloadId)
    {
        if (_activeDownloads.TryGetValue(downloadId, out var progress))
        {
            progress.Status = DownloadStatus.Cancelled;
        }
    }

    /// <summary>
    /// Get active downloads
    /// </summary>
    public IReadOnlyList<DownloadProgress> GetActiveDownloads()
    {
        return _activeDownloads.Values.ToList();
    }

    #endregion

    #region Model Management

    /// <summary>
    /// Get all downloaded models
    /// </summary>
    public async Task<List<ModelInfo>> GetDownloadedModelsAsync(ModelType? filterType = null)
    {
        var models = new List<ModelInfo>();
        var metadataDir = Path.Combine(ModelsDirectory, ".metadata");

        if (!Directory.Exists(metadataDir))
            return models;

        foreach (var file in Directory.GetFiles(metadataDir, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var model = JsonSerializer.Deserialize<ModelInfo>(json);
                if (model != null && (filterType == null || model.Type == filterType))
                {
                    // Check if model files still exist
                    if (Directory.Exists(model.LocalPath))
                    {
                        models.Add(model);
                    }
                }
            }
            catch
            {
                // Skip invalid metadata files
            }
        }

        return models.OrderByDescending(m => m.DownloadedAt).ToList();
    }

    /// <summary>
    /// Delete a downloaded model
    /// </summary>
    public async Task<bool> DeleteModelAsync(string modelId)
    {
        try
        {
            var models = await GetDownloadedModelsAsync();
            var model = models.FirstOrDefault(m => m.Id == modelId);

            if (model == null)
                return false;

            // Delete model files
            if (Directory.Exists(model.LocalPath))
            {
                Directory.Delete(model.LocalPath, true);
            }

            // Delete metadata
            var metadataFile = GetMetadataPath(modelId);
            if (File.Exists(metadataFile))
            {
                File.Delete(metadataFile);
            }

            _loadedModels.TryRemove(modelId, out _);
            ModelStatusChanged?.Invoke(this, new ModelStatusEventArgs(modelId, ModelStatus.Deleted));

            _logger?.LogInformation("Model deleted: {ModelId}", modelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete model: {ModelId}", modelId);
            return false;
        }
    }

    /// <summary>
    /// Get model path
    /// </summary>
    public string GetModelPath(string modelId, ModelType type)
    {
        var subDir = type switch
        {
            ModelType.LoRA => "loras",
            ModelType.VAE => "vae",
            ModelType.ControlNet => "controlnet",
            ModelType.Embedding => "embeddings",
            _ => "checkpoints"
        };

        var safeName = modelId.Replace("/", "--").Replace("\\", "--");
        return Path.Combine(ModelsDirectory, subDir, safeName);
    }

    private string GetMetadataPath(string modelId)
    {
        var metadataDir = Path.Combine(ModelsDirectory, ".metadata");
        Directory.CreateDirectory(metadataDir);

        var safeName = modelId.Replace("/", "--").Replace("\\", "--");
        return Path.Combine(metadataDir, $"{safeName}.json");
    }

    private async Task SaveModelMetadataAsync(ModelInfo model)
    {
        var path = GetMetadataPath(model.Id);
        var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    /// <summary>
    /// Check if model is downloaded
    /// </summary>
    public async Task<bool> IsModelDownloadedAsync(string modelId)
    {
        var models = await GetDownloadedModelsAsync();
        return models.Any(m => m.Id == modelId);
    }

    /// <summary>
    /// Get model storage usage
    /// </summary>
    public async Task<StorageUsage> GetStorageUsageAsync()
    {
        var models = await GetDownloadedModelsAsync();
        var totalSize = models.Sum(m => m.SizeBytes);

        // Get available disk space
        var driveInfo = new DriveInfo(Path.GetPathRoot(ModelsDirectory) ?? "C:");

        return new StorageUsage
        {
            UsedBytes = totalSize,
            AvailableBytes = driveInfo.AvailableFreeSpace,
            TotalBytes = driveInfo.TotalSize,
            ModelCount = models.Count
        };
    }

    #endregion

    #region Model Loading (for generation)

    /// <summary>
    /// Mark a model as loaded (for tracking in UI)
    /// </summary>
    public void MarkModelLoaded(string modelId, ModelInfo info)
    {
        _loadedModels[modelId] = info;
        ModelStatusChanged?.Invoke(this, new ModelStatusEventArgs(modelId, ModelStatus.Loaded));
    }

    /// <summary>
    /// Mark a model as unloaded
    /// </summary>
    public void MarkModelUnloaded(string modelId)
    {
        _loadedModels.TryRemove(modelId, out _);
        ModelStatusChanged?.Invoke(this, new ModelStatusEventArgs(modelId, ModelStatus.Unloaded));
    }

    /// <summary>
    /// Get loaded models
    /// </summary>
    public IReadOnlyList<ModelInfo> GetLoadedModels()
    {
        return _loadedModels.Values.ToList();
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}

#region Models

/// <summary>
/// HuggingFace configuration
/// </summary>
public class HuggingFaceConfig
{
    public string? ApiToken { get; set; }
    public string? ModelsDirectory { get; set; }
    public bool AutoUpdateModels { get; set; } = false;
    public int MaxConcurrentDownloads { get; set; } = 2;
    public Dictionary<string, ModelSettings> ModelSettings { get; set; } = new();
}

/// <summary>
/// Per-model settings
/// </summary>
public class ModelSettings
{
    public bool Enabled { get; set; } = true;
    public string? CustomName { get; set; }
    public int Priority { get; set; } = 0;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Model type categories
/// </summary>
public enum ModelType
{
    TextToImage,
    ImageToImage,
    TextToVideo,
    ImageToVideo,
    LoRA,
    ControlNet,
    VAE,
    Embedding,
    Upscaler
}

/// <summary>
/// Model status
/// </summary>
public enum ModelStatus
{
    NotDownloaded,
    Downloading,
    Downloaded,
    Loaded,
    Unloaded,
    Deleted
}

/// <summary>
/// Download status
/// </summary>
public enum DownloadStatus
{
    Preparing,
    Downloading,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Local model information
/// </summary>
public class ModelInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ModelType Type { get; set; }
    public string LocalPath { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime DownloadedAt { get; set; }
    public string Revision { get; set; } = "main";
    public string? Description { get; set; }
    public double RequiredVramGb { get; set; }
    public ModelSettings Settings { get; set; } = new();

    public string SizeDisplay => SizeBytes switch
    {
        < 1024 => $"{SizeBytes} B",
        < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{SizeBytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{SizeBytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };
}

/// <summary>
/// HuggingFace API model info
/// </summary>
public class HuggingFaceModelInfo
{
    [JsonPropertyName("_id")]
    public string? InternalId { get; set; }

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = "";

    [JsonPropertyName("author")]
    public string? Author { get; set; }

    [JsonPropertyName("downloads")]
    public long Downloads { get; set; }

    [JsonPropertyName("likes")]
    public int Likes { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("pipeline_tag")]
    public string? PipelineTag { get; set; }

    [JsonPropertyName("lastModified")]
    public string? LastModified { get; set; }

    [JsonPropertyName("private")]
    public bool Private { get; set; }

    public string DownloadsDisplay => Downloads switch
    {
        < 1000 => Downloads.ToString(),
        < 1000000 => $"{Downloads / 1000.0:F1}K",
        _ => $"{Downloads / 1000000.0:F1}M"
    };
}

/// <summary>
/// HuggingFace file info
/// </summary>
public class HuggingFaceFile
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("oid")]
    public string? Oid { get; set; }

    [JsonPropertyName("lfs")]
    public HuggingFaceLfsInfo? Lfs { get; set; }
}

public class HuggingFaceLfsInfo
{
    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }
}

/// <summary>
/// Recommended model
/// </summary>
public class RecommendedModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public double RequiredVramGb { get; set; }
    public double SizeGb { get; set; }
}

/// <summary>
/// Download progress
/// </summary>
public class DownloadProgress
{
    public string ModelId { get; set; } = "";
    public DownloadStatus Status { get; set; }
    public long TotalBytes { get; set; }
    public long DownloadedBytes { get; set; }
    public string? CurrentFile { get; set; }
    public double Speed { get; set; }
    public string? Error { get; set; }

    public double Percentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;
}

/// <summary>
/// Storage usage info
/// </summary>
public class StorageUsage
{
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public long TotalBytes { get; set; }
    public int ModelCount { get; set; }

    public string UsedDisplay => FormatBytes(UsedBytes);
    public string AvailableDisplay => FormatBytes(AvailableBytes);
    public double UsagePercent => TotalBytes > 0 ? (double)UsedBytes / TotalBytes * 100 : 0;

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };
}

/// <summary>
/// Event args for download progress
/// </summary>
public class ModelDownloadProgressEventArgs : EventArgs
{
    public DownloadProgress Progress { get; }
    public ModelDownloadProgressEventArgs(DownloadProgress progress) => Progress = progress;
}

/// <summary>
/// Event args for model status changes
/// </summary>
public class ModelStatusEventArgs : EventArgs
{
    public string ModelId { get; }
    public ModelStatus Status { get; }
    public ModelStatusEventArgs(string modelId, ModelStatus status)
    {
        ModelId = modelId;
        Status = status;
    }
}

#endregion
