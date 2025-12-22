using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.Core.WebAutomation;

namespace AIManager.API.Controllers;

/// <summary>
/// AI Code Generation Controller - API สำหรับจัดการการสร้างและรัน JavaScript Code
/// ที่ AI generate มาเพื่อใช้โพสบน Social Media
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AICodeController : ControllerBase
{
    private readonly ILogger<AICodeController> _logger;
    private readonly AICodeGeneratorService _codeGenerator;
    private readonly DynamicCodeExecutor? _codeExecutor;

    public AICodeController(
        ILogger<AICodeController> logger,
        AICodeGeneratorService codeGenerator,
        DynamicCodeExecutor? codeExecutor = null)
    {
        _logger = logger;
        _codeGenerator = codeGenerator;
        _codeExecutor = codeExecutor;
    }

    /// <summary>
    /// Generate JavaScript code for posting
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateCode([FromBody] GenerateCodeRequest request, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Generating code for {Platform}/{TaskType}", request.Platform, request.TaskType);

            if (!Enum.TryParse<SocialPlatform>(request.Platform, true, out var platform))
            {
                return BadRequest(new { error = "Invalid platform" });
            }

            var result = await _codeGenerator.GeneratePostingCodeAsync(
                platform,
                request.TaskType,
                request.ErrorMessage,
                request.CurrentPageHtml ?? "",
                request.CurrentPageUrl,
                null, // workflow
                ct);

            if (result.Success)
            {
                return Ok(new
                {
                    success = true,
                    code = result.GeneratedCode,
                    codeType = result.CodeType,
                    estimatedSteps = result.EstimatedSteps,
                    analysisPrompt = result.AnalysisPrompt,
                    generatedAt = result.CompletedAt
                });
            }

            return BadRequest(new
            {
                success = false,
                error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code generation failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Execute AI-generated code in WebView
    /// </summary>
    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteCode([FromBody] ExecuteCodeRequest request, CancellationToken ct)
    {
        try
        {
            if (_codeExecutor == null)
            {
                return BadRequest(new { error = "Code executor not available. WebView not initialized." });
            }

            _logger.LogInformation("Executing code for {Url}", request.PageUrl);

            var content = new AIManager.Core.WebAutomation.PostContent
            {
                Text = request.Content?.Text,
                Hashtags = request.Content?.Hashtags,
                Link = request.Content?.Link,
                Images = request.Content?.Images,
                Location = request.Content?.Location
            };

            var result = request.DryRun
                ? await _codeExecutor.TestExecuteAsync(request.Code, content, request.PageUrl, ct)
                : await _codeExecutor.ExecuteAsync(request.Code, content, request.PageUrl, ct);

            return Ok(new
            {
                success = result.Success,
                error = result.Error,
                postId = result.PostId,
                postUrl = result.PostUrl,
                isDryRun = result.IsDryRun,
                duration = result.Duration.TotalMilliseconds,
                steps = result.Steps?.Select(s => new
                {
                    name = s.StepName,
                    status = s.Status,
                    error = s.Error
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code execution failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate and execute code in one call
    /// </summary>
    [HttpPost("generate-and-execute")]
    public async Task<IActionResult> GenerateAndExecute([FromBody] GenerateAndExecuteRequest request, CancellationToken ct)
    {
        try
        {
            if (_codeExecutor == null)
            {
                return BadRequest(new { error = "Code executor not available" });
            }

            if (!Enum.TryParse<SocialPlatform>(request.Platform, true, out var platform))
            {
                return BadRequest(new { error = "Invalid platform" });
            }

            _logger.LogInformation("Generate & Execute for {Platform}/{TaskType}", request.Platform, request.TaskType);

            // Step 1: Generate code
            var genResult = await _codeGenerator.GeneratePostingCodeAsync(
                platform,
                request.TaskType,
                request.ErrorMessage ?? "Initial posting attempt",
                request.CurrentPageHtml ?? "",
                request.CurrentPageUrl,
                null,
                ct);

            if (!genResult.Success || string.IsNullOrEmpty(genResult.GeneratedCode))
            {
                return BadRequest(new
                {
                    success = false,
                    stage = "generation",
                    error = genResult.Error
                });
            }

            // Step 2: Execute code
            var content = new AIManager.Core.WebAutomation.PostContent
            {
                Text = request.Content?.Text,
                Hashtags = request.Content?.Hashtags,
                Link = request.Content?.Link,
                Images = request.Content?.Images
            };

            var platformUrl = GetPlatformUrl(platform);
            var execResult = request.DryRun
                ? await _codeExecutor.TestExecuteAsync(genResult.GeneratedCode, content, platformUrl, ct)
                : await _codeExecutor.ExecuteAsync(genResult.GeneratedCode, content, platformUrl, ct);

            return Ok(new
            {
                success = execResult.Success,
                stage = "execution",
                generatedCode = genResult.GeneratedCode,
                codeType = genResult.CodeType,
                postId = execResult.PostId,
                postUrl = execResult.PostUrl,
                isDryRun = execResult.IsDryRun,
                error = execResult.Error,
                duration = execResult.Duration.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generate & Execute failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get execution history
    /// </summary>
    [HttpGet("history")]
    public IActionResult GetHistory([FromQuery] int limit = 20)
    {
        try
        {
            if (_codeExecutor == null)
            {
                return Ok(new { history = Array.Empty<object>(), successRate = 0.0 });
            }

            var history = _codeExecutor.GetExecutionHistory(limit);
            var successRate = _codeExecutor.GetSuccessRate(limit);

            return Ok(new
            {
                history = history.Select(h => new
                {
                    executedAt = h.ExecutedAt,
                    success = h.Result.Success,
                    error = h.Result.Error,
                    postId = h.Result.PostId,
                    duration = h.Result.Duration.TotalMilliseconds
                }),
                successRate,
                count = history.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get history");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Check if should escalate to human training
    /// </summary>
    [HttpGet("should-escalate")]
    public IActionResult ShouldEscalate([FromQuery] string platform, [FromQuery] string taskType)
    {
        try
        {
            if (!Enum.TryParse<SocialPlatform>(platform, true, out var platformEnum))
            {
                return BadRequest(new { error = "Invalid platform" });
            }

            var shouldEscalate = _codeGenerator.ShouldEscalateToHuman(platformEnum, taskType);

            return Ok(new
            {
                shouldEscalate,
                platform,
                taskType,
                reason = shouldEscalate ? "Too many consecutive failures" : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Reset failure count after successful manual fix
    /// </summary>
    [HttpPost("reset-failures")]
    public IActionResult ResetFailures([FromBody] ResetFailuresRequest request)
    {
        try
        {
            if (!Enum.TryParse<SocialPlatform>(request.Platform, true, out var platform))
            {
                return BadRequest(new { error = "Invalid platform" });
            }

            _codeGenerator.ResetFailureCount(platform, request.TaskType);

            return Ok(new
            {
                success = true,
                message = $"Failure count reset for {request.Platform}/{request.TaskType}"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Validate code without executing
    /// </summary>
    [HttpPost("validate")]
    public IActionResult ValidateCode([FromBody] ValidateCodeRequest request)
    {
        try
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var code = request.Code;

            // Check structure
            if (!code.Contains("async"))
                errors.Add("Code must use async/await pattern");

            if (!code.Contains("return"))
                warnings.Add("Code should return a result object");

            // Check for dangerous patterns
            var dangerous = new[] { "eval(", "Function(", "document.cookie" };
            foreach (var pattern in dangerous)
            {
                if (code.Contains(pattern))
                    errors.Add($"Dangerous pattern detected: {pattern}");
            }

            // Check for placeholders
            var placeholders = new[] { "{{content.text}}", "{{content.hashtags}}" };
            foreach (var ph in placeholders)
            {
                if (!code.Contains(ph))
                    warnings.Add($"Missing placeholder: {ph}");
            }

            return Ok(new
            {
                valid = errors.Count == 0,
                errors,
                warnings,
                suggestions = errors.Count == 0 ? new[] { "Code looks valid!" } : null
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string GetPlatformUrl(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => "https://www.facebook.com/",
            SocialPlatform.Instagram => "https://www.instagram.com/",
            SocialPlatform.Twitter => "https://twitter.com/compose/tweet",
            SocialPlatform.TikTok => "https://www.tiktok.com/upload",
            SocialPlatform.LinkedIn => "https://www.linkedin.com/feed/",
            SocialPlatform.YouTube => "https://studio.youtube.com/",
            _ => "https://www.google.com/"
        };
    }
}

#region Request Models

public class GenerateCodeRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "post_content";
    public string ErrorMessage { get; set; } = "";
    public string? CurrentPageHtml { get; set; }
    public string? CurrentPageUrl { get; set; }
}

public class ExecuteCodeRequest
{
    public string Code { get; set; } = "";
    public string PageUrl { get; set; } = "";
    public AICodePostContentRequest? Content { get; set; }
    public bool DryRun { get; set; }
}

public class GenerateAndExecuteRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "post_content";
    public string? ErrorMessage { get; set; }
    public string? CurrentPageHtml { get; set; }
    public string? CurrentPageUrl { get; set; }
    public AICodePostContentRequest? Content { get; set; }
    public bool DryRun { get; set; }
}

public class AICodePostContentRequest
{
    public string? Text { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? Link { get; set; }
    public List<string>? Images { get; set; }
    public string? Title { get; set; }
    public string? Location { get; set; }
}

public class ResetFailuresRequest
{
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
}

public class ValidateCodeRequest
{
    public string Code { get; set; } = "";
}

#endregion
