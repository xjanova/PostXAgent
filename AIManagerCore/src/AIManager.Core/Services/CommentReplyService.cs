using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// COMMENT REPLY SERVICE - Generate and post replies to social media comments
// ═══════════════════════════════════════════════════════════════════════════════

#region Models

public class ReplyGenerationRequest
{
    public SocialComment Comment { get; set; } = new();
    public ToneConfiguration Tone { get; set; } = new();
    public string? PostContent { get; set; }
    public string? BrandName { get; set; }
    public string? BrandContext { get; set; }
    public string Platform { get; set; } = "general";
}

public class GeneratedReply
{
    public string Content { get; set; } = "";
    public double Confidence { get; set; }
    public string? AlternativeReply { get; set; }
    public List<string> SuggestedActions { get; set; } = new();
    public bool RequiresHumanReview { get; set; }
    public string? ReviewReason { get; set; }
}

public class ReplyPostResult
{
    public bool Success { get; set; }
    public string? ReplyId { get; set; }
    public string? Error { get; set; }
    public DateTime? PostedAt { get; set; }
}

public class AutoReplySettings
{
    public bool Enabled { get; set; }
    public int DelaySeconds { get; set; } = 60;
    public int MaxRepliesPerHour { get; set; } = 30;
    public bool SkipNegative { get; set; }
    public bool RequireReviewForNegative { get; set; } = true;
    public double MinConfidence { get; set; } = 0.7;
}

#endregion

public class CommentReplyService
{
    private readonly ILogger<CommentReplyService> _logger;
    private readonly ContentGeneratorService _contentGenerator;
    private readonly TonePersonalityService _toneService;
    private readonly HttpClient _httpClient;

    private const string FacebookGraphUrl = "https://graph.facebook.com/v19.0";

    // Rate limiting
    private readonly Dictionary<string, Queue<DateTime>> _replyHistory = new();
    private readonly object _rateLimitLock = new();

    public event EventHandler<GeneratedReply>? ReplyGenerated;
    public event EventHandler<(SocialComment Comment, ReplyPostResult Result)>? ReplyPosted;

