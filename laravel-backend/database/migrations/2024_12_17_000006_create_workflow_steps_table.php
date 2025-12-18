<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('workflow_steps', function (Blueprint $table) {
            $table->id();
            $table->foreignId('workflow_id')->constrained('learned_workflows')->cascadeOnDelete();
            $table->integer('step_order');
            $table->string('name');
            $table->text('description')->nullable();

            // Action configuration
            $table->enum('action_type', [
                'click', 'type', 'select', 'scroll', 'wait',
                'screenshot', 'upload', 'drag', 'hover',
                'key_press', 'execute_script', 'condition'
            ]);

            // Element selectors (multiple fallback options)
            $table->json('selectors'); // Array of {type, value, confidence, learned_at}

            // Action parameters
            $table->json('action_params')->nullable(); // text, key, coordinates, etc.
            $table->string('input_source')->nullable(); // 'content.title', 'content.body', 'static'

            // Timing
            $table->integer('wait_before_ms')->default(0);
            $table->integer('wait_after_ms')->default(500);
            $table->integer('timeout_ms')->default(30000);

            // Retry configuration
            $table->integer('max_retries')->default(3);
            $table->json('retry_selectors')->nullable(); // Alternative selectors on retry

            // Conditions
            $table->json('preconditions')->nullable(); // Conditions that must be true before executing
            $table->json('postconditions')->nullable(); // Expected state after execution
            $table->boolean('is_optional')->default(false);
            $table->boolean('skip_on_failure')->default(false);

            // Learning data
            $table->integer('success_count')->default(0);
            $table->integer('failure_count')->default(0);
            $table->json('failure_reasons')->nullable(); // Common failure reasons
            $table->json('visual_features')->nullable(); // ML-learned visual features

            $table->json('metadata')->nullable();
            $table->timestamps();

            $table->unique(['workflow_id', 'step_order']);
            $table->index(['action_type']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('workflow_steps');
    }
};
