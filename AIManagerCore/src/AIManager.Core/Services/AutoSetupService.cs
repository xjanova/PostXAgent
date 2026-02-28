using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Automatic setup service for Python, PyTorch, and AI dependencies
/// ติดตั้ง Python และ packages ที่จำเป็นโดยอัตโนมัติ
/// </summary>
public class AutoSetupService
{
    private readonly ILogger<AutoSetupService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly string _installDir;
    private readonly LocalGpuService _gpuService;

    // Python embedded version URLs
    private const string PYTHON_VERSION = "3.11.7";
    private const string PYTHON_EMBED_URL = "https://www.python.org/ftp/python/3.11.7/python-3.11.7-embed-amd64.zip";
    private const string GET_PIP_URL = "https://bootstrap.pypa.io/get-pip.py";

    /// <summary>
    /// Event for setup progress updates
    /// </summary>
    public event EventHandler<SetupProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Gets the Python executable path
    /// </summary>
    public string PythonPath => Path.Combine(_installDir, "python", "python.exe");

    /// <summary>
    /// Gets whether Python is installed locally
    /// </summary>
    public bool IsPythonInstalled => File.Exists(PythonPath);

    /// <summary>
    /// Gets the installation directory path
    /// </summary>
    public string InstallDirectory => _installDir;

    public AutoSetupService(LocalGpuService? gpuService = null, ILogger<AutoSetupService>? logger = null)
    {
        _logger = logger;
        _gpuService = gpuService ?? new LocalGpuService();
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PostXAgent/1.0");

        // Setup installation directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _installDir = Path.Combine(appData, "PostXAgent");
        Directory.CreateDirectory(_installDir);
    }

    #region Full Auto Setup

