using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AIManager.Core.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// SEEK AND POST SYSTEM - Intelligent Group Discovery & Posting
// ═══════════════════════════════════════════════════════════════════════════════

#region Models

/// <summary>
/// Discovered group/community information
/// </summary>
public class DiscoveredGroup
{
    public long Id { get; set; }
    public string Platform { get; set; } = "";
    public string GroupId { get; set; } = ""; // Platform-specific ID
    public string GroupName { get; set; } = "";
    public string? GroupUrl { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    // Group characteristics
    public int MemberCount { get; set; }
    public GroupPrivacy Privacy { get; set; } = GroupPrivacy.Public;
    public bool RequiresApproval { get; set; }
    public string? AdminApprovalStatus { get; set; }
    public bool IsJoined { get; set; }
    public DateTime? JoinedAt { get; set; }

    // Engagement metrics
    public double EngagementScore { get; set; }
    public int PostsPerDay { get; set; }
    public double AverageResponseTime { get; set; }
    public DateTime? LastActivityAt { get; set; }

    // Our activity
    public int OurPostCount { get; set; }
    public int OurSuccessfulPosts { get; set; }
    public DateTime? LastPostedAt { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }

    // Discovery info
    public string DiscoveredBy { get; set; } = ""; // keyword, recommendation, manual
    public string? DiscoveryKeyword { get; set; }
    public DateTime DiscoveredAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Quality scoring
    public double QualityScore { get; set; } // 0-1, calculated from various factors
    public bool IsVerified { get; set; }
    public bool IsRecommended { get; set; }
}

public enum GroupPrivacy
{
    Public,
    Private,
    Secret,
    Restricted
}

/// <summary>
/// Workflow template for different use cases
/// </summary>
public class WorkflowTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string NameTh { get; set; } = ""; // Thai name
    public string Description { get; set; } = "";
    public string DescriptionTh { get; set; } = "";
    public string Category { get; set; } = ""; // marketing, engagement, discovery, etc.
    public string Icon { get; set; } = "Workflow"; // MaterialDesign icon name
    public string Color { get; set; } = "#8B5CF6"; // Accent color

    // Target platforms
    public List<string> SupportedPlatforms { get; set; } = new();
    public bool IsMultiPlatform { get; set; }

    // Template content
    public string WorkflowJson { get; set; } = ""; // The actual workflow graph JSON
    public Dictionary<string, WorkflowVariable> Variables { get; set; } = new();
    public List<string> RequiredInputs { get; set; } = new();

    // Settings
    public bool IsBuiltIn { get; set; } = true; // System templates vs user-created
    public bool IsEnabled { get; set; } = true;
    public bool IsPremium { get; set; }
    public int SortOrder { get; set; }

    // Stats
    public int UsageCount { get; set; }
    public double SuccessRate { get; set; }
    public double AverageRating { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class WorkflowVariable
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "text"; // text, number, select, multiselect, image, boolean
    public string Label { get; set; } = "";
    public string LabelTh { get; set; } = "";
    public string? Description { get; set; }
    public object? DefaultValue { get; set; }
    public List<string>? Options { get; set; } // For select/multiselect
    public bool IsRequired { get; set; } = true;
    public string? Validation { get; set; } // Regex or validation rule
}

/// <summary>
/// Seek and Post task configuration
/// </summary>
public class SeekAndPostTask
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Platform { get; set; } = "";

    // Search criteria
    public List<string> Keywords { get; set; } = new();
    public List<string> ExcludeKeywords { get; set; } = new();
    public string? Category { get; set; }
    public int MinMembers { get; set; } = 100;
    public int MaxMembers { get; set; } = 1000000;

    // Posting settings
    public string? WorkflowTemplateId { get; set; }
    public string? ContentTemplate { get; set; }
    public int MaxPostsPerGroup { get; set; } = 1;
    public int PostIntervalHours { get; set; } = 24;
    public int MaxGroupsPerDay { get; set; } = 10;

    // Schedule
    public bool IsEnabled { get; set; } = true;
    public string? Schedule { get; set; } // Cron or time list

