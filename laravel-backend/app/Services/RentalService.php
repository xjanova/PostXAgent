<?php

namespace App\Services;

use App\Models\User;
use App\Models\RentalPackage;
use App\Models\UserRental;
use App\Models\Payment;
use App\Models\PromoCode;
use App\Models\Invoice;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;
use Carbon\Carbon;

class RentalService
{
    /**
     * Get available packages for display
     */
    public function getAvailablePackages(bool $includeTrial = true): array
    {
        $query = RentalPackage::active()
            ->orderBy('sort_order')
            ->orderBy('price');

        if (!$includeTrial) {
            $query->where('price', '>', 0);
        }

        return $query->get()->map(function ($package) {
            return [
                'id' => $package->id,
                'name' => $package->name,
                'name_th' => $package->name_th,
                'description' => $package->description,
                'description_th' => $package->description_th,
                'duration_type' => $package->duration_type,
                'duration_value' => $package->duration_value,
                'duration_text' => $package->duration_text,
                'price' => $package->price,
                'original_price' => $package->original_price,
                'discount_percentage' => $package->discount_percentage,
                'formatted_price' => $package->getFormattedPrice(),
                'price_per_day' => round($package->getPricePerDay(), 2),
                'currency' => $package->currency,
                'limits' => $package->getUsageLimits(),
                'features' => $package->features,
                'is_featured' => $package->is_featured,
                'is_popular' => $package->is_popular,
                'has_trial' => $package->has_trial,
            ];
        })->toArray();
    }

    /**
     * Get user's current active rental
     */
    public function getUserActiveRental(User $user): ?UserRental
    {
        return $user->rentals()
            ->active()
            ->with('rentalPackage')
            ->first();
    }

    /**
     * Get user's rental history
     */
    public function getUserRentalHistory(User $user, int $limit = 10): array
    {
        return $user->rentals()
            ->with('rentalPackage')
            ->orderBy('created_at', 'desc')
            ->limit($limit)
            ->get()
            ->map(function ($rental) {
                return [
                    'id' => $rental->id,
                    'package_name' => $rental->rentalPackage->display_name,
                    'starts_at' => $rental->starts_at?->toIso8601String(),
                    'expires_at' => $rental->expires_at?->toIso8601String(),
                    'status' => $rental->status,
                    'status_label' => $rental->getStatusLabel(),
                    'amount_paid' => $rental->amount_paid,
                    'is_active' => $rental->is_active,
                    'days_remaining' => $rental->days_remaining,
                    'usage' => [
                        'posts_used' => $rental->posts_used,
                        'ai_generations_used' => $rental->ai_generations_used,
                        'percentage' => $rental->usage_percentage,
                    ],
                ];
            })->toArray();
    }

