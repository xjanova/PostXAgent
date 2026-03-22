using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Production-quality Generation Engine using HuggingFace Diffusers directly (replaces ComfyUI)
/// This service manages a Python process running diffusers for image/video generation
/// with comprehensive GPU detection, VRAM monitoring, and model compatibility checks
/// </summary>
/// <summary>
/// GPU resource configuration for preventing system crashes from VRAM exhaustion
/// </summary>
public class GpuResourceConfig
{
    /// <summary>Max VRAM usage percent (30-95%, default 85%)</summary>
    public double MaxVramUsagePercent { get; set; } = 85.0;

    /// <summary>VRAM reserved for OS/display in GB (default 1.5)</summary>
    public double ReservedVramGb { get; set; } = 1.5;

    /// <summary>Auto-recover from OOM errors</summary>
    public bool OomAutoRecovery { get; set; } = true;

    /// <summary>Auto-reduce batch size when VRAM limit reached</summary>
    public bool AutoReduceBatchSize { get; set; } = true;
}

public class DiffusersGenerationEngine : IDisposable, IAsyncDisposable
{
    private readonly ILogger<DiffusersGenerationEngine>? _logger;
    private readonly HuggingFaceModelService _modelService;
    private readonly LocalGpuService _gpuService;
    private readonly AutoSetupService _autoSetup;
    private readonly string _scriptsDir;
    private Process? _serverProcess;
    private HttpClient? _httpClient;
    private bool _isRunning;
    private bool _disposed;
    private GpuInfo? _cachedGpuInfo;
    private PythonEnvironmentInfo? _cachedPythonEnv;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public const int DEFAULT_PORT = 5050;

    /// <summary>
    /// GPU resource configuration
    /// </summary>
    public GpuResourceConfig GpuConfig { get; set; } = new();

    // VRAM requirements for different model types (in GB)
    public static readonly IReadOnlyDictionary<string, double> ModelVramRequirements = new Dictionary<string, double>
    {
        { "SD1.5", 4.0 },
        { "SD2.1", 5.0 },
        { "SDXL", 8.0 },
        { "SDXL-Turbo", 6.0 },
        { "Flux-Schnell", 12.0 },
        { "Flux-Dev", 16.0 },
        { "SVD", 10.0 },
        { "SVD-XT", 12.0 },
    };

    /// <summary>
    /// Event raised when engine status changes
    /// </summary>
    public event EventHandler<EngineStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Event raised when generation progress updates
    /// </summary>
    public event EventHandler<DiffusersProgressEventArgs>? ProgressChanged;

    /// <summary>
    /// Event raised when model loading progress updates
    /// </summary>
    public event EventHandler<ModelLoadProgressEventArgs>? ModelLoadProgressChanged;

    /// <summary>
    /// Event raised when GPU status changes
    /// </summary>
    public event EventHandler<LocalGpuStatusEventArgs>? GpuStatusChanged;

    /// <summary>
    /// Event raised when auto-setup progress changes
    /// </summary>
    public event EventHandler<SetupProgressEventArgs>? SetupProgressChanged;

    /// <summary>
    /// Event raised when engine outputs log message (stdout/stderr)
    /// </summary>
    public event EventHandler<EngineLogEventArgs>? LogOutput;

    /// <summary>
    /// Event raised when startup validation completes with warnings
    /// </summary>
    public event EventHandler<StartupValidationEventArgs>? StartupValidationCompleted;

    /// <summary>
    /// Gets whether the engine is running
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets the current loaded model
    /// </summary>
    public string? CurrentModel { get; private set; }

    /// <summary>
    /// Gets engine port
    /// </summary>
    public int Port { get; private set; } = DEFAULT_PORT;

    /// <summary>
    /// Gets the cached GPU info
    /// </summary>
    public GpuInfo? GpuInfo => _cachedGpuInfo;

    /// <summary>
    /// Gets the cached Python environment info
    /// </summary>
    public PythonEnvironmentInfo? PythonEnvironment => _cachedPythonEnv;

    /// <summary>
    /// Gets the server process ID (if running)
    /// </summary>
    public int? ServerProcessId => _serverProcess?.HasExited == false ? _serverProcess.Id : null;

    /// <summary>
    /// Gets or sets the HuggingFace API token for accessing gated models
    /// </summary>
    public string? HuggingFaceToken { get; set; }

    public DiffusersGenerationEngine(
        HuggingFaceModelService modelService,
        LocalGpuService? gpuService = null,
        AutoSetupService? autoSetup = null,
        ILogger<DiffusersGenerationEngine>? logger = null)
    {
        _modelService = modelService;
        _gpuService = gpuService ?? new LocalGpuService();
        _autoSetup = autoSetup ?? new AutoSetupService(_gpuService);
        _logger = logger;

        // Subscribe to auto-setup progress events
        _autoSetup.ProgressChanged += (s, e) => SetupProgressChanged?.Invoke(this, e);

        // Setup scripts directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _scriptsDir = Path.Combine(appData, "PostXAgent", "diffusers_engine");
        Directory.CreateDirectory(_scriptsDir);
    }

    /// <summary>
    /// Gets whether the auto-setup has been completed
    /// </summary>
    public bool IsSetupComplete => _autoSetup.IsPythonInstalled;

    /// <summary>
    /// Gets the AutoSetupService instance for manual control
    /// </summary>
    public AutoSetupService AutoSetup => _autoSetup;

    #region Engine Lifecycle

    /// <summary>
    /// Perform pre-flight checks before starting the engine
    /// If autoInstall is true, automatically installs missing dependencies
    /// </summary>
    public async Task<PreflightCheckResult> PerformPreflightChecksAsync(bool autoInstall = true, CancellationToken ct = default)
    {
        var result = new PreflightCheckResult();

        try
        {
            // Check GPU
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Detecting GPU..."));
            _cachedGpuInfo = await _gpuService.DetectGpuAsync(forceRefresh: true, ct: ct);
            result.GpuInfo = _cachedGpuInfo;
            result.HasGpu = _cachedGpuInfo.IsAvailable;

            GpuStatusChanged?.Invoke(this, new LocalGpuStatusEventArgs(_cachedGpuInfo));

            // Check if we have embedded Python from auto-setup
            if (_autoSetup.IsPythonInstalled)
            {
                // Verify embedded Python installation
                StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Verifying embedded Python..."));
                var verification = await _autoSetup.VerifyInstallationAsync(ct);

                if (verification.IsValid)
                {
                    result.HasPython = true;
                    result.PythonReady = true;
                    _cachedPythonEnv = new PythonEnvironmentInfo
                    {
                        IsAvailable = true,
                        IsReady = true,
                        PythonPath = _autoSetup.PythonPath,
                        PythonVersion = verification.PythonVersion,
                        HasCudaSupport = verification.HasCuda,
                        InstalledPackages = new List<string> { "PyTorch", "Diffusers", "Transformers", "Accelerate" }
                    };
                    result.PythonEnv = _cachedPythonEnv;
                    result.IsReady = true;

                    _logger?.LogInformation("Embedded Python ready: {Version}, CUDA: {Cuda}",
                        verification.PythonVersion, verification.HasCuda);
                    return result;
                }
            }

            // Check system Python environment
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Checking Python environment..."));
            _cachedPythonEnv = await _gpuService.CheckPythonEnvironmentAsync(ct);
            result.PythonEnv = _cachedPythonEnv;
            result.HasPython = _cachedPythonEnv.IsAvailable;
            result.PythonReady = _cachedPythonEnv.IsReady;

            // Auto-install if enabled and not ready
            if (autoInstall && (!result.HasPython || !result.PythonReady))
            {
                _logger?.LogInformation("Python not ready, starting automatic installation...");
                StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Installing Python and dependencies automatically..."));

                var setupResult = await _autoSetup.PerformFullSetupAsync(ct);

                if (setupResult.Success && setupResult.VerificationResult?.IsValid == true)
                {
                    result.HasPython = true;
                    result.PythonReady = true;
                    result.AutoInstalled = true;
                    _cachedPythonEnv = new PythonEnvironmentInfo
                    {
                        IsAvailable = true,
                        IsReady = true,
                        PythonPath = _autoSetup.PythonPath,
                        PythonVersion = setupResult.VerificationResult.PythonVersion,
                        HasCudaSupport = setupResult.VerificationResult.HasCuda,
                        InstalledPackages = new List<string> { "PyTorch", "Diffusers", "Transformers", "Accelerate" }
                    };
                    result.PythonEnv = _cachedPythonEnv;

                    _logger?.LogInformation("Automatic installation completed successfully");
                }
                else
                {
                    result.Errors.Add($"Automatic installation failed: {setupResult.Message}");
                    if (setupResult.VerificationResult?.Errors.Count > 0)
                    {
                        result.Errors.AddRange(setupResult.VerificationResult.Errors);
                    }
                }
            }

            // Set overall readiness
            result.IsReady = result.HasPython && result.PythonReady;

            // Generate recommendations
            if (!result.HasGpu)
            {
                result.Warnings.Add("No GPU detected. Generation will use CPU (very slow).");
            }
            else if (_cachedGpuInfo.TotalVramGb < 4)
            {
                result.Warnings.Add($"Low VRAM ({_cachedGpuInfo.TotalVramGb:F1}GB). Only small models supported.");
            }

            if (!result.IsReady && !autoInstall)
            {
                if (!result.HasPython)
                {
                    result.Errors.Add("Python 3.10+ not found. Please install Python.");
                }
                else if (!result.PythonReady)
                {
                    var installCmd = _gpuService.GetInstallCommand(_cachedPythonEnv, _cachedGpuInfo);
                    result.Errors.Add($"Missing packages: {string.Join(", ", _cachedPythonEnv.MissingPackages)}");
                    result.InstallCommand = installCmd;
                }
            }

            if (_cachedPythonEnv != null && !_cachedPythonEnv.HasCudaSupport && result.HasGpu && _cachedGpuInfo.Vendor == GpuVendor.Nvidia)
            {
                result.Warnings.Add("PyTorch doesn't have CUDA support. GPU acceleration unavailable.");
            }

            _logger?.LogInformation("Preflight check complete. Ready: {Ready}, GPU: {Gpu}, Python: {Python}, AutoInstalled: {AutoInstalled}",
                result.IsReady, result.HasGpu, result.PythonReady, result.AutoInstalled);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Preflight check failed");
            result.Errors.Add($"Preflight check failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Start the diffusers generation engine with pre-flight checks
    /// </summary>
    public async Task<EngineStartResult> StartAsync(int port = DEFAULT_PORT, bool skipPreflightChecks = false, CancellationToken ct = default)
    {
        if (_isRunning)
        {
            _logger?.LogWarning("Engine already running");
            return new EngineStartResult { Success = true, Message = "Engine already running" };
        }

        await _operationLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_isRunning)
            {
                return new EngineStartResult { Success = true, Message = "Engine already running" };
            }

            Port = port;

            // Perform pre-flight checks
            if (!skipPreflightChecks)
            {
                var preflightResult = await PerformPreflightChecksAsync(autoInstall: true, ct: ct);
                if (!preflightResult.IsReady)
                {
                    StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Error, "Pre-flight checks failed"));
                    return new EngineStartResult
                    {
                        Success = false,
                        Message = "Pre-flight checks failed",
                        PreflightResult = preflightResult
                    };
                }
            }

            // Ensure Python script exists
            await EnsureScriptExistsAsync();

            // Kill any existing process using the port
            await KillProcessOnPortAsync(port);

            // Start Python process
            var scriptPath = Path.Combine(_scriptsDir, "generation_server.py");

            // Determine Python path - prefer embedded Python from auto-setup
            string pythonPath;
            var useEmbeddedPython = _autoSetup.IsPythonInstalled;

            if (useEmbeddedPython)
            {
                pythonPath = _autoSetup.PythonPath;
                _logger?.LogInformation("Using embedded Python: {Path}", pythonPath);
            }
            else
            {
                pythonPath = _cachedPythonEnv?.PythonPath ?? "python";
                _logger?.LogInformation("Using system Python: {Path}", pythonPath);
            }

            // Build command line arguments
            var args = $"\"{scriptPath}\" --port {port} --models-dir \"{_modelService.ModelsDirectory}\"";

            // Add GPU resource limits (use InvariantCulture for decimal point, not comma)
            args += $" --max-vram-percent {GpuConfig.MaxVramUsagePercent.ToString("F1", CultureInfo.InvariantCulture)}";
            args += $" --reserved-vram {GpuConfig.ReservedVramGb.ToString("F1", CultureInfo.InvariantCulture)}";
            if (!GpuConfig.OomAutoRecovery)
                args += " --no-oom-recovery";
            _logger?.LogInformation("GPU limits: max VRAM {Percent}%, reserved {Reserved} GB",
                GpuConfig.MaxVramUsagePercent, GpuConfig.ReservedVramGb);

            // Add HuggingFace token if configured
            if (!string.IsNullOrEmpty(HuggingFaceToken))
            {
                args += $" --hf-token \"{HuggingFaceToken}\"";
                _logger?.LogInformation("HuggingFace token configured for server");
            }

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _scriptsDir
            };

