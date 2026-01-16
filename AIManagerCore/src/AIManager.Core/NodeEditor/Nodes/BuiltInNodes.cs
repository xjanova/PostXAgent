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

        // Diffusers nodes
        NodeTypeRegistry.RegisterNodeType("diffusers.load_model", () => new DiffusersLoadModelNode());
        NodeTypeRegistry.RegisterNodeType("diffusers.generate_image", () => new DiffusersGenerateImageNode());
        NodeTypeRegistry.RegisterNodeType("diffusers.generate_video", () => new DiffusersGenerateVideoNode());
        NodeTypeRegistry.RegisterNodeType("diffusers.lora", () => new DiffusersLoRANode());
        NodeTypeRegistry.RegisterNodeType("diffusers.controlnet", () => new DiffusersControlNetNode());
        NodeTypeRegistry.RegisterNodeType("diffusers.preprocessor", () => new DiffusersPreprocessorNode());
        NodeTypeRegistry.RegisterNodeType("diffusers.upscale", () => new DiffusersUpscaleNode());
        NodeTypeRegistry.RegisterNodeType("diffusers.vae", () => new DiffusersVAENode());

        // Pipeline Template nodes (all-in-one nodes for quick generation)
        NodeTypeRegistry.RegisterNodeType("pipeline.image_generation", () => new ImageGenerationPipelineNode());
        NodeTypeRegistry.RegisterNodeType("pipeline.video_generation", () => new VideoGenerationPipelineNode());
        NodeTypeRegistry.RegisterNodeType("pipeline.img2img", () => new Img2ImgPipelineNode());
        NodeTypeRegistry.RegisterNodeType("pipeline.inpaint", () => new InpaintPipelineNode());
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

// ═══════════════════════════════════════════════════════════════════════════
// DIFFUSERS NODES
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Load Diffusers model from HuggingFace
/// </summary>
public class DiffusersLoadModelNode : WorkflowNode
{
    public override string NodeType => "diffusers.load_model";

    public DiffusersLoadModelNode()
    {
        Name = "Load Model";
        Description = "Load Diffusers model from HuggingFace or local path";
        Color = "#A855F7";
        Icon = "Download";
        Size = new NodeSize(300, 220);

        Outputs.Add(new NodePort
        {
            Name = "model",
            DataType = PortDataType.Model,
            Direction = PortDirection.Output,
            Color = "#A855F7"
        });

        Outputs.Add(new NodePort
        {
            Name = "model_info",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#10B981"
        });

        Properties["model_id"] = new NodeProperty
        {
            Name = "model_id",
            DisplayName = "Model ID",
            Type = PropertyType.String,
            Value = "stabilityai/stable-diffusion-xl-base-1.0",
            DefaultValue = "stabilityai/stable-diffusion-xl-base-1.0",
            Tooltip = "HuggingFace model ID or local path"
        };

        Properties["model_type"] = new NodeProperty
        {
            Name = "model_type",
            DisplayName = "Model Type",
            Type = PropertyType.Combo,
            Value = "SDXL",
            DefaultValue = "SDXL",
            Options = new List<string> { "SD15", "SD20", "SD21", "SDXL", "Flux", "Wan", "LTXV", "HunyuanVideo" }
        };

        Properties["precision"] = new NodeProperty
        {
            Name = "precision",
            DisplayName = "Precision",
            Type = PropertyType.Combo,
            Value = "fp16",
            DefaultValue = "fp16",
            Options = new List<string> { "fp32", "fp16", "bf16", "int8", "int4" }
        };

        Properties["device"] = new NodeProperty
        {
            Name = "device",
            DisplayName = "Device",
            Type = PropertyType.Combo,
            Value = "cuda",
            DefaultValue = "cuda",
            Options = new List<string> { "cuda", "cuda:0", "cuda:1", "cpu" }
        };
    }
}

/// <summary>
/// Generate image using Diffusers
/// </summary>
public class DiffusersGenerateImageNode : WorkflowNode
{
    public override string NodeType => "diffusers.generate_image";

