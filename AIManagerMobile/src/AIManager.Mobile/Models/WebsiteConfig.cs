using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace AIManager.Mobile.Models;

/// <summary>
/// Configuration for a website that receives payment notifications
/// </summary>
public class WebsiteConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name for the website (e.g., "ร้าน ABC", "เว็บ XYZ")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Webhook URL to receive payment notifications
    /// Format: https://example.com/api/sms-gateway/webhook
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// API Key for authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret Key for signature generation
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Priority order (lower = higher priority, checked first)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// Whether this website is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Last successful connection time
    /// </summary>
    public DateTime? LastConnectedAt { get; set; }

    /// <summary>
    /// Connection status
    /// </summary>
    public WebsiteConnectionStatus Status { get; set; } = WebsiteConnectionStatus.Unknown;

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Maximum retries before skipping
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Timeout in seconds for webhook calls
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Notes or description
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Created timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Generate HMAC-SHA256 signature for request
    /// </summary>
    public string GenerateSignature(string payload, long timestamp)
    {
        var dataToSign = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Validate the configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(Name) &&
               !string.IsNullOrWhiteSpace(WebhookUrl) &&
               !string.IsNullOrWhiteSpace(ApiKey) &&
               !string.IsNullOrWhiteSpace(SecretKey) &&
               Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var uri) &&
               (uri.Scheme == "https" || uri.Scheme == "http");
    }
}

/// <summary>
/// Website connection status
/// </summary>
public enum WebsiteConnectionStatus
{
    Unknown,
    Connected,
    Disconnected,
    Error,
    Timeout
}

/// <summary>
/// Webhook request payload sent to websites
/// </summary>
public class WebhookPayload
{
    /// <summary>
    /// Unique request ID
    /// </summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Event type
    /// </summary>
    public string Event { get; set; } = "payment.received";

    /// <summary>
    /// Timestamp (Unix epoch milliseconds)
    /// </summary>
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Payment data
    /// </summary>
    public WebhookPaymentData Payment { get; set; } = new();

    /// <summary>
    /// Device information
    /// </summary>
    public WebhookDeviceInfo Device { get; set; } = new();

    /// <summary>
    /// Receiving bank accounts info (for websites to display)
    /// </summary>
    public WebhookBankAccountsInfo? BankAccounts { get; set; }

    /// <summary>
    /// Gateway status info
    /// </summary>
    public WebhookGatewayStatus Gateway { get; set; } = new();
}

/// <summary>
/// Bank accounts info for webhook
/// </summary>
public class WebhookBankAccountsInfo
{
    /// <summary>
    /// Whether the gateway is ready to receive payments
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Number of enabled accounts
    /// </summary>
    public int EnabledCount { get; set; }

    /// <summary>
    /// List of available bank accounts for customers to transfer to
    /// </summary>
    public List<BankAccountPublicInfo> Accounts { get; set; } = new();

    /// <summary>
    /// Message if not ready
    /// </summary>
    public string? NotReadyMessage { get; set; }
}

/// <summary>
/// Gateway status in webhook
/// </summary>
public class WebhookGatewayStatus
{
    /// <summary>
    /// Whether the gateway app is online
    /// </summary>
    public bool IsOnline { get; set; } = true;

    /// <summary>
    /// Last successful SMS check time
    /// </summary>
    public DateTime? LastSmsCheck { get; set; }

    /// <summary>
    /// App version
    /// </summary>
    public string AppVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Payment data in webhook payload
/// </summary>
public class WebhookPaymentData
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public string BankName { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? Reference { get; set; }
    public string? SenderName { get; set; }
    public DateTime TransactionTime { get; set; }
    public string RawSmsBody { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Device info in webhook payload
/// </summary>
public class WebhookDeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string AppVersion { get; set; } = "1.0.0";
}

/// <summary>
/// Response from webhook endpoint
/// </summary>
public class WebhookResponse
{
    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Whether an order was matched
    /// </summary>
    public bool Matched { get; set; }

    /// <summary>
    /// Matched order information (if matched)
    /// </summary>
    public MatchedOrderInfo? Order { get; set; }

    /// <summary>
    /// Error message if any
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Response message
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Matched order information
/// </summary>
public class MatchedOrderInfo
{
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? CustomerName { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Result of webhook dispatch to multiple websites
/// </summary>
public class WebhookDispatchResult
{
    public string PaymentId { get; set; } = string.Empty;
    public bool IsMatched { get; set; }
    public string? MatchedWebsiteId { get; set; }
    public string? MatchedWebsiteName { get; set; }
    public MatchedOrderInfo? MatchedOrder { get; set; }
    public List<WebhookAttempt> Attempts { get; set; } = new();
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single webhook attempt result
/// </summary>
public class WebhookAttempt
{
    public string WebsiteId { get; set; } = string.Empty;
    public string WebsiteName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Matched { get; set; }
    public string? Error { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Unmatched payment notification for admin review
/// </summary>
public class UnmatchedPayment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public PaymentInfo Payment { get; set; } = new();
    public WebhookDispatchResult DispatchResult { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public bool IsReviewed { get; set; }
    public string? AdminNotes { get; set; }
}
