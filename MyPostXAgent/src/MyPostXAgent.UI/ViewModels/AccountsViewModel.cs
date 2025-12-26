using System.Collections.ObjectModel;
using System.Windows;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.UI.ViewModels;

public class AccountsViewModel : BaseViewModel
{
    private readonly DatabaseService _database;
    private readonly LocalizationService _localizationService;

    public ObservableCollection<SocialAccount> Accounts { get; } = new();
    public ObservableCollection<SocialPlatform> Platforms { get; } = new();

    private SocialAccount? _selectedAccount;
    public SocialAccount? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    private SocialPlatform? _selectedPlatformFilter;
    public SocialPlatform? SelectedPlatformFilter
    {
        get => _selectedPlatformFilter;
        set
        {
            if (SetProperty(ref _selectedPlatformFilter, value))
            {
                _ = LoadAccountsAsync();
            }
        }
    }

    private bool _showAddDialog;
    public bool ShowAddDialog
    {
        get => _showAddDialog;
        set => SetProperty(ref _showAddDialog, value);
    }

    // Add Account Form
    private SocialPlatform _newAccountPlatform = SocialPlatform.Facebook;
    public SocialPlatform NewAccountPlatform
    {
        get => _newAccountPlatform;
        set => SetProperty(ref _newAccountPlatform, value);
    }

    private string _newAccountName = "";
    public string NewAccountName
    {
        get => _newAccountName;
        set => SetProperty(ref _newAccountName, value);
    }

    private string _newAccountEmail = "";
    public string NewAccountEmail
    {
        get => _newAccountEmail;
        set => SetProperty(ref _newAccountEmail, value);
    }

    private string _newAccountPassword = "";
    public string NewAccountPassword
    {
        get => _newAccountPassword;
        set => SetProperty(ref _newAccountPassword, value);
    }

    // Commands
    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddAccountCommand { get; }
    public RelayCommand<SocialAccount> EditAccountCommand { get; }
    public RelayCommand<SocialAccount> DeleteAccountCommand { get; }
    public RelayCommand<SocialAccount> ToggleActiveCommand { get; }
    public RelayCommand SaveNewAccountCommand { get; }
    public RelayCommand CancelAddCommand { get; }

    public AccountsViewModel(DatabaseService database, LocalizationService localizationService)
    {
        _database = database;
        _localizationService = localizationService;

        Title = LocalizationStrings.Nav.Accounts(_localizationService.IsThaiLanguage);

        // Populate platforms
        foreach (SocialPlatform platform in Enum.GetValues(typeof(SocialPlatform)))
        {
            Platforms.Add(platform);
        }

        // Commands
        RefreshCommand = new RelayCommand(async () => await LoadAccountsAsync());
        AddAccountCommand = new RelayCommand(() => ShowAddDialog = true);
        EditAccountCommand = new RelayCommand<SocialAccount>(EditAccount);
        DeleteAccountCommand = new RelayCommand<SocialAccount>(async acc => await DeleteAccountAsync(acc));
        ToggleActiveCommand = new RelayCommand<SocialAccount>(async acc => await ToggleActiveAsync(acc));
        SaveNewAccountCommand = new RelayCommand(async () => await SaveNewAccountAsync());
        CancelAddCommand = new RelayCommand(CancelAdd);

        // Subscribe to language changes
        _localizationService.LanguageChanged += OnLanguageChanged;

        // Load initial data
        _ = LoadAccountsAsync();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Title = LocalizationStrings.Nav.Accounts(_localizationService.IsThaiLanguage);
    }

    public async Task LoadAccountsAsync()
    {
        try
        {
            IsBusy = true;
            var accounts = await _database.GetSocialAccountsAsync(SelectedPlatformFilter);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Accounts.Clear();
                foreach (var account in accounts)
                {
                    Accounts.Add(account);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading accounts: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void EditAccount(SocialAccount? account)
    {
        if (account == null) return;
        SelectedAccount = account;
        // TODO: Show edit dialog
    }

    private async Task DeleteAccountAsync(SocialAccount? account)
    {
        if (account == null) return;

        var result = MessageBox.Show(
            $"ต้องการลบบัญชี {account.AccountName} หรือไม่?",
            "ยืนยันการลบ",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await _database.DeleteSocialAccountAsync(account.Id);
                Accounts.Remove(account);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async Task ToggleActiveAsync(SocialAccount? account)
    {
        if (account == null) return;

        account.IsActive = !account.IsActive;
        await _database.UpdateSocialAccountAsync(account);
    }

    private async Task SaveNewAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(NewAccountName))
        {
            MessageBox.Show("กรุณากรอกชื่อบัญชี", "ข้อมูลไม่ครบ", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var account = new SocialAccount
            {
                Platform = NewAccountPlatform,
                AccountName = NewAccountName,
                DisplayName = NewAccountName,
                IsActive = true,
                HealthStatus = AccountHealthStatus.Healthy,
                CreatedAt = DateTime.UtcNow
            };

            var id = await _database.AddSocialAccountAsync(account);
            account.Id = id;

            // Save credentials if provided
            if (!string.IsNullOrWhiteSpace(NewAccountEmail) || !string.IsNullOrWhiteSpace(NewAccountPassword))
            {
                var credentials = new AccountCredentials
                {
                    AccountId = id,
                    Email = NewAccountEmail,
                    Password = NewAccountPassword
                };
                await _database.SaveAccountCredentialsAsync(credentials);
            }

            Accounts.Add(account);
            CancelAdd();

            MessageBox.Show("เพิ่มบัญชีสำเร็จ!", "สำเร็จ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"เกิดข้อผิดพลาด: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelAdd()
    {
        ShowAddDialog = false;
        NewAccountName = "";
        NewAccountEmail = "";
        NewAccountPassword = "";
        NewAccountPlatform = SocialPlatform.Facebook;
    }
}
