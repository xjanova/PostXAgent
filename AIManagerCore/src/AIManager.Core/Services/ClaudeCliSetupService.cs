using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIManager.Core.Services;

/// <summary>
/// Claude CLI Setup Service - ตรวจสอบ ติดตั้ง และ login อัตโนมัติ
/// เก็บ credentials ไว้สำหรับ auto-login เมื่อ session หมดอายุ
/// </summary>
public class ClaudeCliSetupService
{
    private readonly DebugLogger _logger = DebugLogger.Instance;
    private readonly string _credentialsPath;
    private readonly string _configPath;

    // Cached status
    private ClaudeCliStatus? _cachedStatus;
    private DateTime _lastStatusCheck = DateTime.MinValue;
    private readonly TimeSpan _statusCacheTimeout = TimeSpan.FromMinutes(1);

    public ClaudeCliSetupService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent");

        Directory.CreateDirectory(appDataPath);

        _credentialsPath = Path.Combine(appDataPath, "claude_credentials.enc");
        _configPath = Path.Combine(appDataPath, "claude_config.json");
    }

    #region Status Check

    /// <summary>
    /// Get comprehensive Claude CLI status
    /// </summary>
    public async Task<ClaudeCliStatus> GetStatusAsync(bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedStatus != null &&
            (DateTime.Now - _lastStatusCheck) < _statusCacheTimeout)
        {
            return _cachedStatus;
        }

        var status = new ClaudeCliStatus();

        try
        {
            // 1. Check if Node.js is installed
            status.NodeInstalled = await CheckCommandExistsAsync("node", "--version");
            if (status.NodeInstalled)
            {
                status.NodeVersion = await GetCommandOutputAsync("node", "--version");
            }

            // 2. Check if npm is installed
            status.NpmInstalled = await CheckCommandExistsAsync("npm", "--version");
            if (status.NpmInstalled)
            {
                status.NpmVersion = await GetCommandOutputAsync("npm", "--version");
            }

            // 3. Check if Claude CLI is installed
            status.ClaudeInstalled = await CheckCommandExistsAsync("claude", "--version");
            if (status.ClaudeInstalled)
            {
                status.ClaudeVersion = await GetCommandOutputAsync("claude", "--version");
            }

            // 4. Check login status
            if (status.ClaudeInstalled)
            {
                status.IsLoggedIn = await CheckClaudeLoginStatusAsync();
            }

            // 5. Check for stored credentials
            status.HasStoredCredentials = File.Exists(_credentialsPath);

            _logger.LogInfo("ClaudeSetup", $"Status check complete - CLI: {status.ClaudeInstalled}, LoggedIn: {status.IsLoggedIn}");
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeSetup", "Error checking status", ex);
            status.Error = ex.Message;
        }

        _cachedStatus = status;
        _lastStatusCheck = DateTime.Now;
        return status;
    }

    /// <summary>
    /// Check if Claude is logged in
    /// Claude CLI uses browser-based OAuth - it picks up session from Chrome automatically
    /// </summary>
    private async Task<bool> CheckClaudeLoginStatusAsync()
    {
        try
        {
            // Method 1: Check Claude config directory for auth/session files
            var claudeConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude");

            if (Directory.Exists(claudeConfigPath))
            {
                // Check for any auth-related files
                var configFiles = Directory.GetFiles(claudeConfigPath, "*", SearchOption.AllDirectories);

                foreach (var file in configFiles)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file);
                        // Look for session tokens, auth tokens, or logged in state
                        if (content.Contains("sessionKey") ||
                            content.Contains("accessToken") ||
                            content.Contains("refreshToken") ||
                            content.Contains("\"authenticated\":true") ||
                            content.Contains("\"hasProSubscription\":true") ||
                            content.Contains("claude.ai"))
                        {
                            _logger.LogInfo("ClaudeSetup", $"Found auth in: {Path.GetFileName(file)}");
                            return true;
                        }
                    }
                    catch { }
                }
            }

            // Method 2: Try a simple Claude command to check auth
            // If already logged in via browser, this will work without prompting
            var result = await RunClaudeCommandAsync("--version", timeoutMs: 10000);
            if (result.Success)
            {
                // Check if Claude can actually run (has valid session from browser)
                var testResult = await RunClaudeCommandAsync("-p \"test\" --no-cache", timeoutMs: 15000);

                // If it doesn't error about authentication, we're logged in
                if (testResult.Success ||
                    (!testResult.Output.Contains("login") &&
                     !testResult.Output.Contains("authenticate") &&
                     !testResult.Output.Contains("unauthorized")))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeSetup", "Error checking login status", ex);
            return false;
        }
    }

    #endregion

    #region Installation

    /// <summary>
    /// Install Claude CLI automatically
    /// </summary>
    public async Task<InstallResult> InstallClaudeCliAsync(IProgress<string>? progress = null)
    {
        var result = new InstallResult();

        try
        {
            // Check prerequisites
            var status = await GetStatusAsync(true);

            if (!status.NodeInstalled)
            {
                result.Success = false;
                result.Error = "Node.js is not installed. Please install Node.js first from https://nodejs.org/";
                result.RequiresManualAction = true;
                result.ManualActionUrl = "https://nodejs.org/";
                _logger.LogWarning("ClaudeSetup", "Node.js not installed");
                return result;
            }

            if (!status.NpmInstalled)
            {
                result.Success = false;
                result.Error = "npm is not installed. Please reinstall Node.js.";
                result.RequiresManualAction = true;
                return result;
            }

            progress?.Report("Installing Claude CLI via npm...");
            _logger.LogInfo("ClaudeSetup", "Starting Claude CLI installation");

            // Install globally
            var installResult = await RunCommandAsync("npm", "install -g @anthropic-ai/claude-code", timeoutMs: 300000);

            if (installResult.ExitCode == 0)
            {
                result.Success = true;
                result.Message = "Claude CLI installed successfully!";

                // Verify installation
                var verifyResult = await GetCommandOutputAsync("claude", "--version");
                result.InstalledVersion = verifyResult;

                _logger.LogInfo("ClaudeSetup", $"Claude CLI installed: {verifyResult}");
                progress?.Report($"Installed: Claude CLI {verifyResult}");
            }
            else
            {
                result.Success = false;
                result.Error = installResult.Error ?? "Installation failed";
                _logger.LogError("ClaudeSetup", $"Installation failed: {installResult.Error}");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError("ClaudeSetup", "Installation error", ex);
        }

        return result;
    }

    #endregion

    #region Login & Credentials

    /// <summary>
    /// Store credentials for auto-login (encrypted)
    /// </summary>
    public async Task SaveCredentialsAsync(string email, string password)
    {
        try
        {
            var credentials = new ClaudeCredentials
            {
                Email = email,
                Password = password,
                SavedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(credentials);
            var encrypted = EncryptString(json);

            await File.WriteAllTextAsync(_credentialsPath, encrypted);
            _logger.LogInfo("ClaudeSetup", "Credentials saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeSetup", "Failed to save credentials", ex);
            throw;
        }
    }

    /// <summary>
    /// Load stored credentials
    /// </summary>
    public async Task<ClaudeCredentials?> LoadCredentialsAsync()
    {
        try
        {
            if (!File.Exists(_credentialsPath))
                return null;

            var encrypted = await File.ReadAllTextAsync(_credentialsPath);
            var json = DecryptString(encrypted);

            return JsonSerializer.Deserialize<ClaudeCredentials>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeSetup", "Failed to load credentials", ex);
            return null;
        }
    }

    /// <summary>
    /// Clear stored credentials
    /// </summary>
    public void ClearCredentials()
    {
        try
        {
            if (File.Exists(_credentialsPath))
            {
                File.Delete(_credentialsPath);
                _logger.LogInfo("ClaudeSetup", "Credentials cleared");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeSetup", "Failed to clear credentials", ex);
        }
    }

    /// <summary>
    /// Attempt login with stored credentials
    /// </summary>
    public async Task<LoginResult> LoginWithStoredCredentialsAsync(IProgress<string>? progress = null)
    {
        var result = new LoginResult();

        try
        {
            var credentials = await LoadCredentialsAsync();

            if (credentials == null)
            {
                result.Success = false;
                result.Error = "No stored credentials found";
                result.RequiresManualLogin = true;
                return result;
            }

            return await LoginAsync(credentials.Email, credentials.Password, progress);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError("ClaudeSetup", "Login with stored credentials failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Login to Claude - uses browser session from Chrome
    /// Claude CLI automatically picks up authentication from your browser
    /// If you're logged into claude.ai in Chrome, it will use that session
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string password, IProgress<string>? progress = null)
    {
        var result = new LoginResult();

        try
        {
            progress?.Report("Checking browser session...");
            _logger.LogInfo("ClaudeSetup", "Initiating Claude login via browser session");

            // First check if already logged in via browser
            var isLoggedIn = await CheckClaudeLoginStatusAsync();

            if (isLoggedIn)
            {
                result.Success = true;
                result.Message = "Already logged in via browser session!";
                _logger.LogInfo("ClaudeSetup", "Already authenticated via browser");
                progress?.Report("Already logged in!");
                return result;
            }

            // Save email for reference (password not needed for OAuth)
            if (!string.IsNullOrEmpty(email))
            {
                await SaveCredentialsAsync(email, password ?? "");
            }

            progress?.Report("Opening Claude login in browser...");

            // Run claude login command - this will open browser automatically
            var loginTask = Task.Run(async () =>
            {
                return await RunClaudeCommandAsync("login", timeoutMs: 5000);
            });

            // Also open claude.ai directly in case CLI doesn't open browser
            OpenBrowserForLogin();

            await loginTask;

            result.Success = false; // Not fully logged in yet
            result.RequiresBrowserAuth = true;
            result.Message = "Please login to claude.ai in your browser. The session will be shared automatically.";
            result.Email = email;

            progress?.Report("Login to claude.ai in browser, then click 'Verify Login'");
            _logger.LogInfo("ClaudeSetup", "Browser auth initiated - waiting for user to login");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError("ClaudeSetup", "Login failed", ex);
        }

        return result;
    }

    /// <summary>
    /// Open browser to Claude.ai login page
    /// Once logged in via Chrome, the session is shared with Claude CLI
    /// </summary>
    private void OpenBrowserForLogin()
    {
        try
        {
            // Open claude.ai - logging in here will allow CLI to use the session
            var psi = new ProcessStartInfo
            {
                FileName = "https://claude.ai/login",
                UseShellExecute = true
            };
            Process.Start(psi);
            _logger.LogInfo("ClaudeSetup", "Opened claude.ai login page in browser");
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeSetup", "Failed to open browser", ex);
        }
    }

    /// <summary>
    /// Quick check - just verify if browser session is available
    /// </summary>
    public async Task<bool> CheckBrowserSessionAsync()
    {
        try
        {
            // Try to run a simple command - if browser session exists, it will work
            var result = await RunClaudeCommandAsync("-p \"hi\" --no-cache", timeoutMs: 30000);

            var isAuthenticated = result.Success ||
                (!string.IsNullOrEmpty(result.Output) &&
                 !result.Output.Contains("login") &&
                 !result.Output.Contains("authenticate"));

            if (isAuthenticated)
            {
                _logger.LogInfo("ClaudeSetup", "Browser session verified - Claude CLI is authenticated");
            }

            return isAuthenticated;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Check if login completed after browser auth
    /// </summary>
    public async Task<bool> VerifyLoginCompletedAsync()
    {
        // Clear cache to get fresh status
        _cachedStatus = null;
        var status = await GetStatusAsync(true);
        return status.IsLoggedIn;
    }

    #endregion

    #region Auto-Setup Flow

    /// <summary>
    /// Complete auto-setup: install if needed, login if needed
    /// </summary>
    public async Task<SetupResult> AutoSetupAsync(IProgress<string>? progress = null)
    {
        var result = new SetupResult();

        try
        {
            progress?.Report("Checking Claude CLI status...");
            var status = await GetStatusAsync(true);

            // Step 1: Install if not installed
            if (!status.ClaudeInstalled)
            {
                progress?.Report("Claude CLI not found, installing...");
                var installResult = await InstallClaudeCliAsync(progress);

                if (!installResult.Success)
                {
                    result.Success = false;
                    result.Error = installResult.Error;
                    result.RequiresManualAction = installResult.RequiresManualAction;
                    result.ManualActionUrl = installResult.ManualActionUrl;
                    return result;
                }

                result.WasInstalled = true;
                status = await GetStatusAsync(true);
            }

            result.ClaudeVersion = status.ClaudeVersion;

            // Step 2: Login if not logged in
            if (!status.IsLoggedIn)
            {
                progress?.Report("Not logged in, attempting login...");

                if (status.HasStoredCredentials)
                {
                    var loginResult = await LoginWithStoredCredentialsAsync(progress);
                    result.RequiresBrowserAuth = loginResult.RequiresBrowserAuth;

                    if (loginResult.RequiresBrowserAuth)
                    {
                        result.Success = false;
                        result.Message = "Please complete login in browser, then click 'Verify Login'";
                        return result;
                    }
                }
                else
                {
                    result.Success = false;
                    result.RequiresCredentials = true;
                    result.Message = "Please enter your Claude credentials to login";
                    return result;
                }
            }

            result.Success = true;
            result.IsLoggedIn = true;
            result.Message = "Claude CLI is ready!";

            _logger.LogInfo("ClaudeSetup", "Auto-setup completed successfully");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError("ClaudeSetup", "Auto-setup failed", ex);
        }

        return result;
    }

    #endregion

    #region Helper Methods

    private async Task<bool> CheckCommandExistsAsync(string command, string args)
    {
        try
        {
            // RunCommandAsync now uses cmd.exe on Windows, so PATH is resolved correctly
            var result = await RunCommandAsync(command, args, timeoutMs: 15000);

            _logger.LogDebug("ClaudeSetup", $"CheckCommand [{command} {args}]: ExitCode={result.ExitCode}, Output={result.Output?.Substring(0, Math.Min(50, result.Output?.Length ?? 0))}...");

            return result.ExitCode == 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ClaudeSetup", $"CheckCommand [{command}] exception: {ex.Message}");
            return false;
        }
    }

    private async Task<string> GetCommandOutputAsync(string command, string args)
    {
        try
        {
            // RunCommandAsync now uses cmd.exe on Windows, so PATH is resolved correctly
            var result = await RunCommandAsync(command, args, timeoutMs: 15000);

            _logger.LogDebug("ClaudeSetup", $"GetCommandOutput [{command} {args}]: Success={result.Success}, Output={result.Output?.Substring(0, Math.Min(50, result.Output?.Length ?? 0))}...");

            return result.Output?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ClaudeSetup", $"GetCommandOutput [{command}] exception: {ex.Message}");
            return "";
        }
    }

    private async Task<CommandResult> RunCommandAsync(string command, string args, int timeoutMs = 30000)
    {
        var result = new CommandResult();

        try
        {
            // On Windows, use cmd.exe to properly resolve PATH
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            ProcessStartInfo psi;

            if (isWindows && !command.Contains("\\") && !command.Contains("/") && command != "cmd.exe")
            {
                // Use cmd.exe /c to run the command - this ensures PATH is resolved correctly
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command} {args}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                result.Error = "Failed to start process";
                return result;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = process.WaitForExit(timeoutMs);

            if (!completed)
            {
                process.Kill();
                result.Error = "Command timed out";
                return result;
            }

            result.Output = await outputTask;
            result.Error = await errorTask;
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<CommandResult> RunClaudeCommandAsync(string args, int timeoutMs = 30000, bool waitForExit = true)
    {
        return await RunCommandAsync("claude", args, timeoutMs);
    }

    // Simple encryption using DPAPI (Windows Data Protection)
    private string EncryptString(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encryptedBytes);
    }

    private string DecryptString(string encryptedText)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedText);
        var plainBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    #endregion
}

#region Models

public class ClaudeCliStatus
{
    public bool NodeInstalled { get; set; }
    public string? NodeVersion { get; set; }
    public bool NpmInstalled { get; set; }
    public string? NpmVersion { get; set; }
    public bool ClaudeInstalled { get; set; }
    public string? ClaudeVersion { get; set; }
    public bool IsLoggedIn { get; set; }
    public bool HasStoredCredentials { get; set; }
    public string? Error { get; set; }

    public bool IsReady => ClaudeInstalled && IsLoggedIn;
}

public class ClaudeCredentials
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public DateTime SavedAt { get; set; }
}

public class InstallResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? InstalledVersion { get; set; }
    public bool RequiresManualAction { get; set; }
    public string? ManualActionUrl { get; set; }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? Email { get; set; }
    public bool RequiresManualLogin { get; set; }
    public bool RequiresBrowserAuth { get; set; }
}

public class SetupResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? ClaudeVersion { get; set; }
    public bool WasInstalled { get; set; }
    public bool IsLoggedIn { get; set; }
    public bool RequiresCredentials { get; set; }
    public bool RequiresBrowserAuth { get; set; }
    public bool RequiresManualAction { get; set; }
    public string? ManualActionUrl { get; set; }
}

public class CommandResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int ExitCode { get; set; }
}

#endregion
