<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\ApiKeyPool;
use App\Models\ApiKeyPoolMember;
use App\Models\ContentPipeline;
use App\Models\PipelineRun;
use App\Services\ApiKeyRotationService;
use App\Services\ContentPipelineService;
use App\Jobs\Pipeline\RunPipelineJob;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Log;

class ContentPipelineController extends Controller
{
    public function __construct(
        private ContentPipelineService $pipelineService,
        private ApiKeyRotationService $keyRotation
    ) {}

    // ====================================================================
    // Pipeline CRUD
    // ====================================================================

    public function index(Request $request): JsonResponse
    {
        $pipelines = ContentPipeline::where('user_id', $request->user()->id)
            ->with(['brand:id,name', 'textPool:id,name,provider', 'ttsPool:id,name,provider'])
            ->withCount('runs')
            ->latest()
            ->paginate(20);

        return response()->json([
            'success' => true,
            'data' => $pipelines,
        ]);
    }

    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:255',
            'description' => 'nullable|string|max:1000',
            'brand_id' => 'nullable|exists:brands,id',
            // Content
            'content_type' => 'in:story,tips,news,promotional,educational',
            'language' => 'string|max:10',
            'tone' => 'string|max:50',
            'content_prompt_template' => 'nullable|string|max:5000',
            'scene_count' => 'integer|min:1|max:20',
            // Image
            'image_style' => 'string|max:50',
            'image_width' => 'integer|min:256|max:2048',
            'image_height' => 'integer|min:256|max:2048',
            'image_provider' => 'string|max:30',
            // Video
            'video_duration_seconds' => 'integer|min:10|max:600',
            'aspect_ratio' => 'in:9:16,16:9,1:1',
            'video_provider' => 'string|max:30',
            'clip_duration_seconds' => 'integer|min:2|max:30',
            'freepik_account_pool_id' => 'nullable|exists:account_pools,id',
            // Audio
            'tts_provider' => 'in:edge,google,both',
            'tts_voice_id' => 'string|max:100',
            'tts_voice_gender' => 'in:female,male',
            'enable_lip_sync' => 'boolean',
            'lip_sync_provider' => 'in:sadtalker,wav2lip',
            'lip_sync_avatar_path' => 'nullable|string|max:500',
            // Music
            'enable_music' => 'boolean',
            'music_style' => 'string|max:100',
            'music_volume' => 'numeric|min:0|max:1',
            'music_source' => 'in:suno,royalty_free,none',
            // Publishing
            'target_platforms' => 'array',
            'target_platforms.*' => 'string|in:tiktok,youtube,facebook,instagram',
            'auto_publish' => 'boolean',
            'publish_account_pool_id' => 'nullable|exists:account_pools,id',
            // Schedule
            'schedule_type' => 'in:manual,interval,cron',
            'interval_hours' => 'integer|min:1|max:720',
            'cron_expression' => 'nullable|string|max:100',
            'max_runs_per_day' => 'integer|min:1|max:50',
            // API Key Pools
            'text_pool_id' => 'nullable|exists:api_key_pools,id',
            'tts_pool_id' => 'nullable|exists:api_key_pools,id',
        ]);

        $validated['user_id'] = $request->user()->id;

        $pipeline = ContentPipeline::create($validated);

        return response()->json([
            'success' => true,
            'data' => $pipeline->load('brand:id,name'),
            'message' => 'สร้าง pipeline สำเร็จ',
        ], 201);
    }

    public function show(ContentPipeline $pipeline): JsonResponse
    {
        $this->authorize('view', $pipeline);

        $pipeline->load([
            'brand:id,name',
            'textPool:id,name,provider',
            'ttsPool:id,name,provider',
            'publishAccountPool:id,name,platform',
        ]);

        $pipeline->loadCount('runs');
        $pipeline->setAttribute('success_rate', $pipeline->getSuccessRate());
        $pipeline->setAttribute('last_run', $pipeline->getLastRun());

        return response()->json([
            'success' => true,
            'data' => $pipeline,
        ]);
    }

    public function update(Request $request, ContentPipeline $pipeline): JsonResponse
    {
        $this->authorize('update', $pipeline);

        $validated = $request->validate([
            'name' => 'string|max:255',
            'description' => 'nullable|string|max:1000',
            'brand_id' => 'nullable|exists:brands,id',
            'content_type' => 'in:story,tips,news,promotional,educational',
            'language' => 'string|max:10',
            'tone' => 'string|max:50',
            'content_prompt_template' => 'nullable|string|max:5000',
            'scene_count' => 'integer|min:1|max:20',
            'image_style' => 'string|max:50',
            'image_width' => 'integer|min:256|max:2048',
            'image_height' => 'integer|min:256|max:2048',
            'image_provider' => 'string|max:30',
            'video_duration_seconds' => 'integer|min:10|max:600',
            'aspect_ratio' => 'in:9:16,16:9,1:1',
            'video_provider' => 'string|max:30',
            'clip_duration_seconds' => 'integer|min:2|max:30',
            'tts_provider' => 'in:edge,google,both',
            'tts_voice_id' => 'string|max:100',
            'tts_voice_gender' => 'in:female,male',
            'enable_lip_sync' => 'boolean',
            'lip_sync_provider' => 'in:sadtalker,wav2lip',
            'lip_sync_avatar_path' => 'nullable|string|max:500',
            'enable_music' => 'boolean',
            'music_style' => 'string|max:100',
            'music_volume' => 'numeric|min:0|max:1',
            'music_source' => 'in:suno,royalty_free,none',
            'target_platforms' => 'array',
            'auto_publish' => 'boolean',
            'publish_account_pool_id' => 'nullable|exists:account_pools,id',
            'schedule_type' => 'in:manual,interval,cron',
            'interval_hours' => 'integer|min:1|max:720',
            'cron_expression' => 'nullable|string|max:100',
            'max_runs_per_day' => 'integer|min:1|max:50',
            'text_pool_id' => 'nullable|exists:api_key_pools,id',
            'tts_pool_id' => 'nullable|exists:api_key_pools,id',
        ]);

        $pipeline->update($validated);

        return response()->json([
            'success' => true,
            'data' => $pipeline->fresh(),
            'message' => 'อัปเดต pipeline สำเร็จ',
        ]);
    }

    public function destroy(ContentPipeline $pipeline): JsonResponse
    {
        $this->authorize('delete', $pipeline);
        $pipeline->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบ pipeline สำเร็จ',
        ]);
    }

    // ====================================================================
    // Pipeline Operations
    // ====================================================================

    public function startRun(ContentPipeline $pipeline): JsonResponse
    {
        $this->authorize('update', $pipeline);

        if ($pipeline->hasActiveRun()) {
            return response()->json([
                'success' => false,
                'error' => 'Pipeline กำลังทำงานอยู่แล้ว',
            ], 409);
        }

        if (!$pipeline->canRunToday()) {
            return response()->json([
                'success' => false,
                'error' => 'เกินจำนวนรันสูงสุดต่อวัน',
            ], 429);
        }

        $run = $this->pipelineService->startRun($pipeline);
        RunPipelineJob::dispatch($run);

        return response()->json([
            'success' => true,
            'data' => $run,
            'message' => 'เริ่ม pipeline สำเร็จ',
        ]);
    }

    public function toggleActive(ContentPipeline $pipeline): JsonResponse
    {
        $this->authorize('update', $pipeline);

        $pipeline->update([
            'is_active' => !$pipeline->is_active,
            'next_run_at' => !$pipeline->is_active ? $pipeline->calculateNextRunAt() : null,
        ]);

        return response()->json([
            'success' => true,
            'data' => ['is_active' => $pipeline->is_active],
            'message' => $pipeline->is_active ? 'เปิดใช้งาน pipeline' : 'ปิดใช้งาน pipeline',
        ]);
    }

    // ====================================================================
    // Pipeline Runs
    // ====================================================================

    public function listRuns(Request $request): JsonResponse
    {
        $query = PipelineRun::with('pipeline:id,name')
            ->whereHas('pipeline', fn($q) => $q->where('user_id', $request->user()->id));

        if ($request->has('pipeline_id')) {
            $query->where('content_pipeline_id', $request->input('pipeline_id'));
        }

        if ($request->has('status')) {
            $query->where('status', $request->input('status'));
        }

        $runs = $query->latest()->paginate(20);

        return response()->json([
            'success' => true,
            'data' => $runs,
        ]);
    }

    public function showRun(PipelineRun $run): JsonResponse
    {
        $run->load('pipeline:id,name,user_id');

        if ($run->pipeline->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to access this run');
        }

        return response()->json([
            'success' => true,
            'data' => $run,
        ]);
    }

    public function cancelRun(PipelineRun $run): JsonResponse
    {
        $run->loadMissing('pipeline:id,user_id');
        if ($run->pipeline->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to cancel this run');
        }

        if (!$run->canCancel()) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่สามารถยกเลิก run นี้ได้',
            ], 400);
        }

        $this->pipelineService->cancelRun($run);

        return response()->json([
            'success' => true,
            'message' => 'ยกเลิก run สำเร็จ',
        ]);
    }

    public function retryRun(PipelineRun $run): JsonResponse
    {
        $run->loadMissing('pipeline:id,user_id');
        if ($run->pipeline->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to retry this run');
        }

        if (!$run->canRetry()) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่สามารถ retry run นี้ได้',
            ], 400);
        }

        $run->update([
            'status' => $run->error_step ?? PipelineRun::STATUS_PENDING,
            'error_message' => null,
            'error_step' => null,
            'retry_count' => $run->retry_count + 1,
            'completed_at' => null,
        ]);

        RunPipelineJob::dispatch($run);

        return response()->json([
            'success' => true,
            'data' => $run->fresh(),
            'message' => 'Retry pipeline สำเร็จ',
        ]);
    }

    // ====================================================================
    // API Key Pools
    // ====================================================================

    public function listApiKeyPools(Request $request): JsonResponse
    {
        $pools = ApiKeyPool::where('user_id', $request->user()->id)
            ->withCount('members')
            ->latest()
            ->get()
            ->map(function (ApiKeyPool $pool) {
                $pool->setAttribute('statistics', $this->keyRotation->getPoolStatistics($pool));
                return $pool;
            });

        return response()->json([
            'success' => true,
            'data' => $pools,
        ]);
    }

    public function createApiKeyPool(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:255',
            'provider' => 'required|string|in:' . implode(',', ApiKeyPool::getProviders()),
            'service_type' => 'required|in:text,image,tts,video,music',
            'rotation_strategy' => 'in:' . implode(',', ApiKeyPool::getStrategies()),
            'cooldown_seconds' => 'integer|min:0|max:86400',
            'max_requests_per_day' => 'integer|min:1|max:1000000',
            'auto_failover' => 'boolean',
        ]);

        $validated['user_id'] = $request->user()->id;
        $pool = ApiKeyPool::create($validated);

        return response()->json([
            'success' => true,
            'data' => $pool,
            'message' => 'สร้าง API key pool สำเร็จ',
        ], 201);
    }

    public function addApiKey(Request $request, ApiKeyPool $pool): JsonResponse
    {
        if ($pool->user_id !== $request->user()->id) {
            abort(403, 'You do not have permission to modify this pool');
        }

        $validated = $request->validate([
            'label' => 'required|string|max:100',
            'api_key' => 'required|string',
            'base_url' => 'nullable|string|url|max:500',
            'model_id' => 'nullable|string|max:100',
            'priority' => 'integer|min:0|max:100',
            'weight' => 'integer|min:1|max:1000',
            'daily_limit' => 'integer|min:0|max:1000000',
        ]);

        $member = $pool->members()->create([
            'label' => $validated['label'],
            'encrypted_api_key' => encrypt($validated['api_key']),
            'base_url' => $validated['base_url'] ?? null,
            'model_id' => $validated['model_id'] ?? null,
            'priority' => $validated['priority'] ?? 0,
            'weight' => $validated['weight'] ?? 100,
            'daily_limit' => $validated['daily_limit'] ?? 0,
        ]);

        return response()->json([
            'success' => true,
            'data' => $member,
            'message' => 'เพิ่ม API key สำเร็จ',
        ], 201);
    }

    public function removeApiKey(ApiKeyPool $pool, ApiKeyPoolMember $member): JsonResponse
    {
        if ($pool->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to modify this pool');
        }

        if ($member->api_key_pool_id !== $pool->id) {
            return response()->json(['success' => false, 'error' => 'Key not in pool'], 404);
        }

        $member->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบ API key สำเร็จ',
        ]);
    }

    public function apiKeyPoolStats(ApiKeyPool $pool): JsonResponse
    {
        if ($pool->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to access this pool');
        }

        return response()->json([
            'success' => true,
            'data' => $this->keyRotation->getPoolStatistics($pool),
        ]);
    }

    // ====================================================================
    // Dashboard Stats
    // ====================================================================

    public function stats(Request $request): JsonResponse
    {
        $userId = $request->user()->id;

        $totalPipelines = ContentPipeline::where('user_id', $userId)->count();
        $activePipelines = ContentPipeline::where('user_id', $userId)->where('is_active', true)->count();
        $activeRuns = PipelineRun::whereHas('pipeline', fn($q) => $q->where('user_id', $userId))
            ->whereNotIn('status', ['completed', 'failed', 'cancelled', 'pending'])
            ->count();
        $completedToday = PipelineRun::whereHas('pipeline', fn($q) => $q->where('user_id', $userId))
            ->where('status', 'completed')
            ->whereDate('completed_at', today())
            ->count();
        $totalApiKeys = ApiKeyPoolMember::whereHas('apiKeyPool', fn($q) => $q->where('is_active', true))
            ->where('status', 'active')
            ->count();

        // Success rate (last 30 days)
        $totalRuns = PipelineRun::whereHas('pipeline', fn($q) => $q->where('user_id', $userId))
            ->where('created_at', '>=', now()->subDays(30))
            ->whereIn('status', ['completed', 'failed'])
            ->count();
        $successRuns = PipelineRun::whereHas('pipeline', fn($q) => $q->where('user_id', $userId))
            ->where('created_at', '>=', now()->subDays(30))
            ->where('status', 'completed')
            ->count();

        return response()->json([
            'success' => true,
            'data' => [
                'total_pipelines' => $totalPipelines,
                'active_pipelines' => $activePipelines,
                'active_runs' => $activeRuns,
                'completed_today' => $completedToday,
                'total_api_keys' => $totalApiKeys,
                'success_rate' => $totalRuns > 0 ? round(($successRuns / $totalRuns) * 100, 1) : 0,
            ],
        ]);
    }

    public function providerStatus(): JsonResponse
    {
        $pools = ApiKeyPool::active()->with('members')->get();
        $status = [];

        /** @var ApiKeyPool $pool */
        foreach ($pools as $pool) {
            $active = $pool->members->where('status', 'active')->count();
            $total = $pool->members->count();
            $status[$pool->provider] = [
                'name' => $pool->name,
                'active_keys' => $active,
                'total_keys' => $total,
                'requests_today' => $pool->members->sum('requests_today'),
                'available' => $active > 0,
            ];
        }

        return response()->json([
            'success' => true,
            'data' => $status,
        ]);
    }
}
