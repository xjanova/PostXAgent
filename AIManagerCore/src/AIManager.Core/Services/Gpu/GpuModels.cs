using System.Text.Json.Serialization;

namespace AIManager.Core.Services.Gpu;

// ═══════════════════════════════════════════════════════════════════════════
// GPU PROVISIONING MODELS - สำหรับเชื่อมต่อกับ Laravel API
// ═══════════════════════════════════════════════════════════════════════════

#region Enums

/// <summary>
/// ประเภทของ GPU Cloud Provider
/// </summary>
public enum CloudGpuProvider
{
    VastAi,
    RunPod,
    Lambda,
    Paperspace,
    GoogleColab,
    Kaggle,
    LightningAI,
    HuggingFace,
    Custom
}

/// <summary>
/// สถานะของ GPU Account
/// </summary>
public enum GpuAccountStatus
{
    Active,
    InUse,
    Cooldown,
    QuotaExhausted,
    Suspended,
    NeedsReauth,
    Error,
    Disabled
}

/// <summary>
/// สถานะของ GPU Instance
/// </summary>
public enum GpuInstanceStatus
{
    Creating,
    Starting,
    Running,
    Stopping,
    Stopped,
    Terminating,
    Terminated,
    Error,
    Unknown
}

/// <summary>
/// ประเภท GPU
/// </summary>
public enum GpuHardwareType
{
    Unknown,
    T4,
    P100,
    V100,
    A10,
    A10G,
    A100_40GB,
    A100_80GB,
    H100,
    L4,
    L40,
    RTX3080,
    RTX3090,
    RTX4080,
    RTX4090
}

/// <summary>
/// Rotation strategy
/// </summary>
public enum PoolRotationStrategy
{
    RoundRobin,
    LeastUsed,
    Priority,
    Random,
    CostOptimized
}

#endregion

#region API Response Models

/// <summary>
/// Standard API response wrapper from Laravel
/// </summary>
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

    [JsonPropertyName("meta")]
    public PaginationMeta? Meta { get; set; }
}

/// <summary>
/// Pagination metadata
/// </summary>
public class PaginationMeta
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("last_page")]
    public int LastPage { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

#endregion

#region GPU Account Models

/// <summary>
/// GPU Cloud Account - represents an account with a GPU provider
/// </summary>
public class GpuCloudAccount
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("balance")]
    public decimal? Balance { get; set; }

    [JsonPropertyName("balance_currency")]
    public string BalanceCurrency { get; set; } = "USD";

    [JsonPropertyName("quota_used_minutes")]
    public int QuotaUsedMinutes { get; set; }

    [JsonPropertyName("quota_limit_minutes")]
    public int? QuotaLimitMinutes { get; set; }

    [JsonPropertyName("quota_reset_at")]
    public DateTime? QuotaResetAt { get; set; }

    [JsonPropertyName("cooldown_until")]
    public DateTime? CooldownUntil { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Computed properties
    [JsonIgnore]
    public int RemainingQuotaMinutes => QuotaLimitMinutes.HasValue
        ? Math.Max(0, QuotaLimitMinutes.Value - QuotaUsedMinutes)
        : int.MaxValue;

    [JsonIgnore]
    public bool IsAvailable =>
        IsEnabled &&
        Status == "active" &&
        (CooldownUntil == null || CooldownUntil <= DateTime.UtcNow) &&
        (!QuotaLimitMinutes.HasValue || RemainingQuotaMinutes > 0);

    [JsonIgnore]
    public CloudGpuProvider ProviderType => Provider.ToLowerInvariant() switch
    {
        "vast" or "vast.ai" or "vastai" => CloudGpuProvider.VastAi,
        "runpod" => CloudGpuProvider.RunPod,
        "lambda" or "lambdalabs" => CloudGpuProvider.Lambda,
        "paperspace" => CloudGpuProvider.Paperspace,
        "colab" or "google_colab" => CloudGpuProvider.GoogleColab,
        "kaggle" => CloudGpuProvider.Kaggle,
        "lightning" or "lightning_ai" => CloudGpuProvider.LightningAI,
        "huggingface" or "hf" => CloudGpuProvider.HuggingFace,
        _ => CloudGpuProvider.Custom
    };
}

