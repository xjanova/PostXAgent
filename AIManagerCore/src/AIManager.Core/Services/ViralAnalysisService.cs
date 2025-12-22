using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// VIRAL ANALYSIS SERVICE - Predict viral potential and track trending keywords
// ═══════════════════════════════════════════════════════════════════════════════

#region Models

public class ViralPrediction
{
    public double Score { get; set; } // 0-100
    public bool IsLikelyViral { get; set; }
    public Dictionary<string, double> Factors { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public List<string> TrendingKeywordsUsed { get; set; } = new();
    public string? OptimalPostingTime { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

public class TrendingKeywordInfo
{
    public string Keyword { get; set; } = "";
    public string Platform { get; set; } = "";
    public string? Category { get; set; }
    public double TrendScore { get; set; }
    public double Velocity { get; set; }
    public double ViralScore { get; set; }
    public double EngagementRate { get; set; }
    public List<string> RelatedKeywords { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

public class ContentAnalysisResult
{
    public double EmotionalScore { get; set; } // 0-100
    public List<string> EmotionalTriggers { get; set; } = new();
    public double ReadabilityScore { get; set; }
    public double UniquenesScore { get; set; }
    public List<string> Keywords { get; set; } = new();
    public List<string> Hashtags { get; set; } = new();
    public string? ContentType { get; set; }
    public string? Tone { get; set; }
}

public class OptimalPostingTime
{
    public string DayOfWeek { get; set; } = "";
    public int Hour { get; set; }
    public double EngagementMultiplier { get; set; }
    public string Reason { get; set; } = "";
}

public class KeywordAnalysisResult
{
    public string Keyword { get; set; } = "";
    public double ViralScore { get; set; }
    public double Velocity { get; set; }
    public int Mentions { get; set; }
    public int Engagement { get; set; }
    public List<string> Platforms { get; set; } = new();
}

public class EmotionalAnalysisResult
{
    public double Score { get; set; }
    public List<string> Triggers { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

#endregion

public class ViralAnalysisService
{
    private readonly ILogger<ViralAnalysisService> _logger;
    private readonly ContentGeneratorService _contentGenerator;

    // In-memory cache for trending keywords
    private readonly ConcurrentDictionary<string, List<TrendingKeywordInfo>> _trendingCache = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastTrendUpdate = new();

    // Platform-specific optimal posting times (Thailand timezone)
    private static readonly Dictionary<string, List<OptimalPostingTime>> _optimalTimes = new()
    {
        ["facebook"] = new()
        {
            new() { DayOfWeek = "Monday", Hour = 12, EngagementMultiplier = 1.2, Reason = "พักกลางวัน" },
            new() { DayOfWeek = "Monday", Hour = 19, EngagementMultiplier = 1.4, Reason = "หลังเลิกงาน" },
            new() { DayOfWeek = "Tuesday", Hour = 12, EngagementMultiplier = 1.2, Reason = "พักกลางวัน" },
            new() { DayOfWeek = "Wednesday", Hour = 12, EngagementMultiplier = 1.3, Reason = "กลางสัปดาห์" },
            new() { DayOfWeek = "Thursday", Hour = 20, EngagementMultiplier = 1.3, Reason = "ใกล้สุดสัปดาห์" },
            new() { DayOfWeek = "Friday", Hour = 18, EngagementMultiplier = 1.5, Reason = "ศุกร์เย็น" },
            new() { DayOfWeek = "Saturday", Hour = 11, EngagementMultiplier = 1.4, Reason = "สุดสัปดาห์เช้า" },
            new() { DayOfWeek = "Sunday", Hour = 20, EngagementMultiplier = 1.3, Reason = "อาทิตย์เย็น" }
        },
        ["instagram"] = new()
        {
            new() { DayOfWeek = "Monday", Hour = 11, EngagementMultiplier = 1.2, Reason = "เช้าสาย" },
            new() { DayOfWeek = "Tuesday", Hour = 19, EngagementMultiplier = 1.3, Reason = "หลังเลิกงาน" },
            new() { DayOfWeek = "Wednesday", Hour = 17, EngagementMultiplier = 1.4, Reason = "กลางสัปดาห์" },
            new() { DayOfWeek = "Thursday", Hour = 19, EngagementMultiplier = 1.3, Reason = "เย็น" },
            new() { DayOfWeek = "Friday", Hour = 12, EngagementMultiplier = 1.2, Reason = "พักเที่ยง" },
            new() { DayOfWeek = "Saturday", Hour = 10, EngagementMultiplier = 1.5, Reason = "เสาร์เช้า" },
            new() { DayOfWeek = "Sunday", Hour = 11, EngagementMultiplier = 1.4, Reason = "อาทิตย์สาย" }
        },
        ["tiktok"] = new()
        {
            new() { DayOfWeek = "Monday", Hour = 18, EngagementMultiplier = 1.3, Reason = "หลังเรียน/งาน" },
            new() { DayOfWeek = "Tuesday", Hour = 21, EngagementMultiplier = 1.4, Reason = "ก่อนนอน" },
            new() { DayOfWeek = "Wednesday", Hour = 19, EngagementMultiplier = 1.3, Reason = "เย็น" },
            new() { DayOfWeek = "Thursday", Hour = 21, EngagementMultiplier = 1.3, Reason = "ก่อนนอน" },
            new() { DayOfWeek = "Friday", Hour = 22, EngagementMultiplier = 1.5, Reason = "ศุกร์คืน" },
            new() { DayOfWeek = "Saturday", Hour = 14, EngagementMultiplier = 1.4, Reason = "บ่าย" },
            new() { DayOfWeek = "Sunday", Hour = 15, EngagementMultiplier = 1.3, Reason = "บ่าย" }
        },
        ["twitter"] = new()
        {
            new() { DayOfWeek = "Monday", Hour = 8, EngagementMultiplier = 1.2, Reason = "เช้า" },
            new() { DayOfWeek = "Tuesday", Hour = 12, EngagementMultiplier = 1.3, Reason = "เที่ยง" },
            new() { DayOfWeek = "Wednesday", Hour = 12, EngagementMultiplier = 1.3, Reason = "เที่ยง" },
            new() { DayOfWeek = "Thursday", Hour = 17, EngagementMultiplier = 1.2, Reason = "เย็น" },
            new() { DayOfWeek = "Friday", Hour = 12, EngagementMultiplier = 1.4, Reason = "เที่ยงศุกร์" }
        },
        ["line"] = new()
        {
            new() { DayOfWeek = "Monday", Hour = 20, EngagementMultiplier = 1.3, Reason = "หลังเลิกงาน" },
            new() { DayOfWeek = "Wednesday", Hour = 12, EngagementMultiplier = 1.2, Reason = "พักเที่ยง" },
            new() { DayOfWeek = "Friday", Hour = 18, EngagementMultiplier = 1.4, Reason = "ศุกร์เย็น" },
            new() { DayOfWeek = "Saturday", Hour = 10, EngagementMultiplier = 1.3, Reason = "เสาร์เช้า" }
        }
    };

    // Emotional trigger words (Thai and English)
    private static readonly Dictionary<string, double> _emotionalTriggers = new()
    {
        // High impact
        ["ฟรี"] = 1.0, ["free"] = 1.0,
        ["ด่วน"] = 0.9, ["urgent"] = 0.9,
        ["จำกัด"] = 0.85, ["limited"] = 0.85,
        ["พิเศษ"] = 0.8, ["special"] = 0.8,
        ["ลดราคา"] = 0.9, ["sale"] = 0.9,
        ["โปรโมชั่น"] = 0.85, ["promotion"] = 0.85,
        ["ใหม่"] = 0.7, ["new"] = 0.7,
        ["เปิดตัว"] = 0.75, ["launch"] = 0.75,
        ["แจก"] = 0.95, ["giveaway"] = 0.95,

        // Emotional
        ["น่ารัก"] = 0.6, ["cute"] = 0.6,
        ["สุดยอด"] = 0.65, ["amazing"] = 0.65,
        ["เจ๋ง"] = 0.6, ["awesome"] = 0.6,
        ["ตกใจ"] = 0.7, ["shocking"] = 0.7,
        ["ลับ"] = 0.75, ["secret"] = 0.75,
        ["เคล็ดลับ"] = 0.7, ["tips"] = 0.6,
        ["วิธี"] = 0.5, ["how to"] = 0.5,

        // Engagement
        ["คิดยังไง"] = 0.6, ["what do you think"] = 0.6,
        ["ใครเห็นด้วย"] = 0.65, ["agree"] = 0.5,
        ["tag"] = 0.55, ["แท็ก"] = 0.55,
        ["share"] = 0.5, ["แชร์"] = 0.5
    };

    public ViralAnalysisService(
        ILogger<ViralAnalysisService> logger,
        ContentGeneratorService contentGenerator)
    {
        _logger = logger;
        _contentGenerator = contentGenerator;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIRAL PREDICTION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Predict viral potential of content before posting
    /// </summary>
    public async Task<ViralPrediction> PredictViralPotentialAsync(
        string content,
        string platform,
        List<string>? hashtags = null,
        DateTime? scheduledTime = null,
        CancellationToken ct = default)
    {
        var factors = new Dictionary<string, double>();
        var recommendations = new List<string>();
        var trendingUsed = new List<string>();

        // 1. Analyze content for emotional triggers (0-25 points)
        var emotionalScore = AnalyzeEmotionalTriggers(content);
        factors["emotional_impact"] = emotionalScore;

        // 2. Check trending keywords (0-25 points)
        var trendingKeywords = await GetTrendingKeywordsAsync(platform, ct: ct);
        var trendingScore = CalculateTrendingScore(content, hashtags, trendingKeywords, out var usedKeywords);
        factors["trending_alignment"] = trendingScore;
        trendingUsed.AddRange(usedKeywords);

        // 3. Analyze posting time (0-20 points)
        var timeScore = CalculateTimingScore(platform, scheduledTime ?? DateTime.Now);
        factors["timing"] = timeScore;

        // 4. Content format and length (0-15 points)
        var formatScore = AnalyzeContentFormat(content, platform);
        factors["format_optimization"] = formatScore;

        // 5. Hashtag optimization (0-15 points)
        var hashtagScore = AnalyzeHashtags(hashtags, platform, trendingKeywords);
        factors["hashtag_strategy"] = hashtagScore;

        // Calculate total score
        var totalScore = factors.Values.Sum();
        var isLikelyViral = totalScore >= 70;

        // Generate recommendations
        recommendations.AddRange(GenerateRecommendations(factors, platform, trendingKeywords));

        // Get optimal posting time
        var optimalTime = GetOptimalPostingTime(platform, scheduledTime ?? DateTime.Now);

        return new ViralPrediction
        {
            Score = totalScore,
            IsLikelyViral = isLikelyViral,
            Factors = factors,
            Recommendations = recommendations,
            TrendingKeywordsUsed = trendingUsed,
            OptimalPostingTime = optimalTime != null
                ? $"{optimalTime.DayOfWeek} {optimalTime.Hour}:00 ({optimalTime.Reason})"
                : null
        };
    }

    /// <summary>
    /// Analyze emotional triggers in content
    /// </summary>
    private double AnalyzeEmotionalTriggers(string content)
    {
        var lowerContent = content.ToLower();
        var totalTriggerScore = 0.0;
        var triggersFound = 0;

        foreach (var (trigger, weight) in _emotionalTriggers)
        {
            if (lowerContent.Contains(trigger.ToLower()))
            {
                totalTriggerScore += weight;
                triggersFound++;
            }
        }

        // Normalize to 0-25 scale
        var normalizedScore = triggersFound > 0
            ? (totalTriggerScore / triggersFound) * 25
            : 5; // Minimum score

        return Math.Min(25, normalizedScore);
    }

    /// <summary>
    /// Calculate trending keyword alignment score
    /// </summary>
    private double CalculateTrendingScore(
        string content,
        List<string>? hashtags,
        List<TrendingKeywordInfo> trendingKeywords,
        out List<string> usedKeywords)
    {
        usedKeywords = new List<string>();
        if (!trendingKeywords.Any()) return 5; // Minimum score

        var lowerContent = content.ToLower();
        var allHashtags = hashtags?.Select(h => h.ToLower().TrimStart('#')).ToList() ?? new();

        var totalScore = 0.0;

        foreach (var keyword in trendingKeywords)
        {
            if (lowerContent.Contains(keyword.Keyword.ToLower()) ||
                allHashtags.Contains(keyword.Keyword.ToLower()))
            {
                usedKeywords.Add(keyword.Keyword);
                totalScore += keyword.ViralScore / 100 * 10; // Weight by viral score
            }
        }

        return Math.Min(25, Math.Max(5, totalScore));
    }

    /// <summary>
    /// Calculate timing score based on optimal posting times
    /// </summary>
    private double CalculateTimingScore(string platform, DateTime postTime)
    {
        if (!_optimalTimes.TryGetValue(platform.ToLower(), out var optimalTimes))
        {
            return 10; // Default score
        }

        var dayOfWeek = postTime.DayOfWeek.ToString();
        var hour = postTime.Hour;

        // Find matching optimal time
        var match = optimalTimes.FirstOrDefault(t =>
            t.DayOfWeek.Equals(dayOfWeek, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(t.Hour - hour) <= 1);

        if (match != null)
        {
            return 20 * match.EngagementMultiplier;
        }

        // Check if within 2 hours of optimal
        var closeMatch = optimalTimes.FirstOrDefault(t =>
            t.DayOfWeek.Equals(dayOfWeek, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs(t.Hour - hour) <= 2);

        if (closeMatch != null)
        {
            return 15 * closeMatch.EngagementMultiplier;
        }

        return 8; // Base score for non-optimal times
    }

    /// <summary>
    /// Analyze content format
    /// </summary>
    private double AnalyzeContentFormat(string content, string platform)
    {
        var score = 10.0; // Base score

        // Length optimization
        var length = content.Length;
        var optimalLength = platform.ToLower() switch
        {
            "twitter" => 200,
            "instagram" => 300,
            "facebook" => 400,
            "tiktok" => 150,
            "line" => 250,
            _ => 300
        };

        // Score based on optimal length (within 50% range)
        var lengthRatio = (double)length / optimalLength;
        if (lengthRatio >= 0.5 && lengthRatio <= 1.5)
        {
            score += 3;
        }
        else if (lengthRatio < 0.3 || lengthRatio > 2)
        {
            score -= 3;
        }

        // Emoji usage
        var emojiCount = CountEmojis(content);
        if (emojiCount >= 1 && emojiCount <= 5)
        {
            score += 2;
        }

        return Math.Max(5, Math.Min(15, score));
    }

    /// <summary>
    /// Analyze hashtag strategy
    /// </summary>
    private double AnalyzeHashtags(
        List<string>? hashtags,
        string platform,
        List<TrendingKeywordInfo> trendingKeywords)
    {
        if (hashtags == null || !hashtags.Any()) return 3;

        var score = 5.0;
        var hashtagCount = hashtags.Count;

        // Optimal hashtag count by platform
        var optimalCount = platform.ToLower() switch
        {
            "instagram" => 8,
            "twitter" => 3,
            "facebook" => 3,
            "tiktok" => 5,
            "linkedin" => 3,
            _ => 3
        };

        // Score based on count
        if (hashtagCount >= optimalCount - 2 && hashtagCount <= optimalCount + 3)
        {
            score += 3;
        }

        // Bonus for trending hashtags
        var trendingUsed = hashtags.Count(h =>
            trendingKeywords.Any(k =>
                k.Keyword.Equals(h.TrimStart('#'), StringComparison.OrdinalIgnoreCase)));

        score += trendingUsed * 2;

        return Math.Min(15, score);
    }

    /// <summary>
    /// Generate recommendations to improve viral potential
    /// </summary>
    private List<string> GenerateRecommendations(
        Dictionary<string, double> factors,
        string platform,
        List<TrendingKeywordInfo> trendingKeywords)
    {
        var recommendations = new List<string>();

        // Emotional impact recommendations
        if (factors.GetValueOrDefault("emotional_impact", 0) < 15)
        {
            recommendations.Add("ลองเพิ่มคำที่กระตุ้นอารมณ์ เช่น 'ด่วน', 'พิเศษ', 'จำกัด'");
        }

        // Trending alignment recommendations
        if (factors.GetValueOrDefault("trending_alignment", 0) < 15 && trendingKeywords.Any())
        {
            var topTrending = trendingKeywords.Take(3).Select(k => k.Keyword);
            recommendations.Add($"ลองใช้คีย์เวิร์ดที่กำลังเทรนด์: {string.Join(", ", topTrending)}");
        }

        // Timing recommendations
        if (factors.GetValueOrDefault("timing", 0) < 15)
        {
            var optimalTime = GetOptimalPostingTime(platform, DateTime.Now);
            if (optimalTime != null)
            {
                recommendations.Add($"เวลาโพสต์ที่ดีที่สุด: {optimalTime.DayOfWeek} {optimalTime.Hour}:00 ({optimalTime.Reason})");
            }
        }

        // Format recommendations
        if (factors.GetValueOrDefault("format_optimization", 0) < 10)
        {
            recommendations.Add("ปรับความยาวเนื้อหาให้เหมาะกับ platform");
            recommendations.Add("ลองเพิ่ม emoji 2-3 ตัวเพื่อความน่าสนใจ");
        }

        // Hashtag recommendations
        if (factors.GetValueOrDefault("hashtag_strategy", 0) < 8)
        {
            var hashtagCount = platform.ToLower() switch
            {
                "instagram" => "5-10",
                "tiktok" => "3-5",
                _ => "2-4"
            };
            recommendations.Add($"เพิ่ม hashtags ที่เกี่ยวข้อง {hashtagCount} อัน");
        }

        return recommendations;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TRENDING KEYWORDS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get trending keywords for platform (with caching)
    /// </summary>
    public async Task<List<TrendingKeywordInfo>> GetTrendingKeywordsAsync(
        string platform,
        string region = "TH",
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        var cacheKey = $"{platform}_{region}";

        // Check cache
        if (!forceRefresh &&
            _trendingCache.TryGetValue(cacheKey, out var cached) &&
            _lastTrendUpdate.TryGetValue(cacheKey, out var lastUpdate) &&
            DateTime.UtcNow - lastUpdate < TimeSpan.FromHours(1))
        {
            return cached;
        }

        // For now, return sample trending keywords
        // In production, this would fetch from platform APIs or a trending service
        var trending = GetSampleTrendingKeywords(platform);

        _trendingCache[cacheKey] = trending;
        _lastTrendUpdate[cacheKey] = DateTime.UtcNow;

        return trending;
    }

    /// <summary>
    /// Get sample trending keywords (placeholder for actual API integration)
    /// </summary>
    private List<TrendingKeywordInfo> GetSampleTrendingKeywords(string platform)
    {
        // These would come from actual platform APIs in production
        var baseKeywords = new List<TrendingKeywordInfo>
        {
            new() { Keyword = "โปรโมชั่น", TrendScore = 85, Velocity = 5.2, ViralScore = 80, Category = "marketing" },
            new() { Keyword = "ลดราคา", TrendScore = 82, Velocity = 4.8, ViralScore = 78, Category = "marketing" },
            new() { Keyword = "ของขวัญ", TrendScore = 75, Velocity = 3.5, ViralScore = 72, Category = "lifestyle" },
            new() { Keyword = "รีวิว", TrendScore = 70, Velocity = 2.8, ViralScore = 68, Category = "product" },
            new() { Keyword = "เทคนิค", TrendScore = 65, Velocity = 2.2, ViralScore = 62, Category = "education" },
            new() { Keyword = "ใหม่2024", TrendScore = 60, Velocity = 1.8, ViralScore = 58, Category = "trend" },
            new() { Keyword = "DIY", TrendScore = 55, Velocity = 1.5, ViralScore = 52, Category = "lifestyle" },
            new() { Keyword = "workfromhome", TrendScore = 50, Velocity = 1.2, ViralScore = 48, Category = "work" },
        };

        // Platform-specific keywords
        var platformSpecific = platform.ToLower() switch
        {
            "tiktok" => new List<TrendingKeywordInfo>
            {
                new() { Keyword = "fyp", TrendScore = 95, Velocity = 10, ViralScore = 90, Category = "platform" },
                new() { Keyword = "viral", TrendScore = 90, Velocity = 8, ViralScore = 88, Category = "platform" },
                new() { Keyword = "trending", TrendScore = 85, Velocity = 7, ViralScore = 82, Category = "platform" },
            },
            "instagram" => new List<TrendingKeywordInfo>
            {
                new() { Keyword = "instagood", TrendScore = 85, Velocity = 6, ViralScore = 78, Category = "platform" },
                new() { Keyword = "photooftheday", TrendScore = 80, Velocity = 5, ViralScore = 75, Category = "platform" },
            },
            "twitter" => new List<TrendingKeywordInfo>
            {
                new() { Keyword = "breaking", TrendScore = 90, Velocity = 15, ViralScore = 85, Category = "news" },
                new() { Keyword = "trend", TrendScore = 85, Velocity = 10, ViralScore = 80, Category = "platform" },
            },
            _ => new List<TrendingKeywordInfo>()
        };

        return platformSpecific.Concat(baseKeywords)
            .Select(k => { k.Platform = platform; k.LastUpdated = DateTime.UtcNow; return k; })
            .OrderByDescending(k => k.ViralScore)
            .ToList();
    }

    /// <summary>
    /// Update keyword trending data from external source
    /// </summary>
    public async Task UpdateTrendingKeywordsAsync(
        string platform,
        List<TrendingKeywordInfo> keywords,
        CancellationToken ct = default)
    {
        var cacheKey = $"{platform}_TH";
        _trendingCache[cacheKey] = keywords;
        _lastTrendUpdate[cacheKey] = DateTime.UtcNow;

        _logger.LogInformation("Updated {Count} trending keywords for {Platform}",
            keywords.Count, platform);
    }

    /// <summary>
    /// Get trending keywords with limit
    /// </summary>
    public async Task<List<TrendingKeywordInfo>> GetTrendingKeywordsAsync(
        string? platform,
        int limit,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(platform))
        {
            // Get from all platforms
            var allKeywords = new List<TrendingKeywordInfo>();
            foreach (var p in new[] { "facebook", "instagram", "twitter", "tiktok" })
            {
                allKeywords.AddRange(await GetTrendingKeywordsAsync(p, ct: ct));
            }
            return allKeywords.OrderByDescending(k => k.ViralScore).Take(limit).ToList();
        }

        var keywords = await GetTrendingKeywordsAsync(platform, ct: ct);
        return keywords.Take(limit).ToList();
    }

    /// <summary>
    /// Track a new keyword for trending analysis
    /// </summary>
    public async Task TrackKeywordAsync(string keyword, List<string>? platforms = null)
    {
        platforms ??= new List<string> { "facebook", "instagram", "twitter" };

        foreach (var platform in platforms)
        {
            var cacheKey = $"{platform}_TH";
            if (_trendingCache.TryGetValue(cacheKey, out var keywords))
            {
                var existing = keywords.FirstOrDefault(k =>
                    k.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    keywords.Add(new TrendingKeywordInfo
                    {
                        Keyword = keyword,
                        Platform = platform,
                        TrendScore = 0,
                        Velocity = 0,
                        ViralScore = 0,
                        LastUpdated = DateTime.UtcNow
                    });
                }
            }
            else
            {
                _trendingCache[cacheKey] = new List<TrendingKeywordInfo>
                {
                    new()
                    {
                        Keyword = keyword,
                        Platform = platform,
                        TrendScore = 0,
                        Velocity = 0,
                        ViralScore = 0,
                        LastUpdated = DateTime.UtcNow
                    }
                };
            }
        }

        _logger.LogInformation("Now tracking keyword: {Keyword} on platforms: {Platforms}",
            keyword, string.Join(", ", platforms));
    }

    /// <summary>
    /// Analyze a specific keyword
    /// </summary>
    public async Task<KeywordAnalysisResult> AnalyzeKeywordAsync(
        string keyword,
        CancellationToken ct = default)
    {
        // Check all platforms for this keyword
        var allMentions = new List<TrendingKeywordInfo>();
        foreach (var platform in new[] { "facebook", "instagram", "twitter", "tiktok" })
        {
            var keywords = await GetTrendingKeywordsAsync(platform, ct: ct);
            var found = keywords.FirstOrDefault(k =>
                k.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                allMentions.Add(found);
            }
        }

        if (allMentions.Any())
        {
            return new KeywordAnalysisResult
            {
                Keyword = keyword,
                ViralScore = allMentions.Average(k => k.ViralScore),
                Velocity = allMentions.Average(k => k.Velocity),
                Mentions = (int)allMentions.Sum(k => k.TrendScore),
                Engagement = (int)(allMentions.Sum(k => k.EngagementRate) * 1000),
                Platforms = allMentions.Select(k => k.Platform).Distinct().ToList()
            };
        }

        // New keyword - start tracking
        await TrackKeywordAsync(keyword);

        return new KeywordAnalysisResult
        {
            Keyword = keyword,
            ViralScore = 0,
            Velocity = 0,
            Mentions = 0,
            Engagement = 0,
            Platforms = new List<string> { "facebook", "instagram", "twitter" }
        };
    }

    /// <summary>
    /// Get optimal posting times for a platform
    /// </summary>
    public List<OptimalPostingTime> GetOptimalPostingTimesAsync(string platform)
    {
        if (_optimalTimes.TryGetValue(platform.ToLower(), out var times))
        {
            return times;
        }
        return _optimalTimes["facebook"]; // Default to Facebook times
    }

    /// <summary>
    /// Analyze emotional triggers in content (public method)
    /// </summary>
    public async Task<EmotionalAnalysisResult> AnalyzeEmotionalTriggersAsync(
        string content,
        CancellationToken ct = default)
    {
        var triggersFound = new List<string>();
        var lowerContent = content.ToLower();
        var totalScore = 0.0;

        foreach (var (trigger, weight) in _emotionalTriggers)
        {
            if (lowerContent.Contains(trigger.ToLower()))
            {
                triggersFound.Add(trigger);
                totalScore += weight;
            }
        }

        // Normalize score to 0-100
        var normalizedScore = triggersFound.Any()
            ? Math.Min(100, (totalScore / triggersFound.Count) * 100)
            : 20; // Base score

        return new EmotionalAnalysisResult
        {
            Score = normalizedScore,
            Triggers = triggersFound,
            Recommendations = GenerateEmotionalRecommendations(triggersFound, normalizedScore)
        };
    }

    private List<string> GenerateEmotionalRecommendations(List<string> foundTriggers, double score)
    {
        var recommendations = new List<string>();

        if (score < 40)
        {
            recommendations.Add("เพิ่มคำที่กระตุ้นอารมณ์ เช่น 'ด่วน', 'พิเศษ', 'แจก'");
        }

        if (!foundTriggers.Any(t => t.Contains("ฟรี") || t.Contains("free")))
        {
            recommendations.Add("ลองใช้คำว่า 'ฟรี' หรือ 'แจก' เพื่อดึงดูดความสนใจ");
        }

        if (!foundTriggers.Any(t => t.Contains("ด่วน") || t.Contains("จำกัด")))
        {
            recommendations.Add("เพิ่มความเร่งด่วน เช่น 'ด่วน', 'จำกัดเวลา'");
        }

        return recommendations;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CONTENT ANALYSIS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Analyze content using AI for viral optimization
    /// </summary>
    public async Task<ContentAnalysisResult> AnalyzeContentAsync(
        string content,
        string platform,
        CancellationToken ct = default)
    {
        try
        {
            var prompt = $@"วิเคราะห์เนื้อหานี้สำหรับ {platform} และตอบเป็น JSON:

เนื้อหา: ""{content}""

วิเคราะห์:
1. emotional_score: คะแนนการกระตุ้นอารมณ์ (0-100)
2. emotional_triggers: คำที่กระตุ้นอารมณ์ในเนื้อหา
3. readability_score: ความง่ายในการอ่าน (0-100)
4. uniqueness_score: ความแปลกใหม่ (0-100)
5. keywords: คำสำคัญ
6. hashtags: hashtag ที่แนะนำ
7. content_type: ประเภทเนื้อหา (promotional, educational, entertainment, engagement)
8. tone: โทนเนื้อหา (casual, professional, humorous, urgent)

ตอบเฉพาะ JSON:";

            var result = await _contentGenerator.GenerateAsync(prompt, null, platform, "th", ct);

            if (result?.Text != null)
            {
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                    result.Text,
                    @"\{[\s\S]*\}",
                    System.Text.RegularExpressions.RegexOptions.Multiline
                );

                if (jsonMatch.Success)
                {
                    var analysis = JsonSerializer.Deserialize<ContentAnalysisJson>(
                        jsonMatch.Value,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (analysis != null)
                    {
                        return new ContentAnalysisResult
                        {
                            EmotionalScore = analysis.EmotionalScore ?? 50,
                            EmotionalTriggers = analysis.EmotionalTriggers ?? new(),
                            ReadabilityScore = analysis.ReadabilityScore ?? 70,
                            UniquenesScore = analysis.UniquenessScore ?? 50,
                            Keywords = analysis.Keywords ?? new(),
                            Hashtags = analysis.Hashtags ?? new(),
                            ContentType = analysis.ContentType,
                            Tone = analysis.Tone
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI content analysis failed, using basic analysis");
        }

        // Fallback to basic analysis
        return new ContentAnalysisResult
        {
            EmotionalScore = AnalyzeEmotionalTriggers(content) * 4, // Scale to 100
            ReadabilityScore = 70,
            UniquenesScore = 50,
            Keywords = ExtractKeywords(content),
            Hashtags = ExtractHashtags(content)
        };
    }

    /// <summary>
    /// Generate optimized content with viral potential
    /// </summary>
    public async Task<(string Content, ViralPrediction Prediction)> GenerateViralContentAsync(
        string topic,
        string platform,
        string? brandContext = null,
        CancellationToken ct = default)
    {
        var trending = await GetTrendingKeywordsAsync(platform, ct: ct);
        var topKeywords = trending.Take(5).Select(k => k.Keyword).ToList();

        var prompt = $@"สร้างเนื้อหาสำหรับ {platform} ที่มีโอกาสเป็น viral สูง

หัวข้อ: {topic}
{(brandContext != null ? $"ข้อมูลแบรนด์: {brandContext}" : "")}

คีย์เวิร์ดที่กำลังเทรนด์: {string.Join(", ", topKeywords)}

สร้างเนื้อหา:
1. น่าสนใจและดึงดูดความสนใจ
2. มีการกระตุ้นอารมณ์
3. ใช้คีย์เวิร์ดที่กำลังเทรนด์อย่างเป็นธรรมชาติ
4. เหมาะกับ {platform}
5. รวม hashtags ที่เหมาะสม

เนื้อหา:";

        var result = await _contentGenerator.GenerateAsync(prompt, null, platform, "th", ct);
        var content = result?.Text ?? topic;

        // Get hashtags from content
        var hashtags = ExtractHashtags(content);

        // Predict viral potential
        var prediction = await PredictViralPotentialAsync(content, platform, hashtags, null, ct);

        return (content, prediction);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════

    private OptimalPostingTime? GetOptimalPostingTime(string platform, DateTime currentTime)
    {
        if (!_optimalTimes.TryGetValue(platform.ToLower(), out var times))
            return null;

        // Find next optimal time
        var currentDayOfWeek = currentTime.DayOfWeek.ToString();
        var currentHour = currentTime.Hour;

        // First check today
        var todayOptimal = times
            .Where(t => t.DayOfWeek.Equals(currentDayOfWeek, StringComparison.OrdinalIgnoreCase) &&
                       t.Hour > currentHour)
            .OrderBy(t => t.Hour)
            .FirstOrDefault();

        if (todayOptimal != null)
            return todayOptimal;

        // Check upcoming days
        for (int i = 1; i <= 7; i++)
        {
            var nextDay = currentTime.AddDays(i).DayOfWeek.ToString();
            var dayOptimal = times
                .Where(t => t.DayOfWeek.Equals(nextDay, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Hour)
                .FirstOrDefault();

            if (dayOptimal != null)
                return dayOptimal;
        }

        return times.OrderByDescending(t => t.EngagementMultiplier).FirstOrDefault();
    }

    private int CountEmojis(string text)
    {
        var emojiPattern = new System.Text.RegularExpressions.Regex(@"[\p{So}\p{Cs}]");
        return emojiPattern.Matches(text).Count;
    }

    private List<string> ExtractKeywords(string content)
    {
        // Simple keyword extraction
        var words = content.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !w.StartsWith("#") && !w.StartsWith("http"))
            .Take(10)
            .ToList();

        return words;
    }

    private List<string> ExtractHashtags(string content)
    {
        var pattern = new System.Text.RegularExpressions.Regex(@"#\w+");
        return pattern.Matches(content)
            .Select(m => m.Value)
            .Distinct()
            .ToList();
    }
}

internal class ContentAnalysisJson
{
    public double? EmotionalScore { get; set; }
    public List<string>? EmotionalTriggers { get; set; }
    public double? ReadabilityScore { get; set; }
    public double? UniquenessScore { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? Hashtags { get; set; }
    public string? ContentType { get; set; }
    public string? Tone { get; set; }
}
