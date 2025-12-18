using AIManager.Core.NodeEditor.Models;

namespace AIManager.Core.NodeEditor.Nodes;

/// <summary>
/// Static class to register all built-in node types for JSON deserialization
/// </summary>
public static class BuiltInNodeRegistration
{
    private static bool _registered = false;

    public static void EnsureRegistered()
    {
        if (_registered) return;
        _registered = true;

        // Input nodes
        NodeTypeRegistry.RegisterNodeType("input.text", () => new TextInputNode());
        NodeTypeRegistry.RegisterNodeType("input.number", () => new NumberInputNode());
        NodeTypeRegistry.RegisterNodeType("input.image", () => new ImageInputNode());
        NodeTypeRegistry.RegisterNodeType("input.seed", () => new SeedNode());

        // AI nodes
        NodeTypeRegistry.RegisterNodeType("ai.text_generator", () => new AITextGeneratorNode());
        NodeTypeRegistry.RegisterNodeType("ai.image_generator", () => new AIImageGeneratorNode());
        NodeTypeRegistry.RegisterNodeType("ai.chat", () => new AIChatNode());

        // Processing nodes
        NodeTypeRegistry.RegisterNodeType("process.text_combiner", () => new TextCombinerNode());
        NodeTypeRegistry.RegisterNodeType("process.image_resize", () => new ImageResizeNode());
        NodeTypeRegistry.RegisterNodeType("process.switch", () => new SwitchNode());

        // Output nodes
        NodeTypeRegistry.RegisterNodeType("output.save_image", () => new SaveImageNode());
        NodeTypeRegistry.RegisterNodeType("output.preview", () => new PreviewNode());
        NodeTypeRegistry.RegisterNodeType("output.console", () => new ConsoleOutputNode());

        // Social media nodes
        NodeTypeRegistry.RegisterNodeType("social.post", () => new SocialMediaPostNode());

        // Utility nodes
        NodeTypeRegistry.RegisterNodeType("util.note", () => new NoteNode());
        NodeTypeRegistry.RegisterNodeType("util.group", () => new GroupNode());
        NodeTypeRegistry.RegisterNodeType("util.loop", () => new LoopNode());
        NodeTypeRegistry.RegisterNodeType("util.delay", () => new DelayNode());
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// INPUT NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Text input node
/// </summary>
public class TextInputNode : WorkflowNode
{
    public override string NodeType => "input.text";

    public TextInputNode()
    {
        Name = "Text Input";
        Description = "Input text value";
        Color = "#10B981";
        Icon = "TextBox";
        Size = new NodeSize(220, 120);

        Outputs.Add(new NodePort
        {
            Name = "text",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#10B981"
        });

        Properties["text"] = new NodeProperty
        {
            Name = "text",
            DisplayName = "Text",
            Type = PropertyType.MultilineText,
            Value = "",
            DefaultValue = ""
        };
    }
}

/// <summary>
/// Number input node
/// </summary>
public class NumberInputNode : WorkflowNode
{
    public override string NodeType => "input.number";

    public NumberInputNode()
    {
        Name = "Number";
        Description = "Input number value";
        Color = "#3B82F6";
        Icon = "Numeric";
        Size = new NodeSize(180, 100);

        Outputs.Add(new NodePort
        {
            Name = "value",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        Properties["value"] = new NodeProperty
        {
            Name = "value",
            DisplayName = "Value",
            Type = PropertyType.Float,
            Value = 0.0,
            DefaultValue = 0.0
        };
    }
}

/// <summary>
/// Image input node (load from file)
/// </summary>
public class ImageInputNode : WorkflowNode
{
    public override string NodeType => "input.image";

    public ImageInputNode()
    {
        Name = "Load Image";
        Description = "Load image from file";
        Color = "#EC4899";
        Icon = "Image";
        Size = new NodeSize(220, 180);

        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#EC4899"
        });

        Properties["path"] = new NodeProperty
        {
            Name = "path",
            DisplayName = "Image Path",
            Type = PropertyType.FilePath,
            Value = "",
            DefaultValue = ""
        };
    }
}

/// <summary>
/// Seed input node for random generation
/// </summary>
public class SeedNode : WorkflowNode
{
    public override string NodeType => "input.seed";

    public SeedNode()
    {
        Name = "Seed";
        Description = "Random seed for reproducible generation";
        Color = "#F59E0B";
        Icon = "Dice3";
        Size = new NodeSize(200, 120);

        Outputs.Add(new NodePort
        {
            Name = "seed",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#F59E0B"
        });

        Properties["seed"] = new NodeProperty
        {
            Name = "seed",
            DisplayName = "Seed",
            Type = PropertyType.Seed,
            Value = -1L,
            DefaultValue = -1L,
            Min = -1,
            Max = long.MaxValue,
            Tooltip = "-1 = random seed each run"
        };

        Properties["randomize"] = new NodeProperty
        {
            Name = "randomize",
            DisplayName = "Randomize",
            Type = PropertyType.Bool,
            Value = true,
            DefaultValue = true
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// AI NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// AI Text Generation node
/// </summary>
public class AITextGeneratorNode : WorkflowNode
{
    public override string NodeType => "ai.text_generator";

    public AITextGeneratorNode()
    {
        Name = "AI Text Generator";
        Description = "Generate text using AI models";
        Color = "#8B5CF6";
        Icon = "Brain";
        Size = new NodeSize(280, 250);

        Inputs.Add(new NodePort
        {
            Name = "prompt",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#10B981"
        });

        Inputs.Add(new NodePort
        {
            Name = "system_prompt",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#10B981"
        });

        Outputs.Add(new NodePort
        {
            Name = "generated_text",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#8B5CF6"
        });

        Properties["provider"] = new NodeProperty
        {
            Name = "provider",
            DisplayName = "AI Provider",
            Type = PropertyType.Combo,
            Value = "ollama",
            DefaultValue = "ollama",
            Options = new List<string> { "ollama", "openai", "anthropic", "gemini" }
        };

        Properties["model"] = new NodeProperty
        {
            Name = "model",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "llama3.2:3b",
            DefaultValue = "llama3.2:3b"
        };

        Properties["temperature"] = new NodeProperty
        {
            Name = "temperature",
            DisplayName = "Temperature",
            Type = PropertyType.Slider,
            Value = 0.7,
            DefaultValue = 0.7,
            Min = 0,
            Max = 2,
            Step = 0.1
        };

        Properties["max_tokens"] = new NodeProperty
        {
            Name = "max_tokens",
            DisplayName = "Max Tokens",
            Type = PropertyType.Int,
            Value = 1000,
            DefaultValue = 1000,
            Min = 1,
            Max = 32000
        };
    }
}

/// <summary>
/// AI Image Generation node
/// </summary>
public class AIImageGeneratorNode : WorkflowNode
{
    public override string NodeType => "ai.image_generator";

    public AIImageGeneratorNode()
    {
        Name = "AI Image Generator";
        Description = "Generate images using Stable Diffusion or other models";
        Color = "#06B6D4";
        Icon = "ImageFilterDrama";
        Size = new NodeSize(300, 350);

        Inputs.Add(new NodePort
        {
            Name = "positive_prompt",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#10B981"
        });

        Inputs.Add(new NodePort
        {
            Name = "negative_prompt",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#EF4444"
        });

        Inputs.Add(new NodePort
        {
            Name = "seed",
            DataType = PortDataType.Number,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#F59E0B"
        });

        Inputs.Add(new NodePort
        {
            Name = "reference_image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#06B6D4"
        });

        Properties["provider"] = new NodeProperty
        {
            Name = "provider",
            DisplayName = "Provider",
            Type = PropertyType.Combo,
            Value = "comfyui",
            DefaultValue = "comfyui",
            Options = new List<string> { "comfyui", "automatic1111", "dalle3", "leonardo" }
        };

        Properties["model"] = new NodeProperty
        {
            Name = "model",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "sd_xl_base_1.0",
            DefaultValue = "sd_xl_base_1.0"
        };

        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 64,
            Max = 4096,
            Step = 64
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 64,
            Max = 4096,
            Step = 64
        };

        Properties["steps"] = new NodeProperty
        {
            Name = "steps",
            DisplayName = "Steps",
            Type = PropertyType.Int,
            Value = 20,
            DefaultValue = 20,
            Min = 1,
            Max = 150
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 7.0,
            DefaultValue = 7.0,
            Min = 1,
            Max = 20,
            Step = 0.5
        };

        Properties["sampler"] = new NodeProperty
        {
            Name = "sampler",
            DisplayName = "Sampler",
            Type = PropertyType.Combo,
            Value = "euler",
            DefaultValue = "euler",
            Options = new List<string> { "euler", "euler_a", "dpm++_2m", "dpm++_sde", "ddim", "lms" }
        };
    }
}

/// <summary>
/// AI Chat node for conversation
/// </summary>
public class AIChatNode : WorkflowNode
{
    public override string NodeType => "ai.chat";

    public AIChatNode()
    {
        Name = "AI Chat";
        Description = "Chat conversation with AI";
        Color = "#A78BFA";
        Icon = "Chat";
        Size = new NodeSize(260, 200);

        Inputs.Add(new NodePort
        {
            Name = "message",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#10B981"
        });

        Inputs.Add(new NodePort
        {
            Name = "context",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = false,
            AllowMultiple = true,
            Color = "#F59E0B"
        });

        Outputs.Add(new NodePort
        {
            Name = "response",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#A78BFA"
        });

        Properties["provider"] = new NodeProperty
        {
            Name = "provider",
            DisplayName = "Provider",
            Type = PropertyType.Combo,
            Value = "ollama",
            DefaultValue = "ollama",
            Options = new List<string> { "ollama", "openai", "anthropic", "gemini" }
        };

        Properties["model"] = new NodeProperty
        {
            Name = "model",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "llama3.2:3b",
            DefaultValue = "llama3.2:3b"
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PROCESSING NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Text combiner node
/// </summary>
public class TextCombinerNode : WorkflowNode
{
    public override string NodeType => "process.text_combiner";

    public TextCombinerNode()
    {
        Name = "Combine Text";
        Description = "Combine multiple text inputs";
        Color = "#22D3EE";
        Icon = "VectorCombine";
        Size = new NodeSize(200, 150);

        Inputs.Add(new NodePort
        {
            Name = "text_1",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#10B981"
        });

        Inputs.Add(new NodePort
        {
            Name = "text_2",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#10B981"
        });

        Outputs.Add(new NodePort
        {
            Name = "combined",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#22D3EE"
        });

        Properties["separator"] = new NodeProperty
        {
            Name = "separator",
            DisplayName = "Separator",
            Type = PropertyType.String,
            Value = "\n",
            DefaultValue = "\n"
        };
    }
}

/// <summary>
/// Image resize node
/// </summary>
public class ImageResizeNode : WorkflowNode
{
    public override string NodeType => "process.image_resize";

    public ImageResizeNode()
    {
        Name = "Resize Image";
        Description = "Resize image to specified dimensions";
        Color = "#F97316";
        Icon = "ImageSizeSelectLarge";
        Size = new NodeSize(220, 180);

        Inputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#F97316"
        });

        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 512,
            DefaultValue = 512,
            Min = 64,
            Max = 4096
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 512,
            DefaultValue = 512,
            Min = 64,
            Max = 4096
        };

        Properties["mode"] = new NodeProperty
        {
            Name = "mode",
            DisplayName = "Mode",
            Type = PropertyType.Combo,
            Value = "scale",
            DefaultValue = "scale",
            Options = new List<string> { "scale", "crop", "pad", "stretch" }
        };
    }
}

/// <summary>
/// Switch/conditional node
/// </summary>
public class SwitchNode : WorkflowNode
{
    public override string NodeType => "process.switch";

    public SwitchNode()
    {
        Name = "Switch";
        Description = "Conditional routing based on boolean";
        Color = "#EAB308";
        Icon = "SourceBranch";
        Size = new NodeSize(180, 140);

        Inputs.Add(new NodePort
        {
            Name = "condition",
            DataType = PortDataType.Boolean,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EAB308"
        });

        Inputs.Add(new NodePort
        {
            Name = "value_true",
            DataType = PortDataType.Any,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#10B981"
        });

        Inputs.Add(new NodePort
        {
            Name = "value_false",
            DataType = PortDataType.Any,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EF4444"
        });

        Outputs.Add(new NodePort
        {
            Name = "output",
            DataType = PortDataType.Any,
            Direction = PortDirection.Output,
            Color = "#EAB308"
        });
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// OUTPUT NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Save image node
/// </summary>
public class SaveImageNode : WorkflowNode
{
    public override string NodeType => "output.save_image";

    public SaveImageNode()
    {
        Name = "Save Image";
        Description = "Save image to file";
        Color = "#EF4444";
        Icon = "ContentSave";
        Size = new NodeSize(240, 160);

        Inputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "path",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#EF4444"
        });

        Properties["folder"] = new NodeProperty
        {
            Name = "folder",
            DisplayName = "Output Folder",
            Type = PropertyType.FolderPath,
            Value = "outputs",
            DefaultValue = "outputs"
        };

        Properties["filename_prefix"] = new NodeProperty
        {
            Name = "filename_prefix",
            DisplayName = "Filename Prefix",
            Type = PropertyType.String,
            Value = "image",
            DefaultValue = "image"
        };

        Properties["format"] = new NodeProperty
        {
            Name = "format",
            DisplayName = "Format",
            Type = PropertyType.Combo,
            Value = "png",
            DefaultValue = "png",
            Options = new List<string> { "png", "jpg", "webp" }
        };

        Properties["quality"] = new NodeProperty
        {
            Name = "quality",
            DisplayName = "Quality",
            Type = PropertyType.Slider,
            Value = 95,
            DefaultValue = 95,
            Min = 1,
            Max = 100
        };
    }
}

/// <summary>
/// Display/Preview node
/// </summary>
public class PreviewNode : WorkflowNode
{
    public override string NodeType => "output.preview";

    public PreviewNode()
    {
        Name = "Preview";
        Description = "Display result in preview panel";
        Color = "#14B8A6";
        Icon = "Eye";
        Size = new NodeSize(200, 200);

        Inputs.Add(new NodePort
        {
            Name = "input",
            DataType = PortDataType.Any,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#14B8A6"
        });
    }
}

/// <summary>
/// Console output node for debugging
/// </summary>
public class ConsoleOutputNode : WorkflowNode
{
    public override string NodeType => "output.console";

    public ConsoleOutputNode()
    {
        Name = "Console Output";
        Description = "Print value to console";
        Color = "#6B7280";
        Icon = "Console";
        Size = new NodeSize(200, 100);

        Inputs.Add(new NodePort
        {
            Name = "input",
            DataType = PortDataType.Any,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#6B7280"
        });

        Properties["label"] = new NodeProperty
        {
            Name = "label",
            DisplayName = "Label",
            Type = PropertyType.String,
            Value = "",
            DefaultValue = ""
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// SOCIAL MEDIA NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Post to social media node
/// </summary>
public class SocialMediaPostNode : WorkflowNode
{
    public override string NodeType => "social.post";

    public SocialMediaPostNode()
    {
        Name = "Social Media Post";
        Description = "Post content to social media platforms";
        Color = "#3B82F6";
        Icon = "ShareVariant";
        Size = new NodeSize(280, 280);

        Inputs.Add(new NodePort
        {
            Name = "text",
            DataType = PortDataType.Text,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#10B981"
        });

        Inputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = false,
            AllowMultiple = true,
            Color = "#EC4899"
        });

        Inputs.Add(new NodePort
        {
            Name = "video",
            DataType = PortDataType.Video,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#EF4444"
        });

        Outputs.Add(new NodePort
        {
            Name = "post_id",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        Outputs.Add(new NodePort
        {
            Name = "url",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#22D3EE"
        });

        Properties["platform"] = new NodeProperty
        {
            Name = "platform",
            DisplayName = "Platform",
            Type = PropertyType.Combo,
            Value = "facebook",
            DefaultValue = "facebook",
            Options = new List<string> { "facebook", "instagram", "twitter", "tiktok", "line", "threads", "youtube", "linkedin", "pinterest" }
        };

        Properties["account_id"] = new NodeProperty
        {
            Name = "account_id",
            DisplayName = "Account",
            Type = PropertyType.String,
            Value = "",
            DefaultValue = ""
        };

        Properties["visibility"] = new NodeProperty
        {
            Name = "visibility",
            DisplayName = "Visibility",
            Type = PropertyType.Combo,
            Value = "public",
            DefaultValue = "public",
            Options = new List<string> { "public", "friends", "private" }
        };

        Properties["schedule"] = new NodeProperty
        {
            Name = "schedule",
            DisplayName = "Schedule Time",
            Type = PropertyType.String,
            Value = "",
            DefaultValue = "",
            Tooltip = "Leave empty to post immediately (format: yyyy-MM-dd HH:mm)"
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// UTILITY NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Note node for adding comments
/// </summary>
public class NoteNode : WorkflowNode
{
    public override string NodeType => "util.note";

    public NoteNode()
    {
        Name = "Note";
        Description = "Add notes to workflow";
        Color = "#FCD34D";
        Icon = "Note";
        Size = new NodeSize(200, 120);

        Properties["text"] = new NodeProperty
        {
            Name = "text",
            DisplayName = "Note",
            Type = PropertyType.MultilineText,
            Value = "Add your notes here...",
            DefaultValue = ""
        };
    }
}

/// <summary>
/// Group node for organizing
/// </summary>
public class GroupNode : WorkflowNode
{
    public override string NodeType => "util.group";

    public GroupNode()
    {
        Name = "Group";
        Description = "Group nodes together";
        Color = "#64748B";
        Icon = "Group";
        Size = new NodeSize(400, 300);

        Properties["title"] = new NodeProperty
        {
            Name = "title",
            DisplayName = "Title",
            Type = PropertyType.String,
            Value = "Group",
            DefaultValue = "Group"
        };

        Properties["color"] = new NodeProperty
        {
            Name = "color",
            DisplayName = "Color",
            Type = PropertyType.Color,
            Value = "#64748B",
            DefaultValue = "#64748B"
        };
    }
}

/// <summary>
/// Loop/Repeat node
/// </summary>
public class LoopNode : WorkflowNode
{
    public override string NodeType => "util.loop";

    public LoopNode()
    {
        Name = "Loop";
        Description = "Repeat workflow section multiple times";
        Color = "#A855F7";
        Icon = "Repeat";
        Size = new NodeSize(200, 150);

        Inputs.Add(new NodePort
        {
            Name = "input",
            DataType = PortDataType.Any,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#A855F7"
        });

        Inputs.Add(new NodePort
        {
            Name = "list",
            DataType = PortDataType.List,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#F59E0B"
        });

        Outputs.Add(new NodePort
        {
            Name = "item",
            DataType = PortDataType.Any,
            Direction = PortDirection.Output,
            Color = "#A855F7"
        });

        Outputs.Add(new NodePort
        {
            Name = "index",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        Properties["count"] = new NodeProperty
        {
            Name = "count",
            DisplayName = "Loop Count",
            Type = PropertyType.Int,
            Value = 1,
            DefaultValue = 1,
            Min = 1,
            Max = 1000,
            Tooltip = "Number of iterations (ignored if list is connected)"
        };
    }
}

/// <summary>
/// Delay node
/// </summary>
public class DelayNode : WorkflowNode
{
    public override string NodeType => "util.delay";

    public DelayNode()
    {
        Name = "Delay";
        Description = "Add delay between operations";
        Color = "#9CA3AF";
        Icon = "Clock";
        Size = new NodeSize(180, 100);

        Inputs.Add(new NodePort
        {
            Name = "trigger",
            DataType = PortDataType.Any,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#9CA3AF"
        });

        Outputs.Add(new NodePort
        {
            Name = "trigger",
            DataType = PortDataType.Any,
            Direction = PortDirection.Output,
            Color = "#9CA3AF"
        });

        Properties["delay_ms"] = new NodeProperty
        {
            Name = "delay_ms",
            DisplayName = "Delay (ms)",
            Type = PropertyType.Int,
            Value = 1000,
            DefaultValue = 1000,
            Min = 0,
            Max = 60000
        };
    }
}
