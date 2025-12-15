<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        // Account Pools - จัดกลุ่ม Account สำหรับ Brand/Platform
        Schema::create('account_pools', function (Blueprint $table) {
            $table->id();
            $table->foreignId('brand_id')->constrained()->onDelete('cascade');
            $table->string('platform');
            $table->string('name'); // ชื่อ Pool เช่น "Facebook Main Pool"
            $table->text('description')->nullable();
            $table->enum('rotation_strategy', ['round_robin', 'random', 'least_used', 'priority'])->default('round_robin');
            $table->unsignedInteger('cooldown_minutes')->default(30); // เวลาพักระหว่างการใช้ Account เดิม
            $table->unsignedInteger('max_posts_per_day')->default(10); // จำกัดโพสต่อวันต่อ Account
            $table->boolean('auto_failover')->default(true); // สลับ Account อัตโนมัติเมื่อ fail
            $table->boolean('is_active')->default(true);
            $table->timestamps();
            $table->softDeletes();

            $table->unique(['brand_id', 'platform', 'name']);
            $table->index(['platform', 'is_active']);
        });

        // Account Pool Members - เชื่อม Account เข้า Pool
        Schema::create('account_pool_members', function (Blueprint $table) {
            $table->id();
            $table->foreignId('account_pool_id')->constrained()->onDelete('cascade');
            $table->foreignId('social_account_id')->constrained()->onDelete('cascade');
            $table->unsignedInteger('priority')->default(0); // ลำดับความสำคัญ (0 = สูงสุด)
            $table->unsignedInteger('weight')->default(100); // น้ำหนักสำหรับ random selection
            $table->enum('status', ['active', 'cooldown', 'suspended', 'banned', 'error'])->default('active');
            $table->timestamp('cooldown_until')->nullable();
            $table->timestamp('last_used_at')->nullable();
            $table->unsignedInteger('posts_today')->default(0);
            $table->unsignedInteger('total_posts')->default(0);
            $table->unsignedInteger('success_count')->default(0);
            $table->unsignedInteger('failure_count')->default(0);
            $table->unsignedInteger('consecutive_failures')->default(0);
            $table->timestamp('last_failure_at')->nullable();
            $table->text('last_error')->nullable();
            $table->timestamps();

            $table->unique(['account_pool_id', 'social_account_id']);
            $table->index(['status', 'cooldown_until']);
            $table->index(['priority', 'weight']);
        });

        // Account Status Logs - บันทึกประวัติสถานะและการใช้งาน
        Schema::create('account_status_logs', function (Blueprint $table) {
            $table->id();
            $table->foreignId('social_account_id')->constrained()->onDelete('cascade');
            $table->foreignId('account_pool_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('post_id')->nullable()->constrained()->onDelete('set null');
            $table->enum('event_type', [
                'post_success',     // โพสสำเร็จ
                'post_failed',      // โพสล้มเหลว
                'rate_limited',     // ถูกจำกัด rate
                'token_expired',    // Token หมดอายุ
                'token_refreshed',  // Refresh token สำเร็จ
                'account_banned',   // ถูก Ban
                'account_suspended', // ถูก Suspend ชั่วคราว
                'account_recovered', // กลับมาใช้งานได้
                'manual_status_change', // เปลี่ยนสถานะด้วยตนเอง
                'cooldown_started', // เริ่มพักการใช้งาน
                'cooldown_ended',   // จบการพัก
            ]);
            $table->enum('old_status', ['active', 'cooldown', 'suspended', 'banned', 'error'])->nullable();
            $table->enum('new_status', ['active', 'cooldown', 'suspended', 'banned', 'error'])->nullable();
            $table->text('message')->nullable();
            $table->json('metadata')->nullable(); // เก็บ error details, response codes ฯลฯ
            $table->string('triggered_by')->nullable(); // system, user, api
            $table->timestamps();

            $table->index(['social_account_id', 'event_type']);
            $table->index(['created_at']);
        });

        // Backup Credentials - เก็บ credentials สำรอง (encrypted)
        Schema::create('backup_credentials', function (Blueprint $table) {
            $table->id();
            $table->foreignId('social_account_id')->constrained()->onDelete('cascade');
            $table->string('credential_type'); // access_token, refresh_token, api_key, etc.
            $table->text('encrypted_value'); // เก็บแบบ encrypted
            $table->text('description')->nullable();
            $table->boolean('is_primary')->default(false);
            $table->timestamp('valid_until')->nullable();
            $table->timestamp('last_verified_at')->nullable();
            $table->timestamps();
            $table->softDeletes();

            $table->index(['social_account_id', 'credential_type', 'is_primary']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('backup_credentials');
        Schema::dropIfExists('account_status_logs');
        Schema::dropIfExists('account_pool_members');
        Schema::dropIfExists('account_pools');
    }
};
