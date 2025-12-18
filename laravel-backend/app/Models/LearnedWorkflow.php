<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class LearnedWorkflow extends Model
{
    use HasFactory;

    // Status constants
    const STATUS_ACTIVE = 'active';
    const STATUS_INACTIVE = 'inactive';
    const STATUS_LEARNING = 'learning';
    const STATUS_DEPRECATED = 'deprecated';

    // Learned source constants
    const SOURCE_MANUAL = 'manual';
    const SOURCE_AI_OBSERVED = 'ai_observed';
    const SOURCE_AUTO_RECOVERED = 'auto_recovered';
    const SOURCE_IMPORTED = 'imported';

    protected $fillable = [
        'user_id',
        'platform',
        'name',
        'description',
        'version',
        'status',
        'confidence_score',
        'success_count',
        'failure_count',
        'last_success_at',
        'last_failure_at',
        'is_active',
        'ai_manager_id',
        'metadata',
    ];

    protected $casts = [
        'confidence_score' => 'decimal:4',
        'success_count' => 'integer',
        'failure_count' => 'integer',
        'version' => 'integer',
        'is_active' => 'boolean',
        'last_success_at' => 'datetime',
        'last_failure_at' => 'datetime',
        'metadata' => 'array',
    ];

    protected $attributes = [
        'version' => 1,
        'confidence_score' => 0.5,
        'success_count' => 0,
        'failure_count' => 0,
        'is_active' => true,
        'status' => self::STATUS_LEARNING,
    ];

    // Relationships
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function steps(): HasMany
    {
        return $this->hasMany(WorkflowStep::class)->orderBy('order');
    }

    public function executions(): HasMany
    {
        return $this->hasMany(WorkflowExecution::class);
    }

    // Scopes
    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeWithHighConfidence($query, float $minConfidence = 0.7)
    {
        return $query->where('confidence_score', '>=', $minConfidence);
    }

    // Helpers
    public function getSuccessRate(): float
    {
        $total = $this->success_count + $this->failure_count;
        if ($total === 0) {
            return 0.0;
        }
        return round(($this->success_count / $total) * 100, 2);
    }

    public function recordSuccess(): void
    {
        $this->increment('success_count');
        $this->update([
            'last_success_at' => now(),
            'confidence_score' => min(1.0, $this->confidence_score + 0.02),
        ]);
    }

    public function recordFailure(): void
    {
        $this->increment('failure_count');
        $this->update([
            'last_failure_at' => now(),
            'confidence_score' => max(0.1, $this->confidence_score - 0.05),
        ]);

        // Deactivate if success rate is too low
        if ($this->getSuccessRate() < 30 && ($this->success_count + $this->failure_count) >= 10) {
            $this->update(['is_active' => false, 'status' => self::STATUS_INACTIVE]);
        }
    }

    public function needsRelearning(): bool
    {
        return $this->getSuccessRate() < 50 &&
               ($this->success_count + $this->failure_count) >= 5;
    }

    public function clone(): self
    {
        $newWorkflow = $this->replicate();
        $newWorkflow->version = $this->version + 1;
        $newWorkflow->success_count = 0;
        $newWorkflow->failure_count = 0;
        $newWorkflow->confidence_score = $this->confidence_score * 0.8;
        $newWorkflow->save();

        foreach ($this->steps as $step) {
            $newStep = $step->replicate();
            $newStep->learned_workflow_id = $newWorkflow->id;
            $newStep->save();
        }

        return $newWorkflow;
    }
}