            // Set environment for embedded Python
            if (useEmbeddedPython)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var pythonDir = Path.Combine(appData, "PostXAgent", "python");
                var scriptsDir = Path.Combine(pythonDir, "Scripts");
                var libDir = Path.Combine(pythonDir, "Lib", "site-packages");

                psi.EnvironmentVariables["PYTHONHOME"] = pythonDir;
                psi.EnvironmentVariables["PYTHONPATH"] = libDir;
                psi.EnvironmentVariables["PATH"] = $"{pythonDir};{scriptsDir};{Environment.GetEnvironmentVariable("PATH")}";
            }

            // Set environment variables for CUDA
            if (_cachedGpuInfo?.Vendor == GpuVendor.Nvidia)
            {
                psi.EnvironmentVariables["CUDA_VISIBLE_DEVICES"] = "0";
            }
            else if (_cachedGpuInfo?.Vendor == GpuVendor.Amd)
            {
                psi.EnvironmentVariables["HSA_OVERRIDE_GFX_VERSION"] = "10.3.0"; // For ROCm compatibility
            }

            // Memory optimization settings
            psi.EnvironmentVariables["PYTORCH_CUDA_ALLOC_CONF"] = "max_split_size_mb:512";

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Starting Python server..."));

            _serverProcess = new Process { StartInfo = psi };
            _serverProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger?.LogDebug("[Engine] {Output}", e.Data);
                    LogOutput?.Invoke(this, new EngineLogEventArgs(e.Data, isError: false));
                    ParseEngineOutput(e.Data);
                }
            };
            _serverProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger?.LogWarning("[Engine Error] {Output}", e.Data);
                    LogOutput?.Invoke(this, new EngineLogEventArgs(e.Data, isError: true));
                }
            };

            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            // Wait for server to be ready
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var ready = await WaitForServerReadyAsync(ct);

            if (ready)
            {
                _isRunning = true;
                StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Engine started"));
                _logger?.LogInformation("Diffusers engine started on port {Port}", port);

                return new EngineStartResult
                {
                    Success = true,
                    Message = $"Engine started on port {port}",
                    GpuInfo = _cachedGpuInfo
                };
            }

            // Failed to start
            await StopAsync();
            return new EngineStartResult
            {
                Success = false,
                Message = "Server failed to start within timeout"
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start diffusers engine");
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Error, ex.Message));
            return new EngineStartResult
            {
                Success = false,
                Message = ex.Message
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void ParseEngineOutput(string output)
    {
        // Parse progress updates from Python server
        if (output.Contains("Step") && output.Contains("/"))
        {
            // Example: "Step 15/30"
            var match = System.Text.RegularExpressions.Regex.Match(output, @"Step (\d+)/(\d+)");
            if (match.Success)
            {
                var step = int.Parse(match.Groups[1].Value);
                var total = int.Parse(match.Groups[2].Value);
                ProgressChanged?.Invoke(this, new DiffusersProgressEventArgs { Step = step, TotalSteps = total });
            }
        }
    }

    /// <summary>
    /// Stop the engine
    /// </summary>
    public async Task StopAsync()
    {
        if (_serverProcess != null && !_serverProcess.HasExited)
        {
            try
            {
                // Try graceful shutdown first
                if (_httpClient != null)
                {
                    await _httpClient.PostAsync($"http://localhost:{Port}/shutdown", null);
                    await Task.Delay(1000);
                }

                if (!_serverProcess.HasExited)
                {
                    _serverProcess.Kill(true);
                }
            }
            catch
            {
                // Force kill
                try { _serverProcess.Kill(true); } catch { }
            }

            _serverProcess.Dispose();
            _serverProcess = null;
        }

        _httpClient?.Dispose();
        _httpClient = null;
        _isRunning = false;
        CurrentModel = null;

        StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Stopped, "Engine stopped"));
        _logger?.LogInformation("Diffusers engine stopped");
    }

    /// <summary>
    /// Kill any process using the specified port
    /// </summary>
    private async Task KillProcessOnPortAsync(int port)
    {
        try
        {
            await Task.Run(() =>
            {
                // Use netstat to find PID using the port
                var psi = new ProcessStartInfo
                {
                    FileName = "netstat",
                    Arguments = "-ano",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                // Parse output to find PIDs listening on our port
                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var pidsToKill = new HashSet<int>();

                foreach (var line in lines)
                {
                    if (line.Contains($":{port}") && (line.Contains("LISTENING") || line.Contains("ESTABLISHED")))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 5 && int.TryParse(parts[^1], out var pid) && pid > 0)
                        {
                            // Don't kill the current process
                            if (pid != Environment.ProcessId)
                            {
                                pidsToKill.Add(pid);
                            }
                        }
                    }
                }

                foreach (var pid in pidsToKill)
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        // Only kill Python processes
                        if (proc.ProcessName.Contains("python", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger?.LogInformation("Killing existing Python process on port {Port}: PID {Pid}", port, pid);
                            proc.Kill(true);
                            proc.WaitForExit(3000);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to kill process {Pid}", pid);
                    }
                }

                // Wait a moment for port to be released
                if (pidsToKill.Count > 0)
                {
                    Thread.Sleep(1000);
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check/kill processes on port {Port}", port);
        }
    }

    private async Task<bool> WaitForServerReadyAsync(CancellationToken ct)
    {
        var maxAttempts = 60; // 60 seconds (models take time to initialize)
        _logger?.LogInformation("Waiting for server to be ready (max {Seconds}s)...", maxAttempts);
        StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Waiting for server..."));

        for (int i = 0; i < maxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/health", ct);
                if (response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("Server ready after {Seconds}s", i + 1);
                    StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, $"Server ready ({i + 1}s)"));
                    return true;
                }
            }
            catch
            {
                // Server not ready yet
            }

            // Log progress every 5 seconds
            if ((i + 1) % 5 == 0)
            {
                _logger?.LogInformation("Still waiting for server... ({Elapsed}/{Max}s)", i + 1, maxAttempts);
                StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, $"Starting server... ({i + 1}/{maxAttempts}s)"));
            }

            await Task.Delay(1000, ct);
        }

        _logger?.LogWarning("Server failed to start within {Seconds}s timeout", maxAttempts);
        StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Error, $"Timeout after {maxAttempts}s"));
        return false;
    }

    #endregion

    #region Model Management

    /// <summary>
    /// Get estimated VRAM requirement for a model
    /// </summary>
    public double EstimateVramRequirement(string modelId)
    {
        var modelIdLower = modelId.ToLowerInvariant();

        // Check known model types
        if (modelIdLower.Contains("flux-dev") || modelIdLower.Contains("flux.1-dev"))
            return ModelVramRequirements["Flux-Dev"];
        if (modelIdLower.Contains("flux") || modelIdLower.Contains("schnell"))
            return ModelVramRequirements["Flux-Schnell"];
        if (modelIdLower.Contains("sdxl-turbo"))
            return ModelVramRequirements["SDXL-Turbo"];
        if (modelIdLower.Contains("sdxl") || modelIdLower.Contains("xl"))
            return ModelVramRequirements["SDXL"];
        if (modelIdLower.Contains("svd-xt"))
            return ModelVramRequirements["SVD-XT"];
        if (modelIdLower.Contains("svd") || modelIdLower.Contains("stable-video"))
            return ModelVramRequirements["SVD"];
        if (modelIdLower.Contains("sd-2") || modelIdLower.Contains("sd2"))
            return ModelVramRequirements["SD2.1"];

        // Default to SD1.5 requirements
        return ModelVramRequirements["SD1.5"];
    }

    /// <summary>
    /// Check if a model can be loaded with current VRAM
    /// </summary>
    public async Task<ModelLoadCheckResult> CheckModelLoadableAsync(string modelId, CancellationToken ct = default)
    {
        var result = new ModelLoadCheckResult { ModelId = modelId };

        try
        {
            var requiredVram = EstimateVramRequirement(modelId);
            result.RequiredVramGb = requiredVram;

            var (canLoad, reason) = await _gpuService.CanLoadModelAsync(requiredVram, ct);
            result.CanLoad = canLoad;
            result.Message = reason;

            // Get current VRAM usage
            var vram = await _gpuService.GetVramUsageAsync(ct);
            result.CurrentFreeVramGb = vram.FreeMb / 1024.0;
            result.VramUsagePercent = vram.UsagePercent;

            // Provide recommendations
            if (!canLoad && CurrentModel != null)
            {
                result.Recommendations.Add($"Unload current model '{CurrentModel}' to free VRAM");
            }

            if (requiredVram > (_cachedGpuInfo?.TotalVramGb ?? 0))
            {
                result.Recommendations.Add("Enable model offloading for low-VRAM mode");
                result.SuggestOffloading = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Message = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Load a model into VRAM with pre-checks
    /// </summary>
    public async Task<ModelLoadResult> LoadModelAsync(string modelId, ModelType type, bool forceLoad = false, CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            _logger?.LogWarning("Engine not running");
            return new ModelLoadResult { Success = false, Error = "Engine not running" };
        }

        await _operationLock.WaitAsync(ct);
        try
        {
            // Check VRAM availability before loading
            if (!forceLoad)
            {
                var checkResult = await CheckModelLoadableAsync(modelId, ct);
                if (!checkResult.CanLoad)
                {
                    _logger?.LogWarning("Cannot load model {ModelId}: {Reason}", modelId, checkResult.Message);

                    return new ModelLoadResult
                    {
                        Success = false,
                        Error = checkResult.Message,
                        VramCheck = checkResult
                    };
                }
            }

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Loading, $"Loading {modelId}..."));

            // Fire progress: Starting
            ModelLoadProgressChanged?.Invoke(this, new ModelLoadProgressEventArgs
            {
                ModelId = modelId,
                Stage = "Starting",
                Progress = 10,
                Message = "Preparing to load model..."
            });

            var request = new LoadModelRequest
            {
                ModelId = modelId,
                ModelType = type.ToString()
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Fire progress: Sending request
            ModelLoadProgressChanged?.Invoke(this, new ModelLoadProgressEventArgs
            {
                ModelId = modelId,
                Stage = "Loading",
                Progress = 30,
                Message = "Loading model into VRAM..."
            });

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/load-model", content, ct);

            // Fire progress: Processing response
            ModelLoadProgressChanged?.Invoke(this, new ModelLoadProgressEventArgs
            {
                ModelId = modelId,
                Stage = "Processing",
                Progress = 70,
                Message = "Processing..."
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                throw new Exception($"Server returned {response.StatusCode}: {errorContent}");
            }

            var serverResult = await response.Content.ReadFromJsonAsync<LoadModelServerResult>(ct);

            if (serverResult?.Success != true)
            {
                return new ModelLoadResult
                {
                    Success = false,
                    Error = serverResult?.Error ?? "Unknown error"
                };
            }

            CurrentModel = modelId;
            _modelService.MarkModelLoaded(modelId, new ModelInfo { Id = modelId, Type = type });

            // Update VRAM status after loading
            var vramUsage = await _gpuService.GetVramUsageAsync(ct);

            // Fire progress: Complete
            ModelLoadProgressChanged?.Invoke(this, new ModelLoadProgressEventArgs
            {
                ModelId = modelId,
                Stage = "Complete",
                Progress = 100,
                Message = "Model loaded successfully!"
            });

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, $"Loaded: {modelId}"));
            _logger?.LogInformation("Model loaded: {ModelId}, VRAM used: {VramMb}MB", modelId, vramUsage.UsedMb);

            return new ModelLoadResult
            {
                Success = true,
                ModelId = modelId,
                VramUsedMb = vramUsage.UsedMb
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load model: {ModelId}", modelId);
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Error, ex.Message));

            return new ModelLoadResult
            {
                Success = false,
                Error = ex.Message
            };
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// Unload current model
    /// </summary>
    public async Task UnloadModelAsync(CancellationToken ct = default)
    {
        if (!_isRunning || CurrentModel == null)
            return;

        try
        {
            await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/unload-model", null, ct);
            _modelService.MarkModelUnloaded(CurrentModel);
            CurrentModel = null;

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Model unloaded"));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to unload model");
        }
    }

    #endregion

    #region Image Generation

    /// <summary>
    /// Generate an image
    /// </summary>
    public async Task<DiffusersResult> GenerateImageAsync(
        DiffusersImageRequest request,
        CancellationToken ct = default)
    {
        if (!EnsureRunning(out var client, out var err))
        {
            return new DiffusersResult { Success = false, Error = err };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, "Generating image..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"http://localhost:{Port}/generate/image", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DiffusersResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Generation complete"));

            return result ?? new DiffusersResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new DiffusersResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Image generation failed");
            return new DiffusersResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Generate an image from another image (img2img)
    /// </summary>
    public async Task<DiffusersResult> GenerateImg2ImgAsync(
        DiffusersImg2ImgRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new DiffusersResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, "Generating img2img..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/img2img", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DiffusersResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Img2img generation complete"));

            return result ?? new DiffusersResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new DiffusersResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Img2img generation failed");
            return new DiffusersResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Generate a video
    /// </summary>
    public async Task<DiffusersResult> GenerateVideoAsync(
        DiffusersVideoRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new DiffusersResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, "Generating video..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/video", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<DiffusersResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Video generation complete"));

            return result ?? new DiffusersResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new DiffusersResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Video generation failed");
            return new DiffusersResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region LoRA Management

    /// <summary>
    /// Load a LoRA adapter
    /// </summary>
    public async Task<LoraLoadResult> LoadLoraAsync(string loraPath, double weight = 1.0, string? adapterName = null, CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new LoraLoadResult { Success = false, Error = "Engine not running" };
        }

        try
        {
            var request = new LoraLoadRequest
            {
                Path = loraPath,
                Weight = weight,
                Name = adapterName
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/lora/load", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LoraLoadResult>(ct);
            _logger?.LogInformation("LoRA loaded: {Path}, weight: {Weight}", loraPath, weight);

            return result ?? new LoraLoadResult { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load LoRA: {Path}", loraPath);
            return new LoraLoadResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Unload all LoRA adapters
    /// </summary>
    public async Task<bool> UnloadLorasAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return false;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/lora/unload", null, ct);
            response.EnsureSuccessStatusCode();
            _logger?.LogInformation("All LoRAs unloaded");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to unload LoRAs");
            return false;
        }
    }

    #endregion

    #region Scheduler Management

    /// <summary>
    /// Get available schedulers from the server
    /// </summary>
    public async Task<List<string>> GetAvailableSchedulersAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return new List<string>();

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/schedulers", ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SchedulersResponse>(ct);
            return result?.Schedulers ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get schedulers");
            return new List<string>();
        }
    }

    #endregion

    #region Inpainting / Outpainting

    /// <summary>
    /// Inpaint (edit) specific parts of an image using a mask
    /// </summary>
    public async Task<InpaintResult> GenerateInpaintAsync(
        DiffusersInpaintRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new InpaintResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, "Generating inpaint..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/inpaint", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<InpaintResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Inpaint complete"));

            return result ?? new InpaintResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new InpaintResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Inpaint generation failed");
            return new InpaintResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Extend image canvas in specified direction(s) using outpainting
    /// </summary>
    public async Task<OutpaintResult> GenerateOutpaintAsync(
        DiffusersOutpaintRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new OutpaintResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, $"Outpainting ({request.Direction})..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/outpaint", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OutpaintResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Outpaint complete"));

            return result ?? new OutpaintResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new OutpaintResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Outpaint generation failed");
            return new OutpaintResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Upscaling

    /// <summary>
    /// Upscale an image using Real-ESRGAN
    /// </summary>
    public async Task<UpscaleResult> GenerateUpscaleAsync(
        DiffusersUpscaleRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new UpscaleResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, $"Upscaling image ({request.Scale}x)..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/upscale", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<UpscaleResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Upscale complete"));

            return result ?? new UpscaleResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new UpscaleResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Upscale generation failed");
            return new UpscaleResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region IP-Adapter

    /// <summary>
    /// Load IP-Adapter for style/content transfer
    /// </summary>
    public async Task<IPAdapterLoadResult> LoadIPAdapterAsync(string adapterName = "ip-adapter_sd15", CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new IPAdapterLoadResult { Success = false, Error = "Engine not running" };
        }

        try
        {
            var url = $"http://localhost:{Port}/ip-adapter/load?adapter_name={Uri.EscapeDataString(adapterName)}";
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync(url, null, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<IPAdapterLoadResult>(ct);
            _logger?.LogInformation("IP-Adapter loaded: {Adapter}", adapterName);

            return result ?? new IPAdapterLoadResult { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load IP-Adapter: {Adapter}", adapterName);
            return new IPAdapterLoadResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Unload IP-Adapter to free VRAM
    /// </summary>
    public async Task<bool> UnloadIPAdapterAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return false;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/ip-adapter/unload", null, ct);
            response.EnsureSuccessStatusCode();
            _logger?.LogInformation("IP-Adapter unloaded");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to unload IP-Adapter");
            return false;
        }
    }

    /// <summary>
    /// Generate image using IP-Adapter for style/content transfer
    /// </summary>
    public async Task<IPAdapterResult> GenerateIPAdapterAsync(
        DiffusersIPAdapterRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new IPAdapterResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, "Generating with IP-Adapter..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/ip-adapter", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<IPAdapterResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "IP-Adapter generation complete"));

            return result ?? new IPAdapterResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new IPAdapterResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "IP-Adapter generation failed");
            return new IPAdapterResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Multi-ControlNet

    /// <summary>
    /// Generate image using multiple ControlNet conditions simultaneously
    /// </summary>
    public async Task<MultiControlNetResult> GenerateMultiControlNetAsync(
        DiffusersMultiControlNetRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new MultiControlNetResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            var controlTypes = string.Join("+", request.Controls?.Select(c => c.ControlType) ?? Array.Empty<string>());
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, $"Generating with Multi-ControlNet ({controlTypes})..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/multi-controlnet", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MultiControlNetResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Multi-ControlNet generation complete"));

            return result ?? new MultiControlNetResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new MultiControlNetResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Multi-ControlNet generation failed");
            return new MultiControlNetResult { Success = false, Error = ex.Message };
        }
    }

    #endregion

    #region Queue System

    /// <summary>
    /// Add a task to the generation queue
    /// </summary>
    public async Task<QueueAddResult> QueueAddTaskAsync(QueuedTaskRequest request, CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new QueueAddResult { Success = false, Error = "Engine not running" };
        }

        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/queue/add", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<QueueAddResult>(ct);
            _logger?.LogInformation("Task queued: {TaskId} (type={Type}, priority={Priority})",
                result?.TaskId, request.TaskType, request.Priority);

            return result ?? new QueueAddResult { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to add task to queue");
            return new QueueAddResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Get status of a queued task
    /// </summary>
    public async Task<QueueTaskStatus?> QueueGetStatusAsync(string taskId, CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/queue/status/{Uri.EscapeDataString(taskId)}", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueTaskStatus>(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get queue task status: {TaskId}", taskId);
            return null;
        }
    }

    /// <summary>
    /// Cancel a queued task
    /// </summary>
    public async Task<bool> QueueCancelTaskAsync(string taskId, CancellationToken ct = default)
    {
        if (!_isRunning)
            return false;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/queue/cancel/{Uri.EscapeDataString(taskId)}", null, ct);
            response.EnsureSuccessStatusCode();
            _logger?.LogInformation("Queue task cancelled: {TaskId}", taskId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cancel queue task: {TaskId}", taskId);
            return false;
        }
    }

    /// <summary>
    /// List all tasks in the queue
    /// </summary>
    public async Task<QueueListResult?> QueueListAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/queue/list", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QueueListResult>(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get queue list");
            return null;
        }
    }

    /// <summary>
    /// Clear all pending tasks from the queue
    /// </summary>
    public async Task<QueueClearResult?> QueueClearAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/queue/clear", null, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<QueueClearResult>(ct);
            _logger?.LogInformation("Queue cleared: {Count} tasks", result?.ClearedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clear queue");
            return null;
        }
    }

    #endregion

    #region ControlNet

    /// <summary>
    /// Generate an image with ControlNet guidance
    /// </summary>
    public async Task<ControlNetResult> GenerateControlNetAsync(
        DiffusersControlNetRequest request,
        CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new ControlNetResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, $"Generating with ControlNet ({request.ControlType})..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/generate/controlnet", content, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ControlNetResult>(ct);

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "ControlNet generation complete"));

            return result ?? new ControlNetResult { Success = false, Error = "Empty response" };
        }
        catch (OperationCanceledException)
        {
            return new ControlNetResult { Success = false, Error = "Cancelled" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ControlNet generation failed");
            return new ControlNetResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Get available ControlNet types for the current model
    /// </summary>
    public async Task<ControlNetTypesResult?> GetAvailableControlNetTypesAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/controlnet/types", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ControlNetTypesResult>(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get ControlNet types");
            return null;
        }
    }

    /// <summary>
    /// Pre-load a ControlNet model
    /// </summary>
    public async Task<ControlNetLoadResult> LoadControlNetAsync(string controlType, string? customModel = null, CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            return new ControlNetLoadResult { Success = false, Error = "Engine not running" };
        }

        try
        {
            var url = $"http://localhost:{Port}/controlnet/load?control_type={Uri.EscapeDataString(controlType)}";
            if (!string.IsNullOrEmpty(customModel))
            {
                url += $"&custom_model={Uri.EscapeDataString(customModel)}";
            }

            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync(url, null, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ControlNetLoadResult>(ct);
            _logger?.LogInformation("ControlNet loaded: {Type}", controlType);

            return result ?? new ControlNetLoadResult { Success = false, Error = "Empty response" };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load ControlNet: {Type}", controlType);
            return new ControlNetLoadResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Unload all ControlNet models to free VRAM
    /// </summary>
    public async Task<bool> UnloadControlNetsAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return false;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/controlnet/unload", null, ct);
            response.EnsureSuccessStatusCode();
            _logger?.LogInformation("All ControlNets unloaded");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to unload ControlNets");
            return false;
        }
    }

    #endregion

    #region Progress & Cancellation

    /// <summary>
    /// Get current generation progress from the server
    /// </summary>
    public async Task<GenerationProgressInfo?> GetGenerationProgressAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/progress", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GenerationProgressInfo>(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get generation progress");
            return null;
        }
    }

    /// <summary>
    /// Cancel current generation
    /// </summary>
    public async Task<bool> CancelGenerationAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return false;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).PostAsync($"http://localhost:{Port}/cancel", null, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CancelGenerationResult>(ct);
            if (result?.Success == true)
            {
                _logger?.LogInformation("Generation cancellation requested");
                StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, "Generation cancelled"));
            }
            return result?.Success ?? false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to cancel generation");
            return false;
        }
    }

    #endregion

    #region Engine Info

    /// <summary>
    /// Get engine status
    /// </summary>
    public async Task<EngineInfo?> GetEngineInfoAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/info", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EngineInfo>(ct);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get startup validation status from server
    /// </summary>
    public async Task<StartupStatusResponse?> GetStartupStatusAsync(CancellationToken ct = default)
    {
        if (!_isRunning)
            return null;

        try
        {
            var response = await (_httpClient ?? throw new InvalidOperationException("Engine not running")).GetAsync($"http://localhost:{Port}/startup-status", ct);
            response.EnsureSuccessStatusCode();
            var status = await response.Content.ReadFromJsonAsync<StartupStatusResponse>(ct);

            // Raise event if there are warnings or missing features
            if (status != null && (status.Warnings.Count > 0 || status.MissingFeatures.Count > 0))
            {
                StartupValidationCompleted?.Invoke(this, new StartupValidationEventArgs(status));
            }

            return status;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get startup status");
            return null;
        }
    }

    /// <summary>
    /// Check startup status and log results
    /// </summary>
    public async Task<bool> ValidateStartupAsync(CancellationToken ct = default)
    {
        var status = await GetStartupStatusAsync(ct);
        if (status == null)
        {
            LogOutput?.Invoke(this, new EngineLogEventArgs("[Validation] Cannot connect to server", true));
            return false;
        }

        // Log all steps
        foreach (var step in status.Steps)
        {
            var icon = step.Status == "ok" ? "[OK]" : step.Status == "error" ? "[X]" : "[!]";
            var msg = $"[Step {step.StepNumber}] {icon} {step.Name}";
            if (!string.IsNullOrEmpty(step.Message))
                msg += $": {step.Message}";
            LogOutput?.Invoke(this, new EngineLogEventArgs(msg, step.Status == "error"));
        }

        // Log summary
        if (status.Errors.Count > 0)
        {
            LogOutput?.Invoke(this, new EngineLogEventArgs($"[Validation] [ERROR] Found {status.Errors.Count} error(s)", true));
            foreach (var err in status.Errors)
                LogOutput?.Invoke(this, new EngineLogEventArgs($"  - {err}", true));
            return false;
        }

        if (status.MissingFeatures.Count > 0)
        {
            LogOutput?.Invoke(this, new EngineLogEventArgs($"[Validation] [WARN] Missing {status.MissingFeatures.Count} feature(s)"));
            foreach (var feature in status.MissingFeatures)
                LogOutput?.Invoke(this, new EngineLogEventArgs($"  - {feature}"));
        }

        if (status.Warnings.Count > 0)
        {
            LogOutput?.Invoke(this, new EngineLogEventArgs($"[Validation] [WARN] Found {status.Warnings.Count} warning(s)"));
        }

        return status.CanContinue;
    }

    #endregion

    #region VRAM Monitoring

    /// <summary>
    /// Get real-time VRAM usage
    /// </summary>
    public async Task<VramUsage> GetVramUsageAsync(CancellationToken ct = default)
    {
        return await _gpuService.GetVramUsageAsync(ct);
    }

    /// <summary>
    /// Refresh GPU information
    /// </summary>
    public async Task<GpuInfo> RefreshGpuInfoAsync(CancellationToken ct = default)
    {
        _cachedGpuInfo = await _gpuService.DetectGpuAsync(forceRefresh: true, ct: ct);
        GpuStatusChanged?.Invoke(this, new LocalGpuStatusEventArgs(_cachedGpuInfo));
        return _cachedGpuInfo;
    }

    /// <summary>
    /// Get compatible models for current GPU
    /// </summary>
    public IEnumerable<string> GetCompatibleModelTypes()
    {
        if (_cachedGpuInfo == null || !_cachedGpuInfo.IsAvailable)
        {
            yield return "SD1.5"; // CPU can handle SD1.5 (slowly)
            yield break;
        }

        foreach (var (modelType, requiredVram) in ModelVramRequirements)
        {
            if (_cachedGpuInfo.TotalVramGb >= requiredVram)
            {
                yield return modelType;
            }
        }
    }

    #endregion

    #region Private Methods

    private async Task EnsureScriptExistsAsync()
    {
        var scriptPath = Path.Combine(_scriptsDir, "generation_server.py");

        // Always update the script
        await File.WriteAllTextAsync(scriptPath, GetGenerationServerScript());
        _logger?.LogInformation("Generation script written to: {Path}", scriptPath);
    }

    #endregion

    #region Python Script

    private static string GetGenerationServerScript()
    {
        // Try to load from embedded resource file first
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var resourceName = "AIManager.Core.Services.generation_server.py";

        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        catch
        {
            // Fall back to inline script
        }

        // Try to load from external Python file
        var scriptPath = Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
            "generation_server.py");

        if (File.Exists(scriptPath))
        {
            return File.ReadAllText(scriptPath);
        }

        // Fallback: Generate minimal script inline
        return GenerateMinimalScript();
    }

    private static string GenerateMinimalScript()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#!/usr/bin/env python3");
        sb.AppendLine("import argparse, base64, gc, io, json, os, sys, time, traceback, threading");
        sb.AppendLine("from http.server import HTTPServer, BaseHTTPRequestHandler");
        sb.AppendLine();
        sb.AppendLine("try:");
        sb.AppendLine("    import torch");
        sb.AppendLine("    from PIL import Image");
        sb.AppendLine("    from diffusers import AutoPipelineForText2Image, StableDiffusionXLPipeline");
        sb.AppendLine("    HAS_DIFFUSERS = True");
        sb.AppendLine("except ImportError:");
        sb.AppendLine("    HAS_DIFFUSERS = False");
        sb.AppendLine("    print('Missing packages: pip install torch diffusers transformers accelerate Pillow')");
        sb.AppendLine("    sys.exit(1)");
        sb.AppendLine();
        sb.AppendLine("class GenerationServer:");
        sb.AppendLine("    def __init__(self, models_dir):");
        sb.AppendLine("        self.models_dir = models_dir");
        sb.AppendLine("        self.pipeline = None");
        sb.AppendLine("        self.current_model = None");
        sb.AppendLine("        self.device = 'cuda' if torch.cuda.is_available() else 'cpu'");
        sb.AppendLine("        self.dtype = torch.float16 if self.device == 'cuda' else torch.float32");
        sb.AppendLine("        print(f'[Engine] Device: {self.device}')");
        sb.AppendLine();
        sb.AppendLine("    def load_model(self, model_id, model_type='TextToImage'):");
        sb.AppendLine("        self.unload_model()");
        sb.AppendLine("        try:");
        sb.AppendLine("            local_path = os.path.join(self.models_dir, 'checkpoints', model_id.replace('/', '--'))");
        sb.AppendLine("            use_local = os.path.exists(local_path)");
        sb.AppendLine("            path = local_path if use_local else model_id");
        sb.AppendLine("            if 'xl' in model_id.lower() or 'sdxl' in model_id.lower():");
        sb.AppendLine("                self.pipeline = StableDiffusionXLPipeline.from_pretrained(path, torch_dtype=self.dtype, use_safetensors=True, local_files_only=use_local)");
        sb.AppendLine("            else:");
        sb.AppendLine("                self.pipeline = AutoPipelineForText2Image.from_pretrained(path, torch_dtype=self.dtype, use_safetensors=True, local_files_only=use_local)");
        sb.AppendLine("            self.pipeline = self.pipeline.to(self.device)");
        sb.AppendLine("            if hasattr(self.pipeline, 'enable_attention_slicing'): self.pipeline.enable_attention_slicing()");
        sb.AppendLine("            self.current_model = model_id");
        sb.AppendLine("            return {'success': True, 'model': model_id}");
        sb.AppendLine("        except Exception as e:");
        sb.AppendLine("            traceback.print_exc()");
        sb.AppendLine("            return {'success': False, 'error': str(e)}");
        sb.AppendLine();
        sb.AppendLine("    def unload_model(self):");
        sb.AppendLine("        if self.pipeline: del self.pipeline");
        sb.AppendLine("        self.pipeline = None");
        sb.AppendLine("        self.current_model = None");
        sb.AppendLine("        gc.collect()");
        sb.AppendLine("        if torch.cuda.is_available(): torch.cuda.empty_cache()");
        sb.AppendLine();
        sb.AppendLine("    def generate_image(self, params):");
        sb.AppendLine("        if not self.pipeline: return {'success': False, 'error': 'No model loaded'}");
        sb.AppendLine("        try:");
        sb.AppendLine("            seed = params.get('seed', -1)");
        sb.AppendLine("            if seed < 0: seed = int(torch.randint(0, 2**32, (1,)).item())");
        sb.AppendLine("            gen = torch.Generator(device=self.device).manual_seed(seed)");
        sb.AppendLine("            result = self.pipeline(prompt=params.get('prompt', ''), negative_prompt=params.get('negative_prompt'),");
        sb.AppendLine("                width=params.get('width', 1024), height=params.get('height', 1024),");
        sb.AppendLine("                num_inference_steps=params.get('steps', 30), guidance_scale=params.get('guidance_scale', 7.5), generator=gen)");
        sb.AppendLine("            images = []");
        sb.AppendLine("            for img in result.images:");
        sb.AppendLine("                buf = io.BytesIO()");
        sb.AppendLine("                img.save(buf, format='PNG')");
        sb.AppendLine("                images.append('data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode())");
        sb.AppendLine("            return {'success': True, 'images': images, 'seed': seed}");
        sb.AppendLine("        except Exception as e:");
        sb.AppendLine("            traceback.print_exc()");
        sb.AppendLine("            return {'success': False, 'error': str(e)}");
        sb.AppendLine();
        sb.AppendLine("    def get_info(self):");
        sb.AppendLine("        gpu_info = {}");
        sb.AppendLine("        if torch.cuda.is_available():");
        sb.AppendLine("            gpu_info = {'name': torch.cuda.get_device_name(0), 'total_memory_gb': torch.cuda.get_device_properties(0).total_memory / 1024**3}");
        sb.AppendLine("        return {'status': 'ready', 'device': self.device, 'current_model': self.current_model, 'gpu': gpu_info}");
        sb.AppendLine();
        sb.AppendLine("class RequestHandler(BaseHTTPRequestHandler):");
        sb.AppendLine("    server_instance = None");
        sb.AppendLine("    def log_message(self, fmt, *args): print(f'[HTTP] {args[0]}')");
        sb.AppendLine("    def send_json(self, data, status=200):");
        sb.AppendLine("        self.send_response(status)");
        sb.AppendLine("        self.send_header('Content-Type', 'application/json')");
        sb.AppendLine("        self.send_header('Access-Control-Allow-Origin', '*')");
        sb.AppendLine("        self.end_headers()");
        sb.AppendLine("        self.wfile.write(json.dumps(data).encode())");
        sb.AppendLine("    def do_GET(self):");
        sb.AppendLine("        if self.path == '/health': self.send_json({'status': 'ok'})");
        sb.AppendLine("        elif self.path == '/info': self.send_json(self.server_instance.engine.get_info())");
        sb.AppendLine("        else: self.send_json({'error': 'Not found'}, 404)");
        sb.AppendLine("    def do_POST(self):");
        sb.AppendLine("        length = int(self.headers.get('Content-Length', 0))");
        sb.AppendLine("        data = json.loads(self.rfile.read(length).decode()) if length > 0 else {}");
        sb.AppendLine("        if self.path == '/load-model': self.send_json(self.server_instance.engine.load_model(data.get('model_id', ''), data.get('model_type', 'TextToImage')))");
        sb.AppendLine("        elif self.path == '/unload-model': self.server_instance.engine.unload_model(); self.send_json({'success': True})");
        sb.AppendLine("        elif self.path == '/generate/image': self.send_json(self.server_instance.engine.generate_image(data))");
        sb.AppendLine("        elif self.path == '/shutdown': self.send_json({'status': 'shutting down'}); threading.Thread(target=self.server_instance.shutdown).start()");
        sb.AppendLine("        else: self.send_json({'error': 'Not found'}, 404)");
        sb.AppendLine("    def do_OPTIONS(self):");
        sb.AppendLine("        self.send_response(200)");
        sb.AppendLine("        self.send_header('Access-Control-Allow-Origin', '*')");
        sb.AppendLine("        self.send_header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS')");
        sb.AppendLine("        self.send_header('Access-Control-Allow-Headers', 'Content-Type')");
        sb.AppendLine("        self.end_headers()");
        sb.AppendLine();
        sb.AppendLine("class GenerationHTTPServer(HTTPServer):");
        sb.AppendLine("    def __init__(self, port, models_dir):");
        sb.AppendLine("        self.engine = GenerationServer(models_dir)");
        sb.AppendLine("        RequestHandler.server_instance = self");
        sb.AppendLine("        super().__init__(('0.0.0.0', port), RequestHandler)");
        sb.AppendLine();
        sb.AppendLine("if __name__ == '__main__':");
        sb.AppendLine("    parser = argparse.ArgumentParser()");
        sb.AppendLine("    parser.add_argument('--port', type=int, default=5050)");
        sb.AppendLine("    parser.add_argument('--models-dir', required=True)");
        sb.AppendLine("    args = parser.parse_args()");
        sb.AppendLine("    print(f'Starting server on port {args.port}...')");
        sb.AppendLine("    server = GenerationHTTPServer(args.port, args.models_dir)");
        sb.AppendLine("    server.serve_forever()");
        return sb.ToString();
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            await StopAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during async dispose");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            // Use Task.Run to avoid deadlock when called from sync context
            Task.Run(async () => await StopAsync()).Wait(TimeSpan.FromSeconds(10));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error during dispose, force-killing process");
            try { _serverProcess?.Kill(true); } catch { }
        }
        finally
        {
            _operationLock.Dispose();
        }
    }

    /// <summary>
    /// Ensures engine is running and HttpClient is available
    /// </summary>
    private bool EnsureRunning([NotNullWhen(true)] out HttpClient? client, out string error)
    {
        if (!_isRunning || _httpClient == null)
        {
            client = null;
            error = "Engine not running";
            return false;
        }
        client = _httpClient;
        error = "";
        return true;
    }

    #region GPU Resource Management

    /// <summary>
    /// Get GPU resource limits from the running server
    /// </summary>
    public async Task<GpuLimitsResponse?> GetGpuLimitsAsync(CancellationToken ct = default)
    {
        if (!EnsureRunning(out var client, out var err))
            return null;

        try
        {
            var response = await client.GetAsync($"http://localhost:{Port}/gpu/limits", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<GpuLimitsResponse>(ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get GPU limits");
        }
        return null;
    }

    /// <summary>
    /// Update GPU resource limits on the running server
    /// </summary>
    public async Task<GpuLimitsResponse?> SetGpuLimitsAsync(
        double? maxVramPercent = null,
        double? reservedVramGb = null,
        bool? oomAutoRecovery = null,
        bool? autoReduceBatchSize = null,
        CancellationToken ct = default)
    {
        if (!EnsureRunning(out var client, out var err))
            return null;

        try
        {
            var queryParts = new List<string>();
            if (maxVramPercent.HasValue) queryParts.Add($"max_vram_percent={maxVramPercent.Value}");
            if (reservedVramGb.HasValue) queryParts.Add($"reserved_vram_gb={reservedVramGb.Value}");
            if (oomAutoRecovery.HasValue) queryParts.Add($"oom_auto_recovery={oomAutoRecovery.Value.ToString().ToLower()}");
            if (autoReduceBatchSize.HasValue) queryParts.Add($"auto_reduce_batch_size={autoReduceBatchSize.Value.ToString().ToLower()}");

            var query = string.Join("&", queryParts);
            var response = await client.PostAsync($"http://localhost:{Port}/gpu/limits?{query}", null, ct);

            if (response.IsSuccessStatusCode)
            {
                // Update local config too
                if (maxVramPercent.HasValue) GpuConfig.MaxVramUsagePercent = maxVramPercent.Value;
                if (reservedVramGb.HasValue) GpuConfig.ReservedVramGb = reservedVramGb.Value;
                if (oomAutoRecovery.HasValue) GpuConfig.OomAutoRecovery = oomAutoRecovery.Value;
                if (autoReduceBatchSize.HasValue) GpuConfig.AutoReduceBatchSize = autoReduceBatchSize.Value;

                return await response.Content.ReadFromJsonAsync<GpuLimitsResponse>(ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to set GPU limits");
        }
        return null;
    }

    /// <summary>
    /// Get GPU status (VRAM usage, throttle state, etc.)
    /// </summary>
    public async Task<GpuStatusResponse?> GetGpuStatusAsync(CancellationToken ct = default)
    {
        if (!EnsureRunning(out var client, out var err))
            return null;

        try
        {
            var response = await client.GetAsync($"http://localhost:{Port}/gpu/status", ct);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<GpuStatusResponse>(ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get GPU status");
        }
        return null;
    }

    #endregion
}

#region Models

/// <summary>
/// Engine status
/// </summary>
public enum EngineStatus
{
    Stopped,
    Starting,
    Running,
    Loading,
    Generating,
    Error
}

/// <summary>
/// Engine status event args
/// </summary>
public class EngineStatusEventArgs : EventArgs
{
    public EngineStatus Status { get; }
    public string Message { get; }
    public EngineStatusEventArgs(EngineStatus status, string message)
    {
        Status = status;
        Message = message;
    }
}

/// <summary>
/// Diffusers generation progress event args
/// </summary>
public class DiffusersProgressEventArgs : EventArgs
{
    public int Step { get; set; }
    public int TotalSteps { get; set; }
    public double Progress => TotalSteps > 0 ? (double)Step / TotalSteps * 100 : 0;
}

/// <summary>
/// Model loading progress event args
/// </summary>
public class ModelLoadProgressEventArgs : EventArgs
{
    public string ModelId { get; set; } = "";
    public string Stage { get; set; } = "";
    public int Progress { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Engine log output event args
/// </summary>
public class EngineLogEventArgs : EventArgs
{
    public string Message { get; }
    public bool IsError { get; }
    public DateTime Timestamp { get; }

    public EngineLogEventArgs(string message, bool isError = false)
    {
        Message = message;
        IsError = isError;
        Timestamp = DateTime.Now;
    }
}

/// <summary>
/// Startup validation status from Python server
/// </summary>
public class StartupStatusResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("can_continue")]
    public bool CanContinue { get; set; }

    [JsonPropertyName("errors")]
    public List<string> Errors { get; set; } = new();

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();

    [JsonPropertyName("missing_features")]
    public List<string> MissingFeatures { get; set; } = new();

    [JsonPropertyName("steps")]
    public List<StartupStep> Steps { get; set; } = new();

    [JsonPropertyName("has_gpu")]
    public bool HasGpu { get; set; }

    [JsonPropertyName("gpu_name")]
    public string? GpuName { get; set; }

    [JsonPropertyName("controlnet_available")]
    public bool ControlNetAvailable { get; set; }

    [JsonPropertyName("ip_adapter_available")]
    public bool IpAdapterAvailable { get; set; }
}

/// <summary>
/// Startup validation step
/// </summary>
public class StartupStep
{
    [JsonPropertyName("step")]
    public int StepNumber { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

/// <summary>
/// Startup validation event args
/// </summary>
public class StartupValidationEventArgs : EventArgs
{
    public StartupStatusResponse Status { get; }
    public bool RequiresUserAction { get; }
    public string Summary { get; }

    public StartupValidationEventArgs(StartupStatusResponse status)
    {
        Status = status;
        RequiresUserAction = status.Warnings.Count > 0 || status.MissingFeatures.Count > 0;

        if (status.Errors.Count > 0)
            Summary = $"[ERROR] Found {status.Errors.Count} error(s) - Cannot start";
        else if (status.Warnings.Count > 0)
            Summary = $"[WARN] Found {status.Warnings.Count} warning(s) - Some features unavailable";
        else
            Summary = "[OK] System ready with all features";
    }
}

/// <summary>
/// Engine info
/// </summary>
public class EngineInfo
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("current_model")]
    public string? CurrentModel { get; set; }

    [JsonPropertyName("gpu")]
    public GpuInfo? Gpu { get; set; }

    [JsonPropertyName("has_diffusers")]
    public bool HasDiffusers { get; set; }

    public class GpuInfo
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("total_memory_gb")]
        public double TotalMemoryGb { get; set; }

        [JsonPropertyName("free_memory_gb")]
        public double FreeMemoryGb { get; set; }
    }
}

/// <summary>
/// Load model request
/// </summary>
public class LoadModelRequest
{
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = "";

    [JsonPropertyName("model_type")]
    public string ModelType { get; set; } = "TextToImage";
}

/// <summary>
/// Diffusers image generation request
/// </summary>
public class DiffusersImageRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("sampler")]
    public string? Sampler { get; set; }

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonPropertyName("clip_skip")]
    public int ClipSkip { get; set; } = 1;

    [JsonPropertyName("lora_models")]
    public List<LoraModelInfo>? LoraModels { get; set; }
}

