using System.Collections.Concurrent;
using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Account Pool Manager - จัดการ pool ของ social accounts สำหรับ rotation
/// รองรับ multi-account per platform, rate limiting, health monitoring
/// </summary>
public class AccountPoolManager
{
    private readonly ConcurrentDictionary<string, AccountPool> _pools = new();
    private readonly ConcurrentDictionary<string, AccountStatus> _accountStatuses = new();
    private readonly LoggingService _logger;
    private readonly object _rotationLock = new();

    public AccountPoolManager(LoggingService logger)
    {
        _logger = logger;
    }

    #region Pool Management

    /// <summary>
    /// สร้าง pool ใหม่สำหรับ platform
    /// </summary>
    public AccountPool CreatePool(SocialPlatform platform, string poolName)
    {
        var poolId = $"{platform}_{poolName}_{Guid.NewGuid():N}";
        var pool = new AccountPool
        {
            Id = poolId,
            Name = poolName,
            Platform = platform,
            CreatedAt = DateTime.UtcNow
        };

        _pools[poolId] = pool;
        _logger.LogInfo($"Created account pool: {poolName} for {platform}");
        return pool;
    }

    /// <summary>
    /// ดึง pool ทั้งหมดสำหรับ platform
    /// </summary>
    public List<AccountPool> GetPools(SocialPlatform? platform = null)
    {
        if (platform.HasValue)
        {
            return _pools.Values.Where(p => p.Platform == platform.Value).ToList();
        }
        return _pools.Values.ToList();
    }

    /// <summary>
    /// ลบ pool
    /// </summary>
    public bool DeletePool(string poolId)
    {
        if (_pools.TryRemove(poolId, out var pool))
        {
            // Remove all account statuses in this pool
            foreach (var account in pool.Accounts)
            {
                _accountStatuses.TryRemove(account.Id, out _);
            }
            _logger.LogInfo($"Deleted pool: {pool.Name}");
            return true;
        }
        return false;
    }

    #endregion

    #region Account Management

    /// <summary>
    /// เพิ่ม account เข้า pool
    /// </summary>
    public PoolAccount AddAccount(string poolId, string accountName, PlatformCredentials credentials, int priority = 0)
    {
        if (!_pools.TryGetValue(poolId, out var pool))
        {
            throw new ArgumentException($"Pool not found: {poolId}");
        }

        var account = new PoolAccount
        {
            Id = $"acc_{Guid.NewGuid():N}",
            PoolId = poolId,
            Name = accountName,
            Credentials = credentials,
            Priority = priority,
            AddedAt = DateTime.UtcNow,
            IsActive = true
        };

        pool.Accounts.Add(account);

        // Initialize status
        _accountStatuses[account.Id] = new AccountStatus
        {
            AccountId = account.Id,
            State = AccountState.Active,
            LastUsed = null,
            DailyPostCount = 0,
            DailyLimitResetAt = DateTime.UtcNow.Date.AddDays(1)
        };

        _logger.LogInfo($"Added account {accountName} to pool {pool.Name}");
        return account;
    }

    /// <summary>
    /// ลบ account จาก pool
    /// </summary>
    public bool RemoveAccount(string poolId, string accountId)
    {
        if (!_pools.TryGetValue(poolId, out var pool))
        {
            return false;
        }

        var account = pool.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (account != null)
        {
            pool.Accounts.Remove(account);
            _accountStatuses.TryRemove(accountId, out _);
            _logger.LogInfo($"Removed account {account.Name} from pool {pool.Name}");
            return true;
        }
        return false;
    }

