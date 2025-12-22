<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Jobs\ProcessSeekAndPostTask;
use App\Models\DiscoveredGroup;
use App\Models\SeekAndPostTask;
use App\Models\WorkflowTemplate;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Validator;

class SeekAndPostController extends Controller
{
    // ═══════════════════════════════════════════════════════════════════════
    // SEEK AND POST TASKS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Get all tasks for the current user
     */
    public function index(Request $request): JsonResponse
    {
        $query = SeekAndPostTask::byUser($request->user()->id)
            ->with(['workflowTemplate:id,name,name_th', 'brand:id,name']);

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        if ($request->has('platform')) {
            $query->byPlatform($request->platform);
        }

        $tasks = $query->orderByDesc('created_at')
            ->paginate($request->get('per_page', 20));

        return response()->json([
            'success' => true,
            'data' => $tasks,
        ]);
    }

    /**
     * Get a single task with full details
     */
    public function show(SeekAndPostTask $task): JsonResponse
    {
        $this->authorizeTask($task);

        $task->load([
            'workflowTemplate',
            'brand',
            'discoveredGroups' => fn($q) => $q->orderByDesc('quality_score')->limit(50),
        ]);

        return response()->json([
            'success' => true,
            'data' => $this->formatTask($task, true),
        ]);
    }

    /**
     * Create a new Seek and Post task
     */
    public function store(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'name' => 'required|string|max:255',
            'description' => 'nullable|string',
            'brand_id' => 'nullable|exists:brands,id',
            'platform' => 'required|string|in:facebook,line,telegram,twitter',
            'target_keywords' => 'required|array|min:1',
            'target_keywords.*' => 'string|max:100',
            'exclude_keywords' => 'nullable|array',
            'exclude_keywords.*' => 'string|max:100',
            'min_group_members' => 'nullable|integer|min:1',
            'max_group_members' => 'nullable|integer|min:1',
            'min_quality_score' => 'nullable|numeric|min:0|max:100',
            'max_groups_to_discover' => 'nullable|integer|min:1|max:500',
            'max_groups_to_join_per_day' => 'nullable|integer|min:1|max:50',
            'auto_join' => 'nullable|boolean',
            'workflow_template_id' => 'nullable|exists:workflow_templates,id',
            'workflow_variables' => 'nullable|array',
            'posts_per_group_per_day' => 'nullable|integer|min:1|max:10',
            'max_posts_per_day' => 'nullable|integer|min:1|max:100',
            'posting_schedule' => 'nullable|array',
            'smart_timing' => 'nullable|boolean',
            'content_template' => 'nullable|string',
            'vary_content' => 'nullable|boolean',
            'media_urls' => 'nullable|array',
            'media_urls.*' => 'string|url',
            'is_recurring' => 'nullable|boolean',
            'recurrence_pattern' => 'nullable|string|in:daily,weekly,monthly',
            'scheduled_at' => 'nullable|date|after:now',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $task = SeekAndPostTask::create([
            'user_id' => $request->user()->id,
            ...$validator->validated(),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'Seek and Post task created successfully',
            'data' => $this->formatTask($task),
        ], 201);
    }

