<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * @property int $id
 * @property string $name
 * @property string $provider
 * @property string $service_type
 * @property string $rotation_strategy
 * @property int $cooldown_seconds
 * @property int $max_requests_per_day
 * @property bool $auto_failover
 * @property bool $is_active
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 *
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool where($column, $operator = null, $value = null, $boolean = 'and')
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool create(array $attributes = [])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool find($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool findOrFail($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool first($columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool active()
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool forProvider(string $provider)
 * @method static \Illuminate\Database\Eloquent\Builder|ApiKeyPool forServiceType(string $serviceType)
 */
class ApiKeyPool extends Model
{
    use HasFactory, SoftDeletes;

    // Rotation strategies
    const STRATEGY_ROUND_ROBIN = 'round_robin';
    const STRATEGY_RANDOM = 'random';
    const STRATEGY_LEAST_USED = 'least_used';
    const STRATEGY_PRIORITY = 'priority';

    // Providers
    const PROVIDER_GROQ = 'groq';
    const PROVIDER_GEMINI = 'gemini';
    const PROVIDER_MISTRAL = 'mistral';
    const PROVIDER_GOOGLE_TTS = 'google_tts';
    const PROVIDER_EDGE_TTS = 'edge_tts';
    const PROVIDER_HUGGINGFACE = 'huggingface';

    // Service types
    const SERVICE_TEXT = 'text';
    const SERVICE_IMAGE = 'image';
    const SERVICE_TTS = 'tts';
    const SERVICE_VIDEO = 'video';
    const SERVICE_MUSIC = 'music';

    protected $fillable = [
        'name',
        'provider',
        'service_type',
        'rotation_strategy',
        'cooldown_seconds',
        'max_requests_per_day',
        'auto_failover',
        'is_active',
    ];

    protected $casts = [
        'cooldown_seconds' => 'integer',
        'max_requests_per_day' => 'integer',
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

    public static function getProviders(): array
    {
        return [
            self::PROVIDER_GROQ,
            self::PROVIDER_GEMINI,
            self::PROVIDER_MISTRAL,
            self::PROVIDER_GOOGLE_TTS,
            self::PROVIDER_EDGE_TTS,
            self::PROVIDER_HUGGINGFACE,
        ];
    }

    // Relationships
    public function members(): HasMany
    {
        return $this->hasMany(ApiKeyPoolMember::class);
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeForProvider($query, string $provider)
    {
        return $query->where('provider', $provider);
    }

    public function scopeForServiceType($query, string $serviceType)
    {
        return $query->where('service_type', $serviceType);
    }

    // Helpers
    public function getAvailableKeys(): \Illuminate\Database\Eloquent\Collection
    {
        return $this->members()
            ->where('status', ApiKeyPoolMember::STATUS_ACTIVE)
            ->where(function ($query) {
                $query->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            })
            ->where(function ($query) {
                $query->where('daily_limit', 0)
                    ->orWhereColumn('requests_today', '<', 'daily_limit');
            })
            ->get();
    }

    public function hasAvailableKeys(): bool
    {
        return $this->getAvailableKeys()->isNotEmpty();
    }

    public function getActiveKeyCount(): int
    {
        return $this->members()
            ->where('status', ApiKeyPoolMember::STATUS_ACTIVE)
            ->count();
    }

    public function getTotalRequestsToday(): int
    {
        return (int) $this->members()->sum('requests_today');
    }
}
