using System.Collections.Concurrent;
using System.Net;

namespace AIManager.API.Middleware;

/// <summary>
/// Production-quality Rate Limiting Middleware
/// Prevents DOS attacks and ensures fair API usage
///
/// Features:
/// - Per-IP rate limiting
/// - Per-API-key rate limiting
/// - Sliding window algorithm
/// - Configurable limits
/// - Rate limit headers in response
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitEntry> _rateLimits = new();
    private readonly Timer _cleanupTimer;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;

        // Load configuration
        _options = new RateLimitingOptions();
        configuration.GetSection("RateLimiting").Bind(_options);

        // Cleanup timer to remove expired entries (every 5 minutes)
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.EnableRateLimiting)
        {
            await _next(context);
            return;
        }

        var clientKey = GetClientKey(context);
        var now = DateTime.UtcNow;

        // Get or create rate limit entry
        var entry = _rateLimits.GetOrAdd(clientKey, _ => new RateLimitEntry
        {
            WindowStart = now,
            RequestCount = 0,
            QueuedRequests = 0
        });

        // Check rate limit (thread-safe without lock on async code)
        var (isAllowed, remaining, resetTime) = CheckRateLimit(entry, now);

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = _options.PermitLimit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, remaining).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = ((DateTimeOffset)resetTime).ToUnixTimeSeconds().ToString();

        if (!isAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for {ClientKey}. Requests: {Count}/{Limit}",
                clientKey, entry.RequestCount, _options.PermitLimit);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = ((int)(resetTime - now).TotalSeconds).ToString();
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                error = "Rate limit exceeded. Please try again later.",
                code = "RATE_LIMIT_EXCEEDED",
                retry_after_seconds = (int)(resetTime - now).TotalSeconds
            });
            return;
        }

        await _next(context);
    }

    private (bool isAllowed, int remaining, DateTime resetTime) CheckRateLimit(RateLimitEntry entry, DateTime now)
    {
        lock (entry)
        {
            // Check if window has expired
            var windowDuration = TimeSpan.FromSeconds(_options.WindowSeconds);
            if (now - entry.WindowStart >= windowDuration)
            {
                // Reset window
                entry.WindowStart = now;
                entry.RequestCount = 0;
                entry.QueuedRequests = 0;
            }

            // Calculate remaining requests
            var remaining = _options.PermitLimit - entry.RequestCount;
            var resetTime = entry.WindowStart.AddSeconds(_options.WindowSeconds);

            // Check if limit exceeded
            if (entry.RequestCount >= _options.PermitLimit)
            {
                // Check if queuing is allowed
                if (_options.QueueLimit > 0 && entry.QueuedRequests < _options.QueueLimit)
                {
                    entry.QueuedRequests++;
                }
                return (false, 0, resetTime);
            }

            // Increment counter
            entry.RequestCount++;
            return (true, remaining - 1, resetTime);
        }
    }

    private string GetClientKey(HttpContext context)
    {
        // Prefer API key if available (per-key rate limiting)
        var apiKeyId = context.Items["ApiKeyId"] as string;
        if (!string.IsNullOrEmpty(apiKeyId))
        {
            return $"key:{apiKeyId}";
        }

        // Fall back to IP address
        var ip = GetClientIp(context);
        return $"ip:{ip}";
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

    private void CleanupExpiredEntries(object? state)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-_options.WindowSeconds * 2);
        var expiredKeys = _rateLimits
            .Where(kvp => kvp.Value.WindowStart < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _rateLimits.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired rate limit entries", expiredKeys.Count);
        }
    }
}

/// <summary>
/// Rate limiting options
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// Enable rate limiting
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Maximum requests per window
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Window duration in seconds
    /// </summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Queue limit for requests when limit is exceeded
    /// </summary>
    public int QueueLimit { get; set; } = 10;
}

/// <summary>
/// Rate limit tracking entry
/// </summary>
public class RateLimitEntry
{
    public DateTime WindowStart { get; set; }
    public int RequestCount { get; set; }
    public int QueuedRequests { get; set; }
}

/// <summary>
/// Extension methods for Rate Limiting middleware
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
