using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Detects if this is the first run of the application
/// Manages setup completion state
/// </summary>
public class FirstRunDetectionService
{
    private readonly string _configPath;
    private readonly ILogger<FirstRunDetectionService>? _logger;

    private const string CONFIG_FILENAME = "setup-state.json";

    public FirstRunDetectionService(ILogger<FirstRunDetectionService>? logger = null)
    {
        _logger = logger;

        // Store config in LocalApplicationData
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent"
        );

        Directory.CreateDirectory(appDataPath);
        _configPath = Path.Combine(appDataPath, CONFIG_FILENAME);

        _logger?.LogDebug("First-run config path: {Path}", _configPath);
    }

    /// <summary>
    /// Check if this is the first run
    /// Returns true if setup has NOT been completed
    /// </summary>
    public bool IsFirstRun()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                _logger?.LogInformation("No setup state found - this is first run");
                return true;
            }

            var json = File.ReadAllText(_configPath);
            var state = JsonSerializer.Deserialize<SetupState>(json);

            if (state == null || !state.SetupCompleted)
            {
                _logger?.LogInformation("Setup not completed - treating as first run");
                return true;
            }

            _logger?.LogDebug("Setup completed at {Date}", state.CompletedAt);
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking first run state");
            // On error, assume first run to be safe
            return true;
        }
    }

    /// <summary>
    /// Mark setup as completed
    /// </summary>
    public void MarkSetupCompleted()
    {
        try
        {
            var state = new SetupState
            {
                SetupCompleted = true,
                CompletedAt = DateTime.UtcNow,
                Version = "1.0.0"
            };

            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(_configPath, json);
            _logger?.LogInformation("Setup marked as completed");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to mark setup as completed");
        }
    }

    /// <summary>
    /// Reset setup state (for testing or re-running wizard)
    /// </summary>
    public void ResetSetupState()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                File.Delete(_configPath);
                _logger?.LogInformation("Setup state reset");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to reset setup state");
        }
    }

    /// <summary>
    /// Get current setup state
    /// </summary>
    public SetupState? GetSetupState()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return null;
            }

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<SetupState>(json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get setup state");
            return null;
        }
    }
}

/// <summary>
/// Setup completion state
/// </summary>
public class SetupState
{
    public bool SetupCompleted { get; set; }
    public DateTime CompletedAt { get; set; }
    public string Version { get; set; } = "";
    public Dictionary<string, bool>? StepsCompleted { get; set; }
}
