using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AIManager.UI.ViewModels;

/// <summary>
/// Settings page ViewModel
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    [ObservableProperty]
    private int _apiPort = 5000;

    [ObservableProperty]
    private int _webSocketPort = 5001;

    [ObservableProperty]
    private int _signalRPort = 5002;

    [ObservableProperty]
    private string _redisConnectionString = "localhost:6379";

    [ObservableProperty]
    private string _ollamaBaseUrl = "http://localhost:11434";

    [ObservableProperty]
    private string _ollamaModel = "llama2";

    [ObservableProperty]
    private bool _useOpenAI;

    [ObservableProperty]
    private bool _useAnthropic;

    [ObservableProperty]
    private bool _useGoogleGemini;

    [ObservableProperty]
    private bool _useOllama = true;

    [ObservableProperty]
    private string _openAIApiKey = "";

    [ObservableProperty]
    private string _anthropicApiKey = "";

    [ObservableProperty]
    private string _googleApiKey = "";

    [ObservableProperty]
    private bool _isDirty;

    public SettingsViewModel()
    {
        Title = "Settings";
        LoadSettings();
    }

    partial void OnApiPortChanged(int value) => IsDirty = true;
    partial void OnWebSocketPortChanged(int value) => IsDirty = true;
    partial void OnSignalRPortChanged(int value) => IsDirty = true;
    partial void OnRedisConnectionStringChanged(string value) => IsDirty = true;

    [RelayCommand]
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

    [RelayCommand]
    private void SaveSettings()
    {
        // In a real app, save to config file
        IsDirty = false;
    }

    [RelayCommand]
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
    }
}
