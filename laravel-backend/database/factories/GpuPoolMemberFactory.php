<?php

namespace Database\Factories;

use App\Models\GpuPoolMember;
use App\Models\GpuAccountPool;
use App\Models\GpuProviderAccount;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\GpuPoolMember>
 */
class GpuPoolMemberFactory extends Factory
{
    protected $model = GpuPoolMember::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'gpu_account_pool_id' => GpuAccountPool::factory(),
            'gpu_provider_account_id' => GpuProviderAccount::factory(),
            'priority' => fake()->numberBetween(0, 10),
            'weight' => 100,
            'status' => GpuPoolMember::STATUS_ACTIVE,
            'cooldown_until' => null,
            'last_used_at' => null,
            'instances_today' => fake()->numberBetween(0, 10),
            'total_instances' => fake()->numberBetween(0, 100),
            'credits_used_today' => fake()->randomFloat(4, 0, 10),
            'success_count' => fake()->numberBetween(0, 50),
            'failure_count' => fake()->numberBetween(0, 5),
            'consecutive_failures' => 0,
            'last_error' => null,
        ];
    }

    /**
     * Member in cooldown
     */
    public function inCooldown(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuPoolMember::STATUS_COOLDOWN,
            'cooldown_until' => now()->addMinutes(30),
        ]);
    }

    /**
     * Member that is depleted
     */
    public function depleted(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuPoolMember::STATUS_DEPLETED,
        ]);
    }

    /**
     * Member with error status
     */
    public function error(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuPoolMember::STATUS_ERROR,
            'last_error' => 'Connection failed',
            'consecutive_failures' => 5,
        ]);
    }

    /**
     * Member that is suspended
     */
    public function suspended(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuPoolMember::STATUS_SUSPENDED,
            'last_error' => 'Suspended due to repeated failures',
        ]);
    }

    /**
     * High priority member
     */
    public function highPriority(): static
    {
        return $this->state(fn (array $attributes) => [
            'priority' => 0,
            'weight' => 200,
        ]);
    }

    /**
     * Low priority member
     */
    public function lowPriority(): static
    {
        return $this->state(fn (array $attributes) => [
            'priority' => 10,
            'weight' => 50,
        ]);
    }
}
