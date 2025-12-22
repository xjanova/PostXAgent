<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * SMS Payment Model
 *
 * การชำระเงินที่ตรวจจับจาก SMS ธนาคาร
 *
 * @property int $id
 * @property string $device_id
 * @property string $sms_sender
 * @property string $sms_body
 * @property \Carbon\Carbon $sms_received_at
 * @property float $amount
 * @property string $bank_name
 * @property string|null $account_number
 * @property string|null $transaction_ref
 * @property float $confidence
 * @property string $status
 * @property \Carbon\Carbon|null $approved_at
 * @property \Carbon\Carbon|null $rejected_at
 * @property int|null $approved_by
 * @property int|null $rejected_by
 * @property string|null $approval_note
 * @property string|null $rejection_reason
 * @property bool $auto_approved
 * @property int|null $order_id
 */
class SmsPayment extends Model
{
    protected $fillable = [
        'device_id',
        'sms_sender',
        'sms_body',
        'sms_received_at',
        'amount',
        'bank_name',
        'account_number',
        'transaction_ref',
        'confidence',
        'status',
        'approved_at',
        'rejected_at',
        'approved_by',
        'rejected_by',
        'approval_note',
        'rejection_reason',
        'auto_approved',
        'order_id',
    ];

    protected $casts = [
        'sms_received_at' => 'datetime',
        'amount' => 'decimal:2',
        'confidence' => 'decimal:2',
        'approved_at' => 'datetime',
        'rejected_at' => 'datetime',
        'auto_approved' => 'boolean',
    ];

    /**
     * Get the device that received this payment SMS
     */
    public function device(): BelongsTo
    {
        return $this->belongsTo(MobileDevice::class, 'device_id', 'device_id');
    }

    /**
     * Get the matched order
     */
    public function matchedOrder(): BelongsTo
    {
        return $this->belongsTo(PaymentOrder::class, 'order_id');
    }

    /**
     * Get the user who approved this payment
     */
    public function approvedBy(): BelongsTo
    {
        return $this->belongsTo(User::class, 'approved_by');
    }

    /**
     * Get the user who rejected this payment
     */
    public function rejectedBy(): BelongsTo
    {
        return $this->belongsTo(User::class, 'rejected_by');
    }

    /**
     * Get status label in Thai
     */
    public function getStatusLabelAttribute(): string
    {
        return match($this->status) {
            'pending' => 'รอตรวจสอบ',
            'approved' => 'อนุมัติแล้ว',
            'rejected' => 'ปฏิเสธ',
            default => $this->status,
        };
    }

    /**
     * Get confidence label
     */
    public function getConfidenceLabelAttribute(): string
    {
        if ($this->confidence >= 0.9) {
            return 'สูงมาก';
        } elseif ($this->confidence >= 0.7) {
            return 'สูง';
        } elseif ($this->confidence >= 0.5) {
            return 'ปานกลาง';
        } else {
            return 'ต่ำ';
        }
    }

    /**
     * Check if payment is matched to an order
     */
    public function getIsMatchedAttribute(): bool
    {
        return $this->order_id !== null;
    }

    /**
     * Append custom attributes
     */
    protected $appends = ['status_label', 'confidence_label', 'is_matched'];

    /**
     * Scope for pending payments
     */
    public function scopePending($query)
    {
        return $query->where('status', 'pending');
    }

    /**
     * Scope for approved payments
     */
    public function scopeApproved($query)
    {
        return $query->where('status', 'approved');
    }

    /**
     * Scope for rejected payments
     */
    public function scopeRejected($query)
    {
        return $query->where('status', 'rejected');
    }

    /**
     * Scope for high confidence payments
     */
    public function scopeHighConfidence($query, float $threshold = 0.8)
    {
        return $query->where('confidence', '>=', $threshold);
    }

    /**
     * Scope for unmatched payments
     */
    public function scopeUnmatched($query)
    {
        return $query->whereNull('order_id');
    }

    /**
     * Scope by bank
     */
    public function scopeByBank($query, string $bank)
    {
        return $query->where('bank_name', $bank);
    }

    /**
     * Scope by date range
     */
    public function scopeDateRange($query, $start, $end)
    {
        return $query->whereBetween('sms_received_at', [$start, $end]);
    }

    /**
     * Thai bank names mapping
     */
    public static function bankNames(): array
    {
        return [
            'KBANK' => 'ธนาคารกสิกรไทย',
            'SCB' => 'ธนาคารไทยพาณิชย์',
            'BBL' => 'ธนาคารกรุงเทพ',
            'KTB' => 'ธนาคารกรุงไทย',
            'TMB' => 'ธนาคารทหารไทยธนชาต',
            'BAY' => 'ธนาคารกรุงศรีอยุธยา',
            'GSB' => 'ธนาคารออมสิน',
            'BAAC' => 'ธ.ก.ส.',
            'PROMPTPAY' => 'พร้อมเพย์',
            'TRUEMONEY' => 'ทรูมันนี่',
        ];
    }
}
