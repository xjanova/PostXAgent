using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace AIManager.Core.Services;

/// <summary>
/// Global debug logger that writes all logs to daily log files
/// Thread-safe singleton implementation
/// </summary>
public sealed class DebugLogger : IDisposable
{
    private static readonly Lazy<DebugLogger> _instance = new(() => new DebugLogger());
    public static DebugLogger Instance => _instance.Value;

    private readonly string _logDirectory;
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writeTask;
    private readonly object _fileLock = new();
    private StreamWriter? _currentWriter;
    private string _currentLogFile = "";
    private DateTime _currentDate = DateTime.MinValue;

    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    private record LogEntry(
        DateTime Timestamp,
        LogLevel Level,
        string Category,
        string Message,
        string? Exception,
        string? CallerFile,
        string? CallerMethod,
        int CallerLine);

    private DebugLogger()
    {
        // Create logs directory in AppData
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent", "logs");

        Directory.CreateDirectory(_logDirectory);

        // Start background writer task
        _writeTask = Task.Run(WriteLogsAsync);

        // Log startup
        LogInfo("DebugLogger", $"Logger initialized. Log directory: {_logDirectory}");
        LogInfo("DebugLogger", $"System: {Environment.OSVersion}, .NET: {Environment.Version}");
        LogInfo("DebugLogger", $"Processors: {Environment.ProcessorCount}, 64-bit: {Environment.Is64BitProcess}");
    }

    #region Public Logging Methods

