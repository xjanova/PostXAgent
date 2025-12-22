using AIManager.Core.Models;

namespace AIManager.Core.Workers;

/// <summary>
/// Factory for creating platform-specific workers
/// </summary>
public class WorkerFactory
{
    private static readonly Dictionary<SocialPlatform, Func<IPlatformWorker>> _factories = new()
    {
        { SocialPlatform.Facebook, () => new FacebookWorker() },
        { SocialPlatform.Instagram, () => new InstagramWorker() },
        { SocialPlatform.TikTok, () => new TikTokWorker() },
        { SocialPlatform.Twitter, () => new TwitterWorker() },
        { SocialPlatform.Line, () => new LineWorker() },
        { SocialPlatform.YouTube, () => new YouTubeWorker() },
        { SocialPlatform.Threads, () => new ThreadsWorker() },
        { SocialPlatform.LinkedIn, () => new LinkedInWorker() },
        { SocialPlatform.Pinterest, () => new PinterestWorker() },
    };

    /// <summary>
    /// Get worker for a specific platform (instance method for DI)
    /// </summary>
    public IPlatformWorker GetWorker(SocialPlatform platform)
    {
        return CreateWorker(platform);
    }

    /// <summary>
    /// Get worker for a specific platform by name
    /// </summary>
    public IPlatformWorker? GetWorker(string platformName)
    {
        if (Enum.TryParse<SocialPlatform>(platformName, true, out var platform))
        {
            try
            {
                return CreateWorker(platform);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Create worker for a specific platform (static method)
    /// </summary>
    public static IPlatformWorker CreateWorker(SocialPlatform platform)
    {
        if (_factories.TryGetValue(platform, out var factory))
        {
            return factory();
        }

        throw new ArgumentException($"No worker available for platform: {platform}");
    }
}
