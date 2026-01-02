using System.Text.Json.Serialization;

namespace AIManager.Core.Models;

// ═══════════════════════════════════════════════════════════════════════════
// GPU PROVIDER TYPES - รองรับหลาย Provider
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// ประเภท GPU Provider ที่รองรับ (ฟรีเป็นหลัก)
/// </summary>
public enum GpuProviderType
{
    GoogleColab,       // Free: ~12 hrs/day, T4 GPU
    Kaggle,            // Free: 30 hrs/week, T4/P100 GPU
    LightningAI,       // Free: 22 GPU-hrs/month, A10G
    HuggingFaceSpaces, // Free: ZeroGPU (shared)
    HuggingFace,       // Alias for HuggingFaceSpaces
    PaperspaceGradient,// Free: 6 hrs/session, M4000
    PaperSpace,        // Alias for PaperspaceGradient
    SaturnCloud,       // Free: 30 hrs/month
    Gradient,          // Free: 6 hrs/session
    RunPod,            // Paid: On-demand GPU
    Vast,              // Paid: Marketplace GPU
    Lambda,            // Paid: Lambda Labs
    Local,             // Local GPU (no quota)
    Custom             // Custom provider
}

/// <summary>
/// ข้อมูล GPU Provider
/// </summary>
public class GpuProviderInfo
{
    [JsonPropertyName("type")]
    public GpuProviderType Type { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("signup_url")]
    public string SignupUrl { get; set; } = string.Empty;

    [JsonPropertyName("login_url")]
    public string LoginUrl { get; set; } = string.Empty;

    [JsonPropertyName("console_url")]
    public string ConsoleUrl { get; set; } = string.Empty;

    [JsonPropertyName("gpu_types")]
    public List<string> GpuTypes { get; set; } = new();

    [JsonPropertyName("free_quota")]
    public string FreeQuota { get; set; } = string.Empty;

    [JsonPropertyName("quota_reset")]
    public string QuotaReset { get; set; } = string.Empty;

    [JsonPropertyName("requires_api_key")]
    public bool RequiresApiKey { get; set; }