    public DiffusersGenerateImageNode()
    {
        Name = "Generate Image";
        Description = "Generate image using loaded Diffusers model";
        Color = "#EC4899";
        Icon = "ImageFilterDrama";
        Size = new NodeSize(320, 400);

        Inputs.Add(new NodePort
        {
            Name = "model",
            DataType = PortDataType.Model,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#A855F7"
        });

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
            Name = "lora",
            DataType = PortDataType.LoRA,
            Direction = PortDirection.Input,
            IsRequired = false,
            AllowMultiple = true,
            Color = "#06B6D4"
        });

        Inputs.Add(new NodePort
        {
            Name = "controlnet",
            DataType = PortDataType.ControlNet,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#8B5CF6"
        });

        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "seed_used",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#F59E0B"
        });

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
            Value = 30,
            DefaultValue = 30,
            Min = 1,
            Max = 150
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 7.5,
            DefaultValue = 7.5,
            Min = 1,
            Max = 30,
            Step = 0.5
        };

        Properties["sampler"] = new NodeProperty
        {
            Name = "sampler",
            DisplayName = "Sampler",
            Type = PropertyType.Combo,
            Value = "euler_a",
            DefaultValue = "euler_a",
            Options = new List<string> { "euler", "euler_a", "dpm++_2m", "dpm++_2m_sde", "dpm++_sde", "ddim", "lms", "heun", "uni_pc" }
        };

        Properties["scheduler"] = new NodeProperty
        {
            Name = "scheduler",
            DisplayName = "Scheduler",
            Type = PropertyType.Combo,
            Value = "normal",
            DefaultValue = "normal",
            Options = new List<string> { "normal", "karras", "exponential", "sgm_uniform" }
        };

        Properties["batch_size"] = new NodeProperty
        {
            Name = "batch_size",
            DisplayName = "Batch Size",
            Type = PropertyType.Int,
            Value = 1,
            DefaultValue = 1,
            Min = 1,
            Max = 16
        };
    }
}

/// <summary>
/// Generate video using Diffusers (CogVideoX, LTXV, Wan, etc.)
/// </summary>
public class DiffusersGenerateVideoNode : WorkflowNode
{
    public override string NodeType => "diffusers.generate_video";

    public DiffusersGenerateVideoNode()
    {
        Name = "Generate Video";
        Description = "Generate video using video diffusion models";
        Color = "#EF4444";
        Icon = "Video";
        Size = new NodeSize(320, 380);

        Inputs.Add(new NodePort
        {
            Name = "model",
            DataType = PortDataType.Model,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#A855F7"
        });

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
            Name = "video",
            DataType = PortDataType.Video,
            Direction = PortDirection.Output,
            Color = "#EF4444"
        });

        Outputs.Add(new NodePort
        {
            Name = "frames",
            DataType = PortDataType.List,
            Direction = PortDirection.Output,
            Color = "#EC4899"
        });

        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 512,
            DefaultValue = 512,
            Min = 256,
            Max = 1920,
            Step = 64
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 512,
            DefaultValue = 512,
            Min = 256,
            Max = 1080,
            Step = 64
        };

        Properties["num_frames"] = new NodeProperty
        {
            Name = "num_frames",
            DisplayName = "Frames",
            Type = PropertyType.Int,
            Value = 49,
            DefaultValue = 49,
            Min = 8,
            Max = 256
        };

        Properties["fps"] = new NodeProperty
        {
            Name = "fps",
            DisplayName = "FPS",
            Type = PropertyType.Int,
            Value = 24,
            DefaultValue = 24,
            Min = 8,
            Max = 60
        };

        Properties["steps"] = new NodeProperty
        {
            Name = "steps",
            DisplayName = "Steps",
            Type = PropertyType.Int,
            Value = 30,
            DefaultValue = 30,
            Min = 1,
            Max = 100
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 6.0,
            DefaultValue = 6.0,
            Min = 1,
            Max = 20,
            Step = 0.5
        };
    }
}

/// <summary>
/// Load and apply LoRA to model
/// </summary>
public class DiffusersLoRANode : WorkflowNode
{
    public override string NodeType => "diffusers.lora";

    public DiffusersLoRANode()
    {
        Name = "LoRA";
        Description = "Load LoRA and control its weight";
        Color = "#06B6D4";
        Icon = "Tune";
        Size = new NodeSize(260, 180);

        Inputs.Add(new NodePort
        {
            Name = "lora_in",
            DataType = PortDataType.LoRA,
            Direction = PortDirection.Input,
            IsRequired = false,
            AllowMultiple = true,
            Color = "#06B6D4"
        });

        Outputs.Add(new NodePort
        {
            Name = "lora",
            DataType = PortDataType.LoRA,
            Direction = PortDirection.Output,
            Color = "#06B6D4"
        });

        Properties["lora_path"] = new NodeProperty
        {
            Name = "lora_path",
            DisplayName = "LoRA Path",
            Type = PropertyType.FilePath,
            Value = "",
            DefaultValue = "",
            Tooltip = "Path to .safetensors LoRA file"
        };

        Properties["lora_id"] = new NodeProperty
        {
            Name = "lora_id",
            DisplayName = "LoRA ID (HF)",
            Type = PropertyType.String,
            Value = "",
            DefaultValue = "",
            Tooltip = "HuggingFace LoRA ID (alternative to path)"
        };

        Properties["weight"] = new NodeProperty
        {
            Name = "weight",
            DisplayName = "Weight",
            Type = PropertyType.Slider,
            Value = 1.0,
            DefaultValue = 1.0,
            Min = -2.0,
            Max = 2.0,
            Step = 0.05
        };
    }
}

