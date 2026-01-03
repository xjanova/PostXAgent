<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

class CheckActiveRental
{
    /**
     * Handle an incoming request.
     *
     * @param  \Closure(\Illuminate\Http\Request): (\Symfony\Component\HttpFoundation\Response)  $next
     */
    public function handle(Request $request, Closure $next): Response
    {
        $user = $request->user();

        if (!$user) {
            return response()->json([
                'success' => false,
                'message' => 'Unauthenticated',
            ], 401);
        }

        $activeRental = $user->activeRental();

        if (!$activeRental) {
            return response()->json([
                'success' => false,
                'message' => 'No active subscription. Please subscribe to continue.',
                'code' => 'NO_ACTIVE_RENTAL',
            ], 403);
        }

        return $next($request);
    }
}
