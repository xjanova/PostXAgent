using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Interface for AI content generation services
/// </summary>
public interface IAIContentGenerator
{
    /// <summary>
    /// AI Provider name
    /// </summary>
    AIProvider Provider { get; }

    /// <summary>
    /// Check if the provider is configured and available
    /// </summary>
    Task<AIProviderStatus> CheckStatusAsync();

    /// <summary>
    /// Generate social media content
    /// </summary>
    Task<ContentGenerationResult> GenerateContentAsync(ContentGenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate hashtags for content
    /// </summary>
    Task<string> GenerateHashtagsAsync(string topic, string keywords, CancellationToken cancellationToken = default);
}
