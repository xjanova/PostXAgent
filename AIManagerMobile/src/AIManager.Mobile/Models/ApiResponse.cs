using System.Text.Json.Serialization;

namespace AIManager.Mobile.Models;

/// <summary>
/// Standard API response from AI Manager Core
/// </summary>
/// <typeparam name="T">Type of data returned</typeparam>
public class ApiResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("errors")]
    public List<string>? Errors { get; set; }
}

/// <summary>
/// System status from AI Manager Core
/// </summary>
public class SystemStatus
{
    [JsonPropertyName("is_online")]
    public bool IsOnline { get; set; }

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("uptime")]
    public TimeSpan Uptime { get; set; }

    [JsonPropertyName("cpu_usage")]
    public double CpuUsage { get; set; }

    [JsonPropertyName("memory_usage")]
    public double MemoryUsage { get; set; }

    [JsonPropertyName("active_workers")]
    public int ActiveWorkers { get; set; }

    [JsonPropertyName("total_workers")]
    public int TotalWorkers { get; set; }

    [JsonPropertyName("pending_tasks")]
    public int PendingTasks { get; set; }

    [JsonPropertyName("running_tasks")]
    public int RunningTasks { get; set; }

    [JsonPropertyName("completed_today")]
    public int CompletedToday { get; set; }

    [JsonPropertyName("failed_today")]
    public int FailedToday { get; set; }
}

/// <summary>
/// Dashboard statistics
/// </summary>
public class DashboardStats
{
    public int TotalTasks { get; set; }
    public int PendingTasks { get; set; }
    public int RunningTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int ActiveWorkers { get; set; }
    public int TotalWorkers { get; set; }
    public int UnreadSms { get; set; }
    public int PendingPayments { get; set; }
    public decimal TotalPaymentsToday { get; set; }
}

// ConnectionSettings moved to ConnectionSettings.cs
