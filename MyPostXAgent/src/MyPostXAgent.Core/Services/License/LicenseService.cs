using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;
using Newtonsoft.Json;

namespace MyPostXAgent.Core.Services.License;

/// <summary>
/// Service สำหรับจัดการ License และ Demo
/// </summary>
public class LicenseService
{
    private readonly ILogger<LicenseService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly MachineIdGenerator _machineIdGenerator;
    private readonly string _apiBaseUrl;
    private readonly string _appVersion;

    private MachineIdentity? _machineIdentity;
    private LicenseInfo? _currentLicense;
    private DemoInfo? _demoInfo;

    public LicenseService(
        HttpClient httpClient,
        MachineIdGenerator machineIdGenerator,
        string apiBaseUrl = "https://api.postxagent.com",
        string appVersion = "1.0.0",
        ILogger<LicenseService>? logger = null)
    {
        _httpClient = httpClient;
        _machineIdGenerator = machineIdGenerator;
        _apiBaseUrl = apiBaseUrl.TrimEnd('/');
        _appVersion = appVersion;
        _logger = logger;
    }

    /// <summary>
    /// ดึง Machine Identity ปัจจุบัน
    /// </summary>
    public MachineIdentity GetMachineIdentity()
    {
        _machineIdentity ??= _machineIdGenerator.Generate();
        return _machineIdentity;
    }

