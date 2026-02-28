using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.Gpu;

/// <summary>
/// GPU Account Rotation Service
/// Handles automatic rotation of GPU accounts within pools on the C# side
/// Works in conjunction with Laravel backend for persistence
/// </summary>
public class GpuAccountRotationService : IDisposable
{
    private readonly IGpuProviderService _providerService;
    private readonly ILogger<GpuAccountRotationService>? _logger;

    private readonly Dictionary<int, GpuAccountPool> _cachedPools = new();
    private readonly Dictionary<int, GpuCloudAccount> _currentAccounts = new();
    private readonly Dictionary<int, int> _failureCounters = new();
    private readonly Dictionary<int, DateTime> _lastRotationTimes = new();

    private Timer? _healthCheckTimer;
    private Timer? _quotaCheckTimer;
    private bool _disposed;

    // Configuration
    private int _maxConsecutiveFailures = 3;
    private int _quotaWarningThresholdMinutes = 30;
    private TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(5);
    private TimeSpan _quotaCheckInterval = TimeSpan.FromMinutes(15);
    private bool _autoRotateOnQuotaLow = true;
    private bool _autoRotateOnError = true;

    /// <summary>
    /// Event raised when automatic rotation occurs
    /// </summary>
    public event EventHandler<AccountRotationEvent>? AccountRotated;

    /// <summary>
    /// Event raised when a pool has no available accounts
    /// </summary>
    public event EventHandler<PoolExhaustedEventArgs>? PoolExhausted;

    /// <summary>
    /// Event raised when quota is running low
    /// </summary>
    public event EventHandler<QuotaWarningEventArgs>? QuotaWarning;

    public GpuAccountRotationService(
        IGpuProviderService providerService,
        ILogger<GpuAccountRotationService>? logger = null)
    {
        _providerService = providerService;
        _logger = logger;

        // Subscribe to provider events
        _providerService.AccountRotated += OnProviderAccountRotated;
    }

    #region Configuration

    /// <summary>
    /// Configure rotation settings
    /// </summary>
    public void Configure(
        int maxConsecutiveFailures = 3,
        int quotaWarningThresholdMinutes = 30,
        bool autoRotateOnQuotaLow = true,
        bool autoRotateOnError = true,
        TimeSpan? healthCheckInterval = null,
        TimeSpan? quotaCheckInterval = null)
    {
        _maxConsecutiveFailures = maxConsecutiveFailures;
        _quotaWarningThresholdMinutes = quotaWarningThresholdMinutes;
        _autoRotateOnQuotaLow = autoRotateOnQuotaLow;
        _autoRotateOnError = autoRotateOnError;

        if (healthCheckInterval.HasValue)
            _healthCheckInterval = healthCheckInterval.Value;

        if (quotaCheckInterval.HasValue)
            _quotaCheckInterval = quotaCheckInterval.Value;

        _logger?.LogInformation(
            "Rotation service configured: MaxFailures={MaxFailures}, QuotaWarning={QuotaWarning}min, " +
            "AutoRotateQuota={AutoRotateQuota}, AutoRotateError={AutoRotateError}",
            _maxConsecutiveFailures, _quotaWarningThresholdMinutes,
            _autoRotateOnQuotaLow, _autoRotateOnError);
    }

    /// <summary>
    /// Start automatic monitoring and rotation
    /// </summary>
    public void StartMonitoring()
    {
        _logger?.LogInformation("Starting GPU account rotation monitoring");

        _healthCheckTimer?.Dispose();
        _quotaCheckTimer?.Dispose();

        _healthCheckTimer = new Timer(
            async _ => await PerformHealthChecksAsync(),
            null,
            TimeSpan.Zero,
            _healthCheckInterval);

        _quotaCheckTimer = new Timer(
            async _ => await CheckQuotasAsync(),
            null,
            TimeSpan.FromSeconds(30),
            _quotaCheckInterval);
    }

    /// <summary>
    /// Stop automatic monitoring
    /// </summary>
    public void StopMonitoring()
    {
        _logger?.LogInformation("Stopping GPU account rotation monitoring");

        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;

        _quotaCheckTimer?.Dispose();
        _quotaCheckTimer = null;
    }

    #endregion

    #region Pool Management

    /// <summary>
    /// Register a pool for rotation management
    /// </summary>
    public async Task<bool> RegisterPoolAsync(int poolId, CancellationToken ct = default)
    {
        try
        {
            var result = await _providerService.GetPoolAsync(poolId, true, ct);
            if (!result.Success || result.Data == null)
            {
                _logger?.LogWarning("Failed to register pool {PoolId}: {Message}", poolId, result.Message);
                return false;
            }

            _cachedPools[poolId] = result.Data;
            _failureCounters[poolId] = 0;

            // Get current account
            if (result.Data.CurrentAccountId.HasValue)
            {
                var currentAccount = result.Data.Accounts?.FirstOrDefault(a => a.Id == result.Data.CurrentAccountId);
                if (currentAccount != null)
                {
                    _currentAccounts[poolId] = currentAccount;
                }
            }
            else if (result.Data.Accounts?.Count > 0)
            {
                _currentAccounts[poolId] = result.Data.Accounts[0];
            }

            _logger?.LogInformation("Registered pool {PoolId} ({Name}) with {Count} accounts",
                poolId, result.Data.Name, result.Data.Accounts?.Count ?? 0);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to register pool {PoolId}", poolId);
            return false;
        }
    }

