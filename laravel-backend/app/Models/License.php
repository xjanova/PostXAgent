<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

/**
 * License Model - จัดการ License สำหรับ MyPostXAgent
 */
class License extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'license_key',
        'machine_id',
        'machine_fingerprint',
        'type', // demo, monthly, yearly, lifetime
        'status', // active, expired, revoked
        'activated_at',
        'expires_at',
        'last_validated_at',
        'allowed_machines',
        'metadata',
    ];

    protected $casts = [
        'activated_at' => 'datetime',
        'expires_at' => 'datetime',
        'last_validated_at' => 'datetime',
        'metadata' => 'array',
        'allowed_machines' => 'integer',
    ];

    protected $hidden = [
        'machine_fingerprint',
    ];

    /**
     * ตรวจสอบว่า License หมดอายุหรือไม่
     */
    public function isExpired(): bool
    {
        if ($this->type === 'lifetime') {
            return false;
        }

        if ($this->expires_at === null) {
            return true;
        }

        return $this->expires_at->isPast();
    }

    /**
     * ตรวจสอบว่า License ยังใช้งานได้
     */
    public function isValid(): bool
    {
        return $this->status === 'active' && !$this->isExpired();
    }

    /**
     * จำนวนวันที่เหลือ
     */
    public function daysRemaining(): int
    {
        if ($this->type === 'lifetime') {
            return 999999;
        }

        if ($this->expires_at === null) {
            return 0;
        }

        return max(0, (int) now()->diffInDays($this->expires_at, false));
    }

    /**
     * Scope สำหรับ active licenses
     */
    public function scopeActive($query)
    {
        return $query->where('status', 'active')
            ->where(function ($q) {
                $q->whereNull('expires_at')
                    ->orWhere('expires_at', '>', now())
                    ->orWhere('type', 'lifetime');
            });
    }

    /**
     * Scope สำหรับค้นหาตาม license key
     */
    public function scopeByKey($query, string $key)
    {
        return $query->where('license_key', $key);
    }

    /**
     * Scope สำหรับค้นหาตาม machine ID
     */
    public function scopeByMachine($query, string $machineId)
    {
        return $query->where('machine_id', $machineId);
    }
}
