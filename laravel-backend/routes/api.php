<?php

use Illuminate\Support\Facades\Route;
use App\Http\Controllers\Api\AuthController;
use App\Http\Controllers\Api\BrandController;
use App\Http\Controllers\Api\CampaignController;
use App\Http\Controllers\Api\PostController;
use App\Http\Controllers\Api\SocialAccountController;
use App\Http\Controllers\Api\SubscriptionController;
use App\Http\Controllers\Api\AnalyticsController;
use App\Http\Controllers\Api\WebhookController;

/*
|--------------------------------------------------------------------------
| API Routes
|--------------------------------------------------------------------------
*/

// Public routes
Route::prefix('v1')->group(function () {
    // Authentication
    Route::post('/auth/register', [AuthController::class, 'register']);
    Route::post('/auth/login', [AuthController::class, 'login']);
    Route::post('/auth/forgot-password', [AuthController::class, 'forgotPassword']);
    Route::post('/auth/reset-password', [AuthController::class, 'resetPassword']);

    // Subscription plans (public)
    Route::get('/plans', [SubscriptionController::class, 'plans']);

    // Stripe webhooks
    Route::post('/webhooks/stripe', [WebhookController::class, 'handleStripe']);

    // OAuth callbacks
    Route::get('/oauth/{platform}/callback', [SocialAccountController::class, 'callback']);
});

// Protected routes
Route::prefix('v1')->middleware(['auth:sanctum'])->group(function () {
    // Auth
    Route::post('/auth/logout', [AuthController::class, 'logout']);
    Route::get('/auth/me', [AuthController::class, 'me']);
    Route::put('/auth/profile', [AuthController::class, 'updateProfile']);
    Route::put('/auth/password', [AuthController::class, 'updatePassword']);

    // Brands
    Route::apiResource('brands', BrandController::class);

    // Social Accounts
    Route::get('/social-accounts', [SocialAccountController::class, 'index']);
    Route::get('/social-accounts/{platform}/connect', [SocialAccountController::class, 'connect']);
    Route::delete('/social-accounts/{id}', [SocialAccountController::class, 'disconnect']);
    Route::post('/social-accounts/{id}/refresh', [SocialAccountController::class, 'refreshToken']);

    // Campaigns
    Route::apiResource('campaigns', CampaignController::class);
    Route::post('/campaigns/{campaign}/start', [CampaignController::class, 'start']);
    Route::post('/campaigns/{campaign}/pause', [CampaignController::class, 'pause']);
    Route::post('/campaigns/{campaign}/stop', [CampaignController::class, 'stop']);

    // Posts
    Route::apiResource('posts', PostController::class);
    Route::post('/posts/generate-content', [PostController::class, 'generateContent']);
    Route::post('/posts/generate-image', [PostController::class, 'generateImage']);
    Route::post('/posts/{post}/publish', [PostController::class, 'publish']);
    Route::get('/posts/{post}/metrics', [PostController::class, 'metrics']);

    // Subscriptions
    Route::prefix('subscription')->group(function () {
        Route::get('/status', [SubscriptionController::class, 'status']);
        Route::post('/checkout', [SubscriptionController::class, 'checkout']);
        Route::get('/portal', [SubscriptionController::class, 'portal']);
        Route::post('/change-plan', [SubscriptionController::class, 'changePlan']);
        Route::post('/cancel', [SubscriptionController::class, 'cancel']);
        Route::post('/resume', [SubscriptionController::class, 'resume']);
        Route::get('/invoices', [SubscriptionController::class, 'invoices']);
    });

    // Analytics
    Route::prefix('analytics')->group(function () {
        Route::get('/overview', [AnalyticsController::class, 'overview']);
        Route::get('/posts', [AnalyticsController::class, 'posts']);
        Route::get('/engagement', [AnalyticsController::class, 'engagement']);
        Route::get('/platforms', [AnalyticsController::class, 'platforms']);
        Route::get('/brands/{brand}', [AnalyticsController::class, 'brand']);
    });

    // AI Manager status (admin only)
    Route::middleware(['role:admin'])->group(function () {
        Route::get('/admin/ai-manager/stats', [AdminController::class, 'aiManagerStats']);
        Route::get('/admin/ai-manager/workers', [AdminController::class, 'workers']);
        Route::post('/admin/ai-manager/restart', [AdminController::class, 'restartAIManager']);
    });
});
