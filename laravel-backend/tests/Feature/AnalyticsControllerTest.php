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
    private SocialAccount $socialAccount;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
        $this->brand = Brand::factory()->create(['user_id' => $this->user->id]);
        $this->socialAccount = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);
        Sanctum::actingAs($this->user);
    }

    public function test_can_get_analytics_overview(): void
    {
        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDays(5),
            'metrics' => [
                'likes' => 100,
                'comments' => 20,
                'shares' => 10,
                'views' => 1000,
                'engagement_rate' => 13.0,
            ],
        ]);

        $response = $this->getJson('/api/v1/analytics/overview');

        // AnalyticsController::overview returns {success, data: {...}}
        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data' => [
                    'period',
                    'summary' => [
                        'total_posts',
                        'published_posts',
                        'posts_in_period',
                    ],
                    'engagement' => [
                        'total_likes',
                        'total_comments',
                        'total_shares',
                        'total_views',
                    ],
                    'posts_by_status',
                ],
            ]);
    }

    public function test_can_get_analytics_overview_with_days_parameter(): void
    {
        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDays(5),
        ]);

        $response = $this->getJson('/api/v1/analytics/overview?days=7');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.period.days', 7);
    }

    public function test_can_get_posts_analytics(): void
    {
        Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDays(2),
            'metrics' => [
                'likes' => 50,
                'comments' => 10,
            ],
        ]);

        $response = $this->getJson('/api/v1/analytics/posts');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data' => [
                    'daily_posts',
                    'top_posts',
                ],
            ]);
    }

    public function test_can_get_engagement_analytics(): void
    {
        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDay(),
            'content_type' => 'image',
            'metrics' => [
                'likes' => 100,
                'comments' => 20,
                'shares' => 10,
                'views' => 500,
                'engagement_rate' => 26.0,
            ],
        ]);

        $response = $this->getJson('/api/v1/analytics/engagement');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data' => [
                    'daily_engagement',
                    'by_content_type',
                    'best_hours',
                ],
            ]);
    }

    public function test_can_get_platform_analytics(): void
    {
        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'platform' => 'facebook',
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDay(),
            'metrics' => ['likes' => 100, 'comments' => 20, 'shares' => 10],
        ]);

        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'platform' => 'instagram',
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDay(),
            'metrics' => ['likes' => 200, 'comments' => 30, 'shares' => 15],
        ]);

        $response = $this->getJson('/api/v1/analytics/platforms');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data',
            ]);

        // Should have platform breakdown data
        $this->assertNotEmpty($response->json('data'));
    }

    public function test_can_get_brand_analytics(): void
    {
        Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDays(3),
            'metrics' => [
                'likes' => 75,
                'comments' => 15,
                'shares' => 8,
                'views' => 800,
            ],
        ]);

        $response = $this->getJson("/api/v1/analytics/brands/{$this->brand->id}");

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonStructure([
                'success',
                'data' => [
                    'brand',
                    'period_days',
                    'overview' => [
                        'total_posts',
                        'total_likes',
                        'total_comments',
                        'total_shares',
                    ],
                    'campaigns',
                    'platforms',
                ],
            ]);
    }

    public function test_cannot_access_other_users_brand_analytics(): void
    {
        $otherUser = User::factory()->create();
        $otherBrand = Brand::factory()->create(['user_id' => $otherUser->id]);

        $response = $this->getJson("/api/v1/analytics/brands/{$otherBrand->id}");

        $response->assertForbidden()
            ->assertJsonPath('success', false);
    }

    public function test_analytics_overview_returns_empty_data_for_new_user(): void
    {
        // No posts created for this user
        $newUser = User::factory()->create();
        Sanctum::actingAs($newUser);

        $response = $this->getJson('/api/v1/analytics/overview');

        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('data.summary.total_posts', 0);
    }
}