    /**
     * Create a new rental (pending payment)
     */
    public function createRental(
        User $user,
        RentalPackage $package,
        ?string $promoCode = null,
        array $metadata = []
    ): array {
        // Check if user already has active rental
        $existingRental = $this->getUserActiveRental($user);
        if ($existingRental) {
            return [
                'success' => false,
                'error' => 'คุณมีแพ็กเกจที่ยังใช้งานอยู่',
                'existing_rental' => $existingRental,
            ];
        }

        // Check if free trial and user has used trial before
        if ($package->has_trial && $package->price == 0) {
            $hasUsedTrial = $user->rentals()
                ->whereHas('rentalPackage', fn($q) => $q->where('has_trial', true))
                ->exists();

            if ($hasUsedTrial) {
                return [
                    'success' => false,
                    'error' => 'คุณใช้ช่วงทดลองฟรีไปแล้ว',
                ];
            }
        }

        $originalAmount = $package->price;
        $discountAmount = 0;
        $finalAmount = $originalAmount;
        $appliedPromoCode = null;

        // Apply promo code if provided
        if ($promoCode) {
            $promo = PromoCode::active()->byCode($promoCode)->first();
            if ($promo) {
                $validation = $promo->canBeUsedBy($user);
                if ($validation['valid']) {
                    $discountAmount = $promo->calculateDiscount($originalAmount, $package->id);
                    $finalAmount = max(0, $originalAmount - $discountAmount);
                    $appliedPromoCode = $promo;
                }
            }
        }

        DB::beginTransaction();
        try {
            // Create rental
            $rental = UserRental::create([
                'user_id' => $user->id,
                'rental_package_id' => $package->id,
                'status' => $finalAmount == 0 ? UserRental::STATUS_ACTIVE : UserRental::STATUS_PENDING,
                'starts_at' => $finalAmount == 0 ? now() : null,
                'expires_at' => $finalAmount == 0 ? $package->calculateExpiryDate(now()) : null,
                'amount_paid' => $finalAmount,
                'currency' => $package->currency,
                'metadata' => array_merge($metadata, [
                    'original_amount' => $originalAmount,
                    'discount_amount' => $discountAmount,
                    'promo_code' => $promoCode,
                ]),
            ]);

            // If free (trial or fully discounted), no payment needed
            if ($finalAmount == 0) {
                DB::commit();
                return [
                    'success' => true,
                    'rental' => $rental,
                    'requires_payment' => false,
                    'message' => 'เปิดใช้งานแพ็กเกจสำเร็จ',
                ];
            }

            // Create pending payment
            $payment = Payment::create([
                'user_id' => $user->id,
                'user_rental_id' => $rental->id,
                'amount' => $finalAmount,
                'net_amount' => $finalAmount,
                'currency' => $package->currency,
                'status' => Payment::STATUS_PENDING,
                'payment_method' => Payment::METHOD_PROMPTPAY, // Default
                'payment_reference' => Payment::generateReference(),
                'description' => "ชำระค่าแพ็กเกจ {$package->display_name}",
                'metadata' => [
                    'package_id' => $package->id,
                    'package_name' => $package->name,
                    'discount_amount' => $discountAmount,
                    'promo_code_id' => $appliedPromoCode?->id,
                ],
            ]);

            // Record promo code usage if applicable
            if ($appliedPromoCode && $discountAmount > 0) {
                $appliedPromoCode->recordUsage($user, $payment, $discountAmount);
            }

            DB::commit();

            return [
                'success' => true,
                'rental' => $rental,
                'payment' => $payment,
                'requires_payment' => true,
                'amount' => $finalAmount,
                'original_amount' => $originalAmount,
                'discount_amount' => $discountAmount,
                'payment_reference' => $payment->payment_reference,
            ];

        } catch (\Exception $e) {
            DB::rollBack();
            Log::error('Failed to create rental', [
                'user_id' => $user->id,
                'package_id' => $package->id,
                'error' => $e->getMessage(),
            ]);

            return [
                'success' => false,
                'error' => 'เกิดข้อผิดพลาดในการสร้างการเช่า',
            ];
        }
    }

    /**
     * Process payment completion
     */
    public function processPaymentCompletion(Payment $payment): array
    {
        if ($payment->status === Payment::STATUS_COMPLETED) {
            return [
                'success' => false,
                'error' => 'การชำระเงินนี้ดำเนินการแล้ว',
            ];
        }

        DB::beginTransaction();
        try {
            $payment->markAsCompleted();

            // Create receipt/invoice
            $invoice = Invoice::createFromPayment($payment, Invoice::TYPE_RECEIPT);

            DB::commit();

            return [
                'success' => true,
                'payment' => $payment->fresh(),
                'rental' => $payment->userRental->fresh(),
                'invoice' => $invoice,
                'message' => 'ชำระเงินสำเร็จ เปิดใช้งานแพ็กเกจแล้ว',
            ];

        } catch (\Exception $e) {
            DB::rollBack();
            Log::error('Failed to process payment completion', [
                'payment_id' => $payment->id,
                'error' => $e->getMessage(),
            ]);

            return [
                'success' => false,
                'error' => 'เกิดข้อผิดพลาดในการประมวลผลการชำระเงิน',
            ];
        }
    }

    /**
     * Verify bank transfer manually
     */
    public function verifyBankTransfer(
        Payment $payment,
        int $adminId,
        ?string $transferSlipUrl = null,
        ?string $notes = null
    ): array {
        if ($payment->status !== Payment::STATUS_PENDING) {
            return [
                'success' => false,
                'error' => 'สถานะการชำระเงินไม่ถูกต้อง',
            ];
        }

        DB::beginTransaction();
        try {
            if ($transferSlipUrl) {
                $payment->update(['transfer_slip_url' => $transferSlipUrl]);
            }

            $payment->verify($adminId, $notes);

            // Create receipt
            $invoice = Invoice::createFromPayment($payment, Invoice::TYPE_RECEIPT);

            DB::commit();

            return [
                'success' => true,
                'payment' => $payment->fresh(),
                'rental' => $payment->userRental->fresh(),
                'invoice' => $invoice,
                'message' => 'ยืนยันการชำระเงินสำเร็จ',
            ];

        } catch (\Exception $e) {
            DB::rollBack();
            Log::error('Failed to verify bank transfer', [
                'payment_id' => $payment->id,
                'error' => $e->getMessage(),
            ]);

            return [
                'success' => false,
                'error' => 'เกิดข้อผิดพลาดในการยืนยันการชำระเงิน',
            ];
        }
    }