    /// <summary>
    /// Perform complete automatic setup - installs everything needed
    /// </summary>
    public async Task<AutoSetupResult> PerformFullSetupAsync(CancellationToken ct = default)
    {
        var result = new AutoSetupResult();
        var startTime = DateTime.Now;

        try
        {
            _logger?.LogInformation("Starting full automatic setup...");
            ReportProgress("Starting automatic setup...", 0, SetupPhase.Starting);

            // Step 1: Detect GPU
            ReportProgress("Detecting GPU...", 5, SetupPhase.DetectingGpu);
            var gpuInfo = await _gpuService.DetectGpuAsync(forceRefresh: true, ct: ct);
            result.GpuInfo = gpuInfo;

            if (gpuInfo.IsAvailable)
            {
                _logger?.LogInformation("GPU detected: {Name} ({Vram}GB VRAM)", gpuInfo.Name, gpuInfo.TotalVramGb);
                ReportProgress($"GPU detected: {gpuInfo.Name}", 10, SetupPhase.DetectingGpu);
            }
            else
            {
                _logger?.LogWarning("No GPU detected, will use CPU mode");
                ReportProgress("No GPU detected - CPU mode will be used", 10, SetupPhase.DetectingGpu);
            }

            // Step 2: Install Python if needed
            ReportProgress("Checking Python installation...", 15, SetupPhase.InstallingPython);

            if (!IsPythonInstalled)
            {
                _logger?.LogInformation("Python not found, installing embedded Python {Version}...", PYTHON_VERSION);
                await InstallEmbeddedPythonAsync(ct);
                result.PythonInstalled = true;
            }
            else
            {
                _logger?.LogInformation("Python already installed at {Path}", PythonPath);
                ReportProgress("Python already installed", 30, SetupPhase.InstallingPython);
            }

            // Step 3: Install pip if needed
            ReportProgress("Checking pip...", 35, SetupPhase.InstallingPip);
            if (!await IsPipInstalledAsync(ct))
            {
                _logger?.LogInformation("Installing pip...");
                await InstallPipAsync(ct);
                result.PipInstalled = true;
            }
            else
            {
                ReportProgress("pip already installed", 40, SetupPhase.InstallingPip);
            }

            // Step 4: Install PyTorch with appropriate CUDA version
            ReportProgress("Installing PyTorch...", 45, SetupPhase.InstallingPyTorch);
            await InstallPyTorchAsync(gpuInfo, ct);
            result.PyTorchInstalled = true;

            // Step 5: Install Diffusers and dependencies
            ReportProgress("Installing AI packages (diffusers, transformers)...", 70, SetupPhase.InstallingPackages);
            await InstallAIPackagesAsync(ct);
            result.PackagesInstalled = true;

            // Step 6: Quick verification (skip slow verification checks to avoid hanging)
            ReportProgress("Completing setup...", 95, SetupPhase.Complete);

            // Do a quick check - just verify files exist
            var verification = new VerificationResult();
            verification.HasPython = File.Exists(PythonPath);
            verification.HasPyTorch = File.Exists(Path.Combine(_installDir, "python", "Lib", "site-packages", "torch", "__init__.py"));
            verification.HasDiffusers = File.Exists(Path.Combine(_installDir, "python", "Lib", "site-packages", "diffusers", "__init__.py"));
            verification.HasTransformers = File.Exists(Path.Combine(_installDir, "python", "Lib", "site-packages", "transformers", "__init__.py"));
            verification.IsValid = verification.HasPython && verification.HasPyTorch && verification.HasDiffusers && verification.HasTransformers;

            // Set version strings from package folders if available
            verification.PythonVersion = $"Python {PYTHON_VERSION}";
            verification.PyTorchVersion = "PyTorch (installed)";
            verification.DiffusersVersion = "Diffusers (installed)";
            verification.TransformersVersion = "Transformers (installed)";

            result.VerificationResult = verification;

            if (verification.IsValid)
            {
                result.Success = true;
                result.Message = "Setup completed successfully!";
                ReportProgress("Setup completed successfully!", 100, SetupPhase.Complete);
                _logger?.LogInformation("Full setup completed in {Duration}", DateTime.Now - startTime);
            }
            else
            {
                // Check what's missing
                var missing = new List<string>();
                if (!verification.HasPython) missing.Add("Python");
                if (!verification.HasPyTorch) missing.Add("PyTorch");
                if (!verification.HasDiffusers) missing.Add("Diffusers");
                if (!verification.HasTransformers) missing.Add("Transformers");

                result.Success = false;
                result.Message = $"Setup incomplete: Missing {string.Join(", ", missing)}";
                ReportProgress($"Setup incomplete", 100, SetupPhase.Complete);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Setup was cancelled by user");
            ReportProgress("Setup cancelled", -1, SetupPhase.Error);
            throw; // Re-throw to let UI handle cancellation
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Setup failed");
            result.Success = false;
            result.Message = $"Setup failed: {ex.Message}";
            result.Error = ex;
            ReportProgress($"Setup failed: {ex.Message}", -1, SetupPhase.Error);
            return result;
        }
    }

    #endregion

    #region Python Installation

    /// <summary>
    /// Install embedded Python (no admin rights required)
    /// </summary>
    private async Task InstallEmbeddedPythonAsync(CancellationToken ct)
    {
        var pythonDir = Path.Combine(_installDir, "python");
        var zipPath = Path.Combine(_installDir, "python-embed.zip");

        try
        {
            // Download Python embedded
            ReportProgress("Downloading Python...", 20, SetupPhase.InstallingPython);
            _logger?.LogInformation("Downloading Python from {Url}", PYTHON_EMBED_URL);

            using (var response = await _httpClient.GetAsync(PYTHON_EMBED_URL, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await using var downloadStream = await response.Content.ReadAsStreamAsync(ct);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await downloadStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var progress = 20 + (int)((downloadedBytes / (double)totalBytes) * 8);
                        ReportProgress($"Downloading Python... {downloadedBytes / 1024 / 1024}MB", progress, SetupPhase.InstallingPython);
                    }
                }
            }

            // Extract Python
            ReportProgress("Extracting Python...", 28, SetupPhase.InstallingPython);
            _logger?.LogInformation("Extracting Python to {Dir}", pythonDir);

            if (Directory.Exists(pythonDir))
            {
                Directory.Delete(pythonDir, true);
            }

            ZipFile.ExtractToDirectory(zipPath, pythonDir);

            // Modify python311._pth to enable site-packages
            var pthFile = Path.Combine(pythonDir, "python311._pth");
            if (File.Exists(pthFile))
            {
                var content = await File.ReadAllTextAsync(pthFile, ct);
                // Uncomment import site
                content = content.Replace("#import site", "import site");
                // Add Lib/site-packages
                if (!content.Contains("Lib/site-packages"))
                {
                    content += "\nLib/site-packages\n";
                }
                await File.WriteAllTextAsync(pthFile, content, ct);
            }

            // Create Lib/site-packages directory
            Directory.CreateDirectory(Path.Combine(pythonDir, "Lib", "site-packages"));

            ReportProgress("Python installed successfully", 30, SetupPhase.InstallingPython);
            _logger?.LogInformation("Python installed successfully");
        }
        finally
        {
            // Cleanup
            if (File.Exists(zipPath))
            {
                try { File.Delete(zipPath); } catch { }
            }
        }
    }

