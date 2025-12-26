using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Interface for SMS listening service
/// </summary>
public interface ISmsListenerService
{
    /// <summary>
    /// Is service running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event when new SMS is received
    /// </summary>
    event EventHandler<ReceivedSmsMessage>? SmsReceived;

    /// <summary>
    /// Start listening for SMS
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop listening for SMS
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Get all received SMS messages
    /// </summary>
    Task<List<ReceivedSmsMessage>> GetAllMessagesAsync();

    /// <summary>
    /// Get unprocessed messages
    /// </summary>
    Task<List<ReceivedSmsMessage>> GetUnprocessedMessagesAsync();

    /// <summary>
    /// Mark message as processed
    /// </summary>
    Task MarkAsProcessedAsync(string messageId);

    /// <summary>
    /// Check if SMS permissions are granted
    /// </summary>
    Task<bool> HasPermissionAsync();

    /// <summary>
    /// Request SMS permissions
    /// </summary>
    Task<bool> RequestPermissionAsync();
}