/// <summary>
/// Apply ControlNet guidance
/// </summary>
public class DiffusersControlNetNode : WorkflowNode
{
    public override string NodeType => "diffusers.controlnet";

    public DiffusersControlNetNode()
    {
        Name = "ControlNet";
        Description = "Apply ControlNet conditioning for guided generation";
        Color = "#8B5CF6";
        Icon = "VectorPolyline";
        Size = new NodeSize(280, 240);

        Inputs.Add(new NodePort
        {
            Name = "control_image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EC4899"
        });

        Inputs.Add(new NodePort
        {
            Name = "controlnet_in",
            DataType = PortDataType.ControlNet,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#8B5CF6"
        });

        Outputs.Add(new NodePort
        {
            Name = "controlnet",
            DataType = PortDataType.ControlNet,
            Direction = PortDirection.Output,
            Color = "#8B5CF6"
        });

        Properties["controlnet_model"] = new NodeProperty
        {
            Name = "controlnet_model",
            DisplayName = "ControlNet Model",
            Type = PropertyType.Combo,
            Value = "canny",
            DefaultValue = "canny",
            Options = new List<string> { "canny", "depth", "openpose", "lineart", "softedge", "scribble", "ip_adapter", "tile" }
        };

        Properties["conditioning_scale"] = new NodeProperty
        {
            Name = "conditioning_scale",
            DisplayName = "Conditioning Scale",
            Type = PropertyType.Slider,
            Value = 1.0,
            DefaultValue = 1.0,
            Min = 0.0,
            Max = 2.0,
            Step = 0.05
        };

        Properties["control_guidance_start"] = new NodeProperty
        {
            Name = "control_guidance_start",
            DisplayName = "Guidance Start",
            Type = PropertyType.Slider,
            Value = 0.0,
            DefaultValue = 0.0,
            Min = 0.0,
            Max = 1.0,
            Step = 0.01,
            Tooltip = "Start applying ControlNet at this step percentage"
        };

        Properties["control_guidance_end"] = new NodeProperty
        {
            Name = "control_guidance_end",
            DisplayName = "Guidance End",
            Type = PropertyType.Slider,
            Value = 1.0,
            DefaultValue = 1.0,
            Min = 0.0,
            Max = 1.0,
            Step = 0.01,
            Tooltip = "Stop applying ControlNet at this step percentage"
        };
    }
}

/// <summary>
/// Image preprocessor for ControlNet
/// </summary>
public class DiffusersPreprocessorNode : WorkflowNode
{
    public override string NodeType => "diffusers.preprocessor";

    public DiffusersPreprocessorNode()
    {
        Name = "Preprocessor";
        Description = "Preprocess image for ControlNet input";
        Color = "#F97316";
        Icon = "ImageEdit";
        Size = new NodeSize(260, 180);

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
            Name = "processed",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#F97316"
        });

        Properties["processor"] = new NodeProperty
        {
            Name = "processor",
            DisplayName = "Processor",
            Type = PropertyType.Combo,
            Value = "canny",
            DefaultValue = "canny",
            Options = new List<string> { "canny", "depth_midas", "depth_zoe", "openpose", "lineart", "lineart_anime", "softedge", "scribble", "normal_bae" }
        };

        Properties["resolution"] = new NodeProperty
        {
            Name = "resolution",
            DisplayName = "Resolution",
            Type = PropertyType.Int,
            Value = 512,
            DefaultValue = 512,
            Min = 256,
            Max = 2048,
            Step = 64
        };

        Properties["threshold_low"] = new NodeProperty
        {
            Name = "threshold_low",
            DisplayName = "Threshold Low",
            Type = PropertyType.Int,
            Value = 100,
            DefaultValue = 100,
            Min = 0,
            Max = 255,
            Tooltip = "For Canny edge detection"
        };

        Properties["threshold_high"] = new NodeProperty
        {
            Name = "threshold_high",
            DisplayName = "Threshold High",
            Type = PropertyType.Int,
            Value = 200,
            DefaultValue = 200,
            Min = 0,
            Max = 255,
            Tooltip = "For Canny edge detection"
        };
    }
}

