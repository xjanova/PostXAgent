using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Thai Text-to-Speech service supporting Edge TTS and Google Cloud TTS
/// บริการสร้างเสียงพูดภาษาไทยรองรับ Edge TTS และ Google Cloud TTS
/// </summary>
public class ThaiTtsService
{
    private readonly ILogger<ThaiTtsService> _logger;
    private readonly FFmpegService _ffmpegService;

    // Thai voices for Edge TTS
    public const string VoiceFemaleThai = "th-TH-PremwadeeNeural";
    public const string VoiceMaleThai = "th-TH-NiwatNeural";

    public ThaiTtsService(ILogger<ThaiTtsService> logger, FFmpegService ffmpegService)
    {
        _logger = logger;
        _ffmpegService = ffmpegService;
    }

    /// <summary>
    /// Generate Thai voiceover for multiple text segments
    /// </summary>
    public async Task<TtsResult> GenerateVoiceoverAsync(
        List<TtsSegment> segments,
        string voiceId = VoiceFemaleThai,
        string provider = "edge",
        string outputDir = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Path.Combine(Path.GetTempPath(), "tts_" + Guid.NewGuid().ToString("N")[..8]);
        }

        Directory.CreateDirectory(outputDir);

        _logger.LogInformation("Generating Thai TTS for {Count} segments with {Provider}", segments.Count, provider);

        var segmentResults = new List<TtsSegmentResult>();
        var audioPaths = new List<string>();

        for (int i = 0; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var segment = segments[i];
            var outputPath = Path.Combine(outputDir, $"segment_{i:D3}.mp3");

            _logger.LogDebug("Generating segment {Index}/{Total}: {Text}",
                i + 1, segments.Count, segment.Text.Length > 50 ? segment.Text[..50] + "..." : segment.Text);

            bool success = provider switch
            {
                "google" => await GenerateWithGoogleTtsAsync(segment.Text, voiceId, outputPath, ct),
                _ => await GenerateWithEdgeTtsAsync(segment.Text, voiceId, outputPath, ct),
            };

            if (success && File.Exists(outputPath))
            {
                var duration = await GetAudioDurationAsync(outputPath, ct);
                segmentResults.Add(new TtsSegmentResult
                {
                    SceneNumber = segment.SceneNumber,
                    Path = outputPath,
                    DurationSeconds = duration,
                    StartSeconds = segmentResults.Sum(s => s.DurationSeconds),
                });
                audioPaths.Add(outputPath);
            }
            else
            {
                _logger.LogWarning("Failed to generate TTS for segment {Index}", i);
            }
        }

        if (audioPaths.Count == 0)
        {
            return new TtsResult { Success = false, Error = "No audio segments generated" };
        }

        // Merge all segments into one audio file
        var mergedPath = Path.Combine(outputDir, "voiceover_merged.mp3");
        await MergeAudioFilesAsync(audioPaths, mergedPath, ct);

        var totalDuration = segmentResults.Sum(s => s.DurationSeconds);

        _logger.LogInformation("TTS voiceover generated: {Duration}s, {Segments} segments",
            totalDuration, segmentResults.Count);

