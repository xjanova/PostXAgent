<?php

namespace App\Console;

use Illuminate\Console\Scheduling\Schedule;
use Illuminate\Foundation\Console\Kernel as ConsoleKernel;

class Kernel extends ConsoleKernel
{
    /**
     * Define the application's command schedule.
     *
     * Note: License/Rental management is now handled by xmanstudio external API
     * See: https://github.com/xjanova/xmanstudio
     */
    protected function schedule(Schedule $schedule): void
    {
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
