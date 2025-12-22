<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\Brand;
use App\Models\Post;
use App\Models\SocialAccount;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Laravel\Sanctum\Sanctum;
use Tests\TestCase;

class AnalyticsControllerTest extends TestCase
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

    public function test_can_get_analytics_overview(): void
    {
        $response = $this->getJson('/api/v1/analytics/overview');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure(['data' => ['period', 'summary', 'engagement']]);
    }

    public function test_can_get_posts_analytics(): void
    {
        $response = $this->getJson('/api/v1/analytics/posts');

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_can_get_engagement_analytics(): void
    {
        $response = $this->getJson('/api/v1/analytics/engagement');

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_can_get_platforms_analytics(): void
    {
        $response = $this->getJson('/api/v1/analytics/platforms');

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_can_get_brand_analytics(): void
    {
        $response = $this->getJson("/api/v1/analytics/brands/{$this->brand->id}");

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_cannot_access_other_users_brand_analytics(): void
    {
        $otherUser = User::factory()->create();
        $otherBrand = Brand::factory()->create(['user_id' => $otherUser->id]);

        $response = $this->getJson("/api/v1/analytics/brands/{$otherBrand->id}");

        $response->assertForbidden();
    }
}
