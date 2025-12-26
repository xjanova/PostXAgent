using System.Text.Json.Serialization;

namespace AIManager.Mobile.Models;

/// <summary>
/// Represents an SMS message received on the device
/// </summary>
public class ReceivedSmsMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Sender { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
    public bool IsProcessed { get; set; }
    public SmsCategory Category { get; set; } = SmsCategory.Unknown;
    public PaymentInfo? PaymentInfo { get; set; }
}

/// <summary>
/// Categories of SMS messages
/// </summary>
public enum SmsCategory
{
    Unknown,
    BankNotification,
    PaymentReceived,
    PaymentSent,
    OTP,
    Promotion,
    Other
}

/// <summary>
/// Extracted payment information from SMS
/// </summary>
public class PaymentInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public PaymentType Type { get; set; }
    public DateTime TransactionTime { get; set; }
    public string? Reference { get; set; }
    public string? SenderName { get; set; }
    public string RawMessage { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public bool IsVerified { get; set; }
    public double ConfidenceScore { get; set; }
}

/// <summary>
/// Types of payment transactions
/// </summary>
public enum PaymentType
{
    Incoming,  // เงินเข้า
    Outgoing,  // เงินออก
    Transfer,  // โอน
    Bill,      // จ่ายบิล
    TopUp,     // เติมเงิน
    Unknown
}

/// <summary>
/// Status of payment processing
/// </summary>
public enum PaymentStatus
{
    Pending,       // รอตรวจสอบ
    Verified,      // ตรวจสอบแล้ว
    Approved,      // อนุมัติแล้ว
    Rejected,      // ปฏิเสธ
    Error          // มีข้อผิดพลาด
}

/// <summary>
/// Supported Thai banks for SMS parsing
/// </summary>
public static class ThaiBank
{
    public const string KBANK = "กสิกรไทย";
    public const string SCB = "ไทยพาณิชย์";
    public const string BBL = "กรุงเทพ";
    public const string KTB = "กรุงไทย";
    public const string TMB = "ทหารไทยธนชาต";
    public const string BAY = "กรุงศรีอยุธยา";
    public const string GSB = "ออมสิน";
    public const string BAAC = "ธกส";
    public const string PROMPTPAY = "พร้อมเพย์";

    public static readonly Dictionary<string, string[]> Keywords = new()
    {
        { KBANK, new[] { "KBANK", "กสิกร", "K PLUS", "K-Plus" } },
        { SCB, new[] { "SCB", "ไทยพาณิชย์", "SCB EASY" } },
        { BBL, new[] { "BBL", "กรุงเทพ", "Bualuang" } },
        { KTB, new[] { "KTB", "กรุงไทย", "Krungthai" } },
        { TMB, new[] { "TMB", "TTB", "ทหารไทยธนชาต" } },
        { BAY, new[] { "BAY", "กรุงศรี", "Krungsri" } },
        { GSB, new[] { "GSB", "ออมสิน", "MyMo" } },
        { BAAC, new[] { "BAAC", "ธกส", "A-Mobile" } },
        { PROMPTPAY, new[] { "PromptPay", "พร้อมเพย์", "PP" } }
    };
}
