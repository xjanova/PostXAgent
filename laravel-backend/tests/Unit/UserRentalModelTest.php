<?php

namespace Tests\Unit;

use App\Models\UserRental;
use App\Models\User;
use App\Models\RentalPackage;
use App\Models\Payment;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class UserRentalModelTest extends TestCase
{
    use RefreshDatabase;

    private User $user;
    private RentalPackage $package;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
        $this->package = RentalPackage::factory()->create([
            'duration_type' => 'monthly',
            'duration_value' => 1,
            'posts_limit' => 100,
            'ai_generations_limit' => 500,
        ]);
    }

    public function test_user_rental_belongs_to_user(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $this->assertInstanceOf(User::class, $rental->user);
        $this->assertEquals($this->user->id, $rental->user->id);
    }

    public function test_user_rental_belongs_to_rental_package(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $this->assertInstanceOf(RentalPackage::class, $rental->rentalPackage);
        $this->assertEquals($this->package->id, $rental->rentalPackage->id);
    }

    public function test_user_rental_has_payments_relationship(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        Payment::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'user_rental_id' => $rental->id,
        ]);

        $this->assertCount(2, $rental->payments);
        $this->assertInstanceOf(Payment::class, $rental->payments->first());
    }

    public function test_user_rental_status_constants(): void
    {
        $this->assertEquals('pending', UserRental::STATUS_PENDING);
        $this->assertEquals('active', UserRental::STATUS_ACTIVE);
        $this->assertEquals('expired', UserRental::STATUS_EXPIRED);
        $this->assertEquals('cancelled', UserRental::STATUS_CANCELLED);
        $this->assertEquals('suspended', UserRental::STATUS_SUSPENDED);
    }

    public function test_is_active_attribute(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(30),
        ]);

        $this->assertTrue($rental->is_active);

        $rental->status = UserRental::STATUS_PENDING;
        $this->assertFalse($rental->is_active);
    }

    public function test_is_expired_attribute(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'expires_at' => now()->subDay(),
        ]);

        $this->assertTrue($rental->is_expired);

        $rental->expires_at = now()->addDays(30);
        $this->assertFalse($rental->is_expired);
    }

    public function test_days_remaining_attribute(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'expires_at' => now()->addDays(15),
        ]);

        $this->assertEquals(15, $rental->days_remaining);

        $rental->expires_at = now()->subDay();
        $this->assertEquals(0, $rental->days_remaining);
    }

    public function test_scope_active(): void
    {
        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(30),
        ]);

        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_PENDING,
            'expires_at' => now()->addDays(30),
        ]);

        $this->assertCount(1, UserRental::active()->get());
    }

    public function test_scope_expired(): void
    {
        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->subDay(),
        ]);

        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(30),
        ]);

        $this->assertCount(1, UserRental::expired()->get());
    }

    public function test_scope_pending(): void
    {
        UserRental::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_PENDING,
        ]);

        $this->assertCount(2, UserRental::pending()->get());
    }

    public function test_activate_method(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_PENDING,
        ]);

        $result = $rental->activate();

        $this->assertTrue($result);
        $rental->refresh();
        $this->assertEquals(UserRental::STATUS_ACTIVE, $rental->status);
        $this->assertNotNull($rental->starts_at);
        $this->assertNotNull($rental->expires_at);
    }

    public function test_activate_fails_when_not_pending(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
        ]);

        $result = $rental->activate();

        $this->assertFalse($result);
    }

    public function test_expire_method(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
        ]);

        $result = $rental->expire();

        $this->assertTrue($result);
        $rental->refresh();
        $this->assertEquals(UserRental::STATUS_EXPIRED, $rental->status);
    }

    public function test_cancel_method(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'auto_renew' => true,
        ]);

        $result = $rental->cancel('User requested');

        $this->assertTrue($result);
        $rental->refresh();
        $this->assertEquals(UserRental::STATUS_CANCELLED, $rental->status);
        $this->assertNotNull($rental->cancelled_at);
        $this->assertFalse($rental->auto_renew);
    }

    public function test_suspend_method(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
        ]);

        $result = $rental->suspend('Payment issue');

        $this->assertTrue($result);
        $rental->refresh();
        $this->assertEquals(UserRental::STATUS_SUSPENDED, $rental->status);
    }

    public function test_increment_post_usage(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'posts_used' => 0,
        ]);

        $rental->incrementPostUsage(5);

        $rental->refresh();
        $this->assertEquals(5, $rental->posts_used);
    }

    public function test_increment_ai_usage(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'ai_generations_used' => 0,
        ]);

        $rental->incrementAIUsage(10);

        $rental->refresh();
        $this->assertEquals(10, $rental->ai_generations_used);
    }

    public function test_can_use_feature(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(30),
            'posts_used' => 50,
            'ai_generations_used' => 250,
        ]);

        $this->assertTrue($rental->canUse('posts'));
        $this->assertTrue($rental->canUse('ai_generations'));

        $rental->posts_used = 100;
        $this->assertFalse($rental->canUse('posts'));
    }

    public function test_can_use_returns_false_when_not_active(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_PENDING,
            'expires_at' => now()->addDays(30),
        ]);

        $this->assertFalse($rental->canUse('posts'));
    }

    public function test_get_remaining_quota(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'posts_used' => 30,
            'ai_generations_used' => 100,
        ]);

        $this->assertEquals(70, $rental->getRemainingQuota('posts'));
        $this->assertEquals(400, $rental->getRemainingQuota('ai_generations'));
    }

    public function test_get_remaining_quota_unlimited(): void
    {
        $unlimitedPackage = RentalPackage::factory()->create([
            'posts_limit' => -1,
        ]);

        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $unlimitedPackage->id,
            'posts_used' => 1000,
        ]);

        $this->assertEquals(-1, $rental->getRemainingQuota('posts'));
    }

    public function test_get_status_label(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $rental->status = UserRental::STATUS_PENDING;
        $this->assertEquals('รอชำระเงิน', $rental->getStatusLabel());

        $rental->status = UserRental::STATUS_ACTIVE;
        $this->assertEquals('ใช้งานอยู่', $rental->getStatusLabel());

        $rental->status = UserRental::STATUS_EXPIRED;
        $this->assertEquals('หมดอายุ', $rental->getStatusLabel());

        $rental->status = UserRental::STATUS_CANCELLED;
        $this->assertEquals('ยกเลิกแล้ว', $rental->getStatusLabel());

        $rental->status = UserRental::STATUS_SUSPENDED;
        $this->assertEquals('ระงับชั่วคราว', $rental->getStatusLabel());
    }

    public function test_renew_creates_new_rental(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(5),
            'auto_renew' => true,
        ]);

        $newRental = $rental->renew();

        $this->assertNotNull($newRental);
        $this->assertEquals($rental->user_id, $newRental->user_id);
        $this->assertEquals($rental->rental_package_id, $newRental->rental_package_id);
        $this->assertEquals(UserRental::STATUS_PENDING, $newRental->status);
        $this->assertTrue($newRental->auto_renew);
    }

    public function test_user_rental_soft_deletes(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $rental->delete();

        $this->assertSoftDeleted('user_rentals', ['id' => $rental->id]);
        $this->assertNull(UserRental::find($rental->id));
        $this->assertNotNull(UserRental::withTrashed()->find($rental->id));
    }

    public function test_usage_percentage_attribute(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'posts_used' => 50,
            'ai_generations_used' => 250,
        ]);

        $percentage = $rental->usage_percentage;

        $this->assertIsArray($percentage);
        $this->assertEquals(50, $percentage['posts']);
        $this->assertEquals(50, $percentage['ai_generations']);
    }
}
