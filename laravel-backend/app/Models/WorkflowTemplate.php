<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * @property int $id
 * @property string $name
 * @property string|null $name_th
 * @property string|null $description
 * @property string|null $description_th
 * @property string|null $category
 * @property string|null $icon
 * @property array|null $supported_platforms
 * @property array|null $variables
 * @property array|null $workflow_json
 * @property bool $is_system
 * @property bool $is_active
 * @property int $use_count
 * @property float $avg_success_rate
 * @property int|null $created_by
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property-read \App\Models\User|null $creator
 * @property-read \Illuminate\Database\Eloquent\Collection<int, \App\Models\UserWorkflow> $userWorkflows
 * @property-read \Illuminate\Database\Eloquent\Collection<int, \App\Models\SeekAndPostTask> $seekAndPostTasks
 * @property-read \Illuminate\Database\Eloquent\Collection<int, \App\Models\WorkflowExecution> $executions
 *
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate where($column, $operator = null, $value = null, $boolean = 'and')
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate create(array $attributes = [])
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate find($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate findOrFail($id, $columns = ['*'])
 * @method static int count(string $columns = '*')
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate selectRaw(string $expression, array $bindings = [])
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate orderByDesc(string|\Illuminate\Contracts\Database\Query\Expression $column)
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate active()
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate system()
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate custom()
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate byCategory(string $category)
 * @method static \Illuminate\Database\Eloquent\Builder|WorkflowTemplate forPlatform(string $platform)
 */
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
        'workflow_json' => 'array',
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
