using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIManager.Core.Models;
using AIManager.Core.Services;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// GPU Node Monitor Page - แสดงและจัดการ GPU Nodes ทั้งหมด
/// </summary>
public partial class GpuNodeMonitorPage : Page
{
    private readonly GpuNodeMonitorService _monitorService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _countdownTimer;
    private List<GpuNodeViewModel> _nodes = new();
    private string _currentFilter = "All Nodes";
    private string _currentSort = "Priority";

    public GpuNodeMonitorPage()
    {
        InitializeComponent();

        // Create monitor service
        _monitorService = new GpuNodeMonitorService();

        // Subscribe to events
        _monitorService.NodeStatusChanged += OnNodeStatusChanged;
        _monitorService.StatsUpdated += OnStatsUpdated;
        _monitorService.NodeSwitchRequired += OnNodeSwitchRequired;
        _monitorService.EmergencyNodeActivated += OnEmergencyNodeActivated;

        // Refresh timer - every 5 seconds
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _refreshTimer.Tick += (s, e) => RefreshUI();

        // Countdown timer - every second
        _countdownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _countdownTimer.Tick += (s, e) => UpdateCountdown();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Create demo nodes
        _monitorService.CreateDemoNodes();
        _monitorService.Start();

        // Initial refresh
        RefreshUI();

        // Start timers
        _refreshTimer.Start();
        _countdownTimer.Start();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _refreshTimer.Stop();
        _countdownTimer.Stop();
        _monitorService.Stop();
        _monitorService.Dispose();
    }

    #region UI Refresh

    private void RefreshUI()
    {
        var stats = _monitorService.GetStats();
        var activeNode = _monitorService.GetActiveNode();
        var allNodes = _monitorService.GetAllNodes();
        var events = _monitorService.GetRecentEvents(20);

        // Update stats
        TxtTotalNodes.Text = stats.TotalNodes.ToString();
        TxtRunningNodes.Text = stats.RunningNodes.ToString();
        TxtReadyNodes.Text = stats.ReadyNodes.ToString();
        TxtErrorNodes.Text = stats.ErrorNodes.ToString();
        TxtTotalQuota.Text = FormatTimeSpan(stats.TotalQuotaRemaining);
        TxtGpuUsage.Text = $"{stats.AverageGpuUtilization:F0}%";

        // Update active node
        if (activeNode != null)
        {
            UpdateActiveNodeUI(activeNode);
            ActiveNodeCard.Visibility = Visibility.Visible;
        }
        else
        {
            ActiveNodeCard.Visibility = Visibility.Collapsed;
        }

        // Update next node
        if (stats.NextNode != null)
        {
            UpdateNextNodeUI(stats.NextNode, stats.NextSwitchTime);
            NextNodeCard.Visibility = Visibility.Visible;
        }
        else
        {
            NextNodeCard.Visibility = Visibility.Collapsed;
        }

        // Update node list
        UpdateNodeList(allNodes, activeNode?.Id);

        // Update events
        UpdateEventsList(events);
    }

    private void UpdateActiveNodeUI(GpuNodeInfo node)
    {
        TxtActiveNodeName.Text = node.DisplayName;
        TxtActiveProvider.Text = node.Provider.ToString();
        TxtActiveGpuType.Text = $"NVIDIA {node.GpuType}";
        TxtActiveAccount.Text = node.AccountEmail ?? "";

        // Session
        var sessionPercent = node.MaxSessionTime.TotalSeconds > 0
            ? (node.SessionDuration.TotalSeconds / node.MaxSessionTime.TotalSeconds) * 100
            : 0;
        TxtActiveSession.Text = $"{FormatTimeSpan(node.SessionDuration)} / {FormatTimeSpan(node.MaxSessionTime)}";
        PbActiveSession.Value = sessionPercent;

        // Quota
        TxtActiveQuota.Text = $"{FormatTimeSpan(node.RemainingQuota)} / {FormatTimeSpan(node.DailyQuota)}";
        PbActiveQuota.Value = 100 - node.QuotaPercentUsed;

        // GPU Stats
        TxtActiveGpuMem.Text = $"{node.GpuMemoryUsed:F1}/{node.GpuMemoryTotal:F0} GB";
        TxtActiveTemp.Text = $"{node.Temperature:F0}°C";
        TxtActiveUtilization.Text = $"{node.GpuUtilization:F0}%";

        // Provider icon
        ActiveProviderIcon.Kind = GetProviderIcon(node.Provider);
    }

