<?php

namespace Tests\Feature;

use App\Models\User;
use Illuminate\Foundation\Testing\RefreshDatabase;
use Spatie\Permission\Models\Role;
use Spatie\Permission\Models\Permission;
use Tests\TestCase;

class RoleControllerTest extends TestCase
{
    use RefreshDatabase;

    private User $admin;
    private User $user;

    protected function setUp(): void
    {
        parent::setUp();

        // Create permissions
        Permission::create(['name' => 'view brands']);
        Permission::create(['name' => 'create brands']);
        Permission::create(['name' => 'manage users']);

        // Create admin role
        $adminRole = Role::create(['name' => 'admin']);
        $adminRole->givePermissionTo(Permission::all());

        // Create user role
        $userRole = Role::create(['name' => 'user']);
        $userRole->givePermissionTo(['view brands', 'create brands']);

        // Create users
        $this->admin = User::factory()->create();
        $this->admin->assignRole('admin');

        $this->user = User::factory()->create();
        $this->user->assignRole('user');
    }

    public function test_admin_can_list_roles(): void
    {
        $response = $this->actingAs($this->admin, 'sanctum')
            ->getJson('/api/v1/admin/roles');

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data' => [
                    '*' => ['id', 'name', 'permissions'],
                ],
            ]);
    }

    public function test_non_admin_cannot_list_roles(): void
    {
        $response = $this->actingAs($this->user, 'sanctum')
            ->getJson('/api/v1/admin/roles');

        $response->assertStatus(403);
    }

    public function test_admin_can_view_role(): void
    {
        $role = Role::where('name', 'user')->first();

        $response = $this->actingAs($this->admin, 'sanctum')
            ->getJson("/api/v1/admin/roles/{$role->id}");

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => ['name' => 'user'],
            ]);
    }

    public function test_admin_can_create_role(): void
    {
        $response = $this->actingAs($this->admin, 'sanctum')
            ->postJson('/api/v1/admin/roles', [
                'name' => 'moderator',
                'description' => 'Content moderator',
                'permissions' => ['view brands'],
            ]);

        $response->assertStatus(201)
            ->assertJson([
                'success' => true,
                'data' => ['name' => 'moderator'],
            ]);

        $this->assertDatabaseHas('roles', ['name' => 'moderator']);
    }

    public function test_admin_can_update_role(): void
    {
        $role = Role::create(['name' => 'test-role']);

        $response = $this->actingAs($this->admin, 'sanctum')
            ->putJson("/api/v1/admin/roles/{$role->id}", [
                'name' => 'updated-role',
                'permissions' => ['view brands', 'create brands'],
            ]);

        $response->assertStatus(200)
            ->assertJson([
                'success' => true,
                'data' => ['name' => 'updated-role'],
            ]);
    }

    public function test_cannot_update_protected_role(): void
    {
        $role = Role::where('name', 'admin')->first();

        $response = $this->actingAs($this->admin, 'sanctum')
            ->putJson("/api/v1/admin/roles/{$role->id}", [
                'name' => 'super-admin-changed',
            ]);

        $response->assertStatus(403);
    }

    public function test_admin_can_delete_custom_role(): void
    {
        $role = Role::create(['name' => 'custom-role']);

        $response = $this->actingAs($this->admin, 'sanctum')
            ->deleteJson("/api/v1/admin/roles/{$role->id}");

        $response->assertStatus(200)
            ->assertJson(['success' => true]);

        $this->assertDatabaseMissing('roles', ['name' => 'custom-role']);
    }

    public function test_cannot_delete_protected_role(): void
    {
        $role = Role::where('name', 'admin')->first();

        $response = $this->actingAs($this->admin, 'sanctum')
            ->deleteJson("/api/v1/admin/roles/{$role->id}");

        $response->assertStatus(403);
    }

    public function test_admin_can_list_permissions(): void
    {
        $response = $this->actingAs($this->admin, 'sanctum')
            ->getJson('/api/v1/admin/roles/permissions');

        $response->assertStatus(200)
            ->assertJsonStructure([
                'success',
                'data',
            ]);
    }

    public function test_admin_can_assign_role_to_user(): void
    {
        $role = Role::where('name', 'user')->first();
        $newUser = User::factory()->create();

        $response = $this->actingAs($this->admin, 'sanctum')
            ->postJson("/api/v1/admin/roles/{$role->id}/assign", [
                'user_id' => $newUser->id,
            ]);

        $response->assertStatus(200)
            ->assertJson(['success' => true]);

        $this->assertTrue($newUser->fresh()->hasRole('user'));
    }

    public function test_admin_can_remove_role_from_user(): void
    {
        $role = Role::where('name', 'user')->first();

        $response = $this->actingAs($this->admin, 'sanctum')
            ->postJson("/api/v1/admin/roles/{$role->id}/remove", [
                'user_id' => $this->user->id,
            ]);

        $response->assertStatus(200)
            ->assertJson(['success' => true]);

        $this->assertFalse($this->user->fresh()->hasRole('user'));
    }

    public function test_role_name_must_be_unique(): void
    {
        $response = $this->actingAs($this->admin, 'sanctum')
            ->postJson('/api/v1/admin/roles', [
                'name' => 'admin', // Already exists
            ]);

        $response->assertStatus(422)
            ->assertJsonValidationErrors(['name']);
    }
}
