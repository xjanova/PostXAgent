namespace MyPostXAgent.Core.Models;

/// <summary>
/// AI Content generation request
/// </summary>
public class ContentGenerationRequest
{
    public string Topic { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public bool IncludeEmojis { get; set; }
    public bool IncludeCTA { get; set; }
    public List<string> TargetPlatforms { get; set; } = new();
}

/// <summary>
/// AI Content generation response
/// </summary>
public class ContentGenerationResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Hashtags { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public AIProvider Provider { get; set; }
    public int TokensUsed { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// AI Provider status
/// </summary>
public class AIProviderStatus
{
    public AIProvider Provider { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsConfigured { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
}
