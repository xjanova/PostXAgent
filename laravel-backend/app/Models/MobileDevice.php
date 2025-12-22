<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * Mobile Device Model
 *
 * อุปกรณ์มือถือที่เชื่อมต่อกับระบบเพื่อรับ SMS Notification
 *
 * @property int $id
 * @property string $device_id
 * @property string $device_name
 * @property string $platform
 * @property string $app_version
 * @property bool $is_online
 * @property \Carbon\Carbon|null $connected_at
 * @property \Carbon\Carbon|null $last_sync_at
 * @property \Carbon\Carbon|null $last_heartbeat
 * @property int|null $battery_level
 * @property string|null $network_type
 * @property bool $sms_monitoring_enabled
 * @property bool $auto_approve_enabled
 * @property int $pending_payments
 * @property float $total_payments_today
 */
class MobileDevice extends Model
{
    protected $fillable = [
        'device_id',
        'device_name',
        'platform',
        'app_version',
        'is_online',
        'connected_at',
        'last_sync_at',
        'last_heartbeat',
        'battery_level',
        'network_type',
        'sms_monitoring_enabled',
        'auto_approve_enabled',
        'pending_payments',
        'total_payments_today',
    ];

    protected $casts = [
        'is_online' => 'boolean',
        'connected_at' => 'datetime',
        'last_sync_at' => 'datetime',
        'last_heartbeat' => 'datetime',
        'battery_level' => 'integer',
        'sms_monitoring_enabled' => 'boolean',
        'auto_approve_enabled' => 'boolean',
        'pending_payments' => 'integer',
        'total_payments_today' => 'decimal:2',
    ];

    /**
     * Get payments received from this device
     */
    public function payments(): HasMany
    {
        return $this->hasMany(SmsPayment::class, 'device_id', 'device_id');
    }

    /**
     * Get sync status
     */
    public function getSyncStatusAttribute(): string
    {
        if (!$this->is_online) {
            return 'offline';
        }

        if (!$this->last_heartbeat) {
            return 'connecting';
        }

        $timeSinceHeartbeat = now()->diffInSeconds($this->last_heartbeat);

        if ($timeSinceHeartbeat < 30) {
            return 'synced';
        }

        if ($timeSinceHeartbeat < 300) { // 5 minutes
            return 'syncing';
        }

        return 'disconnected';
    }

    /**
     * Append sync_status to array/json
     */
    protected $appends = ['sync_status'];

    /**
     * Scope for online devices
     */
    public function scopeOnline($query)
    {
        return $query->where('is_online', true)
            ->where('last_heartbeat', '>=', now()->subMinutes(5));
    }

    /**
     * Scope for devices with SMS monitoring enabled
     */
    public function scopeWithSmsMonitoring($query)
    {
        return $query->where('sms_monitoring_enabled', true);
    }
}
