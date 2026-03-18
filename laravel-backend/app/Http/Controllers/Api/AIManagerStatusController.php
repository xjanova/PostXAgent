<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Services\AIManagerConnectionStatus;
use App\Services\AIManagerClient;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Http;

class AIManagerStatusController extends Controller
{
    public function __construct(
        private AIManagerConnectionStatus $connectionStatus,
        private AIManagerClient $client
    ) {}

    /**
     * Get connection status (simple)
     * ใช้สำหรับแสดงสถานะการเชื่อมต่อใน Dashboard
     */
    public function status(): JsonResponse
    {
        return response()->json(
            $this->connectionStatus->getSimpleStatus()
        );
    }

    /**
     * Get full connection status (detailed)
     */
    public function fullStatus(): JsonResponse
    {
        return response()->json(
            $this->connectionStatus->getStatus()
        );
    }

    /**
     * Force refresh connection status
     */
    public function refresh(): JsonResponse
    {
        return response()->json(
            $this->connectionStatus->refresh()
        );
    }

    /**
     * Get status badge for UI
     */
    public function badge(): JsonResponse
    {
        return response()->json(
            $this->connectionStatus->getStatusBadge()
        );
    }

    /**
     * Ping AI Manager (quick health check)
     */
    public function ping(): JsonResponse
    {
        $startTime = microtime(true);

        try {
            $health = $this->client->getHealth();
            $latency = round((microtime(true) - $startTime) * 1000, 2);

            return response()->json([
                'success' => true,
                'pong' => true,
                'latency_ms' => $latency,
                'status' => $health['Status'] ?? 'unknown',
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'pong' => false,
                'message' => 'AI Manager is not reachable',
                'errors' => [],
            ], 503);
        }
    }

    /**
     * Get real-time stats
     */
    public function stats(): JsonResponse
    {
        if (!$this->connectionStatus->isConnected()) {
            return response()->json([
                'success' => false,
                'message' => 'AI Manager not connected',
                'errors' => [],
            ], 503);
        }

        try {
            $stats = $this->client->getStats();
            return response()->json([
                'success' => true,
                'data' => $stats,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'message' => 'Failed to retrieve AI Manager stats',
                'errors' => [],
            ], 500);
        }
    }

    /**
     * Get all workers
     */
    public function workers(): JsonResponse
    {
        if (!$this->connectionStatus->isConnected()) {
            return response()->json([
                'success' => false,
                'message' => 'AI Manager not connected',
                'errors' => [],
            ], 503);
        }

        try {
            $workers = $this->client->getWorkers();
            return response()->json([
                'success' => true,
                'data' => $workers,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'message' => 'Failed to retrieve workers',
                'errors' => [],
            ], 500);
        }
    }

    /**
     * Get system information
     */
    public function system(): JsonResponse
    {
        if (!$this->connectionStatus->isConnected()) {
            return response()->json([
                'success' => false,
                'message' => 'AI Manager not connected',
                'errors' => [],
            ], 503);
        }

        try {
            $system = $this->client->getSystemInfo();
            return response()->json([
                'success' => true,
                'data' => $system,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'message' => 'Failed to retrieve system information',
                'errors' => [],
            ], 500);
        }
    }

    /**
     * Start AI Manager
     */
    public function start(): JsonResponse
    {
        try {
            $result = $this->client->start();
            $this->connectionStatus->refresh();

            return response()->json([
                'success' => true,
                'message' => 'AI Manager started successfully',
                'data' => $result,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'message' => 'Failed to start AI Manager',
                'errors' => [],
            ], 500);
        }
    }

    /**
     * Stop AI Manager
     */
    public function stop(): JsonResponse
    {
        try {
            $result = $this->client->stop();
            $this->connectionStatus->refresh();

            return response()->json([
                'success' => true,
                'message' => 'AI Manager stopped successfully',
                'data' => $result,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'message' => 'Failed to stop AI Manager',
                'errors' => [],
            ], 500);
        }
    }

    /**
     * Test connection to specific host
     */
    public function testConnection(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'host' => 'required|string|max:255',
            'port' => 'required|integer|min:1024|max:65535',
        ]);

        // SSRF protection: only allow connections to known safe hosts
        $allowedHosts = array_filter([
            'localhost',
            '127.0.0.1',
            '::1',
            config('aimanager.host'),
        ]);

        $host = strtolower(trim($validated['host']));

        if (!in_array($host, $allowedHosts, true)) {
            return response()->json([
                'success' => false,
                'error' => 'Host not allowed. Only localhost and the configured AI Manager host are permitted.',
            ], 403);
        }

        $startTime = microtime(true);

        try {
            $url = "http://{$host}:{$validated['port']}/api/status/health";

            $response = Http::timeout(5)->get($url);
            $latency = round((microtime(true) - $startTime) * 1000, 2);

            if ($response->successful()) {
                return response()->json([
                    'success' => true,
                    'reachable' => true,
                    'latency_ms' => $latency,
                    'response' => $response->json(),
                ]);
            }

            return response()->json([
                'success' => false,
                'reachable' => true,
                'latency_ms' => $latency,
                'error' => 'Server returned error: ' . $response->status(),
            ]);

        } catch (\Exception $e) {
            return response()->json([
                'success' => false,
                'reachable' => false,
                'message' => 'Connection test failed',
                'errors' => [],
            ]);
        }
    }
}
