<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * @property int $id
 * @property int $api_key_pool_id
 * @property string $label
 * @property string $encrypted_api_key
 * @property string|null $base_url
 * @property string|null $model_id
 * @property int $priority
 * @property int $weight
 * @property string $status
 * @property \Illuminate\Support\Carbon|null $cooldown_until
 * @property \Illuminate\Support\Carbon|null $last_used_at
 * @property int $requests_today
 * @property int $total_requests
 * @property int $daily_limit
 * @property int $success_count
 * @property int $failure_count
 * @property int $consecutive_failures
 * @property string|null $last_error
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 *
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPoolMember where($column, $operator = null, $value = null, $boolean = 'and')
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPoolMember create(array $attributes = [])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPoolMember find($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPoolMember findOrFail($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPoolMember first($columns = ['*'])
 */
class ApiKeyPoolMember extends Model
{
    use HasFactory;

    protected $table = 'api_key_pool_members';

    // Status constants
    const STATUS_ACTIVE = 'active';
    const STATUS_COOLDOWN = 'cooldown';
    const STATUS_EXHAUSTED = 'exhausted';
    const STATUS_ERROR = 'error';

    protected $fillable = [
        'api_key_pool_id',
        'label',
        'encrypted_api_key',
        'base_url',
        'model_id',
        'priority',
        'weight',
        'status',
        'cooldown_until',
        'last_used_at',
        'requests_today',
        'total_requests',
        'daily_limit',
        'success_count',
        'failure_count',
        'consecutive_failures',
        'last_error',
    ];

    protected $casts = [
        'priority' => 'integer',
        'weight' => 'integer',
        'cooldown_until' => 'datetime',
        'last_used_at' => 'datetime',
        'requests_today' => 'integer',
        'total_requests' => 'integer',
        'daily_limit' => 'integer',
        'success_count' => 'integer',
        'failure_count' => 'integer',
        'consecutive_failures' => 'integer',
    ];

    protected $hidden = [
        'encrypted_api_key',
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_ACTIVE,
            self::STATUS_COOLDOWN,
            self::STATUS_EXHAUSTED,
            self::STATUS_ERROR,
        ];
    }

    // Relationships
    public function apiKeyPool(): BelongsTo
    {
        return $this->belongsTo(ApiKeyPool::class);
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
        return $query->orderBy('requests_today', 'asc')
            ->orderBy('total_requests', 'asc');
    }

    // Accessors
    public function getDecryptedApiKeyAttribute(): string
    {
        return decrypt($this->encrypted_api_key);
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

        if ($this->daily_limit > 0 && $this->requests_today >= $this->daily_limit) {
            return false;
        }

        return true;
    }

    public function canRequestToday(): bool
    {
        if ($this->daily_limit === 0) {
            return true;
        }
        return $this->requests_today < $this->daily_limit;
    }

    public function startCooldown(int $seconds): void
    {
        $this->update([
            'status' => self::STATUS_COOLDOWN,
            'cooldown_until' => now()->addSeconds($seconds),
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
        $this->increment('total_requests');
        $this->increment('requests_today');
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
            'last_error' => $error,
        ]);
    }

    public function markAsExhausted(): void
    {
        $this->update([
            'status' => self::STATUS_EXHAUSTED,
            'last_error' => 'Daily quota exhausted',
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
