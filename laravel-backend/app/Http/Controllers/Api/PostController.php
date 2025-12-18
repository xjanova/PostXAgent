<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Post;
use App\Models\Brand;
use App\Services\AIManagerService;
use App\Jobs\PublishPost;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;

class PostController extends Controller
{
    public function __construct(
        private AIManagerService $aiManager
    ) {}

    /**
     * List posts for the authenticated user
     */
    public function index(Request $request): JsonResponse
    {
        $posts = Post::where('user_id', $request->user()->id)
            ->with(['brand', 'socialAccount', 'campaign'])
            ->when($request->status, fn($q, $status) => $q->where('status', $status))
            ->when($request->platform, fn($q, $platform) => $q->where('platform', $platform))
            ->when($request->brand_id, fn($q, $brandId) => $q->where('brand_id', $brandId))
            ->latest()
            ->paginate($request->per_page ?? 20);

        return response()->json($posts);
    }

    /**
     * Create a new post
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'brand_id' => 'required|exists:brands,id',
            'social_account_id' => 'required|exists:social_accounts,id',
            'campaign_id' => 'nullable|exists:campaigns,id',
            'content_text' => 'required_without:ai_generate|string|max:10000',
            'content_type' => 'required|in:text,image,video,carousel,story,reel',
            'media_urls' => 'nullable|array',
            'media_urls.*' => 'url',
            'hashtags' => 'nullable|array',
            'link_url' => 'nullable|url',
            'scheduled_at' => 'nullable|date|after:now',
            'ai_generate' => 'nullable|boolean',
            'ai_prompt' => 'required_if:ai_generate,true|string',
        ]);

        // Check user quota
        $user = $request->user();
        $quota = $user->getUsageQuota();

        if ($quota['posts_per_month'] !== -1) {
            $postsThisMonth = Post::where('user_id', $user->id)
                ->whereMonth('created_at', now()->month)
                ->count();

            if ($postsThisMonth >= $quota['posts_per_month']) {
                return response()->json([
                    'error' => 'Monthly post quota exceeded',
                    'limit' => $quota['posts_per_month'],
                    'used' => $postsThisMonth,
                ], 403);
            }
        }

        // Generate content with AI if requested
        if ($request->ai_generate && $request->ai_prompt) {
            $brand = Brand::findOrFail($validated['brand_id']);

            $generated = $this->aiManager->generateContent([
                'prompt' => $request->ai_prompt,
                'brand_info' => $brand->toAIContext(),
                'platform' => $request->platform ?? 'general',
                'content_type' => $validated['content_type'],
            ]);

            if ($generated['success']) {
                $validated['content_text'] = $generated['content']['text'];
                $validated['hashtags'] = $generated['content']['hashtags'];
                $validated['ai_generated'] = true;
                $validated['ai_provider'] = $generated['provider'];
                $validated['ai_prompt'] = $request->ai_prompt;
            }
        }

        $post = Post::create([
            'user_id' => $user->id,
            ...$validated,
            'status' => $request->scheduled_at ? Post::STATUS_SCHEDULED : Post::STATUS_PENDING,
        ]);

        // Queue for publishing if not scheduled
        if (!$request->scheduled_at) {
            PublishPost::dispatch($post);
        }

        return response()->json($post->load(['brand', 'socialAccount']), 201);
    }

    /**
     * Get a specific post
     */
    public function show(Request $request, Post $post): JsonResponse
    {
        $this->authorize('view', $post);

        return response()->json($post->load(['brand', 'socialAccount', 'campaign']));
    }

    /**
     * Update a post
     */
    public function update(Request $request, Post $post): JsonResponse
    {
        $this->authorize('update', $post);

        if (!$post->canBeEdited()) {
            return response()->json([
                'error' => 'Post cannot be edited in current status',
            ], 400);
        }

        $validated = $request->validate([
            'content_text' => 'sometimes|string|max:10000',
            'media_urls' => 'nullable|array',
            'hashtags' => 'nullable|array',
            'link_url' => 'nullable|url',
            'scheduled_at' => 'nullable|date|after:now',
        ]);

        $post->update($validated);

        return response()->json($post->fresh(['brand', 'socialAccount']));
    }

