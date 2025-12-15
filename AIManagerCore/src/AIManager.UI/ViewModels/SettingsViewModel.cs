using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Core.Models;
using AIManager.Core.Services;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Settings page ViewModel with Platform Credentials Management
/// </summary>
public class SettingsViewModel : BaseViewModel
{
    private readonly CredentialManagerService _credentialManager;

    #region General Settings

    private int _apiPort = 5000;
    public int ApiPort
    {
        get => _apiPort;
        set => SetProperty(ref _apiPort, value);
    }

    private int _webSocketPort = 5001;
    public int WebSocketPort
    {
        get => _webSocketPort;
        set => SetProperty(ref _webSocketPort, value);
    }

    private int _signalRPort = 5002;
    public int SignalRPort
    {
        get => _signalRPort;
        set => SetProperty(ref _signalRPort, value);
    }

    private string _redisConnectionString = "localhost:6379";
    public string RedisConnectionString
    {
        get => _redisConnectionString;
        set => SetProperty(ref _redisConnectionString, value);
    }

    #endregion

    #region AI Provider Settings

    private string _ollamaBaseUrl = "http://localhost:11434";
    public string OllamaBaseUrl
    {
        get => _ollamaBaseUrl;
        set => SetProperty(ref _ollamaBaseUrl, value);
    }

    private string _ollamaModel = "llama2";
    public string OllamaModel
    {
        get => _ollamaModel;
        set => SetProperty(ref _ollamaModel, value);
    }

    private bool _useOpenAI;
    public bool UseOpenAI
    {
        get => _useOpenAI;
        set => SetProperty(ref _useOpenAI, value);
    }

    private bool _useAnthropic;
    public bool UseAnthropic
    {
        get => _useAnthropic;
        set => SetProperty(ref _useAnthropic, value);
    }

    private bool _useGoogleGemini;
    public bool UseGoogleGemini
    {
        get => _useGoogleGemini;
        set => SetProperty(ref _useGoogleGemini, value);
    }

    private bool _useOllama = true;
    public bool UseOllama
    {
        get => _useOllama;
        set => SetProperty(ref _useOllama, value);
    }

    private string _openAIApiKey = "";
    public string OpenAIApiKey
    {
        get => _openAIApiKey;
        set => SetProperty(ref _openAIApiKey, value);
    }

    private string _anthropicApiKey = "";
    public string AnthropicApiKey
    {
        get => _anthropicApiKey;
        set => SetProperty(ref _anthropicApiKey, value);
    }

    private string _googleApiKey = "";
    public string GoogleApiKey
    {
        get => _googleApiKey;
        set => SetProperty(ref _googleApiKey, value);
    }

    #endregion

    #region Facebook Credentials

    private string _facebookAppId = "";
    public string FacebookAppId
    {
        get => _facebookAppId;
        set => SetProperty(ref _facebookAppId, value);
    }

    private string _facebookAppSecret = "";
    public string FacebookAppSecret
    {
        get => _facebookAppSecret;
        set => SetProperty(ref _facebookAppSecret, value);
    }

    private string _facebookAccessToken = "";
    public string FacebookAccessToken
    {
        get => _facebookAccessToken;
        set => SetProperty(ref _facebookAccessToken, value);
    }

    private string _facebookPageId = "";
    public string FacebookPageId
    {
        get => _facebookPageId;
        set => SetProperty(ref _facebookPageId, value);
    }

    private bool _facebookConnected;
    public bool FacebookConnected
    {
        get => _facebookConnected;
        set => SetProperty(ref _facebookConnected, value);
    }

    #endregion

    #region Instagram Credentials

    private string _instagramAppId = "";
    public string InstagramAppId
    {
        get => _instagramAppId;
        set => SetProperty(ref _instagramAppId, value);
    }

    private string _instagramAppSecret = "";
    public string InstagramAppSecret
    {
        get => _instagramAppSecret;
        set => SetProperty(ref _instagramAppSecret, value);
    }

