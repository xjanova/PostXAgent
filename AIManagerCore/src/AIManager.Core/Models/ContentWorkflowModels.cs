using Newtonsoft.Json;

namespace AIManager.Core.Models;

#region Workflow Status Enums

/// <summary>
/// สถานะของ Content Workflow
/// </summary>
public enum ContentWorkflowStatus
{
    Draft,           // ร่าง
    Pending,         // รอดำเนินการ
    GeneratingContent,    // กำลังสร้างเนื้อหา
    GeneratingImages,     // กำลังสร้างภาพ
    GeneratingAudio,      // กำลังสร้างเสียง
    UploadingToDrive,     // กำลังอัพโหลดไป Cloud Drive
    AssemblingVideo,      // กำลังประกอบวิดีโอ
    ReadyToPublish,       // พร้อมเผยแพร่
    Publishing,           // กำลังเผยแพร่
    Completed,            // เสร็จสิ้น
    Failed,               // ล้มเหลว
    Cancelled             // ยกเลิก
}

/// <summary>
/// ประเภท Cloud Drive
/// </summary>
public enum CloudDriveType
{
    GoogleDrive,
    OneDrive,
    Dropbox,
    LocalStorage
}

/// <summary>
/// ประเภทเสียง
/// </summary>
public enum AudioType
{
    TextToSpeech,    // AI สร้างเสียงจาก text
    BackgroundMusic, // เพลงประกอบ
    VoiceOver,       // บันทึกเสียง
    Mixed            // ผสมทั้งหมด
}

/// <summary>
/// สไตล์วิดีโอ
/// </summary>
public enum VideoStyle
{
    Slideshow,       // สไลด์โชว์ภาพนิ่ง
    KenBurns,        // zoom/pan effect
    Cinematic,       // แบบหนัง
    Social,          // สำหรับ social media
    Educational,     // สำหรับสอน
    ProductShowcase  // โชว์สินค้า
}

#endregion

#region User Package Models

/// <summary>
/// แพ็คเกจผู้ใช้
/// </summary>
public class UserPackage
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("user_id")]
    public string UserId { get; set; } = "";

    [JsonProperty("package_name")]
    public string PackageName { get; set; } = "Free";

    [JsonProperty("tier")]
    public PackageTier Tier { get; set; } = PackageTier.Free;

    // Quota Limits
    [JsonProperty("max_videos_per_month")]
    public int MaxVideosPerMonth { get; set; } = 5;

    [JsonProperty("max_video_duration_seconds")]
    public int MaxVideoDurationSeconds { get; set; } = 60;

    [JsonProperty("max_images_per_video")]
    public int MaxImagesPerVideo { get; set; } = 10;

    [JsonProperty("max_platforms")]
    public int MaxPlatforms { get; set; } = 2;

    [JsonProperty("allowed_platforms")]
    public List<SocialPlatform> AllowedPlatforms { get; set; } = new() { SocialPlatform.Facebook, SocialPlatform.Instagram };

    [JsonProperty("cloud_storage_mb")]
    public int CloudStorageMB { get; set; } = 500;

    [JsonProperty("has_watermark")]
    public bool HasWatermark { get; set; } = true;

    [JsonProperty("can_use_premium_voices")]
    public bool CanUsePremiumVoices { get; set; } = false;

    [JsonProperty("can_use_premium_music")]
    public bool CanUsePremiumMusic { get; set; } = false;

    // Usage Tracking
    [JsonProperty("videos_created_this_month")]
    public int VideosCreatedThisMonth { get; set; }

    [JsonProperty("storage_used_mb")]
    public double StorageUsedMB { get; set; }

    [JsonProperty("reset_date")]
    public DateTime ResetDate { get; set; } = DateTime.UtcNow.AddMonths(1);

    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    public bool CanCreateVideo() =>
        IsActive && VideosCreatedThisMonth < MaxVideosPerMonth &&
        (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

    public int RemainingVideos => Math.Max(0, MaxVideosPerMonth - VideosCreatedThisMonth);

    public double RemainingStorageMB => Math.Max(0, CloudStorageMB - StorageUsedMB);
}

public enum PackageTier
{
    Free = 0,
    Starter = 1,
    Professional = 2,
    Business = 3,
    Enterprise = 4
}

