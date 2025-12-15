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

    public function subscriptions()
    {
        return $this->hasMany(Subscription::class);
    }

    // Helpers
    public function hasActiveSubscription(): bool
    {
        return $this->subscribed('default');
    }

    public function getUsageQuota(): array
    {
        $subscription = $this->subscription('default');

        if (!$subscription) {
            return [
                'posts_per_month' => 10,
                'brands' => 1,
                'platforms' => 2,
                'ai_generations' => 50,
            ];
        }

        $plan = $subscription->stripe_price;

        return match($plan) {
            'price_starter' => [
                'posts_per_month' => 100,
                'brands' => 3,
                'platforms' => 5,
                'ai_generations' => 500,
            ],
            'price_professional' => [
                'posts_per_month' => 500,
                'brands' => 10,
                'platforms' => 9,
                'ai_generations' => 2000,
            ],
            'price_enterprise' => [
                'posts_per_month' => -1, // Unlimited
                'brands' => -1,
                'platforms' => 9,
                'ai_generations' => -1,
            ],
            default => [
                'posts_per_month' => 10,
                'brands' => 1,
                'platforms' => 2,
                'ai_generations' => 50,
            ],
        };
    }
}