    /// <summary>
    /// Unregister a pool from rotation management
    /// </summary>
    public void UnregisterPool(int poolId)
    {
        _cachedPools.Remove(poolId);
        _currentAccounts.Remove(poolId);
        _failureCounters.Remove(poolId);
        _lastRotationTimes.Remove(poolId);

        _logger?.LogInformation("Unregistered pool {PoolId}", poolId);
    }

    /// <summary>
    /// Refresh pool data from backend
    /// </summary>
    public async Task RefreshPoolAsync(int poolId, CancellationToken ct = default)
    {
        var result = await _providerService.GetPoolAsync(poolId, true, ct);
        if (result.Success && result.Data != null)
        {
            _cachedPools[poolId] = result.Data;

            if (result.Data.CurrentAccountId.HasValue)
            {
                var currentAccount = result.Data.Accounts?.FirstOrDefault(a => a.Id == result.Data.CurrentAccountId);
                if (currentAccount != null)
                {
                    _currentAccounts[poolId] = currentAccount;
                }
            }
        }
    }

    /// <summary>
    /// Get current account for a pool
    /// </summary>
    public GpuCloudAccount? GetCurrentAccount(int poolId)
    {
        return _currentAccounts.TryGetValue(poolId, out var account) ? account : null;
    }

    /// <summary>
    /// Get cached pool info
    /// </summary>
    public GpuAccountPool? GetPool(int poolId)
    {
        return _cachedPools.TryGetValue(poolId, out var pool) ? pool : null;
    }

    #endregion

    #region Rotation Logic

    /// <summary>
    /// Report a failure for the current account in a pool
    /// </summary>
    public async Task<GpuCloudAccount?> ReportFailureAsync(
        int poolId,
        string errorMessage,
        CancellationToken ct = default)
    {
        _failureCounters.TryGetValue(poolId, out var currentCount);
        currentCount++;
        _failureCounters[poolId] = currentCount;

        _logger?.LogWarning("Failure reported for pool {PoolId}. Count: {Count}/{Max}. Error: {Error}",
            poolId, currentCount, _maxConsecutiveFailures, errorMessage);

        if (_autoRotateOnError && currentCount >= _maxConsecutiveFailures)
        {
            _logger?.LogInformation("Max failures reached for pool {PoolId}. Rotating account.", poolId);
            return await RotateAccountAsync(poolId, $"Max failures reached: {errorMessage}", ct);
        }

        return GetCurrentAccount(poolId);
    }

    /// <summary>
    /// Report success for the current account (resets failure counter)
    /// </summary>
    public void ReportSuccess(int poolId)
    {
        _failureCounters[poolId] = 0;
    }

    /// <summary>
    /// Manually trigger rotation to next account
    /// </summary>
    public async Task<GpuCloudAccount?> RotateAccountAsync(
        int poolId,
        string? reason = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Rotating account for pool {PoolId}. Reason: {Reason}", poolId, reason);

            var result = await _providerService.RotatePoolAccountAsync(poolId, reason, ct);

            if (result.Success && result.Data != null)
            {
                // Reset failure counter
                _failureCounters[poolId] = 0;
                _lastRotationTimes[poolId] = DateTime.UtcNow;

                // Refresh pool data
                await RefreshPoolAsync(poolId, ct);

                var newAccount = GetCurrentAccount(poolId);

                AccountRotated?.Invoke(this, result.Data);

                _logger?.LogInformation("Rotated to account {AccountId} ({Name}) in pool {PoolId}",
                    result.Data.NewAccountId, result.Data.NewAccountName, poolId);

                return newAccount;
            }

            _logger?.LogWarning("Failed to rotate account for pool {PoolId}: {Message}",
                poolId, result.Message);

            // Check if pool is exhausted
            if (result.Message?.Contains("no available", StringComparison.OrdinalIgnoreCase) == true)
            {
                PoolExhausted?.Invoke(this, new PoolExhaustedEventArgs
                {
                    PoolId = poolId,
                    PoolName = _cachedPools.TryGetValue(poolId, out var pool) ? pool.Name : null,
                    Reason = result.Message
                });
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error rotating account for pool {PoolId}", poolId);
            return null;
        }
    }

    /// <summary>
    /// Get next available account without rotating
    /// </summary>
    public GpuCloudAccount? GetNextAvailableAccount(int poolId)
    {
        if (!_cachedPools.TryGetValue(poolId, out var pool) || pool.Accounts == null)
            return null;

        var currentId = _currentAccounts.TryGetValue(poolId, out var current) ? current.Id : 0;

        // Find next available account based on rotation strategy
        var availableAccounts = pool.Accounts
            .Where(a => a.IsAvailable && a.Id != currentId)
            .ToList();

        if (availableAccounts.Count == 0)
            return null;

        return pool.RotationStrategyType switch
        {
            PoolRotationStrategy.Priority => availableAccounts.OrderBy(a => a.Priority).First(),
            PoolRotationStrategy.LeastUsed => availableAccounts.OrderBy(a => a.QuotaUsedMinutes).First(),
            PoolRotationStrategy.CostOptimized => availableAccounts.OrderBy(a => a.Balance ?? decimal.MaxValue).First(),
            PoolRotationStrategy.Random => availableAccounts[Random.Shared.Next(availableAccounts.Count)],
            _ => availableAccounts.First() // Round Robin
        };
    }

