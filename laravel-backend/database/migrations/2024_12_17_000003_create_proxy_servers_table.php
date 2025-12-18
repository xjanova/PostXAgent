<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('proxy_servers', function (Blueprint $table) {
            $table->id();
            $table->string('host');
            $table->integer('port');
            $table->enum('type', ['http', 'https', 'socks4', 'socks5'])->default('http');
            $table->string('username')->nullable();
            $table->text('password_encrypted')->nullable();
            $table->enum('provider', ['bright_data', 'oxylabs', 'smartproxy', 'residential', 'datacenter', 'custom'])->default('custom');
            $table->char('country_code', 2)->nullable();
            $table->string('city')->nullable();
            $table->enum('status', ['active', 'inactive', 'banned', 'slow'])->default('active');
            $table->timestamp('last_used_at')->nullable();
            $table->timestamp('last_checked_at')->nullable();
            $table->integer('response_time_ms')->nullable();
            $table->unsignedInteger('success_count')->default(0);
            $table->unsignedInteger('failure_count')->default(0);
            $table->json('banned_platforms')->nullable();
            $table->json('metadata')->nullable();
            $table->timestamps();
            $table->softDeletes();

            $table->unique(['host', 'port']);
            $table->index('status');
            $table->index('country_code');
            $table->index(['status', 'response_time_ms']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('proxy_servers');
    }
};