/// <summary>
/// Diffusers img2img generation request
/// </summary>
public class DiffusersImg2ImgRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    [JsonPropertyName("image")]
    public string Image { get; set; } = "";  // Base64 encoded image

    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 0.75;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonPropertyName("clip_skip")]
    public int ClipSkip { get; set; } = 1;

    [JsonPropertyName("lora_models")]
    public List<LoraModelInfo>? LoraModels { get; set; }
}

/// <summary>
/// Diffusers video generation request
/// </summary>
public class DiffusersVideoRequest
{
    [JsonPropertyName("prompt")]
    public string? Prompt { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("num_frames")]
    public int NumFrames { get; set; } = 25;

    [JsonPropertyName("fps")]
    public int Fps { get; set; } = 7;

    [JsonPropertyName("motion_bucket_id")]
    public int MotionBucketId { get; set; } = 127;

    [JsonPropertyName("noise_aug_strength")]
    public double NoiseAugStrength { get; set; } = 0.02;

    [JsonPropertyName("seed")]
    public int Seed { get; set; } = -1;
}

/// <summary>
/// LoRA model info for generation requests
/// </summary>
public class LoraModelInfo
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// LoRA load request
/// </summary>
public class LoraLoadRequest
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// LoRA load result
/// </summary>
public class LoraLoadResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("loaded_loras")]
    public List<string>? LoadedLoras { get; set; }
}

