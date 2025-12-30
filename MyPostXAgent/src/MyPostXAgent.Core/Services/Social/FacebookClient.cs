using System.Net.Http.Json;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.Social;

/// <summary>
/// Facebook Graph API Client
/// </summary>
public class FacebookClient : ISocialPlatformClient
{
    private readonly HttpClient _httpClient;
    private string _accessToken = string.Empty;
    private string _pageId = string.Empty;
    private const string GraphApiVersion = "v18.0";
    private const string BaseUrl = "https://graph.facebook.com";

    public SocialPlatform Platform => SocialPlatform.Facebook;

    public FacebookClient(string? accessToken = null)
    {
        _accessToken = accessToken ?? string.Empty;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
    }

    public void SetAccessToken(string token) => _accessToken = token;
    public void SetPageId(string pageId) => _pageId = pageId;

    public async Task<bool> IsAuthenticatedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accessToken)) return false;

        try
        {
            var response = await _httpClient.GetAsync($"/{GraphApiVersion}/me?access_token={_accessToken}", ct);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<AuthResult> AuthenticateAsync(AccountCredentials credentials, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(credentials.AccessToken))
        {
            _accessToken = credentials.AccessToken;
            var isValid = await IsAuthenticatedAsync(ct);
            return new AuthResult { Success = isValid, AccessToken = credentials.AccessToken };
        }
        return new AuthResult { Success = false, ErrorMessage = "Facebook requires OAuth flow" };
    }

    public async Task<PostResult> PublishPostAsync(Post post, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accessToken))
            return new PostResult { Success = false, ErrorMessage = "Not authenticated" };

        try
        {
            var targetId = !string.IsNullOrEmpty(_pageId) ? _pageId : "me";
            
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("message", post.Content),
                new KeyValuePair<string, string>("access_token", _accessToken)
            });

            var response = await _httpClient.PostAsync($"/{GraphApiVersion}/{targetId}/feed", content, ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<FbPostResponse>(cancellationToken: ct);
                return new PostResult { Success = true, PostId = result?.id, PostUrl = $"https://facebook.com/{result?.id}" };
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            return new PostResult { Success = false, ErrorMessage = error };
        }
        catch (Exception ex)
        {
            return new PostResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<ProfileInfo?> GetProfileInfoAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_accessToken)) return null;

        try
        {
            var response = await _httpClient.GetFromJsonAsync<FbProfile>(
                $"/{GraphApiVersion}/me?fields=id,name,link&access_token={_accessToken}", ct);

            return response != null ? new ProfileInfo { UserId = response.id, DisplayName = response.name, ProfileUrl = response.link } : null;
        }
        catch { return null; }
    }

    private class FbPostResponse { public string? id { get; set; } }
    private class FbProfile { public string? id { get; set; } public string? name { get; set; } public string? link { get; set; } }
}
