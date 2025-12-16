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
use App\Http\Controllers\Api\AIManagerStatusController;
use App\Http\Controllers\Api\AccountPoolController;
use App\Http\Controllers\Api\RentalController;

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

    // Omise webhooks (Thai payment gateway)
    Route::post('/webhooks/omise', [RentalController::class, 'omiseWebhook']);

    // Rental packages (public)
    Route::get('/rentals/packages', [RentalController::class, 'packages']);
    Route::get('/rentals/packages/{id}', [RentalController::class, 'packageDetail']);
    Route::get('/rentals/payment-methods', [RentalController::class, 'paymentMethods']);

    // OAuth callbacks
    Route::get('/oauth/{platform}/callback', [SocialAccountController::class, 'callback']);

    // AI Manager Public Status (no auth required)
    Route::prefix('ai-manager')->group(function () {
        Route::get('/status', [AIManagerStatusController::class, 'status']);
        Route::get('/status/full', [AIManagerStatusController::class, 'fullStatus']);
        Route::get('/status/badge', [AIManagerStatusController::class, 'badge']);
        Route::get('/ping', [AIManagerStatusController::class, 'ping']);
        Route::get('/stats', [AIManagerStatusController::class, 'stats']);
        Route::get('/workers', [AIManagerStatusController::class, 'workers']);
        Route::get('/system', [AIManagerStatusController::class, 'system']);
    });
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

    // Note: AI Manager status endpoints moved to public routes above

    // AI Manager Admin Controls (admin only)
    Route::prefix('ai-manager')->middleware(['role:admin'])->group(function () {
        Route::post('/start', [AIManagerStatusController::class, 'start']);
        Route::post('/stop', [AIManagerStatusController::class, 'stop']);
        Route::post('/refresh', [AIManagerStatusController::class, 'refresh']);
        Route::post('/test-connection', [AIManagerStatusController::class, 'testConnection']);
    });

    // Account Pools - จัดการ Pool ของ Account สำหรับ Auto-Rotation และ Failover
    Route::prefix('account-pools')->group(function () {
        Route::get('/', [AccountPoolController::class, 'index']);
        Route::post('/', [AccountPoolController::class, 'store']);
        Route::get('/health-report', [AccountPoolController::class, 'healthReport']);
        Route::get('/{accountPool}', [AccountPoolController::class, 'show']);
        Route::put('/{accountPool}', [AccountPoolController::class, 'update']);
        Route::delete('/{accountPool}', [AccountPoolController::class, 'destroy']);

        // Pool statistics and logs
        Route::get('/{accountPool}/statistics', [AccountPoolController::class, 'statistics']);
        Route::get('/{accountPool}/logs', [AccountPoolController::class, 'logs']);
        Route::get('/{accountPool}/next-account', [AccountPoolController::class, 'previewNextAccount']);

        // Account management within pool
        Route::post('/{accountPool}/accounts', [AccountPoolController::class, 'addAccount']);
        Route::delete('/{accountPool}/accounts/{accountId}', [AccountPoolController::class, 'removeAccount']);
        Route::put('/{accountPool}/accounts/{accountId}', [AccountPoolController::class, 'updateMember']);
        Route::post('/{accountPool}/accounts/{accountId}/recover', [AccountPoolController::class, 'recoverAccount']);
    });

    // Account Pools Admin (admin only)
    Route::prefix('account-pools')->middleware(['role:admin'])->group(function () {
        Route::post('/reset-daily-counters', [AccountPoolController::class, 'resetDailyCounters']);
    });

    // Rentals (Thai Market) - ระบบเช่าแพ็กเกจสำหรับตลาดไทย
    Route::prefix('rentals')->group(function () {
        Route::get('/status', [RentalController::class, 'status']);
        Route::get('/history', [RentalController::class, 'history']);
        Route::post('/checkout', [RentalController::class, 'checkout']);
        Route::post('/validate-promo', [RentalController::class, 'validatePromo']);
        Route::post('/{id}/cancel', [RentalController::class, 'cancel']);

        // Payments
        Route::post('/payments/{uuid}/upload-slip', [RentalController::class, 'uploadSlip']);
        Route::get('/payments/{uuid}/status', [RentalController::class, 'paymentStatus']);
        Route::post('/payments/{uuid}/confirm', [RentalController::class, 'confirmPayment']);

        // Invoices
        Route::get('/invoices', [RentalController::class, 'invoices']);
        Route::post('/invoices/{id}/request-tax', [RentalController::class, 'requestTaxInvoice']);
        Route::get('/invoices/{id}/download', [RentalController::class, 'downloadInvoice']);
    });

    // Rentals Admin (admin only)
    Route::prefix('admin/rentals')->middleware(['role:admin'])->group(function () {
        Route::get('/payments', [RentalController::class, 'adminPayments']);
        Route::post('/payments/{uuid}/verify', [RentalController::class, 'adminVerifyPayment']);
        Route::post('/payments/{uuid}/reject', [RentalController::class, 'adminRejectPayment']);
        Route::get('/stats', [RentalController::class, 'adminStats']);
    });
});
