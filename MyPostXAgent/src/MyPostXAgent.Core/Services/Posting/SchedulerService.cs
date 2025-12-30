using System.Timers;
using MyPostXAgent.Core.Models;
using MyPostXAgent.Core.Services.Data;
using MyPostXAgent.Core.Services.Social;

namespace MyPostXAgent.Core.Services.Posting;

/// <summary>
/// Background scheduler service for auto-posting
/// </summary>
public class SchedulerService : IDisposable
{
    private readonly DatabaseService _database;
    private readonly PostingService _postingService;
    private readonly System.Timers.Timer _timer;
    private readonly object _lock = new();
    private bool _isRunning;

    public event EventHandler<PostScheduledEventArgs>? PostScheduled;
    public event EventHandler<PostPublishedEventArgs>? PostPublished;
    public event EventHandler<PostFailedEventArgs>? PostFailed;

    public bool IsRunning => _isRunning;

    public SchedulerService(DatabaseService database, PostingService postingService)
    {
        _database = database;
        _postingService = postingService;

        _timer = new System.Timers.Timer(60000); // Check every minute
        _timer.Elapsed += OnTimerElapsed;
    }

    /// <summary>
    /// Start the scheduler
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            _timer.Start();
            System.Diagnostics.Debug.WriteLine("Scheduler started");
        }
    }

    /// <summary>
    /// Stop the scheduler
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            _timer.Stop();
            System.Diagnostics.Debug.WriteLine("Scheduler stopped");
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (!_isRunning) return;

        try
        {
            await ProcessScheduledPostsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Scheduler error: {ex.Message}");
        }
    }

    private async Task ProcessScheduledPostsAsync()
    {
        var now = DateTime.UtcNow;
        var posts = await _database.GetPostsAsync(PostStatus.Scheduled);

        foreach (var post in posts.Where(p => p.ScheduledAt <= now))
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Publishing scheduled post {post.Id}");

                var result = await _postingService.PublishPostAsync(post);

                if (result.Success)
                {
                    PostPublished?.Invoke(this, new PostPublishedEventArgs(post, result));
                }
                else
                {
                    PostFailed?.Invoke(this, new PostFailedEventArgs(post, result.ErrorMessage ?? "Unknown error"));
                }
            }
            catch (Exception ex)
            {
                PostFailed?.Invoke(this, new PostFailedEventArgs(post, ex.Message));
            }
        }
    }

    /// <summary>
    /// Schedule a post for later
    /// </summary>
    public async Task<bool> SchedulePostAsync(Post post, DateTime scheduledTime, CancellationToken ct = default)
    {
        if (scheduledTime <= DateTime.UtcNow)
        {
            return false;
        }

        post.Status = PostStatus.Scheduled;
        post.ScheduledAt = scheduledTime;

        if (post.Id == 0)
        {
            await _database.AddPostAsync(post, ct);
        }
        else
        {
            await _database.UpdatePostAsync(post, ct);
        }

        PostScheduled?.Invoke(this, new PostScheduledEventArgs(post, scheduledTime));
        return true;
    }

    /// <summary>
    /// Cancel a scheduled post
    /// </summary>
    public async Task<bool> CancelScheduledPostAsync(int postId, CancellationToken ct = default)
    {
        var posts = await _database.GetPostsAsync(PostStatus.Scheduled, ct);
        var post = posts.FirstOrDefault(p => p.Id == postId);

        if (post == null) return false;

        post.Status = PostStatus.Cancelled;
        post.ScheduledAt = null;
        await _database.UpdatePostAsync(post, ct);

        return true;
    }

    /// <summary>
    /// Get upcoming scheduled posts
    /// </summary>
    public async Task<List<Post>> GetUpcomingPostsAsync(int count = 10, CancellationToken ct = default)
    {
        var posts = await _database.GetPostsAsync(PostStatus.Scheduled, ct);
        return posts
            .Where(p => p.ScheduledAt > DateTime.UtcNow)
            .OrderBy(p => p.ScheduledAt)
            .Take(count)
            .ToList();
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}

public class PostScheduledEventArgs : EventArgs
{
    public Post Post { get; }
    public DateTime ScheduledTime { get; }

    public PostScheduledEventArgs(Post post, DateTime scheduledTime)
    {
        Post = post;
        ScheduledTime = scheduledTime;
    }
}

public class PostPublishedEventArgs : EventArgs
{
    public Post Post { get; }
    public PostResult Result { get; }

    public PostPublishedEventArgs(Post post, PostResult result)
    {
        Post = post;
        Result = result;
    }
}

public class PostFailedEventArgs : EventArgs
{
    public Post Post { get; }
    public string Error { get; }

    public PostFailedEventArgs(Post post, string error)
    {
        Post = post;
        Error = error;
    }
}
