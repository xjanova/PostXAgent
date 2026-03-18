<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * @property int $id
 * @property int|null $gpu_provider_account_id
 * @property int|null $gpu_account_pool_id
 * @property int|null $gpu_instance_id
 * @property int $user_id
 * @property string|null $provider
 * @property string $event_type
 * @property string|null $old_status
 * @property string|null $new_status
 * @property string|null $message
 * @property array|null $metadata
 * @property float|null $credits_before
 * @property float|null $credits_after
 * @property string|null $triggered_by
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 *
 * @property-read GpuProviderAccount|null $providerAccount
 * @property-read GpuAccountPool|null $accountPool
 * @property-read GpuInstance|null $instance
 *
 * @method static \Illuminate\Database\Eloquent\Builder|static recent(int $hours = 24)
 * @method static \Illuminate\Database\Eloquent\Builder|static forAccount(int $accountId)
 * @method static \Illuminate\Database\Eloquent\Builder|static forPool(int $poolId)
 *
 * @mixin \Illuminate\Database\Eloquent\Builder<static>
 */
class GpuActivityLog extends Model
{
    use HasFactory;

    // Event Types
    public const EVENT_ACCOUNT_CREATED = 'account_created';
    public const EVENT_ACCOUNT_UPDATED = 'account_updated';
    public const EVENT_ACCOUNT_SUSPENDED = 'account_suspended';
    public const EVENT_ACCOUNT_RECOVERED = 'account_recovered';
    public const EVENT_BALANCE_CHECKED = 'balance_checked';
    public const EVENT_BALANCE_LOW = 'balance_low';
    public const EVENT_BALANCE_DEPLETED = 'balance_depleted';
    public const EVENT_INSTANCE_CREATED = 'instance_created';
    public const EVENT_INSTANCE_STARTED = 'instance_started';
    public const EVENT_INSTANCE_STOPPED = 'instance_stopped';
    public const EVENT_INSTANCE_TERMINATED = 'instance_terminated';
    public const EVENT_INSTANCE_ERROR = 'instance_error';
    public const EVENT_SETUP_STARTED = 'setup_started';
    public const EVENT_SETUP_COMPLETED = 'setup_completed';
    public const EVENT_SETUP_FAILED = 'setup_failed';
    public const EVENT_ROTATION_TRIGGERED = 'rotation_triggered';
    public const EVENT_FAILOVER_TRIGGERED = 'failover_triggered';
    public const EVENT_API_ERROR = 'api_error';
    public const EVENT_QUOTA_EXCEEDED = 'quota_exceeded';

    // Triggered By
    public const TRIGGERED_BY_SYSTEM = 'system';
    public const TRIGGERED_BY_USER = 'user';
    public const TRIGGERED_BY_API = 'api';
    public const TRIGGERED_BY_SCHEDULER = 'scheduler';

    protected $table = 'gpu_activity_logs';

    protected $fillable = [
        'gpu_provider_account_id',
        'gpu_account_pool_id',
        'gpu_instance_id',
        'user_id',
        'provider',
        'event_type',
        'old_status',
        'new_status',
        'message',
        'metadata',
        'credits_before',
        'credits_after',
        'triggered_by',
    ];

    protected $casts = [
        'metadata' => 'array',
        'credits_before' => 'decimal:4',
        'credits_after' => 'decimal:4',
    ];

    // Relationships
    public function providerAccount(): BelongsTo
    {
        return $this->belongsTo(GpuProviderAccount::class, 'gpu_provider_account_id');
    }

    public function accountPool(): BelongsTo
    {
        return $this->belongsTo(GpuAccountPool::class, 'gpu_account_pool_id');
    }