/// <summary>
/// Schedulers response
/// </summary>
public class SchedulersResponse
{
    [JsonPropertyName("schedulers")]
    public List<string> Schedulers { get; set; } = new();
}

/// <summary>
/// Generation progress info from server
/// </summary>
public class GenerationProgressInfo
{
    [JsonPropertyName("is_generating")]
    public bool IsGenerating { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("total_steps")]
    public int TotalSteps { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; }
}

/// <summary>
/// Cancel generation result
/// </summary>
public class CancelGenerationResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// Diffusers generation result
/// </summary>
public class DiffusersResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("frames")]
    public List<string>? Frames { get; set; }

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("generation_time")]
    public double GenerationTime { get; set; }

    [JsonPropertyName("fps")]
    public int Fps { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }

    [JsonPropertyName("frame_count")]
    public int FrameCount { get; set; }
}

/// <summary>
/// Pre-flight check result
/// </summary>
public class PreflightCheckResult
{
    public bool IsReady { get; set; }
    public bool HasGpu { get; set; }
    public bool HasPython { get; set; }
    public bool PythonReady { get; set; }
    public bool AutoInstalled { get; set; }
    public GpuInfo? GpuInfo { get; set; }
    public PythonEnvironmentInfo? PythonEnv { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? InstallCommand { get; set; }
}

/// <summary>
/// Engine start result
/// </summary>
public class EngineStartResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public GpuInfo? GpuInfo { get; set; }
    public PreflightCheckResult? PreflightResult { get; set; }
}

