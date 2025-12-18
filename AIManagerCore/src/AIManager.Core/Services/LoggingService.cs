using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Centralized logging service for the application
/// </summary>
public class LoggingService
{
    private readonly ILogger<LoggingService>? _logger;
    private readonly List<LogEntry> _recentLogs = new();
    private readonly object _lock = new();
    private const int MaxRecentLogs = 1000;

    public event EventHandler<LogEntry>? LogAdded;

    public LoggingService(ILogger<LoggingService>? logger = null)
    {
        _logger = logger;
    }

    public void LogInfo(string message, string? category = null)
    {
        Log(LogLevel.Information, message, category);
    }

    public void LogWarning(string message, string? category = null)
    {
        Log(LogLevel.Warning, message, category);
    }

    public void LogError(string message, Exception? exception = null, string? category = null)
    {
        Log(LogLevel.Error, message, category, exception);
    }

    public void LogDebug(string message, string? category = null)
    {
        Log(LogLevel.Debug, message, category);
    }

    private void Log(LogLevel level, string message, string? category, Exception? exception = null)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Category = category ?? "General",
            Exception = exception?.ToString()
        };

        lock (_lock)
        {
            _recentLogs.Add(entry);
            if (_recentLogs.Count > MaxRecentLogs)
            {
                _recentLogs.RemoveAt(0);
            }
        }

        // Log to ILogger if available
        if (_logger != null)
        {
            _logger.Log(level, exception, "[{Category}] {Message}", category ?? "General", message);
        }
        else
        {
            // Fallback to console/debug
            var timestamp = entry.Timestamp.ToString("HH:mm:ss");
            var levelStr = level.ToString().ToUpper().Substring(0, 4);
            System.Diagnostics.Debug.WriteLine($"[{timestamp}] [{levelStr}] [{category ?? "General"}] {message}");
        }

        // Notify subscribers
        LogAdded?.Invoke(this, entry);
    }

    public List<LogEntry> GetRecentLogs(int count = 100, LogLevel? minLevel = null, string? category = null)
    {
        lock (_lock)
        {
            var query = _recentLogs.AsEnumerable();

            if (minLevel.HasValue)
            {
                query = query.Where(l => l.Level >= minLevel.Value);
            }

            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(l => l.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            return query.OrderByDescending(l => l.Timestamp).Take(count).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _recentLogs.Clear();
        }
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public string Message { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Exception { get; set; }
}
