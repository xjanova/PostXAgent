using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Hugging Face Model Manager - โหลดและจัดการ model จาก Hugging Face
/// รองรับ: LLM (Ollama), Image Generation (SD), Video Generation
/// </summary>
public class HuggingFaceModelManager
{
    private readonly ILogger<HuggingFaceModelManager>? _logger;
    private readonly HttpClient _httpClient;
    private readonly string _modelsDirectory;
    private readonly SystemResourceDetector _resourceDetector;
    private string? _huggingFaceToken;

    /// <summary>
    /// HuggingFace API Token สำหรับเข้าถึง private/gated models
    /// </summary>
    public string? HuggingFaceToken
    {
        get => _huggingFaceToken;
        set
        {
            _huggingFaceToken = value;
            UpdateAuthorizationHeader();
        }
    }

    /// <summary>
    /// ตรวจสอบว่ามี Token หรือไม่
    /// </summary>
    public bool HasToken => !string.IsNullOrEmpty(_huggingFaceToken);

    /// <summary>
    /// Event สำหรับ download progress
    /// </summary>
    public event EventHandler<ModelDownloadProgress>? OnProgress;

    /// <summary>
    /// Event เมื่อดาวน์โหลดเสร็จ
    /// </summary>
    public event EventHandler<ModelDownloadComplete>? OnComplete;

    /// <summary>
    /// Event เมื่อ Token status เปลี่ยน
    /// </summary>
    public event EventHandler<HuggingFaceTokenStatus>? TokenStatusChanged;

    /// <summary>
    /// LLM Models (สำหรับ Ollama)
    /// </summary>
    public static readonly HuggingFaceModel[] LLMModels = new[]
    {
        new HuggingFaceModel
        {
            Id = "llama3.2-3b",
            HuggingFaceRepo = "lmstudio-community/Llama-3.2-3B-Instruct-GGUF",
            FileName = "Llama-3.2-3B-Instruct-Q4_K_M.gguf",
            DisplayName = "Llama 3.2 3B (Recommended)",
            Category = ModelCategory.LLM,
            SizeGB = 2.0,
            RequiredRamGB = 4,
            RequiredVramGB = 3,
            OllamaModelName = "llama3.2:3b",
            Priority = 1
        },
        new HuggingFaceModel
        {
            Id = "llama3.1-8b",
            HuggingFaceRepo = "lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF",
            FileName = "Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf",
            DisplayName = "Llama 3.1 8B (Better quality)",
            Category = ModelCategory.LLM,
            SizeGB = 4.9,
            RequiredRamGB = 8,
            RequiredVramGB = 6,
            OllamaModelName = "llama3.1:8b",
            Priority = 2
        },
        new HuggingFaceModel
        {
            Id = "qwen2.5-7b",
            HuggingFaceRepo = "Qwen/Qwen2.5-7B-Instruct-GGUF",
            FileName = "qwen2.5-7b-instruct-q4_k_m.gguf",
            DisplayName = "Qwen 2.5 7B (Best for Thai)",
            Category = ModelCategory.LLM,
            SizeGB = 4.4,
            RequiredRamGB = 8,
            RequiredVramGB = 6,
            OllamaModelName = "qwen2.5:7b",
            Priority = 3
        },
        new HuggingFaceModel
        {
            Id = "llava-7b",
            HuggingFaceRepo = "mys/ggml_llava-v1.5-7b",
            FileName = "ggml-model-q4_k.gguf",
            DisplayName = "LLaVA 7B (Vision)",
            Category = ModelCategory.LLM,
            SizeGB = 4.5,
            RequiredRamGB = 8,
            RequiredVramGB = 6,
            OllamaModelName = "llava:7b",
            Priority = 4,
            Tags = new[] { "vision", "multimodal" }
        },
        new HuggingFaceModel
        {
            Id = "phi-3-mini",
            HuggingFaceRepo = "microsoft/Phi-3-mini-4k-instruct-gguf",
            FileName = "Phi-3-mini-4k-instruct-q4.gguf",
            DisplayName = "Phi-3 Mini (Very fast)",
            Category = ModelCategory.LLM,
            SizeGB = 2.2,
            RequiredRamGB = 4,
            RequiredVramGB = 3,
            OllamaModelName = "phi3:mini",
            Priority = 5
        }
    };

