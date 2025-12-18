using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AIManager.Core.NodeEditor.Models;

/// <summary>
/// Custom JSON converter for WorkflowNode to handle polymorphic deserialization
/// </summary>
public class WorkflowNodeConverter : JsonConverter<WorkflowNode>
{
    // Flag to prevent infinite recursion during serialization
    [ThreadStatic]
    private static bool _isWriting;

    public override WorkflowNode? ReadJson(JsonReader reader, Type objectType, WorkflowNode? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);
        var nodeType = jsonObject["type"]?.Value<string>() ?? "";

        // Get the concrete node type from registry
        var node = NodeTypeRegistry.CreateNode(nodeType);
        if (node == null)
        {
            // Fallback to GenericNode if type not found
            node = new GenericNode(nodeType);
        }

        // Populate the node properties
        serializer.Populate(jsonObject.CreateReader(), node);
        return node;
    }

    public override bool CanWrite => !_isWriting;

    public override void WriteJson(JsonWriter writer, WorkflowNode? value, JsonSerializer serializer)
    {
        // Prevent infinite recursion by temporarily disabling this converter
        _isWriting = true;
        try
        {
            serializer.Serialize(writer, value);
        }
        finally
        {
            _isWriting = false;
        }
    }
}

/// <summary>
/// Registry to map node types to concrete classes
/// </summary>
public static class NodeTypeRegistry
{
    private static readonly Dictionary<string, Func<WorkflowNode>> _nodeFactories = new();

    static NodeTypeRegistry()
    {
        // Register built-in node types (these will be populated by BuiltInNodes)
    }

    public static void RegisterNodeType(string nodeType, Func<WorkflowNode> factory)
    {
        _nodeFactories[nodeType] = factory;
    }

    public static WorkflowNode? CreateNode(string nodeType)
    {
        if (_nodeFactories.TryGetValue(nodeType, out var factory))
        {
            return factory();
        }
        return null;
    }

    public static IEnumerable<string> GetRegisteredTypes() => _nodeFactories.Keys;
}

/// <summary>
/// Generic node for unknown/custom types - allows loading workflows with unrecognized nodes
/// </summary>
public class GenericNode : WorkflowNode
{
    private readonly string _nodeType;

    public GenericNode(string nodeType)
    {
        _nodeType = nodeType;
        Name = $"Unknown: {nodeType}";
        Color = "#666666";
        Icon = "Help";
    }

    public override string NodeType => _nodeType;
}