    private void UpdateNextNodeUI(GpuNodeInfo node, DateTime? switchTime)
    {
        TxtNextNodeName.Text = node.Name;
        TxtNextNodeStatus.Text = node.StatusText;

        if (switchTime.HasValue)
        {
            var remaining = switchTime.Value - DateTime.UtcNow;
            TxtSwitchCountdown.Text = remaining > TimeSpan.Zero
                ? $"~{FormatTimeSpan(remaining)}"
                : "Now";
        }
        else
        {
            TxtSwitchCountdown.Text = "—";
        }
    }

    private void UpdateNodeList(List<GpuNodeInfo> nodes, string? activeNodeId)
    {
        // Filter
        var filtered = nodes.AsEnumerable();
        switch (_currentFilter)
        {
            case "Running":
                filtered = filtered.Where(n => n.Status == GpuNodeStatus.Running);
                break;
            case "Ready":
                filtered = filtered.Where(n => n.Status == GpuNodeStatus.Ready);
                break;
            case "Errors":
                filtered = filtered.Where(n => n.Status == GpuNodeStatus.Error ||
                                              n.Status == GpuNodeStatus.Disconnected);
                break;
            case "Stopped":
                filtered = filtered.Where(n => n.Status == GpuNodeStatus.Stopped);
                break;
        }

        // Sort
        filtered = _currentSort switch
        {
            "Name" => filtered.OrderBy(n => n.Name),
            "Quota Remaining" => filtered.OrderByDescending(n => n.RemainingQuota),
            "Provider" => filtered.OrderBy(n => n.Provider).ThenBy(n => n.Name),
            _ => filtered.OrderBy(n => n.Priority).ThenBy(n => n.Name)
        };

        // Convert to ViewModels
        _nodes = filtered.Select(n => new GpuNodeViewModel(n, n.Id == activeNodeId)).ToList();
        NodeListControl.ItemsSource = _nodes;
    }

    private void UpdateEventsList(List<GpuNodeEvent> events)
    {
        var viewModels = events.Select(e => new EventViewModel(e)).ToList();
        EventsListControl.ItemsSource = viewModels;
    }

    private void UpdateCountdown()
    {
        var stats = _monitorService.GetStats();
        if (stats.NextNode != null && stats.NextSwitchTime.HasValue)
        {
            var remaining = stats.NextSwitchTime.Value - DateTime.UtcNow;
            TxtSwitchCountdown.Text = remaining > TimeSpan.Zero
                ? $"~{FormatTimeSpan(remaining)}"
                : "Now";
        }

        // Update active node session time
        var activeNode = _monitorService.GetActiveNode();
        if (activeNode != null)
        {
            var sessionPercent = activeNode.MaxSessionTime.TotalSeconds > 0
                ? (activeNode.SessionDuration.TotalSeconds / activeNode.MaxSessionTime.TotalSeconds) * 100
                : 0;
            TxtActiveSession.Text = $"{FormatTimeSpan(activeNode.SessionDuration)} / {FormatTimeSpan(activeNode.MaxSessionTime)}";
            PbActiveSession.Value = sessionPercent;
        }
    }

    #endregion

    #region Event Handlers

