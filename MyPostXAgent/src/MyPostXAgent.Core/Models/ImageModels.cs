namespace MyPostXAgent.Core.Models;

/// <summary>
/// Image generation request
/// </summary>
public class ImageGenerationRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
    public int Steps { get; set; } = 30;
    public double CfgScale { get; set; } = 7.0;
    public string Style { get; set; } = "realistic";
}

/// <summary>
/// Image generation result
/// </summary>
public class ImageGenerationResult
{
    public bool Success { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageUrl { get; set; }
    public string? Base64Image { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public ImageAIProvider Provider { get; set; }
    public TimeSpan Duration { get; set; }
}
