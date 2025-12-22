<?php

declare(strict_types=1);

namespace App\Jobs;

use App\Models\WorkflowExecution;
use App\Models\UserWorkflow;
use App\Models\WorkflowTemplate;
use App\Services\AIManagerService;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Log;

class ExecuteWorkflowJob implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public int $tries = 2;
    public int $timeout = 600; // 10 minutes max
    public int $backoff = 30;

    public function __construct(
        public WorkflowExecution $execution
    ) {}

    public function handle(AIManagerService $aiManager): void
    {
        Log::info("Executing workflow for execution #{$this->execution->id}");

        $startTime = microtime(true);

        try {
            // Update status to running
            $this->execution->update([
                'status' => 'running',
                'started_at' => now(),
            ]);

            // Get workflow JSON
            $workflowJson = $this->getWorkflowJson();
            if (!$workflowJson) {
                throw new \Exception('Workflow JSON not found');
            }

            // Parse workflow
            $workflow = json_decode($workflowJson, true);
            if (json_last_error() !== JSON_ERROR_NONE) {
                throw new \Exception('Invalid workflow JSON');
            }

            // Execute workflow via AI Manager
            $result = $aiManager->executeWorkflow([
                'workflowJson' => $workflowJson,
                'variables' => $this->execution->variables ?? [],
            ]);

            // Calculate duration
            $durationMs = (int) ((microtime(true) - $startTime) * 1000);

            if ($result['success']) {
                $this->execution->update([
                    'status' => 'completed',
                    'node_outputs' => $result['nodeOutputs'] ?? null,
                    'execution_log' => $result['log'] ?? null,
                    'nodes_executed' => $result['nodesExecuted'] ?? count($workflow['nodes'] ?? []),
                    'duration_ms' => $durationMs,
                    'completed_at' => now(),
                ]);

                // Update workflow statistics
                $this->updateWorkflowStats(true);

                Log::info("Workflow execution #{$this->execution->id} completed successfully");
            } else {
                throw new \Exception($result['error'] ?? 'Workflow execution failed');
            }

        } catch (\Exception $e) {
            $durationMs = (int) ((microtime(true) - $startTime) * 1000);

            $this->execution->update([
                'status' => 'failed',
                'error_message' => $e->getMessage(),
                'duration_ms' => $durationMs,
                'completed_at' => now(),
            ]);

            // Update workflow statistics
            $this->updateWorkflowStats(false);

            Log::error("Workflow execution #{$this->execution->id} failed: {$e->getMessage()}");

            throw $e;
        }
    }

    /**
     * Get workflow JSON from user workflow or template
     */
    private function getWorkflowJson(): ?string
    {
        if ($this->execution->userWorkflow) {
            return $this->execution->userWorkflow->workflow_json;
        }

        if ($this->execution->template) {
            return $this->execution->template->workflow_json;
        }

        return null;
    }

    /**
     * Update workflow/template statistics
     */
    private function updateWorkflowStats(bool $successful): void
    {
        if ($this->execution->userWorkflow) {
            $this->execution->userWorkflow->recordExecution($successful);
        }

        if ($this->execution->template) {
            $this->execution->template->updateSuccessRate($successful);
        }
    }

    /**
     * Handle job failure
     */
    public function failed(\Throwable $exception): void
    {
        Log::error("WorkflowExecution #{$this->execution->id} permanently failed: {$exception->getMessage()}");

        $this->execution->update([
            'status' => 'failed',
            'error_message' => $exception->getMessage(),
            'completed_at' => now(),
        ]);

        $this->updateWorkflowStats(false);
    }
}
