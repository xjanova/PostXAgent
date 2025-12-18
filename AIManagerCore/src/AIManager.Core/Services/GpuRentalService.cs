using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// GPU Rental Service - เชื่อมต่อกับบริการ GPU Cloud ฟรีและเสียเงิน
/// รองรับ: Google Colab, Kaggle, Lightning AI, Hugging Face Spaces, RunPod
/// </summary>
public class GpuRentalService
{
    private readonly ILogger<GpuRentalService>? _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, GpuProviderConfig> _providerConfigs = new();
    private readonly Dictionary<string, List<GpuAccount>> _accountPools = new();
    private readonly Dictionary<string, int> _currentAccountIndex = new();

    public event EventHandler<GpuStatusEventArgs>? StatusChanged;
    public event EventHandler<AccountRotationEventArgs>? AccountRotated;

    /// <summary>
    /// รายการ GPU Provider ทั้งหมด (เรียงตามลำดับความสำคัญ - ฟรีก่อน)
    /// </summary>
    public static readonly GpuProvider[] AllProviders = new[]
    {
        // FREE TIER PROVIDERS
        new GpuProvider
        {
            Id = "kaggle",
            Name = "Kaggle Notebooks",
            Description = "ฟรี GPU T4/P100, 30 ชม./สัปดาห์",
            IsFree = true,
            GpuType = "NVIDIA T4 / P100",
            VramGB = 16,
            WeeklyHoursLimit = 30,
            SetupUrl = "https://www.kaggle.com/account",
            DocumentationUrl = "https://www.kaggle.com/docs/notebooks",
            RequiresApiKey = true,
            ApiKeyName = "KAGGLE_KEY",
            Priority = 1,
            Features = new[] { "T4/P100 GPU", "30 hrs/week", "Easy setup", "TPU available" }
        },
        new GpuProvider
        {
            Id = "colab",
            Name = "Google Colab",
            Description = "ฟรี GPU T4, จำกัดเวลา (~12 ชม./session)",
            IsFree = true,
            GpuType = "NVIDIA T4",
            VramGB = 15,
            WeeklyHoursLimit = 0, // Dynamic limit
            SetupUrl = "https://colab.research.google.com",
            DocumentationUrl = "https://research.google.com/colaboratory/faq.html",
            RequiresApiKey = false, // Uses Google OAuth
            Priority = 2,
            Features = new[] { "T4 GPU", "Easy to use", "Google Drive integration", "Free tier" }
        },
        new GpuProvider
        {
            Id = "lightning",
            Name = "Lightning AI Studios",
            Description = "ฟรี 22 GPU-hours/เดือน, A10G",
            IsFree = true,
            GpuType = "NVIDIA A10G",
            VramGB = 24,
            MonthlyCredits = 22,
            SetupUrl = "https://lightning.ai",
            DocumentationUrl = "https://lightning.ai/docs",
            RequiresApiKey = true,
            ApiKeyName = "LIGHTNING_API_KEY",
            Priority = 3,
            Features = new[] { "A10G GPU (24GB)", "22 hrs free/month", "VS Code integration", "Persistent storage" }
        },
        new GpuProvider
        {
            Id = "huggingface_spaces",
            Name = "Hugging Face Spaces",
            Description = "ฟรี ZeroGPU (ใช้ร่วมกัน), หรือเช่า A10G/A100",
            IsFree = true,
            GpuType = "ZeroGPU (Shared)",
            VramGB = 0, // Shared
            SetupUrl = "https://huggingface.co/spaces",
            DocumentationUrl = "https://huggingface.co/docs/hub/spaces-gpus",
            RequiresApiKey = true,
            ApiKeyName = "HF_TOKEN",
            Priority = 4,
            Features = new[] { "ZeroGPU free tier", "Easy deployment", "Gradio integration", "Paid upgrades available" }
        },
        new GpuProvider
        {
            Id = "paperspace_gradient",
            Name = "Paperspace Gradient",
            Description = "ฟรี M4000 GPU, 6 hrs session limit",
            IsFree = true,
            GpuType = "NVIDIA M4000",
            VramGB = 8,
            SetupUrl = "https://console.paperspace.com/signup",
            DocumentationUrl = "https://docs.paperspace.com/gradient/",
            RequiresApiKey = true,
            ApiKeyName = "PAPERSPACE_API_KEY",
            Priority = 5,
            Features = new[] { "M4000 GPU (8GB)", "Jupyter notebooks", "Free tier", "Good for inference" }
        },

        // PAID PROVIDERS (Budget-friendly)
        new GpuProvider
        {
            Id = "runpod",
            Name = "RunPod",
            Description = "เริ่มต้น $0.20/hr สำหรับ RTX 3090",
            IsFree = false,
            GpuType = "Various (RTX 3090, A100, etc.)",
            VramGB = 24,
            HourlyRate = 0.20m,
            SetupUrl = "https://www.runpod.io",
            DocumentationUrl = "https://docs.runpod.io",
            RequiresApiKey = true,
            ApiKeyName = "RUNPOD_API_KEY",
            Priority = 10,
            Features = new[] { "Pay-per-use", "Many GPU options", "Serverless endpoints", "Custom containers" }
        },
        new GpuProvider
        {
            Id = "vast_ai",
            Name = "Vast.ai",
            Description = "GPU marketplace, เริ่มต้น $0.10/hr",
            IsFree = false,
            GpuType = "Various (Community GPUs)",
            VramGB = 0, // Variable
            HourlyRate = 0.10m,
            SetupUrl = "https://vast.ai",
            DocumentationUrl = "https://vast.ai/docs",
            RequiresApiKey = true,
            ApiKeyName = "VAST_API_KEY",
            Priority = 11,
            Features = new[] { "Cheapest option", "P2P marketplace", "Bid system", "Docker support" }
        },
        new GpuProvider
        {
            Id = "lambda_labs",
            Name = "Lambda Labs",
            Description = "Cloud GPUs สำหรับ ML, เริ่มต้น $0.50/hr",
            IsFree = false,
            GpuType = "A100, H100",
            VramGB = 80,
            HourlyRate = 0.50m,
            SetupUrl = "https://lambdalabs.com/cloud",
            DocumentationUrl = "https://lambdalabs.com/blog/getting-started-with-lambda-cloud",
            RequiresApiKey = true,
            ApiKeyName = "LAMBDA_API_KEY",
            Priority = 12,
            Features = new[] { "Enterprise grade", "A100/H100 GPUs", "SSH access", "Reliable" }
        },
        new GpuProvider
        {
            Id = "colab_pro",
            Name = "Google Colab Pro",
            Description = "$9.99/เดือน สำหรับ GPU ดีขึ้นและเวลานานขึ้น",
            IsFree = false,
            GpuType = "NVIDIA T4/P100/V100",
            VramGB = 16,
            MonthlyRate = 9.99m,
            SetupUrl = "https://colab.research.google.com/signup",
            DocumentationUrl = "https://colab.research.google.com/signup",
            RequiresApiKey = false,
            Priority = 13,
            Features = new[] { "Better GPUs", "Longer runtime", "More memory", "Priority access" }
        }
    };

