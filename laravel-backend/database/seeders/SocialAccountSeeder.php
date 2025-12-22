<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use App\Models\SocialAccount;
use App\Models\Brand;
use App\Models\User;
use Illuminate\Support\Str;

class SocialAccountSeeder extends Seeder
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

        // Define platforms with sample data
        $platforms = [
            SocialAccount::PLATFORM_FACEBOOK => [
                'display_name_suffix' => 'Page',
                'avatar_template' => 'https://picsum.photos/seed/{seed}/200',
                'profile_template' => 'https://facebook.com/{username}',
            ],
            SocialAccount::PLATFORM_INSTAGRAM => [
                'display_name_suffix' => 'IG',
                'avatar_template' => 'https://picsum.photos/seed/{seed}/200',
                'profile_template' => 'https://instagram.com/{username}',
            ],
            SocialAccount::PLATFORM_TIKTOK => [
                'display_name_suffix' => 'TikTok',
                'avatar_template' => 'https://picsum.photos/seed/{seed}/200',
                'profile_template' => 'https://tiktok.com/@{username}',
            ],
            SocialAccount::PLATFORM_TWITTER => [
                'display_name_suffix' => 'X',
                'avatar_template' => 'https://picsum.photos/seed/{seed}/200',
                'profile_template' => 'https://twitter.com/{username}',
            ],
            SocialAccount::PLATFORM_LINE => [
                'display_name_suffix' => 'OA',
                'avatar_template' => 'https://picsum.photos/seed/{seed}/200',
                'profile_template' => 'https://line.me/R/ti/p/{username}',
            ],
        ];

        foreach ($brands as $brand) {
            // Create 2-3 social accounts per brand
            $selectedPlatforms = array_rand($platforms, rand(2, 3));
            if (!is_array($selectedPlatforms)) {
                $selectedPlatforms = [$selectedPlatforms];
            }

            foreach ($selectedPlatforms as $platform) {
                $config = $platforms[$platform];
                $username = Str::slug($brand->name) . '_official';
                $seed = $brand->id . '_' . $platform;

                SocialAccount::firstOrCreate(
                    [
                        'user_id' => $demoUser->id,
                        'brand_id' => $brand->id,
                        'platform' => $platform,
                    ],
                    [
                        'platform_user_id' => Str::uuid()->toString(),
                        'platform_username' => $username,
                        'display_name' => $brand->name . ' ' . $config['display_name_suffix'],
                        'access_token' => 'demo_access_token_' . Str::random(32),
                        'refresh_token' => 'demo_refresh_token_' . Str::random(32),
                        'token_expires_at' => now()->addDays(60),
                        'profile_url' => str_replace('{username}', $username, $config['profile_template']),
                        'avatar_url' => str_replace('{seed}', $seed, $config['avatar_template']),
                        'metadata' => [
                            'followers_count' => rand(1000, 50000),
                            'following_count' => rand(100, 500),
                            'posts_count' => rand(50, 500),
                            'verified' => rand(0, 1) === 1,
                        ],
                        'is_active' => true,
                    ]
                );
            }
        }
    }
}
