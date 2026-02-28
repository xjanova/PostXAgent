<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * @property int $id
 * @property int $gpu_provider_account_id
 * @property int|null $gpu_account_pool_id
 * @property int $user_id
 * @property string $provider
 * @property string $instance_id
 * @property string|null $instance_name
 * @property string $status
 * @property string|null $gpu_type
 * @property int $gpu_count
 * @property int|null $cpu_cores
 * @property int|null $ram_gb
 * @property int|null $disk_gb
 * @property string|null $region
 * @property string|null $ip_address
 * @property int|null $ssh_port
 * @property float|null $price_per_hour
 * @property float $total_cost
 * @property string|null $docker_image
 * @property array|null $environment_vars
 * @property array|null $setup_script
 * @property string $setup_status
 * @property string|null $setup_log
 * @property \Illuminate\Support\Carbon|null $started_at
 * @property \Illuminate\Support\Carbon|null $stopped_at
 * @property int $runtime_minutes
 * @property array|null $metadata
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 * @property-read GpuProviderAccount $providerAccount
 * @property-read GpuAccountPool|null $accountPool
 * @property-read User $user
 *
 * @mixin \Illuminate\Database\Eloquent\Builder<static>
 */
class GpuInstance extends Model
{
    use HasFactory, SoftDeletes;

    // Instance Statuses
    public const STATUS_PENDING = 'pending';
    public const STATUS_STARTING = 'starting';
    public const STATUS_RUNNING = 'running';
    public const STATUS_STOPPING = 'stopping';
    public const STATUS_STOPPED = 'stopped';
    public const STATUS_TERMINATED = 'terminated';
    public const STATUS_ERROR = 'error';

    // Setup Statuses
    public const SETUP_PENDING = 'pending';
    public const SETUP_RUNNING = 'running';
    public const SETUP_COMPLETED = 'completed';
    public const SETUP_FAILED = 'failed';

    protected $fillable = [
        'gpu_provider_account_id',
        'gpu_account_pool_id',
        'user_id',
        'provider',
        'instance_id',
        'instance_name',
        'status',
        'gpu_type',
        'gpu_count',
        'cpu_cores',
        'ram_gb',
        'disk_gb',
        'region',
        'ip_address',
        'ssh_port',
        'price_per_hour',
        'total_cost',
        'docker_image',
        'environment_vars',
        'setup_script',
        'setup_status',
        'setup_log',
        'started_at',
        'stopped_at',
        'runtime_minutes',
        'metadata',
    ];

