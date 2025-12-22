<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use App\Models\Post;
use App\Models\Campaign;
use App\Models\SocialAccount;
use App\Models\User;
use Illuminate\Support\Str;

class PostSeeder extends Seeder
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

        $campaigns = Campaign::where('user_id', $demoUser->id)->get();
        if ($campaigns->isEmpty()) {
            return;
        }

        // Thai content templates
        $contentTemplates = [
            'product' => [
                '🎉 สินค้ามาใหม่! {product} คุณภาพเยี่ยม ราคาคุ้ม สั่งเลยวันนี้ ✨',
                '✨ แนะนำสินค้าขายดี! {product} ลูกค้าชอบมากเพราะคุณภาพดี ราคาเบาๆ 💯',
                '🔥 {product} เหลือสต็อกสุดท้ายแล้ว! อย่าพลาดนะคะ 🛍️',
                '💝 สินค้ายอดนิยมประจำสัปดาห์ {product} ลดราคาพิเศษถึงสิ้นเดือนนี้เท่านั้น!',
            ],
            'engagement' => [
                '🤔 ถามจริงๆ นะ วันนี้รู้สึกยังไงกันบ้าง? มาแชร์กันค่ะ 💬',
                '📊 Poll มาแล้วจ้า! คุณชอบแบบไหนมากกว่ากัน? A หรือ B? คอมเมนต์มาเลย!',
                '💡 เคล็ดลับดีๆ มาฝากค่ะ! วิธีใช้ {product} ให้ได้ผลดีที่สุด 👇',
                '🎯 วันนี้เราอยากรู้ว่าทุกคนทำอะไรอยู่บ้าง? Tag เพื่อนมาตอบด้วยนะ 😊',
            ],
            'promotion' => [
                '🔥 SALE! ลดแรง ลดจริง {discount}% เฉพาะวันนี้เท่านั้น! รีบเลยค่า ⏰',
                '💥 Flash Sale! {product} ลดเหลือ {price} บาท! จำนวนจำกัด 🏃‍♀️',
                '🎁 โปรโมชั่นพิเศษ! ซื้อ 1 แถม 1 ถึง {date} นี้เท่านั้น!',
                '✨ ส่งท้ายเดือน! ลดทั้งร้าน {discount}% ใส่โค้ด: {code} 🛒',
            ],
            'cafe' => [
                '☕ เช้านี้ดื่มกาแฟกันยัง? แวะมาจิบที่ร้านเรานะคะ รับรองถูกใจ ❤️',
                '🍰 เมนูใหม่มาแล้วจ้า! {menu} หวานละมุน นุ่มลิ้น ต้องลอง!',
                '🌿 บรรยากาศดี กาแฟอร่อย ที่นี่เลย! มาเช็คอินกันค่ะ 📍',
                '☀️ วันหยุดนี้มานั่งชิลล์ที่ร้านเรานะคะ มีโปรพิเศษสำหรับวันนี้ด้วย 🎉',
            ],
            'fashion' => [
                '👗 Look of the day! ชุดนี้น่ารักมากเลย แมทช์ง่าย ใส่ได้ทุกวัน ✨',
                '💕 เทรนด์ใหม่มาแรง! {item} ต้องมีติดตู้เสื้อผ้านะคะ 👠',
                '🛍️ ลุคคูลๆ สำหรับวันทำงาน ดูดีมีสไตล์ทั้งวัน! OOTD 💼',
                '✨ Mix & Match แบบนี้สวยมาก! แชร์ไอเดียแต่งตัวกันค่ะ 👗👜',
            ],
        ];

        $statuses = [
            Post::STATUS_DRAFT,
            Post::STATUS_SCHEDULED,
            Post::STATUS_PUBLISHED,
            Post::STATUS_PUBLISHED,
            Post::STATUS_PUBLISHED,
        ];

        foreach ($campaigns as $campaign) {
            $socialAccounts = SocialAccount::where('brand_id', $campaign->brand_id)->get();

            if ($socialAccounts->isEmpty()) {
                continue;
            }

            // Determine content category based on campaign type or brand
            $contentCategory = match ($campaign->type) {
                'product_launch' => 'product',
                'engagement' => 'engagement',
                'promotion' => 'promotion',
                default => array_rand(['product' => 1, 'engagement' => 1]),
            };

            // Use brand-specific content if available
            if (Str::contains($campaign->brand->name, 'Cafe')) {
                $contentCategory = 'cafe';
            } elseif (Str::contains($campaign->brand->name, 'Fashion')) {
                $contentCategory = 'fashion';
            }

            $templates = $contentTemplates[$contentCategory] ?? $contentTemplates['product'];

            // Create 5-10 posts per campaign
            $numPosts = rand(5, 10);

            for ($i = 0; $i < $numPosts; $i++) {
                $socialAccount = $socialAccounts->random();
                $status = $statuses[array_rand($statuses)];
                $template = $templates[array_rand($templates)];

                // Replace placeholders
                $content = str_replace(
                    ['{product}', '{discount}', '{price}', '{date}', '{code}', '{menu}', '{item}'],
                    [
                        $campaign->brand->name . ' ' . ['รุ่นใหม่', 'เวอร์ชั่นพิเศษ', 'รุ่นขายดี'][rand(0, 2)],
                        rand(10, 50),
                        rand(199, 999),
                        now()->addDays(rand(1, 7))->format('d/m'),
                        strtoupper(Str::random(6)),
                        ['ลาเต้เย็น', 'คาปูชิโน่', 'ชีสเค้ก', 'ครัวซองต์'][rand(0, 3)],
                        ['กระเป๋า', 'รองเท้า', 'เดรส', 'เสื้อ'][rand(0, 3)],
                    ],
                    $template
                );

                // Add hashtags
                $hashtags = $campaign->brand->hashtags ?? ['PostXAgent', 'Thailand'];

                $scheduledAt = match ($status) {
                    Post::STATUS_SCHEDULED => now()->addHours(rand(1, 72)),
                    Post::STATUS_PUBLISHED => now()->subDays(rand(1, 14)),
                    default => null,
                };

                $publishedAt = $status === Post::STATUS_PUBLISHED ? $scheduledAt : null;

                $metrics = null;
                if ($status === Post::STATUS_PUBLISHED) {
                    $metrics = [
                        'likes' => rand(50, 5000),
                        'comments' => rand(5, 200),
                        'shares' => rand(2, 100),
                        'views' => rand(500, 50000),
                        'engagement_rate' => round(rand(10, 80) / 10, 2),
                        'updated_at' => now()->toIso8601String(),
                    ];
                }

                Post::create([
                    'user_id' => $demoUser->id,
                    'brand_id' => $campaign->brand_id,
                    'campaign_id' => $campaign->id,
                    'social_account_id' => $socialAccount->id,
                    'platform' => $socialAccount->platform,
                    'content_text' => $content,
                    'content_type' => [Post::TYPE_TEXT, Post::TYPE_IMAGE, Post::TYPE_IMAGE][rand(0, 2)],
                    'media_urls' => rand(0, 1) ? ['https://picsum.photos/seed/' . Str::random(8) . '/800/600'] : null,
                    'hashtags' => $hashtags,
                    'ai_generated' => rand(0, 1) === 1,
                    'ai_provider' => rand(0, 1) ? ['ollama', 'google', 'openai'][rand(0, 2)] : null,
                    'scheduled_at' => $scheduledAt,
                    'published_at' => $publishedAt,
                    'status' => $status,
                    'metrics' => $metrics,
                    'platform_post_id' => $status === Post::STATUS_PUBLISHED ? Str::uuid()->toString() : null,
                    'platform_url' => $status === Post::STATUS_PUBLISHED
                        ? 'https://' . $socialAccount->platform . '.com/post/' . Str::random(12)
                        : null,
                ]);
            }
        }
    }
}
