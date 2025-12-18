<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Brand;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;

class BrandController extends Controller
{
    /**
     * List all brands for the authenticated user
     */
    public function index(Request $request): JsonResponse
    {
        $brands = Brand::where('user_id', $request->user()->id)
            ->withCount(['campaigns', 'posts', 'socialAccounts'])
            ->when($request->search, fn($q, $search) => $q->where('name', 'like', "%{$search}%"))
            ->when($request->industry, fn($q, $industry) => $q->where('industry', $industry))
            ->orderBy('created_at', 'desc')
            ->paginate($request->per_page ?? 20);

        return response()->json([
            'success' => true,
            'data' => $brands,
        ]);
    }

    /**
     * Create a new brand
     */
    public function store(Request $request): JsonResponse
    {
        // Check quota
        $user = $request->user();
        $quota = $user->getUsageQuota();

        if ($quota['brands'] !== -1) {
            $currentBrands = Brand::where('user_id', $user->id)->count();
            if ($currentBrands >= $quota['brands']) {
                return response()->json([
                    'success' => false,
                    'message' => 'ถึงขีดจำกัดจำนวนแบรนด์แล้ว',
                    'limit' => $quota['brands'],
                    'used' => $currentBrands,
                ], 403);
            }
        }

        $validated = $request->validate([
            'name' => 'required|string|max:255',
            'description' => 'nullable|string|max:1000',
            'industry' => 'nullable|string|max:100',
            'target_audience' => 'nullable|string|max:500',
            'tone' => 'nullable|string|in:professional,friendly,casual,formal,trendy,urgent',
            'logo_url' => 'nullable|url',
            'brand_colors' => 'nullable|array',
            'brand_colors.*' => 'string|max:20',
            'keywords' => 'nullable|array',
            'keywords.*' => 'string|max:50',
            'hashtags' => 'nullable|array',
            'hashtags.*' => 'string|max:50',
            'website_url' => 'nullable|url',
            'settings' => 'nullable|array',
        ]);

        $brand = Brand::create([
            'user_id' => $user->id,
            ...$validated,
            'is_active' => true,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'สร้างแบรนด์สำเร็จ',
            'data' => $brand,
        ], 201);
    }

    /**
     * Get a specific brand
     */
    public function show(Request $request, Brand $brand): JsonResponse
    {
        if ($brand->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์เข้าถึงแบรนด์นี้',
            ], 403);
        }

        $brand->loadCount(['campaigns', 'posts', 'socialAccounts']);
        $brand->load(['socialAccounts', 'campaigns' => fn($q) => $q->latest()->limit(5)]);

        return response()->json([
            'success' => true,
            'data' => $brand,
        ]);
    }

    /**
     * Update a brand
     */
    public function update(Request $request, Brand $brand): JsonResponse
    {
        if ($brand->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์แก้ไขแบรนด์นี้',
            ], 403);
        }

        $validated = $request->validate([
            'name' => 'sometimes|string|max:255',
            'description' => 'nullable|string|max:1000',
            'industry' => 'nullable|string|max:100',
            'target_audience' => 'nullable|string|max:500',
            'tone' => 'nullable|string|in:professional,friendly,casual,formal,trendy,urgent',
            'logo_url' => 'nullable|url',
            'brand_colors' => 'nullable|array',
            'keywords' => 'nullable|array',
            'hashtags' => 'nullable|array',
            'website_url' => 'nullable|url',
            'settings' => 'nullable|array',
            'is_active' => 'sometimes|boolean',
        ]);

        $brand->update($validated);

        return response()->json([
            'success' => true,
            'message' => 'อัปเดตแบรนด์สำเร็จ',
            'data' => $brand->fresh(),
        ]);
    }

    /**
     * Delete a brand
     */
    public function destroy(Request $request, Brand $brand): JsonResponse
    {
        if ($brand->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์ลบแบรนด์นี้',
            ], 403);
        }

        $brand->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบแบรนด์สำเร็จ',
        ]);
    }
}