    /// <summary>
    /// Image Generation Models (สำหรับ ComfyUI หรือ Custom Engine)
    /// </summary>
    public static readonly HuggingFaceModel[] ImageModels = new[]
    {
        new HuggingFaceModel
        {
            Id = "sd-1.5",
            HuggingFaceRepo = "runwayml/stable-diffusion-v1-5",
            FileName = "v1-5-pruned-emaonly.safetensors",
            DisplayName = "Stable Diffusion 1.5",
            Category = ModelCategory.ImageGeneration,
            SizeGB = 4.3,
            RequiredRamGB = 8,
            RequiredVramGB = 4,
            Priority = 1,
            Description = "รุ่นคลาสสิก มี LoRA เยอะ"
        },
        new HuggingFaceModel
        {
            Id = "sdxl-base",
            HuggingFaceRepo = "stabilityai/stable-diffusion-xl-base-1.0",
            FileName = "sd_xl_base_1.0.safetensors",
            DisplayName = "SDXL 1.0 Base",
            Category = ModelCategory.ImageGeneration,
            SizeGB = 6.9,
            RequiredRamGB = 16,
            RequiredVramGB = 8,
            Priority = 2,
            Description = "คุณภาพสูง 1024x1024"
        },
        new HuggingFaceModel
        {
            Id = "sdxl-refiner",
            HuggingFaceRepo = "stabilityai/stable-diffusion-xl-refiner-1.0",
            FileName = "sd_xl_refiner_1.0.safetensors",
            DisplayName = "SDXL 1.0 Refiner",
            Category = ModelCategory.ImageGeneration,
            SizeGB = 6.1,
            RequiredRamGB = 16,
            RequiredVramGB = 8,
            Priority = 3,
            Description = "ใช้ร่วมกับ SDXL Base เพื่อเพิ่มรายละเอียด"
        },
        new HuggingFaceModel
        {
            Id = "dreamshaper-8",
            HuggingFaceRepo = "Lykon/dreamshaper-8",
            FileName = "dreamshaper_8.safetensors",
            DisplayName = "DreamShaper 8",
            Category = ModelCategory.ImageGeneration,
            SizeGB = 2.1,
            RequiredRamGB = 8,
            RequiredVramGB = 4,
            Priority = 4,
            Description = "สไตล์ fantasy และ artistic"
        },
        new HuggingFaceModel
        {
            Id = "realistic-vision",
            HuggingFaceRepo = "SG161222/Realistic_Vision_V6.0_B1_noVAE",
            FileName = "Realistic_Vision_V6.0_B1.safetensors",
            DisplayName = "Realistic Vision V6",
            Category = ModelCategory.ImageGeneration,
            SizeGB = 2.1,
            RequiredRamGB = 8,
            RequiredVramGB = 4,
            Priority = 5,
            Description = "สำหรับภาพถ่ายสมจริง"
        }
    };

    /// <summary>
    /// Video Generation Models
    /// </summary>
    public static readonly HuggingFaceModel[] VideoModels = new[]
    {
        new HuggingFaceModel
        {
            Id = "animatediff-v3",
            HuggingFaceRepo = "guoyww/animatediff",
            FileName = "mm_sd_v15_v3.safetensors",
            DisplayName = "AnimateDiff v3",
            Category = ModelCategory.VideoGeneration,
            SizeGB = 1.8,
            RequiredRamGB = 16,
            RequiredVramGB = 8,
            Priority = 1,
            Description = "แปลงรูปเป็น animation"
        },
        new HuggingFaceModel
        {
            Id = "svd-xt",
            HuggingFaceRepo = "stabilityai/stable-video-diffusion-img2vid-xt",
            FileName = "svd_xt.safetensors",
            DisplayName = "Stable Video Diffusion XT",
            Category = ModelCategory.VideoGeneration,
            SizeGB = 9.5,
            RequiredRamGB = 32,
            RequiredVramGB = 12,
            Priority = 2,
            Description = "สร้างวิดีโอจากรูปภาพ"
        }
    };