/// <summary>
/// Request to create a GPU account
/// </summary>
public class CreateGpuAccountRequest
{
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100;

    [JsonPropertyName("quota_limit_minutes")]
    public int? QuotaLimitMinutes { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request to update a GPU account
/// </summary>
public class UpdateGpuAccountRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("api_key")]
    public string? ApiKey { get; set; }

    [JsonPropertyName("priority")]
    public int? Priority { get; set; }

    [JsonPropertyName("is_enabled")]
    public bool? IsEnabled { get; set; }

    [JsonPropertyName("quota_limit_minutes")]
    public int? QuotaLimitMinutes { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

#endregion

#region GPU Pool Models

/// <summary>
/// GPU Account Pool - group of accounts for rotation
/// </summary>
public class GpuAccountPool
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("rotation_strategy")]
    public string RotationStrategy { get; set; } = "round_robin";

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("cooldown_minutes")]
    public int CooldownMinutes { get; set; } = 60;

    [JsonPropertyName("auto_failover")]
    public bool AutoFailover { get; set; } = true;

    [JsonPropertyName("current_account_id")]
    public int? CurrentAccountId { get; set; }

    [JsonPropertyName("accounts")]
    public List<GpuCloudAccount>? Accounts { get; set; }

    [JsonPropertyName("account_count")]
    public int AccountCount { get; set; }

    [JsonPropertyName("available_count")]
    public int AvailableCount { get; set; }

    [JsonPropertyName("settings")]
    public GpuPoolSettings? Settings { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public PoolRotationStrategy RotationStrategyType => RotationStrategy.ToLowerInvariant() switch
    {
        "round_robin" or "roundrobin" => PoolRotationStrategy.RoundRobin,
        "least_used" or "leastused" => PoolRotationStrategy.LeastUsed,
        "priority" => PoolRotationStrategy.Priority,
        "random" => PoolRotationStrategy.Random,
        "cost" or "cost_optimized" => PoolRotationStrategy.CostOptimized,
        _ => PoolRotationStrategy.RoundRobin
    };
}

/// <summary>
/// GPU Pool settings
/// </summary>
public class GpuPoolSettings
{
    [JsonPropertyName("max_concurrent_instances")]
    public int MaxConcurrentInstances { get; set; } = 1;

    [JsonPropertyName("quota_threshold_percent")]
    public int QuotaThresholdPercent { get; set; } = 90;

    [JsonPropertyName("auto_rotate_on_quota_low")]
    public bool AutoRotateOnQuotaLow { get; set; } = true;

    [JsonPropertyName("max_consecutive_failures")]
    public int MaxConsecutiveFailures { get; set; } = 3;

    [JsonPropertyName("preferred_gpu_types")]
    public List<string>? PreferredGpuTypes { get; set; }

    [JsonPropertyName("max_hourly_cost")]
    public decimal? MaxHourlyCost { get; set; }

    [JsonPropertyName("min_vram_gb")]
    public int? MinVramGb { get; set; }
}

/// <summary>
/// Request to create a GPU pool
/// </summary>
public class CreateGpuPoolRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("rotation_strategy")]
    public string RotationStrategy { get; set; } = "round_robin";

    [JsonPropertyName("cooldown_minutes")]
    public int CooldownMinutes { get; set; } = 60;

    [JsonPropertyName("auto_failover")]
    public bool AutoFailover { get; set; } = true;

    [JsonPropertyName("account_ids")]
    public List<int>? AccountIds { get; set; }

    [JsonPropertyName("settings")]
    public GpuPoolSettings? Settings { get; set; }
}

/// <summary>
/// Request to add/remove accounts from pool
/// </summary>
public class ModifyPoolAccountsRequest
{
    [JsonPropertyName("account_ids")]
    public List<int> AccountIds { get; set; } = new();
}

