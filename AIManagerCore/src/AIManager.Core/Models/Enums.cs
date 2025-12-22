namespace AIManager.Core.Models;

/// <summary>
/// Supported social media platforms (Thailand)
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
/// Types of tasks the system can handle
/// </summary>
public enum TaskType
{
    // Content Generation
    GenerateContent,
    GenerateImage,

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
