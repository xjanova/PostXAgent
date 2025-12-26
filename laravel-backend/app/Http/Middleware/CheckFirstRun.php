<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use App\Services\FirstRunService;
use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * Check First Run Middleware
 * Redirects to setup wizard if this is the first run
 */
class CheckFirstRun
{
    public function __construct(
        private readonly FirstRunService $firstRunService
    ) {
    }

    /**
     * Handle an incoming request.
     */
    public function handle(Request $request, Closure $next): Response
    {
        // Skip check for setup routes
        if ($request->is('setup/*') || $request->is('api/setup/*')) {
            return $next($request);
        }

        // Skip check for API routes that don't need setup
        if ($request->is('api/health') || $request->is('api/status')) {
            return $next($request);
        }

        // Check if first run
        if ($this->firstRunService->isFirstRun()) {
            // For API requests, return JSON response
            if ($request->expectsJson() || $request->is('api/*')) {
                return response()->json([
                    'success' => false,
                    'error' => 'Setup required',
                    'redirect' => '/setup',
                    'code' => 'SETUP_REQUIRED',
                ], 503);
            }

            // For web requests, redirect to setup
            return redirect('/setup');
        }

        return $next($request);
    }
}