    /**
     * Delete a post
     */
    public function destroy(Request $request, Post $post): JsonResponse
    {
        $this->authorize('delete', $post);

        // If published, try to delete from platform
        if ($post->isPublished() && $post->platform_post_id) {
            $this->aiManager->deletePost($post);
        }

        $post->delete();

        return response()->json(['message' => 'Post deleted']);
    }

    /**
     * Generate AI content for a post
     */
    public function generateContent(Request $request): JsonResponse
    {
        $request->validate([
            'prompt' => 'required|string|max:1000',
            'brand_id' => 'required|exists:brands,id',
            'platform' => 'required|string',
            'content_type' => 'required|string',
        ]);

        $brand = Brand::findOrFail($request->brand_id);

        $result = $this->aiManager->generateContent([
            'prompt' => $request->prompt,
            'brand_info' => $brand->toAIContext(),
            'platform' => $request->platform,
            'content_type' => $request->content_type,
        ]);

        return response()->json($result);
    }

    /**
     * Generate AI image for a post
     */
    public function generateImage(Request $request): JsonResponse
    {
        $request->validate([
            'prompt' => 'required|string|max:1000',
            'style' => 'nullable|string',
            'size' => 'nullable|string',
            'provider' => 'nullable|string|in:auto,dalle,sd,leonardo',
        ]);

        $result = $this->aiManager->generateImage([
            'prompt' => $request->prompt,
            'style' => $request->style ?? 'modern',
            'size' => $request->size ?? '1024x1024',
            'provider' => $request->provider ?? 'auto',
        ]);

        return response()->json($result);
    }

    /**
     * Publish a post immediately
     */
    public function publish(Request $request, Post $post): JsonResponse
    {
        $this->authorize('update', $post);

        if (!in_array($post->status, [Post::STATUS_DRAFT, Post::STATUS_PENDING, Post::STATUS_SCHEDULED])) {
            return response()->json([
                'error' => 'Post cannot be published in current status',
            ], 400);
        }

        PublishPost::dispatch($post);

        $post->update(['status' => Post::STATUS_PUBLISHING]);

        return response()->json([
            'message' => 'Post queued for publishing',
            'post' => $post->fresh(),
        ]);
    }

    /**
     * Get post metrics
     */
    public function metrics(Request $request, Post $post): JsonResponse
    {
        $this->authorize('view', $post);

        if (!$post->isPublished()) {
            return response()->json([
                'error' => 'Post is not published yet',
            ], 400);
        }

        // Fetch fresh metrics from platform
        $metrics = $this->aiManager->getPostMetrics($post);

        if ($metrics['success']) {
            $post->updateMetrics($metrics['data']);
        }

        return response()->json([
            'metrics' => $post->fresh()->metrics,
        ]);
    }

