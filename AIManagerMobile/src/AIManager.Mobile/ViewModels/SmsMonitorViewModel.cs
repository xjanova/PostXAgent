using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIManager.Mobile.Models;
using AIManager.Mobile.Services;

namespace AIManager.Mobile.ViewModels;

/// <summary>
/// SMS Monitor ViewModel - SMS listening and display
/// </summary>
public partial class SmsMonitorViewModel : BaseViewModel
{
    private readonly ISmsListenerService _smsService;
    private readonly IPaymentDetectionService _paymentService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private int _totalMessages;

    [ObservableProperty]
    private int _paymentMessages;

    [ObservableProperty]
    private bool _hasPermission;

    [ObservableProperty]
    private ReceivedSmsMessage? _selectedMessage;

    public ObservableCollection<ReceivedSmsMessage> Messages { get; } = new();

    public SmsMonitorViewModel(
        ISmsListenerService smsService,
        IPaymentDetectionService paymentService,
        ISettingsService settingsService)
    {
        _smsService = smsService;
        _paymentService = paymentService;
        _settingsService = settingsService;

        Title = "ตรวจสอบ SMS";

        _smsService.SmsReceived += OnSmsReceived;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        HasPermission = await _smsService.HasPermissionAsync();
        if (HasPermission)
        {
            await LoadMessagesAsync();
        }
    }

    [RelayCommand]
    private async Task RequestPermissionAsync()
    {
        HasPermission = await _smsService.RequestPermissionAsync();
        if (HasPermission)
        {
            ShowMessage("สำเร็จ", "ได้รับสิทธิ์การอ่าน SMS แล้ว");
            await LoadMessagesAsync();
        }
        else
        {
            ShowError("ไม่สามารถขอสิทธิ์ได้\nกรุณาอนุญาตในการตั้งค่าแอพ");
        }
    }

    [RelayCommand]
    private async Task StartMonitoringAsync()
    {
        if (!HasPermission)
        {
            await RequestPermissionAsync();
            if (!HasPermission) return;
        }

        await ExecuteAsync(async () =>
        {
            await _smsService.StartAsync();
            await _paymentService.StartAutoDetectionAsync();
            IsMonitoring = true;
            _settingsService.IsSmsMonitoringEnabled = true;
        });
    }

    [RelayCommand]
    private async Task StopMonitoringAsync()
    {
        await ExecuteAsync(async () =>
        {
            await _smsService.StopAsync();
            await _paymentService.StopAutoDetectionAsync();
            IsMonitoring = false;
            _settingsService.IsSmsMonitoringEnabled = false;
        });
    }

    [RelayCommand]
    private async Task LoadMessagesAsync()
    {
        await ExecuteAsync(async () =>
        {
            var messages = await _smsService.GetAllMessagesAsync();
            Messages.Clear();
            foreach (var msg in messages)
            {
                Messages.Add(msg);
            }
            TotalMessages = Messages.Count;
            PaymentMessages = Messages.Count(m => m.Category == SmsCategory.PaymentReceived || m.Category == SmsCategory.PaymentSent);
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadMessagesAsync();
    }

    [RelayCommand]
    private async Task AnalyzeMessageAsync(ReceivedSmsMessage message)
    {
        await ExecuteAsync(async () =>
        {
            var payment = await _paymentService.AnalyzeSmsAsync(message);
            if (payment != null)
            {
                message.PaymentInfo = payment;
                message.Category = payment.Type == PaymentType.Incoming
                    ? SmsCategory.PaymentReceived
                    : SmsCategory.PaymentSent;

                ShowMessage("ตรวจพบการชำระเงิน",
                    $"ธนาคาร: {payment.BankName}\n" +
                    $"จำนวน: {payment.Amount:N2} บาท\n" +
                    $"ประเภท: {(payment.Type == PaymentType.Incoming ? "เงินเข้า" : "เงินออก")}\n" +
                    $"ความมั่นใจ: {payment.ConfidenceScore:P0}");
            }
            else
            {
                ShowMessage("ไม่พบข้อมูลการชำระเงิน", "ข้อความนี้ไม่ใช่ SMS แจ้งเตือนจากธนาคาร");
            }
        });
    }

    [RelayCommand]
    private void ViewMessageDetails(ReceivedSmsMessage message)
    {
        SelectedMessage = message;

        var details = $"ผู้ส่ง: {message.Sender}\n" +
                     $"เวลา: {message.ReceivedAt:dd/MM/yyyy HH:mm:ss}\n" +
                     $"หมวดหมู่: {GetCategoryText(message.Category)}\n\n" +
                     $"ข้อความ:\n{message.Body}";

        if (message.PaymentInfo != null)
        {
            details += $"\n\n--- ข้อมูลการชำระเงิน ---\n" +
                      $"ธนาคาร: {message.PaymentInfo.BankName}\n" +
                      $"จำนวน: {message.PaymentInfo.Amount:N2} บาท\n" +
                      $"อ้างอิง: {message.PaymentInfo.Reference ?? "-"}\n" +
                      $"ความมั่นใจ: {message.PaymentInfo.ConfidenceScore:P0}";
        }

        ShowMessage("รายละเอียด SMS", details);
    }

    private string GetCategoryText(SmsCategory category)
    {
        return category switch
        {
            SmsCategory.BankNotification => "แจ้งเตือนธนาคาร",
            SmsCategory.PaymentReceived => "เงินเข้า",
            SmsCategory.PaymentSent => "เงินออก",
            SmsCategory.OTP => "รหัส OTP",
            SmsCategory.Promotion => "โปรโมชั่น",
            SmsCategory.Other => "อื่นๆ",
            _ => "ไม่ทราบ"
        };
    }

    private void OnSmsReceived(object? sender, ReceivedSmsMessage message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Messages.Insert(0, message);
            TotalMessages = Messages.Count;
            if (message.Category == SmsCategory.PaymentReceived || message.Category == SmsCategory.PaymentSent)
            {
                PaymentMessages++;
            }
        });
    }
}
