<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;

class AccountPool extends Model
{
    use HasFactory, SoftDeletes;

    // Rotation strategies
    const STRATEGY_ROUND_ROBIN = 'round_robin';
    const STRATEGY_RANDOM = 'random';
    const STRATEGY_LEAST_USED = 'least_used';
    const STRATEGY_PRIORITY = 'priority';

    protected $fillable = [
        'brand_id',
        'platform',
        'name',
        'description',
        'rotation_strategy',
        'cooldown_minutes',
        'max_posts_per_day',
        'auto_failover',
        'is_active',
    ];

    protected $casts = [
        'cooldown_minutes' => 'integer',
        'max_posts_per_day' => 'integer',
        'auto_failover' => 'boolean',
        'is_active' => 'boolean',
    ];

    public static function getStrategies(): array
    {
        return [
            self::STRATEGY_ROUND_ROBIN,
            self::STRATEGY_RANDOM,
            self::STRATEGY_LEAST_USED,
            self::STRATEGY_PRIORITY,
        ];
    }

    // Relationships
    public function brand(): BelongsTo
    {
        return $this->belongsTo(Brand::class);
    }

    public function members(): HasMany
    {
        return $this->hasMany(AccountPoolMember::class);
    }

    public function socialAccounts(): BelongsToMany
    {
        return $this->belongsToMany(SocialAccount::class, 'account_pool_members')
            ->withPivot([
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
            ])
            ->withTimestamps();
    }

    public function statusLogs(): HasMany
    {
        return $this->hasMany(AccountStatusLog::class);
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeForBrand($query, int $brandId)
    {
        return $query->where('brand_id', $brandId);
    }

    // Helpers
    public function getAvailableAccounts()
    {
        return $this->members()
            ->where('status', AccountPoolMember::STATUS_ACTIVE)
            ->where(function ($query) {
                $query->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            })
            ->whereHas('socialAccount', function ($query) {
                $query->where('is_active', true);
            })
            ->with('socialAccount')
            ->get();
    }

    public function hasAvailableAccounts(): bool
    {
        return $this->getAvailableAccounts()->isNotEmpty();
    }

    public function getActiveAccountCount(): int
    {
        return $this->members()
            ->where('status', AccountPoolMember::STATUS_ACTIVE)
            ->count();
    }
}
