<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\Relations\BelongsToMany;

class SeekAndPostTask extends Model
{
    use HasFactory;

    protected $fillable = [
        'user_id',
        'brand_id',
        'name',
        'description',
        'status',
        'platform',
        'target_keywords',
        'exclude_keywords',
        'min_group_members',
        'max_group_members',
        'min_quality_score',
        'max_groups_to_discover',
        'max_groups_to_join_per_day',
        'auto_join',
        'workflow_template_id',
        'workflow_variables',
        'posts_per_group_per_day',
        'max_posts_per_day',
        'posting_schedule',
        'smart_timing',
        'content_template',
        'content_variations',
        'vary_content',
        'media_urls',
        'groups_discovered',
        'groups_joined',
        'posts_made',
        'posts_successful',
        'posts_failed',
        'last_seek_at',
        'last_post_at',
        'is_recurring',
        'recurrence_pattern',
        'scheduled_at',
        'started_at',
        'completed_at',
    ];

    protected $casts = [
        'target_keywords' => 'array',
        'exclude_keywords' => 'array',
        'workflow_variables' => 'array',
        'posting_schedule' => 'array',
        'content_variations' => 'array',
        'media_urls' => 'array',
        'min_group_members' => 'integer',
        'max_group_members' => 'integer',
        'min_quality_score' => 'double',
        'max_groups_to_discover' => 'integer',
        'max_groups_to_join_per_day' => 'integer',
        'auto_join' => 'boolean',
        'posts_per_group_per_day' => 'integer',
        'max_posts_per_day' => 'integer',
        'smart_timing' => 'boolean',
        'vary_content' => 'boolean',
        'groups_discovered' => 'integer',
        'groups_joined' => 'integer',
        'posts_made' => 'integer',
        'posts_successful' => 'integer',
        'posts_failed' => 'integer',
        'is_recurring' => 'boolean',
        'last_seek_at' => 'datetime',
        'last_post_at' => 'datetime',
        'scheduled_at' => 'datetime',
        'started_at' => 'datetime',
        'completed_at' => 'datetime',
    ];

    public const STATUS_PENDING = 'pending';
    public const STATUS_SEEKING = 'seeking';
    public const STATUS_JOINING = 'joining';
    public const STATUS_POSTING = 'posting';
    public const STATUS_COMPLETED = 'completed';
    public const STATUS_PAUSED = 'paused';
    public const STATUS_FAILED = 'failed';

    // ═══════════════════════════════════════════════════════════════════════
    // RELATIONSHIPS
    // ═══════════════════════════════════════════════════════════════════════

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function brand(): BelongsTo
    {
        return $this->belongsTo(Brand::class);
    }

    public function workflowTemplate(): BelongsTo
    {
        return $this->belongsTo(WorkflowTemplate::class);
    }

