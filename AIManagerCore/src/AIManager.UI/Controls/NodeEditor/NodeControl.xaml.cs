using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AIManager.Core.NodeEditor;
using AIManager.Core.NodeEditor.Models;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Controls.NodeEditor;

/// <summary>
/// Visual control for a workflow node
/// </summary>
public partial class NodeControl : UserControl
{
    private readonly Dictionary<string, Ellipse> _inputPortElements = new();
    private readonly Dictionary<string, Ellipse> _outputPortElements = new();
    private bool _isSelected;
    private NodeExecutionState _executionState = NodeExecutionState.Idle;

    public WorkflowNode Node { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            SelectionBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public NodeExecutionState ExecutionState
    {
        get => _executionState;
        set
        {
            _executionState = value;
            UpdateExecutionIndicator();
        }
    }

    // Events
    public event EventHandler<PortEventArgs>? PortMouseDown;
    public event EventHandler<PortEventArgs>? PortMouseUp;
    public event EventHandler? DeleteRequested;
    public event EventHandler? DuplicateRequested;

    public NodeControl(WorkflowNode node)
    {
        InitializeComponent();
        Node = node;

        SetupNodeAppearance();
        CreatePorts();
        CreatePropertyControls();
    }

    private void SetupNodeAppearance()
    {
        // Set title
        TitleText.Text = Node.Name;

        // Set icon
        if (Enum.TryParse<PackIconKind>(Node.Icon, out var iconKind))
        {
            NodeIcon.Kind = iconKind;
        }

        // Set header color
        var color = (Color)ColorConverter.ConvertFromString(Node.Color);
        HeaderBorder.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop(color, 0),
                new GradientStop(Color.FromArgb(180, color.R, color.G, color.B), 1)
            }
        };

        NodeIcon.Foreground = new SolidColorBrush(color);

        // Set size
        Width = Node.Size.Width;
        MinHeight = Node.Size.Height;

