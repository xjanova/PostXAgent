<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

class Comment extends Model
{
    use HasFactory;

    protected $fillable = [
        'post_id',
        'platform',
        'platform_comment_id',
        'parent_comment_id',
        'author_name',
        'author_id',
        'author_avatar_url',
        'content_text',
        'media_url',
        'sentiment',
        'sentiment_score',
        'is_question',
        'requires_reply',
        'priority',
        'replied_at',
        'reply_content',
        'reply_comment_id',
        'reply_status',
        'likes_count',
        'replies_count',
        'metadata',
        'commented_at',
    ];

    protected $casts = [
        'is_question' => 'boolean',
        'requires_reply' => 'boolean',
        'sentiment_score' => 'float',
        'priority' => 'integer',
        'likes_count' => 'integer',
        'replies_count' => 'integer',
        'metadata' => 'array',
        'replied_at' => 'datetime',
        'commented_at' => 'datetime',
    ];

    // Sentiment constants
    const SENTIMENT_POSITIVE = 'positive';
    const SENTIMENT_NEGATIVE = 'negative';
    const SENTIMENT_NEUTRAL = 'neutral';

    // Reply status constants
    const REPLY_PENDING = 'pending';
    const REPLY_REPLIED = 'replied';
    const REPLY_SKIPPED = 'skipped';
    const REPLY_FAILED = 'failed';

    // Priority levels
    const PRIORITY_LOW = 0;
    const PRIORITY_NORMAL = 5;
    const PRIORITY_HIGH = 10;
    const PRIORITY_URGENT = 20;

    // ═══════════════════════════════════════════════════════════════
    // Relationships
    // ═══════════════════════════════════════════════════════════════

    public function post(): BelongsTo
    {
        return $this->belongsTo(Post::class);
    }

    public function parent(): BelongsTo
    {
        return $this->belongsTo(Comment::class, 'parent_comment_id');
    }

    public function replies(): HasMany
    {
        return $this->hasMany(Comment::class, 'parent_comment_id');
    }

    // ═══════════════════════════════════════════════════════════════
    // Scopes
    // ═══════════════════════════════════════════════════════════════

    public function scopePending($query)
    {
        return $query->where('reply_status', self::REPLY_PENDING);
    }

    public function scopeRequiresReply($query)
    {
        return $query->where('requires_reply', true)
            ->where('reply_status', self::REPLY_PENDING);
    }

    public function scopeQuestions($query)
    {
        return $query->where('is_question', true);
    }

    public function scopeNegative($query)
    {
        return $query->where('sentiment', self::SENTIMENT_NEGATIVE);
    }

    public function scopePositive($query)
    {
        return $query->where('sentiment', self::SENTIMENT_POSITIVE);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeHighPriority($query)
    {
        return $query->where('priority', '>=', self::PRIORITY_HIGH);
    }

    public function scopeOrderByPriority($query)
    {
        return $query->orderByDesc('priority')
            ->orderBy('commented_at');
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    public function isReplied(): bool
    {
        return $this->reply_status === self::REPLY_REPLIED;
    }

    public function isPending(): bool
    {
        return $this->reply_status === self::REPLY_PENDING;
    }

    public function isNegative(): bool
    {
        return $this->sentiment === self::SENTIMENT_NEGATIVE;
    }

    public function isPositive(): bool
    {
        return $this->sentiment === self::SENTIMENT_POSITIVE;
    }

    public function isQuestion(): bool
    {
        return $this->is_question;
    }

    public function markAsReplied(string $replyContent, ?string $replyCommentId = null): void
    {
        $this->update([
            'reply_status' => self::REPLY_REPLIED,
            'reply_content' => $replyContent,
            'reply_comment_id' => $replyCommentId,
            'replied_at' => now(),
        ]);
    }

    public function markAsSkipped(string $reason = null): void
    {
        $this->update([
            'reply_status' => self::REPLY_SKIPPED,
            'metadata' => array_merge($this->metadata ?? [], [
                'skip_reason' => $reason,
                'skipped_at' => now()->toIso8601String(),
            ]),
        ]);
    }

    public function markAsFailed(string $error): void
    {
        $this->update([
            'reply_status' => self::REPLY_FAILED,
            'metadata' => array_merge($this->metadata ?? [], [
                'error' => $error,
                'failed_at' => now()->toIso8601String(),
            ]),
        ]);
    }

    /**
     * Calculate priority based on various factors
     */
    public function calculatePriority(): int
    {
        $priority = self::PRIORITY_NORMAL;

        // Questions are higher priority
        if ($this->is_question) {
            $priority += 5;
        }

        // Negative sentiment is urgent
        if ($this->sentiment === self::SENTIMENT_NEGATIVE) {
            $priority += 10;
        }

        // High engagement comments
        if ($this->likes_count > 10) {
            $priority += 3;
        }

        // Recent comments (within 1 hour)
        if ($this->commented_at && $this->commented_at->diffInHours(now()) < 1) {
            $priority += 5;
        }

        return min($priority, self::PRIORITY_URGENT);
    }

    /**
     * Update sentiment analysis
     */
    public function updateSentiment(string $sentiment, float $score): void
    {
        $this->update([
            'sentiment' => $sentiment,
            'sentiment_score' => $score,
            'priority' => $this->calculatePriority(),
        ]);
    }

    /**
     * Convert to AI context for reply generation
     */
    public function toAIContext(): array
    {
        return [
            'comment_id' => $this->id,
            'author_name' => $this->author_name,
            'content' => $this->content_text,
            'sentiment' => $this->sentiment,
            'is_question' => $this->is_question,
            'platform' => $this->platform,
            'post_content' => $this->post?->content_text,
            'brand_name' => $this->post?->brand?->name,
            'commented_at' => $this->commented_at?->toIso8601String(),
        ];
    }
}
