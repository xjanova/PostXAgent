using System.IO;
using System.Windows;
using System.Windows.Controls;
using AIManager.Core.NodeEditor;
using AIManager.Core.NodeEditor.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace AIManager.UI.Views.Pages;

/// <summary>
/// Workflow Editor Page - Visual node-based workflow designer
/// </summary>
public partial class WorkflowEditorPage : Page
{
    private readonly NodeRegistry _nodeRegistry;
    private readonly WorkflowEngine? _workflowEngine;
    private WorkflowNode? _selectedNode;
    private CancellationTokenSource? _executionCts;
    private bool _hasUnsavedChanges;
    private string? _currentFilePath;

    public WorkflowEditorPage()
    {
        InitializeComponent();

        _nodeRegistry = new NodeRegistry();

        // Try to get services
        try
        {
            var loggerFactory = App.Services.GetService<ILoggerFactory>();
            var contentGenerator = App.Services.GetService<ContentGeneratorService>();
            var imageGenerator = App.Services.GetService<ImageGeneratorService>();

            if (loggerFactory != null && contentGenerator != null && imageGenerator != null)
            {
                _workflowEngine = new WorkflowEngine(
                    loggerFactory.CreateLogger<WorkflowEngine>(),
                    _nodeRegistry,
                    contentGenerator,
                    imageGenerator);

                // Wire up engine events
                _workflowEngine.NodeStateChanged += OnNodeStateChanged;
                _workflowEngine.NodeProgressUpdated += OnNodeProgressUpdated;
                _workflowEngine.LogMessage += OnLogMessage;
                _workflowEngine.WorkflowCompleted += OnWorkflowCompleted;
            }
        }
        catch
        {
            // Services not available, run in design mode
            AppendLog("Running in design mode - execution disabled");
        }

        // Create default workflow
        CreateNewWorkflow();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    private void CreateNewWorkflow()
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before creating a new workflow?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SaveWorkflow_Click(this, new RoutedEventArgs());
                if (_hasUnsavedChanges) return; // Save was cancelled
            }
            else if (result == MessageBoxResult.Cancel)
            {
                return;
            }
        }

        var workflow = new WorkflowGraph
        {
            Name = "New Workflow",
            Description = "Created with PostXAgent Workflow Editor"
        };

        EditorCanvas.Workflow = workflow;
        _currentFilePath = null;
        _hasUnsavedChanges = false;

        AppendLog("Created new workflow");
    }

    private void NewWorkflow_Click(object sender, RoutedEventArgs e)
    {
        CreateNewWorkflow();
    }

    private void OpenWorkflow_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Workflow",
            Filter = "Workflow Files|*.workflow;*.json|All Files|*.*",
            InitialDirectory = GetWorkflowsDirectory()
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = File.ReadAllText(dialog.FileName);
                var workflow = JsonConvert.DeserializeObject<WorkflowGraph>(json);

                if (workflow != null)
                {
                    EditorCanvas.Workflow = workflow;
                    _currentFilePath = dialog.FileName;
                    _hasUnsavedChanges = false;

                    AppendLog($"Opened workflow: {workflow.Name}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open workflow: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                AppendLog($"Error opening workflow: {ex.Message}");
            }
        }
    }

    private void SaveWorkflow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Workflow",
                Filter = "Workflow Files|*.workflow|JSON Files|*.json|All Files|*.*",
                DefaultExt = ".workflow",
                FileName = EditorCanvas.Workflow.Name,
                InitialDirectory = GetWorkflowsDirectory()
            };

            if (dialog.ShowDialog() != true)
                return;

            _currentFilePath = dialog.FileName;
        }

        try
        {
            var json = JsonConvert.SerializeObject(EditorCanvas.Workflow, Formatting.Indented);
            File.WriteAllText(_currentFilePath, json);
            _hasUnsavedChanges = false;

            AppendLog($"Saved workflow: {_currentFilePath}");
            MessageBox.Show("Workflow saved successfully!", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save workflow: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            AppendLog($"Error saving workflow: {ex.Message}");
        }
    }

    private string GetWorkflowsDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent", "Workflows");

        Directory.CreateDirectory(dir);
        return dir;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NODE PALETTE EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    private void NodePalette_NodeDragStarted(string nodeType)
    {
        // Double-click creates node at center
        var node = _nodeRegistry.CreateNode(nodeType);
        if (node != null)
        {
            node.Position = new NodePosition(400, 300);
            EditorCanvas.AddNode(node);
            AppendLog($"Added node: {node.Name}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CANVAS EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    private void EditorCanvas_NodeSelected(WorkflowNode node)
    {
        _selectedNode = node;
        SelectedNodeName.Text = node.Name;
        UpdatePropertiesPanel(node);
    }

    private void EditorCanvas_WorkflowChanged(WorkflowGraph workflow)
    {
        _hasUnsavedChanges = true;
    }

    private async void EditorCanvas_ExecuteRequested()
    {
        if (_workflowEngine == null)
        {
            MessageBox.Show("Workflow engine not available. Please restart the application.",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (EditorCanvas.Workflow.Nodes.Count == 0)
        {
            MessageBox.Show("Add some nodes to the workflow first.",
                "No Nodes", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _executionCts = new CancellationTokenSource();
            EditorCanvas.SetExecutionMode(true);
            AppendLog("Starting workflow execution...");

            var result = await _workflowEngine.ExecuteAsync(
                EditorCanvas.Workflow,
                ct: _executionCts.Token);

            if (result.Success)
            {
                AppendLog($"Workflow completed successfully in {result.Duration.TotalSeconds:F2}s");
            }
            else
            {
                AppendLog($"Workflow failed: {result.Error}");
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog("Workflow execution cancelled");
        }
        catch (Exception ex)
        {
            AppendLog($"Execution error: {ex.Message}");
            MessageBox.Show($"Execution failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            EditorCanvas.SetExecutionMode(false);
            _executionCts?.Dispose();
            _executionCts = null;
        }
    }

    private void EditorCanvas_StopRequested()
    {
        _executionCts?.Cancel();
        AppendLog("Stop requested...");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ENGINE EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    private void OnNodeStateChanged(string nodeId, NodeExecutionState state)
    {
        Dispatcher.Invoke(() => EditorCanvas.SetNodeState(nodeId, state));
    }

    private void OnNodeProgressUpdated(string nodeId, double progress)
    {
        // Could update a progress indicator on the node
    }

    private void OnLogMessage(string message)
    {
        Dispatcher.Invoke(() => AppendLog(message));
    }

    private void OnWorkflowCompleted(Core.NodeEditor.WorkflowExecutionResult result)
    {
        Dispatcher.Invoke(() =>
        {
            if (result.Success)
            {
                AppendLog("=== Workflow completed successfully ===");

                // Show outputs
                if (result.FinalOutputs.Count > 0)
                {
                    AppendLog("Final outputs:");
                    foreach (var output in result.FinalOutputs)
                    {
                        var value = output.Value?.ToString() ?? "null";
                        if (value.Length > 100) value = value[..100] + "...";
                        AppendLog($"  {output.Key}: {value}");
                    }
                }
            }
        });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROPERTIES PANEL
    // ═══════════════════════════════════════════════════════════════════════

    private void UpdatePropertiesPanel(WorkflowNode node)
    {
        PropertiesPanel.Children.Clear();

        // Node info section
        AddPropertySection("Node Info");

        AddPropertyRow("Type", node.NodeType);
        AddPropertyRow("ID", node.Id[..8] + "...");

        // Editable name
        var nameBox = new TextBox
        {
            Text = node.Name,
            Margin = new Thickness(0, 5, 0, 10),
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White
        };
        nameBox.TextChanged += (s, e) => node.Name = nameBox.Text;

        PropertiesPanel.Children.Add(new TextBlock
        {
            Text = "Name",
            FontSize = 11,
            Opacity = 0.6,
            Margin = new Thickness(0, 10, 0, 3)
        });
        PropertiesPanel.Children.Add(nameBox);

        // Properties section
        if (node.Properties.Count > 0)
        {
            AddPropertySection("Properties");

            foreach (var prop in node.Properties.Values)
            {
                AddPropertyControl(prop);
            }
        }

        // Inputs section
        if (node.Inputs.Count > 0)
        {
            AddPropertySection("Inputs");

            foreach (var port in node.Inputs)
            {
                AddPropertyRow(port.Name, port.DataType.ToString(),
                    port.IsRequired ? " (required)" : " (optional)");
            }
        }

        // Outputs section
        if (node.Outputs.Count > 0)
        {
            AddPropertySection("Outputs");

            foreach (var port in node.Outputs)
            {
                AddPropertyRow(port.Name, port.DataType.ToString());
            }
        }
    }

    private void AddPropertySection(string title)
    {
        PropertiesPanel.Children.Add(new TextBlock
        {
            Text = title.ToUpper(),
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            Opacity = 0.5,
            Margin = new Thickness(0, 15, 0, 8)
        });

        PropertiesPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
            Margin = new Thickness(0, 0, 0, 8)
        });
    }

    private void AddPropertyRow(string label, string value, string? suffix = null)
    {
        var panel = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        panel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Opacity = 0.7
        };

        var valueText = new TextBlock
        {
            Text = value + (suffix ?? ""),
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Right,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(valueText, 1);

        panel.Children.Add(labelText);
        panel.Children.Add(valueText);

        PropertiesPanel.Children.Add(panel);
    }

    private void AddPropertyControl(NodeProperty prop)
    {
        PropertiesPanel.Children.Add(new TextBlock
        {
            Text = prop.DisplayName,
            FontSize = 11,
            Opacity = 0.7,
            Margin = new Thickness(0, 5, 0, 3),
            ToolTip = prop.Tooltip
        });

        FrameworkElement? control = prop.Type switch
        {
            PropertyType.String => CreateTextBox(prop),
            PropertyType.MultilineText => CreateMultilineTextBox(prop),
            PropertyType.Int or PropertyType.Float => CreateNumberBox(prop),
            PropertyType.Bool => CreateCheckBox(prop),
            PropertyType.Slider => CreateSlider(prop),
            PropertyType.Combo or PropertyType.Enum => CreateComboBox(prop),
            _ => null
        };

        if (control != null)
        {
            PropertiesPanel.Children.Add(control);
        }
    }

    private TextBox CreateTextBox(NodeProperty prop)
    {
        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "",
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White
        };

        textBox.TextChanged += (s, e) => prop.Value = textBox.Text;
        return textBox;
    }

    private TextBox CreateMultilineTextBox(NodeProperty prop)
    {
        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "",
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 60,
            MaxHeight = 150,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        textBox.TextChanged += (s, e) => prop.Value = textBox.Text;
        return textBox;
    }

    private TextBox CreateNumberBox(NodeProperty prop)
    {
        var textBox = new TextBox
        {
            Text = prop.Value?.ToString() ?? "0",
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0),
            Foreground = System.Windows.Media.Brushes.White
        };

        textBox.TextChanged += (s, e) =>
        {
            if (prop.Type == PropertyType.Int && int.TryParse(textBox.Text, out var intVal))
            {
                intVal = (int)Math.Clamp(intVal, prop.Min ?? int.MinValue, prop.Max ?? int.MaxValue);
                prop.Value = intVal;
            }
            else if (double.TryParse(textBox.Text, out var floatVal))
            {
                floatVal = Math.Clamp(floatVal, prop.Min ?? double.MinValue, prop.Max ?? double.MaxValue);
                prop.Value = floatVal;
            }
        };

        return textBox;
    }

    private CheckBox CreateCheckBox(NodeProperty prop)
    {
        var checkBox = new CheckBox
        {
            IsChecked = Convert.ToBoolean(prop.Value ?? false),
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 5, 0, 5)
        };

        checkBox.Checked += (s, e) => prop.Value = true;
        checkBox.Unchecked += (s, e) => prop.Value = false;
        return checkBox;
    }

    private Grid CreateSlider(NodeProperty prop)
    {
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var slider = new Slider
        {
            Minimum = prop.Min ?? 0,
            Maximum = prop.Max ?? 100,
            Value = Convert.ToDouble(prop.Value ?? 0),
            TickFrequency = prop.Step ?? 1,
            IsSnapToTickEnabled = prop.Step.HasValue
        };

        var valueText = new TextBlock
        {
            Text = slider.Value.ToString("F2"),
            FontSize = 11,
            MinWidth = 40,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
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

    private ComboBox CreateComboBox(NodeProperty prop)
    {
        var comboBox = new ComboBox
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(30, 255, 255, 255)),
            Foreground = System.Windows.Media.Brushes.White
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

    // ═══════════════════════════════════════════════════════════════════════
    // LOG
    // ═══════════════════════════════════════════════════════════════════════

    private void AppendLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        LogText.Text += $"[{timestamp}] {message}\n";
        LogScroller.ScrollToEnd();
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        LogText.Text = "";
    }
}
