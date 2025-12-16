<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\HasMany;

class PromoCode extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'code',
        'name',
        'description',
        'discount_type',
        'discount_value',
        'max_discount',
        'min_purchase',
        'applicable_packages',
        'first_purchase_only',
        'max_uses',
        'max_uses_per_user',
        'times_used',
        'starts_at',
        'expires_at',
        'is_active',
    ];

    protected $casts = [
        'discount_value' => 'decimal:2',
        'max_discount' => 'decimal:2',
        'min_purchase' => 'decimal:2',
        'applicable_packages' => 'array',
        'first_purchase_only' => 'boolean',
        'is_active' => 'boolean',
        'starts_at' => 'datetime',
        'expires_at' => 'datetime',
    ];

    /**
     * Get usages for this promo code
     */
    public function usages(): HasMany
    {
        return $this->hasMany(PromoCodeUsage::class);
    }

    /**
     * Scope for active promo codes
     */
    public function scopeActive($query)
    {
        return $query->where('is_active', true)
            ->where(function ($q) {
                $q->whereNull('starts_at')
                    ->orWhere('starts_at', '<=', now());
            })
            ->where(function ($q) {
                $q->whereNull('expires_at')
                    ->orWhere('expires_at', '>', now());
            });
    }

    /**
     * Scope by code
     */
    public function scopeByCode($query, string $code)
    {
        return $query->where('code', strtoupper($code));
    }

    /**
     * Check if promo code is valid
     */
    public function isValid(): bool
    {
        if (!$this->is_active) {
            return false;
        }

        // Check date range
        if ($this->starts_at && $this->starts_at->isFuture()) {
            return false;
        }

        if ($this->expires_at && $this->expires_at->isPast()) {
            return false;
        }

        // Check usage limit
        if ($this->max_uses && $this->times_used >= $this->max_uses) {
            return false;
        }

        return true;
    }

    /**
     * Check if user can use this promo code
     */
    public function canBeUsedBy(User $user): array
    {
        if (!$this->isValid()) {
            return ['valid' => false, 'message' => 'โค้ดส่วนลดไม่ถูกต้องหรือหมดอายุแล้ว'];
        }

        // Check per-user limit
        $userUsageCount = $this->usages()->where('user_id', $user->id)->count();
        if ($userUsageCount >= $this->max_uses_per_user) {
            return ['valid' => false, 'message' => 'คุณใช้โค้ดนี้ครบจำนวนแล้ว'];
        }

        // Check first purchase only
        if ($this->first_purchase_only) {
            $hasRentals = $user->rentals()->completed()->exists();
            if ($hasRentals) {
                return ['valid' => false, 'message' => 'โค้ดนี้สำหรับการซื้อครั้งแรกเท่านั้น'];
            }
        }

        return ['valid' => true, 'message' => 'โค้ดส่วนลดใช้งานได้'];
    }

    /**
     * Calculate discount amount
     */
    public function calculateDiscount(float $amount, ?int $packageId = null): float
    {
        // Check if applicable to package
        if ($this->applicable_packages && $packageId) {
            if (!in_array($packageId, $this->applicable_packages)) {
                return 0;
            }
        }

        // Check minimum purchase
        if ($amount < $this->min_purchase) {
            return 0;
        }

        // Calculate discount
        if ($this->discount_type === 'percentage') {
            $discount = $amount * ($this->discount_value / 100);
        } else {
            $discount = $this->discount_value;
        }

        // Apply max discount cap
        if ($this->max_discount && $discount > $this->max_discount) {
            $discount = $this->max_discount;
        }

        // Can't exceed original amount
        return min($discount, $amount);
    }

    /**
     * Record usage of promo code
     */
    public function recordUsage(User $user, Payment $payment, float $discountAmount): PromoCodeUsage
    {
        $usage = $this->usages()->create([
            'user_id' => $user->id,
            'payment_id' => $payment->id,
            'discount_amount' => $discountAmount,
        ]);

        $this->increment('times_used');

        return $usage;
    }

    /**
     * Get discount display text
     */
    public function getDiscountText(): string
    {
        if ($this->discount_type === 'percentage') {
            return "ลด {$this->discount_value}%";
        }

        return "ลด " . number_format($this->discount_value, 0) . " บาท";
    }

    /**
     * Generate unique promo code
     */
    public static function generateCode(int $length = 8): string
    {
        $characters = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
        $code = '';

        for ($i = 0; $i < $length; $i++) {
            $code .= $characters[random_int(0, strlen($characters) - 1)];
        }

        // Check uniqueness
        while (static::where('code', $code)->exists()) {
            $code = static::generateCode($length);
        }

        return $code;
    }
}
