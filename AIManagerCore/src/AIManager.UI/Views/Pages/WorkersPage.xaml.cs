using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AIManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AIManager.UI.Views.Pages;

public partial class WorkersPage : Page
{
    private readonly GpuPoolService? _gpuPoolService;
    private readonly ILogger<WorkersPage>? _logger;
    private readonly DispatcherTimer _updateTimer;
    private readonly DispatcherTimer _animationTimer;
    private bool _isInitialized;

    public ObservableCollection<WorkerCardViewModel> Workers { get; } = new();
    public ObservableCollection<ActivityLogItem> ActivityItems { get; } = new();

    public WorkersPage()
    {
        InitializeComponent();

        // Get services from DI
        try
        {
            var services = App.Services;
            _gpuPoolService = services?.GetService<GpuPoolService>();
            _logger = services?.GetService<ILogger<WorkersPage>>();

            if (_gpuPoolService != null)
            {
                _gpuPoolService.WorkerStatusChanged += GpuPoolService_WorkerStatusChanged;
                _gpuPoolService.TaskCompleted += GpuPoolService_TaskCompleted;
            }
        }
        catch
        {
            // DI not available
        }

        WorkersGrid.ItemsSource = Workers;
        ActivityLog.ItemsSource = ActivityItems;

        // Update timer (every 2 seconds)
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _updateTimer.Tick += (s, e) => RefreshStats();

        // Animation timer (every 500ms for blinking effects)
        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _animationTimer.Tick += (s, e) => UpdateAnimations();

        Loaded += async (s, e) =>
        {
            _isInitialized = true;
            await RefreshWorkersAsync();
            _updateTimer.Start();
            _animationTimer.Start();
        };

        Unloaded += (s, e) =>
        {
            _updateTimer.Stop();
            _animationTimer.Stop();
            _isInitialized = false;

            if (_gpuPoolService != null)
            {
                _gpuPoolService.WorkerStatusChanged -= GpuPoolService_WorkerStatusChanged;
                _gpuPoolService.TaskCompleted -= GpuPoolService_TaskCompleted;
            }
        };
    }

