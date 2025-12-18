using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MaterialDesignThemes.Wpf;
using AIManager.Core.Services;

namespace AIManager.UI.Views.Pages;

public partial class SettingsPage : Page
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly SystemResourceDetector _resourceDetector = new();
    private readonly OllamaServiceManager? _ollamaService;
    private readonly ComfyUIService _comfyService = new();
    private readonly AILearningDatabaseService _dbService;
    private string _selectedModel = "auto";
    private SystemResourceDetector.SystemResources? _systemResources;
    private SystemResourceDetector.ModelRecommendation? _recommendation;
    private readonly PaletteHelper _paletteHelper = new();

    // Connection URLs
    private string _backendUrl = "http://localhost:8000";
    private string _ollamaUrl = "http://localhost:11434";
    private string _comfyUrl = "http://127.0.0.1:8188";
    private string _redisUrl = "localhost:6379";

    public SettingsPage()
    {
        InitializeComponent();

        // Get OllamaServiceManager from DI
        _ollamaService = App.Services?.GetService<OllamaServiceManager>();
        if (_ollamaService != null)
        {
            _ollamaService.StatusChanged += OllamaService_StatusChanged;
        }

        // Initialize AI Learning Database service
        var loggerFactory = App.Services?.GetService<ILoggerFactory>();
        var logger = loggerFactory?.CreateLogger<AILearningDatabaseService>()
                     ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AILearningDatabaseService>.Instance;
        _dbService = new AILearningDatabaseService(logger);
        _dbService.StatusChanged += DbService_StatusChanged;
        _dbService.ErrorOccurred += DbService_ErrorOccurred;

        LoadSettings();
        LoadDatabaseSettings();
        CboOllamaModel.SelectionChanged += CboOllamaModel_SelectionChanged;

        // Auto-detect resources and check all connections on load
        Loaded += async (s, e) =>
        {
            DetectResourcesAsync();
            await CheckAllConnectionsAsync();
        };
    }

    private async Task CheckAllConnectionsAsync()
    {
        // Check all connections in parallel
        var tasks = new[]
        {
            CheckBackendConnectionAsync(),
            CheckOllamaConnectionAsync(),
            CheckComfyConnectionAsync(),
            CheckRedisConnectionAsync()
        };

        await Task.WhenAll(tasks);
    }

    private async void RefreshAllConnections_Click(object sender, RoutedEventArgs e)
    {
        BtnRefreshAll.IsEnabled = false;
        await CheckAllConnectionsAsync();
        BtnRefreshAll.IsEnabled = true;
    }

    private async Task CheckBackendConnectionAsync()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                TxtBackendStatus.Text = "Checking...";
                BackendStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
            });

            var response = await _httpClient.GetAsync($"{_backendUrl}/api/health");

            Dispatcher.Invoke(() =>
            {
                if (response.IsSuccessStatusCode)
                {
                    TxtBackendStatus.Text = "Connected";
                    TxtBackendUrl.Text = _backendUrl;
                    BackendStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                    BackendStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                }
                else
                {
                    TxtBackendStatus.Text = $"Error: {response.StatusCode}";
                    TxtBackendUrl.Text = _backendUrl;
                    BackendStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                    BackendStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
                }
            });
        }
        catch (Exception)
        {
            Dispatcher.Invoke(() =>
            {
                TxtBackendStatus.Text = "Offline";
                TxtBackendUrl.Text = _backendUrl;
                BackendStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                BackendStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
            });
        }
    }

    private async Task CheckOllamaConnectionAsync()
    {
        if (_ollamaService != null)
        {
            await UpdateOllamaServiceStatusAsync();
            return;
        }

        try
        {
            Dispatcher.Invoke(() =>
            {
                TxtOllamaServiceStatus.Text = "Checking...";
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            });

            var response = await _httpClient.GetAsync($"{_ollamaUrl}/api/tags");

            Dispatcher.Invoke(() =>
            {
                if (response.IsSuccessStatusCode)
                {
                    TxtOllamaServiceStatus.Text = "Running";
                    OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                }
                else
                {
                    TxtOllamaServiceStatus.Text = "Error";
                    OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
                }
            });
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                TxtOllamaServiceStatus.Text = "Offline";
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
            });
        }
    }

    private async Task CheckComfyConnectionAsync()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                TxtComfyStatus.Text = "Checking...";
                ComfyStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            });

            var isAvailable = await _comfyService.IsAvailableAsync();

            Dispatcher.Invoke(() =>
            {
                if (isAvailable)
                {
                    TxtComfyStatus.Text = "Running";
                    TxtComfyUrl.Text = _comfyUrl;
                    ComfyStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    ComfyStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                }
                else
                {
                    TxtComfyStatus.Text = "Offline";
                    TxtComfyUrl.Text = _comfyUrl;
                    ComfyStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    ComfyStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                }
            });
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                TxtComfyStatus.Text = "Offline";
                TxtComfyUrl.Text = _comfyUrl;
                ComfyStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                ComfyStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
            });
        }
    }

    private async Task CheckRedisConnectionAsync()
    {
        // Note: Simple TCP connection check for Redis
        try
        {
            Dispatcher.Invoke(() =>
            {
                TxtRedisStatus.Text = "Checking...";
                RedisStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7));
            });

            var parts = _redisUrl.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : 6379;

            using var client = new System.Net.Sockets.TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(2000);

            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            var isConnected = completedTask == connectTask && client.Connected;

            Dispatcher.Invoke(() =>
            {
                if (isConnected)
                {
                    TxtRedisStatus.Text = "Running";
                    TxtRedisUrl.Text = _redisUrl;
                    RedisStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    RedisStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                }
                else
                {
                    TxtRedisStatus.Text = "Offline";
                    TxtRedisUrl.Text = _redisUrl;
                    RedisStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    RedisStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                }
            });
        }
        catch
        {
            Dispatcher.Invoke(() =>
            {
                TxtRedisStatus.Text = "Offline";
                TxtRedisUrl.Text = _redisUrl;
                RedisStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                RedisStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
            });
        }
    }

    private void OllamaService_StatusChanged(object? sender, OllamaStatus status)
    {
        Dispatcher.Invoke(() => UpdateOllamaStatusUI(status));
    }

    private async Task UpdateOllamaServiceStatusAsync()
    {
        if (_ollamaService == null) return;

        await _ollamaService.RefreshModelsAsync();
        Dispatcher.Invoke(() =>
        {
            UpdateOllamaStatusUI(_ollamaService.CurrentStatus);
            UpdateModelsDisplay();
        });
    }

    private void UpdateOllamaStatusUI(OllamaStatus status)
    {
        switch (status)
        {
            case OllamaStatus.Running:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233)); // Light green
                TxtOllamaServiceStatus.Text = "Running";
                BtnStartOllama.Visibility = Visibility.Collapsed;
                BtnStopOllama.Visibility = Visibility.Visible;
                break;

            case OllamaStatus.Starting:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Yellow
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 249, 196));
                TxtOllamaServiceStatus.Text = "Starting...";
                BtnStartOllama.Visibility = Visibility.Collapsed;
                BtnStopOllama.Visibility = Visibility.Collapsed;
                break;

            case OllamaStatus.Stopped:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(238, 238, 238));
                TxtOllamaServiceStatus.Text = "Stopped";
                BtnStartOllama.Visibility = Visibility.Visible;
                BtnStopOllama.Visibility = Visibility.Collapsed;
                break;

            case OllamaStatus.Crashed:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                TxtOllamaServiceStatus.Text = "Crashed - restarting...";
                BtnStartOllama.Visibility = Visibility.Visible;
                BtnStopOllama.Visibility = Visibility.Collapsed;
                break;

            case OllamaStatus.NotInstalled:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                TxtOllamaServiceStatus.Text = "Not Installed";
                TxtOllamaModelsLoaded.Text = "ดาวน์โหลดได้ที่: https://ollama.com";
                BtnStartOllama.Visibility = Visibility.Collapsed;
                BtnStopOllama.Visibility = Visibility.Collapsed;
                break;

            case OllamaStatus.Error:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 243, 224));
                TxtOllamaServiceStatus.Text = "Error";
                BtnStartOllama.Visibility = Visibility.Visible;
                BtnStopOllama.Visibility = Visibility.Collapsed;
                break;

            default:
                OllamaStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                OllamaStatusBorder.Background = new SolidColorBrush(Color.FromRgb(238, 238, 238));
                TxtOllamaServiceStatus.Text = status.ToString();
                break;
        }
    }

    private void UpdateModelsDisplay()
    {
        if (_ollamaService == null) return;

        var models = _ollamaService.LoadedModels;
        if (models.Count > 0)
        {
            TxtOllamaModelsLoaded.Text = $"Models: {string.Join(", ", models.Take(5))}" +
                (models.Count > 5 ? $" (+{models.Count - 5} more)" : "");
        }
        else
        {
            TxtOllamaModelsLoaded.Text = "No models installed";
        }
    }

    private async void StartOllama_Click(object sender, RoutedEventArgs e)
    {
        if (_ollamaService == null) return;

        BtnStartOllama.IsEnabled = false;
        await _ollamaService.StartAsync();
        await UpdateOllamaServiceStatusAsync();
        BtnStartOllama.IsEnabled = true;
    }

    private async void StopOllama_Click(object sender, RoutedEventArgs e)
    {
        if (_ollamaService == null) return;

        BtnStopOllama.IsEnabled = false;
        await _ollamaService.StopAsync();
        await UpdateOllamaServiceStatusAsync();
        BtnStopOllama.IsEnabled = true;
    }

    private async void RefreshOllama_Click(object sender, RoutedEventArgs e)
    {
        await UpdateOllamaServiceStatusAsync();
    }

    private void LoadSettings()
    {
        TxtApiPort.Text = Environment.GetEnvironmentVariable("AI_MANAGER_API_PORT") ?? "5000";
        TxtWsPort.Text = Environment.GetEnvironmentVariable("AI_MANAGER_WS_PORT") ?? "5001";
        TxtSignalRPort.Text = Environment.GetEnvironmentVariable("AI_MANAGER_SIGNALR_PORT") ?? "5002";
        TxtRedisConnection.Text = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ?? "localhost:6379";
        TxtOllamaUrl.Text = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";

        _selectedModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "auto";
        CboOllamaModel.Text = _selectedModel;

        // Set selection if model is in list
        foreach (ComboBoxItem item in CboOllamaModel.Items)
        {
            if (item.Content?.ToString() == _selectedModel)
            {
                CboOllamaModel.SelectedItem = item;
                TxtModelDescription.Text = item.Tag?.ToString() ?? "";
                break;
            }
        }

        UpdateAutoSelectedModelDisplay();
    }

    private async void DetectResourcesAsync()
    {
        try
        {
            TxtSystemResources.Text = "Detecting system resources...";

            await Task.Run(() =>
            {
                _systemResources = _resourceDetector.DetectResources();
                _recommendation = _resourceDetector.GetBestModel(_systemResources);
            });

            // Update UI
            Dispatcher.Invoke(() =>
            {
                var res = _systemResources!;
                var gpuInfo = res.HasNvidiaGpu || res.HasAmdGpu
                    ? $"GPU: {res.GpuName} ({res.TotalVramMB / 1000.0:F1}GB VRAM)"
                    : "GPU: ไม่พบ (จะใช้ CPU mode)";

                TxtSystemResources.Text =
                    $"RAM: {res.TotalRamMB / 1000.0:F1}GB (ว่าง: {res.AvailableRamMB / 1000.0:F1}GB)\n" +
                    $"{gpuInfo}\n" +
                    $"CPU: {res.CpuName} ({res.CpuCores} cores)";

                UpdateAutoSelectedModelDisplay();
            });
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                TxtSystemResources.Text = $"ไม่สามารถตรวจจับทรัพยากรได้: {ex.Message}";
            });
        }
    }

    private void UpdateAutoSelectedModelDisplay()
    {
        if (_selectedModel == "auto" && _recommendation != null)
        {
            TxtAutoSelectedModel.Text = $"Model ที่เลือกอัตโนมัติ: {_recommendation.DisplayName}\n{_recommendation.Reason}";
            TxtAutoSelectedModel.Visibility = Visibility.Visible;
        }
        else
        {
            TxtAutoSelectedModel.Visibility = Visibility.Collapsed;
        }
    }

    private void CboOllamaModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CboOllamaModel.SelectedItem is ComboBoxItem selected)
        {
            _selectedModel = selected.Content?.ToString() ?? "auto";
            TxtModelDescription.Text = selected.Tag?.ToString() ?? "";
        }
        else if (!string.IsNullOrEmpty(CboOllamaModel.Text))
        {
            _selectedModel = CboOllamaModel.Text;
            TxtModelDescription.Text = "Custom model";
        }

        UpdateAutoSelectedModelDisplay();
    }

    private void DetectResources_Click(object sender, RoutedEventArgs e)
    {
        DetectResourcesAsync();
    }

    private async void TestOllama_Click(object sender, RoutedEventArgs e)
    {
        BtnTestOllama.IsEnabled = false;
        TxtOllamaStatus.Text = "Testing connection...";
        TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Gray;

        try
        {
            var baseUrl = TxtOllamaUrl.Text.TrimEnd('/');
            var response = await _httpClient.GetAsync($"{baseUrl}/api/tags");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var models = json.GetProperty("models").EnumerateArray().ToList();
                var modelNames = models.Select(m => m.GetProperty("name").GetString()).ToList();

                // Get effective model (resolve auto)
                var effectiveModel = _selectedModel == "auto"
                    ? _recommendation?.ModelName ?? "llama3.1:8b"
                    : _selectedModel;

                var hasModel = modelNames.Any(m => m?.StartsWith(effectiveModel.Split(':')[0]) == true);

                var modelDisplay = _selectedModel == "auto"
                    ? $"auto -> {effectiveModel}"
                    : effectiveModel;

                if (hasModel)
                {
                    TxtOllamaStatus.Text = $"Connected! Model '{modelDisplay}' is available.\n{models.Count} models installed: {string.Join(", ", modelNames.Take(5))}";
                    TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    TxtOllamaStatus.Text = $"Connected! But '{modelDisplay}' not found.\nAvailable: {string.Join(", ", modelNames.Take(5))}\nClick 'Pull Model' to download.";
                    TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Orange;
                }
            }
            else
            {
                TxtOllamaStatus.Text = $"Connection failed: {response.StatusCode}";
                TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        catch (HttpRequestException ex)
        {
            TxtOllamaStatus.Text = $"Cannot connect to Ollama. Make sure Ollama is running.\nError: {ex.Message}";
            TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
        catch (Exception ex)
        {
            TxtOllamaStatus.Text = $"Error: {ex.Message}";
            TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
        finally
        {
            BtnTestOllama.IsEnabled = true;
        }
    }

    private async void PullModel_Click(object sender, RoutedEventArgs e)
    {
        // Get effective model (resolve auto)
        var model = _selectedModel == "auto"
            ? _recommendation?.ModelName ?? "llama3.1:8b"
            : _selectedModel;

        if (string.IsNullOrEmpty(model))
        {
            MessageBox.Show("Please select a model first.", "Pull Model", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var ramRequired = _recommendation?.RequiredRamMB ?? 8000;
        var vramRequired = _recommendation?.RequiredVramMB ?? 6000;

        var result = MessageBox.Show(
            $"This will download the '{model}' model.\n\n" +
            $"Requirements:\n" +
            $"• RAM: {ramRequired / 1000}GB minimum\n" +
            $"• VRAM: {vramRequired / 1000}GB (if using GPU)\n" +
            $"• Disk: Variable (could be 4-50GB+)\n\n" +
            $"Make sure you have enough disk space.\n\nContinue?",
            "Pull Model",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        BtnPullModel.IsEnabled = false;
        TxtOllamaStatus.Text = $"Pulling model '{model}'... This may take a while.";
        TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Blue;

        try
        {
            var baseUrl = TxtOllamaUrl.Text.TrimEnd('/');
            var request = new { name = model, stream = false };

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(60));
            var response = await _httpClient.PostAsJsonAsync($"{baseUrl}/api/pull", request, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                TxtOllamaStatus.Text = $"Model '{model}' pulled successfully!";
                TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Green;

                MessageBox.Show($"Model '{model}' has been downloaded successfully!\n\nYou can now use it for content generation.",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                TxtOllamaStatus.Text = $"Failed to pull model: {error}";
                TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        catch (TaskCanceledException)
        {
            TxtOllamaStatus.Text = "Pull timed out. For large models, use terminal: ollama pull " + model;
            TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Orange;

            MessageBox.Show($"The download is taking longer than expected.\n\nFor large models, please use the terminal:\n\nollama pull {model}",
                "Timeout", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            TxtOllamaStatus.Text = $"Error: {ex.Message}";
            TxtOllamaStatus.Foreground = System.Windows.Media.Brushes.Red;
        }
        finally
        {
            BtnPullModel.IsEnabled = true;
        }
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        // Update the model from ComboBox
        if (CboOllamaModel.SelectedItem is ComboBoxItem selected)
        {
            _selectedModel = selected.Content?.ToString() ?? "auto";
        }
        else if (!string.IsNullOrEmpty(CboOllamaModel.Text))
        {
            _selectedModel = CboOllamaModel.Text;
        }

        // Get effective model for display
        var effectiveModel = _selectedModel == "auto"
            ? _recommendation?.ModelName ?? "llama3.1:8b"
            : _selectedModel;

        // Save to environment (in-memory for this session)
        Environment.SetEnvironmentVariable("AI_MANAGER_API_PORT", TxtApiPort.Text);
        Environment.SetEnvironmentVariable("AI_MANAGER_WS_PORT", TxtWsPort.Text);
        Environment.SetEnvironmentVariable("AI_MANAGER_SIGNALR_PORT", TxtSignalRPort.Text);
        Environment.SetEnvironmentVariable("REDIS_CONNECTION_STRING", TxtRedisConnection.Text);
        Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", TxtOllamaUrl.Text);
        Environment.SetEnvironmentVariable("OLLAMA_MODEL", _selectedModel);
        Environment.SetEnvironmentVariable("OLLAMA_AUTO_SELECT", _selectedModel == "auto" ? "true" : "false");

        var displayModel = _selectedModel == "auto"
            ? $"auto (จะใช้: {effectiveModel})"
            : _selectedModel;

        MessageBox.Show(
            $"Settings saved!\n\n" +
            $"Ollama Model: {displayModel}\n\n" +
            $"Note: For permanent settings, add to your .env file:\n" +
            $"OLLAMA_MODEL={_selectedModel}",
            "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        TxtApiPort.Text = "5000";
        TxtWsPort.Text = "5001";
        TxtSignalRPort.Text = "5002";
        TxtRedisConnection.Text = "localhost:6379";
        TxtOllamaUrl.Text = "http://localhost:11434";

        // Default to auto
        CboOllamaModel.SelectedIndex = 0; // "auto" is first
        _selectedModel = "auto";
        TxtModelDescription.Text = "Auto - เลือกอัตโนมัติตามทรัพยากรระบบ";
        TxtOllamaStatus.Text = "";

        // Reset theme to dark
        RbDarkTheme.IsChecked = true;
        AccentPurple.IsChecked = true;
        TglAnimations.IsChecked = true;

        // Re-detect and update
        DetectResourcesAsync();
    }

    #region Theme Settings

    private void ThemeMode_Changed(object sender, RoutedEventArgs e)
    {
        if (RbDarkTheme == null || RbLightTheme == null || RbAutoTheme == null) return;

        try
        {
            var theme = _paletteHelper.GetTheme();

            if (RbDarkTheme.IsChecked == true)
            {
                theme.SetBaseTheme(BaseTheme.Dark);
            }
            else if (RbLightTheme.IsChecked == true)
            {
                theme.SetBaseTheme(BaseTheme.Light);
            }
            else if (RbAutoTheme.IsChecked == true)
            {
                // Auto: follow system theme
                var isSystemDark = IsSystemDarkMode();
                theme.SetBaseTheme(isSystemDark ? BaseTheme.Dark : BaseTheme.Light);
            }

            _paletteHelper.SetTheme(theme);

            // Save preference
            var themeMode = RbDarkTheme.IsChecked == true ? "dark"
                          : RbLightTheme.IsChecked == true ? "light"
                          : "auto";
            Environment.SetEnvironmentVariable("POSTX_THEME_MODE", themeMode);
        }
        catch
        {
            // Theme change failed, ignore
        }
    }

    private void AccentColor_Changed(object sender, RoutedEventArgs e)
    {
        if (AccentPurple == null) return;

        try
        {
            var theme = _paletteHelper.GetTheme();

            Color primaryColor;
            Color secondaryColor;

            if (AccentPurple.IsChecked == true)
            {
                primaryColor = Color.FromRgb(0x8B, 0x5C, 0xF6);  // Purple
                secondaryColor = Color.FromRgb(0xEC, 0x48, 0x99); // Pink
            }
            else if (AccentPink.IsChecked == true)
            {
                primaryColor = Color.FromRgb(0xEC, 0x48, 0x99);  // Pink
                secondaryColor = Color.FromRgb(0xF4, 0x72, 0xB6); // Light Pink
            }
            else if (AccentCyan.IsChecked == true)
            {
                primaryColor = Color.FromRgb(0x06, 0xB6, 0xD4);  // Cyan
                secondaryColor = Color.FromRgb(0x22, 0xD3, 0xEE); // Light Cyan
            }
            else if (AccentGreen.IsChecked == true)
            {
                primaryColor = Color.FromRgb(0x10, 0xB9, 0x81);  // Green
                secondaryColor = Color.FromRgb(0x34, 0xD3, 0x99); // Light Green
            }
            else if (AccentOrange.IsChecked == true)
            {
                primaryColor = Color.FromRgb(0xF5, 0x9E, 0x0B);  // Orange
                secondaryColor = Color.FromRgb(0xFB, 0xBF, 0x24); // Yellow
            }
            else
            {
                return;
            }

            theme.SetPrimaryColor(primaryColor);
            theme.SetSecondaryColor(secondaryColor);
            _paletteHelper.SetTheme(theme);
        }
        catch
        {
            // Accent color change failed, ignore
        }
    }

    private void Animations_Changed(object sender, RoutedEventArgs e)
    {
        if (TglAnimations == null) return;

        var animationsEnabled = TglAnimations.IsChecked == true;
        Environment.SetEnvironmentVariable("POSTX_ANIMATIONS", animationsEnabled ? "true" : "false");

        // Note: LavaLampBackground and RGB glow will check this setting
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0; // 0 = dark mode
            }
        }
        catch
        {
            // Registry access failed
        }

        return true; // Default to dark mode
    }

    #endregion

    #region Database Settings

    private void LoadDatabaseSettings()
    {
        // Load saved database configuration
        var provider = Environment.GetEnvironmentVariable("POSTX_DB_PROVIDER") ?? "SQLite";
        var sqliteFile = Environment.GetEnvironmentVariable("POSTX_DB_SQLITE_FILE") ?? "ai_learning.db";
        var dbHost = Environment.GetEnvironmentVariable("POSTX_DB_HOST") ?? "localhost";
        var dbPort = Environment.GetEnvironmentVariable("POSTX_DB_PORT") ?? "3306";
        var dbName = Environment.GetEnvironmentVariable("POSTX_DB_NAME") ?? "ai_learning";
        var dbUser = Environment.GetEnvironmentVariable("POSTX_DB_USER") ?? "";

        TxtSqliteFile.Text = sqliteFile;
        TxtDbHost.Text = dbHost;
        TxtDbPort.Text = dbPort;
        TxtDbName.Text = dbName;
        TxtDbUsername.Text = dbUser;

        // Set provider radio
        switch (provider.ToLower())
        {
            case "mysql":
                RbMySql.IsChecked = true;
                break;
            case "mssql":
            case "sqlserver":
                RbMsSql.IsChecked = true;
                break;
            default:
                RbSqlite.IsChecked = true;
                break;
        }

        UpdateDbProviderUI();
    }

    private void DbProvider_Changed(object sender, RoutedEventArgs e)
    {
        UpdateDbProviderUI();
    }

    private void UpdateDbProviderUI()
    {
        if (SqliteSettings == null || ExternalDbSettings == null) return;

        if (RbSqlite?.IsChecked == true)
        {
            SqliteSettings.Visibility = Visibility.Visible;
            ExternalDbSettings.Visibility = Visibility.Collapsed;
            ChkTrustCertificate.Visibility = Visibility.Collapsed;
        }
        else
        {
            SqliteSettings.Visibility = Visibility.Collapsed;
            ExternalDbSettings.Visibility = Visibility.Visible;

            // Show Trust Certificate only for SQL Server
            ChkTrustCertificate.Visibility = RbMsSql?.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Update default port
            if (RbMySql?.IsChecked == true)
            {
                TxtDbPort.Text = "3306";
            }
            else if (RbMsSql?.IsChecked == true)
            {
                TxtDbPort.Text = "1433";
            }
        }
    }

    private DatabaseConfig GetDatabaseConfig()
    {
        var config = new DatabaseConfig();

        if (RbSqlite?.IsChecked == true)
        {
            config.Provider = DatabaseProvider.SQLite;
            config.SqliteFilePath = TxtSqliteFile.Text;
        }
        else if (RbMySql?.IsChecked == true)
        {
            config.Provider = DatabaseProvider.MySQL;
            config.Host = TxtDbHost.Text;
            config.Port = int.TryParse(TxtDbPort.Text, out var port) ? port : 3306;
            config.DatabaseName = TxtDbName.Text;
            config.Username = TxtDbUsername.Text;
            config.Password = TxtDbPassword.Password;
        }
        else if (RbMsSql?.IsChecked == true)
        {
            config.Provider = DatabaseProvider.MSSQL;
            config.Host = TxtDbHost.Text;
            config.Port = int.TryParse(TxtDbPort.Text, out var port) ? port : 1433;
            config.DatabaseName = TxtDbName.Text;
            config.Username = TxtDbUsername.Text;
            config.Password = TxtDbPassword.Password;
            config.TrustServerCertificate = ChkTrustCertificate?.IsChecked == true;
        }

        return config;
    }

    private void SaveDatabaseSettings()
    {
        var provider = RbSqlite?.IsChecked == true ? "SQLite"
                     : RbMySql?.IsChecked == true ? "MySQL"
                     : "MSSQL";

        Environment.SetEnvironmentVariable("POSTX_DB_PROVIDER", provider);
        Environment.SetEnvironmentVariable("POSTX_DB_SQLITE_FILE", TxtSqliteFile.Text);
        Environment.SetEnvironmentVariable("POSTX_DB_HOST", TxtDbHost.Text);
        Environment.SetEnvironmentVariable("POSTX_DB_PORT", TxtDbPort.Text);
        Environment.SetEnvironmentVariable("POSTX_DB_NAME", TxtDbName.Text);
        Environment.SetEnvironmentVariable("POSTX_DB_USER", TxtDbUsername.Text);
        // Note: Password not saved to environment for security
    }

    private void DbService_StatusChanged(object? sender, string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtDbOperationStatus.Text = message;
            TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
        });
    }

    private void DbService_ErrorOccurred(object? sender, Exception ex)
    {
        Dispatcher.Invoke(() =>
        {
            TxtDbOperationStatus.Text = $"Error: {ex.Message}";
            TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
        });
    }

    private async void TestDbConnection_Click(object sender, RoutedEventArgs e)
    {
        BtnTestDbConnection.IsEnabled = false;
        TxtDbOperationStatus.Text = "Testing connection...";
        TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));

        try
        {
            var config = GetDatabaseConfig();
            _dbService.Configure(config);

            var result = await _dbService.TestConnectionAsync();

            if (result.Success)
            {
                TxtDbConnectionStatus.Text = "Connected";
                TxtDbStats.Text = $"Server: {result.ServerVersion} | Response: {result.ResponseTime.TotalMilliseconds:F0}ms";
                DbStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                DbStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                TxtDbOperationStatus.Text = "Connection successful!";
                TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            else
            {
                TxtDbConnectionStatus.Text = "Failed";
                TxtDbStats.Text = result.ErrorMessage;
                DbStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                DbStatusBorder.Background = new SolidColorBrush(Color.FromRgb(255, 235, 238));
                TxtDbOperationStatus.Text = $"Connection failed: {result.ErrorMessage}";
                TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
        }
        catch (Exception ex)
        {
            TxtDbConnectionStatus.Text = "Error";
            TxtDbStats.Text = ex.Message;
            DbStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            TxtDbOperationStatus.Text = $"Error: {ex.Message}";
            TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        }
        finally
        {
            BtnTestDbConnection.IsEnabled = true;
        }
    }

    private async void BuildDatabase_Click(object sender, RoutedEventArgs e)
    {
        var config = GetDatabaseConfig();
        var providerName = config.Provider.ToString();

        var confirm = MessageBox.Show(
            $"This will create the AI Learning database tables.\n\n" +
            $"Provider: {providerName}\n" +
            (config.Provider == DatabaseProvider.SQLite
                ? $"File: {config.SqliteFilePath}"
                : $"Server: {config.Host}:{config.Port}\nDatabase: {config.DatabaseName}") +
            "\n\nContinue?",
            "Build Database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        BtnBuildDatabase.IsEnabled = false;
        TxtDbOperationStatus.Text = "Building database...";
        TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));

        try
        {
            _dbService.Configure(config);
            var success = await _dbService.BuildDatabaseAsync();

            if (success)
            {
                TxtDbConnectionStatus.Text = "Connected";
                DbStatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                DbStatusBorder.Background = new SolidColorBrush(Color.FromRgb(232, 245, 233));

                // Get stats
                var stats = await _dbService.GetStatisticsAsync();
                TxtDbStats.Text = $"Tables created | {string.Join(", ", stats.Select(s => $"{s.Key}: {s.Value}"))}";

                TxtDbOperationStatus.Text = "Database built successfully! Default posting strategies added.";
                TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                // Save settings
                SaveDatabaseSettings();

                MessageBox.Show(
                    "Database initialized successfully!\n\n" +
                    "Created tables:\n" +
                    "- learning_records (AI learning data)\n" +
                    "- content_templates (Content templates)\n" +
                    "- posting_strategies (Posting strategies)\n" +
                    "- user_behavior_patterns (Behavior patterns)\n" +
                    "- ai_conversation_memory (Chat history)\n\n" +
                    "5 default posting strategies have been added.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                TxtDbOperationStatus.Text = "Failed to build database";
                TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
        }
        catch (Exception ex)
        {
            TxtDbOperationStatus.Text = $"Error: {ex.Message}";
            TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            MessageBox.Show($"Failed to build database:\n\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnBuildDatabase.IsEnabled = true;
        }
    }

    private async void ClearDbData_Click(object sender, RoutedEventArgs e)
    {
        if (!_dbService.IsConnected)
        {
            MessageBox.Show("Please build/connect to the database first.", "Not Connected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            "WARNING: This will delete ALL data from the AI Learning database!\n\n" +
            "This includes:\n" +
            "- All learning records\n" +
            "- All content templates\n" +
            "- All posting strategies\n" +
            "- All behavior patterns\n" +
            "- All conversation memory\n\n" +
            "This action cannot be undone!\n\nAre you sure?",
            "Clear All Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        // Double confirmation
        var doubleConfirm = MessageBox.Show(
            "FINAL CONFIRMATION\n\nYou are about to DELETE ALL DATA.\n\nType 'CLEAR' in the next dialog to confirm.",
            "Final Confirmation",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Stop);

        if (doubleConfirm != MessageBoxResult.OK) return;

        BtnClearData.IsEnabled = false;
        TxtDbOperationStatus.Text = "Clearing data...";

        try
        {
            var success = await _dbService.ClearDataAsync();

            if (success)
            {
                // Get stats (should all be 0)
                var stats = await _dbService.GetStatisticsAsync();
                TxtDbStats.Text = string.Join(", ", stats.Select(s => $"{s.Key}: {s.Value}"));

                TxtDbOperationStatus.Text = "All data cleared successfully.";
                TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));

                MessageBox.Show("All data has been cleared.", "Data Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                TxtDbOperationStatus.Text = "Failed to clear data";
                TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
            }
        }
        catch (Exception ex)
        {
            TxtDbOperationStatus.Text = $"Error: {ex.Message}";
            TxtDbOperationStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
        }
        finally
        {
            BtnClearData.IsEnabled = true;
        }
    }

    #endregion
}
