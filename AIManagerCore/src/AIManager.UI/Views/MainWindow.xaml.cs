using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using AIManager.Core.Orchestrator;
using AIManager.Core.Services;
using AIManager.UI.Views.Pages;

namespace AIManager.UI.Views;

public partial class MainWindow : Window
{
    private readonly ProcessOrchestrator _orchestrator;
    private readonly AIAssistantService _aiAssistant;
    private readonly DispatcherTimer _clockTimer;
    private readonly DispatcherTimer _rgbTimer;
    private readonly DispatcherTimer _marqueeTimer;
    private Button? _selectedNavButton;
    private readonly string _logoPath;
    private double _rgbHue = 0;
    private Storyboard? _marqueeStoryboard;
    private bool _isMarqueeNeeded = false;

    // RGB colors for smooth cycling
    private readonly Color[] _rgbColors = new[]
    {
        Color.FromRgb(255, 0, 0),     // Red
        Color.FromRgb(255, 127, 0),   // Orange
        Color.FromRgb(255, 255, 0),   // Yellow
        Color.FromRgb(0, 255, 0),     // Green
        Color.FromRgb(0, 255, 255),   // Cyan
        Color.FromRgb(0, 127, 255),   // Light Blue
        Color.FromRgb(0, 0, 255),     // Blue
        Color.FromRgb(127, 0, 255),   // Purple
        Color.FromRgb(255, 0, 255),   // Magenta
        Color.FromRgb(255, 0, 127),   // Pink
    };

    public MainWindow()
    {
        InitializeComponent();

        _orchestrator = App.Services.GetRequiredService<ProcessOrchestrator>();
        _aiAssistant = App.Services.GetRequiredService<AIAssistantService>();

        // Logo path - check multiple locations
        _logoPath = FindLogoPath();

        // Setup clock
        _clockTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (s, e) => ClockDisplay.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();

        // Setup RGB glow animation timer - slow pulse
        _rgbTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20 FPS for smooth animation
        };
        _rgbTimer.Tick += RgbTimer_Tick;
        _rgbTimer.Start();

        // Setup marquee timer for checking text overflow
        _marqueeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _marqueeTimer.Tick += MarqueeTimer_Tick;

        // Subscribe to AI Assistant messages
        _aiAssistant.OnNewMessage += OnAIAssistantMessage;
        _aiAssistant.Start();

        // Subscribe to orchestrator events
        _orchestrator.StatsUpdated += OnStatsUpdated;
        _orchestrator.TaskCompleted += OnTaskCompleted;
        _orchestrator.TaskFailed += OnTaskFailed;
        _orchestrator.WorkerStatusChanged += OnWorkerStatusChanged;

        // Setup Status Bar events
        AppStatusBar.OllamaClicked += (s, e) => NavigateTo("Settings");
        AppStatusBar.ComfyClicked += (s, e) => NavigateTo("Settings");
        AppStatusBar.HuggingFaceClicked += (s, e) => NavigateTo("AIProviders");
        AppStatusBar.GpuClicked += (s, e) => NavigateTo("AIProviders");
        AppStatusBar.BackendClicked += (s, e) => NavigateTo("Settings");
        AppStatusBar.ClaudeClicked += (s, e) => OpenClaudeChatWindow();

        // Navigate to dashboard
        NavigateTo("Dashboard");
        UpdateServerStatus(false);

        // Load custom logo if exists
        LoadCustomLogo();

        // Update taskbar icon to match logo
        UpdateTaskbarIcon();

