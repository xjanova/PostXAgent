<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Support\Facades\Crypt;

class EmailAccount extends Model
{
    use HasFactory, SoftDeletes;

    // Status constants
    const STATUS_AVAILABLE = 'available';
    const STATUS_IN_USE = 'in_use';
    const STATUS_USED = 'used';
    const STATUS_BLOCKED = 'blocked';

    // Provider constants
    const PROVIDER_GMAIL = 'gmail';
    const PROVIDER_OUTLOOK = 'outlook';
    const PROVIDER_YAHOO = 'yahoo';
    const PROVIDER_TEMP_MAIL = 'temp_mail';
    const PROVIDER_CUSTOM = 'custom';

    protected $fillable = [
        'email',
        'password_encrypted',
        'provider',
        'recovery_email',
        'recovery_phone',
        'status',
        'used_for_platform',
        'used_for_account_id',
        'imap_host',
        'imap_port',
        'smtp_host',
        'smtp_port',
        'last_checked_at',
        'metadata',
    ];

    protected $casts = [
        'last_checked_at' => 'datetime',
        'metadata' => 'array',
        'imap_port' => 'integer',
        'smtp_port' => 'integer',
    ];

    protected $hidden = [
        'password_encrypted',
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_AVAILABLE,
            self::STATUS_IN_USE,
            self::STATUS_USED,
            self::STATUS_BLOCKED,
        ];
    }

    public static function getProviders(): array
    {
        return [
            self::PROVIDER_GMAIL,
            self::PROVIDER_OUTLOOK,
            self::PROVIDER_YAHOO,
            self::PROVIDER_TEMP_MAIL,
            self::PROVIDER_CUSTOM,
        ];
    }

    // Password encryption/decryption
    public function setPasswordAttribute(string $value): void
    {
        $this->attributes['password_encrypted'] = Crypt::encryptString($value);
    }

    public function getPassword(): string
    {
        return Crypt::decryptString($this->password_encrypted);
    }

    // Query Scopes
    public function scopeAvailable($query)
    {
        return $query->where('status', self::STATUS_AVAILABLE);
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
}
