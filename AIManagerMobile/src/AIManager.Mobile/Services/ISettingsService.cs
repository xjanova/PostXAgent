using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Interface for app settings persistence
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get connection settings
    /// </summary>
    ConnectionSettings GetConnectionSettings();

    /// <summary>
    /// Save connection settings
    /// </summary>
    void SaveConnectionSettings(ConnectionSettings settings);

    /// <summary>
    /// Get SMS monitoring enabled state
    /// </summary>
    bool IsSmsMonitoringEnabled { get; set; }

    /// <summary>
    /// Get auto-approve payments setting
    /// </summary>
    bool IsAutoApproveEnabled { get; set; }

    /// <summary>
    /// Get notification enabled state
    /// </summary>
    bool IsNotificationsEnabled { get; set; }

    /// <summary>
    /// Get AI detection confidence threshold
    /// </summary>
    double ConfidenceThreshold { get; set; }
}
