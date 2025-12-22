<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Post;
use App\Models\TrendingKeyword;
use App\Services\AIManagerService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\DB;

class ViralAnalysisController extends Controller
{
    public function __construct(
        private AIManagerService $aiManager
    ) {}

    /**
     * Get viral dashboard overview
     */
    public function dashboard(Request $request): JsonResponse
    {
        $brandId = $request->brand_id;
        $period = $request->period ?? '7d';

        $startDate = match($period) {
            '24h' => now()->subDay(),
            '7d' => now()->subWeek(),
            '30d' => now()->subMonth(),
            '90d' => now()->subMonths(3),
            default => now()->subWeek(),
        };

        // Get viral posts
        $viralPosts = Post::query()
            ->when($brandId, fn($q) => $q->where('brand_id', $brandId))
            ->where('user_id', Auth::id())
            ->viral()
            ->where('published_at', '>=', $startDate)
            ->orderByDesc('viral_score')
            ->limit(10)
            ->get(['id', 'platform', 'content_text', 'viral_score', 'metrics', 'published_at']);

        // Get trending keywords
        $trendingKeywords = TrendingKeyword::query()
            ->when($brandId, fn($q) => $q->where('brand_id', $brandId))
            ->trending()
            ->orderByDesc('viral_score')
            ->limit(20)
            ->get(['keyword', 'viral_score', 'velocity', 'total_engagement', 'platforms']);

        // Get platform performance
        $platformStats = Post::query()
            ->when($brandId, fn($q) => $q->where('brand_id', $brandId))
            ->where('user_id', Auth::id())
            ->where('published_at', '>=', $startDate)
            ->published()
            ->select('platform')
            ->selectRaw('COUNT(*) as total_posts')
            ->selectRaw('AVG(viral_score) as avg_viral_score')
            ->selectRaw('SUM(CASE WHEN is_viral = 1 THEN 1 ELSE 0 END) as viral_count')
            ->groupBy('platform')
            ->get();

        // Get optimal posting times
        $optimalTimes = $this->calculateOptimalPostingTimes($brandId, $startDate);

        return response()->json([
            'success' => true,
            'data' => [
                'viral_posts' => $viralPosts,
                'trending_keywords' => $trendingKeywords,
                'platform_stats' => $platformStats,
                'optimal_times' => $optimalTimes,
                'period' => $period,
            ],
        ]);
    }

