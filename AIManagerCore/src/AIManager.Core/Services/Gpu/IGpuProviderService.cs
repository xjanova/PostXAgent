namespace AIManager.Core.Services.Gpu;

/// <summary>
/// Interface for GPU Cloud Provider Service
/// Handles communication with Laravel backend API for GPU provisioning
/// </summary>
public interface IGpuProviderService
{
    #region Account Management

    /// <summary>
    /// Get all GPU accounts for the current user
    /// </summary>
    /// <param name="provider">Optional: filter by provider</param>
    /// <param name="status">Optional: filter by status</param>
    /// <param name="ct">Cancellation token</param>
    Task<ApiResponse<List<GpuCloudAccount>>> ListAccountsAsync(
        string? provider = null,
        string? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get a specific GPU account by ID
    /// </summary>
    Task<ApiResponse<GpuCloudAccount>> GetAccountAsync(int accountId, CancellationToken ct = default);

    /// <summary>
    /// Create a new GPU account
    /// </summary>
    Task<ApiResponse<GpuCloudAccount>> CreateAccountAsync(
        CreateGpuAccountRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Update an existing GPU account
    /// </summary>
    Task<ApiResponse<GpuCloudAccount>> UpdateAccountAsync(
        int accountId,
        UpdateGpuAccountRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a GPU account
    /// </summary>
    Task<ApiResponse<bool>> DeleteAccountAsync(int accountId, CancellationToken ct = default);

    /// <summary>
    /// Verify account credentials with the provider
    /// </summary>
    Task<ApiResponse<AccountVerificationResult>> VerifyAccountAsync(
        int accountId,
        CancellationToken ct = default);

    /// <summary>
    /// Refresh account balance/quota from provider
    /// </summary>
    Task<ApiResponse<GpuCloudAccount>> RefreshAccountStatusAsync(
        int accountId,
        CancellationToken ct = default);

    #endregion

    #region Pool Management

    /// <summary>
    /// Get all GPU pools for the current user
    /// </summary>
    Task<ApiResponse<List<GpuAccountPool>>> ListPoolsAsync(
        string? provider = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get a specific GPU pool by ID with accounts
    /// </summary>
    Task<ApiResponse<GpuAccountPool>> GetPoolAsync(
        int poolId,
        bool includeAccounts = true,
        CancellationToken ct = default);

    /// <summary>
    /// Create a new GPU account pool
    /// </summary>
    Task<ApiResponse<GpuAccountPool>> CreatePoolAsync(
        CreateGpuPoolRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Update pool settings
    /// </summary>
    Task<ApiResponse<GpuAccountPool>> UpdatePoolAsync(
        int poolId,
        CreateGpuPoolRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Delete a GPU pool
    /// </summary>
    Task<ApiResponse<bool>> DeletePoolAsync(int poolId, CancellationToken ct = default);

    /// <summary>
    /// Add accounts to a pool
    /// </summary>
    Task<ApiResponse<GpuAccountPool>> AddAccountsToPoolAsync(
        int poolId,
        List<int> accountIds,
        CancellationToken ct = default);

    /// <summary>
    /// Remove accounts from a pool
    /// </summary>
    Task<ApiResponse<GpuAccountPool>> RemoveAccountsFromPoolAsync(
        int poolId,
        List<int> accountIds,
        CancellationToken ct = default);

    /// <summary>
    /// Rotate to next available account in pool
    /// </summary>
    Task<ApiResponse<AccountRotationEvent>> RotatePoolAccountAsync(
        int poolId,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the current active account in a pool
    /// </summary>
    Task<ApiResponse<GpuCloudAccount>> GetCurrentPoolAccountAsync(
        int poolId,
        CancellationToken ct = default);

    #endregion

    #region Offer Search

    /// <summary>
    /// Search for available GPU offers from providers
    /// </summary>
    Task<ApiResponse<List<GpuOffer>>> SearchOffersAsync(
        GpuOfferSearchCriteria criteria,
        CancellationToken ct = default);

    /// <summary>
    /// Get a specific offer by ID
    /// </summary>
    Task<ApiResponse<GpuOffer>> GetOfferAsync(
        string provider,
        string offerId,
        CancellationToken ct = default);

    /// <summary>
    /// Refresh offer pricing from provider
    /// </summary>
    Task<ApiResponse<List<GpuOffer>>> RefreshOffersAsync(
        string provider,
        CancellationToken ct = default);

    #endregion

    #region Instance Management

    /// <summary>
    /// Get all GPU instances for the current user
    /// </summary>
    Task<ApiResponse<List<GpuInstance>>> ListInstancesAsync(
        string? status = null,
        int? accountId = null,
        int? poolId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get a specific instance by ID
    /// </summary>
    Task<ApiResponse<GpuInstance>> GetInstanceAsync(int instanceId, CancellationToken ct = default);

    /// <summary>
    /// Provision a new GPU instance
    /// </summary>
    Task<ApiResponse<GpuInstance>> ProvisionInstanceAsync(
        ProvisionInstanceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Start a stopped instance
    /// </summary>
    Task<ApiResponse<GpuInstance>> StartInstanceAsync(
        int instanceId,
        CancellationToken ct = default);

    /// <summary>
    /// Stop a running instance
    /// </summary>
    Task<ApiResponse<GpuInstance>> StopInstanceAsync(
        int instanceId,
        bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Terminate an instance permanently
    /// </summary>
    Task<ApiResponse<GpuInstance>> TerminateInstanceAsync(
        int instanceId,
        bool force = false,
        CancellationToken ct = default);

    /// <summary>
    /// Get instance health status
    /// </summary>
    Task<ApiResponse<InstanceHealthCheck>> GetInstanceHealthAsync(
        int instanceId,
        CancellationToken ct = default);

    /// <summary>
    /// Execute command on instance
    /// </summary>
    Task<ApiResponse<string>> ExecuteCommandAsync(
        int instanceId,
        string command,
        int timeoutSeconds = 60,
        CancellationToken ct = default);

    #endregion

    #region Statistics & Reporting

    /// <summary>
    /// Get GPU usage statistics
    /// </summary>
    Task<ApiResponse<GpuUsageStats>> GetUsageStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default);

    #endregion

    #region Events

    /// <summary>
    /// Event raised when an account rotates
    /// </summary>
    event EventHandler<AccountRotationEvent>? AccountRotated;

    /// <summary>
    /// Event raised when instance state changes
    /// </summary>
    event EventHandler<InstanceStateChangeEvent>? InstanceStateChanged;

    #endregion
}
