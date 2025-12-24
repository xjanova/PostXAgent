using System.Diagnostics;
using System.Text;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Core service for video/audio processing using FFmpeg
/// บริการหลักสำหรับประมวลผลวีดีโอ/เสียงด้วย FFmpeg
/// </summary>
public class FFmpegService
{
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;
    private readonly ILogger<FFmpegService> _logger;

    public FFmpegService(ILogger<FFmpegService> logger, string? ffmpegPath = null, string? ffprobePath = null)
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath ?? "ffmpeg"; // Will use system PATH if not specified
        _ffprobePath = ffprobePath ?? "ffprobe";
    }

    /// <summary>
    /// Get video metadata using FFprobe
    /// ดึงข้อมูลวีดีโอด้วย FFprobe
    /// </summary>
    public async Task<VideoMetadata?> GetVideoMetadataAsync(string videoPath, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Getting metadata for video: {VideoPath}", videoPath);

            var args = $"-v quiet -print_format json -show_format -show_streams \"{videoPath}\"";
            var output = await RunFFprobeAsync(args, ct);

            if (string.IsNullOrEmpty(output))
            {
                _logger.LogWarning("No metadata output from FFprobe");
                return null;
            }

            // Parse JSON output from FFprobe
            var json = System.Text.Json.JsonDocument.Parse(output);
            var format = json.RootElement.GetProperty("format");
            var streams = json.RootElement.GetProperty("streams");

            // Find video stream
            var videoStream = streams.EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");

            // Find audio stream
            var audioStream = streams.EnumerateArray()
                .FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "audio");

            var metadata = new VideoMetadata
            {
                Width = videoStream.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
                Height = videoStream.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
                Duration = format.TryGetProperty("duration", out var d)
                    ? double.Parse(d.GetString() ?? "0")
                    : 0,
                Fps = CalculateFps(videoStream),
                FileSize = format.TryGetProperty("size", out var s)
                    ? long.Parse(s.GetString() ?? "0")
                    : new FileInfo(videoPath).Length,
                Format = format.TryGetProperty("format_name", out var fn)
                    ? fn.GetString() ?? ""
                    : "",
                Codec = videoStream.TryGetProperty("codec_name", out var c)
                    ? c.GetString()
                    : null,
                Bitrate = format.TryGetProperty("bit_rate", out var br)
                    ? long.Parse(br.GetString() ?? "0")
                    : null,
                AspectRatio = CalculateAspectRatio(
                    videoStream.TryGetProperty("width", out var aw) ? aw.GetInt32() : 0,
                    videoStream.TryGetProperty("height", out var ah) ? ah.GetInt32() : 0
                ),
                HasAudio = audioStream.ValueKind != System.Text.Json.JsonValueKind.Undefined,
                AudioCodec = audioStream.TryGetProperty("codec_name", out var ac)
                    ? ac.GetString()
                    : null,
                AudioBitrate = audioStream.TryGetProperty("bit_rate", out var ab)
                    ? long.Parse(ab.GetString() ?? "0")
                    : null,
                CreatedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Video metadata: {Width}x{Height}, {Duration}s, {Format}",
                metadata.Width, metadata.Height, metadata.Duration, metadata.Format
            );

            return metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video metadata for: {VideoPath}", videoPath);
            return null;
        }
    }

    /// <summary>
    /// Mix video with audio
    /// ผสมวีดีโอกับเสียง
    /// </summary>
    public async Task<string?> MixVideoWithAudioAsync(
        string videoPath,
        string audioPath,
        string outputPath,
        double audioVolume = 1.0,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Mixing video {Video} with audio {Audio}", videoPath, audioPath);

            // FFmpeg command to mix video with audio
            // -i video.mp4 -i audio.mp3 -c:v copy -c:a aac -map 0:v:0 -map 1:a:0 -shortest output.mp4
            var args = new StringBuilder();
            args.Append($"-i \"{videoPath}\" ");
            args.Append($"-i \"{audioPath}\" ");
            args.Append("-c:v copy "); // Copy video stream without re-encoding
            args.Append("-c:a aac "); // Encode audio to AAC

            if (Math.Abs(audioVolume - 1.0) > 0.01)
            {
                args.Append($"-filter:a \"volume={audioVolume}\" ");
            }

            args.Append("-map 0:v:0 "); // Map video from first input
            args.Append("-map 1:a:0 "); // Map audio from second input
            args.Append("-shortest "); // Finish encoding when shortest stream ends
            args.Append($"\"{outputPath}\"");

            await RunFFmpegAsync(args.ToString(), ct);

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully mixed video and audio: {Output}", outputPath);
                return outputPath;
            }

            _logger.LogWarning("Output file not created: {Output}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mixing video with audio");
            return null;
        }
    }

    /// <summary>
    /// Concatenate multiple videos
    /// ต่อวีดีโอหลายๆ ไฟล์เข้าด้วยกัน
    /// </summary>
    public async Task<string?> ConcatenateVideosAsync(
        List<string> videoPaths,
        string outputPath,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Concatenating {Count} videos", videoPaths.Count);

            // Create temporary file list for FFmpeg concat demuxer
            var listFile = Path.GetTempFileName();
            var listContent = string.Join("\n", videoPaths.Select(p => $"file '{p}'"));
            await File.WriteAllTextAsync(listFile, listContent, ct);

            try
            {
                // FFmpeg concat demuxer: ffmpeg -f concat -safe 0 -i list.txt -c copy output.mp4
                var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";
                await RunFFmpegAsync(args, ct);

                if (File.Exists(outputPath))
                {
                    _logger.LogInformation("Successfully concatenated videos: {Output}", outputPath);
                    return outputPath;
                }

                _logger.LogWarning("Output file not created: {Output}", outputPath);
                return null;
            }
            finally
            {
                // Clean up temporary file
                if (File.Exists(listFile))
                {
                    File.Delete(listFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error concatenating videos");
            return null;
        }
    }

    /// <summary>
    /// Extract audio from video
    /// ดึงเสียงออกจากวีดีโอ
    /// </summary>
    public async Task<string?> ExtractAudioAsync(
        string videoPath,
        string outputPath,
        string audioCodec = "mp3",
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Extracting audio from video: {Video}", videoPath);

            // FFmpeg command: ffmpeg -i video.mp4 -vn -acodec mp3 audio.mp3
            var args = $"-i \"{videoPath}\" -vn -acodec {audioCodec} \"{outputPath}\"";
            await RunFFmpegAsync(args, ct);

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully extracted audio: {Output}", outputPath);
                return outputPath;
            }

            _logger.LogWarning("Output file not created: {Output}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting audio from video");
            return null;
        }
    }

    /// <summary>
    /// Generate thumbnail from video
    /// สร้าง thumbnail จากวีดีโอ
    /// </summary>
    public async Task<string?> GenerateThumbnailAsync(
        string videoPath,
        string outputPath,
        double timeOffset = 1.0,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Generating thumbnail at {Time}s for: {Video}", timeOffset, videoPath);

            // FFmpeg command: ffmpeg -i video.mp4 -ss 00:00:01 -vframes 1 thumbnail.jpg
            var args = $"-i \"{videoPath}\" -ss {timeOffset} -vframes 1 \"{outputPath}\"";
            await RunFFmpegAsync(args, ct);

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully generated thumbnail: {Output}", outputPath);
                return outputPath;
            }

            _logger.LogWarning("Output file not created: {Output}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating thumbnail");
            return null;
        }
    }

    /// <summary>
    /// Convert video format
    /// แปลงรูปแบบวีดีโอ
    /// </summary>
    public async Task<string?> ConvertVideoFormatAsync(
        string inputPath,
        string outputPath,
        string outputFormat,
        string videoCodec = "libx264",
        string audioCodec = "aac",
        int crf = 23,
        string preset = "medium",
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Converting video to {Format}: {Input}", outputFormat, inputPath);

            // FFmpeg command: ffmpeg -i input.mp4 -c:v libx264 -crf 23 -preset medium -c:a aac output.mp4
            var args = new StringBuilder();
            args.Append($"-i \"{inputPath}\" ");
            args.Append($"-c:v {videoCodec} ");
            args.Append($"-crf {crf} ");
            args.Append($"-preset {preset} ");
            args.Append($"-c:a {audioCodec} ");
            args.Append($"\"{outputPath}\"");

            await RunFFmpegAsync(args.ToString(), ct);

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully converted video: {Output}", outputPath);
                return outputPath;
            }

            _logger.LogWarning("Output file not created: {Output}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting video format");
            return null;
        }
    }

    /// <summary>
    /// Resize video
    /// ปรับขนาดวีดีโอ
    /// </summary>
    public async Task<string?> ResizeVideoAsync(
        string inputPath,
        string outputPath,
        int width,
        int height,
        string videoCodec = "libx264",
        int crf = 23,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Resizing video to {Width}x{Height}: {Input}", width, height, inputPath);

            // FFmpeg command: ffmpeg -i input.mp4 -vf scale=1280:720 -c:v libx264 -crf 23 output.mp4
            var args = new StringBuilder();
            args.Append($"-i \"{inputPath}\" ");
            args.Append($"-vf scale={width}:{height} ");
            args.Append($"-c:v {videoCodec} ");
            args.Append($"-crf {crf} ");
            args.Append($"-c:a copy "); // Copy audio without re-encoding
            args.Append($"\"{outputPath}\"");

            await RunFFmpegAsync(args.ToString(), ct);

            if (File.Exists(outputPath))
            {
                _logger.LogInformation("Successfully resized video: {Output}", outputPath);
                return outputPath;
            }

            _logger.LogWarning("Output file not created: {Output}", outputPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resizing video");
            return null;
        }
    }

    /// <summary>
    /// Run FFmpeg command
    /// รันคำสั่ง FFmpeg
    /// </summary>
    private async Task<string> RunFFmpegAsync(string arguments, CancellationToken ct = default)
    {
        return await RunProcessAsync(_ffmpegPath, arguments, ct);
    }

    /// <summary>
    /// Run FFprobe command
    /// รันคำสั่ง FFprobe
    /// </summary>
    private async Task<string> RunFFprobeAsync(string arguments, CancellationToken ct = default)
    {
        return await RunProcessAsync(_ffprobePath, arguments, ct);
    }

    /// <summary>
    /// Run external process (FFmpeg or FFprobe)
    /// รัน process ภายนอก
    /// </summary>
    private async Task<string> RunProcessAsync(string fileName, string arguments, CancellationToken ct = default)
    {
        var processInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var error = errorBuilder.ToString();
            _logger.LogError("Process failed with exit code {ExitCode}: {Error}", process.ExitCode, error);
            throw new Exception($"Process failed: {error}");
        }

        return outputBuilder.ToString();
    }

    /// <summary>
    /// Calculate FPS from video stream data
    /// คำนวณ FPS จากข้อมูล video stream
    /// </summary>
    private int CalculateFps(System.Text.Json.JsonElement videoStream)
    {
        try
        {
            if (videoStream.TryGetProperty("r_frame_rate", out var rFrameRate))
            {
                var rate = rFrameRate.GetString();
                if (!string.IsNullOrEmpty(rate) && rate.Contains('/'))
                {
                    var parts = rate.Split('/');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out var num) &&
                        int.TryParse(parts[1], out var den) &&
                        den != 0)
                    {
                        return (int)Math.Round((double)num / den);
                    }
                }
            }
            return 30; // Default FPS
        }
        catch
        {
            return 30;
        }
    }

    /// <summary>
    /// Calculate aspect ratio string from width and height
    /// คำนวณ aspect ratio จากความกว้างและความสูง
    /// </summary>
    private string CalculateAspectRatio(int width, int height)
    {
        if (width == 0 || height == 0) return "unknown";

        // Find GCD to simplify ratio
        var gcd = GCD(width, height);
        var ratioW = width / gcd;
        var ratioH = height / gcd;

        return $"{ratioW}:{ratioH}";
    }

    /// <summary>
    /// Calculate Greatest Common Divisor
    /// คำนวณตัวหารร่วมมาก
    /// </summary>
    private int GCD(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}
