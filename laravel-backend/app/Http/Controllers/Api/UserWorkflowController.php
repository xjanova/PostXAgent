<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\UserWorkflow;
use App\Models\WorkflowTemplate;
use App\Models\WorkflowExecution;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Validator;

class UserWorkflowController extends Controller
{
    // ═══════════════════════════════════════════════════════════════════════
    // USER WORKFLOWS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Get all workflows for the current user
     */
    public function index(Request $request): JsonResponse
    {
        $query = UserWorkflow::byUser($request->user()->id)
            ->with('template:id,name,name_th,icon');

        if ($request->has('platform')) {
            $query->forPlatform($request->platform);
        }

        if ($request->boolean('active_only')) {
            $query->active();
        }

        $workflows = $query->orderByDesc('updated_at')
            ->paginate($request->get('per_page', 20));

        return response()->json([
            'success' => true,
            'data' => $workflows,
        ]);
    }

    /**
     * Get a single workflow
     */
    public function show(UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $workflow->load('template');

        return response()->json([
            'success' => true,
            'data' => $workflow,
        ]);
    }

    /**
     * Create a workflow from scratch
     */
    public function store(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'name' => 'required|string|max:255',
            'description' => 'nullable|string',
            'platforms' => 'required|array|min:1',
            'platforms.*' => 'string|in:facebook,instagram,tiktok,twitter,line,youtube,threads,linkedin,pinterest',
            'workflow_json' => 'required|string',
            'default_variables' => 'nullable|array',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Validate workflow JSON
        $workflowData = json_decode($request->workflow_json);
        if (json_last_error() !== JSON_ERROR_NONE) {
            return response()->json([
                'success' => false,
                'error' => 'Invalid workflow JSON',
            ], 422);
        }

        $workflow = UserWorkflow::create([
            'user_id' => $request->user()->id,
            ...$validator->validated(),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'Workflow created successfully',
            'data' => $workflow,
        ], 201);
    }

    /**
     * Create a workflow from a template
     */
    public function createFromTemplate(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'template_id' => 'required|exists:workflow_templates,id',
            'name' => 'nullable|string|max:255',
            'description' => 'nullable|string',
            'default_variables' => 'nullable|array',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $template = WorkflowTemplate::findOrFail($request->template_id);

        // Increment template use count
        $template->incrementUseCount();

        $workflow = UserWorkflow::createFromTemplate(
            $request->user(),
            $template,
            $validator->validated()
        );

        return response()->json([
            'success' => true,
            'message' => 'Workflow created from template',
            'data' => $workflow->load('template'),
        ], 201);
    }

