<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;

class TrendingKeyword extends Model
{
    use HasFactory;

    protected $fillable = [
        'platform',
        'keyword',
        'category',
        'region',
        'trend_score',
        'velocity',
        'mention_count',
        'post_count',
        'avg_likes',
        'avg_comments',
        'avg_shares',
        'engagement_rate',
        'viral_score',
        'first_seen_at',
        'peak_at',
        'last_updated_at',
        'related_keywords',
        'top_posts',
        'hourly_trend',
        'metadata',
    ];

    protected $casts = [
        'trend_score' => 'float',
        'velocity' => 'float',
        'mention_count' => 'integer',
        'post_count' => 'integer',
        'avg_likes' => 'float',
        'avg_comments' => 'float',
        'avg_shares' => 'float',
        'engagement_rate' => 'float',
        'viral_score' => 'float',
        'first_seen_at' => 'datetime',
        'peak_at' => 'datetime',
        'last_updated_at' => 'datetime',
        'related_keywords' => 'array',
        'top_posts' => 'array',
        'hourly_trend' => 'array',
        'metadata' => 'array',
    ];

    // Trend status
    const STATUS_RISING = 'rising';
    const STATUS_PEAK = 'peak';
    const STATUS_DECLINING = 'declining';
    const STATUS_STABLE = 'stable';

    // Category types
    const CATEGORY_PRODUCT = 'product';
    const CATEGORY_EVENT = 'event';
    const CATEGORY_ENTERTAINMENT = 'entertainment';
    const CATEGORY_NEWS = 'news';
    const CATEGORY_LIFESTYLE = 'lifestyle';
    const CATEGORY_TECH = 'tech';
    const CATEGORY_FOOD = 'food';
    const CATEGORY_FASHION = 'fashion';
    const CATEGORY_OTHER = 'other';

    // ═══════════════════════════════════════════════════════════════
    // Scopes
    // ═══════════════════════════════════════════════════════════════

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeForRegion($query, string $region)
    {
        return $query->where('region', $region);
    }

    public function scopeTrending($query, int $minScore = 50)
    {
        return $query->where('trend_score', '>=', $minScore)
            ->orderByDesc('trend_score');
    }

    public function scopeViral($query, int $minScore = 70)
    {
        return $query->where('viral_score', '>=', $minScore)
            ->orderByDesc('viral_score');
    }

    public function scopeRising($query)
    {
        return $query->where('velocity', '>', 0)
            ->orderByDesc('velocity');
    }

    public function scopeRecent($query, int $hours = 24)
    {
        return $query->where('last_updated_at', '>=', now()->subHours($hours));
    }

