<?php

namespace Tests\Feature;

use App\Models\Brand;
use App\Models\Post;
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

    protected function setUp(): void
    {
        parent::setUp();

        $this->user = User::factory()->create();
        $this->brand = Brand::factory()->create(['user_id' => $this->user->id]);

        // Create rental package and active rental for user
        $package = RentalPackage::factory()->create([
            'name' => 'Test Package',
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

        $response->assertStatus(200)
            ->assertJsonStructure([
                'data' => [
                    '*' => ['id', 'content_text', 'platform', 'status'],
                ],
            ]);
    }

    public function test_user_can_create_post(): void
    {
        // Create social account for the user
        $socialAccount = \App\Models\SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'platform' => 'facebook',
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'social_account_id' => $socialAccount->id,
                'content_text' => 'Test post content',
                'content_type' => 'text',
            ]);

        $response->assertStatus(201)
            ->assertJsonStructure([
                'id', 'content_text', 'platform', 'status',
            ]);

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

        $response->assertStatus(200)
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

        $response->assertStatus(200)
            ->assertJsonPath('content_text', 'Updated content');
    }

    public function test_user_can_schedule_post(): void
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

        $response->assertStatus(200);

        $this->assertDatabaseHas('posts', [
            'id' => $post->id,
            'status' => 'scheduled',
        ]);
    }

    public function test_user_can_delete_own_post(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->deleteJson("/api/v1/posts/{$post->id}");

        $response->assertStatus(200)
            ->assertJson(['message' => 'Post deleted']);

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

        $response->assertStatus(403);
    }

    public function test_post_requires_content(): void
    {
        $socialAccount = \App\Models\SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'platform' => 'facebook',
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'social_account_id' => $socialAccount->id,
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

        $response->assertStatus(200)
            ->assertJson(['success' => true]);

        foreach ($postIds as $id) {
            $this->assertSoftDeleted('posts', ['id' => $id]);
        }
    }
}
