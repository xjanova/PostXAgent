using AIManager.Core.Services;

namespace AIManager.API.Middleware;

/// <summary>
/// API Key Authentication Middleware
/// ตรวจสอบ API Key ในทุก request ที่เข้ามา
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly string[] _excludedPaths;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        ApiKeyService apiKeyService,
        ILogger<ApiKeyAuthMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _apiKeyService = apiKeyService;
        _logger = logger;

        // Paths that don't require authentication
        _excludedPaths = configuration.GetSection("ApiKey:ExcludedPaths")
            .Get<string[]>() ?? new[]
            {
                "/swagger",
                "/health",
                "/api/status/health",
                "/hub/aimanager/negotiate"
            };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";

        // Check if path is excluded
        if (_excludedPaths.Any(p => path.StartsWith(p.ToLower())))
        {
            await _next(context);
            return;
        }

        // Get API key from header or query
        var apiKey = GetApiKey(context);

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("API request without key from {IP}", GetClientIp(context));
            await WriteUnauthorizedResponse(context, "API key is required. Use header 'X-API-Key' or query parameter 'api_key'");
            return;
        }

        // Get required scope based on path
        var requiredScope = GetRequiredScope(context);

        // Validate key
        var clientIp = GetClientIp(context);
        var result = _apiKeyService.ValidateKey(apiKey, clientIp, requiredScope);

        if (!result.IsValid)
        {
            _logger.LogWarning("Invalid API key attempt from {IP}: {Error}",
                clientIp, result.ErrorMessage);
            await WriteUnauthorizedResponse(context, result.ErrorMessage ?? "Invalid API key");
            return;
        }

        // Add key info to context for use in controllers
        context.Items["ApiKeyInfo"] = result.KeyInfo;
        context.Items["ApiKeyId"] = result.KeyInfo?.Id;
        context.Items["ApiKeyName"] = result.KeyInfo?.Name;

        _logger.LogDebug("API request authenticated: {KeyName} ({KeyId})",
            result.KeyInfo?.Name, result.KeyInfo?.Id);

        await _next(context);
    }

    private string? GetApiKey(HttpContext context)
    {
        // Try header first
        if (context.Request.Headers.TryGetValue("X-API-Key", out var headerKey))
        {
            return headerKey.FirstOrDefault();
        }

        // Try Authorization header with Bearer scheme
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var auth = authHeader.FirstOrDefault();
            if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return auth["Bearer ".Length..];
            }
            if (auth?.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return auth["ApiKey ".Length..];
            }
        }

        // Try query parameter
        if (context.Request.Query.TryGetValue("api_key", out var queryKey))
        {
            return queryKey.FirstOrDefault();
        }

        return null;
    }

    private string GetClientIp(HttpContext context)
    {
        // Check for proxy headers
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string? GetRequiredScope(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var method = context.Request.Method.ToUpper();

        // Admin endpoints require admin scope
        if (path.Contains("/admin") || path.Contains("/api-keys"))
        {
            return ApiScopes.Admin;
        }

        // Task endpoints
        if (path.Contains("/tasks"))
        {
            return ApiScopes.Tasks;
        }

        // Worker endpoints
        if (path.Contains("/workers"))
        {
            return ApiScopes.Workers;
        }

        // Content generation
        if (path.Contains("/content") || path.Contains("/generate"))
        {
            return ApiScopes.Content;
        }

        // Image generation
        if (path.Contains("/image"))
        {
            return ApiScopes.Images;
        }

        // Web automation
        if (path.Contains("/automation") || path.Contains("/workflow"))
        {
            return ApiScopes.Automation;
        }

        // Read-only for GET requests on analytics
        if (path.Contains("/analytics") || path.Contains("/stats"))
        {
            return method == "GET" ? ApiScopes.ReadOnly : ApiScopes.Analytics;
        }

        return null; // No specific scope required
    }

    private async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/json";

        var response = new
        {
            success = false,
            error = message,
            code = "UNAUTHORIZED"
        };

        await context.Response.WriteAsJsonAsync(response);
    }
}

/// <summary>
/// Extension methods for API Key middleware
/// </summary>
public static class ApiKeyAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyAuth(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthMiddleware>();
    }
}
