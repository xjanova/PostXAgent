using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;

namespace AIManager.API.Controllers;

/// <summary>
/// Controller for comment management and auto-reply operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CommentManagementController : ControllerBase
{
    private readonly CommentMonitorService _commentMonitor;
    private readonly CommentReplyService _commentReply;
    private readonly TonePersonalityService _toneService;
    private readonly ILogger<CommentManagementController> _logger;

    public CommentManagementController(
        CommentMonitorService commentMonitor,
        CommentReplyService commentReply,
        TonePersonalityService toneService,
        ILogger<CommentManagementController> logger)
    {
        _commentMonitor = commentMonitor;
        _commentReply = commentReply;
        _toneService = toneService;
        _logger = logger;
    }

    /// <summary>
    /// Fetch comments from a platform post
    /// </summary>
    [HttpPost("fetch")]
    public async Task<ActionResult<TaskResult>> FetchComments([FromBody] FetchCommentsRequest request)
    {
        try
        {
            _logger.LogInformation("Fetching comments for post {PostId} on {Platform}",
                request.PostId, request.Platform);

            var comments = await _commentMonitor.FetchCommentsAsync(
                request.Platform,
                request.PostId,
                request.Credentials,
                request.Limit);

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Message = $"Fetched {comments.Count} comments",
                    Comments = comments.Select(c => new CommentData
                    {
                        Id = c.Id,
                        ContentText = c.ContentText,
                        AuthorId = c.AuthorId,
                        AuthorName = c.AuthorName,
                        AuthorAvatarUrl = c.AuthorAvatarUrl,
                        Sentiment = c.Sentiment,
                        SentimentScore = c.SentimentScore,
                        IsQuestion = c.IsQuestion,
                        Priority = c.Priority,
                        CommentedAt = c.CommentedAt
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comments for post {PostId}", request.PostId);
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Generate AI reply for a comment
    /// </summary>
    [HttpPost("generate-reply")]
    public async Task<ActionResult<TaskResult>> GenerateReply([FromBody] GenerateReplyRequest request)
    {
        try
        {
            _logger.LogInformation("Generating reply for comment on {Platform}", request.Platform);

            // Get or build tone config
            var toneConfig = _toneService.FromToneConfig(request.ToneConfig);

            // Create request object
            var replyRequest = new ReplyGenerationRequest
            {
                Comment = new SocialComment
                {
                    ContentText = request.CommentContent,
                    AuthorName = request.AuthorName ?? ""
                },
                Tone = toneConfig,
                PostContent = request.PostContent,
                BrandName = request.BrandInfo?.Name,
                BrandContext = request.BrandInfo?.Description,
                Platform = request.Platform.ToString().ToLower()
            };

            var replyResult = await _commentReply.GenerateReplyAsync(replyRequest);

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    ReplyContent = replyResult.Content
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating reply");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Post a reply to a comment
    /// </summary>
    [HttpPost("post-reply")]
    public async Task<ActionResult<TaskResult>> PostReply([FromBody] PostReplyRequest request)
    {
        try
        {
            _logger.LogInformation("Posting reply to comment {CommentId} on {Platform}",
                request.CommentId, request.Platform);

            var postResult = await _commentReply.PostReplyAsync(
                request.Platform.ToString().ToLower(),
                request.CommentId,
                request.ReplyContent,
                request.Credentials?.AccessToken ?? "");

            return Ok(new TaskResult
            {
                Success = postResult.Success,
                Data = new ResultData
                {
                    Message = postResult.Success ? "Reply posted successfully" : postResult.Error,
                    ReplyId = postResult.ReplyId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting reply to comment {CommentId}", request.CommentId);
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Auto-reply to multiple comments
    /// </summary>
    [HttpPost("auto-reply")]
    public async Task<ActionResult<TaskResult>> AutoReply([FromBody] AutoReplyRequest request)
    {
        try
        {
            _logger.LogInformation("Auto-replying to {Count} comments on {Platform}",
                request.Comments?.Count ?? 0, request.Platform);

            var toneConfig = _toneService.FromToneConfig(request.ToneConfig);
            var results = new List<ReplyResult>();

            foreach (var comment in request.Comments ?? new List<CommentInput>())
            {
                try
                {
                    // Generate reply
                    var replyRequest = new ReplyGenerationRequest
                    {
                        Comment = new SocialComment
                        {
                            Id = comment.Id,
                            ContentText = comment.Content,
                            AuthorName = comment.AuthorName ?? "",
                            Sentiment = comment.Sentiment ?? "neutral",
                            IsQuestion = comment.IsQuestion
                        },
                        Tone = toneConfig,
                        PostContent = request.PostContent,
                        BrandName = request.BrandInfo?.Name,
                        BrandContext = request.BrandInfo?.Description,
                        Platform = request.Platform.ToString().ToLower()
                    };

                    var replyResult = await _commentReply.GenerateReplyAsync(replyRequest);

                    // Post reply
                    var postResult = await _commentReply.PostReplyAsync(
                        request.Platform.ToString().ToLower(),
                        comment.Id,
                        replyResult.Content,
                        request.Credentials?.AccessToken ?? "");

                    results.Add(new ReplyResult
                    {
                        CommentId = comment.Id,
                        Success = postResult.Success,
                        ReplyContent = replyResult.Content,
                        ReplyId = postResult.ReplyId
                    });

                    // Rate limiting delay
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to reply to comment {CommentId}", comment.Id);
                    results.Add(new ReplyResult
                    {
                        CommentId = comment.Id,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var successCount = results.Count(r => r.Success);
            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Message = $"Auto-replied to {successCount}/{results.Count} comments",
                    Replies = results.Select(r => (object)r).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-reply");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Analyze comment sentiment
    /// </summary>
    [HttpPost("analyze-sentiment")]
    public async Task<ActionResult<TaskResult>> AnalyzeSentiment([FromBody] AnalyzeSentimentRequest request)
    {
        try
        {
            var analysis = await _commentMonitor.AnalyzeSentimentAsync(
                request.Content,
                request.Context ?? "");

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Sentiment = analysis.Sentiment,
                    SentimentScore = analysis.Score,
                    IsQuestion = analysis.IsQuestion,
                    PriorityScore = analysis.Priority
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing sentiment");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get available tone presets
    /// </summary>
    [HttpGet("tones/presets")]
    public ActionResult<List<ToneConfig>> GetTonePresets()
    {
        var presets = _toneService.GetPresets();

        return Ok(presets);
    }
}

// Request/Response Models

public record FetchCommentsRequest
{
    public SocialPlatform Platform { get; init; }
    public string PostId { get; init; } = "";
    public PlatformCredentials Credentials { get; init; } = new();
    public int Limit { get; init; } = 50;
}

public record GenerateReplyRequest
{
    public SocialPlatform Platform { get; init; }
    public string CommentContent { get; init; } = "";
    public string? PostContent { get; init; }
    public string? AuthorName { get; init; }
    public ToneConfig? ToneConfig { get; init; }
    public BrandInfo? BrandInfo { get; init; }
}

public record PostReplyRequest
{
    public SocialPlatform Platform { get; init; }
    public string CommentId { get; init; } = "";
    public string ReplyContent { get; init; } = "";
    public PlatformCredentials Credentials { get; init; } = new();
}

public record AutoReplyRequest
{
    public SocialPlatform Platform { get; init; }
    public string? PostContent { get; init; }
    public List<CommentInput>? Comments { get; init; }
    public ToneConfig? ToneConfig { get; init; }
    public BrandInfo? BrandInfo { get; init; }
    public PlatformCredentials Credentials { get; init; } = new();
}

public record CommentInput
{
    public string Id { get; init; } = "";
    public string Content { get; init; } = "";
    public string? AuthorName { get; init; }
    public string? Sentiment { get; init; }
    public bool IsQuestion { get; init; }
}

public record AnalyzeSentimentRequest
{
    public string Content { get; init; } = "";
    public string? Context { get; init; }
}

public record ReplyResult
{
    public string CommentId { get; init; } = "";
    public bool Success { get; init; }
    public string? ReplyContent { get; init; }
    public string? ReplyId { get; init; }
    public string? Error { get; init; }
}
