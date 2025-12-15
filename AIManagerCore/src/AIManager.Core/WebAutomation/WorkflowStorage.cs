using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// จัดเก็บและจัดการ Learned Workflows
/// </summary>
public class WorkflowStorage
{
    private readonly ILogger<WorkflowStorage> _logger;
    private readonly string _storagePath;
    private readonly Dictionary<string, LearnedWorkflow> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public WorkflowStorage(ILogger<WorkflowStorage> logger, string? storagePath = null)
    {
        _logger = logger;
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PostXAgent",
            "workflows"
        );

        Directory.CreateDirectory(_storagePath);
    }

    /// <summary>
    /// บันทึก Workflow
    /// </summary>
    public async Task SaveWorkflowAsync(LearnedWorkflow workflow, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            workflow.UpdatedAt = DateTime.UtcNow;

            var filePath = GetWorkflowFilePath(workflow.Id);
            var json = JsonConvert.SerializeObject(workflow, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json, ct);

            _cache[workflow.Id] = workflow;

            _logger.LogInformation("Saved workflow {Id}: {Name}", workflow.Id, workflow.Name);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// โหลด Workflow ตาม ID
    /// </summary>
    public async Task<LearnedWorkflow?> LoadWorkflowAsync(string id, CancellationToken ct = default)
    {
        // Check cache first
        if (_cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var filePath = GetWorkflowFilePath(id);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, ct);
            var workflow = JsonConvert.DeserializeObject<LearnedWorkflow>(json);

            if (workflow != null)
            {
                _cache[id] = workflow;
            }

            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workflow {Id}", id);
            return null;
        }
    }

    /// <summary>
    /// ค้นหา Workflow ตาม Platform และ Type
    /// </summary>
    public async Task<LearnedWorkflow?> FindWorkflowAsync(
        string platform,
        string workflowType,
        CancellationToken ct = default)
    {
        var workflows = await GetAllWorkflowsAsync(ct);

        return workflows
            .Where(w => w.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase) &&
                        w.Name.Equals(workflowType, StringComparison.OrdinalIgnoreCase) &&
                        w.IsActive)
            .OrderByDescending(w => w.ConfidenceScore)
            .ThenByDescending(w => w.SuccessCount)
            .FirstOrDefault();
    }

    /// <summary>
    /// ค้นหา Workflow ที่คล้ายกัน
    /// </summary>
    public async Task<LearnedWorkflow?> FindSimilarWorkflowAsync(
        string platform,
        string workflowType,
        CancellationToken ct = default)
    {
        var workflows = await GetAllWorkflowsAsync(ct);

        // ค้นหาแบบ fuzzy matching
        return workflows
            .Where(w => w.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
            .Where(w => IsSimilarWorkflowType(w.Name, workflowType))
            .OrderByDescending(w => w.ConfidenceScore)
            .FirstOrDefault();
    }

    /// <summary>
    /// โหลด Workflow ทั้งหมด
    /// </summary>
    public async Task<List<LearnedWorkflow>> GetAllWorkflowsAsync(CancellationToken ct = default)
    {
        var workflows = new List<LearnedWorkflow>();

        if (!Directory.Exists(_storagePath))
        {
            return workflows;
        }

        foreach (var file in Directory.GetFiles(_storagePath, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var workflow = JsonConvert.DeserializeObject<LearnedWorkflow>(json);
                if (workflow != null)
                {
                    workflows.Add(workflow);
                    _cache[workflow.Id] = workflow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load workflow from {File}", file);
            }
        }

        return workflows;
    }

    /// <summary>
    /// โหลด Workflows ตาม Platform
    /// </summary>
    public async Task<List<LearnedWorkflow>> GetWorkflowsByPlatformAsync(
        string platform,
        CancellationToken ct = default)
    {
        var all = await GetAllWorkflowsAsync(ct);
        return all
            .Where(w => w.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// ลบ Workflow
    /// </summary>
    public async Task DeleteWorkflowAsync(string id, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var filePath = GetWorkflowFilePath(id);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            _cache.Remove(id);

            _logger.LogInformation("Deleted workflow {Id}", id);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Export Workflow เป็น JSON
    /// </summary>
    public async Task<string> ExportWorkflowAsync(string id, CancellationToken ct = default)
    {
        var workflow = await LoadWorkflowAsync(id, ct);
        if (workflow == null)
        {
            throw new ArgumentException($"Workflow {id} not found");
        }

        return JsonConvert.SerializeObject(workflow, Formatting.Indented);
    }

    /// <summary>
    /// Import Workflow จาก JSON
    /// </summary>
    public async Task<LearnedWorkflow> ImportWorkflowAsync(string json, CancellationToken ct = default)
    {
        var workflow = JsonConvert.DeserializeObject<LearnedWorkflow>(json);
        if (workflow == null)
        {
            throw new ArgumentException("Invalid workflow JSON");
        }

        // สร้าง ID ใหม่
        workflow.Id = Guid.NewGuid().ToString();
        workflow.CreatedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.Steps.ForEach(s => s.LearnedFrom = LearnedSource.Imported);

        await SaveWorkflowAsync(workflow, ct);

        return workflow;
    }

    /// <summary>
    /// สร้าง Backup ของ Workflows ทั้งหมด
    /// </summary>
    public async Task<string> BackupAllAsync(CancellationToken ct = default)
    {
        var workflows = await GetAllWorkflowsAsync(ct);
        var backup = new WorkflowBackup
        {
            Version = "1.0",
            CreatedAt = DateTime.UtcNow,
            Workflows = workflows
        };

        var backupPath = Path.Combine(
            _storagePath,
            "backups",
            $"backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

        var json = JsonConvert.SerializeObject(backup, Formatting.Indented);
        await File.WriteAllTextAsync(backupPath, json, ct);

        _logger.LogInformation("Created backup with {Count} workflows at {Path}", workflows.Count, backupPath);

        return backupPath;
    }

    /// <summary>
    /// Restore จาก Backup
    /// </summary>
    public async Task<int> RestoreFromBackupAsync(string backupPath, CancellationToken ct = default)
    {
        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException("Backup file not found", backupPath);
        }

        var json = await File.ReadAllTextAsync(backupPath, ct);
        var backup = JsonConvert.DeserializeObject<WorkflowBackup>(json);

        if (backup?.Workflows == null)
        {
            throw new ArgumentException("Invalid backup file");
        }

        var count = 0;
        foreach (var workflow in backup.Workflows)
        {
            await SaveWorkflowAsync(workflow, ct);
            count++;
        }

        _logger.LogInformation("Restored {Count} workflows from backup", count);

        return count;
    }

    /// <summary>
    /// ดึงสถิติ Workflows
    /// </summary>
    public async Task<WorkflowStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        var workflows = await GetAllWorkflowsAsync(ct);

        return new WorkflowStatistics
        {
            TotalWorkflows = workflows.Count,
            ActiveWorkflows = workflows.Count(w => w.IsActive),
            TotalSteps = workflows.Sum(w => w.Steps.Count),
            AverageConfidence = workflows.Count > 0
                ? workflows.Average(w => w.ConfidenceScore)
                : 0,
            TotalSuccesses = workflows.Sum(w => w.SuccessCount),
            TotalFailures = workflows.Sum(w => w.FailureCount),
            ByPlatform = workflows
                .GroupBy(w => w.Platform)
                .ToDictionary(g => g.Key, g => g.Count()),
            ByLearnedSource = workflows
                .SelectMany(w => w.Steps)
                .GroupBy(s => s.LearnedFrom)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };
    }

    /// <summary>
    /// ล้าง Workflows ที่ไม่ใช้แล้ว
    /// </summary>
    public async Task<int> CleanupInactiveWorkflowsAsync(
        int daysOld = 30,
        CancellationToken ct = default)
    {
        var workflows = await GetAllWorkflowsAsync(ct);
        var cutoff = DateTime.UtcNow.AddDays(-daysOld);
        var count = 0;

        foreach (var workflow in workflows)
        {
            // ลบ workflow ที่:
            // 1. ไม่ active
            // 2. ไม่เคยสำเร็จ
            // 3. อัพเดทนานแล้ว
            if (!workflow.IsActive &&
                workflow.SuccessCount == 0 &&
                workflow.UpdatedAt < cutoff)
            {
                await DeleteWorkflowAsync(workflow.Id, ct);
                count++;
            }
        }

        _logger.LogInformation("Cleaned up {Count} inactive workflows", count);

        return count;
    }

    private string GetWorkflowFilePath(string id)
    {
        // Sanitize ID for filename
        var safeId = string.Join("_", id.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_storagePath, $"{safeId}.json");
    }

    private bool IsSimilarWorkflowType(string existing, string target)
    {
        // Simple similarity check
        var existingLower = existing.ToLowerInvariant();
        var targetLower = target.ToLowerInvariant();

        // Exact match
        if (existingLower == targetLower) return true;

        // Contains check
        if (existingLower.Contains(targetLower) || targetLower.Contains(existingLower))
            return true;

        // Common workflow type mappings
        var mappings = new Dictionary<string, string[]>
        {
            { "post", new[] { "create_post", "new_post", "publish", "share" } },
            { "login", new[] { "signin", "sign_in", "authenticate" } },
            { "upload", new[] { "upload_image", "upload_video", "add_media" } }
        };

        foreach (var (key, values) in mappings)
        {
            if ((existingLower.Contains(key) || values.Any(v => existingLower.Contains(v))) &&
                (targetLower.Contains(key) || values.Any(v => targetLower.Contains(v))))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Backup container
/// </summary>
public class WorkflowBackup
{
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; }
    public List<LearnedWorkflow> Workflows { get; set; } = new();
}

/// <summary>
/// Workflow statistics
/// </summary>
public class WorkflowStatistics
{
    public int TotalWorkflows { get; set; }
    public int ActiveWorkflows { get; set; }
    public int TotalSteps { get; set; }
    public double AverageConfidence { get; set; }
    public int TotalSuccesses { get; set; }
    public int TotalFailures { get; set; }
    public Dictionary<string, int> ByPlatform { get; set; } = new();
    public Dictionary<string, int> ByLearnedSource { get; set; } = new();
}