    [JsonPropertyName("requires_phone")]
    public bool RequiresPhone { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = "Gpu";

    [JsonPropertyName("color")]
    public string Color { get; set; } = "#8B5CF6";

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    /// <summary>
    /// ดึงข้อมูล Provider ทั้งหมดที่รองรับ
    /// </summary>
    public static List<GpuProviderInfo> GetAllProviders() => new()
    {
        new GpuProviderInfo
        {
            Type = GpuProviderType.GoogleColab,
            Name = "Google Colab",
            Description = "Free GPU จาก Google - T4 15GB VRAM",
            SignupUrl = "https://accounts.google.com/signup",
            LoginUrl = "https://accounts.google.com/signin",
            ConsoleUrl = "https://colab.research.google.com",
            GpuTypes = new() { "T4 (15GB)", "P100 (16GB)", "V100 (16GB)", "A100 (40GB)" },
            FreeQuota = "~12 ชม./วัน",
            QuotaReset = "รายวัน",
            RequiresApiKey = false,
            RequiresPhone = false,
            Icon = "GoogleCloud",
            Color = "#4285F4",
            Priority = 1
        },
        new GpuProviderInfo
        {
            Type = GpuProviderType.Kaggle,
            Name = "Kaggle Notebooks",
            Description = "Free GPU จาก Kaggle - T4/P100",
            SignupUrl = "https://www.kaggle.com/account/register",
            LoginUrl = "https://www.kaggle.com/account/login",
            ConsoleUrl = "https://www.kaggle.com/code",
            GpuTypes = new() { "T4 (16GB)", "P100 (16GB)", "TPU v3-8" },
            FreeQuota = "30 ชม./สัปดาห์",
            QuotaReset = "รายสัปดาห์",
            RequiresApiKey = true,
            RequiresPhone = true,
            Icon = "AlphaKBox",
            Color = "#20BEFF",
            Priority = 2
        },
        new GpuProviderInfo
        {
            Type = GpuProviderType.LightningAI,
            Name = "Lightning AI Studios",
            Description = "Free A10G GPU - 24GB VRAM",
            SignupUrl = "https://lightning.ai/sign-up",
            LoginUrl = "https://lightning.ai/sign-in",
            ConsoleUrl = "https://lightning.ai/studios",
            GpuTypes = new() { "A10G (24GB)", "L4 (24GB)" },
            FreeQuota = "22 GPU-ชม./เดือน",
            QuotaReset = "รายเดือน",
            RequiresApiKey = false,
            RequiresPhone = false,
            Icon = "Lightning",
            Color = "#792EE5",
            Priority = 3
        },
        new GpuProviderInfo
        {
            Type = GpuProviderType.HuggingFaceSpaces,
            Name = "Hugging Face Spaces",
            Description = "ZeroGPU - Shared GPU สำหรับ inference",
            SignupUrl = "https://huggingface.co/join",
            LoginUrl = "https://huggingface.co/login",
            ConsoleUrl = "https://huggingface.co/spaces",
            GpuTypes = new() { "ZeroGPU (Shared)", "T4 (Paid)", "A10G (Paid)" },
            FreeQuota = "Shared (ไม่จำกัด)",
            QuotaReset = "-",
            RequiresApiKey = true,
            RequiresPhone = false,
            Icon = "RobotExcited",
            Color = "#FFD21E",
            Priority = 4
        },
        new GpuProviderInfo
        {
            Type = GpuProviderType.PaperspaceGradient,
            Name = "Paperspace Gradient",
            Description = "Free GPU notebooks - M4000 8GB",
            SignupUrl = "https://console.paperspace.com/signup",
            LoginUrl = "https://console.paperspace.com/login",
            ConsoleUrl = "https://console.paperspace.com/gradient",
            GpuTypes = new() { "M4000 (8GB)", "P4000 (8GB)", "P5000 (16GB)" },
            FreeQuota = "6 ชม./session",
            QuotaReset = "ต่อ session",
            RequiresApiKey = true,
            RequiresPhone = false,
            Icon = "Cube",
            Color = "#7928CA",
            Priority = 5
        },
        new GpuProviderInfo
        {
            Type = GpuProviderType.SaturnCloud,
            Name = "Saturn Cloud",
            Description = "Free GPU compute - T4 GPU",
            SignupUrl = "https://saturncloud.io/signup",
            LoginUrl = "https://app.community.saturnenterprise.io/auth/login",
            ConsoleUrl = "https://app.community.saturnenterprise.io",
            GpuTypes = new() { "T4 (16GB)", "V100 (16GB)" },
            FreeQuota = "30 ชม./เดือน",
            QuotaReset = "รายเดือน",
            RequiresApiKey = false,
            RequiresPhone = false,
            Icon = "Planet",
            Color = "#E91E63",
            Priority = 6
        }
    };
}

/// <summary>
/// Account สำหรับ GPU Provider (ใช้ได้กับทุก provider)
/// </summary>
public class GpuProviderAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("provider_type")]
    public GpuProviderType ProviderType { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("status")]
    public GpuAccountStatus Status { get; set; } = GpuAccountStatus.Active;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    // Auth tokens
    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }

    // Quota
    [JsonPropertyName("quota_used_minutes")]
    public int QuotaUsedMinutes { get; set; }

    [JsonPropertyName("quota_limit_minutes")]
    public int QuotaLimitMinutes { get; set; } = 720;

    [JsonPropertyName("quota_reset_time")]
    public DateTime? QuotaResetTime { get; set; }

    [JsonPropertyName("cooldown_until")]
    public DateTime? CooldownUntil { get; set; }

    // Session
    [JsonPropertyName("current_session_id")]
    public string? CurrentSessionId { get; set; }

    [JsonPropertyName("session_start_time")]
    public DateTime? SessionStartTime { get; set; }

    // Stats
    [JsonPropertyName("total_sessions")]
    public int TotalSessions { get; set; }

    [JsonPropertyName("success_count")]
    public int SuccessCount { get; set; }

    [JsonPropertyName("failure_count")]
    public int FailureCount { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Computed
    [JsonIgnore]
    public int RemainingQuotaMinutes => Math.Max(0, QuotaLimitMinutes - QuotaUsedMinutes);

    [JsonIgnore]
    public double QuotaUsagePercent => QuotaLimitMinutes > 0
        ? (double)QuotaUsedMinutes / QuotaLimitMinutes * 100 : 0;

    [JsonIgnore]
    public bool IsAvailable =>
        IsEnabled &&
        Status == GpuAccountStatus.Active &&
        RemainingQuotaMinutes > 30 &&
        (CooldownUntil == null || CooldownUntil <= DateTime.UtcNow);
}

