using System.Diagnostics;
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
    private readonly ComfyUIService _comfyService = new();

    // Status colors
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(76, 175, 80));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromRgb(255, 193, 7));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(244, 67, 54));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(158, 158, 158));
    private static readonly SolidColorBrush CyanBrush = new(Color.FromRgb(0, 188, 212));

    // Service URLs
    private readonly string _ollamaUrl = "http://localhost:11434";
    private readonly string _backendUrl = "http://localhost:8000";

    // Events
    public event EventHandler? OllamaClicked;
    public event EventHandler? ComfyClicked;
    public event EventHandler? HuggingFaceClicked;
    public event EventHandler? GpuClicked;
    public event EventHandler? BackendClicked;

    public StatusBar()
    {
        InitializeComponent();

        // Try to get services from DI
        _hfManager = App.Services?.GetService<HuggingFaceModelManager>();
        _gpuService = App.Services?.GetService<GpuRentalService>();

        // Setup update timer (every 10 seconds)
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += StatusBar_Loaded;
        Unloaded += StatusBar_Unloaded;
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
            CheckBackendAsync()
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
}
