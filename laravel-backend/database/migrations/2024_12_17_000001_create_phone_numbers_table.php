<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('phone_numbers', function (Blueprint $table) {
            $table->id();
            $table->string('phone_number', 20)->unique();
            $table->char('country_code', 2)->default('TH');
            $table->enum('provider', ['sms_activate', '5sim', 'smspva', 'manual'])->default('manual');
            $table->string('provider_order_id')->nullable();
            $table->enum('status', ['available', 'in_use', 'used', 'blocked', 'expired'])->default('available');
            $table->string('used_for_platform', 20)->nullable();
            $table->foreignId('used_for_account_id')->nullable()->constrained('social_accounts')->nullOnDelete();
            $table->timestamp('expires_at')->nullable();
            $table->timestamp('last_sms_received_at')->nullable();
            $table->json('sms_messages')->nullable();
            $table->decimal('cost', 10, 4)->default(0);
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
        Schema::dropIfExists('phone_numbers');
    }
};
