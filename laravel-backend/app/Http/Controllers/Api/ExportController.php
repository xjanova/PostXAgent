<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Post;
use App\Models\Campaign;
use App\Models\Brand;
use Illuminate\Http\Request;
use Illuminate\Http\Response;
use Illuminate\Support\Facades\Storage;
use Illuminate\Support\Str;

class ExportController extends Controller
{
    /**
     * Export posts to CSV
     */
    public function exportPosts(Request $request): Response
    {
        $request->validate([
            'format' => 'required|in:csv,json',
            'status' => 'nullable|string',
            'platform' => 'nullable|string',
            'brand_id' => 'nullable|integer|exists:brands,id',
            'campaign_id' => 'nullable|integer|exists:campaigns,id',
            'date_from' => 'nullable|date',
            'date_to' => 'nullable|date',
        ]);

        $userId = $request->user()->id;

        $posts = Post::where('user_id', $userId)
            ->with(['brand:id,name', 'socialAccount:id,platform,display_name', 'campaign:id,name'])
            ->when($request->status, fn($q, $s) => $q->where('status', $s))
            ->when($request->platform, fn($q, $p) => $q->where('platform', $p))
            ->when($request->brand_id, fn($q, $b) => $q->where('brand_id', $b))
            ->when($request->campaign_id, fn($q, $c) => $q->where('campaign_id', $c))
            ->when($request->date_from, fn($q, $d) => $q->whereDate('created_at', '>=', $d))
            ->when($request->date_to, fn($q, $d) => $q->whereDate('created_at', '<=', $d))
            ->orderBy('created_at', 'desc')
            ->get();

        if ($request->format === 'json') {
            return $this->exportJson($posts->toArray(), 'posts');
        }

        return $this->exportCsv($posts, 'posts', [
            'ID',
            'แบรนด์',
            'แคมเปญ',
            'แพลตฟอร์ม',
            'บัญชี',
            'เนื้อหา',
            'ประเภท',
            'สถานะ',
            'กำหนดโพสต์',
            'โพสต์แล้ว',
            'ไลค์',
            'คอมเมนต์',
            'แชร์',
            'วิว',
            'Engagement Rate',
            'สร้างเมื่อ',
        ], function ($post) {
            return [
                $post->id,
                $post->brand?->name ?? '-',
                $post->campaign?->name ?? '-',
                $post->platform,
                $post->socialAccount?->display_name ?? '-',
                Str::limit($post->content_text, 100),
                $post->content_type,
                $this->translateStatus($post->status),
                $post->scheduled_at?->format('Y-m-d H:i') ?? '-',
                $post->published_at?->format('Y-m-d H:i') ?? '-',
                $post->metrics['likes'] ?? 0,
                $post->metrics['comments'] ?? 0,
                $post->metrics['shares'] ?? 0,
                $post->metrics['views'] ?? 0,
                ($post->metrics['engagement_rate'] ?? 0) . '%',
                $post->created_at->format('Y-m-d H:i'),
            ];
        });
    }

    /**
     * Export campaigns to CSV
     */
    public function exportCampaigns(Request $request): Response
    {
        $request->validate([
            'format' => 'required|in:csv,json',
            'status' => 'nullable|string',
            'brand_id' => 'nullable|integer|exists:brands,id',
        ]);

        $userId = $request->user()->id;

        $campaigns = Campaign::where('user_id', $userId)
            ->with(['brand:id,name'])
            ->withCount('posts')
            ->when($request->status, fn($q, $s) => $q->where('status', $s))
            ->when($request->brand_id, fn($q, $b) => $q->where('brand_id', $b))
            ->orderBy('created_at', 'desc')
            ->get();

        if ($request->format === 'json') {
            return $this->exportJson($campaigns->toArray(), 'campaigns');
        }

        return $this->exportCsv($campaigns, 'campaigns', [
            'ID',
            'ชื่อแคมเปญ',
            'แบรนด์',
            'ประเภท',
            'เป้าหมาย',
            'งบประมาณ',
            'จำนวนโพสต์',
            'สถานะ',
            'วันเริ่ม',
            'วันสิ้นสุด',
            'สร้างเมื่อ',
        ], function ($campaign) {
            return [
                $campaign->id,
                $campaign->name,
                $campaign->brand?->name ?? '-',
                $campaign->type,
                $campaign->goal,
                number_format($campaign->budget ?? 0) . ' บาท',
                $campaign->posts_count,
                $this->translateCampaignStatus($campaign->status),
                $campaign->start_date?->format('Y-m-d') ?? '-',
                $campaign->end_date?->format('Y-m-d') ?? '-',
                $campaign->created_at->format('Y-m-d H:i'),
            ];
        });
    }

