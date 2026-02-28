<?php

declare(strict_types=1);

namespace App\Console\Commands;

use App\Services\ContentPipelineService;
use Illuminate\Console\Command;

class RunScheduledPipelines extends Command
{
    protected $signature = 'pipeline:run-scheduled';
    protected $description = 'Check and run scheduled content pipelines';

    public function handle(ContentPipelineService $service): int
    {
        $dispatched = $service->checkScheduledPipelines();

        if ($dispatched > 0) {
            $this->info("Dispatched {$dispatched} scheduled pipeline(s)");
        }

        return self::SUCCESS;
    }
}
