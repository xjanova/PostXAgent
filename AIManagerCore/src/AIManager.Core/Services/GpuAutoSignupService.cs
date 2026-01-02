using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AIManager.Core.Services;

/// <summary>
/// Service สำหรับสมัคร GPU Provider อัตโนมัติ
/// - สร้าง email/password อัตโนมัติ
/// - ตรวจสอบ email verification
/// - สมัครหลายรอบได้
/// - บันทึก credentials ลง pool
/// </summary>
public class GpuAutoSignupService
{
    private readonly ILogger<GpuAutoSignupService>? _logger;
    private readonly ColabGpuPoolService _poolService;
    private readonly GpuWorkflowRecordingService _workflowService;
    private readonly TempEmailService _emailService;
    private readonly CredentialGenerator _credentialGen;
    private readonly CredentialStorageService _credentialStorage;

    private CancellationTokenSource? _signupCts;
    private bool _isRunning;

    // Events
    public event Action<AutoSignupProgress>? OnProgress;
    public event Action<AutoSignupResult>? OnAccountCreated;
    public event Action<string>? OnError;
    public event Action<AutoSignupBatchResult>? OnBatchCompleted;

    public bool IsRunning => _isRunning;

    public GpuAutoSignupService(
        ILogger<GpuAutoSignupService>? logger = null,
        ColabGpuPoolService? poolService = null,
        GpuWorkflowRecordingService? workflowService = null)
    {
        _logger = logger;
        _poolService = poolService ?? new ColabGpuPoolService();
        _workflowService = workflowService ?? new GpuWorkflowRecordingService();
        _emailService = new TempEmailService(logger);
        _credentialGen = new CredentialGenerator();
        _credentialStorage = new CredentialStorageService(logger);
    }

    #region Auto Signup

