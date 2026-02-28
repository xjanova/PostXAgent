<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        // API Key Pools - จัดกลุ่ม API keys สำหรับ free API rotation
        Schema::create('api_key_pools', function (Blueprint $table) {
            $table->id();
            $table->string('name'); // เช่น "Groq Text Pool", "Gemini Flash Pool"
            $table->string('provider'); // groq, gemini, mistral, google_tts, edge_tts
            $table->enum('service_type', ['text', 'image', 'tts', 'video', 'music'])->default('text');
            $table->enum('rotation_strategy', ['round_robin', 'random', 'least_used', 'priority'])->default('round_robin');
            $table->unsignedInteger('cooldown_seconds')->default(0); // เวลาพักระหว่างการใช้ key เดิม
            $table->unsignedInteger('max_requests_per_day')->default(1000); // จำกัด request ต่อวันต่อ pool
            $table->boolean('auto_failover')->default(true); // สลับ key อัตโนมัติเมื่อ fail
            $table->boolean('is_active')->default(true);
            $table->timestamps();
            $table->softDeletes();

            $table->unique(['name', 'provider']);
            $table->index(['provider', 'service_type', 'is_active']);
        });

        // API Key Pool Members - แต่ละ API key ใน pool
        Schema::create('api_key_pool_members', function (Blueprint $table) {
            $table->id();
            $table->foreignId('api_key_pool_id')->constrained()->onDelete('cascade');
            $table->string('label'); // เช่น "groq-acct-1", "gemini-key-2"
            $table->text('encrypted_api_key'); // เก็บ API key แบบ encrypted
            $table->string('base_url')->nullable(); // override base URL (เช่น Groq ใช้ OpenAI-compatible)
            $table->string('model_id')->nullable(); // เช่น "llama-3.3-70b-versatile"
            $table->unsignedInteger('priority')->default(0); // ลำดับความสำคัญ (0 = สูงสุด)
            $table->unsignedInteger('weight')->default(100); // น้ำหนักสำหรับ random selection
            $table->enum('status', ['active', 'cooldown', 'exhausted', 'error'])->default('active');
            $table->timestamp('cooldown_until')->nullable();
            $table->timestamp('last_used_at')->nullable();
            $table->unsignedInteger('requests_today')->default(0);
            $table->unsignedInteger('total_requests')->default(0);
            $table->unsignedInteger('daily_limit')->default(0); // 0 = ใช้ค่า pool default
            $table->unsignedInteger('success_count')->default(0);
            $table->unsignedInteger('failure_count')->default(0);
            $table->unsignedInteger('consecutive_failures')->default(0);
            $table->text('last_error')->nullable();
            $table->timestamps();

            $table->unique(['api_key_pool_id', 'label']);
            $table->index(['status', 'cooldown_until']);
            $table->index(['priority', 'weight']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('api_key_pool_members');
        Schema::dropIfExists('api_key_pools');
    }
};
