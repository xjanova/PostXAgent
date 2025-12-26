using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// AI-powered payment detection service
/// Analyzes SMS messages to detect and extract payment information
/// Now supports multi-website webhook dispatching
/// </summary>
public class PaymentDetectionService : IPaymentDetectionService
{
    private readonly ISmsListenerService _smsListener;
    private readonly IAIManagerApiService _apiService;
    private readonly ISettingsService _settingsService;
    private readonly ISmsClassifierService? _smsClassifier;
    private readonly IWebhookDispatchService? _webhookDispatch;
    private readonly ConcurrentDictionary<string, PaymentInfo> _payments = new();
    private bool _isAutoDetecting;

    public event EventHandler<PaymentInfo>? PaymentDetected;

    // Regex patterns for Thai bank SMS parsing
    private static readonly Regex AmountPattern = new(
        @"(?:จำนวน|ยอด(?:เงิน)?|amount|โอน|รับ|หัก|เติม|บาท)[\s:]*([0-9,]+\.?[0-9]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AccountPattern = new(
        @"(?:บัญชี|a/c|acct?|เลขที่)[\s.:]*[xX*]*(\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReferencePattern = new(
        @"(?:ref\.?|อ้างอิง|เลขที่|รหัส)[\s.:]*([A-Za-z0-9]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TimePattern = new(
        @"(\d{1,2}[/:]\d{2}(?:[/:]\d{2})?(?:\s*[APap][Mm])?)",
        RegexOptions.Compiled);

    private static readonly Regex DatePattern = new(
        @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
        RegexOptions.Compiled);

    // Keywords indicating incoming payment
    private static readonly string[] IncomingKeywords =
    {
        "รับโอน", "เงินเข้า", "รับเงิน", "โอนเข้า", "credit", "received",
        "deposit", "เติมเงิน", "topup", "top up", "คืนเงิน", "refund"
    };

    // Keywords indicating outgoing payment
    private static readonly string[] OutgoingKeywords =
    {
        "โอนเงิน", "หักบัญชี", "จ่าย", "ถอน", "debit", "withdraw", "transfer",
        "payment", "ชำระ", "ซื้อ", "purchase"
    };

    public PaymentDetectionService(
        ISmsListenerService smsListener,
        IAIManagerApiService apiService,
        ISettingsService settingsService,
        ISmsClassifierService? smsClassifier = null,
        IWebhookDispatchService? webhookDispatch = null)
    {
        _smsListener = smsListener;
        _apiService = apiService;
        _settingsService = settingsService;
        _smsClassifier = smsClassifier;
        _webhookDispatch = webhookDispatch;
    }

    public async Task StartAutoDetectionAsync()
    {
        if (_isAutoDetecting) return;

        _smsListener.SmsReceived += OnSmsReceived;
        await _smsListener.StartAsync();
        _isAutoDetecting = true;
    }

    public async Task StopAutoDetectionAsync()
    {
        if (!_isAutoDetecting) return;

        _smsListener.SmsReceived -= OnSmsReceived;
        await _smsListener.StopAsync();
        _isAutoDetecting = false;
    }

