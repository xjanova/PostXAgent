<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

class Campaign extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'brand_id',
        'name',
        'description',
        'type',
        'goal',
        'target_platforms',
        'content_themes',
        'posting_schedule',
        'ai_settings',
        'budget',
        'start_date',
        'end_date',
        'status',
    ];

    protected $casts = [
        'target_platforms' => 'array',
        'content_themes' => 'array',
        'posting_schedule' => 'array',
        'ai_settings' => 'array',
        'budget' => 'decimal:2',
        'start_date' => 'datetime',
        'end_date' => 'datetime',
    ];

    // Status constants
    const STATUS_DRAFT = 'draft';
    const STATUS_SCHEDULED = 'scheduled';
    const STATUS_ACTIVE = 'active';
    const STATUS_PAUSED = 'paused';
    const STATUS_COMPLETED = 'completed';
    const STATUS_CANCELLED = 'cancelled';

    // Relationships
    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function brand()
    {
        return $this->belongsTo(Brand::class);
    }

    public function posts()
    {
        return $this->hasMany(Post::class);
    }

    // Scopes
    public function scopeActive($query)
    {
        return $query->where('status', self::STATUS_ACTIVE);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->whereJsonContains('target_platforms', $platform);
    }

    // Helpers
    public function isActive(): bool
    {
        return $this->status === self::STATUS_ACTIVE;
    }

    public function getNextPostTime(): ?\DateTime
    {
        $schedule = $this->posting_schedule;

        if (!$schedule || empty($schedule['times'])) {
            return null;
        }

        // Logic to calculate next post time based on schedule
        // This would use the posting_schedule configuration
        return now()->addHour();
    }

    public function getAIPromptContext(): array
    {
        return [
            'campaign_name' => $this->name,
            'goal' => $this->goal,
            'themes' => $this->content_themes,
            'ai_settings' => $this->ai_settings,
            'brand' => $this->brand->toAIContext(),
        ];
    }
}