/// <summary>
/// Package Definitions
/// </summary>
public static class PackageDefinitions
{
    public static UserPackage CreateFreePackage(string userId) => new()
    {
        UserId = userId,
        PackageName = "Free",
        Tier = PackageTier.Free,
        MaxVideosPerMonth = 5,
        MaxVideoDurationSeconds = 60,
        MaxImagesPerVideo = 10,
        MaxPlatforms = 2,
        AllowedPlatforms = new() { SocialPlatform.Facebook, SocialPlatform.Instagram },
        CloudStorageMB = 500,
        HasWatermark = true,
        CanUsePremiumVoices = false,
        CanUsePremiumMusic = false
    };

    public static UserPackage CreateStarterPackage(string userId) => new()
    {
        UserId = userId,
        PackageName = "Starter",
        Tier = PackageTier.Starter,
        MaxVideosPerMonth = 20,
        MaxVideoDurationSeconds = 180,
        MaxImagesPerVideo = 20,
        MaxPlatforms = 4,
        AllowedPlatforms = new() { SocialPlatform.Facebook, SocialPlatform.Instagram, SocialPlatform.TikTok, SocialPlatform.YouTube },
        CloudStorageMB = 2000,
        HasWatermark = false,
        CanUsePremiumVoices = true,
        CanUsePremiumMusic = false
    };

    public static UserPackage CreateProfessionalPackage(string userId) => new()
    {
        UserId = userId,
        PackageName = "Professional",
        Tier = PackageTier.Professional,
        MaxVideosPerMonth = 50,
        MaxVideoDurationSeconds = 600,
        MaxImagesPerVideo = 50,
        MaxPlatforms = 6,
        AllowedPlatforms = new() { SocialPlatform.Facebook, SocialPlatform.Instagram, SocialPlatform.TikTok, SocialPlatform.YouTube, SocialPlatform.Twitter, SocialPlatform.Line },
        CloudStorageMB = 10000,
        HasWatermark = false,
        CanUsePremiumVoices = true,
        CanUsePremiumMusic = true
    };

    public static UserPackage CreateBusinessPackage(string userId) => new()
    {
        UserId = userId,
        PackageName = "Business",
        Tier = PackageTier.Business,
        MaxVideosPerMonth = 200,
        MaxVideoDurationSeconds = 1800,
        MaxImagesPerVideo = 100,
        MaxPlatforms = 9,
        AllowedPlatforms = Enum.GetValues<SocialPlatform>().ToList(),
        CloudStorageMB = 50000,
        HasWatermark = false,
        CanUsePremiumVoices = true,
        CanUsePremiumMusic = true
    };
}

#endregion

#region Cloud Drive Models

/// <summary>
/// Cloud Drive Connection
/// </summary>
public class CloudDriveConnection
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("user_id")]
    public string UserId { get; set; } = "";

    [JsonProperty("drive_type")]
    public CloudDriveType DriveType { get; set; }

    [JsonProperty("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonProperty("token_expires_at")]
    public DateTime TokenExpiresAt { get; set; }

    [JsonProperty("folder_id")]
    public string FolderId { get; set; } = "";

    [JsonProperty("folder_name")]
    public string FolderName { get; set; } = "AIManager_Content";

    [JsonProperty("is_connected")]
    public bool IsConnected { get; set; }

    [JsonProperty("last_sync")]
    public DateTime? LastSync { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; } = "";

    public bool NeedsRefresh => DateTime.UtcNow >= TokenExpiresAt.AddMinutes(-5);
}

/// <summary>
/// Cloud Drive File Info
/// </summary>
public class CloudDriveFile
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("mime_type")]
    public string MimeType { get; set; } = "";

    [JsonProperty("size_bytes")]
    public long SizeBytes { get; set; }

    [JsonProperty("download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonProperty("thumbnail_url")]
    public string? ThumbnailUrl { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("modified_at")]
    public DateTime ModifiedAt { get; set; }

    [JsonProperty("parent_folder_id")]
    public string ParentFolderId { get; set; } = "";
}

#endregion

#region Content Workflow Models

/// <summary>
/// Content Workflow - Flow หลักของระบบ
/// </summary>
public class ContentWorkflow
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("user_id")]
    public string UserId { get; set; } = "";

    [JsonProperty("title")]
    public string Title { get; set; } = "";

    [JsonProperty("status")]
    public ContentWorkflowStatus Status { get; set; } = ContentWorkflowStatus.Draft;

    // Input
    [JsonProperty("concept")]
    public ContentConcept Concept { get; set; } = new();

    // Generated Content
    [JsonProperty("generated_script")]
    public VideoScript? GeneratedScript { get; set; }

    [JsonProperty("generated_images")]
    public List<GeneratedImage> GeneratedImages { get; set; } = new();

    [JsonProperty("generated_audio")]
    public GeneratedAudio? GeneratedAudio { get; set; }

    // Cloud Drive
    [JsonProperty("cloud_drive_folder_id")]
    public string? CloudDriveFolderId { get; set; }

    [JsonProperty("cloud_drive_files")]
    public List<CloudDriveFile> CloudDriveFiles { get; set; } = new();

    // Final Video
    [JsonProperty("final_video")]
    public FinalVideo? FinalVideo { get; set; }

    // Publishing
    [JsonProperty("publish_targets")]
    public List<PublishTarget> PublishTargets { get; set; } = new();

    [JsonProperty("publish_results")]
    public List<PublishResult> PublishResults { get; set; } = new();

    // Progress
    [JsonProperty("progress")]
    public WorkflowProgress Progress { get; set; } = new();

    // Timestamps
    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Content Concept - Input จากผู้ใช้
