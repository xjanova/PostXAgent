<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Services\AIManagerConnectionStatus;
use App\Services\AIManagerClient;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;

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
                'error' => $e->getMessage(),
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
                'error' => 'AI Manager not connected',
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
                'error' => $e->getMessage(),
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
                'error' => 'AI Manager not connected',
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
                'error' => $e->getMessage(),
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
                'error' => 'AI Manager not connected',
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
                'error' => $e->getMessage(),
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
                'error' => $e->getMessage(),
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
                'error' => $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Test connection to specific host
     */
    public function testConnection(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'host' => 'required|string',
            'port' => 'required|integer|min:1|max:65535',
        ]);

        $startTime = microtime(true);

        try {
            $url = "http://{$validated['host']}:{$validated['port']}/api/status/health";

            $response = \Http::timeout(5)->get($url);
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
                'error' => $e->getMessage(),
            ]);
        }
    }
}
