using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AIManager.Core.Services;

/// <summary>
/// Database status for UI display
/// </summary>
public enum DatabaseStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error,
    Syncing
}

/// <summary>
/// Connection statistics (thread-safe fields for Interlocked operations)
/// </summary>
public class ConnectionStats
{
    // Thread-safe fields for Interlocked
    internal long _totalRequests;
    internal long _successfulRequests;
    internal long _failedRequests;
    internal long _activeConnections;
    internal long _totalApiKeyUsage;

    public long TotalRequests => _totalRequests;
    public long SuccessfulRequests => _successfulRequests;
    public long FailedRequests => _failedRequests;
    public long ActiveConnections => _activeConnections;
    public long TotalApiKeyUsage => _totalApiKeyUsage;
    public DateTime LastRequestAt { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan Uptime => DateTime.UtcNow - StartedAt;
}

/// <summary>
/// API Key usage log entry
/// </summary>
public class ApiKeyUsageLog
{
    public long Id { get; set; }
    public string KeyId { get; set; } = "";
    public string KeyName { get; set; } = "";
    public string ClientIp { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Method { get; set; } = "";
    public int StatusCode { get; set; }
    public long ResponseTimeMs { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// IP usage alert
/// </summary>
public class IpUsageAlert
{
    public long Id { get; set; }
    public string KeyId { get; set; } = "";
    public string KeyName { get; set; } = "";
    public string AlertType { get; set; } = ""; // multiple_ips, suspicious_activity, rate_limit
    public string Message { get; set; } = "";
    public string IpAddresses { get; set; } = ""; // JSON array
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>
/// Core Database Service - Manages all core data with SQLite primary and MySQL failover
/// Auto-creates database, syncs data, and provides status monitoring
/// </summary>
public class CoreDatabaseService : IDisposable
{
    private readonly ILogger<CoreDatabaseService> _logger;
    private readonly string _sqlitePath;
    private SqliteConnection? _sqliteConnection;
    private MySqlConnection? _mysqlConnection;
    private bool _isInitialized;
    private readonly object _lock = new();

    // Connection stats (thread-safe)
    private readonly ConnectionStats _stats = new();

    // IP tracking for alerts
    private readonly ConcurrentDictionary<string, HashSet<string>> _keyIpTracker = new();
    private readonly int _maxIpsPerKey = 5; // Alert threshold

    // Events
    public event EventHandler<DatabaseStatus>? StatusChanged;
    public event EventHandler<IpUsageAlert>? AlertRaised;
    public event EventHandler<ConnectionStats>? StatsUpdated;

    // Status
    public DatabaseStatus SqliteStatus { get; private set; } = DatabaseStatus.Disconnected;
    public DatabaseStatus MysqlStatus { get; private set; } = DatabaseStatus.Disconnected;
    public bool IsSqliteConnected => _sqliteConnection?.State == ConnectionState.Open;
    public bool IsMysqlConnected => _mysqlConnection?.State == ConnectionState.Open;
    public ConnectionStats Stats => _stats;

    // MySQL Configuration
    public string? MysqlHost { get; set; }
    public int MysqlPort { get; set; } = 3306;
    public string? MysqlDatabase { get; set; }
    public string? MysqlUsername { get; set; }
    public string? MysqlPassword { get; set; }
    public bool MysqlEnabled => !string.IsNullOrEmpty(MysqlHost) && !string.IsNullOrEmpty(MysqlDatabase);

    public CoreDatabaseService(ILogger<CoreDatabaseService> logger)
    {
        _logger = logger;

        // Default SQLite path
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIManager"
        );
        Directory.CreateDirectory(appDataPath);
        _sqlitePath = Path.Combine(appDataPath, "aimanager_core.db");
    }

    /// <summary>
    /// Initialize database - creates SQLite automatically, connects to MySQL if configured
    /// </summary>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            SqliteStatus = DatabaseStatus.Connecting;
            StatusChanged?.Invoke(this, SqliteStatus);

            // Always create/connect SQLite first (local backup)
            _sqliteConnection = new SqliteConnection($"Data Source={_sqlitePath}");
            await _sqliteConnection.OpenAsync();

            await CreateTablesAsync(_sqliteConnection, "sqlite");

            SqliteStatus = DatabaseStatus.Connected;
            StatusChanged?.Invoke(this, SqliteStatus);
            _logger.LogInformation("SQLite database initialized at {Path}", _sqlitePath);

            // Try MySQL if configured
            if (MysqlEnabled)
            {
                await ConnectMysqlAsync();
            }

            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            SqliteStatus = DatabaseStatus.Error;
            StatusChanged?.Invoke(this, SqliteStatus);
            _logger.LogError(ex, "Failed to initialize core database");
            return false;
        }
    }

    /// <summary>
    /// Connect to MySQL database
    /// </summary>
    public async Task<bool> ConnectMysqlAsync()
    {
        if (!MysqlEnabled) return false;

        try
        {
            MysqlStatus = DatabaseStatus.Connecting;
            StatusChanged?.Invoke(this, MysqlStatus);

            var connectionString = new MySqlConnectionStringBuilder
            {
                Server = MysqlHost,
                Port = (uint)MysqlPort,
                Database = MysqlDatabase,
                UserID = MysqlUsername,
                Password = MysqlPassword,
                CharacterSet = "utf8mb4",
                AllowUserVariables = true
            }.ConnectionString;

            _mysqlConnection = new MySqlConnection(connectionString);
            await _mysqlConnection.OpenAsync();

            await CreateTablesAsync(_mysqlConnection, "mysql");

            MysqlStatus = DatabaseStatus.Connected;
            StatusChanged?.Invoke(this, MysqlStatus);
            _logger.LogInformation("MySQL database connected at {Host}:{Port}", MysqlHost, MysqlPort);

            // Sync data from SQLite to MySQL if needed
            _ = Task.Run(() => SyncToMysqlAsync());

            return true;
        }
        catch (Exception ex)
        {
            MysqlStatus = DatabaseStatus.Error;
            StatusChanged?.Invoke(this, MysqlStatus);
            _logger.LogWarning(ex, "Failed to connect to MySQL, using SQLite only");
            return false;
        }
    }

    /// <summary>
    /// Sync data from SQLite to MySQL
    /// </summary>
    public async Task SyncToMysqlAsync()
    {
        if (!IsMysqlConnected || !IsSqliteConnected) return;

        try
        {
            MysqlStatus = DatabaseStatus.Syncing;
            StatusChanged?.Invoke(this, MysqlStatus);

            // Get unsynced records from SQLite
            var unsyncedLogs = await _sqliteConnection!.QueryAsync<ApiKeyUsageLog>(
                "SELECT * FROM api_key_usage_logs WHERE synced = 0 ORDER BY created_at LIMIT 1000");

            foreach (var log in unsyncedLogs)
            {
                try
                {
                    await InsertUsageLogAsync(_mysqlConnection!, log);

                    // Mark as synced in SQLite
                    await _sqliteConnection.ExecuteAsync(
                        "UPDATE api_key_usage_logs SET synced = 1 WHERE id = @Id",
                        new { log.Id });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync log {Id} to MySQL", log.Id);
                }
            }

            MysqlStatus = DatabaseStatus.Connected;
            StatusChanged?.Invoke(this, MysqlStatus);
            _logger.LogDebug("Synced {Count} records to MySQL", unsyncedLogs.Count());
        }
        catch (Exception ex)
        {
            MysqlStatus = DatabaseStatus.Error;
            StatusChanged?.Invoke(this, MysqlStatus);
            _logger.LogError(ex, "Failed to sync to MySQL");
        }
    }

    #region API Key Usage Logging

    /// <summary>
    /// Log API key usage
    /// </summary>
    public async Task LogApiKeyUsageAsync(string keyId, string keyName, string clientIp,
        string endpoint, string method, int statusCode, long responseTimeMs, string? userAgent = null)
    {
        EnsureInitialized();

        var log = new ApiKeyUsageLog
        {
            KeyId = keyId,
            KeyName = keyName,
            ClientIp = clientIp,
            Endpoint = endpoint,
            Method = method,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };

        // Update stats
        Interlocked.Increment(ref _stats._totalRequests);
        Interlocked.Increment(ref _stats._totalApiKeyUsage);
        _stats.LastRequestAt = DateTime.UtcNow;

        if (statusCode >= 200 && statusCode < 400)
            Interlocked.Increment(ref _stats._successfulRequests);
        else
            Interlocked.Increment(ref _stats._failedRequests);

        StatsUpdated?.Invoke(this, _stats);

        // Track IP usage per key
        TrackIpUsage(keyId, keyName, clientIp);

        // Save to SQLite (always)
        try
        {
            await InsertUsageLogAsync(_sqliteConnection!, log, includeSynced: true);

            // Try MySQL if connected
            if (IsMysqlConnected)
            {
                try
                {
                    await InsertUsageLogAsync(_mysqlConnection!, log);
                    // Mark as synced in SQLite
                    await _sqliteConnection!.ExecuteAsync(
                        "UPDATE api_key_usage_logs SET synced = 1 WHERE id = last_insert_rowid()");
                }
                catch
                {
                    // MySQL failed, will sync later
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log API key usage");
        }
    }

    /// <summary>
    /// Track IP usage per key and raise alerts if needed
    /// </summary>
    private void TrackIpUsage(string keyId, string keyName, string clientIp)
    {
        var ips = _keyIpTracker.GetOrAdd(keyId, _ => new HashSet<string>());

        lock (ips)
        {
            ips.Add(clientIp);

            if (ips.Count > _maxIpsPerKey)
            {
                // Raise alert for multiple IPs
                var alert = new IpUsageAlert
                {
                    KeyId = keyId,
                    KeyName = keyName,
                    AlertType = "multiple_ips",
                    Message = $"API Key '{keyName}' is being used from {ips.Count} different IP addresses",
                    IpAddresses = JsonSerializer.Serialize(ips.ToList()),
                    CreatedAt = DateTime.UtcNow
                };

                _ = SaveAlertAsync(alert);
                AlertRaised?.Invoke(this, alert);
                _logger.LogWarning("Alert: API Key {KeyName} used from {IpCount} IPs: {Ips}",
                    keyName, ips.Count, string.Join(", ", ips));
            }
        }
    }

    /// <summary>
    /// Get API key usage logs
    /// </summary>
    public async Task<IEnumerable<ApiKeyUsageLog>> GetUsageLogsAsync(
        string? keyId = null, string? clientIp = null,
        DateTime? startDate = null, DateTime? endDate = null,
        int limit = 100)
    {
        EnsureInitialized();

        var sql = "SELECT * FROM api_key_usage_logs WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(keyId))
        {
            sql += " AND key_id = @KeyId";
            parameters.Add("KeyId", keyId);
        }

        if (!string.IsNullOrEmpty(clientIp))
        {
            sql += " AND client_ip = @ClientIp";
            parameters.Add("ClientIp", clientIp);
        }

        if (startDate.HasValue)
        {
            sql += " AND created_at >= @StartDate";
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            sql += " AND created_at <= @EndDate";
            parameters.Add("EndDate", endDate.Value);
        }

        sql += $" ORDER BY created_at DESC LIMIT {limit}";

        return await _sqliteConnection!.QueryAsync<ApiKeyUsageLog>(sql, parameters);
    }

    /// <summary>
    /// Get unique IPs for a key
    /// </summary>
    public async Task<IEnumerable<string>> GetKeyIpAddressesAsync(string keyId)
    {
        EnsureInitialized();

        var sql = "SELECT DISTINCT client_ip FROM api_key_usage_logs WHERE key_id = @KeyId ORDER BY client_ip";
        return await _sqliteConnection!.QueryAsync<string>(sql, new { KeyId = keyId });
    }

    /// <summary>
    /// Get usage stats by key
    /// </summary>
    public async Task<IEnumerable<dynamic>> GetUsageStatsByKeyAsync()
    {
        EnsureInitialized();

        var sql = @"
            SELECT
                key_id,
                key_name,
                COUNT(*) as total_requests,
                COUNT(DISTINCT client_ip) as unique_ips,
                AVG(response_time_ms) as avg_response_time,
                MAX(created_at) as last_used
            FROM api_key_usage_logs
            GROUP BY key_id, key_name
            ORDER BY total_requests DESC";

        return await _sqliteConnection!.QueryAsync(sql);
    }

    #endregion

    #region Alerts

    /// <summary>
    /// Save alert to database
    /// </summary>
    private async Task SaveAlertAsync(IpUsageAlert alert)
    {
        var sql = @"
            INSERT INTO ip_usage_alerts (key_id, key_name, alert_type, message, ip_addresses, is_resolved, created_at)
            VALUES (@KeyId, @KeyName, @AlertType, @Message, @IpAddresses, @IsResolved, @CreatedAt)";

        await _sqliteConnection!.ExecuteAsync(sql, alert);

        if (IsMysqlConnected)
        {
            try
            {
                await _mysqlConnection!.ExecuteAsync(sql, alert);
            }
            catch { }
        }
    }

    /// <summary>
    /// Get active alerts
    /// </summary>
    public async Task<IEnumerable<IpUsageAlert>> GetActiveAlertsAsync()
    {
        EnsureInitialized();

        var sql = "SELECT * FROM ip_usage_alerts WHERE is_resolved = 0 ORDER BY created_at DESC";
        return await _sqliteConnection!.QueryAsync<IpUsageAlert>(sql);
    }

    /// <summary>
    /// Resolve an alert
    /// </summary>
    public async Task ResolveAlertAsync(long alertId)
    {
        EnsureInitialized();

        var sql = "UPDATE ip_usage_alerts SET is_resolved = 1, resolved_at = @ResolvedAt WHERE id = @Id";
        var parameters = new { Id = alertId, ResolvedAt = DateTime.UtcNow };

        await _sqliteConnection!.ExecuteAsync(sql, parameters);

        if (IsMysqlConnected)
        {
            try
            {
                await _mysqlConnection!.ExecuteAsync(sql, parameters);
            }
            catch { }
        }
    }

    #endregion

    #region System Stats

    /// <summary>
    /// Record connection (for tracking)
    /// </summary>
    public void RecordConnection()
    {
        Interlocked.Increment(ref _stats._activeConnections);
        StatsUpdated?.Invoke(this, _stats);
    }

    /// <summary>
    /// Record disconnection
    /// </summary>
    public void RecordDisconnection()
    {
        Interlocked.Decrement(ref _stats._activeConnections);
        StatsUpdated?.Invoke(this, _stats);
    }

    /// <summary>
    /// Get database statistics
    /// </summary>
    public async Task<Dictionary<string, object>> GetDatabaseStatsAsync()
    {
        EnsureInitialized();

        var stats = new Dictionary<string, object>
        {
            ["sqlite_status"] = SqliteStatus.ToString(),
            ["mysql_status"] = MysqlStatus.ToString(),
            ["sqlite_path"] = _sqlitePath,
            ["mysql_host"] = MysqlHost ?? "Not configured"
        };

        // Get table counts
        var tables = new[] { "api_key_usage_logs", "ip_usage_alerts" };
        foreach (var table in tables)
        {
            try
            {
                var count = await _sqliteConnection!.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}");
                stats[$"{table}_count"] = count;
            }
            catch
            {
                stats[$"{table}_count"] = 0;
            }
        }

        // Get unsynced count
        try
        {
            var unsynced = await _sqliteConnection!.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM api_key_usage_logs WHERE synced = 0");
            stats["unsynced_logs"] = unsynced;
        }
        catch
        {
            stats["unsynced_logs"] = 0;
        }

        return stats;
    }

    #endregion

    #region Private Methods

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Database not initialized. Call InitializeAsync() first.");
    }

    private async Task CreateTablesAsync(DbConnection connection, string dbType)
    {
        // API Key Usage Logs table
        var createLogsTable = dbType == "mysql"
            ? @"CREATE TABLE IF NOT EXISTS api_key_usage_logs (
                id BIGINT AUTO_INCREMENT PRIMARY KEY,
                key_id VARCHAR(100) NOT NULL,
                key_name VARCHAR(255) NOT NULL,
                client_ip VARCHAR(50) NOT NULL,
                endpoint VARCHAR(500) NOT NULL,
                method VARCHAR(10) NOT NULL,
                status_code INT NOT NULL,
                response_time_ms BIGINT NOT NULL,
                user_agent VARCHAR(500),
                created_at DATETIME NOT NULL,
                INDEX idx_key_id (key_id),
                INDEX idx_client_ip (client_ip),
                INDEX idx_created_at (created_at)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
            : @"CREATE TABLE IF NOT EXISTS api_key_usage_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                key_id TEXT NOT NULL,
                key_name TEXT NOT NULL,
                client_ip TEXT NOT NULL,
                endpoint TEXT NOT NULL,
                method TEXT NOT NULL,
                status_code INTEGER NOT NULL,
                response_time_ms INTEGER NOT NULL,
                user_agent TEXT,
                synced INTEGER DEFAULT 0,
                created_at TEXT NOT NULL
            )";

        await connection.ExecuteAsync(createLogsTable);

        // IP Usage Alerts table
        var createAlertsTable = dbType == "mysql"
            ? @"CREATE TABLE IF NOT EXISTS ip_usage_alerts (
                id BIGINT AUTO_INCREMENT PRIMARY KEY,
                key_id VARCHAR(100) NOT NULL,
                key_name VARCHAR(255) NOT NULL,
                alert_type VARCHAR(50) NOT NULL,
                message TEXT NOT NULL,
                ip_addresses TEXT NOT NULL,
                is_resolved TINYINT DEFAULT 0,
                created_at DATETIME NOT NULL,
                resolved_at DATETIME,
                INDEX idx_key_id (key_id),
                INDEX idx_is_resolved (is_resolved)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4"
            : @"CREATE TABLE IF NOT EXISTS ip_usage_alerts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                key_id TEXT NOT NULL,
                key_name TEXT NOT NULL,
                alert_type TEXT NOT NULL,
                message TEXT NOT NULL,
                ip_addresses TEXT NOT NULL,
                is_resolved INTEGER DEFAULT 0,
                created_at TEXT NOT NULL,
                resolved_at TEXT
            )";

