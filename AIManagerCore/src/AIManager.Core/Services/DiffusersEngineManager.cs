using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Recommended models that work without HuggingFace login
/// These are public models with no gating requirements
/// </summary>
public static class RecommendedModels
{
    /// <summary>
    /// Default model - SDXL Base (works without HF token, high quality)
    /// </summary>
    public const string DefaultModel = "stabilityai/stable-diffusion-xl-base-1.0";

    /// <summary>
    /// SD 1.5 - Smaller, faster, works on 4GB VRAM
    /// </summary>
    public const string SD15 = "runwayml/stable-diffusion-v1-5";

    /// <summary>
    /// SD 2.1 - Improved quality over 1.5
    /// </summary>
    public const string SD21 = "stabilityai/stable-diffusion-2-1";

    /// <summary>
    /// SDXL Base - High quality, needs 8GB+ VRAM
    /// </summary>
    public const string SDXLBase = "stabilityai/stable-diffusion-xl-base-1.0";

    /// <summary>
    /// SDXL Turbo - Fast generation (4 steps)
    /// </summary>
    public const string SDXLTurbo = "stabilityai/sdxl-turbo";

    /// <summary>
    /// Stable Video Diffusion - Video generation
    /// </summary>
    public const string SVD = "stabilityai/stable-video-diffusion-img2vid-xt";

    /// <summary>
    /// Get all recommended text-to-image models
    /// </summary>
    public static IReadOnlyList<(string Id, string Name, string Description, int MinVramGb)> TextToImageModels => new[]
    {
        (SD15, "SD 1.5", "Fast, works on 4GB VRAM", 4),
        (SD21, "SD 2.1", "Improved quality", 6),
        (SDXLBase, "SDXL Base", "High quality, needs 8GB+ VRAM (Recommended)", 8),
        (SDXLTurbo, "SDXL Turbo", "Fast generation (4 steps)", 8),
    };

    /// <summary>
    /// Models that require HuggingFace token (gated models)
    /// </summary>
    public static IReadOnlyList<string> GatedModels => new[]
    {
        "black-forest-labs/FLUX.1-dev",
        "black-forest-labs/FLUX.1-schnell",
        "meta-llama/",  // All Llama models
    };

