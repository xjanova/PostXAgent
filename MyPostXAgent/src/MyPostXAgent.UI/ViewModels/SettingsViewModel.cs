using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using MyPostXAgent.Core.Services;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.AI;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.UI.ViewModels;

public class SettingsViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly AIContentService _aiService;
    private readonly LocalizationService _localizationService;
    private readonly HttpClient _httpClient;

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

    // Available Ollama models (auto-detected from local installation)
    private ObservableCollection<string> _availableOllamaModels = new();
    public ObservableCollection<string> AvailableOllamaModels
    {
        get => _availableOllamaModels;
        set => SetProperty(ref _availableOllamaModels, value);
    }

    // Provider status indicators
    private string _ollamaStatus = "กำลังตรวจสอบ...";
    public string OllamaStatus
    {
        get => _ollamaStatus;
        set => SetProperty(ref _ollamaStatus, value);
    }

    private string _openAiStatus = "ไม่พร้อม";
    public string OpenAiStatus
    {
        get => _openAiStatus;
        set => SetProperty(ref _openAiStatus, value);
    }

    private string _claudeStatus = "ไม่พร้อม";
    public string ClaudeStatus
    {
        get => _claudeStatus;
        set => SetProperty(ref _claudeStatus, value);
    }

    private string _geminiStatus = "ไม่พร้อม";
    public string GeminiStatus
    {
        get => _geminiStatus;
        set => SetProperty(ref _geminiStatus, value);
    }

    // Commands
    public RelayCommand RefreshOllamaModelsCommand { get; }

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

    public SettingsViewModel(DatabaseService database, AIContentService aiService, LocalizationService localizationService)
    {
        _database = database;
        _aiService = aiService;
        _localizationService = localizationService;
        _httpClient = new HttpClient();

        SaveCommand = new RelayCommand(async () => await SaveSettingsAsync());
        ResetCommand = new RelayCommand(ResetToDefaults);
        RefreshOllamaModelsCommand = new RelayCommand(async () => await RefreshOllamaModelsAsync());

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load settings on startup
        _ = LoadSettingsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Refresh status text when language changes
        _ = RefreshOllamaModelsAsync();
        _ = UpdateProvidersStatusAsync();
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

            // Auto-detect Ollama models and update provider statuses
            _ = RefreshOllamaModelsAsync();
            _ = UpdateProvidersStatusAsync();
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

    /// <summary>
    /// Auto-detect installed Ollama models from Ollama API
    /// </summary>
    private async Task RefreshOllamaModelsAsync()
    {
        try
        {
            var isThai = _localizationService.IsThaiLanguage;
            OllamaStatus = LocalizationStrings.Common.Checking(isThai);

            var response = await _httpClient.GetAsync(
                $"{OllamaBaseUrl}/api/tags",
                new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);

            if (!response.IsSuccessStatusCode)
            {
                OllamaStatus = $"Ollama {LocalizationStrings.AIStatus.NotRunning(isThai)}";
                AvailableOllamaModels.Clear();
                AvailableOllamaModels.Add(OllamaModel); // Keep current selection
                return;
            }

            var tagsResponse = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();

            if (tagsResponse?.Models == null || tagsResponse.Models.Count == 0)
            {
                OllamaStatus = LocalizationStrings.AIStatus.ModelNotFound(isThai);
                AvailableOllamaModels.Clear();
                AvailableOllamaModels.Add(OllamaModel);
                return;
            }

            // Update available models list
            AvailableOllamaModels.Clear();
            foreach (var model in tagsResponse.Models)
            {
                if (!string.IsNullOrWhiteSpace(model.Name))
                {
                    AvailableOllamaModels.Add(model.Name);
                }
            }

            // Ensure current selection is in the list
            if (!string.IsNullOrWhiteSpace(OllamaModel) && !AvailableOllamaModels.Contains(OllamaModel))
            {
                AvailableOllamaModels.Insert(0, OllamaModel);
            }

            OllamaStatus = LocalizationStrings.AIStatus.ModelsAvailable(isThai, AvailableOllamaModels.Count);
        }
        catch (TaskCanceledException)
        {
            var isThai = _localizationService.IsThaiLanguage;
            OllamaStatus = $"Ollama {LocalizationStrings.AIStatus.Timeout(isThai)}";
            AvailableOllamaModels.Clear();
            AvailableOllamaModels.Add(OllamaModel);
        }
        catch (Exception ex)
        {
            OllamaStatus = $"{LocalizationStrings.Common.Error(_localizationService.IsThaiLanguage)}: {ex.Message}";
            AvailableOllamaModels.Clear();
            AvailableOllamaModels.Add(OllamaModel);
            System.Diagnostics.Debug.WriteLine($"Error refreshing Ollama models: {ex.Message}");
        }
    }

    /// <summary>
    /// Update status for all AI providers
    /// </summary>
    private async Task UpdateProvidersStatusAsync()
    {
        try
        {
            var isThai = _localizationService.IsThaiLanguage;

            // Initialize providers first
            await _aiService.InitializeProvidersAsync();

            // Get status for all providers
            var statuses = await _aiService.GetAllProvidersStatusAsync();

            foreach (var status in statuses)
            {
                var statusText = status.IsAvailable
                    ? $"✅ {status.Message}"
                    : status.IsConfigured
                        ? $"⚠️ {status.Message}"
                        : $"❌ {LocalizationStrings.AIStatus.NotConfigured(isThai)}";

                switch (status.Provider)
                {
                    case AIProvider.Ollama:
                        OllamaStatus = statusText;
                        break;
                    case AIProvider.OpenAI:
                        OpenAiStatus = statusText;
                        break;
                    case AIProvider.Claude:
                        ClaudeStatus = statusText;
                        break;
                    case AIProvider.Gemini:
                        GeminiStatus = statusText;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating provider status: {ex.Message}");
        }
    }

    // DTO classes for Ollama API
    private class OllamaTagsResponse
    {
        public List<OllamaModelDto>? Models { get; set; }
    }

    private class OllamaModelDto
    {
        public string? Name { get; set; }
        public long? Size { get; set; }
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

            // Update provider statuses
            await UpdateProvidersStatusAsync();

            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                LocalizationStrings.Settings.SaveSuccess(isThai),
                LocalizationStrings.Common.Success(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            var isThai = _localizationService.IsThaiLanguage;
            MessageBox.Show(
                $"{LocalizationStrings.Common.Error(isThai)}: {ex.Message}",
                LocalizationStrings.Common.Error(isThai),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ResetToDefaults()
    {
        var isThai = _localizationService.IsThaiLanguage;
        var result = MessageBox.Show(
            LocalizationStrings.Settings.ResetConfirm(isThai),
            LocalizationStrings.Settings.ConfirmReset(isThai),
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
