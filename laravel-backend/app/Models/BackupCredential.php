<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Support\Facades\Crypt;

class BackupCredential extends Model
{
    use HasFactory, SoftDeletes;

    protected $table = 'backup_credentials';

    // Credential types
    const TYPE_ACCESS_TOKEN = 'access_token';
    const TYPE_REFRESH_TOKEN = 'refresh_token';
    const TYPE_API_KEY = 'api_key';
    const TYPE_API_SECRET = 'api_secret';
    const TYPE_PASSWORD = 'password';
    const TYPE_COOKIE = 'cookie';
    const TYPE_SESSION = 'session';

    protected $fillable = [
        'social_account_id',
        'credential_type',
        'encrypted_value',
        'description',
        'is_primary',
        'valid_until',
        'last_verified_at',
    ];

    protected $hidden = [
        'encrypted_value',
    ];

    protected $casts = [
        'is_primary' => 'boolean',
        'valid_until' => 'datetime',
        'last_verified_at' => 'datetime',
    ];

    public static function getCredentialTypes(): array
    {
        return [
            self::TYPE_ACCESS_TOKEN,
            self::TYPE_REFRESH_TOKEN,
            self::TYPE_API_KEY,
            self::TYPE_API_SECRET,
            self::TYPE_PASSWORD,
            self::TYPE_COOKIE,
            self::TYPE_SESSION,
        ];
    }

    // Relationships
    public function socialAccount(): BelongsTo
    {
        return $this->belongsTo(SocialAccount::class);
    }

    // Encryption helpers
    public function setValueAttribute(string $value): void
    {
        $this->attributes['encrypted_value'] = Crypt::encryptString($value);
    }

    public function getDecryptedValue(): string
    {
        return Crypt::decryptString($this->encrypted_value);
    }

    // Query Scopes
    public function scopePrimary($query)
    {
        return $query->where('is_primary', true);
    }

    public function scopeOfType($query, string $type)
    {
        return $query->where('credential_type', $type);
    }

    public function scopeValid($query)
    {
        return $query->where(function ($q) {
            $q->whereNull('valid_until')
                ->orWhere('valid_until', '>', now());
        });
    }

    public function scopeForAccount($query, int $accountId)
    {
        return $query->where('social_account_id', $accountId);
    }

    // Helpers
    public function isExpired(): bool
    {
        if (!$this->valid_until) {
            return false;
        }
        return $this->valid_until->isPast();
    }

    public function markAsVerified(): void
    {
        $this->update(['last_verified_at' => now()]);
    }

    public function makePrimary(): void
    {
        // Remove primary status from other credentials of same type
        self::where('social_account_id', $this->social_account_id)
            ->where('credential_type', $this->credential_type)
            ->where('id', '!=', $this->id)
            ->update(['is_primary' => false]);

        $this->update(['is_primary' => true]);
    }

    // Static factory
    public static function storeCredential(
        int $accountId,
        string $type,
        string $value,
        ?string $description = null,
        bool $isPrimary = false,
        ?\DateTimeInterface $validUntil = null
    ): self {
        $credential = new self([
            'social_account_id' => $accountId,
            'credential_type' => $type,
            'description' => $description,
            'is_primary' => $isPrimary,
            'valid_until' => $validUntil,
        ]);

        $credential->setValueAttribute($value);
        $credential->save();

        if ($isPrimary) {
            $credential->makePrimary();
        }

        return $credential;
    }
}
