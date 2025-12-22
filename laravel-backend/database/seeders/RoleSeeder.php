<?php

namespace Database\Seeders;

use Illuminate\Database\Seeder;
use Spatie\Permission\Models\Role;
use Spatie\Permission\Models\Permission;

class RoleSeeder extends Seeder
{
    /**
     * Run the database seeds.
     */
    public function run(): void
    {
        // Reset cached roles and permissions
        app()[\Spatie\Permission\PermissionRegistrar::class]->forgetCachedPermissions();

        // Create permissions by category
        $permissions = [
            // Brand permissions
            'view brands',
            'create brands',
            'edit brands',
            'delete brands',

            // Post permissions
            'view posts',
            'create posts',
            'edit posts',
            'delete posts',
            'publish posts',
            'schedule posts',

            // Campaign permissions
            'view campaigns',
            'create campaigns',
            'edit campaigns',
            'delete campaigns',
            'run campaigns',

            // Social Account permissions
            'view social accounts',
            'manage social accounts',
            'connect social accounts',

            // Analytics permissions
            'view analytics',
            'export analytics',

            // AI permissions
            'generate ai content',
            'generate ai images',
            'use ai assistant',

            // Automation permissions
            'use web automation',
            'create workflows',
            'manage workflows',
            'use seek and post',

            // Comment permissions
            'view comments',
            'reply comments',
            'manage comments',

            // Account Pool permissions
            'view account pools',
            'manage account pools',

            // User management (admin)
            'view users',
            'create users',
            'edit users',
            'delete users',
            'manage users',
            'assign roles',

            // Payment/Rental management (admin)
            'view payments',
            'verify payments',
            'manage rentals',
            'manage promo codes',
            'process refunds',

            // System permissions (admin)
            'view system stats',
            'manage ai manager',
            'manage settings',
            'view audit logs',
            'export audit logs',

            // Role management (super-admin)
            'manage roles',
            'manage permissions',
        ];

        foreach ($permissions as $permission) {
            Permission::firstOrCreate(['name' => $permission]);
        }

        // SUPER ADMIN - Full system access
        $superAdminRole = Role::firstOrCreate([
            'name' => 'super-admin',
            'guard_name' => 'web',
        ]);
        $superAdminRole->givePermissionTo(Permission::all());

        // ADMIN - System management without role management
        $adminRole = Role::firstOrCreate([
            'name' => 'admin',
            'guard_name' => 'web',
        ]);
        $adminRole->syncPermissions(
            Permission::whereNotIn('name', ['manage roles', 'manage permissions'])->get()
        );

        // MODERATOR - Content moderation
        $moderatorRole = Role::firstOrCreate([
            'name' => 'moderator',
            'guard_name' => 'web',
        ]);
        $moderatorRole->syncPermissions([
            'view brands',
            'view posts',
            'edit posts',
            'delete posts',
            'view campaigns',
            'view comments',
            'reply comments',
            'manage comments',
            'view users',
            'view analytics',
            'view system stats',
        ]);

        // PREMIUM - Full user features including automation
        $premiumRole = Role::firstOrCreate([
            'name' => 'premium',
            'guard_name' => 'web',
        ]);
        $premiumRole->syncPermissions([
            'view brands', 'create brands', 'edit brands', 'delete brands',
            'view posts', 'create posts', 'edit posts', 'delete posts', 'publish posts', 'schedule posts',
            'view campaigns', 'create campaigns', 'edit campaigns', 'delete campaigns', 'run campaigns',
            'view social accounts', 'manage social accounts', 'connect social accounts',
            'view analytics', 'export analytics',
            'generate ai content', 'generate ai images', 'use ai assistant',
            'use web automation', 'create workflows', 'manage workflows', 'use seek and post',
            'view comments', 'reply comments', 'manage comments',
            'view account pools', 'manage account pools',
        ]);

        // USER - Basic features
        $userRole = Role::firstOrCreate([
            'name' => 'user',
            'guard_name' => 'web',
        ]);
        $userRole->syncPermissions([
            'view brands', 'create brands', 'edit brands', 'delete brands',
            'view posts', 'create posts', 'edit posts', 'delete posts', 'publish posts', 'schedule posts',
            'view campaigns', 'create campaigns', 'edit campaigns', 'delete campaigns',
            'view social accounts', 'manage social accounts', 'connect social accounts',
            'view analytics',
            'generate ai content',
            'view comments', 'reply comments',
        ]);

        // TRIAL - Limited trial features
        $trialRole = Role::firstOrCreate([
            'name' => 'trial',
            'guard_name' => 'web',
        ]);
        $trialRole->syncPermissions([
            'view brands', 'create brands',
            'view posts', 'create posts', 'edit posts',
            'view campaigns',
            'view social accounts', 'connect social accounts',
            'view analytics',
            'generate ai content',
        ]);
    }
}
