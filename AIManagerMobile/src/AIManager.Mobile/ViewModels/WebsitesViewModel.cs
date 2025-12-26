using System.Collections.ObjectModel;
using System.Windows.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// ViewModel for managing website configurations
/// </summary>
public class WebsitesViewModel : BaseViewModel
{
    private readonly IWebsiteConfigService _websiteConfigService;
    private readonly IWebhookDispatchService _webhookDispatchService;

    private ObservableCollection<WebsiteConfig> _websites = new();
    private WebsiteConfig? _selectedWebsite;
    private bool _isEditing;
    private string _editName = string.Empty;
    private string _editWebhookUrl = string.Empty;
    private string _editApiKey = string.Empty;
    private string _editSecretKey = string.Empty;
    private string _editNotes = string.Empty;
    private int _enabledCount;
    private DispatchStatistics? _statistics;

    public WebsitesViewModel(
        IWebsiteConfigService websiteConfigService,
        IWebhookDispatchService webhookDispatchService)
    {
        _websiteConfigService = websiteConfigService;
        _webhookDispatchService = webhookDispatchService;

        LoadWebsitesCommand = new Command(async () => await LoadWebsitesAsync());
        AddWebsiteCommand = new Command(() => StartAddWebsite());
        EditWebsiteCommand = new Command<WebsiteConfig>(website => StartEditWebsite(website));
        DeleteWebsiteCommand = new Command<WebsiteConfig>(async website => await DeleteWebsiteAsync(website));
        SaveWebsiteCommand = new Command(async () => await SaveWebsiteAsync());
        CancelEditCommand = new Command(() => CancelEdit());
        TestConnectionCommand = new Command<WebsiteConfig>(async website => await TestConnectionAsync(website));
        ToggleEnabledCommand = new Command<WebsiteConfig>(async website => await ToggleEnabledAsync(website));
        MoveUpCommand = new Command<WebsiteConfig>(async website => await MoveUpAsync(website));
        MoveDownCommand = new Command<WebsiteConfig>(async website => await MoveDownAsync(website));
        RefreshStatisticsCommand = new Command(async () => await LoadStatisticsAsync());
    }

    public ObservableCollection<WebsiteConfig> Websites
    {
        get => _websites;
        set => SetProperty(ref _websites, value);
    }