#endregion

#region GPU Offer Models

/// <summary>
/// GPU Offer from a provider (available instances to rent)
/// </summary>
public class GpuOffer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("gpu_type")]
    public string GpuType { get; set; } = string.Empty;

    [JsonPropertyName("gpu_count")]
    public int GpuCount { get; set; } = 1;

    [JsonPropertyName("vram_gb")]
    public int VramGb { get; set; }

    [JsonPropertyName("cpu_cores")]
    public int? CpuCores { get; set; }

    [JsonPropertyName("ram_gb")]
    public int? RamGb { get; set; }

    [JsonPropertyName("disk_gb")]
    public int? DiskGb { get; set; }

    [JsonPropertyName("price_per_hour")]
    public decimal PricePerHour { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("availability")]
    public string Availability { get; set; } = "available";

    [JsonPropertyName("reliability_score")]
    public double? ReliabilityScore { get; set; }

    [JsonPropertyName("cuda_version")]
    public string? CudaVersion { get; set; }

    [JsonPropertyName("driver_version")]
    public string? DriverVersion { get; set; }

    [JsonPropertyName("internet_speed_mbps")]
    public int? InternetSpeedMbps { get; set; }

    [JsonPropertyName("supports_docker")]
    public bool SupportsDocker { get; set; } = true;

    [JsonPropertyName("supports_jupyter")]
    public bool SupportsJupyter { get; set; }

    [JsonPropertyName("min_rental_hours")]
    public int? MinRentalHours { get; set; }

    [JsonPropertyName("raw_data")]
    public Dictionary<string, object>? RawData { get; set; }

    [JsonIgnore]
    public GpuHardwareType HardwareType => GpuType.ToUpperInvariant() switch
    {
        "T4" or "TESLA T4" => GpuHardwareType.T4,
        "P100" or "TESLA P100" => GpuHardwareType.P100,
        "V100" or "TESLA V100" => GpuHardwareType.V100,
        "A10" => GpuHardwareType.A10,
        "A10G" => GpuHardwareType.A10G,
        "A100" when VramGb <= 40 => GpuHardwareType.A100_40GB,
        "A100" or "A100-SXM" or "A100-PCIE" => GpuHardwareType.A100_80GB,
        "H100" or "H100-SXM" or "H100-PCIE" => GpuHardwareType.H100,
        "L4" => GpuHardwareType.L4,
        "L40" or "L40S" => GpuHardwareType.L40,
        "RTX 3080" or "RTX3080" => GpuHardwareType.RTX3080,
        "RTX 3090" or "RTX3090" => GpuHardwareType.RTX3090,
        "RTX 4080" or "RTX4080" => GpuHardwareType.RTX4080,
        "RTX 4090" or "RTX4090" => GpuHardwareType.RTX4090,
        _ => GpuHardwareType.Unknown
    };
}

/// <summary>
/// Search criteria for GPU offers
/// </summary>
public class GpuOfferSearchCriteria
{
    [JsonPropertyName("provider")]
    public string? Provider { get; set; }

    [JsonPropertyName("gpu_type")]
    public string? GpuType { get; set; }

    [JsonPropertyName("min_vram_gb")]
    public int? MinVramGb { get; set; }

    [JsonPropertyName("max_price_per_hour")]
    public decimal? MaxPricePerHour { get; set; }

    [JsonPropertyName("min_gpu_count")]
    public int? MinGpuCount { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("requires_docker")]
    public bool? RequiresDocker { get; set; }

    [JsonPropertyName("requires_jupyter")]
    public bool? RequiresJupyter { get; set; }

    [JsonPropertyName("sort_by")]
    public string SortBy { get; set; } = "price";

    [JsonPropertyName("sort_order")]
    public string SortOrder { get; set; } = "asc";

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 50;
}

#endregion

#region GPU Instance Models