    #endregion

    #region Monitoring

    private async Task PerformHealthChecksAsync()
    {
        foreach (var poolId in _cachedPools.Keys.ToList())
        {
            try
            {
                await RefreshPoolAsync(poolId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Health check failed for pool {PoolId}", poolId);
            }
        }
    }

    private async Task CheckQuotasAsync()
    {
        foreach (var kvp in _currentAccounts.ToList())
        {
            var poolId = kvp.Key;
            var account = kvp.Value;

            try
            {
                // Refresh account status
                var result = await _providerService.RefreshAccountStatusAsync(account.Id);
                if (result.Success && result.Data != null)
                {
                    // Update cached account
                    _currentAccounts[poolId] = result.Data;
                    account = result.Data;

                    // Update in pool cache
                    if (_cachedPools.TryGetValue(poolId, out var pool) && pool.Accounts != null)
                    {
                        var idx = pool.Accounts.FindIndex(a => a.Id == account.Id);
                        if (idx >= 0) pool.Accounts[idx] = account;
                    }
                }

                // Check quota
                if (account.RemainingQuotaMinutes <= _quotaWarningThresholdMinutes)
                {
                    _logger?.LogWarning(
                        "Low quota warning for account {AccountId} ({Name}) in pool {PoolId}: {Remaining} minutes remaining",
                        account.Id, account.Name, poolId, account.RemainingQuotaMinutes);

                    QuotaWarning?.Invoke(this, new QuotaWarningEventArgs
                    {
                        PoolId = poolId,
                        AccountId = account.Id,
                        AccountName = account.Name,
                        RemainingMinutes = account.RemainingQuotaMinutes,
                        LimitMinutes = account.QuotaLimitMinutes ?? 0
                    });

                    // Auto-rotate if configured
                    if (_autoRotateOnQuotaLow && account.RemainingQuotaMinutes <= 0)
                    {
                        await RotateAccountAsync(poolId, "Quota exhausted");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Quota check failed for pool {PoolId}", poolId);
            }
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Get pool statistics
    /// </summary>
    public PoolRotationStats GetPoolStats(int poolId)
    {
        var pool = GetPool(poolId);
        var current = GetCurrentAccount(poolId);

        return new PoolRotationStats
        {
            PoolId = poolId,
            PoolName = pool?.Name,
            TotalAccounts = pool?.Accounts?.Count ?? 0,
            AvailableAccounts = pool?.Accounts?.Count(a => a.IsAvailable) ?? 0,
            CurrentAccountId = current?.Id,
            CurrentAccountName = current?.Name,
            CurrentAccountRemainingMinutes = current?.RemainingQuotaMinutes ?? 0,
            FailureCount = _failureCounters.GetValueOrDefault(poolId, 0),
            LastRotationTime = _lastRotationTimes.GetValueOrDefault(poolId)
        };
    }

    /// <summary>
    /// Get all registered pool stats
    /// </summary>
    public List<PoolRotationStats> GetAllPoolStats()
    {
        return _cachedPools.Keys.Select(GetPoolStats).ToList();
    }

    private void OnProviderAccountRotated(object? sender, AccountRotationEvent e)
    {
        // Sync with backend rotation events
        if (_cachedPools.ContainsKey(e.PoolId))
        {
            _ = RefreshPoolAsync(e.PoolId);
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            StopMonitoring();
            _providerService.AccountRotated -= OnProviderAccountRotated;
        }

        _disposed = true;
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event args for pool exhausted event
/// </summary>
public class PoolExhaustedEventArgs : EventArgs
{
    public int PoolId { get; set; }
    public string? PoolName { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Event args for quota warning event
/// </summary>
public class QuotaWarningEventArgs : EventArgs
{
    public int PoolId { get; set; }
    public int AccountId { get; set; }
    public string? AccountName { get; set; }
    public int RemainingMinutes { get; set; }
    public int LimitMinutes { get; set; }
}

/// <summary>
/// Pool rotation statistics
/// </summary>
public class PoolRotationStats
{
    public int PoolId { get; set; }
    public string? PoolName { get; set; }
    public int TotalAccounts { get; set; }
    public int AvailableAccounts { get; set; }
    public int? CurrentAccountId { get; set; }
    public string? CurrentAccountName { get; set; }
    public int CurrentAccountRemainingMinutes { get; set; }
    public int FailureCount { get; set; }
    public DateTime? LastRotationTime { get; set; }

    public bool IsHealthy => AvailableAccounts > 0 && CurrentAccountRemainingMinutes > 30;
}

#endregion
