using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Service for managing website configurations
/// Stores configurations in MAUI SecureStorage
/// </summary>
public class WebsiteConfigService : IWebsiteConfigService
{
    private const string StorageKey = "website_configs";
    private readonly HttpClient _httpClient;
    private List<WebsiteConfig>? _cachedConfigs;

    public WebsiteConfigService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<List<WebsiteConfig>> GetAllWebsitesAsync()
    {
        if (_cachedConfigs != null)
        {
            return _cachedConfigs;
        }

        try
        {
            var json = await SecureStorage.GetAsync(StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                _cachedConfigs = JsonSerializer.Deserialize<List<WebsiteConfig>>(json) ?? new();
            }
            else
            {
                _cachedConfigs = new();
            }
        }
        catch
        {
            _cachedConfigs = new();
        }

        return _cachedConfigs;
    }

    public async Task<List<WebsiteConfig>> GetEnabledWebsitesByPriorityAsync()
    {
        var websites = await GetAllWebsitesAsync();
        return websites
            .Where(w => w.IsEnabled)
            .OrderBy(w => w.Priority)
            .ThenBy(w => w.CreatedAt)
            .ToList();
    }

    public async Task<WebsiteConfig?> GetWebsiteByIdAsync(string id)
    {
        var websites = await GetAllWebsitesAsync();
        return websites.FirstOrDefault(w => w.Id == id);
    }

    public async Task<WebsiteConfig> AddWebsiteAsync(WebsiteConfig website)
    {
        var websites = await GetAllWebsitesAsync();

        // Assign ID if not set
        if (string.IsNullOrEmpty(website.Id))
        {
            website.Id = Guid.NewGuid().ToString();
        }

        // Set creation time
        website.CreatedAt = DateTime.UtcNow;

        // Set priority to last if not specified
        if (website.Priority == 100 && websites.Any())
        {
            website.Priority = websites.Max(w => w.Priority) + 1;
        }

        websites.Add(website);
        await SaveAsync(websites);

        return website;
    }

    public async Task<bool> UpdateWebsiteAsync(WebsiteConfig website)
    {
        var websites = await GetAllWebsitesAsync();
        var index = websites.FindIndex(w => w.Id == website.Id);

        if (index < 0)
        {
            return false;
        }

        websites[index] = website;
        await SaveAsync(websites);

        return true;
    }

    public async Task<bool> DeleteWebsiteAsync(string id)
    {
        var websites = await GetAllWebsitesAsync();
        var removed = websites.RemoveAll(w => w.Id == id);

        if (removed > 0)
        {
            await SaveAsync(websites);
            return true;
        }

        return false;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(WebsiteConfig website)
    {
        try
        {
            // Create test payload
            var testPayload = new WebhookPayload
            {
                Event = "connection.test",
                Payment = new WebhookPaymentData
                {
                    Amount = 0,
                    BankName = "Test",
                    TransactionTime = DateTime.UtcNow
                },
                Device = new WebhookDeviceInfo
                {
                    DeviceId = DeviceInfo.Current.Model,
                    DeviceName = DeviceInfo.Current.Name,
                    AppVersion = AppInfo.Current.VersionString
                }
            };

            var json = JsonSerializer.Serialize(testPayload);
            var signature = website.GenerateSignature(json, testPayload.Timestamp);

            using var request = new HttpRequestMessage(HttpMethod.Post, website.WebhookUrl);
            request.Headers.Add("X-Api-Key", website.ApiKey);
            request.Headers.Add("X-Timestamp", testPayload.Timestamp.ToString());
            request.Headers.Add("X-Signature", signature);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(website.TimeoutSeconds));
            var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                await UpdateWebsiteStatusAsync(website.Id, WebsiteConnectionStatus.Connected, true);
                return (true, "เชื่อมต่อสำเร็จ");
            }
            else
            {
                await UpdateWebsiteStatusAsync(website.Id, WebsiteConnectionStatus.Error, false);
                return (false, $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
            }
        }
        catch (TaskCanceledException)
        {
            await UpdateWebsiteStatusAsync(website.Id, WebsiteConnectionStatus.Timeout, false);
            return (false, "Connection timeout");
        }
        catch (HttpRequestException ex)
        {
            await UpdateWebsiteStatusAsync(website.Id, WebsiteConnectionStatus.Disconnected, false);
            return (false, $"ไม่สามารถเชื่อมต่อได้: {ex.Message}");
        }
        catch (Exception ex)
        {
            await UpdateWebsiteStatusAsync(website.Id, WebsiteConnectionStatus.Error, false);
            return (false, $"Error: {ex.Message}");
        }
    }

    public async Task UpdateWebsiteStatusAsync(string id, WebsiteConnectionStatus status, bool success)
    {
        var website = await GetWebsiteByIdAsync(id);
        if (website == null) return;

        website.Status = status;

        if (success)
        {
            website.LastConnectedAt = DateTime.UtcNow;
            website.FailureCount = 0;
        }
        else
        {
            website.FailureCount++;
        }

        await UpdateWebsiteAsync(website);
    }

    public async Task<int> GetEnabledCountAsync()
    {
        var websites = await GetAllWebsitesAsync();
        return websites.Count(w => w.IsEnabled);
    }

    public async Task ReorderPrioritiesAsync(List<string> websiteIdsInOrder)
    {
        var websites = await GetAllWebsitesAsync();

        for (int i = 0; i < websiteIdsInOrder.Count; i++)
        {
            var website = websites.FirstOrDefault(w => w.Id == websiteIdsInOrder[i]);
            if (website != null)
            {
                website.Priority = i + 1;
            }
        }

        await SaveAsync(websites);
    }

    private async Task SaveAsync(List<WebsiteConfig> websites)
    {
        _cachedConfigs = websites;
        var json = JsonSerializer.Serialize(websites);
        await SecureStorage.SetAsync(StorageKey, json);
    }
}
