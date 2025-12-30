using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// VideoCreationPipeline - ระบบสร้างวิดีโอแบบครบวงจร
/// รวมการสร้างรูป สร้างวิดีโอ สร้างเพลง และตัดต่อรวมกัน
/// </summary>
public class VideoCreationPipeline
{
    private readonly ILogger<VideoCreationPipeline> _logger;
    private readonly FreepikAutomationService _freepikService;
    private readonly SunoAutomationService _sunoService;
    private readonly VideoProcessor _videoProcessor;
    private readonly AudioProcessor _audioProcessor;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly PostPublisherService _postPublisher;

    private readonly string _outputPath;

    public VideoCreationPipeline(
        ILogger<VideoCreationPipeline> logger,
        FreepikAutomationService freepikService,
        SunoAutomationService sunoService,
        VideoProcessor videoProcessor,
        AudioProcessor audioProcessor,
        ContentGeneratorService contentGenerator,
        PostPublisherService postPublisher,
        string? outputPath = null)
    {
        _logger = logger;
        _freepikService = freepikService;
        _sunoService = sunoService;
        _videoProcessor = videoProcessor;
        _audioProcessor = audioProcessor;
        _contentGenerator = contentGenerator;
        _postPublisher = postPublisher;

        _outputPath = outputPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PostXAgent", "CreatedVideos");

        Directory.CreateDirectory(_outputPath);
    }

    #region Main Pipeline Methods

