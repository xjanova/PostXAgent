using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;
using System.Diagnostics;

namespace AIManager.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly ThaiTtsService _ttsService;
    private readonly LipSyncService _lipSyncService;
    private readonly FFmpegService _ffmpegService;
    private readonly PostPublisherService _postPublisherService;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        ThaiTtsService ttsService,
        LipSyncService lipSyncService,
        FFmpegService ffmpegService,
        PostPublisherService postPublisherService,
        ILogger<PipelineController> logger)
    {
        _ttsService = ttsService;
        _lipSyncService = lipSyncService;
        _ffmpegService = ffmpegService;
        _postPublisherService = postPublisherService;
        _logger = logger;
    }

    /// <summary>
    /// Generate Thai TTS voiceover from text segments
    /// สร้างเสียงบรรยายภาษาไทยจาก text segments
    /// </summary>
    [HttpPost("generate-tts")]
    public async Task<ActionResult> GenerateTts([FromBody] TtsRequest request, CancellationToken ct)
    {
        if (request.Segments == null || request.Segments.Count == 0)
        {
            return BadRequest(new { error = "No text segments provided" });
        }

        var segments = request.Segments.Select(s => new TtsSegment
        {
            Text = s.Text,
            SceneNumber = s.SceneNumber,
        }).ToList();

        var result = await _ttsService.GenerateVoiceoverAsync(
            segments,
            request.VoiceId ?? ThaiTtsService.VoiceFemaleThai,
            request.Provider ?? "edge",
            request.OutputDir ?? "",
            ct
        );

        if (!result.Success)
        {
            return StatusCode(500, new { error = result.Error });
        }

        return Ok(new
        {
            audio_path = result.AudioPath,
            duration_seconds = result.DurationSeconds,
            segments = result.Segments.Select(s => new
            {
                scene = s.SceneNumber,
                start = s.StartSeconds,
                end = s.StartSeconds + s.DurationSeconds,
                path = s.Path,
            }),
            provider = result.Provider,
        });
    }

    /// <summary>
    /// Generate lip-synced video from face image and audio
    /// สร้างวิดีโอ lip sync จากภาพหน้าและเสียง
    /// </summary>
    [HttpPost("generate-lipsync")]
    public async Task<ActionResult> GenerateLipSync([FromBody] LipSyncRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.FaceImagePath))
        {
            return BadRequest(new { error = "face_image_path is required" });
        }

        if (string.IsNullOrEmpty(request.AudioPath))
        {
            return BadRequest(new { error = "audio_path is required" });
        }

        var result = await _lipSyncService.GenerateLipSyncAsync(
            request.FaceImagePath,
            request.AudioPath,
            request.Provider ?? "sadtalker",
            request.OutputDir ?? "",
            ct
        );

        if (!result.Success)
        {
            return StatusCode(500, new { error = result.Error });
        }

        return Ok(new
        {
            video_path = result.VideoPath,
            duration_seconds = result.DurationSeconds,
            provider = result.Provider,
        });
    }

    /// <summary>
    /// Assemble final video from clips + audio + music via FFmpeg
    /// ประกอบวิดีโอสุดท้ายจาก clips + เสียง + เพลง ด้วย FFmpeg
    /// </summary>
    [HttpPost("assemble-video")]
    public async Task<ActionResult> AssembleVideo([FromBody] AssembleVideoRequest request, CancellationToken ct)
    {
        if ((request.VideoClips == null || request.VideoClips.Count == 0) &&
            string.IsNullOrEmpty(request.LipSyncVideoPath))
        {
            return BadRequest(new { error = "No video clips or lip sync video provided" });
        }

        var outputDir = request.OutputDir ?? Path.Combine(Path.GetTempPath(), "assembly_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDir);

        try
        {
            string? concatenatedVideo = null;

            // Step 1: Use lip sync video if available, otherwise concatenate clips
            if (!string.IsNullOrEmpty(request.LipSyncVideoPath) && System.IO.File.Exists(request.LipSyncVideoPath))
            {
                concatenatedVideo = request.LipSyncVideoPath;
            }
            else if (request.VideoClips != null && request.VideoClips.Count > 0)
            {
                concatenatedVideo = Path.Combine(outputDir, "concatenated.mp4");
                await ConcatenateVideosAsync(request.VideoClips, concatenatedVideo, ct);
            }

            if (concatenatedVideo == null || !System.IO.File.Exists(concatenatedVideo))
            {
                return StatusCode(500, new { error = "Failed to prepare video" });
            }

            // Step 2: Mix in voiceover audio
            string videoWithAudio = concatenatedVideo;
            if (!string.IsNullOrEmpty(request.VoiceoverPath) && System.IO.File.Exists(request.VoiceoverPath))
            {
                videoWithAudio = Path.Combine(outputDir, "with_voiceover.mp4");
                await MixAudioToVideoAsync(concatenatedVideo, request.VoiceoverPath, videoWithAudio, 1.0, ct);
            }

            // Step 3: Add background music (if enabled)
            string videoWithMusic = videoWithAudio;
            if (request.EnableMusic && !string.IsNullOrEmpty(request.MusicSource) && request.MusicSource != "none")
            {
                var musicPath = FindBackgroundMusicAsync(request.MusicStyle ?? "cinematic");
                if (musicPath != null)
                {
                    videoWithMusic = Path.Combine(outputDir, "with_music.mp4");
                    await MixAudioToVideoAsync(videoWithAudio, musicPath, videoWithMusic, request.MusicVolume, ct);
                }
            }

            // Step 4: Resize for target aspect ratio
            var finalVideo = Path.Combine(outputDir, $"{SanitizeFilename(request.Title ?? "video")}.mp4");
            await ResizeVideoAsync(videoWithMusic, finalVideo, request.AspectRatio ?? "9:16", ct);

            // Step 5: Generate thumbnail
            var thumbnailPath = Path.Combine(outputDir, "thumbnail.jpg");
            await GenerateThumbnailAsync(finalVideo, thumbnailPath, ct);

            // Get duration
            var duration = await GetDurationAsync(finalVideo, ct);

            return Ok(new
            {
                video_path = finalVideo,
                thumbnail_path = System.IO.File.Exists(thumbnailPath) ? thumbnailPath : null,
                duration_seconds = duration,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video assembly failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Create slideshow video from static image with Ken Burns effect
    /// </summary>
    [HttpPost("create-slideshow")]
    public async Task<ActionResult> CreateSlideshow([FromBody] SlideshowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.ImagePath) || !System.IO.File.Exists(request.ImagePath))
        {
            return BadRequest(new { error = "Image file not found" });
        }

        var outputDir = request.OutputDir ?? Path.Combine(Path.GetTempPath(), "slideshow_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"slide_{Path.GetFileNameWithoutExtension(request.ImagePath)}.mp4");
        var duration = request.DurationSeconds > 0 ? request.DurationSeconds : 5;
        var fps = 25;
        var totalFrames = duration * fps;

        // Ken Burns zoom effect
        var effect = request.Effect ?? "ken_burns";
        var vf = effect switch
        {
            "zoom_in" => $"zoompan=z='min(zoom+0.0015,1.5)':d={totalFrames}:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1080x1920:fps={fps}",
            "zoom_out" => $"zoompan=z='if(lte(zoom,1.0),1.5,max(1.001,zoom-0.0015))':d={totalFrames}:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1080x1920:fps={fps}",
            "pan_left" => $"zoompan=z='1.2':d={totalFrames}:x='iw*(1-on/{totalFrames})':y='ih/2-(ih/zoom/2)':s=1080x1920:fps={fps}",
            _ => $"zoompan=z='min(zoom+0.0015,1.5)':d={totalFrames}:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1080x1920:fps={fps}", // ken_burns default
        };

        var args = $"-y -loop 1 -i \"{request.ImagePath}\" -c:v libx264 -t {duration} -pix_fmt yuv420p -vf \"{vf}\" \"{outputPath}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 || !System.IO.File.Exists(outputPath))
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            return StatusCode(500, new { error = $"Slideshow creation failed: {(stderr.Length > 500 ? stderr[..500] : stderr)}" });
        }

        return Ok(new { video_path = outputPath });
    }

    /// <summary>
    /// Check availability of all pipeline services
    /// </summary>
    [HttpGet("check-services")]
    public async Task<ActionResult> CheckServices()
    {
        var edgeTts = await _ttsService.CheckEdgeTtsAvailableAsync();
        var sadTalker = _lipSyncService.CheckSadTalkerInstalled();
        var wav2Lip = _lipSyncService.CheckWav2LipInstalled();
        var ffmpeg = await CheckFfmpegAvailableAsync();

        return Ok(new
        {
            edge_tts = edgeTts,
            sadtalker = sadTalker,
            wav2lip = wav2Lip,
            ffmpeg = ffmpeg,
            google_tts = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_TTS_API_KEY")),
        });
    }

    /// <summary>
    /// Publish a video to a social media platform
    /// เผยแพร่วิดีโอไปยัง social media platform
    /// </summary>
    [HttpPost("publish-video")]
    public async Task<ActionResult> PublishVideo([FromBody] PublishVideoRequest request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.VideoPath))
        {
            return BadRequest(new { success = false, error = "video_path is required" });
        }

        if (!System.IO.File.Exists(request.VideoPath))
        {
            return BadRequest(new { success = false, error = $"Video file not found: {request.VideoPath}" });
        }

        if (string.IsNullOrEmpty(request.Platform))
        {
            return BadRequest(new { success = false, error = "platform is required" });
        }

        // Parse platform string to SocialPlatform enum
        if (!Enum.TryParse<SocialPlatform>(request.Platform, ignoreCase: true, out var platform))
        {
            return BadRequest(new { success = false, error = $"Unknown platform: {request.Platform}" });
        }

        var videoRequest = new VideoPostRequest
        {
            Platform = platform,
            VideoPath = request.VideoPath,
            ThumbnailPath = request.ThumbnailPath,
            Title = request.Title,
            Description = request.Description,
            Hashtags = request.Hashtags,
            AccountId = request.AccountId,
            Credentials = request.Credentials,
        };

        var result = await _postPublisherService.PublishVideoAsync(videoRequest, ct);

        if (!result.Success)
        {
            return StatusCode(500, new { success = false, error = result.Error });
        }

        return Ok(new
        {
            success = true,
            post_id = result.PostId,
            post_url = result.PostUrl,
        });
    }

    // ====================================================================
    // Private helpers
    // ====================================================================

    private async Task ConcatenateVideosAsync(List<string> videoPaths, string outputPath, CancellationToken ct)
    {
        var listFile = Path.Combine(Path.GetDirectoryName(outputPath)!, "video_list.txt");
        var listContent = string.Join("\n", videoPaths.Where(System.IO.File.Exists).Select(p => $"file '{p.Replace("\\", "/")}'"));
        await System.IO.File.WriteAllTextAsync(listFile, listContent, ct);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);

        try { System.IO.File.Delete(listFile); } catch { }
    }

    private async Task MixAudioToVideoAsync(
        string videoPath, string audioPath, string outputPath, double volume, CancellationToken ct)
    {
        var volumeFilter = volume < 1.0 ? $",volume={volume:F2}" : "";
        var args = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -filter_complex \"[0:a][1:a]amix=inputs=2:duration=first{volumeFilter}[a]\" -map 0:v -map \"[a]\" -c:v copy \"{outputPath}\"";

        // If source video has no audio, just add the audio
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        // If amix fails (no audio stream in video), just add audio directly
        if (process.ExitCode != 0)
        {
            var fallbackArgs = volume < 1.0
                ? $"-y -i \"{videoPath}\" -i \"{audioPath}\" -filter_complex \"[1:a]volume={volume:F2}[a]\" -map 0:v -map \"[a]\" -c:v copy -shortest \"{outputPath}\""
                : $"-y -i \"{videoPath}\" -i \"{audioPath}\" -map 0:v -map 1:a -c:v copy -shortest \"{outputPath}\"";

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = fallbackArgs,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);
        }
    }

    private async Task ResizeVideoAsync(string inputPath, string outputPath, string aspectRatio, CancellationToken ct)
    {
        var (width, height) = aspectRatio switch
        {
            "9:16" => (1080, 1920),
            "16:9" => (1920, 1080),
            "1:1" => (1080, 1080),
            _ => (1080, 1920),
        };

        var args = $"-y -i \"{inputPath}\" -vf \"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black\" -c:a copy \"{outputPath}\"";

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);

        // If resize failed, just copy the original
        if (process.ExitCode != 0 && inputPath != outputPath)
        {
            System.IO.File.Copy(inputPath, outputPath, true);
        }
    }

    private async Task GenerateThumbnailAsync(string videoPath, string outputPath, CancellationToken ct)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{videoPath}\" -ss 00:00:01 -vframes 1 -q:v 2 \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);
    }

    private async Task<double> GetDurationAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (double.TryParse(output.Trim(), out var duration))
                return duration;
        }
        catch { }
        return 0;
    }

    private async Task<bool> CheckFfmpegAvailableAsync()
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
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string FindBackgroundMusicAsync(string style)
    {
        // Check for royalty-free music directory
        var musicDir = Environment.GetEnvironmentVariable("ROYALTY_FREE_MUSIC_DIR");
        if (!string.IsNullOrEmpty(musicDir) && Directory.Exists(musicDir))
        {
            var musicFiles = Directory.GetFiles(musicDir, "*.mp3")
                .Concat(Directory.GetFiles(musicDir, "*.wav"))
                .ToArray();

            if (musicFiles.Length > 0)
            {
                // Random selection
                return musicFiles[Random.Shared.Next(musicFiles.Length)];
            }
        }
        return null!;
    }

    private static string SanitizeFilename(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Where(c => !invalid.Contains(c)).Take(50).ToArray());
    }
}

