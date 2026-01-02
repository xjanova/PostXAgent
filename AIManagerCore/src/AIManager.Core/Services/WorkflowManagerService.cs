using System.Net.Http.Json;
using System.Text.Json;
using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Service สำหรับจัดการ Workflows ทั้งหมดในระบบ
/// </summary>
public class WorkflowManagerService
{
    private readonly ILogger<WorkflowManagerService>? _logger;
    private readonly WorkflowStorage _workflowStorage;
    private readonly HttpClient _httpClient;
    private string _downloadFolder;
    private readonly string _settingsPath;

    public WorkflowManagerService(
        WorkflowStorage workflowStorage,
        ILogger<WorkflowManagerService>? logger = null)
    {
        _workflowStorage = workflowStorage;
        _logger = logger;
        _httpClient = new HttpClient();

        // Default download folder
        _downloadFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "PostXAgent", "Downloads");

        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PostXAgent", "workflow_settings.json");

        LoadSettings();
    }

    #region Platform Definitions

    /// <summary>
    /// ดึงรายการ Platforms ทั้งหมดพร้อมข้อมูล Workflow
    /// </summary>
    public async Task<List<PlatformWorkflowInfo>> GetAllPlatformsWithWorkflowsAsync()
    {
        var platforms = GetAllPlatformDefinitions();

        // Load workflow counts for each platform
        foreach (var platform in platforms)
        {
            var workflows = await _workflowStorage.GetWorkflowsForPlatformAsync(
                platform.PlatformType.ToString());

            platform.WorkflowCount = workflows.Count;
            platform.WorkflowIds = workflows.Select(w => w.Id).ToList();

            // Check for specific workflow types
            platform.HasSignupWorkflow = workflows.Any(w =>
                w.Name.Contains("signup", StringComparison.OrdinalIgnoreCase));
            platform.HasLoginWorkflow = workflows.Any(w =>
                w.Name.Contains("login", StringComparison.OrdinalIgnoreCase));
            platform.HasPrimaryWorkflow = workflows.Any(w =>
                w.Name.Contains(platform.PrimaryActionTag, StringComparison.OrdinalIgnoreCase));
        }

        return platforms;
    }

    private static List<PlatformWorkflowInfo> GetAllPlatformDefinitions()
    {
        return new List<PlatformWorkflowInfo>
        {
            // Social Media Platforms
            new()
            {
                PlatformType = WorkflowPlatformType.Facebook,
                Name = "Facebook",
                Description = "Social media & advertising",
                IconKind = "Facebook",
                IconColorHex = "#1877F2",
                IconBackgroundHex = "#301877F2",
                PrimaryAction = "Post",
                PrimaryActionTag = "Post",
                SupportsOllama = true,
                StartUrl = "https://www.facebook.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.Google,
                Name = "Google",
                Description = "Google account services",
                IconKind = "Google",
                IconColorHex = "#4285F4",
                IconBackgroundHex = "#304285F4",
                PrimaryAction = "Login",
                PrimaryActionTag = "Login",
                SupportsOllama = false,
                StartUrl = "https://accounts.google.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.TikTok,
                Name = "TikTok",
                Description = "Short-form video platform",
                IconKind = "MusicNote",
                IconColorHex = "#FF0050",
                IconBackgroundHex = "#30FF0050",
                PrimaryAction = "Post",
                PrimaryActionTag = "Post",
                SupportsOllama = true,
                StartUrl = "https://www.tiktok.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.Instagram,
                Name = "Instagram",
                Description = "Photo & video sharing",
                IconKind = "Instagram",
                IconColorHex = "#E4405F",
                IconBackgroundHex = "#30E4405F",
                PrimaryAction = "Post",
                PrimaryActionTag = "Post",
                SupportsOllama = true,
                StartUrl = "https://www.instagram.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.YouTube,
                Name = "YouTube",
                Description = "Video platform",
                IconKind = "Youtube",
                IconColorHex = "#FF0000",
                IconBackgroundHex = "#30FF0000",
                PrimaryAction = "Upload",
                PrimaryActionTag = "Upload",
                SupportsOllama = true,
                StartUrl = "https://www.youtube.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.Twitter,
                Name = "Twitter/X",
                Description = "Microblogging platform",
                IconKind = "Twitter",
                IconColorHex = "#1DA1F2",
                IconBackgroundHex = "#301DA1F2",
                PrimaryAction = "Post",
                PrimaryActionTag = "Post",
                SupportsOllama = true,
                StartUrl = "https://twitter.com"
            },

            // AI Video Platforms
            new()
            {
                PlatformType = WorkflowPlatformType.Freepik,
                Name = "Freepik Pikaso",
                Description = "AI Video Generation",
                IconKind = "Video",
                IconColorHex = "#0066FF",
                IconBackgroundHex = "#300066FF",
                PrimaryAction = "Create Video",
                PrimaryActionTag = "CreateVideo",
                SupportsOllama = true,
                StartUrl = "https://www.freepik.com/pikaso/ai-video"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.Runway,
                Name = "Runway ML",
                Description = "AI Video & Image",
                IconKind = "MovieRoll",
                IconColorHex = "#8B5CF6",
                IconBackgroundHex = "#308B5CF6",
                PrimaryAction = "Create Video",
                PrimaryActionTag = "CreateVideo",
                SupportsOllama = true,
                StartUrl = "https://runwayml.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.PikaLabs,
                Name = "Pika Labs",
                Description = "AI Video Generator",
                IconKind = "Animation",
                IconColorHex = "#EC4899",
                IconBackgroundHex = "#30EC4899",
                PrimaryAction = "Create Video",
                PrimaryActionTag = "CreateVideo",
                SupportsOllama = true,
                StartUrl = "https://pika.art"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.LumaAI,
                Name = "Luma AI",
                Description = "Dream Machine Video",
                IconKind = "Cube",
                IconColorHex = "#06B6D4",
                IconBackgroundHex = "#3006B6D4",
                PrimaryAction = "Create Video",
                PrimaryActionTag = "CreateVideo",
                SupportsOllama = true,
                StartUrl = "https://lumalabs.ai/dream-machine"
            },

            // AI Music Platforms
            new()
            {
                PlatformType = WorkflowPlatformType.SunoAI,
                Name = "Suno AI",
                Description = "AI Music Generation",
                IconKind = "MusicNote",
                IconColorHex = "#F59E0B",
                IconBackgroundHex = "#30F59E0B",
                PrimaryAction = "Create Music",
                PrimaryActionTag = "CreateMusic",
                SupportsOllama = true,
                StartUrl = "https://suno.ai"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.StableAudio,
                Name = "Stable Audio",
                Description = "AI Music & Sound",
                IconKind = "Waveform",
                IconColorHex = "#10B981",
                IconBackgroundHex = "#3010B981",
                PrimaryAction = "Create Music",
                PrimaryActionTag = "CreateMusic",
                SupportsOllama = true,
                StartUrl = "https://stableaudio.com"
            },

            // AI Image Platforms
            new()
            {
                PlatformType = WorkflowPlatformType.Leonardo,
                Name = "Leonardo AI",
                Description = "AI Image Generation",
                IconKind = "ImageMultiple",
                IconColorHex = "#8B5CF6",
                IconBackgroundHex = "#308B5CF6",
                PrimaryAction = "Create Image",
                PrimaryActionTag = "CreateImage",
                SupportsOllama = true,
                StartUrl = "https://leonardo.ai"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.MidJourney,
                Name = "MidJourney",
                Description = "AI Art Generator",
                IconKind = "Palette",
                IconColorHex = "#EC4899",
                IconBackgroundHex = "#30EC4899",
                PrimaryAction = "Create Image",
                PrimaryActionTag = "CreateImage",
                SupportsOllama = true,
                StartUrl = "https://www.midjourney.com"
            },

            // GPU Providers
            new()
            {
                PlatformType = WorkflowPlatformType.GoogleColab,
                Name = "Google Colab",
                Description = "Free GPU Compute",
                IconKind = "GoogleCloud",
                IconColorHex = "#4285F4",
                IconBackgroundHex = "#304285F4",
                PrimaryAction = "Run Notebook",
                PrimaryActionTag = "RunNotebook",
                SupportsOllama = false,
                StartUrl = "https://colab.research.google.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.Kaggle,
                Name = "Kaggle",
                Description = "Free GPU Notebooks",
                IconKind = "ChartLine",
                IconColorHex = "#20BEFF",
                IconBackgroundHex = "#3020BEFF",
                PrimaryAction = "Run Notebook",
                PrimaryActionTag = "RunNotebook",
                SupportsOllama = false,
                StartUrl = "https://www.kaggle.com"
            },
            new()
            {
                PlatformType = WorkflowPlatformType.HuggingFace,
                Name = "Hugging Face",
                Description = "AI Model Hub & Spaces",
                IconKind = "RobotHappy",
                IconColorHex = "#FFD21E",
                IconBackgroundHex = "#30FFD21E",
                PrimaryAction = "Run Space",
                PrimaryActionTag = "RunSpace",
                SupportsOllama = false,
                StartUrl = "https://huggingface.co"
            },

            // Custom Platform
            new()
            {
                PlatformType = WorkflowPlatformType.Custom,
                Name = "Custom",
                Description = "เพิ่ม Platform ใหม่",
                IconKind = "Plus",
                IconColorHex = "#999999",
                IconBackgroundHex = "#30999999",
                PrimaryAction = "Add",
                PrimaryActionTag = "Add",
                SupportsOllama = true,
                StartUrl = ""
            }
        };
    }

    #endregion

    #region Download Folder Management

    public string? GetDownloadFolder() => _downloadFolder;

    public void SetDownloadFolder(string path)
    {
        _downloadFolder = path;
        SaveSettings();

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    #endregion

    #region Ollama Integration

    /// <summary>
    /// ตรวจสอบสถานะ Ollama
    /// </summary>
    public async Task<bool> CheckOllamaStatusAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("http://localhost:11434/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// สร้าง Prompt ด้วย Ollama
    /// </summary>
    public async Task<string?> GeneratePromptWithOllamaAsync(
        string model,
        string context,
        string workflowType)
    {
        try
        {
            var systemPrompt = GetSystemPromptForWorkflowType(workflowType);
            var request = new
            {
                model,
                prompt = $"{systemPrompt}\n\nUser context: {context}",
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync(
                "http://localhost:11434/api/generate", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                return result?.Response;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate prompt with Ollama");
        }

        return null;
    }

    private static string GetSystemPromptForWorkflowType(string workflowType)
    {
        return workflowType switch
        {
            "CreateVideo" => "You are a creative video prompt generator. Generate a detailed, creative prompt for AI video generation based on the user's context. Focus on visual elements, motion, camera angles, and mood.",
            "CreateMusic" => "You are a music prompt generator. Generate a detailed prompt for AI music generation including genre, mood, instruments, tempo, and style.",
            "CreateImage" => "You are an image prompt generator. Generate a detailed prompt for AI image generation including subject, style, lighting, colors, and composition.",
            "Post" => "You are a social media content creator. Generate engaging post content based on the user's context. Include relevant hashtags and call-to-action.",
            _ => "Generate appropriate content based on the user's context."
        };
    }

    #endregion

    #region Workflow Execution

    /// <summary>
    /// รัน Workflow ตาม Options ที่กำหนด
    /// </summary>
    public async Task<WorkflowRunResult> ExecuteWorkflowAsync(WorkflowRunOptions options)
    {
        var startTime = DateTime.UtcNow;
        var result = new WorkflowRunResult();

        try
        {
            // Find workflow for platform and type
            var workflow = await FindWorkflowAsync(options.Platform, options.WorkflowType);

            if (workflow == null)
            {
                result.Success = false;
                result.Message = $"No workflow found for {options.Platform} - {options.WorkflowType}";
                return result;
            }

            result.WorkflowId = workflow.Id;
            result.Logs.Add($"Starting workflow: {workflow.Name}");

            // Execute workflow steps
            // Note: In real implementation, this would use BrowserController/WorkflowExecutor
            _logger?.LogInformation("Executing workflow {WorkflowId} for {Platform}",
                workflow.Id, options.Platform);

            // Simulate execution for now
            await Task.Delay(1000);

            result.Success = true;
            result.Message = "Workflow completed successfully";
            result.Duration = DateTime.UtcNow - startTime;

            // Handle auto-download if enabled
            if (options.AutoDownload && !string.IsNullOrEmpty(options.DownloadFolder))
            {
                result.OutputPath = Path.Combine(options.DownloadFolder,
                    $"{options.Platform}_{DateTime.Now:yyyyMMdd_HHmmss}");
                result.Logs.Add($"Output saved to: {result.OutputPath}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Workflow execution failed");
            result.Success = false;
            result.Message = ex.Message;
            result.Duration = DateTime.UtcNow - startTime;
        }

        return result;
    }

    private async Task<LearnedWorkflow?> FindWorkflowAsync(
        WorkflowPlatformType platform, string workflowType)
    {
        var workflows = await _workflowStorage.GetWorkflowsForPlatformAsync(platform.ToString());

        return workflows.FirstOrDefault(w =>
            w.Name.Contains(workflowType, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Export/Import

    /// <summary>
    /// Export ทุก Workflow
    /// </summary>
    public async Task ExportAllWorkflowsAsync(string folderPath)
    {
        var workflows = await _workflowStorage.GetAllWorkflowsAsync();

        foreach (var workflow in workflows)
        {
            var template = new WorkflowExportTemplate
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description ?? string.Empty,
                Platform = ParsePlatformType(workflow.Platform),
                WorkflowJson = await _workflowStorage.ExportWorkflowAsync(workflow.Id),
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                IsCompatibleWithMyPostXAgent = true
            };

            var fileName = $"{workflow.Platform}_{workflow.Name.Replace(" ", "_")}.json";
            var filePath = Path.Combine(folderPath, fileName);
            var json = JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
    }

    /// <summary>
    /// Export Workflows สำหรับ Platform เดียว
    /// </summary>
    public async Task ExportPlatformWorkflowsAsync(WorkflowPlatformType platform, string filePath)
    {
        var workflows = await _workflowStorage.GetWorkflowsForPlatformAsync(platform.ToString());
        var templates = new List<WorkflowExportTemplate>();

        foreach (var workflow in workflows)
        {
            templates.Add(new WorkflowExportTemplate
            {
                Id = workflow.Id,
                Name = workflow.Name,
                Description = workflow.Description ?? string.Empty,
                Platform = platform,
                WorkflowJson = await _workflowStorage.ExportWorkflowAsync(workflow.Id),
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                IsCompatibleWithMyPostXAgent = true
            });
        }

        var json = JsonSerializer.Serialize(templates, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    private static WorkflowPlatformType ParsePlatformType(string platform)
    {
        if (Enum.TryParse<WorkflowPlatformType>(platform, true, out var result))
            return result;

        return WorkflowPlatformType.Custom;
    }

    #endregion

    #region Settings

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                var settings = JsonSerializer.Deserialize<WorkflowManagerSettings>(json);

                if (settings != null)
                {
                    _downloadFolder = settings.DownloadFolder;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load workflow manager settings");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new WorkflowManagerSettings
            {
                DownloadFolder = _downloadFolder
            };

            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save workflow manager settings");
        }
    }

    private class WorkflowManagerSettings
    {
        public string DownloadFolder { get; set; } = string.Empty;
    }

    private class OllamaResponse
    {
        public string? Response { get; set; }
    }

    #endregion
}
