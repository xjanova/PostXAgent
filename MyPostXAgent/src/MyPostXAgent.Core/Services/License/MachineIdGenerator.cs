using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MyPostXAgent.Core.Models;

namespace MyPostXAgent.Core.Services.License;

/// <summary>
/// สร้าง Machine ID จากข้อมูล Hardware ของเครื่อง
/// </summary>
public class MachineIdGenerator
{
    private readonly ILogger<MachineIdGenerator>? _logger;

    public MachineIdGenerator(ILogger<MachineIdGenerator>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// สร้าง Machine Identity จากข้อมูล Hardware
    /// </summary>
    public MachineIdentity Generate()
    {
        var cpuId = GetCpuId();
        var motherboardSerial = GetMotherboardSerial();
        var biosSerial = GetBiosSerial();
        var windowsProductId = GetWindowsProductId();
        var diskSerial = GetFirstDiskSerial();

        // Combine all components
        var components = new List<string>
        {
            $"CPU:{cpuId}",
            $"MB:{motherboardSerial}",
            $"BIOS:{biosSerial}",
            $"WIN:{windowsProductId}"
        };

        if (!string.IsNullOrEmpty(diskSerial))
        {
            components.Add($"DISK:{diskSerial}");
        }

        var combinedString = string.Join("|", components);

        // Generate short machine ID (16 characters)
        var machineId = GenerateShortHash(combinedString, 16);

        // Generate full hash for verification
        var machineHash = ComputeSha256(combinedString);

        _logger?.LogInformation("Generated Machine ID: {MachineId}", machineId);

        return new MachineIdentity
        {
            MachineId = machineId,
            MachineHash = machineHash,
            CpuId = cpuId,
            MotherboardSerial = motherboardSerial,
            BiosSerial = biosSerial,
            WindowsProductId = windowsProductId,
            DiskSerial = diskSerial,
            Hostname = Environment.MachineName,
            OsVersion = Environment.OSVersion.ToString()
        };
    }

    /// <summary>
    /// ตรวจสอบว่า Hardware มีการเปลี่ยนแปลงอย่างมีนัยสำคัญหรือไม่
    /// </summary>
    public HardwareChangeResult VerifyHardware(MachineIdentity current, MachineIdentity stored)
    {
        var changes = new List<string>();
        int changeScore = 0;

        // CPU change is critical (score: 40)
        if (!string.Equals(current.CpuId, stored.CpuId, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("CPU");
            changeScore += 40;
        }

        // Motherboard change is critical (score: 40)
        if (!string.Equals(current.MotherboardSerial, stored.MotherboardSerial, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("Motherboard");
            changeScore += 40;
        }

        // BIOS change is significant (score: 20)
        if (!string.Equals(current.BiosSerial, stored.BiosSerial, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("BIOS");
            changeScore += 20;
        }

        // Windows reinstall (score: 10)
        if (!string.Equals(current.WindowsProductId, stored.WindowsProductId, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("Windows");
            changeScore += 10;
        }

        return new HardwareChangeResult
        {
            HasSignificantChange = changeScore >= 50,
            ChangeScore = changeScore,
            ChangedComponents = changes,
            RequiresReactivation = changeScore >= 80
        };
    }

    /// <summary>
    /// ดึง CPU ID
    /// </summary>
    public string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var item in searcher.Get())
            {
                var id = item["ProcessorId"]?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get CPU ID");
        }

        return "UNKNOWN_CPU";
    }

    /// <summary>
    /// ดึง Motherboard Serial
    /// </summary>
    public string GetMotherboardSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard");
            foreach (var item in searcher.Get())
            {
                var serial = item["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serial) && serial != "To be filled by O.E.M.")
                {
                    return serial;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Motherboard Serial");
        }

        return "UNKNOWN_MB";
    }

    /// <summary>
    /// ดึง BIOS Serial
    /// </summary>
    public string GetBiosSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (var item in searcher.Get())
            {
                var serial = item["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serial) && serial != "To be filled by O.E.M.")
                {
                    return serial;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get BIOS Serial");
        }

        return "UNKNOWN_BIOS";
    }

    /// <summary>
    /// ดึง Windows Product ID
    /// </summary>
    public string GetWindowsProductId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_OperatingSystem");
            foreach (var item in searcher.Get())
            {
                var serial = item["SerialNumber"]?.ToString();
                if (!string.IsNullOrEmpty(serial))
                {
                    return serial;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Windows Product ID");
        }

        return "UNKNOWN_WIN";
    }

    /// <summary>
    /// ดึง Serial ของ Disk แรก
    /// </summary>
    public string GetFirstDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index = 0");
            foreach (var item in searcher.Get())
            {
                var serial = item["SerialNumber"]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(serial))
                {
                    return serial;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Disk Serial");
        }

        return "";
    }

    /// <summary>
    /// สร้าง Short Hash จาก string
    /// </summary>
    private static string GenerateShortHash(string input, int length)
    {
        var hash = ComputeSha256(input);
        return hash[..Math.Min(length, hash.Length)].ToUpperInvariant();
    }

    /// <summary>
    /// คำนวณ SHA256 Hash
    /// </summary>
    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