    /// <summary>
    /// VAE Models
    /// </summary>
    public static readonly HuggingFaceModel[] VAEModels = new[]
    {
        new HuggingFaceModel
        {
            Id = "sdxl-vae",
            HuggingFaceRepo = "stabilityai/sdxl-vae",
            FileName = "sdxl_vae.safetensors",
            DisplayName = "SDXL VAE",
            Category = ModelCategory.VAE,
            SizeGB = 0.335,
            RequiredRamGB = 4,
            RequiredVramGB = 2,
            Priority = 1
        },
        new HuggingFaceModel
        {
            Id = "sd-vae-ft-mse",
            HuggingFaceRepo = "stabilityai/sd-vae-ft-mse-original",
            FileName = "vae-ft-mse-840000-ema-pruned.safetensors",
            DisplayName = "SD VAE (Better faces)",
            Category = ModelCategory.VAE,
            SizeGB = 0.335,
            RequiredRamGB = 4,
            RequiredVramGB = 2,
            Priority = 2
        }
    };

    /// <summary>
    /// รวม models ทั้งหมด
    /// </summary>
    public static HuggingFaceModel[] AllModels => LLMModels
        .Concat(ImageModels)
        .Concat(VideoModels)
        .Concat(VAEModels)
        .ToArray();

    public event EventHandler<DownloadProgressEventArgs>? DownloadProgress;

