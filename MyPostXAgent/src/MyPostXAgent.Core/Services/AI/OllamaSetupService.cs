using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MyPostXAgent.Core.Services.AI;

/// <summary>
/// Service for Ollama installation, setup, and management
/// ตรวจสอบและติดตั้ง Ollama อัตโนมัติเมื่อเครื่องยังไม่มี
/// </summary>
public class OllamaSetupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaSetupService>? _logger;
    private readonly string _baseUrl;

    private const string OLLAMA_SETUP_URL = "https://ollama.com/download/OllamaSetup.exe";

    public OllamaSetupService(
        HttpClient? httpClient = null,
        string baseUrl = "http://localhost:11434",
        ILogger<OllamaSetupService>? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
    }

    /// <summary>
    /// Check if Ollama is installed on the system
    /// </summary>
    public static bool IsOllamaInstalled()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows))
            return false;

        var defaultPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Ollama", "ollama.exe"),
            @"C:\Program Files\Ollama\ollama.exe"
        };

        foreach (var path in defaultPaths)
        {
            if (File.Exists(path))
                return true;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var path in pathEnv.Split(';'))
        {
            try
            {
                var ollamaPath = Path.Combine(path, "ollama.exe");
                if (File.Exists(ollamaPath))
                    return true;
            }
            catch { /* Ignore invalid paths */ }
        }

        return false;
    }

    /// <summary>
    /// Check if Ollama service is running
    /// </summary>
    public async Task<bool> IsOllamaRunningAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(3));
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get list of installed models
    /// </summary>
    public async Task<List<string>> GetInstalledModelsAsync(CancellationToken ct = default)
    {
        var models = new List<string>();
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", ct);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out var nameElement))
                        {
                            var name = nameElement.GetString();
                            if (!string.IsNullOrEmpty(name))
                                models.Add(name);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting installed models");
        }
        return models;
    }

    /// <summary>
    /// Full status check
    /// </summary>
    public async Task<OllamaStatus> CheckFullStatusAsync(CancellationToken ct = default)
    {
        var status = new OllamaStatus
        {
            IsInstalled = IsOllamaInstalled()
        };

        if (!status.IsInstalled)
        {
            status.Message = "Ollama ยังไม่ได้ติดตั้ง";
            status.MessageThai = "Ollama ยังไม่ได้ติดตั้ง";
            return status;
        }

        status.IsRunning = await IsOllamaRunningAsync(ct);

        if (!status.IsRunning)
        {
            status.Message = "Ollama ติดตั้งแล้วแต่ยังไม่ทำงาน";
            status.MessageThai = "Ollama ติดตั้งแล้วแต่ยังไม่ทำงาน";
            return status;
        }

        status.InstalledModels = await GetInstalledModelsAsync(ct);
        status.HasModels = status.InstalledModels.Count > 0;
        status.Message = status.HasModels
            ? $"พร้อมใช้งาน ({status.InstalledModels.Count} models)"
            : "ไม่มี model ติดตั้ง";
        status.MessageThai = status.Message;

        return status;
    }

    /// <summary>
    /// Download and install Ollama
    /// </summary>
    public async Task<OllamaSetupResult> InstallOllamaAsync(
        IProgress<OllamaSetupProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new OllamaSetupResult();

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "MyPostXAgent", "OllamaInstall");
            Directory.CreateDirectory(tempDir);
            var installerPath = Path.Combine(tempDir, "OllamaSetup.exe");

            progress?.Report(new OllamaSetupProgress("กำลังดาวน์โหลด Ollama...", 0));
            _logger?.LogInformation("Downloading Ollama from {Url}", OLLAMA_SETUP_URL);

            using (var response = await _httpClient.GetAsync(OLLAMA_SETUP_URL,
                HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                var downloadedBytes = 0L;

                using var fileStream = new FileStream(installerPath, FileMode.Create, FileAccess.Write);
                using var downloadStream = await response.Content.ReadAsStreamAsync(ct);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await downloadStream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        var percent = (int)((downloadedBytes * 50) / totalBytes);
                        var mb = downloadedBytes / 1024 / 1024;
                        var totalMb = totalBytes / 1024 / 1024;
                        progress?.Report(new OllamaSetupProgress(
                            $"กำลังดาวน์โหลด... {mb}MB / {totalMb}MB", percent));
                    }
                }
            }

            progress?.Report(new OllamaSetupProgress("กำลังติดตั้ง Ollama...", 55));
            _logger?.LogInformation("Running Ollama installer: {Path}", installerPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/S",
                UseShellExecute = true,
                Verb = "runas"
            };

            using var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync(ct);
            }

            progress?.Report(new OllamaSetupProgress("กำลังตรวจสอบการติดตั้ง...", 80));

            for (int i = 0; i < 30; i++)
            {
                await Task.Delay(1000, ct);
                if (await IsOllamaRunningAsync(ct) || IsOllamaInstalled())
                {
                    result.Success = true;
                    result.Message = "ติดตั้ง Ollama สำเร็จ!";
                    progress?.Report(new OllamaSetupProgress(result.Message, 100));

                    try { File.Delete(installerPath); } catch { }
                    return result;
                }
            }

            await StartOllamaServiceAsync(ct);
            await Task.Delay(3000, ct);

            result.Success = IsOllamaInstalled();
            result.Message = result.Success ? "ติดตั้งสำเร็จ กรุณารอสักครู่" : "ติดตั้งไม่สำเร็จ";
            progress?.Report(new OllamaSetupProgress(result.Message, 100));
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error installing Ollama");
            result.Success = false;
            result.Message = $"เกิดข้อผิดพลาด: {ex.Message}";
            progress?.Report(new OllamaSetupProgress(result.Message, 0));
            return result;
        }
    }

    /// <summary>
    /// Start Ollama service
    /// </summary>
    public async Task<bool> StartOllamaServiceAsync(CancellationToken ct = default)
    {
        var ollamaPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Ollama", "ollama.exe"),
            @"C:\Program Files\Ollama\ollama.exe",
            "ollama"
        };

        foreach (var path in ollamaPaths)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                Process.Start(startInfo);
                _logger?.LogInformation("Started Ollama service from {Path}", path);
                await Task.Delay(2000, ct);

                if (await IsOllamaRunningAsync(ct))
                    return true;
            }
            catch { continue; }
        }

        return false;
    }

    /// <summary>
    /// Pull a model
    /// </summary>
    public async Task<bool> PullModelAsync(
        string modelName,
        IProgress<OllamaSetupProgress>? progress = null,
        CancellationToken ct = default)
    {
        try
        {
            progress?.Report(new OllamaSetupProgress($"กำลังดาวน์โหลด model {modelName}...", 10));
            _logger?.LogInformation("Pulling model: {Model}", modelName);

            var request = new { name = modelName, stream = false };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(30));

            var response = await _httpClient.PostAsync($"{_baseUrl}/api/pull", content, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                progress?.Report(new OllamaSetupProgress($"ดาวน์โหลด model {modelName} สำเร็จ!", 100));
                _logger?.LogInformation("Model {Model} pulled successfully", modelName);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error pulling model {Model}", modelName);
            progress?.Report(new OllamaSetupProgress($"เกิดข้อผิดพลาด: {ex.Message}", 0));
            return false;
        }
    }

    /// <summary>
    /// Full setup - install Ollama and default model
    /// </summary>
    public async Task<OllamaSetupResult> SetupOllamaAsync(
        string defaultModel = "llama3.2:3b",
        IProgress<OllamaSetupProgress>? progress = null,
        CancellationToken ct = default)
    {
        var result = new OllamaSetupResult();

        // 1. Install if not present
        if (!IsOllamaInstalled())
        {
            var installResult = await InstallOllamaAsync(progress, ct);
            if (!installResult.Success)
                return installResult;
        }

        // 2. Start service if not running
        if (!await IsOllamaRunningAsync(ct))
        {
            progress?.Report(new OllamaSetupProgress("กำลังเริ่ม Ollama service...", 60));
            await StartOllamaServiceAsync(ct);
            await Task.Delay(3000, ct);
        }

        // 3. Pull model if needed
        var models = await GetInstalledModelsAsync(ct);
        if (models.Count == 0)
        {
            progress?.Report(new OllamaSetupProgress($"กำลังดาวน์โหลด model {defaultModel}...", 70));
            await PullModelAsync(defaultModel, progress, ct);
        }

        // 4. Final check
        result.Success = await IsOllamaRunningAsync(ct);
        result.Message = result.Success ? "Ollama พร้อมใช้งาน!" : "การติดตั้งยังไม่สมบูรณ์";
        progress?.Report(new OllamaSetupProgress(result.Message, result.Success ? 100 : 0));
        return result;
    }
}

/// <summary>
/// Ollama status
/// </summary>
public class OllamaStatus
{
    public bool IsInstalled { get; set; }
    public bool IsRunning { get; set; }
    public bool HasModels { get; set; }
    public List<string> InstalledModels { get; set; } = new();
    public string Message { get; set; } = "";
    public string MessageThai { get; set; } = "";
    public bool IsReady => IsInstalled && IsRunning && HasModels;
}

/// <summary>
/// Setup result
/// </summary>
public class OllamaSetupResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}

/// <summary>
/// Setup progress
/// </summary>
public class OllamaSetupProgress
{
    public string Message { get; }
    public int Percent { get; }

    public OllamaSetupProgress(string message, int percent)
    {
        Message = message;
        Percent = percent;
    }
}
