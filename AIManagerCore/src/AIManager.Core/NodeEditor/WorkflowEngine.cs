using AIManager.Core.NodeEditor.Models;
using AIManager.Core.NodeEditor.Nodes;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.NodeEditor;

/// <summary>
/// Custom Workflow Engine - replaces ComfyUI dependency
/// Executes visual workflow graphs with AI nodes
/// </summary>
public class WorkflowEngine
{
    private readonly ILogger<WorkflowEngine> _logger;
    private readonly NodeRegistry _nodeRegistry;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly ImageGeneratorService _imageGenerator;
    private DiffusersGenerationEngine? _diffusersEngine;

    // Events for UI updates
    public event Action<string, NodeExecutionState>? NodeStateChanged;
    public event Action<string, double>? NodeProgressUpdated;
    public event Action<string, string>? NodeOutputReady;
    public event Action<NodeWorkflowExecutionResult>? WorkflowCompleted;
    public event Action<string>? LogMessage;

    public WorkflowEngine(
        ILogger<WorkflowEngine> logger,
        NodeRegistry nodeRegistry,
        ContentGeneratorService contentGenerator,
        ImageGeneratorService imageGenerator,
        DiffusersGenerationEngine? diffusersEngine = null)
    {
        _logger = logger;
        _nodeRegistry = nodeRegistry;
        _contentGenerator = contentGenerator;
        _imageGenerator = imageGenerator;
        _diffusersEngine = diffusersEngine;

        // Share the diffusers engine with image generator
        if (_diffusersEngine != null)
        {
            _imageGenerator.SetDiffusersEngine(_diffusersEngine);
        }
    }

    /// <summary>
    /// Set or update the DiffusersGenerationEngine
    /// </summary>
    public void SetDiffusersEngine(DiffusersGenerationEngine engine)
    {
        _diffusersEngine = engine;
        _imageGenerator.SetDiffusersEngine(engine);
    }

    /// <summary>
    /// Gets whether DiffusersEngine is running
    /// </summary>
    public bool IsDiffusersEngineRunning => _diffusersEngine?.IsRunning == true;

