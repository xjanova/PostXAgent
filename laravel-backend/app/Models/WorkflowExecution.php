<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class WorkflowExecution extends Model
{
    use HasFactory;

    // Status constants
    public const STATUS_PENDING = 'pending';
    public const STATUS_RUNNING = 'running';
    public const STATUS_SUCCESS = 'success';
    public const STATUS_COMPLETED = 'completed';
    public const STATUS_FAILED = 'failed';
    public const STATUS_CANCELLED = 'cancelled';

    protected $fillable = [
        // Legacy fields (for web learning)
        'learned_workflow_id',
        'brand_id',
        'post_id',
        'failed_at_step',
        'error_screenshot',
        'step_results',
        'content_used',
        'metadata',

        // New fields (for node workflow editor)
        'user_id',
        'user_workflow_id',
        'template_id',
        'status',
        'variables',
        'node_outputs',
        'error_message',
        'execution_log',
        'nodes_executed',
        'total_nodes',
        'duration_ms',
        'started_at',
        'completed_at',
    ];

    protected $casts = [
        'started_at' => 'datetime',
        'completed_at' => 'datetime',
        'failed_at_step' => 'integer',
        'step_results' => 'array',
        'content_used' => 'array',
        'duration_ms' => 'integer',
        'metadata' => 'array',
        // New casts
        'variables' => 'array',
        'node_outputs' => 'array',
        'execution_log' => 'array',
        'nodes_executed' => 'integer',
        'total_nodes' => 'integer',
    ];

    // Relationships
    public function workflow(): BelongsTo
    {
        return $this->belongsTo(LearnedWorkflow::class, 'learned_workflow_id');
    }

    public function userWorkflow(): BelongsTo
    {
        return $this->belongsTo(UserWorkflow::class);
    }

    public function template(): BelongsTo
    {
        return $this->belongsTo(WorkflowTemplate::class, 'template_id');
    }

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function brand(): BelongsTo
    {
        return $this->belongsTo(Brand::class);
    }

    public function post(): BelongsTo
    {
        return $this->belongsTo(Post::class);
    }

    // Scopes
    public function scopeSuccessful($query)
    {
        return $query->where('status', self::STATUS_SUCCESS);
    }

    public function scopeFailed($query)
    {
        return $query->where('status', self::STATUS_FAILED);
    }

    public function scopeRecent($query, int $hours = 24)
    {
        return $query->where('created_at', '>=', now()->subHours($hours));
    }

    // Helpers
    public function isSuccess(): bool
    {
        return $this->status === self::STATUS_SUCCESS;
    }

    public function start(): void
    {
        $this->update([
            'status' => self::STATUS_RUNNING,
            'started_at' => now(),
        ]);
    }

    public function complete(): void
    {
        $this->update([
            'status' => self::STATUS_SUCCESS,
            'completed_at' => now(),
            'duration_ms' => $this->started_at
                ? now()->diffInMilliseconds($this->started_at)
                : null,
        ]);

        $this->workflow->recordSuccess();
    }

    public function fail(int $step, string $error, ?string $screenshot = null): void
    {
        $this->update([
            'status' => self::STATUS_FAILED,
            'completed_at' => now(),
            'failed_at_step' => $step,
            'error_message' => $error,
            'error_screenshot' => $screenshot,
            'duration_ms' => $this->started_at
                ? now()->diffInMilliseconds($this->started_at)
                : null,
        ]);

        $this->workflow->recordFailure();
    }

    public function addStepResult(int $stepOrder, bool $success, ?string $error = null): void
    {
        $results = $this->step_results ?? [];
        $results[] = [
            'step' => $stepOrder,
            'success' => $success,
            'error' => $error,
            'timestamp' => now()->toIso8601String(),
        ];
        $this->update(['step_results' => $results]);
    }

    public function getDurationSeconds(): ?float
    {
        return $this->duration_ms ? $this->duration_ms / 1000 : null;
    }

    public function getSuccessfulStepsCount(): int
    {
        return collect($this->step_results ?? [])->where('success', true)->count();
    }

    public function getTotalStepsCount(): int
    {
        return count($this->step_results ?? []);
    }
}
