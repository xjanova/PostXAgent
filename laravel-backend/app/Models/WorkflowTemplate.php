<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

class WorkflowTemplate extends Model
{
    use HasFactory;

    protected $fillable = [
        'name',
        'name_th',
        'description',
        'description_th',
        'category',
        'icon',
        'supported_platforms',
        'variables',
        'workflow_json',
        'is_system',
        'is_active',
        'use_count',
        'avg_success_rate',
        'created_by',
    ];

    protected $casts = [
        'supported_platforms' => 'array',
        'variables' => 'array',
        'is_system' => 'boolean',
        'is_active' => 'boolean',
        'use_count' => 'integer',
        'avg_success_rate' => 'double',
    ];

    // ═══════════════════════════════════════════════════════════════════════
    // RELATIONSHIPS
    // ═══════════════════════════════════════════════════════════════════════

    public function creator(): BelongsTo
    {
        return $this->belongsTo(User::class, 'created_by');
    }

    public function userWorkflows(): HasMany
    {
        return $this->hasMany(UserWorkflow::class, 'template_id');
    }

    public function seekAndPostTasks(): HasMany
    {
        return $this->hasMany(SeekAndPostTask::class);
    }

    public function executions(): HasMany
    {
        return $this->hasMany(WorkflowExecution::class, 'template_id');
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SCOPES
    // ═══════════════════════════════════════════════════════════════════════

    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeSystem($query)
    {
        return $query->where('is_system', true);
    }

    public function scopeCustom($query)
    {
        return $query->where('is_system', false);
    }

    public function scopeByCategory($query, string $category)
    {
        return $query->where('category', $category);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->whereJsonContains('supported_platforms', $platform);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    public function incrementUseCount(): void
    {
        $this->increment('use_count');
    }

    public function updateSuccessRate(bool $successful): void
    {
        $totalExecutions = $this->use_count;
        $currentRate = $this->avg_success_rate;

        if ($totalExecutions === 0) {
            $this->avg_success_rate = $successful ? 100 : 0;
        } else {
            // Rolling average
            $newRate = (($currentRate * $totalExecutions) + ($successful ? 100 : 0)) / ($totalExecutions + 1);
            $this->avg_success_rate = $newRate;
        }

        $this->save();
    }

    public function getLocalizedName(): string
    {
        $locale = app()->getLocale();
        return $locale === 'th' && $this->name_th ? $this->name_th : $this->name;
    }

    public function getLocalizedDescription(): ?string
    {
        $locale = app()->getLocale();
        return $locale === 'th' && $this->description_th ? $this->description_th : $this->description;
    }

    public function supportsPlatform(string $platform): bool
    {
        return in_array($platform, $this->supported_platforms ?? []);
    }
}
