<?php

namespace App\Policies;

use App\Models\Brand;
use App\Models\User;
use Illuminate\Auth\Access\HandlesAuthorization;

class BrandPolicy
{
    use HandlesAuthorization;

    /**
     * Determine whether the user can view any brands.
     */
    public function viewAny(User $user): bool
    {
        return true;
    }

    /**
     * Determine whether the user can view the brand.
     */
    public function view(User $user, Brand $brand): bool
    {
        return $user->id === $brand->user_id || $user->hasRole('admin');
    }

    /**
     * Determine whether the user can create brands.
     */
    public function create(User $user): bool
    {
        // Check quota
        $quota = $user->getUsageQuota();
        if ($quota['brands'] === -1) {
            return true;
        }

        $currentBrands = Brand::where('user_id', $user->id)->count();
        return $currentBrands < $quota['brands'];
    }

    /**
     * Determine whether the user can update the brand.
     */
    public function update(User $user, Brand $brand): bool
    {
        return $user->id === $brand->user_id || $user->hasRole('admin');
    }

    /**
     * Determine whether the user can delete the brand.
     */
    public function delete(User $user, Brand $brand): bool
    {
        return $user->id === $brand->user_id || $user->hasRole('admin');
    }

    /**
     * Determine whether the user can restore the brand.
     */
    public function restore(User $user, Brand $brand): bool
    {
        return $user->id === $brand->user_id || $user->hasRole('admin');
    }

    /**
     * Determine whether the user can permanently delete the brand.
     */
    public function forceDelete(User $user, Brand $brand): bool
    {
        return $user->hasRole('admin');
    }
}
