<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\WorkflowTemplate;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Validator;

class WorkflowTemplateController extends Controller
{
    /**
     * Get all active templates
     */
    public function index(Request $request): JsonResponse
    {
        $query = WorkflowTemplate::active();

        // Filter by category
        if ($request->has('category')) {
            $query->byCategory($request->category);
        }

        // Filter by platform
        if ($request->has('platform')) {
            $query->forPlatform($request->platform);
        }

        // Filter by type (system/custom)
        if ($request->has('type')) {
            $request->type === 'system' ? $query->system() : $query->custom();
        }

        $templates = $query->orderBy('category')
            ->orderByDesc('use_count')
            ->get()
            ->map(fn($t) => $this->formatTemplate($t));

        return response()->json([
            'success' => true,
            'data' => $templates,
        ]);
    }

    /**
     * Get single template
     */
    public function show(WorkflowTemplate $template): JsonResponse
    {
        return response()->json([
            'success' => true,
            'data' => $this->formatTemplate($template, true),
        ]);
    }

    /**
     * Get templates grouped by category
     */
    public function byCategory(): JsonResponse
    {
        $templates = WorkflowTemplate::active()
            ->orderByDesc('use_count')
            ->get()
            ->groupBy('category')
            ->map(fn($group) => $group->map(fn($t) => $this->formatTemplate($t)));

        $categories = [
            'marketing' => [
                'name' => 'Marketing',
                'name_th' => 'การตลาด',
                'icon' => 'Campaign',
                'templates' => $templates->get('marketing', collect()),
            ],
            'content' => [
                'name' => 'Content',
                'name_th' => 'เนื้อหา',
                'icon' => 'Article',
                'templates' => $templates->get('content', collect()),
            ],
            'engagement' => [
                'name' => 'Engagement',
                'name_th' => 'การมีส่วนร่วม',
                'icon' => 'Forum',
                'templates' => $templates->get('engagement', collect()),
            ],
            'seek_and_post' => [
                'name' => 'Seek and Post',
                'name_th' => 'ค้นหาและโพสต์',
                'icon' => 'TravelExplore',
                'templates' => $templates->get('seek_and_post', collect()),
            ],
            'platform_specific' => [
                'name' => 'Platform Specific',
                'name_th' => 'เฉพาะแพลตฟอร์ม',
                'icon' => 'Devices',
                'templates' => $templates->get('platform_specific', collect()),
            ],
            'special' => [
                'name' => 'Special',
                'name_th' => 'พิเศษ',
                'icon' => 'Star',
                'templates' => $templates->get('special', collect()),
            ],
        ];

        return response()->json([
            'success' => true,
            'data' => $categories,
        ]);
    }

    /**
     * Create a new template (admin only)
     */
    public function store(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'name' => 'required|string|max:255',
            'name_th' => 'nullable|string|max:255',
            'description' => 'nullable|string',
            'description_th' => 'nullable|string',
            'category' => 'required|string|in:marketing,content,engagement,seek_and_post,platform_specific,special',
            'icon' => 'nullable|string|max:50',
            'supported_platforms' => 'required|array|min:1',
            'supported_platforms.*' => 'string|in:facebook,instagram,tiktok,twitter,line,youtube,threads,linkedin,pinterest',
            'variables' => 'nullable|array',
            'workflow_json' => 'required|string',
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

        $template = WorkflowTemplate::create([
            ...$validator->validated(),
            'is_system' => false,
            'created_by' => $request->user()->id,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'Template created successfully',
            'data' => $this->formatTemplate($template, true),
        ], 201);
    }

    /**
     * Update a template (admin only, cannot update system templates)
     */
    public function update(Request $request, WorkflowTemplate $template): JsonResponse
    {
        if ($template->is_system) {
            return response()->json([
                'success' => false,
                'error' => 'Cannot modify system templates',
            ], 403);
        }

        $validator = Validator::make($request->all(), [
            'name' => 'sometimes|string|max:255',
            'name_th' => 'nullable|string|max:255',
            'description' => 'nullable|string',
            'description_th' => 'nullable|string',
            'category' => 'sometimes|string|in:marketing,content,engagement,seek_and_post,platform_specific,special',
            'icon' => 'nullable|string|max:50',
            'supported_platforms' => 'sometimes|array|min:1',
            'supported_platforms.*' => 'string|in:facebook,instagram,tiktok,twitter,line,youtube,threads,linkedin,pinterest',
            'variables' => 'nullable|array',
            'workflow_json' => 'sometimes|string',
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

        $template->update($validator->validated());

        return response()->json([
            'success' => true,
            'message' => 'Template updated successfully',
            'data' => $this->formatTemplate($template, true),
        ]);
    }

    /**
     * Delete a template (admin only, cannot delete system templates)
     */
    public function destroy(WorkflowTemplate $template): JsonResponse
    {
        if ($template->is_system) {
            return response()->json([
                'success' => false,
                'error' => 'Cannot delete system templates',
            ], 403);
        }

        $template->delete();

        return response()->json([
            'success' => true,
            'message' => 'Template deleted successfully',
        ]);
    }

    /**
     * Toggle template active status (admin only)
     */
    public function toggleActive(WorkflowTemplate $template): JsonResponse
    {
        $template->update(['is_active' => !$template->is_active]);

        return response()->json([
            'success' => true,
            'message' => $template->is_active ? 'Template activated' : 'Template deactivated',
            'data' => ['is_active' => $template->is_active],
        ]);
    }

    /**
     * Get template usage statistics
     */
    public function statistics(): JsonResponse
    {
        $stats = [
            'total_templates' => WorkflowTemplate::count(),
            'system_templates' => WorkflowTemplate::system()->count(),
            'custom_templates' => WorkflowTemplate::custom()->count(),
            'active_templates' => WorkflowTemplate::active()->count(),
            'by_category' => WorkflowTemplate::selectRaw('category, count(*) as count')
                ->groupBy('category')
                ->pluck('count', 'category'),
            'top_used' => WorkflowTemplate::orderByDesc('use_count')
                ->limit(10)
                ->get(['id', 'name', 'name_th', 'use_count', 'avg_success_rate']),
            'average_success_rate' => WorkflowTemplate::where('use_count', '>', 0)
                ->avg('avg_success_rate'),
        ];

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }

    /**
     * Format template for API response
     */
    private function formatTemplate(WorkflowTemplate $template, bool $includeWorkflow = false): array
    {
        $data = [
            'id' => $template->id,
            'name' => $template->name,
            'name_th' => $template->name_th,
            'localized_name' => $template->getLocalizedName(),
            'description' => $template->description,
            'description_th' => $template->description_th,
            'localized_description' => $template->getLocalizedDescription(),
            'category' => $template->category,
            'icon' => $template->icon,
            'supported_platforms' => $template->supported_platforms,
            'variables' => $template->variables,
            'is_system' => $template->is_system,
            'is_active' => $template->is_active,
            'use_count' => $template->use_count,
            'avg_success_rate' => round($template->avg_success_rate, 2),
            'created_at' => $template->created_at?->toIso8601String(),
            'updated_at' => $template->updated_at?->toIso8601String(),
        ];

        if ($includeWorkflow) {
            $data['workflow_json'] = $template->workflow_json;
        }

        return $data;
    }
}
