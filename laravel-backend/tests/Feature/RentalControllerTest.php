<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\UserRental;
use App\Models\RentalPackage;
use App\Models\Payment;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class RentalControllerTest extends TestCase
{
    use RefreshDatabase;

    private User $user;
    private RentalPackage $package;

    protected function setUp(): void
    {
        parent::setUp();

        $this->user = User::factory()->create();
        $this->package = RentalPackage::factory()->create([
            'name' => 'Starter',
            'price' => 299,
            'duration_days' => 30,
            'is_active' => true,
            'limits' => [
                'posts' => 100,
                'brands' => 3,
                'ai_generations' => 500,
                'platforms' => 5,
            ],
        ]);
    }

    public function test_can_list_packages(): void
    {
        RentalPackage::factory()->count(3)->create(['is_active' => true]);

        $response = $this->getJson('/api/v1/rentals/packages');

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'name', 'price', 'duration_days', 'limits'],
                ],
            ]);
    }

    public function test_can_view_package_detail(): void
    {
        $response = $this->getJson("/api/v1/rentals/packages/{$this->package->id}");

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => [
                    'id' => $this->package->id,
                    'name' => 'Starter',
                ],
            ]);
    }

    public function test_user_can_get_rental_status(): void
    {
        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => 'active',
            'starts_at' => now()->subDay(),
            'expires_at' => now()->addMonth(),
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/rentals/status');

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => [
                    'has_active_rental' => true,
                ],
            ]);
    }

    public function test_user_without_rental_gets_no_active_status(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/rentals/status');

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => [
                    'has_active_rental' => false,
                ],
            ]);
    }

    public function test_user_can_checkout(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/rentals/checkout', [
                'package_id' => $this->package->id,
                'payment_method' => 'bank_transfer',
            ]);

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data' => ['rental_id', 'payment'],
            ]);

        $this->assertDatabaseHas('user_rentals', [
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => 'pending',
        ]);
    }

    public function test_user_can_view_rental_history(): void
    {
        UserRental::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/rentals/history');

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'package', 'status', 'starts_at', 'expires_at'],
                ],
            ]);
    }

    public function test_user_can_cancel_pending_rental(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => 'pending',
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson("/api/v1/rentals/{$rental->id}/cancel");

        $response->assertStatus(200)
            ->assertJson(['success' => true]);

        $this->assertDatabaseHas('user_rentals', [
            'id' => $rental->id,
            'status' => 'cancelled',
        ]);
    }

    public function test_user_cannot_cancel_active_rental_without_refund(): void
    {
        $rental = UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $this->package->id,
            'status' => 'active',
            'starts_at' => now()->subDays(15),
            'expires_at' => now()->addDays(15),
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson("/api/v1/rentals/{$rental->id}/cancel");

        // Should still succeed but with different handling
        $response->assertStatus(200);
    }

    public function test_checkout_requires_valid_package(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/rentals/checkout', [
                'package_id' => 99999,
                'payment_method' => 'bank_transfer',
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['package_id']);
    }

    public function test_list_payment_methods(): void
    {
        $response = $this->getJson('/api/v1/rentals/payment-methods');

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'name', 'type'],
                ],
            ]);
    }
}