    public void LogTrace(string category, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Enqueue(LogLevel.Trace, category, message, null, callerFile, callerMethod, callerLine);

    public void LogDebug(string category, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Enqueue(LogLevel.Debug, category, message, null, callerFile, callerMethod, callerLine);

    public void LogInfo(string category, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Enqueue(LogLevel.Info, category, message, null, callerFile, callerMethod, callerLine);

    public void LogWarning(string category, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Enqueue(LogLevel.Warning, category, message, null, callerFile, callerMethod, callerLine);

    public void LogError(string category, string message, Exception? ex = null,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Enqueue(LogLevel.Error, category, message, FormatException(ex), callerFile, callerMethod, callerLine);

    public void LogCritical(string category, string message, Exception? ex = null,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Enqueue(LogLevel.Critical, category, message, FormatException(ex), callerFile, callerMethod, callerLine);

    /// <summary>
    /// Log method entry with parameters
    /// </summary>
    public void LogMethodEntry(string category, object?[]? parameters = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        var paramStr = parameters != null && parameters.Length > 0
            ? string.Join(", ", parameters.Select(p => p?.ToString() ?? "null"))
            : "";
        Enqueue(LogLevel.Trace, category, $"→ ENTER {callerMethod}({paramStr})", null, callerFile, callerMethod, callerLine);
    }

    /// <summary>
    /// Log method exit with optional result
    /// </summary>
    public void LogMethodExit(string category, object? result = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        var resultStr = result != null ? $" = {result}" : "";
        Enqueue(LogLevel.Trace, category, $"← EXIT {callerMethod}{resultStr}", null, callerFile, callerMethod, callerLine);
    }

    /// <summary>
    /// Log performance timing
    /// </summary>
    public IDisposable LogTiming(string category, string operation,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
    {
        return new TimingLogger(this, category, operation, callerFile, callerMethod, callerLine);
    }

    #endregion

    #region Internal Methods

    private void Enqueue(LogLevel level, string category, string message, string? exception,
        string? callerFile, string? callerMethod, int callerLine)
    {
        var entry = new LogEntry(
            DateTime.Now,
            level,
            category,
            message,
            exception,
            Path.GetFileName(callerFile),
            callerMethod,
            callerLine);

        _logQueue.Enqueue(entry);

        // Also write to debug output
        Debug.WriteLine(FormatLogLine(entry));
    }

    private async Task WriteLogsAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                // Process all queued entries
                while (_logQueue.TryDequeue(out var entry))
                {
                    await WriteEntryAsync(entry);
                }

                // Wait a bit before next check
                await Task.Delay(100, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DebugLogger] Write error: {ex.Message}");
            }
        }

        // Flush remaining entries on shutdown
        while (_logQueue.TryDequeue(out var entry))
        {
            try { await WriteEntryAsync(entry); } catch { }
        }
    }

    private async Task WriteEntryAsync(LogEntry entry)
    {
        lock (_fileLock)
        {
            // Check if we need a new file (new day)
            if (entry.Timestamp.Date != _currentDate)
            {
                _currentWriter?.Dispose();

                _currentDate = entry.Timestamp.Date;
                _currentLogFile = Path.Combine(_logDirectory, $"debug_{_currentDate:yyyy-MM-dd}.log");

                _currentWriter = new StreamWriter(_currentLogFile, append: true, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                // Write header for new file
                _currentWriter.WriteLine("═══════════════════════════════════════════════════════════════════");
                _currentWriter.WriteLine($"  PostXAgent Debug Log - {_currentDate:yyyy-MM-dd}");
                _currentWriter.WriteLine($"  Machine: {Environment.MachineName}");
                _currentWriter.WriteLine($"  User: {Environment.UserName}");
                _currentWriter.WriteLine("═══════════════════════════════════════════════════════════════════");
                _currentWriter.WriteLine();
            }

            // Write the log entry
            _currentWriter?.WriteLine(FormatLogLine(entry));

            // Write exception details if present
            if (!string.IsNullOrEmpty(entry.Exception))
            {
                foreach (var line in entry.Exception.Split('\n'))
                {
                    _currentWriter?.WriteLine($"    {line.TrimEnd()}");
                }
            }
        }
    }

    private static string FormatLogLine(LogEntry entry)
    {
        var levelIcon = entry.Level switch
        {
            LogLevel.Trace => "🔍",
            LogLevel.Debug => "🐛",
            LogLevel.Info => "ℹ️",
            LogLevel.Warning => "⚠️",
            LogLevel.Error => "❌",
            LogLevel.Critical => "💀",
            _ => "  "
        };

        var levelStr = entry.Level.ToString().ToUpper().PadRight(8);
        var location = $"{entry.CallerFile}:{entry.CallerLine} {entry.CallerMethod}";

        return $"[{entry.Timestamp:HH:mm:ss.fff}] {levelIcon} {levelStr} [{entry.Category}] {entry.Message} ({location})";
    }

    private static string? FormatException(Exception? ex)
    {
        if (ex == null) return null;

        var sb = new StringBuilder();
        sb.AppendLine($"Exception: {ex.GetType().FullName}");
        sb.AppendLine($"Message: {ex.Message}");
        sb.AppendLine($"StackTrace:");
        sb.AppendLine(ex.StackTrace);

        if (ex.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine("--- Inner Exception ---");
            sb.Append(FormatException(ex.InnerException));
        }

        return sb.ToString();
    }

    #endregion

    #region Timing Logger

    private class TimingLogger : IDisposable
    {
        private readonly DebugLogger _logger;
        private readonly string _category;
        private readonly string _operation;
        private readonly string? _callerFile;
        private readonly string? _callerMethod;
        private readonly int _callerLine;
        private readonly Stopwatch _stopwatch;

        public TimingLogger(DebugLogger logger, string category, string operation,
            string? callerFile, string? callerMethod, int callerLine)
        {
            _logger = logger;
            _category = category;
            _operation = operation;
            _callerFile = callerFile;
            _callerMethod = callerMethod;
            _callerLine = callerLine;
            _stopwatch = Stopwatch.StartNew();

            _logger.Enqueue(LogLevel.Debug, category, $"⏱️ START: {operation}", null, callerFile, callerMethod, callerLine);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.Enqueue(LogLevel.Debug, _category,
                $"⏱️ END: {_operation} - Elapsed: {_stopwatch.ElapsedMilliseconds}ms",
                null, _callerFile, _callerMethod, _callerLine);
        }
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Get path to today's log file
    /// </summary>
    public string GetCurrentLogFile() => _currentLogFile;

    /// <summary>
    /// Get log directory path
    /// </summary>
    public string GetLogDirectory() => _logDirectory;

    /// <summary>
    /// Get all log files
    /// </summary>
    public IEnumerable<string> GetAllLogFiles()
    {
        return Directory.GetFiles(_logDirectory, "debug_*.log")
            .OrderByDescending(f => f);
    }

    /// <summary>
    /// Clean up old log files (keep last N days)
    /// </summary>
    public void CleanupOldLogs(int keepDays = 30)
    {
        var cutoff = DateTime.Now.AddDays(-keepDays);
        var oldFiles = Directory.GetFiles(_logDirectory, "debug_*.log")
            .Where(f => File.GetCreationTime(f) < cutoff)
            .ToList();

        foreach (var file in oldFiles)
        {
            try
            {
                File.Delete(file);
                LogInfo("DebugLogger", $"Deleted old log file: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                LogWarning("DebugLogger", $"Failed to delete old log: {Path.GetFileName(file)} - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Flush all pending logs to disk
    /// </summary>
    public void Flush()
    {
        // Wait for queue to empty
        while (!_logQueue.IsEmpty)
        {
            Thread.Sleep(50);
        }

        lock (_fileLock)
        {
            _currentWriter?.Flush();
        }
    }

    #endregion

    public void Dispose()
    {
        LogInfo("DebugLogger", "Logger shutting down...");
        Flush();

        _cts.Cancel();
        try { _writeTask.Wait(TimeSpan.FromSeconds(5)); } catch { }

        lock (_fileLock)
        {
            _currentWriter?.Dispose();
        }

        _cts.Dispose();
    }
}

/// <summary>
/// Extension methods for easy logging
/// </summary>
public static class LoggerExtensions
{
    private static DebugLogger Logger => DebugLogger.Instance;

    public static void LogTrace(this object obj, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Logger.LogTrace(obj.GetType().Name, message, callerFile, callerMethod, callerLine);

    public static void LogDebug(this object obj, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Logger.LogDebug(obj.GetType().Name, message, callerFile, callerMethod, callerLine);

    public static void LogInfo(this object obj, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Logger.LogInfo(obj.GetType().Name, message, callerFile, callerMethod, callerLine);

    public static void LogWarning(this object obj, string message,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Logger.LogWarning(obj.GetType().Name, message, callerFile, callerMethod, callerLine);

    public static void LogError(this object obj, string message, Exception? ex = null,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Logger.LogError(obj.GetType().Name, message, ex, callerFile, callerMethod, callerLine);

    public static void LogCritical(this object obj, string message, Exception? ex = null,
        [CallerFilePath] string? callerFile = null,
        [CallerMemberName] string? callerMethod = null,
        [CallerLineNumber] int callerLine = 0)
        => Logger.LogCritical(obj.GetType().Name, message, ex, callerFile, callerMethod, callerLine);
}