    public GpuRentalService(ILogger<GpuRentalService>? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PostXAgent/1.0");
    }

    /// <summary>
    /// ตั้งค่า API Key สำหรับ provider
    /// </summary>
    public void SetApiKey(string providerId, string apiKey)
    {
        if (!_providerConfigs.ContainsKey(providerId))
        {
            _providerConfigs[providerId] = new GpuProviderConfig();
        }
        _providerConfigs[providerId].ApiKey = apiKey;
        _logger?.LogInformation("API Key set for provider: {Provider}", providerId);
    }

    /// <summary>
    /// ตรวจสอบสถานะและ quota ของ provider
    /// </summary>
    public async Task<GpuProviderStatus> CheckProviderStatusAsync(string providerId)
    {
        var provider = AllProviders.FirstOrDefault(p => p.Id == providerId);
        if (provider == null)
        {
            return new GpuProviderStatus { ProviderId = providerId, IsAvailable = false, Error = "Provider not found" };
        }

        var status = new GpuProviderStatus
        {
            ProviderId = providerId,
            ProviderName = provider.Name
        };

        if (!_providerConfigs.TryGetValue(providerId, out var config) || string.IsNullOrEmpty(config.ApiKey))
        {
            if (provider.RequiresApiKey)
            {
                status.IsAvailable = false;
                status.Error = "API Key required";
                return status;
            }
        }

        try
        {
            switch (providerId)
            {
                case "kaggle":
                    status = await CheckKaggleStatusAsync(config?.ApiKey ?? "");
                    break;
                case "huggingface_spaces":
                    status = await CheckHuggingFaceStatusAsync(config?.ApiKey ?? "");
                    break;
                case "runpod":
                    status = await CheckRunPodStatusAsync(config?.ApiKey ?? "");
                    break;
                default:
                    // Generic check - just verify API key format
                    status.IsAvailable = !provider.RequiresApiKey || !string.IsNullOrEmpty(config?.ApiKey);
                    status.Message = status.IsAvailable ? "Ready (manual verification required)" : "API Key required";
                    break;
            }
        }
        catch (Exception ex)
        {
            status.IsAvailable = false;
            status.Error = ex.Message;
            _logger?.LogError(ex, "Failed to check status for provider: {Provider}", providerId);
        }

        StatusChanged?.Invoke(this, new GpuStatusEventArgs { Status = status });
        return status;
    }