    /**
     * Bulk delete posts
     */
    public function bulkDelete(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'post_ids' => 'required|array|min:1|max:100',
            'post_ids.*' => 'integer|exists:posts,id',
        ]);

        $userId = $request->user()->id;
        $postIds = $validated['post_ids'];

        // Verify ownership of all posts
        $posts = Post::where('user_id', $userId)
            ->whereIn('id', $postIds)
            ->get();

        if ($posts->count() !== count($postIds)) {
            return response()->json([
                'success' => false,
                'error' => 'Some posts do not belong to you or do not exist',
            ], 403);
        }

        // Delete from platform if published
        foreach ($posts as $post) {
            if ($post->isPublished() && $post->platform_post_id) {
                $this->aiManager->deletePost($post);
            }
        }

        $deletedCount = Post::whereIn('id', $postIds)->delete();

        return response()->json([
            'success' => true,
            'message' => "Successfully deleted {$deletedCount} posts",
            'deleted_count' => $deletedCount,
        ]);
    }

    /**
     * Bulk update post status
     */
    public function bulkUpdateStatus(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'post_ids' => 'required|array|min:1|max:100',
            'post_ids.*' => 'integer|exists:posts,id',
            'status' => 'required|string|in:draft,pending,scheduled',
        ]);

        $userId = $request->user()->id;
        $postIds = $validated['post_ids'];

        // Verify ownership and editable status
        $posts = Post::where('user_id', $userId)
            ->whereIn('id', $postIds)
            ->get();

        if ($posts->count() !== count($postIds)) {
            return response()->json([
                'success' => false,
                'error' => 'Some posts do not belong to you or do not exist',
            ], 403);
        }

        $nonEditable = $posts->filter(fn($p) => !$p->canBeEdited());
        if ($nonEditable->isNotEmpty()) {
            return response()->json([
                'success' => false,
                'error' => 'Some posts cannot be edited in their current status',
                'non_editable_ids' => $nonEditable->pluck('id'),
            ], 400);
        }

        $updatedCount = Post::whereIn('id', $postIds)->update([
            'status' => $validated['status'],
        ]);

        return response()->json([
            'success' => true,
            'message' => "Successfully updated {$updatedCount} posts",
            'updated_count' => $updatedCount,
        ]);
    }

    /**
     * Bulk publish posts
     */
    public function bulkPublish(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'post_ids' => 'required|array|min:1|max:50',
            'post_ids.*' => 'integer|exists:posts,id',
        ]);

        $userId = $request->user()->id;
        $postIds = $validated['post_ids'];

        $posts = Post::where('user_id', $userId)
            ->whereIn('id', $postIds)
            ->whereIn('status', [Post::STATUS_DRAFT, Post::STATUS_PENDING, Post::STATUS_SCHEDULED])
            ->get();

        if ($posts->isEmpty()) {
            return response()->json([
                'success' => false,
                'error' => 'No publishable posts found',
            ], 400);
        }

        $queuedCount = 0;
        foreach ($posts as $post) {
            PublishPost::dispatch($post);
            $post->update(['status' => Post::STATUS_PUBLISHING]);
            $queuedCount++;
        }

        return response()->json([
            'success' => true,
            'message' => "Queued {$queuedCount} posts for publishing",
            'queued_count' => $queuedCount,
        ]);
    }

    /**
     * Bulk schedule posts
     */
    public function bulkSchedule(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'post_ids' => 'required|array|min:1|max:100',
            'post_ids.*' => 'integer|exists:posts,id',
            'scheduled_at' => 'required|date|after:now',
            'interval_minutes' => 'nullable|integer|min:5|max:1440',
        ]);

        $userId = $request->user()->id;
        $postIds = $validated['post_ids'];
        $intervalMinutes = $validated['interval_minutes'] ?? 0;

        $posts = Post::where('user_id', $userId)
            ->whereIn('id', $postIds)
            ->get();

        if ($posts->count() !== count($postIds)) {
            return response()->json([
                'success' => false,
                'error' => 'Some posts do not belong to you or do not exist',
            ], 403);
        }

        $nonEditable = $posts->filter(fn($p) => !$p->canBeEdited());
        if ($nonEditable->isNotEmpty()) {
            return response()->json([
                'success' => false,
                'error' => 'Some posts cannot be scheduled in their current status',
                'non_editable_ids' => $nonEditable->pluck('id'),
            ], 400);
        }

        $scheduledAt = \Carbon\Carbon::parse($validated['scheduled_at']);
        $scheduledCount = 0;

        foreach ($posts as $index => $post) {
            $postScheduleTime = $intervalMinutes > 0
                ? $scheduledAt->copy()->addMinutes($index * $intervalMinutes)
                : $scheduledAt;

            $post->update([
                'scheduled_at' => $postScheduleTime,
                'status' => Post::STATUS_SCHEDULED,
            ]);
            $scheduledCount++;
        }

        return response()->json([
            'success' => true,
            'message' => "Successfully scheduled {$scheduledCount} posts",
            'scheduled_count' => $scheduledCount,
        ]);
    }
}
