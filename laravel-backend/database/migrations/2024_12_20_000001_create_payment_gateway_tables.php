<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     *
     * สร้างตารางสำหรับระบบ Payment Gateway
     * - mobile_devices: อุปกรณ์มือถือที่เชื่อมต่อ
     * - payment_orders: คำสั่งชำระเงินที่รอการจ่าย
     * - sms_payments: การชำระเงินที่ตรวจจับจาก SMS
     */
    public function up(): void
    {
        // Mobile Devices - อุปกรณ์มือถือที่เชื่อมต่อ
        Schema::create('mobile_devices', function (Blueprint $table) {
            $table->id();
            $table->string('device_id')->unique();
            $table->string('device_name');
            $table->string('platform')->default('Android');
            $table->string('app_version')->default('1.0.0');
            $table->boolean('is_online')->default(false);
            $table->timestamp('connected_at')->nullable();
            $table->timestamp('last_sync_at')->nullable();
            $table->timestamp('last_heartbeat')->nullable();
            $table->integer('battery_level')->nullable();
            $table->string('network_type')->nullable();
            $table->boolean('sms_monitoring_enabled')->default(false);
            $table->boolean('auto_approve_enabled')->default(false);
            $table->integer('pending_payments')->default(0);
            $table->decimal('total_payments_today', 12, 2)->default(0);
            $table->timestamps();

            $table->index('is_online');
            $table->index('last_heartbeat');
        });

        // Payment Orders - คำสั่งชำระเงินที่รอการจ่าย
        // ระบบใช้ Unique Amount เพื่อให้สามารถจับคู่ SMS กับ Order ได้อัตโนมัติ
        Schema::create('payment_orders', function (Blueprint $table) {
            $table->id();
            $table->string('order_number')->unique();
            $table->decimal('base_amount', 12, 2); // ยอดเดิมที่ลูกค้าต้องการชำระ
            $table->decimal('amount', 12, 2); // ยอดที่ต้องชำระจริง (รวมสตางค์ที่เพิ่ม)
            $table->decimal('amount_suffix', 4, 2)->default(0); // สตางค์ที่เพิ่มเพื่อให้ยอดไม่ซ้ำ
            $table->string('description', 500);
            $table->string('reference', 100)->nullable();
            $table->string('customer_name')->nullable();
            $table->string('customer_email')->nullable();
            $table->string('customer_phone', 20)->nullable();
            $table->string('callback_url', 500)->nullable();
            $table->string('status')->default('pending'); // pending, matched, paid, cancelled, expired
            $table->timestamp('expires_at')->nullable();
            $table->timestamp('matched_at')->nullable();
            $table->timestamp('paid_at')->nullable();
            $table->timestamp('cancelled_at')->nullable();
            $table->string('cancellation_reason', 500)->nullable();
            $table->foreignId('payment_id')->nullable()->constrained('sms_payments')->nullOnDelete();
            $table->foreignId('created_by')->nullable()->constrained('users')->nullOnDelete();
            $table->json('metadata')->nullable();
            $table->timestamps();

            $table->index('status');
            $table->index('amount');
            $table->index('base_amount');
            $table->index('expires_at');
            $table->index(['status', 'amount']);
            $table->index(['status', 'base_amount', 'expires_at']); // สำหรับ unique amount calculation
        });

        // SMS Payments - การชำระเงินที่ตรวจจับจาก SMS
        Schema::create('sms_payments', function (Blueprint $table) {
            $table->id();
            $table->string('device_id');
            $table->string('sms_sender', 50);
            $table->text('sms_body');
            $table->timestamp('sms_received_at');
            $table->decimal('amount', 12, 2);
            $table->string('bank_name', 100);
            $table->string('account_number', 50)->nullable();
            $table->string('transaction_ref', 100)->nullable();
            $table->decimal('confidence', 3, 2)->default(0);
            $table->string('status')->default('pending'); // pending, approved, rejected
            $table->timestamp('approved_at')->nullable();
            $table->timestamp('rejected_at')->nullable();
            $table->foreignId('approved_by')->nullable()->constrained('users')->nullOnDelete();
            $table->foreignId('rejected_by')->nullable()->constrained('users')->nullOnDelete();
            $table->string('approval_note', 500)->nullable();
            $table->string('rejection_reason', 500)->nullable();
            $table->boolean('auto_approved')->default(false);
            $table->foreignId('order_id')->nullable();
            $table->timestamps();

            $table->index('status');
            $table->index('bank_name');
            $table->index('amount');
            $table->index('device_id');
            $table->index('sms_received_at');
            $table->index(['status', 'amount']);
            $table->index('auto_approved');
        });

        // Add foreign key separately to avoid circular reference
        Schema::table('sms_payments', function (Blueprint $table) {
            $table->foreign('order_id')
                ->references('id')
                ->on('payment_orders')
                ->nullOnDelete();
        });

        // Add payment_id to payment_orders if not exists
        // (Already added in the payment_orders table above)
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::table('sms_payments', function (Blueprint $table) {
            $table->dropForeign(['order_id']);
        });

        Schema::dropIfExists('sms_payments');
        Schema::dropIfExists('payment_orders');
        Schema::dropIfExists('mobile_devices');
    }
};