    private void GpuPoolService_WorkerStatusChanged(object? sender, GpuWorkerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            SyncWorkersFromService();
            RefreshStats();

            // Add to activity log
            var icon = e.EventType switch
            {
                "added" => "ServerPlus",
                "removed" => "ServerMinus",
                "online" => "CheckCircle",
                "offline" => "AlertCircle",
                _ => "Information"
            };
            var color = e.EventType switch
            {
                "added" => "#10B981",
                "removed" => "#EF4444",
                "online" => "#10B981",
                "offline" => "#F59E0B",
                _ => "#6B6B8A"
            };

            AddActivityLog(icon, color, e.Worker.Name, $"Worker {e.EventType}");
        });
    }

    private void GpuPoolService_TaskCompleted(object? sender, GpuTaskEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshStats();

            // Find worker and trigger transfer animation
            var worker = Workers.FirstOrDefault(w => w.Id == e.Task.WorkerId);
            if (worker != null)
            {
                worker.TriggerTransferAnimation(e.Result.Success);
                worker.TasksCompleted++;
            }

            var icon = e.Result.Success ? "CheckCircle" : "AlertCircle";
            var color = e.Result.Success ? "#10B981" : "#EF4444";
            var message = e.Result.Success
                ? $"Task completed in {e.Result.GenerationTime:F1}s"
                : $"Task failed: {e.Result.Error}";

            AddActivityLog(icon, color, $"Worker {e.Task.WorkerId[..8]}", message);
        });
    }

    private void AddActivityLog(string icon, string color, string workerName, string message)
    {
        ActivityItems.Insert(0, new ActivityLogItem
        {
            Timestamp = DateTime.Now,
            Icon = icon,
            IconColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
            WorkerName = workerName,
            Message = message
        });

        // Keep only last 50 items
        while (ActivityItems.Count > 50)
        {
            ActivityItems.RemoveAt(ActivityItems.Count - 1);
        }
    }

    private void SyncWorkersFromService()
    {
        if (_gpuPoolService == null) return;

        // Update existing or add new workers
        var serviceWorkers = _gpuPoolService.Workers.ToList();
        var existingIds = Workers.Select(w => w.Id).ToHashSet();
        var serviceIds = serviceWorkers.Select(w => w.Id).ToHashSet();

        // Remove workers that no longer exist
        var toRemove = Workers.Where(w => !serviceIds.Contains(w.Id)).ToList();
        foreach (var worker in toRemove)
        {
            Workers.Remove(worker);
        }

        // Add or update workers
        foreach (var serviceWorker in serviceWorkers)
        {
            var existingWorker = Workers.FirstOrDefault(w => w.Id == serviceWorker.Id);
            if (existingWorker != null)
            {
                // Update existing
                existingWorker.UpdateFromService(serviceWorker);
            }
            else
            {
                // Add new
                Workers.Add(new WorkerCardViewModel(serviceWorker));
            }
        }

        UpdateNoWorkersPanel();
    }

    private void UpdateNoWorkersPanel()
    {
        NoWorkersPanel.Visibility = Workers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtWorkerCount.Text = $" ({Workers.Count})";
    }

    private void RefreshStats()
    {
        if (_gpuPoolService == null)
        {
            ShowEmptyStats();
            return;
        }

        var workers = _gpuPoolService.Workers;
        var onlineWorkers = _gpuPoolService.OnlineWorkers;
        var busyWorkers = workers.Where(w => w.IsBusy).ToList();

        TxtTotalWorkers.Text = workers.Count.ToString();
        TxtOnlineWorkers.Text = onlineWorkers.Count.ToString();
        TxtBusyWorkers.Text = busyWorkers.Count.ToString();
        TxtTotalVram.Text = $"{workers.Sum(w => w.TotalVramGb):F0} GB";
        TxtQueueSize.Text = _gpuPoolService.QueuedTaskCount.ToString();
        TxtCompletedTasks.Text = _gpuPoolService.CompletedTaskCount.ToString();

        // Update connection status
        if (onlineWorkers.Count > 0)
        {
            ConnectionIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            TxtConnectionStatus.Text = "Connected";
            TxtConnectionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
        }
        else if (workers.Count > 0)
        {
            ConnectionIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            TxtConnectionStatus.Text = "Workers Offline";
            TxtConnectionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
        }
        else
        {
            ConnectionIndicator.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B6B8A"));
            TxtConnectionStatus.Text = "No Workers";
            TxtConnectionStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B6B8A"));
        }

        // Update distribution mode display
        TxtDistributionMode.Text = _gpuPoolService.DistributionMode.ToString();
        BtnDistributionMode.IsChecked = _gpuPoolService.DistributionMode == GpuDistributionMode.Parallel;
    }

    private void ShowEmptyStats()
    {
        TxtTotalWorkers.Text = "0";
        TxtOnlineWorkers.Text = "0";
        TxtBusyWorkers.Text = "0";
        TxtTotalVram.Text = "0 GB";
        TxtQueueSize.Text = "0";
        TxtCompletedTasks.Text = "0";
    }

    private void UpdateAnimations()
    {
        foreach (var worker in Workers)
        {
            worker.UpdateAnimation();
        }
    }

    private async Task RefreshWorkersAsync()
    {
        if (_gpuPoolService != null)
        {
            await _gpuPoolService.RefreshAllWorkersAsync();
            SyncWorkersFromService();
        }
        RefreshStats();
    }

    #region Button Handlers

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshWorkersAsync();
    }

    private void BtnAddWorker_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddWorkerDialog();
        if (dialog.ShowDialog() == true)
        {
            _ = AddWorkerAsync(dialog.WorkerName, dialog.WorkerUrl);
        }
    }

    private async Task AddWorkerAsync(string name, string url)
    {
        if (_gpuPoolService != null)
        {
            var worker = await _gpuPoolService.AddWorkerAsync(name, url);
            if (worker != null)
            {
                SyncWorkersFromService();
                RefreshStats();
                _logger?.LogInformation("Worker added: {Name} at {Url}", name, url);
            }
        }
    }

    private void BtnRemoveWorker_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string workerId)
        {
            var result = MessageBox.Show(
                "Are you sure you want to remove this worker?",
                "Confirm Remove",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _gpuPoolService?.RemoveWorker(workerId);
                SyncWorkersFromService();
                RefreshStats();
            }
        }
    }

    private void BtnOpenColab_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://colab.research.google.com",
            UseShellExecute = true
        });
    }

    private void DistributionMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_gpuPoolService != null && _isInitialized)
        {
            _gpuPoolService.DistributionMode = BtnDistributionMode.IsChecked == true
                ? GpuDistributionMode.Parallel
                : GpuDistributionMode.Combined;

            TxtDistributionMode.Text = _gpuPoolService.DistributionMode.ToString();
        }
    }

    #endregion
}

