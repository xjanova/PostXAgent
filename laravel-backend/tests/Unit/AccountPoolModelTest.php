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

    public function test_account_pool_belongs_to_brand(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(Brand::class, $pool->brand);
        $this->assertEquals($this->brand->id, $pool->brand->id);
    }

    public function test_account_pool_has_members_relationship(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        // Create 3 different social accounts for 3 members
        $socialAccounts = SocialAccount::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        foreach ($socialAccounts as $account) {
            AccountPoolMember::factory()->create([
                'account_pool_id' => $pool->id,
                'social_account_id' => $account->id,
            ]);
        }

        $this->assertCount(3, $pool->members);
        $this->assertInstanceOf(AccountPoolMember::class, $pool->members->first());
    }

    public function test_account_pool_fillable_attributes(): void
    {
        $pool = AccountPool::create([
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
            'brand_id' => $this->brand->id,
            'is_active' => true,
        ]);

        AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'is_active' => false,
        ]);

        $this->assertCount(2, AccountPool::active()->get());
    }

    public function test_account_pool_scope_for_platform(): void
    {
        AccountPool::factory()->count(2)->create([
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
        ]);

        AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'platform' => 'instagram',
        ]);

        $this->assertCount(2, AccountPool::forPlatform('facebook')->get());
        $this->assertCount(1, AccountPool::forPlatform('instagram')->get());
    }

    public function test_account_pool_scope_for_brand(): void
    {
        $otherBrand = Brand::factory()->create(['user_id' => $this->user->id]);

        AccountPool::factory()->count(2)->create([
            'brand_id' => $this->brand->id,
        ]);

        AccountPool::factory()->create([
            'brand_id' => $otherBrand->id,
        ]);

        $this->assertCount(2, AccountPool::forBrand($this->brand->id)->get());
    }

    public function test_account_pool_soft_deletes(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
        ]);

        $pool->delete();

        $this->assertSoftDeleted('account_pools', ['id' => $pool->id]);
        $this->assertNull(AccountPool::find($pool->id));
        $this->assertNotNull(AccountPool::withTrashed()->find($pool->id));
    }

    public function test_account_pool_casts_booleans(): void
    {
        $pool = AccountPool::factory()->create([
            'brand_id' => $this->brand->id,
            'auto_failover' => 1,
            'is_active' => 1,
        ]);

        $this->assertIsBool($pool->auto_failover);
        $this->assertIsBool($pool->is_active);
        $this->assertTrue($pool->auto_failover);
        $this->assertTrue($pool->is_active);
    }

    public function test_account_pool_get_strategies(): void
    {
        $strategies = AccountPool::getStrategies();

        $this->assertIsArray($strategies);
        $this->assertContains('round_robin', $strategies);
        $this->assertContains('random', $strategies);
        $this->assertContains('least_used', $strategies);
        $this->assertContains('priority', $strategies);
    }
}