/// </summary>
public class ContentConcept
{
    [JsonProperty("topic")]
    public string Topic { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonProperty("target_audience")]
    public string TargetAudience { get; set; } = "";

    [JsonProperty("tone")]
    public string Tone { get; set; } = "friendly";

    [JsonProperty("language")]
    public string Language { get; set; } = "th";

    [JsonProperty("video_style")]
    public VideoStyle VideoStyle { get; set; } = VideoStyle.Social;

    [JsonProperty("target_duration_seconds")]
    public int TargetDurationSeconds { get; set; } = 60;

    [JsonProperty("brand_info")]
    public BrandInfo? BrandInfo { get; set; }

    [JsonProperty("call_to_action")]
    public string? CallToAction { get; set; }

    [JsonProperty("product_info")]
    public ProductInfo? ProductInfo { get; set; }
}

/// <summary>
/// Product Info for showcasing
/// </summary>
public class ProductInfo
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("price")]
    public decimal? Price { get; set; }

    [JsonProperty("features")]
    public List<string> Features { get; set; } = new();

    [JsonProperty("images")]
    public List<string> Images { get; set; } = new();

    [JsonProperty("link")]
    public string? Link { get; set; }
}

#endregion

#region Video Script Models

/// <summary>
/// Video Script - สคริปต์วิดีโอที่ AI สร้าง
/// </summary>
public class VideoScript
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("title")]
    public string Title { get; set; } = "";

    [JsonProperty("hook")]
    public string Hook { get; set; } = ""; // Opening hook

    [JsonProperty("scenes")]
    public List<VideoScene> Scenes { get; set; } = new();

    [JsonProperty("call_to_action")]
    public string CallToAction { get; set; } = "";

    [JsonProperty("total_duration_seconds")]
    public int TotalDurationSeconds { get; set; }

    [JsonProperty("hashtags")]
    public List<string> Hashtags { get; set; } = new();

    [JsonProperty("captions")]
    public List<Caption> Captions { get; set; } = new();

    [JsonProperty("provider_used")]
    public string ProviderUsed { get; set; } = "";
}

/// <summary>
/// Video Scene - แต่ละฉากในวิดีโอ
/// </summary>
public class VideoScene
{
    [JsonProperty("scene_number")]
    public int SceneNumber { get; set; }

    [JsonProperty("duration_seconds")]
    public double DurationSeconds { get; set; } = 5;

    [JsonProperty("narration")]
    public string Narration { get; set; } = "";

    [JsonProperty("visual_description")]
    public string VisualDescription { get; set; } = "";

    [JsonProperty("image_prompt")]
    public string ImagePrompt { get; set; } = "";

    [JsonProperty("transition")]
    public string Transition { get; set; } = "fade";

    [JsonProperty("text_overlay")]
    public string? TextOverlay { get; set; }

    [JsonProperty("background_music_mood")]
    public string? BackgroundMusicMood { get; set; }
}

/// <summary>
/// Caption for video
/// </summary>
public class Caption
{
    [JsonProperty("start_time")]
    public double StartTime { get; set; }

    [JsonProperty("end_time")]
    public double EndTime { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("style")]
    public string Style { get; set; } = "default";
}