    /**
     * Export analytics summary
     */
    public function exportAnalytics(Request $request): Response
    {
        $request->validate([
            'format' => 'required|in:csv,json',
            'date_from' => 'required|date',
            'date_to' => 'required|date|after_or_equal:date_from',
            'brand_id' => 'nullable|integer|exists:brands,id',
        ]);

        $userId = $request->user()->id;

        // Get posts with metrics in date range
        $posts = Post::where('user_id', $userId)
            ->where('status', Post::STATUS_PUBLISHED)
            ->whereDate('published_at', '>=', $request->date_from)
            ->whereDate('published_at', '<=', $request->date_to)
            ->when($request->brand_id, fn($q, $b) => $q->where('brand_id', $b))
            ->get();

        // Calculate summary by platform
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
                'avg_engagement_rate' => $platformPosts->avg(fn($p) => $p->metrics['engagement_rate'] ?? 0),
            ];
        })->values();

        if ($request->format === 'json') {
            return $this->exportJson([
                'period' => [
                    'from' => $request->date_from,
                    'to' => $request->date_to,
                ],
                'summary' => [
                    'total_posts' => $posts->count(),
                    'total_likes' => $posts->sum(fn($p) => $p->metrics['likes'] ?? 0),
                    'total_comments' => $posts->sum(fn($p) => $p->metrics['comments'] ?? 0),
                    'total_shares' => $posts->sum(fn($p) => $p->metrics['shares'] ?? 0),
                    'total_views' => $posts->sum(fn($p) => $p->metrics['views'] ?? 0),
                ],
                'by_platform' => $platformStats,
            ], 'analytics');
        }

        return $this->exportCsv($platformStats, 'analytics', [
            'แพลตฟอร์ม',
            'จำนวนโพสต์',
            'รวมไลค์',
            'รวมคอมเมนต์',
            'รวมแชร์',
            'รวมวิว',
            'Engagement Rate เฉลี่ย',
        ], function ($stat) {
            return [
                $stat['platform'],
                $stat['posts_count'],
                number_format($stat['total_likes']),
                number_format($stat['total_comments']),
                number_format($stat['total_shares']),
                number_format($stat['total_views']),
                round($stat['avg_engagement_rate'], 2) . '%',
            ];
        });
    }

    /**
     * Export to CSV format
     */
    private function exportCsv($data, string $filename, array $headers, callable $rowMapper): Response
    {
        $csvContent = implode(',', array_map(fn($h) => '"' . $h . '"', $headers)) . "\n";

        foreach ($data as $item) {
            $row = $rowMapper($item);
            $csvContent .= implode(',', array_map(function ($cell) {
                $cell = str_replace('"', '""', (string) $cell);
                $cell = str_replace(["\r\n", "\r", "\n"], ' ', $cell);
                return '"' . $cell . '"';
            }, $row)) . "\n";
        }

        // Add BOM for UTF-8 Excel compatibility
        $csvContent = "\xEF\xBB\xBF" . $csvContent;

        $exportFilename = $filename . '_' . now()->format('Y-m-d_His') . '.csv';

        return response($csvContent)
            ->header('Content-Type', 'text/csv; charset=UTF-8')
            ->header('Content-Disposition', 'attachment; filename="' . $exportFilename . '"');
    }

    /**
     * Export to JSON format
     */
    private function exportJson(array $data, string $filename): Response
    {
        $jsonContent = json_encode([
            'exported_at' => now()->toIso8601String(),
            'data' => $data,
        ], JSON_UNESCAPED_UNICODE | JSON_PRETTY_PRINT);

        $exportFilename = $filename . '_' . now()->format('Y-m-d_His') . '.json';

        return response($jsonContent)
            ->header('Content-Type', 'application/json; charset=UTF-8')
            ->header('Content-Disposition', 'attachment; filename="' . $exportFilename . '"');
    }

    /**
     * Translate post status to Thai
     */
    private function translateStatus(string $status): string
    {
        return match ($status) {
            'draft' => 'แบบร่าง',
            'pending' => 'รอดำเนินการ',
            'scheduled' => 'กำหนดเวลา',
            'publishing' => 'กำลังโพสต์',
            'published' => 'โพสต์แล้ว',
            'failed' => 'ล้มเหลว',
            'deleted' => 'ลบแล้ว',
            default => $status,
        };
    }

    /**
     * Translate campaign status to Thai
     */
    private function translateCampaignStatus(string $status): string
    {
        return match ($status) {
            'draft' => 'แบบร่าง',
            'scheduled' => 'กำหนดเวลา',
            'active' => 'กำลังดำเนินการ',
            'paused' => 'หยุดชั่วคราว',
            'completed' => 'เสร็จสิ้น',
            'cancelled' => 'ยกเลิก',
            default => $status,
        };
    }
}
