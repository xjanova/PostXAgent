<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\LearnedWorkflow;
use App\Models\WorkflowStep;
use App\Models\WorkflowExecution;
use App\Models\SocialAccount;
use App\Services\AIManagerService;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Validator;

class WebLearningController extends Controller
{
    private AIManagerService $aiManager;

    public function __construct(AIManagerService $aiManager)
    {
        $this->aiManager = $aiManager;
    }

    /**
     * List learned workflows
     */
    public function listWorkflows(Request $request): JsonResponse
    {
        $query = LearnedWorkflow::query()
            ->where('user_id', $request->user()->id)
            ->with('steps');

        if ($request->has('platform')) {
            $query->where('platform', $request->platform);
        }

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        if ($request->boolean('active_only')) {
            $query->active();
        }

        $workflows = $query->orderBy('confidence_score', 'desc')
            ->paginate($request->get('per_page', 15));

        return response()->json([
            'success' => true,
            'data' => $workflows,
        ]);
    }

    /**
     * Get workflow details
     */
    public function getWorkflow(LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('view', $workflow);

        return response()->json([
            'success' => true,
            'data' => $workflow->load(['steps', 'executions' => function ($q) {
                $q->latest()->limit(10);
            }]),
        ]);
    }

    /**
     * Start teaching session
     */
    public function startTeachingSession(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'platform' => 'required|in:' . implode(',', SocialAccount::getPlatforms()),
            'workflow_type' => 'required|string|max:100',
            'description' => 'nullable|string|max:500',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Create a new workflow in learning status
        $workflow = LearnedWorkflow::create([
            'user_id' => $request->user()->id,
            'platform' => $request->platform,
            'name' => $request->workflow_type,
            'description' => $request->description,
            'status' => LearnedWorkflow::STATUS_LEARNING,
        ]);

        // Start teaching session in AI Manager
        $session = $this->aiManager->startTeachingSession([
            'workflow_id' => $workflow->id,
            'platform' => $request->platform,
            'workflow_type' => $request->workflow_type,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'เริ่มเซสชันการสอนแล้ว กรุณาทำตามขั้นตอนบน Browser',
            'data' => [
                'workflow' => $workflow,
                'session_id' => $session['session_id'] ?? null,
                'browser_url' => $session['browser_url'] ?? null,
            ],
        ], 201);
    }

    /**
     * Record a step during teaching
     */
    public function recordStep(Request $request, LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('update', $workflow);

        $validator = Validator::make($request->all(), [
            'action' => 'required|in:' . implode(',', WorkflowStep::getActions()),
            'selector_type' => 'nullable|in:' . implode(',', WorkflowStep::getSelectorTypes()),
            'selector_value' => 'nullable|string',
            'description' => 'required|string|max:255',
            'input_value' => 'nullable|string',
            'input_variable' => 'nullable|string',
            'element_info' => 'nullable|array',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $maxOrder = $workflow->steps()->max('order') ?? -1;

        $step = WorkflowStep::create([
            'learned_workflow_id' => $workflow->id,
            'order' => $maxOrder + 1,
            'action' => $request->action,
            'description' => $request->description,
            'selector_type' => $request->selector_type,
            'selector_value' => $request->selector_value,
            'input_value' => $request->input_value,
            'input_variable' => $request->input_variable,
            'learned_from' => LearnedWorkflow::SOURCE_MANUAL,
            'visual_features' => $request->element_info,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'บันทึก Step สำเร็จ',
            'data' => $step,
        ], 201);
    }

    /**
     * Complete teaching session
     */
    public function completeTeachingSession(LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('update', $workflow);

        if ($workflow->steps()->count() === 0) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มี Steps ในเซสชันนี้',
            ], 400);
        }

        $workflow->update([
            'status' => LearnedWorkflow::STATUS_ACTIVE,
            'is_active' => true,
        ]);

        // Notify AI Manager to finalize learning
        $this->aiManager->completeTeachingSession($workflow->id);

