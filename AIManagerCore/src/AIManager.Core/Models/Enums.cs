namespace AIManager.Core.Models;

/// <summary>
/// Supported social media platforms (Thailand) + AI Media Services
/// </summary>
public enum SocialPlatform
{
    // Social Media Platforms
    Facebook,
    Instagram,
    TikTok,
    Twitter,
    Line,
    YouTube,
    Threads,
    LinkedIn,
    Pinterest,

    // AI Video Generation Platforms
    Freepik,       // Freepik Pikaso AI (PRIMARY)
    Runway,        // Runway ML (Fallback)
    PikaLabs,      // Pika Labs (Fallback)
    LumaAI,        // Luma Dream Machine (Fallback)

    // AI Music Generation Platforms
    SunoAI         // Suno AI Music Generator
}

/// <summary>
/// Types of tasks the system can handle
/// </summary>
public enum TaskType
{
    // Content Generation
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
    ConvertVideoFormat,         // แปลงรูปแบบวีดีโอ
    ResizeVideo,                // ปรับขนาดวีดีโอ

    // Posting
    PostContent,
    SchedulePost,
    PostToGroup,
    PostToMultipleGroups,

    // Metrics & Analytics
    AnalyzeMetrics,
    MonitorEngagement,
    AnalyzeViralPotential,
    TrackTrendingKeywords,

    // Comment Management
    FetchComments,
    ReplyToComment,
    AutoReplyComments,
    AnalyzeCommentSentiment,

    // Account Management
    DeletePost,
    RefreshToken,

    // Group Discovery
    SearchGroups,
    JoinGroup,
    AnalyzeGroupActivity
}

/// <summary>
/// Sentiment types for comment analysis
/// </summary>
public enum SentimentType
{
    Positive,
    Neutral,
    Negative,
    Question,
    Complaint,
    Praise
}

/// <summary>
/// Comment priority levels
/// </summary>
public enum CommentPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Task execution status
/// </summary>
public enum TaskStatus
{
    Pending,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// AI provider types
/// </summary>
public enum AIProvider
{
    // Text Generation
    OpenAI,        // Paid
    Anthropic,     // Paid
    Google,        // Free tier available
    Ollama,        // Free - Local

    // Image Generation
    DallE,         // Paid
    StableDiffusion, // Free - Self-hosted
    Leonardo,      // Free tier available
    BingImage      // Free
}

/// <summary>
/// Content types
/// </summary>
public enum ContentType
{
    Text,
    Image,
    Video,
    Carousel,
    Story,
    Reel
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
/// Aspect ratios for video generation (NEW)
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
    Low_480p,          // 480p - Low quality
    Medium_720p,       // 720p - Medium quality
    High_1080p,        // 1080p - Full HD
    Ultra_4K           // 4K - Ultra HD
}

/// <summary>
/// Music genres for AI generation (NEW)
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
    Acoustic,
    Country,
    Blues,
    Reggae,
    Metal,
    Folk,
    RnB,
    Dance,
    Indie,
    Soul,
    Funk
}

/// <summary>
/// Music mood categories (NEW)
/// </summary>
public enum MusicMood
{
    Happy,
    Sad,
    Energetic,
    Calm,
    Romantic,
    Aggressive,
    Mysterious,
    Epic,
    Peaceful,
    Dark,
    Uplifting,
    Melancholic
}
