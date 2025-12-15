<?php

declare(strict_types=1);

namespace App\Services;

use App\Models\Post;
use App\Models\AccountPool;
use App\Models\AccountPoolMember;
use Illuminate\Support\Facades\Redis;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Str;
use Illuminate\Support\Collection;

class AIManagerService
{
    private string $queuePrefix = 'laravel:tasks:';
    private string $resultQueue = 'laravel:results';
    private AccountRotationService $rotationService;

    public function __construct(AccountRotationService $rotationService)
    {
        $this->rotationService = $rotationService;
    }

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
     * Publish post with automatic account failover
     * Uses account pool to rotate accounts and retry on failure
     */
    public function publishPostWithFailover(Post $post, AccountPool $pool, int $maxRetries = 3): array
    {
        $accounts = $this->rotationService->getAccountsWithFailover($pool, $maxRetries);

        if ($accounts->isEmpty()) {
            Log::error("No available accounts for posting", [
                'post_id' => $post->id,
                'pool_id' => $pool->id,
            ]);
            return [
                'success' => false,
                'error' => 'No available accounts in pool',
                'attempts' => 0,
            ];
        }

        $attempts = 0;
        $lastError = null;
        $usedAccounts = [];

        foreach ($accounts as $member) {
            $attempts++;

            Log::info("Attempting post with account", [
                'post_id' => $post->id,
                'account_id' => $member->social_account_id,
                'attempt' => $attempts,
            ]);

            // Build payload with this account's credentials
            $payload = $this->buildPayloadWithAccount($post, $member->socialAccount);

            $result = $this->sendTask($post->platform, 'post_content', $payload);

            if (!$result['success']) {
                $lastError = 'Failed to send task';
                $this->rotationService->recordFailure($member, $lastError, $post->id);
                $usedAccounts[] = $member->social_account_id;
                continue;
            }

            $response = $this->waitForResult($result['task_id'], 60);

            if (!$response) {
                $lastError = 'Task timeout';
                $this->rotationService->recordFailure($member, $lastError, $post->id);
                $usedAccounts[] = $member->social_account_id;
                continue;
            }

            if ($response['status'] === 'completed') {
                // Success!
                $this->rotationService->recordSuccess($member, $post->id, [
                    'platform_post_id' => $response['result']['post_id'] ?? null,
                ]);

                return [
                    'success' => true,
                    'post_id' => $response['result']['post_id'] ?? null,
                    'platform_url' => $response['result']['platform_url'] ?? null,
                    'account_id' => $member->social_account_id,
                    'attempts' => $attempts,
                ];
            }

            // Handle specific error types
            $errorCode = $response['error_code'] ?? null;
            $errorMessage = $response['error'] ?? 'Unknown error';

            if ($this->isBanError($errorCode, $errorMessage)) {
                $this->rotationService->handleAccountBan(
                    $member,
                    $errorMessage,
                    ['error_code' => $errorCode, 'response' => $response]
                );
            } elseif ($this->isRateLimitError($errorCode, $errorMessage)) {
                $this->rotationService->handleRateLimit(
                    $member,
                    60, // 1 hour cooldown
                    ['error_code' => $errorCode]
                );
            } else {
                $this->rotationService->recordFailure(
                    $member,
                    $errorMessage,
                    $post->id,
                    ['error_code' => $errorCode, 'response' => $response]
                );
            }

            $lastError = $errorMessage;
            $usedAccounts[] = $member->social_account_id;
        }

        Log::error("All accounts failed for post", [
            'post_id' => $post->id,
            'pool_id' => $pool->id,
            'attempts' => $attempts,
            'last_error' => $lastError,
        ]);

        return [
            'success' => false,
            'error' => $lastError ?? 'All accounts exhausted',
            'attempts' => $attempts,
            'accounts_tried' => $usedAccounts,
        ];
    }