/// <summary>
/// Local GPU status event args for DiffusersGenerationEngine
/// </summary>
public class LocalGpuStatusEventArgs : EventArgs
{
    public GpuInfo GpuInfo { get; }
    public LocalGpuStatusEventArgs(GpuInfo gpuInfo)
    {
        GpuInfo = gpuInfo;
    }
}

/// <summary>
/// Model load check result
/// </summary>
public class ModelLoadCheckResult
{
    public string ModelId { get; set; } = "";
    public bool CanLoad { get; set; }
    public string Message { get; set; } = "";
    public double RequiredVramGb { get; set; }
    public double CurrentFreeVramGb { get; set; }
    public double VramUsagePercent { get; set; }
    public bool SuggestOffloading { get; set; }
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Model load result
/// </summary>
public class ModelLoadResult
{
    public bool Success { get; set; }
    public string? ModelId { get; set; }
    public string? Error { get; set; }
    public int VramUsedMb { get; set; }
    public ModelLoadCheckResult? VramCheck { get; set; }
}

/// <summary>
/// Server response for load model
/// </summary>
public class LoadModelServerResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }
}

/// <summary>
/// Inpaint request - edit specific parts of an image
/// </summary>
public class DiffusersInpaintRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Original image (base64 encoded)
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    /// <summary>
    /// Mask image (base64 encoded) - white = area to inpaint, black = keep
    /// </summary>
    [JsonPropertyName("mask")]
    public string Mask { get; set; } = "";

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    /// <summary>
    /// Strength controls how much the masked area is changed (0.0-1.0)
    /// </summary>
    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 0.99;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonPropertyName("clip_skip")]
    public int ClipSkip { get; set; } = 1;

    [JsonPropertyName("lora_models")]
    public List<LoraModelInfo>? LoraModels { get; set; }
}

