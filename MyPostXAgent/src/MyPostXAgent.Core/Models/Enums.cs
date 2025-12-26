namespace MyPostXAgent.Core.Models;

/// <summary>
/// Social Media Platforms ที่รองรับ
/// </summary>
public enum SocialPlatform
{
    Facebook,
    Instagram,
    TikTok,
    Twitter,
    Line,
    YouTube,
    Threads,
    LinkedIn,
    Pinterest
}

/// <summary>
/// ประเภท License
/// </summary>
public enum LicenseType
{
    Demo,       // ทดลองใช้ 3 วัน
    Monthly,    // รายเดือน
    Yearly,     // รายปี
    Lifetime    // ตลอดชีพ
}

/// <summary>
/// สถานะ License
/// </summary>
public enum LicenseStatus
{
    Active,     // ใช้งานได้
    Expired,    // หมดอายุ
    Revoked,    // ถูกยกเลิก
    Suspended   // ถูกระงับชั่วคราว
}

/// <summary>
/// สถานะ Demo
/// </summary>
public enum DemoStatus
{
    NotStarted,     // ยังไม่เริ่ม
    Active,         // กำลังใช้งาน
    Expired,        // หมดอายุ
    Blocked         // ถูกบล็อค (เคยใช้แล้ว)
}

/// <summary>
/// ประเภท Content
/// </summary>
public enum ContentType
{
    Text,
    Image,
    Video,
    Carousel,
    Story,
    Reel,
    Short
}

/// <summary>
/// ประเภท Task
/// </summary>
public enum TaskType
{
    GenerateContent,
    GenerateImage,
    GenerateVideo,
    GenerateMusic,
    EditVideo,
    PostContent,
    SchedulePost,
    SearchGroups,
    JoinGroup,
    PostToGroup,
    MonitorComments,
    ReplyComment,
    AnalyzeMetrics
}

/// <summary>
/// สถานะ Task
/// </summary>
public enum TaskStatus
{
    Pending,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
    Retrying
}

/// <summary>
/// สถานะ Post
/// </summary>
public enum PostStatus
{
    Draft,
    Scheduled,
    Posting,
    Posted,
    Failed,
    Cancelled
}

/// <summary>
/// สถานะ Account
/// </summary>
public enum AccountHealthStatus
{
    Healthy,        // ปกติ
    Warning,        // มีปัญหาเล็กน้อย
    Cooldown,       // พักชั่วคราว
    RateLimited,    // ถูกจำกัด rate
    Blocked,        // ถูกบล็อค
    Banned          // ถูกแบน
}

/// <summary>
/// AI Provider สำหรับ Content Generation
/// </summary>
public enum AIProvider
{
    Ollama,         // Local (Free)
    Gemini,         // Google (Free tier)
    OpenAI,         // Paid (GPT-4, GPT-3.5)
    Claude          // Anthropic Claude (Paid)
}

/// <summary>
/// AI Provider สำหรับ Image Generation
/// </summary>
public enum ImageAIProvider
{
    StableDiffusion,    // Local
    ComfyUI,            // Local
    HuggingFace,        // Free
    Leonardo,           // Free tier
    DallE               // Paid
}

/// <summary>
/// AI Provider สำหรับ Video Generation
/// </summary>
public enum VideoAIProvider
{
    WanAI_Local,    // Local GPU
    Freepik,        // Web automation
    Runway,         // API
    PikaLabs,       // API
    LumaAI          // API
}

/// <summary>
/// AI Provider สำหรับ Music Generation
/// </summary>
public enum MusicAIProvider
{
    Suno,           // Web automation / API
    StableAudio,    // API
    AudioCraft,     // Local
    MusicGen        // Local
}

/// <summary>
/// Rotation Strategy สำหรับ Account Pool
/// </summary>
public enum RotationStrategy
{
    RoundRobin,     // หมุนเวียนตามลำดับ
    Random,         // สุ่ม
    LeastUsed,      // ใช้น้อยสุดก่อน
    Priority        // ตาม priority
}

/// <summary>
/// Video Aspect Ratio
/// </summary>
public enum AspectRatio
{
    Landscape_16_9,     // 16:9 (YouTube)
    Portrait_9_16,      // 9:16 (TikTok, Reels)
    Square_1_1,         // 1:1 (Instagram)
    Standard_4_3,       // 4:3
    Cinematic_21_9      // 21:9
}

/// <summary>
/// Video Quality
/// </summary>
public enum VideoQuality
{
    SD_480p,
    HD_720p,
    FHD_1080p,
    UHD_4K
}
