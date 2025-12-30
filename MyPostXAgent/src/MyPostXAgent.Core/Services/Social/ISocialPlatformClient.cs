using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.Social;

/// <summary>
/// Interface for social media platform clients
/// </summary>
public interface ISocialPlatformClient
{
    SocialPlatform Platform { get; }
    
    /// <summary>
    /// Check if client is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Authenticate using OAuth or credentials
    /// </summary>
    Task<AuthResult> AuthenticateAsync(AccountCredentials credentials, CancellationToken ct = default);
    
    /// <summary>
    /// Publish a post
    /// </summary>
    Task<PostResult> PublishPostAsync(Post post, CancellationToken ct = default);
    
    /// <summary>
    /// Get user profile info
    /// </summary>
    Task<ProfileInfo?> GetProfileInfoAsync(CancellationToken ct = default);
}

/// <summary>
/// Authentication result
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? UserId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Post publishing result
/// </summary>
public class PostResult
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// User profile information
/// </summary>
public class ProfileInfo
{
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? ProfileUrl { get; set; }
    public string? AvatarUrl { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
}
