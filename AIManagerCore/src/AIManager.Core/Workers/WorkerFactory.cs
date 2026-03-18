using System.Collections.Concurrent;
using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Factory for creating platform-specific workers
/// </summary>
public class WorkerFactory
{
    // Cache worker instances per platform for reuse (workers are stateless and thread-safe)
    private static readonly ConcurrentDictionary<SocialPlatform, IPlatformWorker> _workerCache = new();

    // Lazy initialization of dependencies for media workers
    private static readonly Lazy<(FFmpegService ffmpeg, VideoProcessor video, AudioProcessor audio)> _mediaServices = new(() =>
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var ffmpegLogger = loggerFactory.CreateLogger<FFmpegService>();
        var videoLogger = loggerFactory.CreateLogger<VideoProcessor>();
        var audioLogger = loggerFactory.CreateLogger<AudioProcessor>();

        var ffmpegService = new FFmpegService(ffmpegLogger);
        var videoProcessor = new VideoProcessor(ffmpegService, videoLogger);
        var audioProcessor = new AudioProcessor(ffmpegService, audioLogger);

        return (ffmpegService, videoProcessor, audioProcessor);
    });

    private static readonly Dictionary<SocialPlatform, Func<IPlatformWorker>> _factories = new()
    {
        // Social Media Platforms
        { SocialPlatform.Facebook, () => new FacebookWorker() },
        { SocialPlatform.Instagram, () => new InstagramWorker() },
        { SocialPlatform.TikTok, () => new TikTokWorker() },
        { SocialPlatform.Twitter, () => new TwitterWorker() },
        { SocialPlatform.Line, () => new LineWorker() },
        { SocialPlatform.YouTube, () => new YouTubeWorker() },
        { SocialPlatform.Threads, () => new ThreadsWorker() },
        { SocialPlatform.LinkedIn, () => new LinkedInWorker() },
        { SocialPlatform.Pinterest, () => new PinterestWorker() },

        // AI Video Generation Platforms
        { SocialPlatform.Freepik, () => {
            var services = _mediaServices.Value;
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<FreepikWorker>();
            return new FreepikWorker(logger, services.video);
        }},

        // AI Music Generation Platforms
        { SocialPlatform.SunoAI, () => {
            var services = _mediaServices.Value;
            var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SunoAIWorker>();
            return new SunoAIWorker(logger, services.audio);
        }},

        // Note: Runway, PikaLabs, LumaAI workers can be added here as fallback video providers
    };

    /// <summary>
    /// Get worker for a specific platform (instance method for DI).
    /// Returns a cached instance if available, creates one on first request.
    /// </summary>
    public IPlatformWorker GetWorker(SocialPlatform platform)
    {
        return GetOrCreateWorker(platform);
    }

    /// <summary>
    /// Get worker for a specific platform by name.
    /// Returns a cached instance if available, creates one on first request.
    /// </summary>
    public IPlatformWorker? GetWorker(string platformName)
    {
        if (Enum.TryParse<SocialPlatform>(platformName, true, out var platform))
        {
            try
            {
                return GetOrCreateWorker(platform);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Get or create a cached worker for a specific platform (thread-safe)
    /// </summary>
    public static IPlatformWorker GetOrCreateWorker(SocialPlatform platform)
    {
        return _workerCache.GetOrAdd(platform, p =>
        {
            if (_factories.TryGetValue(p, out var factory))
            {
                return factory();
            }
            throw new ArgumentException($"No worker available for platform: {p}");
        });
    }

    /// <summary>
    /// Create a new worker for a specific platform (always creates a new instance)
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
