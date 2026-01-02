<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class CheckRentalLimits
{
    /**
     * Handle an incoming request.
     *
     * @param  \Closure(\Illuminate\Http\Request): (\Symfony\Component\HttpFoundation\Response)  $next
     */
    public function handle(Request $request, Closure $next, string $limitType = 'posts'): Response
    {
        $user = $request->user();

        if (!$user) {
            return response()->json([
                'success' => false,
                'message' => 'Unauthenticated',
            ], 401);
        }

        $quota = $user->getUsageQuota();

        switch ($limitType) {
            case 'posts':
                if ($quota['posts_remaining'] <= 0) {
                    return response()->json([
                        'success' => false,
                        'message' => 'Post limit reached. Please upgrade your plan.',
                        'code' => 'POST_LIMIT_REACHED',
                        'quota' => $quota,
                    ], 403);
                }
                break;

            case 'accounts':
                $currentAccounts = $user->socialAccounts()->count();
                if ($currentAccounts >= $quota['accounts_limit']) {
                    return response()->json([
                        'success' => false,
                        'message' => 'Account limit reached. Please upgrade your plan.',
                        'code' => 'ACCOUNT_LIMIT_REACHED',
                        'quota' => $quota,
                    ], 403);
                }
                break;

            case 'brands':
                $currentBrands = $user->brands()->count();
                if ($currentBrands >= $quota['brands_limit']) {
                    return response()->json([
                        'success' => false,
                        'message' => 'Brand limit reached. Please upgrade your plan.',
                        'code' => 'BRAND_LIMIT_REACHED',
                        'quota' => $quota,
                    ], 403);
                }
                break;
        }

        return $next($request);
    }
}
