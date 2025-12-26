using Newtonsoft.Json;

namespace MyPostXAgent.Core.Models;

/// <summary>
/// Machine Identity - ข้อมูลเครื่องสำหรับ License
/// </summary>
public class MachineIdentity
{
    [JsonProperty("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonProperty("machine_hash")]
    public string MachineHash { get; set; } = "";

    [JsonProperty("cpu_id")]
    public string CpuId { get; set; } = "";

    [JsonProperty("motherboard_serial")]
    public string? MotherboardSerial { get; set; }

    [JsonProperty("bios_serial")]
    public string? BiosSerial { get; set; }

    [JsonProperty("windows_product_id")]
    public string? WindowsProductId { get; set; }

    [JsonProperty("disk_serial")]
    public string? DiskSerial { get; set; }

    [JsonProperty("hostname")]
    public string? Hostname { get; set; }

    [JsonProperty("os_version")]
    public string? OsVersion { get; set; }
}

/// <summary>
/// License Info - ข้อมูล License ที่เก็บใน Local
/// </summary>
public class LicenseInfo
{
    [JsonProperty("license_key")]
    public string LicenseKey { get; set; } = "";

    [JsonProperty("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonProperty("license_type")]
    public LicenseType LicenseType { get; set; }

    [JsonProperty("status")]
    public LicenseStatus Status { get; set; } = LicenseStatus.Active;

    [JsonProperty("activated_at")]
    public DateTime ActivatedAt { get; set; }

    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonProperty("last_validated_at")]
    public DateTime? LastValidatedAt { get; set; }

    [JsonProperty("features")]
    public List<string> Features { get; set; } = new();

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

    public int DaysRemaining => ExpiresAt.HasValue
        ? Math.Max(0, (ExpiresAt.Value - DateTime.UtcNow).Days)
        : int.MaxValue;
}

/// <summary>
/// Demo Info - ข้อมูล Demo ที่เก็บใน Local
/// </summary>
public class DemoInfo
{
    [JsonProperty("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonProperty("first_run_at")]
    public DateTime FirstRunAt { get; set; }

    [JsonProperty("demo_expires_at")]
    public DateTime DemoExpiresAt { get; set; }

    [JsonProperty("status")]
    public DemoStatus Status { get; set; } = DemoStatus.NotStarted;

    public bool IsExpired => DateTime.UtcNow > DemoExpiresAt;

    public int DaysRemaining => Math.Max(0, (DemoExpiresAt - DateTime.UtcNow).Days);

    public int HoursRemaining => Math.Max(0, (int)(DemoExpiresAt - DateTime.UtcNow).TotalHours);
}

/// <summary>
/// License Validation Request - ส่งไป Server
/// </summary>
public class LicenseValidationRequest
{
    [JsonProperty("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonProperty("machine_hash")]
    public string MachineHash { get; set; } = "";

    [JsonProperty("license_key")]
    public string? LicenseKey { get; set; }

    [JsonProperty("hardware_info")]
    public MachineIdentity? HardwareInfo { get; set; }

    [JsonProperty("app_version")]
    public string AppVersion { get; set; } = "1.0.0";

    [JsonProperty("os_version")]
    public string? OsVersion { get; set; }
}

/// <summary>
/// License Validation Response - รับจาก Server
/// </summary>
public class LicenseValidationResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("valid")]
    public bool Valid { get; set; }

    [JsonProperty("license_type")]
    public string? LicenseType { get; set; }

    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonProperty("days_remaining")]
    public int DaysRemaining { get; set; }

    [JsonProperty("features")]
    public List<string> Features { get; set; } = new();

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("update_available")]
    public UpdateInfo? UpdateAvailable { get; set; }
}

/// <summary>
/// Demo Check Request
/// </summary>
public class DemoCheckRequest
{
    [JsonProperty("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonProperty("machine_hash")]
    public string MachineHash { get; set; } = "";

    [JsonProperty("hardware_info")]
    public MachineIdentity? HardwareInfo { get; set; }

    [JsonProperty("app_version")]
    public string AppVersion { get; set; } = "1.0.0";

    [JsonProperty("os_version")]
    public string? OsVersion { get; set; }
}

/// <summary>
/// Demo Check Response
/// </summary>
public class DemoCheckResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("eligible")]
    public bool Eligible { get; set; }

    [JsonProperty("demo_days")]
    public int DemoDays { get; set; } = 3;

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }

    [JsonProperty("redirect")]
    public string? Redirect { get; set; }
}

/// <summary>
/// License Activation Request
/// </summary>
public class LicenseActivationRequest
{
    [JsonProperty("license_key")]
    public string LicenseKey { get; set; } = "";

    [JsonProperty("machine_id")]
    public string MachineId { get; set; } = "";

    [JsonProperty("machine_hash")]
    public string MachineHash { get; set; } = "";

    [JsonProperty("hardware_info")]
    public MachineIdentity? HardwareInfo { get; set; }

    [JsonProperty("app_version")]
    public string AppVersion { get; set; } = "1.0.0";
}

/// <summary>
/// License Activation Response
/// </summary>
public class LicenseActivationResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("license_type")]
    public string? LicenseType { get; set; }

    [JsonProperty("activated_at")]
    public DateTime? ActivatedAt { get; set; }

    [JsonProperty("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [JsonProperty("features")]
    public List<string> Features { get; set; } = new();

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("error")]
    public string? Error { get; set; }
}

/// <summary>
/// Update Info
/// </summary>
public class UpdateInfo
{
    [JsonProperty("version")]
    public string Version { get; set; } = "";

    [JsonProperty("version_code")]
    public int VersionCode { get; set; }

    [JsonProperty("mandatory")]
    public bool Mandatory { get; set; }

    [JsonProperty("download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonProperty("release_notes")]
    public string? ReleaseNotes { get; set; }

    [JsonProperty("checksum_sha256")]
    public string? ChecksumSha256 { get; set; }

    [JsonProperty("file_size")]
    public long FileSize { get; set; }
}

/// <summary>
/// Hardware Change Result - ผลการตรวจสอบการเปลี่ยนแปลง Hardware
/// </summary>
public class HardwareChangeResult
{
    public bool HasSignificantChange { get; set; }
    public int ChangeScore { get; set; }
    public List<string> ChangedComponents { get; set; } = new();
    public bool RequiresReactivation { get; set; }
}
