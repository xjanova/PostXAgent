using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Implementation of AI Manager Core API communication
/// </summary>
public class AIManagerApiService : IAIManagerApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private ConnectionSettings _settings = new();
    private HttpClient? _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public AIManagerApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public void SetConnectionSettings(ConnectionSettings settings)
    {
        _settings = settings;
        _httpClient = null; // Force recreate on next request
    }

    private HttpClient GetClient()
    {
        if (_httpClient == null)
        {
            _httpClient = _httpClientFactory.CreateClient("AIManagerApi");
            _httpClient.BaseAddress = new Uri(_settings.ApiBaseUrl);

            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
            }
        }
        return _httpClient;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await GetClient().GetAsync("/api/status/ping");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ApiResponse<SystemStatus>> GetSystemStatusAsync()
    {
        try
        {
            var response = await GetClient().GetFromJsonAsync<ApiResponse<SystemStatus>>(
                "/api/status", JsonOptions);
            return response ?? new ApiResponse<SystemStatus> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<SystemStatus>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<TaskItem>>> GetTasksAsync(int page = 1, int limit = 50)
    {
        try
        {
            var response = await GetClient().GetFromJsonAsync<ApiResponse<List<TaskItem>>>(
                $"/api/tasks?page={page}&limit={limit}", JsonOptions);
            return response ?? new ApiResponse<List<TaskItem>> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<TaskItem>>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<TaskItem>> GetTaskAsync(string taskId)
    {
        try
        {
            var response = await GetClient().GetFromJsonAsync<ApiResponse<TaskItem>>(
                $"/api/tasks/{taskId}", JsonOptions);
            return response ?? new ApiResponse<TaskItem> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TaskItem>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<TaskItem>> CreateTaskAsync(TaskItem task)
    {
        try
        {
            var response = await GetClient().PostAsJsonAsync("/api/tasks", task, JsonOptions);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<TaskItem>>(JsonOptions);
            return result ?? new ApiResponse<TaskItem> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<TaskItem>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> CancelTaskAsync(string taskId)
    {
        try
        {
            var response = await GetClient().PostAsync($"/api/tasks/{taskId}/cancel", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            return result ?? new ApiResponse<bool> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<List<WorkerInfo>>> GetWorkersAsync()
    {
        try
        {
            var response = await GetClient().GetFromJsonAsync<ApiResponse<List<WorkerInfo>>>(
                "/api/workers", JsonOptions);
            return response ?? new ApiResponse<List<WorkerInfo>> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<List<WorkerInfo>>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> StartWorkerAsync(string workerId)
    {
        try
        {
            var response = await GetClient().PostAsync($"/api/workers/{workerId}/start", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            return result ?? new ApiResponse<bool> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> PauseWorkerAsync(string workerId)
    {
        try
        {
            var response = await GetClient().PostAsync($"/api/workers/{workerId}/pause", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            return result ?? new ApiResponse<bool> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> StopWorkerAsync(string workerId)
    {
        try
        {
            var response = await GetClient().PostAsync($"/api/workers/{workerId}/stop", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            return result ?? new ApiResponse<bool> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<DashboardStats>> GetDashboardStatsAsync()
    {
        try
        {
            var response = await GetClient().GetFromJsonAsync<ApiResponse<DashboardStats>>(
                "/api/status/dashboard", JsonOptions);
            return response ?? new ApiResponse<DashboardStats> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<DashboardStats>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> SubmitPaymentAsync(PaymentInfo payment)
    {
        try
        {
            var response = await GetClient().PostAsJsonAsync("/api/payments/submit", payment, JsonOptions);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            return result ?? new ApiResponse<bool> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> ApprovePaymentAsync(string paymentId)
    {
        try
        {
            var response = await GetClient().PostAsync($"/api/payments/{paymentId}/approve", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<bool>>(JsonOptions);
            return result ?? new ApiResponse<bool> { Success = false, Message = "Empty response" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<ApiKeyValidationResult>> ValidateApiKeyAsync()
    {
        try
        {
            var response = await GetClient().GetAsync("/api/status/validate-key");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return new ApiResponse<ApiKeyValidationResult>
                {
                    Success = false,
                    Message = "API Key ไม่ถูกต้อง",
                    Data = new ApiKeyValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "API Key ไม่ถูกต้องหรือหมดอายุ"
                    }
                };
            }

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<ApiKeyValidationResult>>(JsonOptions);
                if (result?.Data != null)
                {
                    result.Data.IsValid = true;
                }
                return result ?? new ApiResponse<ApiKeyValidationResult>
                {
                    Success = true,
                    Data = new ApiKeyValidationResult { IsValid = true }
                };
            }

            return new ApiResponse<ApiKeyValidationResult>
            {
                Success = false,
                Message = $"Server returned {response.StatusCode}",
                Data = new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Server returned {response.StatusCode}"
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ApiKeyValidationResult>
            {
                Success = false,
                Message = ex.Message,
                Data = new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message
                },
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public event EventHandler<ConnectionStatus>? ConnectionStatusChanged;

    private ConnectionStatus _lastStatus = new();

    public async Task<ConnectionStatus> GetConnectionStatusAsync()
    {
        var status = new ConnectionStatus();

        // Test API connection
        try
        {
            var pingResponse = await GetClient().GetAsync("/api/status/ping");
            status.IsApiConnected = pingResponse.IsSuccessStatusCode;

            if (status.IsApiConnected)
            {
                // Validate API Key
                var keyValidation = await ValidateApiKeyAsync();
                status.IsApiKeyValid = keyValidation.Data?.IsValid ?? false;
                status.ServerVersion = keyValidation.Data?.ServerVersion;

                if (!status.IsApiKeyValid)
                {
                    status.LastError = keyValidation.Data?.ErrorMessage ?? "API Key ไม่ถูกต้อง";
                }
            }
            else
            {
                status.LastError = "ไม่สามารถเชื่อมต่อ API ได้";
            }
        }
        catch (Exception ex)
        {
            status.IsApiConnected = false;
            status.LastError = ex.Message;
        }

        // Update last connected time if connected
        if (status.IsConnected)
        {
            status.LastConnectedAt = DateTime.Now;
        }

        // Fire event if status changed
        if (StatusChanged(_lastStatus, status))
        {
            _lastStatus = status;
            ConnectionStatusChanged?.Invoke(this, status);
        }

        return status;
    }

    private static bool StatusChanged(ConnectionStatus old, ConnectionStatus newStatus)
    {
        return old.IsApiConnected != newStatus.IsApiConnected ||
               old.IsApiKeyValid != newStatus.IsApiKeyValid ||
               old.IsSignalRConnected != newStatus.IsSignalRConnected;
    }
}