    /// <summary>
    /// Check if pip is installed
    /// </summary>
    private async Task<bool> IsPipInstalledAsync(CancellationToken ct)
    {
        try
        {
            var result = await RunPythonCommandAsync("-m pip --version", ct);
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Install pip
    /// </summary>
    private async Task InstallPipAsync(CancellationToken ct)
    {
        var getPipPath = Path.Combine(_installDir, "get-pip.py");

        try
        {
            // Download get-pip.py
            ReportProgress("Downloading pip installer...", 36, SetupPhase.InstallingPip);
            var getPipContent = await _httpClient.GetStringAsync(GET_PIP_URL, ct);
            await File.WriteAllTextAsync(getPipPath, getPipContent, ct);

            // Run get-pip.py
            ReportProgress("Installing pip...", 38, SetupPhase.InstallingPip);
            var result = await RunPythonCommandAsync($"\"{getPipPath}\" --no-warn-script-location", ct, timeoutMinutes: 5);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Failed to install pip: {result.Error}");
            }

            ReportProgress("pip installed successfully", 40, SetupPhase.InstallingPip);
            _logger?.LogInformation("pip installed successfully");
        }
        finally
        {
            if (File.Exists(getPipPath))
            {
                try { File.Delete(getPipPath); } catch { }
            }
        }
    }

    #endregion

    #region PyTorch Installation

    /// <summary>
    /// Install PyTorch with appropriate CUDA support
    /// </summary>
    private async Task InstallPyTorchAsync(GpuInfo gpuInfo, CancellationToken ct)
    {
        string pipArgs;
        string cudaInfo;

        if (gpuInfo.Vendor == GpuVendor.Nvidia && gpuInfo.IsAvailable)
        {
            // Install PyTorch with CUDA
            // Determine CUDA version based on compute capability
            var cudaVersion = gpuInfo.ComputeCapability >= 8.0 ? "cu121" : "cu118";
            _logger?.LogInformation("Installing PyTorch with CUDA {Version}", cudaVersion);
            cudaInfo = $"CUDA {cudaVersion}";

            pipArgs = $"install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/{cudaVersion}";
        }
        else if (gpuInfo.Vendor == GpuVendor.Amd && gpuInfo.IsAvailable)
        {
            // Install PyTorch with ROCm
            _logger?.LogInformation("Installing PyTorch with ROCm");
            cudaInfo = "ROCm";

            pipArgs = "install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/rocm5.6";
        }
        else
        {
            // CPU only
            _logger?.LogInformation("Installing PyTorch (CPU only)");
            cudaInfo = "CPU";

            pipArgs = "install torch torchvision torchaudio";
        }

        ReportProgress($"กำลังดาวน์โหลด PyTorch ({cudaInfo})... ขนาดประมาณ 2-3 GB", 50, SetupPhase.InstallingPyTorch);
        ReportProgress($"อาจใช้เวลา 5-15 นาที กรุณารอสักครู่...", 51, SetupPhase.InstallingPyTorch);

        // Use progress callback version
        var result = await RunPipCommandWithProgressAsync(pipArgs, ct,
            timeoutMinutes: 60, // Increase timeout to 60 minutes for slow connections
            progressCallback: (msg, isDownloading) =>
            {
                if (isDownloading)
                {
                    // Parse download progress from pip output
                    var progress = ParsePipDownloadProgress(msg);
                    if (progress > 0)
                    {
                        // Map download progress (0-100) to our range (50-65)
                        var mappedProgress = 50 + (int)(progress * 0.15);
                        ReportProgress($"กำลังดาวน์โหลด: {msg}", mappedProgress, SetupPhase.InstallingPyTorch);
                    }
                }
            });

        if (result.ExitCode != 0)
        {
            throw new Exception($"Failed to install PyTorch: {result.Error}");
        }

        ReportProgress("PyTorch ติดตั้งสำเร็จ", 65, SetupPhase.InstallingPyTorch);
        _logger?.LogInformation("PyTorch installed successfully");
    }

    /// <summary>
    /// Parse pip download progress from output
    /// </summary>
    private static int ParsePipDownloadProgress(string output)
    {
        // pip outputs progress like: "Downloading torch-2.1.0+cu121-cp311-cp311-win_amd64.whl (2.4 GB) ━━━━━━━━━━ 50%"
        // or: "━━━━━━━━━━━━━━━━━━━━ 100%"
        try
        {
            var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+)%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
            {
                return percent;
            }
        }
        catch { }
        return 0;
    }

    #endregion

    #region AI Packages Installation

