using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;

namespace AIManager.API.Controllers;

/// <summary>
/// API Keys Management Controller
/// จัดการ API Keys สำหรับ authentication
/// ต้องมี Admin scope ถึงจะเข้าถึงได้
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ApiKeysController : ControllerBase
{
    private readonly ApiKeyService _apiKeyService;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(ApiKeyService apiKeyService, ILogger<ApiKeysController> logger)
    {
        _apiKeyService = apiKeyService;
        _logger = logger;
    }

    /// <summary>
    /// Get all API keys (without sensitive data)
    /// </summary>
    [HttpGet]
    public ActionResult<IEnumerable<ApiKeyInfo>> GetAll()
    {
        var keys = _apiKeyService.GetAllKeys();
        return Ok(new
        {
            success = true,
            data = keys
        });
    }

    /// <summary>
    /// Get API key by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiKeyInfo> GetById(string id)
    {
        var key = _apiKeyService.GetKeyById(id);
        if (key == null)
        {
            return NotFound(new
            {
                success = false,
                error = "API key not found"
            });
        }

        return Ok(new
        {
            success = true,
            data = key
        });
    }

    /// <summary>
    /// Generate a new API key
    /// </summary>
    [HttpPost]
    public ActionResult<ApiKeyInfo> Create([FromBody] CreateApiKeyRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return BadRequest(new
            {
                success = false,
                error = "Name is required"
            });
        }

        var key = _apiKeyService.GenerateKey(
            request.Name,
            request.Description,
            request.AllowedIps,
            request.Scopes
        );

        _logger.LogInformation("API key created: {KeyName} ({KeyId})", key.Name, key.Id);

        // Return with plain key (only shown once!)
        return CreatedAtAction(nameof(GetById), new { id = key.Id }, new
        {
            success = true,
            data = key,
            message = "API key created successfully. Please save the key as it won't be shown again.",
            warning = "Store this key securely - it cannot be retrieved later!"
        });
    }

    /// <summary>
    /// Update API key settings
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult Update(string id, [FromBody] UpdateApiKeyRequest request)
    {
        var result = _apiKeyService.UpdateKey(
            id,
            request.Name,
            request.Description,
            request.AllowedIps,
            request.Scopes,
            request.IsActive,
            request.ExpiresAt
        );

        if (!result)
        {
            return NotFound(new
            {
                success = false,
                error = "API key not found"
            });
        }

        _logger.LogInformation("API key updated: {KeyId}", id);

        return Ok(new
        {
            success = true,
            message = "API key updated successfully"
        });
    }

    /// <summary>
    /// Revoke/Delete API key
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult Delete(string id)
    {
        var result = _apiKeyService.RevokeKey(id);
        if (!result)
        {
            return NotFound(new
            {
                success = false,
                error = "API key not found"
            });
        }

        _logger.LogInformation("API key revoked: {KeyId}", id);

        return Ok(new
        {
            success = true,
            message = "API key revoked successfully"
        });
    }

    /// <summary>
    /// Regenerate API key (new key, same settings)
    /// </summary>
    [HttpPost("{id}/regenerate")]
    public ActionResult<ApiKeyInfo> Regenerate(string id)
    {
        var key = _apiKeyService.RegenerateKey(id);
        if (key == null)
        {
            return NotFound(new
            {
                success = false,
                error = "API key not found"
            });
        }

        _logger.LogInformation("API key regenerated: {KeyId}", id);

        return Ok(new
        {
            success = true,
            data = key,
            message = "API key regenerated successfully. Please save the new key.",
            warning = "Store this key securely - it cannot be retrieved later!"
        });
    }

    /// <summary>
    /// Enable/Disable API key
    /// </summary>
    [HttpPost("{id}/toggle")]
    public ActionResult Toggle(string id)
    {
        var key = _apiKeyService.GetKeyById(id);
        if (key == null)
        {
            return NotFound(new
            {
                success = false,
                error = "API key not found"
            });
        }

        var newState = !key.IsActive;
        _apiKeyService.UpdateKey(id, isActive: newState);

        _logger.LogInformation("API key {Action}: {KeyId}",
            newState ? "enabled" : "disabled", id);

        return Ok(new
        {
            success = true,
            isActive = newState,
            message = $"API key {(newState ? "enabled" : "disabled")} successfully"
        });
    }

    /// <summary>
    /// Get available scopes
    /// </summary>
    [HttpGet("scopes")]
    public ActionResult GetScopes()
    {
        var scopes = new[]
        {
            new { name = ApiScopes.All, description = "Full access to all endpoints" },
            new { name = ApiScopes.Admin, description = "Administrative operations (manage keys, system config)" },
            new { name = ApiScopes.Tasks, description = "Create and manage tasks" },
            new { name = ApiScopes.Workers, description = "Worker management" },
            new { name = ApiScopes.Content, description = "Content generation" },
            new { name = ApiScopes.Images, description = "Image generation" },
            new { name = ApiScopes.Automation, description = "Web automation and workflows" },
            new { name = ApiScopes.Analytics, description = "View and manage analytics" },
            new { name = ApiScopes.ReadOnly, description = "Read-only access" }
        };

        return Ok(new
        {
            success = true,
            data = scopes
        });
    }

    /// <summary>
    /// Validate current API key (for testing)
    /// </summary>
    [HttpGet("validate")]
    public ActionResult Validate()
    {
        // Key info is set by middleware
        var keyInfo = HttpContext.Items["ApiKeyInfo"] as ApiKeyInfo;

        return Ok(new
        {
            success = true,
            data = new
            {
                keyInfo?.Id,
                keyInfo?.Name,
                keyInfo?.Scopes,
                keyInfo?.LastUsedAt,
                keyInfo?.UsageCount
            },
            message = "API key is valid"
        });
    }

    /// <summary>
    /// Generate master key for initial setup (only works if no keys exist)
    /// </summary>
    [HttpPost("setup")]
    public ActionResult<ApiKeyInfo> SetupMasterKey()
    {
        var key = _apiKeyService.GenerateMasterKeyIfNeeded();
        if (key == null)
        {
            return BadRequest(new
            {
                success = false,
                error = "API keys already exist. Use the admin endpoint to create new keys."
            });
        }

        _logger.LogInformation("Master API key created for initial setup");

        return Ok(new
        {
            success = true,
            data = key,
            message = "Master API key created for initial setup",
            warning = "IMPORTANT: Store this key securely! It is the only way to manage this system."
        });
    }
}

/// <summary>
/// Request model for creating API key
/// </summary>
public record CreateApiKeyRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string[]? AllowedIps { get; init; }
    public string[]? Scopes { get; init; }
}

/// <summary>
/// Request model for updating API key
/// </summary>
public record UpdateApiKeyRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string[]? AllowedIps { get; init; }
    public string[]? Scopes { get; init; }
    public bool? IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