    /// <summary>
    /// สมัคร GPU Provider อัตโนมัติหลายรอบ
    /// </summary>
    public async Task<AutoSignupBatchResult> StartBatchSignupAsync(
        GpuProviderType provider,
        int accountCount,
        AutoSignupConfig config,
        Func<AutoSignupContext, Task<bool>> executeSignup,
        CancellationToken ct = default)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("Signup is already running");
        }

        _isRunning = true;
        _signupCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var result = new AutoSignupBatchResult
        {
            Provider = provider,
            RequestedCount = accountCount,
            StartedAt = DateTime.UtcNow
        };

        _logger?.LogInformation(
            "Starting batch signup for {Provider}: {Count} accounts",
            provider, accountCount);

        try
        {
            for (int i = 0; i < accountCount; i++)
            {
                if (_signupCts.Token.IsCancellationRequested)
                {
                    _logger?.LogInformation("Batch signup cancelled at account {Index}", i);
                    break;
                }

                ReportProgress(new AutoSignupProgress
                {
                    CurrentAccount = i + 1,
                    TotalAccounts = accountCount,
                    Stage = SignupStage.Preparing,
                    Message = $"Preparing account {i + 1} of {accountCount}..."
                });

                var accountResult = await SignupSingleAccountAsync(
                    provider, config, executeSignup, _signupCts.Token);

                if (accountResult.Success)
                {
                    result.SuccessfulAccounts.Add(accountResult);
                    OnAccountCreated?.Invoke(accountResult);
                }
                else
                {
                    result.FailedAccounts.Add(accountResult);
                }

                // Delay between signups to avoid detection
                if (i < accountCount - 1)
                {
                    var delay = config.DelayBetweenSignups ?? TimeSpan.FromSeconds(30);
                    ReportProgress(new AutoSignupProgress
                    {
                        CurrentAccount = i + 1,
                        TotalAccounts = accountCount,
                        Stage = SignupStage.Waiting,
                        Message = $"Waiting {delay.TotalSeconds}s before next signup..."
                    });
                    await Task.Delay(delay, _signupCts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger?.LogInformation("Batch signup was cancelled");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Batch signup failed");
            OnError?.Invoke(ex.Message);
        }
        finally
        {
            _isRunning = false;
            result.CompletedAt = DateTime.UtcNow;
        }

        OnBatchCompleted?.Invoke(result);
        return result;
    }

    /// <summary>
    /// สมัคร account เดียว
    /// </summary>
    private async Task<AutoSignupResult> SignupSingleAccountAsync(
        GpuProviderType provider,
        AutoSignupConfig config,
        Func<AutoSignupContext, Task<bool>> executeSignup,
        CancellationToken ct)
    {
        var result = new AutoSignupResult
        {
            Provider = provider,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // 1. Generate credentials
            ReportProgress(new AutoSignupProgress
            {
                Stage = SignupStage.GeneratingCredentials,
                Message = "Generating email and password..."
            });

            var credentials = await GenerateCredentialsAsync(provider, config, ct);
            result.Email = credentials.Email;
            result.DisplayName = credentials.DisplayName;

            _logger?.LogInformation("Generated credentials for {Email}", credentials.Email);

            // 2. Create signup context
            var context = new AutoSignupContext
            {
                Provider = provider,
                Email = credentials.Email,
                Password = credentials.Password,
                DisplayName = credentials.DisplayName,
                Config = config,
                GetVerificationCode = async () => await WaitForVerificationCodeAsync(
                    credentials.Email, config.VerificationTimeout, ct),
                GetVerificationLink = async () => await WaitForVerificationLinkAsync(
                    credentials.Email, config.VerificationTimeout, ct)
            };

            // 3. Execute signup workflow
            ReportProgress(new AutoSignupProgress
            {
                Stage = SignupStage.FillingForm,
                Message = "Filling signup form..."
            });

            var signupSuccess = await executeSignup(context);

            if (!signupSuccess)
            {
                result.Success = false;
                result.ErrorMessage = "Signup workflow failed";
                return result;
            }

            // 4. Handle email verification if needed
            if (config.RequiresEmailVerification)
            {
                ReportProgress(new AutoSignupProgress
                {
                    Stage = SignupStage.WaitingVerification,
                    Message = "Waiting for email verification..."
                });

                var verified = await HandleEmailVerificationAsync(
                    credentials.Email, config, ct);

                if (!verified)
                {
                    result.Success = false;
                    result.ErrorMessage = "Email verification failed or timed out";
                    return result;
                }
            }

            // 5. Save to pool
            ReportProgress(new AutoSignupProgress
            {
                Stage = SignupStage.SavingToPool,
                Message = "Saving account to pool..."
            });

            var account = new ColabGpuAccount
            {
                Email = credentials.Email,
                DisplayName = credentials.DisplayName,
                Tier = ColabTier.Free,
                Status = ColabAccountStatus.Active,
                Priority = 100,
                CreatedAt = DateTime.UtcNow
            };

            await _poolService.AddAccountAsync(account);

            // 6. Save credentials securely
            await _credentialStorage.SaveCredentialAsync(new StoredCredential
            {
                Provider = provider,
                Email = credentials.Email,
                EncryptedPassword = _credentialStorage.EncryptPassword(credentials.Password),
                DisplayName = credentials.DisplayName,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            result.Success = true;
            result.AccountId = account.Id;

            _logger?.LogInformation(
                "Successfully created account {Email} for {Provider}",
                credentials.Email, provider);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create account");
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// ยกเลิกการสมัคร
    /// </summary>
    public void CancelSignup()
    {
        _signupCts?.Cancel();
        _isRunning = false;
    }

    #endregion

    #region Credential Generation

    private async Task<GeneratedCredentials> GenerateCredentialsAsync(
        GpuProviderType provider,
        AutoSignupConfig config,
        CancellationToken ct)
    {
        string email;
        string password;
        string displayName;

        // Generate display name
        displayName = _credentialGen.GenerateDisplayName(config.NameStyle);

        // Generate password
        password = _credentialGen.GenerateSecurePassword(
            config.PasswordLength,
            config.PasswordRequirements);

        // Generate or get email
        if (config.UseTemporaryEmail)
        {
            // Use temp email service
            var tempEmail = await _emailService.CreateTemporaryEmailAsync(ct);
            email = tempEmail.Address;
        }
        else if (!string.IsNullOrEmpty(config.EmailDomain))
        {
            // Use custom domain with random prefix
            var prefix = _credentialGen.GenerateEmailPrefix(config.EmailPrefixStyle);
            email = $"{prefix}@{config.EmailDomain}";
        }
        else if (!string.IsNullOrEmpty(config.BaseEmail))
        {
            // Use Gmail+ trick
            var suffix = _credentialGen.GenerateRandomSuffix(8);
            var parts = config.BaseEmail.Split('@');
            email = $"{parts[0]}+{suffix}@{parts[1]}";
        }
        else
        {
            throw new ArgumentException("No email configuration provided");
        }

        return new GeneratedCredentials
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        };
    }

    #endregion

    #region Email Verification

    private async Task<bool> HandleEmailVerificationAsync(
        string email,
        AutoSignupConfig config,
        CancellationToken ct)
    {
        var timeout = config.VerificationTimeout;
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            ct.ThrowIfCancellationRequested();

            // Check for verification email
            var verificationData = await _emailService.CheckForVerificationEmailAsync(
                email, ct);

            if (verificationData != null)
            {
                if (!string.IsNullOrEmpty(verificationData.VerificationLink))
                {
                    // Return the link - caller will handle clicking it
                    return true;
                }
                else if (!string.IsNullOrEmpty(verificationData.VerificationCode))
                {
                    // Return the code - caller will handle entering it
                    return true;
                }
            }

            // Wait before checking again
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        return false;
    }

    private async Task<string?> WaitForVerificationCodeAsync(
        string email,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            ct.ThrowIfCancellationRequested();

            var data = await _emailService.CheckForVerificationEmailAsync(email, ct);
            if (data?.VerificationCode != null)
            {
                return data.VerificationCode;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }

        return null;
    }

    private async Task<string?> WaitForVerificationLinkAsync(
        string email,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            ct.ThrowIfCancellationRequested();

            var data = await _emailService.CheckForVerificationEmailAsync(email, ct);
            if (data?.VerificationLink != null)
            {
                return data.VerificationLink;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        }

        return null;
    }

    #endregion

    #region Helpers

    private void ReportProgress(AutoSignupProgress progress)
    {
        OnProgress?.Invoke(progress);
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Configuration สำหรับ Auto Signup
/// </summary>
public class AutoSignupConfig
{
    // Email settings
    public bool UseTemporaryEmail { get; set; } = true;
    public string? EmailDomain { get; set; }
    public string? BaseEmail { get; set; } // For Gmail+ trick
    public EmailPrefixStyle EmailPrefixStyle { get; set; } = EmailPrefixStyle.Random;

    // Password settings
    public int PasswordLength { get; set; } = 16;
    public PasswordRequirements PasswordRequirements { get; set; } = new();

    // Name settings
    public NameStyle NameStyle { get; set; } = NameStyle.RandomThai;

    // Verification settings
    public bool RequiresEmailVerification { get; set; } = true;
    public TimeSpan VerificationTimeout { get; set; } = TimeSpan.FromMinutes(5);

    // Timing
    public TimeSpan? DelayBetweenSignups { get; set; } = TimeSpan.FromSeconds(30);

    // Proxy settings
    public bool UseProxy { get; set; } = false;
    public List<string> ProxyList { get; set; } = new();
}

public class PasswordRequirements
{
    public bool RequireUppercase { get; set; } = true;
    public bool RequireLowercase { get; set; } = true;
    public bool RequireDigits { get; set; } = true;
    public bool RequireSpecialChars { get; set; } = true;
    public string SpecialChars { get; set; } = "!@#$%^&*";
}

public enum EmailPrefixStyle
{
    Random,
    NameBased,
    NumberBased,
    Mixed
}

public enum NameStyle
{
    RandomThai,
    RandomEnglish,
    Mixed,
    Custom
}

/// <summary>
/// Context สำหรับ signup workflow
/// </summary>
public class AutoSignupContext
{
    public GpuProviderType Provider { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AutoSignupConfig Config { get; set; } = new();

    // Functions to get verification data
    public Func<Task<string?>>? GetVerificationCode { get; set; }
    public Func<Task<string?>>? GetVerificationLink { get; set; }
}

public class GeneratedCredentials
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Progress ของการสมัคร
/// </summary>
public class AutoSignupProgress
{
    public int CurrentAccount { get; set; }
    public int TotalAccounts { get; set; }
    public SignupStage Stage { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProgressPercent => TotalAccounts > 0 ? (CurrentAccount * 100) / TotalAccounts : 0;
}

public enum SignupStage
{
    Preparing,
    GeneratingCredentials,
    FillingForm,
    SubmittingForm,
    WaitingVerification,
    VerifyingEmail,
    SavingToPool,
    Completed,
    Failed,
    Waiting
}

/// <summary>
/// ผลลัพธ์การสมัคร account เดียว
/// </summary>
public class AutoSignupResult
{
    public bool Success { get; set; }
    public GpuProviderType Provider { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AccountId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
}

/// <summary>
/// ผลลัพธ์การสมัครหลาย accounts
/// </summary>
public class AutoSignupBatchResult
{
    public GpuProviderType Provider { get; set; }
    public int RequestedCount { get; set; }
    public List<AutoSignupResult> SuccessfulAccounts { get; set; } = new();
    public List<AutoSignupResult> FailedAccounts { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }

    public int SuccessCount => SuccessfulAccounts.Count;
    public int FailureCount => FailedAccounts.Count;
    public double SuccessRate => RequestedCount > 0 ? (double)SuccessCount / RequestedCount : 0;
    public TimeSpan TotalDuration => CompletedAt - StartedAt;
}

#endregion

#region Credential Generator

/// <summary>
/// Generator สำหรับสร้าง credentials
/// </summary>
public class CredentialGenerator
{
    private static readonly string[] ThaiFirstNames = {
        "สมชาย", "สมหญิง", "วิชัย", "วิภา", "ประสิทธิ์", "ประภา",
        "ชัยวัฒน์", "ชนิดา", "พงศ์พัฒน์", "พิมพ์ใจ", "ธนกฤต", "ธนพร",
        "กิตติ", "กมลา", "อนันต์", "อรุณี", "ภูมิ", "ภัทรา"
    };

    private static readonly string[] ThaiLastNames = {
        "ใจดี", "รักษ์ไทย", "มงคล", "สุขสวัสดิ์", "วัฒนา", "เจริญรัตน์",
        "พิทักษ์", "อนันต์", "ทองดี", "ศรีสุข", "บุญมา", "ดวงดี"
    };

    private static readonly string[] EnglishFirstNames = {
        "James", "Mary", "John", "Patricia", "Robert", "Jennifer",
        "Michael", "Linda", "William", "Elizabeth", "David", "Susan"
    };

    private static readonly string[] EnglishLastNames = {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia",
        "Miller", "Davis", "Rodriguez", "Martinez", "Wilson", "Anderson"
    };

    private readonly Random _random = new();

    public string GenerateDisplayName(NameStyle style)
    {
        return style switch
        {
            NameStyle.RandomThai => GenerateThaiName(),
            NameStyle.RandomEnglish => GenerateEnglishName(),
            NameStyle.Mixed => _random.Next(2) == 0 ? GenerateThaiName() : GenerateEnglishName(),
            _ => GenerateEnglishName()
        };
    }

    private string GenerateThaiName()
    {
        var first = ThaiFirstNames[_random.Next(ThaiFirstNames.Length)];
        var last = ThaiLastNames[_random.Next(ThaiLastNames.Length)];
        return $"{first} {last}";
    }

    private string GenerateEnglishName()
    {
        var first = EnglishFirstNames[_random.Next(EnglishFirstNames.Length)];
        var last = EnglishLastNames[_random.Next(EnglishLastNames.Length)];
        return $"{first} {last}";
    }

    public string GenerateSecurePassword(int length, PasswordRequirements req)
    {
        var chars = new List<char>();

        // Ensure required character types
        if (req.RequireUppercase)
            chars.Add((char)('A' + _random.Next(26)));
        if (req.RequireLowercase)
            chars.Add((char)('a' + _random.Next(26)));
        if (req.RequireDigits)
            chars.Add((char)('0' + _random.Next(10)));
        if (req.RequireSpecialChars && !string.IsNullOrEmpty(req.SpecialChars))
            chars.Add(req.SpecialChars[_random.Next(req.SpecialChars.Length)]);

        // Build character pool
        var pool = new StringBuilder();
        if (req.RequireUppercase) pool.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        if (req.RequireLowercase) pool.Append("abcdefghijklmnopqrstuvwxyz");
        if (req.RequireDigits) pool.Append("0123456789");
        if (req.RequireSpecialChars) pool.Append(req.SpecialChars);

        // Fill remaining length
        while (chars.Count < length)
        {
            chars.Add(pool[_random.Next(pool.Length)]);
        }

        // Shuffle
        return new string(chars.OrderBy(_ => _random.Next()).ToArray());
    }

    public string GenerateEmailPrefix(EmailPrefixStyle style)
    {
        return style switch
        {
            EmailPrefixStyle.Random => GenerateRandomPrefix(),
            EmailPrefixStyle.NameBased => GenerateNameBasedPrefix(),
            EmailPrefixStyle.NumberBased => GenerateNumberBasedPrefix(),
            EmailPrefixStyle.Mixed => GenerateMixedPrefix(),
            _ => GenerateRandomPrefix()
        };
    }

    private string GenerateRandomPrefix()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var length = _random.Next(8, 12);
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[_random.Next(chars.Length)])
            .ToArray());
    }

    private string GenerateNameBasedPrefix()
    {
        var first = EnglishFirstNames[_random.Next(EnglishFirstNames.Length)].ToLower();
        var num = _random.Next(100, 999);
        return $"{first}{num}";
    }

    private string GenerateNumberBasedPrefix()
    {
        var prefix = "user";
        var num = _random.Next(100000, 999999);
        return $"{prefix}{num}";
    }

    private string GenerateMixedPrefix()
    {
        var first = EnglishFirstNames[_random.Next(EnglishFirstNames.Length)].ToLower();
        var suffix = GenerateRandomSuffix(4);
        return $"{first}.{suffix}";
    }

    public string GenerateRandomSuffix(int length)
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Range(0, length)
            .Select(_ => chars[_random.Next(chars.Length)])
            .ToArray());
    }
}

#endregion

#region Temp Email Service

/// <summary>
/// Service สำหรับสร้างและตรวจสอบ temporary email
/// </summary>
public class TempEmailService
{
    private readonly ILogger? _logger;
    private readonly HttpClient _httpClient;

    // รายการ temp email providers
    private static readonly string[] TempEmailDomains = {
        "1secmail.com", "1secmail.org", "1secmail.net",
        "guerrillamail.com", "sharklasers.com"
    };

    public TempEmailService(ILogger? logger = null)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// สร้าง temporary email ใหม่
    /// </summary>
    public async Task<TempEmailAccount> CreateTemporaryEmailAsync(CancellationToken ct = default)
    {
        // ใช้ 1secmail API
        var domain = TempEmailDomains[0];
        var login = GenerateRandomLogin();

        return new TempEmailAccount
        {
            Address = $"{login}@{domain}",
            Login = login,
            Domain = domain,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// ตรวจสอบ email สำหรับ verification
    /// </summary>
    public async Task<EmailVerificationData?> CheckForVerificationEmailAsync(
        string email,
        CancellationToken ct = default)
    {
        try
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return null;

            var login = parts[0];
            var domain = parts[1];

            // ใช้ 1secmail API
            var url = $"https://www.1secmail.com/api/v1/?action=getMessages&login={login}&domain={domain}";
            var response = await _httpClient.GetStringAsync(url, ct);

            // Parse response
            var messages = System.Text.Json.JsonSerializer.Deserialize<List<TempEmailMessage>>(response);

            if (messages == null || messages.Count == 0)
                return null;

            // ตรวจหา verification email
            foreach (var msg in messages)
            {
                // Get full message
                var msgUrl = $"https://www.1secmail.com/api/v1/?action=readMessage&login={login}&domain={domain}&id={msg.id}";
                var msgResponse = await _httpClient.GetStringAsync(msgUrl, ct);
                var fullMsg = System.Text.Json.JsonSerializer.Deserialize<TempEmailFullMessage>(msgResponse);

                if (fullMsg == null) continue;

                var body = fullMsg.body ?? fullMsg.textBody ?? "";

                // หา verification link
                var linkMatch = Regex.Match(body, @"https?://[^\s""<>]+(?:verify|confirm|activate)[^\s""<>]*", RegexOptions.IgnoreCase);
                if (linkMatch.Success)
                {
                    return new EmailVerificationData
                    {
                        VerificationLink = linkMatch.Value
                    };
                }

                // หา verification code (6 digits)
                var codeMatch = Regex.Match(body, @"\b(\d{6})\b");
                if (codeMatch.Success)
                {
                    return new EmailVerificationData
                    {
                        VerificationCode = codeMatch.Groups[1].Value
                    };
                }

                // หา verification code (4-8 digits)
                codeMatch = Regex.Match(body, @"(?:code|รหัส)[:\s]*(\d{4,8})", RegexOptions.IgnoreCase);
                if (codeMatch.Success)
                {
                    return new EmailVerificationData
                    {
                        VerificationCode = codeMatch.Groups[1].Value
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to check email {Email}", email);
            return null;
        }
    }

    private string GenerateRandomLogin()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, 10)
            .Select(_ => chars[random.Next(chars.Length)])
            .ToArray());
    }
}

public class TempEmailAccount
{
    public string Address { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TempEmailMessage
{
    public int id { get; set; }
    public string? from { get; set; }
    public string? subject { get; set; }
    public string? date { get; set; }
}

public class TempEmailFullMessage
{
    public int id { get; set; }
    public string? from { get; set; }
    public string? subject { get; set; }
    public string? body { get; set; }
    public string? textBody { get; set; }
}

public class EmailVerificationData
{
    public string? VerificationLink { get; set; }
    public string? VerificationCode { get; set; }
}

#endregion

#region Credential Storage

/// <summary>
/// Service สำหรับเก็บ credentials อย่างปลอดภัย
/// </summary>
public class CredentialStorageService
{
    private readonly ILogger? _logger;
    private readonly string _storagePath;
    private readonly byte[] _encryptionKey;

    public CredentialStorageService(ILogger? logger = null)
    {
        _logger = logger;
        _storagePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PostXAgent",
            "credentials"
        );
        Directory.CreateDirectory(_storagePath);

        // Generate or load encryption key
        _encryptionKey = GetOrCreateEncryptionKey();
    }

    public async Task SaveCredentialAsync(StoredCredential credential)
    {
        var filePath = Path.Combine(_storagePath, $"{credential.Provider}_{SanitizeEmail(credential.Email)}.json");
        var json = System.Text.Json.JsonSerializer.Serialize(credential);
        await File.WriteAllTextAsync(filePath, json);
        _logger?.LogInformation("Saved credential for {Email}", credential.Email);
    }

    public async Task<StoredCredential?> LoadCredentialAsync(GpuProviderType provider, string email)
    {
        var filePath = Path.Combine(_storagePath, $"{provider}_{SanitizeEmail(email)}.json");
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        return System.Text.Json.JsonSerializer.Deserialize<StoredCredential>(json);
    }

    public async Task<List<StoredCredential>> GetAllCredentialsAsync(GpuProviderType? provider = null)
    {
        var credentials = new List<StoredCredential>();
        var pattern = provider.HasValue ? $"{provider}_*.json" : "*.json";

        foreach (var file in Directory.GetFiles(_storagePath, pattern))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var cred = System.Text.Json.JsonSerializer.Deserialize<StoredCredential>(json);
                if (cred != null) credentials.Add(cred);
            }
            catch { }
        }

        return credentials;
    }

    public string EncryptPassword(string password)
    {
        using var aes = Aes.Create();
        aes.Key = _encryptionKey;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(password);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Combine IV + encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

        return Convert.ToBase64String(result);
    }

    public string DecryptPassword(string encryptedPassword)
    {
        var data = Convert.FromBase64String(encryptedPassword);

        using var aes = Aes.Create();
        aes.Key = _encryptionKey;

        // Extract IV
        var iv = new byte[16];
        Buffer.BlockCopy(data, 0, iv, 0, 16);
        aes.IV = iv;

        // Extract encrypted data
        var encryptedBytes = new byte[data.Length - 16];
        Buffer.BlockCopy(data, 16, encryptedBytes, 0, encryptedBytes.Length);

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private byte[] GetOrCreateEncryptionKey()
    {
        var keyPath = Path.Combine(_storagePath, ".key");
        if (File.Exists(keyPath))
        {
            return Convert.FromBase64String(File.ReadAllText(keyPath));
        }

        var key = new byte[32];
        RandomNumberGenerator.Fill(key);
        File.WriteAllText(keyPath, Convert.ToBase64String(key));
        File.SetAttributes(keyPath, FileAttributes.Hidden);

        return key;
    }

    private string SanitizeEmail(string email)
    {
        return Regex.Replace(email, @"[^a-zA-Z0-9]", "_");
    }
}

public class StoredCredential
{
    public GpuProviderType Provider { get; set; }
    public string Email { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

#endregion
