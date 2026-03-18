using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Lip sync service wrapping SadTalker and Wav2Lip
/// บริการ lip sync ผ่าน SadTalker และ Wav2Lip
/// </summary>
public class LipSyncService
{
    private readonly ILogger<LipSyncService> _logger;
    private string? _sadTalkerPath;
    private string? _wav2LipPath;

    public LipSyncService(ILogger<LipSyncService> logger)
    {
        _logger = logger;
        _sadTalkerPath = Environment.GetEnvironmentVariable("SADTALKER_PATH");
        _wav2LipPath = Environment.GetEnvironmentVariable("WAV2LIP_PATH");
    }

    /// <summary>
    /// Generate lip-synced video from face image and audio
    /// </summary>
    public async Task<LipSyncResult> GenerateLipSyncAsync(
        string faceImagePath,
        string audioPath,
        string provider = "sadtalker",
        string outputDir = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Path.Combine(Path.GetTempPath(), "lipsync_" + Guid.NewGuid().ToString("N")[..8]);
        }

        Directory.CreateDirectory(outputDir);

        _logger.LogInformation("Generating lip sync with {Provider}: face={Face}, audio={Audio}",
            provider, Path.GetFileName(faceImagePath), Path.GetFileName(audioPath));

        // Validate input files
        if (!File.Exists(faceImagePath))
        {
            return new LipSyncResult { Success = false, Error = $"Face image not found: {faceImagePath}" };
        }

        if (!File.Exists(audioPath))
        {
            return new LipSyncResult { Success = false, Error = $"Audio file not found: {audioPath}" };
        }

        // Try primary provider first, then fallback chain
        LipSyncResult result;

        if (provider == "wav2lip")
        {
            result = await GenerateWithWav2LipAsync(faceImagePath, audioPath, outputDir, ct);
            if (result.Success)
                return ValidateLipSyncOutput(result);

            _logger.LogWarning("Wav2Lip failed, trying SadTalker fallback");
            result = await GenerateWithSadTalkerAsync(faceImagePath, audioPath, outputDir, ct);
            if (result.Success)
                return ValidateLipSyncOutput(result);
        }
        else
        {
            result = await GenerateWithSadTalkerAsync(faceImagePath, audioPath, outputDir, ct);
            if (result.Success)
                return ValidateLipSyncOutput(result);

            _logger.LogWarning("SadTalker failed, trying Wav2Lip fallback");
            result = await GenerateWithWav2LipAsync(faceImagePath, audioPath, outputDir, ct);
            if (result.Success)
                return ValidateLipSyncOutput(result);
        }

