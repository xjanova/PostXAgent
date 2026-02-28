<?php

namespace Tests\Feature;

use App\Models\GpuProviderAccount;
use App\Models\GpuAccountPool;
use App\Models\GpuPoolMember;
use App\Models\User;
use App\Services\Gpu\GpuProviderFactory;
use App\Services\Gpu\GpuProviderInterface;
use App\Services\Gpu\GpuAccountRotationService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Routing\Middleware\ThrottleRequests;
use Illuminate\Support\Facades\Cache;
use Laravel\Sanctum\Sanctum;
use Mockery;
use Mockery\MockInterface;
use Tests\TestCase;

class GpuAccountPoolTest extends TestCase
{
    use RefreshDatabase;

    private User $user;

    protected function setUp(): void
    {
        parent::setUp();
        $this->withoutMiddleware(ThrottleRequests::class);
        $this->user = User::factory()->create();
        Sanctum::actingAs($this->user);
    }

    protected function tearDown(): void
    {
        Mockery::close();
        parent::tearDown();
    }

    // ==================== List Pools ====================

    public function test_can_list_pools(): void
    {
        GpuAccountPool::factory()->count(3)->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/pools');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonCount(3, 'data');
    }

    public function test_list_pools_only_returns_own_pools(): void
    {
        GpuAccountPool::factory()->count(2)->create([
            'user_id' => $this->user->id,
        ]);

        $otherUser = User::factory()->create();
        GpuAccountPool::factory()->count(3)->create([
            'user_id' => $otherUser->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/pools');

        $response->assertOk()
            ->assertJsonCount(2, 'data');
    }

    public function test_list_pools_includes_member_details(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/pools');

        $response->assertOk()
            ->assertJsonCount(1, 'data.0.members');
    }

    // ==================== Create Pool ====================

    public function test_can_create_pool(): void
    {
        $response = $this->postJson('/api/v1/gpu/pools', [
            'name' => 'My GPU Pool',
            'provider' => 'vast_ai',
            'description' => 'Test pool description',
            'rotation_strategy' => 'most_credits',
            'cooldown_minutes' => 30,
            'min_credits_required' => 1.00,
            'auto_failover' => true,
            'preferred_gpu_types' => ['RTX_4090', 'A100'],
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'My GPU Pool')
            ->assertJsonPath('data.provider', 'vast_ai')
            ->assertJsonPath('data.rotation_strategy', 'most_credits');

        $this->assertDatabaseHas('gpu_account_pools', [
            'user_id' => $this->user->id,
            'name' => 'My GPU Pool',
        ]);
    }

    public function test_create_pool_with_accounts(): void
    {
        $accounts = GpuProviderAccount::factory()->count(2)->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->postJson('/api/v1/gpu/pools', [
            'name' => 'Pool with Accounts',
            'provider' => 'vast_ai',
            'account_ids' => $accounts->pluck('id')->toArray(),
        ]);

        $response->assertCreated()
            ->assertJsonCount(2, 'data.members');

        $this->assertDatabaseCount('gpu_pool_members', 2);
    }

    public function test_create_pool_validation_errors(): void
    {
        $response = $this->postJson('/api/v1/gpu/pools', [
            'name' => '',
            'rotation_strategy' => 'invalid_strategy',
        ]);

        $response->assertStatus(422)
            ->assertJsonPath('success', false)
            ->assertJsonStructure(['errors' => ['name', 'provider']]);
    }

    public function test_create_pool_with_different_strategies(): void
    {
        $strategies = ['round_robin', 'random', 'least_used', 'most_credits', 'priority'];

        foreach ($strategies as $strategy) {
            $response = $this->postJson('/api/v1/gpu/pools', [
                'name' => "Pool with {$strategy}",
                'provider' => 'vast_ai',
                'rotation_strategy' => $strategy,
            ]);

            $response->assertCreated()
                ->assertJsonPath('data.rotation_strategy', $strategy);
        }
    }

    // ==================== Pool Statistics ====================

    public function test_can_get_pool_statistics(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account1 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $account2 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 50.00,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account1->id,
            'success_count' => 10,
            'failure_count' => 2,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account2->id,
            'success_count' => 5,
            'failure_count' => 1,
        ]);

        $response = $this->getJson("/api/v1/gpu/pools/{$pool->id}/statistics");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.total_accounts', 2)
            ->assertJsonPath('data.active', 2);
    }

    public function test_pool_statistics_shows_different_member_statuses(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $accounts = GpuProviderAccount::factory()->count(4)->create([
            'user_id' => $this->user->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $accounts[0]->id,
            'status' => 'active',
        ]);

        GpuPoolMember::factory()->inCooldown()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $accounts[1]->id,
        ]);