    /**
     * Cancel a pending rental
     */
    public function cancelRental(UserRental $rental, string $reason = null): array
    {
        if (!in_array($rental->status, [UserRental::STATUS_PENDING, UserRental::STATUS_ACTIVE])) {
            return [
                'success' => false,
                'error' => 'ไม่สามารถยกเลิกการเช่านี้ได้',
            ];
        }

        DB::beginTransaction();
        try {
            $rental->cancel($reason);

            // Cancel any pending payments
            $rental->payments()
                ->where('status', Payment::STATUS_PENDING)
                ->update(['status' => Payment::STATUS_CANCELLED]);

            DB::commit();

            return [
                'success' => true,
                'rental' => $rental->fresh(),
                'message' => 'ยกเลิกการเช่าสำเร็จ',
            ];

        } catch (\Exception $e) {
            DB::rollBack();
            return [
                'success' => false,
                'error' => 'เกิดข้อผิดพลาดในการยกเลิก',
            ];
        }
    }

    /**
     * Check and process expired rentals (for scheduler)
     */
    public function processExpiredRentals(): int
    {
        $expiredRentals = UserRental::where('status', UserRental::STATUS_ACTIVE)
            ->where('expires_at', '<=', now())
            ->get();

        $processed = 0;
        foreach ($expiredRentals as $rental) {
            // Check for auto-renewal
            if ($rental->auto_renew) {
                // TODO: Implement auto-renewal with stored payment method
                Log::info('Auto-renewal pending for rental', ['rental_id' => $rental->id]);
            }

            $rental->expire();
            $processed++;

            Log::info('Rental expired', [
                'rental_id' => $rental->id,
                'user_id' => $rental->user_id,
            ]);
        }

        return $processed;
    }

    /**
     * Get usage status for user
     */
    public function getUsageStatus(User $user): array
    {
        $rental = $this->getUserActiveRental($user);

        if (!$rental) {
            return [
                'has_active_rental' => false,
                'limits' => [
                    'posts' => 0,
                    'ai_generations' => 0,
                    'brands' => 0,
                    'platforms' => 0,
                ],
                'used' => [
                    'posts' => 0,
                    'ai_generations' => 0,
                ],
                'remaining' => [
                    'posts' => 0,
                    'ai_generations' => 0,
                ],
            ];
        }

        $package = $rental->rentalPackage;

        return [
            'has_active_rental' => true,
            'rental_id' => $rental->id,
            'package_name' => $package->display_name,
            'expires_at' => $rental->expires_at->toIso8601String(),
            'days_remaining' => $rental->days_remaining,
            'limits' => $package->getUsageLimits(),
            'used' => [
                'posts' => $rental->posts_used,
                'ai_generations' => $rental->ai_generations_used,
            ],
            'remaining' => [
                'posts' => $rental->getRemainingQuota('posts'),
                'ai_generations' => $rental->getRemainingQuota('ai_generations'),
            ],
            'can_post' => $rental->canUse('posts'),
            'can_generate_ai' => $rental->canUse('ai_generations'),
        ];
    }

    /**
     * Increment usage counters
     */
    public function incrementUsage(User $user, string $type, int $count = 1): bool
    {
        $rental = $this->getUserActiveRental($user);
        if (!$rental) {
            return false;
        }

        return match ($type) {
            'posts' => $rental->incrementPostUsage($count),
            'ai_generations' => $rental->incrementAIUsage($count),
            default => false,
        };
    }

    /**
     * Validate promo code
     */
    public function validatePromoCode(string $code, User $user, ?int $packageId = null): array
    {
        $promo = PromoCode::active()->byCode($code)->first();

        if (!$promo) {
            return [
                'valid' => false,
                'message' => 'ไม่พบโค้ดส่วนลดนี้',
            ];
        }

        $validation = $promo->canBeUsedBy($user);
        if (!$validation['valid']) {
            return $validation;
        }

        // Check package applicability
        if ($packageId && $promo->applicable_packages) {
            if (!in_array($packageId, $promo->applicable_packages)) {
                return [
                    'valid' => false,
                    'message' => 'โค้ดนี้ไม่สามารถใช้กับแพ็กเกจนี้ได้',
                ];
            }
        }

        return [
            'valid' => true,
            'promo' => [
                'code' => $promo->code,
                'name' => $promo->name,
                'discount_type' => $promo->discount_type,
                'discount_value' => $promo->discount_value,
                'discount_text' => $promo->getDiscountText(),
                'max_discount' => $promo->max_discount,
                'min_purchase' => $promo->min_purchase,
            ],
            'message' => 'โค้ดส่วนลดใช้งานได้',
        ];
    }
}
