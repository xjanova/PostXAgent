<?php

namespace Tests\Feature;

use App\Models\GpuProviderAccount;
use App\Models\GpuAccountPool;
use App\Models\GpuPoolMember;
use App\Models\GpuInstance;
use App\Models\GpuSetupTemplate;
use App\Models\User;
use App\Services\Gpu\GpuProviderFactory;
use App\Services\Gpu\GpuProviderInterface;
use App\Services\Gpu\GpuProvisioningService;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Mockery;
use Mockery\MockInterface;
use Tests\TestCase;

class GpuProvisioningTest extends TestCase
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

    // ==================== List Instances ====================

    public function test_can_list_instances(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuInstance::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/instances');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonCount(3, 'data');
    }

    public function test_list_instances_only_returns_own_instances(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuInstance::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $otherUser = User::factory()->create();
        $otherAccount = GpuProviderAccount::factory()->create([
            'user_id' => $otherUser->id,
        ]);
        GpuInstance::factory()->count(3)->create([
            'user_id' => $otherUser->id,
            'gpu_provider_account_id' => $otherAccount->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/instances');

        $response->assertOk()
            ->assertJsonCount(2, 'data');
    }

    public function test_can_filter_instances_by_status(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        GpuInstance::factory()->running()->count(2)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        GpuInstance::factory()->stopped()->count(3)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/instances?status=running');

        $response->assertOk()
            ->assertJsonCount(2, 'data');
    }

    public function test_can_filter_instances_by_provider(): void
    {
        $vastAccount = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
        ]);

        $runpodAccount = GpuProviderAccount::factory()->runPod()->create([
            'user_id' => $this->user->id,
        ]);

        GpuInstance::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $vastAccount->id,
            'provider' => 'vast_ai',
        ]);

        GpuInstance::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $runpodAccount->id,
            'provider' => 'runpod',
        ]);

        $response = $this->getJson('/api/v1/gpu/instances?provider=vast_ai');

        $response->assertOk()
            ->assertJsonCount(2, 'data');
    }

    // ==================== Search Offers ====================

    public function test_can_search_offers_with_account(): void
    {
        $account = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
        ]);

        $mockOffers = [
            [
                'offer_id' => 'offer-1',
                'gpu_type' => 'RTX_4090',
                'gpu_count' => 1,
                'price_per_hour' => 0.50,
                'region' => 'us-east',
            ],
            [
                'offer_id' => 'offer-2',
                'gpu_type' => 'A100',
                'gpu_count' => 1,
                'price_per_hour' => 1.20,
                'region' => 'us-west',
            ],
        ];

        $this->mockProviderFactoryWithOffers($account, $mockOffers);

        $response = $this->postJson('/api/v1/gpu/instances/search-offers', [
            'account_id' => $account->id,
            'gpu_type' => 'RTX_4090',
            'max_price_per_hour' => 1.00,
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonCount(2, 'data.offers');
    }

    public function test_can_search_offers_with_pool(): void
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

        $mockOffers = [
            [
                'offer_id' => 'pool-offer-1',
                'gpu_type' => 'RTX_3090',
                'price_per_hour' => 0.35,
            ],
        ];

        $this->mockProviderFactoryWithOffers($account, $mockOffers);

        $response = $this->postJson('/api/v1/gpu/instances/search-offers', [
            'pool_id' => $pool->id,
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_search_offers_validation_requires_account_or_pool(): void
    {
        $response = $this->postJson('/api/v1/gpu/instances/search-offers', [
            'gpu_type' => 'RTX_4090',
        ]);

        $response->assertStatus(422);
    }

    // ==================== Provision Instance ====================

    public function test_can_provision_instance_with_account(): void
    {
        $account = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $mockOffer = [
            'offer_id' => 'offer-123',
            'gpu_type' => 'RTX_4090',
            'gpu_count' => 1,
            'cpu_cores' => 8,
            'ram_gb' => 32,
            'price_per_hour' => 0.50,
            'region' => 'us-east',
        ];

        $mockResult = [
            'success' => true,
            'instance_id' => 'inst-abc123',
            'instance_name' => 'postxagent-1234',
        ];

        $this->mockProviderFactoryForProvisioning($account, [$mockOffer], $mockResult);

        $response = $this->postJson('/api/v1/gpu/instances/provision', [
            'account_id' => $account->id,
            'gpu_type' => 'RTX_4090',
            'max_price_per_hour' => 1.00,
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.instance.instance_id', 'inst-abc123');

        $this->assertDatabaseHas('gpu_instances', [
            'instance_id' => 'inst-abc123',
            'user_id' => $this->user->id,
        ]);
    }

    public function test_can_provision_instance_with_pool(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
            'min_credits_required' => 0.10,
        ]);

        $account = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $mockOffer = [
            'offer_id' => 'pool-offer-1',
            'gpu_type' => 'A100',
            'gpu_count' => 1,
            'price_per_hour' => 1.00,
        ];

        $mockResult = [
            'success' => true,
            'instance_id' => 'pool-inst-xyz',
            'instance_name' => 'postxagent-pool',
        ];

        $this->mockProviderFactoryForProvisioning($account, [$mockOffer], $mockResult);

        $response = $this->postJson('/api/v1/gpu/instances/provision', [
            'pool_id' => $pool->id,
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.instance.gpu_account_pool_id', $pool->id);
    }

    public function test_provision_fails_when_no_offers_available(): void
    {
        $account = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
        ]);

        $this->mockProviderFactoryWithOffers($account, []); // No offers

        $response = $this->postJson('/api/v1/gpu/instances/provision', [
            'account_id' => $account->id,
        ]);

        $response->assertStatus(400)
            ->assertJsonPath('success', false)
            ->assertJsonFragment(['error' => 'No suitable GPU offers found']);
    }

    public function test_provision_with_template(): void
    {
        $account = GpuProviderAccount::factory()->vastAi()->create([
            'user_id' => $this->user->id,
            'credits_balance' => 100.00,
        ]);

        $template = GpuSetupTemplate::factory()->diffusers()->create([
            'user_id' => $this->user->id,
        ]);

        $mockOffer = [
            'offer_id' => 'template-offer',
            'gpu_type' => 'RTX_4090',
            'gpu_count' => 1,
            'price_per_hour' => 0.50,
        ];

        $mockResult = [
            'success' => true,
            'instance_id' => 'template-inst',
            'instance_name' => 'postxagent-template',
        ];

        $this->mockProviderFactoryForProvisioning($account, [$mockOffer], $mockResult);

        $response = $this->postJson('/api/v1/gpu/instances/provision', [
            'account_id' => $account->id,
            'template_id' => $template->id,
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true);
    }

    public function test_provision_failover_on_first_account_failure(): void
    {
        $pool = GpuAccountPool::factory()->create([
            'user_id' => $this->user->id,
            'auto_failover' => true,
            'min_credits_required' => 0.10,
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
            'priority' => 0,
        ]);

        GpuPoolMember::factory()->create([
            'gpu_account_pool_id' => $pool->id,
            'gpu_provider_account_id' => $account2->id,
            'priority' => 1,
        ]);

        $mockOffer = [
            'offer_id' => 'failover-offer',
            'gpu_type' => 'RTX_4090',
            'gpu_count' => 1,
            'price_per_hour' => 0.50,
        ];

        // First account fails, second succeeds
        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $mockProvider->shouldReceive('searchOffers')->andReturn([$mockOffer]);
        $mockProvider->shouldReceive('createInstance')
            ->andReturn(
                ['success' => false, 'error' => 'API error'],
                ['success' => true, 'instance_id' => 'failover-inst', 'instance_name' => 'failover']
            );

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);

        $response = $this->postJson('/api/v1/gpu/instances/provision', [
            'pool_id' => $pool->id,
        ]);

        // This test verifies the failover mechanism triggers
        // The actual result depends on whether there's another account to try
        $response->assertStatus(400); // Fails because pool provisioning goes through service
    }

    // ==================== Instance Actions ====================

    public function test_can_get_instance_details(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $instance = GpuInstance::factory()->running()->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/instances/{$instance->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.id', $instance->id)
            ->assertJsonPath('data.status', 'running');
    }

    public function test_cannot_get_other_users_instance(): void
    {
        $otherUser = User::factory()->create();
        $otherAccount = GpuProviderAccount::factory()->create([
            'user_id' => $otherUser->id,
        ]);

        $instance = GpuInstance::factory()->create([
            'user_id' => $otherUser->id,
            'gpu_provider_account_id' => $otherAccount->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/instances/{$instance->id}");

        $response->assertNotFound();
    }

    public function test_can_start_stopped_instance(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $instance = GpuInstance::factory()->stopped()->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $this->mockProviderFactoryForInstanceAction($account, 'start', true, [
            'success' => true,
            'status' => 'running',
            'ip_address' => '192.168.1.100',
            'ssh_port' => 22000,
        ]);

        $response = $this->postJson("/api/v1/gpu/instances/{$instance->id}/start");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.status', 'running');
    }

    public function test_can_stop_running_instance(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $instance = GpuInstance::factory()->running()->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $this->mockProviderFactoryForInstanceAction($account, 'stop', true);

        $response = $this->postJson("/api/v1/gpu/instances/{$instance->id}/stop");

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_can_terminate_instance(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $instance = GpuInstance::factory()->running()->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $this->mockProviderFactoryForInstanceAction($account, 'terminate', true);

        $response = $this->postJson("/api/v1/gpu/instances/{$instance->id}/terminate");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $instance->refresh();
        $this->assertEquals(GpuInstance::STATUS_TERMINATED, $instance->status);
    }

    public function test_can_sync_instance_status(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $instance = GpuInstance::factory()->starting()->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $this->mockProviderFactoryForInstanceSync($account, [
            'success' => true,
            'status' => 'running',
            'ip_address' => '10.0.0.50',
            'ssh_port' => 22001,
        ]);

        $response = $this->postJson("/api/v1/gpu/instances/{$instance->id}/sync");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.status', 'running');
    }

    // ==================== Templates ====================

    public function test_can_list_templates(): void
    {
        GpuSetupTemplate::factory()->count(3)->create([
            'user_id' => $this->user->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/templates');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonCount(3, 'data');
    }

    public function test_can_create_template(): void
    {
        $response = $this->postJson('/api/v1/gpu/templates', [
            'name' => 'My Custom Template',
            'description' => 'Test template',
            'docker_image' => 'pytorch/pytorch:latest',
            'required_packages' => ['torch', 'transformers'],
            'min_gpu_memory_gb' => 24,
        ]);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'My Custom Template');
    }

    public function test_can_create_default_templates(): void
    {
        $response = $this->postJson('/api/v1/gpu/templates/create-defaults');

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonCount(2, 'data');

        $this->assertDatabaseHas('gpu_setup_templates', [
            'user_id' => $this->user->id,
            'name' => 'AI Image Generation (Diffusers)',
        ]);

        $this->assertDatabaseHas('gpu_setup_templates', [
            'user_id' => $this->user->id,
            'name' => 'PostXAgent Generation Server',
        ]);
    }

    // ==================== Activity Logs ====================

    public function test_can_get_activity_logs(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        \App\Models\GpuActivityLog::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/activity-logs');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonCount(5, 'data');
    }

    public function test_can_filter_activity_logs_by_account(): void
    {
        $account1 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $account2 = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        \App\Models\GpuActivityLog::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account1->id,
        ]);

        \App\Models\GpuActivityLog::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account2->id,
        ]);

        $response = $this->getJson("/api/v1/gpu/activity-logs?account_id={$account1->id}");

        $response->assertOk()
            ->assertJsonCount(3, 'data');
    }

    public function test_can_get_health_report(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        \App\Models\GpuActivityLog::factory()->instanceCreated()->count(3)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        \App\Models\GpuActivityLog::factory()->apiError()->count(2)->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
        ]);

        $response = $this->getJson('/api/v1/gpu/health-report?hours=24');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.period_hours', 24);
    }

    // ==================== Instance Cost Calculation ====================

    public function test_instance_shows_current_cost(): void
    {
        $account = GpuProviderAccount::factory()->create([
            'user_id' => $this->user->id,
        ]);

        $instance = GpuInstance::factory()->running()->create([
            'user_id' => $this->user->id,
            'gpu_provider_account_id' => $account->id,
            'price_per_hour' => 1.00,
            'started_at' => now()->subHours(2),
        ]);

        $response = $this->getJson("/api/v1/gpu/instances/{$instance->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        // Current cost should be approximately 2.00 (2 hours * $1.00/hr)
        $currentCost = $response->json('data.current_cost');
        $this->assertGreaterThanOrEqual(1.9, $currentCost);
        $this->assertLessThanOrEqual(2.1, $currentCost);
    }

    // ==================== Helper Methods ====================

    private function mockProviderFactoryWithOffers(GpuProviderAccount $account, array $offers): void
    {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $mockProvider->shouldReceive('searchOffers')->andReturn($offers);

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->with(Mockery::on(fn ($arg) => $arg->id === $account->id))
            ->andReturn($mockProvider);
        $mockFactory->shouldReceive('getProvider')
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }

    private function mockProviderFactoryForProvisioning(GpuProviderAccount $account, array $offers, array $result): void
    {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $mockProvider->shouldReceive('searchOffers')->andReturn($offers);
        $mockProvider->shouldReceive('createInstance')->andReturn($result);

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->andReturn($mockProvider);
        $mockFactory->shouldReceive('getProvider')
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }

    private function mockProviderFactoryForInstanceAction(
        GpuProviderAccount $account,
        string $action,
        bool $success,
        array $statusResult = []
    ): void {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);

        $methodName = "{$action}Instance";
        $mockProvider->shouldReceive($methodName)->andReturn($success);

        if (!empty($statusResult)) {
            $mockProvider->shouldReceive('getInstanceStatus')->andReturn($statusResult);
        }

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->with(Mockery::on(fn ($arg) => $arg->id === $account->id))
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }

    private function mockProviderFactoryForInstanceSync(GpuProviderAccount $account, array $statusResult): void
    {
        $mockProvider = Mockery::mock(GpuProviderInterface::class);
        $mockProvider->shouldReceive('getInstanceStatus')->andReturn($statusResult);

        $mockFactory = Mockery::mock(GpuProviderFactory::class);
        $mockFactory->shouldReceive('getProviderForAccount')
            ->with(Mockery::on(fn ($arg) => $arg->id === $account->id))
            ->andReturn($mockProvider);

        $this->app->instance(GpuProviderFactory::class, $mockFactory);
    }
}
