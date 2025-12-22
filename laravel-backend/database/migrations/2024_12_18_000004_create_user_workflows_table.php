<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        // User's custom workflows (based on templates or created from scratch)
        Schema::create('user_workflows', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('template_id')->nullable()->constrained('workflow_templates')->nullOnDelete();
            $table->string('name');
            $table->text('description')->nullable();
            $table->json('platforms'); // platforms this workflow targets
            $table->longText('workflow_json'); // the workflow definition
            $table->json('default_variables')->nullable(); // default variable values
            $table->boolean('is_active')->default(true);
            $table->integer('run_count')->default(0);
            $table->double('success_rate')->default(0);
            $table->timestamp('last_run_at')->nullable();
            $table->timestamps();

            $table->index('user_id');
            $table->index('is_active');
        });

        // Note: workflow_executions table is created in 2024_12_17_000007_create_workflow_executions_table.php
    }

    public function down(): void
    {
        Schema::dropIfExists('user_workflows');
    }
};
