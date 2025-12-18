<?php

declare(strict_types=1);

namespace App\Console\Commands;

use Illuminate\Console\Command;
use App\Models\UserRental;
use App\Models\User;
use App\Notifications\RentalExpiredNotification;
use Illuminate\Support\Facades\Log;

/**
 * Process expired rentals - mark as expired and notify users
 * รัน hourly เพื่อตรวจสอบและ mark rentals ที่หมดอายุ
 */
class ProcessExpiredRentals extends Command
{
    protected $signature = 'rentals:process-expired';
    protected $description = 'Process expired rentals and notify users';

    public function handle(): int
    {
        $this->info('Processing expired rentals...');

        $expiredCount = 0;

        // Find all active rentals that have expired
        $expiredRentals = UserRental::with(['user', 'package'])
            ->where('status', 'active')
            ->where('ends_at', '<=', now())
            ->get();

        foreach ($expiredRentals as $rental) {
            try {
                // Mark as expired
                $rental->update(['status' => 'expired']);

                // Notify user
                if ($rental->user) {
                    $rental->user->notify(new RentalExpiredNotification($rental));
                }

                $expiredCount++;

                Log::info("Rental expired", [
                    'rental_id' => $rental->id,
                    'user_id' => $rental->user_id,
                    'package' => $rental->package->name ?? 'Unknown',
                ]);
            } catch (\Exception $e) {
                Log::error("Failed to process expired rental", [
                    'rental_id' => $rental->id,
                    'error' => $e->getMessage(),
                ]);
            }
        }

        $this->info("Processed {$expiredCount} expired rentals.");

        return self::SUCCESS;
    }
}
