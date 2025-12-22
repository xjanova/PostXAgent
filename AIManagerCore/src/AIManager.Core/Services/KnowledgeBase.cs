using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Dapper;
using AIManager.Core.Models;
using AIManager.Core.Workers;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Knowledge Base - ฐานความรู้กลางสำหรับ Workers ทั้งหมด
/// </summary>
public class KnowledgeBase
{
    private readonly ILogger<KnowledgeBase> _logger;
    private readonly WorkflowStorage _workflowStorage;
    private readonly string _dbPath;
    private SqliteConnection? _connection;

    // In-memory cache
    private readonly ConcurrentDictionary<string, Knowledge> _knowledgeCache = new();
    private readonly ConcurrentDictionary<string, HumanAssistanceRequest> _assistanceRequests = new();
    private readonly ConcurrentDictionary<string, List<string>> _platformErrors = new();

    // Events
    public event EventHandler<AssistanceRequestedEventArgs>? AssistanceRequested;
    public event EventHandler<KnowledgeLearnedEventArgs>? KnowledgeLearned;

    public KnowledgeBase(
        WorkflowStorage workflowStorage,
        ILogger<KnowledgeBase>? logger = null)
    {
        _workflowStorage = workflowStorage;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<KnowledgeBase>();
        _dbPath = Path.Combine(AppContext.BaseDirectory, "data", "knowledge.db");
    }

    #region Database Initialization

    public async Task InitializeSchemaAsync()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        await _connection.OpenAsync();

