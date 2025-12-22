using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Claude Code Integration Service - ใช้ Claude Code CLI (ที่ใช้ account ของ user)
/// เพื่อให้ Local AI ขอความช่วยเหลือจาก Claude ผ่านสิทธิ์ของ user
/// </summary>
public class ClaudeCodeIntegrationService
{
    private readonly ILogger<ClaudeCodeIntegrationService> _logger;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly string _workingDirectory;

    // Claude Code CLI path
    private readonly string _claudeCodePath;

    // Workspace path for file-based communication with Claude
    private readonly string _workspacePath;
    private int _requestCounter = 0;

    public ClaudeCodeIntegrationService(
        ContentGeneratorService contentGenerator,
        ILogger<ClaudeCodeIntegrationService>? logger = null,
        string? workingDirectory = null)
    {
        _contentGenerator = contentGenerator;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<ClaudeCodeIntegrationService>();
        _workingDirectory = workingDirectory ?? Directory.GetCurrentDirectory();

        // Find Claude Code CLI
        _claudeCodePath = FindClaudeCodePath();

        // Setup workspace path for file-based communication
        _workspacePath = Path.Combine(_workingDirectory, "..", "..", "workspace", "ai-messages");
        try
        {
            Directory.CreateDirectory(_workspacePath);
            _logger.LogInformation("Workspace path: {Path}", _workspacePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create workspace directory");
            _workspacePath = Path.Combine(Path.GetTempPath(), "ai-messages");
            Directory.CreateDirectory(_workspacePath);
        }
    }

    /// <summary>
    /// Find Claude Code CLI path
    /// </summary>
    private string FindClaudeCodePath()
    {
        // Check common locations
        var possiblePaths = new[]
        {
            "claude",  // In PATH
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claude", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Claude", "claude.exe"),
            "/usr/local/bin/claude",
            "/usr/bin/claude"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path) || CanRunCommand(path))
            {
                _logger.LogInformation("Found Claude Code at: {Path}", path);
                return path;
            }
        }

        return "claude"; // Assume in PATH
    }

    private bool CanRunCommand(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit(5000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ส่ง prompt ให้ Claude ผ่าน Claude Code CLI (ใช้สิทธิ์ของ user)
    /// </summary>
    public async Task<ClaudeCodeResponse> SendPromptAsync(
        string prompt,
        bool allowEdits = false,
        CancellationToken ct = default)
    {
        var result = new ClaudeCodeResponse();

        try
        {
            _logger.LogInformation("📤 Sending prompt to Claude Code: {Prompt}", prompt.Length > 100 ? prompt[..100] + "..." : prompt);

            var args = new StringBuilder();
            args.Append("--print "); // Print mode - just respond, don't modify files

            if (!allowEdits)
            {
                args.Append("--no-edit "); // Don't allow file edits
            }

            // Use -p for prompt
            args.Append($"-p \"{EscapeForCommandLine(prompt)}\"");

            var psi = new ProcessStartInfo
            {
                FileName = _claudeCodePath,
                Arguments = args.ToString(),
                WorkingDirectory = _workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait with timeout
            var completed = await Task.Run(() => process.WaitForExit(300000), ct); // 5 min timeout

            if (!completed)
            {
                process.Kill();
                result.Success = false;
                result.Error = "Timeout waiting for Claude Code response";
                return result;
            }

            result.Output = outputBuilder.ToString().Trim();
            result.Error = errorBuilder.ToString().Trim();
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0 && !string.IsNullOrEmpty(result.Output);

            _logger.LogInformation("📥 Claude Code response received: {Length} chars", result.Output?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute Claude Code");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// ให้ Local AI สร้าง prompt แล้วส่งให้ Claude (ผ่านสิทธิ์ user)
    /// </summary>
    public async Task<LocalAiClaudeConversation> LocalAiAskClaudeAsync(
        string task,
        string? context = null,
        CancellationToken ct = default)
    {
        var result = new LocalAiClaudeConversation { Task = task };

        try
        {
            // Step 1: Local AI creates a well-formed prompt for Claude
            _logger.LogInformation("🧠 Local AI preparing prompt for task: {Task}", task);

            var localAiPrompt = await GenerateLocalAiPromptAsync(task, context, ct);
            result.LocalAiPrompt = localAiPrompt;

            // Step 2: Send to Claude via Claude Code CLI (using user's account)
            _logger.LogInformation("📤 Sending to Claude (via your account)...");

            var claudeResponse = await SendPromptAsync(localAiPrompt, allowEdits: false, ct);
            result.ClaudeResponse = claudeResponse.Output;
            result.Success = claudeResponse.Success;

            if (!claudeResponse.Success)
            {
                result.Error = claudeResponse.Error;
                return result;
            }

            // Step 3: Local AI processes Claude's response
            _logger.LogInformation("🧠 Local AI processing Claude's response...");

            result.LocalAiSummary = await ProcessClaudeResponseAsync(claudeResponse.Output ?? "", task, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LocalAiAskClaude failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// ให้ Claude ช่วยแก้ error โดยใช้สิทธิ์ของ user
    /// </summary>
    public async Task<ClaudeCodeFixResult> AskClaudeToFixErrorAsync(
        string platform,
        string errorMessage,
        string? currentCode = null,
        string? pageHtml = null,
        CancellationToken ct = default)
    {
        var result = new ClaudeCodeFixResult { Platform = platform, OriginalError = errorMessage };

        try
        {
            // Build comprehensive prompt
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("ช่วยแก้ปัญหาการโพสต์บน Social Media:");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine($"Platform: {platform}");
            promptBuilder.AppendLine($"Error: {errorMessage}");
            promptBuilder.AppendLine();

            if (!string.IsNullOrEmpty(currentCode))
            {
                promptBuilder.AppendLine("Current Code:");
                promptBuilder.AppendLine("```javascript");
                promptBuilder.AppendLine(currentCode);
                promptBuilder.AppendLine("```");
                promptBuilder.AppendLine();
            }

            if (!string.IsNullOrEmpty(pageHtml) && pageHtml.Length < 5000)
            {
                promptBuilder.AppendLine("Page HTML (snippet):");
                promptBuilder.AppendLine("```html");
                promptBuilder.AppendLine(pageHtml);
                promptBuilder.AppendLine("```");
                promptBuilder.AppendLine();
            }

            promptBuilder.AppendLine("กรุณา:");
            promptBuilder.AppendLine("1. วิเคราะห์สาเหตุของปัญหา");
            promptBuilder.AppendLine("2. เขียน JavaScript code ที่แก้ไขแล้ว");
            promptBuilder.AppendLine("3. อธิบายวิธีการแก้ไข");

            var response = await SendPromptAsync(promptBuilder.ToString(), allowEdits: false, ct);

            result.Success = response.Success;
            result.Analysis = response.Output;

            // Extract code from response
            if (response.Success && !string.IsNullOrEmpty(response.Output))
            {
                result.FixedCode = ExtractCodeFromResponse(response.Output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskClaudeToFixError failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// ให้ Claude เขียน JavaScript code สำหรับ task ที่กำหนด
    /// </summary>
    public async Task<ClaudeCodeGenerationResult> AskClaudeToGenerateCodeAsync(
        string platform,
        string taskType,
        string? requirements = null,
        CancellationToken ct = default)
    {
        var result = new ClaudeCodeGenerationResult { Platform = platform, TaskType = taskType };

        try
        {
            var prompt = $@"
สร้าง JavaScript code สำหรับ {taskType} บน {platform}

Requirements:
{requirements ?? "- โพสต์ content ที่กำหนด\n- รองรับ hashtags\n- รองรับ images (ถ้ามี)"}

Code ต้อง:
1. เป็น async/await pattern
2. ใช้ placeholders: {{{{content.text}}}}, {{{{content.hashtags}}}}, {{{{content.images}}}}
3. มี error handling
4. Return {{ success: boolean, postId?: string, error?: string }}

ส่ง JavaScript code เท่านั้น ไม่ต้องอธิบาย
";

            var response = await SendPromptAsync(prompt, allowEdits: false, ct);

            result.Success = response.Success;
            result.GeneratedCode = response.Success ? ExtractCodeFromResponse(response.Output ?? "") : null;
            result.Error = response.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AskClaudeToGenerateCode failed");
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    #region Workspace-Based Communication

    /// <summary>
    /// เขียนคำขอไปยัง workspace สำหรับ Claude
    /// Claude (ที่กำลังรันใน IDE) จะอ่านและตอบกลับ
    /// </summary>
    public async Task<WorkspaceRequest> WriteRequestToWorkspaceAsync(
        string requestType,
        string content,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default)
    {
        var requestId = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Interlocked.Increment(ref _requestCounter):D3}";
        var request = new WorkspaceRequest
        {
            Id = requestId,
            Timestamp = DateTime.UtcNow,
            From = "local_ai",
            Type = requestType,
            Content = content,
            Context = context ?? new Dictionary<string, object>()
        };

        var requestPath = Path.Combine(_workspacePath, $"request_{requestId}.json");

        try
        {
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(requestPath, json, ct);

            _logger.LogInformation("📝 Request written to workspace: {Path}", requestPath);
            request.FilePath = requestPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write request to workspace");
            request.Error = ex.Message;
        }

        return request;
    }

    /// <summary>
    /// รอ response จาก Claude ใน workspace
    /// </summary>
    public async Task<WorkspaceResponse?> WaitForResponseAsync(
        string requestId,
        int timeoutSeconds = 300,
        CancellationToken ct = default)
    {
        var responsePath = Path.Combine(_workspacePath, $"response_{requestId}.json");
        var startTime = DateTime.UtcNow;

        _logger.LogInformation("⏳ Waiting for Claude response: {Path}", responsePath);

        while ((DateTime.UtcNow - startTime).TotalSeconds < timeoutSeconds && !ct.IsCancellationRequested)
        {
            if (File.Exists(responsePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(responsePath, ct);
                    var response = JsonSerializer.Deserialize<WorkspaceResponse>(json);

                    if (response != null)
                    {
                        _logger.LogInformation("📥 Response received from Claude: {Status}", response.Status);
                        return response;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reading response file, retrying...");
                }
            }

            await Task.Delay(1000, ct); // Check every second
        }

        _logger.LogWarning("⏰ Timeout waiting for Claude response");
        return null;
    }

    /// <summary>
    /// ส่งคำขอและรอ response (สำหรับใช้งานแบบ synchronous)
    /// </summary>
    public async Task<WorkspaceResponse?> SendAndWaitAsync(
        string requestType,
        string content,
        Dictionary<string, object>? context = null,
        int timeoutSeconds = 300,
        CancellationToken ct = default)
    {
        var request = await WriteRequestToWorkspaceAsync(requestType, content, context, ct);

        if (!string.IsNullOrEmpty(request.Error))
        {
            return new WorkspaceResponse
            {
                Id = request.Id,
                Status = "error",
                Content = request.Error
            };
        }

        return await WaitForResponseAsync(request.Id, timeoutSeconds, ct);
    }

    /// <summary>
    /// Get workspace path for external access
    /// </summary>
    public string GetWorkspacePath() => _workspacePath;

    /// <summary>
    /// List pending requests in workspace
    /// </summary>
    public async Task<List<WorkspaceRequest>> GetPendingRequestsAsync(CancellationToken ct = default)
    {
        var requests = new List<WorkspaceRequest>();

        try
        {
            var requestFiles = Directory.GetFiles(_workspacePath, "request_*.json");

            foreach (var file in requestFiles)
            {
                var requestId = Path.GetFileNameWithoutExtension(file).Replace("request_", "");
                var responsePath = Path.Combine(_workspacePath, $"response_{requestId}.json");

                // Only include requests without responses
                if (!File.Exists(responsePath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(file, ct);
                        var request = JsonSerializer.Deserialize<WorkspaceRequest>(json);
                        if (request != null)
                        {
                            request.FilePath = file;
                            requests.Add(request);
                        }
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pending requests");
        }

        return requests;
    }

    #endregion

    #region Helper Methods

    private async Task<string> GenerateLocalAiPromptAsync(string task, string? context, CancellationToken ct)
    {
        try
        {
            var result = await _contentGenerator.GenerateAsync(
                $"สร้าง prompt ที่ชัดเจนสำหรับ AI เพื่อทำงาน: {task}\n\nContext: {context ?? "None"}",
                null,
                "general",
                "th",
                ct);

            return result?.Text ?? task;
        }
        catch
        {
            // Fallback to original task
            return task;
        }
    }

    private async Task<string?> ProcessClaudeResponseAsync(string response, string originalTask, CancellationToken ct)
    {
        try
        {
            var result = await _contentGenerator.GenerateAsync(
                $"สรุปคำตอบนี้และวิธีนำไปใช้:\n\nคำตอบ:\n{response}\n\nTask เดิม: {originalTask}",
                null,
                "general",
                "th",
                ct);

            return result?.Text;
        }
        catch
        {
            return response;
        }
    }

    private string? ExtractCodeFromResponse(string response)
    {
        // Try to find code block
        var codeBlockStart = response.IndexOf("```javascript");
        if (codeBlockStart < 0)
            codeBlockStart = response.IndexOf("```js");
        if (codeBlockStart < 0)
            codeBlockStart = response.IndexOf("```");

        if (codeBlockStart >= 0)
        {
            var codeStart = response.IndexOf('\n', codeBlockStart) + 1;
            var codeEnd = response.IndexOf("```", codeStart);
            if (codeEnd > codeStart)
            {
                return response.Substring(codeStart, codeEnd - codeStart).Trim();
            }
        }

        // Try to find async IIFE
        var asyncStart = response.IndexOf("(async");
        if (asyncStart >= 0)
        {
            var remaining = response.Substring(asyncStart);
            var endIndex = remaining.LastIndexOf("})();");
            if (endIndex > 0)
            {
                return remaining.Substring(0, endIndex + 5);
            }
        }

        return null;
    }

    private string EscapeForCommandLine(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");
    }

    #endregion
}

#region Models

public class ClaudeCodeResponse
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int ExitCode { get; set; }
}

public class LocalAiClaudeConversation
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Task { get; set; } = "";
    public string? LocalAiPrompt { get; set; }
    public string? ClaudeResponse { get; set; }
    public string? LocalAiSummary { get; set; }
}

public class ClaudeCodeFixResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Platform { get; set; } = "";
    public string OriginalError { get; set; } = "";
    public string? Analysis { get; set; }
    public string? FixedCode { get; set; }
}

public class ClaudeCodeGenerationResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Platform { get; set; } = "";
    public string TaskType { get; set; } = "";
    public string? GeneratedCode { get; set; }
}

public class WorkspaceRequest
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string From { get; set; } = "local_ai";
    public string Type { get; set; } = "";
    public string Content { get; set; } = "";
    public Dictionary<string, object> Context { get; set; } = new();
    public string? FilePath { get; set; }
    public string? Error { get; set; }
}

public class WorkspaceResponse
{
    public string Id { get; set; } = "";
    public DateTime Timestamp { get; set; }
    public string From { get; set; } = "claude";
    public string Status { get; set; } = "";
    public string? Content { get; set; }
    public string? Analysis { get; set; }
    public string? Code { get; set; }
    public List<string>? Tips { get; set; }
}

#endregion
