<?php

namespace Database\Factories;

use App\Models\GpuProviderAccount;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\GpuProviderAccount>
 */
class GpuProviderAccountFactory extends Factory
{
    protected $model = GpuProviderAccount::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'user_id' => User::factory(),
            'provider' => fake()->randomElement(GpuProviderAccount::getProviders()),
            'name' => fake()->company() . ' GPU Account',
            'email' => fake()->unique()->safeEmail(),
            'api_key' => 'test-api-key-' . fake()->uuid(),
            'api_secret' => 'test-api-secret-' . fake()->uuid(),
            'credits_balance' => fake()->randomFloat(4, 10, 500),
            'credits_used_today' => fake()->randomFloat(4, 0, 20),
            'daily_credit_limit' => fake()->randomFloat(4, 50, 200),
            'low_balance_threshold' => 5.0,
            'status' => GpuProviderAccount::STATUS_ACTIVE,
            'cooldown_until' => null,
            'last_used_at' => null,
            'last_balance_check_at' => now(),
            'instances_created' => fake()->numberBetween(0, 100),
            'total_gpu_hours' => fake()->numberBetween(0, 1000),
            'metadata' => null,
            'is_active' => true,
        ];
    }

    /**
     * Account for Vast.ai provider
     */
    public function vastAi(): static
    {
        return $this->state(fn (array $attributes) => [
            'provider' => GpuProviderAccount::PROVIDER_VAST_AI,
            'name' => 'Vast.ai Account',
        ]);
    }

    /**
     * Account for RunPod provider
     */
    public function runPod(): static
    {
        return $this->state(fn (array $attributes) => [
            'provider' => GpuProviderAccount::PROVIDER_RUNPOD,
            'name' => 'RunPod Account',
        ]);
    }

    /**
     * Account in cooldown
     */
    public function inCooldown(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuProviderAccount::STATUS_COOLDOWN,
            'cooldown_until' => now()->addMinutes(30),
        ]);
    }

    /**
     * Account that is depleted
     */
    public function depleted(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuProviderAccount::STATUS_DEPLETED,
            'credits_balance' => 0,
        ]);
    }

    /**
     * Account with error status
     */
    public function error(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuProviderAccount::STATUS_ERROR,
            'metadata' => [
                'last_error' => 'API authentication failed',
                'error_at' => now()->toIso8601String(),
            ],
        ]);
    }

    /**
     * Account with low balance
     */
    public function lowBalance(): static
    {
        return $this->state(fn (array $attributes) => [
            'credits_balance' => 2.0,
            'low_balance_threshold' => 5.0,
        ]);
    }

    /**
     * Inactive account
     */
    public function inactive(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_active' => false,
        ]);
    }

    /**
     * Account with high balance
     */
    public function highBalance(): static
    {
        return $this->state(fn (array $attributes) => [
            'credits_balance' => 500.0,
        ]);
    }
}