    private string _instagramAccessToken = "";
    public string InstagramAccessToken
    {
        get => _instagramAccessToken;
        set => SetProperty(ref _instagramAccessToken, value);
    }

    private string _instagramBusinessAccountId = "";
    public string InstagramBusinessAccountId
    {
        get => _instagramBusinessAccountId;
        set => SetProperty(ref _instagramBusinessAccountId, value);
    }

    private bool _instagramConnected;
    public bool InstagramConnected
    {
        get => _instagramConnected;
        set => SetProperty(ref _instagramConnected, value);
    }

    #endregion

    #region TikTok Credentials

    private string _tiktokClientKey = "";
    public string TikTokClientKey
    {
        get => _tiktokClientKey;
        set => SetProperty(ref _tiktokClientKey, value);
    }

    private string _tiktokClientSecret = "";
    public string TikTokClientSecret
    {
        get => _tiktokClientSecret;
        set => SetProperty(ref _tiktokClientSecret, value);
    }

    private string _tiktokAccessToken = "";
    public string TikTokAccessToken
    {
        get => _tiktokAccessToken;
        set => SetProperty(ref _tiktokAccessToken, value);
    }

    private string _tiktokOpenId = "";
    public string TikTokOpenId
    {
        get => _tiktokOpenId;
        set => SetProperty(ref _tiktokOpenId, value);
    }

    private bool _tiktokConnected;
    public bool TikTokConnected
    {
        get => _tiktokConnected;
        set => SetProperty(ref _tiktokConnected, value);
    }

    #endregion

    #region Twitter Credentials

    private string _twitterApiKey = "";
    public string TwitterApiKey
    {
        get => _twitterApiKey;
        set => SetProperty(ref _twitterApiKey, value);
    }

    private string _twitterApiSecret = "";
    public string TwitterApiSecret
    {
        get => _twitterApiSecret;
        set => SetProperty(ref _twitterApiSecret, value);
    }

    private string _twitterAccessToken = "";
    public string TwitterAccessToken
    {
        get => _twitterAccessToken;
        set => SetProperty(ref _twitterAccessToken, value);
    }

    private string _twitterAccessTokenSecret = "";
    public string TwitterAccessTokenSecret
    {
        get => _twitterAccessTokenSecret;
        set => SetProperty(ref _twitterAccessTokenSecret, value);
    }

    private string _twitterBearerToken = "";
    public string TwitterBearerToken
    {
        get => _twitterBearerToken;
        set => SetProperty(ref _twitterBearerToken, value);
    }

    private bool _twitterConnected;
    public bool TwitterConnected
    {
        get => _twitterConnected;
        set => SetProperty(ref _twitterConnected, value);
    }

    #endregion

    #region LINE Credentials

    private string _lineChannelId = "";
    public string LineChannelId
    {
        get => _lineChannelId;
        set => SetProperty(ref _lineChannelId, value);
    }

    private string _lineChannelSecret = "";
    public string LineChannelSecret
    {
        get => _lineChannelSecret;
        set => SetProperty(ref _lineChannelSecret, value);
    }

    private string _lineChannelAccessToken = "";
    public string LineChannelAccessToken
    {
        get => _lineChannelAccessToken;
        set => SetProperty(ref _lineChannelAccessToken, value);
    }

    private bool _lineConnected;
    public bool LineConnected
    {
        get => _lineConnected;
        set => SetProperty(ref _lineConnected, value);
    }

    #endregion

    #region YouTube Credentials

    private string _youtubeClientId = "";
    public string YouTubeClientId
    {
        get => _youtubeClientId;
        set => SetProperty(ref _youtubeClientId, value);
    }

    private string _youtubeClientSecret = "";
    public string YouTubeClientSecret
    {
        get => _youtubeClientSecret;
        set => SetProperty(ref _youtubeClientSecret, value);
    }

    private string _youtubeAccessToken = "";
    public string YouTubeAccessToken
    {
        get => _youtubeAccessToken;
        set => SetProperty(ref _youtubeAccessToken, value);
    }

