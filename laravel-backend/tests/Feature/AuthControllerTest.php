<?php

namespace Tests\Feature;

use App\Models\User;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Hash;
use Tests\TestCase;

class AuthControllerTest extends TestCase
{
    use RefreshDatabase;

    public function test_user_can_register(): void
    {
        $response = $this->postJson('/api/v1/auth/register', [
            'name' => 'Test User',
            'email' => 'test@example.com',
            'password' => 'password123',
            'password_confirmation' => 'password123',
        ]);

        $response->assertStatus(201)
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'message',
                'data' => [
                    'user' => ['id', 'name', 'email'],
                    'token',
                ],
            ]);

        $this->assertDatabaseHas('users', [
            'email' => 'test@example.com',
        ]);
    }

    public function test_registration_fails_with_existing_email(): void
    {
        User::factory()->create(['email' => 'existing@example.com']);

        $response = $this->postJson('/api/v1/auth/register', [
            'name' => 'Test User',
            'email' => 'existing@example.com',
            'password' => 'password123',
            'password_confirmation' => 'password123',
        ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['email']);
    }

    public function test_user_can_login(): void
    {
        $user = User::factory()->create([
            'email' => 'test@example.com',
            'password' => Hash::make('password123'),
            'is_active' => true,
        ]);

        $response = $this->postJson('/api/v1/auth/login', [
            'email' => 'test@example.com',
            'password' => 'password123',
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'message',
                'data' => [
                    'user' => ['id', 'email'],
                    'token',
                ],
            ]);
    }

    public function test_login_fails_with_invalid_credentials(): void
    {
        User::factory()->create([
            'email' => 'test@example.com',
            'password' => Hash::make('password123'),
        ]);

        $response = $this->postJson('/api/v1/auth/login', [
            'email' => 'test@example.com',
            'password' => 'wrongpassword',
        ]);

        // AuthController throws ValidationException which returns 422
        $response->assertStatus(422);
    }

    public function test_inactive_user_cannot_login(): void
    {
        User::factory()->create([
            'email' => 'test@example.com',
            'password' => Hash::make('password123'),
            'is_active' => false,
        ]);

        $response = $this->postJson('/api/v1/auth/login', [
            'email' => 'test@example.com',
            'password' => 'password123',
        ]);

        $response->assertStatus(403)
            ->assertJsonPath('success', false);
    }

    public function test_user_can_logout(): void
    {
        $user = User::factory()->create();

        $response = $this->actingAs($user, 'sanctum')
            ->postJson('/api/v1/auth/logout');

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_user_can_get_profile(): void
    {
        $user = User::factory()->create();

        $response = $this->actingAs($user, 'sanctum')
            ->getJson('/api/v1/auth/me');

        // AuthController::me returns {success, data: {user, quota, active_rental}}
        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.user.id', $user->id)
            ->assertJsonPath('data.user.email', $user->email);
    }

    public function test_user_can_update_profile(): void
    {
        $user = User::factory()->create();

        $response = $this->actingAs($user, 'sanctum')
            ->putJson('/api/v1/auth/profile', [
                'name' => 'Updated Name',
                'phone' => '0812345678',
            ]);

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertDatabaseHas('users', [
            'id' => $user->id,
            'name' => 'Updated Name',
            'phone' => '0812345678',
        ]);
    }

    public function test_user_can_update_password(): void
    {
        $user = User::factory()->create([
            'password' => Hash::make('oldpassword'),
        ]);

        $response = $this->actingAs($user, 'sanctum')
            ->putJson('/api/v1/auth/password', [
                'current_password' => 'oldpassword',
                'password' => 'newpassword123',
                'password_confirmation' => 'newpassword123',
            ]);

        $response->assertOk()
            ->assertJsonPath('success', true);

        // Verify new password works
        $this->assertTrue(Hash::check('newpassword123', $user->fresh()->password));
    }

    public function test_update_password_fails_with_wrong_current_password(): void
    {
        $user = User::factory()->create([
            'password' => Hash::make('oldpassword'),
        ]);

        $response = $this->actingAs($user, 'sanctum')
            ->putJson('/api/v1/auth/password', [
                'current_password' => 'wrongpassword',
                'password' => 'newpassword123',
                'password_confirmation' => 'newpassword123',
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['current_password']);
    }

    public function test_unauthenticated_user_cannot_access_protected_routes(): void
    {
        $response = $this->getJson('/api/v1/auth/me');

        $response->assertStatus(401);
    }

    public function test_registration_requires_valid_email(): void
    {
        $response = $this->postJson('/api/v1/auth/register', [
            'name' => 'Test User',
            'email' => 'invalid-email',
            'password' => 'password123',
            'password_confirmation' => 'password123',
        ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['email']);
    }

    public function test_registration_requires_password_confirmation(): void
    {
        $response = $this->postJson('/api/v1/auth/register', [
            'name' => 'Test User',
            'email' => 'test@example.com',
            'password' => 'password123',
            'password_confirmation' => 'different',
        ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['password']);
    }
}