    /// <summary>
    /// ตรวจสอบว่าสามารถใช้ Demo ได้หรือไม่
    /// Uses xmanstudio API: POST /api/v1/license/demo/check
    /// </summary>
    public async Task<DemoCheckResponse> CheckDemoEligibilityAsync(CancellationToken ct = default)
    {
        try
        {
            var machineId = GetMachineIdentity();

            var request = new DemoCheckRequest
            {
                MachineId = machineId.MachineId,
                MachineFingerprint = machineId.MachineHash,
                HardwareInfo = machineId,
                AppVersion = _appVersion,
                OsVersion = machineId.OsVersion
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/v1/license/demo/check",
                request,
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<DemoCheckResponse>(content);

            return result ?? new DemoCheckResponse
            {
                Success = false,
                Eligible = false,
                Error = "Invalid response from server"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error checking demo eligibility");
            return new DemoCheckResponse
            {
                Success = false,
                Eligible = false,
                Error = "ไม่สามารถเชื่อมต่อ Server ได้ กรุณาตรวจสอบการเชื่อมต่อ Internet"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking demo eligibility");
            return new DemoCheckResponse
            {
                Success = false,
                Eligible = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// เปิดใช้งาน Demo
    /// Uses xmanstudio API: POST /api/v1/license/demo
    /// </summary>
    public async Task<DemoCheckResponse> ActivateDemoAsync(CancellationToken ct = default)
    {
        try
        {
            var machineId = GetMachineIdentity();

            var request = new DemoCheckRequest
            {
                MachineId = machineId.MachineId,
                MachineFingerprint = machineId.MachineHash,
                HardwareInfo = machineId,
                AppVersion = _appVersion,
                OsVersion = machineId.OsVersion
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/v1/license/demo",
                request,
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<DemoCheckResponse>(content);

            if (result?.Success == true)
            {
                _demoInfo = new DemoInfo
                {
                    MachineId = machineId.MachineId,
                    FirstRunAt = DateTime.UtcNow,
                    DemoExpiresAt = DateTime.UtcNow.AddDays(result.DemoDays),
                    Status = DemoStatus.Active
                };

                _logger?.LogInformation("Demo activated successfully. Expires at: {ExpiresAt}", _demoInfo.DemoExpiresAt);
            }

            return result ?? new DemoCheckResponse
            {
                Success = false,
                Error = "Invalid response from server"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error activating demo");
            return new DemoCheckResponse
            {
                Success = false,
                Error = "ไม่สามารถเชื่อมต่อ Server ได้ กรุณาตรวจสอบการเชื่อมต่อ Internet"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error activating demo");
            return new DemoCheckResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// เปิดใช้งาน License Key
    /// Uses xmanstudio API: POST /api/v1/license/activate
    /// </summary>
    public async Task<LicenseActivationResponse> ActivateLicenseAsync(string licenseKey, CancellationToken ct = default)
    {
        try
        {
            var machineId = GetMachineIdentity();

            var request = new LicenseActivationRequest
            {
                LicenseKey = licenseKey.Trim().ToUpperInvariant(),
                MachineId = machineId.MachineId,
                MachineFingerprint = machineId.MachineHash,
                HardwareInfo = machineId,
                AppVersion = _appVersion
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/v1/license/activate",
                request,
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<LicenseActivationResponse>(content);

            if (result?.Success == true)
            {
                _currentLicense = new LicenseInfo
                {
                    LicenseKey = licenseKey,
                    MachineId = machineId.MachineId,
                    LicenseType = Enum.TryParse<LicenseType>(result.LicenseType, true, out var type)
                        ? type
                        : LicenseType.Monthly,
                    Status = LicenseStatus.Active,
                    ActivatedAt = result.ActivatedAt ?? DateTime.UtcNow,
                    ExpiresAt = result.ExpiresAt,
                    LastValidatedAt = DateTime.UtcNow,
                    Features = result.Features
                };

                _logger?.LogInformation("License activated successfully. Type: {Type}, Expires: {ExpiresAt}",
                    _currentLicense.LicenseType, _currentLicense.ExpiresAt);
            }

            return result ?? new LicenseActivationResponse
            {
                Success = false,
                Error = "Invalid response from server"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "Network error activating license");
            return new LicenseActivationResponse
            {
                Success = false,
                Error = "ไม่สามารถเชื่อมต่อ Server ได้ กรุณาตรวจสอบการเชื่อมต่อ Internet"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error activating license");
            return new LicenseActivationResponse
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// ตรวจสอบความถูกต้องของ License กับ Server
    /// Uses xmanstudio API: POST /api/v1/license/validate
    /// </summary>
    public async Task<LicenseValidationResponse> ValidateLicenseAsync(
        string? licenseKey = null,
        CancellationToken ct = default)
    {
        try
        {
            var machineId = GetMachineIdentity();

            var request = new LicenseValidationRequest
            {
                MachineId = machineId.MachineId,
                MachineFingerprint = machineId.MachineHash,
                LicenseKey = licenseKey ?? _currentLicense?.LicenseKey,
                HardwareInfo = machineId,
                AppVersion = _appVersion,
                OsVersion = machineId.OsVersion
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/v1/license/validate",
                request,
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<LicenseValidationResponse>(content);

            if (result?.Valid == true && _currentLicense != null)
            {
                _currentLicense.LastValidatedAt = DateTime.UtcNow;
                _currentLicense.ExpiresAt = result.ExpiresAt;
            }

            return result ?? new LicenseValidationResponse
            {
                Success = false,
                Valid = false,
                Error = "Invalid response from server"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogWarning(ex, "Network error validating license - continuing offline");
            // ถ้าไม่สามารถเชื่อมต่อได้ ให้ใช้ข้อมูลเดิม
            return new LicenseValidationResponse
            {
                Success = true,
                Valid = _currentLicense != null && !_currentLicense.IsExpired,
                LicenseType = _currentLicense?.LicenseType.ToString(),
                ExpiresAt = _currentLicense?.ExpiresAt,
                DaysRemaining = _currentLicense?.DaysRemaining ?? 0,
                Message = "Offline mode - using cached license"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error validating license");
            return new LicenseValidationResponse
            {
                Success = false,
                Valid = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// ตรวจสอบว่ามี Update ใหม่หรือไม่
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var machineId = GetMachineIdentity();

            var request = new LicenseValidationRequest
            {
                MachineId = machineId.MachineId,
                MachineFingerprint = machineId.MachineHash,
                LicenseKey = _currentLicense?.LicenseKey,
                AppVersion = _appVersion
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_apiBaseUrl}/api/v1/license/check-update",
                request,
                ct);

            var content = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<LicenseValidationResponse>(content);

            return result?.UpdateAvailable;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error checking for updates");
            return null;
        }
    }

    /// <summary>
    /// ดึงข้อมูล License ปัจจุบัน
    /// </summary>
    public LicenseInfo? GetCurrentLicense() => _currentLicense;

    /// <summary>
    /// ดึงข้อมูล Demo ปัจจุบัน
    /// </summary>
    public DemoInfo? GetDemoInfo() => _demoInfo;

    /// <summary>
    /// ตั้งค่า License Info (จาก Local Storage)
    /// </summary>
    public void SetLicenseInfo(LicenseInfo? license)
    {
        _currentLicense = license;
    }

    /// <summary>
    /// ตั้งค่า Demo Info (จาก Local Storage)
    /// </summary>
    public void SetDemoInfo(DemoInfo? demo)
    {
        _demoInfo = demo;
    }

    /// <summary>
    /// ตรวจสอบว่า License หรือ Demo ยังใช้งานได้หรือไม่
    /// </summary>
    public bool IsLicensed()
    {
        // Check full license first
        if (_currentLicense != null && _currentLicense.Status == LicenseStatus.Active && !_currentLicense.IsExpired)
        {
            return true;
        }

        // Check demo
        if (_demoInfo != null && _demoInfo.Status == DemoStatus.Active && !_demoInfo.IsExpired)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// ตรวจสอบว่าเป็น Demo Mode หรือไม่
    /// </summary>
    public bool IsDemoMode()
    {
        if (_currentLicense != null && _currentLicense.Status == LicenseStatus.Active && !_currentLicense.IsExpired)
        {
            return false;
        }

        return _demoInfo != null && _demoInfo.Status == DemoStatus.Active && !_demoInfo.IsExpired;
    }

    /// <summary>
    /// ดึงจำนวนวันที่เหลือ
    /// </summary>
    public int GetDaysRemaining()
    {
        if (_currentLicense != null && !_currentLicense.IsExpired)
        {
            return _currentLicense.DaysRemaining;
        }

        if (_demoInfo != null && !_demoInfo.IsExpired)
        {
            return _demoInfo.DaysRemaining;
        }

        return 0;
    }
}
