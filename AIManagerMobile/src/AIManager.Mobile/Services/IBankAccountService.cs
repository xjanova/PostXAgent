using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for managing bank account configurations
/// </summary>
public interface IBankAccountService
{
    /// <summary>
    /// Event fired when accounts are updated
    /// </summary>
    event EventHandler? AccountsUpdated;

    /// <summary>
    /// Get all configured bank accounts
    /// </summary>
    Task<List<BankAccountConfig>> GetAllAccountsAsync();

    /// <summary>
    /// Get only enabled accounts
    /// </summary>
    Task<List<BankAccountConfig>> GetEnabledAccountsAsync();

    /// <summary>
    /// Get the primary account
    /// </summary>
    Task<BankAccountConfig?> GetPrimaryAccountAsync();

    /// <summary>
    /// Get account by ID
    /// </summary>
    Task<BankAccountConfig?> GetAccountByIdAsync(string id);

    /// <summary>
    /// Add a new bank account
    /// </summary>
    Task<BankAccountConfig> AddAccountAsync(BankAccountConfig account);

    /// <summary>
    /// Update an existing account
    /// </summary>
    Task<bool> UpdateAccountAsync(BankAccountConfig account);

    /// <summary>
    /// Delete an account
    /// </summary>
    Task<bool> DeleteAccountAsync(string id);

    /// <summary>
    /// Set account as primary
    /// </summary>
    Task SetPrimaryAccountAsync(string id);

    /// <summary>
    /// Check if the app is ready to receive payments
    /// (has at least one enabled account)
    /// </summary>
    Task<bool> IsReadyForPaymentsAsync();

    /// <summary>
    /// Get bank accounts status for sharing with websites
    /// </summary>
    Task<BankAccountsStatus> GetAccountsStatusAsync();

    /// <summary>
    /// Get public info for all enabled accounts (safe to share)
    /// </summary>
    Task<List<BankAccountPublicInfo>> GetPublicAccountInfoAsync();

    /// <summary>
    /// Get count of enabled accounts
    /// </summary>
    Task<int> GetEnabledCountAsync();

    /// <summary>
    /// Save QR code image for an account
    /// </summary>
    Task<string?> SaveQrCodeAsync(string accountId, Stream imageStream);

    /// <summary>
    /// Get QR code as base64 for an account
    /// </summary>
    Task<string?> GetQrCodeBase64Async(string accountId);
}