/// <summary>
/// GPU Instance - a provisioned GPU resource
/// </summary>
public class GpuInstance
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("user_id")]
    public int UserId { get; set; }

    [JsonPropertyName("account_id")]
    public int AccountId { get; set; }

    [JsonPropertyName("pool_id")]
    public int? PoolId { get; set; }

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    [JsonPropertyName("provider_instance_id")]
    public string ProviderInstanceId { get; set; } = string.Empty;

    [JsonPropertyName("offer_id")]
    public string? OfferId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "creating";

    [JsonPropertyName("gpu_type")]
    public string GpuType { get; set; } = string.Empty;

    [JsonPropertyName("gpu_count")]
    public int GpuCount { get; set; } = 1;

    [JsonPropertyName("vram_gb")]
    public int VramGb { get; set; }

    [JsonPropertyName("cpu_cores")]
    public int? CpuCores { get; set; }

    [JsonPropertyName("ram_gb")]
    public int? RamGb { get; set; }

    [JsonPropertyName("disk_gb")]
    public int? DiskGb { get; set; }

    [JsonPropertyName("price_per_hour")]
    public decimal PricePerHour { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("total_cost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("ip_address")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("ssh_port")]
    public int? SshPort { get; set; }

    [JsonPropertyName("ssh_command")]
    public string? SshCommand { get; set; }

    [JsonPropertyName("jupyter_url")]
    public string? JupyterUrl { get; set; }

    [JsonPropertyName("api_endpoint")]
    public string? ApiEndpoint { get; set; }

    [JsonPropertyName("docker_image")]
    public string? DockerImage { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("stopped_at")]
    public DateTime? StoppedAt { get; set; }

    [JsonPropertyName("terminated_at")]
    public DateTime? TerminatedAt { get; set; }

    [JsonPropertyName("runtime_minutes")]
    public int RuntimeMinutes { get; set; }

    [JsonPropertyName("last_health_check")]
    public DateTime? LastHealthCheck { get; set; }

    [JsonPropertyName("health_status")]
    public string? HealthStatus { get; set; }

    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonIgnore]
    public GpuInstanceStatus StatusType => Status.ToLowerInvariant() switch
    {
        "creating" => GpuInstanceStatus.Creating,
        "starting" => GpuInstanceStatus.Starting,
        "running" => GpuInstanceStatus.Running,
        "stopping" => GpuInstanceStatus.Stopping,
        "stopped" => GpuInstanceStatus.Stopped,
        "terminating" => GpuInstanceStatus.Terminating,
        "terminated" => GpuInstanceStatus.Terminated,
        "error" => GpuInstanceStatus.Error,
        _ => GpuInstanceStatus.Unknown
    };

    [JsonIgnore]
    public bool IsActive => StatusType is GpuInstanceStatus.Running or GpuInstanceStatus.Starting;

    [JsonIgnore]
    public TimeSpan? RunningDuration => StartedAt.HasValue && !StoppedAt.HasValue
        ? DateTime.UtcNow - StartedAt.Value
        : StartedAt.HasValue && StoppedAt.HasValue
            ? StoppedAt.Value - StartedAt.Value
            : null;
}

/// <summary>
/// Request to provision a new GPU instance
/// </summary>
public class ProvisionInstanceRequest
{
    [JsonPropertyName("account_id")]
    public int? AccountId { get; set; }

    [JsonPropertyName("pool_id")]
    public int? PoolId { get; set; }

