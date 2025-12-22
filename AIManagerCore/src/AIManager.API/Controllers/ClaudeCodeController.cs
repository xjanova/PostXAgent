using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;

namespace AIManager.API.Controllers;

/// <summary>
/// Claude Code Integration Controller
/// ใช้ Claude Code CLI (สิทธิ์ของ user) ในการขอความช่วยเหลือจาก Claude
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ClaudeCodeController : ControllerBase
{
    private readonly ILogger<ClaudeCodeController> _logger;
    private readonly ClaudeCodeIntegrationService _claudeCode;

    public ClaudeCodeController(
        ILogger<ClaudeCodeController> logger,
        ClaudeCodeIntegrationService claudeCode)
    {
        _logger = logger;
        _claudeCode = claudeCode;
    }

    /// <summary>
    /// ส่ง prompt ตรงๆ ให้ Claude (ใช้สิทธิ์ของ user)
    /// </summary>
    [HttpPost("prompt")]
    public async Task<IActionResult> SendPrompt([FromBody] ClaudeCodePromptRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Sending prompt to Claude Code: {Length} chars", request.Prompt.Length);

            var response = await _claudeCode.SendPromptAsync(
                request.Prompt,
                request.AllowEdits,
                ct);

            return Ok(new
            {
                success = response.Success,
                output = response.Output,
                error = response.Error,
                exitCode = response.ExitCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send prompt");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Local AI สร้าง prompt แล้วส่งให้ Claude ช่วย
    /// </summary>
    [HttpPost("ask")]
    public async Task<IActionResult> LocalAiAskClaude([FromBody] LocalAiAskRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Local AI asking Claude: {Task}", request.Task);

            var result = await _claudeCode.LocalAiAskClaudeAsync(
                request.Task,
                request.Context,
                ct);

            return Ok(new
            {
                success = result.Success,
                task = result.Task,
                localAiPrompt = result.LocalAiPrompt,
                claudeResponse = result.ClaudeResponse,
                localAiSummary = result.LocalAiSummary,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LocalAiAskClaude failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ขอ Claude ช่วยแก้ error ที่เกิดขึ้น
    /// </summary>
    [HttpPost("fix-error")]
    public async Task<IActionResult> FixError([FromBody] ClaudeCodeFixErrorRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Asking Claude to fix error on {Platform}: {Error}",
                request.Platform, request.ErrorMessage);

            var result = await _claudeCode.AskClaudeToFixErrorAsync(
                request.Platform,
                request.ErrorMessage,
                request.CurrentCode,
                request.PageHtml,
                ct);

            return Ok(new
            {
                success = result.Success,
                platform = result.Platform,
                originalError = result.OriginalError,
                analysis = result.Analysis,
                fixedCode = result.FixedCode,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FixError failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// ขอ Claude สร้าง JavaScript code สำหรับ task
    /// </summary>
    [HttpPost("generate-code")]
    public async Task<IActionResult> GenerateCode([FromBody] ClaudeCodeGenerateRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Asking Claude to generate code for {Platform}/{TaskType}",
                request.Platform, request.TaskType);

            var result = await _claudeCode.AskClaudeToGenerateCodeAsync(
                request.Platform,
                request.TaskType,
                request.Requirements,
                ct);

            return Ok(new
            {
                success = result.Success,
                platform = result.Platform,
                taskType = result.TaskType,
                generatedCode = result.GeneratedCode,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateCode failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get workspace path for Claude communication
    /// </summary>
    [HttpGet("workspace")]
    public IActionResult GetWorkspacePath()
    {
        return Ok(new
        {
            path = _claudeCode.GetWorkspacePath(),
            message = "Local AI can write requests to this folder. Claude will read and respond."
        });
    }

    /// <summary>
    /// เขียน request ไปยัง workspace สำหรับ Claude
    /// </summary>
    [HttpPost("workspace/request")]
    public async Task<IActionResult> WriteWorkspaceRequest([FromBody] WorkspaceRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _claudeCode.WriteRequestToWorkspaceAsync(
                request.Type,
                request.Content,
                request.Context,
                ct);

            return Ok(new
            {
                success = string.IsNullOrEmpty(result.Error),
                requestId = result.Id,
                filePath = result.FilePath,
                error = result.Error,
                message = "Request written. Waiting for Claude to respond..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WriteWorkspaceRequest failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// รอ response จาก Claude
    /// </summary>
    [HttpGet("workspace/response/{requestId}")]
    public async Task<IActionResult> WaitForResponse(string requestId, [FromQuery] int timeoutSeconds = 60, CancellationToken ct = default)
    {
        try
        {
            var response = await _claudeCode.WaitForResponseAsync(requestId, timeoutSeconds, ct);

            if (response == null)
            {
                return Ok(new
                {
                    success = false,
                    pending = true,
                    message = "Still waiting for Claude response..."
                });
            }

            return Ok(new
            {
                success = response.Status == "success",
                response = response
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WaitForResponse failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// List pending requests
    /// </summary>
    [HttpGet("workspace/pending")]
    public async Task<IActionResult> GetPendingRequests(CancellationToken ct)
    {
        try
        {
            var requests = await _claudeCode.GetPendingRequestsAsync(ct);
            return Ok(new
            {
                count = requests.Count,
                requests = requests
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPendingRequests failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

#region Request Models

public class ClaudeCodePromptRequest
{
    public string Prompt { get; set; } = "";
    public bool AllowEdits { get; set; } = false;
}

public class LocalAiAskRequest
{
    public string Task { get; set; } = "";
    public string? Context { get; set; }
}

public class ClaudeCodeFixErrorRequest
{
    public string Platform { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string? CurrentCode { get; set; }
    public string? PageHtml { get; set; }
}

public class ClaudeCodeGenerateRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string? Requirements { get; set; }
}

public class WorkspaceRequestDto
{
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public Dictionary<string, object>? Context { get; set; }
}

#endregion