#endregion

#region Generated Content Models

/// <summary>
/// Generated Image
/// </summary>
public class GeneratedImage
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("scene_number")]
    public int SceneNumber { get; set; }

    [JsonProperty("prompt")]
    public string Prompt { get; set; } = "";

    [JsonProperty("local_path")]
    public string? LocalPath { get; set; }

    [JsonProperty("cloud_drive_file_id")]
    public string? CloudDriveFileId { get; set; }

    [JsonProperty("cloud_drive_url")]
    public string? CloudDriveUrl { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; } = 1920;

    [JsonProperty("height")]
    public int Height { get; set; } = 1080;

    [JsonProperty("provider")]
    public string Provider { get; set; } = "";

    [JsonProperty("generated_at")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Generated Audio
/// </summary>
public class GeneratedAudio
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public AudioType Type { get; set; }

    [JsonProperty("voice_id")]
    public string? VoiceId { get; set; }

    [JsonProperty("voice_name")]
    public string? VoiceName { get; set; }

    [JsonProperty("local_path")]
    public string? LocalPath { get; set; }

    [JsonProperty("cloud_drive_file_id")]
    public string? CloudDriveFileId { get; set; }

    [JsonProperty("cloud_drive_url")]
    public string? CloudDriveUrl { get; set; }

    [JsonProperty("duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonProperty("narration_segments")]
    public List<NarrationSegment> NarrationSegments { get; set; } = new();

    [JsonProperty("background_music")]
    public BackgroundMusic? BackgroundMusic { get; set; }

    [JsonProperty("provider")]
    public string Provider { get; set; } = "";

    [JsonProperty("generated_at")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Narration Segment
/// </summary>
public class NarrationSegment
{
    [JsonProperty("scene_number")]
    public int SceneNumber { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("start_time")]
    public double StartTime { get; set; }

    [JsonProperty("end_time")]
    public double EndTime { get; set; }

    [JsonProperty("audio_file_path")]
    public string? AudioFilePath { get; set; }
}

/// <summary>
/// Background Music
/// </summary>
public class BackgroundMusic
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("mood")]
    public string Mood { get; set; } = "";

    [JsonProperty("file_path")]
    public string? FilePath { get; set; }

    [JsonProperty("volume")]
    public double Volume { get; set; } = 0.3;

    [JsonProperty("is_royalty_free")]
    public bool IsRoyaltyFree { get; set; } = true;
}

#endregion

#region Final Video Models

/// <summary>
/// Final Video
/// </summary>
public class FinalVideo
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("title")]
    public string Title { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    [JsonProperty("local_path")]
    public string? LocalPath { get; set; }

    [JsonProperty("cloud_drive_file_id")]
    public string? CloudDriveFileId { get; set; }

    [JsonProperty("cloud_drive_url")]
    public string? CloudDriveUrl { get; set; }

    [JsonProperty("duration_seconds")]
    public double DurationSeconds { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; } = 1920;

    [JsonProperty("height")]
    public int Height { get; set; } = 1080;

    [JsonProperty("format")]
    public string Format { get; set; } = "mp4";

    [JsonProperty("file_size_bytes")]
    public long FileSizeBytes { get; set; }

    [JsonProperty("thumbnail_path")]
    public string? ThumbnailPath { get; set; }

    [JsonProperty("has_watermark")]
    public bool HasWatermark { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Publishing Models

/// <summary>
/// Publish Target
/// </summary>
public class PublishTarget
{
    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("account_id")]
    public string AccountId { get; set; } = "";

    [JsonProperty("account_name")]
    public string AccountName { get; set; } = "";

    [JsonProperty("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [JsonProperty("custom_caption")]
    public string? CustomCaption { get; set; }

    [JsonProperty("custom_hashtags")]
    public List<string>? CustomHashtags { get; set; }
}

/// <summary>
/// Publish Result
/// </summary>
public class PublishResult
{
    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("post_id")]
    public string? PostId { get; set; }

    [JsonProperty("post_url")]
    public string? PostUrl { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("published_at")]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

#endregion

#region Progress Tracking

/// <summary>
/// Workflow Progress
/// </summary>
public class WorkflowProgress
{
    [JsonProperty("overall_percentage")]
    public int OverallPercentage { get; set; }

    [JsonProperty("current_step")]
    public string CurrentStep { get; set; } = "";

    [JsonProperty("current_step_percentage")]
    public int CurrentStepPercentage { get; set; }

    [JsonProperty("steps")]
    public List<WorkflowStep> Steps { get; set; } = new()
    {
        new WorkflowStep { Name = "Content Generation", Weight = 20 },
        new WorkflowStep { Name = "Image Generation", Weight = 25 },
        new WorkflowStep { Name = "Audio Generation", Weight = 15 },
        new WorkflowStep { Name = "Cloud Upload", Weight = 10 },
        new WorkflowStep { Name = "Video Assembly", Weight = 20 },
        new WorkflowStep { Name = "Publishing", Weight = 10 }
    };

    [JsonProperty("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonProperty("estimated_completion")]
    public DateTime? EstimatedCompletion { get; set; }

    [JsonProperty("logs")]
    public List<ProgressLog> Logs { get; set; } = new();

    public void UpdateStep(string stepName, int percentage, string? message = null)
    {
        var step = Steps.FirstOrDefault(s => s.Name == stepName);
        if (step != null)
        {
            step.Percentage = percentage;
            step.IsComplete = percentage >= 100;
            step.IsActive = percentage > 0 && percentage < 100;
        }

        CurrentStep = stepName;
        CurrentStepPercentage = percentage;
        CalculateOverallProgress();

        if (!string.IsNullOrEmpty(message))
        {
            Logs.Add(new ProgressLog { Step = stepName, Message = message });
        }
    }

    public void CompleteStep(string stepName)
    {
        UpdateStep(stepName, 100, $"{stepName} completed");
    }

    private void CalculateOverallProgress()
    {
        var totalWeight = Steps.Sum(s => s.Weight);
        var completedWeight = Steps.Sum(s => (s.Percentage / 100.0) * s.Weight);
        OverallPercentage = (int)((completedWeight / totalWeight) * 100);
    }
}

/// <summary>
/// Workflow Step
/// </summary>
public class WorkflowStep
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("weight")]
    public int Weight { get; set; }

    [JsonProperty("percentage")]
    public int Percentage { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; }

    [JsonProperty("is_complete")]
    public bool IsComplete { get; set; }
}

/// <summary>
/// Progress Log Entry
/// </summary>
public class ProgressLog
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("step")]
    public string Step { get; set; } = "";

    [JsonProperty("message")]
    public string Message { get; set; } = "";

    [JsonProperty("level")]
    public string Level { get; set; } = "info";
}

#endregion

#region Request/Response Models

/// <summary>
/// Create Workflow Request
/// </summary>
public class CreateWorkflowRequest
{
    [JsonProperty("user_id")]
    public string UserId { get; set; } = "";

    [JsonProperty("concept")]
    public ContentConcept Concept { get; set; } = new();

    [JsonProperty("publish_targets")]
    public List<PublishTarget> PublishTargets { get; set; } = new();

    [JsonProperty("auto_publish")]
    public bool AutoPublish { get; set; } = true;
}

/// <summary>
/// Workflow Status Response
/// </summary>
public class WorkflowStatusResponse
{
    [JsonProperty("workflow_id")]
    public string WorkflowId { get; set; } = "";

    [JsonProperty("status")]
    public ContentWorkflowStatus Status { get; set; }

    [JsonProperty("progress")]
    public WorkflowProgress Progress { get; set; } = new();

    [JsonProperty("final_video")]
    public FinalVideo? FinalVideo { get; set; }

    [JsonProperty("publish_results")]
    public List<PublishResult> PublishResults { get; set; } = new();

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Quota Check Response
/// </summary>
public class QuotaCheckResponse
{
    [JsonProperty("can_create")]
    public bool CanCreate { get; set; }

    [JsonProperty("remaining_videos")]
    public int RemainingVideos { get; set; }

    [JsonProperty("max_duration_seconds")]
    public int MaxDurationSeconds { get; set; }

    [JsonProperty("max_images")]
    public int MaxImages { get; set; }

    [JsonProperty("allowed_platforms")]
    public List<SocialPlatform> AllowedPlatforms { get; set; } = new();

    [JsonProperty("has_watermark")]
    public bool HasWatermark { get; set; }

    [JsonProperty("package_name")]
    public string PackageName { get; set; } = "";

    [JsonProperty("reason")]
    public string? Reason { get; set; }
}

#endregion
