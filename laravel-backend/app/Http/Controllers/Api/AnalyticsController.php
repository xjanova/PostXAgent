<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Post;
use App\Models\Brand;
use App\Models\Campaign;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Carbon\Carbon;

class AnalyticsController extends Controller
{
    /**
     * Get analytics overview
     */
    public function overview(Request $request): JsonResponse
    {
        $userId = $request->user()->id;
        $days = $request->days ?? 30;
        $startDate = Carbon::now()->subDays($days);

        // Total stats
        $totalPosts = Post::where('user_id', $userId)->count();
        $publishedPosts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->count();

        // Posts in period
        $postsInPeriod = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->where('published_at', '>=', $startDate)
            ->get();

        // Engagement metrics
        $totalLikes = $postsInPeriod->sum(fn($p) => $p->metrics['likes'] ?? 0);
        $totalComments = $postsInPeriod->sum(fn($p) => $p->metrics['comments'] ?? 0);
        $totalShares = $postsInPeriod->sum(fn($p) => $p->metrics['shares'] ?? 0);
        $totalViews = $postsInPeriod->sum(fn($p) => $p->metrics['views'] ?? 0);

        // Average engagement rate
        $avgEngagement = $postsInPeriod->avg(fn($p) => $p->metrics['engagement_rate'] ?? 0);

        // Posts by status
        $postsByStatus = Post::where('user_id', $userId)
            ->selectRaw('status, count(*) as count')
            ->groupBy('status')
            ->pluck('count', 'status');

        // Growth (compare with previous period)
        $previousPeriodPosts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->whereBetween('published_at', [$startDate->copy()->subDays($days), $startDate])
            ->count();

        $growth = $previousPeriodPosts > 0
            ? round((($postsInPeriod->count() - $previousPeriodPosts) / $previousPeriodPosts) * 100, 1)
            : 0;

        return response()->json([
            'success' => true,
            'data' => [
                'period' => [
                    'days' => $days,
                    'start_date' => $startDate->toDateString(),
                    'end_date' => Carbon::now()->toDateString(),
                ],
                'summary' => [
                    'total_posts' => $totalPosts,
                    'published_posts' => $publishedPosts,
                    'posts_in_period' => $postsInPeriod->count(),
                    'growth_percentage' => $growth,
                ],
                'engagement' => [
                    'total_likes' => $totalLikes,
                    'total_comments' => $totalComments,
                    'total_shares' => $totalShares,
                    'total_views' => $totalViews,
                    'total_engagement' => $totalLikes + $totalComments + $totalShares,
                    'avg_engagement_rate' => round($avgEngagement, 2),
                ],
                'posts_by_status' => $postsByStatus,
            ],
        ]);
    }

    /**
     * Get posts analytics
     */
    public function posts(Request $request): JsonResponse
    {
        $userId = $request->user()->id;
        $days = $request->days ?? 30;
        $startDate = Carbon::now()->subDays($days);

        // Daily posts count
        $dailyPosts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->where('published_at', '>=', $startDate)
            ->selectRaw('DATE(published_at) as date, count(*) as count')
            ->groupBy('date')
            ->orderBy('date')
            ->pluck('count', 'date');

        // Fill in missing dates
        $dateRange = [];
        for ($i = $days; $i >= 0; $i--) {
            $date = Carbon::now()->subDays($i)->toDateString();
            $dateRange[$date] = $dailyPosts[$date] ?? 0;
        }

        // Top performing posts
        $topPosts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->where('published_at', '>=', $startDate)
            ->with(['brand:id,name', 'socialAccount:id,platform,display_name'])
            ->get()
            ->sortByDesc(fn($p) => ($p->metrics['likes'] ?? 0) + ($p->metrics['comments'] ?? 0))
            ->take(10)
            ->values();

        return response()->json([
            'success' => true,
            'data' => [
                'daily_posts' => $dateRange,
                'top_posts' => $topPosts,
            ],
        ]);
    }

