using AIManager.Core.NodeEditor.Models;
using AIManager.Core.NodeEditor.Nodes;

namespace AIManager.Core.NodeEditor;

/// <summary>
/// Registry for all available node types
/// </summary>
public class NodeRegistry
{
    private readonly Dictionary<string, NodeDefinition> _nodeDefinitions = new();
    private readonly Dictionary<string, List<NodeDefinition>> _nodesByCategory = new();

    public NodeRegistry()
    {
        RegisterBuiltInNodes();
    }

    /// <summary>
    /// Register built-in nodes
    /// </summary>
    private void RegisterBuiltInNodes()
    {
        // Input nodes
        RegisterNode("Input", () => new TextInputNode());
        RegisterNode("Input", () => new NumberInputNode());
        RegisterNode("Input", () => new ImageInputNode());
        RegisterNode("Input", () => new SeedNode());

        // AI nodes
        RegisterNode("AI", () => new AITextGeneratorNode());
        RegisterNode("AI", () => new AIImageGeneratorNode());
        RegisterNode("AI", () => new AIChatNode());

        // Processing nodes
        RegisterNode("Processing", () => new TextCombinerNode());
        RegisterNode("Processing", () => new ImageResizeNode());
        RegisterNode("Processing", () => new SwitchNode());

        // Output nodes
        RegisterNode("Output", () => new SaveImageNode());
        RegisterNode("Output", () => new PreviewNode());
        RegisterNode("Output", () => new ConsoleOutputNode());

        // Social Media nodes
        RegisterNode("Social Media", () => new SocialMediaPostNode());

        // Utility nodes
        RegisterNode("Utility", () => new NoteNode());
        RegisterNode("Utility", () => new GroupNode());
        RegisterNode("Utility", () => new LoopNode());
        RegisterNode("Utility", () => new DelayNode());
    }

    /// <summary>
    /// Register a node type
    /// </summary>
    public void RegisterNode(string category, Func<WorkflowNode> factory)
    {
        var template = factory();
        var definition = new NodeDefinition
        {
            NodeType = template.NodeType,
            Name = template.Name,
            Description = template.Description ?? "",
            Category = category,
            Color = template.Color,
            Icon = template.Icon,
            Factory = factory
        };

        _nodeDefinitions[template.NodeType] = definition;

        if (!_nodesByCategory.ContainsKey(category))
        {
            _nodesByCategory[category] = new List<NodeDefinition>();
        }
        _nodesByCategory[category].Add(definition);
    }

    /// <summary>
    /// Get all categories
    /// </summary>
    public IEnumerable<string> GetCategories()
    {
        return _nodesByCategory.Keys.OrderBy(c => c);
    }

    /// <summary>
    /// Get nodes by category
    /// </summary>
    public IEnumerable<NodeDefinition> GetNodesByCategory(string category)
    {
        return _nodesByCategory.TryGetValue(category, out var nodes)
            ? nodes.OrderBy(n => n.Name)
            : Enumerable.Empty<NodeDefinition>();
    }

    /// <summary>
    /// Get all node definitions
    /// </summary>
    public IEnumerable<NodeDefinition> GetAllNodes()
    {
        return _nodeDefinitions.Values.OrderBy(n => n.Category).ThenBy(n => n.Name);
    }

    /// <summary>
    /// Create a new node instance
    /// </summary>
    public WorkflowNode? CreateNode(string nodeType)
    {
        return _nodeDefinitions.TryGetValue(nodeType, out var definition)
            ? definition.Factory()
            : null;
    }

    /// <summary>
    /// Get node definition by type
    /// </summary>
    public NodeDefinition? GetNodeDefinition(string nodeType)
    {
        return _nodeDefinitions.TryGetValue(nodeType, out var definition)
            ? definition
            : null;
    }

    /// <summary>
    /// Search nodes by name or description
    /// </summary>
    public IEnumerable<NodeDefinition> SearchNodes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllNodes();

        var lowerQuery = query.ToLower();
        return _nodeDefinitions.Values
            .Where(n => n.Name.ToLower().Contains(lowerQuery) ||
                       n.Description.ToLower().Contains(lowerQuery) ||
                       n.NodeType.ToLower().Contains(lowerQuery))
            .OrderBy(n => n.Name);
    }
}

/// <summary>
/// Definition of a node type
/// </summary>
public class NodeDefinition
{
    public string NodeType { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public string Color { get; set; } = "#8B5CF6";
    public string Icon { get; set; } = "Circle";
    public Func<WorkflowNode> Factory { get; set; } = null!;
}
