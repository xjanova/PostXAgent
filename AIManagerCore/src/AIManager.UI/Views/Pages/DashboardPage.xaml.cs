using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Threading;
using AIManager.Core.Models;
using AIManager.Core.Orchestrator;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace AIManager.UI.Views.Pages;

public partial class DashboardPage : Page
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly DispatcherTimer _updateTimer;
    private readonly DateTime _startTime = DateTime.Now;
    private readonly PerformanceCounter? _cpuCounter;

    // Chart data collections
    private readonly ObservableCollection<ObservableValue> _cpuValues = new();
    private readonly ObservableCollection<ObservableValue> _memoryValues = new();
    private readonly ObservableCollection<ObservableValue> _tasksValues = new();

    // Platform colors for pie chart
    private static readonly Dictionary<SocialPlatform, string> PlatformColors = new()
    {
        { SocialPlatform.Facebook, "#1877F2" },
        { SocialPlatform.Instagram, "#DD2A7B" },
        { SocialPlatform.TikTok, "#00F2EA" },
        { SocialPlatform.Twitter, "#1DA1F2" },
        { SocialPlatform.Line, "#00B900" },
        { SocialPlatform.YouTube, "#FF0000" },
        { SocialPlatform.Threads, "#000000" },
        { SocialPlatform.LinkedIn, "#0A66C2" },
        { SocialPlatform.Pinterest, "#E60023" },
        { SocialPlatform.Freepik, "#00C7B7" },
        { SocialPlatform.Runway, "#8B5CF6" },
        { SocialPlatform.PikaLabs, "#F59E0B" },
        { SocialPlatform.LumaAI, "#10B981" },
        { SocialPlatform.SunoAI, "#EC4899" }
    };

    // Platform icons for status list
    private static readonly Dictionary<SocialPlatform, string> PlatformIcons = new()
    {
        { SocialPlatform.Facebook, "Facebook" },
        { SocialPlatform.Instagram, "Instagram" },
        { SocialPlatform.TikTok, "Video" },
        { SocialPlatform.Twitter, "Twitter" },
        { SocialPlatform.Line, "Chat" },
        { SocialPlatform.YouTube, "Youtube" },
        { SocialPlatform.Threads, "At" },
        { SocialPlatform.LinkedIn, "Linkedin" },
        { SocialPlatform.Pinterest, "Pinterest" },
        { SocialPlatform.Freepik, "ImageEdit" },
        { SocialPlatform.Runway, "MovieRoll" },
        { SocialPlatform.PikaLabs, "Animation" },
        { SocialPlatform.LumaAI, "CubeOutline" },
        { SocialPlatform.SunoAI, "MusicNote" }
    };

    public DashboardPage(ProcessOrchestrator orchestrator)
    {
        InitializeComponent();
        _orchestrator = orchestrator;

        // Initialize CPU counter
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call returns 0, need to call twice
        }
        catch
        {
            _cpuCounter = null;
        }

        // Subscribe to stats updates
        _orchestrator.StatsUpdated += OnStatsUpdated;

        // Initialize charts
        InitializeCharts();
        InitializePlatformStatus();

        // Update timer for real-time display (every 1 second)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _updateTimer.Tick += (s, e) => UpdateDisplay();
        _updateTimer.Start();

        // Initial display
        UpdateDisplay();

        Unloaded += (s, e) =>
        {
            _updateTimer.Stop();
            _cpuCounter?.Dispose();
        };
    }

    private void InitializeCharts()
    {
        // Initialize with 60 data points (60 seconds of history)
        for (int i = 0; i < 60; i++)
        {
            _cpuValues.Add(new ObservableValue(0));
            _memoryValues.Add(new ObservableValue(0));
            _tasksValues.Add(new ObservableValue(0));
        }

        // Performance Chart (CPU & Memory)
        PerformanceChart.Series = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Name = "CPU %",
                Values = _cpuValues,
                Fill = new SolidColorPaint(SKColor.Parse("#307C4DFF")),
                Stroke = new SolidColorPaint(SKColor.Parse("#7C4DFF")) { StrokeThickness = 2 },
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.5
            },
            new LineSeries<ObservableValue>
            {
                Name = "Memory %",
                Values = _memoryValues,
                Fill = new SolidColorPaint(SKColor.Parse("#3000BCD4")),
                Stroke = new SolidColorPaint(SKColor.Parse("#00BCD4")) { StrokeThickness = 2 },
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0.5
            }
        };

        PerformanceChart.XAxes = new Axis[]
        {
            new Axis
            {
                ShowSeparatorLines = false,
                Labels = null,
                LabelsPaint = null
            }
        };

        PerformanceChart.YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                MaxLimit = 100,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#808080")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2A3E"))
            }
        };

        // Tasks Throughput Chart
        TasksChart.Series = new ISeries[]
        {
            new ColumnSeries<ObservableValue>
            {
                Name = "Tasks/sec",
                Values = _tasksValues,
                Fill = new SolidColorPaint(SKColor.Parse("#FF9800")),
                Stroke = null,
                MaxBarWidth = 8,
                Rx = 2,
                Ry = 2
            }
        };

        TasksChart.XAxes = new Axis[]
        {
            new Axis
            {
                ShowSeparatorLines = false,
                Labels = null,
                LabelsPaint = null
            }
        };

        TasksChart.YAxes = new Axis[]
        {
            new Axis
            {
                MinLimit = 0,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#808080")),
                SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#2A2A3E"))
            }
        };

        // Platform Distribution Pie Chart - will be updated with real data
        UpdatePlatformDistributionChart();
    }

    private void InitializePlatformStatus()
    {
        // Get real platform data from orchestrator
        UpdatePlatformStatusList();
    }

    private void UpdatePlatformStatusList()
    {
        var workers = _orchestrator.GetWorkers().ToList();
        var stats = _orchestrator.Stats;

        // Group workers by platform
        var platformGroups = workers
            .GroupBy(w => w.Platform)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Build platform status list from real data
        var platforms = new List<PlatformStatusModel>();

        foreach (SocialPlatform platform in Enum.GetValues<SocialPlatform>())
        {
            var platformWorkers = platformGroups.GetValueOrDefault(platform, new List<WorkerInfo>());
            var activeWorkerCount = platformWorkers.Count(w => w.IsActive);
            var totalWorkerCount = platformWorkers.Count;
            var tasksProcessed = platformWorkers.Sum(w => w.TasksProcessed);
            var tasksFailed = platformWorkers.Sum(w => w.TasksFailed);

            // Calculate success rate
            var totalTasks = tasksProcessed + tasksFailed;
            var successRate = totalTasks > 0 ? (tasksProcessed * 100.0 / totalTasks) : 0;

            // Calculate tasks per minute (approximate from tasks per second)
            var tasksPerMin = 0.0;
            if (stats.PlatformStats.TryGetValue(platform, out var platformStats))
            {
                tasksPerMin = platformStats.TasksProcessed / Math.Max(1, stats.Uptime.TotalMinutes);
            }

            // Progress based on active workers ratio
            var progress = totalWorkerCount > 0 ? (activeWorkerCount * 100 / totalWorkerCount) : 0;

            // Status color based on success rate and activity
            string statusColor;
            if (!_orchestrator.IsRunning || activeWorkerCount == 0)
                statusColor = "#757575"; // Gray - inactive
            else if (successRate >= 95)
                statusColor = "#4CAF50"; // Green - good
            else if (successRate >= 80)
                statusColor = "#FF9800"; // Orange - warning
            else
                statusColor = "#F44336"; // Red - error

            var color = PlatformColors.GetValueOrDefault(platform, "#808080");
            var icon = PlatformIcons.GetValueOrDefault(platform, "Web");

            platforms.Add(new PlatformStatusModel
            {
                Name = GetPlatformDisplayName(platform),
                Icon = icon,
                Color = color,
                WorkerCount = $"{activeWorkerCount}/{totalWorkerCount} workers",
                Progress = progress,
                TasksPerMin = tasksPerMin > 0 ? $"{tasksPerMin:F1}/min" : "0/min",
                SuccessRate = totalTasks > 0 ? $"{successRate:F1}%" : "-",
                StatusColor = statusColor
            });
        }

        PlatformStatusList.ItemsSource = platforms;
    }

    private static string GetPlatformDisplayName(SocialPlatform platform) => platform switch
    {
        SocialPlatform.Twitter => "Twitter/X",
        SocialPlatform.SunoAI => "Suno AI",
        SocialPlatform.LumaAI => "Luma AI",
        SocialPlatform.PikaLabs => "Pika Labs",
        _ => platform.ToString()
    };

    private void UpdatePlatformDistributionChart()
    {
        var stats = _orchestrator.Stats;
        var seriesList = new List<ISeries>();

        if (stats.PlatformStats.Count > 0)
        {
            // Use real data from platform stats
            foreach (var (platform, platformStats) in stats.PlatformStats)
            {
                if (platformStats.TasksProcessed > 0)
                {
                    var color = PlatformColors.GetValueOrDefault(platform, "#808080");
                    seriesList.Add(new PieSeries<double>
                    {
                        Name = GetPlatformDisplayName(platform),
                        Values = new double[] { platformStats.TasksProcessed },
                        Fill = new SolidColorPaint(SKColor.Parse(color))
                    });
                }
            }
        }

        // If no real data, show placeholder indicating system is idle
        if (seriesList.Count == 0)
        {
            seriesList.Add(new PieSeries<double>
            {
                Name = "No Tasks Yet",
                Values = new double[] { 1 },
                Fill = new SolidColorPaint(SKColor.Parse("#2A2A3E"))
            });
        }

        PlatformChart.Series = seriesList.ToArray();
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var stats = e.Stats;
            TxtActiveWorkers.Text = stats.ActiveWorkers.ToString();
            TxtTasksCompleted.Text = stats.TasksCompleted.ToString("N0");
            TxtTasksPerSecond.Text = $"{stats.TasksPerSecond:F1}/sec";
            TxtUptime.Text = FormatUptime(stats.Uptime);

            // Update platform status and chart with new data
            UpdatePlatformStatusList();
            UpdatePlatformDistributionChart();
        });
    }

    private void UpdateDisplay()
    {
        // Get real system metrics
        var cpuUsage = GetCpuUsage();
        var (memoryMB, memoryPercent) = GetMemoryUsage();
        var uptime = _orchestrator.IsRunning ? _orchestrator.Stats.Uptime : (DateTime.Now - _startTime);

        // Update stat cards with real values
        TxtCpuUsage.Text = cpuUsage.ToString("F0");
        TxtCpuCores.Text = $"{Environment.ProcessorCount} cores";
        TxtMemoryUsage.Text = memoryMB.ToString("N0");
        TxtMemoryPercent.Text = $"{memoryPercent:F1}% used";
        TxtUptime.Text = FormatUptime(uptime);
        TxtSystemStatus.Text = _orchestrator.IsRunning ? "Running" : "Stopped";

        // Update stats from orchestrator (real data only)
        var stats = _orchestrator.Stats;
        TxtActiveWorkers.Text = _orchestrator.IsRunning ? _orchestrator.ActiveWorkers.ToString() : "0";
        TxtTasksCompleted.Text = stats.TasksCompleted.ToString("N0");
        TxtTasksPerSecond.Text = $"{stats.TasksPerSecond:F1}/sec";

        // Update chart data (shift left and add new value)
        ShiftAndAdd(_cpuValues, cpuUsage);
        ShiftAndAdd(_memoryValues, memoryPercent);
        ShiftAndAdd(_tasksValues, stats.TasksPerSecond);

        // Update network stats with real data
        UpdateNetworkStats(stats);
    }

    private void UpdateNetworkStats(OrchestratorStats stats)
    {
        // Calculate real metrics
        var totalTasks = stats.TasksCompleted + stats.TasksFailed;
        var successRate = totalTasks > 0 ? (stats.TasksCompleted * 100.0 / totalTasks) : 0;
        var errorRate = totalTasks > 0 ? (stats.TasksFailed * 100.0 / totalTasks) : 0;

        // Requests per second (approximate - each task involves multiple API calls)
        var requestsPerSec = stats.TasksPerSecond * 2.5;

        // Calculate trend based on recent activity
        var trend = stats.TasksPerSecond > 0 ? "+Active" : "Idle";

        // Latency - use actual processing time if available
        var avgLatencyMs = 0.0;
        if (stats.PlatformStats.Count > 0)
        {
            avgLatencyMs = stats.PlatformStats.Values
                .Where(p => p.AverageProcessingTimeMs > 0)
                .Select(p => p.AverageProcessingTimeMs)
                .DefaultIfEmpty(0)
                .Average();
        }

        TxtRequestsPerSec.Text = requestsPerSec.ToString("F0");
        TxtRequestsTrend.Text = trend;
        TxtLatency.Text = avgLatencyMs > 0 ? avgLatencyMs.ToString("F0") : "-";
        TxtSuccessRate.Text = totalTasks > 0 ? successRate.ToString("F1") : "-";
        TxtTotalRequests.Text = $"{totalTasks:N0} total";
        TxtErrors.Text = stats.TasksFailed.ToString("N0");
        TxtErrorRate.Text = totalTasks > 0 ? $"{errorRate:F2}% rate" : "0% rate";
    }

    private double GetCpuUsage()
    {
        try
        {
            if (_cpuCounter != null)
            {
                return _cpuCounter.NextValue();
            }
        }
        catch { }

        // Fallback: return 0 if counter not available
        return 0;
    }

    private static (long memoryMB, double percent) GetMemoryUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            var memoryMB = process.WorkingSet64 / (1024 * 1024);

            // Get total system memory
            var gcInfo = GC.GetGCMemoryInfo();
            var totalMemory = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            var percent = totalMemory > 0 ? (memoryMB * 100.0 / totalMemory) : 0;

            return (memoryMB, percent);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static void ShiftAndAdd(ObservableCollection<ObservableValue> collection, double newValue)
    {
        // Remove first element
        if (collection.Count > 0)
        {
            collection.RemoveAt(0);
        }

        // Add new value at end
        collection.Add(new ObservableValue(newValue));
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours:D2}:{uptime.Minutes:D2}";
        return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
    }
}

public class PlatformStatusModel
{
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    public string WorkerCount { get; set; } = "";
    public int Progress { get; set; }
    public string TasksPerMin { get; set; } = "";
    public string SuccessRate { get; set; } = "";
    public string StatusColor { get; set; } = "#4CAF50";
}
