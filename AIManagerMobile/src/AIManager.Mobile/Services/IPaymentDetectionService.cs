using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Interface for AI-powered payment detection from SMS
/// </summary>
public interface IPaymentDetectionService
{
    /// <summary>
    /// Event when payment is detected from SMS
    /// </summary>
    event EventHandler<PaymentInfo>? PaymentDetected;

    /// <summary>
    /// Analyze SMS message for payment information
    /// </summary>
    Task<PaymentInfo?> AnalyzeSmsAsync(ReceivedSmsMessage sms);

    /// <summary>
    /// Get all detected payments
    /// </summary>
    Task<List<PaymentInfo>> GetAllPaymentsAsync();

    /// <summary>
    /// Get pending payments awaiting approval
    /// </summary>
    Task<List<PaymentInfo>> GetPendingPaymentsAsync();

    /// <summary>
    /// Verify payment manually
    /// </summary>
    Task<bool> VerifyPaymentAsync(string paymentId);

    /// <summary>
    /// Approve payment
    /// </summary>
    Task<bool> ApprovePaymentAsync(string paymentId);

    /// <summary>
    /// Reject payment
    /// </summary>
    Task<bool> RejectPaymentAsync(string paymentId, string reason);

    /// <summary>
    /// Start auto-detection service
    /// </summary>
    Task StartAutoDetectionAsync();

    /// <summary>
    /// Stop auto-detection service
    /// </summary>
    Task StopAutoDetectionAsync();
}
