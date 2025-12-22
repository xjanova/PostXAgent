<?php

namespace Tests\Unit;

use App\Models\AccountPool;
use App\Models\AccountPoolMember;
use App\Models\User;
use App\Models\Brand;
use App\Models\SocialAccount;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class AccountPoolModelTest extends TestCase
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

    public function test_account_pool_belongs_to_user(): void
    {
        $pool = AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(User::class, $pool->user);
        $this->assertEquals($this->user->id, $pool->user->id);
    }

    public function test_account_pool_belongs_to_brand(): void
    {
        $pool = AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(Brand::class, $pool->brand);
        $this->assertEquals($this->brand->id, $pool->brand->id);
    }

    public function test_account_pool_has_members_relationship(): void
    {
        $pool = AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $socialAccount = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        AccountPoolMember::factory()->count(3)->create([
            'account_pool_id' => $pool->id,
            'social_account_id' => $socialAccount->id,
        ]);

        $this->assertCount(3, $pool->members);
        $this->assertInstanceOf(AccountPoolMember::class, $pool->members->first());
    }

    public function test_account_pool_fillable_attributes(): void
    {
        $pool = AccountPool::create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'name' => 'Test Pool',
            'platform' => 'facebook',
            'rotation_strategy' => 'round_robin',
            'is_active' => true,
        ]);

        $this->assertEquals('Test Pool', $pool->name);
        $this->assertEquals('facebook', $pool->platform);
        $this->assertEquals('round_robin', $pool->rotation_strategy);
        $this->assertTrue($pool->is_active);
    }

    public function test_account_pool_scope_active(): void
    {
        AccountPool::factory()->count(2)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'is_active' => true,
        ]);

        AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'is_active' => false,
        ]);

        $this->assertCount(2, AccountPool::where('is_active', true)->get());
    }

    public function test_account_pool_casts_settings_to_array(): void
    {
        $pool = AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'settings' => ['max_posts' => 10, 'cooldown' => 30],
        ]);

        $this->assertIsArray($pool->settings);
        $this->assertEquals(10, $pool->settings['max_posts']);
    }

    public function test_account_pool_casts_metadata_to_array(): void
    {
        $pool = AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'metadata' => ['key' => 'value'],
        ]);

        $this->assertIsArray($pool->metadata);
        $this->assertEquals('value', $pool->metadata['key']);
    }

    public function test_account_pool_soft_deletes(): void
    {
        $pool = AccountPool::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $pool->delete();

        $this->assertSoftDeleted('account_pools', ['id' => $pool->id]);
        $this->assertNull(AccountPool::find($pool->id));
        $this->assertNotNull(AccountPool::withTrashed()->find($pool->id));
    }
}