        await _connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS knowledge (
                id TEXT PRIMARY KEY,
                platform TEXT NOT NULL,
                error_pattern TEXT NOT NULL,
                original_error TEXT,
                solution TEXT NOT NULL,
                solution_data TEXT,
                success_count INTEGER DEFAULT 0,
                failure_count INTEGER DEFAULT 0,
                created_at TEXT NOT NULL,
                last_used_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_knowledge_platform ON knowledge(platform);
            CREATE INDEX IF NOT EXISTS idx_knowledge_pattern ON knowledge(error_pattern);

            CREATE TABLE IF NOT EXISTS assistance_requests (
                id TEXT PRIMARY KEY,
                worker_id TEXT NOT NULL,
                worker_name TEXT NOT NULL,
                platform TEXT NOT NULL,
                task_type TEXT NOT NULL,
                task_id TEXT NOT NULL,
                error_message TEXT NOT NULL,
                requested_at TEXT NOT NULL,
                status TEXT NOT NULL,
                resolution TEXT,
                new_workflow_json TEXT,
                resolved_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_assistance_status ON assistance_requests(status);
        ");

        _logger.LogInformation("Knowledge Base schema initialized");
    }

    #endregion

    #region Knowledge Management

    public async Task<Knowledge?> FindSolutionAsync(SocialPlatform platform, string errorMessage)
    {
        var cacheKey = $"{platform}:{GetErrorPattern(errorMessage)}";
        if (_knowledgeCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (_connection == null) return null;

        try
        {
            var pattern = GetErrorPattern(errorMessage);
            var result = await _connection.QueryFirstOrDefaultAsync<KnowledgeRecord>(
                @"SELECT * FROM knowledge
                  WHERE platform = @Platform AND error_pattern = @Pattern
                  AND success_count > failure_count
                  ORDER BY success_count DESC LIMIT 1",
                new { Platform = platform.ToString(), Pattern = pattern }
            );

            if (result != null)
            {
                var knowledge = result.ToKnowledge();
                _knowledgeCache.TryAdd(cacheKey, knowledge);
                return knowledge;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find solution");
        }

        return null;
    }

    public async Task SaveKnowledgeAsync(Knowledge knowledge)
    {
        if (_connection == null) return;

        try
        {
            await _connection.ExecuteAsync(
                @"INSERT INTO knowledge (id, platform, error_pattern, original_error, solution, solution_data, success_count, failure_count, created_at, last_used_at)
                  VALUES (@Id, @Platform, @ErrorPattern, @OriginalError, @Solution, @SolutionData, @SuccessCount, @FailureCount, @CreatedAt, @LastUsedAt)
                  ON CONFLICT(id) DO UPDATE SET
                  success_count = @SuccessCount, failure_count = @FailureCount, last_used_at = @LastUsedAt",
                new
                {
                    knowledge.Id,
                    Platform = knowledge.Platform.ToString(),
                    knowledge.ErrorPattern,
                    knowledge.OriginalError,
                    knowledge.Solution,
                    knowledge.SolutionData,
                    knowledge.SuccessCount,
                    knowledge.FailureCount,
                    CreatedAt = knowledge.CreatedAt.ToString("o"),
                    LastUsedAt = DateTime.UtcNow.ToString("o")
                }
            );

            var cacheKey = $"{knowledge.Platform}:{knowledge.ErrorPattern}";
            _knowledgeCache[cacheKey] = knowledge;

            _logger.LogInformation("📚 Saved knowledge: {Pattern} for {Platform}", knowledge.ErrorPattern, knowledge.Platform);
            KnowledgeLearned?.Invoke(this, new KnowledgeLearnedEventArgs(knowledge));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save knowledge");
        }
    }

    public async Task RecordSuccessAsync(SocialPlatform platform, string taskType, string? errorPattern)
    {
        if (_connection == null || string.IsNullOrEmpty(errorPattern)) return;

        await _connection.ExecuteAsync(
            @"UPDATE knowledge SET success_count = success_count + 1, last_used_at = @Now
              WHERE platform = @Platform AND error_pattern = @Pattern",
            new { Platform = platform.ToString(), Pattern = errorPattern, Now = DateTime.UtcNow.ToString("o") }
        );
    }

    public async Task<LearnedWorkflow?> FindSimilarWorkflowAsync(SocialPlatform platform, string taskType)
    {
        return await _workflowStorage.FindWorkflowAsync(platform.ToString(), taskType);
    }

    #endregion

    #region Human Assistance

    public async Task SaveAssistanceRequestAsync(HumanAssistanceRequest request)
    {
        if (_connection == null) return;

        try
        {
            await _connection.ExecuteAsync(
                @"INSERT INTO assistance_requests (id, worker_id, worker_name, platform, task_type, task_id, error_message, requested_at, status)
                  VALUES (@Id, @WorkerId, @WorkerName, @Platform, @TaskType, @TaskId, @ErrorMessage, @RequestedAt, @Status)",
                new
                {
                    request.Id,
                    request.WorkerId,
                    request.WorkerName,
                    Platform = request.Platform.ToString(),
                    request.TaskType,
                    request.TaskId,
                    request.ErrorMessage,
                    RequestedAt = request.RequestedAt.ToString("o"),
                    Status = request.Status.ToString()
                }
            );

            _assistanceRequests[request.Id] = request;
            _logger.LogWarning("🆘 Human assistance requested: {TaskType} on {Platform}", request.TaskType, request.Platform);
            AssistanceRequested?.Invoke(this, new AssistanceRequestedEventArgs(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save assistance request");
        }
    }

    public async Task<List<HumanAssistanceRequest>> GetPendingAssistanceRequestsAsync()
    {
        if (_connection == null) return new();

        var records = await _connection.QueryAsync<AssistanceRecord>(
            "SELECT * FROM assistance_requests WHERE status = 'Pending' ORDER BY requested_at ASC"
        );

        return records.Select(r => r.ToRequest()).ToList();
    }

    public async Task ResolveAssistanceRequestAsync(string requestId, string resolution, LearnedWorkflow? newWorkflow = null)
    {
        if (_connection == null) return;

        var workflowJson = newWorkflow?.ToJson();

        await _connection.ExecuteAsync(
            @"UPDATE assistance_requests
              SET status = 'Resolved', resolution = @Resolution, new_workflow_json = @WorkflowJson, resolved_at = @ResolvedAt
              WHERE id = @Id",
            new { Id = requestId, Resolution = resolution, WorkflowJson = workflowJson, ResolvedAt = DateTime.UtcNow.ToString("o") }
        );

        if (newWorkflow != null)
        {
            await _workflowStorage.SaveWorkflowAsync(newWorkflow);
        }

        _logger.LogInformation("✅ Assistance request resolved: {Id}", requestId);
    }

    #endregion

    #region Error Tracking

    public void TrackError(SocialPlatform platform, string errorMessage)
    {
        var key = platform.ToString();
        var pattern = GetErrorPattern(errorMessage);

        if (!_platformErrors.ContainsKey(key))
            _platformErrors[key] = new List<string>();

        _platformErrors[key].Add(pattern);

        if (_platformErrors[key].Count > 100)
            _platformErrors[key].RemoveAt(0);
    }

    public Dictionary<string, int> GetFrequentErrors(SocialPlatform platform)
    {
        var key = platform.ToString();
        if (!_platformErrors.ContainsKey(key))
            return new Dictionary<string, int>();

        return _platformErrors[key]
            .GroupBy(e => e)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private string GetErrorPattern(string errorMessage)
    {
        var lower = errorMessage.ToLower();

        if (lower.Contains("element not found") || lower.Contains("no such element"))
            return "ElementNotFound";
        if (lower.Contains("session") || lower.Contains("expired") || lower.Contains("token"))
            return "SessionExpired";
        if (lower.Contains("rate limit") || lower.Contains("too many"))
            return "RateLimit";
        if (lower.Contains("network") || lower.Contains("connection") || lower.Contains("timeout"))
            return "NetworkError";
        if (lower.Contains("permission") || lower.Contains("denied") || lower.Contains("forbidden"))
            return "PermissionDenied";
        if (lower.Contains("banned") || lower.Contains("blocked"))
            return "AccountBlocked";

        return "Unknown";
    }

    #endregion
}

#region Models

internal class KnowledgeRecord
{
    public string Id { get; set; } = "";
    public string Platform { get; set; } = "";
    public string ErrorPattern { get; set; } = "";
    public string? OriginalError { get; set; }
    public string Solution { get; set; } = "";
    public string? SolutionData { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public string CreatedAt { get; set; } = "";
    public string? LastUsedAt { get; set; }

    public Knowledge ToKnowledge() => new()
    {
        Id = Id,
        Platform = Enum.TryParse<SocialPlatform>(Platform, out var p) ? p : SocialPlatform.Facebook,
        ErrorPattern = ErrorPattern,
        OriginalError = OriginalError,
        Solution = Solution,
        SolutionData = SolutionData,
        SuccessCount = SuccessCount,
        FailureCount = FailureCount,
        CreatedAt = DateTime.TryParse(CreatedAt, out var d) ? d : DateTime.UtcNow,
        LastUsedAt = DateTime.TryParse(LastUsedAt, out var l) ? l : null
    };
}

internal class AssistanceRecord
{
    public string Id { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string WorkerName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string RequestedAt { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Resolution { get; set; }
    public string? NewWorkflowJson { get; set; }
    public string? ResolvedAt { get; set; }

    public HumanAssistanceRequest ToRequest() => new()
    {
        Id = Id,
        WorkerId = WorkerId,
        WorkerName = WorkerName,
        Platform = Enum.TryParse<SocialPlatform>(Platform, out var p) ? p : SocialPlatform.Facebook,
        TaskType = TaskType,
        TaskId = TaskId,
        ErrorMessage = ErrorMessage,
        RequestedAt = DateTime.TryParse(RequestedAt, out var d) ? d : DateTime.UtcNow,
        Status = Enum.TryParse<AssistanceStatus>(Status, out var s) ? s : AssistanceStatus.Pending,
        Resolution = Resolution,
        NewWorkflowJson = NewWorkflowJson,
        ResolvedAt = DateTime.TryParse(ResolvedAt, out var r) ? r : null
    };
}

public class AssistanceRequestedEventArgs : EventArgs
{
    public HumanAssistanceRequest Request { get; }
    public AssistanceRequestedEventArgs(HumanAssistanceRequest request) => Request = request;
}

public class KnowledgeLearnedEventArgs : EventArgs
{
    public Knowledge Knowledge { get; }
    public KnowledgeLearnedEventArgs(Knowledge knowledge) => Knowledge = knowledge;
}

#endregion
