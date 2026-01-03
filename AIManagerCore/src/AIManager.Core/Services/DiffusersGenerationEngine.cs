using System.Diagnostics;
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
public class DiffusersGenerationEngine : IDisposable
{
    private readonly ILogger<DiffusersGenerationEngine>? _logger;
    private readonly HuggingFaceModelService _modelService;
    private readonly LocalGpuService _gpuService;
    private readonly string _scriptsDir;
    private Process? _serverProcess;
    private HttpClient? _httpClient;
    private bool _isRunning;
    private bool _disposed;
    private GpuInfo? _cachedGpuInfo;
    private PythonEnvironmentInfo? _cachedPythonEnv;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    public const int DEFAULT_PORT = 5050;

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
    /// Event raised when GPU status changes
    /// </summary>
    public event EventHandler<LocalGpuStatusEventArgs>? GpuStatusChanged;

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

    public DiffusersGenerationEngine(
        HuggingFaceModelService modelService,
        LocalGpuService? gpuService = null,
        ILogger<DiffusersGenerationEngine>? logger = null)
    {
        _modelService = modelService;
        _gpuService = gpuService ?? new LocalGpuService();
        _logger = logger;

        // Setup scripts directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _scriptsDir = Path.Combine(appData, "PostXAgent", "diffusers_engine");
        Directory.CreateDirectory(_scriptsDir);
    }

    #region Engine Lifecycle

    /// <summary>
    /// Perform pre-flight checks before starting the engine
    /// </summary>
    public async Task<PreflightCheckResult> PerformPreflightChecksAsync(CancellationToken ct = default)
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

