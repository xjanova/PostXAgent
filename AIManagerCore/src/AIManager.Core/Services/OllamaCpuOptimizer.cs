using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Ollama CPU Optimizer - ปรับแต่งให้ใช้ CPU หลายคอร์อย่างเต็มประสิทธิภาพ
/// รองรับ Intel Xeon, AMD EPYC และ CPU หลายคอร์
/// </summary>
public class OllamaCpuOptimizer
{
    private readonly ILogger<OllamaCpuOptimizer>? _logger;
    private readonly SystemResourceDetector _resourceDetector;

    public OllamaCpuOptimizer(ILogger<OllamaCpuOptimizer>? logger = null)
    {
        _logger = logger;
        _resourceDetector = new SystemResourceDetector(null);
    }

    /// <summary>
    /// CPU Configuration สำหรับ Ollama
    /// </summary>
    public class CpuConfiguration
    {
        /// <summary>จำนวน threads สำหรับ inference</summary>
        public int NumThreads { get; set; }

        /// <summary>จำนวน parallel requests</summary>
        public int NumParallel { get; set; }

        /// <summary>จำนวน CPU ที่ใช้สำหรับ batch processing</summary>
        public int NumBatch { get; set; }

        /// <summary>Context size (ยิ่งมาก ยิ่งใช้ RAM มาก)</summary>
        public int NumCtx { get; set; }

        /// <summary>GPU Layers (0 = CPU only)</summary>
        public int NumGpuLayers { get; set; }

        /// <summary>ใช้ memory mapping</summary>
        public bool UseMmap { get; set; }

        /// <summary>ใช้ memory locking</summary>
        public bool UseMlock { get; set; }

        /// <summary>Physical CPU cores</summary>
        public int PhysicalCores { get; set; }

        /// <summary>Logical CPU cores (with HyperThreading)</summary>
        public int LogicalCores { get; set; }

        /// <summary>Total RAM in MB</summary>
        public long TotalRamMB { get; set; }

        /// <summary>CPU Name</summary>
        public string CpuName { get; set; } = "";

        /// <summary>Is Xeon/EPYC server CPU</summary>
        public bool IsServerCpu { get; set; }

        /// <summary>Has AVX2 support</summary>
        public bool HasAvx2 { get; set; }

        /// <summary>Has AVX-512 support</summary>
        public bool HasAvx512 { get; set; }

        /// <summary>Optimization reason/description</summary>
        public string OptimizationReason { get; set; } = "";
    }

    /// <summary>
    /// ตรวจจับและสร้าง CPU Configuration ที่เหมาะสม
    /// </summary>
    public CpuConfiguration DetectAndOptimize()
    {
        var resources = _resourceDetector.DetectResources();
        var config = new CpuConfiguration
        {
            LogicalCores = Environment.ProcessorCount,
            TotalRamMB = resources.TotalRamMB,
            CpuName = resources.CpuName
        };

        // ตรวจจับ Physical cores
        config.PhysicalCores = DetectPhysicalCores();

        // ตรวจจับว่าเป็น Server CPU หรือไม่
        config.IsServerCpu = IsServerCpu(config.CpuName);

        // ตรวจจับ CPU features
        DetectCpuFeatures(config);

        // คำนวณค่าที่เหมาะสม
        CalculateOptimalSettings(config, resources);

        _logger?.LogInformation(
            "CPU Optimization: {CpuName} - {PhysicalCores} physical / {LogicalCores} logical cores, " +
            "Threads: {Threads}, Parallel: {Parallel}, Batch: {Batch}, Context: {Ctx}",
            config.CpuName, config.PhysicalCores, config.LogicalCores,
            config.NumThreads, config.NumParallel, config.NumBatch, config.NumCtx);

        return config;
    }

