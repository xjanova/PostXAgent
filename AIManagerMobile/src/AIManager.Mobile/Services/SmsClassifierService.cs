using System.Text.RegularExpressions;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for classifying SMS messages
/// Accurately distinguishes between incoming/outgoing payments and spam
/// </summary>
public class SmsClassifierService : ISmsClassifierService
{
    // Bank sender patterns (Thai banks)
    private static readonly Dictionary<string, string[]> BankSenders = new()
    {
        { "กสิกรไทย", new[] { "KBANK", "K-BANK", "KPLUS", "K PLUS", "K-Plus" } },
        { "ไทยพาณิชย์", new[] { "SCB", "SCBEASY", "SCB EASY" } },
        { "กรุงเทพ", new[] { "BBL", "BUALUANG", "Bangkok Bank" } },
        { "กรุงไทย", new[] { "KTB", "KRUNGTHAI", "Krungthai" } },
        { "ทหารไทยธนชาต", new[] { "TMB", "TTB", "TMBTTB", "ttb" } },
        { "กรุงศรีอยุธยา", new[] { "BAY", "KRUNGSRI", "Krungsri" } },
        { "ออมสิน", new[] { "GSB", "MYMO", "MyMo" } },
        { "ธ.ก.ส.", new[] { "BAAC", "ธกส" } },
        { "พร้อมเพย์", new[] { "PromptPay", "PP", "PROMPTPAY" } },
        { "ทรูมันนี่", new[] { "TrueMoney", "TrueMoney Wallet" } },
        { "LINE Pay", new[] { "LINE", "LINEPAY", "Rabbit LINE Pay" } }
    };

    // Keywords that strongly indicate INCOMING payment (เงินเข้า)
    private static readonly string[] IncomingKeywords = new[]
    {
        // Thai
        "รับโอน", "เงินเข้า", "รับเงิน", "โอนเข้า", "ได้รับ", "เข้าบัญชี",
        "รับจาก", "โอนมาจาก", "ฝากเข้า", "เติมเงินเข้า", "คืนเงิน",
        "received", "credit", "credited", "deposit", "deposited",
        // PromptPay specific
        "พร้อมเพย์เข้า", "PP เข้า", "รับพร้อมเพย์"
    };

    // Keywords that strongly indicate OUTGOING payment (เงินออก)
    private static readonly string[] OutgoingKeywords = new[]
    {
        // Thai
        "โอนเงิน", "หักบัญชี", "จ่าย", "ถอน", "ชำระ", "ซื้อ", "โอนไป",
        "หักเงิน", "ตัดเงิน", "โอนออก", "เงินออก", "จ่ายบิล",
        "โอนให้", "โอนเข้าบัญชี", "สำเร็จ โอน",
        // English
        "withdraw", "withdrawn", "debit", "debited", "transfer to",
        "payment", "paid", "purchase", "bought", "sent"
    };

    // Keywords indicating OTP/verification
    private static readonly string[] OtpKeywords = new[]
    {
        "OTP", "รหัส", "verification", "verify", "code", "PIN",
        "ยืนยัน", "รหัสผ่าน", "password", "one-time", "ครั้งเดียว"
    };

    // Keywords indicating promotional/spam
    private static readonly string[] SpamKeywords = new[]
    {
        "โปรโมชั่น", "promotion", "ส่วนลด", "discount", "สมัคร",
        "apply", "loan", "สินเชื่อ", "กู้", "borrow", "credit card",
        "บัตรเครดิต", "สมัครเลย", "คลิก", "click", "ฟรี", "free",
        "รางวัล", "prize", "ชนะ", "win", "โชคดี", "lucky"
    };

    // Keywords indicating balance notification
    private static readonly string[] BalanceKeywords = new[]
    {
        "ยอดเงินคงเหลือ", "ยอดคงเหลือ", "balance", "คงเหลือ",
        "remaining", "available", "statement"
    };