    /**
     * Update a workflow
     */
    public function update(Request $request, UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $validator = Validator::make($request->all(), [
            'name' => 'sometimes|string|max:255',
            'description' => 'nullable|string',
            'platforms' => 'sometimes|array|min:1',
            'platforms.*' => 'string|in:facebook,instagram,tiktok,twitter,line,youtube,threads,linkedin,pinterest',
            'workflow_json' => 'sometimes|string',
            'default_variables' => 'nullable|array',
            'is_active' => 'sometimes|boolean',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Validate workflow JSON if provided
        if ($request->has('workflow_json')) {
            $workflowData = json_decode($request->workflow_json);
            if (json_last_error() !== JSON_ERROR_NONE) {
                return response()->json([
                    'success' => false,
                    'error' => 'Invalid workflow JSON',
                ], 422);
            }
        }

        $workflow->update($validator->validated());

        return response()->json([
            'success' => true,
            'message' => 'Workflow updated successfully',
            'data' => $workflow,
        ]);
    }

    /**
     * Delete a workflow
     */
    public function destroy(UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $workflow->delete();

        return response()->json([
            'success' => true,
            'message' => 'Workflow deleted successfully',
        ]);
    }

    /**
     * Duplicate a workflow
     */
    public function duplicate(UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $newWorkflow = $workflow->duplicate();

        return response()->json([
            'success' => true,
            'message' => 'Workflow duplicated successfully',
            'data' => $newWorkflow,
        ]);
    }

    /**
     * Toggle workflow active status
     */
    public function toggleActive(UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $workflow->update(['is_active' => !$workflow->is_active]);

        return response()->json([
            'success' => true,
            'message' => $workflow->is_active ? 'Workflow activated' : 'Workflow deactivated',
            'data' => ['is_active' => $workflow->is_active],
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW EXECUTION
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Execute a workflow
     */
    public function execute(Request $request, UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $validator = Validator::make($request->all(), [
            'variables' => 'nullable|array',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Create execution record
        $execution = WorkflowExecution::create([
            'user_id' => $request->user()->id,
            'user_workflow_id' => $workflow->id,
            'template_id' => $workflow->template_id,
            'status' => 'pending',
            'variables' => array_merge(
                $workflow->default_variables ?? [],
                $request->get('variables', [])
            ),
            'total_nodes' => count($workflow->getNodes()),
        ]);

        // TODO: Dispatch job to actually execute the workflow
        // dispatch(new ExecuteWorkflowJob($execution));

        return response()->json([
            'success' => true,
            'message' => 'Workflow execution started',
            'data' => [
                'execution_id' => $execution->id,
                'status' => $execution->status,
            ],
        ]);
    }

    /**
     * Get execution history for a workflow
     */
    public function executions(Request $request, UserWorkflow $workflow): JsonResponse
    {
        $this->authorizeWorkflow($workflow);

        $executions = $workflow->executions()
            ->orderByDesc('created_at')
            ->paginate($request->get('per_page', 20));

        return response()->json([
            'success' => true,
            'data' => $executions,
        ]);
    }

    /**
     * Get a single execution
     */
    public function executionShow(WorkflowExecution $execution): JsonResponse
    {
        if ($execution->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to access this execution');
        }

        $execution->load(['userWorkflow:id,name', 'template:id,name,name_th']);

        return response()->json([
            'success' => true,
            'data' => $execution,
        ]);
    }

    /**
     * Cancel a running execution
     */
    public function cancelExecution(WorkflowExecution $execution): JsonResponse
    {
        if ($execution->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to access this execution');
        }

        if (!in_array($execution->status, ['pending', 'running'])) {
            return response()->json([
                'success' => false,
                'error' => 'Execution cannot be cancelled',
            ], 400);
        }

        $execution->update([
            'status' => 'cancelled',
            'completed_at' => now(),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'Execution cancelled',
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // WORKFLOW EDITOR API
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Get available node types
     */
    public function nodeTypes(): JsonResponse
    {
        $nodeTypes = [
            'inputs' => [
                [
                    'type' => 'TextInput',
                    'name' => 'Text Input',
                    'name_th' => 'ข้อความ',
                    'icon' => 'TextFields',
                    'category' => 'input',
                    'outputs' => ['output'],
                    'config' => [
                        'label' => ['type' => 'string', 'default' => 'Input'],
                        'placeholder' => ['type' => 'string', 'default' => ''],
                        'variableKey' => ['type' => 'string', 'required' => true],
                    ],
                ],
                [
                    'type' => 'NumberInput',
                    'name' => 'Number Input',
                    'name_th' => 'ตัวเลข',
                    'icon' => 'Numbers',
                    'category' => 'input',
                    'outputs' => ['output'],
                    'config' => [
                        'label' => ['type' => 'string', 'default' => 'Number'],
                        'min' => ['type' => 'number'],
                        'max' => ['type' => 'number'],
                        'variableKey' => ['type' => 'string', 'required' => true],
                    ],
                ],
                [
                    'type' => 'ImageInput',
                    'name' => 'Image Input',
                    'name_th' => 'รูปภาพ',
                    'icon' => 'Image',
                    'category' => 'input',
                    'outputs' => ['output'],
                    'config' => [
                        'label' => ['type' => 'string', 'default' => 'Image'],
                        'accept' => ['type' => 'string', 'default' => 'image/*'],
                        'variableKey' => ['type' => 'string', 'required' => true],
                    ],
                ],
            ],
            'ai' => [
                [
                    'type' => 'AITextGenerator',
                    'name' => 'AI Text Generator',
                    'name_th' => 'AI สร้างข้อความ',
                    'icon' => 'AutoAwesome',
                    'category' => 'ai',
                    'inputs' => ['input', 'context', 'variables'],
                    'outputs' => ['output'],
                    'config' => [
                        'prompt' => ['type' => 'text', 'required' => true],
                        'maxTokens' => ['type' => 'number', 'default' => 300],
                        'temperature' => ['type' => 'number', 'default' => 0.7, 'min' => 0, 'max' => 1],
                    ],
                ],
                [
                    'type' => 'AIImageGenerator',
                    'name' => 'AI Image Generator',
                    'name_th' => 'AI สร้างรูปภาพ',
                    'icon' => 'ImageSearch',
                    'category' => 'ai',
                    'inputs' => ['prompt'],
                    'outputs' => ['image'],
                    'config' => [
                        'style' => ['type' => 'select', 'options' => ['realistic', 'anime', 'cartoon', 'abstract']],
                        'size' => ['type' => 'select', 'options' => ['512x512', '1024x1024', '1024x1792']],
                    ],
                ],
                [
                    'type' => 'AITranslator',
                    'name' => 'AI Translator',
                    'name_th' => 'AI แปลภาษา',
                    'icon' => 'Translate',
                    'category' => 'ai',
                    'inputs' => ['input'],
                    'outputs' => ['output'],
                    'config' => [
                        'targetLanguage' => ['type' => 'select', 'options' => ['th', 'en', 'zh', 'ja', 'ko']],
                    ],
                ],
            ],
            'processing' => [
                [
                    'type' => 'TextCombiner',
                    'name' => 'Text Combiner',
                    'name_th' => 'รวมข้อความ',
                    'icon' => 'MergeType',
                    'category' => 'processing',
                    'inputs' => ['text1', 'text2', 'text3'],
                    'outputs' => ['output'],
                    'config' => [
                        'separator' => ['type' => 'string', 'default' => '\n\n'],
                    ],
                ],
                [
                    'type' => 'TextSplitter',
                    'name' => 'Text Splitter',
                    'name_th' => 'แยกข้อความ',
                    'icon' => 'CallSplit',
                    'category' => 'processing',
                    'inputs' => ['input'],
                    'outputs' => ['part1', 'part2', 'part3'],
                    'config' => [
                        'delimiter' => ['type' => 'string', 'default' => '\n'],
                    ],
                ],
                [
                    'type' => 'Condition',
                    'name' => 'Condition',
                    'name_th' => 'เงื่อนไข',
                    'icon' => 'DeviceHub',
                    'category' => 'processing',
                    'inputs' => ['input'],
                    'outputs' => ['true', 'false'],
                    'config' => [
                        'condition' => ['type' => 'string', 'required' => true],
                    ],
                ],
            ],
            'seek_and_post' => [
                [
                    'type' => 'GroupSearch',
                    'name' => 'Group Search',
                    'name_th' => 'ค้นหากลุ่ม',
                    'icon' => 'Search',
                    'category' => 'seek_and_post',
                    'inputs' => [],
                    'outputs' => ['groups'],
                    'config' => [
                        'keywords' => ['type' => 'array', 'required' => true],
                        'platform' => ['type' => 'select', 'options' => ['facebook', 'line', 'telegram']],
                        'minMembers' => ['type' => 'number', 'default' => 100],
                        'maxJoinsPerDay' => ['type' => 'number', 'default' => 10],
                    ],
                ],
                [
                    'type' => 'GroupFilter',
                    'name' => 'Group Filter',
                    'name_th' => 'กรองกลุ่ม',
                    'icon' => 'FilterList',
                    'category' => 'seek_and_post',
                    'inputs' => ['groups'],
                    'outputs' => ['filtered'],
                    'config' => [
                        'excludeJoined' => ['type' => 'boolean', 'default' => true],
                        'excludeBanned' => ['type' => 'boolean', 'default' => true],
                        'minQualityScore' => ['type' => 'number', 'default' => 50],
                    ],
                ],
                [
                    'type' => 'GroupJoinRequest',
                    'name' => 'Join Request',
                    'name_th' => 'ขอเข้ากลุ่ม',
                    'icon' => 'PersonAdd',
                    'category' => 'seek_and_post',
                    'inputs' => ['groups'],
                    'outputs' => ['requested'],
                    'config' => [],
                ],
                [
                    'type' => 'GetRecommendedGroups',
                    'name' => 'Get Recommended',
                    'name_th' => 'กลุ่มแนะนำ',
                    'icon' => 'ThumbUp',
                    'category' => 'seek_and_post',
                    'inputs' => [],
                    'outputs' => ['groups'],
                    'config' => [
                        'keywords' => ['type' => 'array'],
                        'limit' => ['type' => 'number', 'default' => 20],
                    ],
                ],
                [
                    'type' => 'PostToGroups',
                    'name' => 'Post to Groups',
                    'name_th' => 'โพสต์ไปกลุ่ม',
                    'icon' => 'Send',
                    'category' => 'seek_and_post',
                    'inputs' => ['groups', 'content', 'media'],
                    'outputs' => ['results'],
                    'config' => [
                        'postsPerGroup' => ['type' => 'number', 'default' => 1],
                        'smartTiming' => ['type' => 'boolean', 'default' => true],
                    ],
                ],
            ],
            'social' => [
                [
                    'type' => 'PostToFacebook',
                    'name' => 'Post to Facebook',
                    'name_th' => 'โพสต์ Facebook',
                    'icon' => 'Facebook',
                    'category' => 'social',
                    'inputs' => ['content', 'media'],
                    'outputs' => ['result'],
                    'config' => [
                        'pageId' => ['type' => 'string'],
                        'postType' => ['type' => 'select', 'options' => ['feed', 'story', 'reel']],
                    ],
                ],
                [
                    'type' => 'PostToInstagram',
                    'name' => 'Post to Instagram',
                    'name_th' => 'โพสต์ Instagram',
                    'icon' => 'Instagram',
                    'category' => 'social',
                    'inputs' => ['content', 'media'],
                    'outputs' => ['result'],
                    'config' => [
                        'postType' => ['type' => 'select', 'options' => ['feed', 'story', 'reel']],
                    ],
                ],
                [
                    'type' => 'PostToTikTok',
                    'name' => 'Post to TikTok',
                    'name_th' => 'โพสต์ TikTok',
                    'icon' => 'MusicNote',
                    'category' => 'social',
                    'inputs' => ['video', 'caption'],
                    'outputs' => ['result'],
                    'config' => [],
                ],
                [
                    'type' => 'PostToLINE',
                    'name' => 'Post to LINE',
                    'name_th' => 'ส่ง LINE',
                    'icon' => 'Chat',
                    'category' => 'social',
                    'inputs' => ['content', 'media'],
                    'outputs' => ['result'],
                    'config' => [
                        'channelId' => ['type' => 'string'],
                        'messageType' => ['type' => 'select', 'options' => ['text', 'image', 'flex']],
                    ],
                ],
            ],
            'outputs' => [
                [
                    'type' => 'Output',
                    'name' => 'Output',
                    'name_th' => 'ผลลัพธ์',
                    'icon' => 'Output',
                    'category' => 'output',
                    'inputs' => ['input'],
                    'outputs' => [],
                    'config' => [
                        'label' => ['type' => 'string', 'default' => 'Output'],
                    ],
                ],
                [
                    'type' => 'SaveToDatabase',
                    'name' => 'Save to Database',
                    'name_th' => 'บันทึกฐานข้อมูล',
                    'icon' => 'Storage',
                    'category' => 'output',
                    'inputs' => ['data'],
                    'outputs' => ['success'],
                    'config' => [
                        'table' => ['type' => 'string'],
                    ],
                ],
            ],
        ];

        return response()->json([
            'success' => true,
            'data' => $nodeTypes,
        ]);
    }

    /**
     * Validate a workflow definition
     */
    public function validateWorkflow(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'workflow_json' => 'required|string',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $workflow = json_decode($request->workflow_json, true);
        if (json_last_error() !== JSON_ERROR_NONE) {
            return response()->json([
                'success' => false,
                'valid' => false,
                'errors' => ['Invalid JSON format'],
            ]);
        }

        $errors = [];
        $warnings = [];

        // Check for nodes
        if (empty($workflow['nodes'])) {
            $errors[] = 'Workflow must have at least one node';
        }

        // Check for output nodes
        $hasOutput = false;
        foreach ($workflow['nodes'] ?? [] as $node) {
            if (in_array($node['type'] ?? '', ['Output', 'SaveToDatabase', 'PostToFacebook', 'PostToInstagram', 'PostToTikTok', 'PostToLINE', 'PostToGroups'])) {
                $hasOutput = true;
                break;
            }
        }

        if (!$hasOutput) {
            $warnings[] = 'Workflow has no output node';
        }

        // Check connections
        $nodeIds = array_column($workflow['nodes'] ?? [], 'id');
        foreach ($workflow['connections'] ?? [] as $conn) {
            if (!in_array($conn['from'], $nodeIds)) {
                $errors[] = "Connection references non-existent source node: {$conn['from']}";
            }
            if (!in_array($conn['to'], $nodeIds)) {
                $errors[] = "Connection references non-existent target node: {$conn['to']}";
            }
        }

        return response()->json([
            'success' => true,
            'valid' => empty($errors),
            'errors' => $errors,
            'warnings' => $warnings,
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════

    /**
     * Authorize workflow ownership
     */
    private function authorizeWorkflow(UserWorkflow $workflow): void
    {
        if ($workflow->user_id !== request()->user()->id) {
            abort(403, 'You do not have permission to access this workflow');
        }
    }
}
