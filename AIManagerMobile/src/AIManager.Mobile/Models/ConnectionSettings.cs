namespace AIManager.Mobile.Models;

/// <summary>
/// Result of API Key validation
/// </summary>
public class ApiKeyValidationResult
{
    /// <summary>
    /// Is the API Key valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Server version
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Key name/description
    /// </summary>
    public string? KeyName { get; set; }

    /// <summary>
    /// Key expiry date
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message if validation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Connection settings for AI Manager Core
/// </summary>
public class ConnectionSettings
{
    /// <summary>
    /// Server host or IP address
    /// </summary>
    public string ServerHost { get; set; } = "192.168.1.100";

    /// <summary>
    /// API port (default 5000)
    /// </summary>
    public int ApiPort { get; set; } = 5000;

    /// <summary>
    /// SignalR port (default 5002)
    /// </summary>
    public int SignalRPort { get; set; } = 5002;

    /// <summary>
    /// Use HTTPS
    /// </summary>
    public bool UseHttps { get; set; } = false;

    /// <summary>
    /// API Key for authentication with AI Manager Core
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Get base URL for API
    /// </summary>
    public string ApiBaseUrl => $"{(UseHttps ? "https" : "http")}://{ServerHost}:{ApiPort}";

    /// <summary>
    /// Get SignalR hub URL
    /// </summary>
    public string SignalRHubUrl => $"{(UseHttps ? "https" : "http")}://{ServerHost}:{SignalRPort}/hubs/aimanager";

    /// <summary>
    /// Validate settings
    /// </summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(ServerHost) && ApiPort > 0 && SignalRPort > 0;
}

/// <summary>
/// Connection status model
/// </summary>
public class ConnectionStatus
{
    /// <summary>
    /// Is connected to API
    /// </summary>
    public bool IsApiConnected { get; set; }

    /// <summary>
    /// Is connected to SignalR
    /// </summary>
    public bool IsSignalRConnected { get; set; }

    /// <summary>
    /// Last successful connection time
    /// </summary>
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>
    /// Last error message
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Server version
    /// </summary>
    public string? ServerVersion { get; set; }

    /// <summary>
    /// Is API Key valid
    /// </summary>
    public bool IsApiKeyValid { get; set; }

    /// <summary>
    /// Overall connection status
    /// </summary>
    public bool IsConnected => IsApiConnected && IsApiKeyValid;

    /// <summary>
    /// Status text for display
    /// </summary>
    public string StatusText
    {
        get
        {
            if (!IsApiConnected)
                return "ไม่ได้เชื่อมต่อ";
            if (!IsApiKeyValid)
                return "API Key ไม่ถูกต้อง";
            if (!IsSignalRConnected)
                return "เชื่อมต่อ API แล้ว (SignalR ยังไม่เชื่อมต่อ)";
            return "เชื่อมต่อสำเร็จ";
        }
    }

    /// <summary>
    /// Status color for display
    /// </summary>
    public string StatusColor
    {
        get
        {
            if (!IsApiConnected)
                return "#F44336"; // Red
            if (!IsApiKeyValid)
                return "#FF9800"; // Orange
            if (!IsSignalRConnected)
                return "#2196F3"; // Blue
            return "#4CAF50"; // Green
        }
    }
}
