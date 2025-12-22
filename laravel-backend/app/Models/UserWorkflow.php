<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

class UserWorkflow extends Model
{
    use HasFactory;

    protected $fillable = [
        'user_id',
        'template_id',
        'name',
        'description',
        'platforms',
        'workflow_json',
        'default_variables',
        'is_active',
        'run_count',
        'success_rate',
        'last_run_at',
    ];

    protected $casts = [
        'platforms' => 'array',
        'default_variables' => 'array',
        'is_active' => 'boolean',
        'run_count' => 'integer',
        'success_rate' => 'double',
        'last_run_at' => 'datetime',
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // RELATIONSHIPS
    // ═══════════════════════════════════════════════════════════════════════

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function template(): BelongsTo
    {
        return $this->belongsTo(WorkflowTemplate::class, 'template_id');
    }

    public function executions(): HasMany
    {
        return $this->hasMany(WorkflowExecution::class);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SCOPES
    // ═══════════════════════════════════════════════════════════════════════

    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeByUser($query, int $userId)
    {
        return $query->where('user_id', $userId);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->whereJsonContains('platforms', $platform);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Create a workflow from a template
     */
    public static function createFromTemplate(User $user, WorkflowTemplate $template, array $overrides = []): self
    {
        return static::create([
            'user_id' => $user->id,
            'template_id' => $template->id,
            'name' => $overrides['name'] ?? $template->name,
            'description' => $overrides['description'] ?? $template->description,
            'platforms' => $overrides['platforms'] ?? $template->supported_platforms,
            'workflow_json' => $overrides['workflow_json'] ?? $template->workflow_json,
            'default_variables' => $overrides['default_variables'] ?? [],
            'is_active' => true,
        ]);
    }

    /**
     * Record an execution
     */
    public function recordExecution(bool $successful): void
    {
        $this->run_count++;
        $this->last_run_at = now();

        // Update rolling success rate
        $currentRate = $this->success_rate;
        $newRate = (($currentRate * ($this->run_count - 1)) + ($successful ? 100 : 0)) / $this->run_count;
        $this->success_rate = $newRate;

        $this->save();
    }

    /**
     * Duplicate this workflow
     */
    public function duplicate(string $newName = null): self
    {
        return static::create([
            'user_id' => $this->user_id,
            'template_id' => $this->template_id,
            'name' => $newName ?? $this->name . ' (copy)',
            'description' => $this->description,
            'platforms' => $this->platforms,
            'workflow_json' => $this->workflow_json,
            'default_variables' => $this->default_variables,
            'is_active' => false,
        ]);
    }

    /**
     * Parse and get workflow nodes
     */
    public function getNodes(): array
    {
        $workflow = json_decode($this->workflow_json, true);
        return $workflow['nodes'] ?? [];
    }

    /**
     * Parse and get workflow connections
     */
    public function getConnections(): array
    {
        $workflow = json_decode($this->workflow_json, true);
        return $workflow['connections'] ?? [];
    }

    /**
     * Update workflow JSON
     */
    public function updateWorkflow(array $nodes, array $connections): void
    {
        $workflow = json_decode($this->workflow_json, true) ?? [];
        $workflow['nodes'] = $nodes;
        $workflow['connections'] = $connections;
        $this->workflow_json = json_encode($workflow);
        $this->save();
    }
}