/// <summary>
/// Image upscaler using Diffusers or Real-ESRGAN
/// </summary>
public class DiffusersUpscaleNode : WorkflowNode
{
    public override string NodeType => "diffusers.upscale";

    public DiffusersUpscaleNode()
    {
        Name = "Upscale";
        Description = "Upscale image using AI models";
        Color = "#10B981";
        Icon = "ArrowExpand";
        Size = new NodeSize(260, 200);

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
            Name = "upscaled",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#10B981"
        });

        Properties["method"] = new NodeProperty
        {
            Name = "method",
            DisplayName = "Method",
            Type = PropertyType.Combo,
            Value = "real_esrgan",
            DefaultValue = "real_esrgan",
            Options = new List<string> { "real_esrgan", "sd_upscale", "swinir", "lanczos", "nearest" }
        };

        Properties["scale"] = new NodeProperty
        {
            Name = "scale",
            DisplayName = "Scale",
            Type = PropertyType.Combo,
            Value = "2",
            DefaultValue = "2",
            Options = new List<string> { "2", "4", "8" }
        };

        Properties["denoise_strength"] = new NodeProperty
        {
            Name = "denoise_strength",
            DisplayName = "Denoise Strength",
            Type = PropertyType.Slider,
            Value = 0.5,
            DefaultValue = 0.5,
            Min = 0.0,
            Max = 1.0,
            Step = 0.05,
            Tooltip = "For SD Upscale only"
        };
    }
}

/// <summary>
/// VAE Encode/Decode node
/// </summary>
public class DiffusersVAENode : WorkflowNode
{
    public override string NodeType => "diffusers.vae";

