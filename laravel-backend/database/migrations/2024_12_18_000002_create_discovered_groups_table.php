<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('discovered_groups', function (Blueprint $table) {
            $table->id();
            $table->string('platform'); // facebook, line, telegram, etc.
            $table->string('group_id'); // platform-specific group ID
            $table->string('group_name');
            $table->string('group_url')->nullable();
            $table->text('description')->nullable();
            $table->json('keywords'); // keywords that led to discovery
            $table->string('category')->nullable(); // shopping, news, community, etc.
            $table->json('tags')->nullable(); // additional tags

            // Group characteristics
            $table->integer('member_count')->default(0);
            $table->double('engagement_rate')->default(0); // posts per day / members
            $table->integer('posts_per_day')->default(0);
            $table->string('activity_level')->default('unknown'); // very_active, active, moderate, low, dead
            $table->boolean('is_public')->default(true);
            $table->boolean('requires_approval')->default(false);
            $table->string('language')->default('th');
            $table->json('admin_info')->nullable(); // admin names, responsiveness, etc.

            // Our relationship with the group
            $table->boolean('is_joined')->default(false);
            $table->timestamp('join_requested_at')->nullable();
            $table->timestamp('joined_at')->nullable();
            $table->boolean('is_banned')->default(false);
            $table->timestamp('banned_at')->nullable();
            $table->string('ban_reason')->nullable();

            // Our activity in the group
            $table->integer('our_post_count')->default(0);
            $table->integer('successful_posts')->default(0);
            $table->integer('failed_posts')->default(0);
            $table->integer('deleted_posts')->default(0);
            $table->timestamp('last_post_at')->nullable();
            $table->timestamp('last_checked_at')->nullable();

            // Quality scoring
            $table->double('quality_score')->default(0); // 0-100
            $table->double('relevance_score')->default(0); // how relevant to our keywords
            $table->double('success_rate')->default(0); // our post success rate in this group
            $table->json('quality_factors')->nullable(); // detailed quality breakdown

            // Additional metadata
            $table->json('posting_rules')->nullable(); // group rules we've learned
            $table->json('best_posting_times')->nullable(); // optimal times to post
            $table->json('content_preferences')->nullable(); // what works in this group

            $table->timestamps();

            $table->unique(['platform', 'group_id']);
            $table->index('platform');
            $table->index('quality_score');
            $table->index('is_joined');
            $table->index('is_banned');
            $table->index('activity_level');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('discovered_groups');
    }
};
