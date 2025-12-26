using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for dispatching payment webhooks to configured websites
/// Implements sequential dispatching with fallback
/// </summary>
public interface IWebhookDispatchService
{
    /// <summary>
    /// Event fired when a payment is matched with an order
    /// </summary>
    event EventHandler<WebhookDispatchResult>? PaymentMatched;

    /// <summary>
    /// Event fired when a payment could not be matched with any website
    /// </summary>
    event EventHandler<UnmatchedPayment>? PaymentUnmatched;

    /// <summary>
    /// Event fired on dispatch progress (for UI updates)
    /// </summary>
    event EventHandler<DispatchProgressEventArgs>? DispatchProgress;

    /// <summary>
    /// Dispatch payment to configured websites sequentially
    /// Stops when a match is found or all websites are checked
    /// </summary>
    Task<WebhookDispatchResult> DispatchPaymentAsync(PaymentInfo payment);

    /// <summary>
    /// Get list of unmatched payments pending admin review
    /// </summary>
    Task<List<UnmatchedPayment>> GetUnmatchedPaymentsAsync();

    /// <summary>
    /// Mark unmatched payment as reviewed
    /// </summary>
    Task MarkAsReviewedAsync(string paymentId, string? notes = null);

    /// <summary>
    /// Retry dispatching an unmatched payment
    /// </summary>
    Task<WebhookDispatchResult> RetryDispatchAsync(string paymentId);

    /// <summary>
    /// Get dispatch statistics
    /// </summary>
    Task<DispatchStatistics> GetStatisticsAsync();
}

/// <summary>
/// Progress event args for webhook dispatch
/// </summary>
public class DispatchProgressEventArgs : EventArgs
{
    public string PaymentId { get; set; } = string.Empty;
    public int CurrentWebsiteIndex { get; set; }
    public int TotalWebsites { get; set; }
    public string CurrentWebsiteName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}

/// <summary>
/// Dispatch statistics
/// </summary>
public class DispatchStatistics
{
    public int TotalDispatched { get; set; }
    public int TotalMatched { get; set; }
    public int TotalUnmatched { get; set; }
    public int TotalFailed { get; set; }
    public double MatchRate => TotalDispatched > 0 ? (double)TotalMatched / TotalDispatched * 100 : 0;
    public Dictionary<string, int> MatchesByWebsite { get; set; } = new();
    public DateTime? LastDispatchTime { get; set; }
}
