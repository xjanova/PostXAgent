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
        $amount = fake()->randomFloat(2, 99, 9999);
        $fee = fake()->randomFloat(2, 0, 50);

        return [
            'uuid' => fake()->uuid(),
            'user_id' => User::factory(),
            'user_rental_id' => UserRental::factory(),
            'amount' => $amount,
            'fee' => $fee,
            'net_amount' => $amount - $fee,
            'currency' => 'THB',
            'payment_method' => fake()->randomElement(['bank_transfer', 'promptpay', 'credit_card']),
            'status' => 'pending',
            'gateway' => null,
            'gateway_reference' => null,
            'gateway_status' => null,
            'gateway_response' => null,
            'promptpay_qr_url' => null,
            'bank_account_number' => null,
            'bank_name' => null,
            'transfer_slip_url' => null,
            'paid_at' => null,
            'verified_at' => null,
            'verified_by' => null,
            'description' => null,
            'metadata' => [],
            'admin_notes' => null,
        ];
    }

    /**
     * Indicate that the payment is completed.
     */
    public function completed(): static
    {
        return $this->state(fn (array $attributes) => [
            'status' => 'completed',
            'paid_at' => now(),
            'gateway_reference' => 'TXN-' . fake()->uuid(),
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