    private string _youtubeRefreshToken = "";
    public string YouTubeRefreshToken
    {
        get => _youtubeRefreshToken;
        set => SetProperty(ref _youtubeRefreshToken, value);
    }

    private string _youtubeChannelId = "";
    public string YouTubeChannelId
    {
        get => _youtubeChannelId;
        set => SetProperty(ref _youtubeChannelId, value);
    }

    private bool _youtubeConnected;
    public bool YouTubeConnected
    {
        get => _youtubeConnected;
        set => SetProperty(ref _youtubeConnected, value);
    }

    #endregion

    #region Threads Credentials

    private string _threadsAppId = "";
    public string ThreadsAppId
    {
        get => _threadsAppId;
        set => SetProperty(ref _threadsAppId, value);
    }

    private string _threadsAppSecret = "";
    public string ThreadsAppSecret
    {
        get => _threadsAppSecret;
        set => SetProperty(ref _threadsAppSecret, value);
    }

    private string _threadsAccessToken = "";
    public string ThreadsAccessToken
    {
        get => _threadsAccessToken;
        set => SetProperty(ref _threadsAccessToken, value);
    }

    private string _threadsUserId = "";
    public string ThreadsUserId
    {
        get => _threadsUserId;
        set => SetProperty(ref _threadsUserId, value);
    }

    private bool _threadsConnected;
    public bool ThreadsConnected
    {
        get => _threadsConnected;
        set => SetProperty(ref _threadsConnected, value);
    }

    #endregion

    #region LinkedIn Credentials

    private string _linkedInClientId = "";
    public string LinkedInClientId
    {
        get => _linkedInClientId;
        set => SetProperty(ref _linkedInClientId, value);
    }

    private string _linkedInClientSecret = "";
    public string LinkedInClientSecret
    {
        get => _linkedInClientSecret;
        set => SetProperty(ref _linkedInClientSecret, value);
    }

    private string _linkedInAccessToken = "";
    public string LinkedInAccessToken
    {
        get => _linkedInAccessToken;
        set => SetProperty(ref _linkedInAccessToken, value);
    }

    private string _linkedInOrganizationId = "";
    public string LinkedInOrganizationId
    {
        get => _linkedInOrganizationId;
        set => SetProperty(ref _linkedInOrganizationId, value);
    }

    private bool _linkedInConnected;
    public bool LinkedInConnected
    {
        get => _linkedInConnected;
        set => SetProperty(ref _linkedInConnected, value);
    }

    #endregion

    #region Pinterest Credentials

    private string _pinterestAppId = "";
    public string PinterestAppId
    {
        get => _pinterestAppId;
        set => SetProperty(ref _pinterestAppId, value);
    }

    private string _pinterestAppSecret = "";
    public string PinterestAppSecret
    {
        get => _pinterestAppSecret;
        set => SetProperty(ref _pinterestAppSecret, value);
    }

    private string _pinterestAccessToken = "";
    public string PinterestAccessToken
    {
        get => _pinterestAccessToken;
        set => SetProperty(ref _pinterestAccessToken, value);
    }

    private string _pinterestBoardId = "";
    public string PinterestBoardId
    {
        get => _pinterestBoardId;
        set => SetProperty(ref _pinterestBoardId, value);
    }

    private bool _pinterestConnected;
    public bool PinterestConnected
    {
        get => _pinterestConnected;
        set => SetProperty(ref _pinterestConnected, value);
    }

    #endregion

    #region Platform Status

    public ObservableCollection<PlatformStatusItem> PlatformStatuses { get; } = new();

    private string _selectedPlatform = "Facebook";
    public string SelectedPlatform
    {
        get => _selectedPlatform;
        set => SetProperty(ref _selectedPlatform, value);
    }

    public ObservableCollection<string> AvailablePlatforms { get; } = new()
    {
        "Facebook", "Instagram", "TikTok", "Twitter", "LINE", "YouTube", "Threads", "LinkedIn", "Pinterest"
    };

    #endregion

    #region UI State

