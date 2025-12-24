using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// High-level audio processing utilities
/// ยูทิลิตี้ระดับสูงสำหรับประมวลผลเสียง
/// </summary>
public class AudioProcessor
{
    private readonly FFmpegService _ffmpegService;
    private readonly ILogger<AudioProcessor> _logger;
    private readonly string _workingDirectory;

    public AudioProcessor(
        FFmpegService ffmpegService,
        ILogger<AudioProcessor> logger,
        string? workingDirectory = null)
    {
        _ffmpegService = ffmpegService;
        _logger = logger;
        _workingDirectory = workingDirectory ?? Path.Combine(Path.GetTempPath(), "PostXAgent", "Audio");

        // Ensure working directory exists
        if (!Directory.Exists(_workingDirectory))
        {
            Directory.CreateDirectory(_workingDirectory);
        }
    }

    /// <summary>
    /// Extract audio from video and save as MP3
    /// ดึงเสียงออกจากวีดีโอและบันทึกเป็น MP3
    /// </summary>
    public async Task<MediaGenerationResult?> ExtractAudioFromVideoAsync(
        string videoPath,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Extracting audio from video: {VideoPath}", videoPath);

            outputPath ??= GenerateOutputPath("extracted_audio", "mp3");

            var audioPath = await _ffmpegService.ExtractAudioAsync(
                videoPath,
                outputPath,
                "mp3",
                ct
            );

            if (audioPath == null)
            {
                _logger.LogError("Failed to extract audio");
                return null;
            }

            var result = new MediaGenerationResult
            {
                AudioPath = audioPath
            };

            // Get audio metadata if possible
            // Note: FFmpeg's GetVideoMetadataAsync can also read audio files
            var metadata = await _ffmpegService.GetVideoMetadataAsync(audioPath, ct);
            if (metadata != null)
            {
                result.Metadata = metadata;
            }

            _logger.LogInformation("Audio extraction completed: {AudioPath}", audioPath);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting audio from video");
            return null;
        }
    }

    /// <summary>
    /// Convert audio format
    /// แปลงรูปแบบไฟล์เสียง
    /// </summary>
    public async Task<string?> ConvertAudioFormatAsync(
        string inputPath,
        string outputFormat,
        string? outputPath = null,
        int? bitrate = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Converting audio to {Format}: {Input}", outputFormat, inputPath);

            outputPath ??= GenerateOutputPath($"converted_{outputFormat}", outputFormat);

            // Build FFmpeg arguments for audio conversion
            var args = $"-i \"{inputPath}\" ";

            if (bitrate.HasValue)
            {
                args += $"-b:a {bitrate}k ";
            }

            args += $"\"{outputPath}\"";

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(ct);

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    _logger.LogInformation("Audio conversion completed: {Output}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Audio conversion failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting audio format");
            return null;
        }
    }

    /// <summary>
    /// Trim audio to specific duration
    /// ตัดเสียงให้มีความยาวที่กำหนด
    /// </summary>
    public async Task<string?> TrimAudioAsync(
        string inputPath,
        double startTime,
        double duration,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Trimming audio from {Start}s for {Duration}s", startTime, duration);

            outputPath ??= GenerateOutputPath("trimmed_audio", "mp3");

            // FFmpeg command: ffmpeg -i input.mp3 -ss 00:00:10 -t 30 output.mp3
            var args = $"-i \"{inputPath}\" -ss {startTime} -t {duration} -c copy \"{outputPath}\"";

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

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    _logger.LogInformation("Audio trimming completed: {Output}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Audio trimming failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error trimming audio");
            return null;
        }
    }

    /// <summary>
    /// Adjust audio volume
    /// ปรับระดับเสียง
    /// </summary>
    public async Task<string?> AdjustVolumeAsync(
        string inputPath,
        double volumeMultiplier,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Adjusting audio volume by {Multiplier}x", volumeMultiplier);

            outputPath ??= GenerateOutputPath("volume_adjusted", "mp3");

            // FFmpeg command: ffmpeg -i input.mp3 -filter:a "volume=1.5" output.mp3
            var args = $"-i \"{inputPath}\" -filter:a \"volume={volumeMultiplier}\" \"{outputPath}\"";

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

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    _logger.LogInformation("Volume adjustment completed: {Output}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Volume adjustment failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adjusting audio volume");
            return null;
        }
    }

    /// <summary>
    /// Concatenate multiple audio files
    /// ต่อไฟล์เสียงหลายไฟล์เข้าด้วยกัน
    /// </summary>
    public async Task<string?> ConcatenateAudioAsync(
        List<string> audioPaths,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Concatenating {Count} audio files", audioPaths.Count);

            outputPath ??= GenerateOutputPath("concatenated_audio", "mp3");

            // Create temporary file list for FFmpeg concat
            var listFile = Path.GetTempFileName();
            var listContent = string.Join("\n", audioPaths.Select(p => $"file '{p}'"));
            await File.WriteAllTextAsync(listFile, listContent, ct);

            try
            {
                // FFmpeg concat: ffmpeg -f concat -safe 0 -i list.txt -c copy output.mp3
                var args = $"-f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";

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

                    if (process.ExitCode == 0 && File.Exists(outputPath))
                    {
                        _logger.LogInformation("Audio concatenation completed: {Output}", outputPath);
                        return outputPath;
                    }
                }

                _logger.LogWarning("Audio concatenation failed");
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
            _logger.LogError(ex, "Error concatenating audio files");
            return null;
        }
    }

    /// <summary>
    /// Mix multiple audio tracks
    /// ผสมเสียงหลายแทร็คเข้าด้วยกัน
    /// </summary>
    public async Task<string?> MixAudioTracksAsync(
        List<string> audioPaths,
        List<double>? volumes = null,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Mixing {Count} audio tracks", audioPaths.Count);

            outputPath ??= GenerateOutputPath("mixed_audio", "mp3");

            // Build FFmpeg command for mixing multiple audio streams
            var inputArgs = string.Join(" ", audioPaths.Select(p => $"-i \"{p}\""));

            // Build filter complex for mixing
            var filterInputs = string.Join("", Enumerable.Range(0, audioPaths.Count).Select(i => $"[{i}:a]"));

            string filterComplex;
            if (volumes != null && volumes.Count == audioPaths.Count)
            {
                // Apply volume adjustments
                var volumeFilters = volumes.Select((v, i) => $"[{i}:a]volume={v}[a{i}]").ToList();
                var volumeInputs = string.Join("", Enumerable.Range(0, audioPaths.Count).Select(i => $"[a{i}]"));
                filterComplex = $"{string.Join(";", volumeFilters)};{volumeInputs}amix=inputs={audioPaths.Count}:duration=longest[out]";
            }
            else
            {
                // No volume adjustment
                filterComplex = $"{filterInputs}amix=inputs={audioPaths.Count}:duration=longest[out]";
            }

            var args = $"{inputArgs} -filter_complex \"{filterComplex}\" -map \"[out]\" \"{outputPath}\"";

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

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    _logger.LogInformation("Audio mixing completed: {Output}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Audio mixing failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error mixing audio tracks");
            return null;
        }
    }

    /// <summary>
    /// Normalize audio volume
    /// ปรับระดับเสียงให้เป็นมาตรฐาน
    /// </summary>
    public async Task<string?> NormalizeAudioAsync(
        string inputPath,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Normalizing audio: {Input}", inputPath);

            outputPath ??= GenerateOutputPath("normalized_audio", "mp3");

            // FFmpeg loudnorm filter for audio normalization
            var args = $"-i \"{inputPath}\" -filter:a loudnorm \"{outputPath}\"";

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

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    _logger.LogInformation("Audio normalization completed: {Output}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Audio normalization failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error normalizing audio");
            return null;
        }
    }

    /// <summary>
    /// Add fade in/out effects to audio
    /// เพิ่มเอฟเฟกต์ fade in/out ให้กับเสียง
    /// </summary>
    public async Task<string?> AddFadeEffectsAsync(
        string inputPath,
        double fadeInDuration = 0,
        double fadeOutDuration = 0,
        string? outputPath = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Adding fade effects: in={In}s, out={Out}s", fadeInDuration, fadeOutDuration);

            outputPath ??= GenerateOutputPath("faded_audio", "mp3");

            // Get audio duration first
            var metadata = await _ffmpegService.GetVideoMetadataAsync(inputPath, ct);
            if (metadata == null)
            {
                _logger.LogError("Failed to get audio metadata");
                return null;
            }

            var filters = new List<string>();

            if (fadeInDuration > 0)
            {
                filters.Add($"afade=t=in:st=0:d={fadeInDuration}");
            }

            if (fadeOutDuration > 0)
            {
                var fadeOutStart = metadata.Duration - fadeOutDuration;
                filters.Add($"afade=t=out:st={fadeOutStart}:d={fadeOutDuration}");
            }

            if (filters.Count == 0)
            {
                _logger.LogWarning("No fade effects specified");
                return inputPath;
            }

            var filterString = string.Join(",", filters);
            var args = $"-i \"{inputPath}\" -filter:a \"{filterString}\" \"{outputPath}\"";

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

                if (process.ExitCode == 0 && File.Exists(outputPath))
                {
                    _logger.LogInformation("Fade effects added: {Output}", outputPath);
                    return outputPath;
                }
            }

            _logger.LogWarning("Adding fade effects failed");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding fade effects to audio");
            return null;
        }
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
}