    /// <summary>
    /// Install AI packages (diffusers, transformers, etc.)
    /// </summary>
    private async Task InstallAIPackagesAsync(CancellationToken ct)
    {
        // Core packages + production requirements for full ComfyUI-like functionality
        var packages = new[]
        {
            // Core diffusion packages
            ("diffusers", "Installing diffusers...", 70),
            ("transformers", "Installing transformers...", 73),
            ("accelerate", "Installing accelerate...", 75),
            ("safetensors", "Installing safetensors...", 76),
            ("Pillow", "Installing Pillow...", 77),
            ("scipy", "Installing scipy...", 78),
            // Server packages
            ("fastapi", "Installing FastAPI (for server)...", 79),
            ("uvicorn[standard]", "Installing uvicorn (ASGI server)...", 80),
            ("pydantic", "Installing pydantic...", 81),
            // ControlNet preprocessing
            ("controlnet_aux", "Installing ControlNet preprocessors...", 82),
            ("opencv-python", "Installing OpenCV...", 83),
            // Upscaling (Real-ESRGAN)
            ("realesrgan", "Installing Real-ESRGAN upscaler...", 85),
            ("basicsr", "Installing BasicSR...", 86),
            // Additional utilities
            ("omegaconf", "Installing OmegaConf...", 87),
            ("einops", "Installing einops...", 88),
            ("xformers", "Installing xformers (memory optimization)...", 89),
        };

        foreach (var (package, message, progress) in packages)
        {
            ReportProgress(message, progress, SetupPhase.InstallingPackages);
            _logger?.LogInformation("Installing {Package}...", package);

            var result = await RunPipCommandAsync($"install {package}", ct, timeoutMinutes: 10);

            if (result.ExitCode != 0)
            {
                _logger?.LogWarning("Failed to install {Package}: {Error}", package, result.Error);
                // Continue with other packages
            }
        }

        ReportProgress("AI packages installed", 89, SetupPhase.InstallingPackages);
    }

    #endregion

    #region Verification