#region View Models

public class WorkerCardViewModel : INotifyPropertyChanged
{
    private bool _isOnline;
    private bool _isBusy;
    private bool _isTransferring;
    private bool _transferSuccess;
    private double _transferOpacity;
    private double _activityOpacity;
    private int _animationFrame;
    private DateTime _addedAt;
    private int _tasksCompleted;

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string GpuInfo { get; set; } = "";
    public double TotalVramGb { get; set; }
    public double FreeVramGb { get; set; }
    public string? CurrentModel { get; set; }
    public string? CurrentTask { get; set; }

    public bool IsOnline
    {
        get => _isOnline;
        set { _isOnline = value; OnPropertyChanged(); UpdateVisualProperties(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); UpdateVisualProperties(); }
    }

    public int TasksCompleted
    {
        get => _tasksCompleted;
        set { _tasksCompleted = value; OnPropertyChanged(); }
    }

    // Visual Properties
    public SolidColorBrush StatusColor => IsOnline
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsBusy ? "#F59E0B" : "#10B981"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

    public SolidColorBrush BorderColor => IsOnline
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsBusy ? "#3D3D00" : "#1E3A2F"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D1E1E"));

    public string StatusText => IsBusy ? "Working" : (IsOnline ? "Online" : "Offline");

    public SolidColorBrush StatusBadgeBackground => IsOnline
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsBusy ? "#3D3D1E" : "#1E3A2F"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D1E1E"));

