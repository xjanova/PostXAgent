namespace MyPostXAgent.Core.Models;

#region Image Generation Models

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
    public string Style { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Seed { get; set; } = -1;
    public int NumImages { get; set; } = 1;
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Square_1_1;
}

/// <summary>
/// Image generation result
/// </summary>
public class ImageGenerationResult
{
    public bool Success { get; set; }
    public List<GeneratedImage> Images { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
    public ImageAIProvider Provider { get; set; }
    public TimeSpan Duration { get; set; }
    public int CreditsUsed { get; set; }
}

/// <summary>
/// Generated image data
/// </summary>
public class GeneratedImage
{
    public string FilePath { get; set; } = string.Empty;
    public byte[]? ImageData { get; set; }
    public string Base64 { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Seed { get; set; }
}

/// <summary>
/// Image AI Provider status
/// </summary>
public class ImageProviderStatus
{
    public ImageAIProvider Provider { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsConfigured { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RemainingCredits { get; set; }
    public DateTime LastChecked { get; set; }
}

#endregion

#region Video Generation Models

/// <summary>
/// Video generation request
/// </summary>
public class VideoGenerationRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public int DurationSeconds { get; set; } = 5;
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Portrait_9_16;
    public VideoQuality Quality { get; set; } = VideoQuality.HD_720p;
    public string Style { get; set; } = string.Empty;
    public bool WithAudio { get; set; } = false;
}

/// <summary>
/// Video generation result
/// </summary>
public class VideoGenerationResult
{
    public bool Success { get; set; }
    public string VideoPath { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public VideoAIProvider Provider { get; set; }
    public TimeSpan Duration { get; set; }
    public int CreditsUsed { get; set; }
}

#endregion

#region Music Generation Models

/// <summary>
/// Music generation request
/// </summary>
public class MusicGenerationRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string Mood { get; set; } = string.Empty;
    public int DurationSeconds { get; set; } = 30;
    public bool Instrumental { get; set; } = true;
    public string Lyrics { get; set; } = string.Empty;
}

/// <summary>
/// Music generation result
/// </summary>
public class MusicGenerationResult
{
    public bool Success { get; set; }
    public string AudioPath { get; set; } = string.Empty;
    public string AudioUrl { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public MusicAIProvider Provider { get; set; }
    public TimeSpan Duration { get; set; }
    public int CreditsUsed { get; set; }
}

#endregion
