using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;

namespace MyPostXAgent.Core.Services.License;

/// <summary>
/// จัดการระบบ Demo 3 วัน
/// </summary>
public class DemoManager
{
    private readonly ILogger<DemoManager>? _logger;
    private readonly LicenseService _licenseService;
    private readonly DatabaseService _databaseService;

    private DemoInfo? _demoInfo;

    public DemoManager(
        LicenseService licenseService,
        DatabaseService databaseService,
        ILogger<DemoManager>? logger = null)
    {
        _licenseService = licenseService;
        _databaseService = databaseService;
        _logger = logger;
    }

    /// <summary>
    /// ตรวจสอบสถานะ Demo
    /// </summary>
    public async Task<DemoStatus> CheckDemoStatusAsync(CancellationToken ct = default)
    {
        // 1. ตรวจสอบ Local Database ก่อน
        _demoInfo = await _databaseService.GetDemoInfoAsync(ct);

        if (_demoInfo != null)
        {
            // มี Demo อยู่แล้ว
            if (_demoInfo.Status == DemoStatus.Blocked)
            {
                return DemoStatus.Blocked;
            }

            if (_demoInfo.IsExpired)
            {
                _demoInfo.Status = DemoStatus.Expired;
                await _databaseService.SaveDemoInfoAsync(_demoInfo, ct);
                return DemoStatus.Expired;
            }

            return DemoStatus.Active;
        }

        // 2. ไม่มี Demo ใน Local - ต้องเช็คกับ Server
        return DemoStatus.NotStarted;
    }

    /// <summary>
    /// เริ่มต้น Demo ใหม่
    /// </summary>
    public async Task<(bool Success, string Message)> StartDemoAsync(CancellationToken ct = default)
    {
        try
        {
            // 1. ตรวจสอบกับ Server ว่าเครื่องนี้ใช้ Demo ได้ไหม
            var checkResult = await _licenseService.CheckDemoEligibilityAsync(ct);

            if (!checkResult.Success || !checkResult.Eligible)
            {
                var message = checkResult.Error ?? checkResult.Message ?? "ไม่สามารถเริ่ม Demo ได้";

                // ถ้าเครื่องนี้เคยใช้ Demo แล้ว - บล็อค
                if (checkResult.Error == "demo_already_used")
                {
                    _demoInfo = new DemoInfo
                    {
                        MachineId = _licenseService.GetMachineIdentity().MachineId,
                        FirstRunAt = DateTime.UtcNow,
                        DemoExpiresAt = DateTime.UtcNow,
                        Status = DemoStatus.Blocked
                    };
                    await _databaseService.SaveDemoInfoAsync(_demoInfo, ct);
                }

                return (false, message);
            }

            // 2. เปิดใช้งาน Demo กับ Server
            var activateResult = await _licenseService.ActivateDemoAsync(ct);

            if (!activateResult.Success)
            {
                return (false, activateResult.Error ?? "ไม่สามารถเปิดใช้งาน Demo ได้");
            }

            // 3. บันทึกลง Local Database
            var machineId = _licenseService.GetMachineIdentity();
            _demoInfo = new DemoInfo
            {
                MachineId = machineId.MachineId,
                FirstRunAt = DateTime.UtcNow,
                DemoExpiresAt = DateTime.UtcNow.AddDays(activateResult.DemoDays),
                Status = DemoStatus.Active
            };

            await _databaseService.SaveDemoInfoAsync(_demoInfo, ct);

            _logger?.LogInformation("Demo started successfully. Expires: {ExpiresAt}", _demoInfo.DemoExpiresAt);

            return (true, $"เริ่มทดลองใช้ {activateResult.DemoDays} วันแล้ว");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start demo");
            return (false, $"เกิดข้อผิดพลาด: {ex.Message}");
        }
    }

    /// <summary>
    /// ดึงข้อมูล Demo ปัจจุบัน
    /// </summary>
    public DemoInfo? GetDemoInfo() => _demoInfo;

    /// <summary>
    /// ดึงจำนวนวันที่เหลือ
    /// </summary>
    public int GetDaysRemaining() => _demoInfo?.DaysRemaining ?? 0;

    /// <summary>
    /// ดึงจำนวนชั่วโมงที่เหลือ
    /// </summary>
    public int GetHoursRemaining() => _demoInfo?.HoursRemaining ?? 0;

    /// <summary>
    /// ตรวจสอบว่า Demo หมดอายุหรือยัง
    /// </summary>
    public bool IsExpired() => _demoInfo?.IsExpired ?? true;

    /// <summary>
    /// ตรวจสอบว่า Demo ยังใช้งานได้หรือไม่
    /// </summary>
    public bool IsActive() => _demoInfo?.Status == DemoStatus.Active && !(_demoInfo?.IsExpired ?? true);

    /// <summary>
    /// ตรวจสอบว่าถูกบล็อคหรือไม่
    /// </summary>
    public bool IsBlocked() => _demoInfo?.Status == DemoStatus.Blocked;

    /// <summary>
    /// สร้างข้อความแสดงสถานะ Demo
    /// </summary>
    public string GetStatusMessage()
    {
        if (_demoInfo == null)
        {
            return "ยังไม่ได้เริ่ม Demo";
        }

        return _demoInfo.Status switch
        {
            DemoStatus.Active when !_demoInfo.IsExpired =>
                _demoInfo.DaysRemaining > 0
                    ? $"Demo: เหลือ {_demoInfo.DaysRemaining} วัน"
                    : $"Demo: เหลือ {_demoInfo.HoursRemaining} ชั่วโมง",
            DemoStatus.Expired => "Demo หมดอายุแล้ว",
            DemoStatus.Blocked => "เครื่องนี้เคยใช้ Demo แล้ว",
            _ => "ยังไม่ได้เริ่ม Demo"
        };
    }
}
