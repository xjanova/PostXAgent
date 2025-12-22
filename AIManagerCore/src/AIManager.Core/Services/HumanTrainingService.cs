using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using AIManager.Core.Models;
using AIManager.Core.Workers;
using AIManager.Core.WebAutomation;
using AIManager.Core.WebAutomation.Models;

namespace AIManager.Core.Services;

/// <summary>
/// Human Training Service - ระบบให้มนุษย์สอน Worker ใหม่
/// </summary>
public class HumanTrainingService
{
    private readonly ILogger<HumanTrainingService> _logger;
    private readonly WorkflowLearningEngine _learningEngine;
    private readonly WorkflowStorage _workflowStorage;
    private readonly KnowledgeBase _knowledgeBase;

    // Active training sessions
    private readonly ConcurrentDictionary<string, TrainingSession> _trainingSessions = new();

    // Events
    public event EventHandler<TrainingStartedEventArgs>? TrainingStarted;
    public event EventHandler<TrainingStepEventArgs>? TrainingStepRecorded;
    public event EventHandler<TrainingCompletedEventArgs>? TrainingCompleted;

    public HumanTrainingService(
        WorkflowLearningEngine learningEngine,
        WorkflowStorage workflowStorage,
        KnowledgeBase knowledgeBase,
        ILogger<HumanTrainingService>? logger = null)
    {
        _learningEngine = learningEngine;
        _workflowStorage = workflowStorage;
        _knowledgeBase = knowledgeBase;
        _logger = logger ?? LoggerFactory.Create(b => b.AddConsole()).CreateLogger<HumanTrainingService>();
    }

    #region Training Session Management

    public async Task<TrainingSession> StartTrainingSessionAsync(
        SocialPlatform platform,
        string taskType,
        string? assistanceRequestId = null)
    {
        var session = new TrainingSession
        {
            Id = Guid.NewGuid().ToString(),
            Platform = platform,
            TaskType = taskType,
            AssistanceRequestId = assistanceRequestId,
            StartedAt = DateTime.UtcNow,
            Status = TrainingStatus.Recording
        };

        _trainingSessions[session.Id] = session;

        _logger.LogInformation("🎓 Training session started: {Id} for {Platform}/{TaskType}",
            session.Id, platform, taskType);

        TrainingStarted?.Invoke(this, new TrainingStartedEventArgs(session));

        return session;
    }

    public void RecordTrainingStep(string sessionId, TrainingStep step)
    {
        if (!_trainingSessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Training session not found: {Id}", sessionId);
            return;
        }

        step.StepNumber = session.Steps.Count + 1;
        step.RecordedAt = DateTime.UtcNow;
        session.Steps.Add(step);

        _logger.LogDebug("📝 Recorded step {Step}: {Action}", step.StepNumber, step.Action);

        TrainingStepRecorded?.Invoke(this, new TrainingStepEventArgs(session, step));
    }

    public async Task<LearnedWorkflow?> CompleteTrainingSessionAsync(string sessionId, string? notes = null)
    {
        if (!_trainingSessions.TryGetValue(sessionId, out var session))
        {
            _logger.LogWarning("Training session not found: {Id}", sessionId);
            return null;
        }

        session.Status = TrainingStatus.Processing;
        session.Notes = notes;

        try
        {
            _logger.LogInformation("🔄 Processing training session: {Id}", sessionId);

            var workflow = ConvertToWorkflow(session);

            if (workflow != null)
            {
                await _workflowStorage.SaveWorkflowAsync(workflow);

                await _knowledgeBase.SaveKnowledgeAsync(new Knowledge
                {
                    Platform = session.Platform,
                    ErrorPattern = "HumanTrained",
                    Solution = $"Human trained workflow for {session.TaskType}",
                    SolutionData = workflow.ToJson(),
                    SuccessCount = 1,
                    CreatedAt = DateTime.UtcNow
                });

                if (!string.IsNullOrEmpty(session.AssistanceRequestId))
                {
                    await _knowledgeBase.ResolveAssistanceRequestAsync(
                        session.AssistanceRequestId,
                        "Human trained new workflow",
                        workflow
                    );
                }

                session.Status = TrainingStatus.Completed;
                session.CompletedAt = DateTime.UtcNow;
                session.GeneratedWorkflow = workflow;

                _logger.LogInformation("✅ Training completed: {Id} - Generated workflow with {Steps} steps",
                    sessionId, workflow.Steps.Count);

                TrainingCompleted?.Invoke(this, new TrainingCompletedEventArgs(session, workflow));

                return workflow;
            }
            else
            {
                session.Status = TrainingStatus.Failed;
                _logger.LogError("Failed to generate workflow from training session");
            }
        }
        catch (Exception ex)
        {
            session.Status = TrainingStatus.Failed;
            _logger.LogError(ex, "Error completing training session");
        }

        return null;
    }

