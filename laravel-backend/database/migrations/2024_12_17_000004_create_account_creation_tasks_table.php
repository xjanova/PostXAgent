<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('account_creation_tasks', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('brand_id')->constrained()->cascadeOnDelete();
            $table->foreignId('account_pool_id')->nullable()->constrained()->nullOnDelete();
            $table->string('platform', 20);
            $table->enum('status', ['pending', 'in_progress', 'verifying', 'completed', 'failed', 'cancelled'])->default('pending');
            $table->string('current_step', 50)->default('init');
            $table->foreignId('phone_number_id')->nullable()->constrained()->nullOnDelete();
            $table->foreignId('email_account_id')->nullable()->constrained()->nullOnDelete();
            $table->foreignId('proxy_server_id')->nullable()->constrained()->nullOnDelete();
            $table->json('profile_data')->nullable();
            $table->string('generated_username')->nullable();
            $table->text('generated_password')->nullable();
            $table->foreignId('result_social_account_id')->nullable()->constrained('social_accounts')->nullOnDelete();
            $table->text('error_message')->nullable();
            $table->longText('error_screenshot')->nullable();
            $table->unsignedTinyInteger('attempts')->default(0);
            $table->unsignedTinyInteger('max_attempts')->default(3);
            $table->timestamp('started_at')->nullable();
            $table->timestamp('completed_at')->nullable();
            $table->json('step_log')->nullable();
            $table->json('metadata')->nullable();
            $table->timestamps();

            $table->index('status');
            $table->index('platform');
            $table->index(['user_id', 'status']);
            $table->index(['brand_id', 'platform']);
            $table->index('created_at');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('account_creation_tasks');
    }
};