    // Status
    public TaskStatus Status { get; set; } = TaskStatus.Idle;
    public int GroupsDiscovered { get; set; }
    public int GroupsJoined { get; set; }
    public int PostsMade { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum TaskStatus
{
    Idle,
    Seeking,
    Joining,
    Posting,
    Paused,
    Completed,
    Failed
}

#endregion

/// <summary>
/// Service for intelligent group discovery and automated posting
/// </summary>
public class SeekAndPostService
{
    private readonly ILogger<SeekAndPostService> _logger;
    private readonly AILearningDatabaseService _dbService;
    private readonly ContentGeneratorService _contentGenerator;

    private readonly ConcurrentDictionary<string, List<DiscoveredGroup>> _groupCache = new();
    private readonly ConcurrentDictionary<long, SeekAndPostTask> _activeTasks = new();

    public event EventHandler<DiscoveredGroup>? GroupDiscovered;
    public event EventHandler<(SeekAndPostTask Task, string Status)>? TaskStatusChanged;
    public event EventHandler<(string Platform, string GroupId, bool Success)>? PostCompleted;

    public SeekAndPostService(
        ILogger<SeekAndPostService> logger,
        AILearningDatabaseService dbService,
        ContentGeneratorService contentGenerator)
    {
        _logger = logger;
        _dbService = dbService;
        _contentGenerator = contentGenerator;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GROUP DISCOVERY
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Search for groups matching keywords
    /// </summary>
    public async Task<List<DiscoveredGroup>> SearchGroupsAsync(
        string platform,
        List<string> keywords,
        int limit = 50,
        CancellationToken ct = default)
    {
        var results = new List<DiscoveredGroup>();

        // First check cache
        var cacheKey = $"{platform}:{string.Join(",", keywords)}";
        if (_groupCache.TryGetValue(cacheKey, out var cached))
        {
            _logger.LogInformation("Returning {Count} cached groups for {Platform}", cached.Count, platform);
            return cached;
        }

        // Check database for existing groups matching keywords
        var existingGroups = await GetGroupsByKeywordsAsync(platform, keywords);
        results.AddRange(existingGroups);

        // If we don't have enough, trigger discovery (actual platform API would go here)
        if (results.Count < limit)
        {
            _logger.LogInformation("Not enough cached groups, discovery needed for {Platform}", platform);
            // Platform-specific discovery would be implemented here
            // For now, we just return what we have
        }

        // Cache results
        _groupCache[cacheKey] = results;

        return results.Take(limit).ToList();
    }

    /// <summary>
    /// Save discovered group to learning database
    /// </summary>
    public async Task<long> SaveDiscoveredGroupAsync(DiscoveredGroup group)
    {
        group.UpdatedAt = DateTime.UtcNow;
        if (group.Id == 0)
        {
            group.DiscoveredAt = DateTime.UtcNow;
        }

        var record = new LearningRecord
        {
            Category = $"groups_{group.Platform}",
            Key = group.GroupId,
            Value = JsonConvert.SerializeObject(group),
            Metadata = JsonConvert.SerializeObject(new
            {
                group.GroupName,
                group.Category,
                group.MemberCount,
                group.QualityScore
            }),
            Confidence = group.QualityScore,
            UsageCount = group.OurPostCount
        };

        group.Id = await _dbService.SaveLearningRecordAsync(record);

        GroupDiscovered?.Invoke(this, group);
        _logger.LogInformation("Saved group: {Name} ({Platform})", group.GroupName, group.Platform);

        return group.Id;
    }

    /// <summary>
    /// Get groups by keywords from database
    /// </summary>
    public async Task<List<DiscoveredGroup>> GetGroupsByKeywordsAsync(
        string platform,
        List<string> keywords)
    {
        var results = new List<DiscoveredGroup>();

        try
        {
            var records = await _dbService.GetLearningRecordsByCategoryAsync($"groups_{platform}");

            foreach (var record in records)
            {
                try
                {
                    var group = JsonConvert.DeserializeObject<DiscoveredGroup>(record.Value);
                    if (group == null) continue;

                    // Check if group matches any keyword
                    var groupKeywords = group.Keywords.Concat(new[] { group.GroupName, group.Description ?? "" })
                        .Select(k => k.ToLower());

                    if (keywords.Any(k => groupKeywords.Any(gk => gk.Contains(k.ToLower()))))
                    {
                        group.Id = record.Id;
                        results.Add(group);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get groups by keywords");
        }

        return results.OrderByDescending(g => g.QualityScore).ToList();
    }

    /// <summary>
    /// Get all groups for a platform
    /// </summary>
    public async Task<List<DiscoveredGroup>> GetAllGroupsAsync(string platform)
    {
        var results = new List<DiscoveredGroup>();

        try
        {
            var records = await _dbService.GetLearningRecordsByCategoryAsync($"groups_{platform}");

            foreach (var record in records)
            {
                try
                {
                    var group = JsonConvert.DeserializeObject<DiscoveredGroup>(record.Value);
                    if (group != null)
                    {
                        group.Id = record.Id;
                        results.Add(group);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all groups for {Platform}", platform);
        }

        return results;
    }

    /// <summary>
    /// Get recommended groups for posting
    /// </summary>
    public async Task<List<DiscoveredGroup>> GetRecommendedGroupsAsync(
        string platform,
        List<string> keywords,
        int limit = 10)
    {
        var groups = await GetGroupsByKeywordsAsync(platform, keywords);

        // Filter and sort by quality
        return groups
            .Where(g => g.IsJoined && !g.IsBanned)
            .Where(g => g.QualityScore >= 0.5)
            .Where(g => g.LastPostedAt == null ||
                        (DateTime.UtcNow - g.LastPostedAt.Value).TotalHours >= 24)
            .OrderByDescending(g => g.QualityScore)
            .ThenByDescending(g => g.EngagementScore)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Calculate quality score for a group
    /// </summary>
    public double CalculateGroupQualityScore(DiscoveredGroup group)
    {
        var score = 0.0;

        // Member count factor (prefer medium-sized groups)
        if (group.MemberCount >= 1000 && group.MemberCount <= 100000)
            score += 0.25;
        else if (group.MemberCount >= 100 && group.MemberCount < 1000)
            score += 0.15;
        else if (group.MemberCount > 100000)
            score += 0.1;

        // Engagement score
        score += group.EngagementScore * 0.25;

        // Activity factor
        if (group.LastActivityAt.HasValue)
        {
            var hoursSinceActivity = (DateTime.UtcNow - group.LastActivityAt.Value).TotalHours;
            if (hoursSinceActivity < 24)
                score += 0.2;
            else if (hoursSinceActivity < 72)
                score += 0.1;
        }

        // Success rate factor
        if (group.OurPostCount > 0)
        {
            var successRate = (double)group.OurSuccessfulPosts / group.OurPostCount;
            score += successRate * 0.2;
        }
        else
        {
            score += 0.1; // Bonus for new groups we haven't tried
        }

        // Verification and recommendation bonuses
        if (group.IsVerified) score += 0.05;
        if (group.IsRecommended) score += 0.05;

        return Math.Min(1.0, score);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW TEMPLATES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get all workflow templates
    /// </summary>
    public async Task<List<WorkflowTemplate>> GetWorkflowTemplatesAsync(
        string? category = null,
        string? platform = null)
    {
        var templates = new List<WorkflowTemplate>();

        try
        {
            var records = await _dbService.GetLearningRecordsByCategoryAsync("workflow_templates");

            foreach (var record in records)
            {
                try
                {
                    var template = JsonConvert.DeserializeObject<WorkflowTemplate>(record.Value);
                    if (template == null) continue;

                    template.Id = record.Id;

                    // Filter by category
                    if (!string.IsNullOrEmpty(category) && template.Category != category)
                        continue;

                    // Filter by platform
                    if (!string.IsNullOrEmpty(platform) &&
                        !template.IsMultiPlatform &&
                        !template.SupportedPlatforms.Contains(platform))
                        continue;

                    templates.Add(template);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get workflow templates");
        }

        // If no templates exist, seed defaults
        if (templates.Count == 0)
        {
            await SeedDefaultTemplatesAsync();
            return await GetWorkflowTemplatesAsync(category, platform);
        }

        return templates.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToList();
    }

    /// <summary>
    /// Save workflow template
    /// </summary>
    public async Task<long> SaveWorkflowTemplateAsync(WorkflowTemplate template)
    {
        template.UpdatedAt = DateTime.UtcNow;
        if (template.Id == 0)
        {
            template.CreatedAt = DateTime.UtcNow;
        }

        var record = new LearningRecord
        {
            Category = "workflow_templates",
            Key = template.Name.ToLower().Replace(" ", "_"),
            Value = JsonConvert.SerializeObject(template),
            Metadata = JsonConvert.SerializeObject(new
            {
                template.Name,
                template.Category,
                template.SupportedPlatforms
            })
        };

        if (template.Id > 0)
        {
            record.Id = template.Id;
        }

        template.Id = await _dbService.SaveLearningRecordAsync(record);

        _logger.LogInformation("Saved workflow template: {Name}", template.Name);
        return template.Id;
    }

    /// <summary>
    /// Seed default workflow templates
    /// </summary>
    public async Task SeedDefaultTemplatesAsync()
    {
        var templates = GetBuiltInTemplates();

        foreach (var template in templates)
        {
            try
            {
                await SaveWorkflowTemplateAsync(template);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed template: {Name}", template.Name);
            }
        }

        _logger.LogInformation("Seeded {Count} default workflow templates", templates.Count);
    }

    /// <summary>
    /// Get built-in workflow templates
    /// </summary>
    private List<WorkflowTemplate> GetBuiltInTemplates()
    {
        return new List<WorkflowTemplate>
        {
            // ═══════════════════════════════════════════════════════════════
            // PRODUCT MARKETING TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            new WorkflowTemplate
            {
                Name = "Product Promotion Post",
                NameTh = "โพสต์โปรโมทสินค้า",
                Description = "Generate engaging product promotion content with AI-powered copywriting",
                DescriptionTh = "สร้างเนื้อหาโปรโมทสินค้าที่น่าสนใจด้วย AI",
                Category = "marketing",
                Icon = "ShoppingCart",
                Color = "#10B981",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "tiktok", "line" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["product_name"] = new WorkflowVariable
                    {
                        Name = "product_name",
                        Type = "text",
                        Label = "Product Name",
                        LabelTh = "ชื่อสินค้า",
                        IsRequired = true
                    },
                    ["product_description"] = new WorkflowVariable
                    {
                        Name = "product_description",
                        Type = "text",
                        Label = "Product Description",
                        LabelTh = "รายละเอียดสินค้า",
                        IsRequired = true
                    },
                    ["price"] = new WorkflowVariable
                    {
                        Name = "price",
                        Type = "text",
                        Label = "Price",
                        LabelTh = "ราคา",
                        IsRequired = false
                    },
                    ["promotion"] = new WorkflowVariable
                    {
                        Name = "promotion",
                        Type = "text",
                        Label = "Special Promotion",
                        LabelTh = "โปรโมชั่นพิเศษ",
                        IsRequired = false
                    },
                    ["tone"] = new WorkflowVariable
                    {
                        Name = "tone",
                        Type = "select",
                        Label = "Tone of Voice",
                        LabelTh = "โทนการเขียน",
                        DefaultValue = "friendly",
                        Options = new List<string> { "friendly", "professional", "casual", "urgent", "luxury" }
                    },
                    ["include_hashtags"] = new WorkflowVariable
                    {
                        Name = "include_hashtags",
                        Type = "boolean",
                        Label = "Include Hashtags",
                        LabelTh = "ใส่แฮชแท็ก",
                        DefaultValue = true
                    }
                },
                WorkflowJson = CreateProductPromoWorkflow(),
                SortOrder = 1
            },

            new WorkflowTemplate
            {
                Name = "Flash Sale Announcement",
                NameTh = "ประกาศ Flash Sale",
                Description = "Create urgency-driven flash sale announcements",
                DescriptionTh = "สร้างประกาศ Flash Sale ที่กระตุ้นความรู้สึกเร่งด่วน",
                Category = "marketing",
                Icon = "Flash",
                Color = "#EF4444",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "line", "twitter" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["sale_name"] = new WorkflowVariable { Name = "sale_name", Type = "text", Label = "Sale Name", LabelTh = "ชื่อโปรโมชั่น", IsRequired = true },
                    ["discount"] = new WorkflowVariable { Name = "discount", Type = "text", Label = "Discount", LabelTh = "ส่วนลด", DefaultValue = "50%" },
                    ["duration"] = new WorkflowVariable { Name = "duration", Type = "text", Label = "Duration", LabelTh = "ระยะเวลา", DefaultValue = "24 ชั่วโมง" },
                    ["products"] = new WorkflowVariable { Name = "products", Type = "text", Label = "Featured Products", LabelTh = "สินค้าเด่น" }
                },
                WorkflowJson = CreateFlashSaleWorkflow(),
                SortOrder = 2
            },

            // ═══════════════════════════════════════════════════════════════
            // CONTENT CREATION TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            new WorkflowTemplate
            {
                Name = "Educational Content",
                NameTh = "เนื้อหาให้ความรู้",
                Description = "Create informative content that educates your audience",
                DescriptionTh = "สร้างเนื้อหาที่ให้ความรู้แก่ผู้ติดตาม",
                Category = "content",
                Icon = "School",
                Color = "#3B82F6",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "tiktok", "youtube", "linkedin" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["topic"] = new WorkflowVariable { Name = "topic", Type = "text", Label = "Topic", LabelTh = "หัวข้อ", IsRequired = true },
                    ["key_points"] = new WorkflowVariable { Name = "key_points", Type = "text", Label = "Key Points", LabelTh = "ประเด็นสำคัญ" },
                    ["target_audience"] = new WorkflowVariable { Name = "target_audience", Type = "text", Label = "Target Audience", LabelTh = "กลุ่มเป้าหมาย" },
                    ["format"] = new WorkflowVariable
                    {
                        Name = "format",
                        Type = "select",
                        Label = "Format",
                        LabelTh = "รูปแบบ",
                        Options = new List<string> { "tips", "how-to", "listicle", "infographic", "story" },
                        DefaultValue = "tips"
                    }
                },
                WorkflowJson = CreateEducationalWorkflow(),
                SortOrder = 3
            },

            new WorkflowTemplate
            {
                Name = "Entertainment Post",
                NameTh = "โพสต์สร้างความบันเทิง",
                Description = "Create fun and engaging entertainment content",
                DescriptionTh = "สร้างเนื้อหาสนุกสนานเพื่อดึงดูดผู้ติดตาม",
                Category = "content",
                Icon = "EmoticonHappy",
                Color = "#F59E0B",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "tiktok", "twitter" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["theme"] = new WorkflowVariable { Name = "theme", Type = "text", Label = "Theme", LabelTh = "ธีม" },
                    ["style"] = new WorkflowVariable
                    {
                        Name = "style",
                        Type = "select",
                        Label = "Style",
                        LabelTh = "สไตล์",
                        Options = new List<string> { "meme", "quiz", "poll", "story", "challenge" },
                        DefaultValue = "meme"
                    }
                },
                WorkflowJson = CreateEntertainmentWorkflow(),
                SortOrder = 4
            },

            // ═══════════════════════════════════════════════════════════════
            // ENGAGEMENT TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            new WorkflowTemplate
            {
                Name = "Engagement Question",
                NameTh = "คำถามสร้างการมีส่วนร่วม",
                Description = "Ask engaging questions to boost comments and interactions",
                DescriptionTh = "ถามคำถามที่กระตุ้นให้ผู้ติดตามมาคอมเมนต์",
                Category = "engagement",
                Icon = "CommentQuestion",
                Color = "#8B5CF6",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "twitter", "line" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["topic"] = new WorkflowVariable { Name = "topic", Type = "text", Label = "Topic", LabelTh = "หัวข้อ", IsRequired = true },
                    ["question_type"] = new WorkflowVariable
                    {
                        Name = "question_type",
                        Type = "select",
                        Label = "Question Type",
                        LabelTh = "ประเภทคำถาม",
                        Options = new List<string> { "opinion", "preference", "experience", "trivia", "would_you_rather" },
                        DefaultValue = "opinion"
                    }
                },
                WorkflowJson = CreateEngagementQuestionWorkflow(),
                SortOrder = 5
            },

            new WorkflowTemplate
            {
                Name = "Poll/Survey",
                NameTh = "โพลล์/แบบสำรวจ",
                Description = "Create interactive polls to engage your audience",
                DescriptionTh = "สร้างโพลล์เพื่อให้ผู้ติดตามมีส่วนร่วม",
                Category = "engagement",
                Icon = "Poll",
                Color = "#06B6D4",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "twitter" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["question"] = new WorkflowVariable { Name = "question", Type = "text", Label = "Poll Question", LabelTh = "คำถามโพลล์", IsRequired = true },
                    ["options"] = new WorkflowVariable { Name = "options", Type = "text", Label = "Options (comma separated)", LabelTh = "ตัวเลือก (คั่นด้วยจุลภาค)" }
                },
                WorkflowJson = CreatePollWorkflow(),
                SortOrder = 6
            },

            // ═══════════════════════════════════════════════════════════════
            // SEEK AND POST TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            new WorkflowTemplate
            {
                Name = "Group Discovery & Join",
                NameTh = "ค้นหาและเข้าร่วมกลุ่ม",
                Description = "Automatically discover and request to join relevant groups",
                DescriptionTh = "ค้นหาและขอเข้าร่วมกลุ่มที่เกี่ยวข้องอัตโนมัติ",
                Category = "seek_and_post",
                Icon = "AccountGroup",
                Color = "#EC4899",
                SupportedPlatforms = new List<string> { "facebook" },
                IsMultiPlatform = false,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["keywords"] = new WorkflowVariable { Name = "keywords", Type = "text", Label = "Search Keywords", LabelTh = "คำค้นหา", IsRequired = true },
                    ["min_members"] = new WorkflowVariable { Name = "min_members", Type = "number", Label = "Minimum Members", LabelTh = "สมาชิกขั้นต่ำ", DefaultValue = 100 },
                    ["max_joins_per_day"] = new WorkflowVariable { Name = "max_joins_per_day", Type = "number", Label = "Max Joins Per Day", LabelTh = "ขอเข้าสูงสุด/วัน", DefaultValue = 5 }
                },
                WorkflowJson = CreateGroupDiscoveryWorkflow(),
                SortOrder = 10
            },

            new WorkflowTemplate
            {
                Name = "Smart Group Post",
                NameTh = "โพสต์กลุ่มอัจฉริยะ",
                Description = "Post to discovered groups with smart timing and content",
                DescriptionTh = "โพสต์ไปยังกลุ่มที่ค้นพบด้วยเวลาและเนื้อหาที่เหมาะสม",
                Category = "seek_and_post",
                Icon = "SendCircle",
                Color = "#14B8A6",
                SupportedPlatforms = new List<string> { "facebook", "line" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["content_topic"] = new WorkflowVariable { Name = "content_topic", Type = "text", Label = "Content Topic", LabelTh = "หัวข้อเนื้อหา", IsRequired = true },
                    ["target_groups"] = new WorkflowVariable { Name = "target_groups", Type = "text", Label = "Target Group Keywords", LabelTh = "คีย์เวิร์ดกลุ่มเป้าหมาย" },
                    ["posts_per_group"] = new WorkflowVariable { Name = "posts_per_group", Type = "number", Label = "Posts Per Group", LabelTh = "โพสต์/กลุ่ม", DefaultValue = 1 },
                    ["smart_timing"] = new WorkflowVariable { Name = "smart_timing", Type = "boolean", Label = "Use Smart Timing", LabelTh = "ใช้เวลาอัจฉริยะ", DefaultValue = true }
                },
                WorkflowJson = CreateSmartGroupPostWorkflow(),
                SortOrder = 11
            },

            // ═══════════════════════════════════════════════════════════════
            // PLATFORM SPECIFIC TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            new WorkflowTemplate
            {
                Name = "Instagram Story",
                NameTh = "Instagram Story",
                Description = "Create engaging Instagram Stories",
                DescriptionTh = "สร้าง Instagram Stories ที่น่าสนใจ",
                Category = "platform_specific",
                Icon = "Instagram",
                Color = "#E4405F",
                SupportedPlatforms = new List<string> { "instagram" },
                IsMultiPlatform = false,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["story_type"] = new WorkflowVariable
                    {
                        Name = "story_type",
                        Type = "select",
                        Label = "Story Type",
                        LabelTh = "ประเภท Story",
                        Options = new List<string> { "behind_scenes", "product_showcase", "poll", "question", "countdown", "announcement" },
                        DefaultValue = "product_showcase"
                    },
                    ["content"] = new WorkflowVariable { Name = "content", Type = "text", Label = "Content", LabelTh = "เนื้อหา", IsRequired = true }
                },
                WorkflowJson = CreateInstagramStoryWorkflow(),
                SortOrder = 20
            },

            new WorkflowTemplate
            {
                Name = "TikTok Script",
                NameTh = "สคริปต์ TikTok",
                Description = "Generate viral TikTok video scripts",
                DescriptionTh = "สร้างสคริปต์วิดีโอ TikTok ให้ viral",
                Category = "platform_specific",
                Icon = "Music",
                Color = "#000000",
                SupportedPlatforms = new List<string> { "tiktok" },
                IsMultiPlatform = false,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["video_topic"] = new WorkflowVariable { Name = "video_topic", Type = "text", Label = "Video Topic", LabelTh = "หัวข้อวิดีโอ", IsRequired = true },
                    ["duration"] = new WorkflowVariable
                    {
                        Name = "duration",
                        Type = "select",
                        Label = "Duration",
                        LabelTh = "ความยาว",
                        Options = new List<string> { "15s", "30s", "60s", "3min" },
                        DefaultValue = "30s"
                    },
                    ["style"] = new WorkflowVariable
                    {
                        Name = "style",
                        Type = "select",
                        Label = "Style",
                        LabelTh = "สไตล์",
                        Options = new List<string> { "tutorial", "comedy", "storytelling", "trend", "review" },
                        DefaultValue = "tutorial"
                    }
                },
                WorkflowJson = CreateTikTokScriptWorkflow(),
                SortOrder = 21
            },

            new WorkflowTemplate
            {
                Name = "LINE Broadcast",
                NameTh = "LINE Broadcast",
                Description = "Create engaging LINE Official Account broadcasts",
                DescriptionTh = "สร้างข้อความ broadcast LINE OA ที่น่าสนใจ",
                Category = "platform_specific",
                Icon = "MessageText",
                Color = "#00B900",
                SupportedPlatforms = new List<string> { "line" },
                IsMultiPlatform = false,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["message_type"] = new WorkflowVariable
                    {
                        Name = "message_type",
                        Type = "select",
                        Label = "Message Type",
                        LabelTh = "ประเภทข้อความ",
                        Options = new List<string> { "promotion", "news", "reminder", "greeting", "survey" },
                        DefaultValue = "promotion"
                    },
                    ["content"] = new WorkflowVariable { Name = "content", Type = "text", Label = "Content", LabelTh = "เนื้อหา", IsRequired = true },
                    ["include_rich_menu"] = new WorkflowVariable { Name = "include_rich_menu", Type = "boolean", Label = "Include Rich Menu", LabelTh = "แนบ Rich Menu", DefaultValue = false }
                },
                WorkflowJson = CreateLineBroadcastWorkflow(),
                SortOrder = 22
            },

            new WorkflowTemplate
            {
                Name = "Twitter Thread",
                NameTh = "Twitter Thread",
                Description = "Create engaging Twitter/X threads",
                DescriptionTh = "สร้าง Twitter Thread ที่น่าสนใจ",
                Category = "platform_specific",
                Icon = "Twitter",
                Color = "#1DA1F2",
                SupportedPlatforms = new List<string> { "twitter" },
                IsMultiPlatform = false,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["topic"] = new WorkflowVariable { Name = "topic", Type = "text", Label = "Thread Topic", LabelTh = "หัวข้อ Thread", IsRequired = true },
                    ["num_tweets"] = new WorkflowVariable { Name = "num_tweets", Type = "number", Label = "Number of Tweets", LabelTh = "จำนวนทวีต", DefaultValue = 5 },
                    ["include_cta"] = new WorkflowVariable { Name = "include_cta", Type = "boolean", Label = "Include Call-to-Action", LabelTh = "ใส่ CTA", DefaultValue = true }
                },
                WorkflowJson = CreateTwitterThreadWorkflow(),
                SortOrder = 23
            },

            // ═══════════════════════════════════════════════════════════════
            // SPECIAL OCCASION TEMPLATES
            // ═══════════════════════════════════════════════════════════════
            new WorkflowTemplate
            {
                Name = "Holiday Greeting",
                NameTh = "อวยพรวันหยุด",
                Description = "Create festive holiday greetings for your audience",
                DescriptionTh = "สร้างคำอวยพรวันหยุดสำหรับผู้ติดตาม",
                Category = "special",
                Icon = "PartyPopper",
                Color = "#F472B6",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "line", "twitter" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["holiday"] = new WorkflowVariable
                    {
                        Name = "holiday",
                        Type = "select",
                        Label = "Holiday",
                        LabelTh = "วันหยุด",
                        Options = new List<string> { "new_year", "songkran", "loy_krathong", "christmas", "valentines", "mothers_day", "fathers_day", "other" },
                        DefaultValue = "new_year"
                    },
                    ["brand_message"] = new WorkflowVariable { Name = "brand_message", Type = "text", Label = "Brand Message", LabelTh = "ข้อความจากแบรนด์" }
                },
                WorkflowJson = CreateHolidayGreetingWorkflow(),
                SortOrder = 30
            },

            new WorkflowTemplate
            {
                Name = "Thank You Post",
                NameTh = "โพสต์ขอบคุณ",
                Description = "Express gratitude to your customers and followers",
                DescriptionTh = "แสดงความขอบคุณลูกค้าและผู้ติดตาม",
                Category = "special",
                Icon = "Heart",
                Color = "#EF4444",
                SupportedPlatforms = new List<string> { "facebook", "instagram", "line" },
                IsMultiPlatform = true,
                Variables = new Dictionary<string, WorkflowVariable>
                {
                    ["occasion"] = new WorkflowVariable { Name = "occasion", Type = "text", Label = "Occasion", LabelTh = "โอกาส" },
                    ["milestone"] = new WorkflowVariable { Name = "milestone", Type = "text", Label = "Milestone", LabelTh = "เหตุการณ์สำคัญ" }
                },
                WorkflowJson = CreateThankYouWorkflow(),
                SortOrder = 31
            }
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW JSON GENERATORS
    // ═══════════════════════════════════════════════════════════════════════

    private static string CreateProductPromoWorkflow()
    {
        var workflow = new
        {
            name = "Product Promotion",
            nodes = new object[]
            {
                new { id = "input_1", type = "TextInput", x = 100, y = 100, data = new { label = "Product Name", variableKey = "product_name" } },
                new { id = "input_2", type = "TextInput", x = 100, y = 200, data = new { label = "Description", variableKey = "product_description" } },
                new { id = "input_3", type = "TextInput", x = 100, y = 300, data = new { label = "Price", variableKey = "price" } },
                new { id = "ai_1", type = "AITextGenerator", x = 400, y = 150, data = new {
                    prompt = "สร้างข้อความโปรโมทสินค้าภาษาไทยที่น่าสนใจสำหรับ {product_name}\nรายละเอียด: {product_description}\nราคา: {price}\n\nเขียนให้กระชับ น่าสนใจ เหมาะสำหรับโพสต์ใน Social Media",
                    maxTokens = 300
                }},
                new { id = "hashtag_1", type = "AITextGenerator", x = 400, y = 300, data = new {
                    prompt = "สร้าง 5-10 hashtags ภาษาไทยและอังกฤษที่เกี่ยวข้องกับ {product_name}",
                    maxTokens = 100
                }},
                new { id = "combine_1", type = "TextCombiner", x = 700, y = 200, data = new { separator = "\n\n" } },
                new { id = "output_1", type = "Output", x = 900, y = 200, data = new { label = "Final Post" } }
            },
            connections = new object[]
            {
                new { from = "input_1", to = "ai_1", fromPort = "output", toPort = "input" },
                new { from = "input_2", to = "ai_1", fromPort = "output", toPort = "context" },
                new { from = "input_3", to = "ai_1", fromPort = "output", toPort = "variables" },
                new { from = "ai_1", to = "combine_1", fromPort = "output", toPort = "text1" },
                new { from = "hashtag_1", to = "combine_1", fromPort = "output", toPort = "text2" },
                new { from = "combine_1", to = "output_1", fromPort = "output", toPort = "input" }
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateFlashSaleWorkflow()
    {
        var workflow = new
        {
            name = "Flash Sale",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างข้อความ Flash Sale ที่สร้างความเร่งด่วน:\nชื่อโปรโมชั่น: {sale_name}\nส่วนลด: {discount}\nระยะเวลา: {duration}\n\nใช้ emoji และภาษาที่กระตุ้นให้รีบซื้อ",
                    maxTokens = 250
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateEducationalWorkflow()
    {
        var workflow = new
        {
            name = "Educational Content",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างเนื้อหาให้ความรู้เกี่ยวกับ: {topic}\nประเด็นสำคัญ: {key_points}\nกลุ่มเป้าหมาย: {target_audience}\nรูปแบบ: {format}\n\nเขียนให้อ่านง่าย เข้าใจง่าย เหมาะกับ Social Media",
                    maxTokens = 400
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateEntertainmentWorkflow()
    {
        var workflow = new
        {
            name = "Entertainment",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างเนื้อหาความบันเทิงสำหรับ Social Media:\nธีม: {theme}\nสไตล์: {style}\n\nทำให้สนุกสนาน น่าสนใจ และเหมาะกับการแชร์",
                    maxTokens = 300
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateEngagementQuestionWorkflow()
    {
        var workflow = new
        {
            name = "Engagement Question",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างคำถามเพื่อกระตุ้นการมีส่วนร่วมใน Social Media:\nหัวข้อ: {topic}\nประเภทคำถาม: {question_type}\n\nคำถามควรน่าสนใจและง่ายต่อการตอบ",
                    maxTokens = 200
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreatePollWorkflow()
    {
        var workflow = new
        {
            name = "Poll",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างโพลล์สำหรับ Social Media:\nคำถาม: {question}\nตัวเลือก: {options}\n\nเพิ่มข้อความเกริ่นนำที่น่าสนใจ",
                    maxTokens = 200
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateGroupDiscoveryWorkflow()
    {
        var workflow = new
        {
            name = "Group Discovery",
            nodes = new object[]
            {
                new { id = "search_1", type = "GroupSearch", x = 300, y = 150, data = new {
                    keywords = "{keywords}",
                    minMembers = "{min_members}",
                    maxJoinsPerDay = "{max_joins_per_day}"
                }},
                new { id = "filter_1", type = "GroupFilter", x = 500, y = 150, data = new {
                    excludeJoined = true,
                    excludeBanned = true
                }},
                new { id = "join_1", type = "GroupJoinRequest", x = 700, y = 150, data = new { placeholder = true } }
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateSmartGroupPostWorkflow()
    {
        var workflow = new
        {
            name = "Smart Group Post",
            nodes = new object[]
            {
                new { id = "groups_1", type = "GetRecommendedGroups", x = 100, y = 150, data = new {
                    keywords = "{target_groups}"
                }},
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างโพสต์สำหรับกลุ่มเกี่ยวกับ: {content_topic}\n\nเขียนให้เป็นธรรมชาติ ไม่โฆษณาจนเกินไป เหมาะกับการโพสต์ในกลุ่ม",
                    maxTokens = 300
                }},
                new { id = "post_1", type = "PostToGroups", x = 500, y = 150, data = new {
                    postsPerGroup = "{posts_per_group}",
                    smartTiming = "{smart_timing}"
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateInstagramStoryWorkflow()
    {
        var workflow = new
        {
            name = "Instagram Story",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างเนื้อหาสำหรับ Instagram Story:\nประเภท: {story_type}\nเนื้อหา: {content}\n\nเขียนให้สั้น กระชับ น่าสนใจ",
                    maxTokens = 150
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateTikTokScriptWorkflow()
    {
        var workflow = new
        {
            name = "TikTok Script",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างสคริปต์วิดีโอ TikTok:\nหัวข้อ: {video_topic}\nความยาว: {duration}\nสไตล์: {style}\n\nรวม hook เปิด, เนื้อหาหลัก, และ call-to-action",
                    maxTokens = 400
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateLineBroadcastWorkflow()
    {
        var workflow = new
        {
            name = "LINE Broadcast",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างข้อความ LINE Broadcast:\nประเภท: {message_type}\nเนื้อหา: {content}\n\nเขียนให้กระชับ ใช้ emoji และจัดรูปแบบให้อ่านง่าย",
                    maxTokens = 250
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateTwitterThreadWorkflow()
    {
        var workflow = new
        {
            name = "Twitter Thread",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้าง Twitter Thread {num_tweets} ทวีตเกี่ยวกับ: {topic}\n\nแต่ละทวีตไม่เกิน 280 ตัวอักษร มี hook ทวีตแรก และ CTA ทวีตสุดท้าย",
                    maxTokens = 600
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateHolidayGreetingWorkflow()
    {
        var workflow = new
        {
            name = "Holiday Greeting",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างคำอวยพรสำหรับ: {holiday}\nข้อความจากแบรนด์: {brand_message}\n\nเขียนให้อบอุ่น จริงใจ และเหมาะกับ Social Media",
                    maxTokens = 200
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    private static string CreateThankYouWorkflow()
    {
        var workflow = new
        {
            name = "Thank You Post",
            nodes = new[]
            {
                new { id = "ai_1", type = "AITextGenerator", x = 300, y = 150, data = new {
                    prompt = "สร้างโพสต์ขอบคุณ:\nโอกาส: {occasion}\nเหตุการณ์สำคัญ: {milestone}\n\nแสดงความขอบคุณอย่างจริงใจ",
                    maxTokens = 200
                }}
            }
        };
        return JsonConvert.SerializeObject(workflow);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TASK MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Create and start a Seek and Post task
    /// </summary>
    public async Task<SeekAndPostTask> CreateTaskAsync(SeekAndPostTask task)
    {
        task.CreatedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        task.Id = DateTime.UtcNow.Ticks;

        var record = new LearningRecord
        {
            Category = "seek_and_post_tasks",
            Key = task.Id.ToString(),
            Value = JsonConvert.SerializeObject(task),
            Metadata = JsonConvert.SerializeObject(new { task.Name, task.Platform, task.Status })
        };

        await _dbService.SaveLearningRecordAsync(record);

        _activeTasks[task.Id] = task;
        _logger.LogInformation("Created Seek and Post task: {Name}", task.Name);

        return task;
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public async Task<Dictionary<string, object>> GetStatisticsAsync()
    {
        var stats = new Dictionary<string, object>();

        var platforms = new[] { "facebook", "instagram", "tiktok", "twitter", "line", "youtube", "linkedin" };

        foreach (var platform in platforms)
        {
            var groups = await GetAllGroupsAsync(platform);
            stats[$"{platform}_groups"] = groups.Count;
            stats[$"{platform}_joined"] = groups.Count(g => g.IsJoined);
            stats[$"{platform}_posts"] = groups.Sum(g => g.OurPostCount);
        }

        var templates = await GetWorkflowTemplatesAsync();
        stats["total_templates"] = templates.Count;
        stats["enabled_templates"] = templates.Count(t => t.IsEnabled);

        return stats;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POSTING LOOP - Continuous posting to groups
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Post content to a single group
    /// </summary>
    public async Task<GroupPostResult> PostToGroupAsync(
        DiscoveredGroup group,
        PostingContent content,
        PostingCredentials credentials,
        CancellationToken ct = default)
    {
        var result = new GroupPostResult
        {
            GroupId = group.GroupId,
            GroupName = group.GroupName,
            Platform = group.Platform
        };

        try
        {
            // Check if group is eligible for posting
            if (group.IsBanned)
            {
                result.Success = false;
                result.Error = "Group is banned";
                return result;
            }

            // Check cooldown period (24 hours default)
            if (group.LastPostedAt.HasValue &&
                (DateTime.UtcNow - group.LastPostedAt.Value).TotalHours < 24)
            {
                result.Success = false;
                result.Error = "Cooldown period not elapsed";
                result.CooldownRemainingHours = 24 - (DateTime.UtcNow - group.LastPostedAt.Value).TotalHours;
                return result;
            }

            // Use platform-specific posting
            var postResult = group.Platform.ToLower() switch
            {
                "facebook" => await PostToFacebookGroupAsync(group.GroupId, content, credentials, ct),
                "line" => await PostToLineGroupAsync(group.GroupId, content, credentials, ct),
                _ => (Success: false, PostId: null as string, Error: $"Platform {group.Platform} not supported for group posting")
            };

            result.Success = postResult.Success;
            result.PostId = postResult.PostId;
            result.Error = postResult.Error;

            // Update group stats
            group.OurPostCount++;
            if (result.Success)
            {
                group.OurSuccessfulPosts++;
                group.LastPostedAt = DateTime.UtcNow;
            }
            group.UpdatedAt = DateTime.UtcNow;

            await SaveDiscoveredGroupAsync(group);

            PostCompleted?.Invoke(this, (group.Platform, group.GroupId, result.Success));

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post to group {GroupName}", group.GroupName);
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Post to Facebook group via Graph API
    /// </summary>
    private async Task<(bool Success, string? PostId, string? Error)> PostToFacebookGroupAsync(
        string groupId,
        PostingContent content,
        PostingCredentials credentials,
        CancellationToken ct)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            var graphApiUrl = "https://graph.facebook.com/v19.0";

            var postData = new Dictionary<string, string>
            {
                ["message"] = content.Text + "\n\n" + FormatHashtags(content.Hashtags),
                ["access_token"] = credentials.AccessToken
            };

            if (!string.IsNullOrEmpty(content.Link))
            {
                postData["link"] = content.Link;
            }

            var response = await httpClient.PostAsync(
                $"{graphApiUrl}/{groupId}/feed",
                new System.Net.Http.FormUrlEncodedContent(postData),
                ct
            );

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            if (result?.ContainsKey("id") == true)
            {
                return (true, result["id"]?.ToString(), null);
            }

            var error = result?.ContainsKey("error") == true
                ? result["error"]?.ToString()
                : "Unknown error";

            return (false, null, error);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Post to LINE OpenChat/Group
    /// </summary>
    private async Task<(bool Success, string? PostId, string? Error)> PostToLineGroupAsync(
        string groupId,
        PostingContent content,
        PostingCredentials credentials,
        CancellationToken ct)
    {
        try
        {
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {credentials.AccessToken}");

            var messageData = new
            {
                to = groupId,
                messages = new[]
                {
                    new
                    {
                        type = "text",
                        text = content.Text + "\n\n" + FormatHashtags(content.Hashtags)
                    }
                }
            };

            var response = await httpClient.PostAsync(
                "https://api.line.me/v2/bot/message/push",
                new System.Net.Http.StringContent(
                    JsonConvert.SerializeObject(messageData),
                    System.Text.Encoding.UTF8,
                    "application/json"
                ),
                ct
            );

            if (response.IsSuccessStatusCode)
            {
                return (true, Guid.NewGuid().ToString(), null);
            }

            var error = await response.Content.ReadAsStringAsync(ct);
            return (false, null, error);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Post to multiple groups with rate limiting
    /// </summary>
    public async Task<List<GroupPostResult>> PostToGroupsAsync(
        List<DiscoveredGroup> groups,
        PostingContent content,
        PostingCredentials credentials,
        int delayBetweenPostsMs = 60000,
        CancellationToken ct = default)
    {
        var results = new List<GroupPostResult>();

        foreach (var group in groups)
        {
            if (ct.IsCancellationRequested) break;

            var result = await PostToGroupAsync(group, content, credentials, ct);
            results.Add(result);

            _logger.LogInformation(
                "Posted to group {GroupName}: {Status}",
                group.GroupName,
                result.Success ? "Success" : result.Error
            );

            // Rate limiting delay
            if (groups.IndexOf(group) < groups.Count - 1)
            {
                await Task.Delay(delayBetweenPostsMs, ct);
            }
        }

        return results;
    }

    /// <summary>
    /// Start continuous posting loop for a task
    /// </summary>
    public async Task StartPostingLoopAsync(
        SeekAndPostTask task,
        PostingCredentials credentials,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Starting posting loop for task: {TaskName}", task.Name);
        task.Status = TaskStatus.Posting;
        TaskStatusChanged?.Invoke(this, (task, "Started"));

        try
        {
            while (!ct.IsCancellationRequested && task.IsEnabled)
            {
                // Get recommended groups
                var groups = await GetRecommendedGroupsAsync(
                    task.Platform,
                    task.Keywords,
                    task.MaxGroupsPerDay
                );

                if (!groups.Any())
                {
                    _logger.LogInformation("No eligible groups found, waiting...");
                    await Task.Delay(TimeSpan.FromHours(1), ct);
                    continue;
                }

                // Generate content
                var content = await GeneratePostContentAsync(task, ct);

                // Post to groups
                var results = await PostToGroupsAsync(
                    groups,
                    content,
                    credentials,
                    task.PostIntervalHours * 1000, // Convert to ms for delay between posts
                    ct
                );

                // Update task stats
                task.PostsMade += results.Count(r => r.Success);
                task.LastRunAt = DateTime.UtcNow;
                task.NextRunAt = DateTime.UtcNow.AddHours(task.PostIntervalHours);
                task.UpdatedAt = DateTime.UtcNow;

                TaskStatusChanged?.Invoke(this, (task, $"Posted to {results.Count(r => r.Success)} groups"));

                // Wait for next posting cycle
                await Task.Delay(TimeSpan.FromHours(task.PostIntervalHours), ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Posting loop cancelled for task: {TaskName}", task.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Posting loop failed for task: {TaskName}", task.Name);
            task.Status = TaskStatus.Failed;
        }

        task.Status = TaskStatus.Completed;
        TaskStatusChanged?.Invoke(this, (task, "Completed"));
    }

    /// <summary>
    /// Generate post content using AI
    /// </summary>
    private async Task<PostingContent> GeneratePostContentAsync(
        SeekAndPostTask task,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(task.ContentTemplate))
        {
            // Use template directly
            return new PostingContent
            {
                Text = task.ContentTemplate,
                Hashtags = task.Keywords.Take(5).ToList()
            };
        }

        // Generate using AI
        var prompt = $"สร้างโพสต์สำหรับ {task.Platform} เกี่ยวกับ: {string.Join(", ", task.Keywords)}";
        var result = await _contentGenerator.GenerateAsync(prompt, null, task.Platform, "th", ct);

        return new PostingContent
        {
            Text = result?.Text ?? string.Join(" ", task.Keywords),
            Hashtags = result?.Hashtags ?? task.Keywords.Take(5).ToList()
        };
    }

    /// <summary>
    /// Format hashtags for posting
    /// </summary>
    private string FormatHashtags(List<string>? hashtags)
    {
        if (hashtags == null || !hashtags.Any()) return "";

        return string.Join(" ", hashtags.Select(h =>
            h.StartsWith("#") ? h : $"#{h}"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // API HELPER METHODS - For controller compatibility
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Post to a single group by ID (API helper)
    /// </summary>
    public async Task<GroupPostResult> PostToGroupAsync(
        string platform,
        string groupId,
        PostingContent content,
        PostingCredentials credentials,
        CancellationToken ct = default)
    {
        var group = new DiscoveredGroup
        {
            GroupId = groupId,
            Platform = platform,
            GroupName = groupId // Use ID as name if unknown
        };

        return await PostToGroupAsync(group, content, credentials, ct);
    }

    /// <summary>
    /// Post to multiple groups by IDs (API helper)
    /// </summary>
    public async Task<List<GroupPostResult>> PostToGroupsAsync(
        string platform,
        List<string> groupIds,
        PostingContent content,
        PostingCredentials credentials,
        TimeSpan delayBetweenPosts,
        CancellationToken ct = default)
    {
        var groups = groupIds.Select(id => new DiscoveredGroup
        {
            GroupId = id,
            Platform = platform,
            GroupName = id
        }).ToList();

        return await PostToGroupsAsync(groups, content, credentials, (int)delayBetweenPosts.TotalMilliseconds, ct);
    }

    /// <summary>
    /// Start posting loop with simplified parameters (API helper)
    /// </summary>
    public async Task StartPostingLoopAsync(
        string platform,
        List<string> groupIds,
        List<string> contentTemplates,
        PostingCredentials credentials,
        int intervalMinutes,
        int maxPostsPerCycle,
        CancellationToken ct = default)
    {
        var task = new SeekAndPostTask
        {
            Name = $"API Loop - {platform}",
            Platform = platform,
            Keywords = new List<string>(),
            ContentTemplate = contentTemplates.FirstOrDefault(),
            PostIntervalHours = intervalMinutes / 60,
            MaxGroupsPerDay = maxPostsPerCycle,
            IsEnabled = true
        };

        await StartPostingLoopAsync(task, credentials, ct);
    }
}

#region Posting Models

public class GroupPostResult
{
    public string GroupId { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Platform { get; set; } = "";
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public string? PostUrl { get; set; }
    public string? Error { get; set; }
    public double? CooldownRemainingHours { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}

public class PostingContent
{
    public string Text { get; set; } = "";
    public List<string>? Images { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? Link { get; set; }
}

public class PostingCredentials
{
    public string AccessToken { get; set; } = "";
    public string? RefreshToken { get; set; }
    public string? PageId { get; set; }
    public string? UserId { get; set; }
}

#endregion
