using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;

namespace AIManager.Core.Services;

/// <summary>
/// Database provider types for AI Learning storage
/// </summary>
public enum DatabaseProvider
{
    SQLite,    // Built-in, no installation required
    MySQL,     // External MySQL/MariaDB
    MSSQL      // External Microsoft SQL Server
}

/// <summary>
/// Database connection configuration
/// </summary>
public class DatabaseConfig
{
    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SQLite;

    // SQLite specific
    public string SqliteFilePath { get; set; } = "ai_learning.db";

    // External database settings
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3306; // MySQL default, MSSQL is 1433
    public string DatabaseName { get; set; } = "ai_learning";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";

    // Additional options
    public bool TrustServerCertificate { get; set; } = true; // For MSSQL
    public string? AdditionalConnectionOptions { get; set; }

    public string GetConnectionString()
    {
        return Provider switch
        {
            DatabaseProvider.SQLite => $"Data Source={SqliteFilePath}",
            DatabaseProvider.MySQL => BuildMySqlConnectionString(),
            DatabaseProvider.MSSQL => BuildMsSqlConnectionString(),
            _ => throw new NotSupportedException($"Provider {Provider} not supported")
        };
    }

    private string BuildMySqlConnectionString()
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = Host,
            Port = (uint)Port,
            Database = DatabaseName,
            UserID = Username,
            Password = Password,
            AllowUserVariables = true,
            CharacterSet = "utf8mb4"
        };

        return builder.ConnectionString;
    }

    private string BuildMsSqlConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{Host},{Port}",
            InitialCatalog = DatabaseName,
            UserID = Username,
            Password = Password,
            TrustServerCertificate = TrustServerCertificate,
            Encrypt = true
        };

        return builder.ConnectionString;
    }
}

