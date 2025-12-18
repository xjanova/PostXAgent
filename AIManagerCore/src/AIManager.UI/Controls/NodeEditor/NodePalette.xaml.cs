using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AIManager.Core.NodeEditor;
using MaterialDesignThemes.Wpf;

namespace AIManager.UI.Controls.NodeEditor;

/// <summary>
/// Node Palette - displays available nodes for drag & drop
/// </summary>
public partial class NodePalette : UserControl
{
    private readonly NodeRegistry _nodeRegistry;

    public event Action<string>? NodeDragStarted;

    public NodePalette()
    {
        InitializeComponent();
        _nodeRegistry = new NodeRegistry();
        PopulateCategories();
    }

    private void PopulateCategories(string? filter = null)
    {
        CategoriesPanel.Children.Clear();

        foreach (var category in _nodeRegistry.GetCategories())
        {
            var nodes = string.IsNullOrWhiteSpace(filter)
                ? _nodeRegistry.GetNodesByCategory(category).ToList()
                : _nodeRegistry.SearchNodes(filter)
                    .Where(n => n.Category == category)
                    .ToList();

            if (nodes.Count == 0) continue;

            var expander = CreateCategoryExpander(category, nodes);
            CategoriesPanel.Children.Add(expander);
        }

        // If searching and no results
        if (CategoriesPanel.Children.Count == 0 && !string.IsNullOrWhiteSpace(filter))
        {
            CategoriesPanel.Children.Add(new TextBlock
            {
                Text = "No nodes found",
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            });
        }
    }

    private Expander CreateCategoryExpander(string category, IEnumerable<NodeDefinition> nodes)
    {
        // Category header with icon
        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

        var categoryIcon = category switch
        {
            "Input" => PackIconKind.Import,
            "AI" => PackIconKind.Brain,
            "Processing" => PackIconKind.Cogs,
            "Output" => PackIconKind.Export,
            "Social Media" => PackIconKind.ShareVariant,
            "Utility" => PackIconKind.Tools,
            _ => PackIconKind.Circle
        };

        headerPanel.Children.Add(new PackIcon
        {
            Kind = categoryIcon,
            Width = 18,
            Height = 18,
            Margin = new Thickness(0, 0, 8, 0),
            Opacity = 0.7
        });

        headerPanel.Children.Add(new TextBlock
        {
            Text = category.ToUpper(),
            FontWeight = FontWeights.SemiBold,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center
        });

        var nodesPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 0) };

        foreach (var node in nodes)
        {
            var nodeItem = CreateNodeItem(node);
            nodesPanel.Children.Add(nodeItem);
        }

        return new Expander
        {
            Header = headerPanel,
            Content = nodesPanel,
            IsExpanded = true,
            Margin = new Thickness(0, 5, 0, 5)
        };
    }

    private Border CreateNodeItem(NodeDefinition node)
    {
        var color = (Color)ColorConverter.ConvertFromString(node.Color);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Icon
        var iconBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
            Margin = new Thickness(0, 0, 10, 0)
        };

        if (Enum.TryParse<PackIconKind>(node.Icon, out var iconKind))
        {
            iconBorder.Child = new PackIcon
            {
                Kind = iconKind,
                Width = 16,
                Height = 16,
                Foreground = new SolidColorBrush(color),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // Text
        var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        textPanel.Children.Add(new TextBlock
        {
            Text = node.Name,
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(Color.FromArgb(0xEE, 0xFF, 0xFF, 0xFF))
        });

        if (!string.IsNullOrEmpty(node.Description))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = node.Description,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(textPanel, 1);

        grid.Children.Add(iconBorder);
        grid.Children.Add(textPanel);

        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 3, 0, 3),
            Cursor = Cursors.Hand,
            Tag = node.NodeType,
            Child = grid
        };

        // Hover effect
        border.MouseEnter += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(50, color.R, color.G, color.B));
            border.BorderBrush = new SolidColorBrush(Color.FromArgb(100, color.R, color.G, color.B));
            border.BorderThickness = new Thickness(1);
        };

        border.MouseLeave += (s, e) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            border.BorderBrush = null;
            border.BorderThickness = new Thickness(0);
        };

        // Drag support
        border.MouseMove += (s, e) =>
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                NodeDragStarted?.Invoke(node.NodeType);
                DragDrop.DoDragDrop(border, node.NodeType, DragDropEffects.Copy);
            }
        };

        // Double-click to add
        border.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ClickCount == 2)
            {
                NodeDragStarted?.Invoke(node.NodeType);
            }
        };

        return border;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        PopulateCategories(SearchBox.Text);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }
}