        // Final fallback: static image + audio video (no lip movement, but pipeline doesn't crash)
        _logger.LogWarning("Both SadTalker and Wav2Lip failed, generating fallback static video");
        result = await GenerateFallbackVideoAsync(faceImagePath, audioPath, outputDir, ct);
        return ValidateLipSyncOutput(result);
    }

    /// <summary>
    /// Generate lip sync using SadTalker
    /// Command: python inference.py --driven_audio audio.wav --source_image face.png --result_dir output/ --still --preprocess crop
    /// </summary>
    private async Task<LipSyncResult> GenerateWithSadTalkerAsync(
        string faceImagePath, string audioPath, string outputDir, CancellationToken ct)
    {
        var sadTalkerDir = _sadTalkerPath ?? FindToolPath("SadTalker");

        if (string.IsNullOrEmpty(sadTalkerDir) || !Directory.Exists(sadTalkerDir))
        {
            return new LipSyncResult
            {
                Success = false,
                Error = "SadTalker not found. Set SADTALKER_PATH environment variable or install SadTalker."
            };
        }

        var inferenceScript = Path.Combine(sadTalkerDir, "inference.py");
        if (!File.Exists(inferenceScript))
        {
            return new LipSyncResult
            {
                Success = false,
                Error = $"SadTalker inference.py not found at {inferenceScript}"
            };
        }

        var checkpointDir = Environment.GetEnvironmentVariable("SADTALKER_CHECKPOINT_DIR")
            ?? Path.Combine(sadTalkerDir, "checkpoints");

        try
        {
            var args = new StringBuilder();
            args.Append($"inference.py ");
            args.Append($"--driven_audio \"{audioPath}\" ");
            args.Append($"--source_image \"{faceImagePath}\" ");
            args.Append($"--result_dir \"{outputDir}\" ");
            args.Append($"--checkpoint_dir \"{checkpointDir}\" ");
            args.Append("--still "); // No head movement
            args.Append("--preprocess crop "); // Auto-crop face
            args.Append("--enhancer gfpgan "); // Face enhancement

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = args.ToString(),
                    WorkingDirectory = sadTalkerDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };

            _logger.LogDebug("Running SadTalker: python {Args}", args);

            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("SadTalker failed (exit {Code}): {Error}", process.ExitCode, stderr);
                return new LipSyncResult
                {
                    Success = false,
                    Error = $"SadTalker exit code {process.ExitCode}: {(stderr.Length > 500 ? stderr[..500] : stderr)}"
                };
            }

            // Find the output video (SadTalker saves as *.mp4 in result_dir)
            var outputFiles = Directory.GetFiles(outputDir, "*.mp4", SearchOption.AllDirectories);
            var outputVideo = outputFiles.OrderByDescending(f => new FileInfo(f).CreationTime).FirstOrDefault();

            if (outputVideo == null)
            {
                return new LipSyncResult { Success = false, Error = "SadTalker produced no output video" };
            }

            var duration = await GetVideoDurationAsync(outputVideo, ct);

            _logger.LogInformation("SadTalker lip sync completed: {Path}, duration={Duration}s",
                outputVideo, duration);

            return new LipSyncResult
            {
                Success = true,
                VideoPath = outputVideo,
                DurationSeconds = duration,
                Provider = "sadtalker",
            };
        }
        catch (OperationCanceledException)
        {
            return new LipSyncResult { Success = false, Error = "Operation cancelled" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SadTalker lip sync failed");
            return new LipSyncResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Generate lip sync using Wav2Lip (faster alternative)
    /// </summary>
    private async Task<LipSyncResult> GenerateWithWav2LipAsync(
        string faceImagePath, string audioPath, string outputDir, CancellationToken ct)
    {
        var wav2LipDir = _wav2LipPath ?? FindToolPath("Wav2Lip");

        if (string.IsNullOrEmpty(wav2LipDir) || !Directory.Exists(wav2LipDir))
        {
            return new LipSyncResult
            {
                Success = false,
                Error = "Wav2Lip not found. Set WAV2LIP_PATH environment variable or install Wav2Lip."
            };
        }

        var outputVideo = Path.Combine(outputDir, "wav2lip_output.mp4");

        try
        {
            var args = new StringBuilder();
            args.Append("inference.py ");
            args.Append($"--face \"{faceImagePath}\" ");
            args.Append($"--audio \"{audioPath}\" ");
            args.Append($"--outfile \"{outputVideo}\" ");
            args.Append("--pads 0 10 0 0 ");
            args.Append("--resize_factor 1 ");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = args.ToString(),
                    WorkingDirectory = wav2LipDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(outputVideo))
            {
                return new LipSyncResult
                {
                    Success = false,
                    Error = $"Wav2Lip exit code {process.ExitCode}"
                };
            }

            var duration = await GetVideoDurationAsync(outputVideo, ct);

            return new LipSyncResult
            {
                Success = true,
                VideoPath = outputVideo,
                DurationSeconds = duration,
                Provider = "wav2lip",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wav2Lip failed");
            return new LipSyncResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Check if SadTalker is installed and available
    /// </summary>
    public bool CheckSadTalkerInstalled()
    {
        var path = _sadTalkerPath ?? FindToolPath("SadTalker");
        if (string.IsNullOrEmpty(path)) return false;
        return File.Exists(Path.Combine(path, "inference.py"));
    }

    /// <summary>
    /// Check if Wav2Lip is installed and available
    /// </summary>
    public bool CheckWav2LipInstalled()
    {
        var path = _wav2LipPath ?? FindToolPath("Wav2Lip");
        if (string.IsNullOrEmpty(path)) return false;
        return File.Exists(Path.Combine(path, "inference.py"));
    }

    /// <summary>
    /// Fallback: create a static video from face image + audio overlay (no lip movement)
    /// This ensures the pipeline doesn't crash when both SadTalker and Wav2Lip are unavailable
    /// </summary>
    private async Task<LipSyncResult> GenerateFallbackVideoAsync(
        string faceImagePath, string audioPath, string outputDir, CancellationToken ct)
    {
        var outputPath = Path.Combine(outputDir, $"lipsync_fallback_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        try
        {
            // Use FFmpeg to create a static video from image + audio
            // -loop 1: loop the image, -shortest: stop when audio ends
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y -loop 1 -i \"{faceImagePath}\" -i \"{audioPath}\" " +
                                $"-c:v libx264 -c:a aac -b:a 192k " +
                                $"-pix_fmt yuv420p -shortest \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            _logger.LogDebug("Running FFmpeg fallback: static image + audio");

            process.Start();

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || !File.Exists(outputPath))
            {
                _logger.LogWarning("FFmpeg fallback failed (exit {Code}): {Error}",
                    process.ExitCode, stderr.Length > 500 ? stderr[..500] : stderr);
                return new LipSyncResult
                {
                    Success = false,
                    Error = $"FFmpeg fallback failed: exit code {process.ExitCode}"
                };
            }

            var duration = await GetVideoDurationAsync(outputPath, ct);

            _logger.LogInformation("Fallback static video created: {Path}, duration={Duration}s",
                outputPath, duration);

            return new LipSyncResult
            {
                Success = true,
                VideoPath = outputPath,
                DurationSeconds = duration,
                Provider = "fallback_static",
            };
        }
        catch (OperationCanceledException)
        {
            return new LipSyncResult { Success = false, Error = "Operation cancelled" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FFmpeg fallback video generation failed");
            return new LipSyncResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Validate lip sync output video exists and is not corrupt
    /// </summary>
    private LipSyncResult ValidateLipSyncOutput(LipSyncResult result)
    {
        if (!result.Success)
            return result;

        if (string.IsNullOrEmpty(result.VideoPath) || !File.Exists(result.VideoPath))
        {
            return new LipSyncResult { Success = false, Error = "LipSync output file not found" };
        }

        var videoInfo = new FileInfo(result.VideoPath);
        if (videoInfo.Length < 1000) // Less than 1KB is definitely corrupt
        {
            _logger.LogError("LipSync output file is too small ({Size} bytes): {Path}",
                videoInfo.Length, result.VideoPath);
            File.Delete(result.VideoPath);
            return new LipSyncResult { Success = false, Error = "LipSync output file is corrupt or empty" };
        }

        return result;
    }

    // --- Private helpers ---

    private static string? FindToolPath(string toolName)
    {
        // Check common installation paths
        var candidates = new[]
        {
            Path.Combine("C:", "Tools", toolName),
            Path.Combine("C:", "AI", toolName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), toolName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AI", toolName),
        };

        foreach (var path in candidates)
        {
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{videoPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (double.TryParse(output.Trim(), out var duration))
            {
                return duration;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get video duration: {Error}", ex.Message);
        }

        return 0;
    }
}

// --- Models ---

public class LipSyncResult
{
    public bool Success { get; set; }
    public string? VideoPath { get; set; }
    public double DurationSeconds { get; set; }
    public string Provider { get; set; } = "sadtalker";
    public string? Error { get; set; }
}
