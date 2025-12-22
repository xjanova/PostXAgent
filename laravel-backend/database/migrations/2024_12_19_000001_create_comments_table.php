<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('comments', function (Blueprint $table) {
            $table->id();
            $table->foreignId('post_id')->constrained()->onDelete('cascade');
            $table->string('platform');
            $table->string('platform_comment_id');
            $table->unsignedBigInteger('parent_comment_id')->nullable();

            // Author info
            $table->string('author_name');
            $table->string('author_id')->nullable();
            $table->string('author_avatar_url')->nullable();

            // Content
            $table->text('content_text');
            $table->string('media_url')->nullable();

            // Analysis
            $table->enum('sentiment', ['positive', 'negative', 'neutral'])->default('neutral');
            $table->float('sentiment_score')->default(0);
            $table->boolean('is_question')->default(false);
            $table->boolean('requires_reply')->default(true);
            $table->integer('priority')->default(0);

            // Reply tracking
            $table->timestamp('replied_at')->nullable();
            $table->text('reply_content')->nullable();
            $table->string('reply_comment_id')->nullable();
            $table->enum('reply_status', ['pending', 'replied', 'skipped', 'failed'])->default('pending');

            // Engagement
            $table->integer('likes_count')->default(0);
            $table->integer('replies_count')->default(0);

            // Metadata
            $table->json('metadata')->nullable();
            $table->timestamp('commented_at')->nullable();
            $table->timestamps();

            // Indexes
            $table->unique(['platform', 'platform_comment_id']);
            $table->index(['post_id', 'requires_reply']);
            $table->index(['platform', 'reply_status']);
            $table->index('parent_comment_id');
            $table->index('priority');

            $table->foreign('parent_comment_id')
                ->references('id')
                ->on('comments')
                ->onDelete('cascade');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('comments');
    }
};
