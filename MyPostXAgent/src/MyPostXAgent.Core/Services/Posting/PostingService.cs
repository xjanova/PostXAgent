using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.Social;

namespace MyPostXAgent.Core.Services.Posting;

/// <summary>
/// Main service for publishing posts to social media platforms
/// </summary>
public class PostingService
{
    private readonly DatabaseService _database;
    private readonly Dictionary<SocialPlatform, ISocialPlatformClient> _clients = new();

    public PostingService(DatabaseService database)
    {
        _database = database;
    }

    /// <summary>
    /// Initialize platform clients with credentials from database
    /// </summary>
    public async Task InitializeClientsAsync(CancellationToken ct = default)
    {
        var accounts = await _database.GetSocialAccountsAsync(null, ct);

        foreach (var account in accounts.Where(a => a.IsActive))
        {
            await InitializeClientForAccountAsync(account, ct);
        }
    }

    private async Task InitializeClientForAccountAsync(SocialAccount account, CancellationToken ct)
    {
        var credentials = await _database.GetAccountCredentialsAsync(account.Id, ct);
        if (credentials == null) return;

        ISocialPlatformClient? client = account.Platform switch
        {
            SocialPlatform.Facebook => new FacebookClient(credentials.AccessToken),
            SocialPlatform.Instagram => new InstagramClient(credentials.AccessToken),
            _ => null
        };

        if (client != null)
        {
            _clients[account.Platform] = client;
        }
    }

    /// <summary>
    /// Publish a post to its target platform
    /// </summary>
    public async Task<PostResult> PublishPostAsync(Post post, CancellationToken ct = default)
    {
        if (!_clients.TryGetValue(post.Platform, out var client))
        {
            return new PostResult
            {
                Success = false,
                ErrorMessage = $"No client configured for {post.Platform}"
            };
        }

        try
        {
            // Update post status
            post.Status = PostStatus.Posting;
            await _database.UpdatePostAsync(post, ct);

            // Publish
            var result = await client.PublishPostAsync(post, ct);

            // Update post with result
            if (result.Success)
            {
                post.Status = PostStatus.Posted;
                post.PostUrl = result.PostUrl;
                post.PostedAt = DateTime.UtcNow;
            }
            else
            {
                post.Status = PostStatus.Failed;
                post.LastError = result.ErrorMessage;
            }

            await _database.UpdatePostAsync(post, ct);

            return result;
        }
        catch (Exception ex)
        {
            post.Status = PostStatus.Failed;
            post.LastError = ex.Message;
            await _database.UpdatePostAsync(post, ct);

            return new PostResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Publish post to multiple platforms
    /// </summary>
    public async Task<Dictionary<SocialPlatform, PostResult>> PublishToMultiplePlatformsAsync(
        string content,
        List<string> mediaPaths,
        List<SocialPlatform> platforms,
        CancellationToken ct = default)
    {
        var results = new Dictionary<SocialPlatform, PostResult>();

        foreach (var platform in platforms)
        {
            var post = new Post
            {
                Platform = platform,
                Content = content,
                MediaPaths = mediaPaths,
                Status = PostStatus.Draft,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _database.AddPostAsync(post, ct);
            post.Id = id;

            results[platform] = await PublishPostAsync(post, ct);
        }

        return results;
    }

    /// <summary>
    /// Check if a platform client is ready
    /// </summary>
    public async Task<bool> IsPlatformReadyAsync(SocialPlatform platform, CancellationToken ct = default)
    {
        if (!_clients.TryGetValue(platform, out var client))
            return false;

        return await client.IsAuthenticatedAsync(ct);
    }
}
