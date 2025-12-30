using AIManager.Core.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Worker for Suno AI music generation
/// PRIMARY music generation provider
/// ตัวประมวลผลสำหรับการสร้างเพลงด้วย Suno AI
///
/// ใช้ Web Automation ผ่าน SunoAutomationService สำหรับ:
/// - สร้างเพลงจาก prompt/description
/// - สร้างเพลงจากเนื้อเพลง (lyrics)
/// - ดาวน์โหลดไฟล์เพลง
/// - ตรวจสอบ credits ที่เหลือ
/// </summary>
public class SunoAIWorker : BasePlatformWorker
{
    private readonly AudioProcessor _audioProcessor;
    private readonly SunoAutomationService? _automationService;
    private readonly ILogger<SunoAIWorker> _logger2;

    private const string SUNO_AI_URL = "https://suno.com";

    public override SocialPlatform Platform => SocialPlatform.SunoAI;

    /// <summary>
    /// Constructor with automation service
    /// </summary>
    public SunoAIWorker(
        ILogger<SunoAIWorker> logger,
        AudioProcessor audioProcessor,
        SunoAutomationService? automationService = null)
    {
        _logger2 = logger;
        _audioProcessor = audioProcessor;
        _automationService = automationService;
    }

    /// <summary>
    /// Generate music using Suno AI
    /// สร้างเพลงด้วย Suno AI
    /// </summary>
    public async Task<TaskResult> GenerateMusicAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var config = task.Payload.MusicConfig;
            if (config == null)
            {
                throw new ArgumentException("MusicConfig is required for music generation");
            }

            _logger2.LogInformation("Starting Suno AI music generation: {Prompt}", config.Prompt);
            _logger2.LogInformation("URL: {Url}", SUNO_AI_URL);

            // ใช้ Web Automation ถ้ามี
            if (_automationService != null)
            {
                var result = await _automationService.GenerateMusicAsync(
                    config.Prompt,
                    new SunoMusicOptions
                    {
                        UseCustomMode = config.Genre.HasValue || config.Mood.HasValue,
                        Style = BuildStyleString(config),
                        IsInstrumental = config.Instrumental,
                        DownloadAll = config.NumberOfOutputs > 1
                    },
                    ct);

                if (result.Success && result.GeneratedSongs.Count > 0)
                {
                    var outputs = result.GeneratedSongs.Select((song, i) => new MediaOutput
                    {
                        Index = i + 1,
                        Url = song.AudioUrl,
                        Path = song.LocalPath,
                        Metadata = new VideoMetadata
                        {
                            Duration = ParseDuration(song.Duration),
                            Format = "mp3",
                            HasAudio = true,
                            CreatedAt = DateTime.UtcNow
                        }
                    }).ToList();

                    return new TaskResult
                    {
                        TaskId = task.Id ?? "",
                        WorkerId = 0,
                        Platform = Platform.ToString(),
                        Success = true,
                        Data = new ResultData
                        {
                            MediaResult = new MediaGenerationResult
                            {
                                AudioUrl = outputs.First().Url,
                                AudioPath = outputs.First().Path,
                                Metadata = outputs.First().Metadata,
                                MultipleOutputs = outputs
                            },
                            Message = $"Generated {result.GeneratedSongs.Count} songs for prompt: '{config.Prompt}'"
                        },
                        ProcessingTimeMs = sw.ElapsedMilliseconds
                    };
                }
                else
                {
                    throw new Exception(result.Error ?? "Music generation failed");
                }
            }