    public function discoveredGroups(): BelongsToMany
    {
        return $this->belongsToMany(DiscoveredGroup::class, 'seek_task_groups')
            ->withPivot(['status', 'posts_in_group', 'last_post_at'])
            ->withTimestamps();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SCOPES
    // ═══════════════════════════════════════════════════════════════════════

    public function scopeActive($query)
    {
        return $query->whereIn('status', [
            self::STATUS_SEEKING,
            self::STATUS_JOINING,
            self::STATUS_POSTING,
        ]);
    }

    public function scopePending($query)
    {
        return $query->where('status', self::STATUS_PENDING);
    }

    public function scopeByUser($query, int $userId)
    {
        return $query->where('user_id', $userId);
    }

    public function scopeByPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    public function scopeScheduledNow($query)
    {
        return $query->where('status', self::STATUS_PENDING)
            ->where(function ($q) {
                $q->whereNull('scheduled_at')
                    ->orWhere('scheduled_at', '<=', now());
            });
    }

    // ═══════════════════════════════════════════════════════════════════════
    // METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Start the task
     */
    public function start(): void
    {
        $this->status = self::STATUS_SEEKING;
        $this->started_at = now();
        $this->save();
    }

    /**
     * Pause the task
     */
    public function pause(): void
    {
        $this->status = self::STATUS_PAUSED;
        $this->save();
    }

    /**
     * Resume the task
     */
    public function resume(): void
    {
        // Resume to the appropriate phase
        if ($this->groups_discovered < $this->max_groups_to_discover) {
            $this->status = self::STATUS_SEEKING;
        } elseif ($this->groups_joined < $this->discoveredGroups()->count()) {
            $this->status = self::STATUS_JOINING;
        } else {
            $this->status = self::STATUS_POSTING;
        }
        $this->save();
    }

    /**
     * Mark as failed
     */
    public function fail(string $reason = null): void
    {
        $this->status = self::STATUS_FAILED;
        $this->save();

        // Log the failure reason
        if ($reason) {
            logger()->error("SeekAndPostTask {$this->id} failed: {$reason}");
        }
    }

    /**
     * Mark as completed
     */
    public function complete(): void
    {
        $this->status = self::STATUS_COMPLETED;
        $this->completed_at = now();
        $this->save();
    }

    /**
     * Record a group discovery
     */
    public function recordDiscovery(DiscoveredGroup $group): void
    {
        // Attach the group if not already attached
        if (!$this->discoveredGroups()->where('discovered_group_id', $group->id)->exists()) {
            $this->discoveredGroups()->attach($group->id, ['status' => 'discovered']);
            $this->increment('groups_discovered');
            $this->last_seek_at = now();
            $this->save();
        }
    }

    /**
     * Record a join request
     */
    public function recordJoinRequest(DiscoveredGroup $group): void
    {
        $this->discoveredGroups()->updateExistingPivot($group->id, [
            'status' => 'join_requested',
        ]);
    }

    /**
     * Record a successful join
     */
    public function recordJoin(DiscoveredGroup $group): void
    {
        $this->discoveredGroups()->updateExistingPivot($group->id, [
            'status' => 'joined',
        ]);
        $this->increment('groups_joined');
        $group->markAsJoined();
    }

    /**
     * Record a post
     */
    public function recordPost(DiscoveredGroup $group, bool $successful): void
    {
        $this->increment('posts_made');

        if ($successful) {
            $this->increment('posts_successful');
        } else {
            $this->increment('posts_failed');
        }

        $this->last_post_at = now();
        $this->save();

        // Update pivot
        $pivot = $this->discoveredGroups()
            ->where('discovered_group_id', $group->id)
            ->first()
            ->pivot ?? null;

        if ($pivot) {
            $this->discoveredGroups()->updateExistingPivot($group->id, [
                'status' => 'posted',
                'posts_in_group' => ($pivot->posts_in_group ?? 0) + 1,
                'last_post_at' => now(),
            ]);
        }

        // Record in the group itself
        $group->recordPost($successful);
    }

    /**
     * Get progress percentage
     */
    public function getProgressPercentage(): float
    {
        $totalSteps = $this->max_groups_to_discover + $this->max_posts_per_day;
        $completedSteps = $this->groups_discovered + $this->posts_made;

        return $totalSteps > 0 ? min(100, ($completedSteps / $totalSteps) * 100) : 0;
    }

    /**
     * Get success rate
     */
    public function getSuccessRate(): float
    {
        return $this->posts_made > 0
            ? ($this->posts_successful / $this->posts_made) * 100
            : 0;
    }

    /**
     * Check if can continue seeking
     */
    public function canSeek(): bool
    {
        return $this->groups_discovered < $this->max_groups_to_discover
            && !in_array($this->status, [self::STATUS_PAUSED, self::STATUS_FAILED, self::STATUS_COMPLETED]);
    }

    /**
     * Check if can continue posting
     */
    public function canPost(): bool
    {
        $todayPosts = $this->posts_made; // In real implementation, count today's posts only
        return $todayPosts < $this->max_posts_per_day
            && !in_array($this->status, [self::STATUS_PAUSED, self::STATUS_FAILED, self::STATUS_COMPLETED]);
    }

    /**
     * Get groups ready for posting
     */
    public function getGroupsReadyForPosting(): \Illuminate\Database\Eloquent\Collection
    {
        return $this->discoveredGroups()
            ->wherePivot('status', 'joined')
            ->where('is_banned', false)
            ->orderByDesc('quality_score')
            ->get()
            ->filter(fn($group) => $group->canPostNow());
    }
}
