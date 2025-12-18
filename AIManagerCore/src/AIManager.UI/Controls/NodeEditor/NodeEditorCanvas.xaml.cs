using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AIManager.Core.NodeEditor;
using AIManager.Core.NodeEditor.Models;
using AIManager.Core.NodeEditor.Nodes;

namespace AIManager.UI.Controls.NodeEditor;

/// <summary>
/// Node Editor Canvas - supports drag & drop, zoom, pan, and connections
/// </summary>
public partial class NodeEditorCanvas : UserControl
{
    private readonly NodeRegistry _nodeRegistry;
    private WorkflowGraph _workflow = new();

    // Canvas state
    private double _zoom = 1.0;
    private Point _panOffset = new(0, 0);
    private Point _lastMousePosition;
    private bool _isPanning;
    private bool _isSelecting;
    private Point _selectionStart;

    // Connection dragging
    private bool _isDraggingConnection;
    private NodeControl? _connectionSourceNode;
    private NodePort? _connectionSourcePort;
    private bool _isConnectionFromOutput;

    // Node dragging
    private bool _isDraggingNode;
    private NodeControl? _draggedNode;
    private Point _dragOffset;

    // Selection
    private readonly List<NodeControl> _selectedNodes = new();

    // Undo/Redo
    private readonly Stack<WorkflowGraph> _undoStack = new();
    private readonly Stack<WorkflowGraph> _redoStack = new();

    // Node controls mapping
    private readonly Dictionary<string, NodeControl> _nodeControls = new();
    private readonly Dictionary<string, Path> _connectionPaths = new();

    // Events
    public event Action<WorkflowNode>? NodeSelected;
    public event Action<WorkflowGraph>? WorkflowChanged;
    public event Action? ExecuteRequested;
    public event Action? StopRequested;

    public WorkflowGraph Workflow
    {
        get => _workflow;
        set
        {
            _workflow = value;
            RefreshCanvas();
        }
    }

