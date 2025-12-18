using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// AI Image Generation Service
/// Supports multiple FREE providers:
/// - Stable Diffusion WebUI (local)
/// - ComfyUI (local)
/// - Hugging Face Inference API (free tier)
/// - Ollama with vision models
/// Plus paid options: DALL-E, Leonardo.ai
/// </summary>
public class ImageGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly AIConfig _config;
    private readonly ILogger<ImageGeneratorService>? _logger;

    /// <summary>
    /// รายการ Free Image Providers ที่รองรับ
    /// </summary>
    public static readonly ImageProviderInfo[] FreeProviders = new[]
    {
        new ImageProviderInfo
        {
            Id = "sd_webui",
            Name = "Stable Diffusion WebUI",
            Description = "AUTOMATIC1111 WebUI - รันบน local ฟรี 100%",
            DefaultUrl = "http://localhost:7860",
            IsFree = true,
            RequiresInstall = true,
            InstallGuide = "https://github.com/AUTOMATIC1111/stable-diffusion-webui"
        },
        new ImageProviderInfo
        {
            Id = "comfyui",
            Name = "ComfyUI",
            Description = "Node-based UI สำหรับ SD - ยืดหยุ่นกว่า WebUI",
            DefaultUrl = "http://localhost:8188",
            IsFree = true,
            RequiresInstall = true,
            InstallGuide = "https://github.com/comfyanonymous/ComfyUI"
        },
        new ImageProviderInfo
        {
            Id = "hf_inference",
            Name = "Hugging Face Inference",
            Description = "Free API จาก Hugging Face - ไม่ต้องติดตั้งอะไร",
            DefaultUrl = "https://api-inference.huggingface.co",
            IsFree = true,
            RequiresInstall = false,
            RequiresApiKey = true,
            ApiKeyEnvVar = "HF_TOKEN"
        },
        new ImageProviderInfo
        {
            Id = "fal_ai",
            Name = "fal.ai",
            Description = "Free tier สำหรับ FLUX และ SD models",
            DefaultUrl = "https://fal.run",
            IsFree = true,
            RequiresInstall = false,
            RequiresApiKey = true,
            ApiKeyEnvVar = "FAL_KEY"
        }
    };

    public ImageGeneratorService(ILogger<ImageGeneratorService>? logger = null)
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Image generation can take time
        _config = AIConfig.Load();
        _logger = logger;
    }

    public async Task<GeneratedImage> GenerateAsync(
        string prompt,
        string style,
        string size,
        string provider,
        CancellationToken ct)
    {
        _logger?.LogInformation("Generating image with provider: {Provider}", provider);

        // Auto-select provider - ลองฟรีก่อน
        if (provider == "auto")
        {
            // 1. Try local Stable Diffusion WebUI
            var result = await GenerateWithStableDiffusionAsync(prompt, style, size, ct);
            if (result != null)
            {
                _logger?.LogInformation("Generated with Stable Diffusion WebUI");
                return result;
            }

            // 2. Try ComfyUI
            result = await GenerateWithComfyUIAsync(prompt, style, size, ct);
            if (result != null)
            {
                _logger?.LogInformation("Generated with ComfyUI");
                return result;
            }

            // 3. Try Hugging Face free API
            result = await GenerateWithHuggingFaceAsync(prompt, style, size, ct);
            if (result != null)
            {
                _logger?.LogInformation("Generated with Hugging Face Inference");
                return result;
            }

            // 4. Try fal.ai
            result = await GenerateWithFalAiAsync(prompt, style, size, ct);
            if (result != null)
            {
                _logger?.LogInformation("Generated with fal.ai");
                return result;
            }

            // 5. Fallback to DALL-E (paid)
            result = await GenerateWithDallEAsync(prompt, style, size, ct);
            if (result != null)
            {
                _logger?.LogInformation("Generated with DALL-E (paid)");
                return result;
            }

            throw new Exception("No image generator available. Please install Stable Diffusion WebUI or set HF_TOKEN for Hugging Face.");
        }

        return provider switch
        {
            "dalle" => await GenerateWithDallEAsync(prompt, style, size, ct)
                       ?? throw new Exception("DALL-E generation failed"),
            "sd" or "stable_diffusion" or "sd_webui" => await GenerateWithStableDiffusionAsync(prompt, style, size, ct)
                                          ?? throw new Exception("Stable Diffusion generation failed"),
            "comfyui" => await GenerateWithComfyUIAsync(prompt, style, size, ct)
                         ?? throw new Exception("ComfyUI generation failed"),
            "hf" or "huggingface" or "hf_inference" => await GenerateWithHuggingFaceAsync(prompt, style, size, ct)
                                                       ?? throw new Exception("Hugging Face generation failed"),
            "fal" or "fal_ai" => await GenerateWithFalAiAsync(prompt, style, size, ct)
                                 ?? throw new Exception("fal.ai generation failed"),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };
    }

    /// <summary>
    /// ตรวจสอบว่า provider ไหนพร้อมใช้งาน
    /// </summary>
    public async Task<List<AvailableProvider>> CheckAvailableProvidersAsync(CancellationToken ct = default)
    {
        var results = new List<AvailableProvider>();

        // Check SD WebUI
        try
        {
            var sdUrl = Environment.GetEnvironmentVariable("SD_API_URL") ?? "http://localhost:7860";
            var response = await _httpClient.GetAsync($"{sdUrl}/sdapi/v1/sd-models", ct);
            results.Add(new AvailableProvider
            {
                Id = "sd_webui",
                Name = "Stable Diffusion WebUI",
                IsAvailable = response.IsSuccessStatusCode,
                IsFree = true,
                Url = sdUrl
            });
        }
        catch
        {
            results.Add(new AvailableProvider { Id = "sd_webui", Name = "Stable Diffusion WebUI", IsAvailable = false, IsFree = true });
        }

        // Check ComfyUI
        try
        {
            var comfyUrl = Environment.GetEnvironmentVariable("COMFYUI_URL") ?? "http://localhost:8188";
            var response = await _httpClient.GetAsync($"{comfyUrl}/system_stats", ct);
            results.Add(new AvailableProvider
            {
                Id = "comfyui",
                Name = "ComfyUI",
                IsAvailable = response.IsSuccessStatusCode,
                IsFree = true,
                Url = comfyUrl
            });
        }
        catch
        {
            results.Add(new AvailableProvider { Id = "comfyui", Name = "ComfyUI", IsAvailable = false, IsFree = true });
        }

        // Check Hugging Face
        var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
        results.Add(new AvailableProvider
        {
            Id = "hf_inference",
            Name = "Hugging Face Inference",
            IsAvailable = !string.IsNullOrEmpty(hfToken),
            IsFree = true,
            Note = string.IsNullOrEmpty(hfToken) ? "Set HF_TOKEN environment variable" : "Ready"
        });

        // Check fal.ai
        var falKey = Environment.GetEnvironmentVariable("FAL_KEY");
        results.Add(new AvailableProvider
        {
            Id = "fal_ai",
            Name = "fal.ai",
            IsAvailable = !string.IsNullOrEmpty(falKey),
            IsFree = true,
            Note = string.IsNullOrEmpty(falKey) ? "Set FAL_KEY environment variable" : "Ready"
        });

        // Check DALL-E
        results.Add(new AvailableProvider
        {
            Id = "dalle",
            Name = "DALL-E 3",
            IsAvailable = !string.IsNullOrEmpty(_config.OpenAIApiKey),
            IsFree = false,
            Note = string.IsNullOrEmpty(_config.OpenAIApiKey) ? "Set OPENAI_API_KEY" : "Ready (Paid)"
        });

        return results;
    }

    private async Task<GeneratedImage?> GenerateWithDallEAsync(
        string prompt, string style, string size, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_config.OpenAIApiKey)) return null;

        var enhancedPrompt = $"{prompt}. Style: {style}. High quality, suitable for social media marketing.";

        var request = new
        {
            model = "dall-e-3",
            prompt = enhancedPrompt,
            n = 1,
            size = size,
            quality = "standard"
        };

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.OpenAIApiKey}");

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/images/generations",
            request, ct);

        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var imageUrl = result.GetProperty("data")[0]
            .GetProperty("url")
            .GetString();

        var (width, height) = ParseSize(size);

        return new GeneratedImage
        {
            Url = imageUrl ?? "",
            Provider = "dalle",
            Width = width,
            Height = height
        };
    }

    private async Task<GeneratedImage?> GenerateWithStableDiffusionAsync(
        string prompt, string style, string size, CancellationToken ct)
    {
        var sdUrl = Environment.GetEnvironmentVariable("SD_API_URL") ?? "http://localhost:7860";
        var (width, height) = ParseSize(size);

        var enhancedPrompt = $"masterpiece, best quality, {prompt}, {style} style";

        var request = new
        {
            prompt = enhancedPrompt,
            negative_prompt = "blurry, low quality, distorted, watermark",
            width,
            height,
            steps = 30,
            cfg_scale = 7,
            sampler_name = "DPM++ 2M Karras"
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{sdUrl}/sdapi/v1/txt2img",
                request, ct);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var images = result.GetProperty("images");

            if (images.GetArrayLength() == 0) return null;

            var base64Image = images[0].GetString();

            return new GeneratedImage
            {
                Base64Data = base64Image ?? "",
                Provider = "stable_diffusion",
                Width = width,
                Height = height
            };
        }
        catch
        {
            return null; // SD not available
        }
    }

    /// <summary>
    /// สร้างรูปด้วย ComfyUI (FREE - Local)
    /// </summary>
    private async Task<GeneratedImage?> GenerateWithComfyUIAsync(
        string prompt, string style, string size, CancellationToken ct)
    {
        var comfyUrl = Environment.GetEnvironmentVariable("COMFYUI_URL") ?? "http://localhost:8188";
        var (width, height) = ParseSize(size);

        try
        {
            // ComfyUI ใช้ workflow JSON
            var workflow = CreateComfyWorkflow(prompt, style, width, height);

            var response = await _httpClient.PostAsJsonAsync(
                $"{comfyUrl}/prompt",
                new { prompt = workflow },
                ct);

            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var promptId = result.GetProperty("prompt_id").GetString();

            // Poll for completion
            for (var i = 0; i < 120; i++) // Max 2 minutes
            {
                await Task.Delay(1000, ct);

                var historyResponse = await _httpClient.GetAsync($"{comfyUrl}/history/{promptId}", ct);
                if (!historyResponse.IsSuccessStatusCode) continue;

                var history = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (history.TryGetProperty(promptId!, out var promptHistory))
                {
                    if (promptHistory.TryGetProperty("outputs", out var outputs))
                    {
                        // Get first image from outputs
                        foreach (var output in outputs.EnumerateObject())
                        {
                            if (output.Value.TryGetProperty("images", out var images) &&
                                images.GetArrayLength() > 0)
                            {
                                var image = images[0];
                                var filename = image.GetProperty("filename").GetString();
                                var subfolder = image.TryGetProperty("subfolder", out var sf) ? sf.GetString() : "";

                                // Download image
                                var imageUrl = $"{comfyUrl}/view?filename={filename}&subfolder={subfolder}&type=output";
                                var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, ct);
                                var base64 = Convert.ToBase64String(imageBytes);

                                return new GeneratedImage
                                {
                                    Base64Data = base64,
                                    Provider = "comfyui",
                                    Width = width,
                                    Height = height
                                };
                            }
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "ComfyUI not available");
            return null;
        }
    }

    /// <summary>
    /// สร้าง ComfyUI workflow สำหรับ txt2img
    /// </summary>
    private object CreateComfyWorkflow(string prompt, string style, int width, int height)
    {
        var fullPrompt = $"masterpiece, best quality, {prompt}, {style} style";

        return new Dictionary<string, object>
        {
            ["3"] = new
            {
                class_type = "KSampler",
                inputs = new
                {
                    seed = Random.Shared.Next(),
                    steps = 30,
                    cfg = 7,
                    sampler_name = "dpmpp_2m",
                    scheduler = "karras",
                    denoise = 1,
                    model = new object[] { "4", 0 },
                    positive = new object[] { "6", 0 },
                    negative = new object[] { "7", 0 },
                    latent_image = new object[] { "5", 0 }
                }
            },
            ["4"] = new
            {
                class_type = "CheckpointLoaderSimple",
                inputs = new { ckpt_name = "sd_xl_base_1.0.safetensors" }
            },
            ["5"] = new
            {
                class_type = "EmptyLatentImage",
                inputs = new { width, height, batch_size = 1 }
            },
            ["6"] = new
            {
                class_type = "CLIPTextEncode",
                inputs = new
                {
                    text = fullPrompt,
                    clip = new object[] { "4", 1 }
                }
            },
            ["7"] = new
            {
                class_type = "CLIPTextEncode",
                inputs = new
                {
                    text = "blurry, low quality, distorted, watermark, text",
                    clip = new object[] { "4", 1 }
                }
            },
            ["8"] = new
            {
                class_type = "VAEDecode",
                inputs = new
                {
                    samples = new object[] { "3", 0 },
                    vae = new object[] { "4", 2 }
                }
            },
            ["9"] = new
            {
                class_type = "SaveImage",
                inputs = new
                {
                    filename_prefix = "PostXAgent",
                    images = new object[] { "8", 0 }
                }
            }
        };
    }

    /// <summary>
    /// สร้างรูปด้วย Hugging Face Inference API (FREE)
    /// </summary>
    private async Task<GeneratedImage?> GenerateWithHuggingFaceAsync(
        string prompt, string style, string size, CancellationToken ct)
    {
        var hfToken = Environment.GetEnvironmentVariable("HF_TOKEN");
        if (string.IsNullOrEmpty(hfToken)) return null;

        var (width, height) = ParseSize(size);
        var fullPrompt = $"masterpiece, best quality, {prompt}, {style} style";

        // Use Stable Diffusion XL model on HF
        var modelId = "stabilityai/stable-diffusion-xl-base-1.0";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://api-inference.huggingface.co/models/{modelId}");

            request.Headers.Add("Authorization", $"Bearer {hfToken}");
            request.Content = JsonContent.Create(new
            {
                inputs = fullPrompt,
                parameters = new
                {
                    width = Math.Min(width, 1024),
                    height = Math.Min(height, 1024),
                    num_inference_steps = 30,
                    guidance_scale = 7.5
                }
            });

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogWarning("HF Inference failed: {Error}", error);

                // Model might be loading, wait and retry once
                if (error.Contains("loading"))
                {
                    await Task.Delay(20000, ct); // Wait 20 seconds
                    response = await _httpClient.SendAsync(request, ct);
                    if (!response.IsSuccessStatusCode) return null;
                }
                else
                {
                    return null;
                }
            }

            var imageBytes = await response.Content.ReadAsByteArrayAsync(ct);
            var base64 = Convert.ToBase64String(imageBytes);

            return new GeneratedImage
            {
                Base64Data = base64,
                Provider = "huggingface",
                Width = width,
                Height = height
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Hugging Face Inference failed");
            return null;
        }
    }

    /// <summary>
    /// สร้างรูปด้วย fal.ai (FREE tier)
    /// </summary>
    private async Task<GeneratedImage?> GenerateWithFalAiAsync(
        string prompt, string style, string size, CancellationToken ct)
    {
        var falKey = Environment.GetEnvironmentVariable("FAL_KEY");
        if (string.IsNullOrEmpty(falKey)) return null;

        var (width, height) = ParseSize(size);
        var fullPrompt = $"masterpiece, best quality, {prompt}, {style} style";

        try
        {
            // Use FLUX.1-schnell (fastest free model)
            using var request = new HttpRequestMessage(HttpMethod.Post,
                "https://fal.run/fal-ai/flux/schnell");

            request.Headers.Add("Authorization", $"Key {falKey}");
            request.Content = JsonContent.Create(new
            {
                prompt = fullPrompt,
                image_size = new { width, height },
                num_inference_steps = 4, // FLUX schnell is very fast
                num_images = 1,
                enable_safety_checker = true
            });

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogWarning("fal.ai failed: {Error}", error);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var images = result.GetProperty("images");

            if (images.GetArrayLength() == 0) return null;

            var imageUrl = images[0].GetProperty("url").GetString();

            // Download the image
            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl!, ct);
            var base64 = Convert.ToBase64String(imageBytes);

            return new GeneratedImage
            {
                Url = imageUrl ?? "",
                Base64Data = base64,
                Provider = "fal_ai",
                Width = width,
                Height = height
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "fal.ai generation failed");
            return null;
        }
    }

    private (int width, int height) ParseSize(string size)
    {
        var parts = size.Split('x');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var w) &&
            int.TryParse(parts[1], out var h))
        {
            return (w, h);
        }
        return (1024, 1024);
    }
}

/// <summary>
/// ข้อมูล Image Provider
/// </summary>
public class ImageProviderInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string DefaultUrl { get; set; } = "";
    public bool IsFree { get; set; }
    public bool RequiresInstall { get; set; }
    public bool RequiresApiKey { get; set; }
    public string ApiKeyEnvVar { get; set; } = "";
    public string InstallGuide { get; set; } = "";
}

/// <summary>
/// สถานะ Provider ที่พร้อมใช้งาน
/// </summary>
public class AvailableProvider
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsAvailable { get; set; }
    public bool IsFree { get; set; }
    public string? Url { get; set; }
    public string? Note { get; set; }
}

public class GeneratedImage
{
    public string Url { get; set; } = "";
    public string Base64Data { get; set; } = "";
    public string Provider { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}
