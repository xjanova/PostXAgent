<?php

declare(strict_types=1);

namespace Database\Factories;

use App\Models\RentalPackage;
use App\Models\User;
use App\Models\UserRental;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\UserRental>
 */
class UserRentalFactory extends Factory
{
    protected $model = UserRental::class;

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
            'status' => 'active',
            'starts_at' => now()->subDay(),
            'expires_at' => now()->addMonth(),
            'posts_used' => 0,
        ];
    }
}
