using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AIManager.Core.Services;
using Microsoft.Win32;

namespace AIManager.UI.Views.Pages;

public partial class LogsPage : Page
{
    public ObservableCollection<LogEntry> Logs { get; } = new();
    private readonly DebugLogger _debugLogger;

    public LogsPage()
    {
        InitializeComponent();
        DataContext = this;

        // Subscribe to real log events from DebugLogger
        _debugLogger = DebugLogger.Instance;
        _debugLogger.LogAdded += OnLogAdded;

        // Load existing logs from DebugLogger
        LoadExistingLogs();

        Unloaded += (s, e) =>
        {
            _debugLogger.LogAdded -= OnLogAdded;
        };
    }

    private void LoadExistingLogs()
    {
        var existingLogs = _debugLogger.GetRecentLogs(100);
        foreach (var log in existingLogs)
        {
            Logs.Add(new LogEntry
            {
                Timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = log.Level.ToString().ToUpper(),
                Message = $"[{log.Category}] {log.Message}",
                LevelColor = GetLevelColor(log.Level)
            });
        }
    }

    private void OnLogAdded(object? sender, DebugLogEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            Logs.Insert(0, new LogEntry
            {
                Timestamp = e.Log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                Level = e.Log.Level.ToString().ToUpper(),
                Message = $"[{e.Log.Category}] {e.Log.Message}",
                LevelColor = GetLevelColor(e.Log.Level)
            });

            // Keep only last 500 logs in UI
            while (Logs.Count > 500)
            {
                Logs.RemoveAt(Logs.Count - 1);
            }
        });
    }

    private static string GetLevelColor(DebugLogger.LogLevel level) => level switch
    {
        DebugLogger.LogLevel.Error or DebugLogger.LogLevel.Critical => "#F44336",
        DebugLogger.LogLevel.Warning => "#FF9800",
        DebugLogger.LogLevel.Info => "#2196F3",
        DebugLogger.LogLevel.Debug or DebugLogger.LogLevel.Trace => "#9E9E9E",
        _ => "#FFFFFF"
    };

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        Logs.Clear();
    }

    private void ExportLogs_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|Log files (*.log)|*.log",
                DefaultExt = ".txt",
                FileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var lines = Logs.Select(l => $"[{l.Timestamp}] [{l.Level}] {l.Message}");
                File.WriteAllLines(dialog.FileName, lines);
                MessageBox.Show($"Logs exported to {dialog.FileName}", "Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export logs: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logDir = _debugLogger.GetLogDirectory();
            if (Directory.Exists(logDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDir);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open log folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public class LogEntry
    {
        public string Timestamp { get; set; } = "";
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public string LevelColor { get; set; } = "";
    }
}
