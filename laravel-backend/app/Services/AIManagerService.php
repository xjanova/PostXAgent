<?php

namespace App\Services;

use App\Models\Post;
use Illuminate\Support\Facades\Redis;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Str;

class AIManagerService
{
    private string $queuePrefix = 'laravel:tasks:';
    private string $resultQueue = 'laravel:results';

    /**
     * Send a task to the AI Manager
     */
    public function sendTask(string $platform, string $type, array $payload): array
    {
        $taskId = Str::uuid()->toString();

        $task = [
            'id' => $taskId,
            'type' => $type,
            'platform' => $platform,
            'user_id' => auth()->id() ?? 0,
            'brand_id' => $payload['brand_id'] ?? 0,
            'payload' => $payload,
            'priority' => $payload['priority'] ?? 0,
            'created_at' => now()->toIso8601String(),
            'status' => 'pending',
        ];

        // Send to AI Manager via Redis
        Redis::lpush($this->queuePrefix . $platform, json_encode($task));

        Log::info("Task sent to AI Manager", ['task_id' => $taskId, 'type' => $type]);

        return [
            'success' => true,
            'task_id' => $taskId,
        ];
    }

    /**
     * Generate content using AI
     */
    public function generateContent(array $params): array
    {
        $platform = $params['platform'] ?? 'general';

        $result = $this->sendTask($platform, 'generate_content', $params);

        if (!$result['success']) {
            return ['success' => false, 'error' => 'Failed to send task'];
        }

        // Wait for result (with timeout)
        $response = $this->waitForResult($result['task_id'], 30);

        if (!$response) {
            return ['success' => false, 'error' => 'Task timeout'];
        }

        return [
            'success' => $response['status'] === 'completed',
            'content' => $response['result']['content'] ?? null,
            'hashtags' => $response['result']['hashtags'] ?? [],
            'provider' => $response['result']['provider'] ?? 'unknown',
        ];
    }

    /**
     * Generate image using AI
     */
    public function generateImage(array $params): array
    {
        $result = $this->sendTask('general', 'generate_image', $params);

        if (!$result['success']) {
            return ['success' => false, 'error' => 'Failed to send task'];
        }

        // Wait for result (image generation may take longer)
        $response = $this->waitForResult($result['task_id'], 60);

        if (!$response) {
            return ['success' => false, 'error' => 'Task timeout'];
        }

        return [
            'success' => $response['status'] === 'completed',
            'image_url' => $response['result']['image_url'] ?? null,
            'provider' => $response['result']['provider'] ?? 'unknown',
        ];
    }

    /**
     * Publish a post to a platform
     */
    public function publishPost(Post $post): array
    {
        $payload = $post->toWorkerPayload();

        $result = $this->sendTask($post->platform, 'post_content', $payload);

        if (!$result['success']) {
            return ['success' => false, 'error' => 'Failed to send task'];
        }

        // Wait for result
        $response = $this->waitForResult($result['task_id'], 60);

        if (!$response) {
            return ['success' => false, 'error' => 'Task timeout'];
        }

        return [
            'success' => $response['status'] === 'completed',
            'post_id' => $response['result']['post_id'] ?? null,
            'platform_url' => $response['result']['platform_url'] ?? null,
            'error' => $response['error'] ?? null,
        ];
    }

    /**
     * Get metrics for a post
     */
    public function getPostMetrics(Post $post): array
    {
        $payload = [
            'post_ids' => [$post->platform_post_id],
            'credentials' => $post->socialAccount->getCredentials(),
        ];

        $result = $this->sendTask($post->platform, 'analyze_metrics', $payload);

        if (!$result['success']) {
            return ['success' => false, 'error' => 'Failed to send task'];
        }

        $response = $this->waitForResult($result['task_id'], 30);

        if (!$response) {
            return ['success' => false, 'error' => 'Task timeout'];
        }

        return [
            'success' => $response['status'] === 'completed',
            'data' => $response['result']['metrics'][0] ?? [],
        ];
    }

    /**
     * Delete a post from platform
     */
    public function deletePost(Post $post): array
    {
        $payload = [
            'post_id' => $post->platform_post_id,
            'credentials' => $post->socialAccount->getCredentials(),
        ];

        $result = $this->sendTask($post->platform, 'delete_post', $payload);

        if (!$result['success']) {
            return ['success' => false, 'error' => 'Failed to send task'];
        }

        $response = $this->waitForResult($result['task_id'], 30);

        return [
            'success' => $response && $response['status'] === 'completed',
        ];
    }

    /**
     * Get AI Manager statistics
     */
    public function getStats(): array
    {
        $stats = Redis::get('orchestrator:stats');

        if (!$stats) {
            return [
                'connected' => false,
                'message' => 'AI Manager not connected',
            ];
        }

        return [
            'connected' => true,
            ...json_decode($stats, true),
        ];
    }

    /**
     * Wait for a task result from Redis
     */
    private function waitForResult(string $taskId, int $timeoutSeconds = 30): ?array
    {
        $startTime = time();

        while (time() - $startTime < $timeoutSeconds) {
            // Check result queue
            $results = Redis::lrange($this->resultQueue, 0, -1);

            foreach ($results as $index => $resultJson) {
                $result = json_decode($resultJson, true);

                if (isset($result['id']) && $result['id'] === $taskId) {
                    // Remove from queue
                    Redis::lrem($this->resultQueue, 1, $resultJson);
                    return $result;
                }
            }

            usleep(100000); // 100ms
        }

        return null;
    }
}
