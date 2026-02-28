<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * @property int $id
 * @property int $gpu_account_pool_id
 * @property int $gpu_provider_account_id
 * @property int $priority
 * @property int $weight
 * @property string $status
 * @property \Illuminate\Support\Carbon|null $cooldown_until
 * @property \Illuminate\Support\Carbon|null $last_used_at
 * @property int $instances_today
 * @property int $total_instances
 * @property float $credits_used_today
 * @property int $success_count
 * @property int $failure_count
 * @property int $consecutive_failures
 * @property string|null $last_error
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property-read GpuProviderAccount $providerAccount
 *
 * @mixin \Illuminate\Database\Eloquent\Builder<static>
 */
class GpuPoolMember extends Model
{
    use HasFactory;

    protected $table = 'gpu_pool_members';

    // Statuses
    public const STATUS_ACTIVE = 'active';
    public const STATUS_COOLDOWN = 'cooldown';
    public const STATUS_SUSPENDED = 'suspended';
    public const STATUS_DEPLETED = 'depleted';
    public const STATUS_ERROR = 'error';

    protected $fillable = [
        'gpu_account_pool_id',
        'gpu_provider_account_id',
        'priority',
        'weight',
        'status',
        'cooldown_until',
        'last_used_at',
        'instances_today',
        'total_instances',
        'credits_used_today',
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
        'instances_today' => 'integer',
        'total_instances' => 'integer',
        'credits_used_today' => 'decimal:4',
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
            self::STATUS_DEPLETED,
            self::STATUS_ERROR,
        ];
    }

    // Relationships
    public function accountPool(): BelongsTo
    {
        return $this->belongsTo(GpuAccountPool::class, 'gpu_account_pool_id');
    }

    public function providerAccount(): BelongsTo
    {
        return $this->belongsTo(GpuProviderAccount::class, 'gpu_provider_account_id');
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('status', self::STATUS_ACTIVE);
    }

    public function scopeAvailable($query)
    {
        return $query->where('status', self::STATUS_ACTIVE)
            ->where(function ($q) {
                $q->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            });
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

        return $this->providerAccount?->isAvailable() ?? false;
    }

    public function recordSuccess(): void
    {
        $this->increment('success_count');
        $this->increment('total_instances');
        $this->increment('instances_today');
        $this->update([
            'consecutive_failures' => 0,
            'last_used_at' => now(),
            'last_error' => null,
        ]);
    }

    public function recordFailure(string $error): void
    {
        $this->increment('failure_count');
        $this->increment('consecutive_failures');
        $this->update([
            'last_error' => $error,
            'last_used_at' => now(),
        ]);
    }

    public function recordCreditsUsed(float $credits): void
    {
        $this->increment('credits_used_today', $credits);
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
        $this->update([
            'status' => self::STATUS_ACTIVE,
            'cooldown_until' => null,
        ]);
    }

    public function markAsDepleted(): void
    {
        $this->update([
            'status' => self::STATUS_DEPLETED,
        ]);
    }

    public function markAsError(string $error): void
    {
        $this->update([
            'status' => self::STATUS_ERROR,
            'last_error' => $error,
        ]);
    }

    public function markAsSuspended(string $reason): void
    {
        $this->update([
            'status' => self::STATUS_SUSPENDED,
            'last_error' => $reason,
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
