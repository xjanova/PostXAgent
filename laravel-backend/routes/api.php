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
use App\Http\Controllers\Api\ExportController;
use App\Http\Controllers\Api\NotificationController;
use App\Http\Controllers\Api\AccountCreationController;
use App\Http\Controllers\Api\WebLearningController;
use App\Http\Controllers\Api\UsageTrackingController;
use App\Http\Controllers\Api\AdminRentalController;
use App\Http\Controllers\Api\WorkflowTemplateController;
use App\Http\Controllers\Api\UserWorkflowController;
use App\Http\Controllers\Api\SeekAndPostController;
use App\Http\Controllers\Api\CommentController;
use App\Http\Controllers\Api\ResponseToneController;
use App\Http\Controllers\Api\ViralAnalysisController;
use App\Http\Controllers\Api\PaymentGatewayController;

/*
|--------------------------------------------------------------------------
| API Routes
|--------------------------------------------------------------------------
*/

// Public routes
Route::prefix('v1')->group(function () {
    // Authentication (strict rate limit)
    Route::middleware('throttle:auth')->group(function () {
        Route::post('/auth/register', [AuthController::class, 'register']);
        Route::post('/auth/login', [AuthController::class, 'login']);
        Route::post('/auth/forgot-password', [AuthController::class, 'forgotPassword']);
        Route::post('/auth/reset-password', [AuthController::class, 'resetPassword']);
    });

    // Subscription plans (public)
    Route::get('/plans', [SubscriptionController::class, 'plans']);

    // Webhooks (higher rate limit)
    Route::middleware('throttle:webhooks')->group(function () {
        Route::post('/webhooks/stripe', [WebhookController::class, 'handleStripe']);
        Route::post('/webhooks/omise', [RentalController::class, 'omiseWebhook']);
    });

    // Rental packages (public)
    Route::get('/rentals/packages', [RentalController::class, 'packages']);
    Route::get('/rentals/packages/{id}', [RentalController::class, 'packageDetail']);
    Route::get('/rentals/payment-methods', [RentalController::class, 'paymentMethods']);

    // OAuth callbacks
    Route::get('/oauth/{platform}/callback', [SocialAccountController::class, 'callback']);

    // AI Manager Public Status (relaxed rate limit)
    Route::prefix('ai-manager')->middleware('throttle:status')->group(function () {
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
Route::prefix('v1')->middleware(['auth:sanctum', 'throttle:api'])->group(function () {
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

    // Posts (with rental limit checks)
    Route::middleware(['rental.active'])->group(function () {
        Route::apiResource('posts', PostController::class);
        Route::get('/posts/{post}/metrics', [PostController::class, 'metrics']);

        // Bulk operations for posts
        Route::prefix('posts/bulk')->group(function () {
            Route::delete('/', [PostController::class, 'bulkDelete']);
            Route::put('/status', [PostController::class, 'bulkUpdateStatus']);
            Route::post('/schedule', [PostController::class, 'bulkSchedule']);
        });
    });

    // AI Content Generation (with rental limits + rate limit)
    Route::middleware(['rental.active', 'rental.limit:ai_generations', 'throttle:ai-generation'])->group(function () {
        Route::post('/posts/generate-content', [PostController::class, 'generateContent']);
        Route::post('/posts/generate-image', [PostController::class, 'generateImage']);
    });

    // Publishing (with rental limits + rate limit)
    Route::middleware(['rental.active', 'rental.limit:posts', 'throttle:publishing'])->group(function () {
        Route::post('/posts/{post}/publish', [PostController::class, 'publish']);
        Route::post('/posts/bulk/publish', [PostController::class, 'bulkPublish']);
    });

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

    // Export (CSV/JSON)
    Route::prefix('export')->group(function () {
        Route::get('/posts', [ExportController::class, 'exportPosts']);
        Route::get('/campaigns', [ExportController::class, 'exportCampaigns']);
        Route::get('/analytics', [ExportController::class, 'exportAnalytics']);
    });

    // Notifications
    Route::prefix('notifications')->group(function () {
        Route::get('/', [NotificationController::class, 'index']);
        Route::get('/unread-count', [NotificationController::class, 'unreadCount']);
        Route::post('/{id}/read', [NotificationController::class, 'markAsRead']);
        Route::post('/read-all', [NotificationController::class, 'markAllAsRead']);
        Route::delete('/{id}', [NotificationController::class, 'destroy']);
        Route::delete('/', [NotificationController::class, 'destroyAll']);
        Route::get('/preferences', [NotificationController::class, 'preferences']);
        Route::put('/preferences', [NotificationController::class, 'updatePreferences']);
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
        // Legacy endpoints (kept for backward compatibility)
        Route::get('/payments', [RentalController::class, 'adminPayments']);
        Route::post('/payments/{uuid}/verify', [RentalController::class, 'adminVerifyPayment']);
        Route::post('/payments/{uuid}/reject', [RentalController::class, 'adminRejectPayment']);
        Route::get('/stats', [RentalController::class, 'adminStats']);

        // User Rental Management - จัดการ rentals ของผู้ใช้
        Route::get('/', [AdminRentalController::class, 'index']);
        Route::get('/{id}', [AdminRentalController::class, 'show']);
        Route::put('/{id}', [AdminRentalController::class, 'update']);
        Route::post('/{id}/extend', [AdminRentalController::class, 'extend']);
        Route::post('/{id}/suspend', [AdminRentalController::class, 'suspend']);
        Route::post('/{id}/reactivate', [AdminRentalController::class, 'reactivate']);
        Route::post('/create-manual', [AdminRentalController::class, 'createManual']);

        // Refund Management - จัดการการคืนเงิน
        Route::post('/payments/{id}/refund', [AdminRentalController::class, 'processRefund']);
        Route::post('/refunds/{id}/complete', [AdminRentalController::class, 'completeRefund']);

        // Package Management - จัดการแพ็กเกจ
        Route::get('/packages/all', [AdminRentalController::class, 'listPackages']);
        Route::post('/packages', [AdminRentalController::class, 'createPackage']);
        Route::put('/packages/{id}', [AdminRentalController::class, 'updatePackage']);
        Route::post('/packages/{id}/toggle-active', [AdminRentalController::class, 'togglePackageActive']);

        // Reports & Analytics - รายงานและวิเคราะห์
        Route::get('/reports/revenue', [AdminRentalController::class, 'revenueReport']);
        Route::get('/reports/users', [AdminRentalController::class, 'userReport']);
        Route::get('/reports/dashboard', [AdminRentalController::class, 'dashboard']);
    });

    // Account Creation - ระบบสร้างบัญชี Social Media อัตโนมัติ
    Route::prefix('account-creation')->group(function () {
        // Tasks
        Route::get('/tasks', [AccountCreationController::class, 'listTasks']);
        Route::post('/tasks', [AccountCreationController::class, 'createTask']);
        Route::post('/tasks/bulk', [AccountCreationController::class, 'createBulkTasks']);
        Route::get('/tasks/{task}', [AccountCreationController::class, 'getTask']);
        Route::post('/tasks/{task}/retry', [AccountCreationController::class, 'retryTask']);
        Route::post('/tasks/{task}/cancel', [AccountCreationController::class, 'cancelTask']);

        // Statistics and Resources
        Route::get('/statistics', [AccountCreationController::class, 'getStatistics']);
        Route::get('/resources/check', [AccountCreationController::class, 'checkResources']);

        // Phone Numbers
        Route::get('/phone-numbers', [AccountCreationController::class, 'listPhoneNumbers']);
        Route::post('/phone-numbers', [AccountCreationController::class, 'addPhoneNumber']);
        Route::delete('/phone-numbers/{phone}', [AccountCreationController::class, 'deletePhoneNumber']);

        // Email Accounts
        Route::get('/email-accounts', [AccountCreationController::class, 'listEmailAccounts']);
        Route::post('/email-accounts', [AccountCreationController::class, 'addEmailAccount']);
        Route::delete('/email-accounts/{email}', [AccountCreationController::class, 'deleteEmailAccount']);

        // Proxy Servers
        Route::get('/proxies', [AccountCreationController::class, 'listProxies']);
        Route::post('/proxies', [AccountCreationController::class, 'addProxy']);
        Route::delete('/proxies/{proxy}', [AccountCreationController::class, 'deleteProxy']);
        Route::post('/proxies/{proxy}/test', [AccountCreationController::class, 'testProxy']);
    });

    // Web Learning - ระบบเรียนรู้และจดจำ workflow อัตโนมัติ
    Route::prefix('web-learning')->group(function () {
        // Workflows CRUD
        Route::get('/workflows', [WebLearningController::class, 'index']);
        Route::get('/workflows/{workflow}', [WebLearningController::class, 'show']);
        Route::put('/workflows/{workflow}', [WebLearningController::class, 'update']);
        Route::delete('/workflows/{workflow}', [WebLearningController::class, 'destroy']);
        Route::get('/workflows/{workflow}/steps', [WebLearningController::class, 'getSteps']);

        // Teaching Sessions - สอน workflow ใหม่ด้วยการสาธิต
        Route::post('/teaching/start', [WebLearningController::class, 'startTeachingSession']);
        Route::post('/teaching/{workflow}/record-step', [WebLearningController::class, 'recordStep']);
        Route::post('/teaching/{workflow}/complete', [WebLearningController::class, 'completeTeachingSession']);
        Route::post('/teaching/{workflow}/cancel', [WebLearningController::class, 'cancelTeachingSession']);

        // Workflow Testing - ทดสอบ workflow
        Route::post('/workflows/{workflow}/test', [WebLearningController::class, 'testWorkflow']);
        Route::get('/workflows/{workflow}/test-history', [WebLearningController::class, 'testHistory']);

        // Execution - รัน workflow
        Route::post('/workflows/{workflow}/execute', [WebLearningController::class, 'executeWorkflow']);
        Route::get('/executions', [WebLearningController::class, 'listExecutions']);
        Route::get('/executions/{execution}', [WebLearningController::class, 'getExecution']);
        Route::post('/executions/{execution}/cancel', [WebLearningController::class, 'cancelExecution']);

        // AI Analysis - วิเคราะห์หน้าเว็บด้วย AI
        Route::post('/ai/analyze-page', [WebLearningController::class, 'analyzePageWithAI']);
        Route::post('/ai/generate-workflow', [WebLearningController::class, 'generateWorkflowFromAI']);
        Route::post('/ai/suggest-selectors', [WebLearningController::class, 'suggestSelectors']);

        // Optimization - ปรับปรุง workflow
        Route::post('/workflows/{workflow}/optimize', [WebLearningController::class, 'optimizeWorkflow']);
        Route::post('/workflows/{workflow}/clone', [WebLearningController::class, 'cloneWorkflow']);
        Route::post('/workflows/{workflow}/merge', [WebLearningController::class, 'mergeWorkflows']);

        // Statistics
        Route::get('/statistics', [WebLearningController::class, 'statistics']);
        Route::get('/statistics/by-platform', [WebLearningController::class, 'statisticsByPlatform']);
    });

    // Web Learning Admin (admin only)
    Route::prefix('web-learning')->middleware(['role:admin'])->group(function () {
        Route::post('/workflows/import', [WebLearningController::class, 'importWorkflows']);
        Route::get('/workflows/export', [WebLearningController::class, 'exportWorkflows']);
        Route::post('/reset-learning', [WebLearningController::class, 'resetLearning']);
    });
});

// Internal API Routes - สำหรับ C# Core และ internal services
Route::prefix('internal')->middleware(['internal.auth'])->group(function () {
    // Usage Tracking - ติดตามการใช้งาน
    Route::post('/usage/increment', [UsageTrackingController::class, 'increment']);
    Route::post('/usage/check', [UsageTrackingController::class, 'check']);
    Route::get('/usage/{userId}', [UsageTrackingController::class, 'status']);

    // Validate API key
    Route::post('/validate-key', [UsageTrackingController::class, 'validateKey']);
});

// ═══════════════════════════════════════════════════════════════════════════════════
// WORKFLOW TEMPLATES & SEEK AND POST SYSTEM
// ═══════════════════════════════════════════════════════════════════════════════════

Route::prefix('v1')->group(function () {
    // Public Workflow Templates (read-only)
    Route::get('/workflow-templates', [WorkflowTemplateController::class, 'index']);
    Route::get('/workflow-templates/categories', [WorkflowTemplateController::class, 'byCategory']);
    Route::get('/workflow-templates/{template}', [WorkflowTemplateController::class, 'show']);
});

Route::prefix('v1')->middleware(['auth:sanctum', 'throttle:api'])->group(function () {
    // Workflow Templates Admin (admin only)
    Route::prefix('admin/workflow-templates')->middleware(['role:admin'])->group(function () {
        Route::post('/', [WorkflowTemplateController::class, 'store']);
        Route::put('/{template}', [WorkflowTemplateController::class, 'update']);
        Route::delete('/{template}', [WorkflowTemplateController::class, 'destroy']);
        Route::post('/{template}/toggle-active', [WorkflowTemplateController::class, 'toggleActive']);
        Route::get('/statistics', [WorkflowTemplateController::class, 'statistics']);
    });

    // User Workflows - Custom workflows created by users
    Route::prefix('workflows')->group(function () {
        Route::get('/', [UserWorkflowController::class, 'index']);
        Route::post('/', [UserWorkflowController::class, 'store']);
        Route::post('/from-template', [UserWorkflowController::class, 'createFromTemplate']);
        Route::get('/node-types', [UserWorkflowController::class, 'nodeTypes']);
        Route::post('/validate', [UserWorkflowController::class, 'validateWorkflow']);
        Route::get('/{workflow}', [UserWorkflowController::class, 'show']);
        Route::put('/{workflow}', [UserWorkflowController::class, 'update']);
        Route::delete('/{workflow}', [UserWorkflowController::class, 'destroy']);
        Route::post('/{workflow}/duplicate', [UserWorkflowController::class, 'duplicate']);
        Route::post('/{workflow}/toggle-active', [UserWorkflowController::class, 'toggleActive']);
        Route::post('/{workflow}/execute', [UserWorkflowController::class, 'execute']);
        Route::get('/{workflow}/executions', [UserWorkflowController::class, 'executions']);
    });

    // Workflow Executions
    Route::prefix('workflow-executions')->group(function () {
        Route::get('/{execution}', [UserWorkflowController::class, 'executionShow']);
        Route::post('/{execution}/cancel', [UserWorkflowController::class, 'cancelExecution']);
    });

    // Seek and Post - Intelligent Group Discovery and Automated Posting
    Route::prefix('seek-and-post')->group(function () {
        // Tasks
        Route::get('/', [SeekAndPostController::class, 'index']);
        Route::post('/', [SeekAndPostController::class, 'store']);
        Route::get('/statistics', [SeekAndPostController::class, 'statistics']);
        Route::get('/{task}', [SeekAndPostController::class, 'show']);
        Route::put('/{task}', [SeekAndPostController::class, 'update']);
        Route::delete('/{task}', [SeekAndPostController::class, 'destroy']);
        Route::post('/{task}/start', [SeekAndPostController::class, 'start']);
        Route::post('/{task}/pause', [SeekAndPostController::class, 'pause']);
        Route::post('/{task}/resume', [SeekAndPostController::class, 'resume']);

        // Groups
        Route::get('/groups/list', [SeekAndPostController::class, 'groups']);
        Route::get('/groups/search', [SeekAndPostController::class, 'searchGroups']);
        Route::get('/groups/recommended', [SeekAndPostController::class, 'recommendedGroups']);
        Route::get('/groups/statistics', [SeekAndPostController::class, 'groupStatistics']);
        Route::get('/groups/{group}', [SeekAndPostController::class, 'groupShow']);
    });

    // ═══════════════════════════════════════════════════════════════════════════════════
    // COMMENT MANAGEMENT & AUTO-REPLY SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════════════

    // Post Comments - อ่านและตอบคอมเมนต์อัตโนมัติ
    Route::prefix('posts/{post}/comments')->group(function () {
        Route::get('/', [CommentController::class, 'index']);
        Route::post('/fetch', [CommentController::class, 'fetch']);
        Route::post('/auto-reply', [CommentController::class, 'autoReply']);
        Route::get('/stats', [CommentController::class, 'stats']);
    });

    // Individual Comment Actions
    Route::prefix('comments/{comment}')->group(function () {
        Route::post('/reply', [CommentController::class, 'reply']);
        Route::post('/analyze', [CommentController::class, 'analyzeSentiment']);
        Route::post('/skip', [CommentController::class, 'skip']);
    });

    // ═══════════════════════════════════════════════════════════════════════════════════
    // RESPONSE TONE & PERSONALITY CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════════════

    // Response Tones - กำหนดบุคลิก/โทนในการตอบ
    Route::prefix('response-tones')->group(function () {
        Route::get('/', [ResponseToneController::class, 'index']);
        Route::get('/presets', [ResponseToneController::class, 'presets']);
        Route::post('/', [ResponseToneController::class, 'store']);
        Route::get('/{responseTone}', [ResponseToneController::class, 'show']);
        Route::put('/{responseTone}', [ResponseToneController::class, 'update']);
        Route::delete('/{responseTone}', [ResponseToneController::class, 'destroy']);
        Route::post('/{responseTone}/clone', [ResponseToneController::class, 'clone']);
        Route::post('/{responseTone}/test', [ResponseToneController::class, 'test']);
    });

    // ═══════════════════════════════════════════════════════════════════════════════════
    // VIRAL ANALYSIS & TRENDING KEYWORDS
    // ═══════════════════════════════════════════════════════════════════════════════════

    // Viral Analysis - วิเคราะห์และทำนายเนื้อหา Viral
    Route::prefix('viral')->group(function () {
        Route::get('/dashboard', [ViralAnalysisController::class, 'dashboard']);
        Route::post('/analyze', [ViralAnalysisController::class, 'analyzeContent']);
        Route::get('/keywords/trending', [ViralAnalysisController::class, 'trendingKeywords']);
        Route::post('/keywords/track', [ViralAnalysisController::class, 'trackKeyword']);
        Route::post('/keywords/update', [ViralAnalysisController::class, 'updateKeywordMetrics']);
        Route::get('/posts/{post}/analyze', [ViralAnalysisController::class, 'analyzePost']);
        Route::get('/posts/{post}/velocity', [ViralAnalysisController::class, 'velocity']);
        Route::get('/suggestions', [ViralAnalysisController::class, 'suggestions']);
        Route::post('/compare', [ViralAnalysisController::class, 'compare']);
    });

    // ═══════════════════════════════════════════════════════════════════════════════════
    // PAYMENT GATEWAY - SMS-Based Payment System
    // ═══════════════════════════════════════════════════════════════════════════════════

    // Payment Gateway - ระบบรับชำระเงินผ่าน SMS
    Route::prefix('payment-gateway')->group(function () {
        // Dashboard
        Route::get('/dashboard', [PaymentGatewayController::class, 'dashboard']);
        Route::get('/statistics', [PaymentGatewayController::class, 'statistics']);

        // Payments (SMS-detected)
        Route::get('/payments', [PaymentGatewayController::class, 'payments']);
        Route::get('/payments/{id}', [PaymentGatewayController::class, 'showPayment']);
        Route::post('/payments/{id}/approve', [PaymentGatewayController::class, 'approvePayment']);
        Route::post('/payments/{id}/reject', [PaymentGatewayController::class, 'rejectPayment']);
        Route::post('/payments/{id}/match', [PaymentGatewayController::class, 'matchPayment']);
        Route::post('/payments/{id}/link', [PaymentGatewayController::class, 'linkPaymentToOrder']);

        // Orders
        Route::get('/orders', [PaymentGatewayController::class, 'orders']);
        Route::post('/orders', [PaymentGatewayController::class, 'createOrder']);
        Route::get('/orders/{id}', [PaymentGatewayController::class, 'showOrder']);
        Route::post('/orders/{id}/cancel', [PaymentGatewayController::class, 'cancelOrder']);

        // Mobile Devices
        Route::get('/devices', [PaymentGatewayController::class, 'devices']);
    });
});

// ═══════════════════════════════════════════════════════════════════════════════════
// PAYMENT GATEWAY - Mobile Device API (No Auth Required)
// ═══════════════════════════════════════════════════════════════════════════════════

Route::prefix('v1/payment-gateway/mobile')->middleware('throttle:mobile')->group(function () {
    // Device Registration & Heartbeat
    Route::post('/register', [PaymentGatewayController::class, 'registerDevice']);
    Route::post('/heartbeat', [PaymentGatewayController::class, 'deviceHeartbeat']);

    // SMS Payment Submission
    Route::post('/submit-payment', [PaymentGatewayController::class, 'submitSmsPayment']);
});

// ═══════════════════════════════════════════════════════════════════════════════════
// SMS GATEWAY WEBHOOK - Multi-Website Integration (No Auth - Uses HMAC Signature)
// ═══════════════════════════════════════════════════════════════════════════════════

Route::prefix('v1/sms-gateway')->middleware('throttle:webhooks')->group(function () {
    // Universal Webhook Endpoint
    // Security: API Key + Secret Key + HMAC-SHA256 Signature
    Route::post('/webhook', [PaymentGatewayController::class, 'smsWebhook']);
});

Route::prefix('v1/sms-gateway')->middleware(['auth:sanctum', 'throttle:api'])->group(function () {
    // Generate API Credentials for Mobile App integration
    Route::post('/generate-credentials', [PaymentGatewayController::class, 'generateApiCredentials']);
});

// ═══════════════════════════════════════════════════════════════════════════════════
// RBAC & AUDIT LOG - Role-Based Access Control and Activity Logging
// ═══════════════════════════════════════════════════════════════════════════════════

use App\Http\Controllers\Api\RoleController;
use App\Http\Controllers\Api\AuditLogController;

Route::prefix('v1')->middleware(['auth:sanctum', 'throttle:api'])->group(function () {
    // Roles Management (admin only)
    Route::prefix('admin/roles')->middleware(['role:admin'])->group(function () {
        Route::get('/', [RoleController::class, 'index']);
        Route::get('/permissions', [RoleController::class, 'permissions']);
        Route::post('/', [RoleController::class, 'store']);
        Route::get('/{role}', [RoleController::class, 'show']);
        Route::put('/{role}', [RoleController::class, 'update']);
        Route::delete('/{role}', [RoleController::class, 'destroy']);
        Route::post('/{role}/assign', [RoleController::class, 'assignToUser']);
        Route::post('/{role}/remove', [RoleController::class, 'removeFromUser']);
    });

    // Audit Logs (admin only)
    Route::prefix('admin/audit-logs')->middleware(['role:admin'])->group(function () {
        Route::get('/', [AuditLogController::class, 'index']);
        Route::get('/stats', [AuditLogController::class, 'stats']);
        Route::get('/log-names', [AuditLogController::class, 'logNames']);
        Route::get('/export', [AuditLogController::class, 'export']);
        Route::get('/user/{userId}', [AuditLogController::class, 'userActivity']);
        Route::get('/{activity}', [AuditLogController::class, 'show']);
    });

    // User's own activity (for regular users)
    Route::get('/my-activity', function () {
        return app(AuditLogController::class)->userActivity(request(), auth()->id());
    });
});