    /// <summary>
    /// อัพเดท account credentials
    /// </summary>
    public bool UpdateCredentials(string accountId, PlatformCredentials newCredentials)
    {
        foreach (var pool in _pools.Values)
        {
            var account = pool.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.Credentials = newCredentials;
                account.LastCredentialUpdate = DateTime.UtcNow;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// ดึง accounts ทั้งหมดใน pool
    /// </summary>
    public List<PoolAccount> GetAccounts(string poolId)
    {
        if (_pools.TryGetValue(poolId, out var pool))
        {
            return pool.Accounts.ToList();
        }
        return new List<PoolAccount>();
    }

    #endregion

    #region Account Rotation

    /// <summary>
    /// ดึง account ที่เหมาะสมที่สุดสำหรับการใช้งาน
    /// </summary>
    public PoolAccount? GetNextAvailableAccount(SocialPlatform platform, RotationStrategy strategy = RotationStrategy.RoundRobin)
    {
        lock (_rotationLock)
        {
            var platformPools = _pools.Values.Where(p => p.Platform == platform && p.IsEnabled).ToList();
            if (!platformPools.Any())
            {
                _logger.LogWarning($"No active pools found for {platform}");
                return null;
            }

            var allAccounts = platformPools
                .SelectMany(p => p.Accounts)
                .Where(a => a.IsActive && IsAccountAvailable(a.Id))
                .ToList();

            if (!allAccounts.Any())
            {
                _logger.LogWarning($"No available accounts for {platform}");
                return null;
            }

            return strategy switch
            {
                RotationStrategy.RoundRobin => GetRoundRobinAccount(allAccounts),
                RotationStrategy.LeastUsed => GetLeastUsedAccount(allAccounts),
                RotationStrategy.Priority => GetPriorityAccount(allAccounts),
                RotationStrategy.Random => GetRandomAccount(allAccounts),
                _ => GetRoundRobinAccount(allAccounts)
            };
        }
    }

    private PoolAccount GetRoundRobinAccount(List<PoolAccount> accounts)
    {
        // Sort by last used time, pick the oldest
        var account = accounts
            .OrderBy(a => _accountStatuses.TryGetValue(a.Id, out var s) ? s.LastUsed : DateTime.MinValue)
            .First();
        return account;
    }

    private PoolAccount GetLeastUsedAccount(List<PoolAccount> accounts)
    {
        return accounts
            .OrderBy(a => _accountStatuses.TryGetValue(a.Id, out var s) ? s.TotalPostCount : 0)
            .First();
    }

    private PoolAccount GetPriorityAccount(List<PoolAccount> accounts)
    {
        return accounts
            .OrderByDescending(a => a.Priority)
            .ThenBy(a => _accountStatuses.TryGetValue(a.Id, out var s) ? s.LastUsed : DateTime.MinValue)
            .First();
    }

    private PoolAccount GetRandomAccount(List<PoolAccount> accounts)
    {
        var random = new Random();
        return accounts[random.Next(accounts.Count)];
    }

    /// <summary>
    /// ตรวจสอบว่า account พร้อมใช้งานหรือไม่
    /// </summary>
    public bool IsAccountAvailable(string accountId)
    {
        if (!_accountStatuses.TryGetValue(accountId, out var status))
        {
            return false;
        }

        // Check if daily limit reset needed
        if (DateTime.UtcNow >= status.DailyLimitResetAt)
        {
            status.DailyPostCount = 0;
            status.DailyLimitResetAt = DateTime.UtcNow.Date.AddDays(1);
        }

        // Check account state
        if (status.State == AccountState.Banned || status.State == AccountState.Disabled)
        {
            return false;
        }

        // Check cooldown
        if (status.State == AccountState.Cooldown && status.CooldownUntil > DateTime.UtcNow)
        {
            return false;
        }

        // Check rate limit
        if (status.State == AccountState.RateLimited && status.RateLimitResetAt > DateTime.UtcNow)
        {
            return false;
        }

        // Find account and check daily limit
        foreach (var pool in _pools.Values)
        {
            var account = pool.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null && account.DailyPostLimit > 0)
            {
                if (status.DailyPostCount >= account.DailyPostLimit)
                {
                    return false;
                }
            }
        }

        return true;
    }

    #endregion

    #region Usage Tracking

    /// <summary>
    /// บันทึกการใช้งาน account
    /// </summary>
    public void RecordUsage(string accountId, bool success, string? errorMessage = null)
    {
        if (!_accountStatuses.TryGetValue(accountId, out var status))
        {
            return;
        }

        status.LastUsed = DateTime.UtcNow;
        status.TotalPostCount++;

        if (success)
        {
            status.DailyPostCount++;
            status.SuccessCount++;
            status.ConsecutiveFailures = 0;
            status.State = AccountState.Active;
        }
        else
        {
            status.FailureCount++;
            status.ConsecutiveFailures++;
            status.LastError = errorMessage;

            // Auto-cooldown after consecutive failures
            if (status.ConsecutiveFailures >= 3)
            {
                var cooldownMinutes = Math.Min(60 * Math.Pow(2, status.ConsecutiveFailures - 3), 1440); // Max 24 hours
                SetCooldown(accountId, TimeSpan.FromMinutes(cooldownMinutes), "Consecutive failures");
            }
        }

        _logger.LogInfo($"Recorded usage for account {accountId}: success={success}");
    }

    /// <summary>
    /// ตั้ง cooldown สำหรับ account
    /// </summary>
    public void SetCooldown(string accountId, TimeSpan duration, string reason)
    {
        if (_accountStatuses.TryGetValue(accountId, out var status))
        {
            status.State = AccountState.Cooldown;
            status.CooldownUntil = DateTime.UtcNow.Add(duration);
            status.CooldownReason = reason;
            _logger.LogWarning($"Account {accountId} set to cooldown for {duration.TotalMinutes} minutes: {reason}");
        }
    }

    /// <summary>
    /// ตั้ง rate limit สำหรับ account
    /// </summary>
    public void SetRateLimit(string accountId, DateTime resetAt, string? errorMessage = null)
    {
        if (_accountStatuses.TryGetValue(accountId, out var status))
        {
            status.State = AccountState.RateLimited;
            status.RateLimitResetAt = resetAt;
            status.LastError = errorMessage;
            _logger.LogWarning($"Account {accountId} rate limited until {resetAt}");
        }
    }

    /// <summary>
    /// ทำเครื่องหมาย account ว่าถูก ban
    /// </summary>
    public void MarkAsBanned(string accountId, string reason)
    {
        if (_accountStatuses.TryGetValue(accountId, out var status))
        {
            status.State = AccountState.Banned;
            status.BannedReason = reason;
            status.BannedAt = DateTime.UtcNow;
            _logger.LogError($"Account {accountId} marked as banned: {reason}");
        }

        // Disable the account
        foreach (var pool in _pools.Values)
        {
            var account = pool.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.IsActive = false;
            }
        }
    }

