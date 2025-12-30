using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;
using AIManager.Core.Services;

namespace AIManager.Core.Workers;

/// <summary>
/// Self-Healing Worker - Worker ที่ฉลาด สามารถซ่อมแซมตัวเองได้
/// เรียนรู้จากความผิดพลาด และแชร์ความรู้ให้ worker อื่นๆ
/// </summary>
public class SelfHealingWorker
{
    private readonly ILogger<SelfHealingWorker> _logger;
    private readonly WorkerManager _workerManager;
    private readonly WorkflowLearningEngine _learningEngine;
    private readonly WorkflowStorage _workflowStorage;
    private readonly KnowledgeBase _knowledgeBase;
    private readonly AICodeGeneratorService _codeGenerator;
    private readonly DynamicCodeExecutor? _codeExecutor;
    private readonly BrowserController? _browserController;
    private readonly SelfHealingConfig _config;

    public SelfHealingWorker(
        WorkerManager workerManager,
        WorkflowLearningEngine learningEngine,
        WorkflowStorage workflowStorage,
        KnowledgeBase knowledgeBase,
        AICodeGeneratorService codeGenerator,
        SelfHealingConfig? config = null,
        DynamicCodeExecutor? codeExecutor = null,
        BrowserController? browserController = null,
        ILogger<SelfHealingWorker>? logger = null)
    {
        _workerManager = workerManager;
        _learningEngine = learningEngine;
        _workflowStorage = workflowStorage;
        _knowledgeBase = knowledgeBase;
        _codeGenerator = codeGenerator;
        _config = config ?? new SelfHealingConfig();
        _codeExecutor = codeExecutor;
        _browserController = browserController;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SelfHealingWorker>();
    }

    /// <summary>
    /// Get current configuration (for API inspection)
    /// </summary>
    public SelfHealingConfig GetConfiguration() => _config;

    /// <summary>
    /// สร้าง Smart Worker ที่สามารถซ่อมแซมตัวเองได้
    /// </summary>
    public ManagedWorker CreateSmartWorker(
        string name,
        SocialPlatform platform,
        IPlatformWorker platformWorker)
    {
        return _workerManager.CreateWorker(name, platform, async (worker, ct) =>
        {
            _logger.LogInformation("🧠 Smart Worker {Name} started for {Platform}", name, platform);

            while (!ct.IsCancellationRequested && !worker.StopRequested)
            {
                try
                {
                    await _workerManager.CheckPauseAndWaitAsync(worker.Id, ct);
                    if (worker.StopRequested) break;

                    // ดึง task จาก queue
                    var task = await GetNextTaskAsync(platform, ct);
                    if (task == null)
                    {
                        await _workerManager.DelayWithPauseCheckAsync(worker.Id, 2000, ct);
                        continue;
                    }

                    _workerManager.UpdateWorkerProgress(worker.Id, $"Processing: {task.Type}", 0, task.Id);

                    // พยายามทำ task พร้อม self-healing
                    var result = await ExecuteWithSelfHealingAsync(worker, task, platformWorker, ct);

                    // รายงานผล
                    _workerManager.ReportWorkResult(worker.Id, new WorkerReport
                    {
                        TaskType = task.Type.ToString(),
                        Success = result.Success,
                        Message = result.Success ? "Completed" : result.Error,
                        ProcessingTime = TimeSpan.FromMilliseconds(result.ProcessingTimeMs),
                        Metadata = new Dictionary<string, object>
                        {
                            ["TaskId"] = task.Id,
                            ["Platform"] = platform.ToString(),
                            ["SelfHealed"] = result.SelfHealed,
                            ["HealingMethod"] = result.HealingMethod ?? "none"
                        }
                    });

                    // บันทึกความรู้ถ้าเรียนรู้อะไรใหม่
                    if (result.LearnedKnowledge != null)
                    {
                        await _knowledgeBase.SaveKnowledgeAsync(result.LearnedKnowledge);
                    }

                    _workerManager.UpdateWorkerProgress(worker.Id, "Idle", 100, "Waiting for next task");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Smart Worker {Name}", name);
                    worker.LastError = ex.Message;
                    worker.ErrorCount++;
                    await _workerManager.DelayWithPauseCheckAsync(worker.Id, 5000, ct);
                }
            }

            _logger.LogInformation("🧠 Smart Worker {Name} stopped", name);
        });
    }

