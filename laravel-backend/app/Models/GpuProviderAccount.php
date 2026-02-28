<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Support\Facades\Crypt;

/**
 * @property int $id
 * @property int $user_id
 * @property string $provider
 * @property string $name
 * @property string|null $email
 * @property string $api_key
 * @property string|null $api_secret
 * @property float $credits_balance
 * @property float $credits_used_today
 * @property float|null $daily_credit_limit
 * @property float $low_balance_threshold
 * @property string $status
 * @property \Illuminate\Support\Carbon|null $cooldown_until
 * @property \Illuminate\Support\Carbon|null $last_used_at
 * @property \Illuminate\Support\Carbon|null $last_balance_check_at
 * @property int $instances_created
 * @property int $total_gpu_hours
 * @property array|null $metadata
 * @property bool $is_active
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 *
 * @mixin \Illuminate\Database\Eloquent\Builder<static>
 */
class GpuProviderAccount extends Model
{
    use HasFactory, SoftDeletes;

    // Providers
    public const PROVIDER_VAST_AI = 'vast_ai';
    public const PROVIDER_RUNPOD = 'runpod';
    public const PROVIDER_LIGHTNING_AI = 'lightning_ai';
    public const PROVIDER_KAGGLE = 'kaggle';
    public const PROVIDER_LAMBDA = 'lambda';

    // Statuses
    public const STATUS_ACTIVE = 'active';
    public const STATUS_COOLDOWN = 'cooldown';
    public const STATUS_SUSPENDED = 'suspended';
    public const STATUS_DEPLETED = 'depleted';
    public const STATUS_ERROR = 'error';

    protected $fillable = [
        'user_id',
        'provider',
        'name',
        'email',
        'api_key',
        'api_secret',
        'credits_balance',
        'credits_used_today',
        'daily_credit_limit',
        'low_balance_threshold',
        'status',
        'cooldown_until',
        'last_used_at',
        'last_balance_check_at',
        'instances_created',
        'total_gpu_hours',
        'metadata',
        'is_active',
    ];

    protected $hidden = [
        'api_key',
        'api_secret',
    ];

    protected $casts = [
        'credits_balance' => 'decimal:4',
        'credits_used_today' => 'decimal:4',
        'daily_credit_limit' => 'decimal:4',
        'low_balance_threshold' => 'decimal:4',
        'cooldown_until' => 'datetime',
        'last_used_at' => 'datetime',
        'last_balance_check_at' => 'datetime',
        'instances_created' => 'integer',
        'total_gpu_hours' => 'integer',
        'metadata' => 'array',
        'is_active' => 'boolean',
    ];

    public static function getProviders(): array
    {
        return [
            self::PROVIDER_VAST_AI,
            self::PROVIDER_RUNPOD,
            self::PROVIDER_LIGHTNING_AI,
            self::PROVIDER_KAGGLE,
            self::PROVIDER_LAMBDA,
        ];
    }

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

    // Encrypt/Decrypt API Key
    public function setApiKeyAttribute(string $value): void
    {
        $this->attributes['api_key'] = Crypt::encryptString($value);
    }

    public function getDecryptedApiKey(): string
    {
        return Crypt::decryptString($this->attributes['api_key']);
    }

    public function setApiSecretAttribute(?string $value): void
    {
        if ($value !== null) {
            $this->attributes['api_secret'] = Crypt::encryptString($value);
        } else {
            $this->attributes['api_secret'] = null;
        }
    }

    public function getDecryptedApiSecret(): ?string
    {
        if ($this->attributes['api_secret'] === null) {
            return null;
        }
        return Crypt::decryptString($this->attributes['api_secret']);
    }

    // Relationships
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function instances(): HasMany
    {
        return $this->hasMany(GpuInstance::class);
    }

    public function poolMembers(): HasMany
    {
        return $this->hasMany(GpuPoolMember::class);
    }

    public function activityLogs(): HasMany
    {
        return $this->hasMany(GpuActivityLog::class);
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('is_active', true)->where('status', self::STATUS_ACTIVE);
    }

    public function scopeForProvider($query, string $provider)
    {
        return $query->where('provider', $provider);
    }

    public function scopeWithCredits($query, float $minCredits = 0.50)
    {
        return $query->where('credits_balance', '>=', $minCredits);
    }

    public function scopeAvailable($query)
    {
        return $query->where('is_active', true)
            ->whereIn('status', [self::STATUS_ACTIVE])
            ->where(function ($q) {
                $q->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            });
    }

    // Helpers
    public function hasEnoughCredits(float $minCredits = 0.50): bool
    {
        return $this->credits_balance >= $minCredits;
    }

    public function isAvailable(): bool
    {
        if (!$this->is_active) {
            return false;
        }

        if ($this->status !== self::STATUS_ACTIVE) {
            return false;
        }

        if ($this->cooldown_until && $this->cooldown_until->isFuture()) {
            return false;
        }

        return true;
    }

    public function isLowBalance(): bool
    {
        return $this->credits_balance <= $this->low_balance_threshold;
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
            'metadata' => array_merge($this->metadata ?? [], [
                'last_error' => $error,
                'error_at' => now()->toIso8601String(),
            ]),
        ]);
    }

    public function recordUsage(float $creditsUsed): void
    {
        $this->increment('credits_used_today', $creditsUsed);
        $this->decrement('credits_balance', $creditsUsed);
        $this->update(['last_used_at' => now()]);

        if ($this->credits_balance <= 0) {
            $this->markAsDepleted();
        }
    }

    public function updateBalance(float $newBalance): void
    {
        $this->update([
            'credits_balance' => $newBalance,
            'last_balance_check_at' => now(),
        ]);

        if ($newBalance <= 0) {
            $this->markAsDepleted();
        } elseif ($this->status === self::STATUS_DEPLETED && $newBalance > 0) {
            $this->update(['status' => self::STATUS_ACTIVE]);
        }
    }

    public function getCredentials(): array
    {
        return [
            'api_key' => $this->getDecryptedApiKey(),
            'api_secret' => $this->getDecryptedApiSecret(),
            'provider' => $this->provider,
            'email' => $this->email,
        ];
    }
}
