<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\SocialAccount;
use App\Models\Brand;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Str;

class SocialAccountController extends Controller
{
    /**
     * List all social accounts for the authenticated user
     */
    public function index(Request $request): JsonResponse
    {
        $accounts = SocialAccount::where('user_id', $request->user()->id)
            ->with(['brand:id,name'])
            ->when($request->platform, fn($q, $platform) => $q->where('platform', $platform))
            ->when($request->brand_id, fn($q, $brandId) => $q->where('brand_id', $brandId))
            ->orderBy('platform')
            ->get()
            ->groupBy('platform');

        return response()->json([
            'success' => true,
            'data' => $accounts,
            'available_platforms' => SocialAccount::getPlatforms(),
        ]);
    }

    /**
     * Get OAuth URL for connecting a platform
     */
    public function connect(Request $request, string $platform): JsonResponse
    {
        $validPlatforms = SocialAccount::getPlatforms();

        if (!in_array($platform, $validPlatforms)) {
            return response()->json([
                'success' => false,
                'message' => 'แพลตฟอร์มไม่ถูกต้อง',
            ], 400);
        }

        // Generate state token for security
        $state = Str::random(40);
        session(['oauth_state' => $state, 'oauth_platform' => $platform]);

        // Get OAuth URL based on platform
        $oauthUrl = $this->getOAuthUrl($platform, $state);

        return response()->json([
            'success' => true,
            'data' => [
                'oauth_url' => $oauthUrl,
                'platform' => $platform,
            ],
        ]);
    }

    /**
     * Handle OAuth callback
     */
    public function callback(Request $request, string $platform): JsonResponse
    {
        // Verify state
        $storedState = session('oauth_state');
        if ($request->state !== $storedState) {
            return response()->json([
                'success' => false,
                'message' => 'Invalid OAuth state',
            ], 400);
        }

        if ($request->has('error')) {
            return response()->json([
                'success' => false,
                'message' => $request->error_description ?? 'OAuth authorization failed',
            ], 400);
        }

        // Exchange code for tokens (implementation depends on platform)
        $tokens = $this->exchangeCodeForTokens($platform, $request->code);

        if (!$tokens) {
            return response()->json([
                'success' => false,
                'message' => 'Failed to obtain access tokens',
            ], 500);
        }

        // Get user profile from platform
        $profile = $this->getPlatformProfile($platform, $tokens['access_token']);

        // Create or update social account
        $account = SocialAccount::updateOrCreate(
            [
                'user_id' => $request->user()->id,
                'platform' => $platform,
                'platform_user_id' => $profile['id'],
            ],
            [
                'platform_username' => $profile['username'] ?? null,
                'display_name' => $profile['name'] ?? null,
                'access_token' => $tokens['access_token'],
                'refresh_token' => $tokens['refresh_token'] ?? null,
                'token_expires_at' => isset($tokens['expires_in'])
                    ? now()->addSeconds($tokens['expires_in'])
                    : null,
                'profile_url' => $profile['profile_url'] ?? null,
                'avatar_url' => $profile['avatar_url'] ?? null,
                'metadata' => $profile['metadata'] ?? [],
                'is_active' => true,
            ]
        );

        return response()->json([
            'success' => true,
            'message' => 'เชื่อมต่อบัญชีสำเร็จ',
            'data' => $account,
        ]);
    }

    /**
     * Disconnect a social account
     */
    public function disconnect(Request $request, int $id): JsonResponse
    {
        $account = SocialAccount::where('user_id', $request->user()->id)
            ->findOrFail($id);

        $account->delete();

        return response()->json([
            'success' => true,
            'message' => 'ยกเลิกการเชื่อมต่อบัญชีสำเร็จ',
        ]);
    }

