using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace AIManager.UI.Views;

public partial class SplashScreen : Window
{
    private readonly Storyboard _floatingAnimation;
    private readonly Storyboard _pulseAnimation;
    private readonly Storyboard _rotateAnimation;
    private double _progressBarMaxWidth;

    public SplashScreen()
    {
        InitializeComponent();

        // Get animations from resources
        _floatingAnimation = (Storyboard)FindResource("FloatingAnimation");
        _pulseAnimation = (Storyboard)FindResource("PulseAnimation");
        _rotateAnimation = (Storyboard)FindResource("RotateAnimation");

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;

        // Try to load logo
        TryLoadLogo();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Calculate progress bar max width
        _progressBarMaxWidth = ActualWidth - 80; // 40px margin on each side

        // Start animations
        _floatingAnimation?.Begin();
        _pulseAnimation?.Begin();
        _rotateAnimation?.Begin();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _progressBarMaxWidth = ActualWidth - 80;
    }

    private void TryLoadLogo()
    {
        try
        {
            // Try multiple logo paths
            var logoPaths = new[]
            {
                "pack://application:,,,/logo.png",
                "pack://application:,,,/Resources/logo.png",
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png"),
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png")
            };

            foreach (var path in logoPaths)
            {
                try
                {
                    BitmapImage bitmap;
                    if (path.StartsWith("pack://"))
                    {
                        bitmap = new BitmapImage(new Uri(path));
                    }
                    else if (System.IO.File.Exists(path))
                    {
                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    else
                    {
                        continue;
                    }

                    LogoImage.Source = bitmap;
                    FallbackLogo.Visibility = Visibility.Collapsed;
                    return;
                }
                catch
                {
                    // Try next path
                }
            }

            // If no logo found, show fallback
            ShowFallbackLogo();
        }
        catch
        {
            ShowFallbackLogo();
        }
    }

    private void ShowFallbackLogo()
    {
        LogoImage.Visibility = Visibility.Collapsed;
        FallbackLogo.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Updates the progress display with percentage and message
    /// </summary>
    /// <param name="percentage">Progress percentage (0-100)</param>
    /// <param name="message">Status message to display</param>
    public void UpdateProgress(double percentage, string message)
    {
        Dispatcher.Invoke(() =>
        {
            // Update progress bar width
            var targetWidth = (_progressBarMaxWidth > 0 ? _progressBarMaxWidth : 520) * (percentage / 100.0);

            // Animate progress bar
            var animation = new DoubleAnimation
            {
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, animation);

            // Update text
            TxtProgress.Text = message;
            TxtStatus.Text = $"{percentage:F0}%";
        });
    }

    /// <summary>
    /// Updates just the status message
    /// </summary>
    /// <param name="message">Status message to display</param>
    public void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtProgress.Text = message;
        });
    }

    /// <summary>
    /// Completes the loading animation and prepares for close
    /// </summary>
    public async Task CompleteAsync()
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            UpdateProgress(100, "Ready!");

            // Wait a moment to show completion
            await Task.Delay(500);

            // Fade out animation
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            fadeOut.Completed += (s, e) =>
            {
                _floatingAnimation?.Stop();
                _pulseAnimation?.Stop();
                _rotateAnimation?.Stop();
            };

            BeginAnimation(OpacityProperty, fadeOut);
        });
    }
}
