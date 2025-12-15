<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class AccountStatusLog extends Model
{
    use HasFactory;

    protected $table = 'account_status_logs';

    // Event types
    const EVENT_POST_SUCCESS = 'post_success';
    const EVENT_POST_FAILED = 'post_failed';
    const EVENT_RATE_LIMITED = 'rate_limited';
    const EVENT_TOKEN_EXPIRED = 'token_expired';
    const EVENT_TOKEN_REFRESHED = 'token_refreshed';
    const EVENT_ACCOUNT_BANNED = 'account_banned';
    const EVENT_ACCOUNT_SUSPENDED = 'account_suspended';
    const EVENT_ACCOUNT_RECOVERED = 'account_recovered';
    const EVENT_MANUAL_STATUS_CHANGE = 'manual_status_change';
    const EVENT_COOLDOWN_STARTED = 'cooldown_started';
    const EVENT_COOLDOWN_ENDED = 'cooldown_ended';

    // Triggered by
    const TRIGGERED_BY_SYSTEM = 'system';
    const TRIGGERED_BY_USER = 'user';
    const TRIGGERED_BY_API = 'api';

    protected $fillable = [
        'social_account_id',
        'account_pool_id',
        'post_id',
        'event_type',
        'old_status',
        'new_status',
        'message',
        'metadata',
        'triggered_by',
    ];

    protected $casts = [
        'metadata' => 'array',
    ];

    public static function getEventTypes(): array
    {
        return [
            self::EVENT_POST_SUCCESS,
            self::EVENT_POST_FAILED,
            self::EVENT_RATE_LIMITED,
            self::EVENT_TOKEN_EXPIRED,
            self::EVENT_TOKEN_REFRESHED,
            self::EVENT_ACCOUNT_BANNED,
            self::EVENT_ACCOUNT_SUSPENDED,
            self::EVENT_ACCOUNT_RECOVERED,
            self::EVENT_MANUAL_STATUS_CHANGE,
            self::EVENT_COOLDOWN_STARTED,
            self::EVENT_COOLDOWN_ENDED,
        ];
    }

    // Relationships
    public function socialAccount(): BelongsTo
    {
        return $this->belongsTo(SocialAccount::class);
    }

    public function accountPool(): BelongsTo
    {
        return $this->belongsTo(AccountPool::class);
    }

    public function post(): BelongsTo
    {
        return $this->belongsTo(Post::class);
    }

    // Query Scopes
    public function scopeForAccount($query, int $accountId)
    {
        return $query->where('social_account_id', $accountId);
    }

    public function scopeForPool($query, int $poolId)
    {
        return $query->where('account_pool_id', $poolId);
    }

    public function scopeOfType($query, string $eventType)
    {
        return $query->where('event_type', $eventType);
    }

    public function scopeRecent($query, int $hours = 24)
    {
        return $query->where('created_at', '>=', now()->subHours($hours));
    }

    public function scopeFailures($query)
    {
        return $query->whereIn('event_type', [
            self::EVENT_POST_FAILED,
            self::EVENT_RATE_LIMITED,
            self::EVENT_TOKEN_EXPIRED,
            self::EVENT_ACCOUNT_BANNED,
            self::EVENT_ACCOUNT_SUSPENDED,
        ]);
    }

    // Static factory methods
    public static function logSuccess(
        int $accountId,
        ?int $poolId = null,
        ?int $postId = null,
        ?array $metadata = null
    ): self {
        return self::create([
            'social_account_id' => $accountId,
            'account_pool_id' => $poolId,
            'post_id' => $postId,
            'event_type' => self::EVENT_POST_SUCCESS,
            'message' => 'Post published successfully',
            'metadata' => $metadata,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logFailure(
        int $accountId,
        string $errorMessage,
        ?int $poolId = null,
        ?int $postId = null,
        ?array $metadata = null
    ): self {
        return self::create([
            'social_account_id' => $accountId,
            'account_pool_id' => $poolId,
            'post_id' => $postId,
            'event_type' => self::EVENT_POST_FAILED,
            'message' => $errorMessage,
            'metadata' => $metadata,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logBan(
        int $accountId,
        string $reason,
        ?int $poolId = null,
        ?array $metadata = null
    ): self {
        return self::create([
            'social_account_id' => $accountId,
            'account_pool_id' => $poolId,
            'event_type' => self::EVENT_ACCOUNT_BANNED,
            'old_status' => AccountPoolMember::STATUS_ACTIVE,
            'new_status' => AccountPoolMember::STATUS_BANNED,
            'message' => $reason,
            'metadata' => $metadata,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logRateLimited(
        int $accountId,
        ?int $poolId = null,
        ?array $metadata = null
    ): self {
        return self::create([
            'social_account_id' => $accountId,
            'account_pool_id' => $poolId,
            'event_type' => self::EVENT_RATE_LIMITED,
            'message' => 'Rate limit exceeded',
            'metadata' => $metadata,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logCooldownStarted(
        int $accountId,
        int $minutes,
        ?int $poolId = null
    ): self {
        return self::create([
            'social_account_id' => $accountId,
            'account_pool_id' => $poolId,
            'event_type' => self::EVENT_COOLDOWN_STARTED,
            'old_status' => AccountPoolMember::STATUS_ACTIVE,
            'new_status' => AccountPoolMember::STATUS_COOLDOWN,
            'message' => "Cooldown started for {$minutes} minutes",
            'metadata' => ['cooldown_minutes' => $minutes],
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logStatusChange(
        int $accountId,
        string $oldStatus,
        string $newStatus,
        string $reason,
        string $triggeredBy = self::TRIGGERED_BY_USER,
        ?int $poolId = null
    ): self {
        return self::create([
            'social_account_id' => $accountId,
            'account_pool_id' => $poolId,
            'event_type' => self::EVENT_MANUAL_STATUS_CHANGE,
            'old_status' => $oldStatus,
            'new_status' => $newStatus,
            'message' => $reason,
            'triggered_by' => $triggeredBy,
        ]);
    }
}