/// <summary>
/// Inpaint result
/// </summary>
public class InpaintResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("generation_time")]
    public double GenerationTime { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }
}

/// <summary>
/// Outpaint request - extend image canvas
/// </summary>
public class DiffusersOutpaintRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Original image (base64 encoded)
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    /// <summary>
    /// Direction to extend: left, right, top, bottom, or combination like "left,top"
    /// </summary>
    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "right";

    /// <summary>
    /// How many pixels to extend
    /// </summary>
    [JsonPropertyName("extend_pixels")]
    public int ExtendPixels { get; set; } = 256;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    [JsonPropertyName("strength")]
    public double Strength { get; set; } = 0.85;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    /// <summary>
    /// Feather/blend the mask edges for smoother transitions
    /// </summary>
    [JsonPropertyName("feather_pixels")]
    public int FeatherPixels { get; set; } = 32;
}

/// <summary>
/// Outpaint result
/// </summary>
public class OutpaintResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("generation_time")]
    public double GenerationTime { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }

    [JsonPropertyName("original_size")]
    public ImageSize? OriginalSize { get; set; }

    [JsonPropertyName("new_size")]
    public ImageSize? NewSize { get; set; }

    [JsonPropertyName("directions")]
    public List<string>? Directions { get; set; }
}

