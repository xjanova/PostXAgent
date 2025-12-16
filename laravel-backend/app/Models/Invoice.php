<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class Invoice extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'invoice_number',
        'user_id',
        'payment_id',
        'user_rental_id',
        'type',
        'status',
        'subtotal',
        'discount',
        'vat',
        'total',
        'currency',
        'tax_id',
        'company_name',
        'company_address',
        'branch_name',
        'line_items',
        'issue_date',
        'due_date',
        'paid_at',
        'pdf_url',
    ];

    protected $casts = [
        'subtotal' => 'decimal:2',
        'discount' => 'decimal:2',
        'vat' => 'decimal:2',
        'total' => 'decimal:2',
        'line_items' => 'array',
        'issue_date' => 'date',
        'due_date' => 'date',
        'paid_at' => 'datetime',
    ];

    const TYPE_INVOICE = 'invoice';
    const TYPE_RECEIPT = 'receipt';
    const TYPE_TAX_INVOICE = 'tax_invoice';

    const STATUS_DRAFT = 'draft';
    const STATUS_SENT = 'sent';
    const STATUS_PAID = 'paid';
    const STATUS_VOID = 'void';

    protected static function boot()
    {
        parent::boot();

        static::creating(function ($invoice) {
            if (!$invoice->invoice_number) {
                $invoice->invoice_number = static::generateInvoiceNumber($invoice->type);
            }
        });
    }

    /**
     * Get the user
     */
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Get the payment
     */
    public function payment(): BelongsTo
    {
        return $this->belongsTo(Payment::class);
    }

    /**
     * Get the rental
     */
    public function userRental(): BelongsTo
    {
        return $this->belongsTo(UserRental::class);
    }

    /**
     * Generate invoice number
     */
    public static function generateInvoiceNumber(string $type): string
    {
        $prefix = match ($type) {
            self::TYPE_INVOICE => 'INV',
            self::TYPE_RECEIPT => 'RCP',
            self::TYPE_TAX_INVOICE => 'TAX',
            default => 'DOC',
        };

        $year = now()->format('Y');
        $month = now()->format('m');

        // Get last number for this month
        $lastInvoice = static::where('invoice_number', 'like', "{$prefix}{$year}{$month}%")
            ->orderBy('invoice_number', 'desc')
            ->first();

        if ($lastInvoice) {
            $lastNumber = (int) substr($lastInvoice->invoice_number, -5);
            $newNumber = $lastNumber + 1;
        } else {
            $newNumber = 1;
        }

        return sprintf('%s%s%s%05d', $prefix, $year, $month, $newNumber);
    }

    /**
     * Calculate VAT (7%)
     */
    public static function calculateVat(float $amount): float
    {
        return round($amount * 0.07, 2);
    }

    /**
     * Create invoice from rental and payment
     */
    public static function createFromPayment(Payment $payment, string $type = self::TYPE_RECEIPT): self
    {
        $rental = $payment->userRental;
        $package = $rental?->rentalPackage;

        $subtotal = $payment->amount;
        $vat = 0;
        $total = $subtotal;

        // For tax invoice, calculate VAT
        if ($type === self::TYPE_TAX_INVOICE) {
            // Amount includes VAT, need to back-calculate
            $subtotal = round($payment->amount / 1.07, 2);
            $vat = round($payment->amount - $subtotal, 2);
            $total = $payment->amount;
        }

        $lineItems = [];
        if ($package) {
            $lineItems[] = [
                'description' => $package->name . ' (' . $package->duration_text . ')',
                'description_th' => ($package->name_th ?? $package->name) . ' (' . $package->duration_text . ')',
                'quantity' => 1,
                'unit_price' => $subtotal,
                'amount' => $subtotal,
            ];
        }

        return static::create([
            'user_id' => $payment->user_id,
            'payment_id' => $payment->id,
            'user_rental_id' => $rental?->id,
            'type' => $type,
            'status' => $payment->status === Payment::STATUS_COMPLETED ? self::STATUS_PAID : self::STATUS_DRAFT,
            'subtotal' => $subtotal,
            'discount' => 0,
            'vat' => $vat,
            'total' => $total,
            'currency' => $payment->currency,
            'line_items' => $lineItems,
            'issue_date' => now(),
            'paid_at' => $payment->paid_at,
        ]);
    }

    /**
     * Get type label (Thai)
     */
    public function getTypeLabel(): string
    {
        return match ($this->type) {
            self::TYPE_INVOICE => 'ใบแจ้งหนี้',
            self::TYPE_RECEIPT => 'ใบเสร็จรับเงิน',
            self::TYPE_TAX_INVOICE => 'ใบกำกับภาษี',
            default => $this->type,
        };
    }

    /**
     * Get status label (Thai)
     */
    public function getStatusLabel(): string
    {
        return match ($this->status) {
            self::STATUS_DRAFT => 'ร่าง',
            self::STATUS_SENT => 'ส่งแล้ว',
            self::STATUS_PAID => 'ชำระแล้ว',
            self::STATUS_VOID => 'ยกเลิก',
            default => $this->status,
        };
    }
}
