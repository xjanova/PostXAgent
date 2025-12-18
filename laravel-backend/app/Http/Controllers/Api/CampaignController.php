<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Campaign;
use App\Models\Brand;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;

class CampaignController extends Controller
{
    /**
     * List all campaigns for the authenticated user
     */
    public function index(Request $request): JsonResponse
    {
        $campaigns = Campaign::where('user_id', $request->user()->id)
            ->with(['brand:id,name'])
            ->withCount('posts')
            ->when($request->status, fn($q, $status) => $q->where('status', $status))
            ->when($request->brand_id, fn($q, $brandId) => $q->where('brand_id', $brandId))
            ->when($request->search, fn($q, $search) => $q->where('name', 'like', "%{$search}%"))
            ->orderBy('created_at', 'desc')
            ->paginate($request->per_page ?? 20);

        return response()->json([
            'success' => true,
            'data' => $campaigns,
        ]);
    }

    /**
     * Create a new campaign
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'brand_id' => 'required|exists:brands,id',
            'name' => 'required|string|max:255',
            'description' => 'nullable|string|max:1000',
            'type' => 'required|string|in:product_launch,engagement,promotion,awareness,traffic,sales',
            'goal' => 'nullable|string|max:500',
            'target_platforms' => 'required|array|min:1',
            'target_platforms.*' => 'string|in:facebook,instagram,tiktok,twitter,line,youtube,threads,linkedin,pinterest',
            'content_themes' => 'nullable|array',
            'posting_schedule' => 'nullable|array',
            'ai_settings' => 'nullable|array',
            'budget' => 'nullable|numeric|min:0',
            'start_date' => 'required|date',
            'end_date' => 'required|date|after:start_date',
        ]);

        // Verify brand ownership
        $brand = Brand::findOrFail($validated['brand_id']);
        if ($brand->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์ใช้งานแบรนด์นี้',
            ], 403);
        }

        $campaign = Campaign::create([
            'user_id' => $request->user()->id,
            ...$validated,
            'status' => Campaign::STATUS_DRAFT,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'สร้างแคมเปญสำเร็จ',
            'data' => $campaign->load('brand'),
        ], 201);
    }

    /**
     * Get a specific campaign
     */
    public function show(Request $request, Campaign $campaign): JsonResponse
    {
        if ($campaign->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์เข้าถึงแคมเปญนี้',
            ], 403);
        }

        $campaign->load(['brand', 'posts' => fn($q) => $q->latest()->limit(10)]);
        $campaign->loadCount('posts');

        // Calculate stats
        $stats = [
            'total_posts' => $campaign->posts_count,
            'published_posts' => $campaign->posts()->where('status', 'published')->count(),
            'scheduled_posts' => $campaign->posts()->where('status', 'scheduled')->count(),
            'total_engagement' => $campaign->posts()
                ->where('status', 'published')
                ->get()
                ->sum(fn($p) => ($p->metrics['likes'] ?? 0) + ($p->metrics['comments'] ?? 0) + ($p->metrics['shares'] ?? 0)),
        ];

        return response()->json([
            'success' => true,
            'data' => $campaign,
            'stats' => $stats,
        ]);
    }

    /**
     * Update a campaign
     */
    public function update(Request $request, Campaign $campaign): JsonResponse
    {
        if ($campaign->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์แก้ไขแคมเปญนี้',
            ], 403);
        }

        $validated = $request->validate([
            'name' => 'sometimes|string|max:255',
            'description' => 'nullable|string|max:1000',
            'type' => 'sometimes|string|in:product_launch,engagement,promotion,awareness,traffic,sales',
            'goal' => 'nullable|string|max:500',
            'target_platforms' => 'sometimes|array|min:1',
            'content_themes' => 'nullable|array',
            'posting_schedule' => 'nullable|array',
            'ai_settings' => 'nullable|array',
            'budget' => 'nullable|numeric|min:0',
            'start_date' => 'sometimes|date',
            'end_date' => 'sometimes|date|after:start_date',
        ]);

        $campaign->update($validated);

        return response()->json([
            'success' => true,
            'message' => 'อัปเดตแคมเปญสำเร็จ',
            'data' => $campaign->fresh('brand'),
        ]);
    }

    /**
     * Delete a campaign
     */
    public function destroy(Request $request, Campaign $campaign): JsonResponse
    {
        if ($campaign->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์ลบแคมเปญนี้',
            ], 403);
        }

        $campaign->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบแคมเปญสำเร็จ',
        ]);
    }

    /**
     * Start a campaign
     */
    public function start(Request $request, Campaign $campaign): JsonResponse
    {
        if ($campaign->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์ดำเนินการ',
            ], 403);
        }

        if (!in_array($campaign->status, [Campaign::STATUS_DRAFT, Campaign::STATUS_PAUSED, Campaign::STATUS_SCHEDULED])) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถเริ่มแคมเปญในสถานะนี้ได้',
            ], 400);
        }

        $campaign->update(['status' => Campaign::STATUS_ACTIVE]);

        return response()->json([
            'success' => true,
            'message' => 'เริ่มแคมเปญสำเร็จ',
            'data' => $campaign->fresh(),
        ]);
    }

    /**
     * Pause a campaign
     */
    public function pause(Request $request, Campaign $campaign): JsonResponse
    {
        if ($campaign->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์ดำเนินการ',
            ], 403);
        }

        if ($campaign->status !== Campaign::STATUS_ACTIVE) {
            return response()->json([
                'success' => false,
                'message' => 'สามารถหยุดได้เฉพาะแคมเปญที่กำลังทำงานอยู่',
            ], 400);
        }

        $campaign->update(['status' => Campaign::STATUS_PAUSED]);

        return response()->json([
            'success' => true,
            'message' => 'หยุดแคมเปญชั่วคราวสำเร็จ',
            'data' => $campaign->fresh(),
        ]);
    }

    /**
     * Stop/Cancel a campaign
     */
    public function stop(Request $request, Campaign $campaign): JsonResponse
    {
        if ($campaign->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์ดำเนินการ',
            ], 403);
        }

        if (in_array($campaign->status, [Campaign::STATUS_COMPLETED, Campaign::STATUS_CANCELLED])) {
            return response()->json([
                'success' => false,
                'message' => 'แคมเปญนี้จบหรือถูกยกเลิกแล้ว',
            ], 400);
        }

        $campaign->update(['status' => Campaign::STATUS_CANCELLED]);

        return response()->json([
            'success' => true,
            'message' => 'ยกเลิกแคมเปญสำเร็จ',
            'data' => $campaign->fresh(),
        ]);
    }
}
