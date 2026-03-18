<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Add user_id to phone_numbers, email_accounts, proxy_servers, and api_key_pools
     * for ownership-based access control.
     */
    public function up(): void
    {
        $tables = ['phone_numbers', 'email_accounts', 'proxy_servers', 'api_key_pools'];

        foreach ($tables as $table) {
            if (Schema::hasTable($table) && !Schema::hasColumn($table, 'user_id')) {
                Schema::table($table, function (Blueprint $blueprint) {
                    $blueprint->unsignedBigInteger('user_id')->nullable()->after('id');
                    $blueprint->index('user_id');
                });
            }
        }
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        $tables = ['phone_numbers', 'email_accounts', 'proxy_servers', 'api_key_pools'];

        foreach ($tables as $table) {
            if (Schema::hasTable($table) && Schema::hasColumn($table, 'user_id')) {
                Schema::table($table, function (Blueprint $blueprint) {
                    $blueprint->dropIndex(['user_id']);
                    $blueprint->dropColumn('user_id');
                });
            }
        }
    }
};
