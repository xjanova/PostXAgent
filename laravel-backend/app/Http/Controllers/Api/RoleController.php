<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Spatie\Permission\Models\Role;
use Spatie\Permission\Models\Permission;

class RoleController extends Controller
{
    /**
     * Get all roles with their permissions
     */
    public function index(): JsonResponse
    {
        $roles = Role::with('permissions')->get();

        return response()->json([
            'success' => true,
            'data' => $roles->map(fn ($role) => [
                'id' => $role->id,
                'name' => $role->name,
                'description' => $role->description,
                'permissions' => $role->permissions->pluck('name'),
                'users_count' => $role->users()->count(),
            ]),
        ]);
    }

    /**
     * Get a specific role
     */
    public function show(Role $role): JsonResponse
    {
        $role->load('permissions');

        return response()->json([
            'success' => true,
            'data' => [
                'id' => $role->id,
                'name' => $role->name,
                'description' => $role->description,
                'permissions' => $role->permissions->pluck('name'),
                'users' => $role->users()->select('id', 'name', 'email')->get(),
            ],
        ]);
    }

    /**
     * Create a new role
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:125|unique:roles,name',
            'description' => 'nullable|string|max:255',
            'permissions' => 'nullable|array',
            'permissions.*' => 'string|exists:permissions,name',
        ]);

        $role = Role::create([
            'name' => $validated['name'],
            'description' => $validated['description'] ?? null,
        ]);

        if (!empty($validated['permissions'])) {
            $role->syncPermissions($validated['permissions']);
        }

        return response()->json([
            'success' => true,
            'message' => 'สร้าง Role สำเร็จ',
            'data' => [
                'id' => $role->id,
                'name' => $role->name,
                'permissions' => $role->permissions->pluck('name'),
            ],
        ], 201);
    }

    /**
     * Update a role
     */
    public function update(Request $request, Role $role): JsonResponse
    {
        // Prevent updating protected roles
        if (in_array($role->name, ['admin', 'super-admin'])) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่สามารถแก้ไข Role นี้ได้',
            ], 403);
        }

        $validated = $request->validate([
            'name' => 'sometimes|string|max:125|unique:roles,name,' . $role->id,
            'description' => 'nullable|string|max:255',
            'permissions' => 'nullable|array',
            'permissions.*' => 'string|exists:permissions,name',
        ]);

        $role->update([
            'name' => $validated['name'] ?? $role->name,
            'description' => $validated['description'] ?? $role->description,
        ]);

        if (isset($validated['permissions'])) {
            $role->syncPermissions($validated['permissions']);
        }

        return response()->json([
            'success' => true,
            'message' => 'อัพเดท Role สำเร็จ',
            'data' => [
                'id' => $role->id,
                'name' => $role->name,
                'permissions' => $role->permissions->pluck('name'),
            ],
        ]);
    }

    /**
     * Delete a role
     */
    public function destroy(Role $role): JsonResponse
    {
        // Prevent deleting protected roles
        if (in_array($role->name, ['admin', 'super-admin', 'user', 'premium'])) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่สามารถลบ Role นี้ได้',
            ], 403);
        }

        $role->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบ Role สำเร็จ',
        ]);
    }

    /**
     * Get all available permissions
     */
    public function permissions(): JsonResponse
    {
        $permissions = Permission::all()->groupBy(function ($permission) {
            // Group by resource (first word)
            $parts = explode(' ', $permission->name);
            return $parts[count($parts) - 1] ?? 'general';
        });

        return response()->json([
            'success' => true,
            'data' => $permissions->map(fn ($perms) => $perms->pluck('name')),
        ]);
    }

    /**
     * Assign role to user
     */
    public function assignToUser(Request $request, Role $role): JsonResponse
    {
        $validated = $request->validate([
            'user_id' => 'required|exists:users,id',
        ]);

        $user = \App\Models\User::findOrFail($validated['user_id']);
        $user->assignRole($role);

        activity()
            ->causedBy(auth()->user())
            ->performedOn($user)
            ->withProperties(['role' => $role->name])
            ->log('assigned_role');

        return response()->json([
            'success' => true,
            'message' => "กำหนด Role '{$role->name}' ให้ผู้ใช้สำเร็จ",
        ]);
    }

    /**
     * Remove role from user
     */
    public function removeFromUser(Request $request, Role $role): JsonResponse
    {
        $validated = $request->validate([
            'user_id' => 'required|exists:users,id',
        ]);

        $user = \App\Models\User::findOrFail($validated['user_id']);
        $user->removeRole($role);

        activity()
            ->causedBy(auth()->user())
            ->performedOn($user)
            ->withProperties(['role' => $role->name])
            ->log('removed_role');

        return response()->json([
            'success' => true,
            'message' => "ลบ Role '{$role->name}' จากผู้ใช้สำเร็จ",
        ]);
    }
}
