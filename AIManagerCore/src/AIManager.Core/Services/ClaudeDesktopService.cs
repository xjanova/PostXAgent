using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Claude Desktop Integration Service - เชื่อมต่อ Local AI กับ Claude Desktop
/// รองรับ 3 วิธี:
/// 1. MCP Server (Model Context Protocol) - สำหรับ Claude Desktop
/// 2. Claude API - เรียกผ่าน Anthropic API โดยตรง
/// 3. File-based Communication - แชร์ไฟล์ใน workspace
/// </summary>
public class ClaudeDesktopService
{
    private readonly ILogger<ClaudeDesktopService> _logger;
    private readonly ClaudeDesktopConfig _config;
    private readonly HttpClient _httpClient;
    private readonly ContentGeneratorService _contentGenerator;

    // MCP Server
    private HttpListener? _mcpListener;
    private bool _mcpRunning;

    // Message queue for async communication
    private readonly Queue<ClaudeMessage> _incomingMessages = new();
    private readonly Queue<ClaudeMessage> _outgoingMessages = new();

    // Event for message received
    public event EventHandler<ClaudeMessage>? MessageReceived;
    public event EventHandler<string>? ConnectionStatusChanged;

    public ClaudeDesktopService(
        ClaudeDesktopConfig config,
        ContentGeneratorService contentGenerator,
        ILogger<ClaudeDesktopService>? logger = null)
    {
        _config = config;
        _contentGenerator = contentGenerator;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ClaudeDesktopService>();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    /// <summary>
    /// Helper method to generate text using ContentGeneratorService
    /// </summary>
    private async Task<string?> GenerateTextWithLocalAIAsync(string prompt, CancellationToken ct)
    {
        try
        {
            var result = await _contentGenerator.GenerateAsync(
                prompt,
                null, // no brand info
                "general",
                "th",
                ct);
            return result?.Text;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local AI generation failed");
            return null;
        }
    }

    #region MCP Server (for Claude Desktop)

    /// <summary>
    /// Start MCP Server for Claude Desktop to connect
    /// Claude Desktop สามารถเชื่อมต่อกับ MCP Server เพื่อรับ tools และข้อมูลจาก Local AI
    /// </summary>
    public async Task StartMcpServerAsync(CancellationToken ct = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogWarning("Claude Desktop integration is disabled");
            return;
        }

        try
        {
            _mcpListener = new HttpListener();
            _mcpListener.Prefixes.Add($"http://localhost:{_config.McpServerPort}/");
            _mcpListener.Start();
            _mcpRunning = true;

            _logger.LogInformation("🔌 MCP Server started on port {Port}", _config.McpServerPort);
            ConnectionStatusChanged?.Invoke(this, "MCP Server Running");

            while (_mcpRunning && !ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _mcpListener.GetContextAsync();
                    _ = HandleMcpRequestAsync(context, ct);
                }
                catch (HttpListenerException) when (!_mcpRunning)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP Server failed to start");
            ConnectionStatusChanged?.Invoke(this, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop MCP Server
    /// </summary>
    public void StopMcpServer()
    {
        _mcpRunning = false;
        _mcpListener?.Stop();
        _mcpListener?.Close();
        _logger.LogInformation("MCP Server stopped");
        ConnectionStatusChanged?.Invoke(this, "Disconnected");
    }

    /// <summary>
    /// Handle MCP requests from Claude Desktop
    /// </summary>
    private async Task HandleMcpRequestAsync(HttpListenerContext context, CancellationToken ct)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Parse JSON-RPC request
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            var rpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body);

            if (rpcRequest == null)
            {
                await SendErrorResponseAsync(response, -32700, "Parse error");
                return;
            }

            _logger.LogDebug("MCP Request: {Method}", rpcRequest.Method);

            // Handle different MCP methods
            object? result = rpcRequest.Method switch
            {
                "initialize" => HandleInitialize(rpcRequest),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolsCallAsync(rpcRequest, ct),
                "prompts/list" => HandlePromptsList(),
                "prompts/get" => HandlePromptsGet(rpcRequest),
                "resources/list" => HandleResourcesList(),
                "resources/read" => await HandleResourcesReadAsync(rpcRequest, ct),
                "notifications/message" => HandleNotification(rpcRequest),
                _ => null
            };

            if (result != null)
            {
                await SendSuccessResponseAsync(response, rpcRequest.Id, result);
            }
            else
            {
                await SendErrorResponseAsync(response, -32601, $"Method not found: {rpcRequest.Method}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling MCP request");
            await SendErrorResponseAsync(response, -32603, ex.Message);
        }
    }

    private object HandleInitialize(JsonRpcRequest request)
    {
        return new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { },
                prompts = new { },
                resources = new { }
            },
            serverInfo = new
            {
                name = "PostXAgent AI Manager",
                version = "1.0.0"
            }
        };
    }

    private object HandleToolsList()
    {
        return new
        {
            tools = new object[]
            {
                new
                {
                    name = "analyze_posting_error",
                    description = "วิเคราะห์ error ที่เกิดขึ้นขณะโพสต์บน Social Media และเสนอวิธีแก้",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["platform"] = new { type = "string", description = "Platform (Facebook, Instagram, etc.)" },
                            ["error_message"] = new { type = "string", description = "Error message ที่ได้รับ" },
                            ["page_html"] = new { type = "string", description = "HTML ของหน้าปัจจุบัน (optional)" }
                        },
                        required = new[] { "platform", "error_message" }
                    }
                },
                new
                {
                    name = "generate_posting_code",
                    description = "สร้าง JavaScript code สำหรับโพสต์ content บน Social Media",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["platform"] = new { type = "string", description = "Platform target" },
                            ["task_type"] = new { type = "string", description = "Type of task (post_content, reply, etc.)" },
                            ["content"] = new { type = "string", description = "Content to post" }
                        },
                        required = new[] { "platform", "task_type", "content" }
                    }
                },
                new
                {
                    name = "get_worker_status",
                    description = "ดูสถานะของ workers ทั้งหมด",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>()
                    }
                },
                new
                {
                    name = "send_local_ai_message",
                    description = "ส่งข้อความให้ Local AI (Ollama) วิเคราะห์และตอบกลับ",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>
                        {
                            ["message"] = new { type = "string", description = "ข้อความที่จะส่งให้ Local AI" },
                            ["context"] = new { type = "string", description = "Context เพิ่มเติม (optional)" }
                        },
                        required = new[] { "message" }
                    }
                }
            }
        };
    }

    private async Task<object> HandleToolsCallAsync(JsonRpcRequest request, CancellationToken ct)
    {
        var args = request.Params?.GetProperty("arguments");
        var toolName = request.Params?.GetProperty("name").GetString();

        return toolName switch
        {
            "analyze_posting_error" => await AnalyzePostingErrorAsync(args, ct),
            "generate_posting_code" => await GeneratePostingCodeAsync(args, ct),
            "get_worker_status" => GetWorkerStatus(),
            "send_local_ai_message" => await SendLocalAiMessageAsync(args, ct),
            _ => new { error = $"Unknown tool: {toolName}" }
        };
    }

    private object HandlePromptsList()
    {
        return new
        {
            prompts = new[]
            {
                new
                {
                    name = "fix_posting_workflow",
                    description = "ช่วยซ่อมแซม workflow ที่ใช้โพสต์บน Social Media"
                },
                new
                {
                    name = "optimize_content",
                    description = "ปรับปรุงเนื้อหาให้เหมาะกับ platform"
                },
                new
                {
                    name = "debug_automation",
                    description = "Debug ปัญหา automation"
                }
            }
        };
    }

    private object HandlePromptsGet(JsonRpcRequest request)
    {
        var promptName = request.Params?.GetProperty("name").GetString();

        return promptName switch
        {
            "fix_posting_workflow" => new
            {
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new { type = "text", text = "กรุณาวิเคราะห์ปัญหาและช่วยซ่อมแซม workflow สำหรับโพสต์บน Social Media" }
                    }
                }
            },
            "optimize_content" => new
            {
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new { type = "text", text = "กรุณาปรับปรุงเนื้อหาให้เหมาะกับ platform" }
                    }
                }
            },
            _ => new { error = "Prompt not found" }
        };
    }

    private object HandleResourcesList()
    {
        return new
        {
            resources = new[]
            {
                new
                {
                    uri = "postxagent://workers/status",
                    name = "Worker Status",
                    description = "สถานะของ workers ทั้งหมด",
                    mimeType = "application/json"
                },
                new
                {
                    uri = "postxagent://knowledge/recent",
                    name = "Recent Knowledge",
                    description = "ความรู้ที่เรียนรู้ล่าสุด",
                    mimeType = "application/json"
                },
                new
                {
                    uri = "postxagent://config/current",
                    name = "Current Configuration",
                    description = "การตั้งค่าปัจจุบัน",
                    mimeType = "application/json"
                }
            }
        };
    }

    private async Task<object> HandleResourcesReadAsync(JsonRpcRequest request, CancellationToken ct)
    {
        var uri = request.Params?.GetProperty("uri").GetString();

        return uri switch
        {
            "postxagent://workers/status" => new { contents = new[] { new { text = JsonSerializer.Serialize(GetWorkerStatus()) } } },
            "postxagent://config/current" => new { contents = new[] { new { text = JsonSerializer.Serialize(_config) } } },
            _ => new { error = "Resource not found" }
        };
    }

    private object HandleNotification(JsonRpcRequest request)
    {
        // Handle incoming notifications from Claude Desktop
        var message = new ClaudeMessage
        {
            Type = "notification",
            Content = request.Params?.ToString() ?? "",
            ReceivedAt = DateTime.UtcNow
        };

        _incomingMessages.Enqueue(message);
        MessageReceived?.Invoke(this, message);

        return new { received = true };
    }

    #endregion

    #region Tool Implementations

    private async Task<object> AnalyzePostingErrorAsync(JsonElement? args, CancellationToken ct)
    {
        var platform = args?.GetProperty("platform").GetString() ?? "Unknown";
        var errorMessage = args?.GetProperty("error_message").GetString() ?? "";
        var pageHtml = args?.TryGetProperty("page_html", out var html) == true ? html.GetString() : "";

        _logger.LogInformation("Analyzing posting error for {Platform}: {Error}", platform, errorMessage);

        // Use Local AI to analyze
        var analysis = await GenerateTextWithLocalAIAsync(
            $"วิเคราะห์ error นี้และเสนอวิธีแก้: Platform={platform}, Error={errorMessage}",
            ct);

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"## การวิเคราะห์ Error\n\n" +
                           $"**Platform:** {platform}\n" +
                           $"**Error:** {errorMessage}\n\n" +
                           $"### การวิเคราะห์\n{analysis ?? "ไม่สามารถวิเคราะห์ได้"}\n\n" +
                           $"### ข้อเสนอแนะ\n" +
                           $"1. ตรวจสอบ selector ที่ใช้\n" +
                           $"2. ตรวจสอบว่า session ยังใช้งานได้\n" +
                           $"3. ลองรีเฟรชหน้าและลองใหม่"
                }
            }
        };
    }

    private async Task<object> GeneratePostingCodeAsync(JsonElement? args, CancellationToken ct)
    {
        var platform = args?.GetProperty("platform").GetString() ?? "Facebook";
        var taskType = args?.GetProperty("task_type").GetString() ?? "post_content";
        var content = args?.GetProperty("content").GetString() ?? "";

        _logger.LogInformation("Generating posting code for {Platform}/{TaskType}", platform, taskType);

        // This would integrate with AICodeGeneratorService
        var code = await GenerateTextWithLocalAIAsync(
            $"สร้าง JavaScript code สำหรับโพสต์บน {platform}: {content}",
            ct);

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = $"```javascript\n{code ?? "// Code generation failed"}\n```"
                }
            }
        };
    }

    private object GetWorkerStatus()
    {
        // This would integrate with WorkerManager
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = "Worker status: All systems operational"
                }
            }
        };
    }

    private async Task<object> SendLocalAiMessageAsync(JsonElement? args, CancellationToken ct)
    {
        var message = args?.GetProperty("message").GetString() ?? "";
        var context = args?.TryGetProperty("context", out var ctx) == true ? ctx.GetString() : "";

        _logger.LogInformation("Sending message to Local AI: {Message}", message);

        var response = await GenerateTextWithLocalAIAsync(
            string.IsNullOrEmpty(context) ? message : $"Context: {context}\n\n{message}",
            ct);

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = response ?? "Local AI ไม่ตอบกลับ"
                }
            }
        };
    }

    #endregion

    #region Direct Claude API

    /// <summary>
    /// Send message directly to Claude API (alternative to MCP)
    /// </summary>
    public async Task<string?> SendToClaudeApiAsync(
        string message,
        string? systemPrompt = null,
        CancellationToken ct = default)
    {
        var apiKey = _config.ApiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Claude API key not configured");
            return null;
        }

        var request = new
        {
            model = "claude-3-opus-20240229",
            max_tokens = 4000,
            system = systemPrompt ?? "คุณเป็น AI Assistant ที่ช่วยพัฒนาและ debug ระบบ Social Media Automation",
            messages = new[]
            {
                new { role = "user", content = message }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Claude API error: {Error}", error);
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<AnthropicApiResponse>(ct);
        return result?.Content?.FirstOrDefault()?.Text;
    }

    /// <summary>
    /// Have a conversation between Local AI and Claude
    /// Local AI สามารถคุยกับ Claude โดยตรงเพื่อขอความช่วยเหลือ
    /// </summary>
    public async Task<ClaudeConversationResult> ConversationAsync(
        string localAiPrompt,
        string? context = null,
        CancellationToken ct = default)
    {
        var result = new ClaudeConversationResult();

        try
        {
            // Step 1: Local AI generates a question/request for Claude
            var localAiMessage = await GenerateTextWithLocalAIAsync(
                $"Context: {context ?? "None"}\n\n" +
                $"Task: {localAiPrompt}\n\n" +
                $"สร้างคำถามหรือคำขอที่จะส่งให้ Claude ช่วย:",
                ct);

            result.LocalAiMessage = localAiMessage ?? localAiPrompt;

            _logger.LogInformation("Local AI message: {Message}", result.LocalAiMessage);

            // Step 2: Send to Claude API
            result.ClaudeResponse = await SendToClaudeApiAsync(
                result.LocalAiMessage,
                "คุณกำลังช่วย Local AI (Ollama) แก้ปัญหาเกี่ยวกับ Social Media Automation. ตอบเป็นภาษาไทย.",
                ct);

            _logger.LogInformation("Claude response: {Response}", result.ClaudeResponse);

            // Step 3: Local AI processes Claude's response
            var processedResponse = await GenerateTextWithLocalAIAsync(
                $"Claude ตอบว่า:\n{result.ClaudeResponse}\n\n" +
                $"สรุปคำตอบและวิธีการนำไปใช้:",
                ct);

            result.LocalAiSummary = processedResponse;
            result.Success = !string.IsNullOrEmpty(result.ClaudeResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversation failed");
            result.Error = ex.Message;
        }

        return result;
    }

    #endregion

    #region File-based Communication

    /// <summary>
    /// Write a message to the shared workspace for Claude Desktop to read
    /// </summary>
    public async Task WriteToWorkspaceAsync(string fileName, string content)
    {
        if (string.IsNullOrEmpty(_config.WorkspacePath))
        {
            _logger.LogWarning("Workspace path not configured");
            return;
        }

        var filePath = Path.Combine(_config.WorkspacePath, fileName);
        await File.WriteAllTextAsync(filePath, content);
        _logger.LogDebug("Wrote to workspace: {File}", filePath);
    }

    /// <summary>
    /// Read a response from the shared workspace
    /// </summary>
    public async Task<string?> ReadFromWorkspaceAsync(string fileName)
    {
        if (string.IsNullOrEmpty(_config.WorkspacePath))
            return null;

        var filePath = Path.Combine(_config.WorkspacePath, fileName);
        if (File.Exists(filePath))
        {
            return await File.ReadAllTextAsync(filePath);
        }
        return null;
    }

    /// <summary>
    /// Monitor workspace for new files from Claude Desktop
    /// </summary>
    public async Task StartWorkspaceMonitorAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_config.WorkspacePath))
            return;

        var watcher = new FileSystemWatcher(_config.WorkspacePath)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            Filter = "*.json"
        };

        watcher.Created += async (s, e) =>
        {
            await Task.Delay(100, ct); // Wait for file to be written
            var content = await ReadFromWorkspaceAsync(e.Name ?? "");
            if (!string.IsNullOrEmpty(content))
            {
                var message = new ClaudeMessage
                {
                    Type = "file",
                    Content = content,
                    FileName = e.Name,
                    ReceivedAt = DateTime.UtcNow
                };
                MessageReceived?.Invoke(this, message);
            }
        };

        watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Workspace monitor started: {Path}", _config.WorkspacePath);

        // Keep running until cancelled
        await Task.Delay(Timeout.Infinite, ct);
    }

    #endregion

    #region Helper Methods

    private async Task SendSuccessResponseAsync(HttpListenerResponse response, object? id, object result)
    {
        var jsonResponse = new
        {
            jsonrpc = "2.0",
            id = id,
            result = result
        };

        response.ContentType = "application/json";
        response.StatusCode = 200;

        await JsonSerializer.SerializeAsync(response.OutputStream, jsonResponse);
        response.Close();
    }

    private async Task SendErrorResponseAsync(HttpListenerResponse response, int code, string message)
    {
        var jsonResponse = new
        {
            jsonrpc = "2.0",
            error = new { code = code, message = message }
        };

        response.ContentType = "application/json";
        response.StatusCode = 400;

        await JsonSerializer.SerializeAsync(response.OutputStream, jsonResponse);
        response.Close();
    }

    #endregion
}

#region Models

public class JsonRpcRequest
{
    public string Jsonrpc { get; set; } = "2.0";
    public object? Id { get; set; }
    public string Method { get; set; } = "";
    public JsonElement? Params { get; set; }
}

public class ClaudeMessage
{
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public string? FileName { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class ClaudeConversationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? LocalAiMessage { get; set; }
    public string? ClaudeResponse { get; set; }
    public string? LocalAiSummary { get; set; }
}

internal class AnthropicApiResponse
{
    public List<AnthropicApiContent>? Content { get; set; }
}

internal class AnthropicApiContent
{
    public string? Text { get; set; }
}

#endregion
