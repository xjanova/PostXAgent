<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * @property int $id
 * @property int $content_pipeline_id
 * @property string $status
 * @property array|null $generated_story
 * @property array|null $generated_images
 * @property array|null $generated_video_clips
 * @property array|null $generated_voiceover
 * @property array|null $generated_lipsync
 * @property string|null $final_video_path
 * @property string|null $thumbnail_path
 * @property array|null $publish_results
 * @property int $current_step
 * @property int $total_steps
 * @property string|null $step_message
 * @property float $progress_percentage
 * @property \Illuminate\Support\Carbon|null $started_at
 * @property \Illuminate\Support\Carbon|null $completed_at
 * @property string|null $error_message
 * @property string|null $error_step
 * @property int $retry_count
 * @property array|null $api_keys_used
 * @property array|null $tokens_consumed
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 */
class PipelineRun extends Model
{
    use HasFactory;

    protected $fillable = [
        'content_pipeline_id',
        'status',
        'generated_story',
        'generated_images',
        'generated_video_clips',
        'generated_voiceover',
        'generated_lipsync',
        'final_video_path',
        'thumbnail_path',
        'publish_results',
        'current_step',
        'total_steps',
        'step_message',
        'progress_percentage',
        'started_at',
        'completed_at',
        'error_message',
        'error_step',
        'retry_count',
        'api_keys_used',
        'tokens_consumed',
    ];

    protected $casts = [
        'generated_story' => 'array',
        'generated_images' => 'array',
        'generated_video_clips' => 'array',
        'generated_voiceover' => 'array',
        'generated_lipsync' => 'array',
        'publish_results' => 'array',
        'current_step' => 'integer',
        'total_steps' => 'integer',
        'progress_percentage' => 'float',
        'started_at' => 'datetime',
        'completed_at' => 'datetime',
        'retry_count' => 'integer',
        'api_keys_used' => 'array',
        'tokens_consumed' => 'array',
    ];

    // Status constants
    const STATUS_PENDING = 'pending';
    const STATUS_STORY = 'story';
    const STATUS_IMAGES = 'images';
    const STATUS_VIDEO = 'video';
    const STATUS_VOICEOVER = 'voiceover';
    const STATUS_LIPSYNC = 'lipsync';
    const STATUS_ASSEMBLY = 'assembly';
    const STATUS_PUBLISHING = 'publishing';
    const STATUS_COMPLETED = 'completed';
    const STATUS_FAILED = 'failed';
    const STATUS_CANCELLED = 'cancelled';

    // Step labels (Thai)
    const STEP_LABELS = [
        'story' => 'สร้างเรื่องราว',
        'images' => 'สร้างภาพประกอบ',
        'video' => 'สร้างวิดีโอ',
        'voiceover' => 'สร้างเสียงบรรยาย',
        'lipsync' => 'ซิงค์เสียงปาก',
        'assembly' => 'ประกอบวิดีโอ',
        'publishing' => 'โพสต์เผยแพร่',
    ];

    public static function getStepOrder(): array
    {
        return ['story', 'images', 'video', 'voiceover', 'lipsync', 'assembly', 'publishing'];
    }

    // Relationships
    public function pipeline(): BelongsTo
    {
        return $this->belongsTo(ContentPipeline::class, 'content_pipeline_id');
    }

    // Helpers
    public function isRunning(): bool
    {
        return !in_array($this->status, [
            self::STATUS_COMPLETED,
            self::STATUS_FAILED,
            self::STATUS_CANCELLED,
            self::STATUS_PENDING,
        ]);
    }

    public function isFinished(): bool
    {
        return in_array($this->status, [
            self::STATUS_COMPLETED,
            self::STATUS_FAILED,
            self::STATUS_CANCELLED,
        ]);
    }

    public function canRetry(): bool
    {
        return $this->status === self::STATUS_FAILED;
    }

    public function canCancel(): bool
    {
        return $this->isRunning();
    }

    public function updateProgress(string $step, string $message, ?float $percentage = null): void
    {
        $steps = self::getStepOrder();
        $stepIndex = array_search($step, $steps);

        $this->update([
            'status' => $step,
            'current_step' => $stepIndex !== false ? $stepIndex + 1 : $this->current_step,
            'step_message' => $message,
            'progress_percentage' => $percentage ?? (($stepIndex !== false)
                ? round((($stepIndex) / count($steps)) * 100, 1)
                : $this->progress_percentage),
        ]);
    }

    public function markCompleted(): void
    {
        $this->update([
            'status' => self::STATUS_COMPLETED,
            'current_step' => $this->total_steps,
            'step_message' => 'เสร็จสมบูรณ์',
            'progress_percentage' => 100,
            'completed_at' => now(),
        ]);
    }

    public function markFailed(string $step, string $error): void
    {
        $this->update([
            'status' => self::STATUS_FAILED,
            'error_step' => $step,
            'error_message' => $error,
            'completed_at' => now(),
        ]);
    }

    public function markCancelled(): void
    {
        $this->update([
            'status' => self::STATUS_CANCELLED,
            'step_message' => 'ยกเลิกแล้ว',
            'completed_at' => now(),
        ]);
    }

    public function getDurationSeconds(): ?int
    {
        if (!$this->started_at) {
            return null;
        }

        $end = $this->completed_at ?? now();
        return (int) $this->started_at->diffInSeconds($end);
    }

    public function recordApiKeyUsage(string $provider, string $keyLabel): void
    {
        $used = $this->api_keys_used ?? [];
        $used[$provider] = $keyLabel;
        $this->update(['api_keys_used' => $used]);
    }

    public function recordTokensConsumed(string $provider, int $tokens): void
    {
        $consumed = $this->tokens_consumed ?? [];
        $consumed[$provider] = ($consumed[$provider] ?? 0) + $tokens;
        $this->update(['tokens_consumed' => $consumed]);
    }
}
