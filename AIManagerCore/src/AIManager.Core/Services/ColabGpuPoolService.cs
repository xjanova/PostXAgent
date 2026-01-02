using System.Text.Json;
using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// บริการจัดการ Google Colab GPU Pool
/// รองรับ account หลายตัวหมุนเวียนกัน เพื่อใช้งาน GPU ต่อเนื่อง
/// </summary>
public class ColabGpuPoolService : IDisposable
{
    private readonly ILogger<ColabGpuPoolService>? _logger;
    private readonly List<ColabGpuAccount> _accounts = new();
    private readonly object _lock = new();
    private readonly System.Timers.Timer _quotaCheckTimer;
    private readonly System.Timers.Timer _sessionMonitorTimer;
    private readonly string _configPath;

    private ColabPoolSettings _settings = new();
    private ColabSession? _currentSession;
    private int _roundRobinIndex = 0;

    // Events
    public event Action<ColabPoolEvent>? OnPoolEvent;
    public event Action<ColabGpuAccount>? OnAccountRotated;
    public event Action<ColabSession>? OnSessionStarted;
    public event Action<ColabSession>? OnSessionEnded;

    public ColabGpuPoolService(ILogger<ColabGpuPoolService>? logger = null)
    {
        _logger = logger;

        // Config path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent");
        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, "colab_pool.json");

        // Timer ตรวจสอบ quota ทุก 5 นาที
        _quotaCheckTimer = new System.Timers.Timer(5 * 60 * 1000);
        _quotaCheckTimer.Elapsed += (s, e) => CheckQuotaAndRotateIfNeeded();
        _quotaCheckTimer.AutoReset = true;

        // Timer ตรวจสอบ session ทุก 1 นาที
        _sessionMonitorTimer = new System.Timers.Timer(60 * 1000);
        _sessionMonitorTimer.Elapsed += (s, e) => MonitorCurrentSession();
        _sessionMonitorTimer.AutoReset = true;

