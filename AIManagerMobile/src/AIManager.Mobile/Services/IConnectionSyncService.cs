namespace AIManager.Mobile.Services;

/// <summary>
/// Interface for connection sync service
/// </summary>
public interface IConnectionSyncService
{
    /// <summary>
    /// Current sync status
    /// </summary>
    SyncStatus CurrentStatus { get; }

    /// <summary>
    /// Is device registered
    /// </summary>
    bool IsRegistered { get; }

    /// <summary>
    /// Event when sync status changes
    /// </summary>
    event EventHandler<SyncStatus>? SyncStatusChanged;

    /// <summary>
    /// Register device with AI Manager Core
    /// </summary>
    Task<bool> RegisterDeviceAsync();

    /// <summary>
    /// Start heartbeat service
    /// </summary>
    Task StartHeartbeatAsync();

    /// <summary>
    /// Stop heartbeat service
    /// </summary>
    Task StopHeartbeatAsync();

    /// <summary>
    /// Force sync now
    /// </summary>
    Task<bool> ForceSyncAsync();

    /// <summary>
    /// Disconnect from server
    /// </summary>
    Task DisconnectAsync();
}

/// <summary>
/// Sync status enum
/// </summary>
public enum SyncStatus
{
    Disconnected,
    Connecting,
    Connected,
    Syncing,
    Synced,
    Error
}
