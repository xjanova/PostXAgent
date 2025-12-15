<?php

namespace App\Console\Commands;

use App\Services\AccountRotationService;
use Illuminate\Console\Command;

class ResetDailyAccountCounters extends Command
{
    protected $signature = 'accounts:reset-daily-counters';
    protected $description = 'Reset daily post counters for all account pool members';

    public function __construct(
        private AccountRotationService $rotationService
    ) {
        parent::__construct();
    }

    public function handle(): int
    {
        $updated = $this->rotationService->resetDailyCounters();

        $this->info("Reset daily counters for {$updated} account(s)");

        return self::SUCCESS;
    }
}
