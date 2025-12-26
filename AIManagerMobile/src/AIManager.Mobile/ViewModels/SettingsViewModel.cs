using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// Settings ViewModel - App configuration
/// </summary>
public partial class SettingsViewModel : BaseViewModel
{
    private readonly ISettingsService _settingsService;
    private readonly IAIManagerApiService _apiService;
    private readonly ISignalRService _signalRService;

    [ObservableProperty]
    private string _serverHost = "192.168.1.100";

    [ObservableProperty]
    private int _apiPort = 5000;

    [ObservableProperty]
    private int _signalRPort = 5002;

    [ObservableProperty]
    private bool _useHttps;

    [ObservableProperty]
    private string _apiKey = string.Empty;

    [ObservableProperty]
    private bool _smsMonitoringEnabled;

    [ObservableProperty]
    private bool _autoApproveEnabled;

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private double _confidenceThreshold = 0.85;

    [ObservableProperty]
    private string _connectionTestResult = string.Empty;

    [ObservableProperty]
    private Color _connectionTestColor = Colors.Gray;

    [ObservableProperty]
    private bool _isApiKeyValid;

    [ObservableProperty]
    private string _apiKeyStatusText = "ยังไม่ได้ทดสอบ";

    [ObservableProperty]
    private Color _apiKeyStatusColor = Colors.Gray;

    [ObservableProperty]
    private string? _serverVersion;

    public SettingsViewModel(
        ISettingsService settingsService,
        IAIManagerApiService apiService,
        ISignalRService signalRService)
    {
        _settingsService = settingsService;
        _apiService = apiService;
        _signalRService = signalRService;

        Title = "ตั้งค่า";

        LoadSettings();
    }

    private void LoadSettings()
    {
        var connectionSettings = _settingsService.GetConnectionSettings();
        ServerHost = connectionSettings.ServerHost;
        ApiPort = connectionSettings.ApiPort;
        SignalRPort = connectionSettings.SignalRPort;
        UseHttps = connectionSettings.UseHttps;
        ApiKey = connectionSettings.ApiKey ?? string.Empty;

        SmsMonitoringEnabled = _settingsService.IsSmsMonitoringEnabled;
        AutoApproveEnabled = _settingsService.IsAutoApproveEnabled;
        NotificationsEnabled = _settingsService.IsNotificationsEnabled;
        ConfidenceThreshold = _settingsService.ConfidenceThreshold;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var connectionSettings = new ConnectionSettings
        {
            ServerHost = ServerHost,
            ApiPort = ApiPort,
            SignalRPort = SignalRPort,
            UseHttps = UseHttps,
            ApiKey = ApiKey
        };

        _settingsService.SaveConnectionSettings(connectionSettings);
        _settingsService.IsSmsMonitoringEnabled = SmsMonitoringEnabled;
        _settingsService.IsAutoApproveEnabled = AutoApproveEnabled;
        _settingsService.IsNotificationsEnabled = NotificationsEnabled;
        _settingsService.ConfidenceThreshold = ConfidenceThreshold;

        // Update services with new settings
        _apiService.SetConnectionSettings(connectionSettings);
        _signalRService.SetConnectionSettings(connectionSettings);

        ShowMessage("บันทึกสำเร็จ", "บันทึกการตั้งค่าเรียบร้อยแล้ว");
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        await ExecuteAsync(async () =>
        {
            var settings = new ConnectionSettings
            {
                ServerHost = ServerHost,
                ApiPort = ApiPort,
                SignalRPort = SignalRPort,
                UseHttps = UseHttps,
                ApiKey = ApiKey
            };

            _apiService.SetConnectionSettings(settings);

            ConnectionTestResult = "กำลังทดสอบ...";
            ConnectionTestColor = Colors.Orange;
            ApiKeyStatusText = "กำลังตรวจสอบ...";
            ApiKeyStatusColor = Colors.Orange;

            // Get full connection status including API Key validation
            var connectionStatus = await _apiService.GetConnectionStatusAsync();

            if (connectionStatus.IsApiConnected)
            {
                ConnectionTestResult = "เชื่อมต่อ API สำเร็จ";
                ConnectionTestColor = Color.FromArgb("#4CAF50");
                ServerVersion = connectionStatus.ServerVersion;

                // Check API Key
                if (connectionStatus.IsApiKeyValid)
                {
                    IsApiKeyValid = true;
                    ApiKeyStatusText = "API Key ถูกต้อง";
                    ApiKeyStatusColor = Color.FromArgb("#4CAF50");
                }
                else
                {
                    IsApiKeyValid = false;
                    ApiKeyStatusText = "API Key ไม่ถูกต้อง";
                    ApiKeyStatusColor = Color.FromArgb("#F44336");
                }
            }
            else
            {
                ConnectionTestResult = "ไม่สามารถเชื่อมต่อได้";
                ConnectionTestColor = Color.FromArgb("#F44336");
                ApiKeyStatusText = "ไม่สามารถตรวจสอบได้";
                ApiKeyStatusColor = Colors.Gray;
                IsApiKeyValid = false;
            }
        });
    }

    [RelayCommand]
    private async Task ReconnectSignalRAsync()
    {
        await ExecuteAsync(async () =>
        {
            await _signalRService.DisconnectAsync();

            var settings = new ConnectionSettings
            {
                ServerHost = ServerHost,
                ApiPort = ApiPort,
                SignalRPort = SignalRPort,
                UseHttps = UseHttps,
                ApiKey = ApiKey
            };

            _signalRService.SetConnectionSettings(settings);
            await _signalRService.ConnectAsync();

            if (_signalRService.IsConnected)
            {
                ShowMessage("เชื่อมต่อสำเร็จ", "เชื่อมต่อ SignalR สำเร็จ");
            }
            else
            {
                ShowError("ไม่สามารถเชื่อมต่อ SignalR ได้");
            }
        });
    }

    [RelayCommand]
    private void ResetSettings()
    {
        ServerHost = "192.168.1.100";
        ApiPort = 5000;
        SignalRPort = 5002;
        UseHttps = false;
        ApiKey = string.Empty;
        SmsMonitoringEnabled = true;
        AutoApproveEnabled = false;
        NotificationsEnabled = true;
        ConfidenceThreshold = 0.85;

        SaveSettings();
    }

    [RelayCommand]
    private async Task OpenAboutAsync()
    {
        var about = "AI Manager Mobile\n" +
                   "Version 1.0.0\n\n" +
                   "แอพจัดการ AI Manager Core บนมือถือ\n" +
                   "- ดูสถานะระบบ\n" +
                   "- จัดการ Tasks และ Workers\n" +
                   "- ตรวจสอบ SMS เพื่อรับยอดเงิน\n" +
                   "- อนุมัติการชำระเงินอัตโนมัติ\n\n" +
                   "PostXAgent Team";

        ShowMessage("เกี่ยวกับแอพ", about);
    }

    partial void OnSmsMonitoringEnabledChanged(bool value)
    {
        _settingsService.IsSmsMonitoringEnabled = value;
    }

    partial void OnAutoApproveEnabledChanged(bool value)
    {
        _settingsService.IsAutoApproveEnabled = value;
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        _settingsService.IsNotificationsEnabled = value;
    }

    partial void OnConfidenceThresholdChanged(double value)
    {
        _settingsService.ConfidenceThreshold = value;
    }
}
