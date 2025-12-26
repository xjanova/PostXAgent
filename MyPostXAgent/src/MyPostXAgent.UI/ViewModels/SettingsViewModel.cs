using System.Windows;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.AI;

namespace MyPostXAgent.UI.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly AIContentService _aiService;

    // AI Provider Settings
    private string _openAiApiKey = "";
    public string OpenAiApiKey
    {
        get => _openAiApiKey;
        set => SetProperty(ref _openAiApiKey, value);
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

    private string _ollamaBaseUrl = "http://localhost:11434";
    public string OllamaBaseUrl
    {
        get => _ollamaBaseUrl;
        set => SetProperty(ref _ollamaBaseUrl, value);
    }

    private string _ollamaModel = "llama3.2:3b";
    public string OllamaModel
    {
        get => _ollamaModel;
        set => SetProperty(ref _ollamaModel, value);
    }

    // Available Ollama models (common ones)
    public List<string> AvailableOllamaModels { get; } = new()
    {
        "llama3.2:1b",
        "llama3.2:3b",
        "llama3.1:8b",
        "llama3.1:70b",
        "llama3:8b",
        "llama3:70b",
        "qwen3:8b",
        "qwen2.5:7b",
        "mistral:7b",
        "gemma2:9b",
        "phi3:mini",
        "codellama:7b"
    };

    // Image Generation
    private string _leonardoApiKey = "";
    public string LeonardoApiKey
    {
        get => _leonardoApiKey;
        set => SetProperty(ref _leonardoApiKey, value);
    }

    private string _stableDiffusionUrl = "http://localhost:7860";
    public string StableDiffusionUrl
    {
        get => _stableDiffusionUrl;
        set => SetProperty(ref _stableDiffusionUrl, value);
    }

    // Video Generation
    private string _runwayApiKey = "";
    public string RunwayApiKey
    {
        get => _runwayApiKey;
        set => SetProperty(ref _runwayApiKey, value);
    }

    private string _pikaLabsApiKey = "";
    public string PikaLabsApiKey
    {
        get => _pikaLabsApiKey;
        set => SetProperty(ref _pikaLabsApiKey, value);
    }

    // Music Generation
    private string _sunoApiKey = "";
    public string SunoApiKey
    {
        get => _sunoApiKey;
        set => SetProperty(ref _sunoApiKey, value);
    }

    // Social Media APIs
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

    // Commands
    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }

    public SettingsViewModel(DatabaseService database, AIContentService aiService)
    {
        _database = database;
        _aiService = aiService;

        SaveCommand = new RelayCommand(async () => await SaveSettingsAsync());
        ResetCommand = new RelayCommand(ResetToDefaults);

        // Load settings on startup
        _ = LoadSettingsAsync();
    }

    public async Task LoadSettingsAsync()
    {
        try
        {
            IsBusy = true;

            // AI Providers
            OpenAiApiKey = await _database.GetSettingAsync("openai_api_key") ?? "";
            AnthropicApiKey = await _database.GetSettingAsync("anthropic_api_key") ?? "";
            GoogleApiKey = await _database.GetSettingAsync("google_api_key") ?? "";
            OllamaBaseUrl = await _database.GetSettingAsync("ollama_base_url") ?? "http://localhost:11434";
            OllamaModel = await _database.GetSettingAsync("ollama_model") ?? "llama3.2:3b";

            // Image Generation
            LeonardoApiKey = await _database.GetSettingAsync("leonardo_api_key") ?? "";
            StableDiffusionUrl = await _database.GetSettingAsync("stable_diffusion_url") ?? "http://localhost:7860";

            // Video Generation
            RunwayApiKey = await _database.GetSettingAsync("runway_api_key") ?? "";
            PikaLabsApiKey = await _database.GetSettingAsync("pikalabs_api_key") ?? "";

            // Music Generation
            SunoApiKey = await _database.GetSettingAsync("suno_api_key") ?? "";

            // Social Media
            FacebookAppId = await _database.GetSettingAsync("facebook_app_id") ?? "";
            FacebookAppSecret = await _database.GetSettingAsync("facebook_app_secret") ?? "";
            TwitterApiKey = await _database.GetSettingAsync("twitter_api_key") ?? "";
            TwitterApiSecret = await _database.GetSettingAsync("twitter_api_secret") ?? "";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            IsBusy = true;

            // AI Providers
            await _database.SetSettingAsync("openai_api_key", OpenAiApiKey, "ai");
            await _database.SetSettingAsync("anthropic_api_key", AnthropicApiKey, "ai");
            await _database.SetSettingAsync("google_api_key", GoogleApiKey, "ai");
            await _database.SetSettingAsync("ollama_base_url", OllamaBaseUrl, "ai");
            await _database.SetSettingAsync("ollama_model", OllamaModel, "ai");

            // Image Generation
            await _database.SetSettingAsync("leonardo_api_key", LeonardoApiKey, "image");
            await _database.SetSettingAsync("stable_diffusion_url", StableDiffusionUrl, "image");

            // Video Generation
            await _database.SetSettingAsync("runway_api_key", RunwayApiKey, "video");
            await _database.SetSettingAsync("pikalabs_api_key", PikaLabsApiKey, "video");

            // Music Generation
            await _database.SetSettingAsync("suno_api_key", SunoApiKey, "music");

            // Social Media
            await _database.SetSettingAsync("facebook_app_id", FacebookAppId, "social");
            await _database.SetSettingAsync("facebook_app_secret", FacebookAppSecret, "social");
            await _database.SetSettingAsync("twitter_api_key", TwitterApiKey, "social");
            await _database.SetSettingAsync("twitter_api_secret", TwitterApiSecret, "social");

            // Reinitialize AI providers with new settings
            await _aiService.InitializeProvidersAsync();

            MessageBox.Show("บันทึกการตั้งค่าสำเร็จ!\n\nAI Providers ได้รับการอัพเดทแล้ว", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetToDefaults()
    {
        var result = MessageBox.Show(
            "ต้องการรีเซ็ตค่าทั้งหมดเป็นค่าเริ่มต้นหรือไม่?",
            "ยืนยันการรีเซ็ต",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            OpenAiApiKey = "";
            AnthropicApiKey = "";
            GoogleApiKey = "";
            OllamaBaseUrl = "http://localhost:11434";
            OllamaModel = "llama3.2:3b";
            LeonardoApiKey = "";
            StableDiffusionUrl = "http://localhost:7860";
            RunwayApiKey = "";
            PikaLabsApiKey = "";
            SunoApiKey = "";
            FacebookAppId = "";
            FacebookAppSecret = "";
            TwitterApiKey = "";
            TwitterApiSecret = "";
        }
    }
}
