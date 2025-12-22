<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

class Post extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'brand_id',
        'campaign_id',
        'social_account_id',
        'platform',
        'content_text',
        'content_type',
        'media_urls',
        'hashtags',
        'link_url',
        'ai_generated',
        'ai_provider',
        'ai_prompt',
        'platform_post_id',
        'platform_url',
        'scheduled_at',
        'published_at',
        'status',
        'error_message',
        'metrics',
        // Viral analysis
        'viral_score',
        'viral_factors',
        'is_viral',
        'peak_engagement_at',
        'engagement_velocity',
        // Comment tracking
        'comments_fetched_count',
        'comments_replied_count',
        'last_comment_check_at',
    ];

    protected $casts = [
        'media_urls' => 'array',
        'hashtags' => 'array',
        'ai_generated' => 'boolean',
        'scheduled_at' => 'datetime',
        'published_at' => 'datetime',
        'metrics' => 'array',
        // Viral analysis
        'viral_score' => 'float',
        'viral_factors' => 'array',
        'is_viral' => 'boolean',
        'peak_engagement_at' => 'datetime',
        'engagement_velocity' => 'float',
        // Comment tracking
        'comments_fetched_count' => 'integer',
        'comments_replied_count' => 'integer',
        'last_comment_check_at' => 'datetime',
    ];

    // Status constants
    const STATUS_DRAFT = 'draft';
    const STATUS_PENDING = 'pending';
    const STATUS_SCHEDULED = 'scheduled';
    const STATUS_PUBLISHING = 'publishing';
    const STATUS_PUBLISHED = 'published';
    const STATUS_FAILED = 'failed';
    const STATUS_DELETED = 'deleted';

    // Content type constants
    const TYPE_TEXT = 'text';
    const TYPE_IMAGE = 'image';
    const TYPE_VIDEO = 'video';
    const TYPE_CAROUSEL = 'carousel';
    const TYPE_STORY = 'story';
    const TYPE_REEL = 'reel';

    // Relationships
    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function brand()
    {
        return $this->belongsTo(Brand::class);
    }

    public function campaign()
    {
        return $this->belongsTo(Campaign::class);
    }

    public function socialAccount()
    {
        return $this->belongsTo(SocialAccount::class);
    }

    public function comments()
    {
        return $this->hasMany(Comment::class);
    }

    public function pendingComments()
    {
        return $this->hasMany(Comment::class)->pending();
    }

    // Scopes
    public function scopePending($query)
    {
        return $query->where('status', self::STATUS_PENDING);
    }

    public function scopeScheduled($query)
    {
        return $query->where('status', self::STATUS_SCHEDULED);
    }

    public function scopePublished($query)
    {
        return $query->where('status', self::STATUS_PUBLISHED);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeDueForPublishing($query)
    {
        return $query->where('status', self::STATUS_SCHEDULED)
            ->where('scheduled_at', '<=', now());
    }

    // Helpers
    public function isPublished(): bool
    {
        return $this->status === self::STATUS_PUBLISHED;
    }

    public function canBeEdited(): bool
    {
        return in_array($this->status, [
            self::STATUS_DRAFT,
            self::STATUS_PENDING,
            self::STATUS_SCHEDULED,
        ]);
    }

    public function toWorkerPayload(): array
    {
        return [
            'id' => $this->id,
            'platform' => $this->platform,
            'content' => [
                'text' => $this->content_text,
                'images' => $this->media_urls ?? [],
                'hashtags' => $this->hashtags ?? [],
                'link' => $this->link_url,
            ],
            'credentials' => $this->socialAccount->getCredentials(),
        ];
    }

    public function updateMetrics(array $metrics): void
    {
        $this->update([
            'metrics' => array_merge($this->metrics ?? [], [
                'likes' => $metrics['likes'] ?? 0,
                'comments' => $metrics['comments'] ?? 0,
                'shares' => $metrics['shares'] ?? 0,
                'views' => $metrics['views'] ?? 0,
                'engagement_rate' => $metrics['engagement_rate'] ?? 0,
                'updated_at' => now()->toIso8601String(),
            ]),
        ]);
    }

    // ═══════════════════════════════════════════════════════════════
    // Viral Analysis
    // ═══════════════════════════════════════════════════════════════

    /**
     * Calculate viral score based on engagement metrics
     */
    public function calculateViralScore(): float
    {
        $metrics = $this->metrics ?? [];
        $likes = $metrics['likes'] ?? 0;
        $comments = $metrics['comments'] ?? 0;
        $shares = $metrics['shares'] ?? 0;
        $views = $metrics['views'] ?? 1;

        // Engagement rate contribution
        $engagementRate = ($likes + $comments * 2 + $shares * 3) / max($views, 1) * 100;

        // Base score from engagement
        $score = min($engagementRate * 10, 40);

        // Velocity bonus (if engagement is growing fast)
        if ($this->engagement_velocity > 0) {
            $score += min($this->engagement_velocity * 5, 30);
        }

        // Share ratio bonus (shares indicate viral potential)
        $shareRatio = $shares / max($likes + $comments, 1);
        $score += min($shareRatio * 50, 20);

        // Time decay (older posts score less)
        if ($this->published_at) {
            $hoursSincePublish = $this->published_at->diffInHours(now());
            if ($hoursSincePublish < 6) {
                $score += 10; // Recent post bonus
            } elseif ($hoursSincePublish > 48) {
                $score -= 10; // Old post penalty
            }
        }

        return min(max($score, 0), 100);
    }

    /**
     * Update viral analysis
     */
    public function updateViralAnalysis(): void
    {
        $score = $this->calculateViralScore();
        $isViral = $score >= 70;

        $this->update([
            'viral_score' => $score,
            'is_viral' => $isViral,
            'viral_factors' => [
                'engagement_rate' => $this->metrics['engagement_rate'] ?? 0,
                'share_ratio' => ($this->metrics['shares'] ?? 0) / max(($this->metrics['likes'] ?? 0) + ($this->metrics['comments'] ?? 0), 1),
                'velocity' => $this->engagement_velocity,
                'calculated_at' => now()->toIso8601String(),
            ],
            'peak_engagement_at' => $isViral && !$this->peak_engagement_at ? now() : $this->peak_engagement_at,
        ]);
    }

    /**
     * Scope for viral posts
     */
    public function scopeViral($query)
    {
        return $query->where('is_viral', true);
    }

    /**
     * Scope for posts needing comment check
     */
    public function scopeNeedsCommentCheck($query, int $minutesThreshold = 30)
    {
        return $query->published()
            ->where(function ($q) use ($minutesThreshold) {
                $q->whereNull('last_comment_check_at')
                    ->orWhere('last_comment_check_at', '<', now()->subMinutes($minutesThreshold));
            });
    }

    /**
     * Mark comments as checked
     */
    public function markCommentsChecked(int $fetched = 0, int $replied = 0): void
    {
        $this->update([
            'last_comment_check_at' => now(),
            'comments_fetched_count' => $this->comments_fetched_count + $fetched,
            'comments_replied_count' => $this->comments_replied_count + $replied,
        ]);
    }
}