    private void OnNodeStatusChanged(object? sender, GpuNodeEvent e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshUI();

            // Show notification for important events
            if (e.EventType == GpuNodeEventType.Error ||
                e.EventType == GpuNodeEventType.Disconnected ||
                e.EventType == GpuNodeEventType.QuotaExceeded)
            {
                ShowNotification(e.Message, NotificationType.Warning);
            }
        });
    }

    private void OnStatsUpdated(object? sender, GpuNodeStats e)
    {
        Dispatcher.Invoke(() =>
        {
            TxtRunningNodes.Text = e.RunningNodes.ToString();
            TxtReadyNodes.Text = e.ReadyNodes.ToString();
            TxtErrorNodes.Text = e.ErrorNodes.ToString();
            TxtGpuUsage.Text = $"{e.AverageGpuUtilization:F0}%";
        });
    }

    private void OnNodeSwitchRequired(object? sender, NodeSwitchEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            var message = $"Switching from {e.CurrentNode?.Name ?? "None"} to {e.NextNode?.Name}";
            ShowNotification(message, NotificationType.Info);

            if (e.NextNode != null)
            {
                _monitorService.SetActiveNode(e.NextNode.Id);
                RefreshUI();
            }
        });
    }

    private void OnEmergencyNodeActivated(object? sender, GpuNodeInfo e)
    {
        Dispatcher.Invoke(() =>
        {
            ShowNotification($"Emergency node activated: {e.Name}", NotificationType.Warning);
            RefreshUI();
        });
    }

    #endregion

    #region Button Handlers

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshUI();
    }

    private void BtnAddNode_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Add Node dialog will be implemented", "Add GPU Node",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Scheduling Settings dialog will be implemented", "Settings",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPauseActive_Click(object sender, RoutedEventArgs e)
    {
        var activeNode = _monitorService.GetActiveNode();
        if (activeNode != null)
        {
            _monitorService.UpdateNodeStatus(activeNode.Id, GpuNodeStatus.Paused, "Paused by user");
            RefreshUI();
        }
    }

    private void BtnSwitchNode_Click(object sender, RoutedEventArgs e)
    {
        var stats = _monitorService.GetStats();
        if (stats.NextNode != null)
        {
            var result = MessageBox.Show(
                $"Switch to {stats.NextNode.Name}?",
                "Switch Node",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _monitorService.SetActiveNode(stats.NextNode.Id);
                RefreshUI();
            }
        }
    }

    private void BtnPrestartNext_Click(object sender, RoutedEventArgs e)
    {
        var node = _monitorService.PrestartNextNode();
        if (node != null)
        {
            ShowNotification($"Prestarting {node.Name}...", NotificationType.Info);
        }
    }

    private void BtnStartNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string nodeId)
        {
            _monitorService.UpdateNodeStatus(nodeId, GpuNodeStatus.Starting, "Starting by user");
            RefreshUI();
        }
    }

    private void BtnPauseNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string nodeId)
        {
            _monitorService.UpdateNodeStatus(nodeId, GpuNodeStatus.Paused, "Paused by user");
            RefreshUI();
        }
    }

    private void BtnStopNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string nodeId)
        {
            _monitorService.UpdateNodeStatus(nodeId, GpuNodeStatus.Stopped, "Stopped by user");
            RefreshUI();
        }
    }

    private void BtnRebootNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string nodeId)
        {
            _monitorService.UpdateNodeStatus(nodeId, GpuNodeStatus.Rebooting, "Rebooting by user");
            RefreshUI();
        }
    }

    private void BtnSetActiveNode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string nodeId)
        {
            _monitorService.SetActiveNode(nodeId);
            RefreshUI();
        }
    }

    private void BtnNodeMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string nodeId)
        {
            // Show context menu
            var menu = new ContextMenu();

            var syncItem = new MenuItem { Header = "Sync Quota & Time" };
            syncItem.Click += (s, args) =>
            {
                MessageBox.Show("Quota sync will be implemented", "Sync",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            menu.Items.Add(syncItem);

            var resetQuotaItem = new MenuItem { Header = "Reset Daily Quota" };
            resetQuotaItem.Click += (s, args) =>
            {
                _monitorService.ResetDailyQuota(nodeId);
                RefreshUI();
            };
            menu.Items.Add(resetQuotaItem);

            menu.Items.Add(new Separator());

            var scheduleItem = new MenuItem { Header = "Schedule Start/Stop" };
            scheduleItem.Click += (s, args) =>
            {
                MessageBox.Show("Scheduling dialog will be implemented", "Schedule",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };
            menu.Items.Add(scheduleItem);

            menu.Items.Add(new Separator());

            var deleteItem = new MenuItem { Header = "Delete Node", Foreground = Brushes.Red };
            deleteItem.Click += (s, args) =>
            {
                var result = MessageBox.Show("Delete this node?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _monitorService.RemoveNode(nodeId);
                    RefreshUI();
                }
            };
            menu.Items.Add(deleteItem);

            menu.IsOpen = true;
        }
    }

    private void NodeCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is GpuNodeViewModel vm)
        {
            // Show node details
            var node = _monitorService.GetAllNodes().FirstOrDefault(n => n.Id == vm.Id);
            if (node != null)
            {
                var details = $"Name: {node.DisplayName}\n" +
                             $"Provider: {node.Provider}\n" +
                             $"GPU: {node.GpuType}\n" +
                             $"Status: {node.StatusText}\n" +
                             $"Session: {FormatTimeSpan(node.SessionDuration)}\n" +
                             $"Quota Used: {FormatTimeSpan(node.UsedQuota)} / {FormatTimeSpan(node.DailyQuota)}\n" +
                             $"GPU Memory: {node.GpuMemoryUsed:F1}/{node.GpuMemoryTotal:F0} GB\n" +
                             $"Utilization: {node.GpuUtilization:F0}%\n" +
                             $"Tasks Completed: {node.CompletedTasks}\n" +
                             $"Last Error: {node.LastError ?? "None"}";

                MessageBox.Show(details, $"Node: {node.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }

    private void CboFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboFilter.SelectedItem is ComboBoxItem item)
        {
            _currentFilter = item.Content?.ToString() ?? "All Nodes";
            RefreshUI();
        }
    }

    private void CboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboSort.SelectedItem is ComboBoxItem item)
        {
            _currentSort = item.Content?.ToString() ?? "Priority";
            RefreshUI();
        }
    }

    #endregion

    #region Helpers

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours:D2}:{ts.Minutes:D2}";
        return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private static PackIconKind GetProviderIcon(GpuProviderType provider)
    {
        return provider switch
        {
            GpuProviderType.GoogleColab => PackIconKind.Google,
            GpuProviderType.Kaggle => PackIconKind.AlphaKCircle,
            GpuProviderType.PaperSpace => PackIconKind.Cloud,
            GpuProviderType.LightningAI => PackIconKind.LightningBolt,
            GpuProviderType.HuggingFace => PackIconKind.Robot,
            GpuProviderType.SaturnCloud => PackIconKind.Planet,
            GpuProviderType.RunPod => PackIconKind.Server,
            GpuProviderType.Vast => PackIconKind.ServerNetwork,
            GpuProviderType.Lambda => PackIconKind.Lambda,
            _ => PackIconKind.Chip
        };
    }

    private void ShowNotification(string message, NotificationType type)
    {
        // Simple notification - could be enhanced with snackbar
        TxtSubtitle.Text = message;
        TxtSubtitle.Foreground = type switch
        {
            NotificationType.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
            NotificationType.Error => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            NotificationType.Success => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
            _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
        };

        // Reset after 5 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (s, e) =>
        {
            TxtSubtitle.Text = "จัดการและตรวจสอบ GPU Nodes ทั้งหมด";
            TxtSubtitle.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
            timer.Stop();
        };
        timer.Start();
    }

    private enum NotificationType { Info, Success, Warning, Error }

    #endregion
}

