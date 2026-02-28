using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services.Gpu;

/// <summary>
/// GPU Cloud Provider Service
/// Handles communication with Laravel backend API for GPU provisioning
/// </summary>
public class GpuProviderService : IGpuProviderService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GpuProviderService>? _logger;
    private readonly string _baseUrl;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;

    public event EventHandler<AccountRotationEvent>? AccountRotated;
    public event EventHandler<InstanceStateChangeEvent>? InstanceStateChanged;

    /// <summary>
    /// Create a new GPU Provider Service
    /// </summary>
    /// <param name="baseUrl">Base URL of Laravel API (e.g., http://localhost:8000)</param>
    /// <param name="authToken">Optional authentication token</param>
    /// <param name="logger">Optional logger</param>
    public GpuProviderService(
        string? baseUrl = null,
        string? authToken = null,
        ILogger<GpuProviderService>? logger = null)
    {
        _baseUrl = baseUrl ?? GetDefaultBaseUrl();
        _logger = logger;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        };

        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");

        if (!string.IsNullOrEmpty(authToken))
        {
            SetAuthToken(authToken);
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Create service with custom HttpClient for testing
    /// </summary>
    public GpuProviderService(HttpClient httpClient, ILogger<GpuProviderService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = httpClient.BaseAddress?.ToString() ?? "http://localhost:8000";
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Set authentication token for API requests
    /// </summary>
    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Remove("Authorization");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    }

    private static string GetDefaultBaseUrl()
    {
        return Environment.GetEnvironmentVariable("LARAVEL_API_URL")
            ?? Environment.GetEnvironmentVariable("AI_MANAGER_API_URL")
            ?? "http://localhost:8000";
    }

    #region Account Management

    public async Task<ApiResponse<List<GpuCloudAccount>>> ListAccountsAsync(
        string? provider = null,
        string? status = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(provider)) query.Add($"provider={Uri.EscapeDataString(provider)}");
            if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");

            var url = "api/v1/gpu/accounts";
            if (query.Count > 0) url += "?" + string.Join("&", query);

            _logger?.LogDebug("Fetching GPU accounts from: {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            return await HandleResponseAsync<List<GpuCloudAccount>>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list GPU accounts");
            return CreateErrorResponse<List<GpuCloudAccount>>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuCloudAccount>> GetAccountAsync(int accountId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/gpu/accounts/{accountId}", ct);
            return await HandleResponseAsync<GpuCloudAccount>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get GPU account {AccountId}", accountId);
            return CreateErrorResponse<GpuCloudAccount>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuCloudAccount>> CreateAccountAsync(
        CreateGpuAccountRequest request,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Creating GPU account for provider: {Provider}", request.Provider);

            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync("api/v1/gpu/accounts", content, ct);
            return await HandleResponseAsync<GpuCloudAccount>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create GPU account");
            return CreateErrorResponse<GpuCloudAccount>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuCloudAccount>> UpdateAccountAsync(
        int accountId,
        UpdateGpuAccountRequest request,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Updating GPU account: {AccountId}", accountId);

            var content = CreateJsonContent(request);
            var response = await _httpClient.PutAsync($"api/v1/gpu/accounts/{accountId}", content, ct);
            return await HandleResponseAsync<GpuCloudAccount>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update GPU account {AccountId}", accountId);
            return CreateErrorResponse<GpuCloudAccount>(ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeleteAccountAsync(int accountId, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Deleting GPU account: {AccountId}", accountId);

            var response = await _httpClient.DeleteAsync($"api/v1/gpu/accounts/{accountId}", ct);
            var result = await HandleResponseAsync<object>(response, ct);
            return new ApiResponse<bool>
            {
                Success = result.Success,
                Data = result.Success,
                Message = result.Message,
                Errors = result.Errors
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete GPU account {AccountId}", accountId);
            return CreateErrorResponse<bool>(ex.Message);
        }
    }

    public async Task<ApiResponse<AccountVerificationResult>> VerifyAccountAsync(
        int accountId,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Verifying GPU account: {AccountId}", accountId);

            var response = await _httpClient.PostAsync($"api/v1/gpu/accounts/{accountId}/verify", null, ct);
            return await HandleResponseAsync<AccountVerificationResult>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to verify GPU account {AccountId}", accountId);
            return CreateErrorResponse<AccountVerificationResult>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuCloudAccount>> RefreshAccountStatusAsync(
        int accountId,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Refreshing GPU account status: {AccountId}", accountId);

            var response = await _httpClient.PostAsync($"api/v1/gpu/accounts/{accountId}/refresh", null, ct);
            return await HandleResponseAsync<GpuCloudAccount>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh GPU account status {AccountId}", accountId);
            return CreateErrorResponse<GpuCloudAccount>(ex.Message);
        }
    }

    #endregion

    #region Pool Management

    public async Task<ApiResponse<List<GpuAccountPool>>> ListPoolsAsync(
        string? provider = null,
        CancellationToken ct = default)
    {
        try
        {
            var url = "api/v1/gpu/pools";
            if (!string.IsNullOrEmpty(provider))
            {
                url += $"?provider={Uri.EscapeDataString(provider)}";
            }

            _logger?.LogDebug("Fetching GPU pools from: {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            return await HandleResponseAsync<List<GpuAccountPool>>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list GPU pools");
            return CreateErrorResponse<List<GpuAccountPool>>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuAccountPool>> GetPoolAsync(
        int poolId,
        bool includeAccounts = true,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/v1/gpu/pools/{poolId}";
            if (includeAccounts) url += "?include=accounts";

            var response = await _httpClient.GetAsync(url, ct);
            return await HandleResponseAsync<GpuAccountPool>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get GPU pool {PoolId}", poolId);
            return CreateErrorResponse<GpuAccountPool>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuAccountPool>> CreatePoolAsync(
        CreateGpuPoolRequest request,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Creating GPU pool: {Name} for provider: {Provider}",
                request.Name, request.Provider);

            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync("api/v1/gpu/pools", content, ct);
            return await HandleResponseAsync<GpuAccountPool>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create GPU pool");
            return CreateErrorResponse<GpuAccountPool>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuAccountPool>> UpdatePoolAsync(
        int poolId,
        CreateGpuPoolRequest request,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Updating GPU pool: {PoolId}", poolId);

            var content = CreateJsonContent(request);
            var response = await _httpClient.PutAsync($"api/v1/gpu/pools/{poolId}", content, ct);
            return await HandleResponseAsync<GpuAccountPool>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update GPU pool {PoolId}", poolId);
            return CreateErrorResponse<GpuAccountPool>(ex.Message);
        }
    }

    public async Task<ApiResponse<bool>> DeletePoolAsync(int poolId, CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Deleting GPU pool: {PoolId}", poolId);

            var response = await _httpClient.DeleteAsync($"api/v1/gpu/pools/{poolId}", ct);
            var result = await HandleResponseAsync<object>(response, ct);
            return new ApiResponse<bool>
            {
                Success = result.Success,
                Data = result.Success,
                Message = result.Message,
                Errors = result.Errors
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete GPU pool {PoolId}", poolId);
            return CreateErrorResponse<bool>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuAccountPool>> AddAccountsToPoolAsync(
        int poolId,
        List<int> accountIds,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Adding {Count} accounts to pool {PoolId}", accountIds.Count, poolId);

            var request = new ModifyPoolAccountsRequest { AccountIds = accountIds };
            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync($"api/v1/gpu/pools/{poolId}/accounts", content, ct);
            return await HandleResponseAsync<GpuAccountPool>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add accounts to pool {PoolId}", poolId);
            return CreateErrorResponse<GpuAccountPool>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuAccountPool>> RemoveAccountsFromPoolAsync(
        int poolId,
        List<int> accountIds,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Removing {Count} accounts from pool {PoolId}", accountIds.Count, poolId);

            var request = new ModifyPoolAccountsRequest { AccountIds = accountIds };
            var content = CreateJsonContent(request);

            // Use DELETE with body via SendAsync
            using var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/gpu/pools/{poolId}/accounts")
            {
                Content = content
            };
            var response = await _httpClient.SendAsync(httpRequest, ct);
            return await HandleResponseAsync<GpuAccountPool>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to remove accounts from pool {PoolId}", poolId);
            return CreateErrorResponse<GpuAccountPool>(ex.Message);
        }
    }

    public async Task<ApiResponse<AccountRotationEvent>> RotatePoolAccountAsync(
        int poolId,
        string? reason = null,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Rotating account in pool {PoolId}. Reason: {Reason}", poolId, reason);

            var request = new { reason };
            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync($"api/v1/gpu/pools/{poolId}/rotate", content, ct);
            var result = await HandleResponseAsync<AccountRotationEvent>(response, ct);

            if (result.Success && result.Data != null)
            {
                AccountRotated?.Invoke(this, result.Data);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to rotate account in pool {PoolId}", poolId);
            return CreateErrorResponse<AccountRotationEvent>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuCloudAccount>> GetCurrentPoolAccountAsync(
        int poolId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/gpu/pools/{poolId}/current", ct);
            return await HandleResponseAsync<GpuCloudAccount>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get current account for pool {PoolId}", poolId);
            return CreateErrorResponse<GpuCloudAccount>(ex.Message);
        }
    }

    #endregion

    #region Offer Search

    public async Task<ApiResponse<List<GpuOffer>>> SearchOffersAsync(
        GpuOfferSearchCriteria criteria,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("Searching GPU offers with criteria: {@Criteria}", criteria);

            var content = CreateJsonContent(criteria);
            var response = await _httpClient.PostAsync("api/v1/gpu/offers/search", content, ct);
            return await HandleResponseAsync<List<GpuOffer>>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to search GPU offers");
            return CreateErrorResponse<List<GpuOffer>>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuOffer>> GetOfferAsync(
        string provider,
        string offerId,
        CancellationToken ct = default)
    {
        try
        {
            var url = $"api/v1/gpu/offers/{Uri.EscapeDataString(provider)}/{Uri.EscapeDataString(offerId)}";
            var response = await _httpClient.GetAsync(url, ct);
            return await HandleResponseAsync<GpuOffer>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get GPU offer {Provider}/{OfferId}", provider, offerId);
            return CreateErrorResponse<GpuOffer>(ex.Message);
        }
    }

    public async Task<ApiResponse<List<GpuOffer>>> RefreshOffersAsync(
        string provider,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Refreshing GPU offers for provider: {Provider}", provider);

            var url = $"api/v1/gpu/offers/refresh?provider={Uri.EscapeDataString(provider)}";
            var response = await _httpClient.PostAsync(url, null, ct);
            return await HandleResponseAsync<List<GpuOffer>>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh GPU offers for {Provider}", provider);
            return CreateErrorResponse<List<GpuOffer>>(ex.Message);
        }
    }

    #endregion

    #region Instance Management

    public async Task<ApiResponse<List<GpuInstance>>> ListInstancesAsync(
        string? status = null,
        int? accountId = null,
        int? poolId = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = new List<string>();
            if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (accountId.HasValue) query.Add($"account_id={accountId}");
            if (poolId.HasValue) query.Add($"pool_id={poolId}");

            var url = "api/v1/gpu/instances";
            if (query.Count > 0) url += "?" + string.Join("&", query);

            _logger?.LogDebug("Fetching GPU instances from: {Url}", url);

            var response = await _httpClient.GetAsync(url, ct);
            return await HandleResponseAsync<List<GpuInstance>>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list GPU instances");
            return CreateErrorResponse<List<GpuInstance>>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuInstance>> GetInstanceAsync(int instanceId, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/gpu/instances/{instanceId}", ct);
            return await HandleResponseAsync<GpuInstance>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get GPU instance {InstanceId}", instanceId);
            return CreateErrorResponse<GpuInstance>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuInstance>> ProvisionInstanceAsync(
        ProvisionInstanceRequest request,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Provisioning GPU instance from offer: {OfferId}", request.OfferId);

            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync("api/v1/gpu/instances", content, ct);
            var result = await HandleResponseAsync<GpuInstance>(response, ct);

            if (result.Success && result.Data != null)
            {
                RaiseInstanceStateChanged(result.Data.Id, null, "creating", "Instance provisioning started");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to provision GPU instance");
            return CreateErrorResponse<GpuInstance>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuInstance>> StartInstanceAsync(
        int instanceId,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Starting GPU instance: {InstanceId}", instanceId);

            var response = await _httpClient.PostAsync($"api/v1/gpu/instances/{instanceId}/start", null, ct);
            var result = await HandleResponseAsync<GpuInstance>(response, ct);

            if (result.Success && result.Data != null)
            {
                RaiseInstanceStateChanged(instanceId, "stopped", "starting", "Instance starting");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start GPU instance {InstanceId}", instanceId);
            return CreateErrorResponse<GpuInstance>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuInstance>> StopInstanceAsync(
        int instanceId,
        bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Stopping GPU instance: {InstanceId} (force: {Force})", instanceId, force);

            var request = new InstanceActionRequest { Force = force };
            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync($"api/v1/gpu/instances/{instanceId}/stop", content, ct);
            var result = await HandleResponseAsync<GpuInstance>(response, ct);

            if (result.Success && result.Data != null)
            {
                RaiseInstanceStateChanged(instanceId, "running", "stopping", "Instance stopping");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to stop GPU instance {InstanceId}", instanceId);
            return CreateErrorResponse<GpuInstance>(ex.Message);
        }
    }

    public async Task<ApiResponse<GpuInstance>> TerminateInstanceAsync(
        int instanceId,
        bool force = false,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Terminating GPU instance: {InstanceId} (force: {Force})", instanceId, force);

            var request = new InstanceActionRequest { Force = force };
            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync($"api/v1/gpu/instances/{instanceId}/terminate", content, ct);
            var result = await HandleResponseAsync<GpuInstance>(response, ct);

            if (result.Success && result.Data != null)
            {
                RaiseInstanceStateChanged(instanceId, result.Data.Status, "terminating", "Instance terminating");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to terminate GPU instance {InstanceId}", instanceId);
            return CreateErrorResponse<GpuInstance>(ex.Message);
        }
    }

    public async Task<ApiResponse<InstanceHealthCheck>> GetInstanceHealthAsync(
        int instanceId,
        CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"api/v1/gpu/instances/{instanceId}/health", ct);
            return await HandleResponseAsync<InstanceHealthCheck>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get health for GPU instance {InstanceId}", instanceId);
            return CreateErrorResponse<InstanceHealthCheck>(ex.Message);
        }
    }

    public async Task<ApiResponse<string>> ExecuteCommandAsync(
        int instanceId,
        string command,
        int timeoutSeconds = 60,
        CancellationToken ct = default)
    {
        try
        {
            _logger?.LogDebug("Executing command on instance {InstanceId}: {Command}", instanceId, command);

            var request = new { command, timeout = timeoutSeconds };
            var content = CreateJsonContent(request);
            var response = await _httpClient.PostAsync($"api/v1/gpu/instances/{instanceId}/execute", content, ct);
            return await HandleResponseAsync<string>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to execute command on instance {InstanceId}", instanceId);
            return CreateErrorResponse<string>(ex.Message);
        }
    }

    #endregion

    #region Statistics

    public async Task<ApiResponse<GpuUsageStats>> GetUsageStatsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken ct = default)
    {
        try
        {
            var query = new List<string>();
            if (startDate.HasValue) query.Add($"start_date={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue) query.Add($"end_date={endDate.Value:yyyy-MM-dd}");

            var url = "api/v1/gpu/stats";
            if (query.Count > 0) url += "?" + string.Join("&", query);

            var response = await _httpClient.GetAsync(url, ct);
            return await HandleResponseAsync<GpuUsageStats>(response, ct);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get GPU usage stats");
            return CreateErrorResponse<GpuUsageStats>(ex.Message);
        }
    }

    #endregion

    #region Helper Methods

    private StringContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<ApiResponse<T>> HandleResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        _logger?.LogDebug("API Response [{StatusCode}]: {Content}",
            (int)response.StatusCode, content.Length > 500 ? content[..500] + "..." : content);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
                return result ?? new ApiResponse<T>
                {
                    Success = true,
                    Message = "Request completed successfully"
                };
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Failed to deserialize successful response");

                // Try to deserialize just the data
                try
                {
                    var data = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                    return new ApiResponse<T>
                    {
                        Success = true,
                        Data = data
                    };
                }
                catch
                {
                    return new ApiResponse<T>
                    {
                        Success = true,
                        Message = content
                    };
                }
            }
        }

        // Handle error response
        try
        {
            var errorResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
            if (errorResponse != null)
            {
                errorResponse.Success = false;
                return errorResponse;
            }
        }
        catch
        {
            // Ignore deserialization errors for error responses
        }

        return new ApiResponse<T>
        {
            Success = false,
            Message = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
            Errors = new List<string> { content }
        };
    }

    private static ApiResponse<T> CreateErrorResponse<T>(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { message }
        };
    }

    private void RaiseInstanceStateChanged(int instanceId, string? oldStatus, string newStatus, string? message)
    {
        InstanceStateChanged?.Invoke(this, new InstanceStateChangeEvent
        {
            InstanceId = instanceId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }

    #endregion
}
