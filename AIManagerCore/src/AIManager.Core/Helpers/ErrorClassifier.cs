using AIManager.Core.Models;

namespace AIManager.Core.Helpers;

/// <summary>
/// Helper class to classify platform API errors for account rotation handling
/// </summary>
public static class ErrorClassifier
{
    // Facebook/Meta error codes
    private static readonly HashSet<string> FacebookBanCodes = new()
    {
        "190", // OAuthException - invalid access token
        "368", // Spam block
        "200", // Permission denied
        "100", // Invalid parameter
    };

    private static readonly HashSet<string> FacebookRateLimitCodes = new()
    {
        "4", // Too many calls
        "17", // User request limit reached
        "32", // Page request limit reached
        "613", // Calls to graph api too fast
    };

    // Twitter/X error codes
    private static readonly HashSet<string> TwitterBanCodes = new()
    {
        "63", // User suspended
        "64", // Account suspended
        "326", // Account locked
        "32", // Authentication failed
    };

    private static readonly HashSet<string> TwitterRateLimitCodes = new()
    {
        "88", // Rate limit exceeded
        "185", // Daily limit reached
        "187", // Status duplicate
        "226", // Tweet too long
    };

    // Generic ban-related keywords
    private static readonly string[] BanKeywords =
    {
        "banned", "suspended", "disabled", "blocked", "restricted",
        "violated", "policy", "spam", "abuse", "terminated",
        "deactivated", "locked", "forbidden"
    };

    // Generic rate limit keywords
    private static readonly string[] RateLimitKeywords =
    {
        "rate limit", "too many", "slow down", "try again later",
        "quota exceeded", "throttled", "limit reached"
    };

    // Token expired keywords
    private static readonly string[] TokenExpiredKeywords =
    {
        "token expired", "invalid token", "token revoked", "unauthorized",
        "authentication required", "access denied", "login required"
    };

    /// <summary>
    /// Classify an error based on platform, error code, and message
    /// </summary>
    public static (ErrorType Type, bool ShouldRetry, int? RetryAfterSeconds) ClassifyError(
        SocialPlatform platform,
        string? errorCode,
        string errorMessage,
        int? httpStatusCode = null)
    {
        var lowerMessage = errorMessage.ToLowerInvariant();

        // Check HTTP status codes first
        if (httpStatusCode.HasValue)
        {
            switch (httpStatusCode.Value)
            {
                case 401:
                    return (ErrorType.AuthenticationError, false, null);
                case 403:
                    if (ContainsAny(lowerMessage, BanKeywords))
                        return (ErrorType.AccountBanned, false, null);
                    return (ErrorType.AuthenticationError, false, null);
                case 429:
                    return (ErrorType.RateLimited, true, 3600); // Default 1 hour
                case >= 500:
                    return (ErrorType.PlatformError, true, 60); // Retry after 1 minute
            }
        }

        // Platform-specific error code handling
        if (!string.IsNullOrEmpty(errorCode))
        {
            var classified = platform switch
            {
                SocialPlatform.Facebook or SocialPlatform.Instagram => ClassifyFacebookError(errorCode),
                SocialPlatform.Twitter => ClassifyTwitterError(errorCode),
                _ => null
            };

            if (classified.HasValue)
                return classified.Value;
        }

        // Keyword-based classification
        if (ContainsAny(lowerMessage, BanKeywords))
        {
            if (lowerMessage.Contains("suspend") || lowerMessage.Contains("temporary"))
                return (ErrorType.AccountSuspended, false, null);
            return (ErrorType.AccountBanned, false, null);
        }

        if (ContainsAny(lowerMessage, RateLimitKeywords))
            return (ErrorType.RateLimited, true, 3600);

        if (ContainsAny(lowerMessage, TokenExpiredKeywords))
            return (ErrorType.TokenExpired, false, null);

        // Content rejection indicators
        if (lowerMessage.Contains("content") && (lowerMessage.Contains("reject") || lowerMessage.Contains("violat")))
            return (ErrorType.ContentRejected, false, null);

        // Default: unknown error, allow retry
        return (ErrorType.Unknown, true, 30);
    }

    /// <summary>
    /// Create a TaskResult with properly classified error
    /// </summary>
    public static TaskResult CreateErrorResult(
        SocialPlatform platform,
        string? errorCode,
        string errorMessage,
        int? httpStatusCode = null,
        long processingTimeMs = 0,
        int? accountId = null)
    {
        var (errorType, shouldRetry, retryAfter) = ClassifyError(platform, errorCode, errorMessage, httpStatusCode);

        return new TaskResult
        {
            Success = false,
            Error = errorMessage,
            ErrorCode = errorCode,
            ErrorType = errorType,
            ShouldRetry = shouldRetry,
            RetryAfterSeconds = retryAfter,
            AccountId = accountId,
            ProcessingTimeMs = processingTimeMs
        };
    }

    private static (ErrorType, bool, int?)? ClassifyFacebookError(string errorCode)
    {
        if (FacebookBanCodes.Contains(errorCode))
            return (ErrorType.AccountBanned, false, null);
        if (FacebookRateLimitCodes.Contains(errorCode))
            return (ErrorType.RateLimited, true, 3600);
        return null;
    }

    private static (ErrorType, bool, int?)? ClassifyTwitterError(string errorCode)
    {
        if (TwitterBanCodes.Contains(errorCode))
            return (ErrorType.AccountBanned, false, null);
        if (TwitterRateLimitCodes.Contains(errorCode))
            return (ErrorType.RateLimited, true, 900); // Twitter: 15 min windows
        return null;
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(k => text.Contains(k));
    }
}
