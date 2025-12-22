<?php

namespace Tests\Feature;

use App\Models\Brand;
use App\Models\User;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class BrandControllerTest extends TestCase
{
    use RefreshDatabase;

    private User $user;

    protected function setUp(): void
    {
        parent::setUp();
        $this->user = User::factory()->create();
    }

    public function test_user_can_list_brands(): void
    {
        Brand::factory()->count(3)->create(['user_id' => $this->user->id]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/brands');

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'name', 'description', 'logo_url'],
                ],
            ]);

        $this->assertCount(3, $response->json('data'));
    }

    public function test_user_can_create_brand(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/brands', [
                'name' => 'Test Brand',
                'description' => 'Test Description',
                'industry' => 'technology',
                'target_audience' => 'Young adults',
            ]);

        $response->assertStatus(201)
            ->assertJsonStructure([
                'success',
                'data' => ['id', 'name', 'description'],
            ]);

        $this->assertDatabaseHas('brands', [
            'name' => 'Test Brand',
            'user_id' => $this->user->id,
        ]);
    }

    public function test_user_can_view_own_brand(): void
    {
        $brand = Brand::factory()->create(['user_id' => $this->user->id]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson("/api/v1/brands/{$brand->id}");

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => [
                    'id' => $brand->id,
                    'name' => $brand->name,
                ],
            ]);
    }

    public function test_user_cannot_view_other_users_brand(): void
    {
        $otherUser = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $otherUser->id]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson("/api/v1/brands/{$brand->id}");

        $response->assertStatus(403);
    }

    public function test_user_can_update_own_brand(): void
    {
        $brand = Brand::factory()->create(['user_id' => $this->user->id]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->putJson("/api/v1/brands/{$brand->id}", [
                'name' => 'Updated Brand Name',
            ]);

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => ['name' => 'Updated Brand Name'],
            ]);

        $this->assertDatabaseHas('brands', [
            'id' => $brand->id,
            'name' => 'Updated Brand Name',
        ]);
    }

    public function test_user_can_delete_own_brand(): void
    {
        $brand = Brand::factory()->create(['user_id' => $this->user->id]);

        $response = $this->actingAs($this->user, 'sanctum')
            ->deleteJson("/api/v1/brands/{$brand->id}");

        $response->assertStatus(200)
            ->assertJson(['success' => true]);

        $this->assertDatabaseMissing('brands', ['id' => $brand->id]);
    }

    public function test_brand_name_is_required(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->postJson('/api/v1/brands', [
                'description' => 'Test Description',
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['name']);
    }
}