    /// <summary>
    /// Execute task พร้อม Self-Healing
    /// </summary>
    private async Task<SelfHealingResult> ExecuteWithSelfHealingAsync(
        ManagedWorker worker,
        TaskItem task,
        IPlatformWorker platformWorker,
        CancellationToken ct)
    {
        var result = new SelfHealingResult();
        var attempts = 0;
        var selfHealAttempts = 0;
        Exception? lastException = null;

        while (attempts < _config.MaxRetryAttempts && !ct.IsCancellationRequested)
        {
            attempts++;

            try
            {
                _logger.LogDebug("Attempt {Attempt}/{Max} for task {TaskId}", attempts, _config.MaxRetryAttempts, task.Id);

                // ลองทำ task ปกติ
                var taskResult = await ExecuteTaskAsync(task, platformWorker, ct);

                if (taskResult.Success)
                {
                    result.Success = true;
                    result.ProcessingTimeMs = taskResult.ProcessingTimeMs;
                    result.Data = taskResult.Data;

                    // บันทึกว่าวิธีนี้ใช้ได้
                    await _knowledgeBase.RecordSuccessAsync(task.Platform, task.Type.ToString(), null);

                    return result;
                }

                // Task failed - พยายาม diagnose และ heal
                lastException = new Exception(taskResult.Error ?? "Unknown error");

                // ใช้ MaxRetryAttempts สำหรับ self-heal attempts ด้วย (หรือจะแยกเป็น config field ใหม่ก็ได้)
                if (selfHealAttempts < _config.MaxRetryAttempts)
                {
                    selfHealAttempts++;
                    _logger.LogWarning("Task failed, attempting self-healing ({Attempt}/{Max})",
                        selfHealAttempts, _config.MaxRetryAttempts);

                    var healingResult = await TrySelfHealAsync(worker, task, taskResult.Error ?? "", ct);

                    if (healingResult.Healed)
                    {
                        result.SelfHealed = true;
                        result.HealingMethod = healingResult.Method;
                        result.LearnedKnowledge = healingResult.NewKnowledge;

                        _logger.LogInformation("Self-healing successful using method: {Method}", healingResult.Method);
                        // ลองอีกครั้งหลัง heal
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Exception in task execution");
            }

            // Wait before retry (use config delay)
            await _workerManager.DelayWithPauseCheckAsync(worker.Id, _config.RetryDelayMs * attempts, ct);
        }

        // All self-heal attempts failed - ลองใช้ AI Code Generation ก่อน Human Training (if enabled)
        if (_config.EnableAICodeGeneration)
        {
            _logger.LogWarning("🤖 Self-healing failed, attempting AI Code Generation...");

            var aiCodeResult = await TryAICodeGenerationAsync(worker, task, lastException?.Message ?? "Unknown error", ct);

            if (aiCodeResult.Success)
            {
                result.Success = true;
                result.SelfHealed = true;
                result.HealingMethod = "ai_code_generation";
                result.Data = aiCodeResult.Data;
                result.LearnedKnowledge = new Knowledge
                {
                    Platform = task.Platform,
                    ErrorPattern = "AICodeFix",
                    Solution = "AI generated JavaScript code",
                    SolutionData = aiCodeResult.GeneratedCode,
                    SuccessCount = 1,
                    CreatedAt = DateTime.UtcNow
                };
                return result;
            }
        }

        // AI Code Generation ก็ล้มเหลว หรือไม่ได้เปิด - ตอนนี้ขอความช่วยเหลือจากมนุษย์ (if enabled)
        if (_config.EnableHumanTraining)
        {
            _logger.LogWarning("🆘 AI Code Generation failed or disabled, requesting human assistance...");
        }

        result.Success = false;
        result.Error = lastException?.Message ?? "All attempts failed";
        result.NeedsHumanHelp = _config.EnableHumanTraining;

        if (_config.EnableHumanTraining)
        {
            await RequestHumanAssistanceAsync(worker, task, lastException?.Message ?? "Unknown error");
        }

        return result;
    }

    /// <summary>
    /// ลองใช้ AI เขียน JavaScript Code เพื่อแก้ปัญหาการโพส
    /// </summary>
    private async Task<AICodeHealingResult> TryAICodeGenerationAsync(
        ManagedWorker worker,
        TaskItem task,
        string errorMessage,
        CancellationToken ct)
    {
        var result = new AICodeHealingResult();

        try
        {
            // ตรวจสอบว่าควร escalate ไป human หรือยัง
            if (_codeGenerator.ShouldEscalateToHuman(task.Platform, task.Type.ToString()))
            {
                _logger.LogWarning("AI Code Generation has failed too many times, skipping to human training");
                result.Success = false;
                result.Error = "Too many AI code generation failures";
                return result;
            }

            // ต้องมี browser controller และ code executor
            if (_browserController == null || _codeExecutor == null)
            {
                _logger.LogWarning("Browser controller or code executor not available");
                result.Success = false;
                result.Error = "WebView components not available";
                return result;
            }

            _workerManager.UpdateWorkerProgress(worker.Id, "AI Code Generation", 30, "Generating fix code...");

            // ดึง HTML ปัจจุบันจาก browser
            var currentHtml = await _browserController.GetPageHtmlAsync(ct) ?? "";
            var currentUrl = _browserController.CurrentUrl;

            // ดึง workflow ที่ล้มเหลว (ถ้ามี)
            var failedWorkflow = await _workflowStorage.FindWorkflowAsync(
                task.Platform.ToString(),
                task.Type.ToString());

            // ใช้ AI สร้าง JavaScript code
            var codeGenResult = await _codeGenerator.GeneratePostingCodeAsync(
                task.Platform,
                task.Type.ToString(),
                errorMessage,
                currentHtml,
                currentUrl,
                failedWorkflow,
                ct);

            if (!codeGenResult.Success || string.IsNullOrEmpty(codeGenResult.GeneratedCode))
            {
                _logger.LogWarning("AI Code Generation failed: {Error}", codeGenResult.Error);
                result.Success = false;
                result.Error = codeGenResult.Error;
                return result;
            }

            _workerManager.UpdateWorkerProgress(worker.Id, "AI Code Generation", 60, "Executing generated code...");

            // เตรียม content สำหรับโพส
            var content = new WebAutomation.PostContent
            {
                Text = task.Payload?.Content?.Text,
                Hashtags = task.Payload?.Content?.Hashtags,
                Link = task.Payload?.Content?.Link,
                Images = task.Payload?.Content?.Images,
                Location = task.Payload?.Content?.Location
            };

            // รัน code ที่ AI generate มา
            var platformUrl = GetPlatformPostingUrl(task.Platform);
            var execResult = await _codeExecutor.ExecuteAsync(
                codeGenResult.GeneratedCode,
                content,
                platformUrl,
                ct);

            _workerManager.UpdateWorkerProgress(worker.Id, "AI Code Generation", 90, "Checking result...");

            if (execResult.Success)
            {
                _logger.LogInformation("✅ AI-generated code executed successfully!");

                // Reset failure count เมื่อสำเร็จ
                _codeGenerator.ResetFailureCount(task.Platform, task.Type.ToString());

                result.Success = true;
                result.GeneratedCode = codeGenResult.GeneratedCode;
                result.Data = new ResultData
                {
                    PostId = execResult.PostId,
                    PlatformUrl = execResult.PostUrl
                };
            }
            else
            {
                _logger.LogWarning("❌ AI-generated code execution failed: {Error}", execResult.Error);
                result.Success = false;
                result.Error = execResult.Error;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI Code Generation process failed");
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Get platform-specific posting URL
    /// </summary>
    private string GetPlatformPostingUrl(SocialPlatform platform)
    {
        return platform switch
        {
            SocialPlatform.Facebook => "https://www.facebook.com/",
            SocialPlatform.Instagram => "https://www.instagram.com/",
            SocialPlatform.Twitter => "https://twitter.com/compose/tweet",
            SocialPlatform.TikTok => "https://www.tiktok.com/upload",
            SocialPlatform.LinkedIn => "https://www.linkedin.com/feed/",
            SocialPlatform.YouTube => "https://studio.youtube.com/",
            SocialPlatform.Line => "https://manager.line.biz/",
            SocialPlatform.Threads => "https://www.threads.net/",
            SocialPlatform.Pinterest => "https://www.pinterest.com/pin-creation-tool/",
            _ => "https://www.google.com/"
        };
    }

    /// <summary>
    /// พยายาม Self-Heal เมื่อเกิดปัญหา
    /// </summary>
    private async Task<HealingResult> TrySelfHealAsync(
        ManagedWorker worker,
        TaskItem task,
        string errorMessage,
        CancellationToken ct)
    {
        var result = new HealingResult();

        // 1. ตรวจสอบ Knowledge Base ว่าเคยเจอปัญหานี้ไหม
        var solution = await _knowledgeBase.FindSolutionAsync(task.Platform, errorMessage);
        if (solution != null)
        {
            _logger.LogInformation("Found existing solution in Knowledge Base");
            result.Healed = await ApplySolutionAsync(solution, task, ct);
            result.Method = "knowledge_base";
            return result;
        }

        // 2. วิเคราะห์ error และลองแก้อัตโนมัติ
        var errorType = ClassifyError(errorMessage);

        switch (errorType)
        {
            case HealingErrorType.ElementNotFound:
                // ลองเรียนรู้ element ใหม่ด้วย AI
                result = await HealElementNotFoundAsync(task, errorMessage, ct);
                break;

            case HealingErrorType.SessionExpired:
                // ลอง refresh session
                result = await HealSessionExpiredAsync(task, ct);
                break;

            case HealingErrorType.RateLimited:
                // รอแล้วลองใหม่
                result = await HealRateLimitAsync(worker, task, ct);
                break;

            case HealingErrorType.NetworkError:
                // รอ network กลับมา
                result = await HealNetworkErrorAsync(worker, ct);
                break;

            case HealingErrorType.UIChanged:
                // เรียนรู้ UI ใหม่
                result = await HealUIChangedAsync(task, ct);
                break;

            case HealingErrorType.PermissionDenied:
                // ต้องการมนุษย์ช่วย
                result.Healed = false;
                result.Method = "needs_human";
                break;

            default:
                // ลองเรียนรู้ใหม่ด้วย browser automation
                result = await HealByRelearningAsync(task, ct);
                break;
        }

        return result;
    }

    #region Healing Methods

    private async Task<HealingResult> HealElementNotFoundAsync(TaskItem task, string errorMessage, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to heal: Element Not Found");

        var result = new HealingResult { Method = "element_relearning" };

        try
        {
            // ดึง workflow ปัจจุบัน
            var workflow = await _workflowStorage.FindWorkflowAsync(task.Platform.ToString(), task.Type.ToString());
            if (workflow == null)
            {
                result.Healed = false;
                return result;
            }

            // ใช้ AI พยายามซ่อม workflow ที่พัง
            var updatedWorkflow = await _learningEngine.TryAutoRepairWorkflow(
                workflow,
                0, // ใช้ step แรกที่มีปัญหา
                "", // ไม่มี HTML (จะดึงจริงใน implementation)
                null,
                ct
            );

            if (updatedWorkflow != null)
            {
                // บันทึก workflow ใหม่
                await _workflowStorage.SaveWorkflowAsync(updatedWorkflow);

                // บันทึกความรู้
                result.NewKnowledge = new Knowledge
                {
                    Platform = task.Platform,
                    ErrorPattern = "ElementNotFound",
                    OriginalError = errorMessage,
                    Solution = "Updated element selector",
                    SolutionData = updatedWorkflow.ToJson(),
                    SuccessCount = 1,
                    CreatedAt = DateTime.UtcNow
                };

                result.Healed = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to heal element not found");
            result.Healed = false;
        }

        return result;
    }

    private async Task<HealingResult> HealSessionExpiredAsync(TaskItem task, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to heal: Session Expired");

        var result = new HealingResult { Method = "session_refresh" };

        try
        {
            // ขอ refresh token
            // (implementation ขึ้นกับ platform)
            await Task.Delay(1000, ct);

            result.Healed = true;
            result.NewKnowledge = new Knowledge
            {
                Platform = task.Platform,
                ErrorPattern = "SessionExpired",
                Solution = "Refresh token",
                SuccessCount = 1,
                CreatedAt = DateTime.UtcNow
            };
        }
        catch
        {
            result.Healed = false;
        }

        return result;
    }

    private async Task<HealingResult> HealRateLimitAsync(ManagedWorker worker, TaskItem task, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to heal: Rate Limit - waiting...");

        var result = new HealingResult { Method = "rate_limit_wait" };

        // รอ 60 วินาที
        await _workerManager.DelayWithPauseCheckAsync(worker.Id, 60000, ct);

        result.Healed = true;
        result.NewKnowledge = new Knowledge
        {
            Platform = task.Platform,
            ErrorPattern = "RateLimit",
            Solution = "Wait 60 seconds",
            SuccessCount = 1,
            CreatedAt = DateTime.UtcNow
        };

        return result;
    }

    private async Task<HealingResult> HealNetworkErrorAsync(ManagedWorker worker, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to heal: Network Error - waiting for connection...");

        var result = new HealingResult { Method = "network_wait" };

        // รอ 10 วินาที
        await _workerManager.DelayWithPauseCheckAsync(worker.Id, 10000, ct);

        result.Healed = true;
        return result;
    }

    private async Task<HealingResult> HealUIChangedAsync(TaskItem task, CancellationToken ct)
    {
        _logger.LogInformation("Attempting to heal: UI Changed - looking for similar workflow...");

        var result = new HealingResult { Method = "ui_relearning" };

        try
        {
            // ลองหา workflow ที่คล้ายกัน
            var similarWorkflow = await _workflowStorage.FindSimilarWorkflowAsync(
                task.Platform.ToString(),
                task.Type.ToString(),
                ct
            );

            if (similarWorkflow != null)
            {
                // ใช้ workflow ที่คล้ายกัน
                result.Healed = true;
                result.NewKnowledge = new Knowledge
                {
                    Platform = task.Platform,
                    ErrorPattern = "UIChanged",
                    Solution = "Used similar workflow",
                    SolutionData = similarWorkflow.ToJson(),
                    SuccessCount = 1,
                    CreatedAt = DateTime.UtcNow
                };
            }
            else
            {
                // ต้องการ human training
                result.Healed = false;
                result.Method = "needs_human_training";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to heal UI changed");
            result.Healed = false;
        }

        return result;
    }

    private async Task<HealingResult> HealByRelearningAsync(TaskItem task, CancellationToken ct)
    {
        _logger.LogInformation("Attempting generic healing by relearning...");

        var result = new HealingResult { Method = "generic_relearning" };

        try
        {
            // ลองดึง workflow ที่คล้ายกันจาก knowledge base
            var similarWorkflow = await _knowledgeBase.FindSimilarWorkflowAsync(task.Platform, task.Type.ToString());

            if (similarWorkflow != null)
            {
                result.Healed = true;
                result.NewKnowledge = new Knowledge
                {
                    Platform = task.Platform,
                    ErrorPattern = "Generic",
                    Solution = "Used similar workflow",
                    SuccessCount = 1,
                    CreatedAt = DateTime.UtcNow
                };
            }
        }
        catch
        {
            result.Healed = false;
        }

        return result;
    }

    #endregion

    #region Helper Methods

    private async Task<TaskItem?> GetNextTaskAsync(SocialPlatform platform, CancellationToken ct)
    {
        // TODO: Implement task queue retrieval
        await Task.CompletedTask;
        return null;
    }

    private async Task<TaskResult> ExecuteTaskAsync(TaskItem task, IPlatformWorker worker, CancellationToken ct)
    {
        return task.Type switch
        {
            TaskType.GenerateContent => await worker.GenerateContentAsync(task, ct),
            TaskType.GenerateImage => await worker.GenerateImageAsync(task, ct),
            TaskType.PostContent => await worker.PostContentAsync(task, ct),
            TaskType.AnalyzeMetrics => await worker.AnalyzeMetricsAsync(task, ct),
            TaskType.DeletePost => await worker.DeletePostAsync(task, ct),
            TaskType.SchedulePost => await worker.SchedulePostAsync(task, ct),
            _ => new TaskResult { Success = false, Error = $"Unknown task type: {task.Type}" }
        };
    }

    private HealingErrorType ClassifyError(string errorMessage)
    {
        var lower = errorMessage.ToLower();

        if (lower.Contains("element not found") || lower.Contains("selector") || lower.Contains("no such element"))
            return HealingErrorType.ElementNotFound;

        if (lower.Contains("session") || lower.Contains("expired") || lower.Contains("token") || lower.Contains("login"))
            return HealingErrorType.SessionExpired;

        if (lower.Contains("rate limit") || lower.Contains("too many") || lower.Contains("429"))
            return HealingErrorType.RateLimited;

        if (lower.Contains("network") || lower.Contains("connection") || lower.Contains("timeout"))
            return HealingErrorType.NetworkError;

        if (lower.Contains("ui changed") || lower.Contains("layout") || lower.Contains("redesign"))
            return HealingErrorType.UIChanged;

        if (lower.Contains("permission") || lower.Contains("denied") || lower.Contains("forbidden") || lower.Contains("banned"))
            return HealingErrorType.PermissionDenied;

        return HealingErrorType.Unknown;
    }

    private async Task<bool> ApplySolutionAsync(Knowledge solution, TaskItem task, CancellationToken ct)
    {
        try
        {
            if (!string.IsNullOrEmpty(solution.SolutionData))
            {
                // Apply the stored solution
                var workflow = LearnedWorkflow.FromJson(solution.SolutionData);
                if (workflow != null)
                {
                    await _workflowStorage.SaveWorkflowAsync(workflow);
                    solution.SuccessCount++;
                    await _knowledgeBase.SaveKnowledgeAsync(solution);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task RequestHumanAssistanceAsync(ManagedWorker worker, TaskItem task, string errorMessage)
    {
        _logger.LogWarning("🆘 Requesting human assistance for task {TaskId}", task.Id);

        // บันทึกว่าต้องการความช่วยเหลือ
        var request = new HumanAssistanceRequest
        {
            Id = Guid.NewGuid().ToString(),
            WorkerId = worker.Id,
            WorkerName = worker.Name,
            Platform = task.Platform,
            TaskType = task.Type.ToString(),
            TaskId = task.Id,
            ErrorMessage = errorMessage,
            RequestedAt = DateTime.UtcNow,
            Status = AssistanceStatus.Pending
        };

        await _knowledgeBase.SaveAssistanceRequestAsync(request);

        // Set worker to NeedsHelp mode for WebView integration
        var oldMode = worker.ViewMode;
        worker.ViewMode = WorkerViewMode.NeedsHelp;
        worker.ViewContext = new WorkerViewContext
        {
            WorkerId = worker.Id,
            Mode = WorkerViewMode.NeedsHelp,
            HelpRequestedAt = DateTime.UtcNow,
            HelpReason = errorMessage,
            CurrentUrl = _browserController?.CurrentUrl
        };

        // Fire event for UI to show WebView
        _workerManager.RaiseWorkerHelpRequested(new WorkerHelpRequestedEventArgs(
            worker, errorMessage, _browserController?.CurrentUrl));

        _logger.LogInformation("Worker {WorkerId} waiting for human assistance...", worker.Id);

        // Wait for human resolution - worker will be paused until resolved
        var waitStarted = DateTime.UtcNow;
        var maxWaitTime = TimeSpan.FromMinutes(30); // Maximum wait time

        while (worker.ViewMode == WorkerViewMode.NeedsHelp)
        {
            await Task.Delay(500);

            // Check if worker was stopped
            if (worker.StopRequested) break;

            // Timeout after maxWaitTime
            if (DateTime.UtcNow - waitStarted > maxWaitTime)
            {
                _logger.LogWarning("Human assistance request timed out for worker {WorkerId}", worker.Id);
                worker.ViewMode = WorkerViewMode.Headless;
                break;
            }
        }

        // Log resolution
        if (worker.ViewMode == WorkerViewMode.Headless || worker.ViewMode == WorkerViewMode.Resuming)
        {
            _logger.LogInformation("Human assistance resolved for worker {WorkerId}", worker.Id);
        }
    }

    /// <summary>
    /// Process learned workflow from human intervention
    /// </summary>
    public async Task ProcessLearnedWorkflowAsync(
        string workerId,
        List<RecordedStep> recordedSteps,
        TaskItem? originalTask,
        CancellationToken ct = default)
    {
        try
        {
            var worker = _workerManager.GetWorker(workerId);
            if (worker == null)
            {
                _logger.LogWarning("Worker {WorkerId} not found for processing learned workflow", workerId);
                return;
            }

            _logger.LogInformation("Processing {StepCount} recorded steps for worker {WorkerId}",
                recordedSteps.Count, workerId);

            // Convert recorded steps to workflow steps
            var workflowSteps = recordedSteps.Select((step, index) => new WebAutomation.Models.WorkflowStep
            {
                Order = index + 1,
                Action = MapActionType(step.ActionType),
                Selector = new WebAutomation.Models.ElementSelector
                {
                    Type = WebAutomation.Models.SelectorType.CSS,
                    Value = step.Selector ?? ""
                },
                AlternativeSelectors = GenerateSelectorAlternatives(step.ElementInfo)
                    .Select(s => new WebAutomation.Models.ElementSelector
                    {
                        Type = WebAutomation.Models.SelectorType.CSS,
                        Value = s
                    }).ToList(),
                InputValue = step.Value,
                Description = step.Description ?? $"{step.ActionType} on {step.ElementInfo?.TagName}",
                WaitAfterMs = 500,
                IsOptional = false
            }).ToList();

            // Create learned workflow
            var workflow = new LearnedWorkflow
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"HumanTaught_{worker.Platform}_{originalTask?.Type}",
                Platform = worker.Platform.ToString(),
                TaskType = originalTask?.Type.ToString() ?? "Unknown",
                Steps = workflowSteps,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SuccessCount = 1,
                IsHumanTrained = true
            };

            // Save the workflow
            await _workflowStorage.SaveWorkflowAsync(workflow);

            // Save to knowledge base
            await _knowledgeBase.SaveKnowledgeAsync(new Knowledge
            {
                Platform = worker.Platform,
                ErrorPattern = "HumanTaught",
                Solution = "Workflow learned from human",
                SolutionData = workflow.ToJson(),
                SuccessCount = 1,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Saved learned workflow {WorkflowId} with {StepCount} steps",
                workflow.Id, workflow.Steps.Count);

            // Resume worker
            await _workerManager.ResumeWorkerFromHelpAsync(workerId, workflow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process learned workflow for worker {WorkerId}", workerId);
        }
    }

    /// <summary>
    /// Map RecordedActionType to StepAction
    /// </summary>
    private static WebAutomation.Models.StepAction MapActionType(RecordedActionType actionType)
    {
        return actionType switch
        {
            RecordedActionType.Click => WebAutomation.Models.StepAction.Click,
            RecordedActionType.Type => WebAutomation.Models.StepAction.Type,
            RecordedActionType.Clear => WebAutomation.Models.StepAction.Clear,
            RecordedActionType.Navigate => WebAutomation.Models.StepAction.Navigate,
            RecordedActionType.Select => WebAutomation.Models.StepAction.Select,
            RecordedActionType.Check => WebAutomation.Models.StepAction.Click, // Map check to click
            RecordedActionType.Upload => WebAutomation.Models.StepAction.Upload,
            RecordedActionType.Wait => WebAutomation.Models.StepAction.Wait,
            RecordedActionType.Hover => WebAutomation.Models.StepAction.Hover,
            RecordedActionType.Scroll => WebAutomation.Models.StepAction.Scroll,
            _ => WebAutomation.Models.StepAction.Click
        };
    }

    /// <summary>
    /// Generate alternative selectors from element info
    /// </summary>
    private List<string> GenerateSelectorAlternatives(RecordedElementInfo? elementInfo)
    {
        var alternatives = new List<string>();
        if (elementInfo == null) return alternatives;

        // Add ID-based selector (most reliable)
        if (!string.IsNullOrEmpty(elementInfo.Id))
        {
            alternatives.Add($"#{elementInfo.Id}");
        }

        // Add CSS selector
        if (!string.IsNullOrEmpty(elementInfo.CssSelector))
        {
            alternatives.Add(elementInfo.CssSelector);
        }

        // Add XPath
        if (!string.IsNullOrEmpty(elementInfo.XPath))
        {
            alternatives.Add($"xpath={elementInfo.XPath}");
        }

        // Add aria-label based selector
        if (!string.IsNullOrEmpty(elementInfo.AriaLabel))
        {
            alternatives.Add($"[aria-label=\"{elementInfo.AriaLabel}\"]");
        }

        // Add class-based selector
        if (!string.IsNullOrEmpty(elementInfo.ClassName))
        {
            var firstClass = elementInfo.ClassName.Split(' ').FirstOrDefault();
            if (!string.IsNullOrEmpty(firstClass))
            {
                alternatives.Add($".{firstClass}");
            }
        }

        // Add text-based selector
        if (!string.IsNullOrEmpty(elementInfo.Text) && elementInfo.Text.Length < 50)
        {
            alternatives.Add($"text={elementInfo.Text}");
        }

        return alternatives;
    }

    #endregion
}

#region Models

/// <summary>
/// Error types specific to self-healing process
/// </summary>
public enum HealingErrorType
{
    Unknown,
    ElementNotFound,
    SessionExpired,
    RateLimited,
    NetworkError,
    UIChanged,
    PermissionDenied,
    ValidationError
}

public class SelfHealingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public long ProcessingTimeMs { get; set; }
    public ResultData? Data { get; set; }
    public bool SelfHealed { get; set; }
    public string? HealingMethod { get; set; }
    public Knowledge? LearnedKnowledge { get; set; }
    public bool NeedsHumanHelp { get; set; }
}

public class HealingResult
{
    public bool Healed { get; set; }
    public string? Method { get; set; }
    public Knowledge? NewKnowledge { get; set; }
}

public class Knowledge
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SocialPlatform Platform { get; set; }
    public string ErrorPattern { get; set; } = "";
    public string? OriginalError { get; set; }
    public string Solution { get; set; } = "";
    public string? SolutionData { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public class HumanAssistanceRequest
{
    public string Id { get; set; } = "";
    public string WorkerId { get; set; } = "";
    public string WorkerName { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public string TaskType { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public DateTime RequestedAt { get; set; }
    public AssistanceStatus Status { get; set; }
    public string? Resolution { get; set; }
    public string? NewWorkflowJson { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum AssistanceStatus
{
    Pending,
    InProgress,
    Resolved,
    Cancelled
}

/// <summary>
/// Result from AI Code Generation healing attempt
/// </summary>
public class AICodeHealingResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? GeneratedCode { get; set; }
    public ResultData? Data { get; set; }
}

#endregion
