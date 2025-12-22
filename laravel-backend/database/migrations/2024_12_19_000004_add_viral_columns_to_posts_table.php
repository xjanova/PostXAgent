<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::table('posts', function (Blueprint $table) {
            // Viral analysis
            $table->float('viral_score')->default(0)->after('metrics');
            $table->json('viral_factors')->nullable()->after('viral_score');
            $table->boolean('is_viral')->default(false)->after('viral_factors');
            $table->timestamp('peak_engagement_at')->nullable()->after('is_viral');

            // Engagement velocity (rate of engagement growth)
            $table->float('engagement_velocity')->default(0)->after('peak_engagement_at');

            // Comment tracking
            $table->integer('comments_fetched_count')->default(0)->after('engagement_velocity');
            $table->integer('comments_replied_count')->default(0)->after('comments_fetched_count');
            $table->timestamp('last_comment_check_at')->nullable()->after('comments_replied_count');

            // Indexes
            $table->index('viral_score');
            $table->index('is_viral');
        });
    }

    public function down(): void
    {
        Schema::table('posts', function (Blueprint $table) {
            $table->dropIndex(['viral_score']);
            $table->dropIndex(['is_viral']);

            $table->dropColumn([
                'viral_score',
                'viral_factors',
                'is_viral',
                'peak_engagement_at',
                'engagement_velocity',
                'comments_fetched_count',
                'comments_replied_count',
                'last_comment_check_at',
            ]);
        });
    }
};