    public void CancelTrainingSession(string sessionId)
    {
        if (_trainingSessions.TryRemove(sessionId, out var session))
        {
            session.Status = TrainingStatus.Cancelled;
            _logger.LogInformation("Training session cancelled: {Id}", sessionId);
        }
    }

    public TrainingSession? GetTrainingSession(string sessionId)
    {
        _trainingSessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public List<TrainingSession> GetActiveTrainingSessions()
    {
        return _trainingSessions.Values
            .Where(s => s.Status == TrainingStatus.Recording || s.Status == TrainingStatus.Processing)
            .ToList();
    }

    #endregion

    #region Workflow Conversion

    private LearnedWorkflow? ConvertToWorkflow(TrainingSession session)
    {
        if (session.Steps.Count == 0) return null;

        var workflow = new LearnedWorkflow
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"{session.Platform}_{session.TaskType}",
            Platform = session.Platform.ToString(),
            TaskType = session.TaskType,
            CreatedAt = DateTime.UtcNow,
            Version = 1,
            IsHumanTrained = true
        };

        var order = 0;
        foreach (var step in session.Steps)
        {
            var workflowStep = new WebAutomation.Models.WorkflowStep
            {
                Order = ++order,
                Action = ParseAction(step.Action),
                Description = step.Description ?? step.ElementDescription
            };

            // Set selector
            if (!string.IsNullOrEmpty(step.ElementSelector))
            {
                workflowStep.Selector = new ElementSelector
                {
                    Type = ParseSelectorType(step.SelectorType),
                    Value = step.ElementSelector,
                    Confidence = 0.9
                };

                // Add alternatives
                if (!string.IsNullOrEmpty(step.ElementId))
                {
                    workflowStep.AlternativeSelectors.Add(new ElementSelector
                    {
                        Type = SelectorType.Id,
                        Value = step.ElementId,
                        Confidence = 0.95
                    });
                }

                if (!string.IsNullOrEmpty(step.ElementXPath))
                {
                    workflowStep.AlternativeSelectors.Add(new ElementSelector
                    {
                        Type = SelectorType.XPath,
                        Value = step.ElementXPath,
                        Confidence = 0.8
                    });
                }
            }

            if (!string.IsNullOrEmpty(step.InputValue))
            {
                workflowStep.InputValue = step.InputValue;
            }

            if (step.WaitMs > 0)
            {
                workflowStep.WaitAfterMs = step.WaitMs;
            }

            workflow.Steps.Add(workflowStep);
        }

        return workflow;
    }

    private StepAction ParseAction(string action)
    {
        return action.ToLower() switch
        {
            "click" => StepAction.Click,
            "type" => StepAction.Type,
            "select" => StepAction.Select,
            "navigate" => StepAction.Navigate,
            "scroll" => StepAction.Scroll,
            "wait" => StepAction.Wait,
            "upload" => StepAction.Upload,
            "submit" => StepAction.PressKey,
            _ => StepAction.Click
        };
    }

    private SelectorType ParseSelectorType(string? type)
    {
        return type?.ToLower() switch
        {
            "css" => SelectorType.CSS,
            "xpath" => SelectorType.XPath,
            "id" => SelectorType.Id,
            "name" => SelectorType.Name,
            "text" => SelectorType.Text,
            _ => SelectorType.CSS
        };
    }