        return new TtsResult
        {
            Success = true,
            AudioPath = mergedPath,
            DurationSeconds = totalDuration,
            Segments = segmentResults,
            Provider = provider,
        };
    }

    /// <summary>
    /// Generate speech using Edge TTS (free, no API key needed)
    /// </summary>
    public async Task<bool> GenerateWithEdgeTtsAsync(
        string text, string voiceId, string outputPath, CancellationToken ct = default)
    {
        try
        {
            // Escape text for command line
            var escapedText = text.Replace("\"", "\\\"").Replace("\n", " ");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "edge-tts",
                    Arguments = $"--voice \"{voiceId}\" --text \"{escapedText}\" --write-media \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                }
            };

            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync(ct);
            var stderr = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("Edge TTS failed (exit {Code}): {Error}", process.ExitCode, stderr);

                // Fallback: try with python -m edge_tts
                return await GenerateWithEdgeTtsPythonAsync(text, voiceId, outputPath, ct);
            }

            return File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Edge TTS command failed: {Error}", ex.Message);
            return await GenerateWithEdgeTtsPythonAsync(text, voiceId, outputPath, ct);
        }
    }

    /// <summary>
    /// Fallback: run edge-tts via python module
    /// </summary>
    private async Task<bool> GenerateWithEdgeTtsPythonAsync(
        string text, string voiceId, string outputPath, CancellationToken ct)
    {
        try
        {
            var escapedText = text.Replace("\"", "\\\"").Replace("\n", " ");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m edge_tts --voice \"{voiceId}\" --text \"{escapedText}\" --write-media \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);

            return process.ExitCode == 0 && File.Exists(outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Python edge-tts fallback also failed");
            return false;
        }
    }

    /// <summary>
    /// Generate speech using Google Cloud TTS REST API
    /// </summary>
    public async Task<bool> GenerateWithGoogleTtsAsync(
        string text, string voiceId, string outputPath, CancellationToken ct = default)
    {
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_TTS_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("GOOGLE_TTS_API_KEY not set, falling back to Edge TTS");
            return await GenerateWithEdgeTtsAsync(text, voiceId, outputPath, ct);
        }

        try
        {
            using var httpClient = new HttpClient();
            var url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";

            var requestBody = new
            {
                input = new { text },
                voice = new
                {
                    languageCode = "th-TH",
                    name = voiceId.StartsWith("th-") ? voiceId : "th-TH-Standard-A",
                },
                audioConfig = new
                {
                    audioEncoding = "MP3",
                    speakingRate = 1.0,
                    pitch = 0.0,
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(url, content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Google TTS API error: {Error}", error);
                return await GenerateWithEdgeTtsAsync(text, voiceId, outputPath, ct);
            }

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(responseJson);
            var audioContent = doc.RootElement.GetProperty("audioContent").GetString();

            if (string.IsNullOrEmpty(audioContent))
            {
                return false;
            }

            var audioBytes = Convert.FromBase64String(audioContent);
            await File.WriteAllBytesAsync(outputPath, audioBytes, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Google TTS failed, falling back to Edge TTS");
            return await GenerateWithEdgeTtsAsync(text, voiceId, outputPath, ct);
        }
    }

    /// <summary>
    /// Check if Edge TTS is available
    /// </summary>
    public async Task<bool> CheckEdgeTtsAvailableAsync()
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "edge-tts",
                    Arguments = "--list-voices",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // --- Private helpers ---

    private async Task<double> GetAudioDurationAsync(string audioPath, CancellationToken ct)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffprobe",
                    Arguments = $"-v quiet -show_entries format=duration -of csv=p=0 \"{audioPath}\"",
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
            _logger.LogWarning("Failed to get audio duration: {Error}", ex.Message);
        }

        return 0;
    }

    private async Task MergeAudioFilesAsync(List<string> audioPaths, string outputPath, CancellationToken ct)
    {
        if (audioPaths.Count == 1)
        {
            File.Copy(audioPaths[0], outputPath, true);
            return;
        }

        // Create a file list for FFmpeg concat
        var listFile = Path.Combine(Path.GetDirectoryName(outputPath)!, "concat_list.txt");
        var listContent = string.Join("\n", audioPaths.Select(p => $"file '{p.Replace("\\", "/")}'"));
        await File.WriteAllTextAsync(listFile, listContent, ct);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }
        };

        process.Start();
        await process.WaitForExitAsync(ct);

        // Clean up list file
        try { File.Delete(listFile); } catch { }

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("FFmpeg concat failed, trying re-encode merge");
            // Fallback: re-encode and merge
            var inputs = string.Join(" ", audioPaths.Select((p, i) => $"-i \"{p}\""));
            var filterComplex = string.Join("", Enumerable.Range(0, audioPaths.Count).Select(i => $"[{i}:a]"));
            filterComplex += $"concat=n={audioPaths.Count}:v=0:a=1[out]";

            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-y {inputs} -filter_complex \"{filterComplex}\" -map \"[out]\" \"{outputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync(ct);
        }
    }
}

// --- Models ---

public class TtsSegment
{
    public string Text { get; set; } = "";
    public int SceneNumber { get; set; }
}

public class TtsSegmentResult
{
    public int SceneNumber { get; set; }
    public string Path { get; set; } = "";
    public double DurationSeconds { get; set; }
    public double StartSeconds { get; set; }
}

public class TtsResult
{
    public bool Success { get; set; }
    public string? AudioPath { get; set; }
    public double DurationSeconds { get; set; }
    public List<TtsSegmentResult> Segments { get; set; } = new();
    public string Provider { get; set; } = "edge";
    public string? Error { get; set; }
}
