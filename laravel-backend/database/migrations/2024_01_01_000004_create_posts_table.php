<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('posts', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('brand_id')->constrained()->onDelete('cascade');
            $table->foreignId('campaign_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('social_account_id')->constrained()->onDelete('cascade');
            $table->string('platform');
            $table->text('content_text');
            $table->string('content_type')->default('text');
            $table->json('media_urls')->nullable();
            $table->json('hashtags')->nullable();
            $table->string('link_url')->nullable();
            $table->boolean('ai_generated')->default(false);
            $table->string('ai_provider')->nullable();
            $table->text('ai_prompt')->nullable();
            $table->string('platform_post_id')->nullable();
            $table->string('platform_url')->nullable();
            $table->timestamp('scheduled_at')->nullable();
            $table->timestamp('published_at')->nullable();
            $table->string('status')->default('draft');
            $table->text('error_message')->nullable();
            $table->json('metrics')->nullable();
            $table->timestamps();
            $table->softDeletes();

            $table->index(['user_id', 'status']);
            $table->index(['platform', 'status']);
            $table->index(['scheduled_at', 'status']);
            $table->index('platform_post_id');
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('posts');
    }
};
