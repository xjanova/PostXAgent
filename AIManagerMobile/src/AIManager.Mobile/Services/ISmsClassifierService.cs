using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for classifying SMS messages
/// Determines if SMS is: incoming payment, outgoing payment, or spam
/// </summary>
public interface ISmsClassifierService
{
    /// <summary>
    /// Classify an SMS message
    /// </summary>
    SmsClassification ClassifySms(ReceivedSmsMessage sms);

    /// <summary>
    /// Check if SMS is from a known bank
    /// </summary>
    bool IsFromBank(string sender);

    /// <summary>
    /// Extract payment information if it's a valid payment SMS
    /// </summary>
    PaymentInfo? ExtractPaymentInfo(ReceivedSmsMessage sms);

    /// <summary>
    /// Get the bank name from sender or message body
    /// </summary>
    string? DetectBankName(string sender, string body);
}

/// <summary>
/// SMS classification result
/// </summary>
public class SmsClassification
{
    /// <summary>
    /// The type of SMS
    /// </summary>
    public SmsType Type { get; set; } = SmsType.Unknown;

    /// <summary>
    /// Confidence score (0.0 - 1.0)
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Reason for classification
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Whether this SMS should be processed
    /// </summary>
    public bool ShouldProcess => Type == SmsType.IncomingPayment && Confidence >= 0.7;

    /// <summary>
    /// Bank name if detected
    /// </summary>
    public string? BankName { get; set; }

    /// <summary>
    /// Extracted amount if applicable
    /// </summary>
    public decimal? Amount { get; set; }
}

/// <summary>
/// Types of SMS messages
/// </summary>
public enum SmsType
{
    /// <summary>
    /// Cannot determine type
    /// </summary>
    Unknown,

    /// <summary>
    /// Incoming payment notification (เงินเข้า) - PROCESS THIS
    /// </summary>
    IncomingPayment,

    /// <summary>
    /// Outgoing payment notification (เงินออก) - DO NOT PROCESS
    /// </summary>
    OutgoingPayment,

    /// <summary>
    /// OTP or verification code
    /// </summary>
    Otp,

    /// <summary>
    /// Promotional/advertising message
    /// </summary>
    Spam,

    /// <summary>
    /// Balance inquiry or statement
    /// </summary>
    BalanceNotification,

    /// <summary>
    /// Other bank notification (not payment)
    /// </summary>
    OtherBankNotification
}
