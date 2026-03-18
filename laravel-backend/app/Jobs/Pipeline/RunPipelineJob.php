<?php

declare(strict_types=1);

namespace App\Jobs\Pipeline;

use App\Models\PipelineRun;
use App\Services\ContentPipelineService;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Log;

class RunPipelineJob implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public int $tries = 1;
    public int $timeout = 3600; // 1 hour max
    public int $maxExceptions = 1;

    public function __construct(
        public PipelineRun $run
    ) {
        $this->queue = 'pipeline';
    }

    public function handle(ContentPipelineService $service): void
    {
        $run = $this->run->fresh();
        if (!$run || $run->status === PipelineRun::STATUS_CANCELLED) {
            return;
        }

        $pipeline = $run->pipeline;
        /** @phpstan-ignore booleanNot.alwaysFalse */
        if (!$pipeline) {
            $run->markFailed('pending', 'Pipeline configuration not found');
            return;
        }

        $steps = PipelineRun::getStepOrder();
        $startFromStep = $this->getStartStep($run);
        $startIndex = array_search($startFromStep, $steps);

        if ($startIndex === false) {
            $startIndex = 0;
        }

        Log::info('Pipeline job started', [
            'run_id' => $run->id,
            'pipeline_id' => $pipeline->id,
            'start_step' => $startFromStep,
        ]);

        for ($i = $startIndex; $i < count($steps); $i++) {
            $step = $steps[$i];

            // Check if cancelled
            $run->refresh();
            if ($run->status === PipelineRun::STATUS_CANCELLED) {
                Log::info('Pipeline cancelled during execution', ['run_id' => $run->id]);
                return;
            }

            // Skip lip sync if disabled
            if ($step === 'lipsync' && !$pipeline->enable_lip_sync) {
                continue;
            }

            // Skip publishing if disabled
            if ($step === 'publishing' && !$pipeline->auto_publish) {
                continue;
            }

            try {
                $method = 'execute' . ucfirst($step) . 'Step';
                $service->$method($run);
            } catch (\Exception $e) {
                Log::error("Pipeline step '{$step}' failed", [
                    'run_id' => $run->id,
                    'step' => $step,
                    'error' => $e->getMessage(),
                ]);

                $run->markFailed($step, $e->getMessage());
                return;
            }
        }

        $run->markCompleted();

        // Update next scheduled run
        if ($pipeline->is_active && $pipeline->schedule_type !== 'manual') {
            $pipeline->update([
                'next_run_at' => $pipeline->calculateNextRunAt(),
            ]);
        }

        Log::info('Pipeline completed', [
            'run_id' => $run->id,
            'duration_seconds' => $run->getDurationSeconds(),
        ]);
    }

    public function failed(\Throwable $exception): void
    {
        Log::error('Pipeline job failed with unhandled exception', [
            'run_id' => $this->run->id,
            'error' => $exception->getMessage(),
        ]);

        $steps = PipelineRun::getStepOrder();
        $currentStep = $this->run->error_step
            ?? (in_array($this->run->status, $steps) ? $this->run->status : 'unknown');

        $this->run->markFailed(
            $currentStep,
            $exception->getMessage()
        );
    }

    /**
     * Determine which step to start from (for retries)
     */
    private function getStartStep(PipelineRun $run): string
    {
        // If retrying, start from the failed step
        if ($run->error_step && $run->retry_count > 0) {
            return $run->error_step;
        }

        // If the run has already started, determine current step
        if ($run->status !== PipelineRun::STATUS_PENDING) {
            $steps = PipelineRun::getStepOrder();
            $currentIndex = array_search($run->status, $steps);
            if ($currentIndex !== false) {
                return $steps[$currentIndex];
            }
        }

        return 'story';
    }
}