    public function instance(): BelongsTo
    {
        return $this->belongsTo(GpuInstance::class, 'gpu_instance_id');
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    // Query Scopes
    public function scopeRecent($query, int $hours = 24)
    {
        return $query->where('created_at', '>=', now()->subHours($hours));
    }

    public function scopeForAccount($query, int $accountId)
    {
        return $query->where('gpu_provider_account_id', $accountId);
    }

    public function scopeForPool($query, int $poolId)
    {
        return $query->where('gpu_account_pool_id', $poolId);
    }

    public function scopeForInstance($query, int $instanceId)
    {
        return $query->where('gpu_instance_id', $instanceId);
    }

    public function scopeOfType($query, string $eventType)
    {
        return $query->where('event_type', $eventType);
    }

    public function scopeErrors($query)
    {
        return $query->whereIn('event_type', [
            self::EVENT_API_ERROR,
            self::EVENT_INSTANCE_ERROR,
            self::EVENT_SETUP_FAILED,
            self::EVENT_QUOTA_EXCEEDED,
        ]);
    }

    // Static Factory Methods
    public static function logAccountCreated(
        GpuProviderAccount $account,
        string $triggeredBy = self::TRIGGERED_BY_USER
    ): self {
        return self::create([
            'gpu_provider_account_id' => $account->id,
            'user_id' => $account->user_id,
            'provider' => $account->provider,
            'event_type' => self::EVENT_ACCOUNT_CREATED,
            'new_status' => $account->status,
            'message' => "GPU provider account '{$account->name}' created",
            'credits_after' => $account->credits_balance,
            'triggered_by' => $triggeredBy,
        ]);
    }

    public static function logBalanceCheck(
        GpuProviderAccount $account,
        float|string $oldBalance,
        float|string $newBalance
    ): self {
        $oldBalance = (float) $oldBalance;
        $newBalance = (float) $newBalance;
        return self::create([
            'gpu_provider_account_id' => $account->id,
            'user_id' => $account->user_id,
            'provider' => $account->provider,
            'event_type' => self::EVENT_BALANCE_CHECKED,
            'message' => "Balance checked: \${$oldBalance} -> \${$newBalance}",
            'credits_before' => $oldBalance,
            'credits_after' => $newBalance,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logBalanceLow(GpuProviderAccount $account): self
    {
        return self::create([
            'gpu_provider_account_id' => $account->id,
            'user_id' => $account->user_id,
            'provider' => $account->provider,
            'event_type' => self::EVENT_BALANCE_LOW,
            'message' => "Low balance warning: \${$account->credits_balance}",
            'credits_after' => $account->credits_balance,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logBalanceDepleted(GpuProviderAccount $account): self
    {
        return self::create([
            'gpu_provider_account_id' => $account->id,
            'user_id' => $account->user_id,
            'provider' => $account->provider,
            'event_type' => self::EVENT_BALANCE_DEPLETED,
            'old_status' => $account->getOriginal('status'),
            'new_status' => GpuProviderAccount::STATUS_DEPLETED,
            'message' => "Account balance depleted",
            'credits_after' => $account->credits_balance,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logInstanceCreated(
        GpuInstance $instance,
        string $triggeredBy = self::TRIGGERED_BY_SYSTEM
    ): self {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_INSTANCE_CREATED,
            'new_status' => $instance->status,
            'message' => "Instance '{$instance->instance_id}' created ({$instance->gpu_type})",
            'metadata' => [
                'gpu_type' => $instance->gpu_type,
                'gpu_count' => $instance->gpu_count,
                'price_per_hour' => $instance->price_per_hour,
                'region' => $instance->region,
            ],
            'triggered_by' => $triggeredBy,
        ]);
    }

    public static function logInstanceStarted(GpuInstance $instance): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_INSTANCE_STARTED,
            'old_status' => $instance->getOriginal('status'),
            'new_status' => GpuInstance::STATUS_RUNNING,
            'message' => "Instance '{$instance->instance_id}' started at {$instance->ip_address}",
            'metadata' => [
                'ip_address' => $instance->ip_address,
                'ssh_port' => $instance->ssh_port,
            ],
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logInstanceStopped(GpuInstance $instance): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_INSTANCE_STOPPED,
            'old_status' => $instance->getOriginal('status'),
            'new_status' => GpuInstance::STATUS_STOPPED,
            'message' => "Instance '{$instance->instance_id}' stopped after {$instance->runtime_minutes} minutes",
            'metadata' => [
                'runtime_minutes' => $instance->runtime_minutes,
                'total_cost' => $instance->total_cost,
            ],
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logInstanceTerminated(GpuInstance $instance): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_INSTANCE_TERMINATED,
            'old_status' => $instance->getOriginal('status'),
            'new_status' => GpuInstance::STATUS_TERMINATED,
            'message' => "Instance '{$instance->instance_id}' terminated",
            'metadata' => [
                'runtime_minutes' => $instance->runtime_minutes,
                'total_cost' => $instance->total_cost,
            ],
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logInstanceError(GpuInstance $instance, string $error): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_INSTANCE_ERROR,
            'old_status' => $instance->getOriginal('status'),
            'new_status' => GpuInstance::STATUS_ERROR,
            'message' => $error,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logSetupStarted(GpuInstance $instance): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_SETUP_STARTED,
            'message' => "Setup started for instance '{$instance->instance_id}'",
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logSetupCompleted(GpuInstance $instance): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_SETUP_COMPLETED,
            'message' => "Setup completed for instance '{$instance->instance_id}'",
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logSetupFailed(GpuInstance $instance, string $error): self
    {
        return self::create([
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'gpu_instance_id' => $instance->id,
            'user_id' => $instance->user_id,
            'provider' => $instance->provider,
            'event_type' => self::EVENT_SETUP_FAILED,
            'message' => "Setup failed: {$error}",
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logRotationTriggered(
        GpuAccountPool $pool,
        GpuProviderAccount $fromAccount,
        GpuProviderAccount $toAccount,
        string $reason
    ): self {
        return self::create([
            'gpu_provider_account_id' => $toAccount->id,
            'gpu_account_pool_id' => $pool->id,
            'user_id' => $pool->user_id,
            'provider' => $toAccount->provider,
            'event_type' => self::EVENT_ROTATION_TRIGGERED,
            'message' => "Rotation from '{$fromAccount->name}' to '{$toAccount->name}': {$reason}",
            'metadata' => [
                'from_account_id' => $fromAccount->id,
                'to_account_id' => $toAccount->id,
                'reason' => $reason,
            ],
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logFailoverTriggered(
        GpuAccountPool $pool,
        GpuProviderAccount $failedAccount,
        ?GpuProviderAccount $newAccount,
        string $reason
    ): self {
        return self::create([
            'gpu_provider_account_id' => $newAccount?->id,
            'gpu_account_pool_id' => $pool->id,
            'user_id' => $pool->user_id,
            'provider' => $newAccount !== null ? $newAccount->provider : $failedAccount->provider,
            'event_type' => self::EVENT_FAILOVER_TRIGGERED,
            'message' => $newAccount
                ? "Failover from '{$failedAccount->name}' to '{$newAccount->name}': {$reason}"
                : "Failover from '{$failedAccount->name}' failed - no available accounts: {$reason}",
            'metadata' => [
                'failed_account_id' => $failedAccount->id,
                'new_account_id' => $newAccount?->id,
                'reason' => $reason,
            ],
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }

    public static function logApiError(
        GpuProviderAccount $account,
        string $error,
        ?array $metadata = null
    ): self {
        return self::create([
            'gpu_provider_account_id' => $account->id,
            'user_id' => $account->user_id,
            'provider' => $account->provider,
            'event_type' => self::EVENT_API_ERROR,
            'message' => $error,
            'metadata' => $metadata,
            'triggered_by' => self::TRIGGERED_BY_SYSTEM,
        ]);
    }
}
