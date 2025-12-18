using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AIManager.UI.Controls;

/// <summary>
/// Lava Lamp Background - Animated floating blobs effect
/// </summary>
public partial class LavaLampBackground : UserControl
{
    private readonly DispatcherTimer _animationTimer;
    private readonly Random _random = new();
    private readonly List<BlobInfo> _blobs = new();
    private bool _isAnimating;

    public LavaLampBackground()
    {
        InitializeComponent();

        // Initialize blob info
        _blobs.Add(new BlobInfo { Transform = Blob1Transform, SpeedX = 0.3, SpeedY = 0.2, DirectionX = 1, DirectionY = 1 });
        _blobs.Add(new BlobInfo { Transform = Blob2Transform, SpeedX = 0.25, SpeedY = 0.35, DirectionX = -1, DirectionY = 1 });
        _blobs.Add(new BlobInfo { Transform = Blob3Transform, SpeedX = 0.4, SpeedY = 0.15, DirectionX = 1, DirectionY = -1 });
        _blobs.Add(new BlobInfo { Transform = Blob4Transform, SpeedX = 0.2, SpeedY = 0.3, DirectionX = -1, DirectionY = -1 });
        _blobs.Add(new BlobInfo { Transform = Blob5Transform, SpeedX = 0.35, SpeedY = 0.25, DirectionX = 1, DirectionY = 1 });

        // Setup animation timer
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _animationTimer.Tick += AnimationTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartAnimation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopAnimation();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Reposition blobs when size changes
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            RandomizePositions();
        }
    }

    public void StartAnimation()
    {
        if (!_isAnimating)
        {
            _isAnimating = true;
            _animationTimer.Start();
        }
    }

    public void StopAnimation()
    {
        _isAnimating = false;
        _animationTimer.Stop();
    }

    private void RandomizePositions()
    {
        var maxX = Math.Max(100, ActualWidth - 400);
        var maxY = Math.Max(100, ActualHeight - 400);

        foreach (var blob in _blobs)
        {
            blob.Transform.X = _random.NextDouble() * maxX;
            blob.Transform.Y = _random.NextDouble() * maxY;
        }
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var maxX = ActualWidth - 200;
        var maxY = ActualHeight - 200;

        foreach (var blob in _blobs)
        {
            // Update position
            blob.Transform.X += blob.SpeedX * blob.DirectionX;
            blob.Transform.Y += blob.SpeedY * blob.DirectionY;

            // Bounce off edges with smooth direction change
            if (blob.Transform.X <= 0 || blob.Transform.X >= maxX)
            {
                blob.DirectionX *= -1;
                blob.SpeedX = 0.15 + _random.NextDouble() * 0.35; // Randomize speed on bounce
            }

            if (blob.Transform.Y <= 0 || blob.Transform.Y >= maxY)
            {
                blob.DirectionY *= -1;
                blob.SpeedY = 0.15 + _random.NextDouble() * 0.35;
            }

            // Clamp to bounds
            blob.Transform.X = Math.Clamp(blob.Transform.X, 0, maxX);
            blob.Transform.Y = Math.Clamp(blob.Transform.Y, 0, maxY);
        }
    }

    private class BlobInfo
    {
        public TranslateTransform Transform { get; set; } = null!;
        public double SpeedX { get; set; }
        public double SpeedY { get; set; }
        public double DirectionX { get; set; }
        public double DirectionY { get; set; }
    }
}
