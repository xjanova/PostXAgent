<?php

namespace Database\Factories;

use App\Models\Post;
use App\Models\User;
use App\Models\Brand;
use App\Models\Campaign;
use App\Models\SocialAccount;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Post>
 */
class PostFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $platforms = SocialAccount::getPlatforms();
        $statuses = [Post::STATUS_DRAFT, Post::STATUS_PENDING, Post::STATUS_SCHEDULED, Post::STATUS_PUBLISHED];
        $contentTypes = [Post::TYPE_TEXT, Post::TYPE_IMAGE, Post::TYPE_VIDEO];

        $status = fake()->randomElement($statuses);
        $scheduledAt = $status === Post::STATUS_SCHEDULED ? fake()->dateTimeBetween('now', '+30 days') : null;
        $publishedAt = $status === Post::STATUS_PUBLISHED ? fake()->dateTimeBetween('-30 days', 'now') : null;

        return [
            'user_id' => User::factory(),
            'brand_id' => Brand::factory(),
            'campaign_id' => null,
            'social_account_id' => SocialAccount::factory(),
            'platform' => fake()->randomElement($platforms),
            'content_text' => fake('th_TH')->paragraph(3),
            'content_type' => fake()->randomElement($contentTypes),
            'media_urls' => fake()->boolean(60) ? ['https://picsum.photos/seed/' . fake()->uuid() . '/800/600'] : null,
            'hashtags' => fake('th_TH')->words(fake()->numberBetween(3, 6)),
            'link_url' => fake()->boolean(30) ? fake()->url() : null,
            'ai_generated' => fake()->boolean(40),
            'ai_provider' => fake()->boolean(40) ? fake()->randomElement(['ollama', 'google', 'openai']) : null,
            'ai_prompt' => fake()->boolean(40) ? fake('th_TH')->sentence() : null,
            'platform_post_id' => $status === Post::STATUS_PUBLISHED ? fake()->uuid() : null,
            'platform_url' => $status === Post::STATUS_PUBLISHED ? fake()->url() : null,
            'scheduled_at' => $scheduledAt,
            'published_at' => $publishedAt,
            'status' => $status,
            'error_message' => null,
            'metrics' => $status === Post::STATUS_PUBLISHED ? [
                'likes' => fake()->numberBetween(10, 5000),
                'comments' => fake()->numberBetween(1, 200),
                'shares' => fake()->numberBetween(0, 100),
                'views' => fake()->numberBetween(100, 50000),
                'engagement_rate' => fake()->randomFloat(2, 0.5, 10),
                'updated_at' => now()->toIso8601String(),
            ] : null,
        ];
    }

    /**
     * Indicate that the post is published.
     */
    public function published(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => fake()->dateTimeBetween('-30 days', 'now'),
            'platform_post_id' => fake()->uuid(),
            'platform_url' => fake()->url(),
            'metrics' => [
                'likes' => fake()->numberBetween(10, 5000),
                'comments' => fake()->numberBetween(1, 200),
                'shares' => fake()->numberBetween(0, 100),
                'views' => fake()->numberBetween(100, 50000),
                'engagement_rate' => fake()->randomFloat(2, 0.5, 10),
            ],
        ]);
    }

    /**
     * Indicate that the post is scheduled.
     */
    public function scheduled(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Post::STATUS_SCHEDULED,
            'scheduled_at' => fake()->dateTimeBetween('now', '+30 days'),
            'published_at' => null,
        ]);
    }

    /**
     * Indicate that the post is a draft.
     */
    public function draft(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Post::STATUS_DRAFT,
            'scheduled_at' => null,
            'published_at' => null,
        ]);
    }

    /**
     * Indicate that the post failed.
     */
    public function failed(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Post::STATUS_FAILED,
            'error_message' => fake()->sentence(),
        ]);
    }

    /**
     * Set platform to Facebook.
     */
    public function facebook(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'facebook',
        ]);
    }

    /**
     * Set platform to Instagram.
     */
    public function instagram(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'instagram',
            'content_type' => fake()->randomElement([Post::TYPE_IMAGE, Post::TYPE_CAROUSEL, Post::TYPE_REEL]),
        ]);
    }

    /**
     * Set platform to TikTok.
     */
    public function tiktok(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'tiktok',
            'content_type' => Post::TYPE_VIDEO,
        ]);
    }
}
