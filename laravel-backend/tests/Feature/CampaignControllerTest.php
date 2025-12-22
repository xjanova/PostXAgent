<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\Brand;
use App\Models\Campaign;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Tests\TestCase;

class CampaignControllerTest extends TestCase
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

    public function test_can_list_campaigns(): void
    {
        Campaign::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson('/api/v1/campaigns');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data' => [
                    'data' => [
                        '*' => ['id', 'name', 'status'],
                    ],
                ],
            ]);
    }

    public function test_can_create_campaign(): void
    {
        $data = [
            'brand_id' => $this->brand->id,
            'name' => 'New Campaign',
            'description' => 'Campaign description',
            'type' => 'promotion',
            'goal' => 'Increase engagement',
            'target_platforms' => ['facebook', 'instagram'],
            'content_themes' => ['tech', 'innovation'],
            'budget' => 5000,
            'status' => 'draft',
        ];

        $response = $this->postJson('/api/v1/campaigns', $data);

        $response->assertCreated()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'New Campaign');

        $this->assertDatabaseHas('campaigns', ['name' => 'New Campaign']);
    }

    public function test_can_show_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->getJson("/api/v1/campaigns/{$campaign->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.id', $campaign->id);
    }

    public function test_cannot_show_other_users_campaign(): void
    {
        $otherUser = User::factory()->create();
        $otherBrand = Brand::factory()->create(['user_id' => $otherUser->id]);
        $campaign = Campaign::factory()->create([
            'user_id' => $otherUser->id,
            'brand_id' => $otherBrand->id,
        ]);

        $response = $this->getJson("/api/v1/campaigns/{$campaign->id}");

        $response->assertForbidden();
    }

    public function test_can_update_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'name' => 'Original Name',
        ]);

        $response = $this->putJson("/api/v1/campaigns/{$campaign->id}", [
            'name' => 'Updated Name',
            'budget' => 10000,
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.name', 'Updated Name');

        $this->assertDatabaseHas('campaigns', [
            'id' => $campaign->id,
            'name' => 'Updated Name',
        ]);
    }

    public function test_can_delete_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->deleteJson("/api/v1/campaigns/{$campaign->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertSoftDeleted('campaigns', ['id' => $campaign->id]);
    }

    public function test_can_filter_campaigns_by_status(): void
    {
        Campaign::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'active',
        ]);

        Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'draft',
        ]);

        $response = $this->getJson('/api/v1/campaigns?status=active');

        $response->assertOk();
        $data = $response->json('data.data');
        $this->assertCount(2, $data);
    }

    public function test_can_filter_campaigns_by_brand(): void
    {
        $otherBrand = Brand::factory()->create(['user_id' => $this->user->id]);

        Campaign::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        Campaign::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $otherBrand->id,
        ]);

        $response = $this->getJson("/api/v1/campaigns?brand_id={$this->brand->id}");

        $response->assertOk();
        $data = $response->json('data.data');
        $this->assertCount(3, $data);
    }

    public function test_can_start_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'draft',
        ]);

        $response = $this->postJson("/api/v1/campaigns/{$campaign->id}/start");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $campaign->refresh();
        $this->assertEquals('active', $campaign->status);
    }

    public function test_can_pause_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'active',
        ]);

        $response = $this->postJson("/api/v1/campaigns/{$campaign->id}/pause");

        $response->assertOk()
            ->assertJsonPath('success', true);

        $campaign->refresh();
        $this->assertEquals('paused', $campaign->status);
    }

    public function test_validation_requires_name(): void
    {
        $response = $this->postJson('/api/v1/campaigns', [
            'brand_id' => $this->brand->id,
            'status' => 'draft',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['name']);
    }

    public function test_validation_requires_brand_id(): void
    {
        $response = $this->postJson('/api/v1/campaigns', [
            'name' => 'Test Campaign',
            'status' => 'draft',
        ]);

        $response->assertUnprocessable()
            ->assertJsonValidationErrors(['brand_id']);
    }
}
