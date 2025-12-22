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

        // Workflow execution history
        Schema::create('workflow_executions', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('user_workflow_id')->nullable()->constrained()->nullOnDelete();
            $table->foreignId('template_id')->nullable()->constrained('workflow_templates')->nullOnDelete();
            $table->string('status')->default('pending'); // pending, running, completed, failed, cancelled
            $table->json('variables')->nullable(); // variables used in this execution
            $table->json('node_outputs')->nullable(); // output from each node
            $table->text('error_message')->nullable();
            $table->json('execution_log')->nullable(); // detailed step-by-step log
            $table->integer('nodes_executed')->default(0);
            $table->integer('total_nodes')->default(0);
            $table->integer('duration_ms')->nullable();
            $table->timestamp('started_at')->nullable();
            $table->timestamp('completed_at')->nullable();
            $table->timestamps();

            $table->index('status');
            $table->index('user_id');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('workflow_executions');
        Schema::dropIfExists('user_workflows');
    }
};