public enum GpuAccountStatus
{
    Active,
    InUse,
    Cooldown,
    QuotaExhausted,
    Suspended,
    NeedsReauth,
    NeedsSetup,
    Error
}

// ═══════════════════════════════════════════════════════════════════════════
// AI-GUIDED SETUP - สำหรับสอนการสมัคร
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// ขั้นตอนการ Setup
/// </summary>
public class SetupStep
{
    [JsonPropertyName("step_number")]
    public int StepNumber { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("action_type")]
    public SetupActionType ActionType { get; set; }

    [JsonPropertyName("wait_for_selector")]
    public string? WaitForSelector { get; set; }

    [JsonPropertyName("highlight_selectors")]
    public List<string> HighlightSelectors { get; set; } = new();

    [JsonPropertyName("ai_hints")]
    public List<string> AIHints { get; set; } = new();

    [JsonPropertyName("is_optional")]
    public bool IsOptional { get; set; }
}

public enum SetupActionType
{
    Navigate,
    Click,
    FillForm,
    VerifyEmail,
    VerifyPhone,
    WaitForLogin,
    GetApiKey,
    Complete
}

/// <summary>
/// Setup Flow สำหรับแต่ละ Provider
/// </summary>
public class ProviderSetupFlow
{
    [JsonPropertyName("provider_type")]
    public GpuProviderType ProviderType { get; set; }

    [JsonPropertyName("provider_name")]
    public string ProviderName { get; set; } = string.Empty;

    [JsonPropertyName("estimated_time")]
    public string EstimatedTime { get; set; } = "5-10 นาที";

    [JsonPropertyName("requirements")]
    public List<string> Requirements { get; set; } = new();

    [JsonPropertyName("steps")]
    public List<SetupStep> Steps { get; set; } = new();

