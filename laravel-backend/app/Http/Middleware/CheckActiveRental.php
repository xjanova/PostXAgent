<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use App\Models\UserRental;
use Symfony\Component\HttpFoundation\Response;

/**
 * Middleware to check if user has any active rental
 * ตรวจสอบว่าผู้ใช้มีแพ็กเกจที่ใช้งานอยู่หรือไม่
 */
class CheckActiveRental
{
    /**
     * Handle an incoming request.
     *
     * @param  \Closure(\Illuminate\Http\Request): (\Symfony\Component\HttpFoundation\Response)  $next
     */
    public function handle(Request $request, Closure $next): Response
    {
        $user = $request->user();

        if (!$user) {
            return response()->json([
                'success' => false,
                'error' => 'unauthorized',
                'message' => 'กรุณาเข้าสู่ระบบก่อนใช้งาน',
            ], 401);
        }

        // Get active rental
        $rental = UserRental::with('rentalPackage')
            ->where('user_id', $user->id)
            ->where('status', 'active')
            ->where('expires_at', '>', now())
            ->first();

        if (!$rental) {
            // Check if user has an expired rental
            $expiredRental = UserRental::with('rentalPackage')
                ->where('user_id', $user->id)
                ->where('status', 'active')
                ->where('expires_at', '<=', now())
                ->first();

            if ($expiredRental) {
                return response()->json([
                    'success' => false,
                    'error' => 'rental_expired',
                    'message' => 'แพ็กเกจของคุณหมดอายุแล้ว กรุณาต่ออายุหรือซื้อแพ็กเกจใหม่',
                    'action' => 'renew_required',
                    'redirect' => '/pricing',
                    'expired_package' => [
                        'name' => $expiredRental->rentalPackage->name,
                        'expired_at' => $expiredRental->expires_at->toIso8601String(),
                    ],
                ], 402);
            }

            // Check if user has a pending payment
            $pendingRental = UserRental::with(['rentalPackage', 'payments'])
                ->where('user_id', $user->id)
                ->where('status', 'pending')
                ->first();

            if ($pendingRental) {
                $pendingPayment = $pendingRental->payments()
                    ->where('status', 'pending')
                    ->first();

                return response()->json([
                    'success' => false,
                    'error' => 'payment_pending',
                    'message' => 'คุณมีการชำระเงินที่รอดำเนินการ กรุณาชำระเงินให้เสร็จสิ้น',
                    'action' => 'complete_payment',
                    'redirect' => $pendingPayment ? "/payment/{$pendingPayment->uuid}" : '/pricing',
                    'pending_package' => [
                        'name' => $pendingRental->rentalPackage->name,
                        'payment_uuid' => $pendingPayment?->uuid,
                    ],
                ], 402);
            }

            return response()->json([
                'success' => false,
                'error' => 'no_active_rental',
                'message' => 'คุณยังไม่มีแพ็กเกจที่ใช้งานอยู่ กรุณาซื้อแพ็กเกจก่อน',
                'action' => 'purchase_required',
                'redirect' => '/pricing',
            ], 402);
        }

        // Check if rental is about to expire (within 3 days)
        $daysUntilExpiry = now()->diffInDays($rental->expires_at, false);
        $expiringWarning = null;

        if ($daysUntilExpiry <= 3 && $daysUntilExpiry >= 0) {
            $expiringWarning = [
                'message' => $daysUntilExpiry <= 1
                    ? 'แพ็กเกจของคุณจะหมดอายุภายในวันนี้!'
                    : "แพ็กเกจของคุณจะหมดอายุใน {$daysUntilExpiry} วัน",
                'expires_at' => $rental->expires_at->toIso8601String(),
                'days_remaining' => $daysUntilExpiry,
            ];
        }

        // Attach rental info to request
        $request->attributes->set('current_rental', $rental);
        $request->attributes->set('current_package', $rental->rentalPackage);
        $request->attributes->set('rental_expiring_warning', $expiringWarning);

        return $next($request);
    }
}
