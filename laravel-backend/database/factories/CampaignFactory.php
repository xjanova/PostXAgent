<?php

namespace Database\Factories;

use App\Models\Campaign;
use App\Models\User;
use App\Models\Brand;
use App\Models\SocialAccount;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Campaign>
 */
class CampaignFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $types = ['product_launch', 'engagement', 'promotion', 'awareness', 'traffic', 'sales'];
        $statuses = [Campaign::STATUS_DRAFT, Campaign::STATUS_SCHEDULED, Campaign::STATUS_ACTIVE, Campaign::STATUS_PAUSED];
        $platforms = SocialAccount::getPlatforms();

        return [
            'user_id' => User::factory(),
            'brand_id' => Brand::factory(),
            'name' => fake('th_TH')->sentence(3),
            'description' => fake('th_TH')->paragraph(),
            'type' => fake()->randomElement($types),
            'goal' => fake('th_TH')->sentence(),
            'target_platforms' => fake()->randomElements($platforms, fake()->numberBetween(2, 5)),
            'content_themes' => fake('th_TH')->words(4),
            'posting_schedule' => [
                'frequency' => fake()->randomElement(['daily', 'twice_daily', 'weekly']),
                'times' => ['09:00', '18:00'],
                'days' => ['monday', 'wednesday', 'friday'],
            ],
            'ai_settings' => [
                'tone' => fake()->randomElement(['professional', 'friendly', 'casual']),
                'language' => 'th',
                'include_emoji' => true,
                'hashtag_count' => fake()->numberBetween(3, 7),
            ],
            'budget' => fake()->randomFloat(2, 1000, 50000),
            'start_date' => fake()->dateTimeBetween('-7 days', '+7 days'),
            'end_date' => fake()->dateTimeBetween('+14 days', '+60 days'),
            'status' => fake()->randomElement($statuses),
        ];
    }

    /**
     * Indicate that the campaign is active.
     */
    public function active(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Campaign::STATUS_ACTIVE,
            'start_date' => fake()->dateTimeBetween('-14 days', '-1 day'),
        ]);
    }

    /**
     * Indicate that the campaign is completed.
     */
    public function completed(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Campaign::STATUS_COMPLETED,
            'start_date' => fake()->dateTimeBetween('-60 days', '-30 days'),
            'end_date' => fake()->dateTimeBetween('-7 days', '-1 day'),
        ]);
    }

    /**
     * Indicate that the campaign is a draft.
     */
    public function draft(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Campaign::STATUS_DRAFT,
        ]);
    }

    /**
     * Indicate that the campaign is scheduled.
     */
    public function scheduled(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => Campaign::STATUS_SCHEDULED,
            'start_date' => fake()->dateTimeBetween('+1 day', '+14 days'),
        ]);
    }

    /**
     * Set campaign type to product launch.
     */
    public function productLaunch(): static
    {
        return $this->state(fn (array $attributes) => [
            'type' => 'product_launch',
            'goal' => 'เปิดตัวสินค้าใหม่ให้ลูกค้ารับรู้',
        ]);
    }

    /**
     * Set campaign type to engagement.
     */
    public function engagement(): static
    {
        return $this->state(fn (array $attributes) => [
            'type' => 'engagement',
            'goal' => 'เพิ่ม engagement และการมีส่วนร่วม',
        ]);
    }

    /**
     * Set campaign type to promotion.
     */
    public function promotion(): static
    {
        return $this->state(fn (array $attributes) => [
            'type' => 'promotion',
            'goal' => 'โปรโมทสินค้าและเพิ่มยอดขาย',
        ]);
    }
}
