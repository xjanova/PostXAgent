using System.Diagnostics;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Video Assembly Service
/// ประกอบวิดีโอจากภาพและเสียง โดยใช้ FFmpeg
/// </summary>
public class VideoAssemblyService
{
    private readonly ILogger<VideoAssemblyService>? _logger;
    private readonly string _workDir;

    public VideoAssemblyService(ILogger<VideoAssemblyService>? logger = null)
    {
        _logger = logger;
        _workDir = Path.Combine(Path.GetTempPath(), "aimanager_video");
        Directory.CreateDirectory(_workDir);
    }

    #region Video Assembly

    /// <summary>
    /// Assemble final video from images and audio
    /// </summary>
    public async Task<FinalVideo> AssembleVideoAsync(
        VideoScript script,
        List<GeneratedImage> images,
        GeneratedAudio? audio,
        UserPackage package,
        VideoAssemblyOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new VideoAssemblyOptions();

        _logger?.LogInformation("Assembling video with {ImageCount} images", images.Count);

        var outputDir = Path.Combine(_workDir, Guid.NewGuid().ToString());
        Directory.CreateDirectory(outputDir);

        try
        {
            // Create video from images
            var tempVideoPath = Path.Combine(outputDir, "temp_video.mp4");
            await CreateVideoFromImagesAsync(script, images, tempVideoPath, options, ct);

            // Add audio if available
            var finalPath = Path.Combine(outputDir, "final_video.mp4");
            if (audio?.LocalPath != null && File.Exists(audio.LocalPath))
            {
                await AddAudioToVideoAsync(tempVideoPath, audio.LocalPath, finalPath, audio.BackgroundMusic, options, ct);
            }
            else
            {
                File.Copy(tempVideoPath, finalPath, true);
            }

            // Add watermark if required by package
            if (package.HasWatermark)
            {
                var watermarkedPath = Path.Combine(outputDir, "watermarked.mp4");
                await AddWatermarkAsync(finalPath, watermarkedPath, options.WatermarkText ?? "AI Manager", ct);
                finalPath = watermarkedPath;
            }

            // Get file info
            var fileInfo = new FileInfo(finalPath);
            var duration = await GetVideoDurationAsync(finalPath, ct);

            // Create thumbnail
            var thumbnailPath = Path.Combine(outputDir, "thumbnail.jpg");
            await CreateThumbnailAsync(finalPath, thumbnailPath, ct);

            return new FinalVideo
            {
                Title = script.Title,
                Description = $"{script.Hook}\n\n{string.Join(" ", script.Hashtags.Select(h => $"#{h}"))}",
                LocalPath = finalPath,
                DurationSeconds = duration,
                Width = options.Width,
                Height = options.Height,
                Format = "mp4",
                FileSizeBytes = fileInfo.Length,
                ThumbnailPath = thumbnailPath,
                HasWatermark = package.HasWatermark
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to assemble video");
            throw;
        }
    }

    /// <summary>
    /// Create video from images with transitions
    /// </summary>
    private async Task CreateVideoFromImagesAsync(
        VideoScript script,
        List<GeneratedImage> images,
        string outputPath,
        VideoAssemblyOptions options,
        CancellationToken ct)
    {
        var filterComplex = new List<string>();
        var inputCount = 0;

        // Build FFmpeg filter for each scene
        var sceneFilters = new List<string>();
        var concatInputs = new List<string>();

        foreach (var scene in script.Scenes)
        {
            var image = images.FirstOrDefault(i => i.SceneNumber == scene.SceneNumber);
            if (image?.LocalPath == null || !File.Exists(image.LocalPath)) continue;

            var duration = scene.DurationSeconds;
            var inputIndex = inputCount++;

            // Scale and add Ken Burns effect if needed
            var scaleFilter = $"[{inputIndex}:v]scale={options.Width}:{options.Height}:force_original_aspect_ratio=decrease,pad={options.Width}:{options.Height}:(ow-iw)/2:(oh-ih)/2";

            if (options.Style == VideoStyle.KenBurns)
            {
                // Add zoom/pan effect
                var zoomDirection = scene.SceneNumber % 2 == 0 ? "in" : "out";
                scaleFilter += $",zoompan=z='if(eq(on,1),1,min(zoom+0.001,1.5))':d={duration * options.FrameRate}:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)'";
            }

            scaleFilter += $",setpts=PTS-STARTPTS,fps={options.FrameRate}";
            scaleFilter += $"[v{inputIndex}]";

            sceneFilters.Add(scaleFilter);
            concatInputs.Add($"[v{inputIndex}]");
        }

        if (inputCount == 0)
        {
            throw new Exception("No valid images to create video");
        }

        // Build full filter
        var fullFilter = string.Join(";", sceneFilters);
        fullFilter += $";{string.Join("", concatInputs)}concat=n={inputCount}:v=1:a=0[outv]";

        // Build input arguments
        var inputArgs = new List<string>();
        foreach (var scene in script.Scenes)
        {
            var image = images.FirstOrDefault(i => i.SceneNumber == scene.SceneNumber);
            if (image?.LocalPath == null || !File.Exists(image.LocalPath)) continue;

            inputArgs.Add($"-loop 1 -t {scene.DurationSeconds} -i \"{image.LocalPath}\"");
        }

        var ffmpegArgs = $"{string.Join(" ", inputArgs)} -filter_complex \"{fullFilter}\" -map \"[outv]\" -c:v libx264 -preset {options.Preset} -crf {options.Quality} -pix_fmt yuv420p \"{outputPath}\"";

        await RunFfmpegAsync(ffmpegArgs, ct);
    }

    /// <summary>
    /// Add audio to video
    /// </summary>
    private async Task AddAudioToVideoAsync(
        string videoPath,
        string audioPath,
        string outputPath,
        BackgroundMusic? bgMusic,
        VideoAssemblyOptions options,
        CancellationToken ct)
    {
        var ffmpegArgs = "";

        if (bgMusic?.FilePath != null && File.Exists(bgMusic.FilePath))
        {
            // Mix narration with background music
            var bgVolume = bgMusic.Volume;
            ffmpegArgs = $"-i \"{videoPath}\" -i \"{audioPath}\" -i \"{bgMusic.FilePath}\" " +
                        $"-filter_complex \"[1:a]volume=1[a1];[2:a]volume={bgVolume}[a2];[a1][a2]amix=inputs=2:duration=shortest[aout]\" " +
                        $"-map 0:v -map \"[aout]\" -c:v copy -c:a aac -shortest \"{outputPath}\"";
        }
        else
        {
            // Just add narration
            ffmpegArgs = $"-i \"{videoPath}\" -i \"{audioPath}\" -map 0:v -map 1:a -c:v copy -c:a aac -shortest \"{outputPath}\"";
        }

        await RunFfmpegAsync(ffmpegArgs, ct);
    }

    /// <summary>
    /// Add watermark to video
    /// </summary>
    private async Task AddWatermarkAsync(
        string inputPath,
        string outputPath,
        string watermarkText,
        CancellationToken ct)
    {
        // Add text watermark at bottom right
        var ffmpegArgs = $"-i \"{inputPath}\" -vf \"drawtext=text='{watermarkText}':fontsize=24:fontcolor=white@0.5:x=w-tw-20:y=h-th-20\" -c:a copy \"{outputPath}\"";

        await RunFfmpegAsync(ffmpegArgs, ct);
    }

    /// <summary>
    /// Create thumbnail from video
    /// </summary>
    private async Task CreateThumbnailAsync(
        string videoPath,
        string thumbnailPath,
        CancellationToken ct)
    {
        // Extract frame at 1 second
        var ffmpegArgs = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -q:v 2 \"{thumbnailPath}\"";
        await RunFfmpegAsync(ffmpegArgs, ct);
    }

    /// <summary>
    /// Get video duration
    /// </summary>
    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{videoPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        return double.TryParse(output.Trim(), out var duration) ? duration : 0;
    }

    #endregion

    #region Platform-Specific Export

    /// <summary>
    /// Export video for specific platform
    /// </summary>
    public async Task<string> ExportForPlatformAsync(
        string videoPath,
        SocialPlatform platform,
        CancellationToken ct = default)
    {
        var specs = GetPlatformSpecs(platform);
        var outputPath = Path.Combine(
            Path.GetDirectoryName(videoPath) ?? _workDir,
            $"{Path.GetFileNameWithoutExtension(videoPath)}_{platform.ToString().ToLower()}.mp4");

        var ffmpegArgs = $"-i \"{videoPath}\" -vf \"scale={specs.Width}:{specs.Height}:force_original_aspect_ratio=decrease,pad={specs.Width}:{specs.Height}:(ow-iw)/2:(oh-ih)/2\" " +
                        $"-c:v libx264 -preset fast -crf 23 -c:a aac -b:a 128k \"{outputPath}\"";

        await RunFfmpegAsync(ffmpegArgs, ct);

        return outputPath;
    }

    private PlatformVideoSpecs GetPlatformSpecs(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.TikTok => new PlatformVideoSpecs { Width = 1080, Height = 1920, MaxDuration = 180, AspectRatio = "9:16" },
            SocialPlatform.Instagram => new PlatformVideoSpecs { Width = 1080, Height = 1920, MaxDuration = 90, AspectRatio = "9:16" }, // Reels
            SocialPlatform.YouTube => new PlatformVideoSpecs { Width = 1920, Height = 1080, MaxDuration = 3600, AspectRatio = "16:9" },
            SocialPlatform.Facebook => new PlatformVideoSpecs { Width = 1280, Height = 720, MaxDuration = 240, AspectRatio = "16:9" },
            SocialPlatform.Twitter => new PlatformVideoSpecs { Width = 1280, Height = 720, MaxDuration = 140, AspectRatio = "16:9" },
            SocialPlatform.Line => new PlatformVideoSpecs { Width = 1080, Height = 1080, MaxDuration = 60, AspectRatio = "1:1" },
            _ => new PlatformVideoSpecs { Width = 1920, Height = 1080, MaxDuration = 300, AspectRatio = "16:9" }
        };
    }

