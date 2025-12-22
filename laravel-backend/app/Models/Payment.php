<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Support\Str;

class Payment extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'uuid',
        'user_id',
        'user_rental_id',
        'amount',
        'fee',
        'net_amount',
        'currency',
        'payment_method',
        'status',
        'gateway',
        'gateway_reference',
        'gateway_status',
        'gateway_response',
        'promptpay_qr_url',
        'bank_account_number',
        'bank_name',
        'transfer_slip_url',
        'paid_at',
        'verified_at',
        'verified_by',
        'description',
        'metadata',
        'admin_notes',
    ];

    protected $casts = [
        'amount' => 'decimal:2',
        'fee' => 'decimal:2',
        'net_amount' => 'decimal:2',
        'gateway_response' => 'array',
        'metadata' => 'array',
        'paid_at' => 'datetime',
        'verified_at' => 'datetime',
    ];

    protected $hidden = ['gateway_response', 'admin_notes'];

    const METHOD_PROMPTPAY = 'promptpay';
    const METHOD_BANK_TRANSFER = 'bank_transfer';
    const METHOD_CREDIT_CARD = 'credit_card';
    const METHOD_DEBIT_CARD = 'debit_card';
    const METHOD_TRUEMONEY = 'truemoney';
    const METHOD_LINEPAY = 'linepay';
    const METHOD_SHOPEEPAY = 'shopeepay';
    const METHOD_STRIPE = 'stripe';
    const METHOD_MANUAL = 'manual';

    const STATUS_PENDING = 'pending';
    const STATUS_PROCESSING = 'processing';
    const STATUS_COMPLETED = 'completed';
    const STATUS_FAILED = 'failed';
    const STATUS_REFUNDED = 'refunded';
    const STATUS_CANCELLED = 'cancelled';

    protected static function boot()
    {
        parent::boot();

        static::creating(function ($payment) {
            if (!$payment->uuid) {
                $payment->uuid = Str::uuid();
            }
            if (!$payment->net_amount) {
                $payment->net_amount = $payment->amount - ($payment->fee ?? 0);
            }
        });
    }

    /**
     * Get the user that owns the payment
     */
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Get the rental associated with this payment
     */
    public function userRental(): BelongsTo
    {
        return $this->belongsTo(UserRental::class);
    }

    /**
     * Get the admin who verified this payment
     */
    public function verifier(): BelongsTo
    {
        return $this->belongsTo(User::class, 'verified_by');
    }

    /**
     * Get the invoice for this payment
     */
    public function invoice()
    {
        return $this->hasOne(Invoice::class);
    }

    /**
     * Scope for completed payments
     */
    public function scopeCompleted($query)
    {
        return $query->where('status', self::STATUS_COMPLETED);
    }

    /**
     * Scope for pending payments
     */
    public function scopePending($query)
    {
        return $query->where('status', self::STATUS_PENDING);
    }

    /**
     * Scope by payment method
     */
    public function scopeByMethod($query, string $method)
    {
        return $query->where('payment_method', $method);
    }

    /**
     * Mark payment as completed
     */
    public function markAsCompleted(): bool
    {
        $this->update([
            'status' => self::STATUS_COMPLETED,
            'paid_at' => now(),
        ]);

        // Activate the rental if exists
        if ($this->userRental) {
            $this->userRental->activate();
        }

        return true;
    }

    /**
     * Mark payment as failed
     */
    public function markAsFailed(string $reason = null): bool
    {
        $this->update([
            'status' => self::STATUS_FAILED,
            'admin_notes' => $reason,
        ]);

        return true;
    }

    /**
     * Verify payment manually (admin)
     */
    public function verify(int $adminId, string $notes = null): bool
    {
        $this->update([
            'status' => self::STATUS_COMPLETED,
            'verified_at' => now(),
            'verified_by' => $adminId,
            'paid_at' => $this->paid_at ?? now(),
            'admin_notes' => $notes,
        ]);

        // Activate the rental
        if ($this->userRental) {
            $this->userRental->activate();
        }

        return true;
    }

    /**
     * Process refund
     */
    public function refund(string $reason = null): bool
    {
        if ($this->status !== self::STATUS_COMPLETED) {
            return false;
        }

        $this->update([
            'status' => self::STATUS_REFUNDED,
            'admin_notes' => ($this->admin_notes ?? '') . "\nRefunded: " . ($reason ?? 'No reason provided'),
        ]);

        return true;
    }

    /**
     * Get formatted amount
     */
    public function getFormattedAmount(): string
    {
        return number_format($this->amount, 2) . ' ' . $this->currency;
    }

    /**
     * Get payment method label (Thai)
     */
    public function getMethodLabel(): string
    {
        return match ($this->payment_method) {
            self::METHOD_PROMPTPAY => 'พร้อมเพย์',
            self::METHOD_BANK_TRANSFER => 'โอนเงิน',
            self::METHOD_CREDIT_CARD => 'บัตรเครดิต',
            self::METHOD_DEBIT_CARD => 'บัตรเดบิต',
            self::METHOD_TRUEMONEY => 'TrueMoney',
            self::METHOD_LINEPAY => 'LINE Pay',
            self::METHOD_SHOPEEPAY => 'ShopeePay',
            self::METHOD_STRIPE => 'Stripe',
            self::METHOD_MANUAL => 'ชำระด้วยตนเอง',
            default => $this->payment_method,
        };
    }

    /**
     * Get status label (Thai)
     */
    public function getStatusLabel(): string
    {
        return match ($this->status) {
            self::STATUS_PENDING => 'รอชำระเงิน',
            self::STATUS_PROCESSING => 'กำลังตรวจสอบ',
            self::STATUS_COMPLETED => 'ชำระเงินแล้ว',
            self::STATUS_FAILED => 'ล้มเหลว',
            self::STATUS_REFUNDED => 'คืนเงินแล้ว',
            self::STATUS_CANCELLED => 'ยกเลิก',
            default => $this->status,
        };
    }

    /**
     * Generate unique payment reference
     */
    public static function generateReference(): string
    {
        $prefix = 'PAY';
        $date = now()->format('Ymd');
        $random = strtoupper(Str::random(6));

        return "{$prefix}{$date}{$random}";
    }
}
