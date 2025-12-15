using System.Net.Http.Json;
using System.Text.Json;

namespace AIManager.Core.Services;

/// <summary>
/// AI Image Generation Service
/// Supports DALL-E, Stable Diffusion, and Leonardo.ai
/// </summary>
public class ImageGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly AIConfig _config;

    public ImageGeneratorService()
    {
        _httpClient = new HttpClient();
        _config = AIConfig.Load();
    }

    public async Task<GeneratedImage> GenerateAsync(
        string prompt,
        string style,
        string size,
        string provider,
        CancellationToken ct)
    {
        // Auto-select provider
        if (provider == "auto")
        {
            // Try free providers first
            var result = await GenerateWithStableDiffusionAsync(prompt, style, size, ct);
            if (result != null) return result;

            result = await GenerateWithDallEAsync(prompt, style, size, ct);
            if (result != null) return result;

            throw new Exception("No image generator available");
        }

        return provider switch
        {
            "dalle" => await GenerateWithDallEAsync(prompt, style, size, ct)
                       ?? throw new Exception("DALL-E generation failed"),
            "sd" or "stable_diffusion" => await GenerateWithStableDiffusionAsync(prompt, style, size, ct)
                                          ?? throw new Exception("Stable Diffusion generation failed"),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };
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

public class GeneratedImage
{
    public string Url { get; set; } = "";
    public string Base64Data { get; set; } = "";
    public string Provider { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
}
