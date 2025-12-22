<?php

namespace Tests\Feature;

use App\Models\Brand;
use App\Models\Post;
use App\Models\SocialAccount;
use App\Models\User;
use App\Models\UserRental;
use App\Models\RentalPackage;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Illuminate\Support\Facades\Queue;
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

        // Setup rental for quota
        $package = RentalPackage::factory()->create(['posts_limit' => 100]);
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

        $response->assertOk()
            ->assertJsonStructure(['data', 'current_page']);
    }

    public function test_user_can_create_post(): void
    {
        Queue::fake();

        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/posts', [
                'brand_id' => $this->brand->id,
                'social_account_id' => $this->socialAccount->id,
                'content_text' => 'Test post content',
                'content_type' => 'text',
                'scheduled_at' => now()->addDay()->toIso8601String(),
            ]);

        $response->assertStatus(201)
            ->assertJsonPath('content_text', 'Test post content');

        $this->assertDatabaseHas('posts', ['content_text' => 'Test post content']);
    }

    public function test_user_can_view_own_post(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson("/api/v1/posts/{$post->id}");

        $response->assertOk()
            ->assertJsonPath('id', $post->id);
    }

    public function test_user_can_delete_own_post(): void
    {
        $post = Post::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->deleteJson("/api/v1/posts/{$post->id}");

        $response->assertOk();
        $this->assertSoftDeleted('posts', ['id' => $post->id]);
    }

    public function test_user_cannot_view_other_users_post(): void
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
}