    /// <summary>
    /// Check if a model requires HuggingFace token
    /// </summary>
    public static bool RequiresHuggingFaceToken(string modelId)
    {
        if (string.IsNullOrEmpty(modelId)) return false;

        return GatedModels.Any(gated =>
            modelId.StartsWith(gated, StringComparison.OrdinalIgnoreCase) ||
            modelId.Equals(gated, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Settings for DiffusersEngineManager - persisted to disk
/// </summary>
public class DiffusersEngineSettings
{
    public string? DefaultModelId { get; set; }
    public ModelType DefaultModelType { get; set; } = ModelType.TextToImage;
    public bool AutoLoadModelOnStart { get; set; } = true;
    public int ServerPort { get; set; } = 5050;
    public bool LowVramMode { get; set; } = false;
    public bool SkipVramCheck { get; set; } = true;  // Skip VRAM check by default - let PyTorch handle it

    /// <summary>
    /// Last selected model in Model Manager (auto-saved when user selects)
    /// </summary>
    public string? LastSelectedModelId { get; set; }

    /// <summary>
    /// Last selected model type
    /// </summary>
    public ModelType LastSelectedModelType { get; set; } = ModelType.TextToImage;

    /// <summary>
    /// Last selected model's local path (for display)
    /// </summary>
    public string? LastSelectedModelPath { get; set; }

    /// <summary>
    /// Timestamp when last model was selected
    /// </summary>
    public DateTime? LastSelectedAt { get; set; }

    /// <summary>
    /// HuggingFace API token for accessing gated models (like FLUX.1-dev)
    /// </summary>
    public string? HuggingFaceToken { get; set; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PostXAgent", "diffusers_engine_settings.json");

    public static DiffusersEngineSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<DiffusersEngineSettings>(json) ?? new();
            }
        }
        catch { }
        return new DiffusersEngineSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    /// <summary>
    /// Get model ID to load (LastSelected > Default > RecommendedDefault)
    /// Always returns a usable model - never null
    /// </summary>
    public string GetModelToLoad()
    {
        // Priority: LastSelected > DefaultModelId > RecommendedModels.DefaultModel
        if (!string.IsNullOrEmpty(LastSelectedModelId))
            return LastSelectedModelId;

        if (!string.IsNullOrEmpty(DefaultModelId))
            return DefaultModelId;

        // Always fall back to a working model that doesn't require HF token
        return RecommendedModels.DefaultModel;
    }

    /// <summary>
    /// Check if the model to load requires HuggingFace token
    /// </summary>
    public bool ModelRequiresHuggingFaceToken()
    {
        var modelId = GetModelToLoad();
        return RecommendedModels.RequiresHuggingFaceToken(modelId);
    }

    /// <summary>
    /// Save the last selected model
    /// </summary>
    public void SetLastSelectedModel(string modelId, ModelType modelType, string? localPath = null)
    {
        LastSelectedModelId = modelId;
        LastSelectedModelType = modelType;
        LastSelectedModelPath = localPath;
        LastSelectedAt = DateTime.Now;
        Save();
    }
}

/// <summary>
/// Singleton manager for DiffusersGenerationEngine
/// ทำให้ระบบทำงานเร็วเหมือน ComfyUI:
/// - Server ทำงานตลอด ไม่ต้อง restart
/// - Model อยู่ใน VRAM ไม่ต้องโหลดใหม่
/// - Auto-start เมื่อเปิด app
/// - Auto-load default model
/// </summary>
public sealed class DiffusersEngineManager : IDisposable
{
    private static DiffusersEngineManager? _instance;
    private static readonly object _lock = new();

    private readonly DiffusersGenerationEngine _engine;
    private readonly ILogger<DiffusersEngineManager>? _logger;
    private readonly DiffusersEngineSettings _settings;
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
    /// Current loaded model ID (actually in VRAM)
    /// </summary>
    public string? CurrentModel => _engine.CurrentModel;

    /// <summary>
    /// Default model ID that will be auto-loaded on start (from settings)
    /// </summary>
    public string? DefaultModelId => _settings.DefaultModelId;

    /// <summary>
    /// Model that is either loaded or configured as default
    /// Use this for UI display - shows default model even before engine starts
    /// </summary>
    public string? DisplayModel => CurrentModel ?? DefaultModelId;

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

    /// <summary>
    /// Engine settings (persisted)
    /// </summary>
    public DiffusersEngineSettings Settings => _settings;

    private DiffusersEngineManager(ILogger<DiffusersEngineManager>? logger = null)
    {
        _logger = logger;
        _settings = DiffusersEngineSettings.Load();

        var modelService = new HuggingFaceModelService();
        var gpuService = new LocalGpuService();

        _engine = new DiffusersGenerationEngine(modelService, gpuService, logger: null);

        // Set HuggingFace token if configured
        if (!string.IsNullOrEmpty(_settings.HuggingFaceToken))
        {
            _engine.HuggingFaceToken = _settings.HuggingFaceToken;
            _logger?.LogInformation("HuggingFace token loaded from settings");
        }

        // Forward events
        _engine.StatusChanged += (s, e) => StatusChanged?.Invoke(this, e);
        _engine.ModelLoadProgressChanged += (s, e) => ModelLoadProgressChanged?.Invoke(this, e);
        _engine.LogOutput += (s, e) =>
        {
            var prefix = e.IsError ? "[ERROR] " : "";
            LogMessage?.Invoke(this, $"{prefix}{e.Message}");
        };

        _logger?.LogInformation("DiffusersEngineManager created");
    }

    /// <summary>
    /// Set the default model to auto-load on startup
    /// </summary>
    public void SetDefaultModel(string modelId, ModelType modelType)
    {
        _settings.DefaultModelId = modelId;
        _settings.DefaultModelType = modelType;
        _settings.Save();
        _logger?.LogInformation("Default model set: {ModelId} ({Type})", modelId, modelType);
    }

    /// <summary>
    /// Set the last selected model (called from Model Manager when user selects)
    /// </summary>
    public void SetLastSelectedModel(string modelId, ModelType modelType, string? localPath = null)
    {
        _settings.SetLastSelectedModel(modelId, modelType, localPath);
        _logger?.LogInformation("Last selected model saved: {ModelId} ({Type})", modelId, modelType);
        LastSelectedModelChanged?.Invoke(this, new LastSelectedModelEventArgs(modelId, modelType, localPath));
    }

    /// <summary>
    /// Get the last selected model info
    /// </summary>
    public (string? ModelId, ModelType Type, string? Path) GetLastSelectedModel()
    {
        return (_settings.LastSelectedModelId, _settings.LastSelectedModelType, _settings.LastSelectedModelPath);
    }

    /// <summary>
    /// Event when last selected model changes
    /// </summary>
    public event EventHandler<LastSelectedModelEventArgs>? LastSelectedModelChanged;

    /// <summary>
    /// Event when HuggingFace token changes
    /// </summary>
    public event EventHandler<bool>? HuggingFaceTokenChanged;

    /// <summary>
    /// Set the HuggingFace API token for accessing gated models
    /// </summary>
    public void SetHuggingFaceToken(string? token)
    {
        _settings.HuggingFaceToken = token;
        _settings.Save();

        // Update engine if already created
        _engine.HuggingFaceToken = token;

        _logger?.LogInformation("HuggingFace token {Action}", string.IsNullOrEmpty(token) ? "cleared" : "set");
        LogMessage?.Invoke(this, string.IsNullOrEmpty(token)
            ? "[Settings] HuggingFace token cleared"
            : $"[Settings] HuggingFace token set (length: {token!.Length})");

        // Notify listeners (e.g., UI pages)
        HuggingFaceTokenChanged?.Invoke(this, HasHuggingFaceToken);
    }

    /// <summary>
    /// Get whether HuggingFace token is configured
    /// </summary>
    public bool HasHuggingFaceToken => !string.IsNullOrEmpty(_settings.HuggingFaceToken);

    /// <summary>
    /// Clear the default model setting
    /// </summary>
    public void ClearDefaultModel()
    {
        _settings.DefaultModelId = null;
        _settings.Save();
        _logger?.LogInformation("Default model cleared");
    }

    /// <summary>
    /// Initialize and start the engine (call once on app startup)
    /// </summary>
    /// <param name="autoLoadModel">If true and default model is set, load it automatically</param>
    public async Task<bool> InitializeAsync(bool autoLoadModel = true, CancellationToken ct = default)
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

                // Auto-load model if enabled (always has a model - defaults to SDXL)
                if (autoLoadModel && _settings.AutoLoadModelOnStart)
                {
                    await AutoLoadDefaultModelAsync(ct);
                }

                return true;
            }

            // Start the engine
            var result = await _engine.StartAsync(port: _settings.ServerPort, ct: ct);

            if (result.Success)
            {
                _isInitialized = true;
                _logger?.LogInformation("DiffusersEngine initialized successfully");

                // Auto-load model if enabled (always has a model - defaults to SDXL)
                if (autoLoadModel && _settings.AutoLoadModelOnStart)
                {
                    await AutoLoadDefaultModelAsync(ct);
                }

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
    /// Auto-load the model (prioritizes LastSelected > Default > RecommendedDefault)
    /// </summary>
    private async Task AutoLoadDefaultModelAsync(CancellationToken ct)
    {
        // GetModelToLoad() always returns a usable model (never null)
        var modelToLoad = _settings.GetModelToLoad();

        // Determine source for logging
        string source;
        if (!string.IsNullOrEmpty(_settings.LastSelectedModelId))
            source = "last selected";
        else if (!string.IsNullOrEmpty(_settings.DefaultModelId))
            source = "default";
        else
            source = "recommended default (SDXL)";

        // Determine model type
        var modelType = !string.IsNullOrEmpty(_settings.LastSelectedModelId)
            ? _settings.LastSelectedModelType
            : _settings.DefaultModelType;

        // Skip if model is already loaded
        if (_engine.CurrentModel == modelToLoad)
        {
            _logger?.LogInformation("Model already loaded: {Model}", modelToLoad);
            return;
        }

        // Check if model requires HuggingFace token
        if (_settings.ModelRequiresHuggingFaceToken() && !HasHuggingFaceToken)
        {
            _logger?.LogWarning("Model {Model} requires HuggingFace token but none is configured", modelToLoad);
            LogMessage?.Invoke(this, $"[AutoLoad] [WARN] Model '{modelToLoad}' requires HuggingFace token");
            LogMessage?.Invoke(this, $"[AutoLoad] Falling back to recommended model: {RecommendedModels.DefaultModel}");

            // Fall back to default model
            modelToLoad = RecommendedModels.DefaultModel;
            source = "fallback (original requires HF token)";
        }

        try
        {
            _logger?.LogInformation("Auto-loading {Source} model: {Model} ({Type})",
                source, modelToLoad, modelType);

            LogMessage?.Invoke(this, $"[AutoLoad] Loading {source} model: {modelToLoad}");

            var loadResult = await _engine.LoadModelAsync(modelToLoad, modelType, forceLoad: _settings.SkipVramCheck, ct: ct);

            if (loadResult.Success)
            {
                _logger?.LogInformation("Model loaded successfully: {Model}", modelToLoad);
                LogMessage?.Invoke(this, $"[AutoLoad] [OK] Model loaded: {modelToLoad}");
            }
            else
            {
                _logger?.LogWarning("Failed to auto-load model: {Error}", loadResult.Error);
                LogMessage?.Invoke(this, $"[AutoLoad] [X] Failed to load model: {loadResult.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error auto-loading model");
            LogMessage?.Invoke(this, $"[AutoLoad] Error: {ex.Message}");
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

        return await InitializeAsync(autoLoadModel: true, ct: ct);
    }

    /// <summary>
    /// Load a model into VRAM (cached - won't reload if same model)
    /// </summary>
    /// <param name="setAsDefault">If true, save this model as the default for auto-load</param>
    public async Task<ModelLoadResult> LoadModelAsync(string modelId, ModelType type, bool setAsDefault = true, CancellationToken ct = default)
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

            // Still set as default if requested
            if (setAsDefault)
            {
                SetDefaultModel(modelId, type);
            }

            return new ModelLoadResult
            {
                Success = true,
                ModelId = modelId
            };
        }

        // Load the model (skip VRAM check if configured - let PyTorch handle memory)
        var result = await _engine.LoadModelAsync(modelId, type, forceLoad: _settings.SkipVramCheck, ct: ct);

        // Save as default if load was successful and requested
        if (result.Success && setAsDefault)
        {
            SetDefaultModel(modelId, type);
        }

        return result;
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

/// <summary>
/// Event args for LastSelectedModelChanged event
/// </summary>
public class LastSelectedModelEventArgs : EventArgs
{
    public string ModelId { get; }
    public ModelType ModelType { get; }
    public string? LocalPath { get; }

    public LastSelectedModelEventArgs(string modelId, ModelType modelType, string? localPath)
    {
        ModelId = modelId;
        ModelType = modelType;
        LocalPath = localPath;
    }
}