        await connection.ExecuteAsync(createAlertsTable);

        // Create indexes for SQLite
        if (dbType == "sqlite")
        {
            var indexes = new[]
            {
                "CREATE INDEX IF NOT EXISTS idx_logs_key_id ON api_key_usage_logs(key_id)",
                "CREATE INDEX IF NOT EXISTS idx_logs_client_ip ON api_key_usage_logs(client_ip)",
                "CREATE INDEX IF NOT EXISTS idx_logs_created_at ON api_key_usage_logs(created_at)",
                "CREATE INDEX IF NOT EXISTS idx_logs_synced ON api_key_usage_logs(synced)",
                "CREATE INDEX IF NOT EXISTS idx_alerts_key_id ON ip_usage_alerts(key_id)",
                "CREATE INDEX IF NOT EXISTS idx_alerts_resolved ON ip_usage_alerts(is_resolved)"
            };

            foreach (var indexSql in indexes)
            {
                try
                {
                    await connection.ExecuteAsync(indexSql);
                }
                catch { }
            }
        }
    }

    private async Task InsertUsageLogAsync(DbConnection connection, ApiKeyUsageLog log, bool includeSynced = false)
    {
        var sql = includeSynced
            ? @"INSERT INTO api_key_usage_logs
                (key_id, key_name, client_ip, endpoint, method, status_code, response_time_ms, user_agent, synced, created_at)
                VALUES (@KeyId, @KeyName, @ClientIp, @Endpoint, @Method, @StatusCode, @ResponseTimeMs, @UserAgent, 0, @CreatedAt)"
            : @"INSERT INTO api_key_usage_logs
                (key_id, key_name, client_ip, endpoint, method, status_code, response_time_ms, user_agent, created_at)
                VALUES (@KeyId, @KeyName, @ClientIp, @Endpoint, @Method, @StatusCode, @ResponseTimeMs, @UserAgent, @CreatedAt)";

        await connection.ExecuteAsync(sql, log);
    }

    #endregion

    public void Dispose()
    {
        _sqliteConnection?.Close();
        _sqliteConnection?.Dispose();
        _mysqlConnection?.Close();
        _mysqlConnection?.Dispose();
    }
}