    private async void OnSmsReceived(object? sender, ReceivedSmsMessage sms)
    {
        // Use new SMS classifier if available
        if (_smsClassifier != null)
        {
            var classification = _smsClassifier.ClassifySms(sms);

            // Only process incoming payments
            if (!classification.ShouldProcess)
            {
                // Log why it was skipped
                System.Diagnostics.Debug.WriteLine(
                    $"SMS skipped: {classification.Type} ({classification.Reason})");
                return;
            }

            // Extract payment info using classifier
            var paymentInfo = _smsClassifier.ExtractPaymentInfo(sms);
            if (paymentInfo != null && paymentInfo.ConfidenceScore >= _settingsService.ConfidenceThreshold)
            {
                _payments[paymentInfo.Id] = paymentInfo;
                MainThread.BeginInvokeOnMainThread(() => PaymentDetected?.Invoke(this, paymentInfo));

                // Dispatch to configured websites (if multi-website service available)
                if (_webhookDispatch != null)
                {
                    var result = await _webhookDispatch.DispatchPaymentAsync(paymentInfo);

                    if (result.IsMatched)
                    {
                        paymentInfo.Status = PaymentStatus.Approved;
                        System.Diagnostics.Debug.WriteLine(
                            $"Payment matched at {result.MatchedWebsiteName}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "Payment not matched at any website - pending admin review");
                    }
                }
                // Fallback to old API service
                else if (_settingsService.IsAutoApproveEnabled && paymentInfo.ConfidenceScore >= 0.95)
                {
                    await ApprovePaymentAsync(paymentInfo.Id);
                }
            }
            return;
        }

        // Fallback to legacy detection
        var payment = await AnalyzeSmsAsync(sms);
        if (payment != null && payment.ConfidenceScore >= _settingsService.ConfidenceThreshold)
        {
            _payments[payment.Id] = payment;
            MainThread.BeginInvokeOnMainThread(() => PaymentDetected?.Invoke(this, payment));

            // Auto-approve if enabled and confidence is high
            if (_settingsService.IsAutoApproveEnabled && payment.ConfidenceScore >= 0.95)
            {
                await ApprovePaymentAsync(payment.Id);
            }
        }
    }

    public Task<PaymentInfo?> AnalyzeSmsAsync(ReceivedSmsMessage sms)
    {
        return Task.Run(() =>
        {
            var body = sms.Body;
            var sender = sms.Sender;

            // Check if this looks like a bank SMS
            var bankName = DetectBank(sender, body);
            if (string.IsNullOrEmpty(bankName))
            {
                return null;
            }

            // Determine payment type
            var paymentType = DetectPaymentType(body);
            if (paymentType == PaymentType.Unknown)
            {
                return null;
            }

            // Extract amount
            var amount = ExtractAmount(body);
            if (amount <= 0)
            {
                return null;
            }

            // Calculate confidence score
            var confidenceScore = CalculateConfidence(body, bankName, amount);

            var payment = new PaymentInfo
            {
                Id = Guid.NewGuid().ToString(),
                BankName = bankName,
                AccountNumber = ExtractAccountNumber(body),
                Amount = amount,
                Currency = "THB",
                Type = paymentType,
                TransactionTime = ExtractTransactionTime(body, sms.ReceivedAt),
                Reference = ExtractReference(body),
                RawMessage = body,
                Status = PaymentStatus.Pending,
                IsVerified = false,
                ConfidenceScore = confidenceScore
            };

            // Mark SMS category
            sms.Category = paymentType == PaymentType.Incoming
                ? SmsCategory.PaymentReceived
                : SmsCategory.PaymentSent;
            sms.PaymentInfo = payment;

            return payment;
        });
    }

    private string DetectBank(string sender, string body)
    {
        var text = $"{sender} {body}".ToLower();

        foreach (var (bankName, keywords) in ThaiBank.Keywords)
        {
            if (keywords.Any(k => text.Contains(k.ToLower())))
            {
                return bankName;
            }
        }

        return string.Empty;
    }

    private PaymentType DetectPaymentType(string body)
    {
        var lowerBody = body.ToLower();

        if (IncomingKeywords.Any(k => lowerBody.Contains(k.ToLower())))
        {
            return PaymentType.Incoming;
        }

        if (OutgoingKeywords.Any(k => lowerBody.Contains(k.ToLower())))
        {
            return PaymentType.Outgoing;
        }

        // Check for generic transfer keywords
        if (lowerBody.Contains("โอน") || lowerBody.Contains("transfer"))
        {
            return PaymentType.Transfer;
        }

        return PaymentType.Unknown;
    }

    private decimal ExtractAmount(string body)
    {
        var match = AmountPattern.Match(body);
        if (match.Success)
        {
            var amountStr = match.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(amountStr, out var amount))
            {
                return amount;
            }
        }

        // Fallback: look for numbers followed by "บาท" or "THB"
        var fallbackPattern = new Regex(@"([0-9,]+\.?[0-9]*)\s*(?:บาท|THB|฿)", RegexOptions.IgnoreCase);
        var fallbackMatch = fallbackPattern.Match(body);
        if (fallbackMatch.Success)
        {
            var amountStr = fallbackMatch.Groups[1].Value.Replace(",", "");
            if (decimal.TryParse(amountStr, out var amount))
            {
                return amount;
            }
        }

        return 0;
    }

    private string ExtractAccountNumber(string body)
    {
        var match = AccountPattern.Match(body);
        return match.Success ? $"****{match.Groups[1].Value}" : "Unknown";
    }

    private string? ExtractReference(string body)
    {
        var match = ReferencePattern.Match(body);
        return match.Success ? match.Groups[1].Value : null;
    }

    private DateTime ExtractTransactionTime(string body, DateTime fallback)
    {
        var timeMatch = TimePattern.Match(body);
        var dateMatch = DatePattern.Match(body);

        if (timeMatch.Success || dateMatch.Success)
        {
            try
            {
                var dateStr = dateMatch.Success ? dateMatch.Groups[1].Value : fallback.ToString("dd/MM/yyyy");
                var timeStr = timeMatch.Success ? timeMatch.Groups[1].Value : fallback.ToString("HH:mm");

                if (DateTime.TryParse($"{dateStr} {timeStr}", out var result))
                {
                    return result;
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        return fallback;
    }

    private double CalculateConfidence(string body, string bankName, decimal amount)
    {
        double score = 0.5; // Base score

        // Bank detected
        if (!string.IsNullOrEmpty(bankName)) score += 0.2;

        // Amount found
        if (amount > 0) score += 0.15;

        // Reference number found
        if (ReferencePattern.IsMatch(body)) score += 0.1;

        // Account number found
        if (AccountPattern.IsMatch(body)) score += 0.05;

        // Time/Date found
        if (TimePattern.IsMatch(body) || DatePattern.IsMatch(body)) score += 0.05;

        return Math.Min(1.0, score);
    }

    public Task<List<PaymentInfo>> GetAllPaymentsAsync()
    {
        return Task.FromResult(_payments.Values.OrderByDescending(p => p.TransactionTime).ToList());
    }

    public Task<List<PaymentInfo>> GetPendingPaymentsAsync()
    {
        return Task.FromResult(_payments.Values
            .Where(p => p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.TransactionTime)
            .ToList());
    }

    public Task<bool> VerifyPaymentAsync(string paymentId)
    {
        if (_payments.TryGetValue(paymentId, out var payment))
        {
            payment.IsVerified = true;
            payment.Status = PaymentStatus.Verified;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public async Task<bool> ApprovePaymentAsync(string paymentId)
    {
        if (_payments.TryGetValue(paymentId, out var payment))
        {
            payment.Status = PaymentStatus.Approved;

            // Submit to AI Manager Core
            var result = await _apiService.SubmitPaymentAsync(payment);
            if (result.Success)
            {
                await _apiService.ApprovePaymentAsync(paymentId);
            }

            return result.Success;
        }
        return false;
    }

    public Task<bool> RejectPaymentAsync(string paymentId, string reason)
    {
        if (_payments.TryGetValue(paymentId, out var payment))
        {
            payment.Status = PaymentStatus.Rejected;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}
