<?php

namespace Tests\Unit;

use App\Models\User;
use App\Models\Brand;
use App\Models\Post;
use App\Models\UserRental;
use App\Models\RentalPackage;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class UserModelTest extends TestCase
{
    use RefreshDatabase;

    public function test_user_has_brands_relationship(): void
    {
        $user = User::factory()->create();
        Brand::factory()->count(3)->create(['user_id' => $user->id]);

        $this->assertCount(3, $user->brands);
        $this->assertInstanceOf(Brand::class, $user->brands->first());
    }

    public function test_user_has_posts_relationship(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);
        Post::factory()->count(5)->create([
            'user_id' => $user->id,
            'brand_id' => $brand->id,
        ]);

        $this->assertCount(5, $user->posts);
        $this->assertInstanceOf(Post::class, $user->posts->first());
    }

    public function test_user_has_rentals_relationship(): void
    {
        $user = User::factory()->create();
        $package = RentalPackage::factory()->create();
        UserRental::factory()->count(2)->create([
            'user_id' => $user->id,
            'rental_package_id' => $package->id,
        ]);

        $this->assertCount(2, $user->rentals);
        $this->assertInstanceOf(UserRental::class, $user->rentals->first());
    }

    public function test_has_active_rental_returns_true_when_active(): void
    {
        $user = User::factory()->create();
        $package = RentalPackage::factory()->create();

        UserRental::factory()->create([
            'user_id' => $user->id,
            'rental_package_id' => $package->id,
            'status' => 'active',
            'starts_at' => now()->subDay(),
            'expires_at' => now()->addMonth(),
        ]);

        $this->assertTrue($user->hasActiveRental());
    }

    public function test_has_active_rental_returns_false_when_expired(): void
    {
        $user = User::factory()->create();
        $package = RentalPackage::factory()->create();

        UserRental::factory()->create([
            'user_id' => $user->id,
            'rental_package_id' => $package->id,
            'status' => 'active',
            'starts_at' => now()->subMonths(2),
            'expires_at' => now()->subMonth(),
        ]);

        $this->assertFalse($user->hasActiveRental());
    }

    public function test_has_active_rental_returns_false_when_no_rental(): void
    {
        $user = User::factory()->create();

        $this->assertFalse($user->hasActiveRental());
    }

    public function test_active_rental_returns_correct_rental(): void
    {
        $user = User::factory()->create();
        $package = RentalPackage::factory()->create();

        // Old expired rental
        UserRental::factory()->create([
            'user_id' => $user->id,
            'rental_package_id' => $package->id,
            'status' => 'expired',
            'starts_at' => now()->subMonths(2),
            'expires_at' => now()->subMonth(),
        ]);

        // Current active rental
        $activeRental = UserRental::factory()->create([
            'user_id' => $user->id,
            'rental_package_id' => $package->id,
            'status' => 'active',
            'starts_at' => now()->subDay(),
            'expires_at' => now()->addMonth(),
        ]);

        $result = $user->activeRental();

        $this->assertNotNull($result);
        $this->assertEquals($activeRental->id, $result->id);
    }

    public function test_get_usage_quota_returns_free_tier_limits(): void
    {
        $user = User::factory()->create();

        $quota = $user->getUsageQuota();

        $this->assertIsArray($quota);
        $this->assertArrayHasKey('posts_per_month', $quota);
        $this->assertArrayHasKey('brands', $quota);
        $this->assertArrayHasKey('platforms', $quota);
        $this->assertArrayHasKey('ai_generations', $quota);
    }

    public function test_user_password_is_hidden(): void
    {
        $user = User::factory()->create(['password' => 'secret']);

        $array = $user->toArray();

        $this->assertArrayNotHasKey('password', $array);
        $this->assertArrayNotHasKey('remember_token', $array);
    }

    public function test_email_verified_at_is_cast_to_datetime(): void
    {
        $user = User::factory()->create([
            'email_verified_at' => '2024-01-01 12:00:00',
        ]);

        $this->assertInstanceOf(\Carbon\Carbon::class, $user->email_verified_at);
    }

    public function test_is_active_is_cast_to_boolean(): void
    {
        $user = User::factory()->create(['is_active' => 1]);

        $this->assertIsBool($user->is_active);
        $this->assertTrue($user->is_active);
    }
}
