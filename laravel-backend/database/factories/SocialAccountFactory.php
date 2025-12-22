<?php

namespace Database\Factories;

use App\Models\SocialAccount;
use App\Models\User;
use App\Models\Brand;
use Illuminate\Database\Eloquent\Factories\Factory;
use Illuminate\Support\Str;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\SocialAccount>
 */
class SocialAccountFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $platforms = SocialAccount::getPlatforms();
        $platform = fake()->randomElement($platforms);

        return [
            'user_id' => User::factory(),
            'brand_id' => Brand::factory(),
            'platform' => $platform,
            'platform_user_id' => fake()->uuid(),
            'platform_username' => fake()->userName(),
            'display_name' => fake('th_TH')->name(),
            'access_token' => 'test_access_token_' . Str::random(32),
            'refresh_token' => 'test_refresh_token_' . Str::random(32),
            'token_expires_at' => fake()->dateTimeBetween('now', '+60 days'),
            'profile_url' => $this->getProfileUrl($platform, fake()->userName()),
            'avatar_url' => 'https://picsum.photos/seed/' . fake()->uuid() . '/200',
            'metadata' => [
                'followers_count' => fake()->numberBetween(100, 100000),
                'following_count' => fake()->numberBetween(50, 1000),
                'posts_count' => fake()->numberBetween(10, 500),
                'verified' => fake()->boolean(20),
            ],
            'is_active' => true,
        ];
    }

    /**
     * Get profile URL based on platform.
     */
    private function getProfileUrl(string $platform, string $username): string
    {
        return match ($platform) {
            'facebook' => "https://facebook.com/{$username}",
            'instagram' => "https://instagram.com/{$username}",
            'tiktok' => "https://tiktok.com/@{$username}",
            'twitter' => "https://twitter.com/{$username}",
            'line' => "https://line.me/R/ti/p/{$username}",
            'youtube' => "https://youtube.com/channel/{$username}",
            'threads' => "https://threads.net/@{$username}",
            'linkedin' => "https://linkedin.com/in/{$username}",
            'pinterest' => "https://pinterest.com/{$username}",
            default => "https://{$platform}.com/{$username}",
        };
    }

    /**
     * Indicate that the account is inactive.
     */
    public function inactive(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_active' => false,
        ]);
    }

    /**
     * Indicate that the token is expired.
     */
    public function expired(): static
    {
        return $this->state(fn (array $attributes) => [
            'token_expires_at' => fake()->dateTimeBetween('-30 days', '-1 day'),
        ]);
    }

    /**
     * Set platform to Facebook.
     */
    public function facebook(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'facebook',
            'profile_url' => 'https://facebook.com/' . fake()->userName(),
        ]);
    }

    /**
     * Set platform to Instagram.
     */
    public function instagram(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'instagram',
            'profile_url' => 'https://instagram.com/' . fake()->userName(),
        ]);
    }

    /**
     * Set platform to TikTok.
     */
    public function tiktok(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'tiktok',
            'profile_url' => 'https://tiktok.com/@' . fake()->userName(),
        ]);
    }

    /**
     * Set platform to Twitter/X.
     */
    public function twitter(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'twitter',
            'profile_url' => 'https://twitter.com/' . fake()->userName(),
        ]);
    }

    /**
     * Set platform to LINE.
     */
    public function line(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'line',
            'profile_url' => 'https://line.me/R/ti/p/' . fake()->uuid(),
        ]);
    }
}