    /// <summary>
    /// Execute a complete workflow
    /// </summary>
    public async Task<NodeWorkflowExecutionResult> ExecuteAsync(
        WorkflowGraph workflow,
        Dictionary<string, object>? inputVariables = null,
        CancellationToken ct = default)
    {
        var result = new NodeWorkflowExecutionResult
        {
            WorkflowId = workflow.Id,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            Log($"Starting workflow: {workflow.Name}");

            // Merge input variables
            var variables = new Dictionary<string, object>(workflow.Variables);
            if (inputVariables != null)
            {
                foreach (var kv in inputVariables)
                    variables[kv.Key] = kv.Value;
            }

            // Get execution order
            var executionOrder = workflow.GetExecutionOrder();
            Log($"Execution order: {string.Join(" -> ", executionOrder.Select(n => n.Name))}");

            // Store outputs from each node
            var nodeOutputs = new Dictionary<string, Dictionary<string, object?>>();

            // Execute nodes in order
            foreach (var node in executionOrder)
            {
                ct.ThrowIfCancellationRequested();

                if (!node.IsEnabled)
                {
                    Log($"Skipping disabled node: {node.Name}");
                    continue;
                }

                NodeStateChanged?.Invoke(node.Id, NodeExecutionState.Running);

                try
                {
                    // Gather inputs from connected nodes
                    var inputs = GatherInputs(workflow, node, nodeOutputs);

                    // Create execution context
                    var context = new NodeExecutionContext
                    {
                        NodeId = node.Id,
                        InputValues = inputs,
                        Variables = variables,
                        CancellationToken = ct,
                        Progress = new Progress<double>(p => NodeProgressUpdated?.Invoke(node.Id, p)),
                        Log = msg => Log($"[{node.Name}] {msg}")
                    };

                    // Execute node
                    Log($"Executing node: {node.Name} ({node.NodeType})");
                    var nodeResult = await ExecuteNodeAsync(node, context, ct);

                    if (nodeResult.Success)
                    {
                        nodeOutputs[node.Id] = nodeResult.Outputs;
                        result.NodeResults[node.Id] = nodeResult;
                        NodeStateChanged?.Invoke(node.Id, NodeExecutionState.Completed);

                        // Notify output ready
                        if (nodeResult.Outputs.Count > 0)
                        {
                            var outputJson = JsonConvert.SerializeObject(nodeResult.Outputs, Formatting.Indented);
                            NodeOutputReady?.Invoke(node.Id, outputJson);
                        }
                    }
                    else
                    {
                        Log($"Node failed: {node.Name} - {nodeResult.Error}");
                        NodeStateChanged?.Invoke(node.Id, NodeExecutionState.Failed);
                        result.Success = false;
                        result.Error = $"Node '{node.Name}' failed: {nodeResult.Error}";
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"Exception in node {node.Name}: {ex.Message}");
                    NodeStateChanged?.Invoke(node.Id, NodeExecutionState.Failed);
                    result.Success = false;
                    result.Error = $"Node '{node.Name}' exception: {ex.Message}";
                    break;
                }
            }

            // Success if no errors
            if (string.IsNullOrEmpty(result.Error))
            {
                result.Success = true;
                Log("Workflow completed successfully!");
            }

            result.CompletedAt = DateTime.UtcNow;
            result.FinalOutputs = nodeOutputs.Values.LastOrDefault() ?? new Dictionary<string, object?>();

            WorkflowCompleted?.Invoke(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log("Workflow cancelled");
            result.Success = false;
            result.Error = "Workflow was cancelled";
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Workflow execution failed");
            result.Success = false;
            result.Error = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    /// Gather inputs for a node from connected nodes
    /// </summary>
    private Dictionary<string, object?> GatherInputs(
        WorkflowGraph workflow,
        WorkflowNode node,
        Dictionary<string, Dictionary<string, object?>> nodeOutputs)
    {
        var inputs = new Dictionary<string, object?>();

        foreach (var inputPort in node.Inputs)
        {
            // Find connections to this input
            var connections = workflow.Connections
                .Where(c => c.TargetNodeId == node.Id && c.TargetPortId == inputPort.Id)
                .ToList();

            if (connections.Count == 0)
            {
                // Use default value if no connection
                inputs[inputPort.Name] = inputPort.DefaultValue;
            }
            else if (connections.Count == 1)
            {
                // Single connection
                var conn = connections[0];
                if (nodeOutputs.TryGetValue(conn.SourceNodeId, out var sourceOutputs))
                {
                    var sourceNode = workflow.Nodes.First(n => n.Id == conn.SourceNodeId);
                    var sourcePort = sourceNode.Outputs.First(p => p.Id == conn.SourcePortId);
                    inputs[inputPort.Name] = sourceOutputs.GetValueOrDefault(sourcePort.Name);
                }
            }
            else if (inputPort.AllowMultiple)
            {
                // Multiple connections (for ports that allow it)
                var values = new List<object?>();
                foreach (var conn in connections)
                {
                    if (nodeOutputs.TryGetValue(conn.SourceNodeId, out var sourceOutputs))
                    {
                        var sourceNode = workflow.Nodes.First(n => n.Id == conn.SourceNodeId);
                        var sourcePort = sourceNode.Outputs.First(p => p.Id == conn.SourcePortId);
                        values.Add(sourceOutputs.GetValueOrDefault(sourcePort.Name));
                    }
                }
                inputs[inputPort.Name] = values;
            }
        }

        return inputs;
    }

    /// <summary>
    /// Execute a single node
    /// </summary>
    private async Task<NodeExecutionResult> ExecuteNodeAsync(
        WorkflowNode node,
        NodeExecutionContext context,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var outputs = node.NodeType switch
            {
                // Input nodes
                "input.text" => ExecuteTextInput(node),
                "input.number" => ExecuteNumberInput(node),
                "input.image" => await ExecuteImageInputAsync(node, ct),
                "input.seed" => ExecuteSeedInput(node),

                // AI nodes
                "ai.text_generator" => await ExecuteAITextGeneratorAsync(node, context, ct),
                "ai.image_generator" => await ExecuteAIImageGeneratorAsync(node, context, ct),
                "ai.chat" => await ExecuteAIChatAsync(node, context, ct),

                // Processing nodes
                "process.text_combiner" => ExecuteTextCombiner(node, context),
                "process.image_resize" => await ExecuteImageResizeAsync(node, context, ct),
                "process.switch" => ExecuteSwitch(node, context),

                // Output nodes
                "output.save_image" => await ExecuteSaveImageAsync(node, context, ct),
                "output.preview" => ExecutePreview(node, context),
                "output.console" => ExecuteConsoleOutput(node, context),

                // Social media nodes
                "social.post" => await ExecuteSocialPostAsync(node, context, ct),

                // Utility nodes
                "util.note" => new Dictionary<string, object?>(),
                "util.group" => new Dictionary<string, object?>(),
                "util.loop" => await ExecuteLoopAsync(node, context, ct),
                "util.delay" => await ExecuteDelayAsync(node, context, ct),

                // Diffusers nodes
                "diffusers.load_model" => await ExecuteDiffusersLoadModelAsync(node, context, ct),
                "diffusers.generate_image" => await ExecuteDiffusersGenerateImageAsync(node, context, ct),
                "diffusers.generate_video" => await ExecuteDiffusersGenerateVideoAsync(node, context, ct),
                "diffusers.lora" => ExecuteDiffusersLoRA(node, context),
                "diffusers.controlnet" => ExecuteDiffusersControlNet(node, context),
                "diffusers.preprocessor" => await ExecuteDiffusersPreprocessorAsync(node, context, ct),
                "diffusers.upscale" => await ExecuteDiffusersUpscaleAsync(node, context, ct),
                "diffusers.vae" => ExecuteDiffusersVAE(node, context),

                // Pipeline Template nodes (all-in-one macro nodes)
                "pipeline.image_generation" => await ExecutePipelineImageGenerationAsync(node, context, ct),
                "pipeline.video_generation" => await ExecutePipelineVideoGenerationAsync(node, context, ct),
                "pipeline.img2img" => await ExecutePipelineImg2ImgAsync(node, context, ct),
                "pipeline.inpaint" => await ExecutePipelineInpaintAsync(node, context, ct),

                _ => throw new NotSupportedException($"Node type '{node.NodeType}' not supported")
            };

            return new NodeExecutionResult
            {
                Success = true,
                Outputs = outputs,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new NodeExecutionResult
            {
                Success = false,
                Error = ex.Message,
                ExecutionTime = DateTime.UtcNow - startTime
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INPUT NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private Dictionary<string, object?> ExecuteTextInput(WorkflowNode node)
    {
        var text = node.Properties.GetValueOrDefault("text")?.Value?.ToString() ?? "";
        return new Dictionary<string, object?> { ["text"] = text };
    }

    private Dictionary<string, object?> ExecuteNumberInput(WorkflowNode node)
    {
        var value = Convert.ToDouble(node.Properties.GetValueOrDefault("value")?.Value ?? 0);
        return new Dictionary<string, object?> { ["value"] = value };
    }

    private async Task<Dictionary<string, object?>> ExecuteImageInputAsync(WorkflowNode node, CancellationToken ct)
    {
        var path = node.Properties.GetValueOrDefault("path")?.Value?.ToString() ?? "";

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            throw new FileNotFoundException($"Image not found: {path}");
        }

        var bytes = await File.ReadAllBytesAsync(path, ct);
        return new Dictionary<string, object?>
        {
            ["image"] = Convert.ToBase64String(bytes),
            ["path"] = path
        };
    }

    private Dictionary<string, object?> ExecuteSeedInput(WorkflowNode node)
    {
        var seedValue = Convert.ToInt64(node.Properties.GetValueOrDefault("seed")?.Value ?? -1);
        var randomize = Convert.ToBoolean(node.Properties.GetValueOrDefault("randomize")?.Value ?? true);

        if (randomize || seedValue < 0)
        {
            seedValue = Random.Shared.NextInt64();
        }

        return new Dictionary<string, object?> { ["seed"] = seedValue };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AI NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, object?>> ExecuteAITextGeneratorAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var prompt = context.InputValues.GetValueOrDefault("prompt")?.ToString() ?? "";
        var systemPrompt = context.InputValues.GetValueOrDefault("system_prompt")?.ToString();
        var provider = node.Properties.GetValueOrDefault("provider")?.Value?.ToString() ?? "ollama";
        var model = node.Properties.GetValueOrDefault("model")?.Value?.ToString() ?? "llama3.2:3b";
        var temperature = Convert.ToDouble(node.Properties.GetValueOrDefault("temperature")?.Value ?? 0.7);
        var maxTokens = Convert.ToInt32(node.Properties.GetValueOrDefault("max_tokens")?.Value ?? 1000);

        context.Log?.Invoke($"Generating text with {provider}/{model}...");

        var fullPrompt = string.IsNullOrEmpty(systemPrompt)
            ? prompt
            : $"{systemPrompt}\n\n{prompt}";

        // Use the actual GenerateAsync method signature
        var result = await _contentGenerator.GenerateAsync(
            fullPrompt,
            null, // brandInfo
            "general", // platform
            "th", // language
            ct);

        return new Dictionary<string, object?>
        {
            ["generated_text"] = result?.Text ?? ""
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteAIImageGeneratorAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString() ?? "";
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed") ?? -1);
        var referenceImage = context.InputValues.GetValueOrDefault("reference_image")?.ToString();

        var provider = node.Properties.GetValueOrDefault("provider")?.Value?.ToString() ?? "comfyui";
        var model = node.Properties.GetValueOrDefault("model")?.Value?.ToString() ?? "sd_xl_base_1.0";
        var width = Convert.ToInt32(node.Properties.GetValueOrDefault("width")?.Value ?? 1024);
        var height = Convert.ToInt32(node.Properties.GetValueOrDefault("height")?.Value ?? 1024);
        var steps = Convert.ToInt32(node.Properties.GetValueOrDefault("steps")?.Value ?? 20);
        var cfgScale = Convert.ToDouble(node.Properties.GetValueOrDefault("cfg_scale")?.Value ?? 7.0);
        var sampler = node.Properties.GetValueOrDefault("sampler")?.Value?.ToString() ?? "euler";

        context.Log?.Invoke($"Generating image with {provider}...");
        context.Log?.Invoke($"Prompt: {positivePrompt}");
        context.Log?.Invoke($"Size: {width}x{height}, Steps: {steps}");

        // Build combined prompt with negative
        var fullPrompt = negativePrompt.Length > 0
            ? $"{positivePrompt} | Negative: {negativePrompt}"
            : positivePrompt;

        // Use the actual GenerateAsync method signature
        var result = await _imageGenerator.GenerateAsync(
            fullPrompt,
            new ImageGenerationOptions
            {
                Style = "default",
                Width = width,
                Height = height,
                Provider = provider
            },
            ct);

        return new Dictionary<string, object?>
        {
            ["image"] = result?.Base64Data ?? ""
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteAIChatAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var message = context.InputValues.GetValueOrDefault("message")?.ToString() ?? "";
        var contextInputs = context.InputValues.GetValueOrDefault("context");
        var provider = node.Properties.GetValueOrDefault("provider")?.Value?.ToString() ?? "ollama";
        var model = node.Properties.GetValueOrDefault("model")?.Value?.ToString() ?? "llama3.2:3b";

        // Build context string if provided
        var fullMessage = message;
        if (contextInputs is List<object?> contextList)
        {
            var contextStr = string.Join("\n", contextList.Where(c => c != null));
            if (!string.IsNullOrEmpty(contextStr))
            {
                fullMessage = $"Context:\n{contextStr}\n\nMessage: {message}";
            }
        }

        context.Log?.Invoke($"Chatting with {provider}/{model}...");

        // Use the actual GenerateAsync method signature
        var result = await _contentGenerator.GenerateAsync(
            fullMessage,
            null, // brandInfo
            "general", // platform
            "th", // language
            ct);

        return new Dictionary<string, object?>
        {
            ["response"] = result?.Text ?? ""
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PROCESSING NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private Dictionary<string, object?> ExecuteTextCombiner(WorkflowNode node, NodeExecutionContext context)
    {
        var text1 = context.InputValues.GetValueOrDefault("text_1")?.ToString() ?? "";
        var text2 = context.InputValues.GetValueOrDefault("text_2")?.ToString() ?? "";
        var separator = node.Properties.GetValueOrDefault("separator")?.Value?.ToString() ?? "\n";

        var combined = string.IsNullOrEmpty(text2) ? text1 : $"{text1}{separator}{text2}";
        return new Dictionary<string, object?> { ["combined"] = combined };
    }

    private Task<Dictionary<string, object?>> ExecuteImageResizeAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var imageBase64 = context.InputValues.GetValueOrDefault("image")?.ToString();
        var width = Convert.ToInt32(node.Properties.GetValueOrDefault("width")?.Value ?? 512);
        var height = Convert.ToInt32(node.Properties.GetValueOrDefault("height")?.Value ?? 512);

        // For now, pass through (actual resize implementation would use ImageSharp or similar)
        context.Log?.Invoke($"Resizing image to {width}x{height}");

        return Task.FromResult(new Dictionary<string, object?>
        {
            ["image"] = imageBase64
        });
    }

    private Dictionary<string, object?> ExecuteSwitch(WorkflowNode node, NodeExecutionContext context)
    {
        var condition = Convert.ToBoolean(context.InputValues.GetValueOrDefault("condition") ?? false);
        var valueTrue = context.InputValues.GetValueOrDefault("value_true");
        var valueFalse = context.InputValues.GetValueOrDefault("value_false");

        return new Dictionary<string, object?>
        {
            ["output"] = condition ? valueTrue : valueFalse
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // OUTPUT NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, object?>> ExecuteSaveImageAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var imageBase64 = context.InputValues.GetValueOrDefault("image")?.ToString();
        var folder = node.Properties.GetValueOrDefault("folder")?.Value?.ToString() ?? "outputs";
        var prefix = node.Properties.GetValueOrDefault("filename_prefix")?.Value?.ToString() ?? "image";
        var format = node.Properties.GetValueOrDefault("format")?.Value?.ToString() ?? "png";

        if (string.IsNullOrEmpty(imageBase64))
        {
            throw new InvalidOperationException("No image to save");
        }

        // Create output folder
        Directory.CreateDirectory(folder);

        // Generate filename
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"{prefix}_{timestamp}.{format}";
        var path = Path.Combine(folder, filename);

        // Save image
        var bytes = Convert.FromBase64String(imageBase64);
        await File.WriteAllBytesAsync(path, bytes, ct);

        context.Log?.Invoke($"Image saved: {path}");

        return new Dictionary<string, object?>
        {
            ["path"] = Path.GetFullPath(path)
        };
    }

    private Dictionary<string, object?> ExecutePreview(WorkflowNode node, NodeExecutionContext context)
    {
        var input = context.InputValues.GetValueOrDefault("input");

        // Preview node doesn't produce output, just displays
        context.Log?.Invoke($"Preview: {input?.ToString()?.Substring(0, Math.Min(100, input?.ToString()?.Length ?? 0))}...");

        return new Dictionary<string, object?>();
    }

    private Dictionary<string, object?> ExecuteConsoleOutput(WorkflowNode node, NodeExecutionContext context)
    {
        var input = context.InputValues.GetValueOrDefault("input");
        var label = node.Properties.GetValueOrDefault("label")?.Value?.ToString() ?? "";

        var output = string.IsNullOrEmpty(label) ? $"{input}" : $"{label}: {input}";
        context.Log?.Invoke(output);

        return new Dictionary<string, object?>();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SOCIAL MEDIA NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, object?>> ExecuteSocialPostAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var text = context.InputValues.GetValueOrDefault("text")?.ToString() ?? "";
        var image = context.InputValues.GetValueOrDefault("image");
        var platform = node.Properties.GetValueOrDefault("platform")?.Value?.ToString() ?? "facebook";
        var accountId = node.Properties.GetValueOrDefault("account_id")?.Value?.ToString();

        context.Log?.Invoke($"Posting to {platform}...");
        context.Log?.Invoke($"Text: {text.Substring(0, Math.Min(50, text.Length))}...");

        // TODO: Integrate with actual social media posting service
        await Task.Delay(1000, ct); // Simulate posting

        return new Dictionary<string, object?>
        {
            ["post_id"] = $"post_{Guid.NewGuid():N}",
            ["url"] = $"https://{platform}.com/post/12345"
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UTILITY NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, object?>> ExecuteLoopAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var input = context.InputValues.GetValueOrDefault("input");
        var list = context.InputValues.GetValueOrDefault("list") as List<object?>;
        var count = Convert.ToInt32(node.Properties.GetValueOrDefault("count")?.Value ?? 1);

        // If list is provided, use its length
        if (list != null && list.Count > 0)
        {
            // This is a simplified implementation - full loop would require workflow engine changes
            return new Dictionary<string, object?>
            {
                ["item"] = list.FirstOrDefault(),
                ["index"] = 0
            };
        }

        return new Dictionary<string, object?>
        {
            ["item"] = input,
            ["index"] = 0
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteDelayAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var trigger = context.InputValues.GetValueOrDefault("trigger");
        var delayMs = Convert.ToInt32(node.Properties.GetValueOrDefault("delay_ms")?.Value ?? 1000);

        context.Log?.Invoke($"Waiting {delayMs}ms...");
        await Task.Delay(delayMs, ct);

        return new Dictionary<string, object?>
        {
            ["trigger"] = trigger
        };
    }

    private void Log(string message)
    {
        _logger.LogInformation(message);
        LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DIFFUSERS NODE EXECUTORS
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, object?>> ExecuteDiffusersLoadModelAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var modelPath = node.Properties.GetValueOrDefault("model_path")?.Value?.ToString() ?? "";
        var modelType = node.Properties.GetValueOrDefault("model_type")?.Value?.ToString() ?? "stable_diffusion";
        var precision = node.Properties.GetValueOrDefault("precision")?.Value?.ToString() ?? "fp16";
        var device = node.Properties.GetValueOrDefault("device")?.Value?.ToString() ?? "cuda:0";

        context.Log?.Invoke($"Loading model: {modelPath} ({modelType}, {precision})");

        // TODO: Integrate with actual DiffusersGenerationEngine
        await Task.Delay(500, ct); // Simulate loading

        return new Dictionary<string, object?>
        {
            ["model"] = new { Path = modelPath, Type = modelType, Precision = precision, Device = device },
            ["model_path"] = modelPath,
            ["model_type"] = modelType
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteDiffusersGenerateImageAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var model = context.InputValues.GetValueOrDefault("model");
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString() ?? "";
        var latent = context.InputValues.GetValueOrDefault("latent");
        var conditioning = context.InputValues.GetValueOrDefault("conditioning");

        var steps = Convert.ToInt32(node.Properties.GetValueOrDefault("steps")?.Value ?? 20);
        var cfgScale = Convert.ToDouble(node.Properties.GetValueOrDefault("cfg_scale")?.Value ?? 7.0);
        var width = Convert.ToInt32(node.Properties.GetValueOrDefault("width")?.Value ?? 1024);
        var height = Convert.ToInt32(node.Properties.GetValueOrDefault("height")?.Value ?? 1024);
        var sampler = node.Properties.GetValueOrDefault("sampler")?.Value?.ToString() ?? "euler";
        var scheduler = node.Properties.GetValueOrDefault("scheduler")?.Value?.ToString() ?? "normal";
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed") ?? -1);

        if (seed < 0) seed = Random.Shared.NextInt64();

        context.Log?.Invoke($"Generating image: {width}x{height}, Steps: {steps}, CFG: {cfgScale}");
        context.Log?.Invoke($"Prompt: {positivePrompt.Substring(0, Math.Min(50, positivePrompt.Length))}...");

        // Prefer direct DiffusersEngine if available
        if (_diffusersEngine?.IsRunning == true)
        {
            context.Log?.Invoke("Using local Diffusers Engine directly");
            var request = new DiffusersImageRequest
            {
                Prompt = positivePrompt,
                NegativePrompt = negativePrompt,
                Width = width,
                Height = height,
                Steps = steps,
                GuidanceScale = cfgScale,
                Seed = seed,
                Sampler = sampler,
                Scheduler = scheduler
            };

            var engineResult = await _diffusersEngine.GenerateImageAsync(request, ct);

            if (engineResult.Success && engineResult.Images?.Count > 0)
            {
                var imageData = engineResult.Images[0];
                // Extract base64 from data URI if needed
                if (imageData.StartsWith("data:image/"))
                {
                    imageData = imageData.Substring(imageData.IndexOf(",") + 1);
                }

                context.Log?.Invoke($"Generation completed! Seed: {engineResult.Seed}, Time: {engineResult.GenerationTime:F1}s");

                return new Dictionary<string, object?>
                {
                    ["image"] = imageData,
                    ["seed"] = engineResult.Seed
                };
            }
            else
            {
                context.Log?.Invoke($"Direct engine generation failed: {engineResult.Error}. Falling back to ImageGeneratorService.");
            }
        }

        // Fallback to image generator service (tries diffusers provider first, then others)
        context.Log?.Invoke("Using ImageGeneratorService with 'auto' provider");
        var result = await _imageGenerator.GenerateAsync(
            positivePrompt,
            new ImageGenerationOptions
            {
                Width = width,
                Height = height,
                Provider = "auto", // Will try diffusers first if available
                Style = "default"
            },
            ct);

        return new Dictionary<string, object?>
        {
            ["image"] = result?.Base64Data ?? "",
            ["seed"] = seed
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteDiffusersGenerateVideoAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var model = context.InputValues.GetValueOrDefault("model");
        var image = context.InputValues.GetValueOrDefault("image")?.ToString();
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString() ?? "";

        var frames = Convert.ToInt32(node.Properties.GetValueOrDefault("frames")?.Value ?? 24);
        var fps = Convert.ToInt32(node.Properties.GetValueOrDefault("fps")?.Value ?? 8);
        var motionStrength = Convert.ToDouble(node.Properties.GetValueOrDefault("motion_strength")?.Value ?? 0.5);
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed") ?? -1);

        if (seed < 0) seed = Random.Shared.NextInt64();

        context.Log?.Invoke($"Generating video: {frames} frames @ {fps} fps");
        context.Log?.Invoke($"Prompt: {positivePrompt.Substring(0, Math.Min(50, positivePrompt.Length))}...");

        // TODO: Integrate with actual video generation service
        await Task.Delay(2000, ct); // Simulate generation

        return new Dictionary<string, object?>
        {
            ["video"] = "", // Base64 video or path
            ["seed"] = seed,
            ["frames"] = frames,
            ["fps"] = fps
        };
    }

    private Dictionary<string, object?> ExecuteDiffusersLoRA(WorkflowNode node, NodeExecutionContext context)
    {
        var model = context.InputValues.GetValueOrDefault("model");
        var loraPath = node.Properties.GetValueOrDefault("lora_path")?.Value?.ToString() ?? "";
        var strength = Convert.ToDouble(node.Properties.GetValueOrDefault("strength")?.Value ?? 1.0);

        context.Log?.Invoke($"Applying LoRA: {loraPath} (strength: {strength})");

        return new Dictionary<string, object?>
        {
            ["model"] = model,
            ["lora_applied"] = new { Path = loraPath, Strength = strength }
        };
    }

    private Dictionary<string, object?> ExecuteDiffusersControlNet(WorkflowNode node, NodeExecutionContext context)
    {
        var model = context.InputValues.GetValueOrDefault("model");
        var conditionImage = context.InputValues.GetValueOrDefault("condition_image")?.ToString();
        var controlnetPath = node.Properties.GetValueOrDefault("controlnet_path")?.Value?.ToString() ?? "";
        var strength = Convert.ToDouble(node.Properties.GetValueOrDefault("strength")?.Value ?? 1.0);
        var controlType = node.Properties.GetValueOrDefault("control_type")?.Value?.ToString() ?? "canny";

        context.Log?.Invoke($"Applying ControlNet: {controlType} (strength: {strength})");

        return new Dictionary<string, object?>
        {
            ["model"] = model,
            ["conditioning"] = new { Type = controlType, Image = conditionImage, Strength = strength }
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteDiffusersPreprocessorAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var image = context.InputValues.GetValueOrDefault("image")?.ToString();
        var preprocessorType = node.Properties.GetValueOrDefault("preprocessor_type")?.Value?.ToString() ?? "canny";
        var resolution = Convert.ToInt32(node.Properties.GetValueOrDefault("resolution")?.Value ?? 512);

        context.Log?.Invoke($"Running preprocessor: {preprocessorType} @ {resolution}px");

        // TODO: Integrate with actual preprocessor
        await Task.Delay(500, ct);

        return new Dictionary<string, object?>
        {
            ["processed_image"] = image, // Pass through for now
            ["preprocessor_type"] = preprocessorType
        };
    }

    private async Task<Dictionary<string, object?>> ExecuteDiffusersUpscaleAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var image = context.InputValues.GetValueOrDefault("image")?.ToString();
        var upscaleModel = node.Properties.GetValueOrDefault("upscale_model")?.Value?.ToString() ?? "RealESRGAN_x4plus";
        var scaleFactor = Convert.ToInt32(node.Properties.GetValueOrDefault("scale_factor")?.Value ?? 2);

        context.Log?.Invoke($"Upscaling image: {upscaleModel} x{scaleFactor}");

        // TODO: Integrate with actual upscaler
        await Task.Delay(1000, ct);

        return new Dictionary<string, object?>
        {
            ["image"] = image, // Pass through for now
            ["scale_factor"] = scaleFactor
        };
    }

    private Dictionary<string, object?> ExecuteDiffusersVAE(WorkflowNode node, NodeExecutionContext context)
    {
        var model = context.InputValues.GetValueOrDefault("model");
        var vaePath = node.Properties.GetValueOrDefault("vae_path")?.Value?.ToString() ?? "";

        context.Log?.Invoke($"Applying VAE: {vaePath}");

        return new Dictionary<string, object?>
        {
            ["model"] = model,
            ["vae_applied"] = vaePath
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PIPELINE TEMPLATE NODE EXECUTORS (All-in-One Macro Nodes)
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, object?>> ExecutePipelineImageGenerationAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        // Get prompts from inputs or properties
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("positive_prompt")?.Value?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("negative_prompt")?.Value?.ToString() ?? "";

        // Model Block settings
        var checkpoint = node.Properties.GetValueOrDefault("checkpoint")?.Value?.ToString() ?? "";
        var precision = node.Properties.GetValueOrDefault("precision")?.Value?.ToString() ?? "fp16";

        // Sampler Block settings
        var sampler = node.Properties.GetValueOrDefault("sampler")?.Value?.ToString() ?? "euler";
        var scheduler = node.Properties.GetValueOrDefault("scheduler")?.Value?.ToString() ?? "normal";
        var steps = Convert.ToInt32(node.Properties.GetValueOrDefault("steps")?.Value ?? 20);
        var cfgScale = Convert.ToDouble(node.Properties.GetValueOrDefault("cfg_scale")?.Value ?? 7.0);
        var width = Convert.ToInt32(node.Properties.GetValueOrDefault("width")?.Value ?? 1024);
        var height = Convert.ToInt32(node.Properties.GetValueOrDefault("height")?.Value ?? 1024);
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed")
            ?? node.Properties.GetValueOrDefault("seed")?.Value ?? -1);

        if (seed < 0) seed = Random.Shared.NextInt64();

        // Post-Process Block
        var enableUpscale = Convert.ToBoolean(node.Properties.GetValueOrDefault("enable_upscale")?.Value ?? false);
        var upscaleModel = node.Properties.GetValueOrDefault("upscale_model")?.Value?.ToString() ?? "RealESRGAN_x4plus";

        // Output Block
        var outputDir = node.Properties.GetValueOrDefault("output_directory")?.Value?.ToString() ?? "outputs";
        var filenamePrefix = node.Properties.GetValueOrDefault("filename_prefix")?.Value?.ToString() ?? "img";
        var outputFormat = node.Properties.GetValueOrDefault("output_format")?.Value?.ToString() ?? "png";

        context.Log?.Invoke($"=== Image Generation Pipeline ===");
        context.Log?.Invoke($"Model: {checkpoint} ({precision})");
        context.Log?.Invoke($"Size: {width}x{height}, Steps: {steps}, CFG: {cfgScale}");
        context.Log?.Invoke($"Sampler: {sampler}, Scheduler: {scheduler}");
        context.Log?.Invoke($"Prompt: {positivePrompt.Substring(0, Math.Min(60, positivePrompt.Length))}...");

        string imageData = "";

        // Prefer direct DiffusersEngine if available
        if (_diffusersEngine?.IsRunning == true)
        {
            context.Log?.Invoke("Using local Diffusers Engine");
            var request = new DiffusersImageRequest
            {
                Prompt = $"masterpiece, best quality, {positivePrompt}",
                NegativePrompt = string.IsNullOrEmpty(negativePrompt)
                    ? "blurry, low quality, distorted, watermark"
                    : negativePrompt,
                Width = width,
                Height = height,
                Steps = steps,
                GuidanceScale = cfgScale,
                Seed = seed,
                Sampler = sampler,
                Scheduler = scheduler
            };

            var engineResult = await _diffusersEngine.GenerateImageAsync(request, ct);

            if (engineResult.Success && engineResult.Images?.Count > 0)
            {
                imageData = engineResult.Images[0];
                if (imageData.StartsWith("data:image/"))
                {
                    imageData = imageData.Substring(imageData.IndexOf(",") + 1);
                }
                seed = engineResult.Seed;
                context.Log?.Invoke($"Generation completed! Seed: {seed}, Time: {engineResult.GenerationTime:F1}s");
            }
            else
            {
                context.Log?.Invoke($"Engine failed: {engineResult.Error}. Trying ImageGeneratorService...");
            }
        }

        // Fallback if direct engine failed or not available
        if (string.IsNullOrEmpty(imageData))
        {
            context.Log?.Invoke("Using ImageGeneratorService with auto provider");
            var result = await _imageGenerator.GenerateAsync(
                positivePrompt,
                new ImageGenerationOptions
                {
                    Width = width,
                    Height = height,
                    Provider = "auto",
                    Style = "default"
                },
                ct);

            imageData = result?.Base64Data ?? "";
        }

        // Optional upscale
        if (enableUpscale && !string.IsNullOrEmpty(imageData))
        {
            context.Log?.Invoke($"Upscaling with {upscaleModel}...");
            // TODO: Actual upscale integration
        }

        // Save output
        string? savedPath = null;
        if (!string.IsNullOrEmpty(imageData) && !string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"{filenamePrefix}_{timestamp}_{seed}.{outputFormat}";
            savedPath = Path.Combine(outputDir, filename);

            var bytes = Convert.FromBase64String(imageData);
            await File.WriteAllBytesAsync(savedPath, bytes, ct);
            context.Log?.Invoke($"Saved: {savedPath}");
        }

        return new Dictionary<string, object?>
        {
            ["image"] = imageData,
            ["seed"] = seed,
            ["path"] = savedPath
        };
    }

    private async Task<Dictionary<string, object?>> ExecutePipelineVideoGenerationAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("positive_prompt")?.Value?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("negative_prompt")?.Value?.ToString() ?? "";
        var sourceImage = context.InputValues.GetValueOrDefault("source_image")?.ToString();

        // Video settings
        var videoModel = node.Properties.GetValueOrDefault("video_model")?.Value?.ToString() ?? "animatediff";
        var frames = Convert.ToInt32(node.Properties.GetValueOrDefault("frames")?.Value ?? 24);
        var fps = Convert.ToInt32(node.Properties.GetValueOrDefault("fps")?.Value ?? 8);
        var motionStrength = Convert.ToDouble(node.Properties.GetValueOrDefault("motion_strength")?.Value ?? 0.5);
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed")
            ?? node.Properties.GetValueOrDefault("seed")?.Value ?? -1);

        if (seed < 0) seed = Random.Shared.NextInt64();

        // Output settings
        var outputDir = node.Properties.GetValueOrDefault("output_directory")?.Value?.ToString() ?? "outputs/videos";
        var filenamePrefix = node.Properties.GetValueOrDefault("filename_prefix")?.Value?.ToString() ?? "video";
        var outputFormat = node.Properties.GetValueOrDefault("output_format")?.Value?.ToString() ?? "mp4";

        context.Log?.Invoke($"=== Video Generation Pipeline ===");
        context.Log?.Invoke($"Model: {videoModel}");
        context.Log?.Invoke($"Frames: {frames} @ {fps} fps");
        context.Log?.Invoke($"Motion: {motionStrength}");

        // TODO: Integrate with actual video generation service
        await Task.Delay(3000, ct); // Simulate generation

        return new Dictionary<string, object?>
        {
            ["video"] = "",
            ["seed"] = seed,
            ["frames"] = frames,
            ["fps"] = fps,
            ["path"] = null
        };
    }

    private async Task<Dictionary<string, object?>> ExecutePipelineImg2ImgAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var sourceImage = context.InputValues.GetValueOrDefault("source_image")?.ToString();
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("positive_prompt")?.Value?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("negative_prompt")?.Value?.ToString() ?? "";

        var denoise = Convert.ToDouble(node.Properties.GetValueOrDefault("denoise_strength")?.Value ?? 0.75);
        var steps = Convert.ToInt32(node.Properties.GetValueOrDefault("steps")?.Value ?? 20);
        var cfgScale = Convert.ToDouble(node.Properties.GetValueOrDefault("cfg_scale")?.Value ?? 7.0);
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed")
            ?? node.Properties.GetValueOrDefault("seed")?.Value ?? -1);

        if (seed < 0) seed = Random.Shared.NextInt64();

        context.Log?.Invoke($"=== Img2Img Pipeline ===");
        context.Log?.Invoke($"Denoise: {denoise}, Steps: {steps}, CFG: {cfgScale}");

        if (string.IsNullOrEmpty(sourceImage))
        {
            throw new InvalidOperationException("Source image is required for img2img");
        }

        // TODO: Integrate with actual img2img service
        await Task.Delay(2000, ct);

        return new Dictionary<string, object?>
        {
            ["image"] = sourceImage, // Pass through for now
            ["seed"] = seed
        };
    }

    private async Task<Dictionary<string, object?>> ExecutePipelineInpaintAsync(
        WorkflowNode node, NodeExecutionContext context, CancellationToken ct)
    {
        var sourceImage = context.InputValues.GetValueOrDefault("source_image")?.ToString();
        var maskImage = context.InputValues.GetValueOrDefault("mask")?.ToString();
        var positivePrompt = context.InputValues.GetValueOrDefault("positive_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("positive_prompt")?.Value?.ToString() ?? "";
        var negativePrompt = context.InputValues.GetValueOrDefault("negative_prompt")?.ToString()
            ?? node.Properties.GetValueOrDefault("negative_prompt")?.Value?.ToString() ?? "";

        var denoise = Convert.ToDouble(node.Properties.GetValueOrDefault("denoise_strength")?.Value ?? 1.0);
        var maskBlur = Convert.ToInt32(node.Properties.GetValueOrDefault("mask_blur")?.Value ?? 4);
        var steps = Convert.ToInt32(node.Properties.GetValueOrDefault("steps")?.Value ?? 20);
        var seed = Convert.ToInt64(context.InputValues.GetValueOrDefault("seed")
            ?? node.Properties.GetValueOrDefault("seed")?.Value ?? -1);

        if (seed < 0) seed = Random.Shared.NextInt64();

        context.Log?.Invoke($"=== Inpaint Pipeline ===");
        context.Log?.Invoke($"Denoise: {denoise}, Mask Blur: {maskBlur}, Steps: {steps}");

        if (string.IsNullOrEmpty(sourceImage))
        {
            throw new InvalidOperationException("Source image is required for inpainting");
        }

        if (string.IsNullOrEmpty(maskImage))
        {
            throw new InvalidOperationException("Mask image is required for inpainting");
        }

        // TODO: Integrate with actual inpaint service
        await Task.Delay(2000, ct);

        return new Dictionary<string, object?>
        {
            ["image"] = sourceImage, // Pass through for now
            ["seed"] = seed
        };
    }
}

/// <summary>
/// Node execution state
/// </summary>
public enum NodeExecutionState
{
    Idle,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Node-based workflow execution result (different from WebAutomation workflow)
/// </summary>
public class NodeWorkflowExecutionResult
{
    public string WorkflowId { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public Dictionary<string, NodeExecutionResult> NodeResults { get; set; } = new();
    public Dictionary<string, object?> FinalOutputs { get; set; } = new();

    public TimeSpan Duration => CompletedAt - StartedAt;
}
