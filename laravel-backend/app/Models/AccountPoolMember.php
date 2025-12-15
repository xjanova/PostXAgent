<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class AccountPoolMember extends Model
{
    use HasFactory;

    protected $table = 'account_pool_members';

    // Status constants
    const STATUS_ACTIVE = 'active';
    const STATUS_COOLDOWN = 'cooldown';
    const STATUS_SUSPENDED = 'suspended';
    const STATUS_BANNED = 'banned';
    const STATUS_ERROR = 'error';

    protected $fillable = [
        'account_pool_id',
        'social_account_id',
        'priority',
        'weight',
        'status',
        'cooldown_until',
        'last_used_at',
        'posts_today',
        'total_posts',
        'success_count',
        'failure_count',
        'consecutive_failures',
        'last_failure_at',
        'last_error',
    ];

    protected $casts = [
        'priority' => 'integer',
        'weight' => 'integer',
        'cooldown_until' => 'datetime',
        'last_used_at' => 'datetime',
        'last_failure_at' => 'datetime',
        'posts_today' => 'integer',
        'total_posts' => 'integer',
        'success_count' => 'integer',
        'failure_count' => 'integer',
        'consecutive_failures' => 'integer',
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_ACTIVE,
            self::STATUS_COOLDOWN,
            self::STATUS_SUSPENDED,
            self::STATUS_BANNED,
            self::STATUS_ERROR,
        ];
    }

    // Relationships
    public function accountPool(): BelongsTo
    {
        return $this->belongsTo(AccountPool::class);
    }

    public function socialAccount(): BelongsTo
    {
        return $this->belongsTo(SocialAccount::class);
    }

    // Query Scopes
    public function scopeAvailable($query)
    {
        return $query->where('status', self::STATUS_ACTIVE)
            ->where(function ($q) {
                $q->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            });
    }

    public function scopeByPriority($query)
    {
        return $query->orderBy('priority', 'asc')
            ->orderBy('weight', 'desc');
    }

    public function scopeByLeastUsed($query)
    {
        return $query->orderBy('posts_today', 'asc')
            ->orderBy('total_posts', 'asc');
    }

    // Helpers
    public function isAvailable(): bool
    {
        if ($this->status !== self::STATUS_ACTIVE) {
            return false;
        }

        if ($this->cooldown_until && $this->cooldown_until->isFuture()) {
            return false;
        }

        return true;
    }

    public function canPostToday(int $maxPosts): bool
    {
        return $this->posts_today < $maxPosts;
    }

    public function startCooldown(int $minutes): void
    {
        $this->update([
            'status' => self::STATUS_COOLDOWN,
            'cooldown_until' => now()->addMinutes($minutes),
        ]);
    }

    public function endCooldown(): void
    {
        if ($this->status === self::STATUS_COOLDOWN) {
            $this->update([
                'status' => self::STATUS_ACTIVE,
                'cooldown_until' => null,
            ]);
        }
    }

    public function recordSuccess(): void
    {
        $this->increment('success_count');
        $this->increment('total_posts');
        $this->increment('posts_today');
        $this->update([
            'last_used_at' => now(),
            'consecutive_failures' => 0,
            'last_error' => null,
        ]);
    }

    public function recordFailure(string $error): void
    {
        $this->increment('failure_count');
        $this->increment('consecutive_failures');
        $this->update([
            'last_failure_at' => now(),
            'last_error' => $error,
        ]);
    }

    public function markAsBanned(string $reason = null): void
    {
        $this->update([
            'status' => self::STATUS_BANNED,
            'last_error' => $reason ?? 'Account banned by platform',
        ]);
    }

    public function markAsSuspended(string $reason = null): void
    {
        $this->update([
            'status' => self::STATUS_SUSPENDED,
            'last_error' => $reason ?? 'Account suspended',
        ]);
    }

    public function reactivate(): void
    {
        $this->update([
            'status' => self::STATUS_ACTIVE,
            'cooldown_until' => null,
            'consecutive_failures' => 0,
            'last_error' => null,
        ]);
    }

    public function getSuccessRate(): float
    {
        $total = $this->success_count + $this->failure_count;
        if ($total === 0) {
            return 100.0;
        }
        return round(($this->success_count / $total) * 100, 2);
    }
}
