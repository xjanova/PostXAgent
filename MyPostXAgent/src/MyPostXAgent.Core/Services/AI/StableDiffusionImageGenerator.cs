using System.Net.Http.Json;
using System.Text.Json;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Stable Diffusion WebUI Image Generator (Local, FREE)
/// รองรับ Automatic1111 และ Forge
/// </summary>
public class StableDiffusionImageGenerator : IImageGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _outputDir;

    public ImageAIProvider Provider => ImageAIProvider.StableDiffusion;

    public StableDiffusionImageGenerator(string baseUrl = "http://127.0.0.1:7860", string? outputDir = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _outputDir = outputDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyPostXAgent", "generated_images");

        Directory.CreateDirectory(_outputDir);

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ImageGenerationResult { Provider = Provider };

        try
        {
            // Calculate dimensions from aspect ratio
            var (width, height) = GetDimensions(request.AspectRatio, request.Width, request.Height);

            // Build API request
            var apiRequest = new
            {
                prompt = request.Prompt,
                negative_prompt = string.IsNullOrEmpty(request.NegativePrompt) 
                    ? "ugly, blurry, low quality, distorted, deformed" 
                    : request.NegativePrompt,
                width = width,
                height = height,
                steps = request.Steps > 0 ? request.Steps : 30,
                cfg_scale = request.CfgScale > 0 ? request.CfgScale : 7.0,
                seed = request.Seed,
                batch_size = request.NumImages > 0 ? request.NumImages : 1,
                sampler_name = "Euler a",
                scheduler = "normal"
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_baseUrl}/sdapi/v1/txt2img",
                apiRequest,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"API returned {response.StatusCode}";
                return result;
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<Txt2ImgResponse>(cancellationToken: cancellationToken);
            
            if (jsonResponse?.images == null || jsonResponse.images.Count == 0)
            {
                result.ErrorMessage = "No images returned from API";
                return result;
            }

            // Save images
            for (int i = 0; i < jsonResponse.images.Count; i++)
            {
                var base64 = jsonResponse.images[i];
                var imageBytes = Convert.FromBase64String(base64);
                var fileName = $"sd_{DateTime.Now:yyyyMMdd_HHmmss}_{i}.png";
                var filePath = Path.Combine(_outputDir, fileName);

                await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

                result.Images.Add(new GeneratedImage
                {
                    FilePath = filePath,
                    Base64 = base64,
                    ImageData = imageBytes,
                    Width = width,
                    Height = height,
                    Seed = jsonResponse.parameters?.seed ?? -1
                });
            }

            result.Success = true;
            result.Duration = DateTime.UtcNow - startTime;
        }
        catch (HttpRequestException ex)
        {
            result.ErrorMessage = $"Connection failed: {ex.Message}. Make sure Stable Diffusion WebUI is running.";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/sdapi/v1/options", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<SdModel>>(
                $"{_baseUrl}/sdapi/v1/sd-models",
                cancellationToken);

            return response?.Select(m => m.model_name ?? m.title ?? "Unknown").ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private (int width, int height) GetDimensions(AspectRatio ratio, int baseWidth, int baseHeight)
    {
        return ratio switch
        {
            AspectRatio.Square_1_1 => (1024, 1024),
            AspectRatio.Portrait_9_16 => (576, 1024),
            AspectRatio.Landscape_16_9 => (1024, 576),
            AspectRatio.Standard_4_3 => (1024, 768),
            AspectRatio.Cinematic_21_9 => (1024, 440),
            _ => (baseWidth > 0 ? baseWidth : 1024, baseHeight > 0 ? baseHeight : 1024)
        };
    }

    private class Txt2ImgResponse
    {
        public List<string>? images { get; set; }
        public Txt2ImgParameters? parameters { get; set; }
    }

    private class Txt2ImgParameters
    {
        public int seed { get; set; }
    }

    private class SdModel
    {
        public string? title { get; set; }
        public string? model_name { get; set; }
    }
}
