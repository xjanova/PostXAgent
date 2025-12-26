using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// Dashboard ViewModel - Main overview screen with sync status
/// </summary>
public partial class DashboardViewModel : BaseViewModel
{
    private readonly IAIManagerApiService _apiService;
    private readonly ISignalRService _signalRService;
    private readonly IPaymentDetectionService _paymentService;
    private readonly ISettingsService _settingsService;
    private readonly IConnectionSyncService _syncService;

    [ObservableProperty]
    private SystemStatus? _systemStatus;

    [ObservableProperty]
    private DashboardStats? _stats;

    [ObservableProperty]
    private int _pendingTasks;

    [ObservableProperty]
    private int _runningTasks;

    [ObservableProperty]
    private int _completedToday;

    [ObservableProperty]
    private int _activeWorkers;

    [ObservableProperty]
    private int _totalWorkers;

    [ObservableProperty]
    private int _pendingPayments;

    [ObservableProperty]
    private decimal _totalPaymentsToday;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _memoryUsage;

    [ObservableProperty]
    private string _connectionStatus = "ไม่ได้เชื่อมต่อ";

    [ObservableProperty]
    private Color _connectionColor = Color.FromArgb("#F44336");

    [ObservableProperty]
    private bool _isApiKeyValid;

    [ObservableProperty]
    private string? _serverVersion;

    [ObservableProperty]
    private ConnectionStatus? _fullConnectionStatus;

    [ObservableProperty]
    private string _syncStatusText = "Offline";

    [ObservableProperty]
    private Color _syncStatusColor = Color.FromArgb("#9E9E9E");

    [ObservableProperty]
    private string _syncIcon = "sync_disabled";

    [ObservableProperty]
    private bool _isSyncing;

    [ObservableProperty]
    private DateTime? _lastSyncTime;

    public ObservableCollection<TaskItem> RecentTasks { get; } = new();
    public ObservableCollection<PaymentInfo> RecentPayments { get; } = new();

    public DashboardViewModel(
        IAIManagerApiService apiService,
        ISignalRService signalRService,
        IPaymentDetectionService paymentService,
        ISettingsService settingsService,
        IConnectionSyncService syncService)
    {
        _apiService = apiService;
        _signalRService = signalRService;
        _paymentService = paymentService;
        _settingsService = settingsService;
        _syncService = syncService;

        Title = "แดชบอร์ด";

        // Subscribe to real-time updates
        _signalRService.ConnectionStateChanged += OnConnectionStateChanged;
        _signalRService.SystemStatusUpdated += OnSystemStatusUpdated;
        _signalRService.TaskUpdated += OnTaskUpdated;
        _signalRService.PaymentReceived += OnPaymentReceived;

        // Subscribe to sync status
        _syncService.SyncStatusChanged += OnSyncStatusChanged;

        // Subscribe to connection status changes from API service
        _apiService.ConnectionStatusChanged += OnApiConnectionStatusChanged;
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        await ExecuteAsync(async () =>
        {
            // Initialize connection
            var settings = _settingsService.GetConnectionSettings();
            _apiService.SetConnectionSettings(settings);
            _signalRService.SetConnectionSettings(settings);

            // Test connection with full status check (including API Key)
            var connectionStatus = await _apiService.GetConnectionStatusAsync();
            UpdateConnectionStatusFromModel(connectionStatus);

            if (connectionStatus.IsConnected)
            {
                // Register device and start sync
                await _syncService.RegisterDeviceAsync();
                await _syncService.StartHeartbeatAsync();

                // Connect SignalR
                await _signalRService.ConnectAsync();

                // Load system status
                var statusResponse = await _apiService.GetSystemStatusAsync();
                if (statusResponse.Success && statusResponse.Data != null)
                {
                    UpdateFromSystemStatus(statusResponse.Data);
                }

                // Load dashboard stats
                var statsResponse = await _apiService.GetDashboardStatsAsync();
                if (statsResponse.Success && statsResponse.Data != null)
                {
                    Stats = statsResponse.Data;
                    PendingPayments = Stats.PendingPayments;
                    TotalPaymentsToday = Stats.TotalPaymentsToday;
                }

                // Load recent tasks
                var tasksResponse = await _apiService.GetTasksAsync(1, 5);
                if (tasksResponse.Success && tasksResponse.Data != null)
                {
                    RecentTasks.Clear();
                    foreach (var task in tasksResponse.Data)
                    {
                        RecentTasks.Add(task);
                    }
                }

                // Load recent payments
                var payments = await _paymentService.GetAllPaymentsAsync();
                RecentPayments.Clear();
                foreach (var payment in payments.Take(5))
                {
                    RecentPayments.Add(payment);
                }

                LastSyncTime = DateTime.Now;
            }
        }, "ไม่สามารถโหลดข้อมูลได้");
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        await ExecuteAsync(async () =>
        {
            var settings = _settingsService.GetConnectionSettings();
            _apiService.SetConnectionSettings(settings);
            _signalRService.SetConnectionSettings(settings);

            var connectionStatus = await _apiService.GetConnectionStatusAsync();
            UpdateConnectionStatusFromModel(connectionStatus);

            if (connectionStatus.IsConnected)
            {
                await _syncService.RegisterDeviceAsync();
                await _syncService.StartHeartbeatAsync();
                await _signalRService.ConnectAsync();
                await LoadDataAsync();
            }
            else
            {
                var errorMsg = connectionStatus.LastError ?? "ไม่สามารถเชื่อมต่อได้";
                if (!connectionStatus.IsApiConnected)
                {
                    errorMsg = "ไม่สามารถเชื่อมต่อกับ AI Manager Core ได้\nกรุณาตรวจสอบ URL และ Port";
                }
                else if (!connectionStatus.IsApiKeyValid)
                {
                    errorMsg = "API Key ไม่ถูกต้อง\nกรุณาตรวจสอบ API Key ในหน้าตั้งค่า";
                }
                ShowError(errorMsg);
            }
        });
    }

