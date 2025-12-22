using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;
using AIManager.Core.Services;

namespace AIManager.UI.Views;

/// <summary>
/// Claude Code Communication Window
/// ใช้ Claude CLI โดยตรง - ใช้ Max Plan subscription ไม่มีค่าใช้จ่ายเพิ่ม
/// พร้อมระบบ auto-install และ auto-login
/// </summary>
public partial class ClaudeChatWindow : Window
{
    private readonly string _projectPath;
    private readonly DebugLogger _logger = DebugLogger.Instance;
    private readonly ClaudeCliSetupService _setupService;
    private Process? _claudeProcess;
    private bool _isProcessing = false;
    private CancellationTokenSource? _cancellationTokenSource;

    // Status colors
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(255, 193, 7));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(244, 67, 54));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(158, 158, 158));

    public ClaudeChatWindow()
    {
        InitializeComponent();
        _logger.LogInfo("ClaudeChatWindow", "Window opened");
        _setupService = new ClaudeCliSetupService();

        // Set project path for Claude CLI context
        _projectPath = @"D:\Code\PostXAgent";

        // Initial check with auto-setup
        Loaded += async (s, e) => await InitializeClaudeAsync();
        Closed += (s, e) =>
        {
            _logger.LogInfo("ClaudeChatWindow", "Window closed");
            CancelCurrentProcess();
        };
    }

    #region Auto-Setup Flow

    private async Task InitializeClaudeAsync()
    {
        try
        {
            // First check status
            var status = await _setupService.GetStatusAsync(true);
            UpdateStatusUI(status);

            // If not ready, show setup overlay
            if (!status.IsReady)
            {
                ShowSetupOverlay(status);
            }
            else
            {
                // Ready to use
                await CheckClaudeStatusAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeChatWindow", "Initialization error", ex);
            ShowSetupError(ex.Message);
        }
    }

    private void UpdateStatusUI(ClaudeCliStatus status)
    {
        // Node.js
        if (status.NodeInstalled)
        {
            IconNode.Kind = PackIconKind.CheckCircle;
            IconNode.Foreground = GreenBrush;
            TxtNodeVersion.Text = status.NodeVersion ?? "Installed";
        }
        else
        {
            IconNode.Kind = PackIconKind.CloseCircle;
            IconNode.Foreground = RedBrush;
            TxtNodeVersion.Text = "Not installed";
        }

        // npm
        if (status.NpmInstalled)
        {
            IconNpm.Kind = PackIconKind.CheckCircle;
            IconNpm.Foreground = GreenBrush;
            TxtNpmVersion.Text = status.NpmVersion ?? "Installed";
        }
        else
        {
            IconNpm.Kind = PackIconKind.CloseCircle;
            IconNpm.Foreground = RedBrush;
            TxtNpmVersion.Text = "Not installed";
        }

        // Claude CLI
        if (status.ClaudeInstalled)
        {
            IconClaude.Kind = PackIconKind.CheckCircle;
            IconClaude.Foreground = GreenBrush;
            TxtClaudeVersion.Text = status.ClaudeVersion ?? "Installed";
        }
        else
        {
            IconClaude.Kind = PackIconKind.CloseCircle;
            IconClaude.Foreground = RedBrush;
            TxtClaudeVersion.Text = "Not installed";
        }

        // Login Status
        if (status.IsLoggedIn)
        {
            IconLogin.Kind = PackIconKind.CheckCircle;
            IconLogin.Foreground = GreenBrush;
            TxtLoginStatus.Text = "Logged In";
        }
        else if (status.ClaudeInstalled)
        {
            IconLogin.Kind = PackIconKind.AlertCircle;
            IconLogin.Foreground = YellowBrush;
            TxtLoginStatus.Text = status.HasStoredCredentials ? "Session expired" : "Not logged in";
        }
        else
        {
            IconLogin.Kind = PackIconKind.MinusCircle;
            IconLogin.Foreground = GrayBrush;
            TxtLoginStatus.Text = "N/A";
        }
    }

    private void ShowSetupOverlay(ClaudeCliStatus status)
    {
        SetupOverlay.Visibility = Visibility.Visible;
        SetupErrorPanel.Visibility = Visibility.Collapsed;
        SetupProgressPanel.Visibility = Visibility.Collapsed;

        // Determine what action is needed
        if (!status.NodeInstalled)
        {
            TxtSetupSubtitle.Text = "Node.js is required";
            ShowSetupError("Please install Node.js from https://nodejs.org/ first.");
            BtnInstall.Visibility = Visibility.Collapsed;
            BtnLogin.Visibility = Visibility.Collapsed;
        }
        else if (!status.ClaudeInstalled)
        {
            TxtSetupSubtitle.Text = "Claude CLI not installed";
            BtnInstall.Visibility = Visibility.Visible;
            BtnLogin.Visibility = Visibility.Collapsed;
            CredentialsPanel.Visibility = Visibility.Collapsed;
        }
        else if (!status.IsLoggedIn)
        {
            TxtSetupSubtitle.Text = "Login required";
            BtnInstall.Visibility = Visibility.Collapsed;

            if (status.HasStoredCredentials)
            {
                // Try auto-login with stored credentials
                _ = TryAutoLoginAsync();
            }
            else
            {
                // Show credentials form
                CredentialsPanel.Visibility = Visibility.Visible;
                BtnLogin.Visibility = Visibility.Visible;
            }
        }
    }

    private async Task TryAutoLoginAsync()
    {
        SetupProgressPanel.Visibility = Visibility.Visible;
        TxtSetupProgress.Text = "Attempting auto-login with stored credentials...";

        try
        {
            var result = await _setupService.LoginWithStoredCredentialsAsync(new Progress<string>(msg =>
            {
                Dispatcher.Invoke(() => TxtSetupProgress.Text = msg);
            }));

            if (result.RequiresBrowserAuth)
            {
                TxtSetupProgress.Text = "Please complete login in browser...";
                BtnVerifyLogin.Visibility = Visibility.Visible;
            }
            else if (result.Success)
            {
                await CompleteSetupAsync();
            }
            else
            {
                // Show manual login
                SetupProgressPanel.Visibility = Visibility.Collapsed;
                CredentialsPanel.Visibility = Visibility.Visible;
                BtnLogin.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeChatWindow", "Auto-login failed", ex);
            SetupProgressPanel.Visibility = Visibility.Collapsed;
            CredentialsPanel.Visibility = Visibility.Visible;
            BtnLogin.Visibility = Visibility.Visible;
        }
    }

    private void ShowSetupError(string message)
    {
        SetupErrorPanel.Visibility = Visibility.Visible;
        TxtSetupError.Text = message;
        BtnRetry.Visibility = Visibility.Visible;
    }

    private async Task CompleteSetupAsync()
    {
        SetupProgressPanel.Visibility = Visibility.Visible;
        TxtSetupProgress.Text = "Setup complete! Loading...";

        await Task.Delay(1000);

        SetupOverlay.Visibility = Visibility.Collapsed;
        await CheckClaudeStatusAsync();
    }

    #endregion

    #region Setup Button Handlers

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        BtnInstall.IsEnabled = false;
        SetupProgressPanel.Visibility = Visibility.Visible;
        SetupErrorPanel.Visibility = Visibility.Collapsed;
        TxtSetupProgress.Text = "Installing Claude CLI...";

        try
        {
            var result = await _setupService.InstallClaudeCliAsync(new Progress<string>(msg =>
            {
                Dispatcher.Invoke(() => TxtSetupProgress.Text = msg);
            }));

            if (result.Success)
            {
                // Refresh status
                var status = await _setupService.GetStatusAsync(true);
                UpdateStatusUI(status);

                if (!status.IsLoggedIn)
                {
                    TxtSetupSubtitle.Text = "Login required";
                    CredentialsPanel.Visibility = Visibility.Visible;
                    BtnLogin.Visibility = Visibility.Visible;
                    BtnInstall.Visibility = Visibility.Collapsed;
                    SetupProgressPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    await CompleteSetupAsync();
                }
            }
            else
            {
                ShowSetupError(result.Error ?? "Installation failed");
            }
        }
        catch (Exception ex)
        {
            ShowSetupError(ex.Message);
        }
        finally
        {
            BtnInstall.IsEnabled = true;
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        var email = TxtEmail.Text.Trim();
        var password = TxtPassword.Password;

        if (string.IsNullOrEmpty(email))
        {
            ShowSetupError("Please enter your email");
            return;
        }

        BtnLogin.IsEnabled = false;
        SetupProgressPanel.Visibility = Visibility.Visible;
        SetupErrorPanel.Visibility = Visibility.Collapsed;
        TxtSetupProgress.Text = "Initiating login...";

        try
        {
            // Save credentials if requested
            if (ChkRememberCredentials.IsChecked == true && !string.IsNullOrEmpty(password))
            {
                await _setupService.SaveCredentialsAsync(email, password);
            }

            var result = await _setupService.LoginAsync(email, password, new Progress<string>(msg =>
            {
                Dispatcher.Invoke(() => TxtSetupProgress.Text = msg);
            }));

            if (result.RequiresBrowserAuth)
            {
                TxtSetupProgress.Text = "Browser opened - please complete login there...";
                BtnVerifyLogin.Visibility = Visibility.Visible;
                BtnLogin.Visibility = Visibility.Collapsed;
                CredentialsPanel.Visibility = Visibility.Collapsed;
            }
            else if (result.Success)
            {
                await CompleteSetupAsync();
            }
            else
            {
                ShowSetupError(result.Error ?? "Login failed");
            }
        }
        catch (Exception ex)
        {
            ShowSetupError(ex.Message);
        }
        finally
        {
            BtnLogin.IsEnabled = true;
        }
    }

    private async void VerifyLogin_Click(object sender, RoutedEventArgs e)
    {
        BtnVerifyLogin.IsEnabled = false;
        TxtSetupProgress.Text = "Verifying login status...";

        try
        {
            var isLoggedIn = await _setupService.VerifyLoginCompletedAsync();

            if (isLoggedIn)
            {
                var status = await _setupService.GetStatusAsync(true);
                UpdateStatusUI(status);
                await CompleteSetupAsync();
            }
            else
            {
                TxtSetupProgress.Text = "Login not yet completed. Please complete in browser and try again.";
            }
        }
        catch (Exception ex)
        {
            ShowSetupError(ex.Message);
        }
        finally
        {
            BtnVerifyLogin.IsEnabled = true;
        }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        SetupErrorPanel.Visibility = Visibility.Collapsed;
        BtnRetry.Visibility = Visibility.Collapsed;
        await InitializeClaudeAsync();
    }

    private void CloseSetup_Click(object sender, RoutedEventArgs e)
    {
        SetupOverlay.Visibility = Visibility.Collapsed;
    }

    #endregion

    private async Task CheckClaudeStatusAsync()
    {
        try
        {
            var (installed, version) = await CheckClaudeCliAsync();

            if (installed)
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                TxtStatus.Text = $"Claude CLI v{version} - Using Max Plan (no extra cost)";
                PendingBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                TxtStatus.Text = "Claude CLI not installed - Run: npm install -g @anthropic-ai/claude-code";
                PendingBadge.Visibility = Visibility.Visible;
                TxtPendingCount.Text = "Not Installed";
            }
        }
        catch (Exception ex)
        {
            StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            TxtStatus.Text = $"Error: {ex.Message}";
        }
    }

    private async Task<(bool installed, string version)> CheckClaudeCliAsync()
    {
        return await Task.Run(() =>
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
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit(5000);

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        return (true, output);
                    }
                }
            }
            catch { }

            return (false, "");
        });
    }

    private void AddMessageToUI(string from, string content, DateTime timestamp)
    {
        var border = new Border();
        var stack = new StackPanel();

        // Header with icon and name
        var headerStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };

        var iconBorder = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, 8, 0)
        };

        var icon = new PackIcon { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        var nameText = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var timeText = new TextBlock { FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        timeText.Text = timestamp.ToString("HH:mm");

        switch (from.ToLower())
        {
            case "claude":
                border.Style = (Style)FindResource("ClaudeMessageBorder");
                iconBorder.Background = new SolidColorBrush(Color.FromRgb(139, 92, 246)); // Purple
                icon.Kind = PackIconKind.CodeBraces;
                nameText.Text = "Claude Code";
                nameText.Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250));
                break;

            case "user":
                border.Style = (Style)FindResource("UserMessageBorder");
                iconBorder.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Green
                icon.Kind = PackIconKind.Account;
                nameText.Text = "You";
                nameText.Foreground = new SolidColorBrush(Color.FromRgb(52, 211, 153));
                break;

            case "system":
                border.Style = (Style)FindResource("LocalAiMessageBorder");
                iconBorder.Background = new SolidColorBrush(Color.FromRgb(107, 114, 128)); // Gray
                icon.Kind = PackIconKind.Information;
                nameText.Text = "System";
                nameText.Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175));
                break;

            default:
                border.Style = (Style)FindResource("LocalAiMessageBorder");
                iconBorder.Background = new SolidColorBrush(Color.FromRgb(6, 182, 212)); // Cyan
                icon.Kind = PackIconKind.Robot;
                nameText.Text = from;
                nameText.Foreground = new SolidColorBrush(Color.FromRgb(34, 211, 238));
                break;
        }

        iconBorder.Child = icon;
        headerStack.Children.Add(iconBorder);
        headerStack.Children.Add(nameText);
        headerStack.Children.Add(timeText);

        // Content - use TextBox for selectable text
        var contentText = new TextBox
        {
            Text = content,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = Brushes.White
        };

        stack.Children.Add(headerStack);
        stack.Children.Add(contentText);
        border.Child = stack;

        MessagesPanel.Children.Add(border);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await CheckClaudeStatusAsync();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start("explorer.exe", _projectPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cannot open folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TxtMessage_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            Send_Click(sender, e);
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var message = TxtMessage.Text.Trim();
        if (string.IsNullOrEmpty(message) || _isProcessing)
            return;

        _logger.LogInfo("ClaudeChatWindow", $"Sending message: {message.Substring(0, Math.Min(50, message.Length))}...");

        // Clear input
        TxtMessage.Text = "";

        // Add user message to UI
        AddMessageToUI("user", message, DateTime.Now);
        ChatScroller.ScrollToEnd();

        // Show loading
        _isProcessing = true;
        LoadingOverlay.Visibility = Visibility.Visible;
        BtnSend.IsEnabled = false;
        TxtLoadingMessage.Text = "Claude is thinking...";

        try
        {
            // Send to Claude CLI
            var response = await SendToClaudeAsync(message);
            _logger.LogInfo("ClaudeChatWindow", $"Received response: {response.Length} chars");

            // Add Claude's response
            AddMessageToUI("claude", response, DateTime.Now);
            ChatScroller.ScrollToEnd();
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ClaudeChatWindow", "Request cancelled by user");
            AddMessageToUI("system", "Request cancelled.", DateTime.Now);
        }
        catch (Exception ex)
        {
            _logger.LogError("ClaudeChatWindow", "Error sending message", ex);
            AddMessageToUI("system", $"Error: {ex.Message}", DateTime.Now);
        }
        finally
        {
            _isProcessing = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            BtnSend.IsEnabled = true;
        }
    }

    private async Task<string> SendToClaudeAsync(string message)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var ct = _cancellationTokenSource.Token;

        return await Task.Run(async () =>
        {
            var output = new StringBuilder();

            try
            {
                // Escape the message for command line - escape double quotes and wrap
                var escapedMessage = message.Replace("\"", "\\\"").Replace("\r\n", " ").Replace("\n", " ");

                // Use cmd.exe /c on Windows to properly resolve PATH
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c claude -p \"{escapedMessage}\"",
                    WorkingDirectory = _projectPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                _claudeProcess = Process.Start(psi);

                if (_claudeProcess == null)
                    throw new Exception("Failed to start Claude CLI");

                // Read output with cancellation support
                var outputTask = _claudeProcess.StandardOutput.ReadToEndAsync();
                var errorTask = _claudeProcess.StandardError.ReadToEndAsync();

                // Wait for process with timeout (5 minutes max)
                var completedTask = await Task.WhenAny(
                    Task.WhenAll(outputTask, errorTask),
                    Task.Delay(TimeSpan.FromMinutes(5), ct)
                );

                if (ct.IsCancellationRequested)
                {
                    _claudeProcess?.Kill();
                    throw new OperationCanceledException();
                }

                var stdOut = await outputTask;
                var stdErr = await errorTask;

                _claudeProcess.WaitForExit(5000);

                if (!string.IsNullOrEmpty(stdOut))
                {
                    output.Append(stdOut);
                }

                if (!string.IsNullOrEmpty(stdErr) && _claudeProcess.ExitCode != 0)
                {
                    output.AppendLine();
                    output.AppendLine($"[Error]: {stdErr}");
                }

                if (output.Length == 0)
                {
                    output.Append("(No response from Claude)");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                output.AppendLine($"Error communicating with Claude: {ex.Message}");
                output.AppendLine();
                output.AppendLine("Make sure:");
                output.AppendLine("1. Claude CLI is installed: npm install -g @anthropic-ai/claude-code");
                output.AppendLine("2. You are logged in: claude login");
                output.AppendLine("3. You have an active Max Plan subscription");
            }
            finally
            {
                _claudeProcess = null;
            }

            return output.ToString().Trim();
        }, ct);
    }

    private void CancelCurrentProcess()
    {
        try
        {
            _cancellationTokenSource?.Cancel();
            _claudeProcess?.Kill();
        }
        catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        CancelCurrentProcess();
        base.OnClosing(e);
    }
}
