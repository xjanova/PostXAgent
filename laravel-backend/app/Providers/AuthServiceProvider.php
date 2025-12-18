<?php

namespace App\Providers;

use App\Models\Post;
use App\Models\Brand;
use App\Models\Campaign;
use App\Policies\PostPolicy;
use App\Policies\BrandPolicy;
use App\Policies\CampaignPolicy;
use Illuminate\Foundation\Support\Providers\AuthServiceProvider as ServiceProvider;
use Illuminate\Support\Facades\Gate;

class AuthServiceProvider extends ServiceProvider
{
    /**
     * The model to policy mappings for the application.
     *
     * @var array<class-string, class-string>
     */
    protected $policies = [
        Post::class => PostPolicy::class,
        Brand::class => BrandPolicy::class,
        Campaign::class => CampaignPolicy::class,
    ];

    /**
     * Register any authentication / authorization services.
     */
    public function boot(): void
    {
        $this->registerPolicies();

        // Define gates for admin actions
        Gate::define('admin', function ($user) {
            return $user->hasRole('admin');
        });

        Gate::define('manage-ai-manager', function ($user) {
            return $user->hasRole('admin');
        });

        Gate::define('verify-payments', function ($user) {
            return $user->hasRole('admin');
        });

        Gate::define('manage-users', function ($user) {
            return $user->hasRole('admin');
        });
    }
}