// --- Request Models ---

public class TtsRequest
{
    public List<TtsSegmentInput> Segments { get; set; } = new();
    public string? VoiceId { get; set; }
    public string? Provider { get; set; }
    public string? OutputDir { get; set; }
}

public class TtsSegmentInput
{
    public string Text { get; set; } = "";
    public int SceneNumber { get; set; }
}

public class LipSyncRequest
{
    public string FaceImagePath { get; set; } = "";
    public string AudioPath { get; set; } = "";
    public string? Provider { get; set; }
    public string? OutputDir { get; set; }
}

public class AssembleVideoRequest
{
    public List<string>? VideoClips { get; set; }
    public string? VoiceoverPath { get; set; }
    public string? LipSyncVideoPath { get; set; }
    public bool EnableMusic { get; set; }
    public string? MusicStyle { get; set; }
    public double MusicVolume { get; set; } = 0.3;
    public string? MusicSource { get; set; }
    public string? AspectRatio { get; set; }
    public string? OutputDir { get; set; }
    public string? Title { get; set; }
}

public class SlideshowRequest
{
    public string ImagePath { get; set; } = "";
    public int DurationSeconds { get; set; } = 5;
    public string? Effect { get; set; }
    public string? OutputDir { get; set; }
}

public class PublishVideoRequest
{
    public string Platform { get; set; } = "";
    public string VideoPath { get; set; } = "";
    public string? ThumbnailPath { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? AccountId { get; set; }
    public Dictionary<string, string>? Credentials { get; set; }
}
