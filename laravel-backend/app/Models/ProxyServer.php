<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Support\Facades\Crypt;

class ProxyServer extends Model
{
    use HasFactory, SoftDeletes;

    // Status constants
    const STATUS_ACTIVE = 'active';
    const STATUS_INACTIVE = 'inactive';
    const STATUS_BANNED = 'banned';
    const STATUS_SLOW = 'slow';

    // Type constants
    const TYPE_HTTP = 'http';
    const TYPE_HTTPS = 'https';
    const TYPE_SOCKS4 = 'socks4';
    const TYPE_SOCKS5 = 'socks5';

    // Provider constants
    const PROVIDER_BRIGHT_DATA = 'bright_data';
    const PROVIDER_OXYLABS = 'oxylabs';
    const PROVIDER_SMARTPROXY = 'smartproxy';
    const PROVIDER_RESIDENTIAL = 'residential';
    const PROVIDER_DATACENTER = 'datacenter';
    const PROVIDER_CUSTOM = 'custom';

    protected $fillable = [
        'host',
        'port',
        'type',
        'username',
        'password_encrypted',
        'provider',
        'country_code',
        'city',
        'status',
        'last_used_at',
        'last_checked_at',
        'response_time_ms',
        'success_count',
        'failure_count',
        'banned_platforms',
        'metadata',
    ];

    protected $casts = [
        'port' => 'integer',
        'last_used_at' => 'datetime',
        'last_checked_at' => 'datetime',
        'response_time_ms' => 'integer',
        'success_count' => 'integer',
        'failure_count' => 'integer',
        'banned_platforms' => 'array',
        'metadata' => 'array',
    ];

    protected $hidden = [
        'password_encrypted',
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_ACTIVE,
            self::STATUS_INACTIVE,
            self::STATUS_BANNED,
            self::STATUS_SLOW,
        ];
    }

    public static function getTypes(): array
    {
        return [
            self::TYPE_HTTP,
            self::TYPE_HTTPS,
            self::TYPE_SOCKS4,
            self::TYPE_SOCKS5,
        ];
    }

    // Password encryption
    public function setPasswordAttribute(string $value): void
    {
        $this->attributes['password_encrypted'] = Crypt::encryptString($value);
    }

    public function getPassword(): ?string
    {
        if (empty($this->password_encrypted)) {
            return null;
        }
        return Crypt::decryptString($this->password_encrypted);
    }

    // Get proxy URL
    public function getProxyUrl(): string
    {
        $auth = '';
        if ($this->username) {
            $password = $this->getPassword();
            $auth = $password ? "{$this->username}:{$password}@" : "{$this->username}@";
        }

        return "{$this->type}://{$auth}{$this->host}:{$this->port}";
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('status', self::STATUS_ACTIVE);
    }

    public function scopeForCountry($query, string $countryCode)
    {
        return $query->where('country_code', $countryCode);
    }

    public function scopeNotBannedFor($query, string $platform)
    {
        return $query->where(function ($q) use ($platform) {
            $q->whereNull('banned_platforms')
                ->orWhereJsonDoesntContain('banned_platforms', $platform);
        });
    }

    public function scopeFastest($query)
    {
        return $query->orderBy('response_time_ms', 'asc');
    }

    public function scopeLeastUsed($query)
    {
        return $query->orderBy('last_used_at', 'asc');
    }

    // Helpers
    public function recordSuccess(int $responseTimeMs): void
    {
        $this->increment('success_count');
        $this->update([
            'last_used_at' => now(),
            'response_time_ms' => $responseTimeMs,
            'status' => self::STATUS_ACTIVE,
        ]);
    }

    public function recordFailure(): void
    {
        $this->increment('failure_count');

        // Mark as slow if too many failures
        if ($this->failure_count > 10 && $this->getSuccessRate() < 50) {
            $this->update(['status' => self::STATUS_SLOW]);
        }
    }

    public function markAsBannedFor(string $platform): void
    {
        $bannedPlatforms = $this->banned_platforms ?? [];
        if (!in_array($platform, $bannedPlatforms)) {
            $bannedPlatforms[] = $platform;
            $this->update(['banned_platforms' => $bannedPlatforms]);
        }
    }

    public function getSuccessRate(): float
    {
        $total = $this->success_count + $this->failure_count;
        if ($total === 0) {
            return 100.0;
        }
        return round(($this->success_count / $total) * 100, 2);
    }

    public function isBannedFor(string $platform): bool
    {
        return in_array($platform, $this->banned_platforms ?? []);
    }
}
