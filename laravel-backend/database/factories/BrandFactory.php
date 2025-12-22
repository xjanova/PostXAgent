<?php

namespace Database\Factories;

use App\Models\Brand;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\Brand>
 */
class BrandFactory extends Factory
{
    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        $industries = ['Technology', 'Food & Beverage', 'Fashion', 'Health', 'Education', 'Entertainment', 'Finance', 'Travel'];
        $tones = ['professional', 'friendly', 'casual', 'formal', 'trendy', 'urgent'];

        return [
            'user_id' => User::factory(),
            'name' => fake('th_TH')->company(),
            'description' => fake('th_TH')->paragraph(),
            'industry' => fake()->randomElement($industries),
            'target_audience' => fake('th_TH')->sentence(10),
            'tone' => fake()->randomElement($tones),
            'logo_url' => 'https://picsum.photos/seed/' . fake()->uuid() . '/200',
            'brand_colors' => [fake()->hexColor(), fake()->hexColor(), '#ffffff'],
            'keywords' => fake('th_TH')->words(5),
            'hashtags' => array_map(fn($w) => str_replace(' ', '', $w), fake()->words(4)),
            'website_url' => fake()->url(),
            'settings' => [
                'default_language' => 'th',
                'auto_hashtag' => true,
                'emoji_style' => fake()->randomElement(['none', 'moderate', 'heavy']),
            ],
            'is_active' => true,
        ];
    }

    /**
     * Indicate that the brand is inactive.
     */
    public function inactive(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_active' => false,
        ]);
    }

    /**
     * Indicate that this is a tech brand.
     */
    public function tech(): static
    {
        return $this->state(fn (array $attributes) => [
            'industry' => 'Technology',
            'tone' => 'professional',
            'keywords' => ['เทคโนโลยี', 'ไอที', 'นวัตกรรม', 'ดิจิทัล'],
        ]);
    }

    /**
     * Indicate that this is a food brand.
     */
    public function food(): static
    {
        return $this->state(fn (array $attributes) => [
            'industry' => 'Food & Beverage',
            'tone' => 'friendly',
            'keywords' => ['อาหาร', 'อร่อย', 'สดใหม่', 'คุณภาพ'],
        ]);
    }
}
