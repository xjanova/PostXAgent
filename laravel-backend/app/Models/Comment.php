<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * @property int $id
 * @property int $post_id
 * @property string $platform
 * @property string|null $platform_comment_id
 * @property int|null $parent_comment_id
 * @property string|null $author_name
 * @property string|null $author_id
 * @property string|null $author_avatar_url
 * @property string|null $content_text
 * @property string|null $media_url
 * @property string|null $sentiment
 * @property float|null $sentiment_score
 * @property bool $is_question
 * @property bool $requires_reply
 * @property int $priority
 * @property \Illuminate\Support\Carbon|null $replied_at
 * @property string|null $reply_content
 * @property string|null $reply_comment_id
 * @property string|null $reply_status
 * @property int $likes_count
 * @property int $replies_count
 * @property array|null $metadata
 * @property \Illuminate\Support\Carbon|null $commented_at
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property-read \App\Models\Post $post
 * @property-read \App\Models\Comment|null $parent
 * @property-read \Illuminate\Database\Eloquent\Collection<int, \App\Models\Comment> $replies
 *
 * @method static \Illuminate\Database\Eloquent\Builder|Comment where($column, $operator = null, $value = null, $boolean = 'and')
 * @method static \Illuminate\Database\Eloquent\Builder|Comment create(array $attributes = [])
 * @method static \Illuminate\Database\Eloquent\Builder|Comment find($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|Comment findOrFail($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|Comment updateOrCreate(array $attributes, array $values = [])
 * @method static \Illuminate\Database\Eloquent\Builder|Comment pending()
 * @method static \Illuminate\Database\Eloquent\Builder|Comment replied()
 * @method static \Illuminate\Database\Eloquent\Builder|Comment positive()
 * @method static \Illuminate\Database\Eloquent\Builder|Comment neutral()
 * @method static \Illuminate\Database\Eloquent\Builder|Comment negative()
 * @method static \Illuminate\Database\Eloquent\Builder|Comment questions()
 * @method static \Illuminate\Database\Eloquent\Builder|Comment highPriority()
 */
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

    public function scopeReplied($query)
    {
        return $query->where('reply_status', self::REPLY_REPLIED);
    }

    public function scopeNeutral($query)
    {
        return $query->where('sentiment', self::SENTIMENT_NEUTRAL);
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
