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

    /**
     * Get user's rental subscriptions (Thai market)
     */
    public function rentals()
    {
        return $this->hasMany(UserRental::class);
    }

    /**
     * Get user's payments
     */
    public function payments()
    {
        return $this->hasMany(Payment::class);
    }

    /**
     * Get user's invoices
     */
    public function invoices()
    {
        return $this->hasMany(Invoice::class);
    }

    /**
     * Get active rental
     */
    public function activeRental()
    {
        return $this->rentals()
            ->where('status', UserRental::STATUS_ACTIVE)
            ->where('expires_at', '>', now())
            ->first();
    }

    /**
     * Check if user has active rental
     */
    public function hasActiveRental(): bool
    {
        return $this->activeRental() !== null;
    }

    // Helpers
    public function hasActiveSubscription(): bool
    {
        return $this->hasActiveRental();
    }

    /**
     * Get usage quota from active rental package
     */
    public function getUsageQuota(): array
    {
        $rental = $this->activeRental();

        if (!$rental || !$rental->rentalPackage) {
            // Default quota for users without active rental
            return [
                'posts_per_month' => 10,
                'brands' => 1,
                'platforms' => 2,
                'ai_generations' => 50,
            ];
        }

        $package = $rental->rentalPackage;

        return [
            'posts_per_month' => $package->posts_limit,
            'brands' => $package->brands_limit,
            'platforms' => $package->platforms_limit,
            'ai_generations' => $package->ai_generations_limit,
        ];
    }
}