    #endregion

    #region Quick Training

    public async Task<LearnedWorkflow?> QuickTrainAsync(
        SocialPlatform platform,
        string taskType,
        string startUrl,
        List<QuickTrainingStep> steps)
    {
        var session = await StartTrainingSessionAsync(platform, taskType);
        session.StartUrl = startUrl;

        foreach (var quickStep in steps)
        {
            RecordTrainingStep(session.Id, new TrainingStep
            {
                Action = quickStep.Action,
                ElementSelector = quickStep.Selector,
                ElementDescription = quickStep.Description,
                InputValue = quickStep.Value,
                WaitMs = quickStep.WaitMs,
                WaitForElement = quickStep.WaitForElement
            });
        }

        return await CompleteTrainingSessionAsync(session.Id);
    }

    public async Task<LearnedWorkflow?> ImportWorkflowAsync(
        SocialPlatform platform,
        string taskType,
        string workflowJson)
    {
        try
        {
            var workflow = LearnedWorkflow.FromJson(workflowJson);
            if (workflow == null)
            {
                _logger.LogError("Failed to parse workflow JSON");
                return null;
            }

            workflow.Platform = platform.ToString();
            workflow.TaskType = taskType;
            workflow.IsHumanTrained = true;
            workflow.CreatedAt = DateTime.UtcNow;

            await _workflowStorage.SaveWorkflowAsync(workflow);

            await _knowledgeBase.SaveKnowledgeAsync(new Knowledge
            {
                Platform = platform,
                ErrorPattern = "ImportedWorkflow",
                Solution = $"Imported workflow for {taskType}",
                SolutionData = workflowJson,
                SuccessCount = 1,
                CreatedAt = DateTime.UtcNow
            });

            _logger.LogInformation("Workflow imported successfully for {Platform}/{TaskType}", platform, taskType);

            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import workflow");
            return null;
        }
    }

    #endregion
}

#region Models

public class TrainingSession
{
    public string Id { get; set; } = "";
    public SocialPlatform Platform { get; set; }
    public string TaskType { get; set; } = "";
    public string? AssistanceRequestId { get; set; }
    public string? StartUrl { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TrainingStatus Status { get; set; }
    public List<TrainingStep> Steps { get; set; } = new();
    public string? Notes { get; set; }
    public LearnedWorkflow? GeneratedWorkflow { get; set; }
}

public enum TrainingStatus
{
    Recording,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public class TrainingStep
{
    public int StepNumber { get; set; }
    public string Action { get; set; } = "";
    public string? ElementSelector { get; set; }
    public string? SelectorType { get; set; }
    public string? ElementId { get; set; }
    public string? ElementXPath { get; set; }
    public string? ElementText { get; set; }
    public string? ElementDescription { get; set; }
    public string? Description { get; set; }
    public string? InputValue { get; set; }
    public string? Url { get; set; }
    public bool WaitForElement { get; set; }
    public int? WaitTimeoutMs { get; set; }
    public int WaitMs { get; set; }
    public string? Screenshot { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class QuickTrainingStep
{
    public string Action { get; set; } = "";
    public string Selector { get; set; } = "";
    public string? Description { get; set; }
    public string? Value { get; set; }
    public int WaitMs { get; set; }
    public bool WaitForElement { get; set; }
}

#endregion

#region Event Args

public class TrainingStartedEventArgs : EventArgs
{
    public TrainingSession Session { get; }
    public TrainingStartedEventArgs(TrainingSession session) => Session = session;
}

public class TrainingStepEventArgs : EventArgs
{
    public TrainingSession Session { get; }
    public TrainingStep Step { get; }
    public TrainingStepEventArgs(TrainingSession session, TrainingStep step)
    {
        Session = session;
        Step = step;
    }
}

public class TrainingCompletedEventArgs : EventArgs
{
    public TrainingSession Session { get; }
    public LearnedWorkflow Workflow { get; }
    public TrainingCompletedEventArgs(TrainingSession session, LearnedWorkflow workflow)
    {
        Session = session;
        Workflow = workflow;
    }
}

#endregion
