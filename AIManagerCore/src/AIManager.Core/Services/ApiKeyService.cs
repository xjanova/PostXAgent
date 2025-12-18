using System.Security.Cryptography;
using System.Text.Json;

namespace AIManager.Core.Services;

/// <summary>
/// API Key Management Service
/// จัดการ API Keys สำหรับการ authentication กับ AI Manager Core
/// </summary>
public class ApiKeyService
{
    private readonly string _keysFilePath;
    private readonly object _lock = new();
    private Dictionary<string, ApiKeyInfo> _apiKeys = new();

    public ApiKeyService(string? dataPath = null)
    {
        var basePath = dataPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AIManager"
        );
        Directory.CreateDirectory(basePath);
        _keysFilePath = Path.Combine(basePath, "api_keys.json");
        LoadKeys();
    }

    /// <summary>
    /// Generate a new API key
    /// </summary>
    public ApiKeyInfo GenerateKey(string name, string? description = null, string[]? allowedIps = null, string[]? scopes = null)
    {
        var key = GenerateSecureKey();
        var keyHash = HashKey(key);
        var keyPrefix = key[..8]; // First 8 chars for identification

        var keyInfo = new ApiKeyInfo
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = description,
            KeyPrefix = keyPrefix,
            KeyHash = keyHash,
            AllowedIps = allowedIps ?? Array.Empty<string>(),
            Scopes = scopes ?? new[] { "all" },
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            LastUsedAt = null,
            UsageCount = 0
        };

        lock (_lock)
        {
            _apiKeys[keyInfo.Id] = keyInfo;
            SaveKeys();
        }

        // Return with the actual key (only shown once)
        return keyInfo with { PlainKey = key };
    }

    /// <summary>
    /// Validate an API key
    /// </summary>
    public ApiKeyValidationResult ValidateKey(string apiKey, string? clientIp = null, string? requiredScope = null)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return new ApiKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "API key is required"
            };
        }

        var keyHash = HashKey(apiKey);
        var keyPrefix = apiKey.Length >= 8 ? apiKey[..8] : apiKey;

        lock (_lock)
        {
            var keyInfo = _apiKeys.Values.FirstOrDefault(k =>
                k.KeyHash == keyHash && k.KeyPrefix == keyPrefix);

            if (keyInfo == null)
            {
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Invalid API key"
                };
            }

            if (!keyInfo.IsActive)
            {
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "API key is disabled"
                };
            }

            if (keyInfo.ExpiresAt.HasValue && keyInfo.ExpiresAt < DateTime.UtcNow)
            {
                return new ApiKeyValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "API key has expired"
                };
            }

            // Check IP restriction
            if (keyInfo.AllowedIps.Length > 0 && !string.IsNullOrEmpty(clientIp))
            {
                if (!keyInfo.AllowedIps.Contains(clientIp) &&
                    !keyInfo.AllowedIps.Contains("*"))
                {
                    return new ApiKeyValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "IP address not allowed"
                    };
                }
            }

            // Check scope
            if (!string.IsNullOrEmpty(requiredScope))
            {
                if (!keyInfo.Scopes.Contains("all") && !keyInfo.Scopes.Contains(requiredScope))
                {
                    return new ApiKeyValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"API key does not have '{requiredScope}' scope"
                    };
                }
            }

            // Update usage stats
            keyInfo.LastUsedAt = DateTime.UtcNow;
            keyInfo.UsageCount++;
            SaveKeys();

            return new ApiKeyValidationResult
            {
                IsValid = true,
                KeyInfo = keyInfo
            };
        }
    }

    /// <summary>
    /// Get all API keys (without hashes)
    /// </summary>
    public IEnumerable<ApiKeyInfo> GetAllKeys()
    {
        lock (_lock)
        {
            return _apiKeys.Values
                .Select(k => k with { KeyHash = null, PlainKey = null })
                .ToList();
        }
    }

    /// <summary>
    /// Get API key by ID
    /// </summary>
    public ApiKeyInfo? GetKeyById(string id)
    {
        lock (_lock)
        {
            return _apiKeys.TryGetValue(id, out var key)
                ? key with { KeyHash = null, PlainKey = null }
                : null;
        }
    }

    /// <summary>
    /// Update API key
    /// </summary>
    public bool UpdateKey(string id, string? name = null, string? description = null,
        string[]? allowedIps = null, string[]? scopes = null, bool? isActive = null,
        DateTime? expiresAt = null)
    {
        lock (_lock)
        {
            if (!_apiKeys.TryGetValue(id, out var key))
                return false;

            if (name != null) key.Name = name;
            if (description != null) key.Description = description;
            if (allowedIps != null) key.AllowedIps = allowedIps;
            if (scopes != null) key.Scopes = scopes;
            if (isActive.HasValue) key.IsActive = isActive.Value;
            if (expiresAt.HasValue) key.ExpiresAt = expiresAt;

            key.UpdatedAt = DateTime.UtcNow;
            SaveKeys();
            return true;
        }
    }

    /// <summary>
    /// Revoke/Delete API key
    /// </summary>
    public bool RevokeKey(string id)
    {
        lock (_lock)
        {
            if (_apiKeys.Remove(id))
            {
                SaveKeys();
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Regenerate API key (new key, same settings)
    /// </summary>
    public ApiKeyInfo? RegenerateKey(string id)
    {
        lock (_lock)
        {
            if (!_apiKeys.TryGetValue(id, out var existingKey))
                return null;

            var newKey = GenerateSecureKey();
            var newKeyHash = HashKey(newKey);
            var newKeyPrefix = newKey[..8];

            existingKey.KeyHash = newKeyHash;
            existingKey.KeyPrefix = newKeyPrefix;
            existingKey.UpdatedAt = DateTime.UtcNow;
            existingKey.UsageCount = 0;
            existingKey.LastUsedAt = null;

            SaveKeys();

            return existingKey with { PlainKey = newKey };
        }
    }

    /// <summary>
    /// Generate a master key for initial setup (if no keys exist)
    /// </summary>
    public ApiKeyInfo? GenerateMasterKeyIfNeeded()
    {
        lock (_lock)
        {
            if (_apiKeys.Count > 0)
                return null;

            return GenerateKey(
                name: "Master Key",
                description: "Auto-generated master key for initial setup",
                scopes: new[] { "all", "admin" }
            );
        }
    }

    private string GenerateSecureKey()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Format: aim_xxxxxxxx (aim = AI Manager prefix)
        var keyPart = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")[..40];

        return $"aim_{keyPart}";
    }

    private string HashKey(string key)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private void LoadKeys()
    {
        try
        {
            if (File.Exists(_keysFilePath))
            {
                var json = File.ReadAllText(_keysFilePath);
                var keys = JsonSerializer.Deserialize<Dictionary<string, ApiKeyInfo>>(json);
                if (keys != null)
                {
                    _apiKeys = keys;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading API keys: {ex.Message}");
        }
    }

    private void SaveKeys()
    {
        try
        {
            var json = JsonSerializer.Serialize(_apiKeys, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_keysFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving API keys: {ex.Message}");
        }
    }
}

/// <summary>
/// API Key Information
/// </summary>
public record ApiKeyInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public string? KeyHash { get; set; }
    public string? PlainKey { get; set; } // Only returned when generating
    public string[] AllowedIps { get; set; } = Array.Empty<string>();
    public string[] Scopes { get; set; } = new[] { "all" };
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public long UsageCount { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// API Key Validation Result
/// </summary>
public record ApiKeyValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public ApiKeyInfo? KeyInfo { get; set; }
}

/// <summary>
/// Available API Scopes
/// </summary>
public static class ApiScopes
{
    public const string All = "all";
    public const string Admin = "admin";
    public const string Tasks = "tasks";
    public const string Workers = "workers";
    public const string Content = "content";
    public const string Images = "images";
    public const string Automation = "automation";
    public const string Analytics = "analytics";
    public const string ReadOnly = "read";
}
