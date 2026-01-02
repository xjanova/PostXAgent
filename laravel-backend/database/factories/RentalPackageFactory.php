<?php

declare(strict_types=1);

namespace Database\Factories;

use App\Models\RentalPackage;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\RentalPackage>
 */
class RentalPackageFactory extends Factory
{
    protected $model = RentalPackage::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'name' => fake()->words(2, true) . ' Package',
            'description' => fake()->sentence(),
            'posts_limit' => fake()->randomElement([50, 100, 200, 500]),
            'accounts_limit' => fake()->randomElement([5, 10, 20, 50]),
            'brands_limit' => fake()->randomElement([1, 3, 5, 10]),
            'price' => fake()->randomFloat(2, 99, 999),
            'duration_days' => fake()->randomElement([30, 90, 180, 365]),
            'is_active' => true,
        ];
    }
}
