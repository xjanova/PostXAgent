using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// ตรวจจับทรัพยากรระบบเพื่อเลือก AI Model ที่เหมาะสม
/// </summary>
public class SystemResourceDetector
{
    private readonly ILogger<SystemResourceDetector>? _logger;

    public SystemResourceDetector(ILogger<SystemResourceDetector>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// ข้อมูลทรัพยากรระบบ
    /// </summary>
    public class SystemResources
    {
        public long TotalRamMB { get; set; }
        public long AvailableRamMB { get; set; }
        public long TotalVramMB { get; set; }
        public string GpuName { get; set; } = "Unknown";
        public bool HasNvidiaGpu { get; set; }
        public bool HasAmdGpu { get; set; }
        public int CpuCores { get; set; }
        public string CpuName { get; set; } = "Unknown";

        // เพิ่มข้อมูลสำหรับ CPU optimization
        public int PhysicalCores { get; set; }
        public int LogicalCores { get; set; }
        public bool IsServerCpu { get; set; }
        public int RecommendedThreads { get; set; }
        public string CpuOptimizationNote { get; set; } = "";
    }

    /// <summary>
    /// ข้อมูล Model ที่แนะนำ
    /// </summary>
    public class ModelRecommendation
    {
        public string ModelName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Reason { get; set; } = "";
        public long RequiredRamMB { get; set; }
        public long RequiredVramMB { get; set; }
        public int Priority { get; set; }
        public bool IsRecommended { get; set; }
    }

    /// <summary>
    /// รายการ Model และความต้องการทรัพยากร
    /// </summary>
    private static readonly ModelRecommendation[] ModelSpecs = new[]
    {
        new ModelRecommendation
        {
            ModelName = "llama4:maverick",
            DisplayName = "Llama 4 Maverick",
            RequiredRamMB = 48000,  // 48GB RAM
            RequiredVramMB = 24000, // 24GB VRAM (RTX 4090/A100)
            Priority = 1,
            Reason = "คุณภาพสูงสุด สำหรับเครื่องที่มี GPU แรง"
        },
        new ModelRecommendation
        {
            ModelName = "llama4:scout",
            DisplayName = "Llama 4 Scout",
            RequiredRamMB = 32000,  // 32GB RAM
            RequiredVramMB = 16000, // 16GB VRAM (RTX 4080/4090)
            Priority = 2,
            Reason = "สมดุลระหว่างคุณภาพและความเร็ว"
        },
        new ModelRecommendation
        {
            ModelName = "llama4",
            DisplayName = "Llama 4",
            RequiredRamMB = 24000,  // 24GB RAM
            RequiredVramMB = 12000, // 12GB VRAM
            Priority = 3,
            Reason = "Llama 4 เวอร์ชันมาตรฐาน"
        },
        new ModelRecommendation
        {
            ModelName = "llama3.3:70b",
            DisplayName = "Llama 3.3 70B",
            RequiredRamMB = 48000,
            RequiredVramMB = 40000,
            Priority = 4,
            Reason = "Model ขนาดใหญ่ คุณภาพสูง"
        },
        new ModelRecommendation
        {
            ModelName = "llama3.1:8b",
            DisplayName = "Llama 3.1 8B",
            RequiredRamMB = 8000,   // 8GB RAM
            RequiredVramMB = 6000,  // 6GB VRAM
            Priority = 5,
            Reason = "สมดุลสำหรับเครื่องทั่วไป"
        },
        new ModelRecommendation
        {
            ModelName = "llama3.2:3b",
            DisplayName = "Llama 3.2 3B",
            RequiredRamMB = 4000,   // 4GB RAM
            RequiredVramMB = 3000,  // 3GB VRAM
            Priority = 6,
            Reason = "เร็ว ใช้ทรัพยากรน้อย"
        },
        new ModelRecommendation
        {
            ModelName = "qwen2.5:7b",
            DisplayName = "Qwen 2.5 7B",
            RequiredRamMB = 8000,
            RequiredVramMB = 6000,
            Priority = 7,
            Reason = "รองรับภาษาไทยดี"
        },
        new ModelRecommendation
        {
            ModelName = "mistral",
            DisplayName = "Mistral 7B",
            RequiredRamMB = 8000,
            RequiredVramMB = 6000,
            Priority = 8,
            Reason = "เร็ว ประสิทธิภาพดี"
        },
        new ModelRecommendation
        {
            ModelName = "llama2",
            DisplayName = "Llama 2",
            RequiredRamMB = 4000,
            RequiredVramMB = 4000,
            Priority = 9,
            Reason = "รองรับเครื่องรุ่นเก่า"
        }
    };

    /// <summary>
    /// ตรวจจับทรัพยากรระบบ
    /// </summary>
    public SystemResources DetectResources()
    {
        var resources = new SystemResources
        {
            CpuCores = Environment.ProcessorCount,
            LogicalCores = Environment.ProcessorCount
        };

        try
        {
            // Get RAM info
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GetWindowsMemoryInfo(resources);
                GetWindowsGpuInfo(resources);
                GetWindowsCpuInfo(resources);
                GetWindowsPhysicalCores(resources);
            }
            else
            {
                GetLinuxMemoryInfo(resources);
                GetLinuxPhysicalCores(resources);
            }

            // ตรวจจับว่าเป็น Server CPU หรือไม่
            DetectServerCpu(resources);

            // คำนวณ recommended threads
            CalculateRecommendedThreads(resources);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detect system resources, using defaults");
            // Fallback to safe defaults
            resources.TotalRamMB = 8000;
            resources.AvailableRamMB = 4000;
            resources.PhysicalCores = Environment.ProcessorCount / 2;
            resources.RecommendedThreads = Math.Max(2, resources.PhysicalCores - 1);
        }

        _logger?.LogInformation(
            "System Resources: RAM {TotalRam}MB, VRAM: {Vram}MB, GPU: {Gpu}, " +
            "CPU: {PhysicalCores} physical / {LogicalCores} logical cores, Recommended threads: {Threads}",
            resources.TotalRamMB, resources.TotalVramMB, resources.GpuName,
            resources.PhysicalCores, resources.LogicalCores, resources.RecommendedThreads);

        return resources;
    }