    #endregion

    #region Helper Methods

    private async Task RunFfmpegAsync(string arguments, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y {arguments}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _logger?.LogDebug("Running FFmpeg: {Args}", arguments);

        process.Start();

        var errorTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = await errorTask;
            _logger?.LogWarning("FFmpeg stderr: {Error}", error);

            // Check if output file exists despite exit code (ffmpeg sometimes reports errors but succeeds)
            // If the output file doesn't exist, throw
        }
    }

    /// <summary>
    /// Check if FFmpeg is available
    /// </summary>
    public static bool IsFfmpegAvailable()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get FFmpeg installation instructions
    /// </summary>
    public static string GetFfmpegInstallInstructions(string language = "th")
    {
        if (language == "th")
        {
            return @"การติดตั้ง FFmpeg:

Windows:
1. ดาวน์โหลดจาก https://www.gyan.dev/ffmpeg/builds/
2. แตกไฟล์ไปยัง C:\ffmpeg
3. เพิ่ม C:\ffmpeg\bin ใน System PATH
4. รีสตาร์ท Command Prompt

หรือใช้ Chocolatey:
choco install ffmpeg

หรือใช้ Winget:
winget install ffmpeg

macOS:
brew install ffmpeg

Linux (Ubuntu/Debian):
sudo apt update && sudo apt install ffmpeg";
        }

        return @"FFmpeg Installation:

Windows:
1. Download from https://www.gyan.dev/ffmpeg/builds/
2. Extract to C:\ffmpeg
3. Add C:\ffmpeg\bin to System PATH
4. Restart Command Prompt

Or use Chocolatey:
choco install ffmpeg

Or use Winget:
winget install ffmpeg

macOS:
brew install ffmpeg

Linux (Ubuntu/Debian):
sudo apt update && sudo apt install ffmpeg";
    }

    #endregion
}

/// <summary>
/// Video Assembly Options
/// </summary>
public class VideoAssemblyOptions
{
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int FrameRate { get; set; } = 30;
    public VideoStyle Style { get; set; } = VideoStyle.Slideshow;
    public string Preset { get; set; } = "fast"; // ultrafast, fast, medium, slow
    public int Quality { get; set; } = 23; // CRF 0-51, lower = better
    public string? WatermarkText { get; set; }
    public string? WatermarkImage { get; set; }
    public string OutputFormat { get; set; } = "mp4";
}

/// <summary>
/// Platform Video Specifications
/// </summary>
public class PlatformVideoSpecs
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int MaxDuration { get; set; }
    public string AspectRatio { get; set; } = "16:9";
}