    /// <summary>
    /// ปลด ban account
    /// </summary>
    public void Unban(string accountId)
    {
        if (_accountStatuses.TryGetValue(accountId, out var status))
        {
            status.State = AccountState.Active;
            status.BannedReason = null;
            status.BannedAt = null;
            status.ConsecutiveFailures = 0;
        }

        foreach (var pool in _pools.Values)
        {
            var account = pool.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.IsActive = true;
            }
        }
    }

    #endregion

    #region Health Monitoring

    /// <summary>
    /// ดึงสถานะของ account
    /// </summary>
    public AccountStatus? GetAccountStatus(string accountId)
    {
        _accountStatuses.TryGetValue(accountId, out var status);
        return status;
    }

    /// <summary>
    /// ดึงสถานะของทุก account ใน platform
    /// </summary>
    public List<AccountHealthReport> GetHealthReport(SocialPlatform? platform = null)
    {
        var reports = new List<AccountHealthReport>();

        var pools = platform.HasValue
            ? _pools.Values.Where(p => p.Platform == platform.Value)
            : _pools.Values;

        foreach (var pool in pools)
        {
            foreach (var account in pool.Accounts)
            {
                _accountStatuses.TryGetValue(account.Id, out var status);

                var report = new AccountHealthReport
                {
                    AccountId = account.Id,
                    AccountName = account.Name,
                    PoolName = pool.Name,
                    Platform = pool.Platform,
                    State = status?.State ?? AccountState.Unknown,
                    IsAvailable = IsAccountAvailable(account.Id),
                    SuccessRate = status != null && status.TotalPostCount > 0
                        ? (double)status.SuccessCount / status.TotalPostCount * 100
                        : 0,
                    DailyUsage = status?.DailyPostCount ?? 0,
                    DailyLimit = account.DailyPostLimit,
                    LastUsed = status?.LastUsed,
                    LastError = status?.LastError,
                    CooldownUntil = status?.CooldownUntil,
                    RateLimitResetAt = status?.RateLimitResetAt
                };

                reports.Add(report);
            }
        }

        return reports;
    }

    /// <summary>
    /// ตรวจสอบ health ของทุก accounts และส่ง alerts
    /// </summary>
    public List<AccountAlert> CheckHealth()
    {
        var alerts = new List<AccountAlert>();

        foreach (var pool in _pools.Values)
        {
            foreach (var account in pool.Accounts)
            {
                if (!_accountStatuses.TryGetValue(account.Id, out var status))
                {
                    continue;
                }

                // Alert: High failure rate
                if (status.TotalPostCount >= 10 && status.SuccessCount < status.TotalPostCount * 0.5)
                {
                    alerts.Add(new AccountAlert
                    {
                        AccountId = account.Id,
                        AccountName = account.Name,
                        AlertType = AlertType.HighFailureRate,
                        Message = $"Success rate is below 50% ({status.SuccessCount}/{status.TotalPostCount})",
                        Severity = AlertSeverity.Warning
                    });
                }

                // Alert: Banned
                if (status.State == AccountState.Banned)
                {
                    alerts.Add(new AccountAlert
                    {
                        AccountId = account.Id,
                        AccountName = account.Name,
                        AlertType = AlertType.Banned,
                        Message = $"Account is banned: {status.BannedReason}",
                        Severity = AlertSeverity.Critical
                    });
                }

                // Alert: Approaching daily limit
                if (account.DailyPostLimit > 0 && status.DailyPostCount >= account.DailyPostLimit * 0.9)
                {
                    alerts.Add(new AccountAlert
                    {
                        AccountId = account.Id,
                        AccountName = account.Name,
                        AlertType = AlertType.DailyLimitApproaching,
                        Message = $"Daily limit almost reached ({status.DailyPostCount}/{account.DailyPostLimit})",
                        Severity = AlertSeverity.Info
                    });
                }

                // Alert: Long cooldown
                if (status.State == AccountState.Cooldown && status.CooldownUntil > DateTime.UtcNow.AddHours(1))
                {
                    alerts.Add(new AccountAlert
                    {
                        AccountId = account.Id,
                        AccountName = account.Name,
                        AlertType = AlertType.LongCooldown,
                        Message = $"Account in cooldown until {status.CooldownUntil}: {status.CooldownReason}",
                        Severity = AlertSeverity.Warning
                    });
                }
            }

            // Alert: Pool running low on available accounts
            var availableCount = pool.Accounts.Count(a => a.IsActive && IsAccountAvailable(a.Id));
            if (availableCount <= 1 && pool.Accounts.Count > 1)
            {
                alerts.Add(new AccountAlert
                {
                    AccountId = pool.Id,
                    AccountName = pool.Name,
                    AlertType = AlertType.LowAvailableAccounts,
                    Message = $"Only {availableCount} account(s) available in pool",
                    Severity = AlertSeverity.Warning
                });
            }
        }

        return alerts;
    }

    #endregion

    #region Statistics

    /// <summary>
    /// ดึงสถิติของ platform
    /// </summary>
    public PlatformStatistics GetStatistics(SocialPlatform platform)
    {
        var pools = _pools.Values.Where(p => p.Platform == platform).ToList();
        var accounts = pools.SelectMany(p => p.Accounts).ToList();

        var stats = new PlatformStatistics
        {
            Platform = platform,
            TotalPools = pools.Count,
            TotalAccounts = accounts.Count,
            ActiveAccounts = accounts.Count(a => a.IsActive && IsAccountAvailable(a.Id)),
            BannedAccounts = accounts.Count(a => _accountStatuses.TryGetValue(a.Id, out var s) && s.State == AccountState.Banned),
            CooldownAccounts = accounts.Count(a => _accountStatuses.TryGetValue(a.Id, out var s) && s.State == AccountState.Cooldown),
            RateLimitedAccounts = accounts.Count(a => _accountStatuses.TryGetValue(a.Id, out var s) && s.State == AccountState.RateLimited)
        };

        // Calculate total posts and success rate
        foreach (var account in accounts)
        {
            if (_accountStatuses.TryGetValue(account.Id, out var status))
            {
                stats.TotalPostsToday += status.DailyPostCount;
                stats.TotalPostsAllTime += status.TotalPostCount;
                stats.TotalSuccesses += status.SuccessCount;
                stats.TotalFailures += status.FailureCount;
            }
        }

        stats.OverallSuccessRate = stats.TotalPostsAllTime > 0
            ? (double)stats.TotalSuccesses / stats.TotalPostsAllTime * 100
            : 0;

        return stats;
    }

    /// <summary>
    /// Reset daily counters (เรียกจาก scheduler ทุกวัน)
    /// </summary>
    public void ResetDailyCounters()
    {
        foreach (var status in _accountStatuses.Values)
        {
            status.DailyPostCount = 0;
            status.DailyLimitResetAt = DateTime.UtcNow.Date.AddDays(1);
        }
        _logger.LogInfo("Daily account counters reset");
    }

    #endregion
}