        // Handle disabled state
        UpdateEnabledState();
    }

    private void CreatePorts()
    {
        // Create input ports
        foreach (var port in Node.Inputs)
        {
            var portElement = CreatePortElement(port, false);
            _inputPortElements[port.Id] = portElement;

            var portContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4)
            };

            portContainer.Children.Add(portElement);
            portContainer.Children.Add(new TextBlock
            {
                Text = port.Name,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Opacity = 0.8
            });

            InputPortsPanel.Children.Add(portContainer);
        }

        // Create output ports
        foreach (var port in Node.Outputs)
        {
            var portElement = CreatePortElement(port, true);
            _outputPortElements[port.Id] = portElement;

            var portContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 4),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            portContainer.Children.Add(new TextBlock
            {
                Text = port.Name,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Opacity = 0.8
            });
            portContainer.Children.Add(portElement);

            OutputPortsPanel.Children.Add(portContainer);
        }
    }

    private Ellipse CreatePortElement(NodePort port, bool isOutput)
    {
        var color = (Color)ColorConverter.ConvertFromString(port.Color);

        var ellipse = new Ellipse
        {
            Width = 14,
            Height = 14,
            Fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            Cursor = Cursors.Cross,
            Tag = port
        };

        ellipse.MouseLeftButtonDown += (s, e) =>
        {
            var pos = GetPortPosition(port.Id, isOutput);
            PortMouseDown?.Invoke(this, new PortEventArgs(port, isOutput, pos));
            e.Handled = true;
        };

        ellipse.MouseLeftButtonUp += (s, e) =>
        {
            var pos = GetPortPosition(port.Id, isOutput);
            PortMouseUp?.Invoke(this, new PortEventArgs(port, isOutput, pos));
            e.Handled = true;
        };

        ellipse.MouseEnter += (s, e) =>
        {
            ellipse.Width = 18;
            ellipse.Height = 18;
            ellipse.Fill = new SolidColorBrush(color);
            ellipse.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = color,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = 0.8
            };
        };

        ellipse.MouseLeave += (s, e) =>
        {
            ellipse.Width = 14;
            ellipse.Height = 14;
            ellipse.Fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
            ellipse.Effect = null;
        };

        return ellipse;
    }

    private void CreatePropertyControls()
    {
        foreach (var prop in Node.Properties.Values.Where(p => p.IsVisible))
        {
            var container = new StackPanel { Margin = new Thickness(0, 4, 0, 4) };

            // Label
            container.Children.Add(new TextBlock
            {
                Text = prop.DisplayName,
                FontSize = 10,
                Opacity = 0.6,
                Margin = new Thickness(0, 0, 0, 2)
            });

            // Control based on property type
            FrameworkElement? control = prop.Type switch
            {
                PropertyType.String => CreateStringControl(prop),
                PropertyType.MultilineText => CreateMultilineControl(prop),
                PropertyType.Int or PropertyType.Float => CreateNumberControl(prop),
                PropertyType.Bool => CreateBoolControl(prop),
                PropertyType.Slider => CreateSliderControl(prop),
                PropertyType.Combo or PropertyType.Enum => CreateComboControl(prop),
                PropertyType.Color => CreateColorControl(prop),
                PropertyType.FilePath => CreateFilePathControl(prop),
                PropertyType.Seed => CreateSeedControl(prop),
                _ => null
            };

            if (control != null)
            {
                container.Children.Add(control);
                PropertiesPanel.Children.Add(container);
            }
        }
    }

    private TextBox CreateStringControl(NodeProperty prop)
    {
        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "",
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White
        };

        textBox.TextChanged += (s, e) => prop.Value = textBox.Text;
        return textBox;
    }

    private TextBox CreateMultilineControl(NodeProperty prop)
    {
        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "",
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 50,
            MaxHeight = 100,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        textBox.TextChanged += (s, e) => prop.Value = textBox.Text;
        return textBox;
    }

    private TextBox CreateNumberControl(NodeProperty prop)
    {
        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "0",
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White
        };

        textBox.TextChanged += (s, e) =>
        {
            if (prop.Type == PropertyType.Int && int.TryParse(textBox.Text, out var intVal))
                prop.Value = intVal;
            else if (prop.Type == PropertyType.Float && double.TryParse(textBox.Text, out var floatVal))
                prop.Value = floatVal;
        };

        return textBox;
    }

    private CheckBox CreateBoolControl(NodeProperty prop)
    {
        var checkBox = new CheckBox
        {
            IsChecked = Convert.ToBoolean(prop.Value ?? false),
            Foreground = Brushes.White
        };

        checkBox.Checked += (s, e) => prop.Value = true;
        checkBox.Unchecked += (s, e) => prop.Value = false;
        return checkBox;
    }

    private Grid CreateSliderControl(NodeProperty prop)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = new Slider
        {
            Minimum = prop.Min ?? 0,
            Maximum = prop.Max ?? 100,
            Value = Convert.ToDouble(prop.Value ?? 0),
            TickFrequency = prop.Step ?? 1,
            IsSnapToTickEnabled = prop.Step.HasValue,
            Foreground = (Brush)new BrushConverter().ConvertFromString(Node.Color)!
        };

        var valueText = new TextBlock
        {
            Text = slider.Value.ToString("F2"),
            FontSize = 10,
            Opacity = 0.8,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 35
        };

        slider.ValueChanged += (s, e) =>
        {
            prop.Value = slider.Value;
            valueText.Text = slider.Value.ToString("F2");
        };

        Grid.SetColumn(slider, 0);
        Grid.SetColumn(valueText, 1);

        grid.Children.Add(slider);
        grid.Children.Add(valueText);

        return grid;
    }

    private ComboBox CreateComboControl(NodeProperty prop)
    {
        var comboBox = new ComboBox
        {
            FontSize = 11,
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            Foreground = Brushes.White
        };

        if (prop.Options != null)
        {
            foreach (var option in prop.Options)
            {
                comboBox.Items.Add(option);
            }
        }

        comboBox.SelectedItem = prop.Value?.ToString();
        comboBox.SelectionChanged += (s, e) =>
        {
            if (comboBox.SelectedItem != null)
                prop.Value = comboBox.SelectedItem.ToString();
        };

        return comboBox;
    }

    private Border CreateColorControl(NodeProperty prop)
    {
        var colorValue = prop.Value?.ToString() ?? "#FFFFFF";
        var color = (Color)ColorConverter.ConvertFromString(colorValue);

        var colorPreview = new Border
        {
            Width = double.NaN,
            Height = 24,
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(4),
            Cursor = Cursors.Hand
        };

        colorPreview.MouseLeftButtonDown += (s, e) =>
        {
            // TODO: Show color picker dialog
        };

        return colorPreview;
    }

    private Grid CreateFilePathControl(NodeProperty prop)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "",
            FontSize = 10,
            Padding = new Thickness(4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White
        };

        var browseBtn = new Button
        {
            Content = "...",
            Padding = new Thickness(8, 2, 8, 2),
            Margin = new Thickness(4, 0, 0, 0),
            FontSize = 10
        };

        browseBtn.Click += (s, e) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "All Files|*.*|Images|*.png;*.jpg;*.jpeg;*.webp"
            };

            if (dialog.ShowDialog() == true)
            {
                textBox.Text = dialog.FileName;
                prop.Value = dialog.FileName;
            }
        };

        textBox.TextChanged += (s, e) => prop.Value = textBox.Text;

        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(browseBtn, 1);

        grid.Children.Add(textBox);
        grid.Children.Add(browseBtn);

        return grid;
    }

    private Grid CreateSeedControl(NodeProperty prop)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "-1",
            FontSize = 11,
            Padding = new Thickness(6, 4, 6, 4),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = Brushes.White
        };

        var randomBtn = new Button
        {
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(4, 0, 0, 0),
            ToolTip = "Random Seed"
        };
        randomBtn.Content = new PackIcon { Kind = PackIconKind.Dice3, Width = 14, Height = 14 };

        randomBtn.Click += (s, e) =>
        {
            var randomSeed = Random.Shared.NextInt64();
            textBox.Text = randomSeed.ToString();
            prop.Value = randomSeed;
        };

        textBox.TextChanged += (s, e) =>
        {
            if (long.TryParse(textBox.Text, out var seed))
                prop.Value = seed;
        };

        Grid.SetColumn(textBox, 0);
        Grid.SetColumn(randomBtn, 1);

        grid.Children.Add(textBox);
        grid.Children.Add(randomBtn);

        return grid;
    }

    /// <summary>
    /// Get port position relative to canvas
    /// </summary>
    public Point GetPortPosition(string portId, bool isOutput)
    {
        var portElements = isOutput ? _outputPortElements : _inputPortElements;

        if (!portElements.TryGetValue(portId, out var ellipse))
            return new Point(0, 0);

        var transform = ellipse.TransformToAncestor(Parent as Visual ?? this);
        var center = transform.Transform(new Point(ellipse.Width / 2, ellipse.Height / 2));

        return center;
    }

    private void UpdateExecutionIndicator()
    {
        ExecutionIndicator.Visibility = Visibility.Visible;

        (ExecutionIndicator.Background, ExecutionIndicator.Effect) = _executionState switch
        {
            NodeExecutionState.Queued => (
                new SolidColorBrush(Colors.Yellow),
                null),
            NodeExecutionState.Running => (
                new SolidColorBrush(Colors.DeepSkyBlue),
                new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.DeepSkyBlue,
                    BlurRadius = 10,
                    ShadowDepth = 0
                }),
            NodeExecutionState.Completed => (
                new SolidColorBrush(Colors.LimeGreen),
                new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.LimeGreen,
                    BlurRadius = 10,
                    ShadowDepth = 0
                }),
            NodeExecutionState.Failed => (
                new SolidColorBrush(Colors.Red),
                new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Red,
                    BlurRadius = 10,
                    ShadowDepth = 0
                }),
            _ => (
                new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                null)
        };
    }

    private void UpdateEnabledState()
    {
        DisabledOverlay.Visibility = Node.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Duplicate_Click(object sender, RoutedEventArgs e)
    {
        DuplicateRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Collapse_Click(object sender, RoutedEventArgs e)
    {
        Node.IsCollapsed = !Node.IsCollapsed;
        ContentArea.Visibility = Node.IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseMenuItem.Header = Node.IsCollapsed ? "Expand" : "Collapse";
    }

    private void ToggleEnable_Click(object sender, RoutedEventArgs e)
    {
        Node.IsEnabled = !Node.IsEnabled;
        UpdateEnabledState();
    }
}
