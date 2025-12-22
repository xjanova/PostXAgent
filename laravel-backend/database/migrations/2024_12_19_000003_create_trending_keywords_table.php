<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('trending_keywords', function (Blueprint $table) {
            $table->id();
            $table->string('platform');
            $table->string('keyword');
            $table->string('category')->nullable();
            $table->string('region')->default('TH');

            // Trend metrics
            $table->float('trend_score')->default(0); // 0-100
            $table->float('velocity')->default(0); // Growth rate per hour
            $table->integer('mention_count')->default(0);
            $table->integer('post_count')->default(0);

            // Engagement metrics (average per post with this keyword)
            $table->float('avg_likes')->default(0);
            $table->float('avg_comments')->default(0);
            $table->float('avg_shares')->default(0);
            $table->float('engagement_rate')->default(0);

            // Viral potential score (calculated)
            $table->float('viral_score')->default(0); // 0-100

            // Lifecycle tracking
            $table->timestamp('first_seen_at');
            $table->timestamp('peak_at')->nullable();
            $table->timestamp('last_updated_at');

            // Related data
            $table->json('related_keywords')->nullable();
            $table->json('top_posts')->nullable(); // Sample of top performing posts
            $table->json('hourly_trend')->nullable(); // Last 24 hours trend data
            $table->json('metadata')->nullable();

            $table->timestamps();

            $table->unique(['platform', 'keyword', 'region']);
            $table->index(['platform', 'trend_score']);
            $table->index(['platform', 'viral_score']);
            $table->index('category');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('trending_keywords');
    }
};
