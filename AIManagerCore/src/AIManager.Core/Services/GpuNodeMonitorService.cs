using System.Collections.Concurrent;
using System.Timers;
using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Service สำหรับจัดการและ Monitor GPU Nodes อัจฉริยะ
/// </summary>
public class GpuNodeMonitorService : IDisposable
{
    private readonly ConcurrentDictionary<string, GpuNodeInfo> _nodes = new();
    private readonly List<GpuNodeEvent> _events = new();
    private readonly object _eventsLock = new();
    private readonly System.Timers.Timer _monitorTimer;
    private readonly System.Timers.Timer _schedulerTimer;
    private GpuSchedulingConfig _config = new();
    private string? _activeNodeId;
    private int _roundRobinIndex = 0;
    private bool _disposed;

    /// <summary>Event เมื่อสถานะ Node เปลี่ยน</summary>
    public event EventHandler<GpuNodeEvent>? NodeStatusChanged;

    /// <summary>Event เมื่อสถิติอัพเดท</summary>
    public event EventHandler<GpuNodeStats>? StatsUpdated;

    /// <summary>Event เมื่อต้องเปลี่ยน Node</summary>
    public event EventHandler<NodeSwitchEventArgs>? NodeSwitchRequired;

    /// <summary>Event เมื่อ Node ถูก Prestart</summary>
    public event EventHandler<GpuNodeInfo>? NodePrestarted;

    /// <summary>Event เมื่อ Emergency Node ถูกเรียกใช้</summary>
    public event EventHandler<GpuNodeInfo>? EmergencyNodeActivated;

    public GpuNodeMonitorService()
    {
        // Monitor timer - check nodes every 5 seconds
        _monitorTimer = new System.Timers.Timer(5000);
        _monitorTimer.Elapsed += MonitorTimer_Elapsed;

        // Scheduler timer - check scheduling every 30 seconds
        _schedulerTimer = new System.Timers.Timer(30000);
        _schedulerTimer.Elapsed += SchedulerTimer_Elapsed;
    }

    #region Public API

    /// <summary>เริ่มการ Monitor</summary>
    public void Start()
    {
        _monitorTimer.Start();
        _schedulerTimer.Start();
        this.LogInfo("GPU Node Monitor started");
    }

    /// <summary>หยุดการ Monitor</summary>
    public void Stop()
    {
        _monitorTimer.Stop();
        _schedulerTimer.Stop();
        this.LogInfo("GPU Node Monitor stopped");
    }

    /// <summary>เพิ่ม Node ใหม่</summary>
    public void AddNode(GpuNodeInfo node)
    {
        if (_nodes.TryAdd(node.Id, node))
        {
            AddEvent(node.Id, node.Name, GpuNodeEventType.StatusChanged,
                null, node.Status, $"Node added: {node.Name}");
            this.LogInfo($"Node added: {node.Name} ({node.Provider})");
        }
    }

    /// <summary>ลบ Node</summary>
    public bool RemoveNode(string nodeId)
    {
        if (_nodes.TryRemove(nodeId, out var node))
        {
            AddEvent(nodeId, node.Name, GpuNodeEventType.StatusChanged,
                node.Status, null, $"Node removed: {node.Name}");
            return true;
        }
        return false;
    }

    /// <summary>อัพเดทสถานะ Node</summary>
    public void UpdateNodeStatus(string nodeId, GpuNodeStatus newStatus, string? message = null)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            var oldStatus = node.Status;
            node.Status = newStatus;
            node.UpdatedAt = DateTime.UtcNow;

            AddEvent(nodeId, node.Name, GpuNodeEventType.StatusChanged,
                oldStatus, newStatus, message ?? $"Status changed: {oldStatus} -> {newStatus}");

            NodeStatusChanged?.Invoke(this, new GpuNodeEvent
            {
                NodeId = nodeId,
                NodeName = node.Name,
                EventType = GpuNodeEventType.StatusChanged,
                OldStatus = oldStatus,
                NewStatus = newStatus,
                Message = message ?? ""
            });