    /// <summary>
    /// ดึง Setup Flow สำหรับ Provider
    /// </summary>
    public static ProviderSetupFlow GetSetupFlow(GpuProviderType provider) => provider switch
    {
        GpuProviderType.GoogleColab => new ProviderSetupFlow
        {
            ProviderType = GpuProviderType.GoogleColab,
            ProviderName = "Google Colab",
            EstimatedTime = "2-5 นาที",
            Requirements = new() { "Email (Gmail หรืออื่นๆ)", "ไม่ต้องใช้เบอร์โทร" },
            Steps = new()
            {
                new SetupStep
                {
                    StepNumber = 1,
                    Title = "เปิดหน้า Google Colab",
                    Description = "ไปที่หน้า Google Colab เพื่อเริ่มต้นใช้งาน",
                    Url = "https://colab.research.google.com",
                    ActionType = SetupActionType.Navigate,
                    AIHints = new() { "หน้านี้คือ Google Colab - คลิก 'Sign in' มุมขวาบน" }
                },
                new SetupStep
                {
                    StepNumber = 2,
                    Title = "Sign in ด้วย Google Account",
                    Description = "ใช้ Google Account ที่มีอยู่หรือสร้างใหม่",
                    Url = "https://accounts.google.com",
                    ActionType = SetupActionType.WaitForLogin,
                    WaitForSelector = "[data-email]",
                    AIHints = new() { "กรอก email แล้วกด Next", "กรอก password แล้วกด Next" }
                },
                new SetupStep
                {
                    StepNumber = 3,
                    Title = "ทดสอบ GPU",
                    Description = "สร้าง Notebook ใหม่และเปิดใช้ GPU",
                    Url = "https://colab.research.google.com/#create=true",
                    ActionType = SetupActionType.Click,
                    AIHints = new() { "คลิก Runtime > Change runtime type", "เลือก GPU แล้วกด Save" }
                },
                new SetupStep
                {
                    StepNumber = 4,
                    Title = "เสร็จสิ้น!",
                    Description = "พร้อมใช้งาน Google Colab GPU แล้ว",
                    ActionType = SetupActionType.Complete,
                    AIHints = new() { "สามารถใช้ GPU ได้ประมาณ 12 ชม./วัน" }
                }
            }
        },
        GpuProviderType.Kaggle => new ProviderSetupFlow
        {
            ProviderType = GpuProviderType.Kaggle,
            ProviderName = "Kaggle Notebooks",
            EstimatedTime = "5-10 นาที",
            Requirements = new() { "Email", "เบอร์โทรศัพท์ (สำหรับยืนยัน GPU)" },
            Steps = new()
            {
                new SetupStep
                {
                    StepNumber = 1,
                    Title = "สมัครสมาชิก Kaggle",
                    Description = "สร้าง account ใหม่หรือ login ด้วย Google",
                    Url = "https://www.kaggle.com/account/register",
                    ActionType = SetupActionType.Navigate,
                    AIHints = new() { "สามารถ 'Register with Google' ได้เลย", "หรือกรอก email/password เพื่อสมัครใหม่" }
                },
                new SetupStep
                {
                    StepNumber = 2,
                    Title = "ยืนยัน Email",
                    Description = "เช็ค email และคลิกลิงก์ยืนยัน",
                    ActionType = SetupActionType.VerifyEmail,
                    AIHints = new() { "เช็ค inbox หรือ spam folder", "คลิกลิงก์ Verify Email" }
                },
                new SetupStep
                {
                    StepNumber = 3,
                    Title = "ยืนยันเบอร์โทร (สำหรับ GPU)",
                    Description = "ต้องยืนยันเบอร์โทรเพื่อใช้ GPU",
                    Url = "https://www.kaggle.com/settings/account",
                    ActionType = SetupActionType.VerifyPhone,
                    WaitForSelector = "[data-testid='phone-verified']",
                    AIHints = new() { "ไปที่ Settings > Phone", "กรอกเบอร์โทรและรับ OTP" }
                },
                new SetupStep
                {
                    StepNumber = 4,
                    Title = "ดึง API Key",
                    Description = "สร้าง API Token สำหรับเชื่อมต่อ",
                    Url = "https://www.kaggle.com/settings/account",
                    ActionType = SetupActionType.GetApiKey,
                    AIHints = new() { "เลื่อนลงไปที่ 'API'", "คลิก 'Create New Token'", "ไฟล์ kaggle.json จะถูกดาวน์โหลด" }
                },
                new SetupStep
                {
                    StepNumber = 5,
                    Title = "เสร็จสิ้น!",
                    Description = "พร้อมใช้งาน Kaggle GPU แล้ว",
                    ActionType = SetupActionType.Complete,
                    AIHints = new() { "ใช้ GPU ได้ 30 ชม./สัปดาห์", "รองรับ T4, P100, และ TPU" }
                }
            }
        },
        GpuProviderType.LightningAI => new ProviderSetupFlow
        {
            ProviderType = GpuProviderType.LightningAI,
            ProviderName = "Lightning AI Studios",
            EstimatedTime = "3-5 นาที",
            Requirements = new() { "Email หรือ GitHub account" },
            Steps = new()
            {
                new SetupStep
                {
                    StepNumber = 1,
                    Title = "สมัคร Lightning AI",
                    Description = "สมัครด้วย Email หรือ GitHub",
                    Url = "https://lightning.ai/sign-up",
                    ActionType = SetupActionType.Navigate,
                    AIHints = new() { "คลิก 'Sign up with GitHub' สะดวกที่สุด", "หรือกรอก email เพื่อสมัคร" }
                },
                new SetupStep
                {
                    StepNumber = 2,
                    Title = "สร้าง Studio",
                    Description = "สร้าง Studio ใหม่พร้อม GPU",
                    Url = "https://lightning.ai/studios",
                    ActionType = SetupActionType.Click,
                    AIHints = new() { "คลิก '+ New Studio'", "เลือก GPU: A10G หรือ L4" }
                },
                new SetupStep
                {
                    StepNumber = 3,
                    Title = "เสร็จสิ้น!",
                    Description = "พร้อมใช้งาน Lightning AI แล้ว",
                    ActionType = SetupActionType.Complete,
                    AIHints = new() { "ใช้ GPU ได้ 22 ชม./เดือน", "A10G มี VRAM 24GB" }
                }
            }
        },
        _ => new ProviderSetupFlow
        {
            ProviderType = provider,
            ProviderName = provider.ToString(),
            EstimatedTime = "5-10 นาที",
            Requirements = new() { "Email" },
            Steps = new()
            {
                new SetupStep
                {
                    StepNumber = 1,
                    Title = "เปิดหน้าสมัคร",
                    Description = "ไปที่หน้าสมัครสมาชิก",
                    ActionType = SetupActionType.Navigate
                }
            }
        }
    };
}

