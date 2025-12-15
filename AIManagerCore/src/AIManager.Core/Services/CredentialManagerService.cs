using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Service for managing platform credentials
/// </summary>
public class CredentialManagerService
{
    private readonly AllPlatformCredentials _credentials = new();

    public AllPlatformCredentials GetAllCredentials() => _credentials;

    public void UpdateCredentials(FacebookCredentials credentials) => _credentials.Facebook = credentials;
    public void UpdateCredentials(InstagramCredentials credentials) => _credentials.Instagram = credentials;
    public void UpdateCredentials(TikTokCredentials credentials) => _credentials.TikTok = credentials;
    public void UpdateCredentials(TwitterCredentials credentials) => _credentials.Twitter = credentials;
    public void UpdateCredentials(LineCredentials credentials) => _credentials.Line = credentials;
    public void UpdateCredentials(YouTubeCredentials credentials) => _credentials.YouTube = credentials;
    public void UpdateCredentials(ThreadsCredentials credentials) => _credentials.Threads = credentials;
    public void UpdateCredentials(LinkedInCredentials credentials) => _credentials.LinkedIn = credentials;
    public void UpdateCredentials(PinterestCredentials credentials) => _credentials.Pinterest = credentials;

    public List<(SocialPlatform Platform, bool IsConfigured, string Status)> GetPlatformStatusSummary()
    {
        return new List<(SocialPlatform, bool, string)>
        {
            (SocialPlatform.Facebook, _credentials.Facebook.IsConfigured, GetStatus(_credentials.Facebook.IsConfigured)),
            (SocialPlatform.Instagram, _credentials.Instagram.IsConfigured, GetStatus(_credentials.Instagram.IsConfigured)),
            (SocialPlatform.TikTok, _credentials.TikTok.IsConfigured, GetStatus(_credentials.TikTok.IsConfigured)),
            (SocialPlatform.Twitter, _credentials.Twitter.IsConfigured, GetStatus(_credentials.Twitter.IsConfigured)),
            (SocialPlatform.Line, _credentials.Line.IsConfigured, GetStatus(_credentials.Line.IsConfigured)),
            (SocialPlatform.YouTube, _credentials.YouTube.IsConfigured, GetStatus(_credentials.YouTube.IsConfigured)),
            (SocialPlatform.Threads, _credentials.Threads.IsConfigured, GetStatus(_credentials.Threads.IsConfigured)),
            (SocialPlatform.LinkedIn, _credentials.LinkedIn.IsConfigured, GetStatus(_credentials.LinkedIn.IsConfigured)),
            (SocialPlatform.Pinterest, _credentials.Pinterest.IsConfigured, GetStatus(_credentials.Pinterest.IsConfigured))
        };
    }

    private static string GetStatus(bool isConfigured) => isConfigured ? "Connected" : "Not configured";

    public async Task<(bool Success, string Message)> TestConnectionAsync(SocialPlatform platform)
    {
        await Task.Delay(100); // Simulate API call

        var isConfigured = platform switch
        {
            SocialPlatform.Facebook => _credentials.Facebook.IsConfigured,
            SocialPlatform.Instagram => _credentials.Instagram.IsConfigured,
            SocialPlatform.TikTok => _credentials.TikTok.IsConfigured,
            SocialPlatform.Twitter => _credentials.Twitter.IsConfigured,
            SocialPlatform.Line => _credentials.Line.IsConfigured,
            SocialPlatform.YouTube => _credentials.YouTube.IsConfigured,
            SocialPlatform.Threads => _credentials.Threads.IsConfigured,
            SocialPlatform.LinkedIn => _credentials.LinkedIn.IsConfigured,
            SocialPlatform.Pinterest => _credentials.Pinterest.IsConfigured,
            _ => false
        };

        return isConfigured
            ? (true, "Connection successful")
            : (false, "Not configured");
    }
}

#region Platform Credential Models

public class AllPlatformCredentials
{
    public FacebookCredentials Facebook { get; set; } = new();
    public InstagramCredentials Instagram { get; set; } = new();
    public TikTokCredentials TikTok { get; set; } = new();
    public TwitterCredentials Twitter { get; set; } = new();
    public LineCredentials Line { get; set; } = new();
    public YouTubeCredentials YouTube { get; set; } = new();
    public ThreadsCredentials Threads { get; set; } = new();
    public LinkedInCredentials LinkedIn { get; set; } = new();
    public PinterestCredentials Pinterest { get; set; } = new();
}

public class FacebookCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string PageId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class InstagramCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string BusinessAccountId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class TikTokCredentials
{
    public string ClientKey { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string OpenId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class TwitterCredentials
{
    public string ApiKey { get; set; } = "";
    public string ApiSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AccessTokenSecret { get; set; } = "";
    public string BearerToken { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class LineCredentials
{
    public string ChannelId { get; set; } = "";
    public string ChannelSecret { get; set; } = "";
    public string ChannelAccessToken { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class YouTubeCredentials
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string ChannelId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class ThreadsCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string UserId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class LinkedInCredentials
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string OrganizationId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

public class PinterestCredentials
{
    public string AppId { get; set; } = "";
    public string AppSecret { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string BoardId { get; set; } = "";
    public bool IsConfigured { get; set; }
}

#endregion
