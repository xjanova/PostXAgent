using AIManager.Core.Models;

namespace AIManager.Core.Workers;

/// <summary>
/// Interface for platform-specific workers
/// </summary>
public interface IPlatformWorker
{
    SocialPlatform Platform { get; }

    Task<TaskResult> GenerateContentAsync(TaskItem task, CancellationToken ct);
    Task<TaskResult> GenerateImageAsync(TaskItem task, CancellationToken ct);
    Task<TaskResult> PostContentAsync(TaskItem task, CancellationToken ct);
    Task<TaskResult> SchedulePostAsync(TaskItem task, CancellationToken ct);
    Task<TaskResult> AnalyzeMetricsAsync(TaskItem task, CancellationToken ct);
    Task<TaskResult> DeletePostAsync(TaskItem task, CancellationToken ct);
}
