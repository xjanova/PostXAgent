using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// High-level video processing utilities
/// ยูทิลิตี้ระดับสูงสำหรับประมวลผลวีดีโอ
/// </summary>
public class VideoProcessor
{
    private readonly FFmpegService _ffmpegService;
    private readonly ILogger<VideoProcessor> _logger;
    private readonly string _workingDirectory;

    public VideoProcessor(
        FFmpegService ffmpegService,
        ILogger<VideoProcessor> logger,
        string? workingDirectory = null)
    {
        _ffmpegService = ffmpegService;
        _logger = logger;
        _workingDirectory = workingDirectory ?? Path.Combine(Path.GetTempPath(), "PostXAgent", "Videos");

        // Ensure working directory exists
        if (!Directory.Exists(_workingDirectory))
        {
            Directory.CreateDirectory(_workingDirectory);
        }
    }

    /// <summary>
    /// Process video according to MediaProcessingConfig
    /// ประมวลผลวีดีโอตามการตั้งค่า MediaProcessingConfig
    /// </summary>
    public async Task<MediaGenerationResult?> ProcessVideoAsync(
        MediaProcessingConfig config,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Starting video processing with config");

            var result = new MediaGenerationResult();

            // Handle video concatenation if multiple videos provided
            string? processedVideoPath = config.VideoPath;
            if (config.VideosToConcat?.Count > 1)
            {
                _logger.LogInformation("Concatenating {Count} videos", config.VideosToConcat.Count);
                var concatOutput = GenerateOutputPath("concatenated", config.OutputFormat);
                processedVideoPath = await _ffmpegService.ConcatenateVideosAsync(
                    config.VideosToConcat,
                    concatOutput,
                    ct
                );

                if (processedVideoPath == null)
                {
                    _logger.LogError("Failed to concatenate videos");
                    return null;
                }
            }

            if (string.IsNullOrEmpty(processedVideoPath))
            {
                _logger.LogError("No video path provided");
                return null;
            }

            // Mix audio if configured
            if (config.MixAudio && !string.IsNullOrEmpty(config.AudioPath))
            {
                _logger.LogInformation("Mixing audio with video");
                var mixedOutput = GenerateOutputPath("mixed", config.OutputFormat);
                processedVideoPath = await _ffmpegService.MixVideoWithAudioAsync(
                    processedVideoPath,
                    config.AudioPath,
                    mixedOutput,
                    config.AudioVolume,
                    ct
                );

                if (processedVideoPath == null)
                {
                    _logger.LogError("Failed to mix audio with video");
                    return null;
                }
            }

            // Convert format if needed
            var finalOutput = GenerateOutputPath("final", config.OutputFormat);
            var convertedPath = await _ffmpegService.ConvertVideoFormatAsync(
                processedVideoPath,
                finalOutput,
                config.OutputFormat,
                config.VideoCodec,
                config.AudioCodec,
                config.Crf,
                config.Preset,
                ct
            );

            if (convertedPath == null)
            {
                _logger.LogError("Failed to convert video format");
                return null;
            }

            result.VideoPath = convertedPath;

            // Generate thumbnail if configured
            if (config.GenerateThumbnail)
            {
                _logger.LogInformation("Generating thumbnail at {Time}s", config.ThumbnailTimeOffset);
                var thumbnailPath = GenerateOutputPath("thumbnail", "jpg");
                result.ThumbnailPath = await _ffmpegService.GenerateThumbnailAsync(
                    convertedPath,
                    thumbnailPath,
                    config.ThumbnailTimeOffset,
                    ct
                );
            }

            // Get video metadata
            result.Metadata = await _ffmpegService.GetVideoMetadataAsync(convertedPath, ct);

            _logger.LogInformation("Video processing completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video");
            return null;
        }
    }

    /// <summary>
    /// Prepare video for social media platform
    /// เตรียมวีดีโอสำหรับแพลตฟอร์มโซเชียลมีเดีย
    /// </summary>
    public async Task<MediaGenerationResult?> PrepareForPlatformAsync(
        string videoPath,
        SocialPlatform platform,
        AspectRatio? targetAspectRatio = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Preparing video for platform: {Platform}", platform);

            // Get platform-specific video requirements
            var requirements = GetPlatformRequirements(platform);

            // Get current video metadata
            var metadata = await _ffmpegService.GetVideoMetadataAsync(videoPath, ct);
            if (metadata == null)
            {
                _logger.LogError("Failed to get video metadata");
                return null;
            }

            var needsResizing = false;
            var targetWidth = metadata.Width;
            var targetHeight = metadata.Height;

            // Check if resizing is needed based on aspect ratio
            if (targetAspectRatio.HasValue)
            {
                (targetWidth, targetHeight) = GetDimensionsForAspectRatio(
                    targetAspectRatio.Value,
                    requirements.MaxWidth,
                    requirements.MaxHeight
                );
                needsResizing = targetWidth != metadata.Width || targetHeight != metadata.Height;
            }
            // Check if video exceeds platform limits
            else if (metadata.Width > requirements.MaxWidth || metadata.Height > requirements.MaxHeight)
            {
                var scale = Math.Min(
                    (double)requirements.MaxWidth / metadata.Width,
                    (double)requirements.MaxHeight / metadata.Height
                );
                targetWidth = (int)(metadata.Width * scale);
                targetHeight = (int)(metadata.Height * scale);
                needsResizing = true;
            }

            string? processedPath = videoPath;

            // Resize if needed
            if (needsResizing)
            {
                _logger.LogInformation("Resizing video to {Width}x{Height}", targetWidth, targetHeight);
                var resizedOutput = GenerateOutputPath($"resized_{platform}", "mp4");
                processedPath = await _ffmpegService.ResizeVideoAsync(
                    videoPath,
                    resizedOutput,
                    targetWidth,
                    targetHeight,
                    requirements.PreferredCodec,
                    requirements.Crf,
                    ct
                );

                if (processedPath == null)
                {
                    _logger.LogError("Failed to resize video");
                    return null;
                }
            }

            var result = new MediaGenerationResult
            {
                VideoPath = processedPath,
                Metadata = await _ffmpegService.GetVideoMetadataAsync(processedPath, ct)
            };

            // Generate thumbnail
            var thumbnailPath = GenerateOutputPath($"thumb_{platform}", "jpg");
            result.ThumbnailPath = await _ffmpegService.GenerateThumbnailAsync(
                processedPath,
                thumbnailPath,
                1.0,
                ct
            );

            _logger.LogInformation("Video prepared for {Platform} successfully", platform);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preparing video for platform");
            return null;
        }
    }

    /// <summary>
    /// Create video slideshow from images
    /// สร้างวีดีโอสไลด์โชว์จากรูปภาพ
    /// </summary>
    public async Task<string?> CreateSlideshowAsync(
        List<string> imagePaths,
        string? audioPath,
        string outputPath,
        int durationPerImage = 3,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Creating slideshow from {Count} images", imagePaths.Count);

            // Create temporary video clips from each image
            var videoClips = new List<string>();
            foreach (var imagePath in imagePaths)
            {
                var clipPath = GenerateOutputPath($"clip_{Guid.NewGuid()}", "mp4");

                // FFmpeg: Create video from image with duration
                // ffmpeg -loop 1 -i image.jpg -c:v libx264 -t 3 -pix_fmt yuv420p output.mp4
                var args = $"-loop 1 -i \"{imagePath}\" -c:v libx264 -t {durationPerImage} -pix_fmt yuv420p \"{clipPath}\"";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process != null)
                {
                    await process.WaitForExitAsync(ct);
                    if (process.ExitCode == 0 && File.Exists(clipPath))
                    {
                        videoClips.Add(clipPath);
                    }
                }
            }

            if (videoClips.Count == 0)
            {
                _logger.LogError("Failed to create video clips from images");
                return null;
            }

            // Concatenate all clips
            var concatenatedPath = GenerateOutputPath("slideshow_concat", "mp4");
            var slideshowPath = await _ffmpegService.ConcatenateVideosAsync(videoClips, concatenatedPath, ct);

            // Clean up temporary clips
            foreach (var clip in videoClips)
            {
                try { File.Delete(clip); } catch { }
            }

            if (slideshowPath == null)
            {
                _logger.LogError("Failed to concatenate slideshow clips");
                return null;
            }

            // Add audio if provided
            if (!string.IsNullOrEmpty(audioPath))
            {
                var finalPath = await _ffmpegService.MixVideoWithAudioAsync(
                    slideshowPath,
                    audioPath,
                    outputPath,
                    1.0,
                    ct
                );

                // Clean up intermediate file
                try { File.Delete(slideshowPath); } catch { }

                return finalPath;
            }

            // If no audio, just move to output path
            File.Move(slideshowPath, outputPath, true);
            return outputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating slideshow");
            return null;
        }
    }

    /// <summary>
    /// Get platform-specific video requirements
    /// รับข้อกำหนดวีดีโอเฉพาะแพลตฟอร์ม
    /// </summary>
    private PlatformRequirements GetPlatformRequirements(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => new PlatformRequirements
            {
                MaxWidth = 1920,
                MaxHeight = 1080,
                PreferredCodec = "libx264",
                Crf = 23
            },
            SocialPlatform.Instagram => new PlatformRequirements
            {
                MaxWidth = 1080,
                MaxHeight = 1920, // For stories/reels
                PreferredCodec = "libx264",
                Crf = 23
            },
            SocialPlatform.TikTok => new PlatformRequirements
            {
                MaxWidth = 1080,
                MaxHeight = 1920,
                PreferredCodec = "libx264",
                Crf = 23
            },
            SocialPlatform.YouTube => new PlatformRequirements
            {
                MaxWidth = 3840,
                MaxHeight = 2160, // 4K support
                PreferredCodec = "libx264",
                Crf = 18 // Higher quality for YouTube
            },
            SocialPlatform.Twitter => new PlatformRequirements
            {
                MaxWidth = 1920,
                MaxHeight = 1200,
                PreferredCodec = "libx264",
                Crf = 23
            },
            _ => new PlatformRequirements
            {
                MaxWidth = 1920,
                MaxHeight = 1080,
                PreferredCodec = "libx264",
                Crf = 23
            }
        };
    }

    /// <summary>
    /// Get video dimensions for target aspect ratio
    /// คำนวณขนาดวีดีโอสำหรับ aspect ratio ที่ต้องการ
    /// </summary>
    private (int width, int height) GetDimensionsForAspectRatio(
        AspectRatio aspectRatio,
        int maxWidth,
        int maxHeight)
    {
        return aspectRatio switch
        {
            AspectRatio.Landscape_16_9 => (Math.Min(1920, maxWidth), Math.Min(1080, maxHeight)),
            AspectRatio.Portrait_9_16 => (Math.Min(1080, maxWidth), Math.Min(1920, maxHeight)),
            AspectRatio.Square_1_1 => (Math.Min(1080, Math.Min(maxWidth, maxHeight)), Math.Min(1080, Math.Min(maxWidth, maxHeight))),
            AspectRatio.Classic_4_3 => (Math.Min(1440, maxWidth), Math.Min(1080, maxHeight)),
            AspectRatio.Ultrawide_21_9 => (Math.Min(2560, maxWidth), Math.Min(1080, maxHeight)),
            _ => (1920, 1080)
        };
    }

    /// <summary>
    /// Generate output file path
    /// สร้าง path สำหรับไฟล์ output
    /// </summary>
    private string GenerateOutputPath(string prefix, string extension)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"{prefix}_{timestamp}_{Guid.NewGuid():N}.{extension}";
        return Path.Combine(_workingDirectory, filename);
    }

    /// <summary>
    /// Platform-specific video requirements
    /// ข้อกำหนดวีดีโอเฉพาะแพลตฟอร์ม
    /// </summary>
    private class PlatformRequirements
    {
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
        public string PreferredCodec { get; set; } = "libx264";
        public int Crf { get; set; } = 23;
    }
}
