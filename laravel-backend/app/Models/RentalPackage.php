<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Carbon\Carbon;

class RentalPackage extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'name',
        'name_th',
        'description',
        'description_th',
        'duration_type',
        'duration_value',
        'price',
        'original_price',
        'currency',
        'posts_limit',
        'brands_limit',
        'platforms_limit',
        'ai_generations_limit',
        'accounts_per_platform',
        'scheduled_posts_limit',
        'team_members_limit',
        'features',
        'included_platforms',
        'is_active',
        'is_featured',
        'is_popular',
        'sort_order',
        'has_trial',
        'trial_days',
    ];

    protected $casts = [
        'price' => 'decimal:2',
        'original_price' => 'decimal:2',
        'features' => 'array',
        'included_platforms' => 'array',
        'is_active' => 'boolean',
        'is_featured' => 'boolean',
        'is_popular' => 'boolean',
        'has_trial' => 'boolean',
    ];

    protected $appends = ['display_name', 'duration_text', 'discount_percentage'];

    /**
     * Get user rentals for this package
     */
    public function userRentals(): HasMany
    {
        return $this->hasMany(UserRental::class);
    }

    /**
     * Scope for active packages
     */
    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    /**
     * Scope for featured packages
     */
    public function scopeFeatured($query)
    {
        return $query->where('is_featured', true);
    }

    /**
     * Get display name based on locale
     */
    public function getDisplayNameAttribute(): string
    {
        $locale = app()->getLocale();
        return $locale === 'th' && $this->name_th ? $this->name_th : $this->name;
    }

    /**
     * Get duration text for display
     */
    public function getDurationTextAttribute(): string
    {
        $value = $this->duration_value;
        $texts = [
            'hourly' => $value === 1 ? '1 ชั่วโมง' : "{$value} ชั่วโมง",
            'daily' => $value === 1 ? '1 วัน' : "{$value} วัน",
            'weekly' => $value === 1 ? '1 สัปดาห์' : "{$value} สัปดาห์",
            'monthly' => $value === 1 ? '1 เดือน' : "{$value} เดือน",
            'yearly' => $value === 1 ? '1 ปี' : "{$value} ปี",
        ];

        return $texts[$this->duration_type] ?? "{$value} {$this->duration_type}";
    }

    /**
     * Get discount percentage if original price exists
     */
    public function getDiscountPercentageAttribute(): ?int
    {
        if (!$this->original_price || $this->original_price <= $this->price) {
            return null;
        }

        return (int) round((($this->original_price - $this->price) / $this->original_price) * 100);
    }

    /**
     * Calculate expiry date from start date
     */
    public function calculateExpiryDate(Carbon $startDate): Carbon
    {
        return match ($this->duration_type) {
            'hourly' => $startDate->addHours($this->duration_value),
            'daily' => $startDate->addDays($this->duration_value),
            'weekly' => $startDate->addWeeks($this->duration_value),
            'monthly' => $startDate->addMonths($this->duration_value),
            'yearly' => $startDate->addYears($this->duration_value),
            default => $startDate->addDays($this->duration_value),
        };
    }

    /**
     * Get usage limits as array
     */
    public function getUsageLimits(): array
    {
        return [
            'posts' => $this->posts_limit,
            'brands' => $this->brands_limit,
            'platforms' => $this->platforms_limit,
            'ai_generations' => $this->ai_generations_limit,
            'accounts_per_platform' => $this->accounts_per_platform,
            'scheduled_posts' => $this->scheduled_posts_limit,
            'team_members' => $this->team_members_limit,
        ];
    }

    /**
     * Check if limit is unlimited
     */
    public function isUnlimited(string $type): bool
    {
        $limit = match ($type) {
            'posts' => $this->posts_limit,
            'brands' => $this->brands_limit,
            'platforms' => $this->platforms_limit,
            'ai_generations' => $this->ai_generations_limit,
            'scheduled_posts' => $this->scheduled_posts_limit,
            'team_members' => $this->team_members_limit,
            default => 0,
        };

        return $limit === -1;
    }

    /**
     * Get formatted price
     */
    public function getFormattedPrice(): string
    {
        return number_format($this->price, 0) . ' ' . $this->currency;
    }

    /**
     * Get price per day for comparison
     */
    public function getPricePerDay(): float
    {
        $days = $this->getDurationInDays();
        return $days > 0 ? $this->price / $days : $this->price;
    }

    /**
     * Get duration in days
     */
    public function getDurationInDays(): int
    {
        return match ($this->duration_type) {
            'hourly' => max(1, (int) ceil($this->duration_value / 24)),
            'daily', 'days' => $this->duration_value,
            'weekly', 'weeks' => $this->duration_value * 7,
            'monthly', 'months' => $this->duration_value * 30,
            'yearly', 'years' => $this->duration_value * 365,
            default => $this->duration_value,
        };
    }

    /**
     * Create default packages
     */
    public static function createDefaultPackages(): void
    {
        $packages = [
            [
                'name' => 'Trial',
                'name_th' => 'ทดลองใช้',
                'description' => 'Try our service free for 3 days',
                'description_th' => 'ทดลองใช้ฟรี 3 วัน',
                'duration_type' => 'daily',
                'duration_value' => 3,
                'price' => 0,
                'posts_limit' => 10,
                'brands_limit' => 1,
                'platforms_limit' => 2,
                'ai_generations_limit' => 20,
                'accounts_per_platform' => 1,
                'is_active' => true,
                'has_trial' => true,
                'trial_days' => 3,
                'sort_order' => 1,
                'features' => ['basic_posting', 'ai_content'],
            ],
            [
                'name' => 'Daily Pass',
                'name_th' => 'รายวัน',
                'description' => '24-hour access to all features',
                'description_th' => 'ใช้งานได้ 24 ชั่วโมง',
                'duration_type' => 'daily',
                'duration_value' => 1,
                'price' => 99,
                'posts_limit' => 50,
                'brands_limit' => 1,
                'platforms_limit' => 9,
                'ai_generations_limit' => 100,
                'accounts_per_platform' => 1,
                'is_active' => true,
                'sort_order' => 2,
                'features' => ['all_platforms', 'ai_content', 'scheduling'],
            ],
            [
                'name' => 'Weekly',
                'name_th' => 'รายสัปดาห์',
                'description' => '7 days of premium access',
                'description_th' => 'ใช้งานได้ 7 วัน',
                'duration_type' => 'weekly',
                'duration_value' => 1,
                'price' => 499,
                'original_price' => 693,
                'posts_limit' => 200,
                'brands_limit' => 3,
                'platforms_limit' => 9,
                'ai_generations_limit' => 500,
                'accounts_per_platform' => 2,
                'is_active' => true,
                'is_popular' => true,
                'sort_order' => 3,
                'features' => ['all_platforms', 'ai_content', 'scheduling', 'analytics', 'multiple_brands'],
            ],
            [
                'name' => 'Monthly',
                'name_th' => 'รายเดือน',
                'description' => '30 days of full access',
                'description_th' => 'ใช้งานได้ 30 วัน',
                'duration_type' => 'monthly',
                'duration_value' => 1,
                'price' => 1490,
                'original_price' => 2970,
                'posts_limit' => 1000,
                'brands_limit' => 5,
                'platforms_limit' => 9,
                'ai_generations_limit' => 2000,
                'accounts_per_platform' => 3,
                'is_active' => true,
                'is_featured' => true,
                'sort_order' => 4,
                'features' => ['all_platforms', 'ai_content', 'scheduling', 'analytics', 'multiple_brands', 'priority_support'],
            ],
            [
                'name' => 'Quarterly',
                'name_th' => 'รายไตรมาส',
                'description' => '3 months of premium access',
                'description_th' => 'ใช้งานได้ 3 เดือน',
                'duration_type' => 'monthly',
                'duration_value' => 3,
                'price' => 3990,
                'original_price' => 4470,
                'posts_limit' => -1,
                'brands_limit' => 10,
                'platforms_limit' => 9,
                'ai_generations_limit' => -1,
                'accounts_per_platform' => 5,
                'is_active' => true,
                'sort_order' => 5,
                'features' => ['unlimited_posts', 'all_platforms', 'ai_content', 'scheduling', 'analytics', 'multiple_brands', 'priority_support', 'api_access'],
            ],
            [
                'name' => 'Yearly',
                'name_th' => 'รายปี',
                'description' => '12 months - Best Value!',
                'description_th' => 'ใช้งานได้ 1 ปี - คุ้มที่สุด!',
                'duration_type' => 'yearly',
                'duration_value' => 1,
                'price' => 12900,
                'original_price' => 17880,
                'posts_limit' => -1,
                'brands_limit' => -1,
                'platforms_limit' => 9,
                'ai_generations_limit' => -1,
                'accounts_per_platform' => 10,
                'team_members_limit' => 5,
                'is_active' => true,
                'is_featured' => true,
                'sort_order' => 6,
                'features' => ['unlimited_everything', 'all_platforms', 'ai_content', 'scheduling', 'analytics', 'team_access', 'priority_support', 'api_access', 'white_label'],
            ],
        ];

        foreach ($packages as $package) {
            static::updateOrCreate(
                ['name' => $package['name']],
                $package
            );
        }
    }
}
