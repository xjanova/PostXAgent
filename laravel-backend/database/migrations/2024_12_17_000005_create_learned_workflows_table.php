<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('learned_workflows', function (Blueprint $table) {
            $table->id();
            $table->string('name');
            $table->text('description')->nullable();
            $table->string('platform', 20)->index(); // facebook, instagram, tiktok, etc.
            $table->string('workflow_type', 50)->index(); // login, post_text, post_image, post_video, etc.
            $table->enum('status', ['active', 'learning', 'disabled', 'deprecated'])->default('learning');
            $table->enum('source', ['manual', 'ai_observed', 'ai_generated', 'imported'])->default('manual');
            $table->integer('version')->default(1);
            $table->foreignId('parent_workflow_id')->nullable()->constrained('learned_workflows')->nullOnDelete();

            // Learning metrics
            $table->integer('success_count')->default(0);
            $table->integer('failure_count')->default(0);
            $table->float('avg_execution_time')->nullable(); // seconds
            $table->timestamp('last_successful_at')->nullable();
            $table->timestamp('last_failed_at')->nullable();

            // AI learning data
            $table->json('element_patterns')->nullable(); // learned element patterns
            $table->json('timing_patterns')->nullable(); // optimal timing between steps
            $table->json('retry_strategies')->nullable(); // learned retry strategies
            $table->json('platform_variants')->nullable(); // URL/DOM variants for platform

            // Teaching session data
            $table->foreignId('taught_by_user_id')->nullable()->constrained('users')->nullOnDelete();
            $table->timestamp('teaching_started_at')->nullable();
            $table->timestamp('teaching_completed_at')->nullable();

            $table->json('metadata')->nullable();
            $table->timestamps();
            $table->softDeletes();

            $table->index(['platform', 'workflow_type', 'status']);
            $table->index(['status', 'success_count']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('learned_workflows');
    }
};
