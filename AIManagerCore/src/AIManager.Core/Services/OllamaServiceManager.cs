using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// จัดการ Ollama Service แบบ Embedded
/// - Auto-start เมื่อเปิด app
/// - Auto-restart ถ้า crash
/// - Health monitoring
/// - Graceful shutdown
/// </summary>
public class OllamaServiceManager : IDisposable
{
    private readonly ILogger<OllamaServiceManager>? _logger;
    private readonly HttpClient _httpClient;
    private Process? _ollamaProcess;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;
    private bool _isDisposed;

    public event EventHandler<OllamaStatus>? StatusChanged;

    /// <summary>
    /// Ollama executable path
    /// </summary>
    public string OllamaPath { get; set; }

    /// <summary>
    /// API URL
    /// </summary>
    public string ApiUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// สถานะปัจจุบัน
    /// </summary>
    public OllamaStatus CurrentStatus { get; private set; } = OllamaStatus.Stopped;

    /// <summary>
    /// Model ที่โหลดอยู่
    /// </summary>
    public List<string> LoadedModels { get; private set; } = new();

    public OllamaServiceManager(ILogger<OllamaServiceManager>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        // Auto-detect Ollama path
        OllamaPath = FindOllamaPath();
    }

    /// <summary>
    /// ค้นหา Ollama executable
    /// </summary>
    private string FindOllamaPath()
    {
        var possiblePaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Ollama", "ollama.exe"),
            @"C:\Program Files\Ollama\ollama.exe",
            @"C:\Program Files (x86)\Ollama\ollama.exe",
            // Linux/Mac
            "/usr/local/bin/ollama",
            "/usr/bin/ollama"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logger?.LogInformation("Found Ollama at: {Path}", path);
                return path;
            }
        }

        // Try from PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var ollamaPath = Path.Combine(dir, "ollama.exe");
            if (File.Exists(ollamaPath))
            {
                _logger?.LogInformation("Found Ollama in PATH: {Path}", ollamaPath);
                return ollamaPath;
            }
        }

        _logger?.LogWarning("Ollama not found");
        return "ollama"; // Fallback
    }

    /// <summary>
    /// เริ่ม Ollama Service
    /// </summary>
    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (CurrentStatus == OllamaStatus.Running)
        {
            _logger?.LogInformation("Ollama already running");
            return true;
        }

        // Check if already running externally
        if (await IsRunningAsync(ct))
        {
            _logger?.LogInformation("Ollama already running externally");
            SetStatus(OllamaStatus.Running);
            await RefreshModelsAsync(ct);
            StartMonitoring();
            return true;
        }

        if (!File.Exists(OllamaPath) && OllamaPath != "ollama")
        {
            _logger?.LogError("Ollama not found at: {Path}", OllamaPath);
            SetStatus(OllamaStatus.NotInstalled);
            return false;
        }

        SetStatus(OllamaStatus.Starting);

        try
        {
            _ollamaProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OllamaPath,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(OllamaPath) ?? Environment.CurrentDirectory
                },
                EnableRaisingEvents = true
            };

            _ollamaProcess.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger?.LogDebug("[Ollama] {Output}", e.Data);
            };

            _ollamaProcess.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger?.LogWarning("[Ollama Error] {Error}", e.Data);
            };

            _ollamaProcess.Exited += (s, e) =>
            {
                _logger?.LogWarning("Ollama process exited");
                if (CurrentStatus != OllamaStatus.Stopping)
                {
                    SetStatus(OllamaStatus.Crashed);
                }
            };

            _ollamaProcess.Start();
            _ollamaProcess.BeginOutputReadLine();
            _ollamaProcess.BeginErrorReadLine();

            _logger?.LogInformation("Started Ollama process (PID: {PID})", _ollamaProcess.Id);

            // Wait for API to be ready
            for (var i = 0; i < 30; i++)
            {
                await Task.Delay(500, ct);
                if (await IsRunningAsync(ct))
                {
                    SetStatus(OllamaStatus.Running);
                    await RefreshModelsAsync(ct);
                    StartMonitoring();
                    return true;
                }
            }

            _logger?.LogError("Ollama failed to start within timeout");
            SetStatus(OllamaStatus.Error);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start Ollama");
            SetStatus(OllamaStatus.Error);
            return false;
        }
    }

    /// <summary>
    /// หยุด Ollama Service
    /// </summary>
    public async Task StopAsync()
    {
        SetStatus(OllamaStatus.Stopping);

        _monitorCts?.Cancel();
        if (_monitorTask != null)
        {
            try { await _monitorTask; } catch { }
        }

        if (_ollamaProcess != null && !_ollamaProcess.HasExited)
        {
            try
            {
                _ollamaProcess.Kill(entireProcessTree: true);
                await _ollamaProcess.WaitForExitAsync();
                _logger?.LogInformation("Ollama stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping Ollama");
            }
        }

        _ollamaProcess?.Dispose();
        _ollamaProcess = null;
        SetStatus(OllamaStatus.Stopped);
    }

    /// <summary>
    /// ตรวจสอบว่า Ollama API พร้อมใช้งาน
    /// </summary>
    public async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiUrl}/api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ดึงรายชื่อ model ที่มี
    /// </summary>
    public async Task RefreshModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{ApiUrl}/api/tags", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(ct);
                LoadedModels = json?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
                _logger?.LogInformation("Found {Count} models: {Models}",
                    LoadedModels.Count, string.Join(", ", LoadedModels));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get models");
        }
    }

    /// <summary>
    /// Pull model ใหม่
    /// </summary>
    public async Task<bool> PullModelAsync(string modelName, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        _logger?.LogInformation("Pulling model: {Model}", modelName);

        try
        {
            var request = new { name = modelName, stream = true };
            var response = await _httpClient.PostAsJsonAsync($"{ApiUrl}/api/pull", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogError("Failed to pull model: {Status}", response.StatusCode);
                return false;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;

                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(line);
                    if (json.RootElement.TryGetProperty("completed", out var completed) &&
                        json.RootElement.TryGetProperty("total", out var total))
                    {
                        var pct = (double)completed.GetInt64() / total.GetInt64();
                        progress?.Report(pct);
                    }

                    if (json.RootElement.TryGetProperty("status", out var status))
                    {
                        _logger?.LogDebug("Pull status: {Status}", status.GetString());
                    }
                }
                catch { }
            }

            await RefreshModelsAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to pull model");
            return false;
        }
    }

    /// <summary>
    /// เริ่ม monitoring
    /// </summary>
    private void StartMonitoring()
    {
        _monitorCts = new CancellationTokenSource();
        _monitorTask = MonitorAsync(_monitorCts.Token);
    }

    /// <summary>
    /// Monitor และ auto-restart
    /// </summary>
    private async Task MonitorAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(5000, ct); // Check every 5 seconds

                if (!await IsRunningAsync(ct))
                {
                    if (CurrentStatus == OllamaStatus.Running)
                    {
                        _logger?.LogWarning("Ollama stopped unexpectedly, restarting...");
                        SetStatus(OllamaStatus.Crashed);
                        await Task.Delay(2000, ct);
                        await StartAsync(ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Monitor error");
            }
        }
    }

    private void SetStatus(OllamaStatus status)
    {
        if (CurrentStatus != status)
        {
            CurrentStatus = status;
            StatusChanged?.Invoke(this, status);
            _logger?.LogInformation("Ollama status: {Status}", status);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _monitorCts?.Cancel();
        _ollamaProcess?.Kill();
        _ollamaProcess?.Dispose();
        _httpClient.Dispose();
    }
}

/// <summary>
/// สถานะ Ollama Service
/// </summary>
public enum OllamaStatus
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Crashed,
    Error,
    NotInstalled
}

// Response models
internal class OllamaTagsResponse
{
    public List<OllamaModel>? Models { get; set; }
}

internal class OllamaModel
{
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public long Size { get; set; }
}