            // Check if we need to switch nodes
            if (_config.AutoSwitchOnError && newStatus == GpuNodeStatus.Error && nodeId == _activeNodeId)
            {
                TriggerNodeSwitch("Node error detected");
            }
        }
    }

    /// <summary>อัพเดทข้อมูล Node</summary>
    public void UpdateNodeInfo(string nodeId, Action<GpuNodeInfo> updateAction)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            updateAction(node);
            node.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>ตั้งค่า Active Node</summary>
    public void SetActiveNode(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            _activeNodeId = nodeId;
            node.Status = GpuNodeStatus.Running;
            node.SessionStartTime = DateTime.UtcNow;
            AddEvent(nodeId, node.Name, GpuNodeEventType.SessionStarted,
                null, GpuNodeStatus.Running, "Session started");
        }
    }

    /// <summary>ดึงข้อมูล Node ทั้งหมด</summary>
    public List<GpuNodeInfo> GetAllNodes()
    {
        return _nodes.Values.OrderBy(n => n.Priority).ThenBy(n => n.Name).ToList();
    }

    /// <summary>ดึงข้อมูล Node ที่กำลังทำงาน</summary>
    public GpuNodeInfo? GetActiveNode()
    {
        if (_activeNodeId != null && _nodes.TryGetValue(_activeNodeId, out var node))
        {
            return node;
        }
        return null;
    }

    /// <summary>ดึงสถิติรวม</summary>
    public GpuNodeStats GetStats()
    {
        var nodes = _nodes.Values.ToList();
        var activeNode = GetActiveNode();
        var nextNode = SelectNextNode();

        return new GpuNodeStats
        {
            TotalNodes = nodes.Count,
            RunningNodes = nodes.Count(n => n.Status == GpuNodeStatus.Running),
            ReadyNodes = nodes.Count(n => n.Status == GpuNodeStatus.Ready),
            ErrorNodes = nodes.Count(n => n.Status == GpuNodeStatus.Error || n.Status == GpuNodeStatus.Disconnected),
            EmergencyNodes = nodes.Count(n => n.IsEmergencyNode),
            TotalQuotaRemaining = TimeSpan.FromSeconds(nodes.Sum(n => n.RemainingQuota.TotalSeconds)),
            TotalQuotaUsed = TimeSpan.FromSeconds(nodes.Sum(n => n.UsedQuota.TotalSeconds)),
            AverageGpuUtilization = nodes.Count > 0 ? nodes.Average(n => n.GpuUtilization) : 0,
            TotalTasksCompleted = nodes.Sum(n => n.CompletedTasks),
            TotalTasksFailed = nodes.Sum(n => n.FailedTasks),
            ActiveNode = activeNode,
            NextNode = nextNode,
            NextSwitchTime = CalculateNextSwitchTime(activeNode)
        };
    }

    /// <summary>ดึง Events ล่าสุด</summary>
    public List<GpuNodeEvent> GetRecentEvents(int count = 50)
    {
        lock (_eventsLock)
        {
            return _events.OrderByDescending(e => e.Timestamp).Take(count).ToList();
        }
    }

    /// <summary>ตั้งค่า Scheduling</summary>
    public void SetConfig(GpuSchedulingConfig config)
    {
        _config = config;
    }

    /// <summary>เรียกใช้ Emergency Node</summary>
    public GpuNodeInfo? ActivateEmergencyNode()
    {
        var emergencyNode = _nodes.Values
            .Where(n => n.IsEmergencyNode && n.CanQuickStart)
            .Where(n => n.Status == GpuNodeStatus.Ready || n.Status == GpuNodeStatus.Stopped)
            .OrderBy(n => n.EstimatedStartTime)
            .FirstOrDefault();

        if (emergencyNode != null)
        {
            UpdateNodeStatus(emergencyNode.Id, GpuNodeStatus.Starting, "Emergency activation");
            AddEvent(emergencyNode.Id, emergencyNode.Name, GpuNodeEventType.EmergencyActivated,
                null, GpuNodeStatus.Starting, "Emergency node activated");
            EmergencyNodeActivated?.Invoke(this, emergencyNode);
            return emergencyNode;
        }

        return null;
    }

    /// <summary>Prestart Node ถัดไป</summary>
    public GpuNodeInfo? PrestartNextNode()
    {
        var nextNode = SelectNextNode();
        if (nextNode != null && nextNode.Status == GpuNodeStatus.Stopped)
        {
            UpdateNodeStatus(nextNode.Id, GpuNodeStatus.Warming, "Prestart initiated");
            AddEvent(nextNode.Id, nextNode.Name, GpuNodeEventType.PrestartTriggered,
                null, GpuNodeStatus.Warming, "Node prestart triggered");
            NodePrestarted?.Invoke(this, nextNode);
            return nextNode;
        }
        return null;
    }

    /// <summary>Sync โควต้าและเวลา</summary>
    public void SyncNodeQuota(string nodeId, TimeSpan usedQuota, TimeSpan? sessionTime = null)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.UsedQuota = usedQuota;
            if (sessionTime.HasValue && node.SessionStartTime.HasValue)
            {
                // Adjust session start time based on actual session duration
                node.SessionStartTime = DateTime.UtcNow - sessionTime.Value;
            }
            node.UpdatedAt = DateTime.UtcNow;

            // Check quota warning
            if (node.RemainingQuota.TotalSeconds < node.DailyQuota.TotalSeconds * _config.QuotaWarningThreshold)
            {
                AddEvent(nodeId, node.Name, GpuNodeEventType.QuotaWarning,
                    null, null, $"Quota warning: {node.RemainingQuota:hh\\:mm} remaining");
            }

            // Check quota exceeded
            if (node.RemainingQuota <= TimeSpan.Zero)
            {
                UpdateNodeStatus(nodeId, GpuNodeStatus.QuotaExceeded, "Quota exceeded");
                if (_config.AutoSwitchOnQuotaLow && nodeId == _activeNodeId)
                {
                    TriggerNodeSwitch("Quota exceeded");
                }
            }
        }
    }

    /// <summary>Reset โควต้าประจำวัน</summary>
    public void ResetDailyQuota(string nodeId)
    {
        if (_nodes.TryGetValue(nodeId, out var node))
        {
            node.UsedQuota = TimeSpan.Zero;
            node.QuotaResetTime = DateTime.UtcNow.Date.AddDays(1);
            AddEvent(nodeId, node.Name, GpuNodeEventType.QuotaReset,
                null, null, "Daily quota reset");
        }
    }

    /// <summary>สร้าง Nodes ตัวอย่างสำหรับ Demo</summary>
    public void CreateDemoNodes()
    {
        var demoNodes = new List<GpuNodeInfo>
        {
            new GpuNodeInfo
            {
                Id = "colab-1",
                Name = "Colab Pro #1",
                DisplayName = "Google Colab Pro - Account 1",
                Provider = GpuProviderType.GoogleColab,
                GpuType = GpuType.T4,
                Status = GpuNodeStatus.Running,
                Priority = NodePriority.Normal,
                DailyQuota = TimeSpan.FromHours(12),
                UsedQuota = TimeSpan.FromHours(3.5),
                MaxSessionTime = TimeSpan.FromHours(12),
                GpuMemoryTotal = 16,
                GpuMemoryUsed = 8.5,
                GpuUtilization = 75,
                Temperature = 68,
                CurrentTasks = 2,
                CompletedTasks = 15,
                AccountEmail = "user1@gmail.com",
                SessionStartTime = DateTime.UtcNow.AddHours(-3.5),
                IsConnected = true,
                AutoPrestart = true
            },
            new GpuNodeInfo
            {
                Id = "colab-2",
                Name = "Colab Free #2",
                DisplayName = "Google Colab Free - Account 2",
                Provider = GpuProviderType.GoogleColab,
                GpuType = GpuType.T4,
                Status = GpuNodeStatus.Ready,
                Priority = NodePriority.Normal,
                DailyQuota = TimeSpan.FromHours(4),
                UsedQuota = TimeSpan.Zero,
                MaxSessionTime = TimeSpan.FromHours(4),
                GpuMemoryTotal = 16,
                AccountEmail = "user2@gmail.com",
                AutoPrestart = true
            },
            new GpuNodeInfo
            {
                Id = "kaggle-1",
                Name = "Kaggle #1",
                DisplayName = "Kaggle Notebook - Account 1",
                Provider = GpuProviderType.Kaggle,
                GpuType = GpuType.P100,
                Status = GpuNodeStatus.Queued,
                Priority = NodePriority.Normal,
                DailyQuota = TimeSpan.FromHours(30),
                UsedQuota = TimeSpan.FromHours(5),
                MaxSessionTime = TimeSpan.FromHours(9),
                GpuMemoryTotal = 16,
                AccountEmail = "kaggle1@gmail.com",
                AutoPrestart = true
            },
            new GpuNodeInfo
            {
                Id = "lightning-1",
                Name = "Lightning Emergency",
                DisplayName = "Lightning.AI - Emergency Node",
                Provider = GpuProviderType.LightningAI,
                GpuType = GpuType.A10G,
                Status = GpuNodeStatus.Emergency,
                Priority = NodePriority.Emergency,
                DailyQuota = TimeSpan.FromHours(24),
                UsedQuota = TimeSpan.Zero,
                MaxSessionTime = TimeSpan.FromHours(4),
                GpuMemoryTotal = 24,
                AccountEmail = "lightning@email.com",
                IsEmergencyNode = true,
                CanQuickStart = true,
                EstimatedStartTime = TimeSpan.FromSeconds(30)
            },
            new GpuNodeInfo
            {
                Id = "huggingface-1",
                Name = "HuggingFace Space",
                DisplayName = "HuggingFace Space - GPU",
                Provider = GpuProviderType.HuggingFace,
                GpuType = GpuType.T4,
                Status = GpuNodeStatus.Warming,
                Priority = NodePriority.High,
                DailyQuota = TimeSpan.FromHours(8),
                UsedQuota = TimeSpan.FromHours(1),
                MaxSessionTime = TimeSpan.FromHours(8),
                GpuMemoryTotal = 16,
                GpuMemoryUsed = 2,
                GpuUtilization = 15,
                AccountEmail = "hf@email.com",
                AutoPrestart = true
            },
            new GpuNodeInfo
            {
                Id = "paperspace-1",
                Name = "Paperspace GPU",
                DisplayName = "Paperspace Gradient - A100",
                Provider = GpuProviderType.PaperSpace,
                GpuType = GpuType.A100,
                Status = GpuNodeStatus.Stopped,
                Priority = NodePriority.Low,
                DailyQuota = TimeSpan.FromHours(100),
                UsedQuota = TimeSpan.Zero,
                MaxSessionTime = TimeSpan.FromHours(24),
                GpuMemoryTotal = 40,
                AccountEmail = "paperspace@email.com"
            },
            new GpuNodeInfo
            {
                Id = "colab-3",
                Name = "Colab #3 (Error)",
                DisplayName = "Google Colab - Account 3",
                Provider = GpuProviderType.GoogleColab,
                GpuType = GpuType.T4,
                Status = GpuNodeStatus.Error,
                Priority = NodePriority.Normal,
                DailyQuota = TimeSpan.FromHours(12),
                UsedQuota = TimeSpan.FromHours(2),
                MaxSessionTime = TimeSpan.FromHours(12),
                GpuMemoryTotal = 16,
                AccountEmail = "user3@gmail.com",
                LastError = "Runtime disconnected unexpectedly",
                ErrorCount = 3
            },
            new GpuNodeInfo
            {
                Id = "saturn-1",
                Name = "Saturn Cloud",
                DisplayName = "Saturn Cloud - GPU Instance",
                Provider = GpuProviderType.SaturnCloud,
                GpuType = GpuType.T4,
                Status = GpuNodeStatus.Rebooting,
                Priority = NodePriority.Normal,
                DailyQuota = TimeSpan.FromHours(10),
                UsedQuota = TimeSpan.FromHours(4),
                MaxSessionTime = TimeSpan.FromHours(10),
                GpuMemoryTotal = 16,
                AccountEmail = "saturn@email.com",
                RebootCount = 2
            }
        };

        foreach (var node in demoNodes)
        {
            AddNode(node);
        }

        // Set first running node as active
        _activeNodeId = "colab-1";
    }

    #endregion

    #region Private Methods

    private void MonitorTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // Update session times
            foreach (var node in _nodes.Values.Where(n => n.Status == GpuNodeStatus.Running))
            {
                node.UsedQuota += TimeSpan.FromSeconds(5);

                // Check session time warning
                if (node.RemainingSessionTime < _config.SessionWarningTime &&
                    node.RemainingSessionTime > _config.SessionWarningTime - TimeSpan.FromSeconds(10))
                {
                    AddEvent(node.Id, node.Name, GpuNodeEventType.QuotaWarning,
                        null, null, $"Session ending soon: {node.RemainingSessionTime:mm\\:ss} remaining");
                }

                // Check if prestart is needed
                if (_config.EnableAutoPrestart &&
                    node.RemainingSessionTime <= node.PrestartBefore &&
                    node.Id == _activeNodeId)
                {
                    PrestartNextNode();
                }
            }

            // Update stats
            StatsUpdated?.Invoke(this, GetStats());
        }
        catch (Exception ex)
        {
            this.LogError($"Monitor error: {ex.Message}", ex);
        }
    }

    private void SchedulerTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_config.EnableSmartScheduling) return;

        try
        {
            // Check scheduled starts
            var now = DateTime.UtcNow;
            foreach (var node in _nodes.Values.Where(n =>
                n.ScheduledStartTime.HasValue &&
                n.ScheduledStartTime.Value <= now &&
                n.Status == GpuNodeStatus.Stopped))
            {
                UpdateNodeStatus(node.Id, GpuNodeStatus.Starting, "Scheduled start");
            }

            // Check scheduled stops
            foreach (var node in _nodes.Values.Where(n =>
                n.ScheduledStopTime.HasValue &&
                n.ScheduledStopTime.Value <= now &&
                n.Status == GpuNodeStatus.Running))
            {
                UpdateNodeStatus(node.Id, GpuNodeStatus.Stopped, "Scheduled stop");
            }

            // Auto-switch on low quota
            var activeNode = GetActiveNode();
            if (_config.AutoSwitchOnQuotaLow && activeNode != null)
            {
                if (activeNode.RemainingQuota < activeNode.DailyQuota * _config.QuotaWarningThreshold ||
                    activeNode.RemainingSessionTime < _config.SessionWarningTime)
                {
                    TriggerNodeSwitch("Low quota/session time");
                }
            }
        }
        catch (Exception ex)
        {
            this.LogError($"Scheduler error: {ex.Message}", ex);
        }
    }

    private GpuNodeInfo? SelectNextNode()
    {
        var availableNodes = _nodes.Values
            .Where(n => n.Id != _activeNodeId)
            .Where(n => n.Status == GpuNodeStatus.Ready ||
                       n.Status == GpuNodeStatus.Stopped ||
                       n.Status == GpuNodeStatus.Warming ||
                       n.Status == GpuNodeStatus.Emergency)
            .Where(n => n.RemainingQuota > TimeSpan.FromMinutes(30))
            .ToList();

        if (!availableNodes.Any()) return null;

        return _config.SelectionStrategy switch
        {
            NodeSelectionStrategy.MaxQuota =>
                availableNodes.OrderByDescending(n => n.RemainingQuota).First(),

            NodeSelectionStrategy.FastestStart =>
                availableNodes.OrderBy(n => n.Status == GpuNodeStatus.Ready ? 0 : 1)
                             .ThenBy(n => n.EstimatedStartTime).First(),

            NodeSelectionStrategy.SmartBalance =>
                availableNodes.OrderBy(n => n.Priority)
                             .ThenBy(n => n.Status == GpuNodeStatus.Ready ? 0 : 1)
                             .ThenByDescending(n => n.RemainingQuota)
                             .First(),

            NodeSelectionStrategy.RoundRobin =>
                availableNodes[_roundRobinIndex++ % availableNodes.Count],

            _ => availableNodes.First()
        };
    }

    private DateTime? CalculateNextSwitchTime(GpuNodeInfo? activeNode)
    {
        if (activeNode == null) return null;

        var quotaEnd = activeNode.QuotaResetTime ?? DateTime.UtcNow.Date.AddDays(1);
        var sessionEnd = activeNode.SessionStartTime?.Add(activeNode.MaxSessionTime) ?? DateTime.MaxValue;

        var earlierEnd = quotaEnd < sessionEnd ? quotaEnd : sessionEnd;

        // Subtract prestart time
        return earlierEnd - activeNode.PrestartBefore;
    }

    private void TriggerNodeSwitch(string reason)
    {
        var currentNode = GetActiveNode();
        var nextNode = SelectNextNode();

        if (nextNode != null)
        {
            NodeSwitchRequired?.Invoke(this, new NodeSwitchEventArgs
            {
                CurrentNode = currentNode,
                NextNode = nextNode,
                Reason = reason
            });

            this.LogInfo($"Node switch triggered: {currentNode?.Name} -> {nextNode.Name} ({reason})");
        }
        else if (_config.EnableEmergencyNodes)
        {
            // Try emergency node
            var emergency = ActivateEmergencyNode();
            if (emergency != null)
            {
                NodeSwitchRequired?.Invoke(this, new NodeSwitchEventArgs
                {
                    CurrentNode = currentNode,
                    NextNode = emergency,
                    Reason = $"{reason} - Emergency node activated"
                });
            }
        }
    }

    private void AddEvent(string nodeId, string nodeName, GpuNodeEventType eventType,
        GpuNodeStatus? oldStatus, GpuNodeStatus? newStatus, string message)
    {
        var evt = new GpuNodeEvent
        {
            NodeId = nodeId,
            NodeName = nodeName,
            EventType = eventType,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Message = message
        };

        lock (_eventsLock)
        {
            _events.Add(evt);
            // Keep only last 500 events
            while (_events.Count > 500)
            {
                _events.RemoveAt(0);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _monitorTimer.Stop();
        _monitorTimer.Dispose();
        _schedulerTimer.Stop();
        _schedulerTimer.Dispose();
    }

    #endregion
}

/// <summary>
/// Event args สำหรับการเปลี่ยน Node
/// </summary>
public class NodeSwitchEventArgs : EventArgs
{
    public GpuNodeInfo? CurrentNode { get; set; }
    public GpuNodeInfo? NextNode { get; set; }
    public string Reason { get; set; } = string.Empty;
}