    private async Task<GpuProviderStatus> CheckKaggleStatusAsync(string apiKey)
    {
        var status = new GpuProviderStatus { ProviderId = "kaggle", ProviderName = "Kaggle" };

        if (string.IsNullOrEmpty(apiKey))
        {
            status.IsAvailable = false;
            status.Error = "Kaggle API Key required (username:key format)";
            return status;
        }

        try
        {
            // Kaggle API uses basic auth with username:key
            var parts = apiKey.Split(':');
            if (parts.Length != 2)
            {
                status.Error = "Invalid format. Use: username:api_key";
                return status;
            }

            var authValue = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(apiKey));
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://www.kaggle.com/api/v1/kernels/list?pageSize=1");
            request.Headers.Add("Authorization", $"Basic {authValue}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                status.IsAvailable = true;
                status.Message = "Connected to Kaggle";
                status.RemainingHours = 30; // Default weekly limit
            }
            else
            {
                status.IsAvailable = false;
                status.Error = $"Auth failed: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            status.IsAvailable = false;
            status.Error = ex.Message;
        }

        return status;
    }

    private async Task<GpuProviderStatus> CheckHuggingFaceStatusAsync(string token)
    {
        var status = new GpuProviderStatus { ProviderId = "huggingface_spaces", ProviderName = "Hugging Face" };

        if (string.IsNullOrEmpty(token))
        {
            status.IsAvailable = false;
            status.Error = "HuggingFace Token required";
            return status;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://huggingface.co/api/whoami-v2");
            request.Headers.Add("Authorization", $"Bearer {token}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var username = json.GetProperty("name").GetString();

                status.IsAvailable = true;
                status.Message = $"Connected as: {username}";
                status.Username = username;

                // Check for PRO subscription
                if (json.TryGetProperty("isPro", out var isPro) && isPro.GetBoolean())
                {
                    status.Message += " (PRO)";
                    status.IsPremium = true;
                }
            }
            else
            {
                status.IsAvailable = false;
                status.Error = $"Auth failed: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            status.IsAvailable = false;
            status.Error = ex.Message;
        }

        return status;
    }

    private async Task<GpuProviderStatus> CheckRunPodStatusAsync(string apiKey)
    {
        var status = new GpuProviderStatus { ProviderId = "runpod", ProviderName = "RunPod" };

        if (string.IsNullOrEmpty(apiKey))
        {
            status.IsAvailable = false;
            status.Error = "RunPod API Key required";
            return status;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.runpod.io/v1/myself");
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                status.IsAvailable = true;
                if (json.TryGetProperty("balance", out var balance))
                {
                    status.RemainingCredits = balance.GetDecimal();
                    status.Message = $"Balance: ${status.RemainingCredits:F2}";
                }
            }
            else
            {
                status.IsAvailable = false;
                status.Error = $"Auth failed: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            status.IsAvailable = false;
            status.Error = ex.Message;
        }

        return status;
    }

    /// <summary>
    /// รับ providers ที่แนะนำ (เรียงตามฟรี -> ราคาถูก)
    /// </summary>
    public List<GpuProviderRecommendation> GetRecommendedProviders(GpuRequirements? requirements = null)
    {
        var recommendations = new List<GpuProviderRecommendation>();
        requirements ??= new GpuRequirements();

        foreach (var provider in AllProviders.OrderBy(p => p.Priority))
        {
            var isCompatible = requirements.MinVramGB <= 0 || provider.VramGB >= requirements.MinVramGB;
            var hasApiKey = !provider.RequiresApiKey ||
                (_providerConfigs.TryGetValue(provider.Id, out var config) && !string.IsNullOrEmpty(config.ApiKey));

            recommendations.Add(new GpuProviderRecommendation
            {
                Provider = provider,
                IsCompatible = isCompatible,
                IsConfigured = hasApiKey,
                Reason = !isCompatible
                    ? $"ต้องการ VRAM {requirements.MinVramGB}GB แต่มี {provider.VramGB}GB"
                    : !hasApiKey
                        ? "ต้องตั้งค่า API Key"
                        : provider.IsFree
                            ? "พร้อมใช้งาน (ฟรี)"
                            : $"พร้อมใช้งาน (${provider.HourlyRate}/hr)"
            });
        }

        return recommendations;
    }

    /// <summary>
    /// สร้าง notebook URL สำหรับ Colab
    /// </summary>
    public string GetColabNotebookUrl(string? baseNotebook = null)
    {
        var notebook = baseNotebook ?? "https://colab.research.google.com/drive/1AIPostXAgentTemplate";
        return notebook;
    }

    /// <summary>
    /// สร้าง Kaggle kernel
    /// </summary>
    public async Task<string?> CreateKaggleKernelAsync(string title, string code)
    {
        if (!_providerConfigs.TryGetValue("kaggle", out var config) || string.IsNullOrEmpty(config.ApiKey))
        {
            throw new InvalidOperationException("Kaggle API Key not configured");
        }

        // Kaggle API implementation would go here
        _logger?.LogInformation("Creating Kaggle kernel: {Title}", title);
        await Task.Delay(100); // Placeholder

        return $"https://www.kaggle.com/kernels/{title}";
    }

    #region Account Pool Management (หมุนเวียนหลาย Account)

    /// <summary>
    /// เพิ่ม account ใหม่เข้า pool สำหรับ provider
    /// </summary>
    public void AddAccountToPool(string providerId, GpuAccount account)
    {
        if (!_accountPools.ContainsKey(providerId))
        {
            _accountPools[providerId] = new List<GpuAccount>();
            _currentAccountIndex[providerId] = 0;
        }

        // ตรวจสอบว่าไม่ซ้ำ
        if (!_accountPools[providerId].Any(a => a.Username == account.Username))
        {
            _accountPools[providerId].Add(account);
            _logger?.LogInformation("Added account {Username} to {Provider} pool. Total: {Count}",
                account.Username, providerId, _accountPools[providerId].Count);
        }
    }

    /// <summary>
    /// เพิ่มหลาย accounts พร้อมกัน
    /// </summary>
    public void AddAccountsToPool(string providerId, IEnumerable<GpuAccount> accounts)
    {
        foreach (var account in accounts)
        {
            AddAccountToPool(providerId, account);
        }
    }

    /// <summary>
    /// ลบ account ออกจาก pool
    /// </summary>
    public bool RemoveAccountFromPool(string providerId, string username)
    {
        if (_accountPools.TryGetValue(providerId, out var pool))
        {
            var account = pool.FirstOrDefault(a => a.Username == username);
            if (account != null)
            {
                pool.Remove(account);
                _logger?.LogInformation("Removed account {Username} from {Provider} pool", username, providerId);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// รับ account ปัจจุบันที่ใช้งาน
    /// </summary>
    public GpuAccount? GetCurrentAccount(string providerId)
    {
        if (!_accountPools.TryGetValue(providerId, out var pool) || pool.Count == 0)
            return null;

        var index = _currentAccountIndex.GetValueOrDefault(providerId, 0);
        if (index >= pool.Count) index = 0;

        return pool[index];
    }

    /// <summary>
    /// หมุนไปยัง account ถัดไป (เมื่อสิทธิ์หมด/เกิดข้อผิดพลาด)
    /// </summary>
    public GpuAccount? RotateToNextAccount(string providerId, string? reason = null)
    {
        if (!_accountPools.TryGetValue(providerId, out var pool) || pool.Count == 0)
            return null;

        var currentIndex = _currentAccountIndex.GetValueOrDefault(providerId, 0);
        var previousAccount = pool[currentIndex];

        // Mark previous account as exhausted
        previousAccount.IsExhausted = true;
        previousAccount.ExhaustedAt = DateTime.Now;
        previousAccount.ExhaustionReason = reason;

        // Find next available account
        var nextIndex = (currentIndex + 1) % pool.Count;
        var checkedCount = 0;

        while (checkedCount < pool.Count)
        {
            var candidate = pool[nextIndex];
            if (!candidate.IsExhausted || candidate.CanReuse())
            {
                candidate.IsExhausted = false;
                _currentAccountIndex[providerId] = nextIndex;

                // Update active API key
                SetApiKey(providerId, candidate.ApiKey);

                _logger?.LogInformation("Rotated from {Previous} to {Next} for {Provider}. Reason: {Reason}",
                    previousAccount.Username, candidate.Username, providerId, reason ?? "Manual rotation");

                AccountRotated?.Invoke(this, new AccountRotationEventArgs
                {
                    ProviderId = providerId,
                    PreviousAccount = previousAccount,
                    NewAccount = candidate,
                    Reason = reason
                });

                return candidate;
            }

            nextIndex = (nextIndex + 1) % pool.Count;
            checkedCount++;
        }

        _logger?.LogWarning("All accounts exhausted for {Provider}", providerId);
        return null;
    }

    /// <summary>
    /// ตรวจสอบและหมุน account อัตโนมัติเมื่อเกิด quota error
    /// </summary>
    public async Task<GpuAccount?> HandleQuotaExhaustedAsync(string providerId, string errorMessage)
    {
        _logger?.LogWarning("Quota exhausted for {Provider}: {Error}", providerId, errorMessage);

        var newAccount = RotateToNextAccount(providerId, errorMessage);
        if (newAccount != null)
        {
            // Verify new account
            var status = await CheckProviderStatusAsync(providerId);
            if (status.IsAvailable)
            {
                return newAccount;
            }
            else
            {
                // Try next account
                return await HandleQuotaExhaustedAsync(providerId, "New account also unavailable");
            }
        }

        return null;
    }

    /// <summary>
    /// รับรายการ accounts ทั้งหมดใน pool
    /// </summary>
    public List<GpuAccount> GetAccountPool(string providerId)
    {
        return _accountPools.TryGetValue(providerId, out var pool)
            ? pool.ToList()
            : new List<GpuAccount>();
    }

    /// <summary>
    /// รับสถิติ account pool
    /// </summary>
    public AccountPoolStats GetPoolStats(string providerId)
    {
        if (!_accountPools.TryGetValue(providerId, out var pool))
        {
            return new AccountPoolStats { ProviderId = providerId };
        }

        return new AccountPoolStats
        {
            ProviderId = providerId,
            TotalAccounts = pool.Count,
            AvailableAccounts = pool.Count(a => !a.IsExhausted || a.CanReuse()),
            ExhaustedAccounts = pool.Count(a => a.IsExhausted && !a.CanReuse()),
            CurrentAccountIndex = _currentAccountIndex.GetValueOrDefault(providerId, 0),
            CurrentAccount = GetCurrentAccount(providerId)
        };
    }

    /// <summary>
    /// Reset exhaustion status ของทุก account (เมื่อเริ่ม week/month ใหม่)
    /// </summary>
    public void ResetExhaustionStatus(string providerId)
    {
        if (_accountPools.TryGetValue(providerId, out var pool))
        {
            foreach (var account in pool)
            {
                account.IsExhausted = false;
                account.ExhaustedAt = null;
                account.ExhaustionReason = null;
            }
            _logger?.LogInformation("Reset exhaustion status for all {Count} accounts in {Provider}",
                pool.Count, providerId);
        }
    }

    /// <summary>
    /// บันทึก account pool เป็น JSON
    /// </summary>
    public string ExportAccountPoolsToJson()
    {
        var exportData = _accountPools.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(a => new
            {
                a.Username,
                a.ApiKey,
                a.Email,
                a.Notes,
                a.IsExhausted,
                a.ExhaustedAt,
                a.ResetPeriod
            }).ToList()
        );

        return System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Import account pools จาก JSON
    /// </summary>
    public void ImportAccountPoolsFromJson(string json)
    {
        try
        {
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<GpuAccount>>>(json);
            if (data != null)
            {
                foreach (var kvp in data)
                {
                    AddAccountsToPool(kvp.Key, kvp.Value);
                }
                _logger?.LogInformation("Imported {Count} provider pools", data.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to import account pools");
            throw;
        }
    }

    #endregion
}

public class GpuProvider
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsFree { get; set; }
    public string GpuType { get; set; } = "";
    public int VramGB { get; set; }
    public int WeeklyHoursLimit { get; set; }
    public int MonthlyCredits { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal MonthlyRate { get; set; }
    public string SetupUrl { get; set; } = "";
    public string DocumentationUrl { get; set; } = "";
    public bool RequiresApiKey { get; set; }
    public string ApiKeyName { get; set; } = "";
    public int Priority { get; set; }
    public string[] Features { get; set; } = Array.Empty<string>();
}

public class GpuProviderConfig
{
    public string? ApiKey { get; set; }
    public DateTime? LastVerified { get; set; }
    public bool IsEnabled { get; set; } = true;
}

public class GpuProviderStatus
{
    public string ProviderId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public bool IsAvailable { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string? Username { get; set; }
    public double? RemainingHours { get; set; }
    public decimal? RemainingCredits { get; set; }
    public bool IsPremium { get; set; }
}

public class GpuStatusEventArgs : EventArgs
{
    public GpuProviderStatus Status { get; set; } = null!;
}

public class GpuRequirements
{
    public int MinVramGB { get; set; }
    public string? PreferredProvider { get; set; }
    public bool PreferFree { get; set; } = true;
}

public class GpuProviderRecommendation
{
    public GpuProvider Provider { get; set; } = null!;
    public bool IsCompatible { get; set; }
    public bool IsConfigured { get; set; }
    public string Reason { get; set; } = "";
}

/// <summary>
/// Account สำหรับ GPU Provider (รองรับหลาย account ต่อ provider)
/// </summary>
public class GpuAccount
{
    public string Username { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsExhausted { get; set; }
    public DateTime? ExhaustedAt { get; set; }
    public string? ExhaustionReason { get; set; }

    /// <summary>
    /// Reset period: Weekly, Monthly, Daily
    /// </summary>
    public ResetPeriod ResetPeriod { get; set; } = ResetPeriod.Weekly;

    /// <summary>
    /// ตรวจสอบว่า account นี้สามารถใช้ใหม่ได้หรือยัง
    /// </summary>
    public bool CanReuse()
    {
        if (!IsExhausted || ExhaustedAt == null)
            return true;

        var elapsed = DateTime.Now - ExhaustedAt.Value;
        return ResetPeriod switch
        {
            ResetPeriod.Daily => elapsed.TotalDays >= 1,
            ResetPeriod.Weekly => elapsed.TotalDays >= 7,
            ResetPeriod.Monthly => elapsed.TotalDays >= 30,
            _ => false
        };
    }
}

public enum ResetPeriod
{
    Daily,
    Weekly,
    Monthly
}

public class AccountRotationEventArgs : EventArgs
{
    public string ProviderId { get; set; } = "";
    public GpuAccount? PreviousAccount { get; set; }
    public GpuAccount? NewAccount { get; set; }
    public string? Reason { get; set; }
}

public class AccountPoolStats
{
    public string ProviderId { get; set; } = "";
    public int TotalAccounts { get; set; }
    public int AvailableAccounts { get; set; }
    public int ExhaustedAccounts { get; set; }
    public int CurrentAccountIndex { get; set; }
    public GpuAccount? CurrentAccount { get; set; }
}