    [RelayCommand]
    private async Task ForceSyncAsync()
    {
        IsSyncing = true;
        var success = await _syncService.ForceSyncAsync();
        IsSyncing = false;

        if (success)
        {
            LastSyncTime = DateTime.Now;
            await LoadDataAsync();
        }
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _syncService.DisconnectAsync();
        await _signalRService.DisconnectAsync();
        UpdateConnectionStatus(false);
    }

    private void UpdateConnectionStatus(bool connected)
    {
        IsConnected = connected;
        if (connected)
        {
            ConnectionStatus = "เชื่อมต่อแล้ว";
            ConnectionColor = Color.FromArgb("#4CAF50");
        }
        else
        {
            ConnectionStatus = "ไม่ได้เชื่อมต่อ";
            ConnectionColor = Color.FromArgb("#F44336");
        }
    }

    private void UpdateConnectionStatusFromModel(ConnectionStatus status)
    {
        FullConnectionStatus = status;
        IsConnected = status.IsConnected;
        IsApiKeyValid = status.IsApiKeyValid;
        ServerVersion = status.ServerVersion;
        ConnectionStatus = status.StatusText;
        ConnectionColor = Color.FromArgb(status.StatusColor);
    }

    private void OnApiConnectionStatusChanged(object? sender, ConnectionStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateConnectionStatusFromModel(status);
        });
    }

    private void OnSyncStatusChanged(object? sender, SyncStatus status)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (status)
            {
                case SyncStatus.Disconnected:
                    SyncStatusText = "Offline";
                    SyncStatusColor = Color.FromArgb("#9E9E9E");
                    SyncIcon = "sync_disabled";
                    break;
                case SyncStatus.Connecting:
                    SyncStatusText = "กำลังเชื่อมต่อ...";
                    SyncStatusColor = Color.FromArgb("#FF9800");
                    SyncIcon = "sync";
                    break;
                case SyncStatus.Connected:
                    SyncStatusText = "เชื่อมต่อแล้ว";
                    SyncStatusColor = Color.FromArgb("#2196F3");
                    SyncIcon = "cloud_done";
                    break;
                case SyncStatus.Syncing:
                    SyncStatusText = "กำลัง Sync...";
                    SyncStatusColor = Color.FromArgb("#00BCD4");
                    SyncIcon = "sync";
                    IsSyncing = true;
                    break;
                case SyncStatus.Synced:
                    SyncStatusText = "Synced";
                    SyncStatusColor = Color.FromArgb("#4CAF50");
                    SyncIcon = "cloud_done";
                    IsSyncing = false;
                    LastSyncTime = DateTime.Now;
                    break;
                case SyncStatus.Error:
                    SyncStatusText = "Sync Error";
                    SyncStatusColor = Color.FromArgb("#F44336");
                    SyncIcon = "sync_problem";
                    IsSyncing = false;
                    break;
            }
        });
    }

    private void UpdateFromSystemStatus(SystemStatus status)
    {
        SystemStatus = status;
        PendingTasks = status.PendingTasks;
        RunningTasks = status.RunningTasks;
        CompletedToday = status.CompletedToday;
        ActiveWorkers = status.ActiveWorkers;
        TotalWorkers = status.TotalWorkers;
        CpuUsage = status.CpuUsage;
        MemoryUsage = status.MemoryUsage;
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        UpdateConnectionStatus(connected);
    }

    private void OnSystemStatusUpdated(object? sender, SystemStatus status)
    {
        UpdateFromSystemStatus(status);
    }

    private void OnTaskUpdated(object? sender, TaskItem task)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var existing = RecentTasks.FirstOrDefault(t => t.Id == task.Id);
            if (existing != null)
            {
                var index = RecentTasks.IndexOf(existing);
                RecentTasks[index] = task;
            }
            else
            {
                RecentTasks.Insert(0, task);
                if (RecentTasks.Count > 5)
                {
                    RecentTasks.RemoveAt(RecentTasks.Count - 1);
                }
            }
        });
    }

    private void OnPaymentReceived(object? sender, PaymentInfo payment)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RecentPayments.Insert(0, payment);
            if (RecentPayments.Count > 5)
            {
                RecentPayments.RemoveAt(RecentPayments.Count - 1);
            }
            PendingPayments++;
            TotalPaymentsToday += payment.Amount;
        });
    }
}