        return response()->json([
            'success' => true,
            'message' => 'เสร็จสิ้นการสอน Workflow พร้อมใช้งานแล้ว',
            'data' => $workflow->load('steps'),
        ]);
    }

    /**
     * Test workflow execution
     */
    public function testWorkflow(Request $request, LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('view', $workflow);

        $validator = Validator::make($request->all(), [
            'content' => 'nullable|array',
            'content.text' => 'nullable|string',
            'content.hashtags' => 'nullable|array',
            'social_account_id' => 'nullable|exists:social_accounts,id',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Create execution record
        $execution = WorkflowExecution::create([
            'learned_workflow_id' => $workflow->id,
            'user_id' => $request->user()->id,
            'content_used' => $request->content,
            'status' => WorkflowExecution::STATUS_PENDING,
        ]);

        // Send to AI Manager for execution
        $result = $this->aiManager->executeWorkflow([
            'workflow_id' => $workflow->id,
            'execution_id' => $execution->id,
            'content' => $request->content,
            'social_account_id' => $request->social_account_id,
            'test_mode' => true,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'เริ่มทดสอบ Workflow แล้ว',
            'data' => [
                'execution_id' => $execution->id,
                'status' => $result['status'] ?? 'queued',
            ],
        ]);
    }

    /**
     * Get execution result
     */
    public function getExecution(WorkflowExecution $execution): JsonResponse
    {
        $this->authorize('view', $execution->workflow);

        return response()->json([
            'success' => true,
            'data' => $execution->load('workflow'),
        ]);
    }

    /**
     * List workflow executions
     */
    public function listExecutions(Request $request, LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('view', $workflow);

        $executions = $workflow->executions()
            ->orderBy('created_at', 'desc')
            ->paginate($request->get('per_page', 15));

        return response()->json([
            'success' => true,
            'data' => $executions,
        ]);
    }

    /**
     * Update workflow step
     */
    public function updateStep(Request $request, WorkflowStep $step): JsonResponse
    {
        $this->authorize('update', $step->workflow);

        $validator = Validator::make($request->all(), [
            'action' => 'nullable|in:' . implode(',', WorkflowStep::getActions()),
            'selector_type' => 'nullable|in:' . implode(',', WorkflowStep::getSelectorTypes()),
            'selector_value' => 'nullable|string',
            'description' => 'nullable|string|max:255',
            'input_value' => 'nullable|string',
            'input_variable' => 'nullable|string',
            'is_optional' => 'nullable|boolean',
            'wait_before_ms' => 'nullable|integer|min:0|max:10000',
            'wait_after_ms' => 'nullable|integer|min:0|max:10000',
            'timeout_ms' => 'nullable|integer|min:1000|max:60000',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $step->update($request->only([
            'action', 'selector_type', 'selector_value', 'description',
            'input_value', 'input_variable', 'is_optional',
            'wait_before_ms', 'wait_after_ms', 'timeout_ms',
        ]));

        return response()->json([
            'success' => true,
            'message' => 'อัพเดท Step สำเร็จ',
            'data' => $step,
        ]);
    }

    /**
     * Delete workflow step
     */
    public function deleteStep(WorkflowStep $step): JsonResponse
    {
        $this->authorize('update', $step->workflow);

        $workflow = $step->workflow;
        $deletedOrder = $step->order;

        $step->delete();

        // Reorder remaining steps
        $workflow->steps()
            ->where('order', '>', $deletedOrder)
            ->decrement('order');

        return response()->json([
            'success' => true,
            'message' => 'ลบ Step สำเร็จ',
        ]);
    }

    /**
     * Reorder workflow steps
     */
    public function reorderSteps(Request $request, LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('update', $workflow);

        $validator = Validator::make($request->all(), [
            'step_ids' => 'required|array',
            'step_ids.*' => 'exists:workflow_steps,id',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        foreach ($request->step_ids as $order => $stepId) {
            WorkflowStep::where('id', $stepId)
                ->where('learned_workflow_id', $workflow->id)
                ->update(['order' => $order]);
        }

        return response()->json([
            'success' => true,
            'message' => 'เรียงลำดับ Steps สำเร็จ',
            'data' => $workflow->load('steps'),
        ]);
    }

    /**
     * Clone workflow
     */
    public function cloneWorkflow(LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('view', $workflow);

        $newWorkflow = $workflow->clone();

        return response()->json([
            'success' => true,
            'message' => 'Clone Workflow สำเร็จ',
            'data' => $newWorkflow->load('steps'),
        ], 201);
    }

    /**
     * Delete workflow
     */
    public function deleteWorkflow(LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('delete', $workflow);

        $workflow->steps()->delete();
        $workflow->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบ Workflow สำเร็จ',
        ]);
    }

    /**
     * Get workflow statistics
     */
    public function getStatistics(Request $request): JsonResponse
    {
        $userId = $request->user()->id;
        $hours = $request->get('hours', 24);

        $workflows = LearnedWorkflow::where('user_id', $userId)->get();
        $executions = WorkflowExecution::where('user_id', $userId)
            ->where('created_at', '>=', now()->subHours($hours))
            ->get();

        $byPlatform = $workflows->groupBy('platform')
            ->map->count();

        $byStatus = $executions->groupBy('status')
            ->map->count();

        $successRate = $executions->count() > 0
            ? round($executions->where('status', WorkflowExecution::STATUS_SUCCESS)->count() / $executions->count() * 100, 2)
            : 0;

        return response()->json([
            'success' => true,
            'data' => [
                'period_hours' => $hours,
                'total_workflows' => $workflows->count(),
                'active_workflows' => $workflows->where('is_active', true)->count(),
                'by_platform' => $byPlatform,
                'total_executions' => $executions->count(),
                'by_status' => $byStatus,
                'success_rate' => $successRate,
                'avg_confidence' => round($workflows->avg('confidence_score') * 100, 2),
                'total_success' => $workflows->sum('success_count'),
                'total_failure' => $workflows->sum('failure_count'),
            ],
        ]);
    }

    /**
     * AI analyze page and suggest workflow
     */
    public function analyzePageWithAI(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'platform' => 'required|in:' . implode(',', SocialAccount::getPlatforms()),
            'purpose' => 'required|string|max:100',
            'page_html' => 'required|string',
            'screenshot' => 'nullable|string',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Send to AI Manager for analysis
        $result = $this->aiManager->analyzePageWithAI([
            'platform' => $request->platform,
            'purpose' => $request->purpose,
            'page_html' => $request->page_html,
            'screenshot' => $request->screenshot,
        ]);

        return response()->json([
            'success' => true,
            'data' => $result,
        ]);
    }

    /**
     * Generate workflow from AI analysis
     */
    public function generateWorkflowFromAI(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'platform' => 'required|in:' . implode(',', SocialAccount::getPlatforms()),
            'workflow_type' => 'required|string|max:100',
            'suggested_steps' => 'required|array',
            'suggested_steps.*.action' => 'required|string',
            'suggested_steps.*.description' => 'required|string',
            'suggested_steps.*.selector_hint' => 'nullable|string',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Create workflow
        $workflow = LearnedWorkflow::create([
            'user_id' => $request->user()->id,
            'platform' => $request->platform,
            'name' => $request->workflow_type,
            'description' => 'สร้างโดย AI',
            'status' => LearnedWorkflow::STATUS_ACTIVE,
            'is_active' => true,
        ]);

        // Create steps
        foreach ($request->suggested_steps as $index => $stepData) {
            WorkflowStep::create([
                'learned_workflow_id' => $workflow->id,
                'order' => $index,
                'action' => $stepData['action'],
                'description' => $stepData['description'],
                'selector_type' => $this->determineSelectorType($stepData['selector_hint'] ?? null),
                'selector_value' => $stepData['selector_hint'] ?? null,
                'input_variable' => $stepData['input_variable'] ?? null,
                'is_optional' => $stepData['is_optional'] ?? false,
                'learned_from' => LearnedWorkflow::SOURCE_AI_OBSERVED,
                'confidence_score' => $stepData['confidence'] ?? 0.7,
            ]);
        }

        return response()->json([
            'success' => true,
            'message' => 'สร้าง Workflow จาก AI สำเร็จ',
            'data' => $workflow->load('steps'),
        ], 201);
    }

    /**
     * Optimize workflow based on execution history
     */
    public function optimizeWorkflow(LearnedWorkflow $workflow): JsonResponse
    {
        $this->authorize('update', $workflow);

        // Send to AI Manager for optimization
        $result = $this->aiManager->optimizeWorkflow($workflow->id);

        if ($result['success'] ?? false) {
            $workflow->refresh();
        }

        return response()->json([
            'success' => $result['success'] ?? false,
            'message' => $result['message'] ?? 'ปรับปรุง Workflow แล้ว',
            'data' => $workflow->load('steps'),
        ]);
    }

    // Helpers
    private function determineSelectorType(?string $hint): ?string
    {
        if (empty($hint)) {
            return null;
        }

        if (str_starts_with($hint, '#')) {
            return WorkflowStep::SELECTOR_ID;
        }

        if (str_starts_with($hint, '.') || str_starts_with($hint, '[')) {
            return WorkflowStep::SELECTOR_CSS;
        }

        if (str_starts_with($hint, '//') || str_starts_with($hint, '/')) {
            return WorkflowStep::SELECTOR_XPATH;
        }

        return WorkflowStep::SELECTOR_SMART;
    }
}
