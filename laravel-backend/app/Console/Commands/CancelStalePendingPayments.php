<?php

declare(strict_types=1);

namespace App\Console\Commands;

use Illuminate\Console\Command;
use App\Models\Payment;
use App\Models\UserRental;
use Illuminate\Support\Facades\Log;

/**
 * Cancel stale pending payments
 * ยกเลิก payments ที่รอนานเกิน 24 ชั่วโมง
 */
class CancelStalePendingPayments extends Command
{
    protected $signature = 'payments:cancel-stale {--hours=24 : Hours after which to cancel}';
    protected $description = 'Cancel pending payments older than specified hours';

    public function handle(): int
    {
        $hours = (int) $this->option('hours');
        $this->info("Canceling payments pending for more than {$hours} hours...");

        $cancelledCount = 0;
        $cutoffTime = now()->subHours($hours);

        // Find stale pending payments
        $stalePayments = Payment::where('status', 'pending')
            ->where('created_at', '<', $cutoffTime)
            ->get();

        foreach ($stalePayments as $payment) {
            try {
                // Cancel the payment
                $payment->update([
                    'status' => 'cancelled',
                    'notes' => "Auto-cancelled: pending for more than {$hours} hours",
                ]);

                // Also cancel the associated rental if it's still pending
                if ($payment->user_rental_id) {
                    $rental = UserRental::find($payment->user_rental_id);
                    if ($rental && $rental->status === 'pending') {
                        $rental->update(['status' => 'cancelled']);
                    }
                }

                $cancelledCount++;

                Log::info("Cancelled stale payment", [
                    'payment_id' => $payment->id,
                    'payment_uuid' => $payment->uuid,
                    'hours_pending' => $cutoffTime->diffInHours($payment->created_at),
                ]);
            } catch (\Exception $e) {
                Log::error("Failed to cancel stale payment", [
                    'payment_id' => $payment->id,
                    'error' => $e->getMessage(),
                ]);
            }
        }

        $this->info("Cancelled {$cancelledCount} stale payments.");

        return self::SUCCESS;
    }
}
