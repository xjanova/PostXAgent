<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Comment;
use App\Models\Post;
use App\Models\ResponseTone;
use App\Services\AIManagerService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;

class CommentController extends Controller
{
    public function __construct(
        private AIManagerService $aiManager
    ) {}

    /**
     * Get comments for a post
     */
    public function index(Request $request, Post $post): JsonResponse
    {
        $this->authorize('view', $post);

        $comments = $post->comments()
            ->with(['replies'])
            ->when($request->status, fn($q, $status) => $q->where('status', $status))
            ->when($request->sentiment, fn($q, $sentiment) => $q->where('sentiment', $sentiment))
            ->when($request->needs_reply, fn($q) => $q->pending())
            ->orderByDesc('priority_score')
            ->orderByDesc('platform_created_at')
            ->paginate($request->per_page ?? 20);

        return response()->json([
            'success' => true,
            'data' => $comments,
        ]);
    }

    /**
     * Fetch new comments from platform
     */
    public function fetch(Request $request, Post $post): JsonResponse
    {
        $this->authorize('update', $post);

        if (!$post->platform_post_id) {
            return response()->json([
                'success' => false,
                'error' => 'โพสต์นี้ยังไม่ได้เผยแพร่',
                'code' => 'POST_NOT_PUBLISHED',
            ], 400);
        }

        $result = $this->aiManager->dispatchTask([
            'type' => 'FetchComments',
            'platform' => $post->platform,
            'user_id' => Auth::id(),
            'brand_id' => $post->brand_id,
            'payload' => [
                'post_id' => $post->platform_post_id,
                'credentials' => $post->socialAccount->getCredentials(),
                'limit' => $request->limit ?? 50,
            ],
        ]);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'] ?? 'ไม่สามารถดึงคอมเมนต์ได้',
            ], 500);
        }

        // Store fetched comments
        $fetchedCount = 0;
        foreach ($result['data']['comments'] ?? [] as $commentData) {
            $comment = Comment::updateOrCreate(
                [
                    'post_id' => $post->id,
                    'platform_comment_id' => $commentData['id'],
                ],
                [
                    'platform' => $post->platform,
                    'author_id' => $commentData['author_id'] ?? null,
                    'author_name' => $commentData['author_name'] ?? 'Unknown',
                    'author_avatar' => $commentData['author_avatar'] ?? null,
                    'content' => $commentData['content'],
                    'sentiment' => $commentData['sentiment'] ?? 'neutral',
                    'sentiment_score' => $commentData['sentiment_score'] ?? 0,
                    'is_question' => $commentData['is_question'] ?? false,
                    'priority_score' => $commentData['priority_score'] ?? 50,
                    'platform_created_at' => $commentData['created_at'] ?? now(),
                ]
            );

            if ($comment->wasRecentlyCreated) {
                $fetchedCount++;
            }
        }

        $post->markCommentsChecked($fetchedCount, 0);

        return response()->json([
            'success' => true,
            'data' => [
                'fetched' => $fetchedCount,
                'total' => count($result['data']['comments'] ?? []),
                'next_page_token' => $result['data']['next_page_token'] ?? null,
            ],
            'message' => "ดึงคอมเมนต์ใหม่ {$fetchedCount} รายการ",
        ]);
    }

    /**
     * Reply to a comment
     */
    public function reply(Request $request, Comment $comment): JsonResponse
    {
        $post = $comment->post;
        $this->authorize('update', $post);

        $validated = $request->validate([
            'content' => 'required_without:auto_generate|string|max:2000',
            'auto_generate' => 'boolean',
            'tone_id' => 'nullable|exists:response_tones,id',
        ]);

        $replyContent = $validated['content'] ?? null;

        // Auto-generate reply using AI
        if ($request->auto_generate) {
            $tone = isset($validated['tone_id'])
                ? ResponseTone::find($validated['tone_id'])
                : ResponseTone::default()->first();

            $result = $this->aiManager->dispatchTask([
                'type' => 'ReplyToComment',
                'platform' => $post->platform,
                'user_id' => Auth::id(),
                'brand_id' => $post->brand_id,
                'payload' => [
                    'comment_id' => $comment->platform_comment_id,
                    'comment_content' => $comment->content,
                    'post_content' => $post->content_text,
                    'author_name' => $comment->author_name,
                    'tone_config' => $tone?->toAIConfig(),
                    'credentials' => $post->socialAccount->getCredentials(),
                    'brand_info' => [
                        'name' => $post->brand->name ?? '',
                        'tone' => $post->brand->default_tone ?? 'friendly',
                    ],
                ],
            ]);

            if (!$result['success']) {
                return response()->json([
                    'success' => false,
                    'error' => $result['error'] ?? 'ไม่สามารถสร้างคำตอบได้',
                ], 500);
            }

            $replyContent = $result['data']['reply_content'] ?? '';
        }

        if (empty($replyContent)) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่มีเนื้อหาสำหรับตอบกลับ',
            ], 400);
        }

        // Post the reply to platform
        $postResult = $this->aiManager->dispatchTask([
            'type' => 'ReplyToComment',
            'platform' => $post->platform,
            'user_id' => Auth::id(),
            'brand_id' => $post->brand_id,
            'payload' => [
                'comment_id' => $comment->platform_comment_id,
                'reply_content' => $replyContent,
                'credentials' => $post->socialAccount->getCredentials(),
            ],
        ]);

        if ($postResult['success']) {
            $comment->update([
                'status' => 'replied',
                'reply_content' => $replyContent,
                'replied_at' => now(),
                'reply_platform_id' => $postResult['data']['reply_id'] ?? null,
            ]);

            $post->markCommentsChecked(0, 1);
        }

        return response()->json([
            'success' => $postResult['success'],
            'data' => [
                'comment' => $comment->fresh(),
                'reply_content' => $replyContent,
            ],
            'message' => $postResult['success'] ? 'ตอบกลับคอมเมนต์แล้ว' : 'ไม่สามารถโพสต์คำตอบได้',
        ]);
    }

    /**
     * Auto-reply to pending comments
     */
    public function autoReply(Request $request, Post $post): JsonResponse
    {
        $this->authorize('update', $post);

        $validated = $request->validate([
            'tone_id' => 'nullable|exists:response_tones,id',
            'limit' => 'integer|min:1|max:50',
            'priority_threshold' => 'integer|min:0|max:100',
        ]);

        $tone = isset($validated['tone_id'])
            ? ResponseTone::find($validated['tone_id'])
            : ResponseTone::default()->first();

        $pendingComments = $post->comments()
            ->pending()
            ->where('priority_score', '>=', $validated['priority_threshold'] ?? 0)
            ->orderByDesc('priority_score')
            ->limit($validated['limit'] ?? 10)
            ->get();

        if ($pendingComments->isEmpty()) {
            return response()->json([
                'success' => true,
                'data' => ['replied' => 0],
                'message' => 'ไม่มีคอมเมนต์ที่รอตอบกลับ',
            ]);
        }

        $result = $this->aiManager->dispatchTask([
            'type' => 'AutoReplyComments',
            'platform' => $post->platform,
            'user_id' => Auth::id(),
            'brand_id' => $post->brand_id,
            'payload' => [
                'post_id' => $post->platform_post_id,
                'post_content' => $post->content_text,
                'comments' => $pendingComments->map(fn($c) => [
                    'id' => $c->platform_comment_id,
                    'content' => $c->content,
                    'author_name' => $c->author_name,
                    'sentiment' => $c->sentiment,
                    'is_question' => $c->is_question,
                ])->toArray(),
                'tone_config' => $tone?->toAIConfig(),
                'credentials' => $post->socialAccount->getCredentials(),
                'brand_info' => [
                    'name' => $post->brand->name ?? '',
                    'tone' => $post->brand->default_tone ?? 'friendly',
                ],
            ],
        ]);

        $repliedCount = 0;
        if ($result['success']) {
            foreach ($result['data']['replies'] ?? [] as $replyData) {
                $comment = $pendingComments->firstWhere('platform_comment_id', $replyData['comment_id']);
                if ($comment && $replyData['success']) {
                    $comment->update([
                        'status' => 'replied',
                        'reply_content' => $replyData['reply_content'],
                        'replied_at' => now(),
                        'reply_platform_id' => $replyData['reply_id'] ?? null,
                    ]);
                    $repliedCount++;
                }
            }

            $post->markCommentsChecked(0, $repliedCount);
        }

        return response()->json([
            'success' => true,
            'data' => [
                'replied' => $repliedCount,
                'total' => $pendingComments->count(),
            ],
            'message' => "ตอบกลับคอมเมนต์แล้ว {$repliedCount} รายการ",
        ]);
    }

    /**
     * Analyze comment sentiment
     */
    public function analyzeSentiment(Request $request, Comment $comment): JsonResponse
    {
        $post = $comment->post;
        $this->authorize('view', $post);

        $result = $this->aiManager->dispatchTask([
            'type' => 'AnalyzeCommentSentiment',
            'platform' => $post->platform,
            'user_id' => Auth::id(),
            'brand_id' => $post->brand_id,
            'payload' => [
                'content' => $comment->content,
                'author_name' => $comment->author_name,
                'context' => $post->content_text,
            ],
        ]);

        if ($result['success']) {
            $comment->update([
                'sentiment' => $result['data']['sentiment'] ?? 'neutral',
                'sentiment_score' => $result['data']['sentiment_score'] ?? 0,
                'is_question' => $result['data']['is_question'] ?? false,
                'priority_score' => $result['data']['priority_score'] ?? 50,
            ]);
        }

        return response()->json([
            'success' => $result['success'],
            'data' => $comment->fresh(),
        ]);
    }

    /**
     * Mark comment as handled (skip/ignore)
     */
    public function skip(Comment $comment): JsonResponse
    {
        $this->authorize('update', $comment->post);

        $comment->update([
            'status' => 'skipped',
        ]);

        return response()->json([
            'success' => true,
            'data' => $comment->fresh(),
            'message' => 'ข้ามคอมเมนต์นี้แล้ว',
        ]);
    }

    /**
     * Get comment statistics for a post
     */
    public function stats(Post $post): JsonResponse
    {
        $this->authorize('view', $post);

        $stats = [
            'total' => $post->comments()->count(),
            'pending' => $post->comments()->pending()->count(),
            'replied' => $post->comments()->replied()->count(),
            'skipped' => $post->comments()->where('status', 'skipped')->count(),
            'by_sentiment' => [
                'positive' => $post->comments()->positive()->count(),
                'neutral' => $post->comments()->neutral()->count(),
                'negative' => $post->comments()->negative()->count(),
            ],
            'questions' => $post->comments()->questions()->count(),
            'high_priority' => $post->comments()->highPriority()->count(),
            'last_checked' => $post->last_comment_check_at?->toIso8601String(),
        ];

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }
}
