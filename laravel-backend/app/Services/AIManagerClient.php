<?php

namespace App\Services;

use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Facades\Cache;
use Exception;

class AIManagerClient
{
    protected array $config;
    protected ?string $currentServer = null;

    public function __construct()
    {
        $this->config = config('aimanager');
        $this->selectServer();
    }

    /**
     * Select the best available server
     */
    protected function selectServer(): void
    {
        $servers = $this->config['servers'] ?? [];

        if (empty($servers)) {
            $this->currentServer = $this->buildUrl($this->config['primary']);
            return;
        }

        // Check cached healthy server
        $cachedServer = Cache::get('aimanager:current_server');
        if ($cachedServer && $this->isServerHealthy($cachedServer)) {
            $this->currentServer = $cachedServer;
            return;
        }

        // Find a healthy server
        foreach ($servers as $server) {
            $url = $this->buildUrl($server);
            if ($this->isServerHealthy($url)) {
                $this->currentServer = $url;
                Cache::put('aimanager:current_server', $url, 60);
                return;
            }
        }

        // Fallback to primary
        $this->currentServer = $this->buildUrl($this->config['primary']);
    }

    /**
     * Build URL from server config
     */
    protected function buildUrl(array $server): string
    {
        $scheme = ($server['use_ssl'] ?? false) ? 'https' : 'http';
        return "{$scheme}://{$server['host']}:{$server['api_port']}";
    }

    /**
     * Check if server is healthy
     */
    protected function isServerHealthy(string $url): bool
    {
        try {
            $response = Http::timeout(5)->get("{$url}/api/status/health");
            return $response->successful() && $response->json('Status') === 'healthy';
        } catch (Exception $e) {
            Log::warning("AI Manager health check failed for {$url}: {$e->getMessage()}");
            return false;
        }
    }

    /**
     * Get current server URL
     */
    public function getServerUrl(): string
    {
        return $this->currentServer ?? $this->buildUrl($this->config['primary']);
    }

    /**
     * Get SignalR Hub URL
     */
    public function getSignalRUrl(): string
    {
        $primary = $this->config['primary'];
        $scheme = ($primary['use_ssl'] ?? false) ? 'https' : 'http';
        return "{$scheme}://{$primary['host']}:{$primary['signalr_port']}/hub/aimanager";
    }

    /**
     * Make API request with retry logic
     */
    protected function request(string $method, string $endpoint, array $data = []): array
    {
        $attempts = $this->config['connection']['retry_attempts'] ?? 3;
        $delay = $this->config['connection']['retry_delay_ms'] ?? 1000;
        $timeout = $this->config['connection']['timeout'] ?? 30;

        $lastException = null;

        for ($i = 0; $i < $attempts; $i++) {
            try {
                $url = "{$this->getServerUrl()}/api/{$endpoint}";

                $response = Http::timeout($timeout)
                    ->withHeaders([
                        'X-API-Key' => $this->config['auth']['api_key'] ?? '',
                        'Accept' => 'application/json',
                    ])
                    ->$method($url, $data);

                if ($response->successful()) {
                    return $response->json();
                }

                Log::warning("AI Manager request failed", [
                    'endpoint' => $endpoint,
                    'status' => $response->status(),
                    'body' => $response->body(),
                ]);

            } catch (Exception $e) {
                $lastException = $e;
                Log::warning("AI Manager request error", [
                    'endpoint' => $endpoint,
                    'attempt' => $i + 1,
                    'error' => $e->getMessage(),
                ]);
            }

            if ($i < $attempts - 1) {
                usleep($delay * 1000);
            }
        }

        throw new Exception(
            "AI Manager request failed after {$attempts} attempts: " .
            ($lastException?->getMessage() ?? 'Unknown error')
        );
    }

    /**
     * Get server statistics
     */
    public function getStats(): array
    {
        return $this->request('get', 'status/stats');
    }

    /**
     * Get server health
     */
    public function getHealth(): array
    {
        return $this->request('get', 'status/health');
    }

    /**
     * Get all workers
     */
    public function getWorkers(): array
    {
        return $this->request('get', 'status/workers');
    }

    /**
     * Get system information
     */
    public function getSystemInfo(): array
    {
        return $this->request('get', 'status/system');
    }

    /**
     * Submit a task
     */
    public function submitTask(array $task): array
    {
        return $this->request('post', 'tasks', $task);
    }

    /**
     * Get task status
     */
    public function getTask(string $taskId): array
    {
        return $this->request('get', "tasks/{$taskId}");
    }

    /**
     * Cancel a task
     */
    public function cancelTask(string $taskId): array
    {
        return $this->request('delete', "tasks/{$taskId}");
    }

    /**
     * Generate content
     */
    public function generateContent(array $params): array
    {
        return $this->request('post', 'tasks/generate-content', $params);
    }

    /**
     * Generate image
     */
    public function generateImage(array $params): array
    {
        return $this->request('post', 'tasks/generate-image', $params);
    }

    /**
     * Post content to platform
     */
    public function postContent(array $params): array
    {
        return $this->request('post', 'tasks/post', $params);
    }

    /**
     * Start the AI Manager
     */
    public function start(): array
    {
        return $this->request('post', 'status/start');
    }

    /**
     * Stop the AI Manager
     */
    public function stop(): array
    {
        return $this->request('post', 'status/stop');
    }
}
