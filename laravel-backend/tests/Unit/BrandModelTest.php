<?php

namespace Tests\Unit;

use App\Models\Brand;
use App\Models\User;
use App\Models\Post;
use App\Models\Campaign;
use App\Models\SocialAccount;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class BrandModelTest extends TestCase
{
    use RefreshDatabase;

    public function test_brand_belongs_to_user(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);

        $this->assertInstanceOf(User::class, $brand->user);
        $this->assertEquals($user->id, $brand->user->id);
    }

    public function test_brand_has_posts_relationship(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);
        Post::factory()->count(3)->create([
            'user_id' => $user->id,
            'brand_id' => $brand->id,
        ]);

        $this->assertCount(3, $brand->posts);
        $this->assertInstanceOf(Post::class, $brand->posts->first());
    }

    public function test_brand_has_campaigns_relationship(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);
        Campaign::factory()->count(2)->create([
            'user_id' => $user->id,
            'brand_id' => $brand->id,
        ]);

        $this->assertCount(2, $brand->campaigns);
        $this->assertInstanceOf(Campaign::class, $brand->campaigns->first());
    }

    public function test_brand_has_social_accounts_relationship(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);
        SocialAccount::factory()->count(3)->create([
            'user_id' => $user->id,
            'brand_id' => $brand->id,
        ]);

        $this->assertCount(3, $brand->socialAccounts);
        $this->assertInstanceOf(SocialAccount::class, $brand->socialAccounts->first());
    }

    public function test_brand_fillable_attributes(): void
    {
        $user = User::factory()->create();

        $brand = Brand::create([
            'user_id' => $user->id,
            'name' => 'Test Brand',
            'description' => 'Test Description',
            'logo_url' => 'https://example.com/logo.png',
            'industry' => 'technology',
            'target_audience' => 'Young adults',
            'tone' => 'Professional',
            'brand_colors' => ['#FF0000', '#0000FF'],
        ]);

        $this->assertEquals('Test Brand', $brand->name);
        $this->assertEquals('Test Description', $brand->description);
        $this->assertEquals('technology', $brand->industry);
    }

    public function test_brand_scoped_to_user(): void
    {
        $user1 = User::factory()->create();
        $user2 = User::factory()->create();

        Brand::factory()->count(3)->create(['user_id' => $user1->id]);
        Brand::factory()->count(2)->create(['user_id' => $user2->id]);

        $user1Brands = Brand::where('user_id', $user1->id)->get();
        $user2Brands = Brand::where('user_id', $user2->id)->get();

        $this->assertCount(3, $user1Brands);
        $this->assertCount(2, $user2Brands);
    }

    public function test_brand_soft_deletes(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);

        $brand->delete();

        $this->assertSoftDeleted('brands', ['id' => $brand->id]);
        $this->assertNull(Brand::find($brand->id));
        $this->assertNotNull(Brand::withTrashed()->find($brand->id));
    }

    public function test_brand_can_be_restored(): void
    {
        $user = User::factory()->create();
        $brand = Brand::factory()->create(['user_id' => $user->id]);

        $brand->delete();
        $brand->restore();

        $this->assertNotSoftDeleted('brands', ['id' => $brand->id]);
        $this->assertNotNull(Brand::find($brand->id));
    }
}