    /// <summary>
    /// Verify the installation is complete and working
    /// Uses file-based verification first (fast), then optionally does process-based verification
    /// ใช้การตรวจสอบไฟล์ก่อน (เร็ว) แล้วค่อยตรวจสอบแบบรัน process (ถ้าต้องการ)
    /// </summary>
    public async Task<VerificationResult> VerifyInstallationAsync(CancellationToken ct = default, bool quickMode = true)
    {
        var result = new VerificationResult();

        // Always start with file-based verification (fast and reliable)
        var pythonDir = Path.Combine(_installDir, "python");

        result.HasPython = File.Exists(Path.Combine(pythonDir, "python.exe"));
        result.HasPyTorch = File.Exists(Path.Combine(pythonDir, "Lib", "site-packages", "torch", "__init__.py"));
        result.HasDiffusers = File.Exists(Path.Combine(pythonDir, "Lib", "site-packages", "diffusers", "__init__.py"));
        result.HasTransformers = File.Exists(Path.Combine(pythonDir, "Lib", "site-packages", "transformers", "__init__.py"));

        // Set default version strings
        if (result.HasPython) result.PythonVersion = $"Python {PYTHON_VERSION}";
        if (result.HasPyTorch) result.PyTorchVersion = "PyTorch (installed)";
        if (result.HasDiffusers) result.DiffusersVersion = "Diffusers (installed)";
        if (result.HasTransformers) result.TransformersVersion = "Transformers (installed)";

        // Check CUDA by looking for CUDA-specific DLLs
        var torchLibDir = Path.Combine(pythonDir, "Lib", "site-packages", "torch", "lib");
        if (Directory.Exists(torchLibDir))
        {
            try
            {
                var cudaDlls = Directory.GetFiles(torchLibDir, "cudart*.dll");
                result.HasCuda = cudaDlls.Length > 0;
            }
            catch
            {
                result.HasCuda = false;
            }
        }

        result.IsValid = result.HasPython && result.HasPyTorch && result.HasDiffusers && result.HasTransformers;

        // If quick mode or files not found, return early
        if (quickMode || !result.IsValid)
        {
            if (!result.HasPython) result.Errors.Add("Python not installed");
            if (!result.HasPyTorch) result.Errors.Add("PyTorch not installed");
            if (!result.HasDiffusers) result.Errors.Add("Diffusers not installed");
            if (!result.HasTransformers) result.Errors.Add("Transformers not installed");

            _logger?.LogInformation("Quick verification complete: Python={Python}, PyTorch={PyTorch}, CUDA={Cuda}, Diffusers={Diffusers}",
                result.HasPython, result.HasPyTorch, result.HasCuda, result.HasDiffusers);

            return result;
        }

        // Full verification (only if quickMode=false and all files exist)
        // This runs Python processes to get actual versions
        _logger?.LogInformation("Starting full verification with process checks...");

        try
        {
            // Check Python version (15 second timeout - just version check is fast)
            ReportProgress("Verifying Python...", 91, SetupPhase.Verifying);
            ct.ThrowIfCancellationRequested();
            try
            {
                var pythonCheck = await RunProcessWithTimeoutAsync("--version", ct, timeoutSeconds: 15);
                if (pythonCheck.ExitCode == 0)
                {
                    result.PythonVersion = pythonCheck.Output.Trim();
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger?.LogWarning("Python version check timed out, using default");
            }

            // Check PyTorch version - import can be slow on first run (45 second timeout)
            ReportProgress("Verifying PyTorch...", 94, SetupPhase.Verifying);
            ct.ThrowIfCancellationRequested();
            try
            {
                var torchCheck = await RunProcessWithTimeoutAsync("-c \"import torch; print(torch.__version__)\"", ct, timeoutSeconds: 45);
                if (torchCheck.ExitCode == 0)
                {
                    result.PyTorchVersion = torchCheck.Output.Trim();

                    // Check CUDA availability (30 second timeout)
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var cudaCheck = await RunProcessWithTimeoutAsync("-c \"import torch; print(torch.cuda.is_available())\"", ct, timeoutSeconds: 30);
                        result.HasCuda = cudaCheck.Output.Trim().ToLower() == "true";

                        if (result.HasCuda)
                        {
                            var deviceCheck = await RunProcessWithTimeoutAsync("-c \"import torch; print(torch.cuda.get_device_name(0))\"", ct, timeoutSeconds: 15);
                            result.CudaDevice = deviceCheck.Output.Trim();
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        _logger?.LogWarning("CUDA check timed out");
                    }
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger?.LogWarning("PyTorch version check timed out, using file-based result");
            }

            // Check Diffusers version (30 second timeout)
            ReportProgress("Verifying Diffusers...", 97, SetupPhase.Verifying);
            ct.ThrowIfCancellationRequested();
            try
            {
                var diffusersCheck = await RunProcessWithTimeoutAsync("-c \"import diffusers; print(diffusers.__version__)\"", ct, timeoutSeconds: 30);
                if (diffusersCheck.ExitCode == 0)
                {
                    result.DiffusersVersion = diffusersCheck.Output.Trim();
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger?.LogWarning("Diffusers version check timed out, using file-based result");
            }

            // Check Transformers version (30 second timeout)
            ReportProgress("Verifying Transformers...", 99, SetupPhase.Verifying);
            ct.ThrowIfCancellationRequested();
            try
            {
                var transformersCheck = await RunProcessWithTimeoutAsync("-c \"import transformers; print(transformers.__version__)\"", ct, timeoutSeconds: 30);
                if (transformersCheck.ExitCode == 0)
                {
                    result.TransformersVersion = transformersCheck.Output.Trim();
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger?.LogWarning("Transformers version check timed out, using file-based result");
            }
        }
        catch (OperationCanceledException)
        {
            // User cancelled - re-throw to propagate cancellation
            _logger?.LogInformation("Verification cancelled by user");
            throw;
        }

        _logger?.LogInformation("Full verification complete: Python={Python}, PyTorch={PyTorch}, CUDA={Cuda}, Diffusers={Diffusers}",
            result.HasPython, result.HasPyTorch, result.HasCuda, result.HasDiffusers);

        return result;
    }

    /// <summary>
    /// Quick file-based verification only (no Python processes)
    /// ตรวจสอบแบบเร็วโดยเช็คไฟล์เท่านั้น ไม่รัน Python process
    /// </summary>
    public VerificationResult QuickVerifyInstallation()
    {
        var result = new VerificationResult();
        var pythonDir = Path.Combine(_installDir, "python");

        result.HasPython = File.Exists(Path.Combine(pythonDir, "python.exe"));
        result.HasPyTorch = File.Exists(Path.Combine(pythonDir, "Lib", "site-packages", "torch", "__init__.py"));
        result.HasDiffusers = File.Exists(Path.Combine(pythonDir, "Lib", "site-packages", "diffusers", "__init__.py"));
        result.HasTransformers = File.Exists(Path.Combine(pythonDir, "Lib", "site-packages", "transformers", "__init__.py"));

        result.IsValid = result.HasPython && result.HasPyTorch && result.HasDiffusers && result.HasTransformers;

        if (result.HasPython) result.PythonVersion = $"Python {PYTHON_VERSION}";
        if (result.HasPyTorch) result.PyTorchVersion = "PyTorch (installed)";
        if (result.HasDiffusers) result.DiffusersVersion = "Diffusers (installed)";
        if (result.HasTransformers) result.TransformersVersion = "Transformers (installed)";

        // Check CUDA by looking for CUDA DLLs
        var torchLibDir = Path.Combine(pythonDir, "Lib", "site-packages", "torch", "lib");
        if (Directory.Exists(torchLibDir))
        {
            try
            {
                var cudaDlls = Directory.GetFiles(torchLibDir, "cudart*.dll");
                result.HasCuda = cudaDlls.Length > 0;
            }
            catch
            {
                result.HasCuda = false;
            }
        }

        if (!result.HasPython) result.Errors.Add("Python not installed");
        if (!result.HasPyTorch) result.Errors.Add("PyTorch not installed");
        if (!result.HasDiffusers) result.Errors.Add("Diffusers not installed");
        if (!result.HasTransformers) result.Errors.Add("Transformers not installed");

        return result;
    }

    /// <summary>
    /// Run Python command with proper timeout and cancellation handling
    /// </summary>
    private async Task<ProcessResult> RunProcessWithTimeoutAsync(string arguments, CancellationToken ct, int timeoutSeconds = 30)
    {
        var result = new ProcessResult();
        var pythonPath = Path.Combine(_installDir, "python", "python.exe");

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _installDir
            };

            // Set environment for embedded Python
            var pythonDir = Path.Combine(_installDir, "python");
            var scriptsDir = Path.Combine(pythonDir, "Scripts");
            var libDir = Path.Combine(pythonDir, "Lib", "site-packages");

            psi.EnvironmentVariables["PYTHONHOME"] = pythonDir;
            psi.EnvironmentVariables["PYTHONPATH"] = libDir;
            psi.EnvironmentVariables["PATH"] = $"{pythonDir};{scriptsDir};{Environment.GetEnvironmentVariable("PATH")}";

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kill the process first
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        _logger?.LogInformation("Verification process killed");
                    }
                }
                catch { }

                // Check if it was user cancellation or timeout
                if (ct.IsCancellationRequested)
                {
                    throw; // User cancelled - propagate
                }
                // Timeout - throw new exception that will be caught by the when filter
                throw new OperationCanceledException("Timeout", timeoutCts.Token);
            }

            result.ExitCode = process.ExitCode;
            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
        }
        catch (OperationCanceledException)
        {
            result.ExitCode = -1;
            result.Error = ct.IsCancellationRequested ? "User cancelled" : "Timeout";
            throw;
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.Error = ex.Message;
        }

