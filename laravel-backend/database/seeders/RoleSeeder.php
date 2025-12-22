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

        // Create permissions
        $permissions = [
            // User permissions
            'view brands',
            'create brands',
            'edit brands',
            'delete brands',
            'view posts',
            'create posts',
            'edit posts',
            'delete posts',
            'publish posts',
            'view campaigns',
            'create campaigns',
            'edit campaigns',
            'delete campaigns',
            'view social accounts',
            'manage social accounts',
            'view analytics',
            'generate ai content',
            'use web automation',
            // Admin permissions
            'view users',
            'manage users',
            'verify payments',
            'manage rentals',
            'manage promo codes',
            'view system stats',
            'manage ai manager',
        ];

        foreach ($permissions as $permission) {
            Permission::firstOrCreate(['name' => $permission]);
        }

        // Create roles and assign permissions
        $adminRole = Role::firstOrCreate(['name' => 'admin']);
        $adminRole->givePermissionTo(Permission::all());

        $userRole = Role::firstOrCreate(['name' => 'user']);
        $userRole->givePermissionTo([
            'view brands',
            'create brands',
            'edit brands',
            'delete brands',
            'view posts',
            'create posts',
            'edit posts',
            'delete posts',
            'publish posts',
            'view campaigns',
            'create campaigns',
            'edit campaigns',
            'delete campaigns',
            'view social accounts',
            'manage social accounts',
            'view analytics',
            'generate ai content',
        ]);

        // Premium user role with web automation
        $premiumRole = Role::firstOrCreate(['name' => 'premium']);
        $premiumRole->givePermissionTo([
            'view brands',
            'create brands',
            'edit brands',
            'delete brands',
            'view posts',
            'create posts',
            'edit posts',
            'delete posts',
            'publish posts',
            'view campaigns',
            'create campaigns',
            'edit campaigns',
            'delete campaigns',
            'view social accounts',
            'manage social accounts',
            'view analytics',
            'generate ai content',
            'use web automation',
        ]);
    }
}
