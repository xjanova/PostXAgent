<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     * Based on Spatie Permission package requirements
     */
    public function up(): void
    {
        // Always use hardcoded values to avoid config loading issues in tests
        $tableNames = [
            'roles' => 'roles',
            'permissions' => 'permissions',
            'model_has_permissions' => 'model_has_permissions',
            'model_has_roles' => 'model_has_roles',
            'role_has_permissions' => 'role_has_permissions',
        ];

        $columnNames = [
            'role_pivot_key' => 'role_id',
            'permission_pivot_key' => 'permission_id',
            'model_morph_key' => 'model_id',
            'team_foreign_key' => 'team_id',
        ];

        $teams = false;

        // Permissions table
        Schema::create($tableNames['permissions'] ?? 'permissions', function (Blueprint $table) {
            $table->id();
            $table->string('name', 125);
            $table->string('guard_name', 125)->default('web');
            $table->string('description')->nullable();
            $table->timestamps();

            $table->unique(['name', 'guard_name']);
        });

        // Roles table
        Schema::create($tableNames['roles'] ?? 'roles', function (Blueprint $table) use ($teams, $columnNames) {
            $table->id();
            if ($teams) {
                $table->unsignedBigInteger($columnNames['team_foreign_key'])->nullable();
                $table->index($columnNames['team_foreign_key'], 'roles_team_foreign_key_index');
            }
            $table->string('name', 125);
            $table->string('guard_name', 125)->default('web');
            $table->string('description')->nullable();
            $table->timestamps();

            if ($teams) {
                $table->unique([$columnNames['team_foreign_key'], 'name', 'guard_name']);
            } else {
                $table->unique(['name', 'guard_name']);
            }
        });

        // Model has permissions pivot table
        Schema::create($tableNames['model_has_permissions'] ?? 'model_has_permissions', function (Blueprint $table) use ($tableNames, $columnNames, $teams) {
            $table->unsignedBigInteger($columnNames['permission_pivot_key']);
            $table->string('model_type');
            $table->unsignedBigInteger($columnNames['model_morph_key']);

            $table->index([$columnNames['model_morph_key'], 'model_type'], 'model_has_permissions_model_id_model_type_index');

            $table->foreign($columnNames['permission_pivot_key'])
                ->references('id')
                ->on($tableNames['permissions'] ?? 'permissions')
                ->onDelete('cascade');

            if ($teams) {
                $table->unsignedBigInteger($columnNames['team_foreign_key']);
                $table->index($columnNames['team_foreign_key'], 'model_has_permissions_team_foreign_key_index');
                $table->primary([$columnNames['permission_pivot_key'], $columnNames['model_morph_key'], 'model_type', $columnNames['team_foreign_key']],
                    'model_has_permissions_permission_model_type_primary');
            } else {
                $table->primary([$columnNames['permission_pivot_key'], $columnNames['model_morph_key'], 'model_type'],
                    'model_has_permissions_permission_model_type_primary');
            }
        });

        // Model has roles pivot table
        Schema::create($tableNames['model_has_roles'] ?? 'model_has_roles', function (Blueprint $table) use ($tableNames, $columnNames, $teams) {
            $table->unsignedBigInteger($columnNames['role_pivot_key']);
            $table->string('model_type');
            $table->unsignedBigInteger($columnNames['model_morph_key']);

            $table->index([$columnNames['model_morph_key'], 'model_type'], 'model_has_roles_model_id_model_type_index');

            $table->foreign($columnNames['role_pivot_key'])
                ->references('id')
                ->on($tableNames['roles'] ?? 'roles')
                ->onDelete('cascade');

            if ($teams) {
                $table->unsignedBigInteger($columnNames['team_foreign_key']);
                $table->index($columnNames['team_foreign_key'], 'model_has_roles_team_foreign_key_index');
                $table->primary([$columnNames['role_pivot_key'], $columnNames['model_morph_key'], 'model_type', $columnNames['team_foreign_key']],
                    'model_has_roles_role_model_type_primary');
            } else {
                $table->primary([$columnNames['role_pivot_key'], $columnNames['model_morph_key'], 'model_type'],
                    'model_has_roles_role_model_type_primary');
            }
        });

        // Role has permissions pivot table
        Schema::create($tableNames['role_has_permissions'] ?? 'role_has_permissions', function (Blueprint $table) use ($tableNames, $columnNames) {
            $table->unsignedBigInteger($columnNames['permission_pivot_key']);
            $table->unsignedBigInteger($columnNames['role_pivot_key']);

            $table->foreign($columnNames['permission_pivot_key'])
                ->references('id')
                ->on($tableNames['permissions'] ?? 'permissions')
                ->onDelete('cascade');

            $table->foreign($columnNames['role_pivot_key'])
                ->references('id')
                ->on($tableNames['roles'] ?? 'roles')
                ->onDelete('cascade');

            $table->primary([$columnNames['permission_pivot_key'], $columnNames['role_pivot_key']], 'role_has_permissions_permission_id_role_id_primary');
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('role_has_permissions');
        Schema::dropIfExists('model_has_roles');
        Schema::dropIfExists('model_has_permissions');
        Schema::dropIfExists('roles');
        Schema::dropIfExists('permissions');
    }
};
