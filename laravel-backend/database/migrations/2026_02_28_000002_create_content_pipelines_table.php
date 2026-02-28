<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('content_pipelines', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('brand_id')->nullable()->constrained()->onDelete('set null');
            $table->string('name'); // เช่น "Thai Horror Stories", "Tech Tips Daily"
            $table->text('description')->nullable();
            $table->boolean('is_active')->default(false);

            // -- Content Config --
            $table->enum('content_type', ['story', 'tips', 'news', 'promotional', 'educational'])->default('story');
            $table->string('language', 10)->default('th');
            $table->string('tone', 50)->default('engaging'); // engaging, funny, dramatic, informative
            $table->text('content_prompt_template')->nullable(); // Custom prompt template
            $table->unsignedSmallInteger('scene_count')->default(5);

            // -- Image Config --
            $table->string('image_style', 50)->default('cinematic'); // realistic, anime, cinematic, cartoon
            $table->unsignedSmallInteger('image_width')->default(1024);
            $table->unsignedSmallInteger('image_height')->default(1024);
            $table->string('image_provider', 30)->default('auto'); // auto, diffusers, hf_inference

            // -- Video Config --
            $table->unsignedSmallInteger('video_duration_seconds')->default(60);
            $table->enum('aspect_ratio', ['9:16', '16:9', '1:1'])->default('9:16');
            $table->string('video_provider', 30)->default('auto'); // svd, freepik, slideshow, auto
            $table->unsignedSmallInteger('clip_duration_seconds')->default(5);
            $table->foreignId('freepik_account_pool_id')->nullable()->constrained('account_pools')->onDelete('set null');

            // -- Audio Config --
            $table->string('tts_provider', 20)->default('edge'); // edge, google, both
            $table->string('tts_voice_id', 100)->default('th-TH-PremwadeeNeural');
            $table->enum('tts_voice_gender', ['female', 'male'])->default('female');
            $table->boolean('enable_lip_sync')->default(true);
            $table->string('lip_sync_provider', 20)->default('sadtalker'); // sadtalker, wav2lip
            $table->text('lip_sync_avatar_path')->nullable(); // ภาพหน้าคนสำหรับ lip sync

            // -- Music Config --
            $table->boolean('enable_music')->default(true);
            $table->string('music_style', 100)->default('cinematic background');
            $table->float('music_volume')->default(0.3);
            $table->enum('music_source', ['suno', 'royalty_free', 'none'])->default('royalty_free');

            // -- Publishing Config --
            $table->json('target_platforms')->default('["tiktok","youtube"]');
            $table->boolean('auto_publish')->default(true);
            $table->foreignId('publish_account_pool_id')->nullable()->constrained('account_pools')->onDelete('set null');

            // -- Scheduling --
            $table->enum('schedule_type', ['manual', 'interval', 'cron'])->default('manual');
            $table->unsignedSmallInteger('interval_hours')->default(24);
            $table->string('cron_expression', 100)->nullable();
            $table->unsignedSmallInteger('max_runs_per_day')->default(3);
            $table->timestamp('next_run_at')->nullable();

            // -- API Key Pools --
            $table->foreignId('text_pool_id')->nullable()->constrained('api_key_pools')->onDelete('set null');
            $table->foreignId('tts_pool_id')->nullable()->constrained('api_key_pools')->onDelete('set null');

            $table->timestamps();
            $table->softDeletes();

            $table->index(['user_id', 'is_active']);
            $table->index(['is_active', 'schedule_type', 'next_run_at']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('content_pipelines');
    }
};
