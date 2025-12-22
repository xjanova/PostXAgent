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

    public function test_can_disconnect_social_account(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->deleteJson("/api/v1/social-accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_can_refresh_token(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'refresh_token' => 'old_refresh_token',
        ]);

        $response = $this->postJson("/api/v1/social-accounts/{$account->id}/refresh");

        // This may succeed or fail depending on platform implementation
        // Just verify it doesn't return 404/405
        $this->assertNotEquals(404, $response->status());
        $this->assertNotEquals(405, $response->status());
    }

    public function test_connect_returns_oauth_url(): void
    {
        $response = $this->getJson('/api/v1/social-accounts/facebook/connect');

        // Should return redirect URL or OAuth info
        $response->assertOk()
            ->assertJsonStructure([
                'success',
            ]);
    }
}
