using System.Net.Http.Headers;
using System.Net.Http.Json;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Leonardo.AI Image Generator (Free Tier Available)
/// </summary>
public class LeonardoImageGenerator : IImageGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _outputDir;
    private string _apiKey = string.Empty;

    public ImageAIProvider Provider => ImageAIProvider.Leonardo;

    public LeonardoImageGenerator(string? apiKey = null, string? outputDir = null)
    {
        _apiKey = apiKey ?? string.Empty;
        _outputDir = outputDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyPostXAgent", "generated_images");

        Directory.CreateDirectory(_outputDir);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://cloud.leonardo.ai/api/rest/v1/"),
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    public void SetApiKey(string apiKey)
    {
        _apiKey = apiKey;
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        var result = new ImageGenerationResult { Provider = Provider };

        if (string.IsNullOrEmpty(_apiKey))
        {
            result.ErrorMessage = "Leonardo API key not configured";
            return result;
        }

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var (width, height) = GetDimensions(request.AspectRatio);

            var createRequest = new
            {
                prompt = request.Prompt,
                negative_prompt = string.IsNullOrEmpty(request.NegativePrompt) 
                    ? "ugly, blurry, low quality" 
                    : request.NegativePrompt,
                width = width,
                height = height,
                num_images = Math.Min(request.NumImages, 4),
                guidance_scale = request.CfgScale > 0 ? request.CfgScale : 7,
                num_inference_steps = request.Steps > 0 ? request.Steps : 30,
                modelId = "6b645e3a-d64f-4341-a6d8-7a3690fbf042"
            };

            var createResponse = await _httpClient.PostAsJsonAsync("generations", createRequest, cancellationToken);

            if (!createResponse.IsSuccessStatusCode)
            {
                var error = await createResponse.Content.ReadAsStringAsync(cancellationToken);
                result.ErrorMessage = $"Failed to create generation: {error}";
                return result;
            }

            var createResult = await createResponse.Content.ReadFromJsonAsync<CreateGenerationResponse>(cancellationToken: cancellationToken);
            var generationId = createResult?.sdGenerationJob?.generationId;

            if (string.IsNullOrEmpty(generationId))
            {
                result.ErrorMessage = "No generation ID returned";
                return result;
            }

            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(2000, cancellationToken);

                var statusResponse = await _httpClient.GetAsync($"generations/{generationId}", cancellationToken);
                if (!statusResponse.IsSuccessStatusCode) continue;

                var statusResult = await statusResponse.Content.ReadFromJsonAsync<GetGenerationResponse>(cancellationToken: cancellationToken);
                var generation = statusResult?.generations_by_pk;

                if (generation?.status == "COMPLETE" && generation.generated_images != null)
                {
                    foreach (var image in generation.generated_images)
                    {
                        if (!string.IsNullOrEmpty(image.url))
                        {
                            var imageBytes = await _httpClient.GetByteArrayAsync(image.url, cancellationToken);
                            var fileName = $"leonardo_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
                            var filePath = Path.Combine(_outputDir, fileName);

                            await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

                            result.Images.Add(new GeneratedImage
                            {
                                FilePath = filePath,
                                ImageData = imageBytes,
                                Url = image.url,
                                Width = width,
                                Height = height
                            });
                        }
                    }

                    result.Success = result.Images.Count > 0;
                    result.Duration = DateTime.UtcNow - startTime;
                    return result;
                }
                else if (generation?.status == "FAILED")
                {
                    result.ErrorMessage = "Generation failed";
                    return result;
                }
            }

            result.ErrorMessage = "Generation timed out";
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey)) return false;

        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);

            var response = await _httpClient.GetAsync("me", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private (int width, int height) GetDimensions(AspectRatio ratio)
    {
        return ratio switch
        {
            AspectRatio.Square_1_1 => (1024, 1024),
            AspectRatio.Portrait_9_16 => (576, 1024),
            AspectRatio.Landscape_16_9 => (1024, 576),
            _ => (1024, 1024)
        };
    }

    private class CreateGenerationResponse
    {
        public SdGenerationJob? sdGenerationJob { get; set; }
    }

    private class SdGenerationJob
    {
        public string? generationId { get; set; }
    }

    private class GetGenerationResponse
    {
        public GenerationByPk? generations_by_pk { get; set; }
    }

    private class GenerationByPk
    {
        public string? status { get; set; }
        public List<GeneratedImageInfo>? generated_images { get; set; }
    }

    private class GeneratedImageInfo
    {
        public string? url { get; set; }
    }
}
