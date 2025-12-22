<?php

namespace Database\Factories;

use App\Models\AccountPool;
use App\Models\User;
use App\Models\Brand;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\AccountPool>
 */
class AccountPoolFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'user_id' => User::factory(),
            'brand_id' => Brand::factory(),
            'name' => fake()->words(3, true) . ' Pool',
            'platform' => fake()->randomElement(['facebook', 'instagram', 'twitter', 'tiktok']),
            'rotation_strategy' => fake()->randomElement(['round_robin', 'random', 'least_used', 'weighted']),
            'is_active' => true,
            'settings' => [
                'max_posts_per_account_per_day' => 10,
                'cooldown_minutes' => 30,
            ],
            'metadata' => [],
        ];
    }

    /**
     * Indicate that the pool is inactive.
     */
    public function inactive(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_active' => false,
        ]);
    }

    /**
     * Set round robin rotation strategy.
     */
    public function roundRobin(): static
    {
        return $this->state(fn (array $attributes) => [
            'rotation_strategy' => 'round_robin',
        ]);
    }

    /**
     * Set random rotation strategy.
     */
    public function random(): static
    {
        return $this->state(fn (array $attributes) => [
            'rotation_strategy' => 'random',
        ]);
    }

    /**
     * Set Facebook platform.
     */
    public function facebook(): static
    {
        return $this->state(fn (array $attributes) => [
            'platform' => 'facebook',
        ]);
    }
}