    /**
     * Get engagement analytics
     */
    public function engagement(Request $request): JsonResponse
    {
        $userId = $request->user()->id;
        $days = $request->days ?? 30;
        $startDate = Carbon::now()->subDays($days);

        $posts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->where('published_at', '>=', $startDate)
            ->get();

        // Daily engagement
        $dailyEngagement = $posts->groupBy(fn($p) => $p->published_at->toDateString())
            ->map(fn($dayPosts) => [
                'likes' => $dayPosts->sum(fn($p) => $p->metrics['likes'] ?? 0),
                'comments' => $dayPosts->sum(fn($p) => $p->metrics['comments'] ?? 0),
                'shares' => $dayPosts->sum(fn($p) => $p->metrics['shares'] ?? 0),
                'views' => $dayPosts->sum(fn($p) => $p->metrics['views'] ?? 0),
            ]);

        // Engagement by content type
        $byContentType = $posts->groupBy('content_type')
            ->map(fn($typePosts) => [
                'count' => $typePosts->count(),
                'avg_engagement' => round($typePosts->avg(fn($p) =>
                    ($p->metrics['likes'] ?? 0) + ($p->metrics['comments'] ?? 0) + ($p->metrics['shares'] ?? 0)
                ), 1),
            ]);

        // Best time to post (hour analysis)
        $byHour = $posts->groupBy(fn($p) => $p->published_at->format('H'))
            ->map(fn($hourPosts) => round($hourPosts->avg(fn($p) =>
                $p->metrics['engagement_rate'] ?? 0
            ), 2))
            ->sortDesc();

        return response()->json([
            'success' => true,
            'data' => [
                'daily_engagement' => $dailyEngagement,
                'by_content_type' => $byContentType,
                'best_hours' => $byHour->take(5),
            ],
        ]);
    }

    /**
     * Get platform analytics
     */
    public function platforms(Request $request): JsonResponse
    {
        $userId = $request->user()->id;
        $days = $request->days ?? 30;
        $startDate = Carbon::now()->subDays($days);

        $posts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->where('published_at', '>=', $startDate)
            ->get();

        $platformStats = $posts->groupBy('platform')->map(function ($platformPosts, $platform) {
            $totalLikes = $platformPosts->sum(fn($p) => $p->metrics['likes'] ?? 0);
            $totalComments = $platformPosts->sum(fn($p) => $p->metrics['comments'] ?? 0);
            $totalShares = $platformPosts->sum(fn($p) => $p->metrics['shares'] ?? 0);
            $totalViews = $platformPosts->sum(fn($p) => $p->metrics['views'] ?? 0);

            return [
                'platform' => $platform,
                'posts_count' => $platformPosts->count(),
                'total_likes' => $totalLikes,
                'total_comments' => $totalComments,
                'total_shares' => $totalShares,
                'total_views' => $totalViews,
                'total_engagement' => $totalLikes + $totalComments + $totalShares,
                'avg_engagement_rate' => round($platformPosts->avg(fn($p) => $p->metrics['engagement_rate'] ?? 0), 2),
            ];
        })->sortByDesc('total_engagement')->values();

        return response()->json([
            'success' => true,
            'data' => $platformStats,
        ]);
    }

    /**
     * Get brand-specific analytics
     */
    public function brand(Request $request, Brand $brand): JsonResponse
    {
        if ($brand->user_id !== $request->user()->id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีสิทธิ์เข้าถึงข้อมูลนี้',
            ], 403);
        }

        $days = $request->days ?? 30;
        $startDate = Carbon::now()->subDays($days);

        $posts = Post::where('brand_id', $brand->id)
            ->where('status', Post::STATUS_PUBLISHED)
            ->where('published_at', '>=', $startDate)
            ->get();

        // Brand overview
        $totalPosts = $posts->count();
        $totalLikes = $posts->sum(fn($p) => $p->metrics['likes'] ?? 0);
        $totalComments = $posts->sum(fn($p) => $p->metrics['comments'] ?? 0);
        $totalShares = $posts->sum(fn($p) => $p->metrics['shares'] ?? 0);
        $totalViews = $posts->sum(fn($p) => $p->metrics['views'] ?? 0);

        // Campaign performance
        $campaignStats = Campaign::where('brand_id', $brand->id)
            ->withCount(['posts as total_posts', 'posts as published_posts' => fn($q) => $q->where('status', 'published')])
            ->get()
            ->map(fn($c) => [
                'id' => $c->id,
                'name' => $c->name,
                'status' => $c->status,
                'total_posts' => $c->total_posts,
                'published_posts' => $c->published_posts,
            ]);

        // Platform breakdown for brand
        $platformBreakdown = $posts->groupBy('platform')->map(fn($p) => [
            'count' => $p->count(),
            'engagement' => $p->sum(fn($post) =>
                ($post->metrics['likes'] ?? 0) + ($post->metrics['comments'] ?? 0) + ($post->metrics['shares'] ?? 0)
            ),
        ]);

        return response()->json([
            'success' => true,
            'data' => [
                'brand' => $brand->only(['id', 'name', 'industry']),
                'period_days' => $days,
                'overview' => [
                    'total_posts' => $totalPosts,
                    'total_likes' => $totalLikes,
                    'total_comments' => $totalComments,
                    'total_shares' => $totalShares,
                    'total_views' => $totalViews,
                    'avg_engagement_rate' => round($posts->avg(fn($p) => $p->metrics['engagement_rate'] ?? 0), 2),
                ],
                'campaigns' => $campaignStats,
                'platforms' => $platformBreakdown,
            ],
        ]);
    }
}
