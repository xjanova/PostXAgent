using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;

namespace AIManager.API.Controllers;

/// <summary>
/// Claude Desktop Integration Controller
/// API สำหรับจัดการการสื่อสารระหว่าง Local AI กับ Claude Desktop
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ClaudeDesktopController : ControllerBase
{
    private readonly ILogger<ClaudeDesktopController> _logger;
    private readonly ClaudeDesktopService _claudeService;
    private readonly ClaudeDesktopConfig _config;

    public ClaudeDesktopController(
        ILogger<ClaudeDesktopController> logger,
        ClaudeDesktopService claudeService,
        ClaudeDesktopConfig config)
    {
        _logger = logger;
        _claudeService = claudeService;
        _config = config;
    }

    /// <summary>
    /// Get current Claude Desktop configuration
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfiguration()
    {
        return Ok(new
        {
            enabled = _config.Enabled,
            mcpServerPort = _config.McpServerPort,
            workspacePath = _config.WorkspacePath,
            autoConnect = _config.AutoConnect,
            hasApiKey = !string.IsNullOrEmpty(_config.ApiKey)
        });
    }

    /// <summary>
    /// Update Claude Desktop configuration
    /// </summary>
    [HttpPut("config")]
    public IActionResult UpdateConfiguration([FromBody] ClaudeDesktopConfigUpdateRequest request)
    {
        if (request.Enabled.HasValue)
            _config.Enabled = request.Enabled.Value;
        if (request.McpServerPort.HasValue)
            _config.McpServerPort = request.McpServerPort.Value;
        if (!string.IsNullOrEmpty(request.WorkspacePath))
            _config.WorkspacePath = request.WorkspacePath;
        if (request.AutoConnect.HasValue)
            _config.AutoConnect = request.AutoConnect.Value;
        if (!string.IsNullOrEmpty(request.ApiKey))
            _config.ApiKey = request.ApiKey;

        return Ok(new { success = true, message = "Configuration updated" });
    }

    /// <summary>
    /// Start MCP Server for Claude Desktop to connect
    /// </summary>
    [HttpPost("mcp/start")]
    public async Task<IActionResult> StartMcpServer(CancellationToken ct)
    {
        try
        {
            if (!_config.Enabled)
            {
                return BadRequest(new { error = "Claude Desktop integration is disabled" });
            }

            _ = _claudeService.StartMcpServerAsync(ct);

            return Ok(new
            {
                success = true,
                message = "MCP Server starting...",
                port = _config.McpServerPort,
                endpoint = $"http://localhost:{_config.McpServerPort}/"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start MCP Server");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Stop MCP Server
    /// </summary>
    [HttpPost("mcp/stop")]
    public IActionResult StopMcpServer()
    {
        try
        {
            _claudeService.StopMcpServer();
            return Ok(new { success = true, message = "MCP Server stopped" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Send message to Claude API directly
    /// </summary>
    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] ClaudeMessageRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _claudeService.SendToClaudeApiAsync(
                request.Message,
                request.SystemPrompt,
                ct);

            if (response == null)
            {
                return BadRequest(new { error = "Claude API not available or failed to respond" });
            }

            return Ok(new
            {
                success = true,
                message = request.Message,
                response = response,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to Claude");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Have a conversation between Local AI and Claude
    /// Local AI คุยกับ Claude เพื่อขอความช่วยเหลือ
    /// </summary>
    [HttpPost("conversation")]
    public async Task<IActionResult> Conversation([FromBody] ClaudeConversationRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting conversation: {Prompt}", request.LocalAiPrompt);

            var result = await _claudeService.ConversationAsync(
                request.LocalAiPrompt,
                request.Context,
                ct);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    error = result.Error
                });
            }

            return Ok(new
            {
                success = true,
                localAiMessage = result.LocalAiMessage,
                claudeResponse = result.ClaudeResponse,
                localAiSummary = result.LocalAiSummary,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversation failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Write a message to the shared workspace
    /// </summary>
    [HttpPost("workspace/write")]
    public async Task<IActionResult> WriteToWorkspace([FromBody] WorkspaceWriteRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.WorkspacePath))
            {
                return BadRequest(new { error = "Workspace path not configured" });
            }

            await _claudeService.WriteToWorkspaceAsync(request.FileName, request.Content);

            return Ok(new
            {
                success = true,
                filePath = Path.Combine(_config.WorkspacePath, request.FileName),
                message = "Message written to workspace"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Read a message from the shared workspace
    /// </summary>
    [HttpGet("workspace/read/{fileName}")]
    public async Task<IActionResult> ReadFromWorkspace(string fileName)
    {
        try
        {
            var content = await _claudeService.ReadFromWorkspaceAsync(fileName);

            if (content == null)
            {
                return NotFound(new { error = "File not found" });
            }

            return Ok(new
            {
                success = true,
                fileName = fileName,
                content = content
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Start monitoring workspace for new files
    /// </summary>
    [HttpPost("workspace/monitor/start")]
    public async Task<IActionResult> StartWorkspaceMonitor(CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrEmpty(_config.WorkspacePath))
            {
                return BadRequest(new { error = "Workspace path not configured" });
            }

            _ = _claudeService.StartWorkspaceMonitorAsync(ct);

            return Ok(new
            {
                success = true,
                message = "Workspace monitor started",
                path = _config.WorkspacePath
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Ask Claude to help fix a posting error
    /// </summary>
    [HttpPost("help/fix-error")]
    public async Task<IActionResult> AskClaudeToFixError([FromBody] FixErrorRequest request, CancellationToken ct)
    {
        try
        {
            var prompt = $@"
ช่วยแก้ปัญหาการโพสต์บน Social Media:

Platform: {request.Platform}
Error: {request.ErrorMessage}
Task Type: {request.TaskType}
Current URL: {request.CurrentUrl ?? "Unknown"}

กรุณา:
1. วิเคราะห์สาเหตุของปัญหา
2. เสนอวิธีแก้ไข
3. ถ้าต้องเขียน JavaScript code ให้เขียนมาด้วย
";

            var result = await _claudeService.ConversationAsync(
                prompt,
                request.Context,
                ct);

            return Ok(new
            {
                success = result.Success,
                analysis = result.ClaudeResponse,
                summary = result.LocalAiSummary,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ask Claude for help");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

#region Request Models

public class ClaudeDesktopConfigUpdateRequest
{
    public bool? Enabled { get; set; }
    public int? McpServerPort { get; set; }
    public string? WorkspacePath { get; set; }
    public bool? AutoConnect { get; set; }
    public string? ApiKey { get; set; }
}

public class ClaudeMessageRequest
{
    public string Message { get; set; } = "";
    public string? SystemPrompt { get; set; }
}

public class ClaudeConversationRequest
{
    public string LocalAiPrompt { get; set; } = "";
    public string? Context { get; set; }
}

public class WorkspaceWriteRequest
{
    public string FileName { get; set; } = "";
    public string Content { get; set; } = "";
}

public class FixErrorRequest
{
    public string Platform { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string? TaskType { get; set; }
    public string? CurrentUrl { get; set; }
    public string? Context { get; set; }
}

#endregion