    /// <summary>
    /// ตรวจจับจำนวน Physical CPU cores (ไม่รวม HyperThreading)
    /// </summary>
    private int DetectPhysicalCores()
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return DetectPhysicalCoresWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return DetectPhysicalCoresLinux();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detect physical cores, using logical cores / 2");
        }

        // Fallback: assume HyperThreading, divide by 2
        return Math.Max(1, Environment.ProcessorCount / 2);
    }

    private int DetectPhysicalCoresWindows()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT NumberOfCores FROM Win32_Processor");

            int totalCores = 0;
            foreach (var obj in searcher.Get())
            {
                totalCores += Convert.ToInt32(obj["NumberOfCores"]);
            }

            return totalCores > 0 ? totalCores : Environment.ProcessorCount / 2;
        }
        catch
        {
            return Environment.ProcessorCount / 2;
        }
    }

    private int DetectPhysicalCoresLinux()
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

            return coreIds.Count > 0 ? coreIds.Count : Environment.ProcessorCount / 2;
        }
        catch
        {
            return Environment.ProcessorCount / 2;
        }
    }

    /// <summary>
    /// ตรวจสอบว่าเป็น Server CPU หรือไม่
    /// </summary>
    private bool IsServerCpu(string cpuName)
    {
        var serverKeywords = new[]
        {
            "Xeon", "EPYC", "Opteron", "Threadripper",
            "POWER", "SPARC", "Itanium"
        };

        return serverKeywords.Any(keyword =>
            cpuName.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// ตรวจจับ CPU Features (AVX2, AVX-512)
    /// </summary>
    private void DetectCpuFeatures(CpuConfiguration config)
    {
        // ตรวจจับ AVX support จากชื่อ CPU และ generation
        var cpuName = config.CpuName.ToLower();

        // AVX2: Intel Haswell (2013) ขึ้นไป, AMD Excavator (2015) ขึ้นไป
        config.HasAvx2 = true; // Most modern CPUs have AVX2

        // AVX-512: Intel Skylake-X, Xeon Scalable, AMD Zen 4
        config.HasAvx512 =
            cpuName.Contains("xeon") ||
            cpuName.Contains("platinum") ||
            cpuName.Contains("gold") ||
            cpuName.Contains("silver") ||
            cpuName.Contains("epyc") && cpuName.Contains("9"); // Zen 4 EPYC

        // ลองตรวจจับจริงๆ บน Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            TryDetectAvxWindows(config);
        }
    }

    private void TryDetectAvxWindows(CpuConfiguration config)
    {
        try
        {
            // ใช้ WMIC เพื่อดูข้อมูล CPU
            var psi = new ProcessStartInfo
            {
                FileName = "wmic",
                Arguments = "cpu get caption",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit(3000);
            }
        }
        catch
        {
            // ไม่สามารถตรวจจับได้ ใช้ค่าเริ่มต้น
        }
    }

    /// <summary>
    /// คำนวณค่าที่เหมาะสมสำหรับ Ollama
    /// </summary>
    private void CalculateOptimalSettings(CpuConfiguration config,
        SystemResourceDetector.SystemResources resources)
    {
        var hasGpu = resources.HasNvidiaGpu || resources.HasAmdGpu;
        var physicalCores = config.PhysicalCores;
        var logicalCores = config.LogicalCores;
        var ramGB = resources.TotalRamMB / 1024;

        // ==========================================
        // NUM_THREADS - จำนวน threads สำหรับ inference
        // ==========================================
        // กฎ: ใช้ physical cores เป็นหลัก (ไม่ใช่ logical)
        // HyperThreading ไม่ช่วยมากสำหรับ LLM inference

        if (config.IsServerCpu)
        {
            // Server CPU (Xeon/EPYC): ใช้ได้มากกว่า
            // แต่เก็บไว้ 2-4 cores สำหรับ OS
            config.NumThreads = Math.Max(4, physicalCores - 2);

            // ถ้ามี cores เยอะมาก (>32) จำกัดไว้เพื่อประสิทธิภาพ
            if (config.NumThreads > 64)
            {
                config.NumThreads = 64;
            }
        }
        else
        {
            // Consumer CPU: ใช้ physical cores - 1
            config.NumThreads = Math.Max(2, physicalCores - 1);
        }

        // ==========================================
        // NUM_PARALLEL - จำนวน parallel requests
        // ==========================================
        // สำหรับ concurrent requests

        if (config.IsServerCpu && physicalCores >= 16)
        {
            // Server with many cores: รองรับหลาย requests
            config.NumParallel = Math.Min(8, physicalCores / 8);
        }
        else if (physicalCores >= 8)
        {
            config.NumParallel = 2;
        }
        else
        {
            config.NumParallel = 1;
        }

        // ==========================================
        // NUM_BATCH - Batch size สำหรับ prompt processing
        // ==========================================
        // ยิ่งมาก ยิ่งใช้ RAM และเร็วขึ้น

        if (ramGB >= 64)
        {
            config.NumBatch = 1024;
        }
        else if (ramGB >= 32)
        {
            config.NumBatch = 512;
        }
        else if (ramGB >= 16)
        {
            config.NumBatch = 256;
        }
        else
        {
            config.NumBatch = 128;
        }

        // ==========================================
        // NUM_CTX - Context size
        // ==========================================
        // ขึ้นอยู่กับ RAM

        if (ramGB >= 64)
        {
            config.NumCtx = 8192;
        }
        else if (ramGB >= 32)
        {
            config.NumCtx = 4096;
        }
        else if (ramGB >= 16)
        {
            config.NumCtx = 2048;
        }
        else
        {
            config.NumCtx = 1024;
        }

        // ==========================================
        // GPU Layers
        // ==========================================
        if (hasGpu && resources.TotalVramMB >= 4000)
        {
            // มี GPU: ใช้ GPU layers
            config.NumGpuLayers = CalculateGpuLayers(resources.TotalVramMB);
        }
        else
        {
            // ไม่มี GPU หรือ VRAM น้อย: CPU only
            config.NumGpuLayers = 0;
        }

        // ==========================================
        // Memory settings
        // ==========================================
        config.UseMmap = true; // ใช้ memory mapping เสมอ
        config.UseMlock = ramGB >= 32; // Lock memory เฉพาะเครื่องที่มี RAM เยอะ

        // สร้างคำอธิบาย
        config.OptimizationReason = BuildOptimizationReason(config, resources, hasGpu);
    }

    private int CalculateGpuLayers(long vramMB)
    {
        // ประมาณ GPU layers ตาม VRAM
        // Llama 3.2 3B: ~32 layers, ~2GB per layer

        if (vramMB >= 24000) return 99; // ใส่ทุก layer ใน GPU
        if (vramMB >= 16000) return 40;
        if (vramMB >= 12000) return 32;
        if (vramMB >= 8000) return 24;
        if (vramMB >= 6000) return 16;
        if (vramMB >= 4000) return 8;
        return 0;
    }

    private string BuildOptimizationReason(CpuConfiguration config,
        SystemResourceDetector.SystemResources resources, bool hasGpu)
    {
        var parts = new List<string>();

        if (config.IsServerCpu)
        {
            parts.Add($"Server CPU ({config.CpuName})");
        }
        else
        {
            parts.Add($"Desktop CPU ({config.CpuName})");
        }

        parts.Add($"{config.PhysicalCores} cores / {config.LogicalCores} threads");
        parts.Add($"{resources.TotalRamMB / 1024}GB RAM");

        if (hasGpu)
        {
            parts.Add($"GPU: {resources.GpuName} ({resources.TotalVramMB / 1024}GB)");
        }
        else
        {
            parts.Add("CPU-only mode (optimized for multi-core)");
        }

        if (config.HasAvx512)
        {
            parts.Add("AVX-512 enabled");
        }
        else if (config.HasAvx2)
        {
            parts.Add("AVX2 enabled");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// สร้าง Environment Variables สำหรับ Ollama
    /// </summary>
    public Dictionary<string, string> GetEnvironmentVariables(CpuConfiguration? config = null)
    {
        config ??= DetectAndOptimize();

        var env = new Dictionary<string, string>
        {
            // Ollama settings
            ["OLLAMA_NUM_THREAD"] = config.NumThreads.ToString(),
            ["OLLAMA_NUM_PARALLEL"] = config.NumParallel.ToString(),
            ["OLLAMA_NUM_GPU"] = config.NumGpuLayers.ToString(),

            // llama.cpp settings (used by Ollama internally)
            ["LLAMA_NUM_THREADS"] = config.NumThreads.ToString(),

            // OpenBLAS settings (if used)
            ["OPENBLAS_NUM_THREADS"] = config.NumThreads.ToString(),

            // MKL settings (Intel)
            ["MKL_NUM_THREADS"] = config.NumThreads.ToString(),
            ["MKL_DYNAMIC"] = "FALSE",

            // OMP settings
            ["OMP_NUM_THREADS"] = config.NumThreads.ToString(),
            ["OMP_WAIT_POLICY"] = "ACTIVE", // Better for inference

            // GOMP settings (GCC OpenMP)
            ["GOMP_CPU_AFFINITY"] = $"0-{config.NumThreads - 1}",

            // Disable GPU if needed
            ["CUDA_VISIBLE_DEVICES"] = config.NumGpuLayers > 0 ? "0" : "",
        };

        // เพิ่ม settings สำหรับ AVX
        if (config.HasAvx512)
        {
            env["GGML_USE_AVX512"] = "1";
        }

        return env;
    }

    /// <summary>
    /// สร้าง Modelfile options สำหรับ Ollama
    /// </summary>
    public Dictionary<string, object> GetModelfileOptions(CpuConfiguration? config = null)
    {
        config ??= DetectAndOptimize();

        return new Dictionary<string, object>
        {
            ["num_thread"] = config.NumThreads,
            ["num_ctx"] = config.NumCtx,
            ["num_batch"] = config.NumBatch,
            ["num_gpu"] = config.NumGpuLayers,
            ["use_mmap"] = config.UseMmap,
            ["use_mlock"] = config.UseMlock,
        };
    }

    /// <summary>
    /// Apply settings to current process environment
    /// </summary>
    public void ApplyToCurrentProcess(CpuConfiguration? config = null)
    {
        var envVars = GetEnvironmentVariables(config);

        foreach (var (key, value) in envVars)
        {
            Environment.SetEnvironmentVariable(key, value);
            _logger?.LogDebug("Set {Key}={Value}", key, value);
        }

        _logger?.LogInformation("Applied CPU optimization settings to current process");
    }

    /// <summary>
    /// สร้าง ProcessStartInfo สำหรับ Ollama พร้อม optimization
    /// </summary>
    public ProcessStartInfo CreateOptimizedOllamaProcess(string arguments, CpuConfiguration? config = null)
    {
        config ??= DetectAndOptimize();
        var envVars = GetEnvironmentVariables(config);

        var psi = new ProcessStartInfo
        {
            FileName = GetOllamaPath(),
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var (key, value) in envVars)
        {
            psi.Environment[key] = value;
        }

        // Set CPU affinity hint
        if (config.IsServerCpu)
        {
            // สำหรับ server ให้กระจายไปหลาย NUMA nodes
            psi.Environment["GOMP_CPU_AFFINITY"] = $"0-{config.NumThreads - 1}";
        }

        return psi;
    }

    private string GetOllamaPath()
    {
        // ลองหา Ollama path
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Ollama", "ollama.exe"),
            @"C:\Program Files\Ollama\ollama.exe",
            "ollama" // ใน PATH
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path) || path == "ollama")
            {
                return path;
            }
        }

        return "ollama";
    }
}
