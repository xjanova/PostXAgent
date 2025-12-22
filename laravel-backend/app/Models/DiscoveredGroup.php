<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;

class DiscoveredGroup extends Model
{
    use HasFactory;

    protected $fillable = [
        'platform',
        'group_id',
        'group_name',
        'group_url',
        'description',
        'keywords',
        'category',
        'tags',
        'member_count',
        'engagement_rate',
        'posts_per_day',
        'activity_level',
        'is_public',
        'requires_approval',
        'language',
        'admin_info',
        'is_joined',
        'join_requested_at',
        'joined_at',
        'is_banned',
        'banned_at',
        'ban_reason',
        'our_post_count',
        'successful_posts',
        'failed_posts',
        'deleted_posts',
        'last_post_at',
        'last_checked_at',
        'quality_score',
        'relevance_score',
        'success_rate',
        'quality_factors',
        'posting_rules',
        'best_posting_times',
        'content_preferences',
    ];

    protected $casts = [
        'keywords' => 'array',
        'tags' => 'array',
        'admin_info' => 'array',
        'quality_factors' => 'array',
        'posting_rules' => 'array',
        'best_posting_times' => 'array',
        'content_preferences' => 'array',
        'member_count' => 'integer',
        'engagement_rate' => 'double',
        'posts_per_day' => 'integer',
        'is_public' => 'boolean',
        'requires_approval' => 'boolean',
        'is_joined' => 'boolean',
        'is_banned' => 'boolean',
        'our_post_count' => 'integer',
        'successful_posts' => 'integer',
        'failed_posts' => 'integer',
        'deleted_posts' => 'integer',
        'quality_score' => 'double',
        'relevance_score' => 'double',
        'success_rate' => 'double',
        'join_requested_at' => 'datetime',
        'joined_at' => 'datetime',
        'banned_at' => 'datetime',
        'last_post_at' => 'datetime',
        'last_checked_at' => 'datetime',
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // RELATIONSHIPS
    // ═══════════════════════════════════════════════════════════════════════

    public function seekAndPostTasks(): BelongsToMany
    {
        return $this->belongsToMany(SeekAndPostTask::class, 'seek_task_groups')
            ->withPivot(['status', 'posts_in_group', 'last_post_at'])
            ->withTimestamps();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SCOPES
    // ═══════════════════════════════════════════════════════════════════════

    public function scopeJoined($query)
    {
        return $query->where('is_joined', true);
    }

    public function scopeNotBanned($query)
    {
        return $query->where('is_banned', false);
    }

    public function scopeByPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeHighQuality($query, float $minScore = 70)
    {
        return $query->where('quality_score', '>=', $minScore);
    }

    public function scopeActive($query)
    {
        return $query->whereIn('activity_level', ['very_active', 'active', 'moderate']);
    }

    public function scopeByKeyword($query, string $keyword)
    {
        return $query->whereJsonContains('keywords', $keyword);
    }

    public function scopeAvailableForPosting($query)
    {
        return $query->where('is_joined', true)
            ->where('is_banned', false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Calculate and update quality score based on various factors
     */
    public function calculateQualityScore(): float
    {
        $factors = [];

        // Member count score (0-25 points)
        $memberScore = match (true) {
            $this->member_count >= 50000 => 25,
            $this->member_count >= 10000 => 20,
            $this->member_count >= 5000 => 15,
            $this->member_count >= 1000 => 10,
            $this->member_count >= 500 => 5,
            default => 2,
        };
        $factors['member_count'] = $memberScore;

        // Activity level score (0-25 points)
        $activityScore = match ($this->activity_level) {
            'very_active' => 25,
            'active' => 20,
            'moderate' => 15,
            'low' => 5,
            default => 0,
        };
        $factors['activity_level'] = $activityScore;

        // Our success rate score (0-30 points)
        $successScore = min(30, $this->success_rate * 0.3);
        $factors['success_rate'] = $successScore;

        // Engagement rate score (0-20 points)
        $engagementScore = min(20, $this->engagement_rate * 100);
        $factors['engagement_rate'] = $engagementScore;

        $this->quality_factors = $factors;
        $this->quality_score = array_sum($factors);
        $this->save();

        return $this->quality_score;
    }

    /**
     * Record a post attempt in this group
     */
    public function recordPost(bool $successful): void
    {
        $this->our_post_count++;

        if ($successful) {
            $this->successful_posts++;
        } else {
            $this->failed_posts++;
        }

        $this->success_rate = $this->our_post_count > 0
            ? ($this->successful_posts / $this->our_post_count) * 100
            : 0;

        $this->last_post_at = now();
        $this->save();
        $this->calculateQualityScore();
    }

    /**
     * Mark as banned
     */
    public function markAsBanned(string $reason = null): void
    {
        $this->is_banned = true;
        $this->banned_at = now();
        $this->ban_reason = $reason;
        $this->save();
    }

    /**
     * Mark as joined
     */
    public function markAsJoined(): void
    {
        $this->is_joined = true;
        $this->joined_at = now();
        $this->save();
    }

    /**
     * Get the best time to post in this group
     */
    public function getBestPostingTime(): ?string
    {
        if (empty($this->best_posting_times)) {
            return null;
        }

        // Return the time with highest engagement
        $times = $this->best_posting_times;
        if (is_array($times) && !empty($times)) {
            arsort($times);
            return array_key_first($times);
        }

        return null;
    }

    /**
     * Check if we can post now based on posting rules
     */
    public function canPostNow(): bool
    {
        if (!$this->is_joined || $this->is_banned) {
            return false;
        }

        // Check if we've posted too recently
        if ($this->last_post_at && $this->last_post_at->diffInHours(now()) < 4) {
            return false;
        }

        return true;
    }

    /**
     * Get groups matching keywords with caching
     */
    public static function findByKeywords(array $keywords, string $platform = null, int $limit = 50): \Illuminate\Database\Eloquent\Collection
    {
        $query = static::query()
            ->availableForPosting()
            ->orderByDesc('quality_score');

        if ($platform) {
            $query->byPlatform($platform);
        }

        // Match any of the keywords
        $query->where(function ($q) use ($keywords) {
            foreach ($keywords as $keyword) {
                $q->orWhereJsonContains('keywords', $keyword)
                    ->orWhere('group_name', 'like', "%{$keyword}%")
                    ->orWhere('description', 'like', "%{$keyword}%");
            }
        });

        return $query->limit($limit)->get();
    }
}
