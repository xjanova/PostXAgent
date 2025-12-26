<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        Schema::create('licenses', function (Blueprint $table) {
            $table->id();
            $table->string('license_key', 50)->unique();
            $table->string('machine_id', 255)->nullable()->index();
            $table->string('machine_fingerprint', 1024)->nullable();
            $table->enum('type', ['demo', 'monthly', 'yearly', 'lifetime'])->default('monthly');
            $table->enum('status', ['active', 'expired', 'revoked'])->default('active');
            $table->timestamp('activated_at')->nullable();
            $table->timestamp('expires_at')->nullable()->index();
            $table->timestamp('last_validated_at')->nullable();
            $table->integer('allowed_machines')->default(1);
            $table->json('metadata')->nullable();
            $table->timestamps();
            $table->softDeletes();

            // Indexes
            $table->index(['license_key', 'machine_id']);
            $table->index(['type', 'status']);
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('licenses');
    }
};
