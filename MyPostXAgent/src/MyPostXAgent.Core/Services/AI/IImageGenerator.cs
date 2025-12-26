using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Interface for AI image generation services
/// </summary>
public interface IImageGenerator
{
    /// <summary>
    /// Image AI Provider name
    /// </summary>
    ImageAIProvider Provider { get; }

    /// <summary>
    /// Generate image from text prompt
    /// </summary>
    Task<ImageGenerationResult> GenerateImageAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default);
}
