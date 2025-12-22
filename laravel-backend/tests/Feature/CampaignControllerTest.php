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
            ->assertJsonPath('success', true);
    }

    public function test_can_create_campaign(): void
    {
        $response = $this->postJson('/api/v1/campaigns', [
            'brand_id' => $this->brand->id,
            'name' => 'New Campaign',
            'type' => 'promotion',
            'target_platforms' => ['facebook'],
            'start_date' => now()->addDay()->toDateString(),
            'end_date' => now()->addMonth()->toDateString(),
        ]);

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

    public function test_can_update_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->putJson("/api/v1/campaigns/{$campaign->id}", [
            'name' => 'Updated Name',
        ]);

        $response->assertOk()
            ->assertJsonPath('success', true);

        $this->assertDatabaseHas('campaigns', ['id' => $campaign->id, 'name' => 'Updated Name']);
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

        $this->assertDatabaseHas('campaigns', ['id' => $campaign->id, 'status' => 'active']);
    }

    public function test_cannot_access_other_users_campaign(): void
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
}
