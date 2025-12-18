<?php

declare(strict_types=1);

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Symfony\Component\HttpFoundation\Response;

/**
 * Middleware for internal API authentication
 * ใช้สำหรับ C# Core และ internal services ติดต่อกับ Laravel backend
 */
class InternalApiAuth
{
    /**
     * Handle an incoming request.
     *
     * @param  \Closure(\Illuminate\Http\Request): (\Symfony\Component\HttpFoundation\Response)  $next
     */
    public function handle(Request $request, Closure $next): Response
    {
        $apiKey = $request->header('X-Internal-API-Key');
        $expectedKey = config('services.internal_api_key');

        // Also allow IP-based authentication for local services
        $allowedIps = config('services.internal_allowed_ips', ['127.0.0.1', '::1']);
        $clientIp = $request->ip();

        // Check API key
        if ($apiKey && $apiKey === $expectedKey) {
            return $next($request);
        }

        // Check IP whitelist (for local development)
        if (in_array($clientIp, $allowedIps)) {
            return $next($request);
        }

        return response()->json([
            'success' => false,
            'error' => 'unauthorized',
            'message' => 'Invalid or missing internal API key',
        ], 401);
    }
}
