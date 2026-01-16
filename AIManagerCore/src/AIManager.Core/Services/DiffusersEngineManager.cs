using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Singleton manager for DiffusersGenerationEngine
/// ทำให้ระบบทำงานเร็วเหมือน ComfyUI:
/// - Server ทำงานตลอด ไม่ต้อง restart
/// - Model อยู่ใน VRAM ไม่ต้องโหลดใหม่
/// - Auto-start เมื่อเปิด app
/// </summary>
public sealed class DiffusersEngineManager : IDisposable
{
    private static DiffusersEngineManager? _instance;
    private static readonly object _lock = new();

    private readonly DiffusersGenerationEngine _engine;
    private readonly ILogger<DiffusersEngineManager>? _logger;
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    /// Get the singleton instance
    /// </summary>
    public static DiffusersEngineManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new DiffusersEngineManager();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Get the engine instance
    /// </summary>
    public DiffusersGenerationEngine Engine => _engine;

    /// <summary>
    /// Whether the engine is running and ready
    /// </summary>
    public bool IsReady => _engine.IsRunning;

    /// <summary>
    /// Current loaded model ID
    /// </summary>
    public string? CurrentModel => _engine.CurrentModel;

    /// <summary>
    /// Get the process ID of the Python server (if running)
    /// </summary>
    public int? ProcessId => _engine.ServerProcessId;

    /// <summary>
    /// Event when engine status changes
    /// </summary>
    public event EventHandler<EngineStatusEventArgs>? StatusChanged;

    /// <summary>
    /// Event when model load progress changes
    /// </summary>
    public event EventHandler<ModelLoadProgressEventArgs>? ModelLoadProgressChanged;

    /// <summary>
    /// Event when a log message is generated
    /// </summary>
    public event EventHandler<string>? LogMessage;

    private DiffusersEngineManager(ILogger<DiffusersEngineManager>? logger = null)
    {
        _logger = logger;

        var modelService = new HuggingFaceModelService();
        var gpuService = new LocalGpuService();

        _engine = new DiffusersGenerationEngine(modelService, gpuService, logger: null);

        // Forward events
        _engine.StatusChanged += (s, e) => StatusChanged?.Invoke(this, e);
        _engine.ModelLoadProgressChanged += (s, e) => ModelLoadProgressChanged?.Invoke(this, e);

        _logger?.LogInformation("DiffusersEngineManager created");
    }

    /// <summary>
    /// Initialize and start the engine (call once on app startup)
    /// </summary>
    public async Task<bool> InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized && _engine.IsRunning)
        {
            _logger?.LogInformation("Engine already initialized and running");
            return true;
        }

        try
        {
            _logger?.LogInformation("Initializing DiffusersEngine...");

            // Check if server is already running (from previous session)
            if (await CheckServerAliveAsync(ct))
            {
                _logger?.LogInformation("Server already running from previous session");
                _isInitialized = true;
                return true;
            }

            // Start the engine
            var result = await _engine.StartAsync(ct: ct);

            if (result.Success)
            {
                _isInitialized = true;
                _logger?.LogInformation("DiffusersEngine initialized successfully");
                return true;
            }

            _logger?.LogWarning("Failed to initialize DiffusersEngine: {Message}", result.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing DiffusersEngine");
            return false;
        }
    }

    /// <summary>
    /// Ensure engine is running before operations
    /// </summary>
    public async Task<bool> EnsureRunningAsync(CancellationToken ct = default)
    {
        if (_engine.IsRunning)
        {
            return true;
        }

        return await InitializeAsync(ct);
    }

    /// <summary>
    /// Load a model into VRAM (cached - won't reload if same model)
    /// </summary>
    public async Task<ModelLoadResult> LoadModelAsync(string modelId, ModelType type, CancellationToken ct = default)
    {
        // Ensure engine is running
        if (!await EnsureRunningAsync(ct))
        {
            return new ModelLoadResult
            {
                Success = false,
                Error = "Failed to start engine"
            };
        }

        // Check if model is already loaded
        if (_engine.CurrentModel == modelId)
        {
            _logger?.LogInformation("Model already loaded: {ModelId}", modelId);
            return new ModelLoadResult
            {
                Success = true,
                ModelId = modelId
            };
        }

        // Load the model
        return await _engine.LoadModelAsync(modelId, type, ct: ct);
    }

    /// <summary>
    /// Generate an image
    /// </summary>
    public async Task<DiffusersResult> GenerateImageAsync(DiffusersImageRequest request, CancellationToken ct = default)
    {
        if (!await EnsureRunningAsync(ct))
        {
            return new DiffusersResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        return await _engine.GenerateImageAsync(request, ct);
    }

    /// <summary>
    /// Generate a video
    /// </summary>
    public async Task<DiffusersResult> GenerateVideoAsync(DiffusersVideoRequest request, CancellationToken ct = default)
    {
        if (!await EnsureRunningAsync(ct))
        {
            return new DiffusersResult
            {
                Success = false,
                Error = "Engine not running"
            };
        }

        return await _engine.GenerateVideoAsync(request, ct);
    }

    /// <summary>
    /// Check if the Python server is alive
    /// </summary>
    private async Task<bool> CheckServerAliveAsync(CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await client.GetAsync($"http://localhost:{DiffusersGenerationEngine.DEFAULT_PORT}/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get engine info
    /// </summary>
    public async Task<EngineInfo?> GetEngineInfoAsync(CancellationToken ct = default)
    {
        if (!_engine.IsRunning) return null;
        return await _engine.GetEngineInfoAsync(ct);
    }

    /// <summary>
    /// Shutdown the engine (call on app exit)
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_engine.IsRunning)
        {
            await _engine.StopAsync();
            _isInitialized = false;
            _logger?.LogInformation("DiffusersEngine shutdown complete");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _engine.Dispose();
        _instance = null;
    }
}