    public DiffusersVAENode()
    {
        Name = "VAE";
        Description = "Encode/Decode images with VAE";
        Color = "#EAB308";
        Icon = "SwapHorizontal";
        Size = new NodeSize(240, 180);

        Inputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#EC4899"
        });

        Inputs.Add(new NodePort
        {
            Name = "latent",
            DataType = PortDataType.Latent,
            Direction = PortDirection.Input,
            IsRequired = false,
            Color = "#8B5CF6"
        });

        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "latent",
            DataType = PortDataType.Latent,
            Direction = PortDirection.Output,
            Color = "#8B5CF6"
        });

        Properties["mode"] = new NodeProperty
        {
            Name = "mode",
            DisplayName = "Mode",
            Type = PropertyType.Combo,
            Value = "decode",
            DefaultValue = "decode",
            Options = new List<string> { "encode", "decode" }
        };

        Properties["custom_vae"] = new NodeProperty
        {
            Name = "custom_vae",
            DisplayName = "Custom VAE",
            Type = PropertyType.String,
            Value = "",
            DefaultValue = "",
            Tooltip = "Optional: HuggingFace VAE ID"
        };

        Properties["tiling"] = new NodeProperty
        {
            Name = "tiling",
            DisplayName = "Enable Tiling",
            Type = PropertyType.Bool,
            Value = false,
            DefaultValue = false,
            Tooltip = "Enable VAE tiling for large images"
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PIPELINE TEMPLATE NODES (All-in-One Macro Nodes)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// All-in-one Text-to-Image Pipeline node
/// Encapsulates: Load Model → Generate → Upscale → Save
/// </summary>
public class ImageGenerationPipelineNode : WorkflowNode
{
    public override string NodeType => "pipeline.image_generation";

    public ImageGenerationPipelineNode()
    {
        Name = "Image Generation Pipeline";
        Description = "All-in-one text-to-image generation with model, sampler, and upscale options";
        Color = "#EC4899";
        Icon = "ImageFilterDrama";
        Size = new NodeSize(380, 600);

        // Simple inputs
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

        // Outputs
        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "upscaled_image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#10B981"
        });

        Outputs.Add(new NodePort
        {
            Name = "seed_used",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#F59E0B"
        });

        Outputs.Add(new NodePort
        {
            Name = "file_path",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        // ═══════ Model Block ═══════
        Properties["model_id"] = new NodeProperty
        {
            Name = "model_id",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "stabilityai/stable-diffusion-xl-base-1.0",
            DefaultValue = "stabilityai/stable-diffusion-xl-base-1.0",
            Tooltip = "HuggingFace model ID or local path"
        };

        Properties["model_type"] = new NodeProperty
        {
            Name = "model_type",
            DisplayName = "Model Type",
            Type = PropertyType.Combo,
            Value = "SDXL",
            DefaultValue = "SDXL",
            Options = new List<string> { "SD15", "SD21", "SDXL", "Flux" }
        };

        Properties["precision"] = new NodeProperty
        {
            Name = "precision",
            DisplayName = "Precision",
            Type = PropertyType.Combo,
            Value = "fp16",
            DefaultValue = "fp16",
            Options = new List<string> { "fp32", "fp16", "bf16" }
        };

        // ═══════ Sampler Block ═══════
        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 512,
            Max = 2048,
            Step = 64
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 512,
            Max = 2048,
            Step = 64
        };

        Properties["steps"] = new NodeProperty
        {
            Name = "steps",
            DisplayName = "Steps",
            Type = PropertyType.Int,
            Value = 30,
            DefaultValue = 30,
            Min = 1,
            Max = 100
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 7.5,
            DefaultValue = 7.5,
            Min = 1,
            Max = 20,
            Step = 0.5
        };

        Properties["sampler"] = new NodeProperty
        {
            Name = "sampler",
            DisplayName = "Sampler",
            Type = PropertyType.Combo,
            Value = "euler_a",
            DefaultValue = "euler_a",
            Options = new List<string> { "euler", "euler_a", "dpm++_2m", "dpm++_2m_sde", "ddim", "lms" }
        };

        Properties["scheduler"] = new NodeProperty
        {
            Name = "scheduler",
            DisplayName = "Scheduler",
            Type = PropertyType.Combo,
            Value = "normal",
            DefaultValue = "normal",
            Options = new List<string> { "normal", "karras", "exponential" }
        };

        // ═══════ Post-Process Block ═══════
        Properties["enable_upscale"] = new NodeProperty
        {
            Name = "enable_upscale",
            DisplayName = "Enable Upscale",
            Type = PropertyType.Bool,
            Value = false,
            DefaultValue = false
        };

        Properties["upscale_factor"] = new NodeProperty
        {
            Name = "upscale_factor",
            DisplayName = "Upscale Factor",
            Type = PropertyType.Combo,
            Value = "2",
            DefaultValue = "2",
            Options = new List<string> { "2", "4" }
        };

        Properties["upscale_method"] = new NodeProperty
        {
            Name = "upscale_method",
            DisplayName = "Upscale Method",
            Type = PropertyType.Combo,
            Value = "real_esrgan",
            DefaultValue = "real_esrgan",
            Options = new List<string> { "real_esrgan", "lanczos" }
        };

        // ═══════ Output Block ═══════
        Properties["auto_save"] = new NodeProperty
        {
            Name = "auto_save",
            DisplayName = "Auto Save",
            Type = PropertyType.Bool,
            Value = true,
            DefaultValue = true
        };

        Properties["output_folder"] = new NodeProperty
        {
            Name = "output_folder",
            DisplayName = "Output Folder",
            Type = PropertyType.FolderPath,
            Value = "outputs/images",
            DefaultValue = "outputs/images"
        };

        Properties["filename_prefix"] = new NodeProperty
        {
            Name = "filename_prefix",
            DisplayName = "Filename Prefix",
            Type = PropertyType.String,
            Value = "img",
            DefaultValue = "img"
        };

        Properties["output_format"] = new NodeProperty
        {
            Name = "output_format",
            DisplayName = "Format",
            Type = PropertyType.Combo,
            Value = "png",
            DefaultValue = "png",
            Options = new List<string> { "png", "jpg", "webp" }
        };
    }
}

/// <summary>
/// All-in-one Text-to-Video Pipeline node
/// Encapsulates: Load Model → Generate Video → Post-process → Save
/// </summary>
public class VideoGenerationPipelineNode : WorkflowNode
{
    public override string NodeType => "pipeline.video_generation";

