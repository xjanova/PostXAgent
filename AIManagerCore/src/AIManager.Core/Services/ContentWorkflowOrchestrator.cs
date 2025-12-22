using AIManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

/// <summary>
/// Content Workflow Orchestrator
/// ประสานงานทุก service เพื่อสร้างวิดีโอจาก concept
/// </summary>
public class ContentWorkflowOrchestrator
{
    private readonly VideoScriptGeneratorService _scriptGenerator;
    private readonly ImageGeneratorService _imageGenerator;
    private readonly AudioGeneratorService _audioGenerator;
    private readonly VideoAssemblyService _videoAssembly;
    private readonly CloudDriveService _cloudDrive;
    private readonly PostPublisherService _publisher;
    private readonly ILogger<ContentWorkflowOrchestrator>? _logger;

    // Active workflows
    private readonly Dictionary<string, ContentWorkflow> _activeWorkflows = new();
    private readonly Dictionary<string, CancellationTokenSource> _workflowCancellations = new();

    // Events
    public event Action<string, WorkflowProgress>? OnProgressUpdated;
    public event Action<string, ContentWorkflowStatus, string?>? OnStatusChanged;

    public ContentWorkflowOrchestrator(
        VideoScriptGeneratorService scriptGenerator,
        ImageGeneratorService imageGenerator,
        AudioGeneratorService audioGenerator,
        VideoAssemblyService videoAssembly,
        CloudDriveService cloudDrive,
        PostPublisherService publisher,
        ILogger<ContentWorkflowOrchestrator>? logger = null)
    {
        _scriptGenerator = scriptGenerator;
        _imageGenerator = imageGenerator;
        _audioGenerator = audioGenerator;
        _videoAssembly = videoAssembly;
        _cloudDrive = cloudDrive;
        _publisher = publisher;
        _logger = logger;
    }

    #region Workflow Management

    /// <summary>
    /// Start a new content workflow
    /// </summary>
    public async Task<ContentWorkflow> StartWorkflowAsync(
        CreateWorkflowRequest request,
        UserPackage package,
        CloudDriveConnection? cloudDrive = null,
        CancellationToken ct = default)
    {
        // Validate package quota
        if (!package.CanCreateVideo())
        {
            throw new InvalidOperationException("Package quota exceeded or expired");
        }

        // Validate platforms
        var invalidPlatforms = request.PublishTargets
            .Where(t => !package.AllowedPlatforms.Contains(t.Platform))
            .Select(t => t.Platform)
            .ToList();

        if (invalidPlatforms.Any())
        {
            throw new InvalidOperationException($"Platforms not allowed in package: {string.Join(", ", invalidPlatforms)}");
        }

        // Create workflow
        var workflow = new ContentWorkflow
        {
            UserId = request.UserId,
            Title = request.Concept.Topic,
            Concept = request.Concept,
            PublishTargets = request.PublishTargets,
            Status = ContentWorkflowStatus.Pending
        };

        workflow.Progress.StartedAt = DateTime.UtcNow;

        _activeWorkflows[workflow.Id] = workflow;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _workflowCancellations[workflow.Id] = cts;

        _logger?.LogInformation("Starting workflow {WorkflowId} for user {UserId}", workflow.Id, request.UserId);

        // Start workflow execution in background
        _ = ExecuteWorkflowAsync(workflow, package, cloudDrive, request.AutoPublish, cts.Token);

        return workflow;
    }