    public SolidColorBrush StatusBadgeForeground => IsOnline
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsBusy ? "#F59E0B" : "#10B981"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));

    public string GlowColor => IsOnline ? (IsBusy ? "#F59E0B" : "#10B981") : "#EF4444";
    public double GlowRadius => IsOnline ? (IsBusy ? 12 : 8) : 6;

    // Activity ring
    public SolidColorBrush ActivityColor => IsBusy
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"))
        : new SolidColorBrush(Colors.Transparent);

    public double ActivityOpacity
    {
        get => _activityOpacity;
        set { _activityOpacity = value; OnPropertyChanged(); }
    }

    // Transfer animation
    public string TransferIcon => _transferSuccess ? "ArrowDown" : "ArrowUp";
    public SolidColorBrush TransferColor => _transferSuccess
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3B82F6"));

    public Visibility TransferVisibility => _isTransferring ? Visibility.Visible : Visibility.Collapsed;

    public double TransferOpacity
    {
        get => _transferOpacity;
        set { _transferOpacity = value; OnPropertyChanged(); }
    }

    // VRAM bar
    public string VramText => $"{FreeVramGb:F1}/{TotalVramGb:F1} GB";
    public double VramPercent => TotalVramGb > 0 ? ((TotalVramGb - FreeVramGb) / TotalVramGb) * 100 : 0;
    public double VramBarWidth => Math.Min(VramPercent * 2.5, 250); // Scale to max width
    public SolidColorBrush VramBarColor
    {
        get
        {
            var percent = VramPercent;
            if (percent > 80) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            if (percent > 60) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
        }
    }

    // Current task display
    public string CurrentTaskShort => CurrentTask?.Length > 25 ? CurrentTask[..25] + "..." : CurrentTask ?? "";
    public Visibility CurrentTaskVisibility => string.IsNullOrEmpty(CurrentTask) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CurrentModelVisibility => string.IsNullOrEmpty(CurrentModel) ? Visibility.Collapsed : Visibility.Visible;

    // Uptime
    public string UptimeText
    {
        get
        {
            var uptime = DateTime.UtcNow - _addedAt;
            if (uptime.TotalHours >= 1) return $"{uptime.TotalHours:F0}h";
            if (uptime.TotalMinutes >= 1) return $"{uptime.TotalMinutes:F0}m";
            return $"{uptime.TotalSeconds:F0}s";
        }
    }

    // Ping (simulated for now)
    public string PingText => IsOnline ? "< 100ms" : "-";
    public SolidColorBrush PingColor => IsOnline
        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6B6B8A"));

    public WorkerCardViewModel() { }

    public WorkerCardViewModel(GpuWorkerInfo worker)
    {
        UpdateFromService(worker);
        _addedAt = worker.AddedAt;
    }

    public void UpdateFromService(GpuWorkerInfo worker)
    {
        Id = worker.Id;
        Name = worker.Name;
        Url = worker.Url;
        GpuInfo = worker.GpuName ?? "Unknown GPU";
        TotalVramGb = worker.TotalVramGb;
        FreeVramGb = worker.FreeVramGb;
        CurrentModel = worker.CurrentModel;
        CurrentTask = worker.CurrentTask;
        IsOnline = worker.IsOnline;
        IsBusy = worker.IsBusy;

        OnPropertyChanged(nameof(VramText));
        OnPropertyChanged(nameof(VramPercent));
        OnPropertyChanged(nameof(VramBarWidth));
        OnPropertyChanged(nameof(VramBarColor));
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(CurrentTaskShort));
        OnPropertyChanged(nameof(CurrentTaskVisibility));
        OnPropertyChanged(nameof(CurrentModelVisibility));
    }

    public void TriggerTransferAnimation(bool success)
    {
        _isTransferring = true;
        _transferSuccess = success;
        _transferOpacity = 1.0;
        OnPropertyChanged(nameof(TransferVisibility));
        OnPropertyChanged(nameof(TransferIcon));
        OnPropertyChanged(nameof(TransferColor));
        OnPropertyChanged(nameof(TransferOpacity));
    }

    public void UpdateAnimation()
    {
        _animationFrame++;

        // Activity ring animation for busy workers
        if (IsBusy)
        {
            ActivityOpacity = 0.3 + 0.7 * Math.Abs(Math.Sin(_animationFrame * 0.2));
        }
        else
        {
            ActivityOpacity = 0;
        }

        // Transfer animation fade out
        if (_isTransferring)
        {
            _transferOpacity -= 0.2;
            if (_transferOpacity <= 0)
            {
                _isTransferring = false;
                _transferOpacity = 0;
                OnPropertyChanged(nameof(TransferVisibility));
            }
            OnPropertyChanged(nameof(TransferOpacity));
        }
    }

    private void UpdateVisualProperties()
    {
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(BorderColor));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBadgeBackground));
        OnPropertyChanged(nameof(StatusBadgeForeground));
        OnPropertyChanged(nameof(GlowColor));
        OnPropertyChanged(nameof(GlowRadius));
        OnPropertyChanged(nameof(ActivityColor));
        OnPropertyChanged(nameof(PingText));
        OnPropertyChanged(nameof(PingColor));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public class ActivityLogItem
{
    public DateTime Timestamp { get; set; }
    public string Icon { get; set; } = "";
    public SolidColorBrush IconColor { get; set; } = new(Colors.Gray);
    public string WorkerName { get; set; } = "";
    public string Message { get; set; } = "";
}

#endregion