        // Load saved config
        LoadConfig();
    }

    #region Account Management

    /// <summary>
    /// เพิ่ม account ใหม่
    /// </summary>
    public async Task<ColabGpuAccount> AddAccountAsync(ColabGpuAccount account)
    {
        lock (_lock)
        {
            // Check duplicate
            if (_accounts.Any(a => a.Email.Equals(account.Email, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Account {account.Email} already exists in pool");
            }

            account.Id = Guid.NewGuid().ToString();
            account.CreatedAt = DateTime.UtcNow;
            _accounts.Add(account);
        }

        await SaveConfigAsync();

        RaiseEvent(ColabPoolEventType.AccountAdded, account, $"เพิ่ม account {account.Email} เข้า pool แล้ว");
        _logger?.LogInformation("Added Colab account: {Email}", account.Email);

        return account;
    }

    /// <summary>
    /// ลบ account
    /// </summary>
    public async Task RemoveAccountAsync(string accountId)
    {
        ColabGpuAccount? removed = null;

        lock (_lock)
        {
            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                // ถ้ากำลังใช้งานอยู่ ต้อง rotate ก่อน
                if (account.Status == ColabAccountStatus.InUse && _currentSession?.AccountId == accountId)
                {
                    throw new InvalidOperationException("Cannot remove account that is currently in use. End session first.");
                }

                _accounts.Remove(account);
                removed = account;
            }
        }

        if (removed != null)
        {
            await SaveConfigAsync();
            RaiseEvent(ColabPoolEventType.AccountRemoved, removed, $"ลบ account {removed.Email} ออกจาก pool แล้ว");
            _logger?.LogInformation("Removed Colab account: {Email}", removed.Email);
        }
    }

    /// <summary>
    /// อัพเดท account
    /// </summary>
    public async Task UpdateAccountAsync(ColabGpuAccount account)
    {
        lock (_lock)
        {
            var index = _accounts.FindIndex(a => a.Id == account.Id);
            if (index >= 0)
            {
                _accounts[index] = account;
            }
        }

        await SaveConfigAsync();
    }

    /// <summary>
    /// ดึง account ทั้งหมด
    /// </summary>
    public List<ColabGpuAccount> GetAllAccounts()
    {
        lock (_lock)
        {
            return _accounts.ToList();
        }
    }

    /// <summary>
    /// ดึง account ตาม ID
    /// </summary>
    public ColabGpuAccount? GetAccount(string accountId)
    {
        lock (_lock)
        {
            return _accounts.FirstOrDefault(a => a.Id == accountId);
        }
    }

    /// <summary>
    /// ดึง account ที่พร้อมใช้งาน
    /// </summary>
    public List<ColabGpuAccount> GetAvailableAccounts()
    {
        lock (_lock)
        {
            // Reset expired cooldowns
            foreach (var account in _accounts)
            {
                if (account.Status == ColabAccountStatus.Cooldown &&
                    account.CooldownUntil.HasValue &&
                    account.CooldownUntil.Value <= DateTime.UtcNow)
                {
                    account.Status = ColabAccountStatus.Active;
                    account.CooldownUntil = null;
                }

                // Reset daily quota if past reset time
                if (account.QuotaResetTime.HasValue && account.QuotaResetTime.Value <= DateTime.UtcNow)
                {
                    account.DailyQuotaUsedMinutes = 0;
                    account.QuotaResetTime = DateTime.UtcNow.AddDays(1).Date; // Reset at midnight
                    if (account.Status == ColabAccountStatus.QuotaExhausted)
                    {
                        account.Status = ColabAccountStatus.Active;
                    }
                }
            }

            return _accounts.Where(a => a.IsAvailable).ToList();
        }
    }

    #endregion

    #region Account Selection (Rotation)

    /// <summary>
    /// ดึง account ถัดไปตาม rotation strategy
    /// </summary>
    public ColabGpuAccount? GetNextAvailableAccount()
    {
        var available = GetAvailableAccounts();
        if (!available.Any())
            return null;

        return _settings.RotationStrategy switch
        {
            ColabRotationStrategy.RoundRobin => SelectRoundRobin(available),
            ColabRotationStrategy.LeastUsed => SelectLeastUsed(available),
            ColabRotationStrategy.Priority => SelectByPriority(available),
            ColabRotationStrategy.Random => SelectRandom(available),
            _ => SelectByPriority(available)
        };
    }

    private ColabGpuAccount SelectRoundRobin(List<ColabGpuAccount> accounts)
    {
        var sorted = accounts.OrderBy(a => a.Priority).ThenBy(a => a.Email).ToList();
        _roundRobinIndex = _roundRobinIndex % sorted.Count;
        var selected = sorted[_roundRobinIndex];
        _roundRobinIndex = (_roundRobinIndex + 1) % sorted.Count;
        return selected;
    }

    private ColabGpuAccount SelectLeastUsed(List<ColabGpuAccount> accounts)
    {
        return accounts
            .OrderBy(a => a.DailyQuotaUsedMinutes)
            .ThenBy(a => a.TotalSessions)
            .ThenBy(a => a.Priority)
            .First();
    }

    private ColabGpuAccount SelectByPriority(List<ColabGpuAccount> accounts)
    {
        return accounts
            .OrderBy(a => a.Priority)
            .ThenByDescending(a => a.RemainingQuotaMinutes)
            .First();
    }

    private ColabGpuAccount SelectRandom(List<ColabGpuAccount> accounts)
    {
        var random = new Random();
        return accounts[random.Next(accounts.Count)];
    }

    /// <summary>
    /// หมุนไปใช้ account ถัดไป (auto-rotation)
    /// </summary>
    public async Task<ColabGpuAccount?> RotateToNextAccountAsync(string? reason = null)
    {
        ColabGpuAccount? currentAccount = null;
        ColabGpuAccount? nextAccount = null;

        lock (_lock)
        {
            // Mark current as cooldown
            if (_currentSession != null)
            {
                currentAccount = _accounts.FirstOrDefault(a => a.Id == _currentSession.AccountId);
                if (currentAccount != null)
                {
                    currentAccount.Status = ColabAccountStatus.Cooldown;
                    currentAccount.CooldownUntil = DateTime.UtcNow.AddMinutes(_settings.CooldownMinutes);
                }
            }

            // Get next available
            nextAccount = GetNextAvailableAccount();
        }

        if (nextAccount != null)
        {
            RaiseEvent(ColabPoolEventType.AccountRotated, nextAccount,
                $"หมุนเปลี่ยนจาก {currentAccount?.Email ?? "none"} ไป {nextAccount.Email}" +
                (reason != null ? $" เหตุผล: {reason}" : ""));

            OnAccountRotated?.Invoke(nextAccount);
            await SaveConfigAsync();
        }
        else
        {
            RaiseEvent(ColabPoolEventType.PoolEmpty, null!, "ไม่มี account ที่พร้อมใช้งานใน pool");
        }

        return nextAccount;
    }

    #endregion

    #region Session Management

    /// <summary>
    /// เริ่ม session ใหม่กับ account ที่เลือก
    /// </summary>
    public async Task<ColabSession?> StartSessionAsync(string? accountId = null)
    {
        ColabGpuAccount? account;

        lock (_lock)
        {
            if (!string.IsNullOrEmpty(accountId))
            {
                account = _accounts.FirstOrDefault(a => a.Id == accountId);
            }
            else
            {
                account = GetNextAvailableAccount();
            }

            if (account == null)
            {
                _logger?.LogWarning("No available Colab account for new session");
                return null;
            }

            // End current session if any
            if (_currentSession != null)
            {
                EndSessionInternal();
            }

            // Create new session
            _currentSession = new ColabSession
            {
                AccountId = account.Id,
                GpuType = _settings.PreferredGpuType,
                StartedAt = DateTime.UtcNow
            };

            // Update account status
            account.Status = ColabAccountStatus.InUse;
            account.CurrentSessionId = _currentSession.SessionId;
            account.SessionStartTime = _currentSession.StartedAt;
            account.TotalSessions++;
            account.LastUsedAt = DateTime.UtcNow;
        }

        await SaveConfigAsync();

        OnSessionStarted?.Invoke(_currentSession);
        RaiseEvent(ColabPoolEventType.SessionStarted, account!,
            $"เริ่ม session ใหม่ด้วย account {account!.Email}");

        _logger?.LogInformation("Started Colab session with account: {Email}", account.Email);

        // Start monitoring
        _quotaCheckTimer.Start();
        _sessionMonitorTimer.Start();

        return _currentSession;
    }

    /// <summary>
    /// จบ session ปัจจุบัน
    /// </summary>
    public async Task EndSessionAsync()
    {
        ColabGpuAccount? account = null;
        ColabSession? session = null;

        lock (_lock)
        {
            session = _currentSession;
            if (session != null)
            {
                account = _accounts.FirstOrDefault(a => a.Id == session.AccountId);
                EndSessionInternal();
            }
        }

        if (session != null)
        {
            await SaveConfigAsync();
            OnSessionEnded?.Invoke(session);
            RaiseEvent(ColabPoolEventType.SessionEnded, account!,
                $"จบ session ของ account {account?.Email}");
        }
    }

    private void EndSessionInternal()
    {
        if (_currentSession == null) return;

        var account = _accounts.FirstOrDefault(a => a.Id == _currentSession.AccountId);
        if (account != null)
        {
            // Calculate usage
            var duration = DateTime.UtcNow - _currentSession.StartedAt;
            account.DailyQuotaUsedMinutes += (int)duration.TotalMinutes;
            account.Status = ColabAccountStatus.Active;
            account.CurrentSessionId = null;
            account.SessionStartTime = null;
            account.CurrentGpuType = ColabGpuType.None;
        }

        _currentSession.EndedAt = DateTime.UtcNow;
        _currentSession.IsActive = false;
        _currentSession = null;

        _quotaCheckTimer.Stop();
        _sessionMonitorTimer.Stop();
    }

    /// <summary>
    /// ดึง session ปัจจุบัน
    /// </summary>
    public ColabSession? GetCurrentSession()
    {
        lock (_lock)
        {
            return _currentSession;
        }
    }

    #endregion

    #region Quota & Status Tracking

    /// <summary>
    /// อัพเดทการใช้งาน quota
    /// </summary>
    public async Task UpdateQuotaUsageAsync(string accountId, int minutesUsed)
    {
        lock (_lock)
        {
            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.DailyQuotaUsedMinutes += minutesUsed;
                account.LastUsedAt = DateTime.UtcNow;

                // Check if quota exhausted
                if (account.QuotaUsagePercent >= 100)
                {
                    account.Status = ColabAccountStatus.QuotaExhausted;
                    RaiseEvent(ColabPoolEventType.QuotaExhausted, account,
                        $"Account {account.Email} หมดโควต้าแล้ว");
                }
                else if (account.QuotaUsagePercent >= _settings.QuotaThresholdPercent)
                {
                    RaiseEvent(ColabPoolEventType.QuotaLow, account,
                        $"Account {account.Email} โควต้าเหลือน้อย ({100 - account.QuotaUsagePercent:F0}%)");
                }
            }
        }

        await SaveConfigAsync();
    }

    /// <summary>
    /// บันทึกความสำเร็จ
    /// </summary>
    public async Task RecordSuccessAsync(string accountId)
    {
        lock (_lock)
        {
            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.SuccessCount++;
                account.ConsecutiveFailures = 0;
                account.LastUsedAt = DateTime.UtcNow;
            }
        }

        await SaveConfigAsync();
    }

    /// <summary>
    /// บันทึกความล้มเหลว
    /// </summary>
    public async Task<ColabGpuAccount?> RecordFailureAsync(string accountId, string error)
    {
        ColabGpuAccount? nextAccount = null;

        lock (_lock)
        {
            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.FailureCount++;
                account.ConsecutiveFailures++;
                account.LastError = error;
                account.LastUsedAt = DateTime.UtcNow;

                // Auto-suspend if too many failures
                if (account.ConsecutiveFailures >= _settings.MaxConsecutiveFailures)
                {
                    account.Status = ColabAccountStatus.Suspended;
                    RaiseEvent(ColabPoolEventType.AccountError, account,
                        $"Account {account.Email} ถูกระงับเนื่องจากล้มเหลวติดต่อกัน {account.ConsecutiveFailures} ครั้ง");
                }

                // Auto-failover
                if (_settings.AutoFailover)
                {
                    nextAccount = GetNextAvailableAccount();
                }
            }
        }

        await SaveConfigAsync();
        return nextAccount;
    }

    /// <summary>
    /// ตรวจสอบ quota และหมุน account ถ้าจำเป็น
    /// </summary>
    private void CheckQuotaAndRotateIfNeeded()
    {
        try
        {
            lock (_lock)
            {
                if (_currentSession == null) return;

                var account = _accounts.FirstOrDefault(a => a.Id == _currentSession.AccountId);
                if (account == null) return;

                // Update session duration
                var duration = DateTime.UtcNow - _currentSession.StartedAt;
                account.DailyQuotaUsedMinutes = (int)duration.TotalMinutes;

                // Check if need to rotate
                if (_settings.AutoRotateOnQuotaLow &&
                    account.QuotaUsagePercent >= _settings.QuotaThresholdPercent)
                {
                    _logger?.LogWarning("Quota low for {Email} ({Usage}%), initiating rotation",
                        account.Email, account.QuotaUsagePercent);

                    // Schedule rotation on main thread
                    Task.Run(() => RotateToNextAccountAsync("Quota low"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking quota");
        }
    }

    /// <summary>
    /// ตรวจสอบสถานะ session
    /// </summary>
    private void MonitorCurrentSession()
    {
        try
        {
            lock (_lock)
            {
                if (_currentSession == null) return;

                var account = _accounts.FirstOrDefault(a => a.Id == _currentSession.AccountId);
                if (account == null) return;

                // Check session timeout
                var duration = DateTime.UtcNow - _currentSession.StartedAt;
                if (duration.TotalMinutes >= _settings.SessionTimeoutMinutes)
                {
                    _logger?.LogWarning("Session timeout for {Email}, rotating", account.Email);
                    Task.Run(() => RotateToNextAccountAsync("Session timeout"));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error monitoring session");
        }
    }

    #endregion

    #region Pool Status

    /// <summary>
    /// ดึงสถานะรวมของ pool
    /// </summary>
    public ColabPoolStatus GetPoolStatus()
    {
        lock (_lock)
        {
            var available = GetAvailableAccounts();

            return new ColabPoolStatus
            {
                TotalAccounts = _accounts.Count,
                ActiveAccounts = _accounts.Count(a => a.Status == ColabAccountStatus.Active),
                InUseAccounts = _accounts.Count(a => a.Status == ColabAccountStatus.InUse),
                CooldownAccounts = _accounts.Count(a => a.Status == ColabAccountStatus.Cooldown),
                ExhaustedAccounts = _accounts.Count(a => a.Status == ColabAccountStatus.QuotaExhausted),
                ErrorAccounts = _accounts.Count(a => a.Status == ColabAccountStatus.Error ||
                                                      a.Status == ColabAccountStatus.Suspended),
                TotalRemainingQuotaMinutes = _accounts.Sum(a => a.RemainingQuotaMinutes),
                CurrentSession = _currentSession,
                NextAvailableAccountEmail = available.FirstOrDefault()?.Email,
                LastUpdated = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// ดึงการตั้งค่า
    /// </summary>
    public ColabPoolSettings GetSettings() => _settings;

    /// <summary>
    /// บันทึกการตั้งค่า
    /// </summary>
    public async Task UpdateSettingsAsync(ColabPoolSettings settings)
    {
        _settings = settings;
        await SaveConfigAsync();
    }

    #endregion

    #region Recovery

    /// <summary>
    /// กู้คืน account ที่ถูกระงับ
    /// </summary>
    public async Task RecoverAccountAsync(string accountId)
    {
        lock (_lock)
        {
            var account = _accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                account.Status = ColabAccountStatus.Active;
                account.ConsecutiveFailures = 0;
                account.LastError = null;
                account.CooldownUntil = null;

                RaiseEvent(ColabPoolEventType.AccountRecovered, account,
                    $"กู้คืน account {account.Email} แล้ว");
            }
        }

        await SaveConfigAsync();
    }

    /// <summary>
    /// รีเซ็ต daily quota ทั้งหมด (สำหรับ manual reset)
    /// </summary>
    public async Task ResetAllDailyQuotasAsync()
    {
        lock (_lock)
        {
            foreach (var account in _accounts)
            {
                account.DailyQuotaUsedMinutes = 0;
                account.QuotaResetTime = DateTime.UtcNow.AddDays(1).Date;
                if (account.Status == ColabAccountStatus.QuotaExhausted)
                {
                    account.Status = ColabAccountStatus.Active;
                }
            }
        }

        await SaveConfigAsync();
        _logger?.LogInformation("Reset all daily quotas");
    }

    #endregion

    #region Persistence

    private void LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<ColabPoolConfig>(json);

                if (config != null)
                {
                    _accounts.Clear();
                    _accounts.AddRange(config.Accounts);
                    _settings = config.Settings;
                    _roundRobinIndex = config.RoundRobinIndex;
                }

                _logger?.LogInformation("Loaded {Count} Colab accounts from config", _accounts.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load Colab pool config");
        }
    }

    private async Task SaveConfigAsync()
    {
        try
        {
            var config = new ColabPoolConfig
            {
                Accounts = _accounts.ToList(),
                Settings = _settings,
                RoundRobinIndex = _roundRobinIndex
            };

            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configPath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save Colab pool config");
        }
    }

    private class ColabPoolConfig
    {
        public List<ColabGpuAccount> Accounts { get; set; } = new();
        public ColabPoolSettings Settings { get; set; } = new();
        public int RoundRobinIndex { get; set; }
    }

    #endregion

    #region Events

    private void RaiseEvent(ColabPoolEventType eventType, ColabGpuAccount account, string message)
    {
        var evt = new ColabPoolEvent
        {
            EventType = eventType,
            AccountId = account?.Id ?? string.Empty,
            AccountEmail = account?.Email,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        OnPoolEvent?.Invoke(evt);

        if (_settings.EnableNotifications)
        {
            _logger?.LogInformation("[ColabPool] {EventType}: {Message}", eventType, message);
        }
    }

    #endregion

    public void Dispose()
    {
        _quotaCheckTimer.Dispose();
        _sessionMonitorTimer.Dispose();
    }
}
