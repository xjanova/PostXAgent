<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('seek_and_post_tasks', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->cascadeOnDelete();
            $table->foreignId('brand_id')->nullable()->constrained()->nullOnDelete();
            $table->string('name');
            $table->text('description')->nullable();
            $table->string('status')->default('pending'); // pending, seeking, joining, posting, completed, paused, failed

            // Target configuration
            $table->string('platform');
            $table->json('target_keywords'); // keywords to search for groups
            $table->json('exclude_keywords')->nullable(); // keywords to avoid
            $table->integer('min_group_members')->default(100);
            $table->integer('max_group_members')->nullable();
            $table->double('min_quality_score')->default(50);

            // Seek configuration
            $table->integer('max_groups_to_discover')->default(50);
            $table->integer('max_groups_to_join_per_day')->default(10);
            $table->boolean('auto_join')->default(true);

            // Post configuration
            $table->foreignId('workflow_template_id')->nullable()->constrained('workflow_templates')->nullOnDelete();
            $table->json('workflow_variables')->nullable(); // variable values for the workflow
            $table->integer('posts_per_group_per_day')->default(1);
            $table->integer('max_posts_per_day')->default(20);
            $table->json('posting_schedule')->nullable(); // specific times or "smart"
            $table->boolean('smart_timing')->default(true); // use AI to determine best times

            // Content configuration
            $table->text('content_template')->nullable();
            $table->json('content_variations')->nullable(); // AI-generated variations
            $table->boolean('vary_content')->default(true); // generate unique content for each post
            $table->json('media_urls')->nullable(); // images/videos to include

            // Progress tracking
            $table->integer('groups_discovered')->default(0);
            $table->integer('groups_joined')->default(0);
            $table->integer('posts_made')->default(0);
            $table->integer('posts_successful')->default(0);
            $table->integer('posts_failed')->default(0);
            $table->timestamp('last_seek_at')->nullable();
            $table->timestamp('last_post_at')->nullable();

            // Scheduling
            $table->boolean('is_recurring')->default(false);
            $table->string('recurrence_pattern')->nullable(); // daily, weekly, etc.
            $table->timestamp('scheduled_at')->nullable();
            $table->timestamp('started_at')->nullable();
            $table->timestamp('completed_at')->nullable();

            $table->timestamps();

            $table->index('status');
            $table->index('platform');
            $table->index('user_id');
        });

        // Pivot table for task -> discovered groups
        Schema::create('seek_task_groups', function (Blueprint $table) {
            $table->id();
            $table->foreignId('seek_and_post_task_id')->constrained()->cascadeOnDelete();
            $table->foreignId('discovered_group_id')->constrained()->cascadeOnDelete();
            $table->string('status')->default('discovered'); // discovered, join_requested, joined, posted, failed
            $table->integer('posts_in_group')->default(0);
            $table->timestamp('last_post_at')->nullable();
            $table->timestamps();

            $table->unique(['seek_and_post_task_id', 'discovered_group_id'], 'task_group_unique');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('seek_task_groups');
        Schema::dropIfExists('seek_and_post_tasks');
    }
};