    private bool _isDirty;
    public bool IsDirty
    {
        get => _isDirty;
        set => SetProperty(ref _isDirty, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isTesting;
    public bool IsTesting
    {
        get => _isTesting;
        set => SetProperty(ref _isTesting, value);
    }

    #endregion

    #region Commands

    public IRelayCommand LoadSettingsCommand { get; }
    public IRelayCommand SaveSettingsCommand { get; }
    public IRelayCommand ResetToDefaultsCommand { get; }
    public IAsyncRelayCommand<string> TestConnectionCommand { get; }
    public IAsyncRelayCommand TestAllConnectionsCommand { get; }
    public IRelayCommand SavePlatformCredentialsCommand { get; }

    #endregion

    public SettingsViewModel() : this(new CredentialManagerService())
    {
    }

    public SettingsViewModel(CredentialManagerService credentialManager)
    {
        _credentialManager = credentialManager;
        Title = "Settings";

        LoadSettingsCommand = new RelayCommand(LoadSettings);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        ResetToDefaultsCommand = new RelayCommand(ResetToDefaults);
        TestConnectionCommand = new AsyncRelayCommand<string>(TestConnectionAsync);
        TestAllConnectionsCommand = new AsyncRelayCommand(TestAllConnectionsAsync);
        SavePlatformCredentialsCommand = new RelayCommand(SavePlatformCredentials);

        LoadSettings();
        LoadPlatformCredentials();
        RefreshPlatformStatuses();

        // Track changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != nameof(IsDirty) && e.PropertyName != nameof(IsBusy) &&
                e.PropertyName != nameof(Title) && e.PropertyName != nameof(StatusMessage) &&
                e.PropertyName != nameof(IsTesting))
            {
                IsDirty = true;
            }
        };
    }

