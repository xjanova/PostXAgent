<?php

namespace Database\Factories;

use App\Models\AccountPoolMember;
use App\Models\AccountPool;
use App\Models\SocialAccount;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\AccountPoolMember>
 */
class AccountPoolMemberFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'account_pool_id' => AccountPool::factory(),
            'social_account_id' => SocialAccount::factory(),
            'priority' => fake()->numberBetween(1, 10),
            'weight' => fake()->numberBetween(1, 100),
            'status' => 'active',
            'posts_today' => 0,
            'total_posts' => 0,
            'success_count' => 0,
            'failure_count' => 0,
            'consecutive_failures' => 0,
            'last_used_at' => null,
            'cooldown_until' => null,
        ];
    }

    /**
     * Indicate that the member is active.
     */
    public function active(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'active',
            'cooldown_until' => null,
        ]);
    }

    /**
     * Indicate that the member is on cooldown.
     */
    public function onCooldown(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'cooldown',
            'cooldown_until' => now()->addHour(),
        ]);
    }

    /**
     * Indicate that the member is banned.
     */
    public function banned(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'banned',
        ]);
    }

    /**
     * Indicate that the member is disabled.
     */
    public function disabled(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'disabled',
        ]);
    }

    /**
     * Set high priority.
     */
    public function highPriority(): static
    {
        return $this->state(fn (array $attributes) => [
            'priority' => 1,
            'weight' => 100,
        ]);
    }

    /**
     * Set usage stats.
     */
    public function withUsage(int $posts = 10, int $success = 8, int $failure = 2): static
    {
        return $this->state(fn (array $attributes) => [
            'total_posts' => $posts,
            'success_count' => $success,
            'failure_count' => $failure,
            'last_used_at' => now(),
        ]);
    }
}
