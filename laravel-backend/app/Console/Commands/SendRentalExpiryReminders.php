<?php

declare(strict_types=1);

namespace App\Console\Commands;

use Illuminate\Console\Command;
use App\Models\UserRental;
use App\Notifications\RentalExpiringNotification;
use Illuminate\Support\Facades\Log;

/**
 * Send expiry reminders for rentals about to expire
 * รัน daily เพื่อส่งการแจ้งเตือนให้ผู้ใช้ที่แพ็กเกจใกล้หมดอายุ
 */
class SendRentalExpiryReminders extends Command
{
    protected $signature = 'rentals:send-expiry-reminders';
    protected $description = 'Send reminders for rentals expiring soon (3 days, 1 day)';

    public function handle(): int
    {
        $this->info('Sending rental expiry reminders...');

        $remindersSent = 0;

        // Rentals expiring in 3 days
        $expiringIn3Days = UserRental::with(['user', 'package'])
            ->where('status', 'active')
            ->whereDate('ends_at', now()->addDays(3)->toDateString())
            ->get();

        foreach ($expiringIn3Days as $rental) {
            $this->sendReminder($rental, 3);
            $remindersSent++;
        }

        // Rentals expiring in 1 day
        $expiringIn1Day = UserRental::with(['user', 'package'])
            ->where('status', 'active')
            ->whereDate('ends_at', now()->addDay()->toDateString())
            ->get();

        foreach ($expiringIn1Day as $rental) {
            $this->sendReminder($rental, 1);
            $remindersSent++;
        }

        // Rentals expiring today
        $expiringToday = UserRental::with(['user', 'package'])
            ->where('status', 'active')
            ->whereDate('ends_at', now()->toDateString())
            ->get();

        foreach ($expiringToday as $rental) {
            $this->sendReminder($rental, 0);
            $remindersSent++;
        }

        $this->info("Sent {$remindersSent} expiry reminders.");

        return self::SUCCESS;
    }

    private function sendReminder(UserRental $rental, int $daysRemaining): void
    {
        try {
            if ($rental->user) {
                $rental->user->notify(new RentalExpiringNotification($rental, $daysRemaining));
            }

            Log::info("Sent expiry reminder", [
                'rental_id' => $rental->id,
                'user_id' => $rental->user_id,
                'days_remaining' => $daysRemaining,
            ]);
        } catch (\Exception $e) {
            Log::error("Failed to send expiry reminder", [
                'rental_id' => $rental->id,
                'error' => $e->getMessage(),
            ]);
        }
    }
}
