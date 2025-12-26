using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;
using Newtonsoft.Json;

namespace MyPostXAgent.Core.Services.Data;

/// <summary>
/// Service สำหรับจัดการ SQLite Database
/// </summary>
public class DatabaseService : IAsyncDisposable, IDisposable
{
    private readonly ILogger<DatabaseService>? _logger;
    private readonly string _connectionString;
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    public DatabaseService(string? dbPath = null, ILogger<DatabaseService>? logger = null)
    {
        _logger = logger;
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyPostXAgent",
            "data.db");

        // สร้าง folder ถ้ายังไม่มี
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = $"Data Source={_dbPath}";
    }

    /// <summary>
    /// เริ่มต้น Database และสร้าง Schema
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger?.LogInformation("Initializing database at: {Path}", _dbPath);

        await using var connection = await GetConnectionAsync(ct);
        await CreateSchemaAsync(connection, ct);

        _logger?.LogInformation("Database initialized successfully");
    }

    /// <summary>
    /// ดึง Connection
    /// </summary>
    private async Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        if (_connection == null || _connection.State != System.Data.ConnectionState.Open)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync(ct);
        }

        return _connection;
    }

    /// <summary>
    /// สร้าง Database Schema
    /// </summary>
    private async Task CreateSchemaAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        const string schema = """
            -- License & Demo
            CREATE TABLE IF NOT EXISTS app_license (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                license_key TEXT NOT NULL,
                machine_id TEXT NOT NULL,
                license_type TEXT NOT NULL,
                status TEXT DEFAULT 'active',
                activated_at TEXT NOT NULL,
                expires_at TEXT,
                last_validated_at TEXT,
                features TEXT,
                metadata TEXT
            );

            CREATE TABLE IF NOT EXISTS demo_status (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                machine_id TEXT NOT NULL UNIQUE,
                first_run_at TEXT NOT NULL,
                demo_expires_at TEXT NOT NULL,
                status TEXT DEFAULT 'active',
                blocked_reason TEXT
            );

            -- Social Accounts
            CREATE TABLE IF NOT EXISTS social_accounts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                platform TEXT NOT NULL,
                account_name TEXT NOT NULL,
                account_id TEXT,
                display_name TEXT,
                profile_url TEXT,
                avatar_url TEXT,
                credentials_encrypted TEXT,
                is_active INTEGER DEFAULT 1,
                daily_post_count INTEGER DEFAULT 0,
                daily_limit INTEGER DEFAULT 50,
                last_post_at TEXT,
                cooldown_until TEXT,
                health_status TEXT DEFAULT 'Healthy',
                last_error TEXT,
                total_posts INTEGER DEFAULT 0,
                success_count INTEGER DEFAULT 0,
                failure_count INTEGER DEFAULT 0,
                created_at TEXT NOT NULL,
                updated_at TEXT
            );

            -- Account Pools
            CREATE TABLE IF NOT EXISTS account_pools (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                platform TEXT NOT NULL,
                rotation_strategy TEXT DEFAULT 'RoundRobin',
                cooldown_minutes INTEGER DEFAULT 30,
                max_posts_per_day INTEGER DEFAULT 50,
                auto_failover INTEGER DEFAULT 1,
                is_active INTEGER DEFAULT 1,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS account_pool_members (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                pool_id INTEGER NOT NULL,
                account_id INTEGER NOT NULL,
                priority INTEGER DEFAULT 0,
                weight INTEGER DEFAULT 100,
                is_active INTEGER DEFAULT 1,
                posts_today INTEGER DEFAULT 0,
                total_posts INTEGER DEFAULT 0,
                last_used_at TEXT,
                cooldown_until TEXT,
                consecutive_failures INTEGER DEFAULT 0,
                FOREIGN KEY (pool_id) REFERENCES account_pools(id) ON DELETE CASCADE,
                FOREIGN KEY (account_id) REFERENCES social_accounts(id) ON DELETE CASCADE
            );

            -- Posts
            CREATE TABLE IF NOT EXISTS posts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                platform TEXT NOT NULL,
                account_id INTEGER,
                pool_id INTEGER,
                content TEXT NOT NULL,
                content_type TEXT DEFAULT 'Text',
                media_paths TEXT,
                hashtags TEXT,
                link TEXT,
                location TEXT,
                visibility TEXT DEFAULT 'public',
                status TEXT DEFAULT 'draft',
                scheduled_at TEXT,
                posted_at TEXT,
                post_url TEXT,
                platform_post_id TEXT,
                retry_count INTEGER DEFAULT 0,
                max_retries INTEGER DEFAULT 3,
                last_error TEXT,
                metadata TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT,
                FOREIGN KEY (account_id) REFERENCES social_accounts(id),
                FOREIGN KEY (pool_id) REFERENCES account_pools(id)
            );

            -- Scheduled Tasks
            CREATE TABLE IF NOT EXISTS scheduled_tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                task_type TEXT NOT NULL,
                target_id INTEGER,
                scheduled_at TEXT NOT NULL,
                status TEXT DEFAULT 'Pending',
                retry_count INTEGER DEFAULT 0,
                max_retries INTEGER DEFAULT 3,
                last_attempt_at TEXT,
                completed_at TEXT,
                error_message TEXT,
                metadata TEXT,
                created_at TEXT NOT NULL
            );

            -- Comments
            CREATE TABLE IF NOT EXISTS comments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                post_id INTEGER NOT NULL,
                platform TEXT NOT NULL,
                platform_comment_id TEXT,
                author_name TEXT,
                author_id TEXT,
                author_avatar TEXT,
                content TEXT NOT NULL,
                sentiment TEXT DEFAULT 'Neutral',
                priority TEXT DEFAULT 'Normal',
                replied INTEGER DEFAULT 0,
                reply_content TEXT,
                replied_at TEXT,
                fetched_at TEXT NOT NULL,
                created_at TEXT,
                FOREIGN KEY (post_id) REFERENCES posts(id) ON DELETE CASCADE
            );

            -- Discovered Groups
            CREATE TABLE IF NOT EXISTS discovered_groups (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                platform TEXT NOT NULL,
                group_id TEXT NOT NULL,
                group_name TEXT NOT NULL,
                group_url TEXT,
                description TEXT,
                member_count INTEGER,
                post_frequency TEXT,
                is_joined INTEGER DEFAULT 0,
                is_approved INTEGER DEFAULT 0,
                is_private INTEGER DEFAULT 0,
                discovered_at TEXT NOT NULL,
                keywords TEXT,
                relevance_score REAL DEFAULT 0,
                last_post_at TEXT
            );

            -- AI Generation Records
            CREATE TABLE IF NOT EXISTS ai_generations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                generation_type TEXT NOT NULL,
                provider TEXT NOT NULL,
                prompt TEXT NOT NULL,
                result_path TEXT,
                result_text TEXT,
                settings TEXT,
                duration_ms INTEGER,
                success INTEGER DEFAULT 1,
                error_message TEXT,
                created_at TEXT NOT NULL
            );

            -- Projects
            CREATE TABLE IF NOT EXISTS projects (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT,
                folder_path TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT
            );

            -- Settings
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                category TEXT DEFAULT 'general',
                updated_at TEXT
            );

            -- Account Status Logs
            CREATE TABLE IF NOT EXISTS account_status_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                account_id INTEGER NOT NULL,
                pool_id INTEGER,
                event_type TEXT NOT NULL,
                post_id INTEGER,
                error_message TEXT,
                metadata TEXT,
                created_at TEXT NOT NULL,
                FOREIGN KEY (account_id) REFERENCES social_accounts(id) ON DELETE CASCADE
            );

            -- Indexes
            CREATE INDEX IF NOT EXISTS idx_posts_status ON posts(status);
            CREATE INDEX IF NOT EXISTS idx_posts_scheduled ON posts(scheduled_at);
            CREATE INDEX IF NOT EXISTS idx_posts_platform ON posts(platform);
            CREATE INDEX IF NOT EXISTS idx_accounts_platform ON social_accounts(platform);
            CREATE INDEX IF NOT EXISTS idx_tasks_scheduled ON scheduled_tasks(scheduled_at, status);
            CREATE INDEX IF NOT EXISTS idx_comments_post ON comments(post_id);
            CREATE INDEX IF NOT EXISTS idx_groups_platform ON discovered_groups(platform);
            CREATE INDEX IF NOT EXISTS idx_status_logs_account ON account_status_logs(account_id);
            """;

        await connection.ExecuteAsync(new CommandDefinition(schema, cancellationToken: ct));
    }

    #region License & Demo

    /// <summary>
    /// ดึงข้อมูล License
    /// </summary>
    public async Task<LicenseInfo?> GetLicenseInfoAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var row = await connection.QueryFirstOrDefaultAsync<LicenseRow>(
            "SELECT * FROM app_license ORDER BY id DESC LIMIT 1",
            ct);

        if (row == null) return null;

        Enum.TryParse<LicenseType>(row.license_type, out var lt);
        Enum.TryParse<LicenseStatus>(row.status, out var ls);

        return new LicenseInfo
        {
            LicenseKey = row.license_key ?? "",
            MachineId = row.machine_id ?? "",
            LicenseType = lt,
            Status = ls,
            ActivatedAt = DateTime.TryParse(row.activated_at, out var activatedAt) ? activatedAt : DateTime.UtcNow,
            ExpiresAt = DateTime.TryParse(row.expires_at, out var expiresAt) ? expiresAt : null,
            LastValidatedAt = DateTime.TryParse(row.last_validated_at, out var lastValidated) ? lastValidated : null,
            Features = !string.IsNullOrEmpty(row.features)
                ? JsonConvert.DeserializeObject<List<string>>(row.features) ?? new List<string>()
                : new List<string>()
        };
    }

    private class LicenseRow
    {
        public string? license_key { get; set; }
        public string? machine_id { get; set; }
        public string? license_type { get; set; }
        public string? status { get; set; }
        public string? activated_at { get; set; }
        public string? expires_at { get; set; }
        public string? last_validated_at { get; set; }
        public string? features { get; set; }
    }

    /// <summary>
    /// บันทึกข้อมูล License
    /// </summary>
    public async Task SaveLicenseInfoAsync(LicenseInfo license, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        await connection.ExecuteAsync("""
            INSERT OR REPLACE INTO app_license
            (id, license_key, machine_id, license_type, status, activated_at, expires_at, last_validated_at, features)
            VALUES (1, @LicenseKey, @MachineId, @LicenseType, @Status, @ActivatedAt, @ExpiresAt, @LastValidatedAt, @Features)
            """,
            new
            {
                license.LicenseKey,
                license.MachineId,
                LicenseType = license.LicenseType.ToString(),
                Status = license.Status.ToString(),
                ActivatedAt = license.ActivatedAt.ToString("O"),
                ExpiresAt = license.ExpiresAt?.ToString("O"),
                LastValidatedAt = license.LastValidatedAt?.ToString("O"),
                Features = JsonConvert.SerializeObject(license.Features)
            });
    }

    /// <summary>
    /// ดึงข้อมูล Demo
    /// </summary>
    public async Task<DemoInfo?> GetDemoInfoAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var row = await connection.QueryFirstOrDefaultAsync<DemoRow>(
            "SELECT * FROM demo_status LIMIT 1",
            ct);

        if (row == null) return null;

        Enum.TryParse<DemoStatus>(row.status, out var ds);

        return new DemoInfo
        {
            MachineId = row.machine_id ?? "",
            FirstRunAt = DateTime.TryParse(row.first_run_at, out var firstRun) ? firstRun : DateTime.UtcNow,
            DemoExpiresAt = DateTime.TryParse(row.demo_expires_at, out var expires) ? expires : DateTime.UtcNow,
            Status = ds
        };
    }

    private class DemoRow
    {
        public string? machine_id { get; set; }
        public string? first_run_at { get; set; }
        public string? demo_expires_at { get; set; }
        public string? status { get; set; }
    }

    /// <summary>
    /// บันทึกข้อมูล Demo
    /// </summary>
    public async Task SaveDemoInfoAsync(DemoInfo demo, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        await connection.ExecuteAsync("""
            INSERT OR REPLACE INTO demo_status
            (id, machine_id, first_run_at, demo_expires_at, status)
            VALUES (1, @MachineId, @FirstRunAt, @DemoExpiresAt, @Status)
            """,
            new
            {
                demo.MachineId,
                FirstRunAt = demo.FirstRunAt.ToString("O"),
                DemoExpiresAt = demo.DemoExpiresAt.ToString("O"),
                Status = demo.Status.ToString()
            });
    }

    #endregion

    #region Settings

    /// <summary>
    /// ดึงค่า Setting
    /// </summary>
    public async Task<string?> GetSettingAsync(string key, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        return await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT value FROM settings WHERE key = @Key",
            new { Key = key });
    }

    /// <summary>
    /// บันทึกค่า Setting
    /// </summary>
    public async Task SetSettingAsync(string key, string value, string category = "general", CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        await connection.ExecuteAsync("""
            INSERT OR REPLACE INTO settings (key, value, category, updated_at)
            VALUES (@Key, @Value, @Category, @UpdatedAt)
            """,
            new
            {
                Key = key,
                Value = value,
                Category = category,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });
    }

    #endregion

    #region Social Accounts

    /// <summary>
    /// ดึง Social Accounts ทั้งหมด
    /// </summary>
    public async Task<List<SocialAccount>> GetSocialAccountsAsync(SocialPlatform? platform = null, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var sql = "SELECT * FROM social_accounts";
        if (platform.HasValue)
        {
            sql += " WHERE platform = @Platform";
        }
        sql += " ORDER BY created_at DESC";

        var rows = await connection.QueryAsync<AccountRow>(sql, new { Platform = platform?.ToString() });

        return rows.Select(row =>
        {
            Enum.TryParse<SocialPlatform>(row.platform, out var platform);
            Enum.TryParse<AccountHealthStatus>(row.health_status, out var healthStatus);

            return new SocialAccount
            {
                Id = row.id,
                Platform = platform,
                AccountName = row.account_name ?? "",
                AccountId = row.account_id,
                DisplayName = row.display_name,
                ProfileUrl = row.profile_url,
                AvatarUrl = row.avatar_url,
                IsActive = row.is_active == 1,
                DailyPostCount = row.daily_post_count ?? 0,
                DailyLimit = row.daily_limit ?? 50,
                LastPostAt = DateTime.TryParse(row.last_post_at, out var lastPost) ? lastPost : null,
                CooldownUntil = DateTime.TryParse(row.cooldown_until, out var cooldown) ? cooldown : null,
                HealthStatus = healthStatus,
                LastError = row.last_error,
                TotalPosts = row.total_posts ?? 0,
                SuccessCount = row.success_count ?? 0,
                FailureCount = row.failure_count ?? 0,
                CreatedAt = DateTime.TryParse(row.created_at, out var created) ? created : DateTime.UtcNow,
                UpdatedAt = DateTime.TryParse(row.updated_at, out var updated) ? updated : null
            };
        }).ToList();
    }

    private class AccountRow
    {
        public int id { get; set; }
        public string? platform { get; set; }
        public string? account_name { get; set; }
        public string? account_id { get; set; }
        public string? display_name { get; set; }
        public string? profile_url { get; set; }
        public string? avatar_url { get; set; }
        public int? is_active { get; set; }
        public int? daily_post_count { get; set; }
        public int? daily_limit { get; set; }
        public string? last_post_at { get; set; }
        public string? cooldown_until { get; set; }
        public string? health_status { get; set; }
        public string? last_error { get; set; }
        public int? total_posts { get; set; }
        public int? success_count { get; set; }
        public int? failure_count { get; set; }
        public string? created_at { get; set; }
        public string? updated_at { get; set; }
    }

    /// <summary>
    /// เพิ่ม Social Account
    /// </summary>
    public async Task<int> AddSocialAccountAsync(SocialAccount account, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var id = await connection.QuerySingleAsync<int>("""
            INSERT INTO social_accounts
            (platform, account_name, account_id, display_name, is_active, daily_limit, health_status, created_at)
            VALUES (@Platform, @AccountName, @AccountId, @DisplayName, @IsActive, @DailyLimit, @HealthStatus, @CreatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                Platform = account.Platform.ToString(),
                account.AccountName,
                account.AccountId,
                account.DisplayName,
                IsActive = account.IsActive ? 1 : 0,
                account.DailyLimit,
                HealthStatus = account.HealthStatus.ToString(),
                CreatedAt = DateTime.UtcNow.ToString("O")
            });

        return id;
    }

    /// <summary>
    /// อัปเดต Social Account
    /// </summary>
    public async Task UpdateSocialAccountAsync(SocialAccount account, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        await connection.ExecuteAsync("""
            UPDATE social_accounts SET
                account_name = @AccountName,
                display_name = @DisplayName,
                is_active = @IsActive,
                daily_limit = @DailyLimit,
                health_status = @HealthStatus,
                daily_post_count = @DailyPostCount,
                last_post_at = @LastPostAt,
                cooldown_until = @CooldownUntil,
                last_error = @LastError,
                total_posts = @TotalPosts,
                success_count = @SuccessCount,
                failure_count = @FailureCount,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new
            {
                account.Id,
                account.AccountName,
                account.DisplayName,
                IsActive = account.IsActive ? 1 : 0,
                account.DailyLimit,
                HealthStatus = account.HealthStatus.ToString(),
                account.DailyPostCount,
                LastPostAt = account.LastPostAt?.ToString("O"),
                CooldownUntil = account.CooldownUntil?.ToString("O"),
                account.LastError,
                account.TotalPosts,
                account.SuccessCount,
                account.FailureCount,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });
    }

    /// <summary>
    /// ลบ Social Account
    /// </summary>
    public async Task DeleteSocialAccountAsync(int accountId, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        await connection.ExecuteAsync("DELETE FROM social_accounts WHERE id = @Id", new { Id = accountId });
    }

    /// <summary>
    /// ดึง Account ตาม ID
    /// </summary>
    public async Task<SocialAccount?> GetSocialAccountByIdAsync(int accountId, CancellationToken ct = default)
    {
        var accounts = await GetSocialAccountsAsync(null, ct);
        return accounts.FirstOrDefault(a => a.Id == accountId);
    }

    /// <summary>
    /// บันทึก Account Credentials (Encrypted)
    /// </summary>
    public async Task SaveAccountCredentialsAsync(AccountCredentials credentials, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        // Encrypt sensitive data
        var encryptedJson = JsonConvert.SerializeObject(credentials);
        // TODO: Add actual encryption

        await connection.ExecuteAsync("""
            UPDATE social_accounts SET credentials_encrypted = @Credentials, updated_at = @UpdatedAt
            WHERE id = @AccountId
            """,
            new
            {
                credentials.AccountId,
                Credentials = encryptedJson,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });
    }

    /// <summary>
    /// ดึง Account Credentials
    /// </summary>
    public async Task<AccountCredentials?> GetAccountCredentialsAsync(int accountId, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var encryptedJson = await connection.QueryFirstOrDefaultAsync<string>(
            "SELECT credentials_encrypted FROM social_accounts WHERE id = @Id",
            new { Id = accountId });

        if (string.IsNullOrEmpty(encryptedJson)) return null;

        // TODO: Add actual decryption
        return JsonConvert.DeserializeObject<AccountCredentials>(encryptedJson);
    }

    #endregion

    #region Posts

    /// <summary>
    /// ดึง Posts ทั้งหมด
    /// </summary>
    public async Task<List<Post>> GetPostsAsync(PostStatus? status = null, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var sql = "SELECT * FROM posts";
        if (status.HasValue)
        {
            sql += " WHERE status = @Status";
        }
        sql += " ORDER BY created_at DESC";

        var rows = await connection.QueryAsync<PostRow>(sql, new { Status = status?.ToString() });

        return rows.Select(row =>
        {
            Enum.TryParse<SocialPlatform>(row.platform, out var platform);
            Enum.TryParse<ContentType>(row.content_type, out var contentType);
            Enum.TryParse<PostStatus>(row.status, out var postStatus);

            return new Post
            {
                Id = row.id,
                Platform = platform,
                AccountId = row.account_id,
                Content = row.content ?? "",
                ContentType = contentType,
                MediaPaths = !string.IsNullOrEmpty(row.media_paths)
                    ? JsonConvert.DeserializeObject<List<string>>(row.media_paths) ?? new()
                    : new(),
                Hashtags = !string.IsNullOrEmpty(row.hashtags)
                    ? JsonConvert.DeserializeObject<List<string>>(row.hashtags) ?? new()
                    : new(),
                Status = postStatus,
                ScheduledAt = DateTime.TryParse(row.scheduled_at, out var scheduledAt) ? scheduledAt : null,
                PostedAt = DateTime.TryParse(row.posted_at, out var postedAt) ? postedAt : null,
                PostUrl = row.post_url,
                LastError = row.last_error,
                CreatedAt = DateTime.TryParse(row.created_at, out var createdAt) ? createdAt : DateTime.UtcNow
            };
        }).ToList();
    }

    private class PostRow
    {
        public int id { get; set; }
        public string? platform { get; set; }
        public int? account_id { get; set; }
        public string? content { get; set; }
        public string? content_type { get; set; }
        public string? media_paths { get; set; }
        public string? hashtags { get; set; }
        public string? status { get; set; }
        public string? scheduled_at { get; set; }
        public string? posted_at { get; set; }
        public string? post_url { get; set; }
        public string? last_error { get; set; }
        public string? created_at { get; set; }
    }

    /// <summary>
    /// เพิ่ม Post
    /// </summary>
    public async Task<int> AddPostAsync(Post post, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        var id = await connection.QuerySingleAsync<int>("""
            INSERT INTO posts
            (platform, account_id, content, content_type, media_paths, hashtags, status, scheduled_at, created_at)
            VALUES (@Platform, @AccountId, @Content, @ContentType, @MediaPaths, @Hashtags, @Status, @ScheduledAt, @CreatedAt);
            SELECT last_insert_rowid();
            """,
            new
            {
                Platform = post.Platform.ToString(),
                post.AccountId,
                post.Content,
                ContentType = post.ContentType.ToString(),
                MediaPaths = JsonConvert.SerializeObject(post.MediaPaths),
                Hashtags = JsonConvert.SerializeObject(post.Hashtags),
                Status = post.Status.ToString(),
                ScheduledAt = post.ScheduledAt?.ToString("O"),
                CreatedAt = DateTime.UtcNow.ToString("O")
            });

        return id;
    }

    /// <summary>
    /// อัปเดต Post
    /// </summary>
    public async Task UpdatePostAsync(Post post, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        await connection.ExecuteAsync("""
            UPDATE posts SET
                content = @Content,
                content_type = @ContentType,
                media_paths = @MediaPaths,
                hashtags = @Hashtags,
                status = @Status,
                scheduled_at = @ScheduledAt,
                posted_at = @PostedAt,
                post_url = @PostUrl,
                last_error = @LastError,
                updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new
            {
                post.Id,
                post.Content,
                ContentType = post.ContentType.ToString(),
                MediaPaths = JsonConvert.SerializeObject(post.MediaPaths),
                Hashtags = JsonConvert.SerializeObject(post.Hashtags),
                Status = post.Status.ToString(),
                ScheduledAt = post.ScheduledAt?.ToString("O"),
                PostedAt = post.PostedAt?.ToString("O"),
                post.PostUrl,
                post.LastError,
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });
    }

    /// <summary>
    /// ลบ Post
    /// </summary>
    public async Task DeletePostAsync(int postId, CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);
        await connection.ExecuteAsync("DELETE FROM posts WHERE id = @Id", new { Id = postId });
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
