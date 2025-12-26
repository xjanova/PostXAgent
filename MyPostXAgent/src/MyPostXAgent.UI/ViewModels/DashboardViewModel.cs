using MyPostXAgent.Core.Services;
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
    private readonly LocalizationService _localizationService;

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

    // Page UI Text
    public string PageOverview { get; set; } = "";
    public string BtnCreateNewPost { get; set; } = "";
    public string LabelTotalAccounts { get; set; } = "";
    public string LabelPostsToday { get; set; } = "";
    public string LabelScheduled { get; set; } = "";
    public string LabelSuccessRate { get; set; } = "";
    public string LabelThisWeek { get; set; } = "";
    public string LabelVsYesterday { get; set; } = "";
    public string LabelNextIn { get; set; } = "";
    public string LabelExcellent { get; set; } = "";
    public string TitleQuickActions { get; set; } = "";
    public string ActionAIContent { get; set; } = "";
    public string ActionVideoEditor { get; set; } = "";
    public string ActionAddAccount { get; set; } = "";
    public string ActionImportFlow { get; set; } = "";
    public string TitlePlatforms { get; set; } = "";
    public string LabelAccounts { get; set; } = "";
    public string LabelPosts { get; set; } = "";
    public string TitleRecentActivity { get; set; } = "";
    public string LabelViewAll { get; set; } = "";
    public string ActivityPostSuccess { get; set; } = "";
    public string ActivityAIContentReady { get; set; } = "";
    public string ActivityPostsReady { get; set; } = "";
    public string ActivityNewAccount { get; set; } = "";
    public string TimeMinutesAgo { get; set; } = "";
    public string TimeHourAgo { get; set; } = "";
    public string LicenseUnlimitedUse { get; set; } = "";
    public string BtnManageLicense { get; set; } = "";

    public DashboardViewModel(DatabaseService databaseService, LicenseService licenseService, LocalizationService localizationService)
    {
        _databaseService = databaseService;
        _licenseService = licenseService;
        _localizationService = localizationService;

        Title = LocalizationStrings.Nav.Dashboard(_localizationService.IsThaiLanguage);
        UpdateLanguageDisplay();

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        _ = LoadDataAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.Dashboard(_localizationService.IsThaiLanguage);
        UpdateLanguageDisplay();
        _ = LoadDataAsync(); // Reload to update license status text
    }

    private void UpdateLanguageDisplay()
    {
        var isThai = _localizationService.IsThaiLanguage;

        PageOverview = LocalizationStrings.DashboardPage.Overview(isThai);
        BtnCreateNewPost = LocalizationStrings.DashboardPage.CreateNewPost(isThai);
        LabelTotalAccounts = LocalizationStrings.DashboardPage.TotalAccounts(isThai);
        LabelPostsToday = LocalizationStrings.DashboardPage.PostsToday(isThai);
        LabelScheduled = LocalizationStrings.DashboardPage.Scheduled(isThai);
        LabelSuccessRate = LocalizationStrings.DashboardPage.SuccessRate(isThai);
        LabelThisWeek = LocalizationStrings.DashboardPage.ThisWeek(isThai);
        LabelVsYesterday = LocalizationStrings.DashboardPage.VsYesterday(isThai);
        LabelNextIn = LocalizationStrings.DashboardPage.NextIn(isThai);
        LabelExcellent = LocalizationStrings.DashboardPage.Excellent(isThai);
        TitleQuickActions = LocalizationStrings.DashboardPage.QuickActions(isThai);
        ActionAIContent = LocalizationStrings.DashboardPage.AIContentAction(isThai);
        ActionVideoEditor = LocalizationStrings.DashboardPage.VideoEditorAction(isThai);
        ActionAddAccount = LocalizationStrings.DashboardPage.AddAccountAction(isThai);
        ActionImportFlow = LocalizationStrings.DashboardPage.ImportFlowAction(isThai);
        TitlePlatforms = LocalizationStrings.DashboardPage.Platforms(isThai);
        LabelAccounts = LocalizationStrings.DashboardPage.Accounts(isThai);
        LabelPosts = LocalizationStrings.DashboardPage.Posts(isThai);
        TitleRecentActivity = LocalizationStrings.DashboardPage.RecentActivity(isThai);
        LabelViewAll = LocalizationStrings.DashboardPage.ViewAll(isThai);
        ActivityPostSuccess = LocalizationStrings.DashboardPage.PostSuccess(isThai);
        ActivityAIContentReady = LocalizationStrings.DashboardPage.AIContentReady(isThai);
        ActivityPostsReady = LocalizationStrings.DashboardPage.PostsReady(isThai);
        ActivityNewAccount = LocalizationStrings.DashboardPage.NewAccount(isThai);
        TimeMinutesAgo = LocalizationStrings.DashboardPage.MinutesAgo(isThai);
        TimeHourAgo = LocalizationStrings.DashboardPage.HourAgo(isThai);
        LicenseUnlimitedUse = LocalizationStrings.DashboardPage.UnlimitedUse(isThai);
        BtnManageLicense = LocalizationStrings.DashboardPage.ManageLicense(isThai);

        OnPropertyChanged(nameof(PageOverview));
        OnPropertyChanged(nameof(BtnCreateNewPost));
        OnPropertyChanged(nameof(LabelTotalAccounts));
        OnPropertyChanged(nameof(LabelPostsToday));
        OnPropertyChanged(nameof(LabelScheduled));
        OnPropertyChanged(nameof(LabelSuccessRate));
        OnPropertyChanged(nameof(LabelThisWeek));
        OnPropertyChanged(nameof(LabelVsYesterday));
        OnPropertyChanged(nameof(LabelNextIn));
        OnPropertyChanged(nameof(LabelExcellent));
        OnPropertyChanged(nameof(TitleQuickActions));
        OnPropertyChanged(nameof(ActionAIContent));
        OnPropertyChanged(nameof(ActionVideoEditor));
        OnPropertyChanged(nameof(ActionAddAccount));
        OnPropertyChanged(nameof(ActionImportFlow));
        OnPropertyChanged(nameof(TitlePlatforms));
        OnPropertyChanged(nameof(LabelAccounts));
        OnPropertyChanged(nameof(LabelPosts));
        OnPropertyChanged(nameof(TitleRecentActivity));
        OnPropertyChanged(nameof(LabelViewAll));
        OnPropertyChanged(nameof(ActivityPostSuccess));
        OnPropertyChanged(nameof(ActivityAIContentReady));
        OnPropertyChanged(nameof(ActivityPostsReady));
        OnPropertyChanged(nameof(ActivityNewAccount));
        OnPropertyChanged(nameof(TimeMinutesAgo));
        OnPropertyChanged(nameof(TimeHourAgo));
        OnPropertyChanged(nameof(LicenseUnlimitedUse));
        OnPropertyChanged(nameof(BtnManageLicense));
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
            var isThai = _localizationService.IsThaiLanguage;
            if (_licenseService.IsDemoMode())
            {
                LicenseStatus = "Demo";
                DaysRemaining = _licenseService.GetDaysRemaining();
            }
            else if (_licenseService.IsLicensed())
            {
                var license = _licenseService.GetCurrentLicense();
                LicenseStatus = license?.LicenseType.ToString() ?? (isThai ? "ใช้งานอยู่" : "Active");
                DaysRemaining = license?.DaysRemaining ?? 0;
            }
            else
            {
                LicenseStatus = isThai ? "ไม่มี License" : "No License";
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
