<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * Payment Order Model
 *
 * คำสั่งชำระเงินที่รอการจ่ายผ่าน SMS Payment Gateway
 * ใช้ระบบ Unique Amount เพื่อให้สามารถจับคู่ SMS กับ Order ได้อัตโนมัติ
 *
 * @property int $id
 * @property string $order_number
 * @property float $base_amount ยอดเดิมที่ลูกค้าต้องการชำระ
 * @property float $amount ยอดที่ต้องชำระจริง (รวมสตางค์ที่เพิ่ม)
 * @property float $amount_suffix สตางค์ที่เพิ่มเพื่อให้ยอดไม่ซ้ำ
 * @property string $description
 * @property string|null $reference
 * @property string|null $customer_name
 * @property string|null $customer_email
 * @property string|null $customer_phone
 * @property string|null $callback_url
 * @property string $status
 * @property \Carbon\Carbon|null $expires_at
 * @property \Carbon\Carbon|null $matched_at
 * @property \Carbon\Carbon|null $paid_at
 * @property \Carbon\Carbon|null $cancelled_at
 * @property string|null $cancellation_reason
 * @property int|null $payment_id
 * @property int|null $created_by
 * @property array|null $metadata
 */
class PaymentOrder extends Model
{
    protected $fillable = [
        'order_number',
        'base_amount',
        'amount',
        'amount_suffix',
        'description',
        'reference',
        'customer_name',
        'customer_email',
        'customer_phone',
        'callback_url',
        'status',
        'expires_at',
        'matched_at',
        'paid_at',
        'cancelled_at',
        'cancellation_reason',
        'payment_id',
        'created_by',
        'metadata',
    ];

    protected $casts = [
        'base_amount' => 'decimal:2',
        'amount' => 'decimal:2',
        'amount_suffix' => 'decimal:2',
        'expires_at' => 'datetime',
        'matched_at' => 'datetime',
        'paid_at' => 'datetime',
        'cancelled_at' => 'datetime',
        'metadata' => 'array',
    ];

    /**
     * Get the payment that matched this order
     */
    public function payment(): BelongsTo
    {
        return $this->belongsTo(SmsPayment::class, 'payment_id');
    }

    /**
     * Get the user who created this order
     */
    public function createdBy(): BelongsTo
    {
        return $this->belongsTo(User::class, 'created_by');
    }

    /**
     * Check if order is expired
     */
    public function getIsExpiredAttribute(): bool
    {
        if (!$this->expires_at) {
            return false;
        }

        return $this->expires_at->isPast() && $this->status === 'pending';
    }

    /**
     * Check if order can be matched
     */
    public function getCanMatchAttribute(): bool
    {
        return $this->status === 'pending' && !$this->is_expired;
    }

    /**
     * Append custom attributes
     */
    protected $appends = ['is_expired', 'can_match', 'status_label', 'amount_formatted', 'has_suffix'];

    /**
     * Status labels in Thai
     */
    public function getStatusLabelAttribute(): string
    {
        return match($this->status) {
            'pending' => 'รอชำระเงิน',
            'matched' => 'จับคู่แล้ว',
            'paid' => 'ชำระแล้ว',
            'cancelled' => 'ยกเลิก',
            'expired' => 'หมดอายุ',
            default => $this->status,
        };
    }

    /**
     * Get formatted amount with suffix explanation
     */
    public function getAmountFormattedAttribute(): string
    {
        return '฿' . number_format($this->amount, 2);
    }

    /**
     * Check if order has amount suffix
     */
    public function getHasSuffixAttribute(): bool
    {
        return $this->amount_suffix > 0;
    }

    /**
     * Get suffix in satang (for display)
     */
    public function getSuffixInSatangAttribute(): int
    {
        return (int) round($this->amount_suffix * 100);
    }

    /**
     * Get payment instruction text
     */
    public function getPaymentInstructionAttribute(): string
    {
        if ($this->amount_suffix > 0) {
            return sprintf(
                'กรุณาโอนเงินจำนวน %s (ยอดเดิม ฿%s + %d สตางค์เพื่อให้ระบบจับคู่อัตโนมัติ)',
                $this->amount_formatted,
                number_format($this->base_amount, 2),
                $this->suffix_in_satang
            );
        }
        return sprintf('กรุณาโอนเงินจำนวน %s', $this->amount_formatted);
    }

    /**
     * Scope for pending orders
     */
    public function scopePending($query)
    {
        return $query->where('status', 'pending')
            ->where(function ($q) {
                $q->whereNull('expires_at')
                    ->orWhere('expires_at', '>', now());
            });
    }

    /**
     * Scope for matchable orders (pending and not expired)
     */
    public function scopeMatchable($query)
    {
        return $query->pending();
    }

    /**
     * Scope for orders by amount range
     */
    public function scopeByAmountRange($query, float $min, float $max)
    {
        return $query->whereBetween('amount', [$min, $max]);
    }
}
