<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Foundation\Auth\User as Authenticatable;
use Illuminate\Notifications\Notifiable;
use Laravel\Sanctum\HasApiTokens;
use Laravel\Cashier\Billable;
use Spatie\Permission\Traits\HasRoles;
use Spatie\Activitylog\Traits\LogsActivity;
use Spatie\Activitylog\LogOptions;

class User extends Authenticatable
{
    use HasApiTokens, HasFactory, Notifiable, Billable, HasRoles, LogsActivity;

    protected $fillable = [
        'name',
        'email',
        'password',
        'phone',
        'company_name',
        'timezone',
        'language',
        'is_active',
    ];

    protected $hidden = [
        'password',
        'remember_token',
    ];

    protected $casts = [
        'email_verified_at' => 'datetime',
        'password' => 'hashed',
        'is_active' => 'boolean',
    ];

    public function getActivitylogOptions(): LogOptions
    {
        return LogOptions::defaults()
            ->logOnly(['name', 'email', 'is_active'])
            ->logOnlyDirty();
    }

    // Relationships
    public function brands()
    {
        return $this->hasMany(Brand::class);
    }

    public function socialAccounts()
    {
        return $this->hasMany(SocialAccount::class);
    }

    public function campaigns()
    {
        return $this->hasMany(Campaign::class);
    }

    public function posts()
    {
        return $this->hasMany(Post::class);
    }

    public function rentals()
    {
        return $this->hasMany(UserRental::class);
    }

    // Helpers
    public function hasActiveSubscription(): bool
    {
        // License validation is handled by xmanstudio external API
        // See: https://github.com/xjanova/xmanstudio
        return $this->subscribed('default');
    }

    public function activeRental(): ?UserRental
    {
        return $this->rentals()
            ->where('status', 'active')
            ->where('starts_at', '<=', now())
            ->where('expires_at', '>=', now())
            ->first();
    }

    public function getUsageQuota(): array
    {
        $rental = $this->activeRental();
        if (!$rental) {
            return [
                'posts_limit' => 0,
                'posts_used' => 0,
                'posts_remaining' => 0,
                'accounts_limit' => 0,
                'brands_limit' => 0,
            ];
        }

        $package = $rental->rentalPackage;

        return [
            'posts_limit' => $package->posts_limit ?? 0,
            'posts_used' => $rental->posts_used ?? 0,
            'posts_remaining' => max(0, ($package->posts_limit ?? 0) - ($rental->posts_used ?? 0)),
            'accounts_limit' => $package->accounts_limit ?? 0,
            'brands_limit' => $package->brands_limit ?? 0,
        ];
    }
}
