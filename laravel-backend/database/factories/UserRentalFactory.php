<?php

namespace Database\Factories;

use App\Models\UserRental;
use App\Models\User;
use App\Models\RentalPackage;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\UserRental>
 */
class UserRentalFactory extends Factory
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
            'rental_package_id' => RentalPackage::factory(),
            'starts_at' => now(),
            'expires_at' => now()->addMonth(),
            'cancelled_at' => null,
            'status' => UserRental::STATUS_ACTIVE,
            'amount_paid' => fake()->randomFloat(2, 99, 9999),
            'currency' => 'THB',
            'payment_method' => fake()->randomElement(['bank_transfer', 'promptpay', 'credit_card']),
            'payment_reference' => 'PAY-' . fake()->uuid(),
            'posts_used' => 0,
            'ai_generations_used' => 0,
            'usage_posts' => 0,
            'usage_ai_generations' => 0,
            'usage_brands' => 0,
            'usage_platforms' => 0,
            'usage_team_members' => 0,
            'usage_stats' => [],
            'auto_renew' => false,
            'next_renewal_at' => null,
            'metadata' => [],
            'notes' => null,
        ];
    }

    /**
     * Indicate that the rental is pending.
     */
    public function pending(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => UserRental::STATUS_PENDING,
            'starts_at' => null,
            'expires_at' => null,
        ]);
    }

    /**
     * Indicate that the rental is active.
     */
    public function active(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => UserRental::STATUS_ACTIVE,
            'starts_at' => now(),
            'expires_at' => now()->addMonth(),
        ]);
    }

    /**
     * Indicate that the rental is expired.
     */
    public function expired(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => UserRental::STATUS_EXPIRED,
            'starts_at' => now()->subMonth(),
            'expires_at' => now()->subDay(),
        ]);
    }

    /**
     * Indicate that the rental is cancelled.
     */
    public function cancelled(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => UserRental::STATUS_CANCELLED,
            'cancelled_at' => now(),
            'auto_renew' => false,
        ]);
    }

    /**
     * Indicate that the rental has auto-renew enabled.
     */
    public function autoRenew(): static
    {
        return $this->state(fn (array $attributes) => [
            'auto_renew' => true,
            'next_renewal_at' => now()->addMonth()->subDays(3),
        ]);
    }

    /**
     * Indicate that the rental expires soon.
     */
    public function expiringSoon(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => UserRental::STATUS_ACTIVE,
            'starts_at' => now()->subMonth(),
            'expires_at' => now()->addDays(3),
        ]);
    }

    /**
     * Set specific usage.
     */
    public function withUsage(int $posts = 50, int $aiGenerations = 100): static
    {
        return $this->state(fn (array $attributes) => [
            'posts_used' => $posts,
            'ai_generations_used' => $aiGenerations,
            'usage_posts' => $posts,
            'usage_ai_generations' => $aiGenerations,
        ]);
    }
}