    public WebsiteConfig? SelectedWebsite
    {
        get => _selectedWebsite;
        set => SetProperty(ref _selectedWebsite, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public string EditWebhookUrl
    {
        get => _editWebhookUrl;
        set => SetProperty(ref _editWebhookUrl, value);
    }

    public string EditApiKey
    {
        get => _editApiKey;
        set => SetProperty(ref _editApiKey, value);
    }

    public string EditSecretKey
    {
        get => _editSecretKey;
        set => SetProperty(ref _editSecretKey, value);
    }

    public string EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public int EnabledCount
    {
        get => _enabledCount;
        set => SetProperty(ref _enabledCount, value);
    }

    public DispatchStatistics? Statistics
    {
        get => _statistics;
        set => SetProperty(ref _statistics, value);
    }

    public bool IsAddingNew => SelectedWebsite == null && IsEditing;

    public ICommand LoadWebsitesCommand { get; }
    public ICommand AddWebsiteCommand { get; }
    public ICommand EditWebsiteCommand { get; }
    public ICommand DeleteWebsiteCommand { get; }
    public ICommand SaveWebsiteCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand TestConnectionCommand { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand MoveUpCommand { get; }
    public ICommand MoveDownCommand { get; }
    public ICommand RefreshStatisticsCommand { get; }

    public async Task InitializeAsync()
    {
        await LoadWebsitesAsync();
        await LoadStatisticsAsync();
    }

    private async Task LoadWebsitesAsync()
    {
        try
        {
            IsBusy = true;
            var websites = await _websiteConfigService.GetAllWebsitesAsync();
            Websites = new ObservableCollection<WebsiteConfig>(
                websites.OrderBy(w => w.Priority));
            EnabledCount = await _websiteConfigService.GetEnabledCountAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadStatisticsAsync()
    {
        Statistics = await _webhookDispatchService.GetStatisticsAsync();
    }

    private void StartAddWebsite()
    {
        SelectedWebsite = null;
        EditName = string.Empty;
        EditWebhookUrl = "https://";
        EditApiKey = GenerateRandomKey(32);
        EditSecretKey = GenerateRandomKey(64);
        EditNotes = string.Empty;
        IsEditing = true;
        OnPropertyChanged(nameof(IsAddingNew));
    }

    private void StartEditWebsite(WebsiteConfig website)
    {
        SelectedWebsite = website;
        EditName = website.Name;
        EditWebhookUrl = website.WebhookUrl;
        EditApiKey = website.ApiKey;
        EditSecretKey = website.SecretKey;
        EditNotes = website.Notes ?? string.Empty;
        IsEditing = true;
        OnPropertyChanged(nameof(IsAddingNew));
    }

    private async Task SaveWebsiteAsync()
    {
        // Validate
        if (string.IsNullOrWhiteSpace(EditName))
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", "กรุณากรอกชื่อเว็บไซต์", "ตกลง");
            return;
        }

        if (!Uri.TryCreate(EditWebhookUrl, UriKind.Absolute, out var uri))
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", "URL ไม่ถูกต้อง", "ตกลง");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditApiKey) || string.IsNullOrWhiteSpace(EditSecretKey))
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", "กรุณากรอก API Key และ Secret Key", "ตกลง");
            return;
        }

        try
        {
            IsBusy = true;

            if (SelectedWebsite == null)
            {
                // Add new
                var website = new WebsiteConfig
                {
                    Name = EditName,
                    WebhookUrl = EditWebhookUrl,
                    ApiKey = EditApiKey,
                    SecretKey = EditSecretKey,
                    Notes = EditNotes,
                    IsEnabled = true
                };

                await _websiteConfigService.AddWebsiteAsync(website);
            }
            else
            {
                // Update existing
                SelectedWebsite.Name = EditName;
                SelectedWebsite.WebhookUrl = EditWebhookUrl;
                SelectedWebsite.ApiKey = EditApiKey;
                SelectedWebsite.SecretKey = EditSecretKey;
                SelectedWebsite.Notes = EditNotes;

                await _websiteConfigService.UpdateWebsiteAsync(SelectedWebsite);
            }

            IsEditing = false;
            await LoadWebsitesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        SelectedWebsite = null;
    }

    private async Task DeleteWebsiteAsync(WebsiteConfig website)
    {
        var confirm = await Shell.Current.DisplayAlert(
            "ยืนยันการลบ",
            $"คุณต้องการลบ \"{website.Name}\" หรือไม่?",
            "ลบ", "ยกเลิก");

        if (confirm)
        {
            await _websiteConfigService.DeleteWebsiteAsync(website.Id);
            await LoadWebsitesAsync();
        }
    }

    private async Task TestConnectionAsync(WebsiteConfig website)
    {
        try
        {
            IsBusy = true;
            var (success, message) = await _websiteConfigService.TestConnectionAsync(website);

            await Shell.Current.DisplayAlert(
                success ? "สำเร็จ" : "ล้มเหลว",
                message,
                "ตกลง");

            await LoadWebsitesAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleEnabledAsync(WebsiteConfig website)
    {
        website.IsEnabled = !website.IsEnabled;
        await _websiteConfigService.UpdateWebsiteAsync(website);
        await LoadWebsitesAsync();
    }

    private async Task MoveUpAsync(WebsiteConfig website)
    {
        var websites = Websites.ToList();
        var index = websites.FindIndex(w => w.Id == website.Id);

        if (index > 0)
        {
            var ids = websites.OrderBy(w => w.Priority).Select(w => w.Id).ToList();
            var currentIndex = ids.IndexOf(website.Id);

            if (currentIndex > 0)
            {
                ids.RemoveAt(currentIndex);
                ids.Insert(currentIndex - 1, website.Id);
                await _websiteConfigService.ReorderPrioritiesAsync(ids);
                await LoadWebsitesAsync();
            }
        }
    }

    private async Task MoveDownAsync(WebsiteConfig website)
    {
        var websites = Websites.ToList();
        var ids = websites.OrderBy(w => w.Priority).Select(w => w.Id).ToList();
        var currentIndex = ids.IndexOf(website.Id);

        if (currentIndex < ids.Count - 1)
        {
            ids.RemoveAt(currentIndex);
            ids.Insert(currentIndex + 1, website.Id);
            await _websiteConfigService.ReorderPrioritiesAsync(ids);
            await LoadWebsitesAsync();
        }
    }

    private static string GenerateRandomKey(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
