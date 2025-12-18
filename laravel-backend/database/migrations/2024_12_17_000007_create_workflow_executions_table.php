<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('workflow_executions', function (Blueprint $table) {
            $table->id();
            $table->foreignId('workflow_id')->constrained('learned_workflows')->cascadeOnDelete();
            $table->foreignId('user_id')->nullable()->constrained('users')->nullOnDelete();
            $table->foreignId('social_account_id')->nullable()->constrained('social_accounts')->nullOnDelete();
            $table->foreignId('post_id')->nullable()->constrained('posts')->nullOnDelete();

            // Execution status
            $table->enum('status', ['pending', 'running', 'completed', 'failed', 'cancelled'])->default('pending');
            $table->timestamp('started_at')->nullable();
            $table->timestamp('completed_at')->nullable();
            $table->integer('duration_ms')->nullable();

            // Results
            $table->boolean('success')->nullable();
            $table->string('error_code', 50)->nullable();
            $table->text('error_message')->nullable();
            $table->json('step_results')->nullable(); // Per-step execution results

            // Context
            $table->string('trigger_source', 50)->nullable(); // api, scheduler, manual, test
            $table->json('input_data')->nullable(); // Data passed to workflow
            $table->json('output_data')->nullable(); // Data collected from workflow

            // Screenshots and evidence
            $table->json('screenshots')->nullable(); // Array of screenshot paths
            $table->text('browser_logs')->nullable();

            // Learning feedback
            $table->boolean('used_for_learning')->default(false);
            $table->json('learning_insights')->nullable(); // What was learned from this execution

            // Repair attempts
            $table->integer('repair_attempts')->default(0);
            $table->json('repair_actions')->nullable(); // What repairs were attempted
            $table->boolean('required_manual_intervention')->default(false);

            $table->json('metadata')->nullable();
            $table->timestamps();

            $table->index(['workflow_id', 'status']);
            $table->index(['workflow_id', 'success']);
            $table->index(['created_at']);
            $table->index(['social_account_id', 'created_at']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('workflow_executions');
    }
};
