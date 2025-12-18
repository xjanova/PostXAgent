<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

class PhoneNumber extends Model
{
    use HasFactory, SoftDeletes;

    // Status constants
    const STATUS_AVAILABLE = 'available';
    const STATUS_IN_USE = 'in_use';
    const STATUS_USED = 'used';
    const STATUS_BLOCKED = 'blocked';
    const STATUS_EXPIRED = 'expired';

    // Provider constants
    const PROVIDER_SMS_ACTIVATE = 'sms_activate';
    const PROVIDER_5SIM = '5sim';
    const PROVIDER_SMSPVA = 'smspva';
    const PROVIDER_MANUAL = 'manual';

    protected $fillable = [
        'phone_number',
        'country_code',
        'provider',
        'provider_order_id',
        'status',
        'used_for_platform',
        'used_for_account_id',
        'expires_at',
        'last_sms_received_at',
        'sms_messages',
        'cost',
        'metadata',
    ];

    protected $casts = [
        'expires_at' => 'datetime',
        'last_sms_received_at' => 'datetime',
        'sms_messages' => 'array',
        'cost' => 'decimal:4',
        'metadata' => 'array',
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_AVAILABLE,
            self::STATUS_IN_USE,
            self::STATUS_USED,
            self::STATUS_BLOCKED,
            self::STATUS_EXPIRED,
        ];
    }

    public static function getProviders(): array
    {
        return [
            self::PROVIDER_SMS_ACTIVATE,
            self::PROVIDER_5SIM,
            self::PROVIDER_SMSPVA,
            self::PROVIDER_MANUAL,
        ];
    }

    // Query Scopes
    public function scopeAvailable($query)
    {
        return $query->where('status', self::STATUS_AVAILABLE)
            ->where(function ($q) {
                $q->whereNull('expires_at')
                    ->orWhere('expires_at', '>', now());
            });
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where(function ($q) use ($platform) {
            $q->whereNull('used_for_platform')
                ->orWhere('used_for_platform', $platform);
        });
    }

    // Helpers
    public function markAsInUse(string $platform): void
    {
        $this->update([
            'status' => self::STATUS_IN_USE,
            'used_for_platform' => $platform,
        ]);
    }

    public function markAsUsed(int $accountId): void
    {
        $this->update([
            'status' => self::STATUS_USED,
            'used_for_account_id' => $accountId,
        ]);
    }

    public function markAsBlocked(): void
    {
        $this->update(['status' => self::STATUS_BLOCKED]);
    }

    public function addSmsMessage(string $message, string $from = null): void
    {
        $messages = $this->sms_messages ?? [];
        $messages[] = [
            'from' => $from,
            'message' => $message,
            'received_at' => now()->toIso8601String(),
        ];

        $this->update([
            'sms_messages' => $messages,
            'last_sms_received_at' => now(),
        ]);
    }

    public function getLatestOtp(): ?string
    {
        $messages = $this->sms_messages ?? [];
        if (empty($messages)) {
            return null;
        }

        $latestMessage = end($messages);
        $message = $latestMessage['message'] ?? '';

        // Extract OTP (4-8 digits)
        if (preg_match('/\b(\d{4,8})\b/', $message, $matches)) {
            return $matches[1];
        }

        return null;
    }
}