#region ViewModels

public class GpuNodeViewModel
{
    private readonly GpuNodeInfo _node;

    public GpuNodeViewModel(GpuNodeInfo node, bool isActive)
    {
        _node = node;
        IsActive = isActive;
    }

    public string Id => _node.Id;
    public string Name => _node.Name;
    public string ProviderName => _node.Provider.ToString();
    public string GpuTypeName => $"NVIDIA {_node.GpuType}";
    public string AccountEmail => _node.AccountEmail ?? "";
    public string StatusText => _node.StatusText;
    public bool IsActive { get; }

    public Color StatusColor => (Color)ColorConverter.ConvertFromString(_node.StatusColorHex);
    public Brush StatusBrush => new SolidColorBrush(StatusColor);

    public PackIconKind ProviderIcon => _node.Provider switch
    {
        GpuProviderType.GoogleColab => PackIconKind.Google,
        GpuProviderType.Kaggle => PackIconKind.AlphaKCircle,
        GpuProviderType.PaperSpace => PackIconKind.Cloud,
        GpuProviderType.LightningAI => PackIconKind.LightningBolt,
        GpuProviderType.HuggingFace => PackIconKind.Robot,
        GpuProviderType.SaturnCloud => PackIconKind.Planet,
        _ => PackIconKind.Chip
    };

