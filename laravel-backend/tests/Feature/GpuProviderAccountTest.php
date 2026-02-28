<?php

namespace Tests\Feature;

use App\Models\GpuProviderAccount;
use App\Models\GpuInstance;
use App\Models\User;
use App\Services\Gpu\GpuProviderFactory;
use App\Services\Gpu\GpuProviderInterface;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Mockery;
use Mockery\MockInterface;
use Tests\TestCase;

class GpuProviderAccountTest extends TestCase
{
    use RefreshDatabase;

    private User $user;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
        Sanctum::actingAs($this->user);
    }

    protected function tearDown(): void
    {
        Mockery::close();
        parent::tearDown();
    }

    // ==================== List Accounts ====================

    public function test_can_list_gpu_accounts(): void
    {
        GpuProviderAccount::factory()->count(3)->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/accounts');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonCount(3, 'data');
    }

    public function test_list_accounts_only_returns_own_accounts(): void
    {
        GpuProviderAccount::factory()->count(2)->create([
            'user_id' => $this->user->id,
        ]);

        $otherUser = User::factory()->create();
        GpuProviderAccount::factory()->count(3)->create([
            'user_id' => $otherUser->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/accounts');

        $response->assertOk()
            ->assertJsonCount(2, 'data');
    }

    // ==================== Create Account ====================

    public function test_can_create_gpu_account_with_valid_credentials(): void
    {
        $this->mockProviderFactory(function (MockInterface $provider) {
            $provider->shouldReceive('validateCredentials')
                ->once()
                ->andReturn(true);
            $provider->shouldReceive('getBalance')
                ->once()
                ->andReturn(100.50);
        });

        $response = $this->postJson('/api/v1/gpu/accounts', [
            'provider' => 'vast_ai',
            'name' => 'My Vast.ai Account',
            'email' => 'test@example.com',
            'api_key' => 'valid-api-key-123',
            'daily_credit_limit' => 50.00,
            'low_balance_threshold' => 5.00,
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'My Vast.ai Account')
            ->assertJsonPath('data.provider', 'vast_ai')
            ->assertJsonPath('data.credits_balance', '100.5000');

        $this->assertDatabaseHas('gpu_provider_accounts', [
            'user_id' => $this->user->id,
            'name' => 'My Vast.ai Account',
            'provider' => 'vast_ai',
        ]);
    }

    public function test_cannot_create_account_with_invalid_credentials(): void
    {
        $this->mockProviderFactory(function (MockInterface $provider) {
            $provider->shouldReceive('validateCredentials')
                ->once()
                ->andReturn(false);
        });

        $response = $this->postJson('/api/v1/gpu/accounts', [
            'provider' => 'vast_ai',
            'name' => 'Test Account',
            'api_key' => 'invalid-api-key',
        ]);

        $response->assertStatus(400)
            ->assertJsonPath('success', false)
            ->assertJsonPath('error', 'Invalid API credentials. Please check and try again.');
    }

    public function test_create_account_validation_errors(): void
    {
        $response = $this->postJson('/api/v1/gpu/accounts', [
            'provider' => 'invalid_provider',
            'name' => '',
        ]);

        $response->assertStatus(422)
            ->assertJsonPath('success', false)
            ->assertJsonStructure(['errors' => ['provider', 'name', 'api_key']]);
    }

    public function test_create_account_with_runpod_provider(): void
    {
        $this->mockProviderFactory(function (MockInterface $provider) {
            $provider->shouldReceive('validateCredentials')
                ->once()
                ->andReturn(true);
            $provider->shouldReceive('getBalance')
                ->once()
                ->andReturn(200.00);
        }, 'runpod');

        $response = $this->postJson('/api/v1/gpu/accounts', [
            'provider' => 'runpod',
            'name' => 'RunPod Account',
            'api_key' => 'runpod-api-key',
        ]);

        $response->assertCreated()
            ->assertJsonPath('data.provider', 'runpod');
    }

    // ==================== Get Account ====================

    public function test_can_get_own_account(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.id', $account->id);
    }

    public function test_cannot_get_other_users_account(): void
    {
        $otherUser = User::factory()->create();
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $otherUser->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertNotFound();
    }

    // ==================== Update Account ====================

    public function test_can_update_account(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'name' => 'Original Name',
        ]);

        $response = $this->putJson("/api/v1/gpu/accounts/{$account->id}", [
            'name' => 'Updated Name',
            'daily_credit_limit' => 100.00,
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'Updated Name');

        $this->assertDatabaseHas('gpu_provider_accounts', [
            'id' => $account->id,
            'name' => 'Updated Name',
        ]);
    }

    public function test_can_deactivate_account(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'is_active' => true,
        ]);

        $response = $this->putJson("/api/v1/gpu/accounts/{$account->id}", [
            'is_active' => false,
        ]);

        $response->assertOk()
            ->assertJsonPath('data.is_active', false);
    }

    public function test_update_api_key_validates_new_credentials(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $this->mockProviderFactoryForAccount($account, function (MockInterface $provider) {
            $provider->shouldReceive('validateCredentials')
                ->once()
                ->andReturn(true);
        });

        $response = $this->putJson("/api/v1/gpu/accounts/{$account->id}", [
            'api_key' => 'new-api-key',
        ]);

        $response->assertOk();
    }

    public function test_cannot_update_other_users_account(): void
    {
        $otherUser = User::factory()->create();
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $otherUser->id,
        ]);

        $response = $this->putJson("/api/v1/gpu/accounts/{$account->id}", [
            'name' => 'Hacked Name',
        ]);

        $response->assertNotFound();
    }

    // ==================== Delete Account ====================

    public function test_can_delete_account_without_instances(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->deleteJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertSoftDeleted('gpu_provider_accounts', ['id' => $account->id]);
    }

    public function test_cannot_delete_account_with_active_instances(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuInstance::factory()->running()->create([
            'gpu_provider_account_id' => $account->id,
            'user_id' => $this->user->id,
        ]);

        $response = $this->deleteJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertStatus(400)
            ->assertJsonPath('success', false)
            ->assertJsonFragment(['error' => 'Cannot delete account with 1 active instances. Terminate them first.']);
    }

    public function test_cannot_delete_other_users_account(): void
    {
        $otherUser = User::factory()->create();
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $otherUser->id,
        ]);

        $response = $this->deleteJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertNotFound();
    }

    // ==================== Refresh Balance ====================

    public function test_can_refresh_account_balance(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 50.00,
        ]);

        $this->mockProviderFactoryForAccount($account, function (MockInterface $provider) {
            $provider->shouldReceive('getBalance')
                ->once()
                ->andReturn(75.00);
        });

        $response = $this->postJson("/api/v1/gpu/accounts/{$account->id}/refresh-balance");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.old_balance', '50.0000')
            ->assertJsonPath('data.new_balance', 75.00)
            ->assertJsonPath('data.change', 25.00);

        $this->assertDatabaseHas('gpu_provider_accounts', [
            'id' => $account->id,
            'credits_balance' => 75.00,
        ]);
    }

    public function test_refresh_balance_creates_activity_log(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $this->mockProviderFactoryForAccount($account, function (MockInterface $provider) {
            $provider->shouldReceive('getBalance')
                ->once()
                ->andReturn(95.00);
        });

        $this->postJson("/api/v1/gpu/accounts/{$account->id}/refresh-balance");

        $this->assertDatabaseHas('gpu_activity_logs', [
            'gpu_provider_account_id' => $account->id,
            'event_type' => 'balance_checked',
        ]);
    }

    // ==================== Account Status Tests ====================

    public function test_account_status_shows_in_response(): void
    {
        $account = GpuProviderAccount::factory()->inCooldown()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('data.status', 'cooldown');
    }

    public function test_depleted_account_shows_correct_status(): void
    {
        $account = GpuProviderAccount::factory()->depleted()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('data.status', 'depleted')
            ->assertJsonPath('data.credits_balance', '0.0000');
    }

    public function test_low_balance_indicator_in_response(): void
    {
        $account = GpuProviderAccount::factory()->lowBalance()->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/accounts/{$account->id}");

        $response->assertOk()
            ->assertJsonPath('data.is_low_balance', true);
    }

    // ==================== Helper Methods ====================

    private function mockProviderFactory(callable $setup, string $provider = 'vast_ai'): void
    {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $setup($mockProvider);

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProvider')
            ->with($provider)
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }

    private function mockProviderFactoryForAccount(GpuProviderAccount $account, callable $setup): void
    {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $setup($mockProvider);

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->with(Mockery::on(fn ($arg) => $arg->id === $account->id))
            ->andReturn($mockProvider);
        $mockFactory->shouldReceive('getProvider')
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }
}
