using System.Windows.Input;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;
using MyPostXAgent.Core.Services.AI;
using MyPostXAgent.Core.Services.License;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel สำหรับ MainWindow
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly LicenseService _licenseService;
    private readonly AIContentService _aiService;
    private readonly LocalizationService _localizationService;
    private System.Threading.Timer? _aiStatusTimer;

    private bool _isDemoMode;
    public bool IsDemoMode
    {
        get => _isDemoMode;
        set => SetProperty(ref _isDemoMode, value);
    }

    private string _demoStatusText = "";
    public string DemoStatusText
    {
        get => _demoStatusText;
        set => SetProperty(ref _demoStatusText, value);
    }

    private int _postsToday;
    public int PostsToday
    {
        get => _postsToday;
        set => SetProperty(ref _postsToday, value);
    }

    private int _queueCount;
    public int QueueCount
    {
        get => _queueCount;
        set => SetProperty(ref _queueCount, value);
    }

    private int _totalAccounts;
    public int TotalAccounts
    {
        get => _totalAccounts;
        set => SetProperty(ref _totalAccounts, value);
    }

    private string _aiStatusText = "พร้อม";
    public string AIStatusText
    {
        get => _aiStatusText;
        set => SetProperty(ref _aiStatusText, value);
    }

    private string _aiStatusColor = "#10B981";
    public string AIStatusColor
    {
        get => _aiStatusColor;
        set => SetProperty(ref _aiStatusColor, value);
    }

    private string _currentLanguageFlag = "🇹🇭";
    public string CurrentLanguageFlag
    {
        get => _currentLanguageFlag;
        set => SetProperty(ref _currentLanguageFlag, value);
    }

    private string _currentLanguageText = "TH";
    public string CurrentLanguageText
    {
        get => _currentLanguageText;
        set => SetProperty(ref _currentLanguageText, value);
    }

    public ICommand BuyLicenseCommand { get; }
    public RelayCommand ToggleLanguageCommand { get; }

    public MainViewModel(LicenseService licenseService, AIContentService aiService, LocalizationService localizationService)
    {
        _licenseService = licenseService;
        _aiService = aiService;
        _localizationService = localizationService;
        Title = "MyPostXAgent";

        BuyLicenseCommand = new RelayCommand(BuyLicense);
        ToggleLanguageCommand = new RelayCommand(ToggleLanguage);

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;
        UpdateLanguageDisplay();

        UpdateDemoStatus();
        _ = UpdateAIStatusAsync();

        // Update AI status every 1 second for real-time monitoring
        _aiStatusTimer = new System.Threading.Timer(
            _ =>
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    _ = app.Dispatcher.InvokeAsync(async () =>
                    {
                        await UpdateAIStatusAsync();
                    });
                }
            },
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private void UpdateDemoStatus()
    {
        IsDemoMode = _licenseService.IsDemoMode();

        if (IsDemoMode)
        {
            var daysRemaining = _licenseService.GetDaysRemaining();
            DemoStatusText = daysRemaining > 0
                ? $"Demo Mode - เหลือ {daysRemaining} วัน"
                : "Demo Mode - เหลือไม่ถึง 1 วัน";
        }
    }

    private async Task UpdateAIStatusAsync()
    {
        try
        {
            var statuses = await _aiService.GetAllProvidersStatusAsync();
            var available = statuses.Where(s => s.IsAvailable && s.IsConfigured).ToList();
            var configured = statuses.Where(s => s.IsConfigured).ToList();

            if (available.Count == 0)
            {
                // No provider available - show detailed error
                var firstError = configured.FirstOrDefault();
                if (firstError != null && !string.IsNullOrEmpty(firstError.Message))
                {
                    AIStatusText = $"ไม่พร้อม - {firstError.Message}";
                }
                else
                {
                    AIStatusText = "ไม่พร้อม - ไม่มี AI Provider";
                }
                AIStatusColor = "#EF4444"; // Red
            }
            else if (available.Count == 1)
            {
                var provider = available[0];
                AIStatusText = $"พร้อม ({GetProviderName(provider.Provider)})";
                AIStatusColor = "#10B981"; // Green
            }
            else
            {
                AIStatusText = $"พร้อม ({available.Count} providers)";
                AIStatusColor = "#10B981"; // Green
            }
        }
        catch (Exception ex)
        {
            AIStatusText = $"ตรวจสอบไม่ได้ - {ex.Message}";
            AIStatusColor = "#F59E0B"; // Orange
        }
    }

    private string GetProviderName(AIProvider provider)
    {
        return provider switch
        {
            AIProvider.Ollama => "Ollama",
            AIProvider.Gemini => "Gemini",
            AIProvider.OpenAI => "OpenAI",
            AIProvider.Claude => "Claude",
            _ => provider.ToString()
        };
    }

    private void ToggleLanguage()
    {
        _localizationService.ToggleLanguage();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        UpdateLanguageDisplay();
        // Trigger UI updates for localized text
        OnPropertyChanged(nameof(AIStatusText));
        OnPropertyChanged(nameof(DemoStatusText));
    }

    private void UpdateLanguageDisplay()
    {
        if (_localizationService.IsThaiLanguage)
        {
            CurrentLanguageFlag = "🇹🇭";
            CurrentLanguageText = "TH";
        }
        else
        {
            CurrentLanguageFlag = "🇺🇸";
            CurrentLanguageText = "EN";
        }
    }

    private void BuyLicense()
    {
        // Open purchase page
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://postxagent.com/pricing",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore
        }
    }
}
