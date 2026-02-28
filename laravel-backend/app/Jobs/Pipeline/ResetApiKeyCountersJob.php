<?php

declare(strict_types=1);

namespace App\Jobs\Pipeline;

use App\Services\ApiKeyRotationService;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Log;

class ResetApiKeyCountersJob implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public int $tries = 3;
    public int $timeout = 60;

    public function handle(ApiKeyRotationService $service): void
    {
        $count = $service->resetDailyCounters();
        Log::info("Daily API key counters reset", ['keys_reset' => $count]);
    }
}
