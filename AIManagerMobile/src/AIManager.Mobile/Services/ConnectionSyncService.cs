using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for managing connection sync with AI Manager Core
/// </summary>
public class ConnectionSyncService : IConnectionSyncService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISettingsService _settingsService;
    private readonly IPaymentDetectionService _paymentService;
    private readonly ISignalRService _signalRService;

    private CancellationTokenSource? _heartbeatCts;
    private string? _deviceId;
    private bool _isRegistered;
    private SyncStatus _currentStatus = SyncStatus.Disconnected;

    public SyncStatus CurrentStatus
    {
        get => _currentStatus;
        private set
        {
            if (_currentStatus != value)
            {
                _currentStatus = value;
                MainThread.BeginInvokeOnMainThread(() => SyncStatusChanged?.Invoke(this, value));
            }
        }
    }

    public bool IsRegistered => _isRegistered;

    public event EventHandler<SyncStatus>? SyncStatusChanged;

    public ConnectionSyncService(
        IHttpClientFactory httpClientFactory,
        ISettingsService settingsService,
        IPaymentDetectionService paymentService,
        ISignalRService signalRService)
    {
        _httpClientFactory = httpClientFactory;
        _settingsService = settingsService;
        _paymentService = paymentService;
        _signalRService = signalRService;

        // Generate or get device ID
        _deviceId = Preferences.Get("device_id", null);
        if (string.IsNullOrEmpty(_deviceId))
        {
            _deviceId = Guid.NewGuid().ToString();
            Preferences.Set("device_id", _deviceId);
        }
    }

    public async Task<bool> RegisterDeviceAsync()
    {
        try
        {
            CurrentStatus = SyncStatus.Connecting;

            var settings = _settingsService.GetConnectionSettings();
            var client = CreateClient(settings);

            var registration = new
            {
                deviceId = _deviceId,
                deviceName = DeviceInfo.Name,
                platform = DeviceInfo.Platform.ToString(),
                appVersion = AppInfo.VersionString,
                smsMonitoringEnabled = _settingsService.IsSmsMonitoringEnabled,
                autoApproveEnabled = _settingsService.IsAutoApproveEnabled
            };

            var response = await client.PostAsJsonAsync("/api/mobileconnection/register", registration);

            if (response.IsSuccessStatusCode)
            {
                _isRegistered = true;
                CurrentStatus = SyncStatus.Connected;

                // Also connect SignalR
                _signalRService.SetConnectionSettings(settings);
                await _signalRService.ConnectAsync();

                return true;
            }

            CurrentStatus = SyncStatus.Error;
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Registration failed: {ex.Message}");
            CurrentStatus = SyncStatus.Error;
            return false;
        }
    }

    public async Task StartHeartbeatAsync()
    {
        if (_heartbeatCts != null) return;

        _heartbeatCts = new CancellationTokenSource();
        var token = _heartbeatCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await SendHeartbeatAsync();
                    await Task.Delay(TimeSpan.FromSeconds(15), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Heartbeat error: {ex.Message}");
                    CurrentStatus = SyncStatus.Error;
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
            }
        }, token);
    }

    public Task StopHeartbeatAsync()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        return Task.CompletedTask;
    }

    public async Task<bool> ForceSyncAsync()
    {
        try
        {
            CurrentStatus = SyncStatus.Syncing;
            await SendHeartbeatAsync();
            CurrentStatus = SyncStatus.Synced;
            return true;
        }
        catch
        {
            CurrentStatus = SyncStatus.Error;
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await StopHeartbeatAsync();

            var settings = _settingsService.GetConnectionSettings();
            var client = CreateClient(settings);

            await client.PostAsJsonAsync("/api/mobileconnection/disconnect", new { deviceId = _deviceId });

            await _signalRService.DisconnectAsync();

            _isRegistered = false;
            CurrentStatus = SyncStatus.Disconnected;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Disconnect error: {ex.Message}");
        }
    }

    private async Task SendHeartbeatAsync()
    {
        var settings = _settingsService.GetConnectionSettings();
        var client = CreateClient(settings);

        var pendingPayments = await _paymentService.GetPendingPaymentsAsync();
        var allPayments = await _paymentService.GetAllPaymentsAsync();
        var totalToday = allPayments
            .Where(p => p.TransactionTime.Date == DateTime.Today && p.Type == PaymentType.Incoming)
            .Sum(p => p.Amount);

        var heartbeat = new
        {
            deviceId = _deviceId,
            batteryLevel = GetBatteryLevel(),
            networkType = GetNetworkType(),
            pendingPayments = pendingPayments.Count,
            totalPaymentsToday = totalToday
        };

        var response = await client.PostAsJsonAsync("/api/mobileconnection/heartbeat", heartbeat);

        if (response.IsSuccessStatusCode)
        {
            CurrentStatus = SyncStatus.Synced;
        }
        else
        {
            CurrentStatus = SyncStatus.Error;
        }
    }

    private HttpClient CreateClient(ConnectionSettings settings)
    {
        var client = _httpClientFactory.CreateClient("AIManagerApi");
        client.BaseAddress = new Uri(settings.ApiBaseUrl);
        if (!string.IsNullOrEmpty(settings.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", settings.ApiKey);
        }
        return client;
    }

    private int GetBatteryLevel()
    {
        try
        {
            return (int)(Battery.ChargeLevel * 100);
        }
        catch
        {
            return -1;
        }
    }

    private string GetNetworkType()
    {
        try
        {
            var profiles = Connectivity.Current.ConnectionProfiles;
            if (profiles.Contains(ConnectionProfile.WiFi)) return "WiFi";
            if (profiles.Contains(ConnectionProfile.Cellular)) return "Cellular";
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    public void Dispose()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
    }
}