    /// <summary>
    /// เลือก Model ที่ดีที่สุดสำหรับทรัพยากรที่มี
    /// </summary>
    public ModelRecommendation GetBestModel(SystemResources? resources = null)
    {
        resources ??= DetectResources();

        var availableRam = resources.AvailableRamMB;
        var availableVram = resources.TotalVramMB;
        var hasGpu = resources.HasNvidiaGpu || resources.HasAmdGpu;

        _logger?.LogDebug("Selecting model for RAM: {Ram}MB, VRAM: {Vram}MB, HasGPU: {HasGpu}",
            availableRam, availableVram, hasGpu);

        // Sort by priority and filter by available resources
        var candidates = ModelSpecs
            .OrderBy(m => m.Priority)
            .ToList();

        foreach (var model in candidates)
        {
            // Check if we have enough resources
            bool ramOk = resources.TotalRamMB >= model.RequiredRamMB * 0.8; // 80% margin
            bool vramOk = !hasGpu || availableVram >= model.RequiredVramMB * 0.7; // 70% margin for GPU

            // For CPU-only, need more RAM
            if (!hasGpu)
            {
                ramOk = resources.TotalRamMB >= model.RequiredRamMB * 1.2; // Need 20% more RAM without GPU
            }

            if (ramOk && (vramOk || !hasGpu))
            {
                var recommendation = new ModelRecommendation
                {
                    ModelName = model.ModelName,
                    DisplayName = model.DisplayName,
                    RequiredRamMB = model.RequiredRamMB,
                    RequiredVramMB = model.RequiredVramMB,
                    Priority = model.Priority,
                    IsRecommended = true,
                    Reason = BuildRecommendationReason(model, resources, hasGpu)
                };

                _logger?.LogInformation("Recommended model: {Model} - {Reason}",
                    recommendation.ModelName, recommendation.Reason);

                return recommendation;
            }
        }

        // Fallback to smallest model
        var fallback = ModelSpecs.Last();
        return new ModelRecommendation
        {
            ModelName = fallback.ModelName,
            DisplayName = fallback.DisplayName,
            RequiredRamMB = fallback.RequiredRamMB,
            RequiredVramMB = fallback.RequiredVramMB,
            Priority = fallback.Priority,
            IsRecommended = true,
            Reason = "ทรัพยากรจำกัด - ใช้ model ขนาดเล็ก"
        };
    }