/// <summary>
/// สถานะ AI Analysis สำหรับหน้าเว็บ
/// </summary>
public class PageAnalysisResult
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("page_type")]
    public string PageType { get; set; } = string.Empty;

    [JsonPropertyName("detected_provider")]
    public GpuProviderType? DetectedProvider { get; set; }

    [JsonPropertyName("current_step")]
    public string CurrentStep { get; set; } = string.Empty;

    [JsonPropertyName("next_action")]
    public string NextAction { get; set; } = string.Empty;

    [JsonPropertyName("action_target")]
    public string? ActionTarget { get; set; }

    [JsonPropertyName("tips")]
    public List<string> Tips { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("is_logged_in")]
    public bool IsLoggedIn { get; set; }

    [JsonPropertyName("has_gpu_access")]
    public bool HasGpuAccess { get; set; }
}

// ═══════════════════════════════════════════════════════════════════════════
// BACKWARD COMPATIBILITY - เก็บ types เดิมไว้
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// สถานะของ Colab Account
/// </summary>
public enum ColabAccountStatus
{
    /// <summary>พร้อมใช้งาน</summary>
    Active,
    /// <summary>กำลังใช้งานอยู่</summary>
    InUse,
    /// <summary>อยู่ในช่วง cooldown</summary>
    Cooldown,
    /// <summary>หมดโควต้าแล้ว</summary>
    QuotaExhausted,
    /// <summary>ถูกระงับชั่วคราว</summary>
    Suspended,
    /// <summary>ต้องยืนยันตัวตนใหม่</summary>
    NeedsReauth,
    /// <summary>เกิดข้อผิดพลาด</summary>
    Error
}

/// <summary>
/// ประเภท GPU ใน Google Colab
/// </summary>
public enum ColabGpuType
{
    None,
    T4,          // Free tier - 15GB VRAM
    P100,        // Pro - 16GB VRAM
    V100,        // Pro - 16GB VRAM
    A100,        // Pro+ - 40GB VRAM
    L4,          // Pro - 24GB VRAM
    TPU          // TPU v2-8
}

/// <summary>
/// ประเภท Colab subscription
/// </summary>
public enum ColabTier
{
    Free,        // ~12 hrs/day, T4 only
    Pro,         // $9.99/mo - longer runtime, better GPUs
    ProPlus      // $49.99/mo - even better, background execution
}

/// <summary>
/// Rotation strategy สำหรับ account pool
/// </summary>
public enum ColabRotationStrategy
{
    /// <summary>วนรอบตามลำดับ</summary>
    RoundRobin,
    /// <summary>เลือก account ที่ใช้น้อยที่สุด</summary>
    LeastUsed,
    /// <summary>เลือกตาม priority</summary>
    Priority,
    /// <summary>สุ่ม</summary>
    Random
}

