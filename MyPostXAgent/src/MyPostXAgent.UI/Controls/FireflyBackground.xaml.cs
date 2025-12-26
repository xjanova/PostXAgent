using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MyPostXAgent.UI.Controls;

/// <summary>
/// Firefly Background Effect - หิ่งห้อยลอยขึ้นช้าๆ สวยงาม
/// </summary>
public partial class FireflyBackground : UserControl
{
    private readonly List<Firefly> _fireflies = new();
    private readonly Random _random = new();
    private bool _isRunning;
    private DateTime _lastUpdate;

    // Configuration - ปรับให้เห็นชัดขึ้น
    private const int MaxFireflies = 40;
    private const double SpawnChance = 0.08;

    public FireflyBackground()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isRunning = true;
        _lastUpdate = DateTime.Now;
        CompositionTarget.Rendering += OnRendering;

        // Spawn initial fireflies ทันที
        Dispatcher.BeginInvoke(() =>
        {
            for (int i = 0; i < 20; i++)
            {
                SpawnFirefly(randomY: true);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isRunning = false;
        CompositionTarget.Rendering -= OnRendering;
        _fireflies.Clear();
        FireflyCanvas.Children.Clear();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_isRunning) return;

        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var now = DateTime.Now;
        var deltaTime = (now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        if (deltaTime <= 0 || deltaTime > 0.1) return;

        // Spawn new fireflies
        if (_fireflies.Count < MaxFireflies && _random.NextDouble() < SpawnChance)
        {
            SpawnFirefly();
        }

        // Update fireflies
        var toRemove = new List<Firefly>();

        foreach (var firefly in _fireflies)
        {
            // Move upward slowly
            firefly.Y -= firefly.Speed * deltaTime * 50;

            // Horizontal drift
            firefly.X += Math.Sin(firefly.Phase) * firefly.Drift * deltaTime * 30;
            firefly.Phase += firefly.PhaseSpeed * deltaTime;

            // Pulse opacity
            firefly.OpacityPhase += firefly.PulseSpeed * deltaTime;
            var pulse = 0.5 + 0.5 * Math.Sin(firefly.OpacityPhase);
            firefly.Element.Opacity = pulse * firefly.BaseOpacity;

            // Update position
            Canvas.SetLeft(firefly.Element, firefly.X);
            Canvas.SetTop(firefly.Element, firefly.Y);

            // Remove if out of view
            if (firefly.Y < -100)
            {
                toRemove.Add(firefly);
            }
        }

        foreach (var f in toRemove)
        {
            _fireflies.Remove(f);
            FireflyCanvas.Children.Remove(f.Element);
        }
    }

    private void SpawnFirefly(bool randomY = false)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        // ขนาดใหญ่ขึ้น
        var size = 3 + _random.NextDouble() * 8;
        var x = _random.NextDouble() * width;
        var y = randomY ? _random.NextDouble() * height : height + 50;

        // สีหิ่งห้อย
        var colors = new[]
        {
            Color.FromRgb(0, 245, 255),   // Cyan
            Color.FromRgb(139, 92, 246),  // Purple
            Color.FromRgb(236, 72, 153),  // Pink
            Color.FromRgb(16, 185, 129),  // Green
            Color.FromRgb(59, 130, 246),  // Blue
            Color.FromRgb(251, 191, 36),  // Yellow/Gold
        };
        var color = colors[_random.Next(colors.Length)];

        // สร้าง container
        var container = new Canvas
        {
            Width = size * 6,
            Height = size * 6
        };

        // Outer glow (ใหญ่และจาง)
        var outerGlow = new Ellipse
        {
            Width = size * 6,
            Height = size * 6,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(80, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(30, color.R, color.G, color.B), 0.4),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                }
            }
        };
        Canvas.SetLeft(outerGlow, 0);
        Canvas.SetTop(outerGlow, 0);
        container.Children.Add(outerGlow);

        // Inner glow (เล็กและสว่าง)
        var innerGlow = new Ellipse
        {
            Width = size * 2,
            Height = size * 2,
            Fill = new RadialGradientBrush
            {
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(255, color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(200, color.R, color.G, color.B), 0.3),
                    new GradientStop(Color.FromArgb(80, color.R, color.G, color.B), 0.7),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1)
                }
            }
        };
        Canvas.SetLeft(innerGlow, size * 2);
        Canvas.SetTop(innerGlow, size * 2);
        container.Children.Add(innerGlow);

        // Core (จุดศูนย์กลางสว่างมาก)
        var core = new Ellipse
        {
            Width = size * 0.8,
            Height = size * 0.8,
            Fill = new SolidColorBrush(Colors.White)
        };
        Canvas.SetLeft(core, size * 2.6);
        Canvas.SetTop(core, size * 2.6);
        container.Children.Add(core);

        // Position
        var posX = x - size * 3;
        var posY = y - size * 3;
        Canvas.SetLeft(container, posX);
        Canvas.SetTop(container, posY);

        FireflyCanvas.Children.Add(container);

        _fireflies.Add(new Firefly
        {
            X = posX,
            Y = posY,
            Speed = 0.5 + _random.NextDouble() * 1.0,
            Drift = 0.3 + _random.NextDouble() * 0.5,
            Phase = _random.NextDouble() * Math.PI * 2,
            PhaseSpeed = 1 + _random.NextDouble() * 2,
            OpacityPhase = _random.NextDouble() * Math.PI * 2,
            PulseSpeed = 1.5 + _random.NextDouble() * 2.5,
            BaseOpacity = 0.6 + _random.NextDouble() * 0.4,
            Element = container
        });
    }

    private class Firefly
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Speed { get; set; }
        public double Drift { get; set; }
        public double Phase { get; set; }
        public double PhaseSpeed { get; set; }
        public double OpacityPhase { get; set; }
        public double PulseSpeed { get; set; }
        public double BaseOpacity { get; set; }
        public UIElement Element { get; set; } = null!;
    }
}