    /// <summary>
    /// รับรายการ Model ทั้งหมดพร้อมสถานะรองรับ
    /// </summary>
    public List<ModelRecommendation> GetAllModelsWithStatus(SystemResources? resources = null)
    {
        resources ??= DetectResources();
        var bestModel = GetBestModel(resources);

        return ModelSpecs.Select(m => new ModelRecommendation
        {
            ModelName = m.ModelName,
            DisplayName = m.DisplayName,
            RequiredRamMB = m.RequiredRamMB,
            RequiredVramMB = m.RequiredVramMB,
            Priority = m.Priority,
            IsRecommended = m.ModelName == bestModel.ModelName,
            Reason = m.ModelName == bestModel.ModelName
                ? bestModel.Reason
                : (CanRunModel(m, resources) ? m.Reason : "ทรัพยากรไม่เพียงพอ")
        }).ToList();
    }

    private bool CanRunModel(ModelRecommendation model, SystemResources resources)
    {
        return resources.TotalRamMB >= model.RequiredRamMB * 0.7;
    }

    private string BuildRecommendationReason(ModelRecommendation model, SystemResources resources, bool hasGpu)
    {
        var parts = new List<string> { model.Reason };

        if (hasGpu && resources.TotalVramMB >= model.RequiredVramMB)
        {
            parts.Add($"GPU {resources.GpuName} ({resources.TotalVramMB / 1000}GB VRAM)");
        }
        else if (!hasGpu)
        {
            parts.Add($"CPU mode ({resources.TotalRamMB / 1000}GB RAM)");
        }

        return string.Join(" | ", parts);
    }

    #region Windows-specific detection