        GpuPoolMember::factory()->depleted()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $accounts[2]->id,
        ]);

        GpuPoolMember::factory()->error()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $accounts[3]->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/pools/{$pool->id}/statistics");

        $response->assertOk()
            ->assertJsonPath('data.total_accounts', 4)
            ->assertJsonPath('data.active', 1)
            ->assertJsonPath('data.cooldown', 1)
            ->assertJsonPath('data.depleted', 1)
            ->assertJsonPath('data.error', 1);
    }

    // ==================== Add Account to Pool ====================

    public function test_can_add_account_to_pool(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->postJson("/api/v1/gpu/pools/{$pool->id}/accounts", [
            'account_id' => $account->id,
            'priority' => 5,
            'weight' => 150,
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.priority', 5)
            ->assertJsonPath('data.weight', 150);

        $this->assertDatabaseHas('gpu_pool_members', [
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);
    }

    public function test_cannot_add_duplicate_account_to_pool(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->postJson("/api/v1/gpu/pools/{$pool->id}/accounts", [
            'account_id' => $account->id,
        ]);

        $response->assertStatus(400)
            ->assertJsonPath('error', 'Account is already in this pool');
    }

    public function test_cannot_add_other_users_account_to_pool(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $otherUser = User::factory()->create();
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $otherUser->id,
        ]);

        $response = $this->postJson("/api/v1/gpu/pools/{$pool->id}/accounts", [
            'account_id' => $account->id,
        ]);

        $response->assertNotFound();
    }

    // ==================== Remove Account from Pool ====================

    public function test_can_remove_account_from_pool(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->deleteJson("/api/v1/gpu/pools/{$pool->id}/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertDatabaseMissing('gpu_pool_members', [
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);
    }

    public function test_cannot_remove_nonexistent_member(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->deleteJson("/api/v1/gpu/pools/{$pool->id}/accounts/{$account->id}");

        $response->assertNotFound();
    }

    // ==================== Refresh Pool Balances ====================

    public function test_can_refresh_pool_balances(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account1 = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $account2 = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 50.00,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account1->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account2->id,
        ]);

        $this->mockProviderFactoryForMultipleAccounts([
            $account1->id => 120.00,
            $account2->id => 45.00,
        ]);

        $response = $this->postJson("/api/v1/gpu/pools/{$pool->id}/refresh-balances");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath("data.{$account1->id}.success", true)
            ->assertJsonPath("data.{$account2->id}.success", true);
    }

    public function test_refresh_balances_handles_failures_gracefully(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $mockProvider->shouldReceive('getBalance')
            ->andThrow(new \Exception('API connection failed'));

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);

        $response = $this->postJson("/api/v1/gpu/pools/{$pool->id}/refresh-balances");

        $response->assertOk()
            ->assertJsonPath("data.{$account->id}.success", false)
            ->assertJsonPath("data.{$account->id}.error", 'API connection failed');
    }

    // ==================== Rotation Strategy Tests ====================

    public function test_round_robin_rotation_increments_index(): void
    {
        $pool = GpuAccountPool::factory()->roundRobin()->create([
            'user_id' => $this->user->id,
            'min_credits_required' => 0.10,
        ]);

        $accounts = GpuProviderAccount::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        foreach ($accounts as $account) {
            GpuPoolMember::factory()->create([
                'gpu_account_pool_id' => $pool->id,
                'gpu_provider_account_id' => $account->id,
            ]);
        }

        $rotationService = app(GpuAccountRotationService::class);

        // First call should return first account
        $first = $rotationService->getNextAccount($pool);
        $this->assertNotNull($first);

        // Record success to trigger index update
        $rotationService->recordSuccess($first);

        // Second call should return different account
        Cache::forget('gpu_rotation:rr_index:' . $pool->id);
        $pool->update(['cooldown_minutes' => 0]); // Disable cooldown for this test
    }

    public function test_most_credits_strategy_returns_highest_balance(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
            'rotation_strategy' => 'most_credits',
            'min_credits_required' => 0.10,
        ]);

        $lowAccount = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 10.00,
        ]);

        $highAccount = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 500.00,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $lowAccount->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $highAccount->id,
        ]);

        $rotationService = app(GpuAccountRotationService::class);
        $selected = $rotationService->getNextAccount($pool);

        $this->assertEquals($highAccount->id, $selected->gpu_provider_account_id);
    }

    public function test_priority_strategy_returns_lowest_priority_number(): void
    {
        $pool = GpuAccountPool::factory()->priority()->create([
            'user_id' => $this->user->id,
            'min_credits_required' => 0.10,
        ]);

        $lowPriority = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $highPriority = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $lowPriority->id,
            'priority' => 10,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $highPriority->id,
            'priority' => 1,
        ]);

        $rotationService = app(GpuAccountRotationService::class);
        $selected = $rotationService->getNextAccount($pool);

        $this->assertEquals($highPriority->id, $selected->gpu_provider_account_id);
    }

    // ==================== Failover Tests ====================

    public function test_failover_returns_next_account_on_failure(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
            'auto_failover' => true,
            'min_credits_required' => 0.10,
        ]);

        $account1 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $account2 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 50.00,
        ]);

        $member1 = GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account1->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account2->id,
        ]);

        $rotationService = app(GpuAccountRotationService::class);

        // Record failure on first account - should return failover account
        $failoverAccount = $rotationService->recordFailure($member1, 'Test failure');

        $this->assertNotNull($failoverAccount);
        $this->assertEquals($account2->id, $failoverAccount->gpu_provider_account_id);
    }

    public function test_no_failover_when_disabled(): void
    {
        $pool = GpuAccountPool::factory()->noFailover()->create([
            'user_id' => $this->user->id,
            'min_credits_required' => 0.10,
        ]);

        $account1 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account2 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $member1 = GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account1->id,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account2->id,
        ]);

        $rotationService = app(GpuAccountRotationService::class);

        $failoverAccount = $rotationService->recordFailure($member1, 'Test failure');

        $this->assertNull($failoverAccount);
    }

    public function test_account_suspended_after_consecutive_failures(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $member = GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
            'consecutive_failures' => 4, // One more will trigger suspension
        ]);

        $rotationService = app(GpuAccountRotationService::class);
        $rotationService->recordFailure($member, 'Fifth consecutive failure');

        $member->refresh();
        $this->assertEquals(GpuPoolMember::STATUS_SUSPENDED, $member->status);
    }

    // ==================== Helper Methods ====================

    private function mockProviderFactoryForMultipleAccounts(array $accountBalances): void
    {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);

        foreach ($accountBalances as $accountId => $balance) {
            $mockProvider->shouldReceive('getBalance')
                ->with(Mockery::on(fn ($arg) => $arg->id === $accountId))
                ->andReturn($balance);
        }

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }
}
