<?php

namespace App\Providers;

use Illuminate\Support\ServiceProvider;
use Illuminate\Cache\RateLimiting\Limit;
use Illuminate\Support\Facades\RateLimiter;
use Illuminate\Http\Request;
use App\Services\AIManagerClient;
use App\Services\AIManagerConnectionStatus;

class AppServiceProvider extends ServiceProvider
{
    /**
     * Register any application services.
     */
    public function register(): void
    {
        $this->app->singleton(AIManagerClient::class, function ($app) {
            return new AIManagerClient();
        });

        $this->app->singleton(AIManagerConnectionStatus::class, function ($app) {
            return new AIManagerConnectionStatus(
                $app->make(AIManagerClient::class)
            );
        });
    }

    /**
     * Bootstrap any application services.
     */
    public function boot(): void
    {
        $this->configureRateLimiting();
    }

    /**
     * Configure API rate limiting.
     */
    protected function configureRateLimiting(): void
    {
        // Default API rate limit: 60 requests per minute
        RateLimiter::for('api', function (Request $request) {
            return Limit::perMinute(60)->by($request->user()?->id ?: $request->ip());
        });

        // Strict rate limit for authentication endpoints: 5 requests per minute
        RateLimiter::for('auth', function (Request $request) {
            return Limit::perMinute(5)->by($request->ip());
        });

        // Rate limit for content generation (AI): 10 requests per minute
        RateLimiter::for('ai-generation', function (Request $request) {
            return Limit::perMinute(10)->by($request->user()?->id ?: $request->ip());
        });

        // Rate limit for publishing posts: 30 requests per minute
        RateLimiter::for('publishing', function (Request $request) {
            return Limit::perMinute(30)->by($request->user()?->id ?: $request->ip());
        });

        // Rate limit for webhooks: 100 requests per minute
        RateLimiter::for('webhooks', function (Request $request) {
            return Limit::perMinute(100)->by($request->ip());
        });

        // Relaxed rate limit for status checks: 120 requests per minute
        RateLimiter::for('status', function (Request $request) {
            return Limit::perMinute(120)->by($request->ip());
        });

        // Rate limit for mobile device API: 60 requests per minute per device
        RateLimiter::for('mobile', function (Request $request) {
            $deviceId = $request->input('device_id') ?? $request->ip();
            return Limit::perMinute(60)->by($deviceId);
        });
    }
}