/// <summary>
/// Image dimensions
/// </summary>
public class ImageSize
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}

/// <summary>
/// ControlNet generation request
/// </summary>
public class DiffusersControlNetRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Control image (base64 encoded) - can be raw image or pre-processed
    /// </summary>
    [JsonPropertyName("control_image")]
    public string ControlImage { get; set; } = "";

    /// <summary>
    /// ControlNet type: canny, pose, depth, hed, lineart, scribble, softedge, normal, tile
    /// </summary>
    [JsonPropertyName("control_type")]
    public string ControlType { get; set; } = "canny";

    /// <summary>
    /// Custom ControlNet model ID (auto-detected if not specified)
    /// </summary>
    [JsonPropertyName("controlnet_model")]
    public string? ControlNetModel { get; set; }

    /// <summary>
    /// Whether to preprocess the control image (auto-detect edges, pose, etc.)
    /// </summary>
    [JsonPropertyName("preprocess")]
    public bool Preprocess { get; set; } = true;

    /// <summary>
    /// Canny edge detection low threshold (only used when preprocess=true and control_type=canny)
    /// </summary>
    [JsonPropertyName("canny_low")]
    public int CannyLow { get; set; } = 100;

    /// <summary>
    /// Canny edge detection high threshold
    /// </summary>
    [JsonPropertyName("canny_high")]
    public int CannyHigh { get; set; } = 200;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    /// <summary>
    /// ControlNet conditioning scale (0.0-2.0, default 1.0)
    /// Higher values = stronger control image influence
    /// </summary>
    [JsonPropertyName("controlnet_conditioning_scale")]
    public double ControlNetConditioningScale { get; set; } = 1.0;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonPropertyName("clip_skip")]
    public int ClipSkip { get; set; } = 1;

    [JsonPropertyName("lora_models")]
    public List<LoraModelInfo>? LoraModels { get; set; }
}

/// <summary>
/// ControlNet generation result
/// </summary>
public class ControlNetResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    /// <summary>
    /// Preview of the preprocessed control image (for debugging)
    /// </summary>
    [JsonPropertyName("control_preview")]
    public string? ControlPreview { get; set; }

    [JsonPropertyName("control_type")]
    public string? ControlType { get; set; }

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("generation_time")]
    public double GenerationTime { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }
}

/// <summary>
/// Available ControlNet types
/// </summary>
public class ControlNetTypesResult
{
    [JsonPropertyName("model_family")]
    public string ModelFamily { get; set; } = "";

