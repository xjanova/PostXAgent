using System.Windows.Input;
using MyPostXAgent.Core.Models;
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

    public ICommand BuyLicenseCommand { get; }

    public MainViewModel(LicenseService licenseService, AIContentService aiService)
    {
        _licenseService = licenseService;
        _aiService = aiService;
        Title = "MyPostXAgent";

        BuyLicenseCommand = new RelayCommand(BuyLicense);

        UpdateDemoStatus();
        _ = UpdateAIStatusAsync();

        // Update AI status every 30 seconds
        _aiStatusTimer = new System.Threading.Timer(
            async _ => await UpdateAIStatusAsync(),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
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

            if (available.Count == 0)
            {
                AIStatusText = "ไม่พร้อม";
                AIStatusColor = "#EF4444"; // Red
            }
            else if (available.Count == 1)
            {
                var provider = available[0].Provider;
                AIStatusText = $"พร้อม ({GetProviderName(provider)})";
                AIStatusColor = "#10B981"; // Green
            }
            else
            {
                AIStatusText = $"พร้อม ({available.Count} providers)";
                AIStatusColor = "#10B981"; // Green
            }
        }
        catch
        {
            AIStatusText = "ตรวจสอบไม่ได้";
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
