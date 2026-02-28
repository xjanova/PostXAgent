<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('pipeline_runs', function (Blueprint $table) {
            $table->id();
            $table->foreignId('content_pipeline_id')->constrained()->onDelete('cascade');

            $table->enum('status', [
                'pending',      // รอเริ่ม
                'story',        // กำลังสร้างเรื่อง
                'images',       // กำลังสร้างภาพ
                'video',        // กำลังสร้างวิดีโอ
                'voiceover',    // กำลังสร้างเสียงบรรยาย
                'lipsync',      // กำลังทำ lip sync
                'assembly',     // กำลังประกอบวิดีโอ
                'publishing',   // กำลังโพสต์
                'completed',    // เสร็จสมบูรณ์
                'failed',       // ล้มเหลว
                'cancelled',    // ยกเลิก
            ])->default('pending');

            // -- Generated Content (JSON) --
            $table->json('generated_story')->nullable(); // { title, scenes: [{text, image_prompt, narration}] }
            $table->json('generated_images')->nullable(); // [{ scene_number, local_path, provider }]
            $table->json('generated_video_clips')->nullable(); // [{ scene_number, local_path }]
            $table->json('generated_voiceover')->nullable(); // { local_path, duration_seconds, segments: [] }
            $table->json('generated_lipsync')->nullable(); // { local_path, duration }
            $table->text('final_video_path')->nullable();
            $table->text('thumbnail_path')->nullable();

            // -- Publishing Results --
            $table->json('publish_results')->nullable(); // { platform: { success, post_id, url, error } }

            // -- Progress --
            $table->unsignedSmallInteger('current_step')->default(0);
            $table->unsignedSmallInteger('total_steps')->default(7);
            $table->string('step_message')->nullable(); // ข้อความ progress ปัจจุบัน
            $table->float('progress_percentage')->default(0);

            // -- Timing --
            $table->timestamp('started_at')->nullable();
            $table->timestamp('completed_at')->nullable();

            // -- Error Handling --
            $table->text('error_message')->nullable();
            $table->string('error_step', 50)->nullable();
            $table->unsignedSmallInteger('retry_count')->default(0);

            // -- API Usage Tracking --
            $table->json('api_keys_used')->nullable(); // { provider: key_label }
            $table->json('tokens_consumed')->nullable(); // { provider: count }

            $table->timestamps();

            $table->index(['content_pipeline_id', 'status']);
            $table->index(['status', 'created_at']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('pipeline_runs');
    }
};