    [JsonPropertyName("available_types")]
    public List<string> AvailableTypes { get; set; } = new();

    [JsonPropertyName("loaded_types")]
    public List<string> LoadedTypes { get; set; } = new();

    [JsonPropertyName("has_controlnet_aux")]
    public bool HasControlNetAux { get; set; }
}

/// <summary>
/// ControlNet load result
/// </summary>
public class ControlNetLoadResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("control_type")]
    public string? ControlType { get; set; }

    [JsonPropertyName("cached")]
    public bool Cached { get; set; }
}

/// <summary>
/// Upscale request - upscale image using Real-ESRGAN
/// </summary>
public class DiffusersUpscaleRequest
{
    /// <summary>
    /// Input image (base64 encoded)
    /// </summary>
    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    /// <summary>
    /// Scale factor: 2, 3, or 4
    /// </summary>
    [JsonPropertyName("scale")]
    public int Scale { get; set; } = 2;

    /// <summary>
    /// Upscaler model: realesrgan, realesrgan-anime, realesrgan-x2
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = "realesrgan";

    /// <summary>
    /// Denoise strength (0.0-1.0)
    /// </summary>
    [JsonPropertyName("denoise_strength")]
    public double DenoiseStrength { get; set; } = 0.5;

    /// <summary>
    /// Tile size for processing large images (0 = no tiling)
    /// </summary>
    [JsonPropertyName("tile_size")]
    public int TileSize { get; set; } = 0;

    /// <summary>
    /// Output format: png or jpg
    /// </summary>
    [JsonPropertyName("output_format")]
    public string OutputFormat { get; set; } = "png";
}

/// <summary>
/// Upscale result
/// </summary>
public class UpscaleResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("original_size")]
    public ImageSize? OriginalSize { get; set; }

    [JsonPropertyName("output_size")]
    public ImageSize? OutputSize { get; set; }

    [JsonPropertyName("scale")]
    public int Scale { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }
}

/// <summary>
/// IP-Adapter request - use reference images for style/content transfer
/// </summary>
public class DiffusersIPAdapterRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// Reference image(s) for IP-Adapter (base64 encoded)
    /// </summary>
    [JsonPropertyName("reference_images")]
    public List<string> ReferenceImages { get; set; } = new();

    /// <summary>
    /// IP-Adapter scale (0.0-1.5, higher = more influence from reference)
    /// </summary>
    [JsonPropertyName("ip_adapter_scale")]
    public double IPAdapterScale { get; set; } = 0.6;

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonPropertyName("clip_skip")]
    public int ClipSkip { get; set; } = 1;

    [JsonPropertyName("lora_models")]
    public List<LoraModelInfo>? LoraModels { get; set; }
}

/// <summary>
/// IP-Adapter load result
/// </summary>
public class IPAdapterLoadResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("adapter")]
    public string? Adapter { get; set; }
}

/// <summary>
/// IP-Adapter generation result
/// </summary>
public class IPAdapterResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("ip_adapter_scale")]
    public double IPAdapterScale { get; set; }

    [JsonPropertyName("reference_count")]
    public int ReferenceCount { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }
}

/// <summary>
/// Multi-ControlNet request - use multiple control conditions simultaneously
/// </summary>
public class DiffusersMultiControlNetRequest
{
    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("negative_prompt")]
    public string? NegativePrompt { get; set; }

    /// <summary>
    /// List of control conditions
    /// </summary>
    [JsonPropertyName("controls")]
    public List<ControlCondition> Controls { get; set; } = new();

    [JsonPropertyName("width")]
    public int Width { get; set; } = 1024;

    [JsonPropertyName("height")]
    public int Height { get; set; } = 1024;

    [JsonPropertyName("steps")]
    public int Steps { get; set; } = 30;

    [JsonPropertyName("guidance_scale")]
    public double GuidanceScale { get; set; } = 7.5;

    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;

    [JsonPropertyName("scheduler")]
    public string? Scheduler { get; set; }

    [JsonPropertyName("lora_models")]
    public List<LoraModelInfo>? LoraModels { get; set; }
}

/// <summary>
/// A single control condition for Multi-ControlNet
/// </summary>
public class ControlCondition
{
    /// <summary>
    /// Control image (base64 encoded)
    /// </summary>
    [JsonPropertyName("control_image")]
    public string ControlImage { get; set; } = "";

    /// <summary>
    /// ControlNet type: canny, pose, depth, etc.
    /// </summary>
    [JsonPropertyName("control_type")]
    public string ControlType { get; set; } = "canny";

    /// <summary>
    /// Conditioning scale for this control (0.0-2.0)
    /// </summary>
    [JsonPropertyName("weight")]
    public double Weight { get; set; } = 1.0;

    /// <summary>
    /// Whether to preprocess the control image
    /// </summary>
    [JsonPropertyName("preprocess")]
    public bool Preprocess { get; set; } = true;

    [JsonPropertyName("canny_low")]
    public int CannyLow { get; set; } = 100;

    [JsonPropertyName("canny_high")]
    public int CannyHigh { get; set; } = 200;
}

/// <summary>
/// Multi-ControlNet generation result
/// </summary>
public class MultiControlNetResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("images")]
    public List<string>? Images { get; set; }

    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("control_types")]
    public List<string>? ControlTypes { get; set; }

    [JsonPropertyName("control_scales")]
    public List<double>? ControlScales { get; set; }

    [JsonPropertyName("vram_used_gb")]
    public double VramUsedGb { get; set; }
}

/// <summary>
/// Request to add a task to the generation queue
/// </summary>
public class QueuedTaskRequest
{
    /// <summary>
    /// Task type: image, img2img, video, controlnet, multi_controlnet, inpaint, outpaint, upscale, ip_adapter
    /// </summary>
    [JsonPropertyName("task_type")]
    public string TaskType { get; set; } = "";

    /// <summary>
    /// Request data (varies by task type)
    /// </summary>
    [JsonPropertyName("request_data")]
    public Dictionary<string, object> RequestData { get; set; } = new();

    /// <summary>
    /// Priority (0-10, higher = processed first)
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Result of adding a task to the queue
/// </summary>
public class QueueAddResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }
}

/// <summary>
/// Status of a queued task
/// </summary>
public class QueueTaskStatus
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    /// <summary>
    /// Status: pending, processing, completed, failed, cancelled
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }

    [JsonPropertyName("result")]
    public Dictionary<string, object>? Result { get; set; }

    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; }

    [JsonPropertyName("started_at")]
    public double? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public double? CompletedAt { get; set; }
}

/// <summary>
/// Queue list result
/// </summary>
public class QueueListResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("queue_running")]
    public bool QueueRunning { get; set; }

    [JsonPropertyName("pending_count")]
    public int PendingCount { get; set; }

    [JsonPropertyName("pending")]
    public List<QueueTaskInfo>? Pending { get; set; }

    [JsonPropertyName("processing")]
    public List<QueueTaskInfo>? Processing { get; set; }

    [JsonPropertyName("recent_completed")]
    public List<QueueTaskInfo>? RecentCompleted { get; set; }
}

/// <summary>
/// Basic task info for queue listing
/// </summary>
public class QueueTaskInfo
{
    [JsonPropertyName("task_id")]
    public string? TaskId { get; set; }

    [JsonPropertyName("task_type")]
    public string? TaskType { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("created_at")]
    public double CreatedAt { get; set; }
}

/// <summary>
/// Result of clearing the queue
/// </summary>
public class QueueClearResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("cleared_count")]
    public int ClearedCount { get; set; }
}

/// <summary>
/// GPU resource limits response from Python server
/// </summary>
public class GpuLimitsResponse
{
    [JsonPropertyName("limits")]
    public GpuLimitsData? Limits { get; set; }

    [JsonPropertyName("status")]
    public GpuStatusResponse? Status { get; set; }
}

/// <summary>
/// GPU limits configuration data
/// </summary>
public class GpuLimitsData
{
    [JsonPropertyName("max_vram_usage_percent")]
    public double MaxVramUsagePercent { get; set; }

    [JsonPropertyName("reserved_vram_gb")]
    public double ReservedVramGb { get; set; }

    [JsonPropertyName("oom_auto_recovery")]
    public bool OomAutoRecovery { get; set; }

    [JsonPropertyName("auto_reduce_batch_size")]
    public bool AutoReduceBatchSize { get; set; }

    [JsonPropertyName("monitor_interval_seconds")]
    public int MonitorIntervalSeconds { get; set; }
}

/// <summary>
/// GPU/VRAM status response
/// </summary>
public class GpuStatusResponse
{
    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("total_gb")]
    public double TotalGb { get; set; }

    [JsonPropertyName("allocated_gb")]
    public double AllocatedGb { get; set; }

    [JsonPropertyName("reserved_gb")]
    public double ReservedGb { get; set; }

    [JsonPropertyName("free_gb")]
    public double FreeGb { get; set; }

    [JsonPropertyName("usage_percent")]
    public double UsagePercent { get; set; }

    [JsonPropertyName("limit_percent")]
    public double LimitPercent { get; set; }

    [JsonPropertyName("max_usable_gb")]
    public double MaxUsableGb { get; set; }

    [JsonPropertyName("available_for_generation_gb")]
    public double AvailableForGenerationGb { get; set; }

    [JsonPropertyName("throttled")]
    public bool Throttled { get; set; }

    [JsonPropertyName("oom_count")]
    public int OomCount { get; set; }
}

#endregion
