using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using AIManager.Core.Services;

namespace AIManager.UI.Controls;

/// <summary>
/// Status Bar - แถบสถานะด้านล่างแสดงการเชื่อมต่อและทรัพยากรระบบ
/// </summary>
public partial class StatusBar : UserControl
{
    private readonly DispatcherTimer _updateTimer;
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly HuggingFaceModelManager? _hfManager;
    private readonly GpuRentalService? _gpuService;
    private readonly CoreDatabaseService? _coreDb;
    private readonly ComfyUIService _comfyService = new();
    private int _alertCount = 0;

    // Status colors
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(255, 193, 7));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(244, 67, 54));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(158, 158, 158));
    private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(0, 188, 212));
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(33, 150, 243));

    // Service URLs
    private readonly string _ollamaUrl = "http://localhost:11434";
    private readonly string _backendUrl = "http://localhost:8000";

    // Claude Code status
    private bool _claudeConnected = false;
    private DateTime? _claudeLastChecked;
    private string? _claudeTokenInfo;

    // Events
    public event EventHandler? OllamaClicked;
    public event EventHandler? ComfyClicked;
    public event EventHandler? HuggingFaceClicked;
    public event EventHandler? GpuClicked;
    public event EventHandler? BackendClicked;
    public event EventHandler? ClaudeClicked;
    public event EventHandler? AlertsClicked;

    public StatusBar()
    {
        InitializeComponent();

        // Try to get services from DI
        _hfManager = App.Services?.GetService<HuggingFaceModelManager>();
        _gpuService = App.Services?.GetService<GpuRentalService>();
        _coreDb = App.Services?.GetService<CoreDatabaseService>();

        // Subscribe to database events
        if (_coreDb != null)
        {
            _coreDb.StatusChanged += CoreDb_StatusChanged;
            _coreDb.StatsUpdated += CoreDb_StatsUpdated;
            _coreDb.AlertRaised += CoreDb_AlertRaised;
        }

        // Setup update timer (every 10 seconds)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += StatusBar_Loaded;
        Unloaded += StatusBar_Unloaded;
    }

    private void CoreDb_StatusChanged(object? sender, DatabaseStatus status)
    {
        Dispatcher.Invoke(() =>
        {
            if (_coreDb == null) return;

            // Update SQLite indicator
            SqliteIndicator.Fill = _coreDb.SqliteStatus switch
            {
                Core.Services.DatabaseStatus.Connected => GreenBrush,
                Core.Services.DatabaseStatus.Connecting => YellowBrush,
                Core.Services.DatabaseStatus.Syncing => CyanBrush,
                Core.Services.DatabaseStatus.Error => RedBrush,
                _ => GrayBrush
            };

            // Update MySQL indicator
            if (_coreDb.MysqlEnabled)
            {
                MysqlBadge.Visibility = Visibility.Visible;
                MysqlIndicator.Fill = _coreDb.MysqlStatus switch
                {
                    Core.Services.DatabaseStatus.Connected => GreenBrush,
                    Core.Services.DatabaseStatus.Connecting => YellowBrush,
                    Core.Services.DatabaseStatus.Syncing => BlueBrush,
                    Core.Services.DatabaseStatus.Error => RedBrush,
                    _ => GrayBrush
                };
            }
            else
            {
                MysqlBadge.Visibility = Visibility.Collapsed;
            }

            // Update tooltip
            var tooltip = $"SQLite: {_coreDb.SqliteStatus}";
            if (_coreDb.MysqlEnabled)
            {
                tooltip += $"\nMySQL: {_coreDb.MysqlStatus}";
            }
            DatabaseStatus.ToolTip = tooltip;
        });
    }

    private void CoreDb_StatsUpdated(object? sender, ConnectionStats stats)
    {
        Dispatcher.Invoke(() =>
        {
            TxtConnections.Text = stats.ActiveConnections.ToString();
            TxtRequests.Text = FormatNumber(stats.TotalRequests);

            // Update tooltip with more details
            TxtConnections.ToolTip = $"Active: {stats.ActiveConnections}\nUptime: {FormatUptime(stats.Uptime)}";
            TxtRequests.ToolTip = $"Total: {stats.TotalRequests:N0}\nSuccess: {stats.SuccessfulRequests:N0}\nFailed: {stats.FailedRequests:N0}";
        });
    }

    private void CoreDb_AlertRaised(object? sender, IpUsageAlert alert)
    {
        _alertCount++;
        Dispatcher.Invoke(() =>
        {
            AlertsBadge.Visibility = Visibility.Visible;
            TxtAlerts.Text = _alertCount.ToString();
            AlertsBadge.ToolTip = $"Latest: {alert.Message}";
        });
    }

    private static string FormatNumber(long number)
    {
        return number switch
        {
            >= 1000000 => $"{number / 1000000.0:F1}M",
            >= 1000 => $"{number / 1000.0:F1}K",
            _ => number.ToString()
        };
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    private async void StatusBar_Loaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Start();
        await RefreshAllStatusAsync();
        UpdateMemoryUsage();
    }

    private void StatusBar_Unloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
    }

    private async void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        await RefreshAllStatusAsync();
        UpdateMemoryUsage();
    }

    /// <summary>
    /// รีเฟรชสถานะทั้งหมด
    /// </summary>
    public async Task RefreshAllStatusAsync()
    {
        await Task.WhenAll(
            CheckOllamaAsync(),
            CheckComfyUIAsync(),
            CheckHuggingFaceAsync(),
            CheckGpuProviderAsync(),
            CheckBackendAsync(),
            CheckClaudeCodeAsync()
        );
    }

    private async Task CheckOllamaAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_ollamaUrl}/api/tags");
            Dispatcher.Invoke(() =>
            {
                OllamaIndicator.Fill = response.IsSuccessStatusCode ? GreenBrush : RedBrush;
                OllamaStatus.ToolTip = response.IsSuccessStatusCode ? "Ollama: Running" : "Ollama: Offline";
            });
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                OllamaIndicator.Fill = RedBrush;
                OllamaStatus.ToolTip = "Ollama: Offline";
            });
        }
    }

    private async Task CheckComfyUIAsync()
    {
        try
        {
            var isAvailable = await _comfyService.IsAvailableAsync();
            Dispatcher.Invoke(() =>
            {
                ComfyIndicator.Fill = isAvailable ? GreenBrush : GrayBrush;
                ComfyStatus.ToolTip = isAvailable ? "ComfyUI: Running" : "ComfyUI: Not running";
            });
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                ComfyIndicator.Fill = GrayBrush;
                ComfyStatus.ToolTip = "ComfyUI: Not running";
            });
        }
    }

    private async Task CheckHuggingFaceAsync()
    {
        if (_hfManager == null)
        {
            Dispatcher.Invoke(() =>
            {
                HFIndicator.Fill = GrayBrush;
                TxtHFStatus.Text = "HF";
                HFStatus.ToolTip = "HuggingFace: Not configured";
            });
            return;
        }

        try
        {
            if (_hfManager.HasToken)
            {
                var status = await _hfManager.ValidateTokenAsync();
                Dispatcher.Invoke(() =>
                {
                    if (status.IsValid)
                    {
                        HFIndicator.Fill = GreenBrush;
                        TxtHFStatus.Text = status.Username ?? "HF";
                        HFStatus.ToolTip = $"HuggingFace: {status.Message}";
                    }
                    else
                    {
                        HFIndicator.Fill = YellowBrush;
                        TxtHFStatus.Text = "HF (!)";
                        HFStatus.ToolTip = $"HuggingFace: {status.Message}";
                    }
                });
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    HFIndicator.Fill = GrayBrush;
                    TxtHFStatus.Text = "HF";
                    HFStatus.ToolTip = "HuggingFace: No token (click to configure)";
                });
            }
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                HFIndicator.Fill = RedBrush;
                TxtHFStatus.Text = "HF (!)";
                HFStatus.ToolTip = "HuggingFace: Error";
            });
        }
    }

    private async Task CheckGpuProviderAsync()
    {
        if (_gpuService == null)
        {
            Dispatcher.Invoke(() =>
            {
                TxtGpuStatus.Text = "Local GPU";
            });
            return;
        }

        // Check for configured GPU providers
        await Task.Run(() =>
        {
            var recommendations = _gpuService.GetRecommendedProviders();
            var configured = recommendations.Where(r => r.IsConfigured).ToList();

            Dispatcher.Invoke(() =>
            {
                if (configured.Any())
                {
                    var first = configured.First();
                    TxtGpuStatus.Text = first.Provider.Name;
                    GpuStatus.ToolTip = $"GPU: {first.Provider.Description}";
                }
                else
                {
                    TxtGpuStatus.Text = "No GPU Cloud";
                    GpuStatus.ToolTip = "Click to configure GPU rental (Kaggle, Colab, etc.)";
                }
            });
        });
    }

    private async Task CheckBackendAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_backendUrl}/api/health");
            Dispatcher.Invoke(() =>
            {
                BackendIndicator.Fill = response.IsSuccessStatusCode ? GreenBrush : RedBrush;
                BackendStatus.ToolTip = response.IsSuccessStatusCode ? "Laravel Backend: Running" : "Laravel Backend: Error";
            });
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                BackendIndicator.Fill = GrayBrush;
                BackendStatus.ToolTip = "Laravel Backend: Offline";
            });
        }
    }

    private async Task CheckClaudeCodeAsync()
    {
        try
        {
            // Check if Claude CLI is installed and get version
            var cliInstalled = false;
            var cliVersion = "";

            await Task.Run(() =>
            {
                try
                {
                    // Use cmd.exe /c on Windows to properly resolve PATH
                    var psi = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c claude --version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit(3000);

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            cliInstalled = true;
                            cliVersion = output.Trim();
                        }
                    }
                }
                catch
                {
                    cliInstalled = false;
                }
            });

            // Check if there's an active Claude Code session (look for lock file or process)
            var claudeRunning = false;
            await Task.Run(() =>
            {
                try
                {
                    // Check for claude process
                    var processes = Process.GetProcessesByName("claude");
                    claudeRunning = processes.Length > 0;

                    // Also check for node processes that might be claude
                    if (!claudeRunning)
                    {
                        // Check Claude config directory for active session
                        var claudeConfigPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".claude");

                        if (Directory.Exists(claudeConfigPath))
                        {
                            // Check for recent activity (modified within last 5 minutes)
                            var configFiles = Directory.GetFiles(claudeConfigPath, "*.json", SearchOption.AllDirectories);
                            claudeRunning = configFiles.Any(f =>
                                (DateTime.Now - File.GetLastWriteTime(f)).TotalMinutes < 5);
                        }
                    }
                }
                catch
                {
                    claudeRunning = false;
                }
            });

            _claudeConnected = cliInstalled;
            _claudeLastChecked = DateTime.Now;

            Dispatcher.Invoke(() =>
            {
                if (cliInstalled && claudeRunning)
                {
                    // CLI installed and active session
                    ClaudeIndicator.Fill = GreenBrush;
                    TxtClaudeStatus.Text = "Claude";
                    TokenBadge.Visibility = Visibility.Visible;
                    TxtTokenUsage.Text = "Active";
                    TxtTokenUsage.Foreground = GreenBrush;
                    TokenBadge.ToolTip = "Claude Code session active\nUsing your Max Plan (no extra cost)";
                    ClaudeStatus.ToolTip = $"Claude Code: Active\nVersion: {cliVersion}\nUsing Max Plan subscription\nLast checked: {_claudeLastChecked:HH:mm:ss}";
                }
                else if (cliInstalled)
                {
                    // CLI installed but no active session
                    ClaudeIndicator.Fill = YellowBrush;
                    TxtClaudeStatus.Text = "Claude";
                    TokenBadge.Visibility = Visibility.Visible;
                    TxtTokenUsage.Text = "Idle";
                    TxtTokenUsage.Foreground = YellowBrush;
                    TokenBadge.ToolTip = "Claude CLI ready\nClick to open chat window";
                    ClaudeStatus.ToolTip = $"Claude Code: Installed (idle)\nVersion: {cliVersion}\nUsing Max Plan subscription\nLast checked: {_claudeLastChecked:HH:mm:ss}";
                }
                else
                {
                    // CLI not installed
                    ClaudeIndicator.Fill = GrayBrush;
                    TxtClaudeStatus.Text = "Claude";
                    TokenBadge.Visibility = Visibility.Visible;
                    TxtTokenUsage.Text = "Not Installed";
                    TxtTokenUsage.Foreground = RedBrush;
                    TokenBadge.ToolTip = "Claude CLI not installed\nRun: npm install -g @anthropic-ai/claude-code";
                    ClaudeStatus.ToolTip = "Claude Code: Not installed\nInstall with: npm install -g @anthropic-ai/claude-code";
                }
            });
        }
        catch (Exception ex)
        {
            _claudeConnected = false;
            Dispatcher.Invoke(() =>
            {
                ClaudeIndicator.Fill = GrayBrush;
                TxtClaudeStatus.Text = "Claude";
                TokenBadge.Visibility = Visibility.Collapsed;
                ClaudeStatus.ToolTip = $"Claude: Error - {ex.Message}";
            });
        }
    }

    private async Task CheckClaudeTokenUsageAsync()
    {
        // This method is now handled in CheckClaudeCodeAsync
        await Task.CompletedTask;
    }

    /// <summary>
    /// แสดงสถานะการเชื่อมต่อ Claude ด้วย token info
    /// </summary>
    public void UpdateClaudeStatus(bool connected, string? tokenInfo = null, double? usagePercent = null)
    {
        _claudeConnected = connected;
        _claudeTokenInfo = tokenInfo;
        _claudeLastChecked = DateTime.Now;

        Dispatcher.Invoke(() =>
        {
            if (connected)
            {
                ClaudeIndicator.Fill = GreenBrush;
                TxtClaudeStatus.Text = "Claude";

                if (usagePercent.HasValue)
                {
                    TokenBadge.Visibility = Visibility.Visible;
                    TxtTokenUsage.Text = $"{usagePercent:F0}%";

                    // Color based on usage
                    if (usagePercent < 50)
                        TxtTokenUsage.Foreground = GreenBrush;
                    else if (usagePercent < 80)
                        TxtTokenUsage.Foreground = YellowBrush;
                    else
                        TxtTokenUsage.Foreground = RedBrush;

                    TokenBadge.ToolTip = $"Token usage: {usagePercent:F1}%\n{tokenInfo ?? "Using your Anthropic account"}";
                }
                else
                {
                    TokenBadge.Visibility = Visibility.Visible;
                    TxtTokenUsage.Text = "Active";
                    TxtTokenUsage.Foreground = GreenBrush;
                    TokenBadge.ToolTip = tokenInfo ?? "Claude Code session active";
                }

                ClaudeStatus.ToolTip = $"Claude Code: Connected\nLast checked: {_claudeLastChecked:HH:mm:ss}";
            }
            else
            {
                ClaudeIndicator.Fill = GrayBrush;
                TxtClaudeStatus.Text = "Claude";
                TokenBadge.Visibility = Visibility.Collapsed;
                ClaudeStatus.ToolTip = tokenInfo ?? "Claude Code: Not connected";
            }
        });
    }

    private void UpdateMemoryUsage()
    {
        var process = Process.GetCurrentProcess();
        var memoryMB = process.WorkingSet64 / (1024 * 1024);

        Dispatcher.Invoke(() =>
        {
            TxtMemory.Text = $"{memoryMB} MB";
        });
    }

    /// <summary>
    /// แสดงกิจกรรมที่กำลังทำงาน
    /// </summary>
    public void ShowActivity(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtActivity.Text = message;
            ActivityPanel.Visibility = Visibility.Visible;
        });
    }

    /// <summary>
    /// ซ่อนกิจกรรม
    /// </summary>
    public void HideActivity()
    {
        Dispatcher.Invoke(() =>
        {
            ActivityPanel.Visibility = Visibility.Collapsed;
        });
    }

    /// <summary>
    /// อัพเดท current task
    /// </summary>
    public void SetCurrentTask(string? taskDescription)
    {
        Dispatcher.Invoke(() =>
        {
            TxtCurrentTask.Text = taskDescription ?? "";
        });
    }

    /// <summary>
    /// ตั้งค่า HuggingFace token
    /// </summary>
    public void SetHuggingFaceToken(string token)
    {
        if (_hfManager != null)
        {
            _hfManager.HuggingFaceToken = token;
            _ = CheckHuggingFaceAsync();
        }
    }

    // Click handlers
    private void OllamaStatus_Click(object sender, MouseButtonEventArgs e)
    {
        OllamaClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ComfyStatus_Click(object sender, MouseButtonEventArgs e)
    {
        ComfyClicked?.Invoke(this, EventArgs.Empty);
    }

    private void HFStatus_Click(object sender, MouseButtonEventArgs e)
    {
        HuggingFaceClicked?.Invoke(this, EventArgs.Empty);
    }

    private void GpuStatus_Click(object sender, MouseButtonEventArgs e)
    {
        GpuClicked?.Invoke(this, EventArgs.Empty);
    }

    private void BackendStatus_Click(object sender, MouseButtonEventArgs e)
    {
        BackendClicked?.Invoke(this, EventArgs.Empty);
    }

    private void ClaudeStatus_Click(object sender, MouseButtonEventArgs e)
    {
        ClaudeClicked?.Invoke(this, EventArgs.Empty);
    }

    private void AlertsBadge_Click(object sender, MouseButtonEventArgs e)
    {
        AlertsClicked?.Invoke(this, EventArgs.Empty);
        // Reset alert count when clicked
        _alertCount = 0;
        AlertsBadge.Visibility = Visibility.Collapsed;
    }
}