    protected $casts = [
        'gpu_count' => 'integer',
        'cpu_cores' => 'integer',
        'ram_gb' => 'integer',
        'disk_gb' => 'integer',
        'ssh_port' => 'integer',
        'price_per_hour' => 'decimal:4',
        'total_cost' => 'decimal:4',
        'environment_vars' => 'array',
        'setup_script' => 'array',
        'started_at' => 'datetime',
        'stopped_at' => 'datetime',
        'runtime_minutes' => 'integer',
        'metadata' => 'array',
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_PENDING,
            self::STATUS_STARTING,
            self::STATUS_RUNNING,
            self::STATUS_STOPPING,
            self::STATUS_STOPPED,
            self::STATUS_TERMINATED,
            self::STATUS_ERROR,
        ];
    }

    public static function getSetupStatuses(): array
    {
        return [
            self::SETUP_PENDING,
            self::SETUP_RUNNING,
            self::SETUP_COMPLETED,
            self::SETUP_FAILED,
        ];
    }

    // Relationships
    public function providerAccount(): BelongsTo
    {
        return $this->belongsTo(GpuProviderAccount::class, 'gpu_provider_account_id');
    }

    public function accountPool(): BelongsTo
    {
        return $this->belongsTo(GpuAccountPool::class, 'gpu_account_pool_id');
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function activityLogs(): HasMany
    {
        return $this->hasMany(GpuActivityLog::class);
    }

    // Query Scopes
    public function scopeRunning($query)
    {
        return $query->where('status', self::STATUS_RUNNING);
    }

    public function scopeActive($query)
    {
        return $query->whereIn('status', [
            self::STATUS_PENDING,
            self::STATUS_STARTING,
            self::STATUS_RUNNING,
        ]);
    }

    public function scopeForProvider($query, string $provider)
    {
        return $query->where('provider', $provider);
    }

    // Helpers
    public function isRunning(): bool
    {
        return $this->status === self::STATUS_RUNNING;
    }

    public function isActive(): bool
    {
        return in_array($this->status, [
            self::STATUS_PENDING,
            self::STATUS_STARTING,
            self::STATUS_RUNNING,
        ]);
    }

    public function canStart(): bool
    {
        return in_array($this->status, [
            self::STATUS_STOPPED,
            self::STATUS_ERROR,
        ]);
    }

    public function canStop(): bool
    {
        return in_array($this->status, [
            self::STATUS_RUNNING,
            self::STATUS_STARTING,
        ]);
    }

    public function markAsStarting(): void
    {
        $this->update([
            'status' => self::STATUS_STARTING,
        ]);
    }

    public function markAsRunning(?string $ipAddress = null, ?int $sshPort = null): void
    {
        $this->update([
            'status' => self::STATUS_RUNNING,
            'started_at' => now(),
            'ip_address' => $ipAddress ?? $this->ip_address,
            'ssh_port' => $sshPort ?? $this->ssh_port,
        ]);
    }

    public function markAsStopping(): void
    {
        $this->update([
            'status' => self::STATUS_STOPPING,
        ]);
    }

    public function markAsStopped(): void
    {
        $this->update([
            'status' => self::STATUS_STOPPED,
            'stopped_at' => now(),
        ]);
        $this->calculateRuntime();
    }

    public function markAsTerminated(): void
    {
        $this->update([
            'status' => self::STATUS_TERMINATED,
            'stopped_at' => now(),
        ]);
        $this->calculateRuntime();
    }

    public function markAsError(string $error): void
    {
        $this->update([
            'status' => self::STATUS_ERROR,
            'setup_log' => ($this->setup_log ?? '') . "\n[ERROR] " . $error,
            'metadata' => array_merge($this->metadata ?? [], [
                'last_error' => $error,
                'error_at' => now()->toIso8601String(),
            ]),
        ]);
    }

    public function updateSetupStatus(string $status, ?string $log = null): void
    {
        $updates = ['setup_status' => $status];
        if ($log !== null) {
            $updates['setup_log'] = ($this->setup_log ?? '') . "\n" . $log;
        }
        $this->update($updates);
    }

    public function appendSetupLog(string $log): void
    {
        $this->update([
            'setup_log' => ($this->setup_log ?? '') . "\n" . $log,
        ]);
    }

    public function calculateRuntime(): void
    {
        if ($this->started_at) {
            $endTime = $this->stopped_at ?? now();
            $minutes = $this->started_at->diffInMinutes($endTime);
            $cost = ($minutes / 60) * ($this->price_per_hour ?? 0);

            $this->update([
                'runtime_minutes' => $minutes,
                'total_cost' => $cost,
            ]);
        }
    }

    public function getCurrentCost(): float
    {
        if (!$this->started_at || !$this->price_per_hour) {
            return 0;
        }

        $endTime = $this->stopped_at ?? now();
        $hours = $this->started_at->diffInMinutes($endTime) / 60;
        return round($hours * $this->price_per_hour, 4);
    }

    public function getSshCommand(): ?string
    {
        if (!$this->ip_address || !$this->ssh_port) {
            return null;
        }
        return "ssh -p {$this->ssh_port} root@{$this->ip_address}";
    }

    public function getConnectionInfo(): array
    {
        return [
            'ip_address' => $this->ip_address,
            'ssh_port' => $this->ssh_port,
            'ssh_command' => $this->getSshCommand(),
            'provider' => $this->provider,
            'instance_id' => $this->instance_id,
            'gpu_type' => $this->gpu_type,
            'status' => $this->status,
        ];
    }
}
