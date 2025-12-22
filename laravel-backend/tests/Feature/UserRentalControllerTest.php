<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\UserRental;
use App\Models\RentalPackage;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Tests\TestCase;

class UserRentalControllerTest extends TestCase
{
    use RefreshDatabase;

    private User $user;
    private RentalPackage $package;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
        $this->package = RentalPackage::factory()->create();
        Sanctum::actingAs($this->user);
    }

    public function test_can_list_user_rentals(): void
    {
        UserRental::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $response = $this->getJson('/api/v1/rentals');

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'status', 'starts_at', 'expires_at'],
                ],
            ]);
    }

    public function test_can_get_active_rental(): void
    {
        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(30),
        ]);

        $response = $this->getJson('/api/v1/rentals/active');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.status', 'active');
    }

    public function test_can_create_rental(): void
    {
        $response = $this->postJson('/api/v1/rentals', [
            'rental_package_id' => $this->package->id,
            'payment_method' => 'bank_transfer',
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.status', 'pending');

        $this->assertDatabaseHas('user_rentals', [
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);
    }

    public function test_can_show_rental(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $response = $this->getJson("/api/v1/rentals/{$rental->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.id', $rental->id);
    }

    public function test_cannot_show_other_users_rental(): void
    {
        $otherUser = User::factory()->create();
        $rental = UserRental::factory()->create([
            'user_id' => $otherUser->id,
            'rental_package_id' => $this->package->id,
        ]);

        $response = $this->getJson("/api/v1/rentals/{$rental->id}");

        $response->assertForbidden();
    }

    public function test_can_cancel_rental(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
        ]);

        $response = $this->postJson("/api/v1/rentals/{$rental->id}/cancel", [
            'reason' => 'No longer needed',
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true);

        $rental->refresh();
        $this->assertEquals(UserRental::STATUS_CANCELLED, $rental->status);
    }

    public function test_can_renew_rental(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
            'expires_at' => now()->addDays(5),
        ]);

        $response = $this->postJson("/api/v1/rentals/{$rental->id}/renew");

        $response->assertCreated()
            ->assertJsonPath('success', true);

        $this->assertDatabaseCount('user_rentals', 2);
    }

    public function test_can_toggle_auto_renew(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'auto_renew' => false,
        ]);

        $response = $this->postJson("/api/v1/rentals/{$rental->id}/toggle-auto-renew");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $rental->refresh();
        $this->assertTrue($rental->auto_renew);
    }

    public function test_can_get_usage_stats(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'posts_used' => 50,
            'ai_generations_used' => 100,
        ]);

        $response = $this->getJson("/api/v1/rentals/{$rental->id}/usage");

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'posts_used',
                    'ai_generations_used',
                    'remaining_quota',
                ],
            ]);
    }

    public function test_can_filter_rentals_by_status(): void
    {
        UserRental::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_ACTIVE,
        ]);

        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => UserRental::STATUS_EXPIRED,
        ]);

        $response = $this->getJson('/api/v1/rentals?status=active');

        $response->assertOk();
        $data = $response->json('data');
        $this->assertCount(2, $data);
    }

    public function test_validation_requires_package_id(): void
    {
        $response = $this->postJson('/api/v1/rentals', [
            'payment_method' => 'bank_transfer',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['rental_package_id']);
    }

    public function test_validation_requires_valid_payment_method(): void
    {
        $response = $this->postJson('/api/v1/rentals', [
            'rental_package_id' => $this->package->id,
            'payment_method' => 'invalid_method',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['payment_method']);
    }

    public function test_cannot_create_rental_with_inactive_package(): void
    {
        $inactivePackage = RentalPackage::factory()->create(['is_active' => false]);

        $response = $this->postJson('/api/v1/rentals', [
            'rental_package_id' => $inactivePackage->id,
            'payment_method' => 'bank_transfer',
        ]);

        $response->assertUnprocessable();
    }
}
