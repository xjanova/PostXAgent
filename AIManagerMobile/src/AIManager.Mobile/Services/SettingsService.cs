using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Settings service using MAUI Preferences
/// </summary>
public class SettingsService : ISettingsService
{
    private const string ServerHostKey = "server_host";
    private const string ApiPortKey = "api_port";
    private const string SignalRPortKey = "signalr_port";
    private const string UseHttpsKey = "use_https";
    private const string ApiKeyKey = "api_key";
    private const string SmsMonitoringKey = "sms_monitoring";
    private const string AutoApproveKey = "auto_approve";
    private const string NotificationsKey = "notifications";
    private const string ConfidenceThresholdKey = "confidence_threshold";

    public ConnectionSettings GetConnectionSettings()
    {
        return new ConnectionSettings
        {
            ServerHost = Preferences.Get(ServerHostKey, "192.168.1.100"),
            ApiPort = Preferences.Get(ApiPortKey, 5000),
            SignalRPort = Preferences.Get(SignalRPortKey, 5002),
            UseHttps = Preferences.Get(UseHttpsKey, false),
            ApiKey = Preferences.Get(ApiKeyKey, string.Empty)
        };
    }

    public void SaveConnectionSettings(ConnectionSettings settings)
    {
        Preferences.Set(ServerHostKey, settings.ServerHost);
        Preferences.Set(ApiPortKey, settings.ApiPort);
        Preferences.Set(SignalRPortKey, settings.SignalRPort);
        Preferences.Set(UseHttpsKey, settings.UseHttps);
        Preferences.Set(ApiKeyKey, settings.ApiKey ?? string.Empty);
    }

    public bool IsSmsMonitoringEnabled
    {
        get => Preferences.Get(SmsMonitoringKey, true);
        set => Preferences.Set(SmsMonitoringKey, value);
    }

    public bool IsAutoApproveEnabled
    {
        get => Preferences.Get(AutoApproveKey, false);
        set => Preferences.Set(AutoApproveKey, value);
    }

    public bool IsNotificationsEnabled
    {
        get => Preferences.Get(NotificationsKey, true);
        set => Preferences.Set(NotificationsKey, value);
    }

    public double ConfidenceThreshold
    {
        get => Preferences.Get(ConfidenceThresholdKey, 0.85);
        set => Preferences.Set(ConfidenceThresholdKey, value);
    }
}