    public VideoGenerationPipelineNode()
    {
        Name = "Video Generation Pipeline";
        Description = "All-in-one text-to-video generation with model and output options";
        Color = "#EF4444";
        Icon = "Video";
        Size = new NodeSize(380, 550);

        // Simple inputs
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

        // Outputs
        Outputs.Add(new NodePort
        {
            Name = "video",
            DataType = PortDataType.Video,
            Direction = PortDirection.Output,
            Color = "#EF4444"
        });

        Outputs.Add(new NodePort
        {
            Name = "frames",
            DataType = PortDataType.List,
            Direction = PortDirection.Output,
            Color = "#EC4899"
        });

        Outputs.Add(new NodePort
        {
            Name = "seed_used",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#F59E0B"
        });

        Outputs.Add(new NodePort
        {
            Name = "file_path",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        // ═══════ Model Block ═══════
        Properties["model_id"] = new NodeProperty
        {
            Name = "model_id",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "THUDM/CogVideoX-5b",
            DefaultValue = "THUDM/CogVideoX-5b",
            Tooltip = "HuggingFace video model ID"
        };

        Properties["model_type"] = new NodeProperty
        {
            Name = "model_type",
            DisplayName = "Model Type",
            Type = PropertyType.Combo,
            Value = "CogVideoX",
            DefaultValue = "CogVideoX",
            Options = new List<string> { "CogVideoX", "LTXV", "Wan", "HunyuanVideo" }
        };

        Properties["precision"] = new NodeProperty
        {
            Name = "precision",
            DisplayName = "Precision",
            Type = PropertyType.Combo,
            Value = "bf16",
            DefaultValue = "bf16",
            Options = new List<string> { "fp32", "fp16", "bf16" }
        };

        // ═══════ Video Settings Block ═══════
        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 720,
            DefaultValue = 720,
            Min = 256,
            Max = 1920,
            Step = 64
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 480,
            DefaultValue = 480,
            Min = 256,
            Max = 1080,
            Step = 64
        };

        Properties["num_frames"] = new NodeProperty
        {
            Name = "num_frames",
            DisplayName = "Frames",
            Type = PropertyType.Int,
            Value = 49,
            DefaultValue = 49,
            Min = 8,
            Max = 128
        };

        Properties["fps"] = new NodeProperty
        {
            Name = "fps",
            DisplayName = "FPS",
            Type = PropertyType.Int,
            Value = 24,
            DefaultValue = 24,
            Min = 8,
            Max = 60
        };

        Properties["steps"] = new NodeProperty
        {
            Name = "steps",
            DisplayName = "Steps",
            Type = PropertyType.Int,
            Value = 30,
            DefaultValue = 30,
            Min = 1,
            Max = 100
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 6.0,
            DefaultValue = 6.0,
            Min = 1,
            Max = 15,
            Step = 0.5
        };

        // ═══════ Output Block ═══════
        Properties["auto_save"] = new NodeProperty
        {
            Name = "auto_save",
            DisplayName = "Auto Save",
            Type = PropertyType.Bool,
            Value = true,
            DefaultValue = true
        };

        Properties["output_folder"] = new NodeProperty
        {
            Name = "output_folder",
            DisplayName = "Output Folder",
            Type = PropertyType.FolderPath,
            Value = "outputs/videos",
            DefaultValue = "outputs/videos"
        };

        Properties["filename_prefix"] = new NodeProperty
        {
            Name = "filename_prefix",
            DisplayName = "Filename Prefix",
            Type = PropertyType.String,
            Value = "video",
            DefaultValue = "video"
        };

        Properties["output_format"] = new NodeProperty
        {
            Name = "output_format",
            DisplayName = "Format",
            Type = PropertyType.Combo,
            Value = "mp4",
            DefaultValue = "mp4",
            Options = new List<string> { "mp4", "webm", "gif" }
        };
    }
}

/// <summary>
/// All-in-one Image-to-Image Pipeline node
/// Encapsulates: Load Image → Load Model → Img2Img → Upscale → Save
/// </summary>
public class Img2ImgPipelineNode : WorkflowNode
{
    public override string NodeType => "pipeline.img2img";

    public Img2ImgPipelineNode()
    {
        Name = "Img2Img Pipeline";
        Description = "All-in-one image-to-image transformation with denoise control";
        Color = "#8B5CF6";
        Icon = "ImageEdit";
        Size = new NodeSize(380, 580);

        // Inputs
        Inputs.Add(new NodePort
        {
            Name = "input_image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EC4899"
        });

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

        // Outputs
        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#8B5CF6"
        });

        Outputs.Add(new NodePort
        {
            Name = "upscaled_image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#10B981"
        });

        Outputs.Add(new NodePort
        {
            Name = "seed_used",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#F59E0B"
        });

