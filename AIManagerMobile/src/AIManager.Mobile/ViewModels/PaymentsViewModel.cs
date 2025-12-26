using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// Payments ViewModel - Payment management and approval
/// </summary>
public partial class PaymentsViewModel : BaseViewModel
{
    private readonly IPaymentDetectionService _paymentService;
    private readonly IAIManagerApiService _apiService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private int _pendingCount;

    [ObservableProperty]
    private int _approvedCount;

    [ObservableProperty]
    private decimal _totalToday;

    [ObservableProperty]
    private string _filterStatus = "ทั้งหมด";

    [ObservableProperty]
    private bool _autoApproveEnabled;

    [ObservableProperty]
    private double _confidenceThreshold;

    public ObservableCollection<PaymentInfo> Payments { get; } = new();
    public ObservableCollection<string> StatusFilters { get; } = new()
    {
        "ทั้งหมด",
        "รอตรวจสอบ",
        "อนุมัติแล้ว",
        "ปฏิเสธ"
    };

    public PaymentsViewModel(
        IPaymentDetectionService paymentService,
        IAIManagerApiService apiService,
        ISettingsService settingsService)
    {
        _paymentService = paymentService;
        _apiService = apiService;
        _settingsService = settingsService;

        Title = "การชำระเงิน";

        AutoApproveEnabled = _settingsService.IsAutoApproveEnabled;
        ConfidenceThreshold = _settingsService.ConfidenceThreshold;

        _paymentService.PaymentDetected += OnPaymentDetected;
    }

    [RelayCommand]
    private async Task LoadPaymentsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var payments = await _paymentService.GetAllPaymentsAsync();
            Payments.Clear();

            var filtered = FilterPayments(payments);
            foreach (var payment in filtered)
            {
                Payments.Add(payment);
            }