    private void LoadSettings()
    {
        // Load from environment or config file
        ApiPort = int.TryParse(Environment.GetEnvironmentVariable("AI_MANAGER_API_PORT"), out var port) ? port : 5000;
        WebSocketPort = int.TryParse(Environment.GetEnvironmentVariable("AI_MANAGER_WS_PORT"), out var wsPort) ? wsPort : 5001;
        SignalRPort = int.TryParse(Environment.GetEnvironmentVariable("AI_MANAGER_SIGNALR_PORT"), out var srPort) ? srPort : 5002;
        RedisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? "localhost:6379";
        OllamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        OllamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "llama2";

        UseOpenAI = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        UseAnthropic = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
        UseGoogleGemini = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));
        UseOllama = true;

        IsDirty = false;
    }

    private void LoadPlatformCredentials()
    {
        var creds = _credentialManager.GetAllCredentials();

        // Facebook
        FacebookAppId = creds.Facebook.AppId;
        FacebookAppSecret = creds.Facebook.AppSecret;
        FacebookAccessToken = creds.Facebook.AccessToken;
        FacebookPageId = creds.Facebook.PageId;
        FacebookConnected = creds.Facebook.IsConfigured;

        // Instagram
        InstagramAppId = creds.Instagram.AppId;
        InstagramAppSecret = creds.Instagram.AppSecret;
        InstagramAccessToken = creds.Instagram.AccessToken;
        InstagramBusinessAccountId = creds.Instagram.BusinessAccountId;
        InstagramConnected = creds.Instagram.IsConfigured;

        // TikTok
        TikTokClientKey = creds.TikTok.ClientKey;
        TikTokClientSecret = creds.TikTok.ClientSecret;
        TikTokAccessToken = creds.TikTok.AccessToken;
        TikTokOpenId = creds.TikTok.OpenId;
        TikTokConnected = creds.TikTok.IsConfigured;

        // Twitter
        TwitterApiKey = creds.Twitter.ApiKey;
        TwitterApiSecret = creds.Twitter.ApiSecret;
        TwitterAccessToken = creds.Twitter.AccessToken;
        TwitterAccessTokenSecret = creds.Twitter.AccessTokenSecret;
        TwitterBearerToken = creds.Twitter.BearerToken;
        TwitterConnected = creds.Twitter.IsConfigured;

        // LINE
        LineChannelId = creds.Line.ChannelId;
        LineChannelSecret = creds.Line.ChannelSecret;
        LineChannelAccessToken = creds.Line.ChannelAccessToken;
        LineConnected = creds.Line.IsConfigured;

        // YouTube
        YouTubeClientId = creds.YouTube.ClientId;
        YouTubeClientSecret = creds.YouTube.ClientSecret;
        YouTubeAccessToken = creds.YouTube.AccessToken;
        YouTubeRefreshToken = creds.YouTube.RefreshToken;
        YouTubeChannelId = creds.YouTube.ChannelId;
        YouTubeConnected = creds.YouTube.IsConfigured;

        // Threads
        ThreadsAppId = creds.Threads.AppId;
        ThreadsAppSecret = creds.Threads.AppSecret;
        ThreadsAccessToken = creds.Threads.AccessToken;
        ThreadsUserId = creds.Threads.UserId;
        ThreadsConnected = creds.Threads.IsConfigured;

        // LinkedIn
        LinkedInClientId = creds.LinkedIn.ClientId;
        LinkedInClientSecret = creds.LinkedIn.ClientSecret;
        LinkedInAccessToken = creds.LinkedIn.AccessToken;
        LinkedInOrganizationId = creds.LinkedIn.OrganizationId;
        LinkedInConnected = creds.LinkedIn.IsConfigured;

        // Pinterest
        PinterestAppId = creds.Pinterest.AppId;
        PinterestAppSecret = creds.Pinterest.AppSecret;
        PinterestAccessToken = creds.Pinterest.AccessToken;
        PinterestBoardId = creds.Pinterest.BoardId;
        PinterestConnected = creds.Pinterest.IsConfigured;

        IsDirty = false;
    }

    private void SavePlatformCredentials()
    {
        // Facebook
        _credentialManager.UpdateCredentials(new FacebookCredentials
        {
            AppId = FacebookAppId,
            AppSecret = FacebookAppSecret,
            AccessToken = FacebookAccessToken,
            PageId = FacebookPageId,
            IsConfigured = FacebookConnected
        });

        // Instagram
        _credentialManager.UpdateCredentials(new InstagramCredentials
        {
            AppId = InstagramAppId,
            AppSecret = InstagramAppSecret,
            AccessToken = InstagramAccessToken,
            BusinessAccountId = InstagramBusinessAccountId,
            IsConfigured = InstagramConnected
        });

        // TikTok
        _credentialManager.UpdateCredentials(new TikTokCredentials
        {
            ClientKey = TikTokClientKey,
            ClientSecret = TikTokClientSecret,
            AccessToken = TikTokAccessToken,
            OpenId = TikTokOpenId,
            IsConfigured = TikTokConnected
        });

        // Twitter
        _credentialManager.UpdateCredentials(new TwitterCredentials
        {
            ApiKey = TwitterApiKey,
            ApiSecret = TwitterApiSecret,
            AccessToken = TwitterAccessToken,
            AccessTokenSecret = TwitterAccessTokenSecret,
            BearerToken = TwitterBearerToken,
            IsConfigured = TwitterConnected
        });

        // LINE
        _credentialManager.UpdateCredentials(new LineCredentials
        {
            ChannelId = LineChannelId,
            ChannelSecret = LineChannelSecret,
            ChannelAccessToken = LineChannelAccessToken,
            IsConfigured = LineConnected
        });

        // YouTube
        _credentialManager.UpdateCredentials(new YouTubeCredentials
        {
            ClientId = YouTubeClientId,
            ClientSecret = YouTubeClientSecret,
            AccessToken = YouTubeAccessToken,
            RefreshToken = YouTubeRefreshToken,
            ChannelId = YouTubeChannelId,
            IsConfigured = YouTubeConnected
        });

        // Threads
        _credentialManager.UpdateCredentials(new ThreadsCredentials
        {
            AppId = ThreadsAppId,
            AppSecret = ThreadsAppSecret,
            AccessToken = ThreadsAccessToken,
            UserId = ThreadsUserId,
            IsConfigured = ThreadsConnected
        });

        // LinkedIn
        _credentialManager.UpdateCredentials(new LinkedInCredentials
        {
            ClientId = LinkedInClientId,
            ClientSecret = LinkedInClientSecret,
            AccessToken = LinkedInAccessToken,
            OrganizationId = LinkedInOrganizationId,
            IsConfigured = LinkedInConnected
        });

        // Pinterest
        _credentialManager.UpdateCredentials(new PinterestCredentials
        {
            AppId = PinterestAppId,
            AppSecret = PinterestAppSecret,
            AccessToken = PinterestAccessToken,
            BoardId = PinterestBoardId,
            IsConfigured = PinterestConnected
        });

        StatusMessage = "Platform credentials saved";
        RefreshPlatformStatuses();
        IsDirty = false;
    }

    private void RefreshPlatformStatuses()
    {
        PlatformStatuses.Clear();
        var statuses = _credentialManager.GetPlatformStatusSummary();

        foreach (var (platform, isConfigured, status) in statuses)
        {
            PlatformStatuses.Add(new PlatformStatusItem
            {
                Platform = platform.ToString(),
                IsConfigured = isConfigured,
                Status = status
            });
        }
    }

    private async Task TestConnectionAsync(string? platformName)
    {
        if (string.IsNullOrEmpty(platformName)) return;

        IsTesting = true;
        StatusMessage = $"Testing {platformName} connection...";

        try
        {
            SavePlatformCredentials();

            var platform = Enum.Parse<SocialPlatform>(platformName == "LINE" ? "Line" : platformName);
            var (success, message) = await _credentialManager.TestConnectionAsync(platform);

            StatusMessage = $"{platformName}: {message}";
            UpdateConnectionStatus(platform, success);
            RefreshPlatformStatuses();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    private async Task TestAllConnectionsAsync()
    {
        IsTesting = true;
        SavePlatformCredentials();

        var platforms = Enum.GetValues<SocialPlatform>();
        var results = new List<string>();

        foreach (var platform in platforms)
        {
            StatusMessage = $"Testing {platform}...";
            var (success, message) = await _credentialManager.TestConnectionAsync(platform);
            results.Add($"{platform}: {(success ? "OK" : "Failed")}");
            UpdateConnectionStatus(platform, success);
        }

        RefreshPlatformStatuses();
        StatusMessage = $"Tested all platforms. Results: {string.Join(", ", results)}";
        IsTesting = false;
    }

    private void UpdateConnectionStatus(SocialPlatform platform, bool connected)
    {
        switch (platform)
        {
            case SocialPlatform.Facebook: FacebookConnected = connected; break;
            case SocialPlatform.Instagram: InstagramConnected = connected; break;
            case SocialPlatform.TikTok: TikTokConnected = connected; break;
            case SocialPlatform.Twitter: TwitterConnected = connected; break;
            case SocialPlatform.Line: LineConnected = connected; break;
            case SocialPlatform.YouTube: YouTubeConnected = connected; break;
            case SocialPlatform.Threads: ThreadsConnected = connected; break;
            case SocialPlatform.LinkedIn: LinkedInConnected = connected; break;
            case SocialPlatform.Pinterest: PinterestConnected = connected; break;
        }
    }

    private void SaveSettings()
    {
        SavePlatformCredentials();
        IsDirty = false;
        StatusMessage = "All settings saved";
    }

    private void ResetToDefaults()
    {
        ApiPort = 5000;
        WebSocketPort = 5001;
        SignalRPort = 5002;
        RedisConnectionString = "localhost:6379";
        OllamaBaseUrl = "http://localhost:11434";
        OllamaModel = "llama2";
        UseOllama = true;
        UseOpenAI = false;
        UseAnthropic = false;
        UseGoogleGemini = false;
        IsDirty = true;
        StatusMessage = "Settings reset to defaults";
    }
}

public class PlatformStatusItem
{
    public string Platform { get; set; } = "";
    public bool IsConfigured { get; set; }
    public string Status { get; set; } = "";
}