            // Check Python environment
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Starting, "Checking Python environment..."));
            _cachedPythonEnv = await _gpuService.CheckPythonEnvironmentAsync(ct);
            result.PythonEnv = _cachedPythonEnv;
            result.HasPython = _cachedPythonEnv.IsAvailable;
            result.PythonReady = _cachedPythonEnv.IsReady;

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

            if (!_cachedPythonEnv.HasCudaSupport && result.HasGpu && _cachedGpuInfo.Vendor == GpuVendor.Nvidia)
            {
                result.Warnings.Add("PyTorch doesn't have CUDA support. GPU acceleration unavailable.");
                result.InstallCommand = _gpuService.GetInstallCommand(
                    new PythonEnvironmentInfo { MissingPackages = new List<string> { "PyTorch" } },
                    _cachedGpuInfo);
            }

            _logger?.LogInformation("Preflight check complete. Ready: {Ready}, GPU: {Gpu}, Python: {Python}",
                result.IsReady, result.HasGpu, result.PythonReady);

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
                var preflightResult = await PerformPreflightChecksAsync(ct);
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

            // Start Python process
            var scriptPath = Path.Combine(_scriptsDir, "generation_server.py");
            var pythonPath = _cachedPythonEnv?.PythonPath ?? "python";

            var psi = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\" --port {port} --models-dir \"{_modelService.ModelsDirectory}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _scriptsDir
            };

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
                    ParseEngineOutput(e.Data);
                }
            };
            _serverProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger?.LogWarning("[Engine Error] {Output}", e.Data);
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

    private async Task<bool> WaitForServerReadyAsync(CancellationToken ct)
    {
        var maxAttempts = 30; // 30 seconds
        for (int i = 0; i < maxAttempts; i++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var response = await _httpClient!.GetAsync($"http://localhost:{Port}/health", ct);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(1000, ct);
        }

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

            var request = new LoadModelRequest
            {
                ModelId = modelId,
                ModelType = type.ToString()
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient!.PostAsync($"http://localhost:{Port}/load-model", content, ct);

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
            await _httpClient!.PostAsync($"http://localhost:{Port}/unload-model", null, ct);
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
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Generating, "Generating image..."));

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient!.PostAsync($"http://localhost:{Port}/generate/image", content, ct);
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

            var response = await _httpClient!.PostAsync($"http://localhost:{Port}/generate/video", content, ct);
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
            var response = await _httpClient!.GetAsync($"http://localhost:{Port}/info", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EngineInfo>(ct);
        }
        catch
        {
            return null;
        }
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

    private static string GetGenerationServerScript() => """
#!/usr/bin/env python3
"""
+ "\"\"\"" + """
PostXAgent Diffusers Generation Server
A lightweight HTTP server for image/video generation using HuggingFace Diffusers
""" + "\"\"\"" + """

import argparse
import base64
import gc
import io
import json
import os
import sys
import threading
import time
import traceback
from http.server import HTTPServer, BaseHTTPRequestHandler

# Try to import torch and diffusers
try:
    import torch
    from diffusers import (
        StableDiffusionXLPipeline,
        StableDiffusionPipeline,
        DiffusionPipeline,
        StableVideoDiffusionPipeline,
        AutoPipelineForText2Image,
    )
    HAS_DIFFUSERS = True
except ImportError:
    HAS_DIFFUSERS = False
    print("WARNING: diffusers not installed. Run: pip install diffusers transformers accelerate")

try:
    from PIL import Image
    HAS_PIL = True
except ImportError:
    HAS_PIL = False


class GenerationServer:
    def __init__(self, models_dir: str):
        self.models_dir = models_dir
        self.current_model = None
        self.pipeline = None
        self.device = "cuda" if torch.cuda.is_available() else "cpu"
        self.dtype = torch.float16 if self.device == "cuda" else torch.float32

        print(f"Device: {self.device}")
        print(f"Models directory: {models_dir}")

        if self.device == "cuda":
            print(f"GPU: {torch.cuda.get_device_name(0)}")
            print(f"VRAM: {torch.cuda.get_device_properties(0).total_memory / 1024**3:.1f} GB")

    def load_model(self, model_id: str, model_type: str) -> dict:
        try:
            self.unload_model()
            print(f"Loading model: {model_id}")

            local_path = os.path.join(self.models_dir, "checkpoints", model_id.replace("/", "--"))
            use_local = os.path.exists(local_path)
            model_path = local_path if use_local else model_id

            if model_type in ["TextToImage", "ImageToImage"]:
                if "xl" in model_id.lower() or "sdxl" in model_id.lower():
                    self.pipeline = StableDiffusionXLPipeline.from_pretrained(
                        model_path, torch_dtype=self.dtype, use_safetensors=True, local_files_only=use_local)
                elif "flux" in model_id.lower():
                    self.pipeline = DiffusionPipeline.from_pretrained(
                        model_path, torch_dtype=self.dtype, local_files_only=use_local)
                else:
                    self.pipeline = AutoPipelineForText2Image.from_pretrained(
                        model_path, torch_dtype=self.dtype, use_safetensors=True, local_files_only=use_local)
            elif model_type in ["TextToVideo", "ImageToVideo"]:
                self.pipeline = StableVideoDiffusionPipeline.from_pretrained(
                    model_path, torch_dtype=self.dtype, local_files_only=use_local)
            else:
                self.pipeline = AutoPipelineForText2Image.from_pretrained(
                    model_path, torch_dtype=self.dtype, local_files_only=use_local)

            self.pipeline = self.pipeline.to(self.device)
            if hasattr(self.pipeline, "enable_attention_slicing"):
                self.pipeline.enable_attention_slicing()
            if hasattr(self.pipeline, "enable_vae_slicing"):
                self.pipeline.enable_vae_slicing()

            self.current_model = model_id
            print(f"Model loaded: {model_id}")
            return {"success": True, "model": model_id}
        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e)}

    def unload_model(self):
        if self.pipeline is not None:
            del self.pipeline
            self.pipeline = None
            self.current_model = None
            gc.collect()
            if torch.cuda.is_available():
                torch.cuda.empty_cache()
            print("Model unloaded")

    def generate_image(self, params: dict) -> dict:
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}
        try:
            prompt = params.get("prompt", "")
            negative_prompt = params.get("negative_prompt", "")
            width = params.get("width", 1024)
            height = params.get("height", 1024)
            steps = params.get("steps", 30)
            guidance = params.get("guidance_scale", 7.5)
            seed = params.get("seed", -1)
            batch_size = params.get("batch_size", 1)

            if seed >= 0:
                generator = torch.Generator(device=self.device).manual_seed(seed)
            else:
                seed = torch.randint(0, 2**32, (1,)).item()
                generator = torch.Generator(device=self.device).manual_seed(seed)

            start_time = time.time()
            result = self.pipeline(
                prompt=prompt,
                negative_prompt=negative_prompt if negative_prompt else None,
                width=width, height=height, num_inference_steps=steps,
                guidance_scale=guidance, num_images_per_prompt=batch_size, generator=generator)

            gen_time = time.time() - start_time
            images = []
            for img in result.images:
                buffer = io.BytesIO()
                img.save(buffer, format="PNG")
                b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                images.append(f"data:image/png;base64,{b64}")

            return {"success": True, "images": images, "seed": seed, "generation_time": gen_time}
        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e)}

    def generate_video(self, params: dict) -> dict:
        if self.pipeline is None:
            return {"success": False, "error": "No model loaded"}
        try:
            image_b64 = params.get("image")
            num_frames = params.get("num_frames", 25)
            fps = params.get("fps", 7)
            seed = params.get("seed", -1)

            if seed >= 0:
                generator = torch.Generator(device=self.device).manual_seed(seed)
            else:
                seed = torch.randint(0, 2**32, (1,)).item()
                generator = torch.Generator(device=self.device).manual_seed(seed)

            start_time = time.time()
            input_image = None
            if image_b64:
                if image_b64.startswith("data:"):
                    image_b64 = image_b64.split(",")[1]
                image_data = base64.b64decode(image_b64)
                input_image = Image.open(io.BytesIO(image_data))
                input_image = input_image.resize((1024, 576))

            if input_image:
                frames = self.pipeline(input_image, num_frames=num_frames, generator=generator).frames[0]
            else:
                return {"success": False, "error": "Text-to-video not supported yet"}

            gen_time = time.time() - start_time
            frame_images = []
            for frame in frames:
                buffer = io.BytesIO()
                frame.save(buffer, format="PNG")
                b64 = base64.b64encode(buffer.getvalue()).decode("utf-8")
                frame_images.append(f"data:image/png;base64,{b64}")

            return {"success": True, "frames": frame_images, "fps": fps, "seed": seed, "generation_time": gen_time}
        except Exception as e:
            traceback.print_exc()
            return {"success": False, "error": str(e)}

    def get_info(self) -> dict:
        gpu_info = {}
        if torch.cuda.is_available():
            gpu_info = {
                "name": torch.cuda.get_device_name(0),
                "total_memory_gb": torch.cuda.get_device_properties(0).total_memory / 1024**3,
                "free_memory_gb": (torch.cuda.get_device_properties(0).total_memory - torch.cuda.memory_allocated(0)) / 1024**3,
            }
        return {"status": "ready", "device": self.device, "current_model": self.current_model, "gpu": gpu_info, "has_diffusers": HAS_DIFFUSERS}


class RequestHandler(BaseHTTPRequestHandler):
    server_instance = None

    def log_message(self, format, *args):
        print(f"[HTTP] {args[0]}")

    def send_json(self, data: dict, status: int = 200):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(json.dumps(data).encode("utf-8"))

    def do_GET(self):
        if self.path == "/health":
            self.send_json({"status": "ok"})
        elif self.path == "/info":
            self.send_json(self.server_instance.engine.get_info())
        else:
            self.send_json({"error": "Not found"}, 404)

    def do_POST(self):
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length).decode("utf-8") if content_length > 0 else "{}"
        try:
            data = json.loads(body) if body else {}
        except:
            data = {}

        if self.path == "/load-model":
            result = self.server_instance.engine.load_model(data.get("model_id", ""), data.get("model_type", "TextToImage"))
            self.send_json(result)
        elif self.path == "/unload-model":
            self.server_instance.engine.unload_model()
            self.send_json({"success": True})
        elif self.path == "/generate/image":
            self.send_json(self.server_instance.engine.generate_image(data))
        elif self.path == "/generate/video":
            self.send_json(self.server_instance.engine.generate_video(data))
        elif self.path == "/shutdown":
            self.send_json({"status": "shutting down"})
            threading.Thread(target=self.server_instance.shutdown).start()
        else:
            self.send_json({"error": "Not found"}, 404)

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()


class GenerationHTTPServer(HTTPServer):
    def __init__(self, port: int, models_dir: str):
        self.engine = GenerationServer(models_dir)
        RequestHandler.server_instance = self
        super().__init__(("0.0.0.0", port), RequestHandler)


def main():
    parser = argparse.ArgumentParser(description="PostXAgent Diffusers Generation Server")
    parser.add_argument("--port", type=int, default=5050, help="Server port")
    parser.add_argument("--models-dir", type=str, required=True, help="Models directory")
    args = parser.parse_args()

    if not HAS_DIFFUSERS:
        print("ERROR: diffusers is required. Install with: pip install diffusers transformers accelerate")
        sys.exit(1)

    print(f"Starting generation server on port {args.port}...")
    server = GenerationHTTPServer(args.port, args.models_dir)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("Shutting down...")
        server.shutdown()


if __name__ == "__main__":
    main()
""";

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAsync().Wait();
    }
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
    public int Seed { get; set; } = -1;

    [JsonPropertyName("batch_size")]
    public int BatchSize { get; set; } = 1;
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

    [JsonPropertyName("seed")]
    public int Seed { get; set; } = -1;
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

#endregion
