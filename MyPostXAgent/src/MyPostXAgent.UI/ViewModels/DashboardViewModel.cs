using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.License;

namespace MyPostXAgent.UI.ViewModels;

/// <summary>
/// ViewModel สำหรับ Dashboard
/// </summary>
public class DashboardViewModel : BaseViewModel
{
    private readonly DatabaseService _databaseService;
    private readonly LicenseService _licenseService;

    private int _totalAccounts;
    public int TotalAccounts
    {
        get => _totalAccounts;
        set => SetProperty(ref _totalAccounts, value);
    }

    private int _totalPosts;
    public int TotalPosts
    {
        get => _totalPosts;
        set => SetProperty(ref _totalPosts, value);
    }

    private int _postsToday;
    public int PostsToday
    {
        get => _postsToday;
        set => SetProperty(ref _postsToday, value);
    }

    private int _scheduledPosts;
    public int ScheduledPosts
    {
        get => _scheduledPosts;
        set => SetProperty(ref _scheduledPosts, value);
    }

    private int _failedPosts;
    public int FailedPosts
    {
        get => _failedPosts;
        set => SetProperty(ref _failedPosts, value);
    }

    private double _successRate;
    public double SuccessRate
    {
        get => _successRate;
        set => SetProperty(ref _successRate, value);
    }

    private string _licenseStatus = "";
    public string LicenseStatus
    {
        get => _licenseStatus;
        set => SetProperty(ref _licenseStatus, value);
    }

    private int _daysRemaining;
    public int DaysRemaining
    {
        get => _daysRemaining;
        set => SetProperty(ref _daysRemaining, value);
    }

    public DashboardViewModel(DatabaseService databaseService, LicenseService licenseService)
    {
        _databaseService = databaseService;
        _licenseService = licenseService;

        Title = "Dashboard";

        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;

        try
        {
            // Load accounts
            var accounts = await _databaseService.GetSocialAccountsAsync();
            TotalAccounts = accounts.Count;

            // License status
            if (_licenseService.IsDemoMode())
            {
                LicenseStatus = "Demo";
                DaysRemaining = _licenseService.GetDaysRemaining();
            }
            else if (_licenseService.IsLicensed())
            {
                var license = _licenseService.GetCurrentLicense();
                LicenseStatus = license?.LicenseType.ToString() ?? "Active";
                DaysRemaining = license?.DaysRemaining ?? 0;
            }
            else
            {
                LicenseStatus = "ไม่มี License";
                DaysRemaining = 0;
            }

            // TODO: Load more stats from database
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
