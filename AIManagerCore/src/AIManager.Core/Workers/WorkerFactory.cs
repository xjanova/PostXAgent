using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Factory for creating platform-specific workers
/// </summary>
public class WorkerFactory
{
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
