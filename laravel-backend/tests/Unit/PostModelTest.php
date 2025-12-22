<?php

namespace Tests\Unit;

use App\Models\Post;
use App\Models\User;
use App\Models\Brand;
use App\Models\Campaign;
use App\Models\SocialAccount;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class PostModelTest extends TestCase
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
    }

    public function test_post_belongs_to_user(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
        ]);

        $this->assertInstanceOf(User::class, $post->user);
        $this->assertEquals($this->user->id, $post->user->id);
    }

    public function test_post_belongs_to_brand(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
        ]);

        $this->assertInstanceOf(Brand::class, $post->brand);
        $this->assertEquals($this->brand->id, $post->brand->id);
    }

    public function test_post_belongs_to_social_account(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
        ]);

        $this->assertInstanceOf(SocialAccount::class, $post->socialAccount);
        $this->assertEquals($this->socialAccount->id, $post->socialAccount->id);
    }

    public function test_post_belongs_to_campaign(): void
    {
        $campaign = Campaign::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'campaign_id' => $campaign->id,
            'social_account_id' => $this->socialAccount->id,
        ]);

        $this->assertInstanceOf(Campaign::class, $post->campaign);
        $this->assertEquals($campaign->id, $post->campaign->id);
    }

    public function test_post_status_constants(): void
    {
        $this->assertEquals('draft', Post::STATUS_DRAFT);
        $this->assertEquals('pending', Post::STATUS_PENDING);
        $this->assertEquals('scheduled', Post::STATUS_SCHEDULED);
        $this->assertEquals('publishing', Post::STATUS_PUBLISHING);
        $this->assertEquals('published', Post::STATUS_PUBLISHED);
        $this->assertEquals('failed', Post::STATUS_FAILED);
    }

    public function test_post_content_type_constants(): void
    {
        $this->assertEquals('text', Post::TYPE_TEXT);
        $this->assertEquals('image', Post::TYPE_IMAGE);
        $this->assertEquals('video', Post::TYPE_VIDEO);
        $this->assertEquals('carousel', Post::TYPE_CAROUSEL);
        $this->assertEquals('story', Post::TYPE_STORY);
        $this->assertEquals('reel', Post::TYPE_REEL);
    }

    public function test_post_is_published_helper(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
        ]);

        $this->assertTrue($post->isPublished());

        $post->status = Post::STATUS_DRAFT;
        $this->assertFalse($post->isPublished());
    }

    public function test_post_can_be_edited_helper(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_DRAFT,
        ]);

        $this->assertTrue($post->canBeEdited());

        $post->status = Post::STATUS_SCHEDULED;
        $this->assertTrue($post->canBeEdited());

        $post->status = Post::STATUS_PUBLISHED;
        $this->assertFalse($post->canBeEdited());

        $post->status = Post::STATUS_FAILED;
        $this->assertFalse($post->canBeEdited());
    }

    public function test_post_scope_pending(): void
    {
        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PENDING,
        ]);

        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_DRAFT,
        ]);

        $this->assertCount(1, Post::pending()->get());
    }

    public function test_post_scope_published(): void
    {
        Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_PUBLISHED,
        ]);

        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_DRAFT,
        ]);

        $this->assertCount(3, Post::published()->get());
    }

    public function test_post_scope_for_platform(): void
    {
        Post::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'platform' => 'facebook',
        ]);

        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'platform' => 'instagram',
        ]);

        $this->assertCount(2, Post::forPlatform('facebook')->get());
        $this->assertCount(1, Post::forPlatform('instagram')->get());
    }

    public function test_post_scope_scheduled(): void
    {
        Post::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'status' => Post::STATUS_SCHEDULED,
        ]);

        $this->assertCount(2, Post::scheduled()->get());
    }

    public function test_post_soft_deletes(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
        ]);

        $post->delete();

        $this->assertSoftDeleted('posts', ['id' => $post->id]);
        $this->assertNull(Post::find($post->id));
        $this->assertNotNull(Post::withTrashed()->find($post->id));
    }

    public function test_post_fillable_attributes(): void
    {
        $post = Post::create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'platform' => 'facebook',
            'content_text' => 'Test content',
            'content_type' => Post::TYPE_TEXT,
            'hashtags' => ['#test', '#laravel'],
            'status' => Post::STATUS_DRAFT,
        ]);

        $this->assertEquals('Test content', $post->content_text);
        $this->assertEquals('facebook', $post->platform);
        $this->assertEquals(['#test', '#laravel'], $post->hashtags);
    }

    public function test_post_casts_media_urls_to_array(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'media_urls' => ['https://example.com/image1.jpg', 'https://example.com/image2.jpg'],
        ]);

        $this->assertIsArray($post->media_urls);
        $this->assertCount(2, $post->media_urls);
    }

    public function test_post_update_metrics(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
        ]);

        $post->updateMetrics([
            'likes' => 100,
            'comments' => 25,
            'shares' => 10,
            'views' => 1000,
        ]);

        $post->refresh();

        $this->assertEquals(100, $post->metrics['likes']);
        $this->assertEquals(25, $post->metrics['comments']);
        $this->assertEquals(10, $post->metrics['shares']);
        $this->assertEquals(1000, $post->metrics['views']);
    }

    public function test_post_calculate_viral_score(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'metrics' => [
                'likes' => 500,
                'comments' => 100,
                'shares' => 50,
                'views' => 10000,
            ],
            'published_at' => now(),
        ]);

        $score = $post->calculateViralScore();

        $this->assertIsFloat($score);
        $this->assertGreaterThanOrEqual(0, $score);
        $this->assertLessThanOrEqual(100, $score);
    }

    public function test_post_scope_viral(): void
    {
        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'is_viral' => true,
        ]);

        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $this->socialAccount->id,
            'is_viral' => false,
        ]);

        $this->assertCount(1, Post::viral()->get());
    }
}
