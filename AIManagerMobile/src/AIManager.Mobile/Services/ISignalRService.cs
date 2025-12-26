using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Interface for SignalR real-time communication
/// </summary>
public interface ISignalRService
{
    /// <summary>
    /// Connection state
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event when connection state changes
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// Event when task status updates
    /// </summary>
    event EventHandler<TaskItem>? TaskUpdated;

    /// <summary>
    /// Event when worker status updates
    /// </summary>
    event EventHandler<WorkerInfo>? WorkerUpdated;

    /// <summary>
    /// Event when new payment is detected
    /// </summary>
    event EventHandler<PaymentInfo>? PaymentReceived;

    /// <summary>
    /// Event when system status updates
    /// </summary>
    event EventHandler<SystemStatus>? SystemStatusUpdated;

    /// <summary>
    /// Connect to SignalR hub
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// Disconnect from SignalR hub
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Set connection settings
    /// </summary>
    void SetConnectionSettings(ConnectionSettings settings);
}