/// <summary>
/// AI Learning data models
/// </summary>
public class LearningRecord
{
    public long Id { get; set; }
    public string Category { get; set; } = "";
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Metadata { get; set; }
    public double? Confidence { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ContentTemplate
{
    public long Id { get; set; }
    public string TemplateType { get; set; } = ""; // post, caption, hashtag, etc.
    public string Platform { get; set; } = ""; // facebook, instagram, etc.
    public string Language { get; set; } = "th";
    public string Name { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Variables { get; set; } // JSON array of variable names
    public double SuccessRate { get; set; }
    public int UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PostingStrategy
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string StrategyType { get; set; } = ""; // aggressive, moderate, organic, etc.
    public string? TargetAudience { get; set; }
    public string? OptimalTimes { get; set; } // JSON array of optimal posting times
    public int PostsPerDay { get; set; } = 3;
    public int MinIntervalMinutes { get; set; } = 120;
    public string? ContentMix { get; set; } // JSON object with content type ratios
    public string? HashtagStrategy { get; set; }
    public double EngagementScore { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UserBehaviorPattern
{
    public long Id { get; set; }
    public string Platform { get; set; } = "";
    public string PatternType { get; set; } = ""; // engagement, timing, content_preference
    public string PatternData { get; set; } = ""; // JSON
    public double Confidence { get; set; }
    public int SampleSize { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AIConversationMemory
{
    public long Id { get; set; }
    public string SessionId { get; set; } = "";
    public string Role { get; set; } = ""; // user, assistant, system
    public string Content { get; set; } = "";
    public string? Context { get; set; }
    public string? Embedding { get; set; } // Vector embedding as JSON array
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Database connection test result
/// </summary>
public class DatabaseTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ServerVersion { get; set; }
    public TimeSpan ResponseTime { get; set; }
}

/// <summary>
/// AI Learning Database Service - Manages storage for AI learning data
/// Supports SQLite (built-in) and external databases (MySQL, MSSQL)
/// </summary>
public class AILearningDatabaseService : IDisposable
{
    private readonly ILogger<AILearningDatabaseService> _logger;
    private DatabaseConfig _config;
    private DbConnection? _connection;
    private bool _isInitialized;

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsConnected => _connection?.State == ConnectionState.Open;
    public DatabaseProvider CurrentProvider => _config.Provider;

    public AILearningDatabaseService(ILogger<AILearningDatabaseService> logger)
    {
        _logger = logger;
        _config = new DatabaseConfig();
    }

    /// <summary>
    /// Configure database connection
    /// </summary>
    public void Configure(DatabaseConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _isInitialized = false;

        // Close existing connection if any
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }

    /// <summary>
    /// Test database connection without initializing tables
    /// </summary>
    public async Task<DatabaseTestResult> TestConnectionAsync()
    {
        var result = new DatabaseTestResult();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            // Get server version
            result.ServerVersion = _config.Provider switch
            {
                DatabaseProvider.SQLite => "SQLite " + connection.ServerVersion,
                DatabaseProvider.MySQL => await connection.QueryFirstOrDefaultAsync<string>("SELECT VERSION()"),
                DatabaseProvider.MSSQL => await connection.QueryFirstOrDefaultAsync<string>("SELECT @@VERSION"),
                _ => "Unknown"
            };

            result.Success = true;
            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;

            StatusChanged?.Invoke(this, $"Connection test successful: {result.ServerVersion}");
            _logger.LogInformation("Database connection test successful: {Version}", result.ServerVersion);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.ResponseTime = stopwatch.Elapsed;

            ErrorOccurred?.Invoke(this, ex);
            _logger.LogError(ex, "Database connection test failed");
        }

        return result;
    }

    /// <summary>
    /// Initialize database and create tables
    /// </summary>
    public async Task<bool> BuildDatabaseAsync()
    {
        try
        {
            StatusChanged?.Invoke(this, "Building database structure...");

            _connection = CreateConnection();
            await _connection.OpenAsync();

            // Create tables based on provider
            await CreateTablesAsync();

            _isInitialized = true;
            StatusChanged?.Invoke(this, "Database initialized successfully");
            _logger.LogInformation("AI Learning database initialized with provider: {Provider}", _config.Provider);

            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger.LogError(ex, "Failed to initialize database");
            return false;
        }
    }

    /// <summary>
    /// Clear all data from database
    /// </summary>
    public async Task<bool> ClearDataAsync()
    {
        if (!IsConnected) return false;

        try
        {
            StatusChanged?.Invoke(this, "Clearing all data...");

            var tables = new[]
            {
                "ai_conversation_memory",
                "user_behavior_patterns",
                "posting_strategies",
                "content_templates",
                "learning_records"
            };

            foreach (var table in tables)
            {
                var sql = _config.Provider == DatabaseProvider.MSSQL
                    ? $"DELETE FROM {table}"
                    : $"DELETE FROM {table}";

                await _connection!.ExecuteAsync(sql);
            }

            StatusChanged?.Invoke(this, "All data cleared successfully");
            _logger.LogInformation("All AI learning data cleared");
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger.LogError(ex, "Failed to clear data");
            return false;
        }
    }

    /// <summary>
    /// Drop all tables (destructive)
    /// </summary>
    public async Task<bool> DropAllTablesAsync()
    {
        if (!IsConnected) return false;

        try
        {
            StatusChanged?.Invoke(this, "Dropping all tables...");

            var tables = new[]
            {
                "ai_conversation_memory",
                "user_behavior_patterns",
                "posting_strategies",
                "content_templates",
                "learning_records"
            };

            foreach (var table in tables)
            {
                var sql = $"DROP TABLE IF EXISTS {table}";
                await _connection!.ExecuteAsync(sql);
            }

            _isInitialized = false;
            StatusChanged?.Invoke(this, "All tables dropped");
            return true;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
            _logger.LogError(ex, "Failed to drop tables");
            return false;
        }
    }

    #region Learning Records

    public async Task<long> SaveLearningRecordAsync(LearningRecord record)
    {
        EnsureInitialized();

        record.UpdatedAt = DateTime.UtcNow;
        if (record.Id == 0)
            record.CreatedAt = DateTime.UtcNow;

        var sql = record.Id == 0
            ? GetInsertLearningRecordSql()
            : GetUpdateLearningRecordSql();

        if (record.Id == 0)
        {
            record.Id = await _connection!.ExecuteScalarAsync<long>(sql, record);
        }
        else
        {
            await _connection!.ExecuteAsync(sql, record);
        }

        return record.Id;
    }

    public async Task<LearningRecord?> GetLearningRecordAsync(string category, string key)
    {
        EnsureInitialized();

        var sql = "SELECT * FROM learning_records WHERE Category = @Category AND [Key] = @Key";
        if (_config.Provider != DatabaseProvider.MSSQL)
            sql = sql.Replace("[Key]", "`Key`");

        return await _connection!.QueryFirstOrDefaultAsync<LearningRecord>(sql, new { Category = category, Key = key });
    }

    public async Task<IEnumerable<LearningRecord>> GetLearningRecordsByCategoryAsync(string category)
    {
        EnsureInitialized();

        var sql = "SELECT * FROM learning_records WHERE Category = @Category ORDER BY UsageCount DESC";
        return await _connection!.QueryAsync<LearningRecord>(sql, new { Category = category });
    }

    public async Task IncrementUsageCountAsync(long recordId)
    {
        EnsureInitialized();

        var sql = "UPDATE learning_records SET UsageCount = UsageCount + 1, UpdatedAt = @UpdatedAt WHERE Id = @Id";
        await _connection!.ExecuteAsync(sql, new { Id = recordId, UpdatedAt = DateTime.UtcNow });
    }

    #endregion

    #region Content Templates

    public async Task<long> SaveContentTemplateAsync(ContentTemplate template)
    {
        EnsureInitialized();

        template.UpdatedAt = DateTime.UtcNow;
        if (template.Id == 0)
            template.CreatedAt = DateTime.UtcNow;

        var sql = template.Id == 0
            ? GetInsertContentTemplateSql()
            : GetUpdateContentTemplateSql();

        if (template.Id == 0)
        {
            template.Id = await _connection!.ExecuteScalarAsync<long>(sql, template);
        }
        else
        {
            await _connection!.ExecuteAsync(sql, template);
        }

        return template.Id;
    }

    public async Task<IEnumerable<ContentTemplate>> GetContentTemplatesAsync(string? templateType = null, string? platform = null)
    {
        EnsureInitialized();

        var sql = "SELECT * FROM content_templates WHERE IsActive = 1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrEmpty(templateType))
        {
            sql += " AND TemplateType = @TemplateType";
            parameters.Add("TemplateType", templateType);
        }

        if (!string.IsNullOrEmpty(platform))
        {
            sql += " AND Platform = @Platform";
            parameters.Add("Platform", platform);
        }

        sql += " ORDER BY SuccessRate DESC, UsageCount DESC";

        return await _connection!.QueryAsync<ContentTemplate>(sql, parameters);
    }

    public async Task UpdateTemplateSuccessRateAsync(long templateId, double successRate)
    {
        EnsureInitialized();

        var sql = @"UPDATE content_templates
                    SET SuccessRate = @SuccessRate, UsageCount = UsageCount + 1, UpdatedAt = @UpdatedAt
                    WHERE Id = @Id";
        await _connection!.ExecuteAsync(sql, new { Id = templateId, SuccessRate = successRate, UpdatedAt = DateTime.UtcNow });
    }

    #endregion

    #region Posting Strategies

    public async Task<long> SavePostingStrategyAsync(PostingStrategy strategy)
    {
        EnsureInitialized();

        strategy.UpdatedAt = DateTime.UtcNow;
        if (strategy.Id == 0)
            strategy.CreatedAt = DateTime.UtcNow;

        var sql = strategy.Id == 0
            ? GetInsertPostingStrategySql()
            : GetUpdatePostingStrategySql();

        if (strategy.Id == 0)
        {
            strategy.Id = await _connection!.ExecuteScalarAsync<long>(sql, strategy);
        }
        else
        {
            await _connection!.ExecuteAsync(sql, strategy);
        }

        return strategy.Id;
    }

    public async Task<IEnumerable<PostingStrategy>> GetPostingStrategiesAsync(string? strategyType = null)
    {
        EnsureInitialized();

        var sql = "SELECT * FROM posting_strategies WHERE IsActive = 1";

        if (!string.IsNullOrEmpty(strategyType))
            sql += " AND StrategyType = @StrategyType";

        sql += " ORDER BY EngagementScore DESC";

        return await _connection!.QueryAsync<PostingStrategy>(sql, new { StrategyType = strategyType });
    }

    public async Task<PostingStrategy?> GetBestStrategyForPlatformAsync(string platform)
    {
        EnsureInitialized();

        // Get strategies and filter by content that mentions the platform
        var strategies = await GetPostingStrategiesAsync();
        return strategies.OrderByDescending(s => s.EngagementScore).FirstOrDefault();
    }

    #endregion

    #region User Behavior Patterns

    public async Task<long> SaveBehaviorPatternAsync(UserBehaviorPattern pattern)
    {
        EnsureInitialized();

        pattern.CreatedAt = DateTime.UtcNow;
        pattern.AnalyzedAt = DateTime.UtcNow;

        var sql = GetInsertBehaviorPatternSql();
        pattern.Id = await _connection!.ExecuteScalarAsync<long>(sql, pattern);

        return pattern.Id;
    }

    public async Task<IEnumerable<UserBehaviorPattern>> GetBehaviorPatternsAsync(string platform, string? patternType = null)
    {
        EnsureInitialized();

        var sql = "SELECT * FROM user_behavior_patterns WHERE Platform = @Platform";

        if (!string.IsNullOrEmpty(patternType))
            sql += " AND PatternType = @PatternType";

        sql += " ORDER BY AnalyzedAt DESC";

        return await _connection!.QueryAsync<UserBehaviorPattern>(sql, new { Platform = platform, PatternType = patternType });
    }

    #endregion

    #region AI Conversation Memory

    public async Task<long> SaveConversationMemoryAsync(AIConversationMemory memory)
    {
        EnsureInitialized();

        memory.CreatedAt = DateTime.UtcNow;

        var sql = GetInsertConversationMemorySql();
        memory.Id = await _connection!.ExecuteScalarAsync<long>(sql, memory);

        return memory.Id;
    }

    public async Task<IEnumerable<AIConversationMemory>> GetConversationHistoryAsync(string sessionId, int limit = 50)
    {
        EnsureInitialized();

        var sql = $"SELECT * FROM ai_conversation_memory WHERE SessionId = @SessionId ORDER BY CreatedAt DESC";

        if (_config.Provider == DatabaseProvider.MSSQL)
            sql = $"SELECT TOP {limit} * FROM ai_conversation_memory WHERE SessionId = @SessionId ORDER BY CreatedAt DESC";
        else
            sql += $" LIMIT {limit}";

        var results = await _connection!.QueryAsync<AIConversationMemory>(sql, new { SessionId = sessionId });
        return results.Reverse(); // Return in chronological order
    }

    public async Task ClearSessionMemoryAsync(string sessionId)
    {
        EnsureInitialized();

        var sql = "DELETE FROM ai_conversation_memory WHERE SessionId = @SessionId";
        await _connection!.ExecuteAsync(sql, new { SessionId = sessionId });
    }

    #endregion

    #region Statistics

    public async Task<Dictionary<string, long>> GetStatisticsAsync()
    {
        EnsureInitialized();

        var stats = new Dictionary<string, long>();

        var tables = new[]
        {
            ("learning_records", "Learning Records"),
            ("content_templates", "Content Templates"),
            ("posting_strategies", "Posting Strategies"),
            ("user_behavior_patterns", "Behavior Patterns"),
            ("ai_conversation_memory", "Conversation Memory")
        };

        foreach (var (table, name) in tables)
        {
            var count = await _connection!.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {table}");
            stats[name] = count;
        }

        return stats;
    }

    #endregion

    #region Configuration Persistence

    public string ExportConfigToJson()
    {
        return JsonConvert.SerializeObject(_config, Formatting.Indented);
    }

    public void ImportConfigFromJson(string json)
    {
        var config = JsonConvert.DeserializeObject<DatabaseConfig>(json);
        if (config != null)
            Configure(config);
    }

    public DatabaseConfig GetCurrentConfig() => _config;

    #endregion

    #region Private Methods

    private DbConnection CreateConnection()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => new SqliteConnection(_config.GetConnectionString()),
            DatabaseProvider.MySQL => new MySqlConnection(_config.GetConnectionString()),
            DatabaseProvider.MSSQL => new SqlConnection(_config.GetConnectionString()),
            _ => throw new NotSupportedException($"Provider {_config.Provider} not supported")
        };
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized || !IsConnected)
            throw new InvalidOperationException("Database not initialized. Call BuildDatabaseAsync() first.");
    }

    private async Task CreateTablesAsync()
    {
        // Learning Records table
        await _connection!.ExecuteAsync(GetCreateLearningRecordsTableSql());

        // Content Templates table
        await _connection!.ExecuteAsync(GetCreateContentTemplatesTableSql());

        // Posting Strategies table
        await _connection!.ExecuteAsync(GetCreatePostingStrategiesTableSql());

        // User Behavior Patterns table
        await _connection!.ExecuteAsync(GetCreateBehaviorPatternsTableSql());

        // AI Conversation Memory table
        await _connection!.ExecuteAsync(GetCreateConversationMemoryTableSql());

        // Create indexes
        await CreateIndexesAsync();

        // Seed default strategies
        await SeedDefaultStrategiesAsync();
    }

    private async Task CreateIndexesAsync()
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_learning_category ON learning_records(Category)",
            "CREATE INDEX IF NOT EXISTS idx_learning_key ON learning_records(Category, [Key])",
            "CREATE INDEX IF NOT EXISTS idx_template_type ON content_templates(TemplateType, Platform)",
            "CREATE INDEX IF NOT EXISTS idx_strategy_type ON posting_strategies(StrategyType)",
            "CREATE INDEX IF NOT EXISTS idx_behavior_platform ON user_behavior_patterns(Platform, PatternType)",
            "CREATE INDEX IF NOT EXISTS idx_conversation_session ON ai_conversation_memory(SessionId)"
        };

        foreach (var indexSql in indexes)
        {
            try
            {
                var sql = indexSql;
                if (_config.Provider == DatabaseProvider.MySQL)
                {
                    sql = sql.Replace("IF NOT EXISTS ", "");
                    sql = sql.Replace("[Key]", "`Key`");
                }
                else if (_config.Provider == DatabaseProvider.MSSQL)
                {
                    // MSSQL doesn't support IF NOT EXISTS for indexes
                    continue;
                }

                await _connection!.ExecuteAsync(sql);
            }
            catch
            {
                // Index might already exist
            }
        }
    }

    private async Task SeedDefaultStrategiesAsync()
    {
        // Check if strategies already exist
        var count = await _connection!.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM posting_strategies");
        if (count > 0) return;

        var defaultStrategies = new[]
        {
            new PostingStrategy
            {
                Name = "Organic Growth",
                Description = "โพสต์แบบธรรมชาติ เน้นคุณภาพและการมีส่วนร่วม",
                StrategyType = "organic",
                PostsPerDay = 2,
                MinIntervalMinutes = 240,
                OptimalTimes = "[\"09:00\", \"12:00\", \"19:00\"]",
                ContentMix = "{\"promotional\": 20, \"educational\": 40, \"entertaining\": 30, \"engagement\": 10}",
                HashtagStrategy = "ใช้ 5-10 hashtags ที่เกี่ยวข้อง",
                EngagementScore = 0.7
            },
            new PostingStrategy
            {
                Name = "Aggressive Marketing",
                Description = "โพสต์บ่อย เน้นการเข้าถึงมากที่สุด",
                StrategyType = "aggressive",
                PostsPerDay = 5,
                MinIntervalMinutes = 120,
                OptimalTimes = "[\"08:00\", \"10:00\", \"12:00\", \"15:00\", \"19:00\", \"21:00\"]",
                ContentMix = "{\"promotional\": 50, \"educational\": 20, \"entertaining\": 20, \"engagement\": 10}",
                HashtagStrategy = "ใช้ 15-30 hashtags รวม trending",
                EngagementScore = 0.5
            },
            new PostingStrategy
            {
                Name = "Brand Building",
                Description = "สร้างแบรนด์ระยะยาว เน้นความน่าเชื่อถือ",
                StrategyType = "brand_building",
                PostsPerDay = 3,
                MinIntervalMinutes = 180,
                OptimalTimes = "[\"09:00\", \"13:00\", \"20:00\"]",
                ContentMix = "{\"promotional\": 30, \"educational\": 35, \"entertaining\": 25, \"engagement\": 10}",
                HashtagStrategy = "ใช้ branded hashtags และ niche hashtags",
                EngagementScore = 0.8
            },
            new PostingStrategy
            {
                Name = "Product Launch",
                Description = "แคมเปญเปิดตัวสินค้า เน้น hype และ countdown",
                StrategyType = "product_launch",
                PostsPerDay = 4,
                MinIntervalMinutes = 90,
                OptimalTimes = "[\"07:00\", \"11:00\", \"15:00\", \"19:00\", \"22:00\"]",
                ContentMix = "{\"promotional\": 60, \"educational\": 15, \"entertaining\": 15, \"engagement\": 10}",
                HashtagStrategy = "ใช้ campaign hashtag เฉพาะ + trending",
                EngagementScore = 0.6
            },
            new PostingStrategy
            {
                Name = "Community Focus",
                Description = "เน้นสร้างชุมชน ตอบคำถาม และมีส่วนร่วม",
                StrategyType = "community",
                PostsPerDay = 3,
                MinIntervalMinutes = 180,
                OptimalTimes = "[\"10:00\", \"14:00\", \"20:00\"]",
                ContentMix = "{\"promotional\": 15, \"educational\": 30, \"entertaining\": 25, \"engagement\": 30}",
                HashtagStrategy = "ใช้ community hashtags และ local hashtags",
                EngagementScore = 0.85
            }
        };

        foreach (var strategy in defaultStrategies)
        {
            await SavePostingStrategyAsync(strategy);
        }

        _logger.LogInformation("Seeded {Count} default posting strategies", defaultStrategies.Length);
    }

    #region SQL Generation

    private string GetCreateLearningRecordsTableSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                CREATE TABLE IF NOT EXISTS learning_records (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Category TEXT NOT NULL,
                    Key TEXT NOT NULL,
                    Value TEXT NOT NULL,
                    Metadata TEXT,
                    Confidence REAL,
                    UsageCount INTEGER DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                )",
            DatabaseProvider.MySQL => @"
                CREATE TABLE IF NOT EXISTS learning_records (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Category VARCHAR(255) NOT NULL,
                    `Key` VARCHAR(500) NOT NULL,
                    Value LONGTEXT NOT NULL,
                    Metadata JSON,
                    Confidence DOUBLE,
                    UsageCount INT DEFAULT 0,
                    CreatedAt DATETIME NOT NULL,
                    UpdatedAt DATETIME NOT NULL,
                    INDEX idx_category (Category),
                    INDEX idx_key (Category, `Key`(255))
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            DatabaseProvider.MSSQL => @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='learning_records')
                CREATE TABLE learning_records (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    Category NVARCHAR(255) NOT NULL,
                    [Key] NVARCHAR(500) NOT NULL,
                    Value NVARCHAR(MAX) NOT NULL,
                    Metadata NVARCHAR(MAX),
                    Confidence FLOAT,
                    UsageCount INT DEFAULT 0,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL
                )",
            _ => throw new NotSupportedException()
        };
    }

    private string GetCreateContentTemplatesTableSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                CREATE TABLE IF NOT EXISTS content_templates (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TemplateType TEXT NOT NULL,
                    Platform TEXT NOT NULL,
                    Language TEXT DEFAULT 'th',
                    Name TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Variables TEXT,
                    SuccessRate REAL DEFAULT 0,
                    UsageCount INTEGER DEFAULT 0,
                    IsActive INTEGER DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                )",
            DatabaseProvider.MySQL => @"
                CREATE TABLE IF NOT EXISTS content_templates (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    TemplateType VARCHAR(100) NOT NULL,
                    Platform VARCHAR(100) NOT NULL,
                    Language VARCHAR(10) DEFAULT 'th',
                    Name VARCHAR(255) NOT NULL,
                    Content LONGTEXT NOT NULL,
                    Variables JSON,
                    SuccessRate DOUBLE DEFAULT 0,
                    UsageCount INT DEFAULT 0,
                    IsActive TINYINT DEFAULT 1,
                    CreatedAt DATETIME NOT NULL,
                    UpdatedAt DATETIME NOT NULL,
                    INDEX idx_type_platform (TemplateType, Platform)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            DatabaseProvider.MSSQL => @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='content_templates')
                CREATE TABLE content_templates (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    TemplateType NVARCHAR(100) NOT NULL,
                    Platform NVARCHAR(100) NOT NULL,
                    Language NVARCHAR(10) DEFAULT 'th',
                    Name NVARCHAR(255) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    Variables NVARCHAR(MAX),
                    SuccessRate FLOAT DEFAULT 0,
                    UsageCount INT DEFAULT 0,
                    IsActive BIT DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL
                )",
            _ => throw new NotSupportedException()
        };
    }

    private string GetCreatePostingStrategiesTableSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                CREATE TABLE IF NOT EXISTS posting_strategies (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    StrategyType TEXT NOT NULL,
                    TargetAudience TEXT,
                    OptimalTimes TEXT,
                    PostsPerDay INTEGER DEFAULT 3,
                    MinIntervalMinutes INTEGER DEFAULT 120,
                    ContentMix TEXT,
                    HashtagStrategy TEXT,
                    EngagementScore REAL DEFAULT 0,
                    IsActive INTEGER DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                )",
            DatabaseProvider.MySQL => @"
                CREATE TABLE IF NOT EXISTS posting_strategies (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Name VARCHAR(255) NOT NULL,
                    Description TEXT,
                    StrategyType VARCHAR(100) NOT NULL,
                    TargetAudience TEXT,
                    OptimalTimes JSON,
                    PostsPerDay INT DEFAULT 3,
                    MinIntervalMinutes INT DEFAULT 120,
                    ContentMix JSON,
                    HashtagStrategy TEXT,
                    EngagementScore DOUBLE DEFAULT 0,
                    IsActive TINYINT DEFAULT 1,
                    CreatedAt DATETIME NOT NULL,
                    UpdatedAt DATETIME NOT NULL,
                    INDEX idx_strategy_type (StrategyType)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            DatabaseProvider.MSSQL => @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='posting_strategies')
                CREATE TABLE posting_strategies (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    Name NVARCHAR(255) NOT NULL,
                    Description NVARCHAR(MAX),
                    StrategyType NVARCHAR(100) NOT NULL,
                    TargetAudience NVARCHAR(MAX),
                    OptimalTimes NVARCHAR(MAX),
                    PostsPerDay INT DEFAULT 3,
                    MinIntervalMinutes INT DEFAULT 120,
                    ContentMix NVARCHAR(MAX),
                    HashtagStrategy NVARCHAR(MAX),
                    EngagementScore FLOAT DEFAULT 0,
                    IsActive BIT DEFAULT 1,
                    CreatedAt DATETIME2 NOT NULL,
                    UpdatedAt DATETIME2 NOT NULL
                )",
            _ => throw new NotSupportedException()
        };
    }

    private string GetCreateBehaviorPatternsTableSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                CREATE TABLE IF NOT EXISTS user_behavior_patterns (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Platform TEXT NOT NULL,
                    PatternType TEXT NOT NULL,
                    PatternData TEXT NOT NULL,
                    Confidence REAL DEFAULT 0,
                    SampleSize INTEGER DEFAULT 0,
                    AnalyzedAt TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                )",
            DatabaseProvider.MySQL => @"
                CREATE TABLE IF NOT EXISTS user_behavior_patterns (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    Platform VARCHAR(100) NOT NULL,
                    PatternType VARCHAR(100) NOT NULL,
                    PatternData JSON NOT NULL,
                    Confidence DOUBLE DEFAULT 0,
                    SampleSize INT DEFAULT 0,
                    AnalyzedAt DATETIME NOT NULL,
                    CreatedAt DATETIME NOT NULL,
                    INDEX idx_platform_type (Platform, PatternType)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            DatabaseProvider.MSSQL => @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='user_behavior_patterns')
                CREATE TABLE user_behavior_patterns (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    Platform NVARCHAR(100) NOT NULL,
                    PatternType NVARCHAR(100) NOT NULL,
                    PatternData NVARCHAR(MAX) NOT NULL,
                    Confidence FLOAT DEFAULT 0,
                    SampleSize INT DEFAULT 0,
                    AnalyzedAt DATETIME2 NOT NULL,
                    CreatedAt DATETIME2 NOT NULL
                )",
            _ => throw new NotSupportedException()
        };
    }

    private string GetCreateConversationMemoryTableSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                CREATE TABLE IF NOT EXISTS ai_conversation_memory (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SessionId TEXT NOT NULL,
                    Role TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Context TEXT,
                    Embedding TEXT,
                    CreatedAt TEXT NOT NULL
                )",
            DatabaseProvider.MySQL => @"
                CREATE TABLE IF NOT EXISTS ai_conversation_memory (
                    Id BIGINT AUTO_INCREMENT PRIMARY KEY,
                    SessionId VARCHAR(100) NOT NULL,
                    Role VARCHAR(50) NOT NULL,
                    Content LONGTEXT NOT NULL,
                    Context JSON,
                    Embedding JSON,
                    CreatedAt DATETIME NOT NULL,
                    INDEX idx_session (SessionId)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            DatabaseProvider.MSSQL => @"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ai_conversation_memory')
                CREATE TABLE ai_conversation_memory (
                    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                    SessionId NVARCHAR(100) NOT NULL,
                    Role NVARCHAR(50) NOT NULL,
                    Content NVARCHAR(MAX) NOT NULL,
                    Context NVARCHAR(MAX),
                    Embedding NVARCHAR(MAX),
                    CreatedAt DATETIME2 NOT NULL
                )",
            _ => throw new NotSupportedException()
        };
    }

    private string GetInsertLearningRecordSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                INSERT INTO learning_records (Category, Key, Value, Metadata, Confidence, UsageCount, CreatedAt, UpdatedAt)
                VALUES (@Category, @Key, @Value, @Metadata, @Confidence, @UsageCount, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();",
            DatabaseProvider.MySQL => @"
                INSERT INTO learning_records (Category, `Key`, Value, Metadata, Confidence, UsageCount, CreatedAt, UpdatedAt)
                VALUES (@Category, @Key, @Value, @Metadata, @Confidence, @UsageCount, @CreatedAt, @UpdatedAt);
                SELECT LAST_INSERT_ID();",
            DatabaseProvider.MSSQL => @"
                INSERT INTO learning_records (Category, [Key], Value, Metadata, Confidence, UsageCount, CreatedAt, UpdatedAt)
                VALUES (@Category, @Key, @Value, @Metadata, @Confidence, @UsageCount, @CreatedAt, @UpdatedAt);
                SELECT SCOPE_IDENTITY();",
            _ => throw new NotSupportedException()
        };
    }

    private string GetUpdateLearningRecordSql()
    {
        var keyCol = _config.Provider == DatabaseProvider.MySQL ? "`Key`" : "[Key]";
        if (_config.Provider == DatabaseProvider.SQLite) keyCol = "Key";

        return $@"
            UPDATE learning_records
            SET Category = @Category, {keyCol} = @Key, Value = @Value, Metadata = @Metadata,
                Confidence = @Confidence, UsageCount = @UsageCount, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";
    }

    private string GetInsertContentTemplateSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                INSERT INTO content_templates (TemplateType, Platform, Language, Name, Content, Variables, SuccessRate, UsageCount, IsActive, CreatedAt, UpdatedAt)
                VALUES (@TemplateType, @Platform, @Language, @Name, @Content, @Variables, @SuccessRate, @UsageCount, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();",
            DatabaseProvider.MySQL => @"
                INSERT INTO content_templates (TemplateType, Platform, Language, Name, Content, Variables, SuccessRate, UsageCount, IsActive, CreatedAt, UpdatedAt)
                VALUES (@TemplateType, @Platform, @Language, @Name, @Content, @Variables, @SuccessRate, @UsageCount, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT LAST_INSERT_ID();",
            DatabaseProvider.MSSQL => @"
                INSERT INTO content_templates (TemplateType, Platform, Language, Name, Content, Variables, SuccessRate, UsageCount, IsActive, CreatedAt, UpdatedAt)
                VALUES (@TemplateType, @Platform, @Language, @Name, @Content, @Variables, @SuccessRate, @UsageCount, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT SCOPE_IDENTITY();",
            _ => throw new NotSupportedException()
        };
    }

    private string GetUpdateContentTemplateSql()
    {
        return @"
            UPDATE content_templates
            SET TemplateType = @TemplateType, Platform = @Platform, Language = @Language, Name = @Name,
                Content = @Content, Variables = @Variables, SuccessRate = @SuccessRate,
                UsageCount = @UsageCount, IsActive = @IsActive, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";
    }

    private string GetInsertPostingStrategySql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                INSERT INTO posting_strategies (Name, Description, StrategyType, TargetAudience, OptimalTimes, PostsPerDay, MinIntervalMinutes, ContentMix, HashtagStrategy, EngagementScore, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Name, @Description, @StrategyType, @TargetAudience, @OptimalTimes, @PostsPerDay, @MinIntervalMinutes, @ContentMix, @HashtagStrategy, @EngagementScore, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT last_insert_rowid();",
            DatabaseProvider.MySQL => @"
                INSERT INTO posting_strategies (Name, Description, StrategyType, TargetAudience, OptimalTimes, PostsPerDay, MinIntervalMinutes, ContentMix, HashtagStrategy, EngagementScore, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Name, @Description, @StrategyType, @TargetAudience, @OptimalTimes, @PostsPerDay, @MinIntervalMinutes, @ContentMix, @HashtagStrategy, @EngagementScore, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT LAST_INSERT_ID();",
            DatabaseProvider.MSSQL => @"
                INSERT INTO posting_strategies (Name, Description, StrategyType, TargetAudience, OptimalTimes, PostsPerDay, MinIntervalMinutes, ContentMix, HashtagStrategy, EngagementScore, IsActive, CreatedAt, UpdatedAt)
                VALUES (@Name, @Description, @StrategyType, @TargetAudience, @OptimalTimes, @PostsPerDay, @MinIntervalMinutes, @ContentMix, @HashtagStrategy, @EngagementScore, @IsActive, @CreatedAt, @UpdatedAt);
                SELECT SCOPE_IDENTITY();",
            _ => throw new NotSupportedException()
        };
    }

    private string GetUpdatePostingStrategySql()
    {
        return @"
            UPDATE posting_strategies
            SET Name = @Name, Description = @Description, StrategyType = @StrategyType,
                TargetAudience = @TargetAudience, OptimalTimes = @OptimalTimes, PostsPerDay = @PostsPerDay,
                MinIntervalMinutes = @MinIntervalMinutes, ContentMix = @ContentMix,
                HashtagStrategy = @HashtagStrategy, EngagementScore = @EngagementScore,
                IsActive = @IsActive, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";
    }

    private string GetInsertBehaviorPatternSql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                INSERT INTO user_behavior_patterns (Platform, PatternType, PatternData, Confidence, SampleSize, AnalyzedAt, CreatedAt)
                VALUES (@Platform, @PatternType, @PatternData, @Confidence, @SampleSize, @AnalyzedAt, @CreatedAt);
                SELECT last_insert_rowid();",
            DatabaseProvider.MySQL => @"
                INSERT INTO user_behavior_patterns (Platform, PatternType, PatternData, Confidence, SampleSize, AnalyzedAt, CreatedAt)
                VALUES (@Platform, @PatternType, @PatternData, @Confidence, @SampleSize, @AnalyzedAt, @CreatedAt);
                SELECT LAST_INSERT_ID();",
            DatabaseProvider.MSSQL => @"
                INSERT INTO user_behavior_patterns (Platform, PatternType, PatternData, Confidence, SampleSize, AnalyzedAt, CreatedAt)
                VALUES (@Platform, @PatternType, @PatternData, @Confidence, @SampleSize, @AnalyzedAt, @CreatedAt);
                SELECT SCOPE_IDENTITY();",
            _ => throw new NotSupportedException()
        };
    }

    private string GetInsertConversationMemorySql()
    {
        return _config.Provider switch
        {
            DatabaseProvider.SQLite => @"
                INSERT INTO ai_conversation_memory (SessionId, Role, Content, Context, Embedding, CreatedAt)
                VALUES (@SessionId, @Role, @Content, @Context, @Embedding, @CreatedAt);
                SELECT last_insert_rowid();",
            DatabaseProvider.MySQL => @"
                INSERT INTO ai_conversation_memory (SessionId, Role, Content, Context, Embedding, CreatedAt)
                VALUES (@SessionId, @Role, @Content, @Context, @Embedding, @CreatedAt);
                SELECT LAST_INSERT_ID();",
            DatabaseProvider.MSSQL => @"
                INSERT INTO ai_conversation_memory (SessionId, Role, Content, Context, Embedding, CreatedAt)
                VALUES (@SessionId, @Role, @Content, @Context, @Embedding, @CreatedAt);
                SELECT SCOPE_IDENTITY();",
            _ => throw new NotSupportedException()
        };
    }

    #endregion

    #endregion

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
