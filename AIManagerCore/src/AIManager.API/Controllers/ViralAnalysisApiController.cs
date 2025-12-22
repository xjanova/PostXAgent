using Microsoft.AspNetCore.Mvc;
using AIManager.Core.Services;
using AIManager.Core.Models;

namespace AIManager.API.Controllers;

/// <summary>
/// Controller for viral analysis and trending keyword operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ViralAnalysisApiController : ControllerBase
{
    private readonly ViralAnalysisService _viralService;
    private readonly ILogger<ViralAnalysisApiController> _logger;

    public ViralAnalysisApiController(
        ViralAnalysisService viralService,
        ILogger<ViralAnalysisApiController> logger)
    {
        _viralService = viralService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze viral potential of content before posting
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<TaskResult>> AnalyzeContent([FromBody] AnalyzeViralRequest request)
    {
        try
        {
            _logger.LogInformation("Analyzing viral potential for {Platform}", request.Platform);

            var result = await _viralService.PredictViralPotentialAsync(
                request.Content,
                request.Platform.ToString().ToLower(),
                request.Hashtags ?? new List<string>());

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    ViralScore = result.Score,
                    ViralFactors = result.Factors.ToDictionary(
                        f => f.Key,
                        f => (object)f.Value)
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing viral potential");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get trending keywords
    /// </summary>
    [HttpGet("keywords/trending")]
    public async Task<ActionResult<TaskResult>> GetTrendingKeywords(
        [FromQuery] string? platform = null,
        [FromQuery] int limit = 20)
    {
        try
        {
            var keywords = await _viralService.GetTrendingKeywordsAsync(platform, limit);

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    Keywords = keywords.Select(k => (object)new KeywordData
                    {
                        Keyword = k.Keyword,
                        ViralScore = k.ViralScore,
                        Velocity = k.Velocity,
                        TotalMentions = (int)k.TrendScore,
                        TotalEngagement = (int)(k.EngagementRate * 1000),
                        Platforms = new List<string> { k.Platform }
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting trending keywords");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Track a new keyword for viral analysis
    /// </summary>
    [HttpPost("keywords/track")]
    public async Task<ActionResult<TaskResult>> TrackKeyword([FromBody] TrackKeywordRequest request)
    {
        try
        {
            _logger.LogInformation("Tracking keyword: {Keyword}", request.Keyword);

            await _viralService.TrackKeywordAsync(
                request.Keyword,
                request.Platforms ?? new List<string> { "facebook", "instagram", "twitter" });

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData { Message = $"Now tracking keyword: {request.Keyword}" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error tracking keyword");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Update metrics for tracked keywords
    /// </summary>
    [HttpPost("keywords/update")]
    public async Task<ActionResult<TaskResult>> UpdateKeywordMetrics([FromBody] UpdateKeywordsRequest request)
    {
        try
        {
            var results = new List<KeywordUpdateResult>();

            foreach (var keyword in request.Keywords ?? new List<string>())
            {
                try
                {
                    var metrics = await _viralService.AnalyzeKeywordAsync(keyword);
                    results.Add(new KeywordUpdateResult
                    {
                        Keyword = keyword,
                        Success = true,
                        ViralScore = metrics.ViralScore,
                        Velocity = metrics.Velocity,
                        Mentions = metrics.Mentions,
                        Engagement = metrics.Engagement
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new KeywordUpdateResult
                    {
                        Keyword = keyword,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    KeywordUpdates = results.Select(r => (object)r).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating keyword metrics");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Get optimal posting times based on platform
    /// </summary>
    [HttpGet("optimal-times")]
    public ActionResult<TaskResult> GetOptimalPostingTimes([FromQuery] string platform)
    {
        try
        {
            var times = _viralService.GetOptimalPostingTimesAsync(platform);

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    OptimalTimes = times
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting optimal posting times");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Analyze emotional triggers in content
    /// </summary>
    [HttpPost("analyze-emotions")]
    public async Task<ActionResult<TaskResult>> AnalyzeEmotions([FromBody] AnalyzeEmotionsRequest request)
    {
        try
        {
            var emotions = await _viralService.AnalyzeEmotionalTriggersAsync(request.Content);

            return Ok(new TaskResult
            {
                Success = true,
                Data = new ResultData
                {
                    EmotionalScore = emotions.Score,
                    EmotionalTriggers = emotions.Triggers,
                    Recommendations = emotions.Recommendations
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing emotions");
            return Ok(new TaskResult
            {
                Success = false,
                Error = ex.Message
            });
        }
    }
}

// Request/Response Models

public record AnalyzeViralRequest
{
    public SocialPlatform Platform { get; init; }
    public string Content { get; init; } = "";
    public List<string>? Hashtags { get; init; }
    public string? MediaType { get; init; }
}

public record TrackKeywordRequest
{
    public string Keyword { get; init; } = "";
    public List<string>? Platforms { get; init; }
    public string? Category { get; init; }
}

public record UpdateKeywordsRequest
{
    public List<string>? Keywords { get; init; }
}

public record AnalyzeEmotionsRequest
{
    public string Content { get; init; } = "";
}

public record KeywordData
{
    public string Keyword { get; init; } = "";
    public double ViralScore { get; init; }
    public double Velocity { get; init; }
    public int TotalMentions { get; init; }
    public int TotalEngagement { get; init; }
    public List<string> Platforms { get; init; } = new();
}

public record KeywordUpdateResult
{
    public string Keyword { get; init; } = "";
    public bool Success { get; init; }
    public double ViralScore { get; init; }
    public double Velocity { get; init; }
    public int Mentions { get; init; }
    public int Engagement { get; init; }
    public string? Error { get; init; }
}