        Outputs.Add(new NodePort
        {
            Name = "file_path",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        // ═══════ Model Block ═══════
        Properties["model_id"] = new NodeProperty
        {
            Name = "model_id",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "stabilityai/stable-diffusion-xl-base-1.0",
            DefaultValue = "stabilityai/stable-diffusion-xl-base-1.0"
        };

        Properties["precision"] = new NodeProperty
        {
            Name = "precision",
            DisplayName = "Precision",
            Type = PropertyType.Combo,
            Value = "fp16",
            DefaultValue = "fp16",
            Options = new List<string> { "fp32", "fp16", "bf16" }
        };

        // ═══════ Img2Img Settings ═══════
        Properties["denoise_strength"] = new NodeProperty
        {
            Name = "denoise_strength",
            DisplayName = "Denoise Strength",
            Type = PropertyType.Slider,
            Value = 0.75,
            DefaultValue = 0.75,
            Min = 0.0,
            Max = 1.0,
            Step = 0.05,
            Tooltip = "Higher = more change from original"
        };

        Properties["resize_mode"] = new NodeProperty
        {
            Name = "resize_mode",
            DisplayName = "Resize Mode",
            Type = PropertyType.Combo,
            Value = "resize_and_fill",
            DefaultValue = "resize_and_fill",
            Options = new List<string> { "just_resize", "crop_and_resize", "resize_and_fill" }
        };

        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 512,
            Max = 2048,
            Step = 64
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 512,
            Max = 2048,
            Step = 64
        };

        Properties["steps"] = new NodeProperty
        {
            Name = "steps",
            DisplayName = "Steps",
            Type = PropertyType.Int,
            Value = 30,
            DefaultValue = 30,
            Min = 1,
            Max = 100
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 7.5,
            DefaultValue = 7.5,
            Min = 1,
            Max = 20,
            Step = 0.5
        };

        Properties["sampler"] = new NodeProperty
        {
            Name = "sampler",
            DisplayName = "Sampler",
            Type = PropertyType.Combo,
            Value = "euler_a",
            DefaultValue = "euler_a",
            Options = new List<string> { "euler", "euler_a", "dpm++_2m", "dpm++_2m_sde", "ddim" }
        };

        // ═══════ Post-Process Block ═══════
        Properties["enable_upscale"] = new NodeProperty
        {
            Name = "enable_upscale",
            DisplayName = "Enable Upscale",
            Type = PropertyType.Bool,
            Value = false,
            DefaultValue = false
        };

        Properties["upscale_factor"] = new NodeProperty
        {
            Name = "upscale_factor",
            DisplayName = "Upscale Factor",
            Type = PropertyType.Combo,
            Value = "2",
            DefaultValue = "2",
            Options = new List<string> { "2", "4" }
        };

        // ═══════ Output Block ═══════
        Properties["auto_save"] = new NodeProperty
        {
            Name = "auto_save",
            DisplayName = "Auto Save",
            Type = PropertyType.Bool,
            Value = true,
            DefaultValue = true
        };

        Properties["output_folder"] = new NodeProperty
        {
            Name = "output_folder",
            DisplayName = "Output Folder",
            Type = PropertyType.FolderPath,
            Value = "outputs/img2img",
            DefaultValue = "outputs/img2img"
        };

        Properties["output_format"] = new NodeProperty
        {
            Name = "output_format",
            DisplayName = "Format",
            Type = PropertyType.Combo,
            Value = "png",
            DefaultValue = "png",
            Options = new List<string> { "png", "jpg", "webp" }
        };
    }
}

/// <summary>
/// All-in-one Inpainting Pipeline node
/// Encapsulates: Load Image + Mask → Load Model → Inpaint → Save
/// </summary>
public class InpaintPipelineNode : WorkflowNode
{
    public override string NodeType => "pipeline.inpaint";