/// <summary>
/// ข้อมูล Google Colab Account สำหรับ GPU Pool
/// </summary>
public class ColabGpuAccount
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("tier")]
    public ColabTier Tier { get; set; } = ColabTier.Free;

    [JsonPropertyName("status")]
    public ColabAccountStatus Status { get; set; } = ColabAccountStatus.Active;

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100; // Lower = higher priority

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    // ===== Authentication =====
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }

    // ===== Quota Tracking =====
    [JsonPropertyName("daily_quota_used_minutes")]
    public int DailyQuotaUsedMinutes { get; set; }

    [JsonPropertyName("daily_quota_limit_minutes")]
    public int DailyQuotaLimitMinutes { get; set; } = 720; // 12 hours default for free

    [JsonPropertyName("quota_reset_time")]
    public DateTime? QuotaResetTime { get; set; }

    [JsonPropertyName("cooldown_until")]
    public DateTime? CooldownUntil { get; set; }

    // ===== Current Session =====
    [JsonPropertyName("current_session_id")]
    public string? CurrentSessionId { get; set; }

    [JsonPropertyName("session_start_time")]
    public DateTime? SessionStartTime { get; set; }

    [JsonPropertyName("current_gpu_type")]
    public ColabGpuType CurrentGpuType { get; set; } = ColabGpuType.None;

    [JsonPropertyName("notebook_url")]
    public string? NotebookUrl { get; set; }

    // ===== Statistics =====
    [JsonPropertyName("total_sessions")]
    public int TotalSessions { get; set; }

    [JsonPropertyName("success_count")]
    public int SuccessCount { get; set; }

    [JsonPropertyName("failure_count")]
    public int FailureCount { get; set; }

    [JsonPropertyName("consecutive_failures")]
    public int ConsecutiveFailures { get; set; }

    [JsonPropertyName("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ===== Computed Properties =====

    /// <summary>
    /// เหลือโควต้าอีกกี่นาที
    /// </summary>
    [JsonIgnore]
    public int RemainingQuotaMinutes => Math.Max(0, DailyQuotaLimitMinutes - DailyQuotaUsedMinutes);

    /// <summary>
    /// เปอร์เซ็นต์การใช้โควต้า
    /// </summary>
    [JsonIgnore]
    public double QuotaUsagePercent => DailyQuotaLimitMinutes > 0
        ? (double)DailyQuotaUsedMinutes / DailyQuotaLimitMinutes * 100
        : 0;

    /// <summary>
    /// พร้อมใช้งานหรือไม่
    /// </summary>
    [JsonIgnore]
    public bool IsAvailable =>
        IsEnabled &&
        Status == ColabAccountStatus.Active &&
        RemainingQuotaMinutes > 30 && // ต้องเหลืออย่างน้อย 30 นาที
        (CooldownUntil == null || CooldownUntil <= DateTime.UtcNow);

    /// <summary>
    /// Token หมดอายุหรือยัง
    /// </summary>
    [JsonIgnore]
    public bool IsTokenExpired => TokenExpiresAt.HasValue && TokenExpiresAt.Value <= DateTime.UtcNow;

    /// <summary>
    /// Success rate
    /// </summary>
    [JsonIgnore]
    public double SuccessRate => (SuccessCount + FailureCount) > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount) * 100
        : 100;

    /// <summary>
    /// ระยะเวลา session ปัจจุบัน
    /// </summary>
    [JsonIgnore]
    public TimeSpan? CurrentSessionDuration => SessionStartTime.HasValue
        ? DateTime.UtcNow - SessionStartTime.Value
        : null;
}

/// <summary>
/// ข้อมูล Colab Session
/// </summary>
public class ColabSession
{
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("notebook_id")]
    public string? NotebookId { get; set; }

    [JsonPropertyName("notebook_url")]
    public string? NotebookUrl { get; set; }

    [JsonPropertyName("gpu_type")]
    public ColabGpuType GpuType { get; set; }

    [JsonPropertyName("gpu_memory_gb")]
    public int GpuMemoryGB { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("runtime_type")]
    public string RuntimeType { get; set; } = "python3"; // python3, r

    [JsonPropertyName("execution_count")]
    public int ExecutionCount { get; set; }

    [JsonPropertyName("last_execution_at")]
    public DateTime? LastExecutionAt { get; set; }

    [JsonIgnore]
    public TimeSpan Duration => (EndedAt ?? DateTime.UtcNow) - StartedAt;
}

