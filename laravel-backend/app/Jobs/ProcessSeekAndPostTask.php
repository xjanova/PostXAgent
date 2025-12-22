<?php

declare(strict_types=1);

namespace App\Jobs;

use App\Models\SeekAndPostTask;
use App\Models\DiscoveredGroup;
use App\Services\AIManagerService;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Log;

class ProcessSeekAndPostTask implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    public int $tries = 3;
    public int $timeout = 3600; // 1 hour max
    public int $backoff = 60;

    public function __construct(
        public SeekAndPostTask $task
    ) {}

    public function handle(AIManagerService $aiManager): void
    {
        Log::info("Processing SeekAndPostTask #{$this->task->id}: {$this->task->name}");

        try {
            switch ($this->task->status) {
                case SeekAndPostTask::STATUS_SEEKING:
                    $this->seekGroups($aiManager);
                    break;
                case SeekAndPostTask::STATUS_JOINING:
                    $this->joinGroups($aiManager);
                    break;
                case SeekAndPostTask::STATUS_POSTING:
                    $this->postToGroups($aiManager);
                    break;
                default:
                    Log::warning("Task #{$this->task->id} has unexpected status: {$this->task->status}");
                    return;
            }

            // Schedule next phase if needed
            $this->scheduleNextPhase();

        } catch (\Exception $e) {
            Log::error("SeekAndPostTask #{$this->task->id} failed: {$e->getMessage()}");
            $this->task->fail($e->getMessage());
            throw $e;
        }
    }

    /**
     * Phase 1: Seek and discover groups
     */
    private function seekGroups(AIManagerService $aiManager): void
    {
        Log::info("Seeking groups for task #{$this->task->id}");

        if (!$this->task->canSeek()) {
            $this->task->status = SeekAndPostTask::STATUS_JOINING;
            $this->task->save();
            return;
        }

        // Send seek request to AI Manager Core
        $result = $aiManager->seekGroups([
            'platform' => $this->task->platform,
            'keywords' => $this->task->target_keywords,
            'excludeKeywords' => $this->task->exclude_keywords,
            'minMembers' => $this->task->min_group_members,
            'maxMembers' => $this->task->max_group_members,
            'limit' => min(20, $this->task->max_groups_to_discover - $this->task->groups_discovered),
        ]);

        if (!$result['success']) {
            Log::warning("Group seek failed: " . ($result['error'] ?? 'Unknown error'));
            return;
        }

        // Process discovered groups
        foreach ($result['groups'] ?? [] as $groupData) {
            $group = DiscoveredGroup::updateOrCreate(
                [
                    'platform' => $this->task->platform,
                    'group_id' => $groupData['id'],
                ],
                [
                    'group_name' => $groupData['name'] ?? '',
                    'group_url' => $groupData['url'] ?? null,
                    'description' => $groupData['description'] ?? null,
                    'keywords' => array_unique(array_merge(
                        $groupData['keywords'] ?? [],
                        $this->task->target_keywords
                    )),
                    'member_count' => $groupData['memberCount'] ?? 0,
                    'activity_level' => $groupData['activityLevel'] ?? 'unknown',
                    'is_public' => $groupData['isPublic'] ?? true,
                    'requires_approval' => $groupData['requiresApproval'] ?? false,
                    'last_checked_at' => now(),
                ]
            );

            // Calculate quality score
            $group->calculateQualityScore();

            // Check if meets minimum quality
            if ($group->quality_score >= $this->task->min_quality_score) {
                $this->task->recordDiscovery($group);
            }
        }

        $this->task->last_seek_at = now();
        $this->task->save();

        // Check if we should move to joining phase
        if ($this->task->groups_discovered >= $this->task->max_groups_to_discover) {
            $this->task->status = SeekAndPostTask::STATUS_JOINING;
            $this->task->save();
        }
    }

    /**
     * Phase 2: Request to join discovered groups
     */
    private function joinGroups(AIManagerService $aiManager): void
    {
        Log::info("Joining groups for task #{$this->task->id}");

        // Get groups that need joining
        $groupsToJoin = $this->task->discoveredGroups()
            ->wherePivot('status', 'discovered')
            ->where('is_joined', false)
            ->where('is_banned', false)
            ->orderByDesc('quality_score')
            ->limit($this->task->max_groups_to_join_per_day)
            ->get();

        if ($groupsToJoin->isEmpty()) {
            // All groups joined or attempted, move to posting
            $this->task->status = SeekAndPostTask::STATUS_POSTING;
            $this->task->save();
            return;
        }

        foreach ($groupsToJoin as $group) {
            if (!$this->task->auto_join) {
                // Just mark as needing manual join
                $this->task->recordJoinRequest($group);
                continue;
            }

            // Send join request via AI Manager
            $result = $aiManager->joinGroup([
                'platform' => $group->platform,
                'groupId' => $group->group_id,
                'groupUrl' => $group->group_url,
            ]);

            if ($result['success']) {
                if ($result['joined'] ?? false) {
                    // Immediately joined (public group)
                    $this->task->recordJoin($group);
                } else {
                    // Join request sent (requires approval)
                    $this->task->recordJoinRequest($group);
                    $group->join_requested_at = now();
                    $group->save();
                }
            } else {
                Log::warning("Failed to join group {$group->group_id}: " . ($result['error'] ?? 'Unknown'));
            }

            // Rate limiting - wait between requests
            sleep(rand(3, 8));
        }
    }

    /**
     * Phase 3: Post to joined groups
     */
    private function postToGroups(AIManagerService $aiManager): void
    {
        Log::info("Posting to groups for task #{$this->task->id}");

        if (!$this->task->canPost()) {
            $this->task->complete();
            return;
        }

        // Get groups ready for posting
        $groups = $this->task->getGroupsReadyForPosting()
            ->take($this->task->max_posts_per_day - $this->task->posts_made);

        if ($groups->isEmpty()) {
            Log::info("No groups ready for posting for task #{$this->task->id}");
            return;
        }

        // Generate content if using workflow
        $content = $this->generateContent($aiManager);

        foreach ($groups as $group) {
            // Generate unique content if vary_content is enabled
            $postContent = $this->task->vary_content
                ? $this->generateContent($aiManager)
                : $content;

            // Determine posting time
            $postAt = $this->task->smart_timing
                ? $this->getSmartPostingTime($group)
                : now();

            // If smart timing suggests a future time, schedule the post
            if ($postAt->isFuture()) {
                // Schedule for later (would dispatch a delayed job)
                Log::info("Scheduled post for group {$group->group_id} at {$postAt}");
                continue;
            }

            // Post now
            $result = $aiManager->postToGroup([
                'platform' => $group->platform,
                'groupId' => $group->group_id,
                'content' => $postContent,
                'mediaUrls' => $this->task->media_urls,
            ]);

            $this->task->recordPost($group, $result['success'] ?? false);

            if (!($result['success'] ?? false)) {
                Log::warning("Failed to post to group {$group->group_id}: " . ($result['error'] ?? 'Unknown'));

                // Check if banned
                if (str_contains($result['error'] ?? '', 'banned') || str_contains($result['error'] ?? '', 'blocked')) {
                    $group->markAsBanned($result['error']);
                }
            }

            // Rate limiting
            sleep(rand(30, 120));
        }

        // Check if task is complete
        if ($this->task->posts_made >= $this->task->max_posts_per_day) {
            if ($this->task->is_recurring) {
                // Reset for next day
                $this->task->posts_made = 0;
                $this->task->posts_successful = 0;
                $this->task->posts_failed = 0;
                $this->task->status = SeekAndPostTask::STATUS_POSTING;
                $this->task->save();
            } else {
                $this->task->complete();
            }
        }
    }

    /**
     * Generate content using workflow or template
     */
    private function generateContent(AIManagerService $aiManager): string
    {
        if ($this->task->workflowTemplate) {
            // Execute workflow
            $result = $aiManager->executeWorkflow([
                'workflowJson' => $this->task->workflowTemplate->workflow_json,
                'variables' => $this->task->workflow_variables ?? [],
            ]);

            if ($result['success'] && isset($result['output'])) {
                return $result['output'];
            }
        }

        // Fallback to content template
        if ($this->task->content_template) {
            return $this->processTemplate($this->task->content_template);
        }

        // Generate with AI
        $result = $aiManager->generateContent([
            'prompt' => "สร้างโพสต์สำหรับกลุ่มเกี่ยวกับ: " . implode(', ', $this->task->target_keywords),
            'maxTokens' => 300,
        ]);

        return $result['content'] ?? 'Default post content';
    }

    /**
     * Process template variables
     */
    private function processTemplate(string $template): string
    {
        $variables = $this->task->workflow_variables ?? [];

        foreach ($variables as $key => $value) {
            $template = str_replace("{{$key}}", $value, $template);
        }

        return $template;
    }

    /**
     * Get optimal posting time using smart timing
     */
    private function getSmartPostingTime(DiscoveredGroup $group): \Carbon\Carbon
    {
        // Check if group has known best times
        if ($bestTime = $group->getBestPostingTime()) {
            $time = \Carbon\Carbon::parse($bestTime);
            if ($time->isFuture()) {
                return $time;
            }
            // If best time already passed today, use it tomorrow
            return $time->addDay();
        }

        // Default: post during peak hours (10-12, 19-21)
        $hour = now()->hour;
        if ($hour >= 10 && $hour <= 12) {
            return now();
        }
        if ($hour >= 19 && $hour <= 21) {
            return now();
        }

        // Next peak hour
        if ($hour < 10) {
            return now()->setHour(10)->setMinute(rand(0, 30));
        }
        if ($hour < 19) {
            return now()->setHour(19)->setMinute(rand(0, 30));
        }

        // Tomorrow morning
        return now()->addDay()->setHour(10)->setMinute(rand(0, 30));
    }

    /**
     * Schedule next phase of the task
     */
    private function scheduleNextPhase(): void
    {
        if (in_array($this->task->status, [
            SeekAndPostTask::STATUS_COMPLETED,
            SeekAndPostTask::STATUS_FAILED,
            SeekAndPostTask::STATUS_PAUSED,
        ])) {
            return;
        }

        // Schedule next run with delay to prevent rate limiting
        $delay = match ($this->task->status) {
            SeekAndPostTask::STATUS_SEEKING => 60 * 5,    // 5 minutes
            SeekAndPostTask::STATUS_JOINING => 60 * 10,   // 10 minutes
            SeekAndPostTask::STATUS_POSTING => 60 * 30,   // 30 minutes
            default => 60 * 5,
        };

        self::dispatch($this->task->fresh())->delay(now()->addSeconds($delay));
    }

    /**
     * Handle job failure
     */
    public function failed(\Throwable $exception): void
    {
        Log::error("SeekAndPostTask #{$this->task->id} permanently failed: {$exception->getMessage()}");
        $this->task->fail($exception->getMessage());
    }
}
