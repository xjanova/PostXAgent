using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Production-quality service for detecting and managing local GPU resources
/// Supports NVIDIA CUDA, AMD ROCm, and Intel OneAPI
/// </summary>
public class LocalGpuService
{
    private readonly ILogger<LocalGpuService>? _logger;
    private GpuInfo? _cachedGpuInfo;
    private DateTime _lastDetection = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);
    private static readonly SemaphoreSlim _detectionLock = new(1, 1);

    public LocalGpuService(ILogger<LocalGpuService>? logger = null)
    {
        _logger = logger;
    }

    #region GPU Detection

    /// <summary>
    /// Detect available GPUs on the system
    /// </summary>
    public async Task<GpuInfo> DetectGpuAsync(bool forceRefresh = false, CancellationToken ct = default)
    {
        // Return cached if valid
        if (!forceRefresh && _cachedGpuInfo != null && DateTime.Now - _lastDetection < _cacheExpiry)
        {
            return _cachedGpuInfo;
        }

        await _detectionLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (!forceRefresh && _cachedGpuInfo != null && DateTime.Now - _lastDetection < _cacheExpiry)
            {
                return _cachedGpuInfo;
            }

            _logger?.LogInformation("Detecting local GPU...");

            var gpuInfo = new GpuInfo
            {
                DetectedAt = DateTime.Now
            };

            // Try NVIDIA first (most common for ML)
            var nvidiaGpu = await DetectNvidiaGpuAsync(ct);
            if (nvidiaGpu != null)
            {
                gpuInfo.Vendor = GpuVendor.Nvidia;
                gpuInfo.Name = nvidiaGpu.Name;
                gpuInfo.TotalVramGb = nvidiaGpu.TotalMemoryMb / 1024.0;
                gpuInfo.FreeVramGb = nvidiaGpu.FreeMemoryMb / 1024.0;
                gpuInfo.DriverVersion = nvidiaGpu.DriverVersion;
                gpuInfo.CudaVersion = nvidiaGpu.CudaVersion;
                gpuInfo.ComputeCapability = nvidiaGpu.ComputeCapability;
                gpuInfo.Temperature = nvidiaGpu.Temperature;
                gpuInfo.PowerUsageWatts = nvidiaGpu.PowerUsage;
                gpuInfo.UtilizationPercent = nvidiaGpu.GpuUtilization;
                gpuInfo.IsAvailable = true;
                gpuInfo.SupportsCuda = true;
                gpuInfo.SupportsFp16 = true;
                gpuInfo.SupportsBf16 = nvidiaGpu.ComputeCapability >= 8.0; // Ampere+
            }
            else
            {
                // Try AMD
                var amdGpu = await DetectAmdGpuAsync(ct);
                if (amdGpu != null)
                {
                    gpuInfo.Vendor = GpuVendor.Amd;
                    gpuInfo.Name = amdGpu.Name;
                    gpuInfo.TotalVramGb = amdGpu.TotalMemoryMb / 1024.0;
                    gpuInfo.FreeVramGb = amdGpu.FreeMemoryMb / 1024.0;
                    gpuInfo.IsAvailable = true;
                    gpuInfo.SupportsRocm = true;
                    gpuInfo.SupportsFp16 = true;
                }
                else
                {
                    // Try Intel
                    var intelGpu = await DetectIntelGpuAsync(ct);
                    if (intelGpu != null)
                    {
                        gpuInfo.Vendor = GpuVendor.Intel;
                        gpuInfo.Name = intelGpu.Name;
                        gpuInfo.TotalVramGb = intelGpu.TotalMemoryMb / 1024.0;
                        gpuInfo.IsAvailable = true;
                        gpuInfo.SupportsOneApi = true;
                    }
                    else
                    {
                        gpuInfo.Vendor = GpuVendor.None;
                        gpuInfo.Name = "CPU Only";
                        gpuInfo.IsAvailable = false;
                    }
                }
            }

            // Calculate recommended settings
            gpuInfo.RecommendedBatchSize = CalculateRecommendedBatchSize(gpuInfo);
            gpuInfo.RecommendedMaxResolution = CalculateRecommendedMaxResolution(gpuInfo);
            gpuInfo.CanLoadXL = gpuInfo.TotalVramGb >= 8.0;
            gpuInfo.CanLoadFlux = gpuInfo.TotalVramGb >= 12.0;

            _cachedGpuInfo = gpuInfo;
            _lastDetection = DateTime.Now;

            _logger?.LogInformation("GPU detected: {Name}, VRAM: {Vram:F1}GB, Available: {Available}",
                gpuInfo.Name, gpuInfo.TotalVramGb, gpuInfo.IsAvailable);

            return gpuInfo;
        }
        finally
        {
            _detectionLock.Release();
        }
    }

    /// <summary>
    /// Get real-time VRAM usage
    /// </summary>
    public async Task<VramUsage> GetVramUsageAsync(CancellationToken ct = default)
    {
        var usage = new VramUsage();

        try
        {
            var nvidia = await DetectNvidiaGpuAsync(ct);
            if (nvidia != null)
            {
                usage.TotalMb = nvidia.TotalMemoryMb;
                usage.UsedMb = nvidia.TotalMemoryMb - nvidia.FreeMemoryMb;
                usage.FreeMb = nvidia.FreeMemoryMb;
                usage.UsagePercent = (usage.UsedMb / (double)usage.TotalMb) * 100;
                usage.IsAvailable = true;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get VRAM usage");
        }

        return usage;
    }

    /// <summary>
    /// Check if enough VRAM is available for a model
    /// </summary>
    public async Task<(bool CanLoad, string Reason)> CanLoadModelAsync(
        double requiredVramGb,
        CancellationToken ct = default)
    {
        var gpu = await DetectGpuAsync(ct: ct);

        if (!gpu.IsAvailable)
        {
            return (false, "No GPU available. Will use CPU (very slow).");
        }

        var vram = await GetVramUsageAsync(ct);
        var freeGb = vram.FreeMb / 1024.0;

        // Add buffer (keep 1GB free for system)
        var requiredWithBuffer = requiredVramGb + 1.0;

        if (freeGb < requiredWithBuffer)
        {
            return (false, $"Not enough VRAM. Need {requiredVramGb:F1}GB, have {freeGb:F1}GB free.");
        }

        // Check if model will fit with optimization
        if (freeGb < requiredVramGb * 1.2 && gpu.TotalVramGb >= requiredVramGb)
        {
            return (true, $"Low VRAM warning. May need to unload other models. Free: {freeGb:F1}GB");
        }

        return (true, $"Sufficient VRAM available. Free: {freeGb:F1}GB");
    }

    #endregion

    #region NVIDIA Detection

    private async Task<NvidiaGpuInfo?> DetectNvidiaGpuAsync(CancellationToken ct)
    {
        try
        {
            // Try nvidia-smi
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total,memory.free,memory.used,driver_version,temperature.gpu,power.draw,utilization.gpu,compute_cap --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                return null;

            // Parse CSV output
            var parts = output.Trim().Split(',').Select(s => s.Trim()).ToArray();
            if (parts.Length < 5) return null;

            var info = new NvidiaGpuInfo
            {
                Name = parts[0],
                TotalMemoryMb = ParseInt(parts[1]),
                FreeMemoryMb = ParseInt(parts[2]),
                UsedMemoryMb = ParseInt(parts[3]),
                DriverVersion = parts[4]
            };

            if (parts.Length > 5) info.Temperature = ParseInt(parts[5]);
            if (parts.Length > 6) info.PowerUsage = ParseDouble(parts[6]);
            if (parts.Length > 7) info.GpuUtilization = ParseInt(parts[7]);
            if (parts.Length > 8) info.ComputeCapability = ParseDouble(parts[8]);

            // Get CUDA version
            info.CudaVersion = await GetCudaVersionAsync(ct);

            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "NVIDIA GPU not detected");
            return null;
        }
    }

    private async Task<string?> GetCudaVersionAsync(CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=driver_version --format=csv,noheader",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            // Also try nvcc
            var nvccPsi = new ProcessStartInfo
            {
                FileName = "nvcc",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            try
            {
                using var nvccProcess = Process.Start(nvccPsi);
                if (nvccProcess != null)
                {
                    var nvccOutput = await nvccProcess.StandardOutput.ReadToEndAsync(ct);
                    var match = Regex.Match(nvccOutput, @"release (\d+\.\d+)");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
            }
            catch { }

            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region AMD Detection

    private async Task<AmdGpuInfo?> DetectAmdGpuAsync(CancellationToken ct)
    {
        try
        {
            // Try rocm-smi for AMD GPUs
            var psi = new ProcessStartInfo
            {
                FileName = "rocm-smi",
                Arguments = "--showmeminfo vram --json",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                return null;

            // Parse JSON output
            var json = JsonSerializer.Deserialize<JsonDocument>(output);
            if (json == null) return null;

            // Get first GPU
            var gpuElement = json.RootElement.EnumerateObject().FirstOrDefault();
            if (gpuElement.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            var info = new AmdGpuInfo
            {
                Name = "AMD GPU"
            };

            if (gpuElement.Value.TryGetProperty("VRAM Total Memory (B)", out var total))
            {
                info.TotalMemoryMb = (int)(total.GetInt64() / 1024 / 1024);
            }
            if (gpuElement.Value.TryGetProperty("VRAM Total Used Memory (B)", out var used))
            {
                var usedMb = (int)(used.GetInt64() / 1024 / 1024);
                info.FreeMemoryMb = info.TotalMemoryMb - usedMb;
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "AMD GPU not detected");
            return null;
        }
    }

    #endregion

    #region Intel Detection

    private async Task<IntelGpuInfo?> DetectIntelGpuAsync(CancellationToken ct)
    {
        try
        {
            // Try sycl-ls for Intel OneAPI
            var psi = new ProcessStartInfo
            {
                FileName = "sycl-ls",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                return null;

            if (output.Contains("Intel") || output.Contains("Level-Zero"))
            {
                return new IntelGpuInfo
                {
                    Name = "Intel GPU",
                    TotalMemoryMb = 0 // Intel integrated GPUs share system memory
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Intel GPU not detected");
            return null;
        }
    }

    #endregion

    #region Python Environment

    /// <summary>
    /// Check Python environment and required packages
    /// </summary>
    public async Task<PythonEnvironmentInfo> CheckPythonEnvironmentAsync(CancellationToken ct = default)
    {
        var info = new PythonEnvironmentInfo();

        // Find Python
        var pythonPaths = new[]
        {
            "python",
            "python3",
            @"C:\Python312\python.exe",
            @"C:\Python311\python.exe",
            @"C:\Python310\python.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Python", "Python311", "python.exe"),
        };

        foreach (var pythonPath in pythonPaths)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync(ct);
                    await process.WaitForExitAsync(ct);

                    if (process.ExitCode == 0)
                    {
                        info.PythonPath = pythonPath;
                        info.PythonVersion = output.Trim().Replace("Python ", "");
                        info.IsAvailable = true;
                        break;
                    }
                }
            }
            catch { }
        }

        if (!info.IsAvailable)
        {
            info.MissingPackages.Add("Python (3.10+ required)");
            return info;
        }

        // Check required packages
        var requiredPackages = new[]
        {
            ("torch", "PyTorch"),
            ("diffusers", "Diffusers"),
            ("transformers", "Transformers"),
            ("accelerate", "Accelerate"),
            ("safetensors", "Safetensors"),
            ("PIL", "Pillow")
        };

        foreach (var (package, displayName) in requiredPackages)
        {
            var hasPackage = await CheckPythonPackageAsync(info.PythonPath!, package, ct);
            if (hasPackage)
            {
                info.InstalledPackages.Add(displayName);
            }
            else
            {
                info.MissingPackages.Add(displayName);
            }
        }

        // Check PyTorch CUDA support
        if (info.InstalledPackages.Contains("PyTorch"))
        {
            info.HasCudaSupport = await CheckTorchCudaAsync(info.PythonPath!, ct);
        }

        info.IsReady = info.MissingPackages.Count == 0;

        return info;
    }

    private async Task<bool> CheckPythonPackageAsync(string pythonPath, string package, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"-c \"import {package}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            await process.WaitForExitAsync(ct);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckTorchCudaAsync(string pythonPath, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = "-c \"import torch; print(torch.cuda.is_available())\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = await process.StandardOutput.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return output.Trim().ToLower() == "true";
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get pip install command for missing packages
    /// </summary>
    public string GetInstallCommand(PythonEnvironmentInfo env, GpuInfo gpu)
    {
        var packages = new List<string>();

        if (env.MissingPackages.Contains("PyTorch"))
        {
            // Recommend appropriate PyTorch version
            if (gpu.Vendor == GpuVendor.Nvidia)
            {
                packages.Add("torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121");
            }
            else if (gpu.Vendor == GpuVendor.Amd)
            {
                packages.Add("torch torchvision torchaudio --index-url https://download.pytorch.org/whl/rocm5.6");
            }
            else
            {
                packages.Add("torch torchvision torchaudio");
            }
        }

        if (env.MissingPackages.Contains("Diffusers"))
            packages.Add("diffusers");
        if (env.MissingPackages.Contains("Transformers"))
            packages.Add("transformers");
        if (env.MissingPackages.Contains("Accelerate"))
            packages.Add("accelerate");
        if (env.MissingPackages.Contains("Safetensors"))
            packages.Add("safetensors");
        if (env.MissingPackages.Contains("Pillow"))
            packages.Add("Pillow");

        return packages.Count > 0
            ? $"pip install {string.Join(" ", packages)}"
            : "";
    }

    #endregion

    #region Helpers

    private static int ParseInt(string value)
    {
        if (int.TryParse(value.Replace(" ", ""), out var result))
            return result;
        return 0;
    }

    private static double ParseDouble(string value)
    {
        if (double.TryParse(value.Replace(" ", ""), out var result))
            return result;
        return 0;
    }

    private static int CalculateRecommendedBatchSize(GpuInfo gpu)
    {
        if (gpu.TotalVramGb >= 24) return 4;
        if (gpu.TotalVramGb >= 16) return 2;
        if (gpu.TotalVramGb >= 12) return 2;
        if (gpu.TotalVramGb >= 8) return 1;
        return 1;
    }

    private static int CalculateRecommendedMaxResolution(GpuInfo gpu)
    {
        if (gpu.TotalVramGb >= 16) return 1536;
        if (gpu.TotalVramGb >= 12) return 1280;
        if (gpu.TotalVramGb >= 8) return 1024;
        if (gpu.TotalVramGb >= 6) return 768;
        return 512;
    }

    #endregion
}

#region Models

public enum GpuVendor
{
    None,
    Nvidia,
    Amd,
    Intel
}

public class GpuInfo
{
    public GpuVendor Vendor { get; set; }
    public string Name { get; set; } = "";
    public double TotalVramGb { get; set; }
    public double FreeVramGb { get; set; }
    public double UsedVramGb => TotalVramGb - FreeVramGb;
    public double UsagePercent => TotalVramGb > 0 ? (UsedVramGb / TotalVramGb) * 100 : 0;

    public string? DriverVersion { get; set; }
    public string? CudaVersion { get; set; }
    public double ComputeCapability { get; set; }

    public int Temperature { get; set; }
    public double PowerUsageWatts { get; set; }
    public int UtilizationPercent { get; set; }

    public bool IsAvailable { get; set; }
    public bool SupportsCuda { get; set; }
    public bool SupportsRocm { get; set; }
    public bool SupportsOneApi { get; set; }
    public bool SupportsFp16 { get; set; }
    public bool SupportsBf16 { get; set; }

    public int RecommendedBatchSize { get; set; }
    public int RecommendedMaxResolution { get; set; }
    public bool CanLoadXL { get; set; }
    public bool CanLoadFlux { get; set; }

    public DateTime DetectedAt { get; set; }

    public string GetDisplayString()
    {
        if (!IsAvailable)
            return "No GPU - CPU Only";

        return $"{Name} ({TotalVramGb:F0}GB VRAM)";
    }
}

public class VramUsage
{
    public int TotalMb { get; set; }
    public int UsedMb { get; set; }
    public int FreeMb { get; set; }
    public double UsagePercent { get; set; }
    public bool IsAvailable { get; set; }
}

public class NvidiaGpuInfo
{
    public string Name { get; set; } = "";
    public int TotalMemoryMb { get; set; }
    public int FreeMemoryMb { get; set; }
    public int UsedMemoryMb { get; set; }
    public string? DriverVersion { get; set; }
    public string? CudaVersion { get; set; }
    public double ComputeCapability { get; set; }
    public int Temperature { get; set; }
    public double PowerUsage { get; set; }
    public int GpuUtilization { get; set; }
}

public class AmdGpuInfo
{
    public string Name { get; set; } = "";
    public int TotalMemoryMb { get; set; }
    public int FreeMemoryMb { get; set; }
}

public class IntelGpuInfo
{
    public string Name { get; set; } = "";
    public int TotalMemoryMb { get; set; }
}

public class PythonEnvironmentInfo
{
    public bool IsAvailable { get; set; }
    public bool IsReady { get; set; }
    public string? PythonPath { get; set; }
    public string? PythonVersion { get; set; }
    public bool HasCudaSupport { get; set; }
    public List<string> InstalledPackages { get; set; } = new();
    public List<string> MissingPackages { get; set; } = new();
}

#endregion
