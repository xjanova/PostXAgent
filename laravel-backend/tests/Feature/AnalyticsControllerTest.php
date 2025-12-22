<?php

namespace Tests\Feature;

use App\Models\User;
use App\Models\Brand;
use App\Models\Post;
use App\Models\Campaign;
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

    public function test_can_get_dashboard_analytics(): void
    {
        Post::factory()->count(10)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'metrics' => [
                'likes' => 100,
                'comments' => 20,
                'shares' => 10,
            ],
        ]);

        $response = $this->getJson('/api/v1/analytics/dashboard');

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'total_posts',
                    'total_engagement',
                    'posts_by_platform',
                    'engagement_by_platform',
                ],
            ]);
    }

    public function test_can_get_brand_analytics(): void
    {
        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
        ]);

        $response = $this->getJson("/api/v1/analytics/brands/{$this->brand->id}");

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'posts_count',
                    'total_engagement',
                    'engagement_rate',
                ],
            ]);
    }

    public function test_can_get_post_analytics(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'metrics' => [
                'likes' => 150,
                'comments' => 30,
                'shares' => 15,
                'views' => 1000,
            ],
        ]);

        $response = $this->getJson("/api/v1/analytics/posts/{$post->id}");

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'post_id',
                    'metrics',
                    'engagement_rate',
                ],
            ]);
    }

    public function test_can_get_campaign_analytics(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'campaign_id' => $campaign->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
        ]);

        $response = $this->getJson("/api/v1/analytics/campaigns/{$campaign->id}");

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'campaign_id',
                    'posts_count',
                    'total_engagement',
                ],
            ]);
    }

    public function test_can_get_platform_analytics(): void
    {
        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'platform' => 'facebook',
            'status' => Post::STATUS_PUBLISHED,
        ]);

        $response = $this->getJson('/api/v1/analytics/platforms/facebook');

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'platform',
                    'posts_count',
                    'total_engagement',
                ],
            ]);
    }

    public function test_can_get_analytics_by_date_range(): void
    {
        Post::factory()->count(10)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'published_at' => now()->subDays(5),
        ]);

        $response = $this->getJson('/api/v1/analytics/dashboard?start_date=' . now()->subWeek()->format('Y-m-d') . '&end_date=' . now()->format('Y-m-d'));

        $response->assertOk()
            ->assertJsonPath('success', true);
    }

    public function test_can_get_engagement_trends(): void
    {
        Post::factory()->count(20)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
        ]);

        $response = $this->getJson('/api/v1/analytics/trends?period=weekly');

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    'trends',
                ],
            ]);
    }

    public function test_can_get_top_performing_posts(): void
    {
        Post::factory()->count(20)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'metrics' => [
                'likes' => rand(10, 500),
                'comments' => rand(1, 50),
                'shares' => rand(0, 20),
            ],
        ]);

        $response = $this->getJson('/api/v1/analytics/top-posts?limit=10');

        $response->assertOk()
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'metrics'],
                ],
            ]);
    }

    public function test_can_export_analytics_csv(): void
    {
        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
        ]);

        $response = $this->getJson('/api/v1/analytics/export?format=csv');

        $response->assertOk()
            ->assertHeader('Content-Type', 'text/csv; charset=UTF-8');
    }

    public function test_cannot_access_other_users_brand_analytics(): void
    {
        $otherUser = User::factory()->create();
        $otherBrand = Brand::factory()->create(['user_id' => $otherUser->id]);

        $response = $this->getJson("/api/v1/analytics/brands/{$otherBrand->id}");

        $response->assertForbidden();
    }

    public function test_can_get_viral_posts(): void
    {
        Post::factory()->count(5)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'is_viral' => true,
            'viral_score' => 85.5,
        ]);

        Post::factory()->count(10)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
            'is_viral' => false,
        ]);

        $response = $this->getJson('/api/v1/analytics/viral-posts');

        $response->assertOk();
        $data = $response->json('data');
        $this->assertCount(5, $data);
    }
}