    public function scopeInCategory($query, string $category)
    {
        return $query->where('category', $category);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    /**
     * Get trend status based on velocity
     */
    public function getTrendStatus(): string
    {
        if ($this->velocity > 5) {
            return self::STATUS_RISING;
        } elseif ($this->velocity < -5) {
            return self::STATUS_DECLINING;
        } elseif ($this->trend_score >= 80) {
            return self::STATUS_PEAK;
        }
        return self::STATUS_STABLE;
    }

    /**
     * Check if keyword is currently viral
     */
    public function isViral(): bool
    {
        return $this->viral_score >= 70;
    }

    /**
     * Check if keyword is rising
     */
    public function isRising(): bool
    {
        return $this->velocity > 0;
    }

    /**
     * Calculate viral score based on metrics
     */
    public function calculateViralScore(): float
    {
        // Weight factors
        $weights = [
            'trend_score' => 0.25,
            'velocity' => 0.20,
            'engagement_rate' => 0.25,
            'mention_growth' => 0.15,
            'recency' => 0.15,
        ];

        $score = 0;

        // Trend score contribution (0-25)
        $score += ($this->trend_score / 100) * 25;

        // Velocity contribution (0-20)
        $velocityScore = min(max($this->velocity / 10, 0), 1);
        $score += $velocityScore * 20;

        // Engagement rate contribution (0-25)
        $engagementScore = min($this->engagement_rate / 10, 1);
        $score += $engagementScore * 25;

        // Mention growth (0-15)
        $hourlyTrend = $this->hourly_trend ?? [];
        if (count($hourlyTrend) >= 2) {
            $recent = array_slice($hourlyTrend, -6);
            $older = array_slice($hourlyTrend, -12, 6);
            $recentSum = array_sum($recent);
            $olderSum = array_sum($older) ?: 1;
            $growthRate = min(($recentSum / $olderSum) - 1, 1);
            $score += max($growthRate, 0) * 15;
        }

        // Recency bonus (0-15)
        if ($this->last_updated_at) {
            $hoursAgo = $this->last_updated_at->diffInHours(now());
            $recencyScore = max(1 - ($hoursAgo / 48), 0);
            $score += $recencyScore * 15;
        }

        return min($score, 100);
    }

    /**
     * Update viral score
     */
    public function updateViralScore(): void
    {
        $this->update([
            'viral_score' => $this->calculateViralScore(),
            'last_updated_at' => now(),
        ]);
    }

    /**
     * Add hourly data point
     */
    public function addHourlyDataPoint(int $mentions): void
    {
        $hourly = $this->hourly_trend ?? [];
        $hourly[] = $mentions;

        // Keep only last 48 hours
        if (count($hourly) > 48) {
            $hourly = array_slice($hourly, -48);
        }

        // Calculate velocity (mentions per hour change)
        $velocity = 0;
        if (count($hourly) >= 2) {
            $recent = array_slice($hourly, -6);
            $older = array_slice($hourly, -12, 6);
            $velocity = (array_sum($recent) / max(count($recent), 1)) -
                        (array_sum($older) / max(count($older), 1));
        }

        $this->update([
            'hourly_trend' => $hourly,
            'velocity' => $velocity,
            'mention_count' => $this->mention_count + $mentions,
            'last_updated_at' => now(),
        ]);

        $this->updateViralScore();
    }

    /**
     * Get recommendations for content using this keyword
     */
    public function getContentRecommendations(): array
    {
        $recommendations = [];

        if ($this->isViral()) {
            $recommendations[] = [
                'type' => 'timing',
                'message' => 'โพสต์ตอนนี้เลย! คีย์เวิร์ดกำลัง viral',
                'priority' => 'high',
            ];
        }

        if ($this->isRising()) {
            $recommendations[] = [
                'type' => 'timing',
                'message' => 'คีย์เวิร์ดกำลังมาแรง ควรรีบโพสต์',
                'priority' => 'medium',
            ];
        }

        if ($this->engagement_rate > 5) {
            $recommendations[] = [
                'type' => 'engagement',
                'message' => 'คีย์เวิร์ดนี้มี engagement สูง',
                'priority' => 'info',
            ];
        }

        if (!empty($this->related_keywords)) {
            $recommendations[] = [
                'type' => 'keywords',
                'message' => 'ลองใช้คีย์เวิร์ดที่เกี่ยวข้อง: ' . implode(', ', array_slice($this->related_keywords, 0, 5)),
                'priority' => 'info',
            ];
        }

        return $recommendations;
    }

    /**
     * Get top trending keywords for platform
     */
    public static function getTopTrending(string $platform, string $region = 'TH', int $limit = 20): \Illuminate\Database\Eloquent\Collection
    {
        return self::forPlatform($platform)
            ->forRegion($region)
            ->recent(24)
            ->trending(30)
            ->limit($limit)
            ->get();
    }

    /**
     * Get viral keywords for platform
     */
    public static function getViralKeywords(string $platform, string $region = 'TH', int $limit = 10): \Illuminate\Database\Eloquent\Collection
    {
        return self::forPlatform($platform)
            ->forRegion($region)
            ->viral(70)
            ->limit($limit)
            ->get();
    }
}