    public CommentReplyService(
        ILogger<CommentReplyService> logger,
        ContentGeneratorService contentGenerator,
        TonePersonalityService toneService)
    {
        _logger = logger;
        _contentGenerator = contentGenerator;
        _toneService = toneService;
        _httpClient = new HttpClient();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // REPLY GENERATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generate a reply for a comment using AI
    /// </summary>
    public async Task<GeneratedReply> GenerateReplyAsync(
        ReplyGenerationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            // Check for keyword triggers first
            var trigger = _toneService.CheckKeywordTriggers(request.Tone, request.Comment.ContentText);
            if (trigger != null)
            {
                _logger.LogInformation("Using keyword trigger response for comment {Id}", request.Comment.Id);
                return new GeneratedReply
                {
                    Content = trigger.Response,
                    Confidence = 0.95,
                    SuggestedActions = trigger.Action != null ? new List<string> { trigger.Action } : new()
                };
            }

            // Build system prompt based on tone
            var systemPrompt = _toneService.BuildSystemPrompt(
                request.Tone,
                request.Platform,
                request.BrandName
            );

            // Build user prompt for reply generation
            var userPrompt = BuildReplyPrompt(request);

            // Generate reply using AI
            // Note: Combine system prompt with user prompt for the generator
            var fullPrompt = $"{systemPrompt}\n\n{userPrompt}";
            var result = await _contentGenerator.GenerateAsync(
                fullPrompt,
                null,
                request.Platform,
                "th",
                ct
            );

            if (result?.Text == null)
            {
                return new GeneratedReply
                {
                    Content = GetFallbackReply(request),
                    Confidence = 0.3,
                    RequiresHumanReview = true,
                    ReviewReason = "AI generation failed, using fallback"
                };
            }

            // Clean up the response
            var reply = CleanReplyContent(result.Text);

            // Adjust to match tone
            reply = _toneService.AdjustResponseToTone(reply, request.Tone);

            // Validate
            var (isValid, violations) = _toneService.ValidateResponse(reply, request.Tone);

            // Calculate confidence
            var confidence = CalculateConfidence(request.Comment, reply, isValid, violations);

            // Check if human review is needed
            var (needsReview, reviewReason) = DetermineIfNeedsReview(request.Comment, confidence);

            var generatedReply = new GeneratedReply
            {
                Content = reply,
                Confidence = confidence,
                RequiresHumanReview = needsReview,
                ReviewReason = reviewReason,
                SuggestedActions = GetSuggestedActions(request.Comment)
            };

            ReplyGenerated?.Invoke(this, generatedReply);

            return generatedReply;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate reply for comment {Id}", request.Comment.Id);
            return new GeneratedReply
            {
                Content = GetFallbackReply(request),
                Confidence = 0.2,
                RequiresHumanReview = true,
                ReviewReason = $"Generation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Build the prompt for reply generation
    /// </summary>
    private string BuildReplyPrompt(ReplyGenerationRequest request)
    {
        var comment = request.Comment;
        var prompt = $@"ตอบคอมเมนต์ต่อไปนี้:

ผู้แสดงความคิดเห็น: {comment.AuthorName}
คอมเมนต์: ""{comment.ContentText}""
Sentiment: {comment.Sentiment}
{(comment.IsQuestion ? "นี่คือคำถาม - ต้องตอบให้ชัดเจน" : "")}";

        if (!string.IsNullOrEmpty(request.PostContent))
        {
            prompt += $@"

โพสต์ต้นฉบับ: ""{request.PostContent}""";
        }

        if (!string.IsNullOrEmpty(request.BrandContext))
        {
            prompt += $@"

ข้อมูลแบรนด์: {request.BrandContext}";
        }

        prompt += @"

กรุณาตอบ:
1. ตอบตรงประเด็นกับคอมเมนต์
2. ใช้โทนตามที่กำหนดไว้
3. ความยาวพอเหมาะ (ไม่สั้นเกินไปไม่ยาวเกินไป)
4. ตอบเฉพาะข้อความตอบกลับ ไม่ต้องมีคำอธิบายเพิ่มเติม

คำตอบ:";

        return prompt;
    }

    /// <summary>
    /// Get fallback reply when AI fails
    /// </summary>
    private string GetFallbackReply(ReplyGenerationRequest request)
    {
        var comment = request.Comment;

        // Get template from tone if available
        if (comment.IsQuestion)
        {
            var template = _toneService.GetRandomTemplate(request.Tone, "question_response");
            if (template != null) return template;
        }

        if (comment.Sentiment == "positive")
        {
            var template = _toneService.GetRandomTemplate(request.Tone, "thank_you");
            if (template != null) return template;
            return "ขอบคุณมากครับ/ค่ะ! 🙏";
        }

        if (comment.Sentiment == "negative")
        {
            var template = _toneService.GetRandomTemplate(request.Tone, "apology");
            if (template != null) return template;
            return "ขอบคุณสำหรับความคิดเห็นครับ/ค่ะ จะนำไปปรับปรุงต่อไป 🙏";
        }

        return "ขอบคุณสำหรับความคิดเห็นครับ/ค่ะ! 😊";
    }

    /// <summary>
    /// Clean up generated reply content
    /// </summary>
    private string CleanReplyContent(string content)
    {
        var cleaned = content.Trim();

        // Remove common AI prefixes
        var prefixes = new[] { "คำตอบ:", "ตอบ:", "Reply:", "Answer:", "Response:" };
        foreach (var prefix in prefixes)
        {
            if (cleaned.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(prefix.Length).Trim();
            }
        }

        // Remove quotes if the entire response is quoted
        if (cleaned.StartsWith("\"") && cleaned.EndsWith("\""))
        {
            cleaned = cleaned.Trim('"');
        }

        return cleaned;
    }

    /// <summary>
    /// Calculate confidence score for the reply
    /// </summary>
    private double CalculateConfidence(SocialComment comment, string reply, bool isValid, List<string> violations)
    {
        var confidence = 0.8; // Base confidence

        // Reduce for violations
        confidence -= violations.Count * 0.1;

        // Reduce for negative comments (harder to respond appropriately)
        if (comment.Sentiment == "negative")
            confidence -= 0.1;

        // Reduce for questions (need accurate answers)
        if (comment.IsQuestion)
            confidence -= 0.05;

        // Reduce for very short or very long replies
        if (reply.Length < 10 || reply.Length > 500)
            confidence -= 0.1;

        return Math.Max(0.1, Math.Min(1.0, confidence));
    }

    /// <summary>
    /// Determine if human review is needed
    /// </summary>
    private (bool NeedsReview, string? Reason) DetermineIfNeedsReview(SocialComment comment, double confidence)
    {
        if (confidence < 0.6)
            return (true, "Low confidence score");

        if (comment.Sentiment == "negative" && comment.Priority >= 10)
            return (true, "High priority negative comment");

        if (comment.IsQuestion && comment.Priority >= 15)
            return (true, "High priority question");

        return (false, null);
    }

    /// <summary>
    /// Get suggested actions based on comment
    /// </summary>
    private List<string> GetSuggestedActions(SocialComment comment)
    {
        var actions = new List<string>();

        if (comment.IsQuestion)
            actions.Add("Verify answer accuracy");

        if (comment.Sentiment == "negative")
        {
            actions.Add("Follow up with customer");
            actions.Add("Check for product/service issue");
        }

        if (comment.LikesCount > 10)
            actions.Add("Consider pinning this comment");

        return actions;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POST REPLIES TO PLATFORMS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Post a reply to a platform
    /// </summary>
    public async Task<ReplyPostResult> PostReplyAsync(
        string platform,
        string commentId,
        string replyContent,
        string accessToken,
        CancellationToken ct = default)
    {
        // Check rate limit
        if (!CheckRateLimit(platform))
        {
            return new ReplyPostResult
            {
                Success = false,
                Error = "Rate limit exceeded. Please wait before posting more replies."
            };
        }

        return platform.ToLower() switch
        {
            "facebook" => await PostFacebookReplyAsync(commentId, replyContent, accessToken, ct),
            "instagram" => await PostInstagramReplyAsync(commentId, replyContent, accessToken, ct),
            "twitter" or "x" => await PostTwitterReplyAsync(commentId, replyContent, accessToken, ct),
            "youtube" => await PostYouTubeReplyAsync(commentId, replyContent, accessToken, ct),
            _ => new ReplyPostResult { Success = false, Error = $"Platform {platform} not supported for replies" }
        };
    }

    /// <summary>
    /// Post reply to Facebook comment
    /// </summary>
    private async Task<ReplyPostResult> PostFacebookReplyAsync(
        string commentId,
        string replyContent,
        string accessToken,
        CancellationToken ct)
    {
        try
        {
            var postData = new Dictionary<string, string>
            {
                ["message"] = replyContent,
                ["access_token"] = accessToken
            };

            var response = await _httpClient.PostAsync(
                $"{FacebookGraphUrl}/{commentId}/comments",
                new FormUrlEncodedContent(postData),
                ct
            );

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<FacebookPostResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Id != null)
            {
                RecordReply("facebook");
                return new ReplyPostResult
                {
                    Success = true,
                    ReplyId = result.Id,
                    PostedAt = DateTime.UtcNow
                };
            }

            return new ReplyPostResult
            {
                Success = false,
                Error = result?.Error?.Message ?? "Unknown error"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post Facebook reply to comment {CommentId}", commentId);
            return new ReplyPostResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Post reply to Instagram comment
    /// </summary>
    private async Task<ReplyPostResult> PostInstagramReplyAsync(
        string commentId,
        string replyContent,
        string accessToken,
        CancellationToken ct)
    {
        try
        {
            var postData = new Dictionary<string, string>
            {
                ["message"] = replyContent,
                ["access_token"] = accessToken
            };

            var response = await _httpClient.PostAsync(
                $"{FacebookGraphUrl}/{commentId}/replies",
                new FormUrlEncodedContent(postData),
                ct
            );

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<FacebookPostResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result?.Id != null)
            {
                RecordReply("instagram");
                return new ReplyPostResult
                {
                    Success = true,
                    ReplyId = result.Id,
                    PostedAt = DateTime.UtcNow
                };
            }

            return new ReplyPostResult
            {
                Success = false,
                Error = result?.Error?.Message ?? "Unknown error"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post Instagram reply to comment {CommentId}", commentId);
            return new ReplyPostResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Post reply to Twitter/X
    /// </summary>
    private async Task<ReplyPostResult> PostTwitterReplyAsync(
        string tweetId,
        string replyContent,
        string bearerToken,
        CancellationToken ct)
    {
        try
        {
            var requestBody = new
            {
                text = replyContent,
                reply = new { in_reply_to_tweet_id = tweetId }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/tweets")
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Add("Authorization", $"Bearer {bearerToken}");

            var response = await _httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<TwitterPostResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                RecordReply("twitter");
                return new ReplyPostResult
                {
                    Success = true,
                    ReplyId = result?.Data?.Id,
                    PostedAt = DateTime.UtcNow
                };
            }

            return new ReplyPostResult
            {
                Success = false,
                Error = $"Twitter API error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post Twitter reply to tweet {TweetId}", tweetId);
            return new ReplyPostResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Post reply to YouTube comment
    /// </summary>
    private async Task<ReplyPostResult> PostYouTubeReplyAsync(
        string commentId,
        string replyContent,
        string accessToken,
        CancellationToken ct)
    {
        try
        {
            var requestBody = new
            {
                snippet = new
                {
                    parentId = commentId,
                    textOriginal = replyContent
                }
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://www.googleapis.com/youtube/v3/comments?part=snippet&access_token={accessToken}"
            )
            {
                Content = JsonContent.Create(requestBody)
            };

            var response = await _httpClient.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<YouTubeCommentResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                RecordReply("youtube");
                return new ReplyPostResult
                {
                    Success = true,
                    ReplyId = result?.Id,
                    PostedAt = DateTime.UtcNow
                };
            }

            return new ReplyPostResult
            {
                Success = false,
                Error = $"YouTube API error: {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post YouTube reply to comment {CommentId}", commentId);
            return new ReplyPostResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RATE LIMITING
    // ═══════════════════════════════════════════════════════════════════════

    private bool CheckRateLimit(string platform, int maxPerHour = 30)
    {
        lock (_rateLimitLock)
        {
            if (!_replyHistory.TryGetValue(platform, out var history))
            {
                history = new Queue<DateTime>();
                _replyHistory[platform] = history;
            }

            // Remove old entries
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            while (history.Count > 0 && history.Peek() < oneHourAgo)
            {
                history.Dequeue();
            }

            return history.Count < maxPerHour;
        }
    }

    private void RecordReply(string platform)
    {
        lock (_rateLimitLock)
        {
            if (!_replyHistory.TryGetValue(platform, out var history))
            {
                history = new Queue<DateTime>();
                _replyHistory[platform] = history;
            }

            history.Enqueue(DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Get remaining replies allowed this hour
    /// </summary>
    public int GetRemainingReplies(string platform, int maxPerHour = 30)
    {
        lock (_rateLimitLock)
        {
            if (!_replyHistory.TryGetValue(platform, out var history))
            {
                return maxPerHour;
            }

            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            var recentCount = history.Count(t => t >= oneHourAgo);
            return Math.Max(0, maxPerHour - recentCount);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTO-REPLY WORKFLOW
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Process and auto-reply to a comment
    /// </summary>
    public async Task<(GeneratedReply Reply, ReplyPostResult? PostResult)> ProcessCommentAutoReplyAsync(
        SocialComment comment,
        ToneConfiguration tone,
        string accessToken,
        AutoReplySettings settings,
        string? postContent = null,
        string? brandName = null,
        CancellationToken ct = default)
    {
        // Skip if auto-reply disabled
        if (!settings.Enabled)
        {
            return (new GeneratedReply { RequiresHumanReview = true, ReviewReason = "Auto-reply disabled" }, null);
        }

        // Skip negative comments if configured
        if (settings.SkipNegative && comment.Sentiment == "negative")
        {
            return (new GeneratedReply { RequiresHumanReview = true, ReviewReason = "Negative comment - skipped" }, null);
        }

        // Generate reply
        var request = new ReplyGenerationRequest
        {
            Comment = comment,
            Tone = tone,
            PostContent = postContent,
            BrandName = brandName,
            Platform = comment.Platform
        };

        var generatedReply = await GenerateReplyAsync(request, ct);

        // Check confidence threshold
        if (generatedReply.Confidence < settings.MinConfidence)
        {
            generatedReply.RequiresHumanReview = true;
            generatedReply.ReviewReason = $"Low confidence: {generatedReply.Confidence:P0}";
            return (generatedReply, null);
        }

        // Check if negative needs review
        if (settings.RequireReviewForNegative && comment.Sentiment == "negative")
        {
            generatedReply.RequiresHumanReview = true;
            generatedReply.ReviewReason = "Negative comment requires review";
            return (generatedReply, null);
        }

        // Check if needs human review
        if (generatedReply.RequiresHumanReview)
        {
            return (generatedReply, null);
        }

        // Apply delay
        if (settings.DelaySeconds > 0)
        {
            await Task.Delay(settings.DelaySeconds * 1000, ct);
        }

        // Post the reply
        var postResult = await PostReplyAsync(
            comment.Platform,
            comment.PlatformCommentId,
            generatedReply.Content,
            accessToken,
            ct
        );

        ReplyPosted?.Invoke(this, (comment, postResult));

        return (generatedReply, postResult);
    }
}

#region API Response Models

internal class FacebookPostResponse
{
    public string? Id { get; set; }
    public FacebookErrorResponse? Error { get; set; }
}

internal class FacebookErrorResponse
{
    public string? Message { get; set; }
    public int Code { get; set; }
}

internal class TwitterPostResponse
{
    public TwitterPostData? Data { get; set; }
}

internal class TwitterPostData
{
    public string? Id { get; set; }
}

internal class YouTubeCommentResponse
{
    public string? Id { get; set; }
}

#endregion