            // Fallback: placeholder ถ้าไม่มี automation service
            return await GeneratePlaceholder(task, config, sw, ct);
        }
        catch (Exception ex)
        {
            _logger2.LogError(ex, "Suno AI music generation failed");

            return new TaskResult
            {
                TaskId = task.Id ?? "",
                WorkerId = 0,
                Platform = Platform.ToString(),
                Success = false,
                Error = ex.Message,
                ErrorType = ErrorType.PlatformError,
                ShouldRetry = true,
                RetryAfterSeconds = 60,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Generate music with custom lyrics
    /// สร้างเพลงจากเนื้อเพลง
    /// </summary>
    public async Task<TaskResult> GenerateMusicWithLyricsAsync(
        string lyrics,
        string style,
        string? title = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (_automationService == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Automation service not available",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            var result = await _automationService.GenerateMusicWithLyricsAsync(
                lyrics, style, title, ct);

            if (result.Success && result.GeneratedSongs.Count > 0)
            {
                var song = result.GeneratedSongs.First();
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        MediaResult = new MediaGenerationResult
                        {
                            AudioUrl = song.AudioUrl,
                            AudioPath = song.LocalPath,
                            Metadata = new VideoMetadata
                            {
                                Duration = ParseDuration(song.Duration),
                                Format = "mp3",
                                HasAudio = true
                            }
                        },
                        Message = $"Generated song: {song.Title}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = result.Error ?? "Failed to generate music",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger2.LogError(ex, "Failed to generate music with lyrics");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Generate instrumental music
    /// สร้างเพลงแบบ instrumental
    /// </summary>
    public async Task<TaskResult> GenerateInstrumentalAsync(
        string description,
        string style,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (_automationService == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Automation service not available",
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            var result = await _automationService.GenerateInstrumentalAsync(
                description, style, ct);

            if (result.Success && result.GeneratedSongs.Count > 0)
            {
                var song = result.GeneratedSongs.First();
                return new TaskResult
                {
                    Success = true,
                    Data = new ResultData
                    {
                        MediaResult = new MediaGenerationResult
                        {
                            AudioUrl = song.AudioUrl,
                            AudioPath = song.LocalPath,
                            Metadata = new VideoMetadata
                            {
                                Duration = ParseDuration(song.Duration),
                                Format = "mp3",
                                HasAudio = true
                            }
                        },
                        Message = $"Generated instrumental: {song.Title}"
                    },
                    ProcessingTimeMs = sw.ElapsedMilliseconds
                };
            }

            return new TaskResult
            {
                Success = false,
                Error = result.Error ?? "Failed to generate instrumental",
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger2.LogError(ex, "Failed to generate instrumental");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Check remaining credits
    /// ตรวจสอบจำนวน credits ที่เหลือ
    /// </summary>
    public async Task<SunoCreditsInfo> GetCreditsInfoAsync(CancellationToken ct = default)
    {
        if (_automationService == null)
        {
            return new SunoCreditsInfo
            {
                Credits = -1,
                Error = "Automation service not available"
            };
        }

        return await _automationService.GetCreditsInfoAsync(ct);
    }

    /// <summary>
    /// Get supported genres
    /// ดึงรายการ genres ที่รองรับ
    /// </summary>
    public IReadOnlyList<string> GetSupportedGenres()
    {
        return SunoAutomationService.SupportedGenres;
    }

    /// <summary>
    /// Get supported moods
    /// ดึงรายการ moods ที่รองรับ
    /// </summary>
    public IReadOnlyList<string> GetSupportedMoods()
    {
        return SunoAutomationService.SupportedMoods;
    }

    #region Private Methods

    private string BuildStyleString(MusicGenerationConfig config)
    {
        var parts = new List<string>();

        if (config.Genre.HasValue)
            parts.Add(config.Genre.Value.ToString().Replace("_", " "));

        if (config.Mood.HasValue)
            parts.Add(config.Mood.Value.ToString());

        if (config.Bpm.HasValue && config.Bpm > 0)
            parts.Add($"{config.Bpm} BPM");

        if (!string.IsNullOrEmpty(config.KeySignature))
            parts.Add($"Key of {config.KeySignature}");

        if (config.Instrumental)
            parts.Add("Instrumental");

        return string.Join(", ", parts);
    }

    private double ParseDuration(string? durationStr)
    {
        if (string.IsNullOrEmpty(durationStr)) return 0;

        // Try to parse formats like "2:30" or "150"
        if (durationStr.Contains(':'))
        {
            var parts = durationStr.Split(':');
            if (parts.Length == 2 &&
                int.TryParse(parts[0], out var minutes) &&
                int.TryParse(parts[1], out var seconds))
            {
                return minutes * 60 + seconds;
            }
        }

        if (double.TryParse(durationStr, out var duration))
        {
            return duration;
        }

        return 0;
    }

    private Task<TaskResult> GeneratePlaceholder(
        TaskItem task,
        MusicGenerationConfig config,
        System.Diagnostics.Stopwatch sw,
        CancellationToken ct)
    {
        _logger2.LogWarning("Returning placeholder result - automation service not available");

        // Suno typically generates 2 variations
        var multipleOutputs = new List<MediaOutput>();
        for (int i = 1; i <= config.NumberOfOutputs; i++)
        {
            multipleOutputs.Add(new MediaOutput
            {
                Index = i,
                Url = $"https://placeholder.suno.ai/audio/{Guid.NewGuid()}",
                Path = null,
                Metadata = new VideoMetadata
                {
                    Duration = config.Duration,
                    Format = "mp3",
                    FileSize = 0,
                    HasAudio = true,
                    CreatedAt = DateTime.UtcNow
                }
            });
        }

        var mediaResult = new MediaGenerationResult
        {
            AudioUrl = multipleOutputs.First().Url,
            AudioPath = null,
            Metadata = multipleOutputs.First().Metadata,
            MultipleOutputs = multipleOutputs
        };

        return Task.FromResult(new TaskResult
        {
            TaskId = task.Id ?? "",
            WorkerId = 0,
            Platform = Platform.ToString(),
            Success = true,
            Data = new ResultData
            {
                MediaResult = mediaResult,
                Message = $"[PLACEHOLDER] Music for prompt: '{config.Prompt}' ({config.NumberOfOutputs} variations). Use web automation for real generation."
            },
            ProcessingTimeMs = sw.ElapsedMilliseconds
        });
    }

    #endregion

    #region Platform Worker Overrides

    /// <summary>
    /// PostContentAsync not applicable for music generation platform
    /// </summary>
    public override Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        return Task.FromResult(new TaskResult
        {
            Success = false,
            Error = "Suno AI is a music generation platform, not a posting platform. Use other platform workers for posting."
        });
    }

    /// <summary>
    /// AnalyzeMetricsAsync not applicable for music generation platform
    /// </summary>
    public override Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct)
    {
        return Task.FromResult(new TaskResult
        {
            Success = false,
            Error = "Suno AI is a music generation platform, metrics analysis not applicable"
        });
    }

    #endregion
}
