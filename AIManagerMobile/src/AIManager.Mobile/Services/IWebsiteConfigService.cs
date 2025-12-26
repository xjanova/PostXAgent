using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for managing website configurations
/// </summary>
public interface IWebsiteConfigService
{
    /// <summary>
    /// Get all configured websites
    /// </summary>
    Task<List<WebsiteConfig>> GetAllWebsitesAsync();

    /// <summary>
    /// Get enabled websites sorted by priority
    /// </summary>
    Task<List<WebsiteConfig>> GetEnabledWebsitesByPriorityAsync();

    /// <summary>
    /// Get a website by ID
    /// </summary>
    Task<WebsiteConfig?> GetWebsiteByIdAsync(string id);

    /// <summary>
    /// Add a new website
    /// </summary>
    Task<WebsiteConfig> AddWebsiteAsync(WebsiteConfig website);

    /// <summary>
    /// Update a website
    /// </summary>
    Task<bool> UpdateWebsiteAsync(WebsiteConfig website);

    /// <summary>
    /// Delete a website
    /// </summary>
    Task<bool> DeleteWebsiteAsync(string id);

    /// <summary>
    /// Test connection to a website
    /// </summary>
    Task<(bool Success, string Message)> TestConnectionAsync(WebsiteConfig website);

    /// <summary>
    /// Update website status after webhook attempt
    /// </summary>
    Task UpdateWebsiteStatusAsync(string id, WebsiteConnectionStatus status, bool success);

    /// <summary>
    /// Get count of enabled websites
    /// </summary>
    Task<int> GetEnabledCountAsync();

    /// <summary>
    /// Reorder website priorities
    /// </summary>
    Task ReorderPrioritiesAsync(List<string> websiteIdsInOrder);
}
