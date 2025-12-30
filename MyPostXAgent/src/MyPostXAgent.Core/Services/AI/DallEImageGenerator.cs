using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// OpenAI DALL-E 3 Image Generator (Paid)
/// </summary>
public class DallEImageGenerator : IImageGenerator
{
    private readonly HttpClient _httpClient;
    private readonly string _outputDir;
    private string _apiKey = string.Empty;

    public ImageAIProvider Provider => ImageAIProvider.DallE;

    public DallEImageGenerator(string? apiKey = null, string? outputDir = null)
    {
        _apiKey = apiKey ?? string.Empty;
        _outputDir = outputDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyPostXAgent", "generated_images");

        Directory.CreateDirectory(_outputDir);

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/"),
            Timeout = TimeSpan.FromMinutes(3)
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
            result.ErrorMessage = "OpenAI API key not configured";
            return result;
        }

        try
        {
            // Set auth header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);

            // Get size based on aspect ratio
            var size = GetDalleSize(request.AspectRatio);

            // Build API request
            var apiRequest = new
            {
                model = "dall-e-3",
                prompt = request.Prompt,
                n = 1, // DALL-E 3 only supports 1 image at a time
                size = size,
                quality = "standard",
                response_format = "b64_json"
            };

            var response = await _httpClient.PostAsJsonAsync(
                "v1/images/generations",
                apiRequest,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                result.ErrorMessage = $"API error: {response.StatusCode} - {errorContent}";
                return result;
            }

            var jsonResponse = await response.Content.ReadFromJsonAsync<DallEResponse>(cancellationToken: cancellationToken);

            if (jsonResponse?.data == null || jsonResponse.data.Count == 0)
            {
                result.ErrorMessage = "No images returned from API";
                return result;
            }

            // Parse size
            var sizeParts = size.Split('x');
            var width = int.Parse(sizeParts[0]);
            var height = int.Parse(sizeParts[1]);

            // Save images
            foreach (var imageData in jsonResponse.data)
            {
                var base64 = imageData.b64_json ?? string.Empty;
                
                if (!string.IsNullOrEmpty(base64))
                {
                    var imageBytes = Convert.FromBase64String(base64);
                    var fileName = $"dalle_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
                    var filePath = Path.Combine(_outputDir, fileName);

                    await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

                    result.Images.Add(new GeneratedImage
                    {
                        FilePath = filePath,
                        Base64 = base64,
                        ImageData = imageBytes,
                        Url = imageData.url ?? string.Empty,
                        Width = width,
                        Height = height
                    });
                }
                else if (!string.IsNullOrEmpty(imageData.url))
                {
                    // Download from URL
                    var imageBytes = await _httpClient.GetByteArrayAsync(imageData.url, cancellationToken);
                    var fileName = $"dalle_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.png";
                    var filePath = Path.Combine(_outputDir, fileName);

                    await File.WriteAllBytesAsync(filePath, imageBytes, cancellationToken);

                    result.Images.Add(new GeneratedImage
                    {
                        FilePath = filePath,
                        ImageData = imageBytes,
                        Url = imageData.url,
                        Width = width,
                        Height = height
                    });
                }
            }

            result.Success = result.Images.Count > 0;
            result.Duration = DateTime.UtcNow - startTime;
            result.CreditsUsed = 1; // Each DALL-E 3 generation costs credits
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

            var response = await _httpClient.GetAsync("v1/models", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string GetDalleSize(AspectRatio ratio)
    {
        return ratio switch
        {
            AspectRatio.Square_1_1 => "1024x1024",
            AspectRatio.Portrait_9_16 => "1024x1792",
            AspectRatio.Landscape_16_9 => "1792x1024",
            _ => "1024x1024"
        };
    }

    private class DallEResponse
    {
        public List<DallEImageData>? data { get; set; }
    }

    private class DallEImageData
    {
        public string? b64_json { get; set; }
        public string? url { get; set; }
        public string? revised_prompt { get; set; }
    }
}
