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

        // AccountPoolController::index returns {success, data: [pools]} (not paginated)
        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'name', 'platform', 'rotation_strategy'],
                ],
            ]);

        $this->assertCount(3, $response->json('data'));
    }

    public function test_can_create_account_pool(): void
    {
        $response = $this->postJson('/api/v1/account-pools', [
            'brand_id' => $this->brand->id,
            'name' => 'Facebook Pool',
            'platform' => 'facebook',
            'rotation_strategy' => 'round_robin',
        ]);

        // AccountPoolController::store returns {success, data, message}
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

        // AccountPoolController::show returns {success, data: pool with statistics}
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

        // AccountPoolController::update returns {success, data, message}
        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'Updated Name');

        $this->assertDatabaseHas('account_pools', [
            'id' => $pool->id,
            'name' => 'Updated Name',
        ]);
    }

    public function test_can_delete_account_pool(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->deleteJson("/api/v1/account-pools/{$pool->id}");

        // AccountPoolController::destroy returns {success, message}
        $response->assertOk()
            ->assertJsonPath('success', true);

        // Model uses SoftDeletes
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

        // AccountPoolController::addAccount returns {success, data, message}
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

        $response = $this->deleteJson("/api/v1/account-pools/{$pool->id}/accounts/{$account->id}");

        // AccountPoolController::removeAccount returns {success, message}
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

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertCount(2, $response->json('data'));
    }

    public function test_can_get_pool_statistics(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson("/api/v1/account-pools/{$pool->id}/statistics");

        // AccountPoolController::statistics returns {success, data: statistics}
        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data',
            ]);
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

        $response->assertStatus(422)
            ->assertJsonPath('success', false);
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

        $response->assertStatus(422)
            ->assertJsonPath('success', false);
    }

    public function test_index_requires_brand_id(): void
    {
        $response = $this->getJson('/api/v1/account-pools');

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['brand_id']);
    }

    public function test_cannot_create_duplicate_pool_name(): void
    {
        AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
            'name' => 'My Pool',
        ]);

        $response = $this->postJson('/api/v1/account-pools', [
            'brand_id' => $this->brand->id,
            'name' => 'My Pool',
            'platform' => 'facebook',
        ]);

        $response->assertStatus(422)
            ->assertJsonPath('success', false);
    }
}
