using AIManager.Core.Models;

namespace AIManager.Core.Workers;

/// <summary>
/// Factory for creating platform-specific workers
/// </summary>
public static class WorkerFactory
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

    public static IPlatformWorker CreateWorker(SocialPlatform platform)
    {
        if (_factories.TryGetValue(platform, out var factory))
        {
            return factory();
        }

        throw new ArgumentException($"No worker available for platform: {platform}");
    }
}