            UpdateCounts(payments);
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadPaymentsAsync();
    }

    [RelayCommand]
    private async Task ApprovePaymentAsync(PaymentInfo payment)
    {
        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Verified)
        {
            ShowMessage("ไม่สามารถอนุมัติได้", "สามารถอนุมัติได้เฉพาะรายการที่รอตรวจสอบเท่านั้น");
            return;
        }

        var confirmed = await ConfirmAsync("ยืนยันการอนุมัติ",
            $"อนุมัติการชำระเงิน {payment.Amount:N2} บาท\n" +
            $"จาก {payment.BankName}\n" +
            $"ความมั่นใจ: {payment.ConfidenceScore:P0}");

        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var success = await _paymentService.ApprovePaymentAsync(payment.Id);
            if (success)
            {
                payment.Status = PaymentStatus.Approved;
                await LoadPaymentsAsync();
                ShowMessage("สำเร็จ", "อนุมัติการชำระเงินเรียบร้อย");
            }
            else
            {
                ShowError("ไม่สามารถอนุมัติการชำระเงินได้");
            }
        });
    }

    [RelayCommand]
    private async Task RejectPaymentAsync(PaymentInfo payment)
    {
        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Verified)
        {
            ShowMessage("ไม่สามารถปฏิเสธได้", "สามารถปฏิเสธได้เฉพาะรายการที่รอตรวจสอบเท่านั้น");
            return;
        }

        var confirmed = await ConfirmAsync("ยืนยันการปฏิเสธ",
            $"ปฏิเสธการชำระเงิน {payment.Amount:N2} บาท?");

        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            var success = await _paymentService.RejectPaymentAsync(payment.Id, "Manual rejection");
            if (success)
            {
                payment.Status = PaymentStatus.Rejected;
                await LoadPaymentsAsync();
            }
        });
    }

    [RelayCommand]
    private async Task VerifyPaymentAsync(PaymentInfo payment)
    {
        await ExecuteAsync(async () =>
        {
            var success = await _paymentService.VerifyPaymentAsync(payment.Id);
            if (success)
            {
                payment.Status = PaymentStatus.Verified;
                payment.IsVerified = true;
                ShowMessage("ตรวจสอบแล้ว", "ยืนยันความถูกต้องของการชำระเงินเรียบร้อย");
                await LoadPaymentsAsync();
            }
        });
    }

    [RelayCommand]
    private async Task ApproveAllPendingAsync()
    {
        var pending = Payments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Verified).ToList();
        if (!pending.Any())
        {
            ShowMessage("ไม่มีรายการ", "ไม่มีรายการที่รอตรวจสอบ");
            return;
        }

        var confirmed = await ConfirmAsync("อนุมัติทั้งหมด",
            $"อนุมัติ {pending.Count} รายการ\n" +
            $"รวม {pending.Sum(p => p.Amount):N2} บาท?");

        if (!confirmed) return;

        await ExecuteAsync(async () =>
        {
            foreach (var payment in pending)
            {
                await _paymentService.ApprovePaymentAsync(payment.Id);
            }
            await LoadPaymentsAsync();
            ShowMessage("สำเร็จ", $"อนุมัติ {pending.Count} รายการเรียบร้อย");
        });
    }

    [RelayCommand]
    private void ViewPaymentDetails(PaymentInfo payment)
    {
        var statusText = payment.Status switch
        {
            PaymentStatus.Pending => "รอตรวจสอบ",
            PaymentStatus.Verified => "ตรวจสอบแล้ว",
            PaymentStatus.Approved => "อนุมัติแล้ว",
            PaymentStatus.Rejected => "ปฏิเสธ",
            PaymentStatus.Error => "มีข้อผิดพลาด",
            _ => "ไม่ทราบ"
        };

        var details = $"ธนาคาร: {payment.BankName}\n" +
                     $"บัญชี: {payment.AccountNumber}\n" +
                     $"จำนวน: {payment.Amount:N2} บาท\n" +
                     $"ประเภท: {(payment.Type == PaymentType.Incoming ? "เงินเข้า" : "เงินออก")}\n" +
                     $"เวลา: {payment.TransactionTime:dd/MM/yyyy HH:mm}\n" +
                     $"อ้างอิง: {payment.Reference ?? "-"}\n" +
                     $"สถานะ: {statusText}\n" +
                     $"ความมั่นใจ: {payment.ConfidenceScore:P0}\n\n" +
                     $"--- ข้อความต้นฉบับ ---\n{payment.RawMessage}";

        ShowMessage("รายละเอียดการชำระเงิน", details);
    }

    partial void OnFilterStatusChanged(string value)
    {
        _ = LoadPaymentsAsync();
    }

    partial void OnAutoApproveEnabledChanged(bool value)
    {
        _settingsService.IsAutoApproveEnabled = value;
    }

    partial void OnConfidenceThresholdChanged(double value)
    {
        _settingsService.ConfidenceThreshold = value;
    }

    private IEnumerable<PaymentInfo> FilterPayments(List<PaymentInfo> payments)
    {
        return FilterStatus switch
        {
            "รอตรวจสอบ" => payments.Where(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Verified),
            "อนุมัติแล้ว" => payments.Where(p => p.Status == PaymentStatus.Approved),
            "ปฏิเสธ" => payments.Where(p => p.Status == PaymentStatus.Rejected),
            _ => payments
        };
    }

    private void UpdateCounts(List<PaymentInfo> payments)
    {
        PendingCount = payments.Count(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Verified);
        ApprovedCount = payments.Count(p => p.Status == PaymentStatus.Approved);
        TotalToday = payments
            .Where(p => p.TransactionTime.Date == DateTime.Today && p.Type == PaymentType.Incoming)
            .Sum(p => p.Amount);
    }

    private void OnPaymentDetected(object? sender, PaymentInfo payment)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            Payments.Insert(0, payment);
            PendingCount++;
            if (payment.Type == PaymentType.Incoming)
            {
                TotalToday += payment.Amount;
            }

            // Show notification
            if (_settingsService.IsNotificationsEnabled)
            {
                ShowMessage("พบการชำระเงินใหม่",
                    $"จำนวน: {payment.Amount:N2} บาท\n" +
                    $"จาก: {payment.BankName}\n" +
                    $"ความมั่นใจ: {payment.ConfidenceScore:P0}");
            }
        });
    }
}