    /// <summary>
    /// Execute the complete workflow
    /// </summary>
    private async Task ExecuteWorkflowAsync(
        ContentWorkflow workflow,
        UserPackage package,
        CloudDriveConnection? cloudDrive,
        bool autoPublish,
        CancellationToken ct)
    {
        try
        {
            // Step 1: Generate Script
            await ExecuteStep(workflow, "Content Generation", async () =>
            {
                UpdateStatus(workflow, ContentWorkflowStatus.GeneratingContent);
                workflow.Progress.UpdateStep("Content Generation", 10, "Generating video script...");
                NotifyProgress(workflow);

                workflow.GeneratedScript = await _scriptGenerator.GenerateScriptAsync(
                    workflow.Concept, package, ct);

                workflow.Progress.UpdateStep("Content Generation", 100, $"Script generated with {workflow.GeneratedScript.Scenes.Count} scenes");
                NotifyProgress(workflow);
            }, ct);

            // Step 2: Generate Images
            await ExecuteStep(workflow, "Image Generation", async () =>
            {
                UpdateStatus(workflow, ContentWorkflowStatus.GeneratingImages);

                var totalScenes = workflow.GeneratedScript!.Scenes.Count;
                var processedScenes = 0;

                foreach (var scene in workflow.GeneratedScript!.Scenes)
                {
                    ct.ThrowIfCancellationRequested();

                    var progress = (int)((processedScenes / (double)totalScenes) * 100);
                    workflow.Progress.UpdateStep("Image Generation", progress, $"Generating image {processedScenes + 1}/{totalScenes}...");
                    NotifyProgress(workflow);

                    try
                    {
                        var imageResult = await _imageGenerator.GenerateAsync(
                            scene.ImagePrompt,
                            new ImageGenerationOptions
                            {
                                Width = 1920,
                                Height = 1080,
                                Style = workflow.Concept.VideoStyle.ToString()
                            },
                            ct);

                        // Save image to local file
                        var tempDir = Path.Combine(Path.GetTempPath(), "aimanager_images", workflow.Id);
                        var imagePath = await imageResult.SaveToFileAsync(tempDir, $"scene_{scene.SceneNumber}.png");

                        workflow.GeneratedImages.Add(new Models.GeneratedImage
                        {
                            SceneNumber = scene.SceneNumber,
                            Prompt = scene.ImagePrompt,
                            LocalPath = imagePath,
                            Width = 1920,
                            Height = 1080,
                            Provider = imageResult.Provider
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to generate image for scene {SceneNumber}", scene.SceneNumber);
                        // Continue with placeholder or skip
                    }

                    processedScenes++;
                }

                workflow.Progress.CompleteStep("Image Generation");
                NotifyProgress(workflow);
            }, ct);

            // Step 3: Generate Audio
            await ExecuteStep(workflow, "Audio Generation", async () =>
            {
                UpdateStatus(workflow, ContentWorkflowStatus.GeneratingAudio);
                workflow.Progress.UpdateStep("Audio Generation", 10, "Generating voice narration...");
                NotifyProgress(workflow);

                // Get default voice
                var voices = _audioGenerator.GetAvailableVoices(workflow.Concept.Language, package.CanUsePremiumVoices);
                var voice = voices.FirstOrDefault(v => v.IsFree) ?? voices.First();

                workflow.GeneratedAudio = await _audioGenerator.GenerateFromScriptAsync(
                    workflow.GeneratedScript!, voice.Id, package, ct);

                workflow.Progress.CompleteStep("Audio Generation");
                NotifyProgress(workflow);
            }, ct);

            // Step 4: Upload to Cloud Drive
            if (cloudDrive != null && cloudDrive.IsConnected)
            {
                await ExecuteStep(workflow, "Cloud Upload", async () =>
                {
                    UpdateStatus(workflow, ContentWorkflowStatus.UploadingToDrive);
                    workflow.Progress.UpdateStep("Cloud Upload", 10, "Creating folder on cloud drive...");
                    NotifyProgress(workflow);

                    // Upload images
                    var totalFiles = workflow.GeneratedImages.Count + 1; // +1 for audio
                    var uploadedFiles = 0;

                    foreach (var image in workflow.GeneratedImages)
                    {
                        if (image.LocalPath != null && File.Exists(image.LocalPath))
                        {
                            var file = await _cloudDrive.UploadFileAsync(
                                cloudDrive,
                                image.LocalPath,
                                $"scene_{image.SceneNumber}.png",
                                null, ct);

                            image.CloudDriveFileId = file.Id;
                            image.CloudDriveUrl = file.DownloadUrl;
                            workflow.CloudDriveFiles.Add(file);
                        }

                        uploadedFiles++;
                        var progress = (int)((uploadedFiles / (double)totalFiles) * 100);
                        workflow.Progress.UpdateStep("Cloud Upload", progress, $"Uploaded {uploadedFiles}/{totalFiles} files");
                        NotifyProgress(workflow);
                    }

                    // Upload audio
                    if (workflow.GeneratedAudio?.LocalPath != null && File.Exists(workflow.GeneratedAudio.LocalPath))
                    {
                        var audioFile = await _cloudDrive.UploadFileAsync(
                            cloudDrive,
                            workflow.GeneratedAudio.LocalPath,
                            "narration.mp3",
                            null, ct);

                        workflow.GeneratedAudio.CloudDriveFileId = audioFile.Id;
                        workflow.GeneratedAudio.CloudDriveUrl = audioFile.DownloadUrl;
                        workflow.CloudDriveFiles.Add(audioFile);
                    }

                    workflow.Progress.CompleteStep("Cloud Upload");
                    NotifyProgress(workflow);
                }, ct);
            }
            else
            {
                workflow.Progress.CompleteStep("Cloud Upload");
            }

            // Step 5: Assemble Video
            await ExecuteStep(workflow, "Video Assembly", async () =>
            {
                UpdateStatus(workflow, ContentWorkflowStatus.AssemblingVideo);
                workflow.Progress.UpdateStep("Video Assembly", 10, "Assembling final video...");
                NotifyProgress(workflow);

                var options = new VideoAssemblyOptions
                {
                    Width = 1920,
                    Height = 1080,
                    Style = workflow.Concept.VideoStyle,
                    WatermarkText = package.HasWatermark ? "AI Manager" : null
                };

                workflow.FinalVideo = await _videoAssembly.AssembleVideoAsync(
                    workflow.GeneratedScript!,
                    workflow.GeneratedImages,
                    workflow.GeneratedAudio,
                    package,
                    options,
                    ct);

                // Upload final video to cloud drive
                if (cloudDrive != null && cloudDrive.IsConnected && workflow.FinalVideo.LocalPath != null)
                {
                    workflow.Progress.UpdateStep("Video Assembly", 80, "Uploading final video to cloud...");
                    NotifyProgress(workflow);

                    var videoFile = await _cloudDrive.UploadFileAsync(
                        cloudDrive,
                        workflow.FinalVideo.LocalPath,
                        $"{workflow.Title.Replace(" ", "_")}.mp4",
                        null, ct);

                    workflow.FinalVideo.CloudDriveFileId = videoFile.Id;
                    workflow.FinalVideo.CloudDriveUrl = videoFile.DownloadUrl;
                    workflow.CloudDriveFiles.Add(videoFile);
                }

                workflow.Progress.CompleteStep("Video Assembly");
                NotifyProgress(workflow);
            }, ct);

            // Step 6: Publish (if auto-publish enabled)
            if (autoPublish && workflow.PublishTargets.Any())
            {
                await ExecuteStep(workflow, "Publishing", async () =>
                {
                    UpdateStatus(workflow, ContentWorkflowStatus.Publishing);

                    var totalTargets = workflow.PublishTargets.Count;
                    var publishedCount = 0;

                    foreach (var target in workflow.PublishTargets)
                    {
                        ct.ThrowIfCancellationRequested();

                        var progress = (int)((publishedCount / (double)totalTargets) * 100);
                        workflow.Progress.UpdateStep("Publishing", progress, $"Publishing to {target.Platform}...");
                        NotifyProgress(workflow);

                        try
                        {
                            var result = await PublishToTargetAsync(workflow, target, ct);
                            workflow.PublishResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            workflow.PublishResults.Add(new PublishResult
                            {
                                Platform = target.Platform,
                                Success = false,
                                ErrorMessage = ex.Message
                            });
                        }

                        publishedCount++;
                    }

                    workflow.Progress.CompleteStep("Publishing");
                    NotifyProgress(workflow);
                }, ct);
            }
            else
            {
                UpdateStatus(workflow, ContentWorkflowStatus.ReadyToPublish);
                workflow.Progress.CompleteStep("Publishing");
            }

            // Complete workflow
            workflow.Status = ContentWorkflowStatus.Completed;
            workflow.CompletedAt = DateTime.UtcNow;
            workflow.Progress.OverallPercentage = 100;

            NotifyProgress(workflow);
            OnStatusChanged?.Invoke(workflow.Id, ContentWorkflowStatus.Completed, null);

            _logger?.LogInformation("Workflow {WorkflowId} completed successfully", workflow.Id);
        }
        catch (OperationCanceledException)
        {
            workflow.Status = ContentWorkflowStatus.Cancelled;
            workflow.ErrorMessage = "Workflow was cancelled";
            OnStatusChanged?.Invoke(workflow.Id, ContentWorkflowStatus.Cancelled, workflow.ErrorMessage);
        }
        catch (Exception ex)
        {
            workflow.Status = ContentWorkflowStatus.Failed;
            workflow.ErrorMessage = ex.Message;
            _logger?.LogError(ex, "Workflow {WorkflowId} failed", workflow.Id);
            OnStatusChanged?.Invoke(workflow.Id, ContentWorkflowStatus.Failed, ex.Message);
        }
        finally
        {
            _workflowCancellations.Remove(workflow.Id);
        }
    }

    private async Task ExecuteStep(ContentWorkflow workflow, string stepName, Func<Task> action, CancellationToken ct)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            workflow.Progress.Logs.Add(new ProgressLog
            {
                Step = stepName,
                Message = $"Error: {ex.Message}",
                Level = "error"
            });
            throw;
        }
    }