    public InpaintPipelineNode()
    {
        Name = "Inpaint Pipeline";
        Description = "All-in-one inpainting with mask control and fill options";
        Color = "#06B6D4";
        Icon = "Draw";
        Size = new NodeSize(380, 580);

        // Inputs
        Inputs.Add(new NodePort
        {
            Name = "input_image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#EC4899"
        });

        Inputs.Add(new NodePort
        {
            Name = "mask",
            DataType = PortDataType.Mask,
            Direction = PortDirection.Input,
            IsRequired = true,
            Color = "#FFFFFF"
        });

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

        // Outputs
        Outputs.Add(new NodePort
        {
            Name = "image",
            DataType = PortDataType.Image,
            Direction = PortDirection.Output,
            Color = "#06B6D4"
        });

        Outputs.Add(new NodePort
        {
            Name = "seed_used",
            DataType = PortDataType.Number,
            Direction = PortDirection.Output,
            Color = "#F59E0B"
        });

        Outputs.Add(new NodePort
        {
            Name = "file_path",
            DataType = PortDataType.Text,
            Direction = PortDirection.Output,
            Color = "#3B82F6"
        });

        // ═══════ Model Block ═══════
        Properties["model_id"] = new NodeProperty
        {
            Name = "model_id",
            DisplayName = "Model",
            Type = PropertyType.String,
            Value = "diffusers/stable-diffusion-xl-1.0-inpainting-0.1",
            DefaultValue = "diffusers/stable-diffusion-xl-1.0-inpainting-0.1"
        };

        Properties["precision"] = new NodeProperty
        {
            Name = "precision",
            DisplayName = "Precision",
            Type = PropertyType.Combo,
            Value = "fp16",
            DefaultValue = "fp16",
            Options = new List<string> { "fp32", "fp16", "bf16" }
        };

        // ═══════ Inpaint Settings ═══════
        Properties["denoise_strength"] = new NodeProperty
        {
            Name = "denoise_strength",
            DisplayName = "Denoise Strength",
            Type = PropertyType.Slider,
            Value = 1.0,
            DefaultValue = 1.0,
            Min = 0.0,
            Max = 1.0,
            Step = 0.05,
            Tooltip = "1.0 = completely regenerate masked area"
        };

        Properties["mask_blur"] = new NodeProperty
        {
            Name = "mask_blur",
            DisplayName = "Mask Blur",
            Type = PropertyType.Int,
            Value = 4,
            DefaultValue = 4,
            Min = 0,
            Max = 64,
            Tooltip = "Blur mask edges for smoother blending"
        };

        Properties["inpaint_mode"] = new NodeProperty
        {
            Name = "inpaint_mode",
            DisplayName = "Inpaint Mode",
            Type = PropertyType.Combo,
            Value = "original",
            DefaultValue = "original",
            Options = new List<string> { "original", "fill", "latent_noise", "latent_nothing" }
        };

        Properties["mask_invert"] = new NodeProperty
        {
            Name = "mask_invert",
            DisplayName = "Invert Mask",
            Type = PropertyType.Bool,
            Value = false,
            DefaultValue = false
        };

        // ═══════ Generation Settings ═══════
        Properties["width"] = new NodeProperty
        {
            Name = "width",
            DisplayName = "Width",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 512,
            Max = 2048,
            Step = 64
        };

        Properties["height"] = new NodeProperty
        {
            Name = "height",
            DisplayName = "Height",
            Type = PropertyType.Int,
            Value = 1024,
            DefaultValue = 1024,
            Min = 512,
            Max = 2048,
            Step = 64
        };

        Properties["steps"] = new NodeProperty
        {
            Name = "steps",
            DisplayName = "Steps",
            Type = PropertyType.Int,
            Value = 30,
            DefaultValue = 30,
            Min = 1,
            Max = 100
        };

        Properties["cfg_scale"] = new NodeProperty
        {
            Name = "cfg_scale",
            DisplayName = "CFG Scale",
            Type = PropertyType.Slider,
            Value = 7.5,
            DefaultValue = 7.5,
            Min = 1,
            Max = 20,
            Step = 0.5
        };

        Properties["sampler"] = new NodeProperty
        {
            Name = "sampler",
            DisplayName = "Sampler",
            Type = PropertyType.Combo,
            Value = "euler_a",
            DefaultValue = "euler_a",
            Options = new List<string> { "euler", "euler_a", "dpm++_2m", "dpm++_2m_sde", "ddim" }
        };

        // ═══════ Output Block ═══════
        Properties["auto_save"] = new NodeProperty
        {
            Name = "auto_save",
            DisplayName = "Auto Save",
            Type = PropertyType.Bool,
            Value = true,
            DefaultValue = true
        };

        Properties["output_folder"] = new NodeProperty
        {
            Name = "output_folder",
            DisplayName = "Output Folder",
            Type = PropertyType.FolderPath,
            Value = "outputs/inpaint",
            DefaultValue = "outputs/inpaint"
        };

        Properties["output_format"] = new NodeProperty
        {
            Name = "output_format",
            DisplayName = "Format",
            Type = PropertyType.Combo,
            Value = "png",
            DefaultValue = "png",
            Options = new List<string> { "png", "jpg", "webp" }
        };
    }
}
