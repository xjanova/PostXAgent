<?php

namespace App\Services;

use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Facades\Http;
use Exception;

class AIManagerConnectionStatus
{
    const CACHE_KEY = 'aimanager:connection_status';
    const CACHE_TTL = 30; // seconds

    protected AIManagerClient $client;

    public function __construct()
    {
        $this->client = new AIManagerClient();
    }

    /**
     * Get current connection status
     */
    public function getStatus(): array
    {
        return Cache::remember(self::CACHE_KEY, self::CACHE_TTL, function () {
            return $this->checkConnection();
        });
    }

    /**
     * Force refresh connection status
     */
    public function refresh(): array
    {
        Cache::forget(self::CACHE_KEY);
        return $this->getStatus();
    }

    /**
     * Check connection to AI Manager
     */
    protected function checkConnection(): array
    {
        $startTime = microtime(true);
        $status = [
            'connected' => false,
            'server' => [
                'host' => config('aimanager.primary.host'),
                'api_port' => config('aimanager.primary.api_port'),
                'signalr_port' => config('aimanager.primary.signalr_port'),
            ],
            'health' => null,
            'stats' => null,
            'system' => null,
            'latency_ms' => 0,
            'last_check' => now()->toIso8601String(),
            'error' => null,
        ];

        try {
            // Check health endpoint
            $health = $this->client->getHealth();
            $status['connected'] = ($health['Status'] ?? '') === 'healthy';
            $status['health'] = $health;

            if ($status['connected']) {
                // Get stats
                try {
                    $status['stats'] = $this->client->getStats();
                } catch (Exception $e) {
                    Log::warning('Failed to get AI Manager stats: ' . $e->getMessage());
                }

                // Get system info
                try {
                    $status['system'] = $this->client->getSystemInfo();
                } catch (Exception $e) {
                    Log::warning('Failed to get AI Manager system info: ' . $e->getMessage());
                }
            }

        } catch (Exception $e) {
            $status['connected'] = false;
            $status['error'] = $e->getMessage();
            Log::error('AI Manager connection failed: ' . $e->getMessage());
        }

        $status['latency_ms'] = round((microtime(true) - $startTime) * 1000, 2);

        return $status;
    }

    /**
     * Check if connected
     */
    public function isConnected(): bool
    {
        return $this->getStatus()['connected'];
    }

    /**
     * Get connection status as badge color
     */
    public function getStatusBadge(): array
    {
        $status = $this->getStatus();

        if ($status['connected']) {
            $isRunning = ($status['health']['Status'] ?? '') === 'healthy';

            if ($isRunning) {
                return [
                    'status' => 'online',
                    'color' => 'green',
                    'text' => 'Connected & Running',
                    'icon' => 'check-circle',
                ];
            }

            return [
                'status' => 'idle',
                'color' => 'yellow',
                'text' => 'Connected (Idle)',
                'icon' => 'pause-circle',
            ];
        }

        return [
            'status' => 'offline',
            'color' => 'red',
            'text' => 'Disconnected',
            'icon' => 'x-circle',
        ];
    }

    /**
     * Get simplified status for API
     */
    public function getSimpleStatus(): array
    {
        $status = $this->getStatus();
        $badge = $this->getStatusBadge();

        return [
            'connected' => $status['connected'],
            'status' => $badge['status'],
            'status_text' => $badge['text'],
            'status_color' => $badge['color'],
            'server_url' => $this->client->getServerUrl(),
            'signalr_url' => $this->client->getSignalRUrl(),
            'latency_ms' => $status['latency_ms'],
            'last_check' => $status['last_check'],
            'error' => $status['error'],
            'stats' => $status['connected'] ? [
                'total_workers' => $status['stats']['TotalWorkers'] ?? 0,
                'active_workers' => $status['stats']['ActiveWorkers'] ?? 0,
                'tasks_completed' => $status['stats']['TasksCompleted'] ?? 0,
                'tasks_failed' => $status['stats']['TasksFailed'] ?? 0,
                'uptime' => $status['stats']['Uptime'] ?? '00:00:00',
            ] : null,
            'system' => $status['connected'] ? [
                'machine_name' => $status['system']['MachineName'] ?? 'Unknown',
                'processor_count' => $status['system']['ProcessorCount'] ?? 0,
                'os_version' => $status['system']['OSVersion'] ?? 'Unknown',
                'memory_mb' => $status['system']['WorkingSet'] ?? 0,
            ] : null,
        ];
    }
}
