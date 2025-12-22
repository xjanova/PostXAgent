<?php

namespace Tests\Feature;

use App\Models\Brand;
use App\Models\Post;
use App\Models\SocialAccount;
use App\Models\User;
use App\Models\UserRental;
use App\Models\RentalPackage;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class PostControllerTest extends TestCase
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
            'platform' => 'facebook',
        ]);

        // Create rental package and active rental for user
        $package = RentalPackage::factory()->create([
            'posts_limit' => 100,
            'brands_limit' => 5,
            'ai_generations_limit' => 500,
            'platforms_limit' => 9,
        ]);

        UserRental::factory()->create([
            'user_id' => $this->user->id,
            'rental_package_id' => $package->id,
            'status' => 'active',
            'starts_at' => now()->subDay(),
            'expires_at' => now()->addMonth(),
        ]);
    }

    public function test_user_can_list_posts(): void
    {
        Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/posts');

        // PostController::index returns raw paginator without wrapper
        $response->assertOk()
            ->assertJsonStructure([
                'data' => [
                    '*' => ['id', 'content_text', 'platform', 'status'],
                ],
                'current_page',
                'last_page',
            ]);
    }

    public function test_user_can_create_post(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'social_account_id' => $this->socialAccount->id,
                'content_text' => 'Test post content',
                'content_type' => 'text',
            ]);

        // PostController::store returns raw Post model
        $response->assertStatus(201)
            ->assertJsonPath('content_text', 'Test post content')
            ->assertJsonPath('platform', 'facebook');

        $this->assertDatabaseHas('posts', [
            'content_text' => 'Test post content',
            'user_id' => $this->user->id,
        ]);
    }

    public function test_user_can_view_own_post(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson("/api/v1/posts/{$post->id}");

        // PostController::show returns raw Post model
        $response->assertOk()
            ->assertJsonPath('id', $post->id);
    }

    public function test_user_can_update_draft_post(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'draft',
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->putJson("/api/v1/posts/{$post->id}", [
                'content_text' => 'Updated content',
            ]);

        // PostController::update returns raw Post model
        $response->assertOk()
            ->assertJsonPath('content_text', 'Updated content');
    }

    public function test_user_can_set_scheduled_at(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'draft',
        ]);

        $scheduledAt = now()->addHours(2)->format('Y-m-d H:i:s');

        $response = $this->actingAs($this->user, 'sanctum')
            ->putJson("/api/v1/posts/{$post->id}", [
                'scheduled_at' => $scheduledAt,
            ]);

        $response->assertOk();
        $post->refresh();
        $this->assertNotNull($post->scheduled_at);
    }

    public function test_user_can_delete_own_post(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->deleteJson("/api/v1/posts/{$post->id}");

        // PostController::destroy returns {message: 'Post deleted'}
        $response->assertOk()
            ->assertJsonPath('message', 'Post deleted');

        $this->assertSoftDeleted('posts', ['id' => $post->id]);
    }

    public function test_user_cannot_access_other_users_post(): void
    {
        $otherUser = User::factory()->create();
        $otherBrand = Brand::factory()->create(['user_id' => $otherUser->id]);
        $post = Post::factory()->create([
            'user_id' => $otherUser->id,
            'brand_id' => $otherBrand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson("/api/v1/posts/{$post->id}");

        $response->assertForbidden();
    }

    public function test_post_requires_content(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'social_account_id' => $this->socialAccount->id,
                'content_type' => 'text',
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['content_text']);
    }

    public function test_post_requires_valid_social_account(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'content_text' => 'Test content',
                'content_type' => 'text',
                'social_account_id' => 99999,
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['social_account_id']);
    }

    public function test_bulk_delete_posts(): void
    {
        $posts = Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $postIds = $posts->pluck('id')->toArray();

        $response = $this->actingAs($this->user, 'sanctum')
            ->deleteJson('/api/v1/posts/bulk', [
                'post_ids' => $postIds,
            ]);

        // PostController::bulkDelete returns {success, message, deleted_count}
        $response->assertOk()
            ->assertJsonPath('success', true)
            ->assertJsonPath('deleted_count', 3);

        foreach ($postIds as $id) {
            $this->assertSoftDeleted('posts', ['id' => $id]);
        }
    }

    public function test_user_can_filter_posts_by_status(): void
    {
        Post::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'published',
        ]);

        Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'status' => 'draft',
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/posts?status=published');

        $response->assertOk();
        $this->assertCount(2, $response->json('data'));
    }
}
