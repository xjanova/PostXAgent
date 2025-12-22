using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Models;
using AIManager.Core.Services;
using AIManager.Core.Workers;

namespace AIManager.API.Controllers;

/// <summary>
/// Seek and Post API - ค้นหากลุ่มและโพสต์อัตโนมัติ
/// </summary>
[ApiController]
[Route("api/seek-and-post")]
public class SeekAndPostController : ControllerBase
{
    private readonly ILogger<SeekAndPostController> _logger;
    private readonly SeekAndPostService _seekAndPostService;
    private readonly WorkerFactory _workerFactory;

    public SeekAndPostController(
        ILogger<SeekAndPostController> logger,
        SeekAndPostService seekAndPostService,
        WorkerFactory workerFactory)
    {
        _logger = logger;
        _seekAndPostService = seekAndPostService;
        _workerFactory = workerFactory;
    }

    #region Group Discovery API

    /// <summary>
    /// ค้นหากลุ่มตาม keywords
    /// </summary>
    [HttpPost("seek-groups")]
    public async Task<ActionResult<SeekGroupsResponse>> SeekGroups(
        [FromBody] SeekGroupsRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Seeking groups on {Platform} with keywords: {Keywords}",
            request.Platform, string.Join(", ", request.Keywords));

        try
        {
            // Get worker for platform
            var worker = _workerFactory.GetWorker(request.Platform);
            if (worker == null)
            {
                return BadRequest(new SeekGroupsResponse
                {
                    Success = false,
                    Error = $"Platform '{request.Platform}' not supported"
                });
            }

            // Search for groups using worker
            var discoveredGroups = await SearchGroupsViaWorkerAsync(
                worker,
                request.Platform,
                request.Keywords,
                request.ExcludeKeywords,
                request.MinMembers,
                request.MaxMembers,
                request.Limit,
                ct
            );

            // Save discovered groups to database
            foreach (var group in discoveredGroups)
            {
                group.QualityScore = _seekAndPostService.CalculateGroupQualityScore(group);
                await _seekAndPostService.SaveDiscoveredGroupAsync(group);
            }

            _logger.LogInformation("Discovered {Count} groups on {Platform}",
                discoveredGroups.Count, request.Platform);

            return Ok(new SeekGroupsResponse
            {
                Success = true,
                Groups = discoveredGroups.Select(g => new GroupInfo
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Url = g.GroupUrl,
                    Description = g.Description,
                    MemberCount = g.MemberCount,
                    IsPublic = g.Privacy == GroupPrivacy.Public,
                    RequiresApproval = g.RequiresApproval,
                    ActivityLevel = GetActivityLevel(g.EngagementScore),
                    QualityScore = g.QualityScore,
                    Keywords = g.Keywords
                }).ToList(),
                TotalFound = discoveredGroups.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seek groups on {Platform}", request.Platform);
            return StatusCode(500, new SeekGroupsResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ดึงกลุ่มที่ค้นพบแล้วทั้งหมด
    /// </summary>
    [HttpGet("groups")]
    public async Task<ActionResult<GetGroupsResponse>> GetGroups(
        [FromQuery] string? platform = null,
        [FromQuery] string? keywords = null,
        [FromQuery] bool? joinedOnly = null,
        [FromQuery] double? minQualityScore = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            List<DiscoveredGroup> groups;

            if (!string.IsNullOrEmpty(keywords))
            {
                var keywordList = keywords.Split(',').Select(k => k.Trim()).ToList();
                groups = await _seekAndPostService.GetGroupsByKeywordsAsync(
                    platform ?? "facebook", keywordList);
            }
            else if (!string.IsNullOrEmpty(platform))
            {
                groups = await _seekAndPostService.GetAllGroupsAsync(platform);
            }
            else
            {
                // Get all platforms
                groups = new List<DiscoveredGroup>();
                foreach (var p in new[] { "facebook", "line", "telegram", "twitter" })
                {
                    groups.AddRange(await _seekAndPostService.GetAllGroupsAsync(p));
                }
            }

            // Apply filters
            if (joinedOnly == true)
            {
                groups = groups.Where(g => g.IsJoined).ToList();
            }
            if (minQualityScore.HasValue)
            {
                groups = groups.Where(g => g.QualityScore >= minQualityScore.Value).ToList();
            }

            return Ok(new GetGroupsResponse
            {
                Success = true,
                Groups = groups.Take(limit).Select(g => new GroupInfo
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Url = g.GroupUrl,
                    Description = g.Description,
                    MemberCount = g.MemberCount,
                    IsPublic = g.Privacy == GroupPrivacy.Public,
                    RequiresApproval = g.RequiresApproval,
                    ActivityLevel = GetActivityLevel(g.EngagementScore),
                    QualityScore = g.QualityScore,
                    Keywords = g.Keywords,
                    IsJoined = g.IsJoined,
                    JoinedAt = g.JoinedAt,
                    OurPostCount = g.OurPostCount,
                    LastPostedAt = g.LastPostedAt,
                    IsBanned = g.IsBanned
                }).ToList(),
                TotalCount = groups.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get groups");
            return StatusCode(500, new GetGroupsResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// ดึงกลุ่มที่แนะนำสำหรับการโพสต์
    /// </summary>
    [HttpGet("groups/recommended")]
    public async Task<ActionResult<GetGroupsResponse>> GetRecommendedGroups(
        [FromQuery] string platform = "facebook",
        [FromQuery] string? keywords = null,
        [FromQuery] int limit = 10)
    {
        try
        {
            var keywordList = string.IsNullOrEmpty(keywords)
                ? new List<string>()
                : keywords.Split(',').Select(k => k.Trim()).ToList();

            var groups = await _seekAndPostService.GetRecommendedGroupsAsync(
                platform, keywordList, limit);

            return Ok(new GetGroupsResponse
            {
                Success = true,
                Groups = groups.Select(g => new GroupInfo
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Url = g.GroupUrl,
                    Description = g.Description,
                    MemberCount = g.MemberCount,
                    IsPublic = g.Privacy == GroupPrivacy.Public,
                    RequiresApproval = g.RequiresApproval,
                    ActivityLevel = GetActivityLevel(g.EngagementScore),
                    QualityScore = g.QualityScore,
                    Keywords = g.Keywords,
                    IsJoined = g.IsJoined,
                    JoinedAt = g.JoinedAt,
                    OurPostCount = g.OurPostCount,
                    LastPostedAt = g.LastPostedAt
                }).ToList(),
                TotalCount = groups.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommended groups");
            return StatusCode(500, new GetGroupsResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    #endregion

    #region Group Join API

    /// <summary>
    /// ขอเข้าร่วมกลุ่ม
    /// </summary>
    [HttpPost("join-group")]
    public async Task<ActionResult<JoinGroupResponse>> JoinGroup(
        [FromBody] JoinGroupRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Joining group {GroupId} on {Platform}",
            request.GroupId, request.Platform);

        try
        {
            var worker = _workerFactory.GetWorker(request.Platform);
            if (worker == null)
            {
                return BadRequest(new JoinGroupResponse
                {
                    Success = false,
                    Error = $"Platform '{request.Platform}' not supported"
                });
            }

            // Attempt to join group via worker
            var result = await JoinGroupViaWorkerAsync(
                worker,
                request.Platform,
                request.GroupId,
                request.GroupUrl,
                ct
            );

            // Update group status in database
            var groups = await _seekAndPostService.GetAllGroupsAsync(request.Platform);
            var group = groups.FirstOrDefault(g => g.GroupId == request.GroupId);

            if (group != null)
            {
                if (result.Joined)
                {
                    group.IsJoined = true;
                    group.JoinedAt = DateTime.UtcNow;
                }
                group.AdminApprovalStatus = result.Joined ? "approved" : "pending";
                await _seekAndPostService.SaveDiscoveredGroupAsync(group);
            }

            return Ok(new JoinGroupResponse
            {
                Success = result.Success,
                Joined = result.Joined,
                RequiresApproval = result.RequiresApproval,
                Message = result.Message,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join group {GroupId}", request.GroupId);
            return StatusCode(500, new JoinGroupResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    #endregion

    #region Group Post API

    /// <summary>
    /// โพสต์ไปยังกลุ่ม
    /// </summary>
    [HttpPost("post-to-group")]
    public async Task<ActionResult<PostToGroupResponse>> PostToGroup(
        [FromBody] PostToGroupRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Posting to group {GroupId} on {Platform}",
            request.GroupId, request.Platform);

        try
        {
            var worker = _workerFactory.GetWorker(request.Platform);
            if (worker == null)
            {
                return BadRequest(new PostToGroupResponse
                {
                    Success = false,
                    Error = $"Platform '{request.Platform}' not supported"
                });
            }

            // Post to group via worker
            var result = await PostToGroupViaWorkerAsync(
                worker,
                request.Platform,
                request.GroupId,
                request.Content,
                request.MediaUrls,
                ct
            );

            // Update group statistics
            var groups = await _seekAndPostService.GetAllGroupsAsync(request.Platform);
            var group = groups.FirstOrDefault(g => g.GroupId == request.GroupId);

            if (group != null)
            {
                group.OurPostCount++;
                if (result.Success)
                {
                    group.OurSuccessfulPosts++;
                }
                group.LastPostedAt = DateTime.UtcNow;

                // Check if banned
                if (!result.Success && IsBanError(result.Error))
                {
                    group.IsBanned = true;
                    group.BanReason = result.Error;
                }

                await _seekAndPostService.SaveDiscoveredGroupAsync(group);
            }

            return Ok(new PostToGroupResponse
            {
                Success = result.Success,
                PostId = result.PostId,
                PostUrl = result.PostUrl,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to group {GroupId}", request.GroupId);
            return StatusCode(500, new PostToGroupResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// โพสต์ไปยังหลายกลุ่มพร้อมกัน
    /// </summary>
    [HttpPost("post-to-groups")]
    public async Task<ActionResult<PostToMultipleGroupsResponse>> PostToMultipleGroups(
        [FromBody] PostToMultipleGroupsRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Posting to {Count} groups on {Platform}",
            request.GroupIds.Count, request.Platform);

        var results = new List<GroupPostResult>();

        foreach (var groupId in request.GroupIds)
        {
            var postResult = await PostToGroup(new PostToGroupRequest
            {
                Platform = request.Platform,
                GroupId = groupId,
                Content = request.Content,
                MediaUrls = request.MediaUrls
            }, ct);

            var response = (postResult.Result as OkObjectResult)?.Value as PostToGroupResponse;

            results.Add(new GroupPostResult
            {
                GroupId = groupId,
                Success = response?.Success ?? false,
                PostId = response?.PostId,
                Error = response?.Error
            });

            // Rate limiting between posts
            if (request.DelayBetweenPostsMs > 0)
            {
                await Task.Delay(request.DelayBetweenPostsMs, ct);
            }
            else
            {
                await Task.Delay(Random.Shared.Next(30000, 120000), ct);
            }
        }

        return Ok(new PostToMultipleGroupsResponse
        {
            Success = results.Any(r => r.Success),
            TotalGroups = request.GroupIds.Count,
            SuccessfulPosts = results.Count(r => r.Success),
            FailedPosts = results.Count(r => !r.Success),
            Results = results
        });
    }

    #endregion

    #region Statistics API

    /// <summary>
    /// ดึงสถิติ Seek and Post
    /// </summary>
    [HttpGet("statistics")]
    public async Task<ActionResult<SeekAndPostStatistics>> GetStatistics()
    {
        try
        {
            var stats = await _seekAndPostService.GetStatisticsAsync();

            return Ok(new SeekAndPostStatistics
            {
                Success = true,
                PlatformStats = stats
                    .Where(kv => kv.Key.EndsWith("_groups"))
                    .ToDictionary(
                        kv => kv.Key.Replace("_groups", ""),
                        kv => new PlatformStatistics
                        {
                            TotalGroups = Convert.ToInt32(kv.Value),
                            JoinedGroups = Convert.ToInt32(stats.GetValueOrDefault($"{kv.Key.Replace("_groups", "")}_joined", 0)),
                            TotalPosts = Convert.ToInt32(stats.GetValueOrDefault($"{kv.Key.Replace("_groups", "")}_posts", 0))
                        }
                    ),
                TotalTemplates = Convert.ToInt32(stats.GetValueOrDefault("total_templates", 0)),
                EnabledTemplates = Convert.ToInt32(stats.GetValueOrDefault("enabled_templates", 0))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get statistics");
            return StatusCode(500, new SeekAndPostStatistics
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    #endregion

    #region Workflow Templates API

    /// <summary>
    /// ดึง workflow templates ทั้งหมด
    /// </summary>
    [HttpGet("templates")]
    public async Task<ActionResult<WorkflowTemplatesResponse>> GetWorkflowTemplates(
        [FromQuery] string? category = null,
        [FromQuery] string? platform = null)
    {
        try
        {
            var templates = await _seekAndPostService.GetWorkflowTemplatesAsync(category, platform);

            return Ok(new WorkflowTemplatesResponse
            {
                Success = true,
                Templates = templates.Select(t => new WorkflowTemplateInfo
                {
                    Id = t.Id,
                    Name = t.Name,
                    NameTh = t.NameTh,
                    Description = t.Description,
                    DescriptionTh = t.DescriptionTh,
                    Category = t.Category,
                    Icon = t.Icon,
                    Color = t.Color,
                    SupportedPlatforms = t.SupportedPlatforms,
                    IsBuiltIn = t.IsBuiltIn,
                    IsEnabled = t.IsEnabled,
                    UsageCount = t.UsageCount,
                    SuccessRate = t.SuccessRate,
                    Variables = t.Variables.ToDictionary(
                        v => v.Key,
                        v => new WorkflowVariableInfo
                        {
                            Name = v.Value.Name,
                            Type = v.Value.Type,
                            Label = v.Value.Label,
                            LabelTh = v.Value.LabelTh,
                            DefaultValue = v.Value.DefaultValue?.ToString(),
                            IsRequired = v.Value.IsRequired,
                            Options = v.Value.Options
                        }
                    )
                }).ToList(),
                TotalCount = templates.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow templates");
            return StatusCode(500, new WorkflowTemplatesResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// รัน workflow template
    /// </summary>
    [HttpPost("execute-workflow")]
    public async Task<ActionResult<ExecuteWorkflowResponse>> ExecuteWorkflow(
        [FromBody] ExecuteWorkflowTemplateRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Executing workflow template for {Platform}", request.Platform);

        try
        {
            // Generate content using template variables
            var contentGenerator = new ContentGeneratorService();
            var prompt = BuildPromptFromVariables(request.Variables);

            // Create BrandInfo if brand_info is provided
            BrandInfo? brandInfo = null;
            if (request.Variables.TryGetValue("brand_info", out var brandInfoStr) && !string.IsNullOrEmpty(brandInfoStr))
            {
                brandInfo = new BrandInfo { Name = brandInfoStr };
            }

            var content = await contentGenerator.GenerateAsync(
                prompt,
                brandInfo,
                request.Platform,
                request.Variables.GetValueOrDefault("language", "th"),
                ct
            );

            return Ok(new ExecuteWorkflowResponse
            {
                Success = true,
                Output = content.Text,
                Hashtags = content.Hashtags,
                Provider = content.Provider
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute workflow");
            return StatusCode(500, new ExecuteWorkflowResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    #endregion

    #region Helper Methods

    private async Task<List<DiscoveredGroup>> SearchGroupsViaWorkerAsync(
        IPlatformWorker worker,
        string platform,
        List<string> keywords,
        List<string>? excludeKeywords,
        int minMembers,
        int maxMembers,
        int limit,
        CancellationToken ct)
    {
        var groups = new List<DiscoveredGroup>();

        // Use GroupSearchService to search for groups
        var searchService = new GroupSearchService();
        var searchResults = await searchService.SearchGroupsAsync(
            platform,
            keywords,
            limit,
            ct
        );

        foreach (var result in searchResults)
        {
            // Filter by member count
            if (result.MemberCount < minMembers || result.MemberCount > maxMembers)
                continue;

            // Filter by exclude keywords
            if (excludeKeywords != null && excludeKeywords.Any())
            {
                var groupText = $"{result.Name} {result.Description}".ToLower();
                if (excludeKeywords.Any(k => groupText.Contains(k.ToLower())))
                    continue;
            }

            groups.Add(new DiscoveredGroup
            {
                Platform = platform,
                GroupId = result.Id,
                GroupName = result.Name,
                GroupUrl = result.Url,
                Description = result.Description,
                Category = result.Category,
                Keywords = keywords,
                MemberCount = result.MemberCount,
                Privacy = result.IsPublic ? GroupPrivacy.Public : GroupPrivacy.Private,
                RequiresApproval = !result.IsPublic,
                EngagementScore = result.EngagementScore,
                PostsPerDay = result.PostsPerDay,
                LastActivityAt = result.LastActivityAt,
                DiscoveredBy = "keyword_search",
                DiscoveryKeyword = string.Join(",", keywords)
            });
        }

        return groups;
    }

    private async Task<JoinResult> JoinGroupViaWorkerAsync(
        IPlatformWorker worker,
        string platform,
        string groupId,
        string? groupUrl,
        CancellationToken ct)
    {
        // Use GroupSearchService to join group
        var searchService = new GroupSearchService();
        return await searchService.JoinGroupAsync(platform, groupId, groupUrl, ct);
    }

    private async Task<PostResult> PostToGroupViaWorkerAsync(
        IPlatformWorker worker,
        string platform,
        string groupId,
        string content,
        List<string>? mediaUrls,
        CancellationToken ct)
    {
        // Use PostPublisherService to post to group
        var publisherService = new PostPublisherService();
        return await publisherService.PostToGroupAsync(
            platform,
            groupId,
            content,
            mediaUrls,
            ct
        );
    }

    private static string GetActivityLevel(double engagementScore)
    {
        return engagementScore switch
        {
            >= 0.7 => "very_active",
            >= 0.5 => "active",
            >= 0.3 => "moderate",
            _ => "low"
        };
    }

    private static bool IsBanError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return false;

        var banIndicators = new[]
        {
            "banned", "blocked", "suspended", "disabled",
            "restricted", "violated", "spam"
        };

        return banIndicators.Any(i => error.Contains(i, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildPromptFromVariables(Dictionary<string, string> variables)
    {
        var parts = new List<string>();

        if (variables.TryGetValue("topic", out var topic))
            parts.Add($"เขียนเกี่ยวกับ: {topic}");

        if (variables.TryGetValue("product_name", out var productName))
            parts.Add($"สินค้า: {productName}");

        if (variables.TryGetValue("product_description", out var productDesc))
            parts.Add($"รายละเอียด: {productDesc}");

        if (variables.TryGetValue("keywords", out var keywords))
            parts.Add($"คีย์เวิร์ด: {keywords}");

        if (variables.TryGetValue("tone", out var tone))
            parts.Add($"โทนการเขียน: {tone}");

        return string.Join("\n", parts);
    }

    #endregion
}

#region Request/Response Models

public class SeekGroupsRequest
{
    public string Platform { get; set; } = "facebook";
    public List<string> Keywords { get; set; } = new();
    public List<string>? ExcludeKeywords { get; set; }
    public int MinMembers { get; set; } = 100;
    public int MaxMembers { get; set; } = 1000000;
    public int Limit { get; set; } = 20;
}

public class SeekGroupsResponse
{
    public bool Success { get; set; }
    public List<GroupInfo> Groups { get; set; } = new();
    public int TotalFound { get; set; }
    public string? Error { get; set; }
}

public class GetGroupsResponse
{
    public bool Success { get; set; }
    public List<GroupInfo> Groups { get; set; } = new();
    public int TotalCount { get; set; }
    public string? Error { get; set; }
}

public class GroupInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Url { get; set; }
    public string? Description { get; set; }
    public int MemberCount { get; set; }
    public bool IsPublic { get; set; }
    public bool RequiresApproval { get; set; }
    public string ActivityLevel { get; set; } = "";
    public double QualityScore { get; set; }
    public List<string> Keywords { get; set; } = new();
    public bool IsJoined { get; set; }
    public DateTime? JoinedAt { get; set; }
    public int OurPostCount { get; set; }
    public DateTime? LastPostedAt { get; set; }
    public bool IsBanned { get; set; }
}

public class JoinGroupRequest
{
    public string Platform { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string? GroupUrl { get; set; }
}

public class JoinGroupResponse
{
    public bool Success { get; set; }
    public bool Joined { get; set; }
    public bool RequiresApproval { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public class PostToGroupRequest
{
    public string Platform { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string>? MediaUrls { get; set; }
}

public class PostToGroupResponse
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public string? Error { get; set; }
}

public class PostToMultipleGroupsRequest
{
    public string Platform { get; set; } = "";
    public List<string> GroupIds { get; set; } = new();
    public string Content { get; set; } = "";
    public List<string>? MediaUrls { get; set; }
    public int DelayBetweenPostsMs { get; set; } = 0;
}

public class PostToMultipleGroupsResponse
{
    public bool Success { get; set; }
    public int TotalGroups { get; set; }
    public int SuccessfulPosts { get; set; }
    public int FailedPosts { get; set; }
    public List<GroupPostResult> Results { get; set; } = new();
}

public class GroupPostResult
{
    public string GroupId { get; set; } = "";
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? Error { get; set; }
}

public class SeekAndPostStatistics
{
    public bool Success { get; set; }
    public Dictionary<string, PlatformStatistics> PlatformStats { get; set; } = new();
    public int TotalTemplates { get; set; }
    public int EnabledTemplates { get; set; }
    public string? Error { get; set; }
}

public class PlatformStatistics
{
    public int TotalGroups { get; set; }
    public int JoinedGroups { get; set; }
    public int TotalPosts { get; set; }
}

public class WorkflowTemplatesResponse
{
    public bool Success { get; set; }
    public List<WorkflowTemplateInfo> Templates { get; set; } = new();
    public int TotalCount { get; set; }
    public string? Error { get; set; }
}

public class WorkflowTemplateInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string NameTh { get; set; } = "";
    public string Description { get; set; } = "";
    public string DescriptionTh { get; set; } = "";
    public string Category { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Color { get; set; } = "";
    public List<string> SupportedPlatforms { get; set; } = new();
    public bool IsBuiltIn { get; set; }
    public bool IsEnabled { get; set; }
    public int UsageCount { get; set; }
    public double SuccessRate { get; set; }
    public Dictionary<string, WorkflowVariableInfo> Variables { get; set; } = new();
}

public class WorkflowVariableInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
    public string LabelTh { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool IsRequired { get; set; }
    public List<string>? Options { get; set; }
}

public class ExecuteWorkflowTemplateRequest
{
    public string Platform { get; set; } = "";
    public string? WorkflowJson { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class ExecuteWorkflowResponse
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? Provider { get; set; }
    public string? Error { get; set; }
}

#endregion
