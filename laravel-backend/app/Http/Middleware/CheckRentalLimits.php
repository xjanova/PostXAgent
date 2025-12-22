<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use App\Models\UserRental;
use Symfony\Component\HttpFoundation\Response;

/**
 * Middleware to check rental limits before allowing actions
 * ตรวจสอบ limits ของแพ็กเกจก่อนอนุญาตให้ทำ actions
 */
class CheckRentalLimits
{
    /**
     * Handle an incoming request.
     *
     * @param  \Closure(\Illuminate\Http\Request): (\Symfony\Component\HttpFoundation\Response)  $next
     * @param  string  $limitType  Type of limit to check (posts, ai_generations, platforms, brands)
     */
    public function handle(Request $request, Closure $next, string $limitType = 'posts'): Response
    {
        $user = $request->user();

        if (!$user) {
            return response()->json([
                'success' => false,
                'error' => 'unauthorized',
                'message' => 'กรุณาเข้าสู่ระบบก่อนใช้งาน',
            ], 401);
        }

        // Get active rental
        $rental = UserRental::with('rentalPackage')
            ->where('user_id', $user->id)
            ->where('status', 'active')
            ->where('expires_at', '>', now())
            ->first();

        if (!$rental) {
            return response()->json([
                'success' => false,
                'error' => 'no_active_rental',
                'message' => 'คุณยังไม่มีแพ็กเกจที่ใช้งานอยู่ กรุณาซื้อแพ็กเกจก่อน',
                'action' => 'purchase_required',
                'redirect' => '/pricing',
            ], 402);
        }

        $package = $rental->rentalPackage;
        $limitExceeded = false;
        $limitMessage = '';
        $usageInfo = [];

        switch ($limitType) {
            case 'posts':
                $used = $rental->posts_used;
                $limit = $package->posts_limit;
                $remaining = $limit - $used;
                $usageInfo = [
                    'type' => 'posts',
                    'used' => $used,
                    'limit' => $limit,
                    'remaining' => max(0, $remaining),
                ];

                if ($limit > 0 && $used >= $limit) {
                    $limitExceeded = true;
                    $limitMessage = "คุณใช้โควต้าโพสต์ครบแล้ว ({$used}/{$limit}) กรุณาอัพเกรดแพ็กเกจ";
                }
                break;

            case 'ai_generations':
                $used = $rental->ai_generations_used;
                $limit = $package->ai_generations_limit;
                $remaining = $limit - $used;
                $usageInfo = [
                    'type' => 'ai_generations',
                    'used' => $used,
                    'limit' => $limit,
                    'remaining' => max(0, $remaining),
                ];

                if ($limit > 0 && $used >= $limit) {
                    $limitExceeded = true;
                    $limitMessage = "คุณใช้โควต้า AI Generation ครบแล้ว ({$used}/{$limit}) กรุณาอัพเกรดแพ็กเกจ";
                }
                break;

            case 'brands':
                $brandCount = $user->brands()->count();
                $limit = $package->brands_limit;
                $usageInfo = [
                    'type' => 'brands',
                    'used' => $brandCount,
                    'limit' => $limit,
                    'remaining' => max(0, $limit - $brandCount),
                ];

                if ($limit > 0 && $brandCount >= $limit) {
                    $limitExceeded = true;
                    $limitMessage = "คุณสร้างแบรนด์ครบตามโควต้าแล้ว ({$brandCount}/{$limit}) กรุณาอัพเกรดแพ็กเกจ";
                }
                break;

            case 'platforms':
                // Check if the requested platform is allowed
                $requestedPlatform = $request->input('platform');
                $allowedPlatforms = $package->included_platforms ?? [];
                $usageInfo = [
                    'type' => 'platforms',
                    'allowed' => $allowedPlatforms,
                    'requested' => $requestedPlatform,
                ];

                if (!empty($allowedPlatforms) && $requestedPlatform && !in_array($requestedPlatform, $allowedPlatforms)) {
                    $limitExceeded = true;
                    $limitMessage = "แพ็กเกจของคุณไม่รองรับแพลตฟอร์ม {$requestedPlatform} กรุณาอัพเกรดแพ็กเกจ";
                }
                break;

            case 'team_members':
                $teamCount = $user->teamMembers()->count() ?? 0;
                $limit = $package->team_members_limit;
                $usageInfo = [
                    'type' => 'team_members',
                    'used' => $teamCount,
                    'limit' => $limit,
                    'remaining' => max(0, $limit - $teamCount),
                ];

                if ($limit > 0 && $teamCount >= $limit) {
                    $limitExceeded = true;
                    $limitMessage = "คุณเชิญสมาชิกทีมครบตามโควต้าแล้ว ({$teamCount}/{$limit}) กรุณาอัพเกรดแพ็กเกจ";
                }
                break;
        }

        if ($limitExceeded) {
            return response()->json([
                'success' => false,
                'error' => 'limit_exceeded',
                'message' => $limitMessage,
                'usage' => $usageInfo,
                'action' => 'upgrade_required',
                'redirect' => '/pricing',
                'current_package' => [
                    'id' => $package->id,
                    'name' => $package->name,
                ],
            ], 403);
        }

        // Attach rental and usage info to request for use in controllers
        $request->attributes->set('current_rental', $rental);
        $request->attributes->set('rental_usage', $usageInfo);

        return $next($request);
    }
}