    public NodeEditorCanvas()
    {
        InitializeComponent();

        // Register all built-in node types for JSON deserialization
        BuiltInNodeRegistration.EnsureRegistered();

        _nodeRegistry = new NodeRegistry();

        // Set initial zoom display
        UpdateZoomDisplay();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Add a node to the canvas
    /// </summary>
    public void AddNode(WorkflowNode node, Point? position = null)
    {
        SaveUndoState();

        if (position.HasValue)
        {
            node.Position = new NodePosition(position.Value.X, position.Value.Y);
        }

        _workflow.AddNode(node);
        CreateNodeControl(node);
        UpdateStatus();
        WorkflowChanged?.Invoke(_workflow);
    }

    /// <summary>
    /// Remove selected nodes
    /// </summary>
    public void DeleteSelectedNodes()
    {
        if (_selectedNodes.Count == 0) return;

        SaveUndoState();

        foreach (var nodeControl in _selectedNodes.ToList())
        {
            var nodeId = nodeControl.Node.Id;
            _workflow.RemoveNode(nodeId);

            // Remove from canvas
            NodeLayer.Children.Remove(nodeControl);
            _nodeControls.Remove(nodeId);

            // Remove associated connections
            RemoveConnectionsForNode(nodeId);
        }

        _selectedNodes.Clear();
        UpdateStatus();
        WorkflowChanged?.Invoke(_workflow);
    }

    /// <summary>
    /// Clear all nodes
    /// </summary>
    public void ClearCanvas()
    {
        SaveUndoState();

        _workflow = new WorkflowGraph();
        NodeLayer.Children.Clear();
        ConnectionLayer.Children.Clear();
        _nodeControls.Clear();
        _connectionPaths.Clear();
        _selectedNodes.Clear();
        UpdateStatus();
        WorkflowChanged?.Invoke(_workflow);
    }

    /// <summary>
    /// Refresh canvas from workflow
    /// </summary>
    public void RefreshCanvas()
    {
        NodeLayer.Children.Clear();
        ConnectionLayer.Children.Clear();
        _nodeControls.Clear();
        _connectionPaths.Clear();
        _selectedNodes.Clear();

        // Create node controls
        foreach (var node in _workflow.Nodes)
        {
            CreateNodeControl(node);
        }

        // Create connection paths
        foreach (var connection in _workflow.Connections)
        {
            CreateConnectionPath(connection);
        }

        UpdateStatus();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NODE CONTROL CREATION
    // ═══════════════════════════════════════════════════════════════════════

    private void CreateNodeControl(WorkflowNode node)
    {
        var nodeControl = new NodeControl(node);

        // Set position
        Canvas.SetLeft(nodeControl, node.Position.X);
        Canvas.SetTop(nodeControl, node.Position.Y);

        // Wire up events
        nodeControl.MouseLeftButtonDown += NodeControl_MouseLeftButtonDown;
        nodeControl.MouseLeftButtonUp += NodeControl_MouseLeftButtonUp;
        nodeControl.MouseMove += NodeControl_MouseMove;
        nodeControl.PortMouseDown += NodeControl_PortMouseDown;
        nodeControl.PortMouseUp += NodeControl_PortMouseUp;
        nodeControl.DeleteRequested += NodeControl_DeleteRequested;
        nodeControl.DuplicateRequested += NodeControl_DuplicateRequested;

        NodeLayer.Children.Add(nodeControl);
        _nodeControls[node.Id] = nodeControl;
    }

    private void CreateConnectionPath(NodeConnection connection)
    {
        if (!_nodeControls.TryGetValue(connection.SourceNodeId, out var sourceControl) ||
            !_nodeControls.TryGetValue(connection.TargetNodeId, out var targetControl))
            return;

        var sourcePt = sourceControl.GetPortPosition(connection.SourcePortId, true);
        var targetPt = targetControl.GetPortPosition(connection.TargetPortId, false);

        var path = CreateBezierPath(sourcePt, targetPt, connection.Color);
        path.Tag = connection.Id;
        path.MouseRightButtonDown += ConnectionPath_RightClick;

        ConnectionLayer.Children.Add(path);
        _connectionPaths[connection.Id] = path;
    }

    private Path CreateBezierPath(Point start, Point end, string color)
    {
        var controlOffset = Math.Abs(end.X - start.X) * 0.5;
        controlOffset = Math.Max(controlOffset, 50);

        var pathFigure = new PathFigure
        {
            StartPoint = start,
            Segments =
            {
                new BezierSegment(
                    new Point(start.X + controlOffset, start.Y),
                    new Point(end.X - controlOffset, end.Y),
                    end,
                    true)
            }
        };

        var pathGeometry = new PathGeometry { Figures = { pathFigure } };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 0),
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString(color), 0),
                new GradientStop((Color)ColorConverter.ConvertFromString(color), 1)
            }
        };

        return new Path
        {
            Data = pathGeometry,
            Stroke = brush,
            StrokeThickness = 3,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = (Color)ColorConverter.ConvertFromString(color),
                BlurRadius = 8,
                ShadowDepth = 0,
                Opacity = 0.5
            }
        };
    }

    private void UpdateConnectionPath(string connectionId)
    {
        var connection = _workflow.Connections.FirstOrDefault(c => c.Id == connectionId);
        if (connection == null) return;

        if (!_connectionPaths.TryGetValue(connectionId, out var path)) return;
        if (!_nodeControls.TryGetValue(connection.SourceNodeId, out var sourceControl)) return;
        if (!_nodeControls.TryGetValue(connection.TargetNodeId, out var targetControl)) return;

        var sourcePt = sourceControl.GetPortPosition(connection.SourcePortId, true);
        var targetPt = targetControl.GetPortPosition(connection.TargetPortId, false);

        var controlOffset = Math.Abs(targetPt.X - sourcePt.X) * 0.5;
        controlOffset = Math.Max(controlOffset, 50);

        var pathFigure = new PathFigure
        {
            StartPoint = sourcePt,
            Segments =
            {
                new BezierSegment(
                    new Point(sourcePt.X + controlOffset, sourcePt.Y),
                    new Point(targetPt.X - controlOffset, targetPt.Y),
                    targetPt,
                    true)
            }
        };

        ((PathGeometry)path.Data).Figures[0] = pathFigure;
    }

    private void RemoveConnectionsForNode(string nodeId)
    {
        var connectionsToRemove = _workflow.Connections
            .Where(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId)
            .ToList();

        foreach (var conn in connectionsToRemove)
        {
            if (_connectionPaths.TryGetValue(conn.Id, out var path))
            {
                ConnectionLayer.Children.Remove(path);
                _connectionPaths.Remove(conn.Id);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NODE EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    private void NodeControl_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not NodeControl nodeControl) return;

        // Handle selection
        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !_selectedNodes.Contains(nodeControl))
        {
            ClearSelection();
        }

        SelectNode(nodeControl);
        NodeSelected?.Invoke(nodeControl.Node);

        // Start dragging
        _isDraggingNode = true;
        _draggedNode = nodeControl;
        _dragOffset = e.GetPosition(nodeControl);
        nodeControl.CaptureMouse();

        e.Handled = true;
    }

    private void NodeControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingNode && _draggedNode != null)
        {
            _draggedNode.ReleaseMouseCapture();
            _isDraggingNode = false;
            _draggedNode = null;

            // Save position to workflow
            WorkflowChanged?.Invoke(_workflow);
        }
    }

    private void NodeControl_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingNode || _draggedNode == null) return;

        var pos = e.GetPosition(NodeCanvas);
        var newX = pos.X - _dragOffset.X;
        var newY = pos.Y - _dragOffset.Y;

        // Snap to grid
        newX = Math.Round(newX / 10) * 10;
        newY = Math.Round(newY / 10) * 10;

        Canvas.SetLeft(_draggedNode, newX);
        Canvas.SetTop(_draggedNode, newY);

        // Update node position in workflow
        _draggedNode.Node.Position.X = newX;
        _draggedNode.Node.Position.Y = newY;

        // Update connected paths
        foreach (var conn in _workflow.Connections.Where(c =>
            c.SourceNodeId == _draggedNode.Node.Id ||
            c.TargetNodeId == _draggedNode.Node.Id))
        {
            UpdateConnectionPath(conn.Id);
        }
    }

    private void NodeControl_PortMouseDown(object sender, PortEventArgs e)
    {
        _isDraggingConnection = true;
        _connectionSourceNode = (NodeControl)sender;
        _connectionSourcePort = e.Port;
        _isConnectionFromOutput = e.IsOutput;

        // Show temp connection line
        TempConnectionLine.Visibility = Visibility.Visible;
        UpdateTempConnection(e.Position);
    }

    private void NodeControl_PortMouseUp(object sender, PortEventArgs e)
    {
        if (!_isDraggingConnection || _connectionSourceNode == null) return;

        var targetNode = (NodeControl)sender;

        // Can't connect to same node
        if (targetNode == _connectionSourceNode)
        {
            CancelConnectionDrag();
            return;
        }

        // Determine source and target
        NodeControl sourceNode, targetNodeCtrl;
        NodePort sourcePort, targetPort;

        if (_isConnectionFromOutput)
        {
            sourceNode = _connectionSourceNode;
            sourcePort = _connectionSourcePort!;
            targetNodeCtrl = targetNode;
            targetPort = e.Port;
        }
        else
        {
            sourceNode = targetNode;
            sourcePort = e.Port;
            targetNodeCtrl = _connectionSourceNode;
            targetPort = _connectionSourcePort!;
        }

        // Create connection
        var connection = new NodeConnection
        {
            SourceNodeId = sourceNode.Node.Id,
            SourcePortId = sourcePort.Id,
            TargetNodeId = targetNodeCtrl.Node.Id,
            TargetPortId = targetPort.Id,
            Color = sourcePort.Color
        };

        if (_workflow.AddConnection(connection))
        {
            SaveUndoState();
            CreateConnectionPath(connection);
            UpdateStatus();
            WorkflowChanged?.Invoke(_workflow);
        }

        CancelConnectionDrag();
    }

    private void NodeControl_DeleteRequested(object sender, EventArgs e)
    {
        if (sender is NodeControl nodeControl)
        {
            SelectNode(nodeControl);
            DeleteSelectedNodes();
        }
    }

    private void NodeControl_DuplicateRequested(object sender, EventArgs e)
    {
        if (sender is NodeControl nodeControl)
        {
            var newNode = _nodeRegistry.CreateNode(nodeControl.Node.NodeType);
            if (newNode != null)
            {
                newNode.Position = new NodePosition(
                    nodeControl.Node.Position.X + 30,
                    nodeControl.Node.Position.Y + 30);
                AddNode(newNode);
            }
        }
    }

    private void ConnectionPath_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Path path && path.Tag is string connectionId)
        {
            SaveUndoState();
            _workflow.RemoveConnection(connectionId);
            ConnectionLayer.Children.Remove(path);
            _connectionPaths.Remove(connectionId);
            UpdateStatus();
            WorkflowChanged?.Invoke(_workflow);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CANVAS EVENTS
    // ═══════════════════════════════════════════════════════════════════════

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Source != sender) return;

        ClearSelection();
        _lastMousePosition = e.GetPosition(this);
        Focus();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isPanning = false;
        _isSelecting = false;
        SelectionRect.Visibility = Visibility.Collapsed;

        if (_isDraggingConnection)
        {
            CancelConnectionDrag();
        }
    }

    private void Canvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isPanning = true;
        _lastMousePosition = e.GetPosition(this);
        Cursor = Cursors.Hand;
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var currentPos = e.GetPosition(this);

        if (_isPanning && e.RightButton == MouseButtonState.Pressed)
        {
            var delta = currentPos - _lastMousePosition;
            _panOffset.X += delta.X;
            _panOffset.Y += delta.Y;

            CanvasTranslate.X = _panOffset.X;
            CanvasTranslate.Y = _panOffset.Y;
            GridTranslateTransform.X = _panOffset.X % 50;
            GridTranslateTransform.Y = _panOffset.Y % 50;

            _lastMousePosition = currentPos;
        }
        else if (_isDraggingConnection)
        {
            var canvasPos = e.GetPosition(NodeCanvas);
            UpdateTempConnection(canvasPos);
        }
        else
        {
            Cursor = Cursors.Arrow;
        }
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var mousePos = e.GetPosition(NodeCanvas);
        var delta = e.Delta > 0 ? 1.1 : 0.9;

        var newZoom = _zoom * delta;
        newZoom = Math.Max(0.1, Math.Min(3.0, newZoom));

        // Zoom towards mouse position
        var oldZoom = _zoom;
        _zoom = newZoom;

        CanvasScale.ScaleX = _zoom;
        CanvasScale.ScaleY = _zoom;
        GridScaleTransform.ScaleX = _zoom;
        GridScaleTransform.ScaleY = _zoom;

        // Adjust pan to zoom towards mouse
        var factor = 1 - newZoom / oldZoom;
        _panOffset.X += (mousePos.X - _panOffset.X) * factor;
        _panOffset.Y += (mousePos.Y - _panOffset.Y) * factor;

        CanvasTranslate.X = _panOffset.X;
        CanvasTranslate.Y = _panOffset.Y;

        UpdateZoomDisplay();
    }

    private void Canvas_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteSelectedNodes();
        }
        else if (e.Key == Key.Z && Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            Undo();
        }
        else if (e.Key == Key.Y && Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            Redo();
        }
        else if (e.Key == Key.A && Keyboard.IsKeyDown(Key.LeftCtrl))
        {
            SelectAllNodes();
        }
        else if (e.Key == Key.F5)
        {
            ExecuteRequested?.Invoke();
        }
    }

    private void Canvas_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(string)))
        {
            var nodeType = (string)e.Data.GetData(typeof(string));
            var node = _nodeRegistry.CreateNode(nodeType);

            if (node != null)
            {
                var pos = e.GetPosition(NodeCanvas);
                AddNode(node, pos);
            }
        }
    }

    private void Canvas_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(string))
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TOOLBAR HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom * 1.2);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_zoom / 1.2);

    private void FitView_Click(object sender, RoutedEventArgs e)
    {
        if (_workflow.Nodes.Count == 0) return;

        // Calculate bounds
        var minX = _workflow.Nodes.Min(n => n.Position.X);
        var maxX = _workflow.Nodes.Max(n => n.Position.X + n.Size.Width);
        var minY = _workflow.Nodes.Min(n => n.Position.Y);
        var maxY = _workflow.Nodes.Max(n => n.Position.Y + n.Size.Height);

        var width = maxX - minX + 100;
        var height = maxY - minY + 100;

        var scaleX = ActualWidth / width;
        var scaleY = ActualHeight / height;
        var scale = Math.Min(scaleX, scaleY);
        scale = Math.Max(0.1, Math.Min(1.5, scale));

        SetZoom(scale);

        // Center view
        _panOffset.X = (ActualWidth - width * scale) / 2 - minX * scale;
        _panOffset.Y = (ActualHeight - height * scale) / 2 - minY * scale;

        CanvasTranslate.X = _panOffset.X;
        CanvasTranslate.Y = _panOffset.Y;
    }

    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();
    private void Execute_Click(object sender, RoutedEventArgs e) => ExecuteRequested?.Invoke();
    private void Stop_Click(object sender, RoutedEventArgs e) => StopRequested?.Invoke();

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void SetZoom(double zoom)
    {
        _zoom = Math.Max(0.1, Math.Min(3.0, zoom));
        CanvasScale.ScaleX = _zoom;
        CanvasScale.ScaleY = _zoom;
        GridScaleTransform.ScaleX = _zoom;
        GridScaleTransform.ScaleY = _zoom;
        UpdateZoomDisplay();
    }

    private void UpdateZoomDisplay()
    {
        ZoomLevelText.Text = $"{_zoom * 100:F0}%";
    }

    private void SelectNode(NodeControl node)
    {
        if (!_selectedNodes.Contains(node))
        {
            _selectedNodes.Add(node);
            node.IsSelected = true;
        }
    }

    private void ClearSelection()
    {
        foreach (var node in _selectedNodes)
        {
            node.IsSelected = false;
        }
        _selectedNodes.Clear();
    }

    private void SelectAllNodes()
    {
        foreach (var node in _nodeControls.Values)
        {
            SelectNode(node);
        }
    }

    private void UpdateTempConnection(Point endPoint)
    {
        if (_connectionSourceNode == null) return;

        var startPoint = _connectionSourceNode.GetPortPosition(
            _connectionSourcePort!.Id,
            _isConnectionFromOutput);

        var controlOffset = Math.Abs(endPoint.X - startPoint.X) * 0.5;
        controlOffset = Math.Max(controlOffset, 50);

        var start = _isConnectionFromOutput ? startPoint : endPoint;
        var end = _isConnectionFromOutput ? endPoint : startPoint;

        var pathFigure = new PathFigure
        {
            StartPoint = start,
            Segments =
            {
                new BezierSegment(
                    new Point(start.X + controlOffset, start.Y),
                    new Point(end.X - controlOffset, end.Y),
                    end,
                    true)
            }
        };

        TempConnectionLine.Data = new PathGeometry { Figures = { pathFigure } };
    }

    private void CancelConnectionDrag()
    {
        _isDraggingConnection = false;
        _connectionSourceNode = null;
        _connectionSourcePort = null;
        TempConnectionLine.Visibility = Visibility.Collapsed;
    }

    private void SaveUndoState()
    {
        // Deep clone workflow for undo
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_workflow);
        var clone = Newtonsoft.Json.JsonConvert.DeserializeObject<WorkflowGraph>(json);
        if (clone != null)
        {
            _undoStack.Push(clone);
            _redoStack.Clear();
        }
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_workflow);
        var current = Newtonsoft.Json.JsonConvert.DeserializeObject<WorkflowGraph>(json);
        if (current != null) _redoStack.Push(current);

        _workflow = _undoStack.Pop();
        RefreshCanvas();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0) return;

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_workflow);
        var current = Newtonsoft.Json.JsonConvert.DeserializeObject<WorkflowGraph>(json);
        if (current != null) _undoStack.Push(current);

        _workflow = _redoStack.Pop();
        RefreshCanvas();
    }

    private void UpdateStatus()
    {
        NodeCountText.Text = $" | Nodes: {_workflow.Nodes.Count}";
        ConnectionCountText.Text = $" | Connections: {_workflow.Connections.Count}";
    }

    /// <summary>
    /// Update node execution state visually
    /// </summary>
    public void SetNodeState(string nodeId, NodeExecutionState state)
    {
        if (_nodeControls.TryGetValue(nodeId, out var control))
        {
            Dispatcher.Invoke(() => control.ExecutionState = state);
        }
    }

    /// <summary>
    /// Enable/disable execution buttons
    /// </summary>
    public void SetExecutionMode(bool isRunning)
    {
        Dispatcher.Invoke(() =>
        {
            BtnPlay.IsEnabled = !isRunning;
            BtnStop.IsEnabled = isRunning;
            StatusText.Text = isRunning ? "Running..." : "Ready";
        });
    }
}

/// <summary>
/// Event args for port events
/// </summary>
public class PortEventArgs : EventArgs
{
    public NodePort Port { get; }
    public bool IsOutput { get; }
    public Point Position { get; }

    public PortEventArgs(NodePort port, bool isOutput, Point position)
    {
        Port = port;
        IsOutput = isOutput;
        Position = position;
    }
}
