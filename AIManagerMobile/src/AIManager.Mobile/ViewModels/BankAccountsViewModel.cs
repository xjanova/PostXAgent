using System.Collections.ObjectModel;
using System.Windows.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// ViewModel for managing bank account configurations
/// </summary>
public class BankAccountsViewModel : BaseViewModel
{
    private readonly IBankAccountService _bankAccountService;

    private ObservableCollection<BankAccountConfig> _accounts = new();
    private BankAccountConfig? _selectedAccount;
    private bool _isEditing;
    private bool _isAddingNew;
    private int _enabledCount;
    private bool _isReady;

    // Edit form fields
    private BankType _editBankType;
    private string _editDisplayName = string.Empty;
    private string _editAccountNumber = string.Empty;
    private string _editAccountName = string.Empty;
    private string _editNotes = string.Empty;
    private bool _editIsEnabled = true;

    // Bank selection
    private ObservableCollection<BankDisplayInfo> _availableBanks = new();
    private ObservableCollection<BankDisplayInfo> _availableEWallets = new();
    private BankDisplayInfo? _selectedBankInfo;

    public BankAccountsViewModel(IBankAccountService bankAccountService)
    {
        _bankAccountService = bankAccountService;

        // Load bank options
        AvailableBanks = new ObservableCollection<BankDisplayInfo>(BankInfo.GetAllBanks());
        AvailableEWallets = new ObservableCollection<BankDisplayInfo>(BankInfo.GetAllEWallets());

        // Commands
        LoadAccountsCommand = new Command(async () => await LoadAccountsAsync());
        AddAccountCommand = new Command(() => StartAddAccount());
        EditAccountCommand = new Command<BankAccountConfig>(account => StartEditAccount(account));
        DeleteAccountCommand = new Command<BankAccountConfig>(async account => await DeleteAccountAsync(account));
        SaveAccountCommand = new Command(async () => await SaveAccountAsync());
        CancelEditCommand = new Command(() => CancelEdit());
        SetPrimaryCommand = new Command<BankAccountConfig>(async account => await SetPrimaryAsync(account));
        ToggleEnabledCommand = new Command<BankAccountConfig>(async account => await ToggleEnabledAsync(account));
        SelectBankCommand = new Command<BankDisplayInfo>(info => SelectBank(info));
        UploadQrCodeCommand = new Command<BankAccountConfig>(async account => await UploadQrCodeAsync(account));
    }

    public ObservableCollection<BankAccountConfig> Accounts
    {
        get => _accounts;
        set => SetProperty(ref _accounts, value);
    }

