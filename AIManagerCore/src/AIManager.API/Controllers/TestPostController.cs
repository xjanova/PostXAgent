using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Models;
using AIManager.Core.Workers;
using AIManager.Core.Helpers;

namespace AIManager.API.Controllers;

/// <summary>
/// Test posting controller - ทดสอบโพสโดยตรงไม่ผ่าน Queue
/// ใช้สำหรับทดสอบ Account และ Credentials
/// </summary>
[ApiController]
[Route("api/test")]
public class TestPostController : ControllerBase
{
    private readonly ILogger<TestPostController> _logger;
    private readonly WorkerFactory _workerFactory;

    public TestPostController(ILogger<TestPostController> logger, WorkerFactory workerFactory)
    {
        _logger = logger;
        _workerFactory = workerFactory;
    }

    /// <summary>
    /// ทดสอบโพสไปยัง Platform โดยตรง (ไม่ผ่าน Queue)
    /// </summary>
    [HttpPost("post")]
    public async Task<ActionResult<TestPostResponse>> TestPost([FromBody] TestPostRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Test posting to {Platform}", request.Platform);

        try
        {
            // Validate request
            if (request.Credentials == null)
            {
                return BadRequest(new TestPostResponse
                {
                    Success = false,
                    Error = "Credentials are required",
                    ErrorType = ErrorType.ValidationError
                });
            }

            if (request.Content == null || string.IsNullOrWhiteSpace(request.Content.Text))
            {
                return BadRequest(new TestPostResponse
                {
                    Success = false,
                    Error = "Content text is required",
                    ErrorType = ErrorType.ValidationError
                });
            }

            // Get worker for platform
            var worker = _workerFactory.GetWorker(request.Platform);

            // Create task item
            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = TaskType.PostContent,
                Platform = request.Platform,
                UserId = request.UserId,
                BrandId = request.BrandId,
                Payload = new TaskPayload
                {
                    Content = request.Content,
                    Credentials = request.Credentials
                },
                CreatedAt = DateTime.UtcNow
            };

            // Execute post directly
            var result = await worker.PostContentAsync(task, ct);

            return Ok(new TestPostResponse
            {
                Success = result.Success,
                PostId = result.Data?.PostId,
                PlatformUrl = result.Data?.PlatformUrl,
                Error = result.Error,
                ErrorCode = result.ErrorCode,
                ErrorType = result.ErrorType,
                ShouldRetry = result.ShouldRetry,
                RetryAfterSeconds = result.RetryAfterSeconds,
                ProcessingTimeMs = result.ProcessingTimeMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test post failed for platform {Platform}", request.Platform);

            return Ok(new TestPostResponse
            {
                Success = false,
                Error = ex.Message,
                ErrorType = ErrorType.Unknown
            });
        }
    }

    /// <summary>
    /// ทดสอบ Credentials ว่าใช้งานได้หรือไม่
    /// </summary>
    [HttpPost("validate-credentials")]
    public async Task<ActionResult<CredentialValidationResponse>> ValidateCredentials(
        [FromBody] ValidateCredentialsRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Validating credentials for {Platform}", request.Platform);

        try
        {
            var worker = _workerFactory.GetWorker(request.Platform);
            var isValid = await ValidateCredentialsForPlatform(request.Platform, request.Credentials, ct);

            return Ok(new CredentialValidationResponse
            {
                Valid = isValid,
                Platform = request.Platform.ToString(),
                Message = isValid ? "Credentials are valid" : "Credentials are invalid or expired"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Credential validation failed");

            return Ok(new CredentialValidationResponse
            {
                Valid = false,
                Platform = request.Platform.ToString(),
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ทดสอบโพสพร้อม Failover หลาย Account
    /// </summary>
    [HttpPost("post-with-failover")]
    public async Task<ActionResult<FailoverPostResponse>> TestPostWithFailover(
        [FromBody] FailoverPostRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Test posting with failover, {AccountCount} accounts", request.Accounts.Count);

        var attempts = new List<PostAttempt>();
        var worker = _workerFactory.GetWorker(request.Platform);

        foreach (var account in request.Accounts)
        {
            var attempt = new PostAttempt
            {
                AccountId = account.AccountId,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                var task = new TaskItem
                {
                    Id = Guid.NewGuid().ToString(),
                    Type = TaskType.PostContent,
                    Platform = request.Platform,
                    Payload = new TaskPayload
                    {
                        Content = request.Content,
                        Credentials = account.Credentials
                    }
                };

                var result = await worker.PostContentAsync(task, ct);
                attempt.CompletedAt = DateTime.UtcNow;
                attempt.Success = result.Success;
                attempt.PostId = result.Data?.PostId;
                attempt.PlatformUrl = result.Data?.PlatformUrl;
                attempt.Error = result.Error;
                attempt.ErrorType = result.ErrorType;

                attempts.Add(attempt);

                // ถ้าสำเร็จ หยุดทันที
                if (result.Success)
                {
                    return Ok(new FailoverPostResponse
                    {
                        Success = true,
                        PostId = result.Data?.PostId,
                        PlatformUrl = result.Data?.PlatformUrl,
                        SuccessAccountId = account.AccountId,
                        TotalAttempts = attempts.Count,
                        Attempts = attempts
                    });
                }

                // ถ้า Error ไม่ควร Retry ให้หยุด
                if (!result.ShouldRetry)
                {
                    _logger.LogWarning("Account {AccountId} failed with non-retryable error: {Error}",
                        account.AccountId, result.Error);
                }
            }
            catch (Exception ex)
            {
                attempt.CompletedAt = DateTime.UtcNow;
                attempt.Success = false;
                attempt.Error = ex.Message;
                attempt.ErrorType = ErrorType.Unknown;
                attempts.Add(attempt);
            }
        }

        // ทุก Account ล้มเหลว
        return Ok(new FailoverPostResponse
        {
            Success = false,
            Error = "All accounts failed",
            TotalAttempts = attempts.Count,
            Attempts = attempts
        });
    }

    /// <summary>
    /// ดึง Content จาก Backend แล้วโพส
    /// </summary>
    [HttpPost("post-from-backend")]
    public async Task<ActionResult<TestPostResponse>> PostFromBackend(
        [FromBody] PostFromBackendRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Fetching content from backend and posting to {Platform}", request.Platform);

        try
        {
            // Fetch content from Laravel backend
            using var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(request.BackendUrl ?? "http://localhost:8000");

            var response = await httpClient.GetAsync($"/api/v1/posts/{request.PostId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(new TestPostResponse
                {
                    Success = false,
                    Error = $"Failed to fetch post from backend: {response.StatusCode}"
                });
            }

            var postData = await response.Content.ReadFromJsonAsync<BackendPostResponse>(ct);

            if (postData?.Data == null)
            {
                return BadRequest(new TestPostResponse
                {
                    Success = false,
                    Error = "Invalid post data from backend"
                });
            }

            // Create content from backend data
            var content = new PostContent
            {
                Text = postData.Data.Content ?? "",
                Hashtags = postData.Data.Hashtags,
                Images = postData.Data.MediaUrls,
                Link = postData.Data.LinkUrl
            };

            // Post using provided credentials
            var worker = _workerFactory.GetWorker(request.Platform);
            var task = new TaskItem
            {
                Id = Guid.NewGuid().ToString(),
                Type = TaskType.PostContent,
                Platform = request.Platform,
                Payload = new TaskPayload
                {
                    Content = content,
                    Credentials = request.Credentials
                }
            };

            var result = await worker.PostContentAsync(task, ct);

            return Ok(new TestPostResponse
            {
                Success = result.Success,
                PostId = result.Data?.PostId,
                PlatformUrl = result.Data?.PlatformUrl,
                Error = result.Error,
                ErrorType = result.ErrorType,
                ProcessingTimeMs = result.ProcessingTimeMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post from backend failed");
            return Ok(new TestPostResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    private async Task<bool> ValidateCredentialsForPlatform(
        SocialPlatform platform,
        PlatformCredentials credentials,
        CancellationToken ct)
    {
        using var httpClient = new HttpClient();

        return platform switch
        {
            SocialPlatform.Facebook or SocialPlatform.Instagram => await ValidateFacebookCredentials(httpClient, credentials, ct),
            SocialPlatform.Twitter => await ValidateTwitterCredentials(httpClient, credentials, ct),
            _ => true // Default: assume valid
        };
    }

    private async Task<bool> ValidateFacebookCredentials(HttpClient client, PlatformCredentials creds, CancellationToken ct)
    {
        try
        {
            var response = await client.GetAsync(
                $"https://graph.facebook.com/v19.0/me?access_token={creds.AccessToken}",
                ct
            );
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ValidateTwitterCredentials(HttpClient client, PlatformCredentials creds, CancellationToken ct)
    {
        // Twitter OAuth validation would go here
        return !string.IsNullOrEmpty(creds.AccessToken);
    }
}

// Request/Response Models

public class TestPostRequest
{
    public SocialPlatform Platform { get; set; }
    public int UserId { get; set; }
    public int BrandId { get; set; }
    public PostContent Content { get; set; } = new();
    public PlatformCredentials? Credentials { get; set; }
}

public class TestPostResponse
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PlatformUrl { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public ErrorType ErrorType { get; set; }
    public bool ShouldRetry { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public long ProcessingTimeMs { get; set; }
}

public class ValidateCredentialsRequest
{
    public SocialPlatform Platform { get; set; }
    public PlatformCredentials Credentials { get; set; } = new();
}

public class CredentialValidationResponse
{
    public bool Valid { get; set; }
    public string Platform { get; set; } = "";
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class FailoverPostRequest
{
    public SocialPlatform Platform { get; set; }
    public PostContent Content { get; set; } = new();
    public List<AccountCredential> Accounts { get; set; } = new();
}

public class AccountCredential
{
    public int AccountId { get; set; }
    public PlatformCredentials Credentials { get; set; } = new();
}

public class FailoverPostResponse
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PlatformUrl { get; set; }
    public int? SuccessAccountId { get; set; }
    public string? Error { get; set; }
    public int TotalAttempts { get; set; }
    public List<PostAttempt> Attempts { get; set; } = new();
}

public class PostAttempt
{
    public int AccountId { get; set; }
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PlatformUrl { get; set; }
    public string? Error { get; set; }
    public ErrorType ErrorType { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class PostFromBackendRequest
{
    public int PostId { get; set; }
    public SocialPlatform Platform { get; set; }
    public PlatformCredentials Credentials { get; set; } = new();
    public string? BackendUrl { get; set; }
}

public class BackendPostResponse
{
    public bool Success { get; set; }
    public BackendPostData? Data { get; set; }
}

public class BackendPostData
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<string>? MediaUrls { get; set; }
    public string? LinkUrl { get; set; }
}
