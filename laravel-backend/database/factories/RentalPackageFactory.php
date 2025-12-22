<?php

namespace Database\Factories;

use App\Models\RentalPackage;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\RentalPackage>
 */
class RentalPackageFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'name' => fake()->randomElement(['Trial', 'Basic', 'Pro', 'Enterprise']),
            'name_th' => fake('th_TH')->randomElement(['ทดลองใช้', 'พื้นฐาน', 'โปร', 'องค์กร']),
            'description' => fake()->sentence(),
            'description_th' => 'แพ็กเกจสำหรับการใช้งาน',
            'duration_type' => fake()->randomElement(['daily', 'weekly', 'monthly', 'yearly']),
            'duration_value' => fake()->randomElement([1, 7, 30, 365]),
            'price' => fake()->randomFloat(2, 99, 9999),
            'original_price' => null,
            'currency' => 'THB',
            'posts_limit' => fake()->randomElement([100, 500, 1000, -1]),
            'brands_limit' => fake()->randomElement([1, 5, 10, -1]),
            'platforms_limit' => 9,
            'ai_generations_limit' => fake()->randomElement([100, 500, 2000, -1]),
            'accounts_per_platform' => fake()->randomElement([1, 3, 5, 10]),
            'scheduled_posts_limit' => fake()->randomElement([10, 50, 100, -1]),
            'team_members_limit' => fake()->randomElement([1, 5, 10, null]),
            'features' => ['basic_posting', 'ai_content', 'scheduling'],
            'included_platforms' => ['facebook', 'instagram', 'twitter'],
            'is_active' => true,
            'is_featured' => false,
            'is_popular' => false,
            'sort_order' => fake()->numberBetween(1, 10),
            'has_trial' => false,
            'trial_days' => null,
        ];
    }

    /**
     * Indicate that the package is featured.
     */
    public function featured(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_featured' => true,
        ]);
    }

    /**
     * Indicate that the package is popular.
     */
    public function popular(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_popular' => true,
        ]);
    }

    /**
     * Indicate that the package is inactive.
     */
    public function inactive(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_active' => false,
        ]);
    }

    /**
     * Create a trial package.
     */
    public function trial(): static
    {
        return $this->state(fn (array $attributes) => [
            'name' => 'Trial',
            'name_th' => 'ทดลองใช้',
            'price' => 0,
            'has_trial' => true,
            'trial_days' => 3,
            'duration_type' => 'daily',
            'duration_value' => 3,
        ]);
    }

    /**
     * Create a monthly package.
     */
    public function monthly(): static
    {
        return $this->state(fn (array $attributes) => [
            'duration_type' => 'monthly',
            'duration_value' => 1,
        ]);
    }

    /**
     * Create an unlimited package.
     */
    public function unlimited(): static
    {
        return $this->state(fn (array $attributes) => [
            'posts_limit' => -1,
            'brands_limit' => -1,
            'ai_generations_limit' => -1,
            'scheduled_posts_limit' => -1,
        ]);
    }
}
