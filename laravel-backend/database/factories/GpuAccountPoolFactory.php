<?php

namespace Database\Factories;

use App\Models\GpuAccountPool;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\GpuAccountPool>
 */
class GpuAccountPoolFactory extends Factory
{
    protected $model = GpuAccountPool::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'user_id' => User::factory(),
            'provider' => fake()->randomElement(['vast_ai', 'runpod', 'mixed']),
            'name' => fake()->company() . ' GPU Pool',
            'description' => fake()->sentence(),
            'rotation_strategy' => GpuAccountPool::STRATEGY_MOST_CREDITS,
            'cooldown_minutes' => 60,
            'min_credits_required' => 0.50,
            'auto_failover' => true,
            'auto_provision' => true,
            'preferred_gpu_types' => ['RTX_4090', 'RTX_3090', 'A100'],
            'preferred_regions' => ['us-east', 'us-west', 'eu-west'],
            'max_price_per_hour' => 1.50,
            'is_active' => true,
        ];
    }

    /**
     * Pool for Vast.ai provider
     */
    public function vastAi(): static
    {
        return $this->state(fn (array $attributes) => [
            'provider' => 'vast_ai',
            'name' => 'Vast.ai Pool',
        ]);
    }

    /**
     * Pool for RunPod provider
     */
    public function runPod(): static
    {
        return $this->state(fn (array $attributes) => [
            'provider' => 'runpod',
            'name' => 'RunPod Pool',
        ]);
    }

    /**
     * Pool with round robin strategy
     */
    public function roundRobin(): static
    {
        return $this->state(fn (array $attributes) => [
            'rotation_strategy' => GpuAccountPool::STRATEGY_ROUND_ROBIN,
        ]);
    }

    /**
     * Pool with priority strategy
     */
    public function priority(): static
    {
        return $this->state(fn (array $attributes) => [
            'rotation_strategy' => GpuAccountPool::STRATEGY_PRIORITY,
        ]);
    }

    /**
     * Pool with auto failover disabled
     */
    public function noFailover(): static
    {
        return $this->state(fn (array $attributes) => [
            'auto_failover' => false,
        ]);
    }

    /**
     * Inactive pool
     */
    public function inactive(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_active' => false,
        ]);
    }
}
