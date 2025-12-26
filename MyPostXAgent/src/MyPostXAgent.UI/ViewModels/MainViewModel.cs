using System.Windows.Input;
using MyPostXAgent.Core.Services.License;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel สำหรับ MainWindow
/// </summary>
public class MainViewModel : BaseViewModel
{
    private readonly LicenseService _licenseService;

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

    public ICommand BuyLicenseCommand { get; }

    public MainViewModel(LicenseService licenseService)
    {
        _licenseService = licenseService;
        Title = "MyPostXAgent";

        BuyLicenseCommand = new RelayCommand(BuyLicense);

        UpdateDemoStatus();
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
