<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Carbon\Carbon;

class UserRental extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'rental_package_id',
        'starts_at',
        'expires_at',
        'cancelled_at',
        'status',
        'amount_paid',
        'currency',
        'payment_method',
        'payment_reference',
        'posts_used',
        'ai_generations_used',
        'usage_stats',
        'auto_renew',
        'next_renewal_at',
        'metadata',
        'notes',
    ];

    protected $casts = [
        'starts_at' => 'datetime',
        'expires_at' => 'datetime',
        'cancelled_at' => 'datetime',
        'next_renewal_at' => 'datetime',
        'amount_paid' => 'decimal:2',
        'usage_stats' => 'array',
        'metadata' => 'array',
        'auto_renew' => 'boolean',
    ];

    protected $appends = ['is_active', 'is_expired', 'days_remaining', 'usage_percentage'];

    const STATUS_PENDING = 'pending';
    const STATUS_ACTIVE = 'active';
    const STATUS_EXPIRED = 'expired';
    const STATUS_CANCELLED = 'cancelled';
    const STATUS_SUSPENDED = 'suspended';

    /**
     * Get the user that owns the rental
     */
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Get the rental package
     */
    public function rentalPackage(): BelongsTo
    {
        return $this->belongsTo(RentalPackage::class);
    }

    /**
     * Get payments for this rental
     */
    public function payments(): HasMany
    {
        return $this->hasMany(Payment::class);
    }

    /**
     * Get usage logs for this rental
     */
    public function usageLogs(): HasMany
    {
        return $this->hasMany(UsageLog::class);
    }

    /**
     * Scope for active rentals
     */
    public function scopeActive($query)
    {
        return $query->where('status', self::STATUS_ACTIVE)
            ->where('expires_at', '>', now());
    }

    /**
     * Scope for expired rentals
     */
    public function scopeExpired($query)
    {
        return $query->where('status', self::STATUS_ACTIVE)
            ->where('expires_at', '<=', now());
    }

    /**
     * Scope for pending rentals
     */
    public function scopePending($query)
    {
        return $query->where('status', self::STATUS_PENDING);
    }

    /**
     * Check if rental is currently active
     */
    public function getIsActiveAttribute(): bool
    {
        if (!$this->expires_at) {
            return false;
        }
        return $this->status === self::STATUS_ACTIVE
            && $this->expires_at->isFuture();
    }

    /**
     * Check if rental is expired
     */
    public function getIsExpiredAttribute(): bool
    {
        if (!$this->expires_at) {
            return false;
        }
        return $this->expires_at->isPast();
    }

    /**
     * Get remaining days
     */
    public function getDaysRemainingAttribute(): int
    {
        if (!$this->expires_at || $this->is_expired) {
            return 0;
        }

        return max(0, now()->diffInDays($this->expires_at, false));
    }

    /**
     * Get usage percentage for posts
     */
    public function getUsagePercentageAttribute(): ?array
    {
        $package = $this->rentalPackage;
        if (!$package) {
            return null;
        }

        $calculatePercentage = function ($used, $limit) {
            if ($limit === -1) return 0; // Unlimited
            if ($limit === 0) return 100;
            return min(100, round(($used / $limit) * 100));
        };

        return [
            'posts' => $calculatePercentage($this->posts_used, $package->posts_limit),
            'ai_generations' => $calculatePercentage($this->ai_generations_used, $package->ai_generations_limit),
        ];
    }

    /**
     * Activate the rental
     */
    public function activate(): bool
    {
        if ($this->status !== self::STATUS_PENDING) {
            return false;
        }

        $this->update([
            'status' => self::STATUS_ACTIVE,
            'starts_at' => now(),
            'expires_at' => $this->rentalPackage->calculateExpiryDate(now()),
        ]);

        return true;
    }

    /**
     * Expire the rental
     */
    public function expire(): bool
    {
        $this->update(['status' => self::STATUS_EXPIRED]);
        return true;
    }

    /**
     * Cancel the rental
     */
    public function cancel(string $reason = null): bool
    {
        $this->update([
            'status' => self::STATUS_CANCELLED,
            'cancelled_at' => now(),
            'auto_renew' => false,
            'notes' => $reason ? ($this->notes . "\nCancelled: " . $reason) : $this->notes,
        ]);

        return true;
    }

    /**
     * Suspend the rental
     */
    public function suspend(string $reason = null): bool
    {
        $this->update([
            'status' => self::STATUS_SUSPENDED,
            'notes' => $reason ? ($this->notes . "\nSuspended: " . $reason) : $this->notes,
        ]);

        return true;
    }

    /**
     * Renew the rental
     */
    public function renew(): ?UserRental
    {
        $package = $this->rentalPackage;
        if (!$package || !$package->is_active) {
            return null;
        }

        // Create new rental starting from current expiry
        $newRental = static::create([
            'user_id' => $this->user_id,
            'rental_package_id' => $this->rental_package_id,
            'starts_at' => $this->expires_at,
            'expires_at' => $package->calculateExpiryDate($this->expires_at),
            'status' => self::STATUS_PENDING,
            'amount_paid' => $package->price,
            'currency' => $package->currency,
            'auto_renew' => $this->auto_renew,
            'metadata' => ['renewed_from' => $this->id],
        ]);

        return $newRental;
    }

    /**
     * Increment post usage
     */
    public function incrementPostUsage(int $count = 1): bool
    {
        $this->increment('posts_used', $count);
        return true;
    }

    /**
     * Increment AI generation usage
     */
    public function incrementAIUsage(int $count = 1): bool
    {
        $this->increment('ai_generations_used', $count);
        return true;
    }

    /**
     * Check if user can use feature based on limits
     */
    public function canUse(string $feature): bool
    {
        if (!$this->is_active) {
            return false;
        }

        $package = $this->rentalPackage;
        if (!$package) {
            return false;
        }

        return match ($feature) {
            'posts' => $package->posts_limit === -1 || $this->posts_used < $package->posts_limit,
            'ai_generations' => $package->ai_generations_limit === -1 || $this->ai_generations_used < $package->ai_generations_limit,
            default => true,
        };
    }

    /**
     * Get remaining quota for feature
     */
    public function getRemainingQuota(string $feature): int
    {
        $package = $this->rentalPackage;
        if (!$package) {
            return 0;
        }

        return match ($feature) {
            'posts' => $package->posts_limit === -1 ? -1 : max(0, $package->posts_limit - $this->posts_used),
            'ai_generations' => $package->ai_generations_limit === -1 ? -1 : max(0, $package->ai_generations_limit - $this->ai_generations_used),
            default => 0,
        };
    }

    /**
     * Get formatted status
     */
    public function getStatusLabel(): string
    {
        return match ($this->status) {
            self::STATUS_PENDING => 'รอชำระเงิน',
            self::STATUS_ACTIVE => 'ใช้งานอยู่',
            self::STATUS_EXPIRED => 'หมดอายุ',
            self::STATUS_CANCELLED => 'ยกเลิกแล้ว',
            self::STATUS_SUSPENDED => 'ระงับชั่วคราว',
            default => $this->status,
        };
    }
}
