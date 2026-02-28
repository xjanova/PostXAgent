<?php

namespace Database\Factories;

use App\Models\GpuActivityLog;
use App\Models\GpuProviderAccount;
use App\Models\GpuAccountPool;
use App\Models\GpuInstance;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\GpuActivityLog>
 */
class GpuActivityLogFactory extends Factory
{
    protected $model = GpuActivityLog::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'gpu_provider_account_id' => GpuProviderAccount::factory(),
            'gpu_account_pool_id' => null,
            'gpu_instance_id' => null,
            'user_id' => User::factory(),
            'provider' => 'vast_ai',
            'event_type' => GpuActivityLog::EVENT_BALANCE_CHECKED,
            'old_status' => null,
            'new_status' => null,
            'message' => fake()->sentence(),
            'metadata' => null,
            'credits_before' => null,
            'credits_after' => null,
            'triggered_by' => GpuActivityLog::TRIGGERED_BY_SYSTEM,
        ];
    }

    /**
     * Instance created event
     */
    public function instanceCreated(): static
    {
        return $this->state(fn (array $attributes) => [
            'event_type' => GpuActivityLog::EVENT_INSTANCE_CREATED,
            'new_status' => 'pending',
            'message' => 'Instance created',
        ]);
    }

    /**
     * API error event
     */
    public function apiError(): static
    {
        return $this->state(fn (array $attributes) => [
            'event_type' => GpuActivityLog::EVENT_API_ERROR,
            'message' => 'API request failed: Connection timeout',
        ]);
    }

    /**
     * Balance checked event
     */
    public function balanceChecked(): static
    {
        return $this->state(fn (array $attributes) => [
            'event_type' => GpuActivityLog::EVENT_BALANCE_CHECKED,
            'credits_before' => 100.0,
            'credits_after' => 95.0,
            'message' => 'Balance checked: $100.00 -> $95.00',
        ]);
    }

    /**
     * Failover triggered event
     */
    public function failoverTriggered(): static
    {
        return $this->state(fn (array $attributes) => [
            'event_type' => GpuActivityLog::EVENT_FAILOVER_TRIGGERED,
            'message' => 'Failover triggered due to API error',
        ]);
    }

    /**
     * User triggered event
     */
    public function userTriggered(): static
    {
        return $this->state(fn (array $attributes) => [
            'triggered_by' => GpuActivityLog::TRIGGERED_BY_USER,
        ]);
    }
}
