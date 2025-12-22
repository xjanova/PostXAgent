<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\UserRental;
use App\Models\User;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Log;

/**
 * Usage Tracking Controller - สำหรับ C# Core ติดต่อ
 * ใช้ API key authentication สำหรับ internal service communication
 */
class UsageTrackingController extends Controller
{
    /**
     * Increment usage counter (posts, ai_generations, etc.)
     * POST /api/internal/usage/increment
     *
     * Called by C# Core when a post is created or AI is generated
     */
    public function increment(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'user_id' => 'required|integer|exists:users,id',
            'type' => 'required|string|in:posts,ai_generations',
            'amount' => 'sometimes|integer|min:1',
            'metadata' => 'sometimes|array',
        ]);

        $userId = $validated['user_id'];
        $type = $validated['type'];
        $amount = $validated['amount'] ?? 1;

        // Get active rental
        $rental = UserRental::with('rentalPackage')
            ->where('user_id', $userId)
            ->where('status', 'active')
            ->where('expires_at', '>', now())
            ->first();

        if (!$rental) {
            return response()->json([
                'success' => false,
                'error' => 'no_active_rental',
                'message' => 'User does not have an active rental',
            ], 404);
        }

        $package = $rental->rentalPackage;
        $field = $type === 'posts' ? 'posts_used' : 'ai_generations_used';
        $limitField = $type === 'posts' ? 'posts_limit' : 'ai_generations_limit';

        $currentUsage = $rental->$field;
        $limit = $package->$limitField;
        $newUsage = $currentUsage + $amount;

        // Check if would exceed limit
        if ($limit > 0 && $newUsage > $limit) {
            return response()->json([
                'success' => false,
                'error' => 'limit_exceeded',
                'message' => "Would exceed {$type} limit",
                'usage' => [
                    'current' => $currentUsage,
                    'limit' => $limit,
                    'requested' => $amount,
                ],
            ], 403);
        }

        // Increment usage
        $rental->increment($field, $amount);

        Log::info("Usage incremented", [
            'user_id' => $userId,
            'rental_id' => $rental->id,
            'type' => $type,
            'amount' => $amount,
            'new_total' => $newUsage,
        ]);

        return response()->json([
            'success' => true,
            'data' => [
                'type' => $type,
                'used' => $newUsage,
                'limit' => $limit,
                'remaining' => $limit > 0 ? max(0, $limit - $newUsage) : -1,
            ],
            'message' => 'Usage incremented successfully',
        ]);
    }

    /**
     * Check if user can perform an action (validate limits)
     * POST /api/internal/usage/check
     *
     * Called by C# Core before processing a task
     */
    public function check(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'user_id' => 'required|integer|exists:users,id',
            'type' => 'required|string|in:posts,ai_generations,platforms,brands',
            'platform' => 'sometimes|string',
            'amount' => 'sometimes|integer|min:1',
        ]);

        $userId = $validated['user_id'];
        $type = $validated['type'];
        $amount = $validated['amount'] ?? 1;

        // Get active rental
        $rental = UserRental::with('rentalPackage')
            ->where('user_id', $userId)
            ->where('status', 'active')
            ->where('expires_at', '>', now())
            ->first();

        if (!$rental) {
            return response()->json([
                'success' => false,
                'allowed' => false,
                'error' => 'no_active_rental',
                'message' => 'User does not have an active rental',
            ]);
        }

        $package = $rental->rentalPackage;
        $allowed = true;
        $reason = null;
        $usage = [];

        switch ($type) {
            case 'posts':
                $used = $rental->posts_used;
                $limit = $package->posts_limit;
                $remaining = $limit - $used;
                $usage = [
                    'type' => 'posts',
                    'used' => $used,
                    'limit' => $limit,
                    'remaining' => max(0, $remaining),
                ];

                if ($limit > 0 && ($used + $amount) > $limit) {
                    $allowed = false;
                    $reason = "Post limit exceeded ({$used}/{$limit})";
                }
                break;

            case 'ai_generations':
                $used = $rental->ai_generations_used;
                $limit = $package->ai_generations_limit;
                $remaining = $limit - $used;
                $usage = [
                    'type' => 'ai_generations',
                    'used' => $used,
                    'limit' => $limit,
                    'remaining' => max(0, $remaining),
                ];

                if ($limit > 0 && ($used + $amount) > $limit) {
                    $allowed = false;
                    $reason = "AI generation limit exceeded ({$used}/{$limit})";
                }
                break;

            case 'platforms':
                $platform = $validated['platform'] ?? null;
                $allowedPlatforms = $package->platforms ?? [];
                $usage = [
                    'type' => 'platforms',
                    'allowed_platforms' => $allowedPlatforms,
                    'requested' => $platform,
                ];

                if (!empty($allowedPlatforms) && $platform && !in_array($platform, $allowedPlatforms)) {
                    $allowed = false;
                    $reason = "Platform {$platform} not included in package";
                }
                break;

            case 'brands':
                $user = User::find($userId);
                $brandCount = $user->brands()->count();
                $limit = $package->brands_limit;
                $usage = [
                    'type' => 'brands',
                    'used' => $brandCount,
                    'limit' => $limit,
                    'remaining' => max(0, $limit - $brandCount),
                ];

                if ($limit > 0 && $brandCount >= $limit) {
                    $allowed = false;
                    $reason = "Brand limit reached ({$brandCount}/{$limit})";
                }
                break;
        }

        return response()->json([
            'success' => true,
            'allowed' => $allowed,
            'reason' => $reason,
            'usage' => $usage,
            'rental' => [
                'id' => $rental->id,
                'package' => $package->name,
                'expires_at' => $rental->expires_at->toIso8601String(),
            ],
        ]);
    }

    /**
     * Get user's current usage status
     * GET /api/internal/usage/{userId}
     */
    public function status(int $userId): JsonResponse
    {
        $user = User::find($userId);

        if (!$user) {
            return response()->json([
                'success' => false,
                'error' => 'user_not_found',
                'message' => 'User not found',
            ], 404);
        }

        // Get active rental
        $rental = UserRental::with('rentalPackage')
            ->where('user_id', $userId)
            ->where('status', 'active')
            ->where('expires_at', '>', now())
            ->first();

        if (!$rental) {
            return response()->json([
                'success' => true,
                'has_rental' => false,
                'data' => null,
            ]);
        }

        $package = $rental->rentalPackage;

        return response()->json([
            'success' => true,
            'has_rental' => true,
            'data' => [
                'rental_id' => $rental->id,
                'package' => [
                    'id' => $package->id,
                    'name' => $package->name,
                    'slug' => $package->slug,
                ],
                'status' => $rental->status,
                'starts_at' => $rental->starts_at?->toIso8601String(),
                'expires_at' => $rental->expires_at->toIso8601String(),
                'days_remaining' => now()->diffInDays($rental->expires_at, false),
                'usage' => [
                    'posts' => [
                        'used' => $rental->posts_used,
                        'limit' => $package->posts_limit,
                        'remaining' => max(0, $package->posts_limit - $rental->posts_used),
                    ],
                    'ai_generations' => [
                        'used' => $rental->ai_generations_used,
                        'limit' => $package->ai_generations_limit,
                        'remaining' => max(0, $package->ai_generations_limit - $rental->ai_generations_used),
                    ],
                ],
                'limits' => [
                    'brands' => $package->brands_limit,
                    'platforms' => $package->platforms ?? [],
                    'team_members' => $package->team_members_limit,
                ],
            ],
        ]);
    }

    /**
     * Validate API key for internal service authentication
     * POST /api/internal/validate-key
     */
    public function validateKey(Request $request): JsonResponse
    {
        $apiKey = $request->header('X-Internal-API-Key');
        $expectedKey = config('services.internal_api_key');

        if (!$apiKey || $apiKey !== $expectedKey) {
            return response()->json([
                'success' => false,
                'error' => 'invalid_api_key',
                'message' => 'Invalid or missing API key',
            ], 401);
        }

        return response()->json([
            'success' => true,
            'message' => 'API key is valid',
        ]);
    }
}
