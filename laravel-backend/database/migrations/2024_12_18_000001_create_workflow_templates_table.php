<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('workflow_templates', function (Blueprint $table) {
            $table->id();
            $table->string('name');
            $table->string('name_th')->nullable();
            $table->text('description')->nullable();
            $table->text('description_th')->nullable();
            $table->string('category'); // marketing, content, engagement, seek_and_post, platform_specific, special
            $table->string('icon')->default('WorkOutline');
            $table->json('supported_platforms'); // ["facebook", "instagram", "tiktok", etc.]
            $table->json('variables')->nullable(); // template variables schema
            $table->longText('workflow_json'); // the actual workflow
            $table->boolean('is_system')->default(false); // system templates cannot be deleted
            $table->boolean('is_active')->default(true);
            $table->integer('use_count')->default(0);
            $table->double('avg_success_rate')->default(0);
            $table->foreignId('created_by')->nullable()->constrained('users')->nullOnDelete();
            $table->timestamps();

            $table->index('category');
            $table->index('is_active');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('workflow_templates');
    }
};
