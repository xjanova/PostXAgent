<?php

namespace Tests\Unit;

use App\Models\RentalPackage;
use App\Models\UserRental;
use App\Models\User;
use Carbon\Carbon;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class RentalPackageModelTest extends TestCase
{
    use RefreshDatabase;

    public function test_rental_package_has_user_rentals_relationship(): void
    {
        $package = RentalPackage::factory()->create();
        $user = User::factory()->create();

        UserRental::factory()->count(3)->create([
            'user_id' => $user->id,
            'rental_package_id' => $package->id,
        ]);

        $this->assertCount(3, $package->userRentals);
        $this->assertInstanceOf(UserRental::class, $package->userRentals->first());
    }

    public function test_rental_package_scope_active(): void
    {
        RentalPackage::factory()->count(2)->create(['is_active' => true]);
        RentalPackage::factory()->create(['is_active' => false]);

        $this->assertCount(2, RentalPackage::active()->get());
    }

    public function test_rental_package_scope_featured(): void
    {
        RentalPackage::factory()->create(['is_featured' => true]);
        RentalPackage::factory()->count(2)->create(['is_featured' => false]);

        $this->assertCount(1, RentalPackage::featured()->get());
    }

    public function test_get_display_name_returns_thai_when_locale_is_th(): void
    {
        app()->setLocale('th');

        $package = RentalPackage::factory()->create([
            'name' => 'Monthly',
            'name_th' => 'รายเดือน',
        ]);

        $this->assertEquals('รายเดือน', $package->display_name);
    }

    public function test_get_display_name_returns_english_when_locale_is_en(): void
    {
        app()->setLocale('en');

        $package = RentalPackage::factory()->create([
            'name' => 'Monthly',
            'name_th' => 'รายเดือน',
        ]);

        $this->assertEquals('Monthly', $package->display_name);
    }

    public function test_get_duration_text_for_monthly(): void
    {
        $package = RentalPackage::factory()->create([
            'duration_type' => 'monthly',
            'duration_value' => 1,
        ]);

        $this->assertEquals('1 เดือน', $package->duration_text);

        $package->duration_value = 3;
        $this->assertEquals('3 เดือน', $package->duration_text);
    }

    public function test_get_duration_text_for_daily(): void
    {
        $package = RentalPackage::factory()->create([
            'duration_type' => 'daily',
            'duration_value' => 1,
        ]);

        $this->assertEquals('1 วัน', $package->duration_text);

        $package->duration_value = 7;
        $this->assertEquals('7 วัน', $package->duration_text);
    }

    public function test_get_discount_percentage(): void
    {
        $package = RentalPackage::factory()->create([
            'price' => 1000,
            'original_price' => 2000,
        ]);

        $this->assertEquals(50, $package->discount_percentage);
    }

    public function test_get_discount_percentage_returns_null_when_no_discount(): void
    {
        $package = RentalPackage::factory()->create([
            'price' => 1000,
            'original_price' => null,
        ]);

        $this->assertNull($package->discount_percentage);

        $package->original_price = 1000;
        $this->assertNull($package->discount_percentage);
    }

    public function test_calculate_expiry_date_for_monthly(): void
    {
        $package = RentalPackage::factory()->create([
            'duration_type' => 'monthly',
            'duration_value' => 1,
        ]);

        $startDate = Carbon::parse('2024-01-15');
        $expiryDate = $package->calculateExpiryDate($startDate);

        $this->assertEquals('2024-02-15', $expiryDate->format('Y-m-d'));
    }

    public function test_calculate_expiry_date_for_weekly(): void
    {
        $package = RentalPackage::factory()->create([
            'duration_type' => 'weekly',
            'duration_value' => 2,
        ]);

        $startDate = Carbon::parse('2024-01-01');
        $expiryDate = $package->calculateExpiryDate($startDate);

        $this->assertEquals('2024-01-15', $expiryDate->format('Y-m-d'));
    }

    public function test_calculate_expiry_date_for_daily(): void
    {
        $package = RentalPackage::factory()->create([
            'duration_type' => 'daily',
            'duration_value' => 7,
        ]);

        $startDate = Carbon::parse('2024-01-01');
        $expiryDate = $package->calculateExpiryDate($startDate);

        $this->assertEquals('2024-01-08', $expiryDate->format('Y-m-d'));
    }

    public function test_calculate_expiry_date_for_yearly(): void
    {
        $package = RentalPackage::factory()->create([
            'duration_type' => 'yearly',
            'duration_value' => 1,
        ]);

        $startDate = Carbon::parse('2024-01-15');
        $expiryDate = $package->calculateExpiryDate($startDate);

        $this->assertEquals('2025-01-15', $expiryDate->format('Y-m-d'));
    }

    public function test_get_usage_limits_returns_array(): void
    {
        $package = RentalPackage::factory()->create([
            'posts_limit' => 100,
            'brands_limit' => 5,
            'platforms_limit' => 9,
            'ai_generations_limit' => 500,
            'accounts_per_platform' => 3,
            'scheduled_posts_limit' => 50,
            'team_members_limit' => 10,
        ]);

        $limits = $package->getUsageLimits();

        $this->assertIsArray($limits);
        $this->assertEquals(100, $limits['posts']);
        $this->assertEquals(5, $limits['brands']);
        $this->assertEquals(9, $limits['platforms']);
        $this->assertEquals(500, $limits['ai_generations']);
        $this->assertEquals(3, $limits['accounts_per_platform']);
        $this->assertEquals(50, $limits['scheduled_posts']);
        $this->assertEquals(10, $limits['team_members']);
    }

    public function test_is_unlimited_returns_true_for_minus_one(): void
    {
        $package = RentalPackage::factory()->create([
            'posts_limit' => -1,
            'brands_limit' => 5,
        ]);

        $this->assertTrue($package->isUnlimited('posts'));
        $this->assertFalse($package->isUnlimited('brands'));
    }

    public function test_get_formatted_price(): void
    {
        $package = RentalPackage::factory()->create([
            'price' => 1490,
            'currency' => 'THB',
        ]);

        $this->assertEquals('1,490 THB', $package->getFormattedPrice());
    }

    public function test_get_price_per_day(): void
    {
        $package = RentalPackage::factory()->create([
            'price' => 2970,
            'duration_type' => 'monthly',
            'duration_value' => 1,
        ]);

        $pricePerDay = $package->getPricePerDay();

        $this->assertEquals(99, $pricePerDay);
    }

    public function test_get_duration_in_days_for_various_types(): void
    {
        $monthly = RentalPackage::factory()->create([
            'duration_type' => 'monthly',
            'duration_value' => 1,
        ]);
        $this->assertEquals(30, $monthly->getDurationInDays());

        $weekly = RentalPackage::factory()->create([
            'duration_type' => 'weekly',
            'duration_value' => 2,
        ]);
        $this->assertEquals(14, $weekly->getDurationInDays());

        $daily = RentalPackage::factory()->create([
            'duration_type' => 'daily',
            'duration_value' => 7,
        ]);
        $this->assertEquals(7, $daily->getDurationInDays());

        $yearly = RentalPackage::factory()->create([
            'duration_type' => 'yearly',
            'duration_value' => 1,
        ]);
        $this->assertEquals(365, $yearly->getDurationInDays());
    }

    public function test_rental_package_soft_deletes(): void
    {
        $package = RentalPackage::factory()->create();

        $package->delete();

        $this->assertSoftDeleted('rental_packages', ['id' => $package->id]);
        $this->assertNull(RentalPackage::find($package->id));
        $this->assertNotNull(RentalPackage::withTrashed()->find($package->id));
    }

    public function test_rental_package_fillable_attributes(): void
    {
        $package = RentalPackage::create([
            'name' => 'Premium',
            'name_th' => 'พรีเมียม',
            'description' => 'Premium package',
            'duration_type' => 'monthly',
            'duration_value' => 1,
            'price' => 1990,
            'currency' => 'THB',
            'posts_limit' => 1000,
            'brands_limit' => 10,
            'is_active' => true,
        ]);

        $this->assertEquals('Premium', $package->name);
        $this->assertEquals('พรีเมียม', $package->name_th);
        $this->assertEquals(1990, $package->price);
    }

    public function test_rental_package_casts(): void
    {
        $package = RentalPackage::factory()->create([
            'price' => 1990.50,
            'features' => ['feature1', 'feature2'],
            'included_platforms' => ['facebook', 'instagram'],
            'is_active' => true,
            'is_featured' => false,
        ]);

        $this->assertIsArray($package->features);
        $this->assertIsArray($package->included_platforms);
        $this->assertIsBool($package->is_active);
        $this->assertIsBool($package->is_featured);
    }

    public function test_create_default_packages(): void
    {
        RentalPackage::createDefaultPackages();

        $this->assertDatabaseCount('rental_packages', 6);
        $this->assertDatabaseHas('rental_packages', ['name' => 'Trial']);
        $this->assertDatabaseHas('rental_packages', ['name' => 'Daily Pass']);
        $this->assertDatabaseHas('rental_packages', ['name' => 'Weekly']);
        $this->assertDatabaseHas('rental_packages', ['name' => 'Monthly']);
        $this->assertDatabaseHas('rental_packages', ['name' => 'Quarterly']);
        $this->assertDatabaseHas('rental_packages', ['name' => 'Yearly']);
    }
}
