using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Generation Engine using HuggingFace Diffusers directly (replaces ComfyUI)
/// This service manages a Python process running diffusers for image/video generation
/// </summary>
public class DiffusersGenerationEngine : IDisposable
{
    private readonly ILogger<DiffusersGenerationEngine>? _logger;
    private readonly HuggingFaceModelService _modelService;
    private readonly string _pythonPath;
    private readonly string _scriptsDir;
    private Process? _serverProcess;
    private HttpClient? _httpClient;
    private bool _isRunning;
    private bool _disposed;

    public const int DEFAULT_PORT = 5050;

    /// <summary>
    /// Event raised when engine status changes
    /// </summary>
    public event EventHandler<EngineStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Event raised when generation progress updates
    /// </summary>
    public event EventHandler<DiffusersProgressEventArgs>? ProgressChanged;

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

    public DiffusersGenerationEngine(
        HuggingFaceModelService modelService,
        ILogger<DiffusersGenerationEngine>? logger = null)
    {
        _modelService = modelService;
        _logger = logger;

        // Find Python path
        _pythonPath = FindPythonPath();

        // Setup scripts directory
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _scriptsDir = Path.Combine(appData, "PostXAgent", "diffusers_engine");
        Directory.CreateDirectory(_scriptsDir);
    }

    #region Engine Lifecycle

    /// <summary>
    /// Start the diffusers generation engine
    /// </summary>
    public async Task<bool> StartAsync(int port = DEFAULT_PORT, CancellationToken ct = default)
    {
        if (_isRunning)
        {
            _logger?.LogWarning("Engine already running");
            return true;
        }

        try
        {
            Port = port;

            // Ensure Python script exists
            await EnsureScriptExistsAsync();

            // Start Python process
            var scriptPath = Path.Combine(_scriptsDir, "generation_server.py");

            var psi = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{scriptPath}\" --port {port} --models-dir \"{_modelService.ModelsDirectory}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _scriptsDir
            };

            _serverProcess = new Process { StartInfo = psi };
            _serverProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger?.LogDebug("[Engine] {Output}", e.Data);
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
                return true;
            }

            // Failed to start
            await StopAsync();
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start diffusers engine");
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Error, ex.Message));
            return false;
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
    /// Load a model into VRAM
    /// </summary>
    public async Task<bool> LoadModelAsync(string modelId, ModelType type, CancellationToken ct = default)
    {
        if (!_isRunning)
        {
            _logger?.LogWarning("Engine not running");
            return false;
        }

        try
        {
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Loading, $"Loading {modelId}"));

            var request = new LoadModelRequest
            {
                ModelId = modelId,
                ModelType = type.ToString()
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient!.PostAsync($"http://localhost:{Port}/load-model", content, ct);
            response.EnsureSuccessStatusCode();

            CurrentModel = modelId;
            _modelService.MarkModelLoaded(modelId, new ModelInfo { Id = modelId, Type = type });

            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Running, $"Loaded: {modelId}"));
            _logger?.LogInformation("Model loaded: {ModelId}", modelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load model: {ModelId}", modelId);
            StatusChanged?.Invoke(this, new EngineStatusEventArgs(EngineStatus.Error, ex.Message));
            return false;
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

    #region Private Methods

    private string FindPythonPath()
    {
        // Check common Python paths
        var paths = new[]
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

        foreach (var path in paths)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                    {
                        return path;
                    }
                }
            }
            catch { }
        }

        return "python"; // Default
    }

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

#endregion
