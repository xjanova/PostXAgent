<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;

/**
 * @property int $id
 * @property int $user_id
 * @property string $provider
 * @property string $name
 * @property string|null $description
 * @property string $rotation_strategy
 * @property int $cooldown_minutes
 * @property float $min_credits_required
 * @property bool $auto_failover
 * @property bool $auto_provision
 * @property array|null $preferred_gpu_types
 * @property array|null $preferred_regions
 * @property float|null $max_price_per_hour
 * @property bool $is_active
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 */
class GpuAccountPool extends Model
{
    use HasFactory, SoftDeletes;

    // Rotation strategies
    public const STRATEGY_ROUND_ROBIN = 'round_robin';
    public const STRATEGY_RANDOM = 'random';
    public const STRATEGY_LEAST_USED = 'least_used';
    public const STRATEGY_MOST_CREDITS = 'most_credits';
    public const STRATEGY_PRIORITY = 'priority';

    // Provider constants
    public const PROVIDER_MIXED = 'mixed';

    protected $fillable = [
        'user_id',
        'provider',
        'name',
        'description',
        'rotation_strategy',
        'cooldown_minutes',
        'min_credits_required',
        'auto_failover',
        'auto_provision',
        'preferred_gpu_types',
        'preferred_regions',
        'max_price_per_hour',
        'is_active',
    ];

    protected $casts = [
        'cooldown_minutes' => 'integer',
        'min_credits_required' => 'decimal:4',
        'auto_failover' => 'boolean',
        'auto_provision' => 'boolean',
        'preferred_gpu_types' => 'array',
        'preferred_regions' => 'array',
        'max_price_per_hour' => 'decimal:4',
        'is_active' => 'boolean',
    ];

    public static function getStrategies(): array
    {
        return [
            self::STRATEGY_ROUND_ROBIN,
            self::STRATEGY_RANDOM,
            self::STRATEGY_LEAST_USED,
            self::STRATEGY_MOST_CREDITS,
            self::STRATEGY_PRIORITY,
        ];
    }

    // Relationships
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function members(): HasMany
    {
        return $this->hasMany(GpuPoolMember::class);
    }

    public function providerAccounts(): BelongsToMany
    {
        return $this->belongsToMany(GpuProviderAccount::class, 'gpu_pool_members')
            ->withPivot([
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
            ])
            ->withTimestamps();
    }

    public function instances(): HasMany
    {
        return $this->hasMany(GpuInstance::class);
    }

    public function activityLogs(): HasMany
    {
        return $this->hasMany(GpuActivityLog::class);
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeForProvider($query, string $provider)
    {
        return $query->where(function ($q) use ($provider) {
            $q->where('provider', $provider)
                ->orWhere('provider', self::PROVIDER_MIXED);
        });
    }

    // Helpers
    public function getAvailableAccounts()
    {
        return $this->members()
            ->where('status', GpuPoolMember::STATUS_ACTIVE)
            ->where(function ($query) {
                $query->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            })
            ->whereHas('providerAccount', function ($query) {
                $query->where('is_active', true)
                    ->where('credits_balance', '>=', $this->min_credits_required);
            })
            ->with('providerAccount')
            ->get();
    }

    public function hasAvailableAccounts(): bool
    {
        return $this->getAvailableAccounts()->isNotEmpty();
    }

    public function getActiveAccountCount(): int
    {
        return $this->members()
            ->where('status', GpuPoolMember::STATUS_ACTIVE)
            ->count();
    }

    public function getTotalCredits(): float
    {
        return $this->providerAccounts()
            ->where('is_active', true)
            ->sum('credits_balance');
    }

    public function getStatistics(): array
    {
        $members = $this->members()->with('providerAccount')->get();

        $statusCounts = $members->groupBy('status')->map->count();
        $totalCredits = $members->sum(fn ($m) => $m->providerAccount?->credits_balance ?? 0);

        return [
            'total_accounts' => $members->count(),
            'active' => $statusCounts->get(GpuPoolMember::STATUS_ACTIVE, 0),
            'cooldown' => $statusCounts->get(GpuPoolMember::STATUS_COOLDOWN, 0),
            'suspended' => $statusCounts->get(GpuPoolMember::STATUS_SUSPENDED, 0),
            'depleted' => $statusCounts->get(GpuPoolMember::STATUS_DEPLETED, 0),
            'error' => $statusCounts->get(GpuPoolMember::STATUS_ERROR, 0),
            'total_credits' => $totalCredits,
            'instances_today' => $members->sum('instances_today'),
            'total_instances' => $members->sum('total_instances'),
            'credits_used_today' => $members->sum('credits_used_today'),
            'available_now' => $this->getAvailableAccounts()->count(),
        ];
    }
}