    /**
     * Update a task
     */
    public function update(Request $request, SeekAndPostTask $task): JsonResponse
    {
        $this->authorizeTask($task);

        // Cannot update active tasks
        if (in_array($task->status, ['seeking', 'joining', 'posting'])) {
            return response()->json([
                'success' => false,
                'error' => 'Cannot update an active task. Please pause it first.',
            ], 400);
        }

        $validator = Validator::make($request->all(), [
            'name' => 'sometimes|string|max:255',
            'description' => 'nullable|string',
            'target_keywords' => 'sometimes|array|min:1',
            'exclude_keywords' => 'nullable|array',
            'min_group_members' => 'nullable|integer|min:1',
            'max_group_members' => 'nullable|integer|min:1',
            'min_quality_score' => 'nullable|numeric|min:0|max:100',
            'max_groups_to_discover' => 'nullable|integer|min:1|max:500',
            'max_groups_to_join_per_day' => 'nullable|integer|min:1|max:50',
            'auto_join' => 'nullable|boolean',
            'workflow_template_id' => 'nullable|exists:workflow_templates,id',
            'workflow_variables' => 'nullable|array',
            'posts_per_group_per_day' => 'nullable|integer|min:1|max:10',
            'max_posts_per_day' => 'nullable|integer|min:1|max:100',
            'smart_timing' => 'nullable|boolean',
            'content_template' => 'nullable|string',
            'vary_content' => 'nullable|boolean',
            'media_urls' => 'nullable|array',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $task->update($validator->validated());

        return response()->json([
            'success' => true,
            'message' => 'Task updated successfully',
            'data' => $this->formatTask($task),
        ]);
    }

    /**
     * Delete a task
     */
    public function destroy(SeekAndPostTask $task): JsonResponse
    {
        $this->authorizeTask($task);

        // Cannot delete active tasks
        if (in_array($task->status, ['seeking', 'joining', 'posting'])) {
            return response()->json([
                'success' => false,
                'error' => 'Cannot delete an active task. Please pause it first.',
            ], 400);
        }

        $task->delete();

        return response()->json([
            'success' => true,
            'message' => 'Task deleted successfully',
        ]);
    }

    /**
     * Start a task
     */
    public function start(SeekAndPostTask $task): JsonResponse
    {
        $this->authorizeTask($task);

        if (!in_array($task->status, ['pending', 'paused'])) {
            return response()->json([
                'success' => false,
                'error' => 'Task cannot be started from current status',
            ], 400);
        }

        $task->start();

        // Dispatch job to process the task
        ProcessSeekAndPostTask::dispatch($task);

        return response()->json([
            'success' => true,
            'message' => 'Task started successfully',
            'data' => $this->formatTask($task),
        ]);
    }

    /**
     * Pause a task
     */
    public function pause(SeekAndPostTask $task): JsonResponse
    {
        $this->authorizeTask($task);

        if (!in_array($task->status, ['seeking', 'joining', 'posting'])) {
            return response()->json([
                'success' => false,
                'error' => 'Task is not currently active',
            ], 400);
        }

        $task->pause();

        return response()->json([
            'success' => true,
            'message' => 'Task paused successfully',
            'data' => $this->formatTask($task),
        ]);
    }

    /**
     * Resume a paused task
     */
    public function resume(SeekAndPostTask $task): JsonResponse
    {
        $this->authorizeTask($task);

        if ($task->status !== 'paused') {
            return response()->json([
                'success' => false,
                'error' => 'Task is not paused',
            ], 400);
        }

        $task->resume();

        // Dispatch job to continue processing
        ProcessSeekAndPostTask::dispatch($task);

        return response()->json([
            'success' => true,
            'message' => 'Task resumed successfully',
            'data' => $this->formatTask($task),
        ]);
    }

    /**
     * Get task statistics
     */
    public function statistics(Request $request): JsonResponse
    {
        $userId = $request->user()->id;

        $stats = [
            'total_tasks' => SeekAndPostTask::byUser($userId)->count(),
            'active_tasks' => SeekAndPostTask::byUser($userId)->active()->count(),
            'completed_tasks' => SeekAndPostTask::byUser($userId)->where('status', 'completed')->count(),
            'total_groups_discovered' => SeekAndPostTask::byUser($userId)->sum('groups_discovered'),
            'total_groups_joined' => SeekAndPostTask::byUser($userId)->sum('groups_joined'),
            'total_posts_made' => SeekAndPostTask::byUser($userId)->sum('posts_made'),
            'total_posts_successful' => SeekAndPostTask::byUser($userId)->sum('posts_successful'),
            'overall_success_rate' => $this->calculateSuccessRate($userId),
            'by_platform' => SeekAndPostTask::byUser($userId)
                ->selectRaw('platform, count(*) as tasks, sum(posts_made) as posts, sum(posts_successful) as successful')
                ->groupBy('platform')
                ->get(),
        ];

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DISCOVERED GROUPS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Get discovered groups
     */
    public function groups(Request $request): JsonResponse
    {
        $query = DiscoveredGroup::query();

        if ($request->has('platform')) {
            $query->byPlatform($request->platform);
        }

        if ($request->has('keyword')) {
            $query->byKeyword($request->keyword);
        }

        if ($request->has('min_quality')) {
            $query->highQuality((float) $request->min_quality);
        }

        if ($request->boolean('joined_only')) {
            $query->joined();
        }

        if ($request->boolean('available_only')) {
            $query->availableForPosting();
        }

        $groups = $query->notBanned()
            ->orderByDesc('quality_score')
            ->paginate($request->get('per_page', 50));

        return response()->json([
            'success' => true,
            'data' => $groups,
        ]);
    }

    /**
     * Get a single group
     */
    public function groupShow(DiscoveredGroup $group): JsonResponse
    {
        return response()->json([
            'success' => true,
            'data' => $group,
        ]);
    }

    /**
     * Search groups by keywords
     */
    public function searchGroups(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'keywords' => 'required|array|min:1',
            'keywords.*' => 'string|max:100',
            'platform' => 'nullable|string|in:facebook,line,telegram,twitter',
            'limit' => 'nullable|integer|min:1|max:100',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $groups = DiscoveredGroup::findByKeywords(
            $request->keywords,
            $request->platform,
            $request->get('limit', 50)
        );

        return response()->json([
            'success' => true,
            'data' => $groups,
            'count' => $groups->count(),
        ]);
    }

    /**
     * Get recommended groups based on past success
     */
    public function recommendedGroups(Request $request): JsonResponse
    {
        $query = DiscoveredGroup::availableForPosting()
            ->highQuality(70)
            ->orderByDesc('success_rate')
            ->orderByDesc('quality_score');

        if ($request->has('platform')) {
            $query->byPlatform($request->platform);
        }

        $groups = $query->limit($request->get('limit', 20))->get();

        return response()->json([
            'success' => true,
            'data' => $groups,
        ]);
    }

    /**
     * Get group statistics
     */
    public function groupStatistics(): JsonResponse
    {
        $stats = [
            'total_groups' => DiscoveredGroup::count(),
            'joined_groups' => DiscoveredGroup::joined()->count(),
            'available_groups' => DiscoveredGroup::availableForPosting()->count(),
            'banned_groups' => DiscoveredGroup::where('is_banned', true)->count(),
            'by_platform' => DiscoveredGroup::selectRaw('platform, count(*) as count, avg(quality_score) as avg_quality')
                ->groupBy('platform')
                ->get(),
            'by_activity' => DiscoveredGroup::selectRaw('activity_level, count(*) as count')
                ->groupBy('activity_level')
                ->pluck('count', 'activity_level'),
            'average_quality_score' => DiscoveredGroup::avg('quality_score'),
            'top_performing' => DiscoveredGroup::availableForPosting()
                ->where('our_post_count', '>', 0)
                ->orderByDesc('success_rate')
                ->limit(10)
                ->get(['id', 'group_name', 'platform', 'success_rate', 'our_post_count']),
        ];

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Authorize task ownership
     */
    private function authorizeTask(SeekAndPostTask $task): void
    {
        if ($task->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to access this task');
        }
    }

    /**
     * Calculate overall success rate for user
     */
    private function calculateSuccessRate(int $userId): float
    {
        $totals = SeekAndPostTask::byUser($userId)
            ->selectRaw('sum(posts_made) as total, sum(posts_successful) as successful')
            ->first();

        if ($totals->total > 0) {
            return round(($totals->successful / $totals->total) * 100, 2);
        }

        return 0;
    }

    /**
     * Format task for API response
     */
    private function formatTask(SeekAndPostTask $task, bool $detailed = false): array
    {
        $data = [
            'id' => $task->id,
            'name' => $task->name,
            'description' => $task->description,
            'status' => $task->status,
            'platform' => $task->platform,
            'target_keywords' => $task->target_keywords,
            'progress' => [
                'percentage' => round($task->getProgressPercentage(), 1),
                'groups_discovered' => $task->groups_discovered,
                'groups_joined' => $task->groups_joined,
                'posts_made' => $task->posts_made,
                'posts_successful' => $task->posts_successful,
                'posts_failed' => $task->posts_failed,
                'success_rate' => round($task->getSuccessRate(), 1),
            ],
            'brand' => $task->brand ? [
                'id' => $task->brand->id,
                'name' => $task->brand->name,
            ] : null,
            'workflow_template' => $task->workflowTemplate ? [
                'id' => $task->workflowTemplate->id,
                'name' => $task->workflowTemplate->getLocalizedName(),
            ] : null,
            'scheduled_at' => $task->scheduled_at?->toIso8601String(),
            'started_at' => $task->started_at?->toIso8601String(),
            'completed_at' => $task->completed_at?->toIso8601String(),
            'last_seek_at' => $task->last_seek_at?->toIso8601String(),
            'last_post_at' => $task->last_post_at?->toIso8601String(),
            'created_at' => $task->created_at?->toIso8601String(),
        ];

        if ($detailed) {
            $data['configuration'] = [
                'exclude_keywords' => $task->exclude_keywords,
                'min_group_members' => $task->min_group_members,
                'max_group_members' => $task->max_group_members,
                'min_quality_score' => $task->min_quality_score,
                'max_groups_to_discover' => $task->max_groups_to_discover,
                'max_groups_to_join_per_day' => $task->max_groups_to_join_per_day,
                'auto_join' => $task->auto_join,
                'posts_per_group_per_day' => $task->posts_per_group_per_day,
                'max_posts_per_day' => $task->max_posts_per_day,
                'smart_timing' => $task->smart_timing,
                'vary_content' => $task->vary_content,
                'is_recurring' => $task->is_recurring,
                'recurrence_pattern' => $task->recurrence_pattern,
            ];

            $data['content'] = [
                'workflow_variables' => $task->workflow_variables,
                'posting_schedule' => $task->posting_schedule,
                'content_template' => $task->content_template,
                'media_urls' => $task->media_urls,
            ];

            if ($task->relationLoaded('discoveredGroups')) {
                $data['groups'] = $task->discoveredGroups->map(fn($g) => [
                    'id' => $g->id,
                    'name' => $g->group_name,
                    'platform' => $g->platform,
                    'quality_score' => $g->quality_score,
                    'member_count' => $g->member_count,
                    'status' => $g->pivot->status,
                    'posts_in_group' => $g->pivot->posts_in_group,
                ]);
            }
        }

        return $data;
    }
}
