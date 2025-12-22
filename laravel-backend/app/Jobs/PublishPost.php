<?php

namespace App\Jobs;

use App\Models\Post;
use App\Services\AIManagerService;
use App\Notifications\PostPublished;
use App\Notifications\PostFailed;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Foundation\Bus\Dispatchable;
use Illuminate\Queue\InteractsWithQueue;
use Illuminate\Queue\SerializesModels;
use Illuminate\Support\Facades\Log;

class PublishPost implements ShouldQueue
{
    use Dispatchable, InteractsWithQueue, Queueable, SerializesModels;

    /**
     * The number of times the job may be attempted.
     */
    public int $tries = 3;

    /**
     * The number of seconds to wait before retrying the job.
     */
    public int $backoff = 60;

    /**
     * Create a new job instance.
     */
    public function __construct(
        public Post $post
    ) {}

    /**
     * Execute the job.
     */
    public function handle(AIManagerService $aiManager): void
    {
        Log::info('Publishing post', ['post_id' => $this->post->id, 'platform' => $this->post->platform]);

        try {
            // Update status to publishing
            $this->post->update(['status' => Post::STATUS_PUBLISHING]);

            // Get credentials from social account
            $socialAccount = $this->post->socialAccount;

            if (!$socialAccount || !$socialAccount->is_active) {
                throw new \Exception('Social account not found or inactive');
            }

            if ($socialAccount->isTokenExpired()) {
                throw new \Exception('Social account token has expired');
            }

            // Prepare payload for AI Manager
            $payload = $this->post->toWorkerPayload();

            // Send to AI Manager for publishing
            $result = $aiManager->publishPost($payload);

            if ($result['success']) {
                // Update post with platform info
                $this->post->update([
                    'status' => Post::STATUS_PUBLISHED,
                    'published_at' => now(),
                    'platform_post_id' => $result['data']['post_id'] ?? null,
                    'platform_url' => $result['data']['url'] ?? null,
                    'error_message' => null,
                ]);

                Log::info('Post published successfully', [
                    'post_id' => $this->post->id,
                    'platform_post_id' => $result['data']['post_id'] ?? null,
                ]);

                // Notify user
                $this->post->user->notify(new PostPublished($this->post));

            } else {
                throw new \Exception($result['error'] ?? 'Unknown error from AI Manager');
            }

        } catch (\Exception $e) {
            Log::error('Post publishing failed', [
                'post_id' => $this->post->id,
                'error' => $e->getMessage(),
            ]);

            $this->post->update([
                'status' => Post::STATUS_FAILED,
                'error_message' => $e->getMessage(),
            ]);

            // Notify user of failure
            $this->post->user->notify(new PostFailed($this->post, $e->getMessage()));

            // Re-throw to trigger retry if attempts remaining
            throw $e;
        }
    }

    /**
     * Handle a job failure.
     */
    public function failed(\Throwable $exception): void
    {
        Log::error('Post publishing job failed permanently', [
            'post_id' => $this->post->id,
            'error' => $exception->getMessage(),
        ]);

        $this->post->update([
            'status' => Post::STATUS_FAILED,
            'error_message' => 'การโพสต์ล้มเหลวหลังจากลองหลายครั้ง: ' . $exception->getMessage(),
        ]);
    }
}