    /**
     * Publish post using account from pool (single attempt with failover tracking)
     */
    public function publishPostFromPool(Post $post, AccountPool $pool): array
    {
        $member = $this->rotationService->getNextAccount($pool);

        if (!$member) {
            return [
                'success' => false,
                'error' => 'No available accounts in pool',
            ];
        }

        $payload = $this->buildPayloadWithAccount($post, $member->socialAccount);

        $result = $this->sendTask($post->platform, 'post_content', $payload);

        if (!$result['success']) {
            $nextMember = $this->rotationService->recordFailure(
                $member,
                'Failed to send task',
                $post->id
            );

            return [
                'success' => false,
                'error' => 'Failed to send task',
                'account_id' => $member->social_account_id,
                'next_account_id' => $nextMember?->social_account_id,
            ];
        }

        $response = $this->waitForResult($result['task_id'], 60);

        if (!$response) {
            $nextMember = $this->rotationService->recordFailure(
                $member,
                'Task timeout',
                $post->id
            );

            return [
                'success' => false,
                'error' => 'Task timeout',
                'account_id' => $member->social_account_id,
                'next_account_id' => $nextMember?->social_account_id,
            ];
        }

        if ($response['status'] === 'completed') {
            $this->rotationService->recordSuccess($member, $post->id);

            return [
                'success' => true,
                'post_id' => $response['result']['post_id'] ?? null,
                'platform_url' => $response['result']['platform_url'] ?? null,
                'account_id' => $member->social_account_id,
            ];
        }

        // Handle failure
        $errorMessage = $response['error'] ?? 'Unknown error';
        $nextMember = $this->rotationService->recordFailure(
            $member,
            $errorMessage,
            $post->id,
            ['response' => $response]
        );

        return [
            'success' => false,
            'error' => $errorMessage,
            'account_id' => $member->social_account_id,
            'next_account_id' => $nextMember?->social_account_id,
        ];
    }

    /**
     * Build task payload using specific social account credentials
     */
    private function buildPayloadWithAccount(Post $post, \App\Models\SocialAccount $account): array
    {
        return [
            'id' => $post->id,
            'platform' => $post->platform,
            'content' => [
                'text' => $post->content,
                'images' => $post->media_urls ?? [],
                'link' => $post->link_url,
                'hashtags' => $post->hashtags ?? [],
            ],
            'credentials' => $account->getCredentials(),
            'scheduled_at' => $post->scheduled_at?->toIso8601String(),
        ];
    }

    /**
     * Check if error indicates account ban
     */
    private function isBanError(?string $errorCode, string $errorMessage): bool
    {
        $banIndicators = [
            'account_banned',
            'account_suspended',
            'user_banned',
            'access_denied',
            'account_disabled',
            'policy_violation',
            '190', // Facebook OAuthException
            '368', // Facebook spam block
        ];

        $messageBanIndicators = [
            'banned',
            'suspended',
            'disabled',
            'blocked',
            'restricted',
            'violated',
        ];

        if ($errorCode && in_array(strtolower($errorCode), $banIndicators)) {
            return true;
        }

        $lowerMessage = strtolower($errorMessage);
        foreach ($messageBanIndicators as $indicator) {
            if (str_contains($lowerMessage, $indicator)) {
                return true;
            }
        }

        return false;
    }

    /**
     * Check if error indicates rate limiting
     */
    private function isRateLimitError(?string $errorCode, string $errorMessage): bool
    {
        $rateLimitIndicators = [
            'rate_limit',
            'too_many_requests',
            '429',
            '88', // Twitter rate limit
            '32', // Facebook rate limit
        ];

        $messageIndicators = [
            'rate limit',
            'too many',
            'slow down',
            'try again later',
        ];

        if ($errorCode && in_array(strtolower($errorCode), $rateLimitIndicators)) {
            return true;
        }

        $lowerMessage = strtolower($errorMessage);
        foreach ($messageIndicators as $indicator) {
            if (str_contains($lowerMessage, $indicator)) {
                return true;
            }
        }

        return false;
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
