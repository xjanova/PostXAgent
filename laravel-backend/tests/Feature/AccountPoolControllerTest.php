<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\Brand;
use App\Models\AccountPool;
use App\Models\AccountPoolMember;
use App\Models\SocialAccount;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Tests\TestCase;

class AccountPoolControllerTest extends TestCase
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

    public function test_can_list_account_pools(): void
    {
        AccountPool::factory()->count(3)->create([
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson('/api/v1/account-pools?brand_id=' . $this->brand->id);

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'name', 'platform', 'rotation_strategy'],
                ],
            ]);
    }

    public function test_can_create_account_pool(): void
    {
        $data = [
            'brand_id' => $this->brand->id,
            'name' => 'Facebook Pool',
            'platform' => 'facebook',
            'rotation_strategy' => 'round_robin',
        ];

        $response = $this->postJson('/api/v1/account-pools', $data);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'Facebook Pool');

        $this->assertDatabaseHas('account_pools', ['name' => 'Facebook Pool']);
    }

    public function test_can_show_account_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson("/api/v1/account-pools/{$pool->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.id', $pool->id);
    }

    public function test_can_update_account_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'name' => 'Original Name',
        ]);

        $response = $this->putJson("/api/v1/account-pools/{$pool->id}", [
            'name' => 'Updated Name',
            'rotation_strategy' => 'random',
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'Updated Name');
    }

    public function test_can_delete_account_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->deleteJson("/api/v1/account-pools/{$pool->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertSoftDeleted('account_pools', ['id' => $pool->id]);
    }

    public function test_can_add_account_to_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        $response = $this->postJson("/api/v1/account-pools/{$pool->id}/accounts", [
            'social_account_id' => $account->id,
            'priority' => 1,
            'weight' => 100,
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true);

        $this->assertDatabaseHas('account_pool_members', [
            'account_pool_id' => $pool->id,
            'social_account_id' => $account->id,
        ]);
    }

    public function test_can_remove_account_from_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        AccountPoolMember::factory()->create([
            'account_pool_id' => $pool->id,
            'social_account_id' => $account->id,
        ]);

        $response = $this->deleteJson("/api/v1/account-pools/{$pool->id}/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertDatabaseMissing('account_pool_members', [
            'account_pool_id' => $pool->id,
            'social_account_id' => $account->id,
        ]);
    }

    public function test_can_filter_pools_by_platform(): void
    {
        AccountPool::factory()->count(2)->create([
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'platform' => 'instagram',
        ]);

        $response = $this->getJson('/api/v1/account-pools?brand_id=' . $this->brand->id . '&platform=facebook');

        $response->assertOk();
        $data = $response->json('data');
        $this->assertCount(2, $data);
    }

    public function test_can_get_pool_statistics(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson("/api/v1/account-pools/{$pool->id}/statistics");

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_validation_requires_name(): void
    {
        $response = $this->postJson('/api/v1/account-pools', [
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['name']);
    }

    public function test_validation_requires_valid_platform(): void
    {
        $response = $this->postJson('/api/v1/account-pools', [
            'brand_id' => $this->brand->id,
            'name' => 'Test Pool',
            'platform' => 'invalid_platform',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['platform']);
    }

    public function test_validation_requires_valid_rotation_strategy(): void
    {
        $response = $this->postJson('/api/v1/account-pools', [
            'brand_id' => $this->brand->id,
            'name' => 'Test Pool',
            'platform' => 'facebook',
            'rotation_strategy' => 'invalid_strategy',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['rotation_strategy']);
    }

    public function test_cannot_add_mismatched_platform_account(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'platform' => 'instagram',
        ]);

        $response = $this->postJson("/api/v1/account-pools/{$pool->id}/accounts", [
            'social_account_id' => $account->id,
        ]);

        $response->assertStatus(422);
    }

    public function test_cannot_add_duplicate_account_to_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        AccountPoolMember::factory()->create([
            'account_pool_id' => $pool->id,
            'social_account_id' => $account->id,
        ]);

        $response = $this->postJson("/api/v1/account-pools/{$pool->id}/accounts", [
            'social_account_id' => $account->id,
        ]);

        $response->assertStatus(422);
    }
}