/// <summary>
/// สถานะรวมของ Colab Pool
/// </summary>
public class ColabPoolStatus
{
    [JsonPropertyName("total_accounts")]
    public int TotalAccounts { get; set; }

    [JsonPropertyName("active_accounts")]
    public int ActiveAccounts { get; set; }

    [JsonPropertyName("in_use_accounts")]
    public int InUseAccounts { get; set; }

    [JsonPropertyName("cooldown_accounts")]
    public int CooldownAccounts { get; set; }

    [JsonPropertyName("exhausted_accounts")]
    public int ExhaustedAccounts { get; set; }

    [JsonPropertyName("error_accounts")]
    public int ErrorAccounts { get; set; }

    [JsonPropertyName("total_remaining_quota_minutes")]
    public int TotalRemainingQuotaMinutes { get; set; }

    [JsonPropertyName("current_session")]
    public ColabSession? CurrentSession { get; set; }

    [JsonPropertyName("next_available_account")]
    public string? NextAvailableAccountEmail { get; set; }

    [JsonPropertyName("is_pool_available")]
    public bool IsPoolAvailable => ActiveAccounts > 0 || InUseAccounts > 0;

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ข้อมูล GPU ที่ได้รับจาก Colab
/// </summary>
public class ColabGpuInfo
{
    [JsonPropertyName("gpu_name")]
    public string GpuName { get; set; } = string.Empty;

    [JsonPropertyName("gpu_type")]
    public ColabGpuType GpuType { get; set; }

    [JsonPropertyName("memory_total_gb")]
    public double MemoryTotalGB { get; set; }

    [JsonPropertyName("memory_used_gb")]
    public double MemoryUsedGB { get; set; }

    [JsonPropertyName("memory_free_gb")]
    public double MemoryFreeGB { get; set; }

    [JsonPropertyName("cuda_version")]
    public string? CudaVersion { get; set; }

    [JsonPropertyName("driver_version")]
    public string? DriverVersion { get; set; }

    [JsonPropertyName("utilization_percent")]
    public double UtilizationPercent { get; set; }

    [JsonPropertyName("temperature_celsius")]
    public int? TemperatureCelsius { get; set; }
}

/// <summary>
/// การตั้งค่า Colab Pool
/// </summary>
public class ColabPoolSettings
{
    [JsonPropertyName("rotation_strategy")]
    public ColabRotationStrategy RotationStrategy { get; set; } = ColabRotationStrategy.Priority;

    [JsonPropertyName("cooldown_minutes")]
    public int CooldownMinutes { get; set; } = 60;

    [JsonPropertyName("auto_failover")]
    public bool AutoFailover { get; set; } = true;

    [JsonPropertyName("quota_threshold_percent")]
    public int QuotaThresholdPercent { get; set; } = 90;

    [JsonPropertyName("auto_rotate_on_quota_low")]
    public bool AutoRotateOnQuotaLow { get; set; } = true;

    [JsonPropertyName("max_consecutive_failures")]
    public int MaxConsecutiveFailures { get; set; } = 3;

    [JsonPropertyName("session_timeout_minutes")]
    public int SessionTimeoutMinutes { get; set; } = 90;

    [JsonPropertyName("preferred_gpu_type")]
    public ColabGpuType PreferredGpuType { get; set; } = ColabGpuType.T4;

    [JsonPropertyName("enable_notifications")]
    public bool EnableNotifications { get; set; } = true;
}

/// <summary>
/// Event สำหรับแจ้งเตือนการเปลี่ยนแปลง
/// </summary>
public class ColabPoolEvent
{
    public ColabPoolEventType EventType { get; set; }
    public string AccountId { get; set; } = string.Empty;
    public string? AccountEmail { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Metadata { get; set; }
}

public enum ColabPoolEventType
{
    AccountAdded,
    AccountRemoved,
    AccountRotated,
    SessionStarted,
    SessionEnded,
    QuotaLow,
    QuotaExhausted,
    AccountError,
    AccountRecovered,
    PoolEmpty
}