        return result;
    }

    #endregion

    #region Process Execution

    private async Task<ProcessResult> RunPythonCommandAsync(string arguments, CancellationToken ct, int timeoutMinutes = 5)
    {
        return await RunProcessAsync(PythonPath, arguments, ct, timeoutMinutes);
    }

    private async Task<ProcessResult> RunPipCommandAsync(string arguments, CancellationToken ct, int timeoutMinutes = 10)
    {
        return await RunProcessAsync(PythonPath, $"-m pip {arguments} --no-warn-script-location", ct, timeoutMinutes);
    }

    private async Task<ProcessResult> RunPipCommandWithProgressAsync(string arguments, CancellationToken ct,
        int timeoutMinutes = 10, Action<string, bool>? progressCallback = null)
    {
        return await RunProcessWithProgressAsync(PythonPath, $"-m pip {arguments} --no-warn-script-location --progress-bar on",
            ct, timeoutMinutes, progressCallback);
    }

    private async Task<ProcessResult> RunProcessWithProgressAsync(string fileName, string arguments, CancellationToken ct,
        int timeoutMinutes = 5, Action<string, bool>? progressCallback = null)
    {
        var result = new ProcessResult();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _installDir
            };

            // Set environment for embedded Python
            var pythonDir = Path.Combine(_installDir, "python");
            var scriptsDir = Path.Combine(pythonDir, "Scripts");
            var libDir = Path.Combine(pythonDir, "Lib", "site-packages");

            psi.EnvironmentVariables["PYTHONHOME"] = pythonDir;
            psi.EnvironmentVariables["PYTHONPATH"] = libDir;
            psi.EnvironmentVariables["PATH"] = $"{pythonDir};{scriptsDir};{Environment.GetEnvironmentVariable("PATH")}";

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();
            var lastProgressUpdate = DateTime.Now;

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger?.LogDebug("[pip] {Output}", e.Data);

                    // Check if this is download progress
                    var isDownloading = e.Data.Contains("Downloading") ||
                                        e.Data.Contains("━") ||
                                        e.Data.Contains("%");

                    // Throttle progress updates to avoid overwhelming UI
                    if (progressCallback != null && (DateTime.Now - lastProgressUpdate).TotalMilliseconds > 500)
                    {
                        progressCallback(e.Data, isDownloading);
                        lastProgressUpdate = DateTime.Now;
                    }
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);

                    // pip sends download progress to stderr too
                    var isDownloading = e.Data.Contains("Downloading") ||
                                        e.Data.Contains("━") ||
                                        e.Data.Contains("%");

                    if (progressCallback != null && (DateTime.Now - lastProgressUpdate).TotalMilliseconds > 500)
                    {
                        progressCallback(e.Data, isDownloading);
                        lastProgressUpdate = DateTime.Now;
                    }

                    if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning("[pip Error] {Output}", e.Data);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to exit with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

            // Periodically report "still working" if no progress
            _ = Task.Run(async () =>
            {
                var elapsed = 0;
                try
                {
                    while (!process.HasExited && !timeoutCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(30000, timeoutCts.Token); // Every 30 seconds
                        elapsed += 30;
                        if (!process.HasExited)
                        {
                            ReportProgress($"กำลังติดตั้ง... ({elapsed / 60} นาที)", 52 + Math.Min(elapsed / 60, 10), SetupPhase.InstallingPyTorch);
                        }
                    }
                }
                catch { }
            }, timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancelled
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        _logger?.LogInformation("pip process killed due to cancellation");
                    }
                }
                catch { }
                throw;
            }

            result.ExitCode = process.ExitCode;
            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
        }
        catch (OperationCanceledException)
        {
            result.ExitCode = -1;
            result.Error = "Operation cancelled or timeout";
            throw;
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, CancellationToken ct, int timeoutMinutes = 5)
    {
        var result = new ProcessResult();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _installDir
            };

            // Set environment for embedded Python
            var pythonDir = Path.Combine(_installDir, "python");
            var scriptsDir = Path.Combine(pythonDir, "Scripts");
            var libDir = Path.Combine(pythonDir, "Lib", "site-packages");

            psi.EnvironmentVariables["PYTHONHOME"] = pythonDir;
            psi.EnvironmentVariables["PYTHONPATH"] = libDir;
            psi.EnvironmentVariables["PATH"] = $"{pythonDir};{scriptsDir};{Environment.GetEnvironmentVariable("PATH")}";

            using var process = new Process { StartInfo = psi };
            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger?.LogDebug("[Python] {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    // Some pip output goes to stderr even for non-errors
                    if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        e.Data.Contains("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning("[Python Error] {Output}", e.Data);
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for process to exit with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Kill the process if cancelled
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        _logger?.LogInformation("Process killed due to cancellation");
                    }
                }
                catch { }
                throw;
            }

            result.ExitCode = process.ExitCode;
            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
        }
        catch (OperationCanceledException)
        {
            result.ExitCode = -1;
            result.Error = "Operation cancelled";
            throw;
        }
        catch (Exception ex)
        {
            result.ExitCode = -1;
            result.Error = ex.Message;
        }

        return result;
    }

    #endregion

    #region Helpers

    private void ReportProgress(string message, int percentage, SetupPhase phase)
    {
        ProgressChanged?.Invoke(this, new SetupProgressEventArgs
        {
            Message = message,
            Percentage = percentage,
            Phase = phase
        });
    }

    /// <summary>
    /// Get the pip install command for manual installation
    /// </summary>
    public string GetManualInstallCommand(GpuInfo gpuInfo)
    {
        var commands = new List<string>();

        if (gpuInfo.Vendor == GpuVendor.Nvidia && gpuInfo.IsAvailable)
        {
            var cudaVersion = gpuInfo.ComputeCapability >= 8.0 ? "cu121" : "cu118";
            commands.Add($"pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/{cudaVersion}");
        }
        else if (gpuInfo.Vendor == GpuVendor.Amd)
        {
            commands.Add("pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/rocm5.6");
        }
        else
        {
            commands.Add("pip install torch torchvision torchaudio");
        }

        commands.Add("pip install diffusers transformers accelerate safetensors Pillow scipy flask");

        return string.Join("\n", commands);
    }

    /// <summary>
    /// Clean up all installed files - removes Python and all packages
    /// ลบไฟล์ที่ติดตั้งทั้งหมดเพื่อคืนพื้นที่
    /// </summary>
    public async Task<CleanupResult> CleanupInstallationAsync(IProgress<string>? progress = null)
    {
        var result = new CleanupResult();
        var pythonDir = Path.Combine(_installDir, "python");

        try
        {
            _logger?.LogInformation("Starting cleanup of installation directory: {Dir}", _installDir);
            progress?.Report("กำลังเตรียมลบไฟล์...");

            // Calculate size before cleanup
            if (Directory.Exists(pythonDir))
            {
                result.SizeFreedBytes = await Task.Run(() => GetDirectorySize(pythonDir));
            }

            // Kill any running Python processes from our installation
            progress?.Report("กำลังหยุด Python processes...");
            await KillPythonProcessesAsync();

            // Delete Python directory
            if (Directory.Exists(pythonDir))
            {
                progress?.Report("กำลังลบ Python และ packages...");
                await Task.Run(() =>
                {
                    try
                    {
                        Directory.Delete(pythonDir, recursive: true);
                        result.PythonRemoved = true;
                        _logger?.LogInformation("Python directory deleted successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to delete Python directory, trying force delete");
                        // Try force delete with retry
                        ForceDeleteDirectory(pythonDir);
                        result.PythonRemoved = true;
                    }
                });
            }

            // Delete any temp files
            progress?.Report("กำลังลบไฟล์ชั่วคราว...");
            var tempFiles = new[] { "python-embed.zip", "get-pip.py" };
            foreach (var tempFile in tempFiles)
            {
                var tempPath = Path.Combine(_installDir, tempFile);
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch
                    {
                        // Ignore temp file deletion errors
                    }
                }
            }

            // Delete pip cache
            var pipCacheDir = Path.Combine(_installDir, "pip-cache");
            if (Directory.Exists(pipCacheDir))
            {
                progress?.Report("กำลังลบ pip cache...");
                try
                {
                    Directory.Delete(pipCacheDir, recursive: true);
                    result.CacheCleared = true;
                }
                catch
                {
                    // Ignore cache deletion errors
                }
            }

            result.Success = true;
            result.Message = $"ลบไฟล์เสร็จสิ้น คืนพื้นที่ {FormatSize(result.SizeFreedBytes)}";
            progress?.Report(result.Message);
            _logger?.LogInformation("Cleanup completed. Freed {Size} bytes", result.SizeFreedBytes);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"เกิดข้อผิดพลาด: {ex.Message}";
            result.Error = ex;
            _logger?.LogError(ex, "Cleanup failed");
        }

        return result;
    }

    /// <summary>
    /// Kill any Python processes running from our installation directory
    /// </summary>
    private async Task KillPythonProcessesAsync()
    {
        try
        {
            var pythonExe = PythonPath;
            var processes = Process.GetProcessesByName("python")
                .Concat(Process.GetProcessesByName("pythonw"));

            foreach (var process in processes)
            {
                try
                {
                    // Check if it's our Python installation
                    var processPath = process.MainModule?.FileName;
                    if (processPath != null && processPath.StartsWith(_installDir, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogInformation("Killing Python process: {Pid}", process.Id);
                        process.Kill(entireProcessTree: true);
                        await Task.Delay(100); // Give it time to terminate
                    }
                }
                catch
                {
                    // Ignore process access errors
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error killing Python processes");
        }
    }

    /// <summary>
    /// Force delete a directory with retry logic
    /// </summary>
    private void ForceDeleteDirectory(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                // Reset file attributes
                foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore
                    }
                }

                Directory.Delete(path, recursive: true);
                return;
            }
            catch
            {
                Thread.Sleep(500 * (attempt + 1)); // Wait longer between retries
            }
        }

        // Final attempt - delete file by file
        try
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
            foreach (var dir in Directory.GetDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                try { Directory.Delete(dir); } catch { }
            }
            try { Directory.Delete(path); } catch { }
        }
        catch
        {
            // Give up
        }
    }

    /// <summary>
    /// Get total size of a directory
    /// </summary>
    private long GetDirectorySize(string path)
    {
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try { return new FileInfo(f).Length; }
                    catch { return 0; }
                });
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Format file size for display
    /// </summary>
    private static string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:F1} {sizes[order]}";
    }

    #endregion
}