        Closed += (s, e) =>
        {
            _clockTimer.Stop();
            _rgbTimer.Stop();
            _marqueeTimer.Stop();
            _aiAssistant.Stop();
            LavaBackground?.StopAnimation();
            _orchestrator.Dispose();
        };
    }

    /// <summary>
    /// RGB glow animation - smooth color cycling on the glow ring
    /// </summary>
    private void RgbTimer_Tick(object? sender, EventArgs e)
    {
        // Slowly increment hue (0.5 degrees per tick = full cycle in ~36 seconds)
        _rgbHue = (_rgbHue + 0.5) % 360;

        // Calculate three colors offset by 120 degrees for gradient
        var color1 = HsvToRgb(_rgbHue, 1.0, 1.0);
        var color2 = HsvToRgb((_rgbHue + 120) % 360, 1.0, 1.0);
        var color3 = HsvToRgb((_rgbHue + 240) % 360, 1.0, 1.0);

        // Update gradient colors on the glow ring
        if (RgbGlowRing?.Background is LinearGradientBrush gradient)
        {
            gradient.GradientStops[0].Color = color1;
            gradient.GradientStops[1].Color = color2;
            gradient.GradientStops[2].Color = color3;
        }

        // Update glow effect color (use primary color)
        if (RgbGlowRing?.Effect is DropShadowEffect glowEffect)
        {
            glowEffect.Color = color1;
        }
    }

    /// <summary>
    /// Convert HSV to RGB color
    /// </summary>
    private static Color HsvToRgb(double h, double s, double v)
    {
        var hi = (int)(h / 60) % 6;
        var f = h / 60 - (int)(h / 60);
        var p = v * (1 - s);
        var q = v * (1 - f * s);
        var t = v * (1 - (1 - f) * s);

        double r, g, b;
        switch (hi)
        {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }

        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    /// <summary>
    /// Update taskbar icon to match the logo
    /// </summary>
    private void UpdateTaskbarIcon()
    {
        try
        {
            if (File.Exists(_logoPath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(_logoPath, UriKind.Absolute);
                bitmap.DecodePixelWidth = 256;
                bitmap.DecodePixelHeight = 256;
                bitmap.EndInit();
                bitmap.Freeze();

                Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    CreateBitmapFromSource(bitmap).GetHbitmap(),
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
        }
        catch
        {
            // Keep default icon on error
        }
    }

    /// <summary>
    /// Convert BitmapSource to System.Drawing.Bitmap for icon
    /// </summary>
    private static System.Drawing.Bitmap CreateBitmapFromSource(BitmapSource source)
    {
        var bitmap = new System.Drawing.Bitmap(
            source.PixelWidth,
            source.PixelHeight,
            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

        var bitmapData = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.WriteOnly,
            bitmap.PixelFormat);

        source.CopyPixels(
            Int32Rect.Empty,
            bitmapData.Scan0,
            bitmapData.Height * bitmapData.Stride,
            bitmapData.Stride);

        bitmap.UnlockBits(bitmapData);
        return bitmap;
    }

    // Load custom logo
    private void LoadCustomLogo()
    {
        if (File.Exists(_logoPath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(_logoPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                CustomLogoImage.Source = bitmap;
                LogoImageContainer.Visibility = Visibility.Visible;
                FallbackLogoBg.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // Use fallback icon
                LogoImageContainer.Visibility = Visibility.Collapsed;
                FallbackLogoBg.Visibility = Visibility.Visible;
            }
        }
    }

    // Window drag support - fixed to prevent DragMove error
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Only drag if left button is still down and not handled by other controls
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Ignore - button was released before DragMove could start
            }
        }
    }

    // Window control buttons
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowMaximize;
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WindowRestore;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
        {
            NavigateTo(page);
            UpdateSelectedNav(button);
        }
    }

    /// <summary>
    /// Public method to navigate to a specific page - for use from other pages
    /// </summary>
    public void NavigateToPage(string pageName, string? initialUrl = null, string? platformName = null)
    {
        NavigateTo(pageName, initialUrl, platformName);
    }

    private void NavigateTo(string pageName, string? initialUrl = null, string? platformName = null)
    {
        PageTitle.Text = pageName;

        Page? page = pageName switch
        {
            "Dashboard" => new DashboardPage(_orchestrator),
            "Workers" => new WorkersPage(),
            "Tasks" => new TasksPage(_orchestrator),
            "Platforms" => new PlatformsPage(),
            "AIProviders" => new AIProvidersPage(),
            "ContentCreator" => new ContentCreatorPage(),
            "Chat" => new ChatPage(),
            "ImageGenerator" => new ImageGeneratorPage(),
            "SunoOptions" => new SunoOptionsPage(),
            "ModelManager" => new ModelManagerPage(),
            "ColabGpu" => new ColabGpuPage(),
            "GpuNodeMonitor" => new GpuNodeMonitorPage(),
            "GpuSetupWizard" => new GpuSetupWizardPage(),
            "AIServicesInfo" => new AIServicesInfoPage(),
            "WorkflowManager" => new WorkflowManagerPage(),
            "WorkflowEditor" => new WorkflowEditorPage(),
            "WorkflowMonitor" => new WorkflowMonitorPage(),
            "WebLearning" => new WebLearningPage(initialUrl, platformName),
            "WorkerWebView" => new WorkerWebViewPage(),
            "Strategies" => new StrategiesPage(),
            "ApiKeys" => new ApiKeysPage(),
            "Logs" => new LogsPage(),
            "Settings" => new SettingsPage(),
            _ => new DashboardPage(_orchestrator)
        };

        ContentFrame.Navigate(page);
    }

    private void UpdateSelectedNav(Button button)
    {
        if (_selectedNavButton != null)
        {
            _selectedNavButton.Background = Brushes.Transparent;
        }

        button.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
        _selectedNavButton = button;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        BtnStart.IsEnabled = false;
        StatusText.Text = "Starting...";

        try
        {
            await _orchestrator.StartAsync();
            UpdateServerStatus(true);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            UpdateServerStatus(false);
        }

        BtnStart.IsEnabled = true;
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        BtnStop.IsEnabled = false;
        StatusText.Text = "Stopping...";

        try
        {
            await _orchestrator.StopAsync();
            UpdateServerStatus(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to stop: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }

        BtnStop.IsEnabled = true;
    }

    private void UpdateServerStatus(bool isRunning)
    {
        Dispatcher.Invoke(() =>
        {
            if (isRunning)
            {
                StatusIndicatorBorder.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                if (StatusIndicatorBorder.Effect is System.Windows.Media.Effects.DropShadowEffect effect)
                {
                    effect.Color = Color.FromRgb(76, 175, 80);
                }
                StatusText.Text = "Server Running";
                BtnStart.Visibility = Visibility.Collapsed;
                BtnStop.Visibility = Visibility.Visible;
            }
            else
            {
                StatusIndicatorBorder.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                if (StatusIndicatorBorder.Effect is System.Windows.Media.Effects.DropShadowEffect effect)
                {
                    effect.Color = Color.FromRgb(244, 67, 54);
                }
                StatusText.Text = "Server Stopped";
                BtnStart.Visibility = Visibility.Visible;
                BtnStop.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void OnStatsUpdated(object? sender, StatsEventArgs e)
    {
        // Update will be handled by individual pages
    }

    private void OnTaskCompleted(object? sender, TaskEventArgs e)
    {
        // Show notification or update UI
    }

    private void OnTaskFailed(object? sender, TaskEventArgs e)
    {
        // Show error notification
    }

    private void OnWorkerStatusChanged(object? sender, WorkerEventArgs e)
    {
        // Update worker status display
    }

    /// <summary>
    /// Open Claude Chat Window for AI-Claude communication
    /// </summary>
    private void OpenClaudeChatWindow()
    {
        var chatWindow = new ClaudeChatWindow
        {
            Owner = this
        };
        chatWindow.Show();
    }

    /// <summary>
    /// Find logo.png in multiple possible locations
    /// </summary>
    private string FindLogoPath()
    {
        // Priority order for finding logo
        var possiblePaths = new[]
        {
            // 1. User's local app data (for customized logo)
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PostXAgent", "logo.png"),

            // 2. Same directory as executable
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),

            // 3. Parent directories (development mode)
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "logo.png"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "logo.png"),

            // 4. Project root (development mode)
            @"D:\Code\PostXAgent\logo.png",
            @"D:\Code\PostXAgent\AIManagerCore\logo.png"
        };

        foreach (var path in possiblePaths)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Skip invalid paths
            }
        }

        // Return default local app data path (will be created if user selects custom logo)
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent", "logo.png");
    }

    /// <summary>
    /// Handle AI Assistant message updates
    /// </summary>
    private void OnAIAssistantMessage(AIMessage message)
    {
        Dispatcher.Invoke(() =>
        {
            // Stop any existing marquee animation
            StopMarquee();

            // Update text and icon
            TxtAIMessage.Text = message.Text;

            // Update icon based on message type
            AIMessageIcon.Kind = message.Icon switch
            {
                "PartyPopper" => MaterialDesignThemes.Wpf.PackIconKind.PartyPopper,
                "AlertCircle" => MaterialDesignThemes.Wpf.PackIconKind.AlertCircle,
                "CheckCircle" => MaterialDesignThemes.Wpf.PackIconKind.CheckCircle,
                "Lightbulb" => MaterialDesignThemes.Wpf.PackIconKind.Lightbulb,
                "ChartLine" => MaterialDesignThemes.Wpf.PackIconKind.ChartLine,
                "Memory" => MaterialDesignThemes.Wpf.PackIconKind.Memory,
                "Cpu" => MaterialDesignThemes.Wpf.PackIconKind.Chip,
                "Cog" => MaterialDesignThemes.Wpf.PackIconKind.Cog,
                "ListCheck" => MaterialDesignThemes.Wpf.PackIconKind.FormatListChecks,
                "Keyboard" => MaterialDesignThemes.Wpf.PackIconKind.Keyboard,
                "Clock" => MaterialDesignThemes.Wpf.PackIconKind.Clock,
                "ChartBar" => MaterialDesignThemes.Wpf.PackIconKind.ChartBar,
                "Share" => MaterialDesignThemes.Wpf.PackIconKind.ShareVariant,
                "Image" => MaterialDesignThemes.Wpf.PackIconKind.Image,
                "MessageCircle" => MaterialDesignThemes.Wpf.PackIconKind.MessageText,
                "GitBranch" => MaterialDesignThemes.Wpf.PackIconKind.SourceBranch,
                _ => MaterialDesignThemes.Wpf.PackIconKind.Robot
            };

            // Update icon color based on message type
            AIMessageIcon.Foreground = message.Type switch
            {
                AIMessageType.Warning => new SolidColorBrush(Color.FromRgb(244, 67, 54)),   // Red
                AIMessageType.Celebration => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                AIMessageType.Suggestion => new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow
                _ => new SolidColorBrush(Color.FromRgb(139, 92, 246))  // Purple (default)
            };

            // Update tooltip with full message
            AIMessageTooltip.Content = message.Text;

            // Check if marquee is needed after layout update
            _marqueeTimer.Start();
        });
    }

    /// <summary>
    /// Check if text overflows and needs marquee
    /// </summary>
    private void MarqueeTimer_Tick(object? sender, EventArgs e)
    {
        _marqueeTimer.Stop();

        // Measure text width
        TxtAIMessage.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = TxtAIMessage.DesiredSize.Width;
        var canvasWidth = MarqueeCanvas.ActualWidth;

        if (canvasWidth <= 0) return;

        if (textWidth > canvasWidth)
        {
            // Text is too long - start marquee animation
            _isMarqueeNeeded = true;
            StartMarquee(textWidth, canvasWidth);
        }
        else
        {
            // Text fits - center it
            _isMarqueeNeeded = false;
            Canvas.SetLeft(TxtAIMessage, 0);
        }
    }

    /// <summary>
    /// Start marquee scrolling animation
    /// </summary>
    private void StartMarquee(double textWidth, double canvasWidth)
    {
        StopMarquee();

        // Create smooth scrolling animation
        var animation = new DoubleAnimation
        {
            From = 0,
            To = -(textWidth + 50), // Scroll completely off + gap
            Duration = TimeSpan.FromSeconds(textWidth / 50), // Speed: 50 pixels per second
            RepeatBehavior = RepeatBehavior.Forever
        };

        // Add ease for smooth feel
        animation.EasingFunction = new LinearEasing();

        _marqueeStoryboard = new Storyboard();
        _marqueeStoryboard.Children.Add(animation);
        Storyboard.SetTarget(animation, TxtAIMessage);
        Storyboard.SetTargetProperty(animation, new PropertyPath("(Canvas.Left)"));

        _marqueeStoryboard.Begin();
    }

    /// <summary>
    /// Stop marquee animation
    /// </summary>
    private void StopMarquee()
    {
        _marqueeStoryboard?.Stop();
        _marqueeStoryboard = null;
        Canvas.SetLeft(TxtAIMessage, 0);
    }

    /// <summary>
    /// Handle AI Message Panel click - open AI Chat page
    /// </summary>
    private void AIMessagePanel_Click(object sender, MouseButtonEventArgs e)
    {
        NavigateTo("Chat");
        UpdateSelectedNav(BtnChat);
    }

    /// <summary>
    /// Linear easing for smooth marquee
    /// </summary>
    private class LinearEasing : EasingFunctionBase
    {
        protected override double EaseInCore(double normalizedTime) => normalizedTime;
        protected override Freezable CreateInstanceCore() => new LinearEasing();
    }
}