    /**
     * Refresh access token
     */
    public function refreshToken(Request $request, int $id): JsonResponse
    {
        $account = SocialAccount::where('user_id', $request->user()->id)
            ->findOrFail($id);

        if (!$account->refresh_token) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มี refresh token สำหรับบัญชีนี้',
            ], 400);
        }

        $tokens = $this->refreshAccessToken($account->platform, $account->refresh_token);

        if (!$tokens) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถรีเฟรช token ได้ กรุณาเชื่อมต่อใหม่',
            ], 500);
        }

        $account->update([
            'access_token' => $tokens['access_token'],
            'refresh_token' => $tokens['refresh_token'] ?? $account->refresh_token,
            'token_expires_at' => isset($tokens['expires_in'])
                ? now()->addSeconds($tokens['expires_in'])
                : null,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'รีเฟรช token สำเร็จ',
            'data' => $account->fresh(),
        ]);
    }

    /**
     * Get OAuth URL for platform
     */
    private function getOAuthUrl(string $platform, string $state): string
    {
        $redirectUri = url("/api/v1/oauth/{$platform}/callback");

        return match ($platform) {
            'facebook', 'instagram' => $this->getFacebookOAuthUrl($state, $redirectUri),
            'twitter' => $this->getTwitterOAuthUrl($state, $redirectUri),
            'tiktok' => $this->getTikTokOAuthUrl($state, $redirectUri),
            'line' => $this->getLineOAuthUrl($state, $redirectUri),
            'youtube' => $this->getYouTubeOAuthUrl($state, $redirectUri),
            'linkedin' => $this->getLinkedInOAuthUrl($state, $redirectUri),
            'pinterest' => $this->getPinterestOAuthUrl($state, $redirectUri),
            default => '#',
        };
    }

    private function getFacebookOAuthUrl(string $state, string $redirectUri): string
    {
        $appId = config('services.facebook.client_id');
        $scopes = 'public_profile,email,pages_manage_posts,pages_read_engagement,instagram_basic,instagram_content_publish';

        return "https://www.facebook.com/v18.0/dialog/oauth?" . http_build_query([
            'client_id' => $appId,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => $scopes,
        ]);
    }

    private function getTwitterOAuthUrl(string $state, string $redirectUri): string
    {
        $clientId = config('services.twitter.client_id');
        $scopes = 'tweet.read tweet.write users.read offline.access';

        return "https://twitter.com/i/oauth2/authorize?" . http_build_query([
            'response_type' => 'code',
            'client_id' => $clientId,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => $scopes,
            'code_challenge' => 'challenge',
            'code_challenge_method' => 'plain',
        ]);
    }

    private function getTikTokOAuthUrl(string $state, string $redirectUri): string
    {
        $clientKey = config('services.tiktok.client_key');
        $scopes = 'user.info.basic,video.upload,video.publish';

        return "https://www.tiktok.com/v2/auth/authorize/?" . http_build_query([
            'client_key' => $clientKey,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => $scopes,
            'response_type' => 'code',
        ]);
    }

    private function getLineOAuthUrl(string $state, string $redirectUri): string
    {
        $channelId = config('services.line.channel_id');

        return "https://access.line.me/oauth2/v2.1/authorize?" . http_build_query([
            'response_type' => 'code',
            'client_id' => $channelId,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => 'profile openid',
        ]);
    }

    private function getYouTubeOAuthUrl(string $state, string $redirectUri): string
    {
        $clientId = config('services.google.client_id');
        $scopes = 'https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube';

        return "https://accounts.google.com/o/oauth2/v2/auth?" . http_build_query([
            'client_id' => $clientId,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => $scopes,
            'response_type' => 'code',
            'access_type' => 'offline',
        ]);
    }

    private function getLinkedInOAuthUrl(string $state, string $redirectUri): string
    {
        $clientId = config('services.linkedin.client_id');
        $scopes = 'r_liteprofile w_member_social';

        return "https://www.linkedin.com/oauth/v2/authorization?" . http_build_query([
            'response_type' => 'code',
            'client_id' => $clientId,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => $scopes,
        ]);
    }

    private function getPinterestOAuthUrl(string $state, string $redirectUri): string
    {
        $clientId = config('services.pinterest.client_id');
        $scopes = 'boards:read,pins:read,pins:write';

        return "https://api.pinterest.com/oauth/?" . http_build_query([
            'response_type' => 'code',
            'client_id' => $clientId,
            'redirect_uri' => $redirectUri,
            'state' => $state,
            'scope' => $scopes,
        ]);
    }

    /**
     * Exchange authorization code for tokens (placeholder)
     */
    private function exchangeCodeForTokens(string $platform, string $code): ?array
    {
        // Implementation would depend on each platform's API
        // This is a placeholder that should be implemented per platform
        return null;
    }

    /**
     * Get user profile from platform (placeholder)
     */
    private function getPlatformProfile(string $platform, string $accessToken): array
    {
        // Implementation would depend on each platform's API
        return [
            'id' => '',
            'username' => '',
            'name' => '',
        ];
    }

    /**
     * Refresh access token (placeholder)
     */
    private function refreshAccessToken(string $platform, string $refreshToken): ?array
    {
        // Implementation would depend on each platform's API
        return null;
    }
}
