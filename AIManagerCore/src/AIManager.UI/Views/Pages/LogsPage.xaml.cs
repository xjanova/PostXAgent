using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace AIManager.UI.Views.Pages;

public partial class LogsPage : Page
{
    public ObservableCollection<LogEntry> Logs { get; } = new();

    public LogsPage()
    {
        InitializeComponent();
        DataContext = this;

        // Add some sample logs
        AddLog("INFO", "Application started");
        AddLog("INFO", "ProcessOrchestrator initialized with 40 CPU cores");
        AddLog("INFO", "Workers created for all platforms");
    }

    private void AddLog(string level, string message)
    {
        Logs.Add(new LogEntry
        {
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Level = level,
            Message = message,
            LevelColor = level switch
            {
                "ERROR" => "#F44336",
                "WARN" => "#FF9800",
                "INFO" => "#2196F3",
                "DEBUG" => "#9E9E9E",
                _ => "#FFFFFF"
            }
        });
    }

    private void ClearLogs_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        Logs.Clear();
    }

    public class LogEntry
    {
        public string Timestamp { get; set; } = "";
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
        public string LevelColor { get; set; } = "";
    }
}
