<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

class SocialAccount extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'brand_id',
        'platform',
        'platform_user_id',
        'platform_username',
        'display_name',
        'access_token',
        'refresh_token',
        'token_expires_at',
        'profile_url',
        'avatar_url',
        'metadata',
        'is_active',
    ];

    protected $hidden = [
        'access_token',
        'refresh_token',
    ];

    protected $casts = [
        'token_expires_at' => 'datetime',
        'metadata' => 'array',
        'is_active' => 'boolean',
    ];

    // Constants for platforms
    const PLATFORM_FACEBOOK = 'facebook';
    const PLATFORM_INSTAGRAM = 'instagram';
    const PLATFORM_TIKTOK = 'tiktok';
    const PLATFORM_TWITTER = 'twitter';
    const PLATFORM_LINE = 'line';
    const PLATFORM_YOUTUBE = 'youtube';
    const PLATFORM_THREADS = 'threads';
    const PLATFORM_LINKEDIN = 'linkedin';
    const PLATFORM_PINTEREST = 'pinterest';

    public static function getPlatforms(): array
    {
        return [
            self::PLATFORM_FACEBOOK,
            self::PLATFORM_INSTAGRAM,
            self::PLATFORM_TIKTOK,
            self::PLATFORM_TWITTER,
            self::PLATFORM_LINE,
            self::PLATFORM_YOUTUBE,
            self::PLATFORM_THREADS,
            self::PLATFORM_LINKEDIN,
            self::PLATFORM_PINTEREST,
        ];
    }

    // Relationships
    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function brand()
    {
        return $this->belongsTo(Brand::class);
    }

    public function posts()
    {
        return $this->hasMany(Post::class);
    }

    // Helpers
    public function isTokenExpired(): bool
    {
        if (!$this->token_expires_at) {
            return false;
        }
        return $this->token_expires_at->isPast();
    }

    public function needsRefresh(): bool
    {
        if (!$this->token_expires_at) {
            return false;
        }
        return $this->token_expires_at->subHour()->isPast();
    }

    public function getCredentials(): array
    {
        return [
            'access_token' => $this->access_token,
            'refresh_token' => $this->refresh_token,
            'platform_user_id' => $this->platform_user_id,
            ...$this->metadata ?? [],
        ];
    }
}
