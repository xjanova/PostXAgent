using System.Net.Http.Json;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.Social;

/// <summary>
/// Instagram Graph API Client (via Facebook Business)
/// </summary>
public class InstagramClient : ISocialPlatformClient
{
    private readonly HttpClient _httpClient;
    private string _accessToken = string.Empty;
    private string _instagramAccountId = string.Empty;
    private const string GraphApiVersion = "v18.0";
    private const string BaseUrl = "https://graph.facebook.com";

    public SocialPlatform Platform => SocialPlatform.Instagram;

    public InstagramClient(string? accessToken = null)
    {
        _accessToken = accessToken ?? string.Empty;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public void SetAccessToken(string token) => _accessToken = token;
    public void SetInstagramAccountId(string accountId) => _instagramAccountId = accountId;

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_instagramAccountId)) 
            return false;

        try
        {
            var response = await _httpClient.GetAsync(
                $"/{GraphApiVersion}/{_instagramAccountId}?access_token={_accessToken}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<AuthResult> AuthenticateAsync(AccountCredentials credentials, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(credentials.AccessToken))
        {
            _accessToken = credentials.AccessToken;
            
            // Get Instagram account ID from Facebook Page
            var igAccount = await GetInstagramAccountAsync(ct);
            if (igAccount != null)
            {
                _instagramAccountId = igAccount;
                return new AuthResult { Success = true, AccessToken = credentials.AccessToken, UserId = igAccount };
            }
        }
        return new AuthResult { Success = false, ErrorMessage = "Instagram requires Facebook Business connection" };
    }

    public async Task<PostResult> PublishPostAsync(Post post, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_instagramAccountId))
            return new PostResult { Success = false, ErrorMessage = "Not authenticated" };

        try
        {
            // Instagram requires media - check for image
            if (!post.MediaPaths.Any())
            {
                return new PostResult { Success = false, ErrorMessage = "Instagram posts require an image" };
            }

            var mediaPath = post.MediaPaths.First();
            
            // Step 1: Create media container
            var containerId = await CreateMediaContainerAsync(mediaPath, post.Content, ct);
            if (string.IsNullOrEmpty(containerId))
            {
                return new PostResult { Success = false, ErrorMessage = "Failed to create media container" };
            }

            // Step 2: Publish the container
            return await PublishMediaAsync(containerId, ct);
        }
        catch (Exception ex)
        {
            return new PostResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<string?> CreateMediaContainerAsync(string imagePath, string caption, CancellationToken ct)
    {
        // For local files, you need to host them or use a URL
        // This is a simplified version - in production, upload to a hosting service first
        
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("image_url", imagePath), // Must be a public URL
            new KeyValuePair<string, string>("caption", caption),
            new KeyValuePair<string, string>("access_token", _accessToken)
        });

        var response = await _httpClient.PostAsync(
            $"/{GraphApiVersion}/{_instagramAccountId}/media", content, ct);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<IgMediaResponse>(cancellationToken: ct);
            return result?.id;
        }

        return null;
    }

    private async Task<PostResult> PublishMediaAsync(string containerId, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("creation_id", containerId),
            new KeyValuePair<string, string>("access_token", _accessToken)
        });

        var response = await _httpClient.PostAsync(
            $"/{GraphApiVersion}/{_instagramAccountId}/media_publish", content, ct);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<IgMediaResponse>(cancellationToken: ct);
            return new PostResult
            {
                Success = true,
                PostId = result?.id,
                PostUrl = $"https://instagram.com/p/{result?.id}"
            };
        }

        var error = await response.Content.ReadAsStringAsync(ct);
        return new PostResult { Success = false, ErrorMessage = error };
    }

    public async Task<ProfileInfo?> GetProfileInfoAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accessToken) || string.IsNullOrEmpty(_instagramAccountId)) 
            return null;

        try
        {
            var response = await _httpClient.GetFromJsonAsync<IgProfile>(
                $"/{GraphApiVersion}/{_instagramAccountId}?fields=id,username,name,profile_picture_url,followers_count&access_token={_accessToken}", ct);

            return response != null ? new ProfileInfo
            {
                UserId = response.id,
                Username = response.username,
                DisplayName = response.name,
                AvatarUrl = response.profile_picture_url,
                FollowersCount = response.followers_count
            } : null;
        }
        catch { return null; }
    }

    private async Task<string?> GetInstagramAccountAsync(CancellationToken ct)
    {
        try
        {
            // Get connected Instagram account from Facebook Page
            var response = await _httpClient.GetFromJsonAsync<IgAccountResponse>(
                $"/{GraphApiVersion}/me/accounts?fields=instagram_business_account&access_token={_accessToken}", ct);

            return response?.data?.FirstOrDefault()?.instagram_business_account?.id;
        }
        catch { return null; }
    }

    private class IgMediaResponse { public string? id { get; set; } }
    private class IgProfile 
    { 
        public string? id { get; set; } 
        public string? username { get; set; } 
        public string? name { get; set; } 
        public string? profile_picture_url { get; set; }
        public int followers_count { get; set; }
    }
    private class IgAccountResponse { public List<IgAccountData>? data { get; set; } }
    private class IgAccountData { public IgBusinessAccount? instagram_business_account { get; set; } }
    private class IgBusinessAccount { public string? id { get; set; } }
}
