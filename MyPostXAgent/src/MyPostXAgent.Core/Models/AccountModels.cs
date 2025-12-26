using Newtonsoft.Json;

namespace MyPostXAgent.Core.Models;

/// <summary>
/// Social Account - บัญชี Social Media
/// </summary>
public class SocialAccount
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("account_name")]
    public string AccountName { get; set; } = "";

    [JsonProperty("account_id")]
    public string? AccountId { get; set; }

    [JsonProperty("display_name")]
    public string? DisplayName { get; set; }

    [JsonProperty("profile_url")]
    public string? ProfileUrl { get; set; }

    [JsonProperty("avatar_url")]
    public string? AvatarUrl { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("daily_post_count")]
    public int DailyPostCount { get; set; }

    [JsonProperty("daily_limit")]
    public int DailyLimit { get; set; } = 50;

    [JsonProperty("last_post_at")]
    public DateTime? LastPostAt { get; set; }

    [JsonProperty("cooldown_until")]
    public DateTime? CooldownUntil { get; set; }

    [JsonProperty("health_status")]
    public AccountHealthStatus HealthStatus { get; set; } = AccountHealthStatus.Healthy;

    [JsonProperty("last_error")]
    public string? LastError { get; set; }

    [JsonProperty("total_posts")]
    public int TotalPosts { get; set; }

    [JsonProperty("success_count")]
    public int SuccessCount { get; set; }

    [JsonProperty("failure_count")]
    public int FailureCount { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public bool IsInCooldown => CooldownUntil.HasValue && CooldownUntil.Value > DateTime.UtcNow;

    public bool CanPost => IsActive && !IsInCooldown &&
                          HealthStatus == AccountHealthStatus.Healthy &&
                          DailyPostCount < DailyLimit;

    public double SuccessRate => (SuccessCount + FailureCount) > 0
        ? (double)SuccessCount / (SuccessCount + FailureCount)
        : 1.0;
}

/// <summary>
/// Account Credentials - ข้อมูลสำหรับ Login (Encrypted)
/// </summary>
public class AccountCredentials
{
    [JsonProperty("account_id")]
    public int AccountId { get; set; }

    [JsonProperty("username")]
    public string? Username { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    [JsonProperty("password")]
    public string Password { get; set; } = "";

    [JsonProperty("two_factor_secret")]
    public string? TwoFactorSecret { get; set; }

    [JsonProperty("access_token")]
    public string? AccessToken { get; set; }

    [JsonProperty("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonProperty("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }

    [JsonProperty("cookies")]
    public string? CookiesJson { get; set; }

    [JsonProperty("local_storage")]
    public string? LocalStorageJson { get; set; }

    public bool HasValidToken => !string.IsNullOrEmpty(AccessToken) &&
                                 (!TokenExpiresAt.HasValue || TokenExpiresAt.Value > DateTime.UtcNow);
}

/// <summary>
/// Account Pool - กลุ่มของ Accounts สำหรับ Rotation
/// </summary>
public class AccountPool
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("platform")]
    public SocialPlatform Platform { get; set; }

    [JsonProperty("rotation_strategy")]
    public RotationStrategy RotationStrategy { get; set; } = RotationStrategy.RoundRobin;

    [JsonProperty("cooldown_minutes")]
    public int CooldownMinutes { get; set; } = 30;

    [JsonProperty("max_posts_per_day")]
    public int MaxPostsPerDay { get; set; } = 50;

    [JsonProperty("auto_failover")]
    public bool AutoFailover { get; set; } = true;

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("members")]
    public List<AccountPoolMember> Members { get; set; } = new();

    public int ActiveMemberCount => Members.Count(m => m.IsActive);
}

/// <summary>
/// Account Pool Member - สมาชิกใน Pool
/// </summary>
public class AccountPoolMember
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("pool_id")]
    public int PoolId { get; set; }

    [JsonProperty("account_id")]
    public int AccountId { get; set; }

    [JsonProperty("priority")]
    public int Priority { get; set; } = 0;

    [JsonProperty("weight")]
    public int Weight { get; set; } = 100;

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("posts_today")]
    public int PostsToday { get; set; }

    [JsonProperty("total_posts")]
    public int TotalPosts { get; set; }

    [JsonProperty("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [JsonProperty("cooldown_until")]
    public DateTime? CooldownUntil { get; set; }

    [JsonProperty("consecutive_failures")]
    public int ConsecutiveFailures { get; set; }

    // Navigation property
    [JsonIgnore]
    public SocialAccount? Account { get; set; }
}

/// <summary>
/// Account Status Log - ประวัติสถานะ Account
/// </summary>
public class AccountStatusLog
{
    [JsonProperty("id")]
    public int Id { get; set; }

    [JsonProperty("account_id")]
    public int AccountId { get; set; }

    [JsonProperty("pool_id")]
    public int? PoolId { get; set; }

    [JsonProperty("event_type")]
    public AccountEventType EventType { get; set; }

    [JsonProperty("post_id")]
    public int? PostId { get; set; }

    [JsonProperty("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonProperty("metadata")]
    public string? MetadataJson { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AccountEventType
{
    PostSuccess,
    PostFailure,
    RateLimited,
    Cooldown,
    Recovered,
    Blocked,
    Banned,
    LoginSuccess,
    LoginFailure
}
