using System.Text.Json;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for managing bank account configurations
/// Stores configurations in MAUI SecureStorage
/// </summary>
public class BankAccountService : IBankAccountService
{
    private const string StorageKey = "bank_accounts";
    private const string QrCodeFolder = "qrcodes";
    private List<BankAccountConfig>? _cachedAccounts;

    public event EventHandler? AccountsUpdated;

    public async Task<List<BankAccountConfig>> GetAllAccountsAsync()
    {
        if (_cachedAccounts != null)
        {
            return _cachedAccounts;
        }

        try
        {
            var json = await SecureStorage.GetAsync(StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _cachedAccounts = JsonSerializer.Deserialize<List<BankAccountConfig>>(json) ?? new();
            }
            else
            {
                _cachedAccounts = new();
            }
        }
        catch
        {
            _cachedAccounts = new();
        }

        return _cachedAccounts;
    }

    public async Task<List<BankAccountConfig>> GetEnabledAccountsAsync()
    {
        var accounts = await GetAllAccountsAsync();
        return accounts
            .Where(a => a.IsEnabled)
            .OrderByDescending(a => a.IsPrimary)
            .ThenBy(a => a.BankType)
            .ToList();
    }

    public async Task<BankAccountConfig?> GetPrimaryAccountAsync()
    {
        var accounts = await GetAllAccountsAsync();
        return accounts.FirstOrDefault(a => a.IsPrimary && a.IsEnabled)
            ?? accounts.FirstOrDefault(a => a.IsEnabled);
    }

    public async Task<BankAccountConfig?> GetAccountByIdAsync(string id)
    {
        var accounts = await GetAllAccountsAsync();
        return accounts.FirstOrDefault(a => a.Id == id);
    }

    public async Task<BankAccountConfig> AddAccountAsync(BankAccountConfig account)
    {
        var accounts = await GetAllAccountsAsync();

        // Assign ID if not set
        if (string.IsNullOrEmpty(account.Id))
        {
            account.Id = Guid.NewGuid().ToString();
        }

        // Set creation time
        account.CreatedAt = DateTime.UtcNow;

        // If first account, make it primary
        if (!accounts.Any(a => a.IsEnabled))
        {
            account.IsPrimary = true;
        }

        accounts.Add(account);
        await SaveAsync(accounts);

        AccountsUpdated?.Invoke(this, EventArgs.Empty);
        return account;
    }

    public async Task<bool> UpdateAccountAsync(BankAccountConfig account)
    {
        var accounts = await GetAllAccountsAsync();
        var index = accounts.FindIndex(a => a.Id == account.Id);

        if (index < 0)
        {
            return false;
        }

        accounts[index] = account;
        await SaveAsync(accounts);

        AccountsUpdated?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task<bool> DeleteAccountAsync(string id)
    {
        var accounts = await GetAllAccountsAsync();
        var account = accounts.FirstOrDefault(a => a.Id == id);

        if (account == null)
        {
            return false;
        }

        // Delete QR code if exists
        if (!string.IsNullOrEmpty(account.QrCodePath))
        {
            try
            {
                File.Delete(account.QrCodePath);
            }
            catch { }
        }

        accounts.Remove(account);

        // If deleted primary, set new primary
        if (account.IsPrimary && accounts.Any(a => a.IsEnabled))
        {
            accounts.First(a => a.IsEnabled).IsPrimary = true;
        }

        await SaveAsync(accounts);

        AccountsUpdated?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public async Task SetPrimaryAccountAsync(string id)
    {
        var accounts = await GetAllAccountsAsync();

        foreach (var account in accounts)
        {
            account.IsPrimary = account.Id == id;
        }

        await SaveAsync(accounts);
        AccountsUpdated?.Invoke(this, EventArgs.Empty);
    }

    public async Task<bool> IsReadyForPaymentsAsync()
    {
        var accounts = await GetEnabledAccountsAsync();
        return accounts.Any();
    }

    public async Task<BankAccountsStatus> GetAccountsStatusAsync()
    {
        var accounts = await GetEnabledAccountsAsync();
        var isReady = accounts.Any();

        return new BankAccountsStatus
        {
            IsReady = isReady,
            EnabledAccountsCount = accounts.Count,
            Message = isReady
                ? null
                : "แอพยังไม่พร้อมรับชำระเงิน กรุณาเพิ่มบัญชีธนาคารอย่างน้อย 1 บัญชี หรือใช้ช่องทางอื่นในการชำระเงิน",
            Accounts = await GetPublicAccountInfoAsync()
        };
    }

    public async Task<List<BankAccountPublicInfo>> GetPublicAccountInfoAsync()
    {
        var accounts = await GetEnabledAccountsAsync();
        var publicInfoList = new List<BankAccountPublicInfo>();

        foreach (var account in accounts)
        {
            var displayInfo = account.GetDisplayInfo();
            var qrBase64 = await GetQrCodeBase64Async(account.Id);

            publicInfoList.Add(new BankAccountPublicInfo
            {
                Id = account.Id,
                BankType = account.BankType,
                BankNameTh = displayInfo.NameTh,
                BankNameEn = displayInfo.NameEn,
                BankShortName = displayInfo.ShortName,
                BankColor = displayInfo.PrimaryColor,
                AccountNumber = account.AccountNumber,
                AccountName = account.AccountName,
                IsPrimary = account.IsPrimary,
                IsEWallet = displayInfo.IsEWallet,
                QrCodeBase64 = qrBase64
            });
        }

        return publicInfoList;
    }

    public async Task<int> GetEnabledCountAsync()
    {
        var accounts = await GetAllAccountsAsync();
        return accounts.Count(a => a.IsEnabled);
    }

    public async Task<string?> SaveQrCodeAsync(string accountId, Stream imageStream)
    {
        try
        {
            var folder = Path.Combine(FileSystem.AppDataDirectory, QrCodeFolder);
            Directory.CreateDirectory(folder);

            var fileName = $"{accountId}.png";
            var filePath = Path.Combine(folder, fileName);

            using var fileStream = File.Create(filePath);
            await imageStream.CopyToAsync(fileStream);

            // Update account
            var account = await GetAccountByIdAsync(accountId);
            if (account != null)
            {
                account.QrCodePath = filePath;
                await UpdateAccountAsync(account);
            }

            return filePath;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetQrCodeBase64Async(string accountId)
    {
        var account = await GetAccountByIdAsync(accountId);
        if (account == null || string.IsNullOrEmpty(account.QrCodePath))
        {
            return null;
        }

        try
        {
            if (File.Exists(account.QrCodePath))
            {
                var bytes = await File.ReadAllBytesAsync(account.QrCodePath);
                return Convert.ToBase64String(bytes);
            }
        }
        catch { }

        return null;
    }

    private async Task SaveAsync(List<BankAccountConfig> accounts)
    {
        _cachedAccounts = accounts;
        var json = JsonSerializer.Serialize(accounts);
        await SecureStorage.SetAsync(StorageKey, json);
    }
}