    public BankAccountConfig? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            SetProperty(ref _isEditing, value);
            OnPropertyChanged(nameof(IsNotEditing));
        }
    }

    public bool IsNotEditing => !IsEditing;

    public bool IsAddingNew
    {
        get => _isAddingNew;
        set => SetProperty(ref _isAddingNew, value);
    }

    public int EnabledCount
    {
        get => _enabledCount;
        set => SetProperty(ref _enabledCount, value);
    }

    public bool IsReady
    {
        get => _isReady;
        set => SetProperty(ref _isReady, value);
    }

    public string ReadyStatusText => IsReady
        ? $"พร้อมรับชำระ ({EnabledCount} บัญชี)"
        : "ยังไม่พร้อม - กรุณาเพิ่มบัญชี";

    public string ReadyStatusColor => IsReady ? "#4CAF50" : "#FF5722";

    // Edit form properties
    public BankType EditBankType
    {
        get => _editBankType;
        set
        {
            SetProperty(ref _editBankType, value);
            OnPropertyChanged(nameof(SelectedBankDisplayInfo));
        }
    }

    public string EditDisplayName
    {
        get => _editDisplayName;
        set => SetProperty(ref _editDisplayName, value);
    }

    public string EditAccountNumber
    {
        get => _editAccountNumber;
        set => SetProperty(ref _editAccountNumber, value);
    }

    public string EditAccountName
    {
        get => _editAccountName;
        set => SetProperty(ref _editAccountName, value);
    }

    public string EditNotes
    {
        get => _editNotes;
        set => SetProperty(ref _editNotes, value);
    }

    public bool EditIsEnabled
    {
        get => _editIsEnabled;
        set => SetProperty(ref _editIsEnabled, value);
    }

    public ObservableCollection<BankDisplayInfo> AvailableBanks
    {
        get => _availableBanks;
        set => SetProperty(ref _availableBanks, value);
    }

    public ObservableCollection<BankDisplayInfo> AvailableEWallets
    {
        get => _availableEWallets;
        set => SetProperty(ref _availableEWallets, value);
    }

    public BankDisplayInfo? SelectedBankInfo
    {
        get => _selectedBankInfo;
        set
        {
            SetProperty(ref _selectedBankInfo, value);
            if (value != null)
            {
                EditBankType = value.Type;
            }
        }
    }

    public BankDisplayInfo SelectedBankDisplayInfo => BankInfo.GetDisplayInfo(EditBankType);

    // Commands
    public ICommand LoadAccountsCommand { get; }
    public ICommand AddAccountCommand { get; }
    public ICommand EditAccountCommand { get; }
    public ICommand DeleteAccountCommand { get; }
    public ICommand SaveAccountCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand SetPrimaryCommand { get; }
    public ICommand ToggleEnabledCommand { get; }
    public ICommand SelectBankCommand { get; }
    public ICommand UploadQrCodeCommand { get; }

    public async Task InitializeAsync()
    {
        await LoadAccountsAsync();
    }

    private async Task LoadAccountsAsync()
    {
        try
        {
            IsBusy = true;
            var accounts = await _bankAccountService.GetAllAccountsAsync();
            Accounts = new ObservableCollection<BankAccountConfig>(
                accounts.OrderByDescending(a => a.IsPrimary).ThenBy(a => a.BankType));

            EnabledCount = await _bankAccountService.GetEnabledCountAsync();
            IsReady = await _bankAccountService.IsReadyForPaymentsAsync();

            OnPropertyChanged(nameof(ReadyStatusText));
            OnPropertyChanged(nameof(ReadyStatusColor));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void StartAddAccount()
    {
        SelectedAccount = null;
        IsAddingNew = true;

        // Reset form
        EditBankType = BankType.PromptPay; // Default to PromptPay
        EditDisplayName = string.Empty;
        EditAccountNumber = string.Empty;
        EditAccountName = string.Empty;
        EditNotes = string.Empty;
        EditIsEnabled = true;
        SelectedBankInfo = AvailableEWallets.FirstOrDefault(b => b.Type == BankType.PromptPay);

        IsEditing = true;
    }

    private void StartEditAccount(BankAccountConfig account)
    {
        SelectedAccount = account;
        IsAddingNew = false;

        // Load form with existing data
        EditBankType = account.BankType;
        EditDisplayName = account.DisplayName;
        EditAccountNumber = account.AccountNumber;
        EditAccountName = account.AccountName;
        EditNotes = account.Notes ?? string.Empty;
        EditIsEnabled = account.IsEnabled;

        var displayInfo = BankInfo.GetDisplayInfo(account.BankType);
        SelectedBankInfo = displayInfo.IsEWallet
            ? AvailableEWallets.FirstOrDefault(b => b.Type == account.BankType)
            : AvailableBanks.FirstOrDefault(b => b.Type == account.BankType);

        IsEditing = true;
    }

    private async Task SaveAccountAsync()
    {
        // Validate
        if (EditBankType == BankType.Unknown)
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", "กรุณาเลือกธนาคารหรือช่องทางชำระเงิน", "ตกลง");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditAccountNumber))
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", "กรุณากรอกเลขบัญชีหรือเบอร์พร้อมเพย์", "ตกลง");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditAccountName))
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", "กรุณากรอกชื่อบัญชี", "ตกลง");
            return;
        }

        try
        {
            IsBusy = true;

            if (IsAddingNew)
            {
                var displayInfo = BankInfo.GetDisplayInfo(EditBankType);
                var account = new BankAccountConfig
                {
                    BankType = EditBankType,
                    DisplayName = string.IsNullOrWhiteSpace(EditDisplayName)
                        ? displayInfo.NameTh
                        : EditDisplayName,
                    AccountNumber = EditAccountNumber.Trim(),
                    AccountName = EditAccountName.Trim(),
                    Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes,
                    IsEnabled = EditIsEnabled
                };

                await _bankAccountService.AddAccountAsync(account);
                await Shell.Current.DisplayAlert("สำเร็จ", "เพิ่มบัญชีเรียบร้อยแล้ว", "ตกลง");
            }
            else if (SelectedAccount != null)
            {
                var displayInfo = BankInfo.GetDisplayInfo(EditBankType);
                SelectedAccount.BankType = EditBankType;
                SelectedAccount.DisplayName = string.IsNullOrWhiteSpace(EditDisplayName)
                    ? displayInfo.NameTh
                    : EditDisplayName;
                SelectedAccount.AccountNumber = EditAccountNumber.Trim();
                SelectedAccount.AccountName = EditAccountName.Trim();
                SelectedAccount.Notes = string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes;
                SelectedAccount.IsEnabled = EditIsEnabled;

                await _bankAccountService.UpdateAccountAsync(SelectedAccount);
            }

            IsEditing = false;
            await LoadAccountsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        SelectedAccount = null;
    }

    private async Task DeleteAccountAsync(BankAccountConfig account)
    {
        var confirm = await Shell.Current.DisplayAlert(
            "ยืนยันการลบ",
            $"คุณต้องการลบบัญชี \"{account.DisplayName}\" หรือไม่?",
            "ลบ", "ยกเลิก");

        if (confirm)
        {
            await _bankAccountService.DeleteAccountAsync(account.Id);
            await LoadAccountsAsync();
        }
    }

    private async Task SetPrimaryAsync(BankAccountConfig account)
    {
        await _bankAccountService.SetPrimaryAccountAsync(account.Id);
        await LoadAccountsAsync();
    }

    private async Task ToggleEnabledAsync(BankAccountConfig account)
    {
        account.IsEnabled = !account.IsEnabled;
        await _bankAccountService.UpdateAccountAsync(account);
        await LoadAccountsAsync();
    }

    private void SelectBank(BankDisplayInfo info)
    {
        SelectedBankInfo = info;
        EditBankType = info.Type;

        // Set default display name
        if (string.IsNullOrWhiteSpace(EditDisplayName))
        {
            EditDisplayName = info.NameTh;
        }
    }

    private async Task UploadQrCodeAsync(BankAccountConfig account)
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "เลือกรูป QR Code"
            });

            if (result != null)
            {
                using var stream = await result.OpenReadAsync();
                var path = await _bankAccountService.SaveQrCodeAsync(account.Id, stream);

                if (path != null)
                {
                    await Shell.Current.DisplayAlert("สำเร็จ", "บันทึก QR Code เรียบร้อยแล้ว", "ตกลง");
                    await LoadAccountsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("ข้อผิดพลาด", $"ไม่สามารถบันทึก QR Code: {ex.Message}", "ตกลง");
        }
    }
}
