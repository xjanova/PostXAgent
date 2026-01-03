<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Services\AIManagerClient;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Artisan;

class AIManagerController extends Controller
{
    public function __construct(
        private AIManagerClient $client
    ) {}

    /**
     * Get AI Manager connection info
     */
    public function connection(): JsonResponse
    {
        return response()->json([
            'api_url' => $this->client->getServerUrl(),
            'signalr_url' => $this->client->getSignalRUrl(),
            'config' => [
                'host' => config('aimanager.primary.host'),
                'api_port' => config('aimanager.primary.api_port'),
                'signalr_port' => config('aimanager.primary.signalr_port'),
            ],
        ]);
    }

    /**
     * Get AI Manager health status
     */
    public function health(): JsonResponse
    {
        try {
            $health = $this->client->getHealth();
            return response()->json([
                'connected' => true,
                ...$health,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'connected' => false,
                'error' => $e->getMessage(),
            ], 503);
        }
    }

    /**
     * Get AI Manager statistics
     */
    public function stats(): JsonResponse
    {
        try {
            $stats = $this->client->getStats();
            return response()->json($stats);
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
        try {
            $workers = $this->client->getWorkers();
            return response()->json(['workers' => $workers]);
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
        try {
            $info = $this->client->getSystemInfo();
            return response()->json($info);
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
            return response()->json($result);
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
            return response()->json($result);
        } catch (\Exception $e) {
            return response()->json([
                'error' => $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Update AI Manager settings
     */
    public function updateSettings(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'host' => 'required|string',
            'api_port' => 'required|integer|min:1|max:65535',
            'signalr_port' => 'required|integer|min:1|max:65535',
            'use_ssl' => 'boolean',
        ]);

        // Update .env file (in production, use proper config management)
        $envPath = base_path('.env');
        $envContent = file_get_contents($envPath);

        $envContent = preg_replace(
            '/AI_MANAGER_HOST=.*/m',
            "AI_MANAGER_HOST={$validated['host']}",
            $envContent
        );
        $envContent = preg_replace(
            '/AI_MANAGER_API_PORT=.*/m',
            "AI_MANAGER_API_PORT={$validated['api_port']}",
            $envContent
        );
        $envContent = preg_replace(
            '/AI_MANAGER_SIGNALR_PORT=.*/m',
            "AI_MANAGER_SIGNALR_PORT={$validated['signalr_port']}",
            $envContent
        );

        file_put_contents($envPath, $envContent);

        // Clear config cache
        Artisan::call('config:clear');

        return response()->json([
            'success' => true,
            'message' => 'Settings updated successfully',
        ]);
    }
}