    // Regex for extracting amount
    private static readonly Regex AmountRegex = new(
        @"(?:จำนวน|ยอด|amount|THB|บาท|฿)[\s:]*([0-9,]+\.?\d*)|([0-9,]+\.?\d*)[\s]*(?:บาท|THB|฿)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Regex for detecting amount with context
    private static readonly Regex AmountWithContextRegex = new(
        @"([0-9,]+\.?\d*)[\s]*(?:บาท|THB|฿)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SmsClassification ClassifySms(ReceivedSmsMessage sms)
    {
        var result = new SmsClassification();
        var body = sms.Body.ToLower();
        var sender = sms.Sender.ToUpper();

        // Step 1: Check if it's from a bank
        var bankName = DetectBankName(sms.Sender, sms.Body);
        result.BankName = bankName;

        if (string.IsNullOrEmpty(bankName))
        {
            // Not from a recognized bank - likely spam or irrelevant
            result.Type = SmsType.Unknown;
            result.Confidence = 0.3;
            result.Reason = "ไม่พบชื่อธนาคารที่รู้จัก";
            return result;
        }

        // Step 2: Check for OTP first (high priority)
        if (OtpKeywords.Any(k => body.Contains(k.ToLower())))
        {
            result.Type = SmsType.Otp;
            result.Confidence = 0.95;
            result.Reason = "พบคำสำคัญ OTP/รหัส";
            return result;
        }

        // Step 3: Check for spam/promotional
        var spamScore = SpamKeywords.Count(k => body.Contains(k.ToLower()));
        if (spamScore >= 2)
        {
            result.Type = SmsType.Spam;
            result.Confidence = 0.8 + (spamScore * 0.05);
            result.Reason = $"พบคำโฆษณา {spamScore} คำ";
            return result;
        }

        // Step 4: Check for balance notification
        if (BalanceKeywords.Any(k => body.Contains(k.ToLower())))
        {
            // Could be balance notification OR payment with balance info
            var hasPaymentContext = IncomingKeywords.Any(k => body.Contains(k.ToLower())) ||
                                    OutgoingKeywords.Any(k => body.Contains(k.ToLower()));

            if (!hasPaymentContext)
            {
                result.Type = SmsType.BalanceNotification;
                result.Confidence = 0.85;
                result.Reason = "แจ้งยอดเงินคงเหลือ";
                return result;
            }
        }

        // Step 5: Classify as incoming or outgoing payment
        var incomingScore = CalculateKeywordScore(body, IncomingKeywords);
        var outgoingScore = CalculateKeywordScore(body, OutgoingKeywords);

        // Extract amount
        result.Amount = ExtractAmount(sms.Body);

        if (incomingScore > outgoingScore && incomingScore > 0)
        {
            result.Type = SmsType.IncomingPayment;
            result.Confidence = CalculateConfidence(incomingScore, outgoingScore, result.Amount.HasValue);
            result.Reason = $"เงินเข้า (คะแนน: {incomingScore:F2} vs {outgoingScore:F2})";
        }
        else if (outgoingScore > incomingScore && outgoingScore > 0)
        {
            result.Type = SmsType.OutgoingPayment;
            result.Confidence = CalculateConfidence(outgoingScore, incomingScore, result.Amount.HasValue);
            result.Reason = $"เงินออก (คะแนน: {outgoingScore:F2} vs {incomingScore:F2})";
        }
        else if (result.Amount.HasValue)
        {
            // Has amount but no clear direction - might be payment but uncertain
            result.Type = SmsType.OtherBankNotification;
            result.Confidence = 0.5;
            result.Reason = "พบยอดเงินแต่ไม่ชัดว่าเข้าหรือออก";
        }
        else
        {
            result.Type = SmsType.OtherBankNotification;
            result.Confidence = 0.4;
            result.Reason = "แจ้งเตือนจากธนาคารอื่นๆ";
        }

        return result;
    }

    private double CalculateKeywordScore(string body, string[] keywords)
    {
        double score = 0;
        foreach (var keyword in keywords)
        {
            if (body.Contains(keyword.ToLower()))
            {
                // Weight by keyword length (longer keywords are more specific)
                score += 1.0 + (keyword.Length * 0.1);
            }
        }
        return score;
    }

    private double CalculateConfidence(double primaryScore, double oppositeScore, bool hasAmount)
    {
        var baseConfidence = 0.6;

        // Higher confidence if score is clear winner
        var scoreDiff = primaryScore - oppositeScore;
        baseConfidence += Math.Min(0.25, scoreDiff * 0.05);

        // Boost if amount was found
        if (hasAmount)
        {
            baseConfidence += 0.1;
        }

        return Math.Min(0.99, baseConfidence);
    }

    public bool IsFromBank(string sender)
    {
        var upperSender = sender.ToUpper();
        return BankSenders.Values.Any(keywords =>
            keywords.Any(k => upperSender.Contains(k.ToUpper())));
    }

    public string? DetectBankName(string sender, string body)
    {
        var text = $"{sender} {body}".ToUpper();

        foreach (var (bankName, keywords) in BankSenders)
        {
            if (keywords.Any(k => text.Contains(k.ToUpper())))
            {
                return bankName;
            }
        }

        return null;
    }

    public PaymentInfo? ExtractPaymentInfo(ReceivedSmsMessage sms)
    {
        var classification = ClassifySms(sms);

        if (classification.Type != SmsType.IncomingPayment)
        {
            return null;
        }

        var amount = classification.Amount ?? ExtractAmount(sms.Body);
        if (!amount.HasValue || amount <= 0)
        {
            return null;
        }

        return new PaymentInfo
        {
            Id = Guid.NewGuid().ToString(),
            BankName = classification.BankName ?? "Unknown",
            Amount = amount.Value,
            Currency = "THB",
            Type = PaymentType.Incoming,
            TransactionTime = sms.ReceivedAt,
            Reference = ExtractReference(sms.Body),
            RawMessage = sms.Body,
            Status = PaymentStatus.Pending,
            ConfidenceScore = classification.Confidence
        };
    }

    private decimal? ExtractAmount(string body)
    {
        var match = AmountRegex.Match(body);
        if (match.Success)
        {
            var amountStr = match.Groups[1].Success
                ? match.Groups[1].Value
                : match.Groups[2].Value;

            amountStr = amountStr.Replace(",", "");
            if (decimal.TryParse(amountStr, out var amount) && amount > 0)
            {
                return amount;
            }
        }

        // Fallback pattern
        var fallbackMatch = AmountWithContextRegex.Match(body);
        if (fallbackMatch.Success)
        {
            var amountStr = fallbackMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(amountStr, out var amount) && amount > 0)
            {
                return amount;
            }
        }

        return null;
    }

    private string? ExtractReference(string body)
    {
        var refPatterns = new[]
        {
            @"(?:ref\.?|อ้างอิง|เลขที่|รหัส)[\s.:]*([A-Za-z0-9]+)",
            @"#([A-Za-z0-9]+)"
        };

        foreach (var pattern in refPatterns)
        {
            var match = Regex.Match(body, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }
}
