<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use App\Models\Campaign;
use App\Models\Brand;
use App\Models\User;
use App\Models\SocialAccount;

class CampaignSeeder extends Seeder
{
    /**
     * Run the database seeds.
     */
    public function run(): void
    {
        $demoUser = User::where('email', 'demo@postxagent.com')->first();
        if (!$demoUser) {
            return;
        }

        $brands = Brand::where('user_id', $demoUser->id)->get();
        if ($brands->isEmpty()) {
            return;
        }

        $campaignTemplates = [
            [
                'name' => 'แคมเปญเปิดตัวสินค้าใหม่',
                'description' => 'แคมเปญโปรโมทสินค้าใหม่ประจำเดือน',
                'type' => 'product_launch',
                'goal' => 'เพิ่มการรับรู้แบรนด์และยอดขาย',
                'content_themes' => ['product_showcase', 'unboxing', 'reviews', 'promotion'],
                'posting_schedule' => [
                    'frequency' => 'daily',
                    'times' => ['09:00', '12:00', '18:00'],
                    'days' => ['monday', 'tuesday', 'wednesday', 'thursday', 'friday'],
                ],
                'ai_settings' => [
                    'tone' => 'professional',
                    'language' => 'th',
                    'include_emoji' => true,
                    'hashtag_count' => 5,
                ],
                'budget' => 15000,
                'status' => Campaign::STATUS_ACTIVE,
            ],
            [
                'name' => 'แคมเปญสร้าง Engagement',
                'description' => 'กิจกรรมและคอนเทนต์สร้างการมีส่วนร่วม',
                'type' => 'engagement',
                'goal' => 'เพิ่ม engagement rate 50%',
                'content_themes' => ['questions', 'polls', 'behind_scenes', 'tips', 'memes'],
                'posting_schedule' => [
                    'frequency' => 'twice_daily',
                    'times' => ['10:00', '19:00'],
                    'days' => ['monday', 'wednesday', 'friday', 'sunday'],
                ],
                'ai_settings' => [
                    'tone' => 'friendly',
                    'language' => 'th',
                    'include_emoji' => true,
                    'hashtag_count' => 3,
                ],
                'budget' => 5000,
                'status' => Campaign::STATUS_ACTIVE,
            ],
            [
                'name' => 'แคมเปญโปรโมชั่นพิเศษ',
                'description' => 'โปรโมชั่นส่งท้ายปี ลดราคาพิเศษ',
                'type' => 'promotion',
                'goal' => 'เพิ่มยอดขาย 100%',
                'content_themes' => ['discount', 'flash_sale', 'limited_time', 'countdown'],
                'posting_schedule' => [
                    'frequency' => 'multiple',
                    'times' => ['08:00', '12:00', '16:00', '20:00'],
                    'days' => ['saturday', 'sunday'],
                ],
                'ai_settings' => [
                    'tone' => 'urgent',
                    'language' => 'th',
                    'include_emoji' => true,
                    'hashtag_count' => 5,
                    'include_cta' => true,
                ],
                'budget' => 20000,
                'status' => Campaign::STATUS_SCHEDULED,
            ],
        ];

        foreach ($brands as $index => $brand) {
            // Each brand gets 1-2 campaigns
            $numCampaigns = min($index + 1, count($campaignTemplates));

            for ($i = 0; $i < $numCampaigns; $i++) {
                $template = $campaignTemplates[$i];

                // Get available platforms from social accounts
                $platforms = SocialAccount::where('brand_id', $brand->id)
                    ->pluck('platform')
                    ->toArray();

                if (empty($platforms)) {
                    $platforms = ['facebook', 'instagram'];
                }

                Campaign::firstOrCreate(
                    [
                        'user_id' => $demoUser->id,
                        'brand_id' => $brand->id,
                        'name' => $template['name'],
                    ],
                    [
                        ...$template,
                        'target_platforms' => $platforms,
                        'start_date' => now()->subDays(rand(1, 7)),
                        'end_date' => now()->addDays(rand(14, 30)),
                    ]
                );
            }
        }
    }
}