    private async Task<PublishResult> PublishToTargetAsync(ContentWorkflow workflow, PublishTarget target, CancellationToken ct)
    {
        if (workflow.FinalVideo?.LocalPath == null)
        {
            return new PublishResult
            {
                Platform = target.Platform,
                Success = false,
                ErrorMessage = "No video to publish"
            };
        }

        // Export video for platform
        var platformVideoPath = await _videoAssembly.ExportForPlatformAsync(
            workflow.FinalVideo.LocalPath, target.Platform, ct);

        // Build caption
        var caption = target.CustomCaption ?? workflow.FinalVideo.Description;
        var hashtags = target.CustomHashtags ?? workflow.GeneratedScript?.Hashtags ?? new List<string>();

        // Build full caption with hashtags
        var fullCaption = $"{caption}\n\n{string.Join(" ", hashtags.Select(h => h.StartsWith("#") ? h : $"#{h}"))}";

        // Create credentials dictionary for the platform
        // In production, these would be loaded from secure storage based on target.AccountId
        var credentials = new Dictionary<string, string>
        {
            ["access_token"] = "", // Would be loaded from database
            ["page_id"] = target.AccountId,
            ["video_path"] = platformVideoPath
        };

        try
        {
            var result = await _publisher.PostContentAsync(
                target.Platform.ToString().ToLower(),
                fullCaption,
                credentials,
                ct);

            return new PublishResult
            {
                Platform = target.Platform,
                Success = result.Success,
                PostId = result.PostId,
                PostUrl = result.PostUrl,
                ErrorMessage = result.Error
            };
        }
        catch (Exception ex)
        {
            return new PublishResult
            {
                Platform = target.Platform,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #endregion

    #region Status Management

    private void UpdateStatus(ContentWorkflow workflow, ContentWorkflowStatus status)
    {
        workflow.Status = status;
        workflow.UpdatedAt = DateTime.UtcNow;
        OnStatusChanged?.Invoke(workflow.Id, status, null);
    }

    private void NotifyProgress(ContentWorkflow workflow)
    {
        OnProgressUpdated?.Invoke(workflow.Id, workflow.Progress);
    }

    /// <summary>
    /// Get workflow status
    /// </summary>
    public ContentWorkflow? GetWorkflow(string workflowId)
    {
        return _activeWorkflows.TryGetValue(workflowId, out var workflow) ? workflow : null;
    }

    /// <summary>
    /// Get workflow status response
    /// </summary>
    public WorkflowStatusResponse? GetWorkflowStatus(string workflowId)
    {
        var workflow = GetWorkflow(workflowId);
        if (workflow == null) return null;

        return new WorkflowStatusResponse
        {
            WorkflowId = workflow.Id,
            Status = workflow.Status,
            Progress = workflow.Progress,
            FinalVideo = workflow.FinalVideo,
            PublishResults = workflow.PublishResults,
            ErrorMessage = workflow.ErrorMessage
        };
    }

    /// <summary>
    /// Cancel workflow
    /// </summary>
    public bool CancelWorkflow(string workflowId)
    {
        if (_workflowCancellations.TryGetValue(workflowId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get all active workflows for user
    /// </summary>
    public List<ContentWorkflow> GetActiveWorkflows(string userId)
    {
        return _activeWorkflows.Values
            .Where(w => w.UserId == userId && w.Status != ContentWorkflowStatus.Completed && w.Status != ContentWorkflowStatus.Failed)
            .ToList();
    }

    #endregion

    #region Quota Check

    /// <summary>
    /// Check if user can create content
    /// </summary>
    public QuotaCheckResponse CheckQuota(UserPackage package, ContentConcept concept, List<PublishTarget> targets)
    {
        var response = new QuotaCheckResponse
        {
            PackageName = package.PackageName,
            RemainingVideos = package.RemainingVideos,
            MaxDurationSeconds = package.MaxVideoDurationSeconds,
            MaxImages = package.MaxImagesPerVideo,
            AllowedPlatforms = package.AllowedPlatforms,
            HasWatermark = package.HasWatermark
        };

        if (!package.CanCreateVideo())
        {
            response.CanCreate = false;
            response.Reason = package.VideosCreatedThisMonth >= package.MaxVideosPerMonth
                ? "Monthly video quota exceeded"
                : package.ExpiresAt <= DateTime.UtcNow
                    ? "Package expired"
                    : "Package is not active";
            return response;
        }

        if (concept.TargetDurationSeconds > package.MaxVideoDurationSeconds)
        {
            response.CanCreate = false;
            response.Reason = $"Video duration ({concept.TargetDurationSeconds}s) exceeds package limit ({package.MaxVideoDurationSeconds}s)";
            return response;
        }

        var invalidPlatforms = targets
            .Where(t => !package.AllowedPlatforms.Contains(t.Platform))
            .Select(t => t.Platform)
            .ToList();

        if (invalidPlatforms.Any())
        {
            response.CanCreate = false;
            response.Reason = $"Platforms not allowed: {string.Join(", ", invalidPlatforms)}";
            return response;
        }

        if (targets.Count > package.MaxPlatforms)
        {
            response.CanCreate = false;
            response.Reason = $"Too many platforms ({targets.Count}) - package allows {package.MaxPlatforms}";
            return response;
        }

        response.CanCreate = true;
        return response;
    }

    #endregion
}
