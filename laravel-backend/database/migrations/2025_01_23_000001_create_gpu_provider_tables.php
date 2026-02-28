<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        // GPU Provider Accounts - เก็บ accounts ของ GPU providers (Vast.ai, RunPod, etc.)
        Schema::create('gpu_provider_accounts', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->string('provider'); // vast_ai, runpod, lightning_ai, kaggle
            $table->string('name'); // ชื่อ account เช่น "Vast.ai Account 1"
            $table->string('email')->nullable();
            $table->text('api_key'); // encrypted API key
            $table->text('api_secret')->nullable(); // encrypted API secret (ถ้ามี)
            $table->decimal('credits_balance', 10, 4)->default(0); // ยอดเงินคงเหลือ
            $table->decimal('credits_used_today', 10, 4)->default(0); // ใช้ไปวันนี้
            $table->decimal('daily_credit_limit', 10, 4)->nullable(); // limit ต่อวัน
            $table->decimal('low_balance_threshold', 10, 4)->default(1.00); // แจ้งเตือนเมื่อเหลือน้อย
            $table->enum('status', ['active', 'cooldown', 'suspended', 'depleted', 'error'])->default('active');
            $table->timestamp('cooldown_until')->nullable();
            $table->timestamp('last_used_at')->nullable();
            $table->timestamp('last_balance_check_at')->nullable();
            $table->unsignedInteger('instances_created')->default(0);
            $table->unsignedInteger('total_gpu_hours')->default(0);
            $table->json('metadata')->nullable(); // เก็บข้อมูลเพิ่มเติม
            $table->boolean('is_active')->default(true);
            $table->timestamps();
            $table->softDeletes();

            $table->index(['provider', 'status', 'is_active']);
            $table->index(['user_id', 'provider']);
            $table->unique(['user_id', 'provider', 'email']);
        });

        // GPU Account Pools - จัดกลุ่ม GPU accounts สำหรับ rotation
        Schema::create('gpu_account_pools', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->string('provider'); // vast_ai, runpod, etc. (หรือ 'mixed' สำหรับผสม)
            $table->string('name');
            $table->text('description')->nullable();
            $table->enum('rotation_strategy', ['round_robin', 'random', 'least_used', 'most_credits', 'priority'])->default('most_credits');
            $table->unsignedInteger('cooldown_minutes')->default(60); // พักหลังใช้ quota หมด
            $table->decimal('min_credits_required', 10, 4)->default(0.50); // credits ขั้นต่ำที่ต้องมี
            $table->boolean('auto_failover')->default(true);
            $table->boolean('auto_provision')->default(true); // สร้าง instance อัตโนมัติ
            $table->json('preferred_gpu_types')->nullable(); // ['RTX_4090', 'A100', 'H100']
            $table->json('preferred_regions')->nullable(); // ['us-east', 'eu-west']
            $table->decimal('max_price_per_hour', 10, 4)->nullable(); // ราคาสูงสุดที่ยอมจ่าย
            $table->boolean('is_active')->default(true);
            $table->timestamps();
            $table->softDeletes();

            $table->unique(['user_id', 'name']);
            $table->index(['provider', 'is_active']);
        });

        // GPU Pool Members - เชื่อม accounts เข้า pool
        Schema::create('gpu_pool_members', function (Blueprint $table) {
            $table->id();
            $table->foreignId('gpu_account_pool_id')->constrained()->onDelete('cascade');
            $table->foreignId('gpu_provider_account_id')->constrained()->onDelete('cascade');
            $table->unsignedInteger('priority')->default(0);
            $table->unsignedInteger('weight')->default(100);
            $table->enum('status', ['active', 'cooldown', 'suspended', 'depleted', 'error'])->default('active');
            $table->timestamp('cooldown_until')->nullable();
            $table->timestamp('last_used_at')->nullable();
            $table->unsignedInteger('instances_today')->default(0);
            $table->unsignedInteger('total_instances')->default(0);
            $table->decimal('credits_used_today', 10, 4)->default(0);
            $table->unsignedInteger('success_count')->default(0);
            $table->unsignedInteger('failure_count')->default(0);
            $table->unsignedInteger('consecutive_failures')->default(0);
            $table->text('last_error')->nullable();
            $table->timestamps();

            $table->unique(['gpu_account_pool_id', 'gpu_provider_account_id']);
            $table->index(['status', 'cooldown_until']);
        });

        // GPU Instances - ติดตาม instances ที่สร้างขึ้น
        Schema::create('gpu_instances', function (Blueprint $table) {
            $table->id();
            $table->foreignId('gpu_provider_account_id')->constrained()->onDelete('cascade');
            $table->foreignId('gpu_account_pool_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->string('provider');
            $table->string('instance_id'); // ID จาก provider
            $table->string('instance_name')->nullable();
            $table->enum('status', ['pending', 'starting', 'running', 'stopping', 'stopped', 'terminated', 'error'])->default('pending');
            $table->string('gpu_type')->nullable(); // RTX_4090, A100, etc.
            $table->unsignedInteger('gpu_count')->default(1);
            $table->unsignedInteger('cpu_cores')->nullable();
            $table->unsignedInteger('ram_gb')->nullable();
            $table->unsignedInteger('disk_gb')->nullable();
            $table->string('region')->nullable();
            $table->string('ip_address')->nullable();
            $table->unsignedInteger('ssh_port')->nullable();
            $table->decimal('price_per_hour', 10, 4)->nullable();
            $table->decimal('total_cost', 10, 4)->default(0);
            $table->string('docker_image')->nullable();
            $table->json('environment_vars')->nullable();
            $table->json('setup_script')->nullable(); // script ที่รันหลังสร้าง instance
            $table->enum('setup_status', ['pending', 'running', 'completed', 'failed'])->default('pending');
            $table->text('setup_log')->nullable();
            $table->timestamp('started_at')->nullable();
            $table->timestamp('stopped_at')->nullable();
            $table->unsignedInteger('runtime_minutes')->default(0);
            $table->json('metadata')->nullable();
            $table->timestamps();
            $table->softDeletes();

            $table->unique(['provider', 'instance_id']);
            $table->index(['user_id', 'status']);
            $table->index(['gpu_provider_account_id', 'status']);
        });

        // GPU Activity Logs - บันทึกกิจกรรม
        Schema::create('gpu_activity_logs', function (Blueprint $table) {
            $table->id();
            $table->foreignId('gpu_provider_account_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('gpu_account_pool_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('gpu_instance_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->string('provider')->nullable();
            $table->enum('event_type', [
                'account_created',
                'account_updated',
                'account_suspended',
                'account_recovered',
                'balance_checked',
                'balance_low',
                'balance_depleted',
                'instance_created',
                'instance_started',
                'instance_stopped',
                'instance_terminated',
                'instance_error',
                'setup_started',
                'setup_completed',
                'setup_failed',
                'rotation_triggered',
                'failover_triggered',
                'api_error',
                'quota_exceeded',
            ]);
            $table->enum('old_status', ['active', 'cooldown', 'suspended', 'depleted', 'error', 'pending', 'starting', 'running', 'stopping', 'stopped', 'terminated'])->nullable();
            $table->enum('new_status', ['active', 'cooldown', 'suspended', 'depleted', 'error', 'pending', 'starting', 'running', 'stopping', 'stopped', 'terminated'])->nullable();
            $table->text('message')->nullable();
            $table->json('metadata')->nullable();
            $table->decimal('credits_before', 10, 4)->nullable();
            $table->decimal('credits_after', 10, 4)->nullable();
            $table->string('triggered_by')->nullable(); // system, user, api, scheduler
            $table->timestamps();

            $table->index(['gpu_provider_account_id', 'event_type']);
            $table->index(['user_id', 'event_type']);
            $table->index(['created_at']);
        });

        // GPU Setup Templates - templates สำหรับตั้งค่า instance
        Schema::create('gpu_setup_templates', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->string('name');
            $table->text('description')->nullable();
            $table->string('docker_image')->nullable();
            $table->json('environment_vars')->nullable();
            $table->text('setup_script')->nullable(); // bash script
            $table->text('post_setup_script')->nullable(); // script หลัง setup เสร็จ
            $table->json('required_packages')->nullable(); // python packages
            $table->json('required_gpu_types')->nullable();
            $table->unsignedInteger('min_gpu_memory_gb')->nullable();
            $table->unsignedInteger('min_cpu_cores')->nullable();
            $table->unsignedInteger('min_ram_gb')->nullable();
            $table->unsignedInteger('min_disk_gb')->nullable();
            $table->boolean('is_default')->default(false);
            $table->boolean('is_public')->default(false); // share กับ users อื่น
            $table->timestamps();
            $table->softDeletes();

            $table->index(['user_id', 'is_default']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('gpu_setup_templates');
        Schema::dropIfExists('gpu_activity_logs');
        Schema::dropIfExists('gpu_instances');
        Schema::dropIfExists('gpu_pool_members');
        Schema::dropIfExists('gpu_account_pools');
        Schema::dropIfExists('gpu_provider_accounts');
    }
};
