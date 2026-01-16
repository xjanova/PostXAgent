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

        // Load config first to get custom models directory if set
        _config = LoadConfig();

        // Ensure the model directory structure exists (either default or custom)
        EnsureModelDirectoryStructure(ModelsDirectory);
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
    /// Sets the models directory and creates the folder structure
    /// </summary>
    public void SetModelsDirectory(string path)
    {
        _config.ModelsDirectory = path;
        SaveConfig();

        // Create the same folder structure as the default directory
        EnsureModelDirectoryStructure(path);

        _logger?.LogInformation("Models directory changed to: {Path}", path);
    }

    /// <summary>
    /// Ensures the model directory has the correct folder structure
    /// Creates subdirectories for checkpoints, loras, vae, controlnet, embeddings, and metadata
    /// </summary>
    public void EnsureModelDirectoryStructure(string? basePath = null)
    {
        var targetPath = basePath ?? ModelsDirectory;

        try
        {
            Directory.CreateDirectory(targetPath);
            Directory.CreateDirectory(Path.Combine(targetPath, "checkpoints"));
            Directory.CreateDirectory(Path.Combine(targetPath, "loras"));
            Directory.CreateDirectory(Path.Combine(targetPath, "vae"));
            Directory.CreateDirectory(Path.Combine(targetPath, "controlnet"));
            Directory.CreateDirectory(Path.Combine(targetPath, "embeddings"));
            Directory.CreateDirectory(Path.Combine(targetPath, ".metadata"));

            _logger?.LogInformation("Model directory structure ensured at: {Path}", targetPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create model directory structure at: {Path}", targetPath);
            throw;
        }
    }

    /// <summary>
    /// Validates if the model directory has the correct folder structure
    /// </summary>
    public bool ValidateModelDirectoryStructure(string? basePath = null)
    {
        var targetPath = basePath ?? ModelsDirectory;

        var requiredSubDirs = new[] { "checkpoints", "loras", "vae", "controlnet", "embeddings" };

        foreach (var subDir in requiredSubDirs)
        {
            var subDirPath = Path.Combine(targetPath, subDir);
            if (!Directory.Exists(subDirPath))
            {
                return false;
            }
        }

        return Directory.Exists(targetPath);
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
                    SizeGb = 6.5,
                    ThumbnailUrl = "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0_sample.png"
                },
                new RecommendedModel
                {
                    Id = "runwayml/stable-diffusion-v1-5",
                    Name = "SD 1.5",
                    Description = "Classic Stable Diffusion - Good for most uses",
                    RequiredVramGb = 4,
                    SizeGb = 4.0,
                    ThumbnailUrl = "https://huggingface.co/runwayml/stable-diffusion-v1-5/resolve/main/images/v1-5.png"
                },
                new RecommendedModel
                {
                    Id = "black-forest-labs/FLUX.1-schnell",
                    Name = "FLUX Schnell",
                    Description = "Fast high-quality generation",
                    RequiredVramGb = 12,
                    SizeGb = 23.0,
                    ThumbnailUrl = "https://huggingface.co/black-forest-labs/FLUX.1-schnell/resolve/main/images/example.png"
                },
                new RecommendedModel
                {
                    Id = "black-forest-labs/FLUX.1-dev",
                    Name = "FLUX Dev",
                    Description = "Best quality FLUX model",
                    RequiredVramGb = 16,
                    SizeGb = 23.0,
                    ThumbnailUrl = "https://huggingface.co/black-forest-labs/FLUX.1-dev/resolve/main/images/example.png"
                },
                new RecommendedModel
                {
                    Id = "SG161222/Realistic_Vision_V5.1_noVAE",
                    Name = "Realistic Vision V5.1",
                    Description = "Photorealistic image generation",
                    RequiredVramGb = 4,
                    SizeGb = 2.0,
                    ThumbnailUrl = "https://huggingface.co/SG161222/Realistic_Vision_V5.1_noVAE/resolve/main/images/sample.png"
                },
                new RecommendedModel
                {
                    Id = "Lykon/dreamshaper-8",
                    Name = "DreamShaper 8",
                    Description = "Artistic and creative generations",
                    RequiredVramGb = 4,
                    SizeGb = 2.0,
                    ThumbnailUrl = "https://huggingface.co/Lykon/dreamshaper-8/resolve/main/sample.png"
                }
            },
            [ModelType.TextToVideo] = new()
            {
                // Consumer GPU (8-12GB VRAM)
                new RecommendedModel
                {
                    Id = "THUDM/CogVideoX-2b",
                    Name = "CogVideoX-2B",
                    Description = "Best for 8GB GPU - High quality text-to-video",
                    RequiredVramGb = 8,
                    SizeGb = 4.5
                },
                new RecommendedModel
                {
                    Id = "Lightricks/LTX-Video",
                    Name = "LTX-Video",
                    Description = "Fastest video generation - 30fps real-time",
                    RequiredVramGb = 12,
                    SizeGb = 5.0
                },
                new RecommendedModel
                {
                    Id = "Wan-AI/Wan2.1-T2V-1.3B",
                    Name = "Wan 2.1 (1.3B)",
                    Description = "Lightweight Wan model for consumer GPUs",
                    RequiredVramGb = 8,
                    SizeGb = 3.0
                },
                new RecommendedModel
                {
                    Id = "guoyww/animatediff-motion-adapter-v1-5-3",
                    Name = "AnimateDiff v1.5.3",
                    Description = "Text to video animation with SD1.5",
                    RequiredVramGb = 6,
                    SizeGb = 1.5
                },
                // Mid-range GPU (16-24GB VRAM)
                new RecommendedModel
                {
                    Id = "THUDM/CogVideoX-5b",
                    Name = "CogVideoX-5B",
                    Description = "High quality text-to-video for 24GB GPU",
                    RequiredVramGb = 24,
                    SizeGb = 12.0
                },
                new RecommendedModel
                {
                    Id = "stabilityai/stable-video-diffusion-img2vid-xt",
                    Name = "SVD-XT",
                    Description = "Image to video - 25 frames generation",
                    RequiredVramGb = 16,
                    SizeGb = 9.0
                },
                // High-end GPU (40GB+ VRAM)
                new RecommendedModel
                {
                    Id = "Wan-AI/Wan2.1-T2V-14B",
                    Name = "Wan 2.1 (14B)",
                    Description = "Best quality - requires 40GB+ VRAM",
                    RequiredVramGb = 40,
                    SizeGb = 28.0
                },
                new RecommendedModel
                {
                    Id = "tencent/HunyuanVideo",
                    Name = "HunyuanVideo",
                    Description = "Cinema quality - requires 40GB+ VRAM",
                    RequiredVramGb = 40,
                    SizeGb = 26.0
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
                        progress.SpeedBytesPerSecond = CalculateSpeed(progress);
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

            // Try to fetch thumbnail URL
            try
            {
                modelInfo.ThumbnailUrl = await GetModelThumbnailUrlAsync(modelId, ct);
            }
            catch
            {
                // Ignore thumbnail fetch errors during download
            }

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
        var now = DateTime.UtcNow;
        var elapsed = (now - progress.LastUpdateTime).TotalSeconds;

        if (elapsed < 0.1) return progress.SpeedBytesPerSecond; // Too short, keep previous

        var bytesDownloaded = progress.DownloadedBytes - progress.LastDownloadedBytes;
        var speed = bytesDownloaded / elapsed;

        // Update tracking values
        progress.LastUpdateTime = now;
        progress.LastDownloadedBytes = progress.DownloadedBytes;

        // Smooth the speed with exponential moving average
        if (progress.SpeedBytesPerSecond > 0)
        {
            speed = progress.SpeedBytesPerSecond * 0.7 + speed * 0.3;
        }

        return speed;
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
    /// Update thumbnail URL for an existing downloaded model
    /// </summary>
    public async Task<bool> UpdateModelThumbnailAsync(string modelId, string? thumbnailUrl = null, CancellationToken ct = default)
    {
        try
        {
            var metadataPath = GetMetadataPath(modelId);
            if (!File.Exists(metadataPath))
                return false;

            var json = await File.ReadAllTextAsync(metadataPath, ct);
            var model = JsonSerializer.Deserialize<ModelInfo>(json);
            if (model == null)
                return false;

            // If no URL provided, try to fetch from HuggingFace
            if (string.IsNullOrEmpty(thumbnailUrl))
            {
                thumbnailUrl = await GetModelThumbnailUrlAsync(modelId, ct);
            }

            if (!string.IsNullOrEmpty(thumbnailUrl))
            {
                model.ThumbnailUrl = thumbnailUrl;
                await SaveModelMetadataAsync(model);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to update thumbnail for model: {ModelId}", modelId);
            return false;
        }
    }

    /// <summary>
    /// Get thumbnail URL for a model from HuggingFace and alternative sources
    /// Tries common image file patterns used in model repositories
    /// Falls back to web image search if no thumbnail is found
    /// </summary>
    public async Task<string?> GetModelThumbnailUrlAsync(string modelId, CancellationToken ct = default)
    {
        // Common thumbnail file names used by model authors
        var possibleThumbnails = new[]
        {
            "thumbnail.png",
            "thumbnail.jpg",
            "thumbnail.jpeg",
            "preview.png",
            "preview.jpg",
            "sample.png",
            "sample.jpg",
            "sample_0.png",
            "sample_1.png",
            "example.png",
            "example.jpg",
            "cover.png",
            "cover.jpg",
            "images/thumbnail.png",
            "images/preview.png",
            "images/sample.png",
            "samples/sample_0.png",
            "samples/00.png",
            "output.png",
            "output/sample.png"
        };

        try
        {
            // First try to get files list from HuggingFace API
            var filesUrl = $"{HF_API_BASE}/models/{modelId}/tree/main";
            var response = await _httpClient.GetAsync(filesUrl, ct);

            if (response.IsSuccessStatusCode)
            {
                var files = await response.Content.ReadFromJsonAsync<List<HuggingFaceFile>>(ct);
                if (files != null)
                {
                    // Look for image files in the root and images folder
                    var imageFile = files
                        .Where(f => f.Type == "file")
                        .Where(f => IsImageFile(f.Path))
                        .OrderByDescending(f => GetThumbnailPriority(f.Path))
                        .FirstOrDefault();

                    if (imageFile != null)
                    {
                        return $"{HF_DOWNLOAD_BASE}/{modelId}/resolve/main/{imageFile.Path}";
                    }
                }
            }

            // Try common thumbnail URLs directly with HEAD requests
            foreach (var thumbnail in possibleThumbnails)
            {
                var url = $"{HF_DOWNLOAD_BASE}/{modelId}/resolve/main/{thumbnail}";
                try
                {
                    var headResponse = await _httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, url), ct);
                    if (headResponse.IsSuccessStatusCode)
                    {
                        return url;
                    }
                }
                catch
                {
                    // Continue to next thumbnail option
                }
            }

            // Try HuggingFace card thumbnail (used for model cards)
            var cardThumbUrl = $"https://thumbnails.huggingface.co/social-thumbnails/models/{modelId}.png";
            try
            {
                var cardResponse = await _httpClient.SendAsync(
                    new HttpRequestMessage(HttpMethod.Head, cardThumbUrl), ct);
                if (cardResponse.IsSuccessStatusCode)
                {
                    return cardThumbUrl;
                }
            }
            catch
            {
                // Continue
            }

            // Fallback: Search for model thumbnail from web sources
            var webThumbnail = await SearchWebThumbnailAsync(modelId, ct);
            if (!string.IsNullOrEmpty(webThumbnail))
            {
                return webThumbnail;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get thumbnail for model: {ModelId}", modelId);
            return null;
        }
    }

    /// <summary>
    /// Search for model thumbnail from alternative web sources
    /// Uses CivitAI, GitHub, and other model hosting platforms
    /// </summary>
    private async Task<string?> SearchWebThumbnailAsync(string modelId, CancellationToken ct = default)
    {
        try
        {
            var modelName = modelId.Split('/').LastOrDefault() ?? modelId;
            var authorName = modelId.Contains('/') ? modelId.Split('/').First() : null;

            // Try CivitAI (popular model hosting platform)
            var civitaiUrl = await TryCivitAIThumbnailAsync(modelName, ct);
            if (!string.IsNullOrEmpty(civitaiUrl))
            {
                return civitaiUrl;
            }

            // Try GitHub user avatar as fallback for author
            if (!string.IsNullOrEmpty(authorName))
            {
                var githubUrl = $"https://github.com/{authorName}.png?size=200";
                try
                {
                    var ghResponse = await _httpClient.SendAsync(
                        new HttpRequestMessage(HttpMethod.Head, githubUrl), ct);
                    if (ghResponse.IsSuccessStatusCode)
                    {
                        return githubUrl;
                    }
                }
                catch
                {
                    // Continue
                }
            }

            // Try known model sample image URLs for popular models
            var knownThumbnail = GetKnownModelThumbnail(modelId);
            if (!string.IsNullOrEmpty(knownThumbnail))
            {
                return knownThumbnail;
            }

            // Generate placeholder URL based on model type
            return GetPlaceholderThumbnailUrl(modelId);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to search web thumbnail for model: {ModelId}", modelId);
            return null;
        }
    }

    /// <summary>
    /// Try to find thumbnail from CivitAI for the model
    /// </summary>
    private async Task<string?> TryCivitAIThumbnailAsync(string modelName, CancellationToken ct)
    {
        try
        {
            // CivitAI API search
            var searchUrl = $"https://civitai.com/api/v1/models?query={Uri.EscapeDataString(modelName)}&limit=1";
            var response = await _httpClient.GetAsync(searchUrl, ct);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(content);

                if (doc.RootElement.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                {
                    var firstItem = items[0];
                    if (firstItem.TryGetProperty("modelVersions", out var versions) && versions.GetArrayLength() > 0)
                    {
                        var firstVersion = versions[0];
                        if (firstVersion.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                        {
                            var firstImage = images[0];
                            if (firstImage.TryGetProperty("url", out var urlElement))
                            {
                                return urlElement.GetString();
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // CivitAI search failed, continue to other sources
        }

        return null;
    }

    /// <summary>
    /// Get known thumbnail URLs for popular models
    /// </summary>
    private static string? GetKnownModelThumbnail(string modelId)
    {
        // Dictionary of known model thumbnails that may not be available from HuggingFace
        var knownThumbnails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Stable Diffusion variants
            ["CompVis/stable-diffusion-v1-4"] = "https://raw.githubusercontent.com/CompVis/stable-diffusion/main/assets/stable-samples/txt2img/merged-0005.png",
            ["runwayml/stable-diffusion-v1-5"] = "https://raw.githubusercontent.com/runwayml/stable-diffusion/main/assets/stable-samples/txt2img/merged-0005.png",

            // FLUX models
            ["black-forest-labs/FLUX.1-schnell"] = "https://cdn-uploads.huggingface.co/production/uploads/6435ddfe1479cf89bd4a8c6e/eWblS4u1jvKlgT8tpqU6i.png",
            ["black-forest-labs/FLUX.1-dev"] = "https://cdn-uploads.huggingface.co/production/uploads/6435ddfe1479cf89bd4a8c6e/eWblS4u1jvKlgT8tpqU6i.png",

            // Popular checkpoints
            ["Lykon/dreamshaper-8"] = "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/bb3ee20b-1f5a-4e4c-9fc2-8c8ec4c7afe6/width=450/00176-4251613958.jpeg",
            ["SG161222/Realistic_Vision_V5.1_noVAE"] = "https://image.civitai.com/xG1nkqKTMzGDvpLrqFT7WA/a4b82f53-48cd-4b0e-9e5c-7cf93a9aec60/width=450/123456.jpeg",

            // AnimateDiff
            ["guoyww/animatediff-motion-adapter-v1-5-2"] = "https://raw.githubusercontent.com/guoyww/AnimateDiff/main/assets/animations/model_02/01.gif",

            // ControlNet
            ["lllyasviel/control_v11p_sd15_canny"] = "https://huggingface.co/lllyasviel/control_v11p_sd15_canny/resolve/main/images/control.png",
            ["lllyasviel/control_v11f1p_sd15_depth"] = "https://huggingface.co/lllyasviel/control_v11f1p_sd15_depth/resolve/main/images/control.png"
        };

        return knownThumbnails.TryGetValue(modelId, out var url) ? url : null;
    }

    /// <summary>
    /// Get a placeholder thumbnail URL based on model type
    /// Uses DiceBear avatars or UI Avatars as fallback
    /// </summary>
    private static string? GetPlaceholderThumbnailUrl(string modelId)
    {
        var modelName = modelId.Split('/').LastOrDefault() ?? modelId;
        var cleanName = Uri.EscapeDataString(modelName);

        // Determine model type from name
        var lowerName = modelName.ToLowerInvariant();
        var category = lowerName switch
        {
            var n when n.Contains("lora") => "lora",
            var n when n.Contains("controlnet") || n.Contains("control") => "controlnet",
            var n when n.Contains("vae") => "vae",
            var n when n.Contains("video") || n.Contains("animate") || n.Contains("svd") => "video",
            var n when n.Contains("embed") || n.Contains("textual") => "embed",
            _ => "image"
        };

        // Use UI Avatars for a clean text-based placeholder
        var bgColor = category switch
        {
            "lora" => "F59E0B",      // Orange
            "controlnet" => "10B981", // Green
            "vae" => "E91E63",        // Pink
            "video" => "06B6D4",      // Cyan
            "embed" => "8B5CF6",      // Purple
            _ => "7C4DFF"             // Default purple
        };

        // Get first letter(s) for avatar
        var initials = string.Join("", modelName
            .Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(s => s.FirstOrDefault().ToString().ToUpperInvariant()));

        if (string.IsNullOrEmpty(initials))
        {
            initials = modelName.Length > 0 ? modelName[0].ToString().ToUpperInvariant() : "M";
        }

        return $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(initials)}&background={bgColor}&color=fff&size=200&bold=true&format=png";
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif";
    }

    private static int GetThumbnailPriority(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return name switch
        {
            "thumbnail" => 100,
            "preview" => 90,
            "cover" => 80,
            "sample" => 70,
            "example" => 60,
            _ when path.Contains("images/") => 50,
            _ => 0
        };
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
    public string? ThumbnailUrl { get; set; }
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
    public string? ThumbnailUrl { get; set; }

    /// <summary>
    /// Gets the thumbnail URL, falling back to HuggingFace default if not set
    /// </summary>
    public string GetThumbnailUrl() => ThumbnailUrl ?? $"https://huggingface.co/{Id}/resolve/main/thumbnail.png";
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
    public double SpeedBytesPerSecond { get; set; }
    public string? Error { get; set; }

    // For speed calculation
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
    public long LastDownloadedBytes { get; set; }

    public double Percentage => TotalBytes > 0 ? (double)DownloadedBytes / TotalBytes * 100 : 0;

    /// <summary>
    /// Format downloaded size (e.g., "1.5 GB / 3.2 GB")
    /// </summary>
    public string DownloadedDisplay => $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";

    /// <summary>
    /// Format speed (e.g., "15.2 MB/s")
    /// </summary>
    public string SpeedDisplay
    {
        get
        {
            if (SpeedBytesPerSecond <= 0) return "--";
            if (SpeedBytesPerSecond < 1024) return $"{SpeedBytesPerSecond:F0} B/s";
            if (SpeedBytesPerSecond < 1024 * 1024) return $"{SpeedBytesPerSecond / 1024.0:F1} KB/s";
            return $"{SpeedBytesPerSecond / 1024.0 / 1024.0:F1} MB/s";
        }
    }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public string EtaDisplay
    {
        get
        {
            if (SpeedBytesPerSecond <= 0) return "--";
            var remainingBytes = TotalBytes - DownloadedBytes;
            var seconds = remainingBytes / SpeedBytesPerSecond;
            if (seconds < 60) return $"{seconds:F0}s";
            if (seconds < 3600) return $"{seconds / 60:F0}m {seconds % 60:F0}s";
            return $"{seconds / 3600:F0}h {(seconds % 3600) / 60:F0}m";
        }
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / 1024.0 / 1024.0:F1} MB",
        _ => $"{bytes / 1024.0 / 1024.0 / 1024.0:F2} GB"
    };
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