#region Models

public class AccountPool
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public List<PoolAccount> Accounts { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }
}

public class PoolAccount
{
    public string Id { get; set; } = "";
    public string PoolId { get; set; } = "";
    public string Name { get; set; } = "";
    public PlatformCredentials Credentials { get; set; } = new();
    public int Priority { get; set; } // Higher = more preferred
    public int DailyPostLimit { get; set; } = 50; // 0 = unlimited
    public int MinPostInterval { get; set; } = 300; // Seconds between posts
    public bool IsActive { get; set; } = true;
    public DateTime AddedAt { get; set; }
    public DateTime? LastCredentialUpdate { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class AccountStatus
{
    public string AccountId { get; set; } = "";
    public AccountState State { get; set; } = AccountState.Unknown;
    public DateTime? LastUsed { get; set; }
    public int DailyPostCount { get; set; }
    public DateTime DailyLimitResetAt { get; set; }
    public int TotalPostCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int ConsecutiveFailures { get; set; }
    public string? LastError { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public string? CooldownReason { get; set; }
    public DateTime? RateLimitResetAt { get; set; }
    public DateTime? BannedAt { get; set; }
    public string? BannedReason { get; set; }
}

public enum AccountState
{
    Unknown,
    Active,
    Cooldown,
    RateLimited,
    Disabled,
    Banned
}

public enum RotationStrategy
{
    RoundRobin,   // วนรอบตามลำดับการใช้ล่าสุด
    LeastUsed,    // ใช้ account ที่ใช้น้อยที่สุด
    Priority,     // ใช้ตาม priority (high first)
    Random        // สุ่ม
}

public class AccountHealthReport
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string PoolName { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public AccountState State { get; set; }
    public bool IsAvailable { get; set; }
    public double SuccessRate { get; set; }
    public int DailyUsage { get; set; }
    public int DailyLimit { get; set; }
    public DateTime? LastUsed { get; set; }
    public string? LastError { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public DateTime? RateLimitResetAt { get; set; }
}

public class AccountAlert
{
    public string AccountId { get; set; } = "";
    public string AccountName { get; set; } = "";
    public AlertType AlertType { get; set; }
    public string Message { get; set; } = "";
    public AlertSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum AlertType
{
    HighFailureRate,
    Banned,
    DailyLimitApproaching,
    LongCooldown,
    LowAvailableAccounts,
    CredentialsExpiring,
    RateLimited
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

public class PlatformStatistics
{
    public SocialPlatform Platform { get; set; }
    public int TotalPools { get; set; }
    public int TotalAccounts { get; set; }
    public int ActiveAccounts { get; set; }
    public int BannedAccounts { get; set; }
    public int CooldownAccounts { get; set; }
    public int RateLimitedAccounts { get; set; }
    public int TotalPostsToday { get; set; }
    public int TotalPostsAllTime { get; set; }
    public int TotalSuccesses { get; set; }
    public int TotalFailures { get; set; }
    public double OverallSuccessRate { get; set; }
}

#endregion
