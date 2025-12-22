namespace AIManager.Core.Models;

/// <summary>
/// Configuration for AI Code Generation system
/// </summary>
public class AICodeGenerationConfig
{
    /// <summary>
    /// Maximum attempts for code generation before giving up
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Maximum consecutive failures before escalating to human training
    /// </summary>
    public int MaxConsecutiveFailuresBeforeEscalation { get; set; } = 3;

    /// <summary>
    /// Base URL for Ollama (Local AI)
    /// </summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use with Ollama
    /// </summary>
    public string OllamaModel { get; set; } = "llama3.2";

    /// <summary>
    /// External AI provider: openai, anthropic, google
    /// </summary>
    public string ExternalAIProvider { get; set; } = "openai";

    /// <summary>
    /// OpenAI model to use
    /// </summary>
    public string OpenAIModel { get; set; } = "gpt-4-turbo-preview";

    /// <summary>
    /// Anthropic model to use
    /// </summary>
    public string AnthropicModel { get; set; } = "claude-3-opus-20240229";

    /// <summary>
    /// Google AI model to use
    /// </summary>
    public string GoogleModel { get; set; } = "gemini-pro";

    /// <summary>
    /// Timeout for code generation in seconds
    /// </summary>
    public int CodeGenerationTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Enable code validation before execution
    /// </summary>
    public bool EnableCodeValidation { get; set; } = true;

    /// <summary>
    /// Required patterns in generated code
    /// </summary>
    public List<string> AllowedCodePatterns { get; set; } = new() { "async", "await", "return" };

    /// <summary>
    /// Blocked patterns in generated code (security)
    /// </summary>
    public List<string> BlockedCodePatterns { get; set; } = new() { "eval(", "Function(", "document.cookie" };
}

/// <summary>
/// Configuration for Self-Healing Worker system
/// </summary>
public class SelfHealingConfig
{
    /// <summary>
    /// Maximum retry attempts for self-healing
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts in milliseconds
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Enable AI code generation for healing
    /// </summary>
    public bool EnableAICodeGeneration { get; set; } = true;

    /// <summary>
    /// Enable human training fallback
    /// </summary>
    public bool EnableHumanTraining { get; set; } = true;

    /// <summary>
    /// Max AI code generation attempts before escalating to human
    /// </summary>
    public int MaxAICodeAttemptsBeforeHuman { get; set; } = 3;
}

/// <summary>
/// Configuration for Claude Desktop integration
/// </summary>
public class ClaudeDesktopConfig
{
    /// <summary>
    /// Enable Claude Desktop integration
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// MCP Server port for Claude Desktop communication
    /// </summary>
    public int McpServerPort { get; set; } = 5010;

    /// <summary>
    /// Shared workspace path for file-based communication
    /// </summary>
    public string WorkspacePath { get; set; } = "";

    /// <summary>
    /// Auto-connect to Claude Desktop on startup
    /// </summary>
    public bool AutoConnect { get; set; } = false;

    /// <summary>
    /// API endpoint for Claude API (alternative to MCP)
    /// </summary>
    public string? ApiEndpoint { get; set; }

    /// <summary>
    /// API key for Claude API
    /// </summary>
    public string? ApiKey { get; set; }
}