/// <summary>
/// Result of cleanup operation
/// </summary>
public class CleanupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Exception? Error { get; set; }
    public bool PythonRemoved { get; set; }
    public bool CacheCleared { get; set; }
    public long SizeFreedBytes { get; set; }
}

#region Models

public enum SetupPhase
{
    Starting,
    DetectingGpu,
    InstallingPython,
    InstallingPip,
    InstallingPyTorch,
    InstallingPackages,
    Verifying,
    Complete,
    Error
}

public class SetupProgressEventArgs : EventArgs
{
    public string Message { get; set; } = "";
    public int Percentage { get; set; }
    public SetupPhase Phase { get; set; }
}

public class AutoSetupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public Exception? Error { get; set; }
    public GpuInfo? GpuInfo { get; set; }
    public bool PythonInstalled { get; set; }
    public bool PipInstalled { get; set; }
    public bool PyTorchInstalled { get; set; }
    public bool PackagesInstalled { get; set; }
    public VerificationResult? VerificationResult { get; set; }
}

public class VerificationResult
{
    public bool IsValid { get; set; }
    public bool HasPython { get; set; }
    public bool HasPyTorch { get; set; }
    public bool HasCuda { get; set; }
    public bool HasDiffusers { get; set; }
    public bool HasTransformers { get; set; }
    public string? PythonVersion { get; set; }
    public string? PyTorchVersion { get; set; }
    public string? CudaDevice { get; set; }
    public string? DiffusersVersion { get; set; }
    public string? TransformersVersion { get; set; }
    public List<string> Errors { get; set; } = new();

    public string GetSummary()
    {
        var lines = new List<string>();
        lines.Add(HasPython ? $"✓ Python: {PythonVersion}" : "✗ Python: Not installed");
        lines.Add(HasPyTorch ? $"✓ PyTorch: {PyTorchVersion}" : "✗ PyTorch: Not installed");
        lines.Add(HasCuda ? $"✓ CUDA: {CudaDevice}" : "○ CUDA: Not available (CPU mode)");
        lines.Add(HasDiffusers ? $"✓ Diffusers: {DiffusersVersion}" : "✗ Diffusers: Not installed");
        lines.Add(HasTransformers ? $"✓ Transformers: {TransformersVersion}" : "✗ Transformers: Not installed");
        return string.Join("\n", lines);
    }
}

public class ProcessResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
}

#endregion
