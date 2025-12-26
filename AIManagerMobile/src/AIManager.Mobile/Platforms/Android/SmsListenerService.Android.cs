#if ANDROID
using Android;
using Android.Content;
using Android.Provider;
using Android.Database;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AIManager.Mobile.Models;
using Android.App;

namespace AIManager.Mobile.Services;

/// <summary>
/// Android-specific SMS Listener implementation
/// </summary>
public partial class SmsListenerService
{
    private SmsBroadcastReceiver? _smsReceiver;

    public partial async Task StartAsync()
    {
        if (_isRunning) return;

        if (!await HasPermissionAsync())
        {
            var granted = await RequestPermissionAsync();
            if (!granted) return;
        }

        // Register SMS broadcast receiver
        _smsReceiver = new SmsBroadcastReceiver(OnSmsReceived);
        var intentFilter = new IntentFilter("android.provider.Telephony.SMS_RECEIVED");
        intentFilter.Priority = 999;

#pragma warning disable CA1416
        Platform.CurrentActivity?.RegisterReceiver(_smsReceiver, intentFilter);
#pragma warning restore CA1416

        _isRunning = true;
    }

    public partial Task StopAsync()
    {
        if (!_isRunning) return Task.CompletedTask;

        if (_smsReceiver != null)
        {
#pragma warning disable CA1416
            Platform.CurrentActivity?.UnregisterReceiver(_smsReceiver);
#pragma warning restore CA1416
            _smsReceiver = null;
        }

        _isRunning = false;
        return Task.CompletedTask;
    }

    public partial async Task<bool> HasPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Sms>();
        return status == PermissionStatus.Granted;
    }

    public partial async Task<bool> RequestPermissionAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Sms>();
        return status == PermissionStatus.Granted;
    }

    private void OnSmsReceived(string sender, string body)
    {
        var message = new ReceivedSmsMessage
        {
            Id = Guid.NewGuid().ToString(),
            Sender = sender,
            Body = body,
            ReceivedAt = DateTime.Now,
            IsProcessed = false
        };

        OnSmsReceived(message);
    }
}

/// <summary>
/// Broadcast receiver for incoming SMS
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" }, Priority = 999)]
public class SmsBroadcastReceiver : BroadcastReceiver
{
    private readonly Action<string, string>? _onSmsReceived;

    public SmsBroadcastReceiver() { }

    public SmsBroadcastReceiver(Action<string, string> onSmsReceived)
    {
        _onSmsReceived = onSmsReceived;
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED") return;

        var bundle = intent.Extras;
        if (bundle == null) return;

        try
        {
            var pdus = bundle.Get("pdus");
            if (pdus == null) return;

            var pduArray = (Java.Lang.Object[])pdus;
            var format = bundle.GetString("format") ?? "3gpp";

            foreach (var pdu in pduArray)
            {
                var bytes = (byte[]?)pdu;
                if (bytes == null) continue;

#pragma warning disable CA1416
                var smsMessage = Android.Telephony.SmsMessage.CreateFromPdu(bytes, format);
#pragma warning restore CA1416
                if (smsMessage == null) continue;

                var sender = smsMessage.OriginatingAddress ?? "Unknown";
                var body = smsMessage.MessageBody ?? "";

                _onSmsReceived?.Invoke(sender, body);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error processing SMS: {ex.Message}");
        }
    }
}
#endif
