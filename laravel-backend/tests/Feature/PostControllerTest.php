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
            'limits' => [
                'posts' => 100,
                'brands' => 5,
                'ai_generations' => 500,
                'platforms' => 9,
            ],
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
                'success',
                'data' => [
                    '*' => ['id', 'content', 'platforms', 'status'],
                ],
            ]);
    }

    public function test_user_can_create_post(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'content' => 'Test post content',
                'platforms' => ['facebook', 'instagram'],
            ]);

        $response->assertStatus(201)
            ->assertJsonStructure([
                'success',
                'data' => ['id', 'content', 'platforms', 'status'],
            ]);

        $this->assertDatabaseHas('posts', [
            'content' => 'Test post content',
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
            ->assertJson([
                'success' => true,
                'data' => ['id' => $post->id],
            ]);
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
                'content' => 'Updated content',
            ]);

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => ['content' => 'Updated content'],
            ]);
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
            ->assertJson(['success' => true]);

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
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'platforms' => ['facebook'],
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['content']);
    }

    public function test_post_requires_valid_platforms(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'content' => 'Test content',
                'platforms' => ['invalid_platform'],
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['platforms.0']);
    }

    public function test_bulk_delete_posts(): void
    {
        $posts = Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $ids = $posts->pluck('id')->toArray();

        $response = $this->actingAs($this->user, 'sanctum')
            ->deleteJson('/api/v1/posts/bulk', [
                'ids' => $ids,
            ]);

        $response->assertStatus(200);

        foreach ($ids as $id) {
            $this->assertSoftDeleted('posts', ['id' => $id]);
        }
    }
}
