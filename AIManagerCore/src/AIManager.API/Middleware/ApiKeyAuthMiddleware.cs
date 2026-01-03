using System.Diagnostics;
using AIManager.Core.Services;

namespace AIManager.API.Middleware;

/// <summary>
/// API Key Authentication Middleware
/// ตรวจสอบ API Key ในทุก request ที่เข้ามา
/// พร้อม logging การใช้งานและแจ้งเตือน
///
/// Production Security:
/// - Test/Setup endpoints are protected in production
/// - Only health endpoints are excluded
/// - API keys in query parameters are logged as warnings
/// </summary>
public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyService _apiKeyService;
    private readonly CoreDatabaseService _coreDb;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;
    private readonly string[] _excludedPaths;
    private readonly bool _isProduction;

    public ApiKeyAuthMiddleware(
        RequestDelegate next,
        ApiKeyService apiKeyService,
        CoreDatabaseService coreDb,
        ILogger<ApiKeyAuthMiddleware> logger,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _next = next;
        _apiKeyService = apiKeyService;
        _coreDb = coreDb;
        _logger = logger;
        _isProduction = environment.IsProduction();

        // Paths that don't require authentication
        // Production: Only essential paths are excluded
        // Development: Additional test/setup paths are excluded
        var basePaths = new[]
        {
            "/swagger",
            "/health",
            "/api/status/health",
            "/hub/aimanager/negotiate"
        };

        var devOnlyPaths = new[]
        {
            "/api/apikeys/setup",
            "/api/apitest",     // API Test page - DEVELOPMENT ONLY
            "/api/setupwizard"  // Setup wizard - DEVELOPMENT ONLY
        };

        // Read from configuration first, then use defaults
        var configuredPaths = configuration.GetSection("ApiKey:ExcludedPaths").Get<string[]>();

        if (configuredPaths != null && configuredPaths.Length > 0)
        {
            _excludedPaths = configuredPaths;
        }
        else if (environment.IsDevelopment())
        {
            // Development: include test/setup paths
            _excludedPaths = basePaths.Concat(devOnlyPaths).ToArray();
            _logger.LogWarning("Running in DEVELOPMENT mode - test/setup endpoints are NOT protected");
        }
        else
        {
            // Production: only base paths excluded
            _excludedPaths = basePaths;
            _logger.LogInformation("Running in PRODUCTION mode - all sensitive endpoints are protected");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower() ?? "";
        var stopwatch = Stopwatch.StartNew();

        // Check if path is excluded
        if (_excludedPaths.Any(p => path.StartsWith(p.ToLower())))
        {
            await _next(context);
            return;
        }

        // Get API key from header or query
        var (apiKey, source) = GetApiKeyWithSource(context);
        var clientIp = GetClientIp(context);
        var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
        var method = context.Request.Method;
        var endpoint = context.Request.Path.Value ?? "/";

        // Warn about query parameter usage (security risk - logged in URLs)
        if (source == "query" && _isProduction)
        {
            _logger.LogWarning(
                "API key passed in query parameter from {IP} - this is insecure and may be logged. Use X-API-Key header instead.",
                clientIp);
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("API request without key from {IP}", clientIp);
            await WriteUnauthorizedResponse(context, "API key is required. Use header 'X-API-Key' or 'Authorization: Bearer <key>'");

            // Log failed attempt
            stopwatch.Stop();
            _ = _coreDb.LogApiKeyUsageAsync("none", "No Key", clientIp, endpoint, method, 401, stopwatch.ElapsedMilliseconds, userAgent);
            return;
        }

        // Get required scope based on path
        var requiredScope = GetRequiredScope(context);

        // Validate key
        var result = _apiKeyService.ValidateKey(apiKey, clientIp, requiredScope);

        if (!result.IsValid)
        {
            _logger.LogWarning("Invalid API key attempt from {IP}: {Error}",
                clientIp, result.ErrorMessage);
            await WriteUnauthorizedResponse(context, result.ErrorMessage ?? "Invalid API key");

            // Log failed attempt
            stopwatch.Stop();
            _ = _coreDb.LogApiKeyUsageAsync(
                apiKey[..Math.Min(8, apiKey.Length)],
                "Invalid Key",
                clientIp, endpoint, method, 401, stopwatch.ElapsedMilliseconds, userAgent);
            return;
        }

        // Add key info to context for use in controllers
        context.Items["ApiKeyInfo"] = result.KeyInfo;
        context.Items["ApiKeyId"] = result.KeyInfo?.Id;
        context.Items["ApiKeyName"] = result.KeyInfo?.Name;
        context.Items["RequestStartTime"] = stopwatch;

        _logger.LogDebug("API request authenticated: {KeyName} ({KeyId})",
            result.KeyInfo?.Name, result.KeyInfo?.Id);

        // Track connection
        _coreDb.RecordConnection();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _coreDb.RecordDisconnection();

            // Log successful request
            _ = _coreDb.LogApiKeyUsageAsync(
                result.KeyInfo?.Id ?? "unknown",
                result.KeyInfo?.Name ?? "unknown",
                clientIp, endpoint, method,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                userAgent);
        }
    }

    private (string? key, string source) GetApiKeyWithSource(HttpContext context)
    {
        // Try header first (preferred, secure method)
        if (context.Request.Headers.TryGetValue("X-API-Key", out var headerKey))
        {
            return (headerKey.FirstOrDefault(), "header");
        }

        // Try Authorization header with Bearer scheme (secure)
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var auth = authHeader.FirstOrDefault();
            if (auth?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return (auth["Bearer ".Length..], "bearer");
            }
            if (auth?.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase) == true)
            {
                return (auth["ApiKey ".Length..], "apikey-header");
            }
        }

        // Try query parameter (insecure - logged in URLs, browser history)
        // Supported for backwards compatibility but warned in production
        if (context.Request.Query.TryGetValue("api_key", out var queryKey))
        {
            return (queryKey.FirstOrDefault(), "query");
        }

        return (null, "none");
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
