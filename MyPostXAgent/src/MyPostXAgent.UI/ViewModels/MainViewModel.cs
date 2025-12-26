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

    // Localized UI Text Properties
    private string _searchHint = "ค้นหา...";
    public string SearchHint
    {
        get => _searchHint;
        set => SetProperty(ref _searchHint, value);
    }

    private string _minimizeTooltip = "ย่อ";
    public string MinimizeTooltip
    {
        get => _minimizeTooltip;
        set => SetProperty(ref _minimizeTooltip, value);
    }

    private string _maximizeTooltip = "ขยาย";
    public string MaximizeTooltip
    {
        get => _maximizeTooltip;
        set => SetProperty(ref _maximizeTooltip, value);
    }

    private string _closeTooltip = "ปิด";
    public string CloseTooltip
    {
        get => _closeTooltip;
        set => SetProperty(ref _closeTooltip, value);
    }

    private string _languageTooltip = "สลับภาษา (Switch Language)";
    public string LanguageTooltip
    {
        get => _languageTooltip;
        set => SetProperty(ref _languageTooltip, value);
    }

    // Navigation Menu Items
    private string _navDashboard = "แดชบอร์ด";
    public string NavDashboard
    {
        get => _navDashboard;
        set => SetProperty(ref _navDashboard, value);
    }

    private string _navPosts = "โพสต์";
    public string NavPosts
    {
        get => _navPosts;
        set => SetProperty(ref _navPosts, value);
    }

    private string _navSchedule = "ตั้งเวลา";
    public string NavSchedule
    {
        get => _navSchedule;
        set => SetProperty(ref _navSchedule, value);
    }

    private string _navAccounts = "บัญชี Social";
    public string NavAccounts
    {
        get => _navAccounts;
        set => SetProperty(ref _navAccounts, value);
    }

    private string _navAIContent = "AI Content";
    public string NavAIContent
    {
        get => _navAIContent;
        set => SetProperty(ref _navAIContent, value);
    }

    private string _navVideoEditor = "Video Editor";
    public string NavVideoEditor
    {
        get => _navVideoEditor;
        set => SetProperty(ref _navVideoEditor, value);
    }

    private string _navGroups = "ค้นหากลุ่ม";
    public string NavGroups
    {
        get => _navGroups;
        set => SetProperty(ref _navGroups, value);
    }

    private string _navComments = "Comments";
    public string NavComments
    {
        get => _navComments;
        set => SetProperty(ref _navComments, value);
    }

    private string _navWorkflows = "Workflows";
    public string NavWorkflows
    {
        get => _navWorkflows;
        set => SetProperty(ref _navWorkflows, value);
    }

    private string _navSettings = "ตั้งค่า";
    public string NavSettings
    {
        get => _navSettings;
        set => SetProperty(ref _navSettings, value);
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
            DemoStatusText = LocalizationStrings.Demo.DemoMode(_localizationService.IsThaiLanguage, daysRemaining);
        }
    }

    private async Task UpdateAIStatusAsync()
    {
        try
        {
            var statuses = await _aiService.GetAllProvidersStatusAsync();
            var available = statuses.Where(s => s.IsAvailable && s.IsConfigured).ToList();
            var configured = statuses.Where(s => s.IsConfigured).ToList();
            var isThai = _localizationService.IsThaiLanguage;

            if (available.Count == 0)
            {
                // No provider available - show detailed error
                var firstError = configured.FirstOrDefault();
                if (firstError != null && !string.IsNullOrEmpty(firstError.Message))
                {
                    AIStatusText = $"{LocalizationStrings.AIStatus.NotReady(isThai)} - {firstError.Message}";
                }
                else
                {
                    AIStatusText = $"{LocalizationStrings.AIStatus.NotReady(isThai)} - {LocalizationStrings.AIStatus.NoProvider(isThai)}";
                }
                AIStatusColor = "#EF4444"; // Red
            }
            else if (available.Count == 1)
            {
                var provider = available[0];
                AIStatusText = $"{LocalizationStrings.AIStatus.Ready(isThai)} ({GetProviderName(provider.Provider)})";
                AIStatusColor = "#10B981"; // Green
            }
            else
            {
                AIStatusText = $"{LocalizationStrings.AIStatus.Ready(isThai)} ({available.Count} providers)";
                AIStatusColor = "#10B981"; // Green
            }
        }
        catch (Exception ex)
        {
            var isThai = _localizationService.IsThaiLanguage;
            AIStatusText = $"{LocalizationStrings.AIStatus.CannotCheck(isThai)} - {ex.Message}";
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

        // Update all localized text
        UpdateDemoStatus();
        _ = UpdateAIStatusAsync();
    }

    private void UpdateLanguageDisplay()
    {
        var isThai = _localizationService.IsThaiLanguage;

        if (isThai)
        {
            CurrentLanguageFlag = "🇹🇭";
            CurrentLanguageText = "TH";
        }
        else
        {
            CurrentLanguageFlag = "🇺🇸";
            CurrentLanguageText = "EN";
        }

        // Update all UI text
        SearchHint = LocalizationStrings.Common.Search(isThai);
        MinimizeTooltip = LocalizationStrings.Window.Minimize(isThai);
        MaximizeTooltip = LocalizationStrings.Window.Maximize(isThai);
        CloseTooltip = LocalizationStrings.Window.Close(isThai);
        LanguageTooltip = LocalizationStrings.Window.SwitchLanguage(isThai);

        // Update navigation menu
        NavDashboard = LocalizationStrings.Nav.Dashboard(isThai);
        NavPosts = LocalizationStrings.Nav.Posts(isThai);
        NavSchedule = LocalizationStrings.Nav.Schedule(isThai);
        NavAccounts = LocalizationStrings.Nav.Accounts(isThai);
        NavAIContent = LocalizationStrings.Nav.AIContent(isThai);
        NavVideoEditor = LocalizationStrings.Nav.VideoEditor(isThai);
        NavGroups = LocalizationStrings.Nav.Groups(isThai);
        NavComments = LocalizationStrings.Nav.Comments(isThai);
        NavWorkflows = LocalizationStrings.Nav.Workflows(isThai);
        NavSettings = LocalizationStrings.Nav.Settings(isThai);
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
