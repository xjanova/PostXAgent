using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Models;
using AIManager.Core.Services;

namespace AIManager.API.Controllers;

/// <summary>
/// Content Workflow Controller
/// จัดการ workflow สำหรับสร้างเนื้อหาวิดีโอและเผยแพร่
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContentWorkflowController : ControllerBase
{
    private readonly ILogger<ContentWorkflowController> _logger;
    private readonly ContentWorkflowOrchestrator _orchestrator;
    private readonly CloudDriveService _cloudDrive;
    private readonly AudioGeneratorService _audioGenerator;
    private readonly VideoAssemblyService _videoAssembly;

    // In-memory storage for demo (use database in production)
    private static readonly Dictionary<string, UserPackage> _userPackages = new();
    private static readonly Dictionary<string, CloudDriveConnection> _cloudConnections = new();

    public ContentWorkflowController(
        ILogger<ContentWorkflowController> logger,
        ContentWorkflowOrchestrator orchestrator,
        CloudDriveService cloudDrive,
        AudioGeneratorService audioGenerator,
        VideoAssemblyService videoAssembly)
    {
        _logger = logger;
        _orchestrator = orchestrator;
        _cloudDrive = cloudDrive;
        _audioGenerator = audioGenerator;
        _videoAssembly = videoAssembly;
    }

    #region Workflow Endpoints

    /// <summary>
    /// Start a new content workflow
    /// </summary>
    [HttpPost("start")]
    public async Task<ActionResult<object>> StartWorkflow([FromBody] CreateWorkflowRequest request)
    {
        try
        {
            // Get or create user package
            if (!_userPackages.TryGetValue(request.UserId, out var package))
            {
                package = PackageDefinitions.CreateFreePackage(request.UserId);
                _userPackages[request.UserId] = package;
            }

            // Check quota
            var quotaCheck = _orchestrator.CheckQuota(package, request.Concept, request.PublishTargets);
            if (!quotaCheck.CanCreate)
            {
                return BadRequest(new
                {
                    success = false,
                    error = quotaCheck.Reason,
                    quota = quotaCheck
                });
            }

            // Get cloud drive connection if available
            _cloudConnections.TryGetValue(request.UserId, out var cloudDrive);

            // Start workflow
            var workflow = await _orchestrator.StartWorkflowAsync(request, package, cloudDrive);

            // Increment usage
            package.VideosCreatedThisMonth++;

            return Ok(new
            {
                success = true,
                workflowId = workflow.Id,
                status = workflow.Status.ToString(),
                message = "Workflow started successfully",
                quota = new
                {
                    remaining = package.RemainingVideos,
                    maxDuration = package.MaxVideoDurationSeconds,
                    hasWatermark = package.HasWatermark
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start workflow");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get workflow status
    /// </summary>
    [HttpGet("status/{workflowId}")]
    public ActionResult<object> GetWorkflowStatus(string workflowId)
    {
        var status = _orchestrator.GetWorkflowStatus(workflowId);
        if (status == null)
        {
            return NotFound(new { success = false, error = "Workflow not found" });
        }

        return Ok(new
        {
            success = true,
            data = status
        });
    }

    /// <summary>
    /// Cancel workflow
    /// </summary>
    [HttpPost("cancel/{workflowId}")]
    public ActionResult<object> CancelWorkflow(string workflowId)
    {
        var cancelled = _orchestrator.CancelWorkflow(workflowId);
        return Ok(new
        {
            success = cancelled,
            message = cancelled ? "Workflow cancelled" : "Workflow not found or already completed"
        });
    }

    /// <summary>
    /// Get active workflows for user
    /// </summary>
    [HttpGet("active/{userId}")]
    public ActionResult<object> GetActiveWorkflows(string userId)
    {
        var workflows = _orchestrator.GetActiveWorkflows(userId);
        return Ok(new
        {
            success = true,
            count = workflows.Count,
            workflows = workflows.Select(w => new
            {
                id = w.Id,
                title = w.Title,
                status = w.Status.ToString(),
                progress = w.Progress.OverallPercentage,
                currentStep = w.Progress.CurrentStep,
                createdAt = w.CreatedAt
            })
        });
    }

    /// <summary>
    /// Check quota for user
    /// </summary>
    [HttpPost("check-quota")]
    public ActionResult<QuotaCheckResponse> CheckQuota([FromBody] QuotaCheckRequest request)
    {
        if (!_userPackages.TryGetValue(request.UserId, out var package))
        {
            package = PackageDefinitions.CreateFreePackage(request.UserId);
            _userPackages[request.UserId] = package;
        }

        var result = _orchestrator.CheckQuota(package, request.Concept, request.PublishTargets);
        return Ok(result);
    }

    #endregion

    #region Package Endpoints

    /// <summary>
    /// Get user package info
    /// </summary>
    [HttpGet("package/{userId}")]
    public ActionResult<object> GetUserPackage(string userId)
    {
        if (!_userPackages.TryGetValue(userId, out var package))
        {
            package = PackageDefinitions.CreateFreePackage(userId);
            _userPackages[userId] = package;
        }

        return Ok(new
        {
            success = true,
            package = new
            {
                name = package.PackageName,
                tier = package.Tier.ToString(),
                videosCreated = package.VideosCreatedThisMonth,
                maxVideos = package.MaxVideosPerMonth,
                remaining = package.RemainingVideos,
                maxDuration = package.MaxVideoDurationSeconds,
                maxImages = package.MaxImagesPerVideo,
                maxPlatforms = package.MaxPlatforms,
                allowedPlatforms = package.AllowedPlatforms.Select(p => p.ToString()),
                hasWatermark = package.HasWatermark,
                canUsePremiumVoices = package.CanUsePremiumVoices,
                storageUsed = package.StorageUsedMB,
                storageLimit = package.CloudStorageMB,
                resetDate = package.ResetDate,
                expiresAt = package.ExpiresAt
            }
        });
    }

    /// <summary>
    /// Get available packages
    /// </summary>
    [HttpGet("packages")]
    public ActionResult<object> GetAvailablePackages()
    {
        return Ok(new
        {
            success = true,
            packages = new[]
            {
                new
                {
                    name = "Free",
                    tier = "Free",
                    price = 0,
                    videosPerMonth = 5,
                    maxDurationSeconds = 60,
                    maxPlatforms = 2,
                    hasWatermark = true,
                    premiumVoices = false,
                    storageGB = 0.5
                },
                new
                {
                    name = "Starter",
                    tier = "Starter",
                    price = 299,
                    videosPerMonth = 20,
                    maxDurationSeconds = 180,
                    maxPlatforms = 4,
                    hasWatermark = false,
                    premiumVoices = true,
                    storageGB = 2.0
                },
                new
                {
                    name = "Professional",
                    tier = "Professional",
                    price = 799,
                    videosPerMonth = 50,
                    maxDurationSeconds = 600,
                    maxPlatforms = 6,
                    hasWatermark = false,
                    premiumVoices = true,
                    storageGB = 10.0
                },
                new
                {
                    name = "Business",
                    tier = "Business",
                    price = 1999,
                    videosPerMonth = 200,
                    maxDurationSeconds = 1800,
                    maxPlatforms = 9,
                    hasWatermark = false,
                    premiumVoices = true,
                    storageGB = 50.0
                }
            }
        });
    }

    /// <summary>
    /// Upgrade package (demo)
    /// </summary>
    [HttpPost("package/upgrade")]
    public ActionResult<object> UpgradePackage([FromBody] UpgradePackageRequest request)
    {
        var package = request.PackageTier.ToLower() switch
        {
            "starter" => PackageDefinitions.CreateStarterPackage(request.UserId),
            "professional" => PackageDefinitions.CreateProfessionalPackage(request.UserId),
            "business" => PackageDefinitions.CreateBusinessPackage(request.UserId),
            _ => PackageDefinitions.CreateFreePackage(request.UserId)
        };

        _userPackages[request.UserId] = package;

        return Ok(new
        {
            success = true,
            message = $"Upgraded to {package.PackageName} package",
            package = new
            {
                name = package.PackageName,
                tier = package.Tier.ToString(),
                maxVideos = package.MaxVideosPerMonth,
                maxDuration = package.MaxVideoDurationSeconds
            }
        });
    }

    #endregion

    #region Cloud Drive Endpoints

    /// <summary>
    /// Get cloud drive setup instructions
    /// </summary>
    [HttpGet("cloud-drive/setup/{driveType}")]
    public ActionResult<object> GetCloudDriveSetup(string driveType, [FromQuery] string language = "th")
    {
        if (!Enum.TryParse<CloudDriveType>(driveType, true, out var type))
        {
            return BadRequest(new { success = false, error = "Invalid drive type" });
        }

        var instructions = CloudDriveService.GetSetupInstructions(type, language);
        return Ok(new
        {
            success = true,
            data = instructions
        });
    }

    /// <summary>
    /// Get OAuth URL for cloud drive
    /// </summary>
    [HttpPost("cloud-drive/auth-url")]
    public ActionResult<object> GetCloudDriveAuthUrl([FromBody] CloudDriveAuthRequest request)
    {
        try
        {
            var url = _cloudDrive.GetAuthorizationUrl(
                request.DriveType,
                request.ClientId,
                request.RedirectUri,
                request.UserId);

            return Ok(new
            {
                success = true,
                authUrl = url
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Complete OAuth callback
    /// </summary>
    [HttpPost("cloud-drive/callback")]
    public async Task<ActionResult<object>> CloudDriveCallback([FromBody] CloudDriveCallbackRequest request)
    {
        try
        {
            var connection = await _cloudDrive.ExchangeCodeForTokensAsync(
                request.DriveType,
                request.Code,
                request.ClientId,
                request.ClientSecret,
                request.RedirectUri,
                request.UserId);

            _cloudConnections[request.UserId] = connection;

            return Ok(new
            {
                success = true,
                message = "Cloud drive connected successfully",
                connection = new
                {
                    driveType = connection.DriveType.ToString(),
                    email = connection.Email,
                    folderName = connection.FolderName,
                    isConnected = connection.IsConnected
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud drive callback failed");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Get cloud drive connection status
    /// </summary>
    [HttpGet("cloud-drive/status/{userId}")]
    public ActionResult<object> GetCloudDriveStatus(string userId)
    {
        if (_cloudConnections.TryGetValue(userId, out var connection))
        {
            return Ok(new
            {
                success = true,
                isConnected = connection.IsConnected,
                driveType = connection.DriveType.ToString(),
                email = connection.Email,
                folderName = connection.FolderName,
                needsRefresh = connection.NeedsRefresh
            });
        }

        return Ok(new
        {
            success = true,
            isConnected = false
        });
    }

    /// <summary>
    /// List files in cloud drive
    /// </summary>
    [HttpGet("cloud-drive/files/{userId}")]
    public async Task<ActionResult<object>> ListCloudDriveFiles(string userId)
    {
        if (!_cloudConnections.TryGetValue(userId, out var connection))
        {
            return BadRequest(new { success = false, error = "Cloud drive not connected" });
        }

        try
        {
            var files = await _cloudDrive.ListFilesAsync(connection);
            return Ok(new
            {
                success = true,
                count = files.Count,
                files = files.Select(f => new
                {
                    id = f.Id,
                    name = f.Name,
                    mimeType = f.MimeType,
                    size = f.SizeBytes,
                    url = f.DownloadUrl
                })
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    #endregion

    #region Voice & Audio Endpoints

    /// <summary>
    /// Get available voices
    /// </summary>
    [HttpGet("voices")]
    public ActionResult<object> GetAvailableVoices([FromQuery] string language = "th", [FromQuery] bool includePremium = false)
    {
        var voices = _audioGenerator.GetAvailableVoices(language, includePremium);
        return Ok(new
        {
            success = true,
            count = voices.Count,
            voices = voices.Select(v => new
            {
                id = v.Id,
                name = v.Name,
                gender = v.Gender,
                provider = v.Provider,
                isFree = v.IsFree,
                isPremium = v.IsPremium
            })
        });
    }

    /// <summary>
    /// Get available background music
    /// </summary>
    [HttpGet("music")]
    public ActionResult<object> GetAvailableMusic()
    {
        return Ok(new
        {
            success = true,
            music = AudioGeneratorService.AvailableMusic.Select(m => new
            {
                id = m.Id,
                name = m.Name,
                mood = m.Mood,
                bpm = m.BPM
            })
        });
    }

    #endregion

    #region System Check Endpoints

    /// <summary>
    /// Check system requirements
    /// </summary>
    [HttpGet("system-check")]
    public ActionResult<object> CheckSystemRequirements()
    {
        var ffmpegAvailable = VideoAssemblyService.IsFfmpegAvailable();

        return Ok(new
        {
            success = true,
            requirements = new
            {
                ffmpeg = new
                {
                    available = ffmpegAvailable,
                    required = true,
                    installInstructions = ffmpegAvailable ? null : VideoAssemblyService.GetFfmpegInstallInstructions()
                }
            },
            allMet = ffmpegAvailable
        });
    }

    #endregion
}

#region Request Models

public class QuotaCheckRequest
{
    public string UserId { get; set; } = "";
    public ContentConcept Concept { get; set; } = new();
    public List<PublishTarget> PublishTargets { get; set; } = new();
}

public class UpgradePackageRequest
{
    public string UserId { get; set; } = "";
    public string PackageTier { get; set; } = "";
}

public class CloudDriveAuthRequest
{
    public CloudDriveType DriveType { get; set; }
    public string ClientId { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string UserId { get; set; } = "";
}

public class CloudDriveCallbackRequest
{
    public CloudDriveType DriveType { get; set; }
    public string Code { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
    public string UserId { get; set; } = "";
}

#endregion
