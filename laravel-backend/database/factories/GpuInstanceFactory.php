<?php

namespace Database\Factories;

use App\Models\GpuInstance;
use App\Models\GpuProviderAccount;
use App\Models\GpuAccountPool;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\GpuInstance>
 */
class GpuInstanceFactory extends Factory
{
    protected $model = GpuInstance::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $gpuTypes = ['RTX_4090', 'RTX_3090', 'A100', 'A6000', 'H100'];

        return [
            'gpu_provider_account_id' => GpuProviderAccount::factory(),
            'gpu_account_pool_id' => null,
            'user_id' => User::factory(),
            'provider' => 'vast_ai',
            'instance_id' => 'inst-' . fake()->uuid(),
            'instance_name' => 'postxagent-' . fake()->numberBetween(1000, 9999),
            'status' => GpuInstance::STATUS_PENDING,
            'gpu_type' => fake()->randomElement($gpuTypes),
            'gpu_count' => 1,
            'cpu_cores' => fake()->randomElement([4, 8, 16, 32]),
            'ram_gb' => fake()->randomElement([16, 32, 64, 128]),
            'disk_gb' => fake()->randomElement([50, 100, 200]),
            'region' => fake()->randomElement(['us-east', 'us-west', 'eu-west']),
            'ip_address' => null,
            'ssh_port' => null,
            'price_per_hour' => fake()->randomFloat(4, 0.30, 2.00),
            'total_cost' => 0,
            'docker_image' => 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime',
            'environment_vars' => ['PYTHONUNBUFFERED' => '1'],
            'setup_script' => null,
            'setup_status' => GpuInstance::SETUP_PENDING,
            'setup_log' => null,
            'started_at' => null,
            'stopped_at' => null,
            'runtime_minutes' => 0,
            'metadata' => null,
        ];
    }

    /**
     * Instance that is running
     */
    public function running(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuInstance::STATUS_RUNNING,
            'ip_address' => fake()->ipv4(),
            'ssh_port' => fake()->numberBetween(22000, 29000),
            'started_at' => now()->subMinutes(fake()->numberBetween(10, 120)),
        ]);
    }

    /**
     * Instance that is stopped
     */
    public function stopped(): static
    {
        $startedAt = now()->subHours(fake()->numberBetween(1, 5));
        $stoppedAt = now()->subMinutes(fake()->numberBetween(10, 30));
        $runtime = $startedAt->diffInMinutes($stoppedAt);

        return $this->state(fn (array $attributes) => [
            'status' => GpuInstance::STATUS_STOPPED,
            'started_at' => $startedAt,
            'stopped_at' => $stoppedAt,
            'runtime_minutes' => $runtime,
        ]);
    }

    /**
     * Instance that is terminated
     */
    public function terminated(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuInstance::STATUS_TERMINATED,
            'started_at' => now()->subHours(2),
            'stopped_at' => now()->subMinutes(5),
            'runtime_minutes' => 115,
        ]);
    }

    /**
     * Instance with error
     */
    public function error(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuInstance::STATUS_ERROR,
            'metadata' => [
                'last_error' => 'Instance failed to start',
                'error_at' => now()->toIso8601String(),
            ],
        ]);
    }

    /**
     * Instance that is starting
     */
    public function starting(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => GpuInstance::STATUS_STARTING,
        ]);
    }

    /**
     * Instance with completed setup
     */
    public function setupCompleted(): static
    {
        return $this->state(fn (array $attributes) => [
            'setup_status' => GpuInstance::SETUP_COMPLETED,
            'setup_log' => "Setup completed successfully\nAll packages installed",
        ]);
    }

    /**
     * Instance with failed setup
     */
    public function setupFailed(): static
    {
        return $this->state(fn (array $attributes) => [
            'setup_status' => GpuInstance::SETUP_FAILED,
            'setup_log' => "Setup failed: pip install error",
        ]);
    }

    /**
     * Instance with high-end GPU
     */
    public function highEnd(): static
    {
        return $this->state(fn (array $attributes) => [
            'gpu_type' => 'H100',
            'gpu_count' => 1,
            'cpu_cores' => 32,
            'ram_gb' => 128,
            'disk_gb' => 200,
            'price_per_hour' => 3.50,
        ]);
    }

    /**
     * Instance for RunPod
     */
    public function runPod(): static
    {
        return $this->state(fn (array $attributes) => [
            'provider' => 'runpod',
        ]);
    }
}
