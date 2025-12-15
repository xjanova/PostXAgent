<?php

namespace App\Providers;

use Illuminate\Support\ServiceProvider;
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
        //
    }
}
