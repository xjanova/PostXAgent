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

/**
 * @property int $id
 * @property string $name
 * @property string $email
 * @property string|null $phone
 * @property string|null $company_name
 * @property string $timezone
 * @property string $language
 * @property bool $is_active
 * @property \Illuminate\Support\Carbon|null $email_verified_at
 * @property string $password
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 *
 * @method static \Illuminate\Database\Eloquent\Builder|User where($column, $operator = null, $value = null, $boolean = 'and')
 * @method static \Illuminate\Database\Eloquent\Builder|User create(array $attributes = [])
 * @method static \Illuminate\Database\Eloquent\Builder|User find($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|User findOrFail($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|User first($columns = ['*'])
 */
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

    public function contentPipelines(): \Illuminate\Database\Eloquent\Relations\HasMany
    {
        return $this->hasMany(ContentPipeline::class);
    }

    public function gpuProviderAccounts(): \Illuminate\Database\Eloquent\Relations\HasMany
    {
        return $this->hasMany(GpuProviderAccount::class);
    }

    public function gpuAccountPools(): \Illuminate\Database\Eloquent\Relations\HasMany
    {
        return $this->hasMany(GpuAccountPool::class);
    }

    public function learnedWorkflows(): \Illuminate\Database\Eloquent\Relations\HasMany
    {
        return $this->hasMany(LearnedWorkflow::class);
    }

    public function seekAndPostTasks(): \Illuminate\Database\Eloquent\Relations\HasMany
    {
        return $this->hasMany(SeekAndPostTask::class);
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
                'posts_per_month' => 0,
                'accounts_limit' => 0,
                'brands_limit' => 0,
            ];
        }

        $package = $rental->rentalPackage;
        $postsLimit = $package->posts_limit ?? 0;

        return [
            'posts_limit' => $postsLimit,
            'posts_used' => $rental->posts_used ?? 0,
            'posts_remaining' => max(0, $postsLimit - ($rental->posts_used ?? 0)),
            'posts_per_month' => $postsLimit, // Alias for backward compatibility
            'accounts_limit' => $package->accounts_limit ?? 0,
            'brands_limit' => $package->brands_limit ?? 0,
        ];
    }
}
