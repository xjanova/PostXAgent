<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\Brand;
use App\Models\SocialAccount;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Tests\TestCase;

class SocialAccountControllerTest extends TestCase
{
    use RefreshDatabase;

    private User $user;
    private Brand $brand;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
        $this->brand = Brand::factory()->create(['user_id' => $this->user->id]);
        Sanctum::actingAs($this->user);
    }

    public function test_can_list_social_accounts(): void
    {
        SocialAccount::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson('/api/v1/social-accounts');

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'platform', 'platform_username'],
                ],
            ]);
    }

    public function test_can_create_social_account(): void
    {
        $data = [
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
            'platform_user_id' => '123456789',
            'platform_username' => 'testuser',
            'display_name' => 'Test User',
            'access_token' => 'test_access_token',
            'is_active' => true,
        ];

        $response = $this->postJson('/api/v1/social-accounts', $data);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.platform', 'facebook');

        $this->assertDatabaseHas('social_accounts', [
            'platform' => 'facebook',
            'platform_username' => 'testuser',
        ]);
    }

    public function test_can_show_social_account(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson("/api/v1/social-accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.id', $account->id);
    }

    public function test_cannot_show_other_users_account(): void
    {
        $otherUser = User::factory()->create();
        $otherBrand = Brand::factory()->create(['user_id' => $otherUser->id]);
        $account = SocialAccount::factory()->create([
            'user_id' => $otherUser->id,
            'brand_id' => $otherBrand->id,
        ]);

        $response = $this->getJson("/api/v1/social-accounts/{$account->id}");

        $response->assertForbidden();
    }

    public function test_can_update_social_account(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'display_name' => 'Original Name',
        ]);

        $response = $this->putJson("/api/v1/social-accounts/{$account->id}", [
            'display_name' => 'Updated Name',
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.display_name', 'Updated Name');
    }

    public function test_can_delete_social_account(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->deleteJson("/api/v1/social-accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertSoftDeleted('social_accounts', ['id' => $account->id]);
    }

    public function test_can_filter_by_platform(): void
    {
        SocialAccount::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'platform' => 'instagram',
        ]);

        $response = $this->getJson('/api/v1/social-accounts?platform=facebook');

        $response->assertOk();
        $data = $response->json('data');
        $this->assertCount(2, $data);
    }

    public function test_can_filter_by_brand(): void
    {
        $otherBrand = Brand::factory()->create(['user_id' => $this->user->id]);

        SocialAccount::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        SocialAccount::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $otherBrand->id,
        ]);

        $response = $this->getJson("/api/v1/social-accounts?brand_id={$this->brand->id}");

        $response->assertOk();
        $data = $response->json('data');
        $this->assertCount(3, $data);
    }

    public function test_can_toggle_active_status(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'is_active' => true,
        ]);

        $response = $this->postJson("/api/v1/social-accounts/{$account->id}/toggle-active");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $account->refresh();
        $this->assertFalse($account->is_active);
    }

    public function test_can_refresh_token(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'refresh_token' => 'old_refresh_token',
        ]);

        $response = $this->postJson("/api/v1/social-accounts/{$account->id}/refresh-token");

        $response->assertOk();
    }

    public function test_validation_requires_platform(): void
    {
        $response = $this->postJson('/api/v1/social-accounts', [
            'brand_id' => $this->brand->id,
            'platform_username' => 'testuser',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['platform']);
    }

    public function test_validation_requires_valid_platform(): void
    {
        $response = $this->postJson('/api/v1/social-accounts', [
            'brand_id' => $this->brand->id,
            'platform' => 'invalid_platform',
            'platform_username' => 'testuser',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['platform']);
    }

    public function test_hides_sensitive_fields_in_response(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'access_token' => 'secret_token',
            'refresh_token' => 'secret_refresh',
        ]);

        $response = $this->getJson("/api/v1/social-accounts/{$account->id}");

        $response->assertOk();
        $data = $response->json('data');
        $this->assertArrayNotHasKey('access_token', $data);
        $this->assertArrayNotHasKey('refresh_token', $data);
    }
}
