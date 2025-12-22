<?php

namespace Database\Factories;

use App\Models\Payment;
use App\Models\User;
use App\Models\UserRental;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Payment>
 */
class PaymentFactory extends Factory
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
            'user_rental_id' => UserRental::factory(),
            'amount' => fake()->randomFloat(2, 99, 9999),
            'currency' => 'THB',
            'payment_method' => fake()->randomElement(['bank_transfer', 'promptpay', 'credit_card', 'qr_code']),
            'status' => 'pending',
            'reference_number' => 'REF-' . fake()->uuid(),
            'transaction_id' => null,
            'gateway_response' => null,
            'paid_at' => null,
            'metadata' => [],
        ];
    }

    /**
     * Indicate that the payment is confirmed.
     */
    public function confirmed(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'confirmed',
            'paid_at' => now(),
            'transaction_id' => 'TXN-' . fake()->uuid(),
        ]);
    }

    /**
     * Indicate that the payment is pending.
     */
    public function pending(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'pending',
            'paid_at' => null,
        ]);
    }

    /**
     * Indicate that the payment is failed.
     */
    public function failed(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'failed',
            'gateway_response' => ['error' => 'Payment declined'],
        ]);
    }

    /**
     * Indicate that the payment is refunded.
     */
    public function refunded(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'refunded',
            'metadata' => ['refund_reason' => 'Customer request'],
        ]);
    }

    /**
     * Set bank transfer as payment method.
     */
    public function bankTransfer(): static
    {
        return $this->state(fn (array $attributes) => [
            'payment_method' => 'bank_transfer',
        ]);
    }

    /**
     * Set PromptPay as payment method.
     */
    public function promptpay(): static
    {
        return $this->state(fn (array $attributes) => [
            'payment_method' => 'promptpay',
        ]);
    }
}