    private void GetWindowsMemoryInfo(SystemResources resources)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                var totalBytes = Convert.ToInt64(obj["TotalPhysicalMemory"]);
                resources.TotalRamMB = totalBytes / (1024 * 1024);
            }

            // Get available memory
            using var perfSearcher = new ManagementObjectSearcher("SELECT AvailableBytes FROM Win32_PerfFormattedData_PerfOS_Memory");
            foreach (var obj in perfSearcher.Get())
            {
                var availableBytes = Convert.ToInt64(obj["AvailableBytes"]);
                resources.AvailableRamMB = availableBytes / (1024 * 1024);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Windows memory info");
            // Fallback using GC
            resources.TotalRamMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            resources.AvailableRamMB = resources.TotalRamMB / 2;
        }
    }

    private void GetWindowsGpuInfo(SystemResources resources)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                var vramBytes = Convert.ToInt64(obj["AdapterRAM"] ?? 0);

                // Check for NVIDIA
                if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                {
                    resources.HasNvidiaGpu = true;
                    resources.GpuName = name;
                    resources.TotalVramMB = vramBytes / (1024 * 1024);

                    // nvidia-smi gives more accurate VRAM
                    TryGetNvidiaVram(resources);
                    break;
                }
                // Check for AMD
                else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                         name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                {
                    resources.HasAmdGpu = true;
                    resources.GpuName = name;
                    resources.TotalVramMB = vramBytes / (1024 * 1024);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Windows GPU info");
        }
    }

    private void TryGetNvidiaVram(SystemResources resources)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=memory.total --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                if (int.TryParse(output, out var vramMB))
                {
                    resources.TotalVramMB = vramMB;
                }
            }
        }
        catch
        {
            // nvidia-smi not available, use WMI value
        }
    }

    private void GetWindowsCpuInfo(SystemResources resources)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                resources.CpuName = obj["Name"]?.ToString() ?? "Unknown";
                break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get CPU info");
        }
    }

    #endregion

    #region Linux-specific detection

    private void GetLinuxMemoryInfo(SystemResources resources)
    {
        try
        {
            var memInfo = File.ReadAllText("/proc/meminfo");
            var lines = memInfo.Split('\n');

            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    var value = ExtractMemValue(line);
                    resources.TotalRamMB = value / 1024; // kB to MB
                }
                else if (line.StartsWith("MemAvailable:"))
                {
                    var value = ExtractMemValue(line);
                    resources.AvailableRamMB = value / 1024;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Linux memory info");
            resources.TotalRamMB = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            resources.AvailableRamMB = resources.TotalRamMB / 2;
        }
    }

    private long ExtractMemValue(string line)
    {
        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && long.TryParse(parts[1], out var value))
        {
            return value;
        }
        return 0;
    }

    #endregion

    #region CPU Physical Cores Detection

    private void GetWindowsPhysicalCores(SystemResources resources)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT NumberOfCores FROM Win32_Processor");

            int totalCores = 0;
            foreach (var obj in searcher.Get())
            {
                totalCores += Convert.ToInt32(obj["NumberOfCores"]);
            }

            resources.PhysicalCores = totalCores > 0 ? totalCores : Environment.ProcessorCount / 2;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get physical cores, using estimate");
            resources.PhysicalCores = Environment.ProcessorCount / 2;
        }
    }

    private void GetLinuxPhysicalCores(SystemResources resources)
    {
        try
        {
            var cpuInfo = File.ReadAllText("/proc/cpuinfo");
            var coreIds = new HashSet<string>();
            var physicalIds = new HashSet<string>();

            string? currentPhysicalId = null;

            foreach (var line in cpuInfo.Split('\n'))
            {
                if (line.StartsWith("physical id"))
                {
                    currentPhysicalId = line.Split(':').LastOrDefault()?.Trim();
                    if (currentPhysicalId != null)
                        physicalIds.Add(currentPhysicalId);
                }
                else if (line.StartsWith("core id") && currentPhysicalId != null)
                {
                    var coreId = line.Split(':').LastOrDefault()?.Trim();
                    if (coreId != null)
                        coreIds.Add($"{currentPhysicalId}:{coreId}");
                }
            }

            resources.PhysicalCores = coreIds.Count > 0 ? coreIds.Count : Environment.ProcessorCount / 2;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Linux physical cores");
            resources.PhysicalCores = Environment.ProcessorCount / 2;
        }
    }

    private void DetectServerCpu(SystemResources resources)
    {
        var serverKeywords = new[]
        {
            "Xeon", "EPYC", "Opteron", "Threadripper",
            "POWER", "SPARC", "Itanium"
        };

        resources.IsServerCpu = serverKeywords.Any(keyword =>
            resources.CpuName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        if (resources.IsServerCpu)
        {
            _logger?.LogInformation("Detected server CPU: {CpuName}", resources.CpuName);
        }
    }

    private void CalculateRecommendedThreads(SystemResources resources)
    {
        var hasGpu = resources.HasNvidiaGpu || resources.HasAmdGpu;
        var physicalCores = resources.PhysicalCores > 0 ? resources.PhysicalCores : Environment.ProcessorCount / 2;

        if (resources.IsServerCpu)
        {
            // Server CPU (Xeon/EPYC): ใช้ได้มากกว่า แต่เก็บ 2-4 cores สำหรับ OS
            resources.RecommendedThreads = Math.Max(4, physicalCores - 2);

            // จำกัดไว้ที่ 64 threads สำหรับประสิทธิภาพ
            if (resources.RecommendedThreads > 64)
            {
                resources.RecommendedThreads = 64;
            }

            resources.CpuOptimizationNote = hasGpu
                ? $"Server CPU พร้อม GPU - ใช้ {resources.RecommendedThreads} threads"
                : $"Server CPU (CPU-only mode) - ใช้ {resources.RecommendedThreads} threads, " +
                  "เหมาะสำหรับ multi-core inference";
        }
        else
        {
            // Consumer CPU: ใช้ physical cores - 1
            resources.RecommendedThreads = Math.Max(2, physicalCores - 1);

            resources.CpuOptimizationNote = hasGpu
                ? $"Desktop CPU พร้อม GPU - ใช้ {resources.RecommendedThreads} threads"
                : $"Desktop CPU (CPU-only mode) - ใช้ {resources.RecommendedThreads} threads";
        }

        _logger?.LogDebug(
            "Calculated recommended threads: {Threads} (Physical: {Physical}, Server: {IsServer}, HasGPU: {HasGpu})",
            resources.RecommendedThreads, physicalCores, resources.IsServerCpu, hasGpu);
    }

    #endregion
}
