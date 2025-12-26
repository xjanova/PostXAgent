using AIManager.Mobile.Models;

namespace AIManager.Mobile.Services;

/// <summary>
/// Interface for AI Manager Core API communication
/// </summary>
public interface IAIManagerApiService
{
    /// <summary>
    /// Get system status from AI Manager Core
    /// </summary>
    Task<ApiResponse<SystemStatus>> GetSystemStatusAsync();

    /// <summary>
    /// Get all tasks
    /// </summary>
    Task<ApiResponse<List<TaskItem>>> GetTasksAsync(int page = 1, int limit = 50);

    /// <summary>
    /// Get task by ID
    /// </summary>
    Task<ApiResponse<TaskItem>> GetTaskAsync(string taskId);

    /// <summary>
    /// Create new task
    /// </summary>
    Task<ApiResponse<TaskItem>> CreateTaskAsync(TaskItem task);

    /// <summary>
    /// Cancel task
    /// </summary>
    Task<ApiResponse<bool>> CancelTaskAsync(string taskId);

    /// <summary>
    /// Get all workers status
    /// </summary>
    Task<ApiResponse<List<WorkerInfo>>> GetWorkersAsync();

    /// <summary>
    /// Start/Resume a worker
    /// </summary>
    Task<ApiResponse<bool>> StartWorkerAsync(string workerId);

    /// <summary>
    /// Pause a worker
    /// </summary>
    Task<ApiResponse<bool>> PauseWorkerAsync(string workerId);

    /// <summary>
    /// Stop a worker
    /// </summary>
    Task<ApiResponse<bool>> StopWorkerAsync(string workerId);

    /// <summary>
    /// Get dashboard statistics
    /// </summary>
    Task<ApiResponse<DashboardStats>> GetDashboardStatsAsync();

    /// <summary>
    /// Submit payment for verification
    /// </summary>
    Task<ApiResponse<bool>> SubmitPaymentAsync(PaymentInfo payment);

    /// <summary>
    /// Approve payment
    /// </summary>
    Task<ApiResponse<bool>> ApprovePaymentAsync(string paymentId);

    /// <summary>
    /// Test connection to AI Manager Core
    /// </summary>
    Task<bool> TestConnectionAsync();

    /// <summary>
    /// Validate API Key with AI Manager Core
    /// </summary>
    Task<ApiResponse<ApiKeyValidationResult>> ValidateApiKeyAsync();

    /// <summary>
    /// Get full connection status including API and SignalR
    /// </summary>
    Task<ConnectionStatus> GetConnectionStatusAsync();

    /// <summary>
    /// Set connection settings
    /// </summary>
    void SetConnectionSettings(ConnectionSettings settings);

    /// <summary>
    /// Event when connection status changes
    /// </summary>
    event EventHandler<ConnectionStatus>? ConnectionStatusChanged;
}
