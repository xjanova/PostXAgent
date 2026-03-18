<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\HasMany;

/**
 * @property int $id
 * @property int $user_id
 * @property int|null $brand_id
 * @property string $name
 * @property string|null $description
 * @property bool $is_active
 * @property string $content_type
 * @property string $language
 * @property string $tone
 * @property string|null $content_prompt_template
 * @property int $scene_count
 * @property string $image_style
 * @property int $image_width
 * @property int $image_height
 * @property string $image_provider
 * @property int $video_duration_seconds
 * @property string $aspect_ratio
 * @property string $video_provider
 * @property int $clip_duration_seconds
 * @property int|null $freepik_account_pool_id
 * @property string $tts_provider
 * @property string $tts_voice_id
 * @property string $tts_voice_gender
 * @property bool $enable_lip_sync
 * @property string $lip_sync_provider
 * @property string|null $lip_sync_avatar_path
 * @property bool $enable_music
 * @property string $music_style
 * @property float $music_volume
 * @property string $music_source
 * @property array $target_platforms
 * @property bool $auto_publish
 * @property int|null $publish_account_pool_id
 * @property string $schedule_type
 * @property int $interval_hours
 * @property string|null $cron_expression
 * @property int $max_runs_per_day
 * @property \Illuminate\Support\Carbon|null $next_run_at
 * @property int|null $text_pool_id
 * @property int|null $tts_pool_id
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 * @property-read Brand|null $brand
 * @property-read \Illuminate\Database\Eloquent\Collection<int, PipelineRun> $runs
 * @property-read ApiKeyPool|null $textPool
 * @property-read ApiKeyPool|null $ttsPool
 * @property-read AccountPool|null $publishAccountPool
 * @property-read AccountPool|null $freepikAccountPool
 *
 * @method static \Illuminate\Database\Eloquent\Builder|static active()
 * @method static \Illuminate\Database\Eloquent\Builder|static scheduled()
 *
 * @mixin \Illuminate\Database\Eloquent\Builder<static>
 */
class ContentPipeline extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id', 'brand_id', 'name', 'description', 'is_active',
        // Content
        'content_type', 'language', 'tone', 'content_prompt_template', 'scene_count',
        // Image
        'image_style', 'image_width', 'image_height', 'image_provider',
        // Video
        'video_duration_seconds', 'aspect_ratio', 'video_provider', 'clip_duration_seconds',
        'freepik_account_pool_id',
        // Audio
        'tts_provider', 'tts_voice_id', 'tts_voice_gender',
        'enable_lip_sync', 'lip_sync_provider', 'lip_sync_avatar_path',
        // Music
        'enable_music', 'music_style', 'music_volume', 'music_source',
        // Publishing
        'target_platforms', 'auto_publish', 'publish_account_pool_id',
        // Scheduling
        'schedule_type', 'interval_hours', 'cron_expression', 'max_runs_per_day', 'next_run_at',
        // API Key Pools
        'text_pool_id', 'tts_pool_id',
    ];

    protected $casts = [
        'is_active' => 'boolean',
        'scene_count' => 'integer',
        'image_width' => 'integer',
        'image_height' => 'integer',
        'video_duration_seconds' => 'integer',
        'clip_duration_seconds' => 'integer',
        'enable_lip_sync' => 'boolean',
        'enable_music' => 'boolean',
        'music_volume' => 'float',
        'target_platforms' => 'array',
        'auto_publish' => 'boolean',
        'interval_hours' => 'integer',
        'max_runs_per_day' => 'integer',
        'next_run_at' => 'datetime',
    ];

    // Relationships
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function brand(): BelongsTo
    {
        return $this->belongsTo(Brand::class);
    }

    public function runs(): HasMany
    {
        return $this->hasMany(PipelineRun::class, 'content_pipeline_id');
    }

    public function textPool(): BelongsTo
    {
        return $this->belongsTo(ApiKeyPool::class, 'text_pool_id');
    }

    public function ttsPool(): BelongsTo
    {
        return $this->belongsTo(ApiKeyPool::class, 'tts_pool_id');
    }

    public function publishAccountPool(): BelongsTo
    {
        return $this->belongsTo(AccountPool::class, 'publish_account_pool_id');
    }

    public function freepikAccountPool(): BelongsTo
    {
        return $this->belongsTo(AccountPool::class, 'freepik_account_pool_id');
    }

    // Query Scopes
    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeScheduled($query)
    {
        return $query->where('is_active', true)
            ->where('schedule_type', '!=', 'manual')
            ->whereNotNull('next_run_at')
            ->where('next_run_at', '<=', now());
    }

    // Helpers
    public function getRunsToday(): int
    {
        return $this->runs()
            ->whereDate('created_at', today())
            ->count();
    }

    public function canRunToday(): bool
    {
        return $this->getRunsToday() < $this->max_runs_per_day;
    }

    public function getLastRun(): ?PipelineRun
    {
        /** @var PipelineRun|null */
        return $this->runs()->latest()->first();
    }

    public function hasActiveRun(): bool
    {
        return $this->runs()
            ->whereNotIn('status', ['completed', 'failed', 'cancelled'])
            ->exists();
    }

    public function calculateNextRunAt(): ?\Illuminate\Support\Carbon
    {
        if ($this->schedule_type === 'manual') {
            return null;
        }

        if ($this->schedule_type === 'interval') {
            return now()->addHours($this->interval_hours);
        }

        if ($this->schedule_type === 'cron' && $this->cron_expression) {
            // ใช้ cron-expression library สำหรับ parse
            try {
                $cron = new \Cron\CronExpression($this->cron_expression);
                return \Illuminate\Support\Carbon::instance($cron->getNextRunDate());
            } catch (\Exception $e) {
                return now()->addHours(24);
            }
        }

        return null;
    }

    public function getSuccessRate(): float
    {
        $completed = $this->runs()->where('status', 'completed')->count();
        $total = $this->runs()->whereIn('status', ['completed', 'failed'])->count();

        if ($total === 0) {
            return 0.0;
        }

        return round(($completed / $total) * 100, 2);
    }
}
