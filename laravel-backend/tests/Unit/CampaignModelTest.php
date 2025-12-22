<?php

namespace Tests\Unit;

use App\Models\Campaign;
use App\Models\User;
use App\Models\Brand;
use App\Models\Post;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class CampaignModelTest extends TestCase
{
    use RefreshDatabase;

    private User $user;
    private Brand $brand;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
        $this->brand = Brand::factory()->create(['user_id' => $this->user->id]);
    }

    public function test_campaign_belongs_to_user(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(User::class, $campaign->user);
        $this->assertEquals($this->user->id, $campaign->user->id);
    }

    public function test_campaign_belongs_to_brand(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(Brand::class, $campaign->brand);
        $this->assertEquals($this->brand->id, $campaign->brand->id);
    }

    public function test_campaign_has_posts_relationship(): void
    {
        $socialAccount = \App\Models\SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'campaign_id' => $campaign->id,
            'social_account_id' => $socialAccount->id,
        ]);

        $this->assertCount(3, $campaign->posts);
        $this->assertInstanceOf(Post::class, $campaign->posts->first());
    }

    public function test_campaign_status_constants(): void
    {
        $this->assertEquals('draft', Campaign::STATUS_DRAFT);
        $this->assertEquals('scheduled', Campaign::STATUS_SCHEDULED);
        $this->assertEquals('active', Campaign::STATUS_ACTIVE);
        $this->assertEquals('paused', Campaign::STATUS_PAUSED);
        $this->assertEquals('completed', Campaign::STATUS_COMPLETED);
        $this->assertEquals('cancelled', Campaign::STATUS_CANCELLED);
    }

    public function test_campaign_is_active_helper(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => Campaign::STATUS_ACTIVE,
        ]);

        $this->assertTrue($campaign->isActive());

        $campaign->status = Campaign::STATUS_PAUSED;
        $this->assertFalse($campaign->isActive());
    }

    public function test_campaign_scope_active(): void
    {
        Campaign::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => Campaign::STATUS_ACTIVE,
        ]);

        Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => Campaign::STATUS_DRAFT,
        ]);

        $this->assertCount(2, Campaign::active()->get());
    }

    public function test_campaign_scope_for_platform(): void
    {
        Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'target_platforms' => ['facebook', 'instagram'],
        ]);

        Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'target_platforms' => ['twitter'],
        ]);

        $this->assertCount(1, Campaign::forPlatform('facebook')->get());
        $this->assertCount(1, Campaign::forPlatform('instagram')->get());
        $this->assertCount(1, Campaign::forPlatform('twitter')->get());
    }

    public function test_campaign_soft_deletes(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $campaign->delete();

        $this->assertSoftDeleted('campaigns', ['id' => $campaign->id]);
        $this->assertNull(Campaign::find($campaign->id));
        $this->assertNotNull(Campaign::withTrashed()->find($campaign->id));
    }

    public function test_campaign_fillable_attributes(): void
    {
        $campaign = Campaign::create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'name' => 'Test Campaign',
            'description' => 'Test Description',
            'type' => 'promotion',
            'goal' => 'Increase engagement',
            'target_platforms' => ['facebook', 'instagram'],
            'content_themes' => ['tech', 'innovation'],
            'budget' => 1000.00,
            'status' => Campaign::STATUS_DRAFT,
        ]);

        $this->assertEquals('Test Campaign', $campaign->name);
        $this->assertEquals('Test Description', $campaign->description);
        $this->assertEquals(['facebook', 'instagram'], $campaign->target_platforms);
        $this->assertEquals(1000.00, $campaign->budget);
    }

    public function test_campaign_casts_arrays(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'target_platforms' => ['facebook', 'twitter'],
            'content_themes' => ['tech'],
            'posting_schedule' => ['times' => ['09:00', '15:00']],
            'ai_settings' => ['tone' => 'professional'],
        ]);

        $this->assertIsArray($campaign->target_platforms);
        $this->assertIsArray($campaign->content_themes);
        $this->assertIsArray($campaign->posting_schedule);
        $this->assertIsArray($campaign->ai_settings);
    }

    public function test_campaign_casts_dates(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'start_date' => now(),
            'end_date' => now()->addMonth(),
        ]);

        $this->assertInstanceOf(\Carbon\Carbon::class, $campaign->start_date);
        $this->assertInstanceOf(\Carbon\Carbon::class, $campaign->end_date);
    }

    public function test_campaign_get_next_post_time(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'posting_schedule' => ['times' => ['09:00', '15:00']],
        ]);

        $nextTime = $campaign->getNextPostTime();

        $this->assertNotNull($nextTime);
        $this->assertInstanceOf(\DateTime::class, $nextTime);
    }

    public function test_campaign_get_next_post_time_returns_null_without_schedule(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'posting_schedule' => null,
        ]);

        $this->assertNull($campaign->getNextPostTime());
    }
}