    [JsonPropertyName("offer_id")]
    public string OfferId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("docker_image")]
    public string? DockerImage { get; set; }

    [JsonPropertyName("disk_gb")]
    public int? DiskGb { get; set; }

    [JsonPropertyName("ssh_key")]
    public string? SshKey { get; set; }

    [JsonPropertyName("startup_script")]
    public string? StartupScript { get; set; }

    [JsonPropertyName("environment_variables")]
    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    [JsonPropertyName("auto_stop_hours")]
    public int? AutoStopHours { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request to start/stop/terminate an instance
/// </summary>
public class InstanceActionRequest
{
    [JsonPropertyName("force")]
    public bool Force { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}

#endregion

#region Statistics & Events Models

/// <summary>
/// GPU usage statistics
/// </summary>
public class GpuUsageStats
{
    [JsonPropertyName("total_accounts")]
    public int TotalAccounts { get; set; }

    [JsonPropertyName("active_accounts")]
    public int ActiveAccounts { get; set; }

    [JsonPropertyName("total_pools")]
    public int TotalPools { get; set; }

    [JsonPropertyName("active_instances")]
    public int ActiveInstances { get; set; }

    [JsonPropertyName("total_runtime_minutes")]
    public int TotalRuntimeMinutes { get; set; }

    [JsonPropertyName("total_cost")]
    public decimal TotalCost { get; set; }

    [JsonPropertyName("cost_by_provider")]
    public Dictionary<string, decimal>? CostByProvider { get; set; }

    [JsonPropertyName("instances_by_status")]
    public Dictionary<string, int>? InstancesByStatus { get; set; }

    [JsonPropertyName("period_start")]
    public DateTime PeriodStart { get; set; }

    [JsonPropertyName("period_end")]
    public DateTime PeriodEnd { get; set; }
}

/// <summary>
/// Account rotation event
/// </summary>
public class AccountRotationEvent
{
    [JsonPropertyName("pool_id")]
    public int PoolId { get; set; }

    [JsonPropertyName("pool_name")]
    public string? PoolName { get; set; }

    [JsonPropertyName("previous_account_id")]
    public int? PreviousAccountId { get; set; }

    [JsonPropertyName("previous_account_name")]
    public string? PreviousAccountName { get; set; }

    [JsonPropertyName("new_account_id")]
    public int NewAccountId { get; set; }

    [JsonPropertyName("new_account_name")]
    public string? NewAccountName { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Instance state change event
/// </summary>
public class InstanceStateChangeEvent
{
    [JsonPropertyName("instance_id")]
    public int InstanceId { get; set; }

    [JsonPropertyName("provider_instance_id")]
    public string? ProviderInstanceId { get; set; }

    [JsonPropertyName("old_status")]
    public string? OldStatus { get; set; }

    [JsonPropertyName("new_status")]
    public string NewStatus { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

#endregion

#region Health Check Models

/// <summary>
/// Instance health check result
/// </summary>
public class InstanceHealthCheck
{
    [JsonPropertyName("instance_id")]
    public int InstanceId { get; set; }

    [JsonPropertyName("is_healthy")]
    public bool IsHealthy { get; set; }

    [JsonPropertyName("gpu_utilization")]
    public double? GpuUtilization { get; set; }

    [JsonPropertyName("gpu_memory_used_gb")]
    public double? GpuMemoryUsedGb { get; set; }

    [JsonPropertyName("gpu_memory_total_gb")]
    public double? GpuMemoryTotalGb { get; set; }

    [JsonPropertyName("gpu_temperature")]
    public int? GpuTemperature { get; set; }

    [JsonPropertyName("cpu_utilization")]
    public double? CpuUtilization { get; set; }

    [JsonPropertyName("memory_used_gb")]
    public double? MemoryUsedGb { get; set; }

    [JsonPropertyName("disk_used_gb")]
    public double? DiskUsedGb { get; set; }

    [JsonPropertyName("network_rx_mbps")]
    public double? NetworkRxMbps { get; set; }

    [JsonPropertyName("network_tx_mbps")]
    public double? NetworkTxMbps { get; set; }

    [JsonPropertyName("cuda_available")]
    public bool? CudaAvailable { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("checked_at")]
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Account verification result
/// </summary>
public class AccountVerificationResult
{
    [JsonPropertyName("account_id")]
    public int AccountId { get; set; }

    [JsonPropertyName("is_valid")]
    public bool IsValid { get; set; }

    [JsonPropertyName("balance")]
    public decimal? Balance { get; set; }

    [JsonPropertyName("quota_remaining_minutes")]
    public int? QuotaRemainingMinutes { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("verified_at")]
    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}

#endregion