    public HuggingFaceModelManager(ILogger<HuggingFaceModelManager>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromHours(2) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostXAgent/1.0");
        _resourceDetector = new SystemResourceDetector();

        // Default models directory
        _modelsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent", "models");

        Directory.CreateDirectory(_modelsDirectory);
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "checkpoints"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "loras"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "vae"));
        Directory.CreateDirectory(Path.Combine(_modelsDirectory, "video"));

        // Load token from environment if available
        var envToken = Environment.GetEnvironmentVariable("HF_TOKEN")
            ?? Environment.GetEnvironmentVariable("HUGGING_FACE_HUB_TOKEN");
        if (!string.IsNullOrEmpty(envToken))
        {
            HuggingFaceToken = envToken;
        }
    }

    /// <summary>
    /// อัพเดท Authorization header
    /// </summary>
    private void UpdateAuthorizationHeader()
    {
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        if (!string.IsNullOrEmpty(_huggingFaceToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_huggingFaceToken}");
            _logger?.LogInformation("HuggingFace token configured");
        }
    }

    /// <summary>
    /// ตรวจสอบความถูกต้องของ Token
    /// </summary>
    public async Task<HuggingFaceTokenStatus> ValidateTokenAsync()
    {
        var status = new HuggingFaceTokenStatus();

        if (string.IsNullOrEmpty(_huggingFaceToken))
        {
            status.IsValid = false;
            status.Message = "No token configured";
            TokenStatusChanged?.Invoke(this, status);
            return status;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://huggingface.co/api/whoami-v2");
            request.Headers.Add("Authorization", $"Bearer {_huggingFaceToken}");

            using var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                status.IsValid = true;
                status.Username = json.GetProperty("name").GetString();

                if (json.TryGetProperty("email", out var email))
                    status.Email = email.GetString();

                if (json.TryGetProperty("isPro", out var isPro))
                    status.IsPro = isPro.GetBoolean();

                if (json.TryGetProperty("canPay", out var canPay))
                    status.CanPay = canPay.GetBoolean();

                // Check access level
                if (json.TryGetProperty("auth", out var auth))
                {
                    status.AccessType = auth.TryGetProperty("accessToken", out var at)
                        ? at.TryGetProperty("role", out var role)
                            ? role.GetString()
                            : "read"
                        : "read";
                }

                status.Message = $"Connected as {status.Username}" + (status.IsPro ? " (PRO)" : "");
                _logger?.LogInformation("HuggingFace token validated: {Username}", status.Username);
            }
            else
            {
                status.IsValid = false;
                status.Message = $"Invalid token: {response.StatusCode}";
                _logger?.LogWarning("HuggingFace token validation failed: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            status.IsValid = false;
            status.Message = $"Validation error: {ex.Message}";
            _logger?.LogError(ex, "Failed to validate HuggingFace token");
        }

        TokenStatusChanged?.Invoke(this, status);
        return status;
    }

    /// <summary>
    /// ตรวจสอบว่า model ต้องการ authentication หรือไม่
    /// </summary>
    public async Task<bool> RequiresAuthenticationAsync(string repoId)
    {
        try
        {
            // Try without auth first
            using var request = new HttpRequestMessage(HttpMethod.Head, $"https://huggingface.co/{repoId}/resolve/main/README.md");
            using var response = await _httpClient.SendAsync(request);

            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                   response.StatusCode == System.Net.HttpStatusCode.Forbidden;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ค้นหา models จาก HuggingFace Hub
    /// </summary>
    public async Task<List<HuggingFaceSearchResult>> SearchModelsAsync(
        string query,
        string? filter = null,
        int limit = 20)
    {
        var results = new List<HuggingFaceSearchResult>();

        try
        {
            var url = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&limit={limit}";
            if (!string.IsNullOrEmpty(filter))
            {
                url += $"&filter={Uri.EscapeDataString(filter)}";
            }

            using var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var models = await response.Content.ReadFromJsonAsync<JsonElement[]>();
                foreach (var model in models ?? Array.Empty<JsonElement>())
                {
                    results.Add(new HuggingFaceSearchResult
                    {
                        Id = model.GetProperty("id").GetString() ?? "",
                        ModelId = model.TryGetProperty("modelId", out var mid) ? mid.GetString() ?? "" : "",
                        Author = model.TryGetProperty("author", out var author) ? author.GetString() : null,
                        Downloads = model.TryGetProperty("downloads", out var dl) ? dl.GetInt32() : 0,
                        Likes = model.TryGetProperty("likes", out var likes) ? likes.GetInt32() : 0,
                        IsPrivate = model.TryGetProperty("private", out var priv) && priv.GetBoolean(),
                        IsGated = model.TryGetProperty("gated", out var gated) && (gated.ValueKind == JsonValueKind.True || gated.ValueKind == JsonValueKind.String),
                        Tags = model.TryGetProperty("tags", out var tags)
                            ? tags.EnumerateArray().Select(t => t.GetString() ?? "").ToArray()
                            : Array.Empty<string>()
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search HuggingFace models");
        }

        return results;
    }

    /// <summary>
    /// รับ Models ที่แนะนำตามทรัพยากรระบบ พร้อมสถานะความเข้ากันได้
    /// </summary>
    public async Task<List<ModelCompatibilityInfo>> GetCompatibleModelsAsync()
    {
        return await Task.Run(() =>
        {
            var resources = _resourceDetector.DetectResources();
            var result = new List<ModelCompatibilityInfo>();

            var totalRamGB = resources.TotalRamMB / 1024.0;
            var vramGB = resources.TotalVramMB / 1024.0;

            foreach (var model in AllModels)
            {
                var isCompatible = model.RequiredRamGB <= totalRamGB &&
                                  (model.RequiredVramGB == 0 || model.RequiredVramGB <= vramGB);

                var isInstalled = IsModelDownloaded(model);

                result.Add(new ModelCompatibilityInfo
                {
                    Model = model,
                    IsCompatible = isCompatible,
                    IsInstalled = isInstalled,
                    AvailableRamGB = totalRamGB,
                    AvailableVramGB = vramGB,
                    CompatibilityReason = !isCompatible
                        ? $"ต้องการ RAM {model.RequiredRamGB}GB, VRAM {model.RequiredVramGB}GB"
                        : null
                });
            }

            return result;
        });
    }

    /// <summary>
    /// รับเฉพาะ Models ตามหมวดหมู่
    /// </summary>
    public async Task<List<ModelCompatibilityInfo>> GetModelsByCategoryAsync(ModelCategory category)
    {
        var all = await GetCompatibleModelsAsync();
        return all.Where(m => m.Model.Category == category).ToList();
    }

    /// <summary>
    /// เลือก Model ที่ดีที่สุดตามทรัพยากร
    /// </summary>
    public HuggingFaceModel GetBestModelForSystem()
    {
        var detector = new SystemResourceDetector();
        var resources = detector.DetectResources();

        var availableRam = resources.TotalRamMB / 1000.0; // GB
        var availableVram = resources.TotalVramMB / 1000.0; // GB

        _logger?.LogInformation("Selecting model for RAM: {Ram}GB, VRAM: {Vram}GB",
            availableRam, availableVram);

        foreach (var model in LLMModels.OrderBy(m => m.Priority))
        {
            if (availableRam >= model.RequiredRamGB)
            {
                _logger?.LogInformation("Selected model: {Model}", model.DisplayName);
                return model;
            }
        }

        // Fallback to smallest
        return LLMModels.Last();
    }

    /// <summary>
    /// ตรวจสอบว่า model มีอยู่แล้วหรือไม่
    /// </summary>
    public bool IsModelDownloaded(HuggingFaceModel model)
    {
        var modelPath = GetModelPath(model);
        return File.Exists(modelPath);
    }

    /// <summary>
    /// รับ path ของ model file
    /// </summary>
    public string GetModelPath(HuggingFaceModel model)
    {
        return Path.Combine(_modelsDirectory, model.FileName);
    }

    /// <summary>
    /// โหลด model จาก Hugging Face
    /// </summary>
    public async Task<DownloadResult> DownloadModelAsync(
        HuggingFaceModel model,
        CancellationToken ct = default,
        IProgress<DownloadProgressEventArgs>? progress = null)
    {
        var modelPath = GetModelPath(model);

        if (File.Exists(modelPath))
        {
            _logger?.LogInformation("Model already exists: {Path}", modelPath);
            return new DownloadResult
            {
                Success = true,
                ModelPath = modelPath,
                Message = "Model already downloaded"
            };
        }

        var downloadUrl = $"https://huggingface.co/{model.HuggingFaceRepo}/resolve/main/{model.FileName}";
        _logger?.LogInformation("Downloading model from: {Url}", downloadUrl);

        try
        {
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            var totalMB = totalBytes / (1024.0 * 1024.0);

            _logger?.LogInformation("Download size: {Size:F1} MB", totalMB);

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(modelPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;
            var lastReportTime = DateTime.UtcNow;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                downloadedBytes += bytesRead;

                // Report progress every 500ms
                if ((DateTime.UtcNow - lastReportTime).TotalMilliseconds > 500)
                {
                    var progressPercent = totalBytes > 0 ? (double)downloadedBytes / totalBytes * 100 : 0;
                    var downloadedMB = downloadedBytes / (1024.0 * 1024.0);

                    var args = new DownloadProgressEventArgs
                    {
                        ModelName = model.DisplayName,
                        DownloadedBytes = downloadedBytes,
                        TotalBytes = totalBytes,
                        ProgressPercent = progressPercent,
                        DownloadedMB = downloadedMB,
                        TotalMB = totalMB
                    };

                    progress?.Report(args);
                    DownloadProgress?.Invoke(this, args);
                    lastReportTime = DateTime.UtcNow;
                }
            }

            // Rename temp file to final
            File.Move(modelPath + ".tmp", modelPath, true);

            _logger?.LogInformation("Model downloaded successfully: {Path}", modelPath);

            return new DownloadResult
            {
                Success = true,
                ModelPath = modelPath,
                Message = "Download completed"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to download model");

            // Clean up temp file
            var tempPath = modelPath + ".tmp";
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }

            return new DownloadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Import model เข้า Ollama
    /// </summary>
    public async Task<bool> ImportToOllamaAsync(HuggingFaceModel model, CancellationToken ct = default)
    {
        var modelPath = GetModelPath(model);
        if (!File.Exists(modelPath))
        {
            _logger?.LogError("Model file not found: {Path}", modelPath);
            return false;
        }

        try
        {
            // Create Modelfile for Ollama
            var modelfilePath = Path.Combine(_modelsDirectory, $"{model.Id}.modelfile");
            var modelfileContent = $@"FROM {modelPath}

PARAMETER temperature 0.7
PARAMETER top_p 0.9
PARAMETER num_ctx 4096

SYSTEM ""You are a helpful AI assistant for social media content creation. You can write in Thai and English.""
";
            await File.WriteAllTextAsync(modelfilePath, modelfileContent, ct);

            // Run ollama create
            var psi = new ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = $"create {model.OllamaModelName} -f \"{modelfilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                _logger?.LogError("Failed to start ollama process");
                return false;
            }

            await process.WaitForExitAsync(ct);
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            var error = await process.StandardError.ReadToEndAsync(ct);

            if (process.ExitCode == 0)
            {
                _logger?.LogInformation("Model imported to Ollama: {Model}", model.OllamaModelName);
                return true;
            }
            else
            {
                _logger?.LogError("Ollama import failed: {Error}", error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import model to Ollama");
            return false;
        }
    }

    /// <summary>
    /// โหลดและ import model ในขั้นตอนเดียว
    /// </summary>
    public async Task<DownloadResult> DownloadAndImportAsync(
        HuggingFaceModel? model = null,
        CancellationToken ct = default,
        IProgress<DownloadProgressEventArgs>? progress = null)
    {
        model ??= GetBestModelForSystem();

        _logger?.LogInformation("Starting download and import for: {Model}", model.DisplayName);

        // Download
        var downloadResult = await DownloadModelAsync(model, ct, progress);
        if (!downloadResult.Success)
        {
            return downloadResult;
        }

        // Try to import to Ollama (optional, model can be used directly too)
        var imported = await ImportToOllamaAsync(model, ct);
        downloadResult.ImportedToOllama = imported;

        return downloadResult;
    }

    /// <summary>
    /// รับรายการ models ที่โหลดแล้ว
    /// </summary>
    public List<HuggingFaceModel> GetDownloadedModels()
    {
        return AllModels
            .Where(IsModelDownloaded)
            .ToList();
    }

    /// <summary>
    /// ลบ model
    /// </summary>
    public bool DeleteModel(HuggingFaceModel model)
    {
        var modelPath = GetModelPath(model);
        if (File.Exists(modelPath))
        {
            try
            {
                File.Delete(modelPath);
                _logger?.LogInformation("Deleted model: {Path}", modelPath);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to delete model");
                return false;
            }
        }
        return true;
    }
}

public enum ModelCategory
{
    LLM,
    ImageGeneration,
    VideoGeneration,
    VAE,
    LoRA,
    ControlNet
}

public class HuggingFaceModel
{
    public string Id { get; set; } = "";
    public string HuggingFaceRepo { get; set; } = "";
    public string FileName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ModelCategory Category { get; set; } = ModelCategory.LLM;
    public double SizeGB { get; set; }
    public int RequiredRamGB { get; set; }
    public int RequiredVramGB { get; set; }
    public string OllamaModelName { get; set; } = "";
    public int Priority { get; set; }
    public string Description { get; set; } = "";
    public string[] Tags { get; set; } = Array.Empty<string>();
}

public class ModelCompatibilityInfo
{
    public HuggingFaceModel Model { get; set; } = null!;
    public bool IsCompatible { get; set; }
    public bool IsInstalled { get; set; }
    public double AvailableRamGB { get; set; }
    public double AvailableVramGB { get; set; }
    public string? CompatibilityReason { get; set; }
}

public class ModelDownloadProgress : EventArgs
{
    public HuggingFaceModel Model { get; set; } = null!;
    public double Progress { get; set; }
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public string Status { get; set; } = "";
    public TimeSpan EstimatedTimeRemaining { get; set; }

    public string SpeedDisplay => SpeedBytesPerSecond switch
    {
        > 1_000_000 => $"{SpeedBytesPerSecond / 1_000_000:F1} MB/s",
        > 1000 => $"{SpeedBytesPerSecond / 1000:F1} KB/s",
        _ => $"{SpeedBytesPerSecond:F0} B/s"
    };
}

public class ModelDownloadComplete : EventArgs
{
    public HuggingFaceModel Model { get; set; } = null!;
    public bool Success { get; set; }
    public string? FilePath { get; set; }
    public string? Error { get; set; }
}

public class DownloadProgressEventArgs : EventArgs
{
    public string ModelName { get; set; } = "";
    public long DownloadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public double ProgressPercent { get; set; }
    public double DownloadedMB { get; set; }
    public double TotalMB { get; set; }
}

public class DownloadResult
{
    public bool Success { get; set; }
    public string? ModelPath { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public bool ImportedToOllama { get; set; }
}

public class HuggingFaceTokenStatus
{
    public bool IsValid { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? Message { get; set; }
    public bool IsPro { get; set; }
    public bool CanPay { get; set; }
    public string? AccessType { get; set; } // "read", "write", "admin"
}

public class HuggingFaceSearchResult
{
    public string Id { get; set; } = "";
    public string ModelId { get; set; } = "";
    public string? Author { get; set; }
    public int Downloads { get; set; }
    public int Likes { get; set; }
    public bool IsPrivate { get; set; }
    public bool IsGated { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();

    public string DisplayName => string.IsNullOrEmpty(Author) ? Id : $"{Author}/{ModelId}";
    public bool RequiresAuth => IsPrivate || IsGated;
}