    public bool IsBlinking => _node.Status == GpuNodeStatus.Ready ||
                              _node.Status == GpuNodeStatus.Rebooting ||
                              _node.Status == GpuNodeStatus.Disconnected;

    public string QuotaDisplay => $"{FormatTimeSpan(_node.RemainingQuota)} / {FormatTimeSpan(_node.DailyQuota)}";
    public double QuotaPercent => 100 - _node.QuotaPercentUsed;
    public string SessionDisplay => _node.SessionStartTime.HasValue
        ? $"Session: {FormatTimeSpan(_node.SessionDuration)}"
        : "No active session";

    public string GpuMemDisplay => $"{_node.GpuMemoryUsed:F1}/{_node.GpuMemoryTotal:F0} GB";
    public string GpuUtilDisplay => $"{_node.GpuUtilization:F0}% util";

    public Visibility GpuStatsVisibility => _node.Status == GpuNodeStatus.Running
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EmergencyVisibility => _node.IsEmergencyNode
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StartVisibility => _node.Status == GpuNodeStatus.Stopped ||
                                         _node.Status == GpuNodeStatus.Ready ||
                                         _node.Status == GpuNodeStatus.Emergency
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PauseVisibility => _node.Status == GpuNodeStatus.Running
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StopVisibility => _node.Status == GpuNodeStatus.Running ||
                                        _node.Status == GpuNodeStatus.Paused
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RebootVisibility => _node.Status == GpuNodeStatus.Error ||
                                          _node.Status == GpuNodeStatus.Disconnected
        ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SetActiveVisibility => !IsActive &&
                                             (_node.Status == GpuNodeStatus.Running ||
                                              _node.Status == GpuNodeStatus.Ready)
        ? Visibility.Visible : Visibility.Collapsed;

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalDays >= 1)
            return $"{(int)ts.TotalDays}d {ts.Hours:D2}:{ts.Minutes:D2}";
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}

public class EventViewModel
{
    private readonly GpuNodeEvent _event;

    public EventViewModel(GpuNodeEvent evt)
    {
        _event = evt;
    }

    public string NodeName => _event.NodeName;
    public string Message => _event.Message;

    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - _event.Timestamp;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }
    }

    public PackIconKind EventIcon => _event.EventType switch
    {
        GpuNodeEventType.StatusChanged => PackIconKind.SwapHorizontal,
        GpuNodeEventType.Connected => PackIconKind.LanConnect,
        GpuNodeEventType.Disconnected => PackIconKind.LanDisconnect,
        GpuNodeEventType.TaskStarted => PackIconKind.Play,
        GpuNodeEventType.TaskCompleted => PackIconKind.Check,
        GpuNodeEventType.TaskFailed => PackIconKind.Close,
        GpuNodeEventType.QuotaWarning => PackIconKind.Alert,
        GpuNodeEventType.QuotaExceeded => PackIconKind.TimerOff,
        GpuNodeEventType.QuotaReset => PackIconKind.Refresh,
        GpuNodeEventType.SessionStarted => PackIconKind.Login,
        GpuNodeEventType.SessionEnded => PackIconKind.Logout,
        GpuNodeEventType.Rebooting => PackIconKind.Restart,
        GpuNodeEventType.Error => PackIconKind.AlertCircle,
        GpuNodeEventType.EmergencyActivated => PackIconKind.LightningBolt,
        GpuNodeEventType.PrestartTriggered => PackIconKind.Rocket,
        _ => PackIconKind.Information
    };

    public Brush EventColor => _event.EventType switch
    {
        GpuNodeEventType.Error or GpuNodeEventType.Disconnected or GpuNodeEventType.TaskFailed =>
            new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        GpuNodeEventType.QuotaWarning or GpuNodeEventType.QuotaExceeded =>
            new SolidColorBrush(Color.FromRgb(245, 158, 11)),
        GpuNodeEventType.TaskCompleted or GpuNodeEventType.Connected =>
            new SolidColorBrush(Color.FromRgb(16, 185, 129)),
        GpuNodeEventType.EmergencyActivated =>
            new SolidColorBrush(Color.FromRgb(139, 92, 246)),
        _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
    };
}

#endregion
