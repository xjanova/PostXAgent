# แผนการพัฒนาต่อยอดระบบ PostXAgent

**วัตถุประสงค์**: เพิ่มความสามารถ AI Video Generation และ Music Generation ลงในระบบที่มีอยู่
**วันที่**: 24 ธันวาคม 2025
**เวอร์ชัน**: 2.0.0

---

## 📋 สารบัญ

1. [ภาพรวมการพัฒนา](#ภาพรวมการพัฒนา)
2. [ฟีเจอร์ที่ต้องเพิ่ม](#ฟีเจอร์ที่ต้องเพิ่ม)
3. [การเปลี่ยนแปลงใน Core Models](#การเปลี่ยนแปลงใน-core-models)
4. [Platform Workers ใหม่](#platform-workers-ใหม่)
5. [FFmpeg Integration](#ffmpeg-integration)
6. [Queue System Enhancement](#queue-system-enhancement)
7. [API Endpoints](#api-endpoints)
8. [UI Components](#ui-components)
9. [Database Schema](#database-schema)
10. [Timeline](#timeline)

---

## ภาพรวมการพัฒนา

### 🎯 เป้าหมาย

เพิ่มความสามารถใหม่ลงในระบบ PostXAgent โดยใช้โครงสร้างและ Web Learning Engine ที่มีอยู่แล้ว:

1. **AI Video Generation** - สร้างวีดีโอด้วย AI (Freepik, Runway, Pika, Luma)
2. **AI Music Generation** - สร้างเพลงด้วย AI (Suno AI)
3. **Media Processing** - ประมวลผลวีดีโอและเสียงด้วย FFmpeg
4. **Video & Music Mixing** - ผสมวีดีโอกับเพลง

### 🏗️ สถาปัตยกรรมที่มีอยู่

```
AIManagerCore/
├── src/AIManager.Core/
│   ├── Models/
│   │   ├── Enums.cs                    ⚠️ ต้องเพิ่ม enum
│   │   ├── TaskItem.cs                 ⚠️ ต้องเพิ่ม properties
│   │   └── ...
│   ├── Workers/
│   │   ├── BasePlatformWorker.cs       ✅ ใช้ซ้ำได้
│   │   ├── PlatformWorkers.cs          ⚠️ ต้องเพิ่ม workers
│   │   └── WorkerFactory.cs            ⚠️ ต้องอัพเดท
│   ├── WebAutomation/
│   │   ├── BrowserController.cs        ✅ ใช้ได้เลย
│   │   ├── WorkflowLearningEngine.cs   ✅ ใช้ได้เลย
│   │   ├── WorkflowExecutor.cs         ✅ ใช้ได้เลย
│   │   └── ...                         ✅ ใช้ทุกไฟล์ได้เลย
│   ├── Services/
│   │   ├── AIBrainService.cs           ✅ ใช้ได้เลย
│   │   ├── ContentGeneratorService.cs  ✅ ใช้ได้เลย
│   │   └── ...
│   └── NEW: MediaProcessing/           ❌ ต้องสร้างใหม่
│       ├── FFmpegService.cs
│       ├── VideoProcessor.cs
│       ├── AudioProcessor.cs
│       └── MixingService.cs
```

---

## ฟีเจอร์ที่ต้องเพิ่ม

### ✅ สิ่งที่มีอยู่แล้ว (ใช้ซ้ำได้)

| Component | สถานะ | ใช้กับ Video/Music ได้หรือไม่ |
|-----------|------|------------------------------|
| **Web Learning System** | ✅ Complete | ✅ ใช้ได้เลย - รองรับทุก platform |
| **BrowserController** | ✅ Complete | ✅ ใช้ได้เลย - Playwright automation |
| **WorkflowLearningEngine** | ✅ Complete | ✅ ใช้ได้เลย - เรียนรู้ workflow ใดๆ ก็ได้ |
| **WorkflowExecutor** | ✅ Complete | ✅ ใช้ได้เลย - Execute workflows |
| **AIElementAnalyzer** | ✅ Complete | ✅ ใช้ได้เลย - หา elements อัตโนมัติ |
| **VisualElementRecognizer** | ✅ Complete | ✅ ใช้ได้เลย - Visual matching |
| **WorkflowStorage** | ✅ Complete | ✅ ใช้ได้เลย - บันทึก workflows |
| **BasePlatformWorker** | ✅ Complete | ✅ ใช้ได้เลย - Base class |
| **TaskItem/TaskResult** | ⚠️ Partial | ⚠️ ต้องเพิ่ม properties |
| **Enums** | ⚠️ Partial | ⚠️ ต้องเพิ่ม platforms และ task types |

### ❌ สิ่งที่ต้องสร้างใหม่

1. **Platform Workers ใหม่**
   - FreepikWorker
   - RunwayWorker
   - PikaLabsWorker
   - LumaAIWorker
   - SunoAIWorker

2. **Media Processing Services**
   - FFmpegService
   - VideoProcessor
   - AudioProcessor
   - MixingService

3. **New Task Types**
   - GenerateVideo
   - GenerateMusic
   - ProcessVideo
   - MixVideoWithMusic

4. **API Endpoints**
   - VideoGenerationController
   - MusicGenerationController
   - MediaProcessingController

5. **UI Components**
   - VideoGenerationPage
   - MusicGenerationPage
   - MediaLibraryPage

---

## การเปลี่ยนแปลงใน Core Models

### 1. อัพเดท `Enums.cs`

```csharp
/// <summary>
/// Supported platforms (Thailand + AI Services)
/// </summary>
public enum SocialPlatform
{
    // Social Media (existing)
    Facebook,
    Instagram,
    TikTok,
    Twitter,
    Line,
    YouTube,
    Threads,
    LinkedIn,
    Pinterest,

    // AI Video Generation (NEW)
    Freepik,       // Freepik Pikaso AI
    Runway,        // Runway ML
    PikaLabs,      // Pika Labs
    LumaAI,        // Luma Dream Machine

    // AI Music Generation (NEW)
    SunoAI         // Suno AI Music
}

/// <summary>
/// Types of tasks (extended)
/// </summary>
public enum TaskType
{
    // Content Generation (existing)
    GenerateContent,
    GenerateImage,

    // AI Media Generation (NEW)
    GenerateVideo,              // สร้างวีดีโอด้วย AI
    GenerateMusic,              // สร้างเพลงด้วย AI
    ProcessVideo,               // ประมวลผลวีดีโอ
    ProcessAudio,               // ประมวลผลเสียง
    MixVideoWithMusic,          // ผสมวีดีโอกับเพลง
    ConcatenateVideos,          // ต่อคลิปวีดีโอ
    ExtractAudioFromVideo,      // แยกเสียงจากวีดีโอ
    GenerateThumbnail,          // สร้าง thumbnail

    // Posting (existing)
    PostContent,
    SchedulePost,
    PostToGroup,
    PostToMultipleGroups,

    // ... (existing types)
}

/// <summary>
/// Video generation modes (NEW)
/// </summary>
public enum VideoGenerationMode
{
    TextToVideo,       // สร้างจากข้อความ
    ImageToVideo,      // สร้างจากรูปภาพ
    VideoToVideo,      // แปลงวีดีโอ
    ExpandCanvas       // ขยาย canvas
}

/// <summary>
/// Aspect ratios (NEW)
/// </summary>
public enum AspectRatio
{
    Landscape_16_9,    // 16:9 - YouTube, Landscape
    Portrait_9_16,     // 9:16 - TikTok, Reels, Stories
    Square_1_1,        // 1:1 - Instagram Feed
    Classic_4_3,       // 4:3 - Classic
    Ultrawide_21_9     // 21:9 - Ultrawide
}

/// <summary>
/// Video quality levels (NEW)
/// </summary>
public enum VideoQuality
{
    Low_480p,
    Medium_720p,
    High_1080p,
    Ultra_4K
}

/// <summary>
/// Music genres (NEW)
/// </summary>
public enum MusicGenre
{
    Pop,
    Rock,
    Electronic,
    HipHop,
    Jazz,
    Classical,
    Ambient,
    Cinematic,
    LoFi,
    Acoustic
}
```

---

### 2. เพิ่ม Properties ใน `TaskItem.cs`

```csharp
public class TaskItem
{
    // Existing properties...

    // NEW: Video Generation Properties
    public VideoGenerationConfig? VideoConfig { get; set; }

    // NEW: Music Generation Properties
    public MusicGenerationConfig? MusicConfig { get; set; }

    // NEW: Media Processing Properties
    public MediaProcessingConfig? ProcessingConfig { get; set; }
}

/// <summary>
/// Configuration for video generation (NEW)
/// </summary>
public class VideoGenerationConfig
{
    public VideoGenerationMode Mode { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string? NegativePrompt { get; set; }
    public int Duration { get; set; } = 5; // seconds
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Landscape_16_9;
    public VideoQuality Quality { get; set; } = VideoQuality.High_1080p;
    public int Fps { get; set; } = 30;
    public string? SourceImageUrl { get; set; }
    public string? SourceVideoUrl { get; set; }
    public int? Seed { get; set; }
    public int NumberOfOutputs { get; set; } = 1;
    public double Strength { get; set; } = 0.8; // 0-1
    public Dictionary<string, object>? ProviderSpecific { get; set; }
}

/// <summary>
/// Configuration for music generation (NEW)
/// </summary>
public class MusicGenerationConfig
{
    public string Prompt { get; set; } = string.Empty;
    public int Duration { get; set; } = 30; // seconds
    public MusicGenre? Genre { get; set; }
    public string? Mood { get; set; }
    public bool Instrumental { get; set; } = false;
    public string? Lyrics { get; set; }
}

/// <summary>
/// Configuration for media processing (NEW)
/// </summary>
public class MediaProcessingConfig
{
    public string? VideoPath { get; set; }
    public string? AudioPath { get; set; }
    public string? OutputFormat { get; set; } = "mp4";
    public VideoQuality? OutputQuality { get; set; }
    public bool MixAudio { get; set; } = false;
    public double AudioVolume { get; set; } = 1.0; // 0-1
    public List<string>? VideosToConcat { get; set; }
    public bool GenerateThumbnail { get; set; } = true;
}
```

---

### 3. เพิ่ม Properties ใน `TaskResult.cs`

```csharp
public class TaskResult
{
    // Existing properties...

    // NEW: Media generation results
    public MediaGenerationResult? MediaResult { get; set; }
}

/// <summary>
/// Result from media generation (NEW)
/// </summary>
public class MediaGenerationResult
{
    public string? VideoUrl { get; set; }
    public string? VideoPath { get; set; }
    public string? AudioUrl { get; set; }
    public string? AudioPath { get; set; }
    public string? ThumbnailUrl { get; set; }
    public VideoMetadata? Metadata { get; set; }
}

/// <summary>
/// Video metadata (NEW)
/// </summary>
public class VideoMetadata
{
    public int Width { get; set; }
    public int Height { get; set; }
    public double Duration { get; set; } // seconds
    public int Fps { get; set; }
    public long FileSize { get; set; } // bytes
    public string Format { get; set; } = string.Empty;
    public string? Codec { get; set; }
    public long? Bitrate { get; set; }
    public Dictionary<string, object>? Extra { get; set; }
}
```

---

## Platform Workers ใหม่

### 1. FreepikWorker.cs (PRIMARY)

```csharp
using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Workers;

/// <summary>
/// Freepik Pikaso AI Worker
/// Primary video generation provider
/// Uses Web Learning for automation
/// </summary>
public class FreepikWorker : BasePlatformWorker
{
    private readonly BrowserController _browser;
    private readonly WorkflowLearningEngine _learningEngine;
    private readonly WorkflowExecutor _executor;
    private const string PikasoUrl = "https://www.freepik.com/pikaso/ai-video-generator";

    public override SocialPlatform Platform => SocialPlatform.Freepik;

    public FreepikWorker(
        BrowserController browser,
        WorkflowLearningEngine learningEngine,
        WorkflowExecutor executor,
        ILogger<FreepikWorker> logger) : base(logger)
    {
        _browser = browser;
        _learningEngine = learningEngine;
        _executor = executor;
    }

    public override async Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var videoConfig = task.VideoConfig;
            if (videoConfig == null)
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Missing video configuration"
                };
            }

            _logger.LogInformation("Starting video generation with Freepik Pikaso AI");
            _logger.LogInformation("Prompt: {Prompt}", videoConfig.Prompt);

            // Initialize browser
            await _browser.LaunchAsync(headless: true);
            await _browser.NavigateAsync(PikasoUrl);

            // ตรวจสอบว่ามี workflow ที่เรียนรู้ไว้แล้วหรือไม่
            var workflowType = "generate_video";
            var existingWorkflow = await _learningEngine.FindWorkflowAsync(
                Platform.ToString(),
                workflowType
            );

            string? videoUrl = null;

            if (existingWorkflow != null && existingWorkflow.ConfidenceScore >= 0.7)
            {
                _logger.LogInformation("Using learned workflow (confidence: {Confidence})",
                    existingWorkflow.ConfidenceScore);

                // Execute learned workflow
                var result = await _executor.ExecuteAsync(
                    existingWorkflow,
                    new Dictionary<string, object>
                    {
                        ["prompt"] = videoConfig.Prompt,
                        ["duration"] = videoConfig.Duration,
                        ["aspectRatio"] = videoConfig.AspectRatio.ToString()
                    }
                );

                if (result.Success)
                {
                    videoUrl = await ExtractVideoUrlAsync();
                }
                else
                {
                    _logger.LogWarning("Workflow execution failed, entering learning mode");
                    videoUrl = await LearnAndGenerateAsync(videoConfig);
                }
            }
            else
            {
                _logger.LogInformation("No learned workflow found, entering learning mode");
                videoUrl = await LearnAndGenerateAsync(videoConfig);
            }

            if (string.IsNullOrEmpty(videoUrl))
            {
                return new TaskResult
                {
                    Success = false,
                    Error = "Failed to generate video"
                };
            }

            // Download video
            var videoPath = await DownloadVideoAsync(videoUrl, task.Id);

            // Extract metadata using FFmpeg
            var metadata = await ExtractMetadataAsync(videoPath);

            return new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    PostId = task.Id,
                    PlatformUrl = videoUrl
                },
                MediaResult = new MediaGenerationResult
                {
                    VideoUrl = videoUrl,
                    VideoPath = videoPath,
                    Metadata = metadata
                },
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating video with Freepik");
            return new TaskResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = sw.ElapsedMilliseconds
            };
        }
        finally
        {
            await _browser.CloseAsync();
        }
    }

    /// <summary>
    /// เรียนรู้ workflow ใหม่และสร้างวีดีโอ
    /// </summary>
    private async Task<string?> LearnAndGenerateAsync(VideoGenerationConfig config)
    {
        // สร้าง teaching session
        var session = await _learningEngine.StartTeachingSessionAsync(
            Platform.ToString(),
            "generate_video"
        );

        // เริ่ม recording
        await _browser.StartRecordingAsync();

        // AI จะช่วยหา elements และทำงานอัตโนมัติ

        // 1. หา prompt input field
        var promptInput = await _browser.FindElementAsync(new[] {
            "textarea[placeholder*='prompt' i]",
            "textarea[placeholder*='describe' i]",
            "input[type='text'][placeholder*='prompt' i]",
            "div[contenteditable='true']",
            "[data-testid*='prompt']"
        });

        if (promptInput == null)
        {
            throw new Exception("Cannot find prompt input field");
        }

        await _browser.TypeAsync(promptInput, config.Prompt, humanLike: true);
        await Task.Delay(1000);

        // 2. หา generate button
        var generateButton = await _browser.FindElementAsync(new[] {
            "button:has-text('Generate')",
            "button:has-text('Create')",
            "[data-testid*='generate']",
            "[aria-label*='generate' i]"
        });

        if (generateButton == null)
        {
            throw new Exception("Cannot find generate button");
        }

        await _browser.ClickAsync(generateButton);

        // 3. รอจนกว่าวีดีโอจะสร้างเสร็จ
        await _browser.WaitForSelectorAsync("video, [data-testid*='video']", timeout: 300000);

        // หยุด recording
        var recordedSteps = await _browser.StopRecordingAsync();

        // เรียนรู้จาก recorded steps
        var workflow = await _learningEngine.LearnFromTeachingSessionAsync(
            session.Id,
            recordedSteps
        );

        _logger.LogInformation("Learned new workflow with {StepCount} steps", workflow.Steps.Count);

        // Extract video URL
        return await ExtractVideoUrlAsync();
    }

    /// <summary>
    /// ดึง video URL จากหน้าเว็บ
    /// </summary>
    private async Task<string?> ExtractVideoUrlAsync()
    {
        // ลอง extract จาก video element
        var videoSrc = await _browser.EvaluateAsync<string>(@"
            const video = document.querySelector('video');
            return video ? video.src : null;
        ");

        if (!string.IsNullOrEmpty(videoSrc))
        {
            return videoSrc;
        }

        // ลอง extract จาก download link
        var downloadHref = await _browser.EvaluateAsync<string>(@"
            const link = document.querySelector('a[download], a[href*="".mp4""]');
            return link ? link.href : null;
        ");

        return downloadHref;
    }

    /// <summary>
    /// ดาวน์โหลดวีดีโอ
    /// </summary>
    private async Task<string> DownloadVideoAsync(string url, string taskId)
    {
        var outputDir = Path.Combine(AppContext.BaseDirectory, "downloads", "videos");
        Directory.CreateDirectory(outputDir);

        var filename = $"{taskId}.mp4";
        var outputPath = Path.Combine(outputDir, filename);

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(outputPath, FileMode.Create);
        await response.Content.CopyToAsync(fs);

        _logger.LogInformation("Downloaded video to {Path}", outputPath);

        return outputPath;
    }

    /// <summary>
    /// Extract metadata using FFmpeg
    /// </summary>
    private async Task<VideoMetadata> ExtractMetadataAsync(string videoPath)
    {
        // TODO: Implement FFmpeg metadata extraction
        // For now, return basic metadata
        var fileInfo = new FileInfo(videoPath);

        return new VideoMetadata
        {
            Width = 1920,
            Height = 1080,
            Duration = 5,
            Fps = 30,
            FileSize = fileInfo.Length,
            Format = "mp4"
        };
    }
}
```

**หมายเหตุ**: Workers อื่นๆ (Runway, Pika, Luma, Suno) จะมีโครงสร้างคล้ายกัน แค่เปลี่ยน URL และ element selectors

---

## FFmpeg Integration

สร้างโฟลเดอร์และไฟล์ใหม่:

```
AIManagerCore/src/AIManager.Core/MediaProcessing/
├── FFmpegService.cs
├── VideoProcessor.cs
├── AudioProcessor.cs
└── MixingService.cs
```

### FFmpegService.cs

```csharp
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.MediaProcessing;

/// <summary>
/// FFmpeg integration service
/// Handles video and audio processing
/// </summary>
public class FFmpegService
{
    private readonly ILogger<FFmpegService> _logger;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    public FFmpegService(ILogger<FFmpegService> logger)
    {
        _logger = logger;
        _ffmpegPath = FindFFmpeg();
        _ffprobePath = FindFFprobe();
    }

    /// <summary>
    /// Extract video metadata
    /// </summary>
    public async Task<VideoMetadata> GetMetadataAsync(string videoPath)
    {
        var args = $"-v quiet -print_format json -show_format -show_streams \"{videoPath}\"";
        var output = await RunFFprobeAsync(args);

        // Parse JSON output
        var json = System.Text.Json.JsonDocument.Parse(output);
        var format = json.RootElement.GetProperty("format");
        var videoStream = json.RootElement.GetProperty("streams")
            .EnumerateArray()
            .FirstOrDefault(s => s.GetProperty("codec_type").GetString() == "video");

        return new VideoMetadata
        {
            Duration = double.Parse(format.GetProperty("duration").GetString() ?? "0"),
            FileSize = long.Parse(format.GetProperty("size").GetString() ?? "0"),
            Format = format.GetProperty("format_name").GetString() ?? "unknown",
            Width = videoStream.GetProperty("width").GetInt32(),
            Height = videoStream.GetProperty("height").GetInt32(),
            Fps = EvaluateFrameRate(videoStream.GetProperty("r_frame_rate").GetString() ?? "30/1"),
            Codec = videoStream.GetProperty("codec_name").GetString(),
            Bitrate = long.TryParse(format.GetProperty("bit_rate").GetString(), out var br) ? br : null
        };
    }

    /// <summary>
    /// Convert video to different format/quality
    /// </summary>
    public async Task<string> ConvertVideoAsync(
        string inputPath,
        string outputPath,
        VideoQuality quality = VideoQuality.High_1080p,
        string codec = "libx264")
    {
        var resolution = quality switch
        {
            VideoQuality.Low_480p => "854:480",
            VideoQuality.Medium_720p => "1280:720",
            VideoQuality.High_1080p => "1920:1080",
            VideoQuality.Ultra_4K => "3840:2160",
            _ => "1920:1080"
        };

        var args = $"-i \"{inputPath}\" -vf scale={resolution} -c:v {codec} -preset medium -crf 23 -c:a aac -b:a 128k \"{outputPath}\"";

        await RunFFmpegAsync(args);

        return outputPath;
    }

    /// <summary>
    /// Mix video with audio
    /// </summary>
    public async Task<string> MixVideoWithAudioAsync(
        string videoPath,
        string audioPath,
        string outputPath,
        double audioVolume = 1.0)
    {
        var args = $"-i \"{videoPath}\" -i \"{audioPath}\" -c:v copy -c:a aac -filter:a \"volume={audioVolume}\" -shortest \"{outputPath}\"";

        await RunFFmpegAsync(args);

        return outputPath;
    }

    /// <summary>
    /// Concatenate multiple videos
    /// </summary>
    public async Task<string> ConcatenateVideosAsync(
        List<string> videoPaths,
        string outputPath)
    {
        // สร้าง concat file
        var concatFile = Path.GetTempFileName();
        var concatContent = string.Join("\n", videoPaths.Select(p => $"file '{p}'"));
        await File.WriteAllTextAsync(concatFile, concatContent);

        var args = $"-f concat -safe 0 -i \"{concatFile}\" -c copy \"{outputPath}\"";

        await RunFFmpegAsync(args);

        File.Delete(concatFile);

        return outputPath;
    }

    /// <summary>
    /// Generate thumbnail
    /// </summary>
    public async Task<string> GenerateThumbnailAsync(
        string videoPath,
        string outputPath,
        double timeOffset = 1.0)
    {
        var args = $"-ss {timeOffset} -i \"{videoPath}\" -vframes 1 \"{outputPath}\"";

        await RunFFmpegAsync(args);

        return outputPath;
    }

    /// <summary>
    /// Extract audio from video
    /// </summary>
    public async Task<string> ExtractAudioAsync(
        string videoPath,
        string outputPath)
    {
        var args = $"-i \"{videoPath}\" -vn -acodec libmp3lame -q:a 2 \"{outputPath}\"";

        await RunFFmpegAsync(args);

        return outputPath;
    }

    private async Task<string> RunFFmpegAsync(string arguments)
    {
        return await RunProcessAsync(_ffmpegPath, arguments);
    }

    private async Task<string> RunFFprobeAsync(string arguments)
    {
        return await RunProcessAsync(_ffprobePath, arguments);
    }

    private async Task<string> RunProcessAsync(string filename, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = filename,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new Exception($"Failed to start {filename}");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            _logger.LogError("FFmpeg error: {Error}", error);
            throw new Exception($"FFmpeg failed: {error}");
        }

        return output;
    }

    private string FindFFmpeg()
    {
        // Try common locations
        var paths = new[]
        {
            "ffmpeg",
            "ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            "/usr/bin/ffmpeg",
            "/usr/local/bin/ffmpeg"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path) || IsInPath(path))
            {
                return path;
            }
        }

        throw new Exception("FFmpeg not found. Please install FFmpeg and add it to PATH");
    }

    private string FindFFprobe()
    {
        return FindFFmpeg().Replace("ffmpeg", "ffprobe");
    }

    private bool IsInPath(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "-version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    private int EvaluateFrameRate(string frameRate)
    {
        var parts = frameRate.Split('/');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var num) &&
            int.TryParse(parts[1], out var den) &&
            den != 0)
        {
            return num / den;
        }
        return 30;
    }
}
```

---

## สรุป

การพัฒนาต่อยอดระบบเดิมจะ:

✅ **ใช้ซ้ำ** Web Learning System ที่มีอยู่ทั้งหมด
✅ **เพิ่ม** Platform Workers ใหม่ (Freepik, Runway, Pika, Luma, Suno)
✅ **เพิ่ม** FFmpeg Integration สำหรับ video processing
✅ **ขยาย** Enums และ Models เพื่อรองรับ video/music generation
✅ **เพิ่ม** API Endpoints และ UI Components

**ไม่ต้องสร้างใหม่ทั้งหมด** - ประหยัดเวลาและใช้ประโยชน์จากโค๊ดที่มีอยู่แล้ว! 🚀

---

**Next Steps**: ต้องการให้ผมเริ่ม implement จากส่วนไหนก่อนครับ?

1. FreepikWorker
2. FFmpegService
3. API Controllers
4. UI Components
5. Tests

บอกผมได้เลยครับ! 💪
