<?php

namespace App\Console;

use Illuminate\Console\Scheduling\Schedule;
use Illuminate\Foundation\Console\Kernel as ConsoleKernel;

class Kernel extends ConsoleKernel
{
    /**
     * Define the application's command schedule.
     */
    protected function schedule(Schedule $schedule): void
    {
        // === Rental Management ===
        // Process expired rentals - mark as expired and notify
        $schedule->command('rentals:process-expired')
            ->hourly()
            ->withoutOverlapping()
            ->runInBackground();

        // Send expiry reminders (3 days, 1 day, today)
        $schedule->command('rentals:send-expiry-reminders')
            ->dailyAt('09:00')
            ->withoutOverlapping();

        // Cancel pending payments older than 24 hours
        $schedule->command('payments:cancel-stale --hours=24')
            ->hourly()
            ->withoutOverlapping();

        // === Account Management ===
        // Reset daily post counters for social accounts
        $schedule->command('accounts:reset-daily-counters')
            ->dailyAt('00:00')
            ->withoutOverlapping();

        // === Cleanup ===
        // Clean up old logs (Laravel Telescope, etc.)
        // $schedule->command('telescope:prune --hours=48')->daily();
    }

    /**
     * Register the commands for the application.
     */
    protected function commands(): void
    {
        $this->load(__DIR__.'/Commands');

        require base_path('routes/console.php');
    }
}