    /// <summary>
    /// สร้างวิดีโอแบบครบวงจรจาก Concept
    /// </summary>
    public async Task<PipelineResult> CreateVideoFromConceptAsync(
        VideoConceptRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new PipelineResult
        {
            ConceptId = Guid.NewGuid().ToString(),
            Concept = request.Concept,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("Starting video creation pipeline for concept: {Concept}", request.Concept);
            var totalSteps = CalculateTotalSteps(request);
            var currentStep = 0;

            // ============================================
            // STEP 1: สร้าง Content Plan จาก AI
            // ============================================
            ReportProgress(progress, ++currentStep, totalSteps, "กำลังวางแผนเนื้อหา...", PipelineStage.Planning);

            var contentPlan = await GenerateContentPlanAsync(request, ct);
            result.ContentPlan = contentPlan;

            if (!contentPlan.Success)
            {
                result.Success = false;
                result.Error = "Failed to generate content plan";
                return result;
            }

            // ============================================
            // STEP 2: สร้างรูปภาพด้วย Freepik
            // ============================================
            ReportProgress(progress, ++currentStep, totalSteps, "กำลังสร้างรูปภาพ...", PipelineStage.ImageGeneration);

            var generatedImages = new List<string>();
            foreach (var imagePrompt in contentPlan.ImagePrompts)
            {
                if (ct.IsCancellationRequested) break;

                var imageResult = await _freepikService.GenerateImageAsync(imagePrompt, new FreepikImageOptions
                {
                    AspectRatio = request.AspectRatio
                }, ct);

                if (imageResult.Success && !string.IsNullOrEmpty(imageResult.LocalPath))
                {
                    generatedImages.Add(imageResult.LocalPath);
                    result.GeneratedImages.Add(imageResult);
                }
                else
                {
                    _logger.LogWarning("Failed to generate image for prompt: {Prompt}", imagePrompt);
                }

                ReportProgress(progress, currentStep, totalSteps,
                    $"สร้างรูป {generatedImages.Count}/{contentPlan.ImagePrompts.Count}...",
                    PipelineStage.ImageGeneration);
            }

            if (generatedImages.Count == 0)
            {
                result.Success = false;
                result.Error = "Failed to generate any images";
                return result;
            }

            // ============================================
            // STEP 3: สร้างวิดีโอจากรูป (ถ้าเลือก)
            // ============================================
            var generatedVideos = new List<string>();

            if (request.GenerateVideoFromImages)
            {
                ReportProgress(progress, ++currentStep, totalSteps, "กำลังสร้างวิดีโอจากรูป...", PipelineStage.VideoGeneration);

                foreach (var imagePath in generatedImages.Take(request.MaxVideosFromImages))
                {
                    if (ct.IsCancellationRequested) break;

                    var motionPrompt = contentPlan.VideoPrompts?.FirstOrDefault()
                                       ?? "Smooth cinematic camera movement";

                    var videoResult = await _freepikService.GenerateVideoFromImageAsync(
                        imagePath, motionPrompt, new FreepikVideoOptions
                        {
                            DurationSeconds = request.VideoClipDurationSeconds
                        }, ct);

                    if (videoResult.Success && !string.IsNullOrEmpty(videoResult.LocalPath))
                    {
                        generatedVideos.Add(videoResult.LocalPath);
                        result.GeneratedVideos.Add(videoResult);
                    }
                }
            }

            // ============================================
            // STEP 4: สร้างเพลงด้วย Suno
            // ============================================
            string? generatedMusicPath = null;

            if (request.GenerateMusic)
            {
                ReportProgress(progress, ++currentStep, totalSteps, "กำลังสร้างเพลง...", PipelineStage.MusicGeneration);

                var musicResult = await _sunoService.GenerateMusicAsync(
                    contentPlan.MusicPrompt ?? request.MusicStyle ?? "Background music for promotional video",
                    new SunoMusicOptions
                    {
                        UseCustomMode = true,
                        Style = request.MusicStyle,
                        IsInstrumental = request.InstrumentalOnly
                    }, ct);

                if (musicResult.Success && musicResult.GeneratedSongs.Count > 0)
                {
                    var bestSong = musicResult.GeneratedSongs.First();
                    if (!string.IsNullOrEmpty(bestSong.LocalPath))
                    {
                        generatedMusicPath = bestSong.LocalPath;
                        result.GeneratedMusic = musicResult;
                    }
                }
            }

            // ============================================
            // STEP 5: ตัดต่อรวมเป็นวิดีโอสมบูรณ์
            // ============================================
            ReportProgress(progress, ++currentStep, totalSteps, "กำลังตัดต่อวิดีโอ...", PipelineStage.VideoEditing);

            var finalVideoPath = await ComposeVideoAsync(
                generatedImages,
                generatedVideos,
                generatedMusicPath,
                request,
                ct);

            if (string.IsNullOrEmpty(finalVideoPath))
            {
                result.Success = false;
                result.Error = "Failed to compose final video";
                return result;
            }

            result.FinalVideoPath = finalVideoPath;

            // ============================================
            // STEP 6: เตรียมวิดีโอสำหรับแต่ละ Platform
            // ============================================
            if (request.TargetPlatforms?.Any() == true)
            {
                ReportProgress(progress, ++currentStep, totalSteps, "กำลังเตรียมวิดีโอสำหรับแต่ละ Platform...", PipelineStage.PlatformPreparation);

                foreach (var platform in request.TargetPlatforms)
                {
                    var preparedResult = await _videoProcessor.PrepareForPlatformAsync(
                        finalVideoPath, platform, request.AspectRatio, ct);

                    if (preparedResult?.VideoPath != null)
                    {
                        result.PlatformVideos[platform] = preparedResult.VideoPath;
                    }
                }
            }

            // ============================================
            // STEP 7: Auto Post (ถ้าเลือก)
            // ============================================
            if (request.AutoPost && request.TargetPlatforms?.Any() == true)
            {
                ReportProgress(progress, ++currentStep, totalSteps, "กำลังโพสต์วิดีโอ...", PipelineStage.Publishing);

                foreach (var platform in request.TargetPlatforms)
                {
                    try
                    {
                        var videoToPost = result.PlatformVideos.GetValueOrDefault(platform) ?? finalVideoPath;

                        var postResult = await _postPublisher.PublishVideoAsync(new VideoPostRequest
                        {
                            Platform = platform,
                            VideoPath = videoToPost,
                            Title = contentPlan.SuggestedTitle,
                            Description = contentPlan.SuggestedDescription,
                            Hashtags = contentPlan.SuggestedHashtags
                        }, ct);

                        result.PostResults[platform] = postResult;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to post to {Platform}", platform);
                        result.PostResults[platform] = new PostResult { Success = false, Error = ex.Message };
                    }
                }
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;

            ReportProgress(progress, totalSteps, totalSteps, "เสร็จสมบูรณ์!", PipelineStage.Completed);

            _logger.LogInformation("Video creation pipeline completed successfully. Final video: {Path}", finalVideoPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Video creation pipeline failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// สร้างวิดีโอ Slideshow อย่างเดียว (ไม่ใช้ AI generation)
    /// </summary>
    public async Task<string?> CreateSlideshowAsync(
        List<string> imagePaths,
        string? audioPath,
        SlideshowOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new SlideshowOptions();

        var outputPath = Path.Combine(_outputPath, $"slideshow_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        return await _videoProcessor.CreateSlideshowAsync(
            imagePaths,
            audioPath,
            outputPath,
            options.DurationPerImage,
            ct);
    }

    /// <summary>
    /// รวมวิดีโอหลายคลิปเข้าด้วยกันพร้อมเพลง
    /// </summary>
    public async Task<string?> MergeVideosWithMusicAsync(
        List<string> videoPaths,
        string audioPath,
        double audioVolume = 0.8,
        CancellationToken ct = default)
    {
        // Concatenate videos first
        var concatPath = Path.Combine(_outputPath, $"concat_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        var ffmpegService = new FFmpegService(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FFmpegService>.Instance);

        var concatenated = await ffmpegService.ConcatenateVideosAsync(videoPaths, concatPath, ct);

        if (string.IsNullOrEmpty(concatenated))
        {
            return null;
        }

        // Mix with audio
        var finalPath = Path.Combine(_outputPath, $"final_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        return await ffmpegService.MixVideoWithAudioAsync(concatenated, audioPath, finalPath, audioVolume, ct);
    }

    #endregion

    #region Private Methods

    private async Task<ContentPlan> GenerateContentPlanAsync(VideoConceptRequest request, CancellationToken ct)
    {
        var plan = new ContentPlan { Success = false };

        try
        {
            var prompt = $@"Create a content plan for a promotional video with the following concept:
Concept: {request.Concept}
Target Duration: {request.TotalDurationSeconds} seconds
Style: {request.Style ?? "Professional and modern"}
Target Audience: {request.TargetAudience ?? "General audience"}

Please generate:
1. 3-5 image prompts for AI image generation (each describing a single scene)
2. 1-2 video motion prompts (how the camera should move)
3. A music prompt describing the ideal background music
4. A suggested video title
5. A short description (for social media)
6. 5-10 relevant hashtags

Respond in JSON format:
{{
    ""imagePrompts"": [""prompt1"", ""prompt2"", ...],
    ""videoPrompts"": [""motion prompt1"", ...],
    ""musicPrompt"": ""music description"",
    ""suggestedTitle"": ""Video Title"",
    ""suggestedDescription"": ""Short description..."",
    ""suggestedHashtags"": [""#tag1"", ""#tag2"", ...]
}}";

            var result = await _contentGenerator.GenerateAsync(prompt, null, "content_planning", "en", ct);

            if (!string.IsNullOrEmpty(result.Text))
            {
                // Parse JSON response
                var jsonStart = result.Text.IndexOf('{');
                var jsonEnd = result.Text.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = result.Text.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<ContentPlan>(json);

                    if (parsed != null)
                    {
                        plan = parsed;
                        plan.Success = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate content plan");

            // Fallback: สร้าง basic plan
            plan = new ContentPlan
            {
                Success = true,
                ImagePrompts = new List<string>
                {
                    $"{request.Concept}, professional photography, high quality",
                    $"{request.Concept}, different angle, detailed",
                    $"{request.Concept}, lifestyle shot, vibrant colors"
                },
                VideoPrompts = new List<string> { "Smooth pan, cinematic movement" },
                MusicPrompt = request.MusicStyle ?? "Upbeat background music, professional",
                SuggestedTitle = request.Concept,
                SuggestedDescription = $"Check out our amazing {request.Concept}!",
                SuggestedHashtags = new List<string> { "#promo", "#video", "#ai" }
            };
        }

        return plan;
    }

    private async Task<string?> ComposeVideoAsync(
        List<string> images,
        List<string> videos,
        string? musicPath,
        VideoConceptRequest request,
        CancellationToken ct)
    {
        try
        {
            var outputPath = Path.Combine(_outputPath, $"video_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
            string? composedPath = null;

            // ถ้ามี generated videos ให้ใช้เป็นหลัก
            if (videos.Count > 0)
            {
                // Concatenate videos
                var concatPath = Path.Combine(_outputPath, $"concat_{Guid.NewGuid():N}.mp4");
                var ffmpegService = new FFmpegService(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<FFmpegService>.Instance);

                composedPath = await ffmpegService.ConcatenateVideosAsync(videos, concatPath, ct);

                // ถ้า videos ไม่พอ เติมด้วย slideshow จากรูป
                if (videos.Count < 3 && images.Count > videos.Count)
                {
                    var remainingImages = images.Skip(videos.Count).ToList();
                    var slideshowPath = await _videoProcessor.CreateSlideshowAsync(
                        remainingImages, null,
                        Path.Combine(_outputPath, $"slideshow_{Guid.NewGuid():N}.mp4"),
                        3, ct);

                    if (!string.IsNullOrEmpty(slideshowPath) && !string.IsNullOrEmpty(composedPath))
                    {
                        var mergedPath = Path.Combine(_outputPath, $"merged_{Guid.NewGuid():N}.mp4");
                        composedPath = await ffmpegService.ConcatenateVideosAsync(
                            new List<string> { composedPath, slideshowPath }, mergedPath, ct);
                    }
                }
            }
            else
            {
                // ใช้ images ทำ slideshow
                composedPath = await _videoProcessor.CreateSlideshowAsync(
                    images, null, outputPath,
                    request.SlideshowDurationPerImage, ct);
            }

            // เพิ่มเพลง
            if (!string.IsNullOrEmpty(musicPath) && !string.IsNullOrEmpty(composedPath))
            {
                var ffmpegService = new FFmpegService(
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<FFmpegService>.Instance);

                var finalPath = Path.Combine(_outputPath, $"final_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
                composedPath = await ffmpegService.MixVideoWithAudioAsync(
                    composedPath, musicPath, finalPath, 0.8, ct);
            }

            return composedPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compose video");
            return null;
        }
    }

    private int CalculateTotalSteps(VideoConceptRequest request)
    {
        var steps = 3; // Plan + Images + Compose

        if (request.GenerateVideoFromImages) steps++;
        if (request.GenerateMusic) steps++;
        if (request.TargetPlatforms?.Any() == true) steps++;
        if (request.AutoPost) steps++;

        return steps;
    }

    private void ReportProgress(
        IProgress<PipelineProgress>? progress,
        int currentStep,
        int totalSteps,
        string message,
        PipelineStage stage)
    {
        progress?.Report(new PipelineProgress
        {
            CurrentStep = currentStep,
            TotalSteps = totalSteps,
            Percentage = (double)currentStep / totalSteps * 100,
            Message = message,
            Stage = stage
        });

        _logger.LogInformation("[{Step}/{Total}] {Message}", currentStep, totalSteps, message);
    }

    #endregion
}

#region Models

/// <summary>
/// Request สำหรับสร้างวิดีโอจาก Concept
/// </summary>
public class VideoConceptRequest
{
    /// <summary>แนวคิด/หัวข้อหลักของวิดีโอ</summary>
    public string Concept { get; set; } = "";

    /// <summary>สไตล์ของวิดีโอ</summary>
    public string? Style { get; set; }

    /// <summary>กลุ่มเป้าหมาย</summary>
    public string? TargetAudience { get; set; }

    /// <summary>ความยาวรวม (วินาที)</summary>
    public int TotalDurationSeconds { get; set; } = 60;

    /// <summary>Aspect ratio</summary>
    public AspectRatio AspectRatio { get; set; } = AspectRatio.Portrait_9_16;

    /// <summary>สร้างวิดีโอจากรูปหรือไม่</summary>
    public bool GenerateVideoFromImages { get; set; } = true;

    /// <summary>จำนวนวิดีโอสูงสุดที่จะสร้างจากรูป</summary>
    public int MaxVideosFromImages { get; set; } = 3;

    /// <summary>ความยาวของแต่ละ video clip (วินาที)</summary>
    public int VideoClipDurationSeconds { get; set; } = 5;

    /// <summary>ความยาวต่อรูปใน slideshow (วินาที)</summary>
    public int SlideshowDurationPerImage { get; set; } = 3;

    /// <summary>สร้างเพลงหรือไม่</summary>
    public bool GenerateMusic { get; set; } = true;

    /// <summary>สไตล์เพลง</summary>
    public string? MusicStyle { get; set; }

    /// <summary>เพลงแบบ instrumental (ไม่มีเนื้อร้อง)</summary>
    public bool InstrumentalOnly { get; set; } = true;

    /// <summary>Platforms ที่จะเผยแพร่</summary>
    public List<SocialPlatform>? TargetPlatforms { get; set; }

    /// <summary>โพสต์อัตโนมัติหลังสร้างเสร็จ</summary>
    public bool AutoPost { get; set; } = false;
}

/// <summary>
/// ผลลัพธ์จาก Pipeline
/// </summary>
public class PipelineResult
{
    public bool Success { get; set; }
    public string ConceptId { get; set; } = "";
    public string? Concept { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ContentPlan? ContentPlan { get; set; }
    public List<FreepikGenerationResult> GeneratedImages { get; set; } = new();
    public List<FreepikGenerationResult> GeneratedVideos { get; set; } = new();
    public SunoGenerationResult? GeneratedMusic { get; set; }

    public string? FinalVideoPath { get; set; }
    public Dictionary<SocialPlatform, string> PlatformVideos { get; set; } = new();
    public Dictionary<SocialPlatform, PostResult> PostResults { get; set; } = new();

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// Content Plan ที่สร้างจาก AI
/// </summary>
public class ContentPlan
{
    public bool Success { get; set; }
    public List<string> ImagePrompts { get; set; } = new();
    public List<string>? VideoPrompts { get; set; }
    public string? MusicPrompt { get; set; }
    public string? SuggestedTitle { get; set; }
    public string? SuggestedDescription { get; set; }
    public List<string>? SuggestedHashtags { get; set; }
}

/// <summary>
/// Progress report
/// </summary>
public class PipelineProgress
{
    public int CurrentStep { get; set; }
    public int TotalSteps { get; set; }
    public double Percentage { get; set; }
    public string Message { get; set; } = "";
    public PipelineStage Stage { get; set; }
}

public enum PipelineStage
{
    Planning,
    ImageGeneration,
    VideoGeneration,
    MusicGeneration,
    VideoEditing,
    PlatformPreparation,
    Publishing,
    Completed
}

/// <summary>
/// Slideshow options
/// </summary>
public class SlideshowOptions
{
    public int DurationPerImage { get; set; } = 3;
    public string Transition { get; set; } = "fade";
    public double TransitionDuration { get; set; } = 0.5;
}

/// <summary>
/// Video post request
/// </summary>
public class VideoPostRequest
{
    public SocialPlatform Platform { get; set; }
    public string VideoPath { get; set; } = "";
    public string? Title { get; set; }
    public string? Description { get; set; }
    public List<string>? Hashtags { get; set; }
    public DateTime? ScheduleTime { get; set; }
}

// Note: PostResult class is defined in PostPublisherService.cs

#endregion