/// <summary>
/// Base class for all workflow nodes
/// </summary>
[JsonConverter(typeof(WorkflowNodeConverter))]
public abstract class WorkflowNode
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("type")]
    public abstract string NodeType { get; }

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("position")]
    public NodePosition Position { get; set; } = new();

    [JsonProperty("size")]
    public NodeSize Size { get; set; } = new(200, 150);

    [JsonProperty("inputs")]
    public List<NodePort> Inputs { get; set; } = new();

    [JsonProperty("outputs")]
    public List<NodePort> Outputs { get; set; } = new();

    [JsonProperty("properties")]
    public Dictionary<string, NodeProperty> Properties { get; set; } = new();

    [JsonProperty("color")]
    public string Color { get; set; } = "#8B5CF6";

    [JsonProperty("icon")]
    public string Icon { get; set; } = "Circle";

    [JsonProperty("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonProperty("is_collapsed")]
    public bool IsCollapsed { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Position of node in the canvas
/// </summary>
public class NodePosition
{
    [JsonProperty("x")]
    public double X { get; set; }

    [JsonProperty("y")]
    public double Y { get; set; }

    public NodePosition() { }

    public NodePosition(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Size of node
/// </summary>
public class NodeSize
{
    [JsonProperty("width")]
    public double Width { get; set; } = 200;

    [JsonProperty("height")]
    public double Height { get; set; } = 150;

    public NodeSize() { }

    public NodeSize(double width, double height)
    {
        Width = width;
        Height = height;
    }
}

/// <summary>
/// Input/Output port of a node
/// </summary>
public class NodePort
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("data_type")]
    public PortDataType DataType { get; set; }

    [JsonProperty("direction")]
    public PortDirection Direction { get; set; }

    [JsonProperty("is_required")]
    public bool IsRequired { get; set; }

    [JsonProperty("allow_multiple")]
    public bool AllowMultiple { get; set; }

    [JsonProperty("default_value")]
    public object? DefaultValue { get; set; }

    [JsonProperty("color")]
    public string Color { get; set; } = "#FFFFFF";
}

/// <summary>
/// Data types for ports
/// </summary>
public enum PortDataType
{
    Any,
    Text,
    Number,
    Boolean,
    Image,
    Video,
    Audio,
    File,
    List,
    Dictionary,
    Model,
    Latent,
    Conditioning,
    Mask,
    Embedding,
    Workflow
}

/// <summary>
/// Port direction
/// </summary>
public enum PortDirection
{
    Input,
    Output
}

/// <summary>
/// Property of a node (configurable parameter)
/// </summary>
public class NodeProperty
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("display_name")]
    public string DisplayName { get; set; } = "";

    [JsonProperty("type")]
    public PropertyType Type { get; set; }

    [JsonProperty("value")]
    public object? Value { get; set; }

    [JsonProperty("default_value")]
    public object? DefaultValue { get; set; }

    [JsonProperty("min")]
    public double? Min { get; set; }

    [JsonProperty("max")]
    public double? Max { get; set; }

    [JsonProperty("step")]
    public double? Step { get; set; }

    [JsonProperty("options")]
    public List<string>? Options { get; set; }

    [JsonProperty("is_visible")]
    public bool IsVisible { get; set; } = true;

    [JsonProperty("tooltip")]
    public string? Tooltip { get; set; }
}

/// <summary>
/// Property types
/// </summary>
public enum PropertyType
{
    String,
    Int,
    Float,
    Bool,
    Enum,
    Color,
    FilePath,
    FolderPath,
    MultilineText,
    Password,
    Slider,
    Seed,
    Combo
}

/// <summary>
/// Connection between two nodes
/// </summary>
public class NodeConnection
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("source_node_id")]
    public string SourceNodeId { get; set; } = "";

    [JsonProperty("source_port_id")]
    public string SourcePortId { get; set; } = "";

    [JsonProperty("target_node_id")]
    public string TargetNodeId { get; set; } = "";

    [JsonProperty("target_port_id")]
    public string TargetPortId { get; set; } = "";

    [JsonProperty("color")]
    public string Color { get; set; } = "#8B5CF6";

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Complete workflow graph
/// </summary>
public class WorkflowGraph
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("name")]
    public string Name { get; set; } = "New Workflow";

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonProperty("nodes")]
    public List<WorkflowNode> Nodes { get; set; } = new();

    [JsonProperty("connections")]
    public List<NodeConnection> Connections { get; set; } = new();

    [JsonProperty("viewport")]
    public ViewportState Viewport { get; set; } = new();

    [JsonProperty("variables")]
    public Dictionary<string, object> Variables { get; set; } = new();

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("author")]
    public string? Author { get; set; }

    [JsonProperty("tags")]
    public List<string>? Tags { get; set; }

    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Add a node to the graph
    /// </summary>
    public void AddNode(WorkflowNode node)
    {
        Nodes.Add(node);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Remove a node and its connections
    /// </summary>
    public void RemoveNode(string nodeId)
    {
        Nodes.RemoveAll(n => n.Id == nodeId);
        Connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Add a connection between nodes
    /// </summary>
    public bool AddConnection(NodeConnection connection)
    {
        // Validate connection
        var sourceNode = Nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
        var targetNode = Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);

        if (sourceNode == null || targetNode == null)
            return false;

        var sourcePort = sourceNode.Outputs.FirstOrDefault(p => p.Id == connection.SourcePortId);
        var targetPort = targetNode.Inputs.FirstOrDefault(p => p.Id == connection.TargetPortId);

        if (sourcePort == null || targetPort == null)
            return false;

        // Check if port types are compatible
        if (!ArePortsCompatible(sourcePort.DataType, targetPort.DataType))
            return false;

        // Remove existing connection to this input if not allowing multiple
        if (!targetPort.AllowMultiple)
        {
            Connections.RemoveAll(c => c.TargetNodeId == connection.TargetNodeId &&
                                       c.TargetPortId == connection.TargetPortId);
        }

        Connections.Add(connection);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// Remove a connection
    /// </summary>
    public void RemoveConnection(string connectionId)
    {
        Connections.RemoveAll(c => c.Id == connectionId);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Get nodes in execution order (topological sort)
    /// </summary>
    public List<WorkflowNode> GetExecutionOrder()
    {
        var result = new List<WorkflowNode>();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        void Visit(string nodeId)
        {
            if (visited.Contains(nodeId)) return;
            if (visiting.Contains(nodeId))
                throw new InvalidOperationException("Workflow contains a cycle");

            visiting.Add(nodeId);

            var incomingConnections = Connections.Where(c => c.TargetNodeId == nodeId);
            foreach (var conn in incomingConnections)
            {
                Visit(conn.SourceNodeId);
            }

            visiting.Remove(nodeId);
            visited.Add(nodeId);

            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
                result.Add(node);
        }

        foreach (var node in Nodes)
        {
            Visit(node.Id);
        }

        return result;
    }

    private static bool ArePortsCompatible(PortDataType source, PortDataType target)
    {
        if (source == PortDataType.Any || target == PortDataType.Any)
            return true;

        return source == target;
    }
}

/// <summary>
/// Viewport state for canvas
/// </summary>
public class ViewportState
{
    [JsonProperty("pan_x")]
    public double PanX { get; set; }

    [JsonProperty("pan_y")]
    public double PanY { get; set; }

    [JsonProperty("zoom")]
    public double Zoom { get; set; } = 1.0;
}

/// <summary>
/// Node execution context
/// </summary>
public class NodeExecutionContext
{
    public string NodeId { get; set; } = "";
    public Dictionary<string, object?> InputValues { get; set; } = new();
    public Dictionary<string, object?> OutputValues { get; set; } = new();
    public Dictionary<string, object> Variables { get; set; } = new();
    public CancellationToken CancellationToken { get; set; }
    public IProgress<double>? Progress { get; set; }
    public Action<string>? Log { get; set; }
}

/// <summary>
/// Node execution result
/// </summary>
public class NodeExecutionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object?> Outputs { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
}
