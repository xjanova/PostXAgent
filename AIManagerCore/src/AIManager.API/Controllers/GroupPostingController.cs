using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;
using System.Collections.Concurrent;

namespace AIManager.API.Controllers;

/// <summary>
/// Controller for group discovery and automated posting loop
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GroupPostingController : ControllerBase
{
    private readonly SeekAndPostService _seekAndPost;
    private readonly GroupSearchService _groupSearch;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly ViralAnalysisService _viralAnalysis;
    private readonly ILogger<GroupPostingController> _logger;

    // Active posting loops (task ID -> cancellation token)
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeLoops = new();

    public GroupPostingController(
        SeekAndPostService seekAndPost,
        GroupSearchService groupSearch,
        ContentGeneratorService contentGenerator,
        ViralAnalysisService viralAnalysis,
        ILogger<GroupPostingController> logger)
    {
        _seekAndPost = seekAndPost;
        _groupSearch = groupSearch;
        _contentGenerator = contentGenerator;
        _viralAnalysis = viralAnalysis;
        _logger = logger;
    }

    /// <summary>
    /// Search for groups by keywords
    /// </summary>
    [HttpPost("groups/search")]
    public async Task<ActionResult<TaskResult>> SearchGroups([FromBody] SearchGroupsRequest request)
    {
        try
        {
            _logger.LogInformation("Searching groups for keywords: {Keywords}", string.Join(", ", request.Keywords ?? new List<string>()));

            var groups = await _groupSearch.SearchGroupsAsync(
                request.Platform.ToString().ToLower(),
                request.Keywords ?? new List<string>(),
                request.Limit);

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Groups = groups.Select(g => (object)new GroupData
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Description = g.Description,
                        MemberCount = g.MemberCount,
                        Platform = g.Platform,
                        IsJoined = g.IsJoined,
                        ActivityScore = g.ActivityScore
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching groups");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get recommended groups based on brand/niche
    /// </summary>
    [HttpPost("groups/recommend")]
    public async Task<ActionResult<TaskResult>> RecommendGroups([FromBody] RecommendGroupsRequest request)
    {
        try
        {
            var keywords = await _viralAnalysis.GetTrendingKeywordsAsync(request.Platform.ToString().ToLower(), 10);
            var allKeywords = new List<string>(request.Niche?.Split(',').Select(s => s.Trim()) ?? new string[0]);
            allKeywords.AddRange(keywords.Select(k => k.Keyword));

            var groups = await _groupSearch.SearchGroupsAsync(
                request.Platform.ToString().ToLower(),
                allKeywords.Distinct().ToList(),
                request.Limit);

            // Sort by activity score (most active first)
            var sortedGroups = groups.OrderByDescending(g => g.ActivityScore).ToList();

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Groups = sortedGroups.Select(g => (object)new GroupData
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Description = g.Description,
                        MemberCount = g.MemberCount,
                        Platform = g.Platform,
                        IsJoined = g.IsJoined,
                        ActivityScore = g.ActivityScore
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recommending groups");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Post to a single group
    /// </summary>
    [HttpPost("post")]
    public async Task<ActionResult<TaskResult>> PostToGroup([FromBody] GroupPostRequest request)
    {
        try
        {
            _logger.LogInformation("Posting to group {GroupId} on {Platform}", request.GroupId, request.Platform);

            // Create a discovered group for posting
            var group = new DiscoveredGroup
            {
                GroupId = request.GroupId,
                Platform = request.Platform.ToString().ToLower()
            };

            var result = await _seekAndPost.PostToGroupAsync(
                group,
                new PostingContent
                {
                    Text = request.Content.Text,
                    Images = request.Content.Images,
                    Link = request.Content.Link
                },
                new PostingCredentials
                {
                    AccessToken = request.Credentials.AccessToken,
                    PageId = request.Credentials.PageId
                });

            return Ok(new TaskResult
            {
                Success = result.Success,
                Data = new ResultData
                {
                    Message = result.Success ? "Posted successfully" : result.Error,
                    PostId = result.PostId,
                    PostUrl = result.PostUrl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting to group");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Post to multiple groups
    /// </summary>
    [HttpPost("post/batch")]
    public async Task<ActionResult<TaskResult>> PostToMultipleGroups([FromBody] BatchPostRequest request)
    {
        try
        {
            _logger.LogInformation("Posting to {Count} groups on {Platform}",
                request.GroupIds?.Count ?? 0, request.Platform);

            var results = await _seekAndPost.PostToGroupsAsync(
                request.Platform.ToString().ToLower(),
                request.GroupIds ?? new List<string>(),
                new PostingContent
                {
                    Text = request.Content.Text,
                    Images = request.Content.Images,
                    Link = request.Content.Link
                },
                new PostingCredentials
                {
                    AccessToken = request.Credentials.AccessToken,
                    PageId = request.Credentials.PageId
                },
                request.DelayBetweenPosts);

            var successCount = results.Count(r => r.Success);
            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Message = $"Posted to {successCount}/{results.Count} groups",
                    PostResults = results.Select(r => new AIManager.Core.Models.PostResultData
                    {
                        GroupId = r.GroupId,
                        Success = r.Success,
                        PostId = r.PostId,
                        PostUrl = r.PostUrl,
                        Error = r.Error
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch posting");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Start continuous posting loop
    /// </summary>
    [HttpPost("loop/start")]
    public async Task<ActionResult<TaskResult>> StartPostingLoop([FromBody] StartLoopRequest request)
    {
        try
        {
            var taskId = Guid.NewGuid().ToString();
            var cts = new CancellationTokenSource();
            _activeLoops[taskId] = cts;

            _logger.LogInformation("Starting posting loop {TaskId} for {Count} groups",
                taskId, request.GroupIds?.Count ?? 0);

            // Start loop in background
            _ = Task.Run(async () =>
            {
                try
                {
                    await _seekAndPost.StartPostingLoopAsync(
                        request.Platform.ToString().ToLower(),
                        request.GroupIds ?? new List<string>(),
                        request.ContentTemplates ?? new List<string>(),
                        new PostingCredentials
                        {
                            AccessToken = request.Credentials.AccessToken,
                            PageId = request.Credentials.PageId
                        },
                        request.IntervalMinutes,
                        request.MaxPostsPerCycle,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Posting loop {TaskId} cancelled", taskId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in posting loop {TaskId}", taskId);
                }
                finally
                {
                    _activeLoops.TryRemove(taskId, out _);
                }
            });

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Message = "Posting loop started",
                    TaskId = taskId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting posting loop");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Stop a running posting loop
    /// </summary>
    [HttpPost("loop/stop/{taskId}")]
    public ActionResult<TaskResult> StopPostingLoop(string taskId)
    {
        try
        {
            if (_activeLoops.TryRemove(taskId, out var cts))
            {
                cts.Cancel();
                _logger.LogInformation("Stopping posting loop {TaskId}", taskId);

                return Ok(new TaskResult
                {
                    Success = true,
                    Data = new ResultData { Message = "Posting loop stopped" }
                });
            }

            return Ok(new TaskResult
            {
                Success = false,
                Error = "Loop not found or already stopped"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping posting loop");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get active posting loops
    /// </summary>
    [HttpGet("loop/active")]
    public ActionResult<TaskResult> GetActiveLoops()
    {
        return Ok(new TaskResult
        {
            Success = true,
            Data = new ResultData
            {
                ActiveLoops = _activeLoops.Keys.ToList()
            }
        });
    }

    /// <summary>
    /// Generate AI content optimized for viral potential
    /// </summary>
    [HttpPost("content/generate")]
    public async Task<ActionResult<TaskResult>> GenerateViralContent([FromBody] GenerateContentRequest request)
    {
        try
        {
            // Get trending keywords for context
            var trendingKeywords = await _viralAnalysis.GetTrendingKeywordsAsync(
                request.Platform.ToString().ToLower(), 10);

            // Generate content with viral optimization
            var prompt = BuildViralPrompt(request, trendingKeywords.Select(k => k.Keyword).ToList());

            var generatedContent = await _contentGenerator.GenerateAsync(
                prompt,
                request.BrandInfo,
                request.Platform.ToString().ToLower(),
                "th",
                default);

            // Analyze viral potential
            var contentText = generatedContent?.Text ?? "";
            var viralScore = await _viralAnalysis.PredictViralPotentialAsync(
                contentText,
                request.Platform.ToString().ToLower(),
                request.Hashtags ?? new List<string>());

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    GeneratedContent = contentText,
                    ViralScore = viralScore.Score,
                    ViralFactors = viralScore.Factors.ToDictionary(
                        f => f.Key,
                        f => (object)f.Value)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating content");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    private string BuildViralPrompt(GenerateContentRequest request, List<string> trendingKeywords)
    {
        var prompt = $"สร้างโพสต์สำหรับ {request.Platform} ในหัวข้อ: {request.Topic ?? "โปรโมทแบรนด์"}";

        if (request.BrandInfo != null)
        {
            prompt += $"\nแบรนด์: {request.BrandInfo.Name}";
            if (!string.IsNullOrEmpty(request.BrandInfo.Industry))
                prompt += $"\nอุตสาหกรรม: {request.BrandInfo.Industry}";
        }

        if (trendingKeywords.Any())
        {
            prompt += $"\nคีย์เวิร์ดที่กำลัง trending: {string.Join(", ", trendingKeywords.Take(5))}";
        }

        prompt += "\nให้เนื้อหามีโอกาสไวรัลสูง กระตุ้นความสนใจ และเหมาะกับกลุ่มเป้าหมายในไทย";

        return prompt;
    }
}

// Request/Response Models

public record SearchGroupsRequest
{
    public SocialPlatform Platform { get; init; }
    public List<string>? Keywords { get; init; }
    public PlatformCredentials Credentials { get; init; } = new();
    public int Limit { get; init; } = 20;
}

public record RecommendGroupsRequest
{
    public SocialPlatform Platform { get; init; }
    public string? Niche { get; init; }
    public PlatformCredentials Credentials { get; init; } = new();
    public int Limit { get; init; } = 20;
}

public record GroupPostRequest
{
    public SocialPlatform Platform { get; init; }
    public string GroupId { get; init; } = "";
    public PostContentData Content { get; init; } = new();
    public PlatformCredentials Credentials { get; init; } = new();
}

public record BatchPostRequest
{
    public SocialPlatform Platform { get; init; }
    public List<string>? GroupIds { get; init; }
    public PostContentData Content { get; init; } = new();
    public PlatformCredentials Credentials { get; init; } = new();
    public TimeSpan DelayBetweenPosts { get; init; } = TimeSpan.FromMinutes(2);
}

public record StartLoopRequest
{
    public SocialPlatform Platform { get; init; }
    public List<string>? GroupIds { get; init; }
    public List<string>? ContentTemplates { get; init; }
    public PlatformCredentials Credentials { get; init; } = new();
    public int IntervalMinutes { get; init; } = 60;
    public int MaxPostsPerCycle { get; init; } = 5;
}

public record GenerateContentRequest
{
    public SocialPlatform Platform { get; init; }
    public string? Topic { get; init; }
    public BrandInfo? BrandInfo { get; init; }
    public List<string>? Hashtags { get; init; }
}

public record PostContentData
{
    public string Text { get; init; } = "";
    public List<string>? Images { get; init; }
    public string? Link { get; init; }
}

public record GroupData
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    public int MemberCount { get; init; }
    public string Platform { get; init; } = "";
    public bool IsJoined { get; init; }
    public double ActivityScore { get; init; }
}

// Note: Using PostResultData from AIManager.Core.Models namespace