    /**
     * Analyze viral potential of content before posting
     */
    public function analyzeContent(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'content' => 'required|string|max:5000',
            'platform' => 'required|string',
            'brand_id' => 'nullable|exists:brands,id',
            'hashtags' => 'nullable|array',
            'media_type' => 'nullable|string',
        ]);

        $result = $this->aiManager->dispatchTask([
            'type' => 'AnalyzeViralPotential',
            'platform' => $validated['platform'],
            'user_id' => Auth::id(),
            'brand_id' => $validated['brand_id'] ?? null,
            'payload' => [
                'content' => $validated['content'],
                'hashtags' => $validated['hashtags'] ?? [],
                'media_type' => $validated['media_type'] ?? 'text',
            ],
        ]);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'] ?? 'ไม่สามารถวิเคราะห์ได้',
            ], 500);
        }

        return response()->json([
            'success' => true,
            'data' => [
                'viral_score' => $result['data']['viral_score'] ?? 0,
                'factors' => $result['data']['factors'] ?? [],
                'suggestions' => $result['data']['suggestions'] ?? [],
                'optimal_time' => $result['data']['optimal_time'] ?? null,
                'predicted_engagement' => $result['data']['predicted_engagement'] ?? null,
            ],
        ]);
    }

    /**
     * Get trending keywords
     */
    public function trendingKeywords(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'platform' => 'nullable|string',
            'category' => 'nullable|string',
            'limit' => 'integer|min:1|max:100',
        ]);

        $keywords = TrendingKeyword::query()
            ->when($validated['platform'] ?? null, function ($q, $platform) {
                $q->whereJsonContains('platforms', $platform);
            })
            ->when($validated['category'] ?? null, fn($q, $cat) => $q->where('category', $cat))
            ->trending()
            ->orderByDesc('viral_score')
            ->limit($validated['limit'] ?? 50)
            ->get();

        return response()->json([
            'success' => true,
            'data' => $keywords,
        ]);
    }

    /**
     * Track a new keyword
     */
    public function trackKeyword(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'keyword' => 'required|string|max:100',
            'brand_id' => 'nullable|exists:brands,id',
            'platforms' => 'nullable|array',
            'category' => 'nullable|string|max:50',
        ]);

        $keyword = TrendingKeyword::updateOrCreate(
            [
                'keyword' => strtolower(trim($validated['keyword'])),
                'brand_id' => $validated['brand_id'] ?? null,
            ],
            [
                'platforms' => $validated['platforms'] ?? ['facebook', 'instagram', 'twitter'],
                'category' => $validated['category'] ?? null,
                'is_tracking' => true,
            ]
        );

        return response()->json([
            'success' => true,
            'data' => $keyword,
            'message' => 'เริ่มติดตามคีย์เวิร์ดแล้ว',
        ]);
    }

    /**
     * Update keyword metrics from AI analysis
     */
    public function updateKeywordMetrics(Request $request): JsonResponse
    {
        $result = $this->aiManager->dispatchTask([
            'type' => 'TrackTrendingKeywords',
            'user_id' => Auth::id(),
            'payload' => [
                'keywords' => TrendingKeyword::where('is_tracking', true)
                    ->pluck('keyword')
                    ->toArray(),
            ],
        ]);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'] ?? 'ไม่สามารถอัปเดตได้',
            ], 500);
        }

        $updatedCount = 0;
        foreach ($result['data']['keywords'] ?? [] as $kwData) {
            $updated = TrendingKeyword::where('keyword', $kwData['keyword'])
                ->update([
                    'viral_score' => $kwData['viral_score'] ?? 0,
                    'velocity' => $kwData['velocity'] ?? 0,
                    'total_mentions' => DB::raw('total_mentions + ' . ($kwData['mentions'] ?? 0)),
                    'total_engagement' => DB::raw('total_engagement + ' . ($kwData['engagement'] ?? 0)),
                    'hourly_data' => $this->updateHourlyData($kwData),
                    'last_analyzed_at' => now(),
                ]);
            if ($updated) $updatedCount++;
        }

        return response()->json([
            'success' => true,
            'data' => ['updated' => $updatedCount],
            'message' => "อัปเดต {$updatedCount} คีย์เวิร์ดแล้ว",
        ]);
    }

    /**
     * Analyze a post's viral performance
     */
    public function analyzePost(Post $post): JsonResponse
    {
        $this->authorize('view', $post);

        // Recalculate viral score
        $post->updateViralAnalysis();

        // Get AI insights
        $result = $this->aiManager->dispatchTask([
            'type' => 'AnalyzeViralPotential',
            'platform' => $post->platform,
            'user_id' => Auth::id(),
            'brand_id' => $post->brand_id,
            'payload' => [
                'content' => $post->content_text,
                'hashtags' => $post->hashtags ?? [],
                'metrics' => $post->metrics ?? [],
                'published_at' => $post->published_at?->toIso8601String(),
            ],
        ]);

        return response()->json([
            'success' => true,
            'data' => [
                'post' => $post->fresh(),
                'insights' => $result['data'] ?? null,
            ],
        ]);
    }

    /**
     * Get viral content suggestions
     */
    public function suggestions(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'platform' => 'required|string',
            'brand_id' => 'nullable|exists:brands,id',
            'topic' => 'nullable|string|max:200',
            'count' => 'integer|min:1|max:10',
        ]);

        // Get trending keywords for context
        $trendingKeywords = TrendingKeyword::trending()
            ->orderByDesc('viral_score')
            ->limit(10)
            ->pluck('keyword')
            ->toArray();

        // Get recent viral posts for inspiration
        $viralPosts = Post::query()
            ->when($validated['brand_id'] ?? null, fn($q, $brandId) => $q->where('brand_id', $brandId))
            ->where('platform', $validated['platform'])
            ->viral()
            ->orderByDesc('viral_score')
            ->limit(5)
            ->pluck('content_text')
            ->toArray();

        $result = $this->aiManager->dispatchTask([
            'type' => 'GenerateContent',
            'platform' => $validated['platform'],
            'user_id' => Auth::id(),
            'brand_id' => $validated['brand_id'] ?? null,
            'payload' => [
                'prompt' => $validated['topic'] ?? 'สร้างเนื้อหาที่มีโอกาสไวรัลสูง',
                'trending_keywords' => $trendingKeywords,
                'viral_examples' => $viralPosts,
                'count' => $validated['count'] ?? 3,
                'optimize_for_viral' => true,
            ],
        ]);

        return response()->json([
            'success' => $result['success'],
            'data' => [
                'suggestions' => $result['data']['content'] ?? [],
                'trending_keywords' => $trendingKeywords,
            ],
        ]);
    }

    /**
     * Get engagement velocity for a post
     */
    public function velocity(Post $post): JsonResponse
    {
        $this->authorize('view', $post);

        if (!$post->published_at) {
            return response()->json([
                'success' => false,
                'error' => 'โพสต์ยังไม่ได้เผยแพร่',
            ], 400);
        }

        $hoursSincePublish = $post->published_at->diffInHours(now());
        $metrics = $post->metrics ?? [];

        $velocity = 0;
        if ($hoursSincePublish > 0) {
            $totalEngagement = ($metrics['likes'] ?? 0) +
                               ($metrics['comments'] ?? 0) * 2 +
                               ($metrics['shares'] ?? 0) * 3;
            $velocity = $totalEngagement / $hoursSincePublish;
        }

        // Update velocity
        $post->update(['engagement_velocity' => $velocity]);

        return response()->json([
            'success' => true,
            'data' => [
                'velocity' => round($velocity, 2),
                'hours_since_publish' => $hoursSincePublish,
                'metrics' => $metrics,
                'viral_score' => $post->viral_score,
                'is_viral' => $post->is_viral,
            ],
        ]);
    }

    /**
     * Compare posts performance
     */
    public function compare(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'post_ids' => 'required|array|min:2|max:10',
            'post_ids.*' => 'exists:posts,id',
        ]);

        $posts = Post::whereIn('id', $validated['post_ids'])
            ->where('user_id', Auth::id())
            ->get(['id', 'platform', 'content_text', 'viral_score', 'metrics', 'published_at', 'hashtags']);

        if ($posts->count() < 2) {
            return response()->json([
                'success' => false,
                'error' => 'ต้องมีโพสต์อย่างน้อย 2 โพสต์',
            ], 400);
        }

        $comparison = $posts->map(function ($post) {
            $metrics = $post->metrics ?? [];
            return [
                'id' => $post->id,
                'platform' => $post->platform,
                'content_preview' => mb_substr($post->content_text, 0, 100) . '...',
                'viral_score' => $post->viral_score,
                'likes' => $metrics['likes'] ?? 0,
                'comments' => $metrics['comments'] ?? 0,
                'shares' => $metrics['shares'] ?? 0,
                'views' => $metrics['views'] ?? 0,
                'engagement_rate' => $metrics['engagement_rate'] ?? 0,
                'hashtag_count' => count($post->hashtags ?? []),
                'published_at' => $post->published_at?->toIso8601String(),
            ];
        });

        // Find best performer
        $bestPost = $comparison->sortByDesc('viral_score')->first();

        return response()->json([
            'success' => true,
            'data' => [
                'posts' => $comparison->values(),
                'best_performer' => $bestPost,
                'insights' => $this->generateComparisonInsights($comparison),
            ],
        ]);
    }

    /**
     * Calculate optimal posting times based on historical data
     */
    private function calculateOptimalPostingTimes(?int $brandId, $startDate): array
    {
        $postsByHour = Post::query()
            ->when($brandId, fn($q) => $q->where('brand_id', $brandId))
            ->where('user_id', Auth::id())
            ->where('published_at', '>=', $startDate)
            ->published()
            ->selectRaw('HOUR(published_at) as hour')
            ->selectRaw('AVG(viral_score) as avg_score')
            ->selectRaw('COUNT(*) as post_count')
            ->groupBy('hour')
            ->orderByDesc('avg_score')
            ->limit(5)
            ->get();

        return $postsByHour->map(function ($row) {
            return [
                'hour' => (int) $row->hour,
                'time_range' => sprintf('%02d:00 - %02d:59', $row->hour, $row->hour),
                'avg_viral_score' => round($row->avg_score, 2),
                'sample_size' => $row->post_count,
            ];
        })->toArray();
    }

    /**
     * Update hourly tracking data
     */
    private function updateHourlyData(array $kwData): array
    {
        $hour = now()->format('Y-m-d H:00');
        return [
            $hour => [
                'mentions' => $kwData['mentions'] ?? 0,
                'engagement' => $kwData['engagement'] ?? 0,
                'velocity' => $kwData['velocity'] ?? 0,
            ],
        ];
    }

    /**
     * Generate insights from post comparison
     */
    private function generateComparisonInsights($posts): array
    {
        $insights = [];

        $avgScore = $posts->avg('viral_score');
        $avgLikes = $posts->avg('likes');
        $avgHashtags = $posts->avg('hashtag_count');

        if ($avgScore > 60) {
            $insights[] = 'เนื้อหาโดยรวมมีประสิทธิภาพดี';
        }

        $bestPost = $posts->sortByDesc('viral_score')->first();
        if ($bestPost['hashtag_count'] > $avgHashtags) {
            $insights[] = 'โพสต์ที่ดีที่สุดใช้แฮชแท็กมากกว่าค่าเฉลี่ย';
        }

        return $insights;
    }
}
