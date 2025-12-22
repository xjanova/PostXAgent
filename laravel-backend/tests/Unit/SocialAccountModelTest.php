<?php

namespace Tests\Unit;

use App\Models\SocialAccount;
use App\Models\User;
use App\Models\Brand;
use App\Models\Post;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Tests\TestCase;

class SocialAccountModelTest extends TestCase
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

    public function test_social_account_belongs_to_user(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(User::class, $account->user);
        $this->assertEquals($this->user->id, $account->user->id);
    }

    public function test_social_account_belongs_to_brand(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $this->assertInstanceOf(Brand::class, $account->brand);
        $this->assertEquals($this->brand->id, $account->brand->id);
    }

    public function test_social_account_has_posts_relationship(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        Post::factory()->count(3)->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'social_account_id' => $account->id,
        ]);

        $this->assertCount(3, $account->posts);
        $this->assertInstanceOf(Post::class, $account->posts->first());
    }

    public function test_social_account_platform_constants(): void
    {
        $this->assertEquals('facebook', SocialAccount::PLATFORM_FACEBOOK);
        $this->assertEquals('instagram', SocialAccount::PLATFORM_INSTAGRAM);
        $this->assertEquals('tiktok', SocialAccount::PLATFORM_TIKTOK);
        $this->assertEquals('twitter', SocialAccount::PLATFORM_TWITTER);
        $this->assertEquals('line', SocialAccount::PLATFORM_LINE);
        $this->assertEquals('youtube', SocialAccount::PLATFORM_YOUTUBE);
        $this->assertEquals('threads', SocialAccount::PLATFORM_THREADS);
        $this->assertEquals('linkedin', SocialAccount::PLATFORM_LINKEDIN);
        $this->assertEquals('pinterest', SocialAccount::PLATFORM_PINTEREST);
    }

    public function test_get_platforms_returns_all_platforms(): void
    {
        $platforms = SocialAccount::getPlatforms();

        $this->assertIsArray($platforms);
        $this->assertCount(9, $platforms);
        $this->assertContains('facebook', $platforms);
        $this->assertContains('instagram', $platforms);
        $this->assertContains('tiktok', $platforms);
        $this->assertContains('twitter', $platforms);
        $this->assertContains('line', $platforms);
        $this->assertContains('youtube', $platforms);
        $this->assertContains('threads', $platforms);
        $this->assertContains('linkedin', $platforms);
        $this->assertContains('pinterest', $platforms);
    }

    public function test_is_token_expired_returns_false_when_no_expiry(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'token_expires_at' => null,
        ]);

        $this->assertFalse($account->isTokenExpired());
    }

    public function test_is_token_expired_returns_true_when_expired(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'token_expires_at' => now()->subHour(),
        ]);

        $this->assertTrue($account->isTokenExpired());
    }

    public function test_is_token_expired_returns_false_when_valid(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'token_expires_at' => now()->addHour(),
        ]);

        $this->assertFalse($account->isTokenExpired());
    }

    public function test_needs_refresh_returns_false_when_no_expiry(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'token_expires_at' => null,
        ]);

        $this->assertFalse($account->needsRefresh());
    }

    public function test_needs_refresh_returns_true_when_expires_soon(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'token_expires_at' => now()->addMinutes(30),
        ]);

        $this->assertTrue($account->needsRefresh());
    }

    public function test_needs_refresh_returns_false_when_expires_later(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'token_expires_at' => now()->addHours(2),
        ]);

        $this->assertFalse($account->needsRefresh());
    }

    public function test_get_credentials_returns_array(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'access_token' => 'test_access_token',
            'refresh_token' => 'test_refresh_token',
            'platform_user_id' => '12345',
            'metadata' => ['page_id' => '67890'],
        ]);

        $credentials = $account->getCredentials();

        $this->assertIsArray($credentials);
        $this->assertEquals('test_access_token', $credentials['access_token']);
        $this->assertEquals('test_refresh_token', $credentials['refresh_token']);
        $this->assertEquals('12345', $credentials['platform_user_id']);
        $this->assertEquals('67890', $credentials['page_id']);
    }

    public function test_social_account_hides_sensitive_fields(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'access_token' => 'secret_access_token',
            'refresh_token' => 'secret_refresh_token',
        ]);

        $array = $account->toArray();

        $this->assertArrayNotHasKey('access_token', $array);
        $this->assertArrayNotHasKey('refresh_token', $array);
    }

    public function test_social_account_soft_deletes(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
        ]);

        $account->delete();

        $this->assertSoftDeleted('social_accounts', ['id' => $account->id]);
        $this->assertNull(SocialAccount::find($account->id));
        $this->assertNotNull(SocialAccount::withTrashed()->find($account->id));
    }

    public function test_social_account_fillable_attributes(): void
    {
        $account = SocialAccount::create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'platform' => 'facebook',
            'platform_user_id' => '12345',
            'platform_username' => 'testuser',
            'display_name' => 'Test User',
            'access_token' => 'access_token',
            'is_active' => true,
        ]);

        $this->assertEquals('facebook', $account->platform);
        $this->assertEquals('12345', $account->platform_user_id);
        $this->assertEquals('testuser', $account->platform_username);
        $this->assertTrue($account->is_active);
    }

    public function test_social_account_casts_metadata_to_array(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'metadata' => ['key' => 'value', 'page_id' => '123'],
        ]);

        $this->assertIsArray($account->metadata);
        $this->assertEquals('value', $account->metadata['key']);
    }

    public function test_social_account_casts_is_active_to_boolean(): void
    {
        $account = SocialAccount::factory()->create([
            'user_id' => $this->user->id,
            'brand_id' => $this->brand->id,
            'is_active' => 1,
        ]);

        $this->assertIsBool($account->is_active);
        $this->assertTrue($account->is_active);
    }
}
