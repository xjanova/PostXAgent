<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('email_accounts', function (Blueprint $table) {
            $table->id();
            $table->string('email')->unique();
            $table->text('password_encrypted');
            $table->enum('provider', ['gmail', 'outlook', 'yahoo', 'temp_mail', 'custom'])->default('custom');
            $table->string('recovery_email')->nullable();
            $table->string('recovery_phone', 20)->nullable();
            $table->enum('status', ['available', 'in_use', 'used', 'blocked'])->default('available');
            $table->string('used_for_platform', 20)->nullable();
            $table->foreignId('used_for_account_id')->nullable()->constrained('social_accounts')->nullOnDelete();
            $table->string('imap_host')->nullable();
            $table->integer('imap_port')->nullable();
            $table->string('smtp_host')->nullable();
            $table->integer('smtp_port')->nullable();
            $table->timestamp('last_checked_at')->nullable();
            $table->json('metadata')->nullable();
            $table->timestamps();
            $table->softDeletes();

            $table->index('status');
            $table->index('provider');
            $table->index(['status', 'used_for_platform']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('email_accounts');
    }
};
