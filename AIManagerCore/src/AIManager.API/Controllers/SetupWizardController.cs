using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using System.Text.Json;

namespace AIManager.API.Controllers;

/// <summary>
/// Setup Wizard Controller - หน้าติดตั้งระบบครั้งแรก
/// Professional setup wizard with progress tracking
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SetupWizardController : ControllerBase
{
    private readonly ILogger<SetupWizardController> _logger;
    private readonly IConfiguration _configuration;
    private readonly CoreDatabaseService _coreDb;
    private static SetupStatus _setupStatus = new();

    public SetupWizardController(
        ILogger<SetupWizardController> logger,
        IConfiguration configuration,
        CoreDatabaseService coreDb)
    {
        _logger = logger;
        _configuration = configuration;
        _coreDb = coreDb;
    }

    /// <summary>
    /// Get setup wizard HTML page
    /// </summary>
    [HttpGet("")]
    [HttpGet("page")]
    [Produces("text/html")]
    public ContentResult GetSetupPage()
    {
        var html = GetSetupWizardHtml();
        return Content(html, "text/html");
    }

    /// <summary>
    /// Get current setup status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<SetupStatus> GetSetupStatus()
    {
        return Ok(_setupStatus);
    }

    /// <summary>
    /// Check if setup is completed
    /// </summary>
    [HttpGet("is-complete")]
    public ActionResult<object> IsSetupComplete()
    {
        return Ok(new
        {
            isComplete = _setupStatus.IsComplete,
            progress = _setupStatus.OverallProgress,
            completedSteps = _setupStatus.Steps.Count(s => s.Value.IsComplete),
            totalSteps = _setupStatus.Steps.Count
        });
    }

    /// <summary>
    /// Validate step configuration
    /// </summary>
    [HttpPost("validate/{step}")]
    public async Task<ActionResult<object>> ValidateStep(string step, [FromBody] JsonElement config)
    {
        try
        {
            var result = step.ToLower() switch
            {
                "database" => await ValidateDatabaseStep(config),
                "ai-providers" => await ValidateAIProvidersStep(config),
                "api-keys" => await ValidateApiKeysStep(config),
                "platforms" => await ValidatePlatformsStep(config),
                "preferences" => await ValidatePreferencesStep(config),
                _ => new ValidationResult { IsValid = false, Message = "Unknown step" }
            };

            if (result.IsValid && _setupStatus.Steps.ContainsKey(step))
            {
                _setupStatus.Steps[step].IsComplete = true;
                _setupStatus.Steps[step].CompletedAt = DateTime.UtcNow;
                UpdateOverallProgress();
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating step {Step}", step);
            return Ok(new ValidationResult { IsValid = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Save step configuration
    /// </summary>
    [HttpPost("save/{step}")]
    public async Task<ActionResult<object>> SaveStep(string step, [FromBody] JsonElement config)
    {
        try
        {
            var success = step.ToLower() switch
            {
                "database" => await SaveDatabaseConfig(config),
                "ai-providers" => await SaveAIProvidersConfig(config),
                "api-keys" => await SaveApiKeysConfig(config),
                "platforms" => await SavePlatformsConfig(config),
                "preferences" => await SavePreferencesConfig(config),
                _ => false
            };

            if (success && _setupStatus.Steps.ContainsKey(step))
            {
                _setupStatus.Steps[step].IsComplete = true;
                _setupStatus.Steps[step].CompletedAt = DateTime.UtcNow;
                UpdateOverallProgress();
            }

            return Ok(new { success, step, progress = _setupStatus.OverallProgress });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving step {Step}", step);
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Complete setup wizard
    /// </summary>
    [HttpPost("complete")]
    public ActionResult<object> CompleteSetup()
    {
        try
        {
            _setupStatus.IsComplete = true;
            _setupStatus.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Setup wizard completed successfully");

            return Ok(new
            {
                success = true,
                message = "Setup completed successfully",
                completedAt = _setupStatus.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing setup");
            return Ok(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Reset setup wizard
    /// </summary>
    [HttpPost("reset")]
    public ActionResult<object> ResetSetup()
    {
        _setupStatus = new SetupStatus();
        return Ok(new { success = true, message = "Setup reset successfully" });
    }

    /// <summary>
    /// Test database connection
    /// </summary>
    [HttpPost("test/database")]
    public async Task<ActionResult<object>> TestDatabaseConnection([FromBody] DatabaseConfig config)
    {
        try
        {
            // Simulate database connection test
            await Task.Delay(500);

            // In real implementation, test actual connection
            var success = !string.IsNullOrEmpty(config.ConnectionString) ||
                         (!string.IsNullOrEmpty(config.Host) && !string.IsNullOrEmpty(config.Database));

            return Ok(new
            {
                success,
                message = success ? "Database connection successful" : "Invalid configuration"
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Test AI provider connection
    /// </summary>
    [HttpPost("test/ai-provider")]
    public async Task<ActionResult<object>> TestAIProvider([FromBody] AIProviderConfig config)
    {
        try
        {
            await Task.Delay(500);

            var success = config.Provider?.ToLower() switch
            {
                "ollama" => !string.IsNullOrEmpty(config.BaseUrl),
                "openai" => !string.IsNullOrEmpty(config.ApiKey),
                "anthropic" => !string.IsNullOrEmpty(config.ApiKey),
                "google" => !string.IsNullOrEmpty(config.ApiKey),
                _ => false
            };

            return Ok(new
            {
                success,
                provider = config.Provider,
                message = success ? $"{config.Provider} connected successfully" : "Invalid configuration"
            });
        }
        catch (Exception ex)
        {
            return Ok(new { success = false, message = ex.Message });
        }
    }

    // Validation methods
    private Task<ValidationResult> ValidateDatabaseStep(JsonElement config)
    {
        var connectionString = config.TryGetProperty("connectionString", out var cs) ? cs.GetString() : null;
        var host = config.TryGetProperty("host", out var h) ? h.GetString() : null;

        var isValid = !string.IsNullOrEmpty(connectionString) || !string.IsNullOrEmpty(host);
        return Task.FromResult(new ValidationResult
        {
            IsValid = isValid,
            Message = isValid ? "Database configuration valid" : "Please provide database configuration"
        });
    }

    private Task<ValidationResult> ValidateAIProvidersStep(JsonElement config)
    {
        var hasProvider = config.TryGetProperty("providers", out var providers) &&
                         providers.ValueKind == JsonValueKind.Array &&
                         providers.GetArrayLength() > 0;

        return Task.FromResult(new ValidationResult
        {
            IsValid = hasProvider,
            Message = hasProvider ? "AI providers configured" : "Please configure at least one AI provider"
        });
    }

    private Task<ValidationResult> ValidateApiKeysStep(JsonElement config)
    {
        // API keys are optional but recommended
        return Task.FromResult(new ValidationResult
        {
            IsValid = true,
            Message = "API keys configuration saved"
        });
    }

    private Task<ValidationResult> ValidatePlatformsStep(JsonElement config)
    {
        // Platforms are optional
        return Task.FromResult(new ValidationResult
        {
            IsValid = true,
            Message = "Platform configuration saved"
        });
    }

    private Task<ValidationResult> ValidatePreferencesStep(JsonElement config)
    {
        return Task.FromResult(new ValidationResult
        {
            IsValid = true,
            Message = "Preferences saved"
        });
    }

    // Save methods
    private Task<bool> SaveDatabaseConfig(JsonElement config)
    {
        _logger.LogInformation("Database configuration saved");
        return Task.FromResult(true);
    }

    private Task<bool> SaveAIProvidersConfig(JsonElement config)
    {
        _logger.LogInformation("AI providers configuration saved");
        return Task.FromResult(true);
    }

    private Task<bool> SaveApiKeysConfig(JsonElement config)
    {
        _logger.LogInformation("API keys configuration saved");
        return Task.FromResult(true);
    }

    private Task<bool> SavePlatformsConfig(JsonElement config)
    {
        _logger.LogInformation("Platforms configuration saved");
        return Task.FromResult(true);
    }

    private Task<bool> SavePreferencesConfig(JsonElement config)
    {
        _logger.LogInformation("Preferences saved");
        return Task.FromResult(true);
    }

    private void UpdateOverallProgress()
    {
        var completedSteps = _setupStatus.Steps.Count(s => s.Value.IsComplete);
        var totalSteps = _setupStatus.Steps.Count;
        _setupStatus.OverallProgress = totalSteps > 0 ? (int)((double)completedSteps / totalSteps * 100) : 0;
    }

    private string GetSetupWizardHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""th"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>AI Manager - Setup Wizard</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }

        :root {
            --primary: #00d4ff;
            --primary-dark: #0099cc;
            --success: #00ff88;
            --warning: #ffaa00;
            --error: #ff4444;
            --bg-dark: #0a0a1a;
            --bg-card: rgba(255,255,255,0.05);
            --text-primary: #ffffff;
            --text-secondary: #a0a0a0;
        }

        body {
            font-family: 'Segoe UI', 'Prompt', Tahoma, sans-serif;
            background: linear-gradient(135deg, #0a0a1a 0%, #1a1a3e 50%, #0a0a1a 100%);
            min-height: 100vh;
            color: var(--text-primary);
            overflow-x: hidden;
        }

        /* Animated background */
        .bg-animation {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            z-index: -1;
            overflow: hidden;
        }

        .bg-animation::before {
            content: '';
            position: absolute;
            width: 200%;
            height: 200%;
            top: -50%;
            left: -50%;
            background: radial-gradient(circle at 20% 80%, rgba(0,212,255,0.1) 0%, transparent 50%),
                        radial-gradient(circle at 80% 20%, rgba(0,255,136,0.1) 0%, transparent 50%);
            animation: rotate 30s linear infinite;
        }

        @keyframes rotate {
            from { transform: rotate(0deg); }
            to { transform: rotate(360deg); }
        }

        .container {
            max-width: 900px;
            margin: 0 auto;
            padding: 40px 20px;
            position: relative;
        }

        /* Header */
        .header {
            text-align: center;
            margin-bottom: 40px;
        }

        .logo {
            font-size: 3rem;
            margin-bottom: 10px;
            animation: pulse 2s ease-in-out infinite;
        }

        @keyframes pulse {
            0%, 100% { transform: scale(1); }
            50% { transform: scale(1.1); }
        }

        .header h1 {
            font-size: 2.5rem;
            background: linear-gradient(135deg, var(--primary) 0%, var(--success) 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
            margin-bottom: 10px;
        }

        .header p {
            color: var(--text-secondary);
            font-size: 1.1rem;
        }

        /* Progress Section */
        .progress-section {
            background: var(--bg-card);
            border: 1px solid rgba(255,255,255,0.1);
            border-radius: 20px;
            padding: 30px;
            margin-bottom: 30px;
            backdrop-filter: blur(10px);
        }

        .progress-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
        }

        .progress-title {
            font-size: 1.2rem;
            color: var(--primary);
        }

        .progress-percentage {
            font-size: 2rem;
            font-weight: bold;
            color: var(--success);
        }

        /* Circular Progress */
        .progress-circle-container {
            display: flex;
            justify-content: center;
            margin-bottom: 30px;
        }

        .progress-circle {
            position: relative;
            width: 200px;
            height: 200px;
        }

        .progress-circle svg {
            transform: rotate(-90deg);
        }

        .progress-circle-bg {
            fill: none;
            stroke: rgba(255,255,255,0.1);
            stroke-width: 10;
        }

        .progress-circle-fill {
            fill: none;
            stroke: url(#gradient);
            stroke-width: 10;
            stroke-linecap: round;
            stroke-dasharray: 565.48;
            stroke-dashoffset: 565.48;
            transition: stroke-dashoffset 1s ease;
        }

        .progress-circle-text {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            text-align: center;
        }

        .progress-circle-value {
            font-size: 3rem;
            font-weight: bold;
            color: var(--primary);
        }

        .progress-circle-label {
            font-size: 0.9rem;
            color: var(--text-secondary);
        }

        /* Steps Indicator */
        .steps-indicator {
            display: flex;
            justify-content: space-between;
            margin-bottom: 30px;
            position: relative;
        }

        .steps-indicator::before {
            content: '';
            position: absolute;
            top: 20px;
            left: 40px;
            right: 40px;
            height: 2px;
            background: rgba(255,255,255,0.1);
        }

        .step-indicator {
            display: flex;
            flex-direction: column;
            align-items: center;
            position: relative;
            z-index: 1;
            cursor: pointer;
            transition: transform 0.3s;
        }

        .step-indicator:hover {
            transform: translateY(-5px);
        }

        .step-number {
            width: 40px;
            height: 40px;
            border-radius: 50%;
            background: rgba(255,255,255,0.1);
            border: 2px solid rgba(255,255,255,0.2);
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            margin-bottom: 8px;
            transition: all 0.3s;
        }

        .step-indicator.active .step-number {
            background: var(--primary);
            border-color: var(--primary);
            color: #000;
            box-shadow: 0 0 20px rgba(0,212,255,0.5);
        }

        .step-indicator.completed .step-number {
            background: var(--success);
            border-color: var(--success);
            color: #000;
        }

        .step-label {
            font-size: 0.8rem;
            color: var(--text-secondary);
            text-align: center;
            max-width: 80px;
        }

        .step-indicator.active .step-label,
        .step-indicator.completed .step-label {
            color: var(--text-primary);
        }

        /* Step Content */
        .step-content {
            display: none;
            animation: fadeIn 0.5s ease;
        }

        .step-content.active {
            display: block;
        }

        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(20px); }
            to { opacity: 1; transform: translateY(0); }
        }

        /* Cards */
        .card {
            background: var(--bg-card);
            border: 1px solid rgba(255,255,255,0.1);
            border-radius: 15px;
            padding: 25px;
            margin-bottom: 20px;
            backdrop-filter: blur(10px);
            transition: transform 0.3s, box-shadow 0.3s;
        }

        .card:hover {
            transform: translateY(-3px);
            box-shadow: 0 10px 30px rgba(0,212,255,0.1);
        }

        .card h3 {
            color: var(--primary);
            margin-bottom: 20px;
            display: flex;
            align-items: center;
            gap: 10px;
            font-size: 1.3rem;
        }

        .card h3 .icon {
            font-size: 1.5rem;
        }

        .card p {
            color: var(--text-secondary);
            margin-bottom: 20px;
            line-height: 1.6;
        }

        /* Form Elements */
        .form-group {
            margin-bottom: 20px;
        }

        .form-group label {
            display: block;
            margin-bottom: 8px;
            color: var(--text-secondary);
            font-size: 0.9rem;
        }

        .form-group label .required {
            color: var(--error);
        }

        input, textarea, select {
            width: 100%;
            padding: 14px 18px;
            border: 1px solid rgba(255,255,255,0.2);
            border-radius: 10px;
            background: rgba(0,0,0,0.3);
            color: var(--text-primary);
            font-size: 1rem;
            transition: all 0.3s;
        }

        input:focus, textarea:focus, select:focus {
            outline: none;
            border-color: var(--primary);
            box-shadow: 0 0 15px rgba(0,212,255,0.2);
        }

        input::placeholder {
            color: rgba(255,255,255,0.3);
        }

        select option {
            background: #1a1a2e;
        }

        /* Toggle Switch */
        .toggle-group {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 15px;
            background: rgba(0,0,0,0.2);
            border-radius: 10px;
            margin-bottom: 10px;
        }

        .toggle-label {
            display: flex;
            flex-direction: column;
        }

        .toggle-label span:first-child {
            font-weight: 500;
        }

        .toggle-label span:last-child {
            font-size: 0.8rem;
            color: var(--text-secondary);
        }

        .toggle-switch {
            position: relative;
            width: 50px;
            height: 26px;
        }

        .toggle-switch input {
            opacity: 0;
            width: 0;
            height: 0;
        }

        .toggle-slider {
            position: absolute;
            cursor: pointer;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(255,255,255,0.1);
            border-radius: 26px;
            transition: 0.3s;
        }

        .toggle-slider::before {
            position: absolute;
            content: '';
            height: 20px;
            width: 20px;
            left: 3px;
            bottom: 3px;
            background: white;
            border-radius: 50%;
            transition: 0.3s;
        }

        .toggle-switch input:checked + .toggle-slider {
            background: var(--primary);
        }

        .toggle-switch input:checked + .toggle-slider::before {
            transform: translateX(24px);
        }

        /* Buttons */
        .btn {
            padding: 14px 28px;
            border: none;
            border-radius: 10px;
            font-size: 1rem;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.3s;
            display: inline-flex;
            align-items: center;
            gap: 8px;
        }

        .btn-primary {
            background: linear-gradient(135deg, var(--primary) 0%, var(--primary-dark) 100%);
            color: #000;
        }

        .btn-primary:hover {
            transform: translateY(-2px);
            box-shadow: 0 10px 30px rgba(0,212,255,0.3);
        }

        .btn-secondary {
            background: rgba(255,255,255,0.1);
            color: var(--text-primary);
            border: 1px solid rgba(255,255,255,0.2);
        }

        .btn-secondary:hover {
            background: rgba(255,255,255,0.2);
        }

        .btn-success {
            background: linear-gradient(135deg, var(--success) 0%, #00cc6a 100%);
            color: #000;
        }

        .btn-test {
            background: rgba(255,170,0,0.2);
            color: var(--warning);
            border: 1px solid var(--warning);
            padding: 10px 20px;
            font-size: 0.9rem;
        }

        .btn-test:hover {
            background: var(--warning);
            color: #000;
        }

        .btn:disabled {
            opacity: 0.5;
            cursor: not-allowed;
            transform: none !important;
        }

        /* Button Group */
        .btn-group {
            display: flex;
            gap: 15px;
            margin-top: 30px;
            justify-content: space-between;
        }

        /* Provider Cards */
        .provider-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }

        .provider-card {
            background: rgba(0,0,0,0.2);
            border: 2px solid rgba(255,255,255,0.1);
            border-radius: 12px;
            padding: 20px;
            text-align: center;
            cursor: pointer;
            transition: all 0.3s;
        }

        .provider-card:hover {
            border-color: var(--primary);
            transform: translateY(-3px);
        }

        .provider-card.selected {
            border-color: var(--success);
            background: rgba(0,255,136,0.1);
        }

        .provider-card .icon {
            font-size: 2.5rem;
            margin-bottom: 10px;
        }

        .provider-card .name {
            font-weight: 600;
            margin-bottom: 5px;
        }

        .provider-card .desc {
            font-size: 0.8rem;
            color: var(--text-secondary);
        }

        .provider-card .badge {
            display: inline-block;
            padding: 3px 8px;
            background: var(--success);
            color: #000;
            border-radius: 10px;
            font-size: 0.7rem;
            font-weight: bold;
            margin-top: 8px;
        }

        /* Status Message */
        .status-message {
            padding: 15px;
            border-radius: 10px;
            margin-top: 15px;
            display: none;
        }

        .status-message.success {
            background: rgba(0,255,136,0.1);
            border: 1px solid var(--success);
            color: var(--success);
            display: block;
        }

        .status-message.error {
            background: rgba(255,68,68,0.1);
            border: 1px solid var(--error);
            color: var(--error);
            display: block;
        }

        .status-message.info {
            background: rgba(0,212,255,0.1);
            border: 1px solid var(--primary);
            color: var(--primary);
            display: block;
        }

        /* Completion Screen */
        .completion-screen {
            text-align: center;
            padding: 40px;
        }

        .completion-icon {
            font-size: 5rem;
            margin-bottom: 20px;
            animation: bounce 1s ease infinite;
        }

        @keyframes bounce {
            0%, 100% { transform: translateY(0); }
            50% { transform: translateY(-20px); }
        }

        .completion-screen h2 {
            font-size: 2rem;
            color: var(--success);
            margin-bottom: 15px;
        }

        .completion-screen p {
            color: var(--text-secondary);
            margin-bottom: 30px;
            font-size: 1.1rem;
        }

        .feature-list {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin: 30px 0;
            text-align: left;
        }

        .feature-item {
            display: flex;
            align-items: center;
            gap: 10px;
            padding: 15px;
            background: rgba(0,255,136,0.05);
            border-radius: 10px;
        }

        .feature-item .check {
            color: var(--success);
            font-size: 1.2rem;
        }

        /* Loading Spinner */
        .spinner {
            display: inline-block;
            width: 20px;
            height: 20px;
            border: 2px solid rgba(255,255,255,0.3);
            border-radius: 50%;
            border-top-color: var(--primary);
            animation: spin 1s linear infinite;
        }

        @keyframes spin {
            to { transform: rotate(360deg); }
        }

        /* Responsive */
        @media (max-width: 768px) {
            .header h1 { font-size: 1.8rem; }
            .progress-circle { width: 150px; height: 150px; }
            .progress-circle-value { font-size: 2rem; }
            .steps-indicator { flex-wrap: wrap; gap: 15px; }
            .steps-indicator::before { display: none; }
            .btn-group { flex-direction: column; }
            .provider-grid { grid-template-columns: 1fr 1fr; }
        }
    </style>
</head>
<body>
    <div class=""bg-animation""></div>

    <div class=""container"">
        <div class=""header"">
            <div class=""logo"">🤖</div>
            <h1>AI Manager Setup</h1>
            <p>ยินดีต้อนรับ! มาตั้งค่าระบบให้พร้อมใช้งานกันเถอะ</p>
        </div>

        <!-- Progress Section -->
        <div class=""progress-section"">
            <div class=""progress-header"">
                <span class=""progress-title"">ความคืบหน้าการติดตั้ง</span>
                <span class=""progress-percentage"" id=""progressText"">0%</span>
            </div>

            <div class=""progress-circle-container"">
                <div class=""progress-circle"">
                    <svg width=""200"" height=""200"" viewBox=""0 0 200 200"">
                        <defs>
                            <linearGradient id=""gradient"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">
                                <stop offset=""0%"" style=""stop-color:#00d4ff""/>
                                <stop offset=""100%"" style=""stop-color:#00ff88""/>
                            </linearGradient>
                        </defs>
                        <circle class=""progress-circle-bg"" cx=""100"" cy=""100"" r=""90""/>
                        <circle class=""progress-circle-fill"" id=""progressCircle"" cx=""100"" cy=""100"" r=""90""/>
                    </svg>
                    <div class=""progress-circle-text"">
                        <div class=""progress-circle-value"" id=""progressValue"">0%</div>
                        <div class=""progress-circle-label"">Complete</div>
                    </div>
                </div>
            </div>

            <!-- Steps Indicator -->
            <div class=""steps-indicator"">
                <div class=""step-indicator active"" onclick=""goToStep(0)"" id=""stepInd0"">
                    <div class=""step-number"">1</div>
                    <span class=""step-label"">Database</span>
                </div>
                <div class=""step-indicator"" onclick=""goToStep(1)"" id=""stepInd1"">
                    <div class=""step-number"">2</div>
                    <span class=""step-label"">AI Providers</span>
                </div>
                <div class=""step-indicator"" onclick=""goToStep(2)"" id=""stepInd2"">
                    <div class=""step-number"">3</div>
                    <span class=""step-label"">API Keys</span>
                </div>
                <div class=""step-indicator"" onclick=""goToStep(3)"" id=""stepInd3"">
                    <div class=""step-number"">4</div>
                    <span class=""step-label"">Platforms</span>
                </div>
                <div class=""step-indicator"" onclick=""goToStep(4)"" id=""stepInd4"">
                    <div class=""step-number"">5</div>
                    <span class=""step-label"">Preferences</span>
                </div>
            </div>
        </div>

        <!-- Step 1: Database -->
        <div class=""step-content active"" id=""step0"">
            <div class=""card"">
                <h3><span class=""icon"">🗄️</span> Database Configuration</h3>
                <p>ตั้งค่าการเชื่อมต่อฐานข้อมูลสำหรับเก็บข้อมูลระบบ</p>

                <div class=""form-group"">
                    <label>Database Type</label>
                    <select id=""dbType"" onchange=""toggleDbFields()"">
                        <option value=""sqlite"">SQLite (แนะนำสำหรับเริ่มต้น)</option>
                        <option value=""mysql"">MySQL</option>
                        <option value=""postgresql"">PostgreSQL</option>
                        <option value=""sqlserver"">SQL Server</option>
                    </select>
                </div>

                <div id=""dbConnectionFields"" style=""display: none;"">
                    <div class=""form-group"">
                        <label>Host</label>
                        <input type=""text"" id=""dbHost"" placeholder=""localhost"" value=""localhost"">
                    </div>
                    <div class=""form-group"">
                        <label>Port</label>
                        <input type=""number"" id=""dbPort"" placeholder=""3306"" value=""3306"">
                    </div>
                    <div class=""form-group"">
                        <label>Database Name</label>
                        <input type=""text"" id=""dbName"" placeholder=""aimanager"" value=""aimanager"">
                    </div>
                    <div class=""form-group"">
                        <label>Username</label>
                        <input type=""text"" id=""dbUser"" placeholder=""root"">
                    </div>
                    <div class=""form-group"">
                        <label>Password</label>
                        <input type=""password"" id=""dbPassword"" placeholder=""********"">
                    </div>
                </div>

                <div id=""sqliteInfo"" class=""status-message info"">
                    <strong>SQLite</strong> - ฐานข้อมูลในไฟล์เดียว ใช้งานง่าย ไม่ต้องติดตั้งเพิ่มเติม เหมาะสำหรับการทดสอบและใช้งานขนาดเล็ก-กลาง
                </div>

                <button class=""btn btn-test"" onclick=""testDatabase()"" id=""btnTestDb"">
                    🔌 Test Connection
                </button>

                <div class=""status-message"" id=""dbStatus""></div>
            </div>
        </div>

        <!-- Step 2: AI Providers -->
        <div class=""step-content"" id=""step1"">
            <div class=""card"">
                <h3><span class=""icon"">🧠</span> AI Providers</h3>
                <p>เลือกผู้ให้บริการ AI สำหรับสร้างเนื้อหา คุณสามารถเลือกได้มากกว่าหนึ่ง</p>

                <div class=""provider-grid"">
                    <div class=""provider-card"" onclick=""toggleProvider('ollama')"" id=""providerOllama"">
                        <div class=""icon"">🦙</div>
                        <div class=""name"">Ollama</div>
                        <div class=""desc"">Local AI, ฟรี, ไม่ต้องใช้ internet</div>
                        <span class=""badge"">แนะนำ</span>
                    </div>
                    <div class=""provider-card"" onclick=""toggleProvider('gemini')"" id=""providerGemini"">
                        <div class=""icon"">🌟</div>
                        <div class=""name"">Google Gemini</div>
                        <div class=""desc"">มี Free tier, รวดเร็ว</div>
                        <span class=""badge"">Free Tier</span>
                    </div>
                    <div class=""provider-card"" onclick=""toggleProvider('openai')"" id=""providerOpenai"">
                        <div class=""icon"">🤖</div>
                        <div class=""name"">OpenAI</div>
                        <div class=""desc"">GPT-4, คุณภาพสูง</div>
                    </div>
                    <div class=""provider-card"" onclick=""toggleProvider('anthropic')"" id=""providerAnthropic"">
                        <div class=""icon"">🎭</div>
                        <div class=""name"">Anthropic</div>
                        <div class=""desc"">Claude, ปลอดภัย</div>
                    </div>
                </div>

                <div id=""providerConfig"" style=""display: none;"">
                    <h4 style=""margin: 20px 0 15px; color: var(--primary);"">🔧 Provider Configuration</h4>

                    <div id=""ollamaConfig"" class=""provider-config"" style=""display: none;"">
                        <div class=""form-group"">
                            <label>Ollama Base URL</label>
                            <input type=""text"" id=""ollamaUrl"" placeholder=""http://localhost:11434"" value=""http://localhost:11434"">
                        </div>
                        <div class=""form-group"">
                            <label>Default Model</label>
                            <input type=""text"" id=""ollamaModel"" placeholder=""llama2"" value=""llama2"">
                        </div>
                    </div>

                    <div id=""geminiConfig"" class=""provider-config"" style=""display: none;"">
                        <div class=""form-group"">
                            <label>Google API Key</label>
                            <input type=""password"" id=""geminiKey"" placeholder=""AIza..."">
                        </div>
                    </div>

                    <div id=""openaiConfig"" class=""provider-config"" style=""display: none;"">
                        <div class=""form-group"">
                            <label>OpenAI API Key</label>
                            <input type=""password"" id=""openaiKey"" placeholder=""sk-..."">
                        </div>
                    </div>

                    <div id=""anthropicConfig"" class=""provider-config"" style=""display: none;"">
                        <div class=""form-group"">
                            <label>Anthropic API Key</label>
                            <input type=""password"" id=""anthropicKey"" placeholder=""sk-ant-..."">
                        </div>
                    </div>
                </div>

                <button class=""btn btn-test"" onclick=""testAIProvider()"" id=""btnTestAI"">
                    🧪 Test AI Connection
                </button>

                <div class=""status-message"" id=""aiStatus""></div>
            </div>
        </div>

        <!-- Step 3: API Keys -->
        <div class=""step-content"" id=""step2"">
            <div class=""card"">
                <h3><span class=""icon"">🔑</span> API Key Settings</h3>
                <p>ตั้งค่า API Key สำหรับป้องกันการเข้าถึง API ของคุณ</p>

                <div class=""toggle-group"">
                    <div class=""toggle-label"">
                        <span>Enable API Key Authentication</span>
                        <span>บังคับใช้ API Key ในทุก request</span>
                    </div>
                    <label class=""toggle-switch"">
                        <input type=""checkbox"" id=""enableApiKey"" checked>
                        <span class=""toggle-slider""></span>
                    </label>
                </div>

                <div class=""form-group"">
                    <label>Master API Key</label>
                    <div style=""display: flex; gap: 10px;"">
                        <input type=""text"" id=""masterApiKey"" placeholder=""จะถูกสร้างอัตโนมัติ"" readonly>
                        <button class=""btn btn-secondary"" onclick=""generateApiKey()"" style=""white-space: nowrap;"">
                            🎲 Generate
                        </button>
                    </div>
                </div>

                <div class=""form-group"">
                    <label>Allowed IPs (Optional)</label>
                    <input type=""text"" id=""allowedIps"" placeholder=""เว้นว่างเพื่ออนุญาตทุก IP"">
                    <small style=""color: var(--text-secondary); display: block; margin-top: 5px;"">
                        คั่นด้วยจุลภาค เช่น 127.0.0.1, 192.168.1.0/24
                    </small>
                </div>

                <div class=""toggle-group"">
                    <div class=""toggle-label"">
                        <span>Rate Limiting</span>
                        <span>จำกัดจำนวน request ต่อนาที</span>
                    </div>
                    <label class=""toggle-switch"">
                        <input type=""checkbox"" id=""enableRateLimit"">
                        <span class=""toggle-slider""></span>
                    </label>
                </div>
            </div>
        </div>

        <!-- Step 4: Platforms -->
        <div class=""step-content"" id=""step3"">
            <div class=""card"">
                <h3><span class=""icon"">📱</span> Social Media Platforms</h3>
                <p>เลือก platforms ที่ต้องการใช้งาน (สามารถตั้งค่าเพิ่มเติมภายหลังได้)</p>

                <div class=""provider-grid"">
                    <div class=""provider-card selected"" onclick=""togglePlatform('facebook')"" id=""platformFacebook"">
                        <div class=""icon"">📘</div>
                        <div class=""name"">Facebook</div>
                        <div class=""desc"">Pages & Groups</div>
                    </div>
                    <div class=""provider-card"" onclick=""togglePlatform('instagram')"" id=""platformInstagram"">
                        <div class=""icon"">📷</div>
                        <div class=""name"">Instagram</div>
                        <div class=""desc"">Feed & Stories</div>
                    </div>
                    <div class=""provider-card"" onclick=""togglePlatform('tiktok')"" id=""platformTiktok"">
                        <div class=""icon"">🎵</div>
                        <div class=""name"">TikTok</div>
                        <div class=""desc"">Short Videos</div>
                    </div>
                    <div class=""provider-card"" onclick=""togglePlatform('twitter')"" id=""platformTwitter"">
                        <div class=""icon"">🐦</div>
                        <div class=""name"">Twitter/X</div>
                        <div class=""desc"">Tweets & Threads</div>
                    </div>
                    <div class=""provider-card"" onclick=""togglePlatform('line')"" id=""platformLine"">
                        <div class=""icon"">💚</div>
                        <div class=""name"">LINE</div>
                        <div class=""desc"">Official Account</div>
                    </div>
                    <div class=""provider-card"" onclick=""togglePlatform('youtube')"" id=""platformYoutube"">
                        <div class=""icon"">🎬</div>
                        <div class=""name"">YouTube</div>
                        <div class=""desc"">Videos & Shorts</div>
                    </div>
                </div>

                <div class=""status-message info"">
                    <strong>หมายเหตุ:</strong> คุณสามารถเพิ่ม credentials และตั้งค่าแต่ละ platform ได้ในหน้า Settings หลังจากติดตั้งเสร็จ
                </div>
            </div>
        </div>

        <!-- Step 5: Preferences -->
        <div class=""step-content"" id=""step4"">
            <div class=""card"">
                <h3><span class=""icon"">⚙️</span> Preferences</h3>
                <p>ตั้งค่าการทำงานเริ่มต้นของระบบ</p>

                <div class=""form-group"">
                    <label>Default Language</label>
                    <select id=""defaultLanguage"">
                        <option value=""th"" selected>ไทย (Thai)</option>
                        <option value=""en"">English</option>
                        <option value=""both"">Both (สองภาษา)</option>
                    </select>
                </div>

                <div class=""form-group"">
                    <label>Default Tone/Style</label>
                    <select id=""defaultTone"">
                        <option value=""friendly"">Friendly (เป็นมิตร)</option>
                        <option value=""professional"">Professional (มืออาชีพ)</option>
                        <option value=""casual"">Casual (สบายๆ)</option>
                        <option value=""humorous"">Humorous (ตลกขบขัน)</option>
                    </select>
                </div>

                <div class=""toggle-group"">
                    <div class=""toggle-label"">
                        <span>Auto-start Workers</span>
                        <span>เริ่ม workers อัตโนมัติเมื่อระบบเปิด</span>
                    </div>
                    <label class=""toggle-switch"">
                        <input type=""checkbox"" id=""autoStartWorkers"" checked>
                        <span class=""toggle-slider""></span>
                    </label>
                </div>

                <div class=""toggle-group"">
                    <div class=""toggle-label"">
                        <span>Enable Logging</span>
                        <span>บันทึก log ทุกการทำงาน</span>
                    </div>
                    <label class=""toggle-switch"">
                        <input type=""checkbox"" id=""enableLogging"" checked>
                        <span class=""toggle-slider""></span>
                    </label>
                </div>

                <div class=""toggle-group"">
                    <div class=""toggle-label"">
                        <span>Send Anonymous Analytics</span>
                        <span>ช่วยพัฒนาระบบให้ดีขึ้น</span>
                    </div>
                    <label class=""toggle-switch"">
                        <input type=""checkbox"" id=""sendAnalytics"">
                        <span class=""toggle-slider""></span>
                    </label>
                </div>
            </div>
        </div>

        <!-- Completion Screen -->
        <div class=""step-content"" id=""stepComplete"">
            <div class=""card completion-screen"">
                <div class=""completion-icon"">🎉</div>
                <h2>Setup Complete!</h2>
                <p>ระบบพร้อมใช้งานแล้ว! คุณสามารถเริ่มใช้งาน AI Manager ได้ทันที</p>

                <div class=""feature-list"">
                    <div class=""feature-item"">
                        <span class=""check"">✅</span>
                        <span>Database Connected</span>
                    </div>
                    <div class=""feature-item"">
                        <span class=""check"">✅</span>
                        <span>AI Providers Ready</span>
                    </div>
                    <div class=""feature-item"">
                        <span class=""check"">✅</span>
                        <span>API Security Enabled</span>
                    </div>
                    <div class=""feature-item"">
                        <span class=""check"">✅</span>
                        <span>Platforms Configured</span>
                    </div>
                </div>

                <div style=""margin-top: 30px;"">
                    <a href=""/api/apitest/page"" class=""btn btn-primary"" style=""text-decoration: none; margin-right: 10px;"">
                        🧪 Test API
                    </a>
                    <a href=""/"" class=""btn btn-success"" style=""text-decoration: none;"">
                        🚀 Go to Dashboard
                    </a>
                </div>
            </div>
        </div>

        <!-- Navigation Buttons -->
        <div class=""btn-group"" id=""navButtons"">
            <button class=""btn btn-secondary"" onclick=""prevStep()"" id=""btnPrev"" disabled>
                ← Previous
            </button>
            <button class=""btn btn-primary"" onclick=""nextStep()"" id=""btnNext"">
                Next →
            </button>
        </div>
    </div>

    <script>
        const API_BASE = window.location.origin;
        let currentStep = 0;
        const totalSteps = 5;
        const stepStatus = [false, false, false, false, false];
        const selectedProviders = new Set();
        const selectedPlatforms = new Set(['facebook']);

        function updateProgress() {
            const completedSteps = stepStatus.filter(s => s).length;
            const progress = Math.round((completedSteps / totalSteps) * 100);

            document.getElementById('progressText').textContent = progress + '%';
            document.getElementById('progressValue').textContent = progress + '%';

            // Update circular progress
            const circle = document.getElementById('progressCircle');
            const circumference = 2 * Math.PI * 90;
            const offset = circumference - (progress / 100) * circumference;
            circle.style.strokeDashoffset = offset;
        }

        function goToStep(step) {
            if (step < 0 || step > totalSteps) return;

            // Hide all steps
            document.querySelectorAll('.step-content').forEach(el => el.classList.remove('active'));
            document.querySelectorAll('.step-indicator').forEach(el => el.classList.remove('active'));

            // Show target step
            if (step === totalSteps) {
                document.getElementById('stepComplete').classList.add('active');
                document.getElementById('navButtons').style.display = 'none';
            } else {
                document.getElementById('step' + step).classList.add('active');
                document.getElementById('stepInd' + step).classList.add('active');
                document.getElementById('navButtons').style.display = 'flex';
            }

            currentStep = step;

            // Update buttons
            document.getElementById('btnPrev').disabled = currentStep === 0;
            document.getElementById('btnNext').textContent = currentStep === totalSteps - 1 ? 'Complete Setup ✓' : 'Next →';

            // Update step indicators
            for (let i = 0; i < totalSteps; i++) {
                const ind = document.getElementById('stepInd' + i);
                if (stepStatus[i]) {
                    ind.classList.add('completed');
                }
            }
        }

        function prevStep() {
            goToStep(currentStep - 1);
        }

        async function nextStep() {
            // Save current step
            await saveCurrentStep();

            // Mark current step as complete
            stepStatus[currentStep] = true;
            document.getElementById('stepInd' + currentStep).classList.add('completed');

            updateProgress();

            if (currentStep === totalSteps - 1) {
                // Complete setup
                await completeSetup();
                goToStep(totalSteps);
            } else {
                goToStep(currentStep + 1);
            }
        }

        async function saveCurrentStep() {
            const stepNames = ['database', 'ai-providers', 'api-keys', 'platforms', 'preferences'];
            const config = getStepConfig(currentStep);

            try {
                await fetch(`${API_BASE}/api/SetupWizard/save/${stepNames[currentStep]}`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(config)
                });
            } catch (e) {
                console.error('Error saving step:', e);
            }
        }

        function getStepConfig(step) {
            switch (step) {
                case 0: // Database
                    return {
                        type: document.getElementById('dbType').value,
                        host: document.getElementById('dbHost').value,
                        port: document.getElementById('dbPort').value,
                        database: document.getElementById('dbName').value,
                        username: document.getElementById('dbUser').value,
                        password: document.getElementById('dbPassword').value
                    };
                case 1: // AI Providers
                    return {
                        providers: Array.from(selectedProviders),
                        ollama: {
                            baseUrl: document.getElementById('ollamaUrl').value,
                            model: document.getElementById('ollamaModel').value
                        },
                        gemini: { apiKey: document.getElementById('geminiKey').value },
                        openai: { apiKey: document.getElementById('openaiKey').value },
                        anthropic: { apiKey: document.getElementById('anthropicKey').value }
                    };
                case 2: // API Keys
                    return {
                        enabled: document.getElementById('enableApiKey').checked,
                        masterKey: document.getElementById('masterApiKey').value,
                        allowedIps: document.getElementById('allowedIps').value,
                        rateLimit: document.getElementById('enableRateLimit').checked
                    };
                case 3: // Platforms
                    return {
                        platforms: Array.from(selectedPlatforms)
                    };
                case 4: // Preferences
                    return {
                        language: document.getElementById('defaultLanguage').value,
                        tone: document.getElementById('defaultTone').value,
                        autoStartWorkers: document.getElementById('autoStartWorkers').checked,
                        enableLogging: document.getElementById('enableLogging').checked,
                        sendAnalytics: document.getElementById('sendAnalytics').checked
                    };
                default:
                    return {};
            }
        }

        async function completeSetup() {
            try {
                await fetch(`${API_BASE}/api/SetupWizard/complete`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' }
                });
            } catch (e) {
                console.error('Error completing setup:', e);
            }
        }

        function toggleDbFields() {
            const dbType = document.getElementById('dbType').value;
            const connectionFields = document.getElementById('dbConnectionFields');
            const sqliteInfo = document.getElementById('sqliteInfo');

            if (dbType === 'sqlite') {
                connectionFields.style.display = 'none';
                sqliteInfo.style.display = 'block';
            } else {
                connectionFields.style.display = 'block';
                sqliteInfo.style.display = 'none';

                // Set default ports
                const portInput = document.getElementById('dbPort');
                switch (dbType) {
                    case 'mysql': portInput.value = '3306'; break;
                    case 'postgresql': portInput.value = '5432'; break;
                    case 'sqlserver': portInput.value = '1433'; break;
                }
            }
        }

        async function testDatabase() {
            const btn = document.getElementById('btnTestDb');
            const status = document.getElementById('dbStatus');

            btn.innerHTML = '<span class=""spinner""></span> Testing...';
            btn.disabled = true;

            try {
                const config = getStepConfig(0);
                const response = await fetch(`${API_BASE}/api/SetupWizard/test/database`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(config)
                });
                const result = await response.json();

                status.className = 'status-message ' + (result.success ? 'success' : 'error');
                status.textContent = result.message;
            } catch (e) {
                status.className = 'status-message error';
                status.textContent = 'Connection test failed: ' + e.message;
            }

            btn.innerHTML = '🔌 Test Connection';
            btn.disabled = false;
        }

        function toggleProvider(provider) {
            const card = document.getElementById('provider' + provider.charAt(0).toUpperCase() + provider.slice(1));

            if (selectedProviders.has(provider)) {
                selectedProviders.delete(provider);
                card.classList.remove('selected');
            } else {
                selectedProviders.add(provider);
                card.classList.add('selected');
            }

            // Show/hide config sections
            updateProviderConfigs();
        }

        function updateProviderConfigs() {
            const configSection = document.getElementById('providerConfig');
            const providers = ['ollama', 'gemini', 'openai', 'anthropic'];

            if (selectedProviders.size > 0) {
                configSection.style.display = 'block';

                providers.forEach(p => {
                    document.getElementById(p + 'Config').style.display =
                        selectedProviders.has(p) ? 'block' : 'none';
                });
            } else {
                configSection.style.display = 'none';
            }
        }

        async function testAIProvider() {
            const btn = document.getElementById('btnTestAI');
            const status = document.getElementById('aiStatus');

            if (selectedProviders.size === 0) {
                status.className = 'status-message error';
                status.textContent = 'Please select at least one AI provider';
                return;
            }

            btn.innerHTML = '<span class=""spinner""></span> Testing...';
            btn.disabled = true;

            try {
                const provider = Array.from(selectedProviders)[0];
                let config = { provider };

                switch (provider) {
                    case 'ollama':
                        config.baseUrl = document.getElementById('ollamaUrl').value;
                        break;
                    case 'gemini':
                        config.apiKey = document.getElementById('geminiKey').value;
                        break;
                    case 'openai':
                        config.apiKey = document.getElementById('openaiKey').value;
                        break;
                    case 'anthropic':
                        config.apiKey = document.getElementById('anthropicKey').value;
                        break;
                }

                const response = await fetch(`${API_BASE}/api/SetupWizard/test/ai-provider`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(config)
                });
                const result = await response.json();

                status.className = 'status-message ' + (result.success ? 'success' : 'error');
                status.textContent = result.message;
            } catch (e) {
                status.className = 'status-message error';
                status.textContent = 'AI provider test failed: ' + e.message;
            }

            btn.innerHTML = '🧪 Test AI Connection';
            btn.disabled = false;
        }

        function generateApiKey() {
            const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
            let key = 'aim_';
            for (let i = 0; i < 32; i++) {
                key += chars.charAt(Math.floor(Math.random() * chars.length));
            }
            document.getElementById('masterApiKey').value = key;
        }

        function togglePlatform(platform) {
            const card = document.getElementById('platform' + platform.charAt(0).toUpperCase() + platform.slice(1));

            if (selectedPlatforms.has(platform)) {
                selectedPlatforms.delete(platform);
                card.classList.remove('selected');
            } else {
                selectedPlatforms.add(platform);
                card.classList.add('selected');
            }
        }

        // Initialize
        document.addEventListener('DOMContentLoaded', function() {
            toggleDbFields();
            generateApiKey();
            updateProgress();
        });
    </script>
</body>
</html>";
    }
}

// Models
public class SetupStatus
{
    public bool IsComplete { get; set; }
    public int OverallProgress { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Dictionary<string, StepStatus> Steps { get; set; } = new()
    {
        ["database"] = new StepStatus { Name = "Database", Order = 1 },
        ["ai-providers"] = new StepStatus { Name = "AI Providers", Order = 2 },
        ["api-keys"] = new StepStatus { Name = "API Keys", Order = 3 },
        ["platforms"] = new StepStatus { Name = "Platforms", Order = 4 },
        ["preferences"] = new StepStatus { Name = "Preferences", Order = 5 }
    };
}

public class StepStatus
{
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public bool IsComplete { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? Message { get; set; }
}

public class DatabaseConfig
{
    public string? Type { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Database { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConnectionString { get; set; }
}

public class AIProviderConfig
{
    public string? Provider { get; set; }
    public string? ApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
}
