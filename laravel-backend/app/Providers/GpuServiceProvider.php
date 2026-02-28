<?php

declare(strict_types=1);

namespace App\Providers;

use App\Services\Gpu\GpuProviderFactory;
use App\Services\Gpu\GpuAccountRotationService;
use App\Services\Gpu\GpuProvisioningService;
use App\Services\Gpu\VastAiProvider;
use App\Services\Gpu\RunPodProvider;
use Illuminate\Support\ServiceProvider;

class GpuServiceProvider extends ServiceProvider
{
    /**
     * Register services.
     */
    public function register(): void
    {
        // Register GPU Provider Factory as singleton
        $this->app->singleton(GpuProviderFactory::class, function ($app) {
            $factory = new GpuProviderFactory();

            // Register default providers
            $factory->registerProvider(new VastAiProvider());
            $factory->registerProvider(new RunPodProvider());

            return $factory;
        });

        // Register GPU Account Rotation Service as singleton
        $this->app->singleton(GpuAccountRotationService::class, function ($app) {
            return new GpuAccountRotationService(
                $app->make(GpuProviderFactory::class)
            );
        });

        // Register GPU Provisioning Service as singleton
        $this->app->singleton(GpuProvisioningService::class, function ($app) {
            return new GpuProvisioningService(
                $app->make(GpuProviderFactory::class),
                $app->make(GpuAccountRotationService::class)
            );
        });
    }

    /**
     * Bootstrap services.
     */
    public function boot(): void
    {
        // Schedule daily counter reset
        if ($this->app->runningInConsole()) {
            $this->app->booted(function () {
                $schedule = $this->app->make(\Illuminate\Console\Scheduling\Schedule::class);

                $schedule->call(function () {
                    app(GpuAccountRotationService::class)->resetDailyCounters();
                })->dailyAt('00:00')->name('gpu-reset-daily-counters')->withoutOverlapping();

                // Refresh balances every hour
                $schedule->call(function () {
                    $this->refreshAllPoolBalances();
                })->hourly()->name('gpu-refresh-balances')->withoutOverlapping();
            });
        }
    }

    /**
     * Refresh balances for all active pools
     */
    private function refreshAllPoolBalances(): void
    {
        $pools = \App\Models\GpuAccountPool::where('is_active', true)->get();
        $rotationService = app(GpuAccountRotationService::class);

        foreach ($pools as $pool) {
            try {
                $rotationService->refreshPoolBalances($pool);
            } catch (\Exception $e) {
                \Illuminate\Support\Facades\Log::error('Failed to refresh GPU pool balances', [
                    'pool_id' => $pool->id,
                    'error' => $e->getMessage(),
                ]);
            }
        }
    }
}
