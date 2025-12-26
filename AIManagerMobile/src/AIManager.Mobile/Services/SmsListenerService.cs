using AIManager.Mobile.Models;
using System.Collections.Concurrent;

namespace AIManager.Mobile.Services;

/// <summary>
/// SMS Listener Service - Platform specific implementation via partial class
/// </summary>
public partial class SmsListenerService : ISmsListenerService
{
    private readonly ConcurrentDictionary<string, ReceivedSmsMessage> _messages = new();
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public event EventHandler<ReceivedSmsMessage>? SmsReceived;

    public Task<List<ReceivedSmsMessage>> GetAllMessagesAsync()
    {
        return Task.FromResult(_messages.Values.OrderByDescending(m => m.ReceivedAt).ToList());
    }

    public Task<List<ReceivedSmsMessage>> GetUnprocessedMessagesAsync()
    {
        return Task.FromResult(_messages.Values
            .Where(m => !m.IsProcessed)
            .OrderByDescending(m => m.ReceivedAt)
            .ToList());
    }

    public Task MarkAsProcessedAsync(string messageId)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.IsProcessed = true;
        }
        return Task.CompletedTask;
    }

    protected void OnSmsReceived(ReceivedSmsMessage message)
    {
        _messages[message.Id] = message;
        MainThread.BeginInvokeOnMainThread(() => SmsReceived?.Invoke(this, message));
    }

    // Platform-specific implementations
    public partial Task StartAsync();
    public partial Task StopAsync();
    public partial Task<bool> HasPermissionAsync();
    public partial Task<bool> RequestPermissionAsync();
}
