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
    ];

    protected $casts = [
        'media_urls' => 'array',
        'hashtags' => 'array',
        'ai_generated' => 'boolean',
        'scheduled_at' => 'datetime',
        'published_at' => 'datetime',
        'metrics' => 'array',
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
}
