<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     * ระบบให้เช่าแพ็กเกจสำหรับตลาดไทย
     */
    public function up(): void
    {
        // Rental Packages - แพ็กเกจให้เช่า
        Schema::create('rental_packages', function (Blueprint $table) {
            $table->id();
            $table->string('name');                           // ชื่อแพ็กเกจ
            $table->string('name_th')->nullable();            // ชื่อภาษาไทย
            $table->text('description')->nullable();
            $table->text('description_th')->nullable();
            $table->enum('duration_type', ['hourly', 'daily', 'weekly', 'monthly', 'yearly']);
            $table->integer('duration_value')->default(1);    // จำนวน (1 วัน, 7 วัน, etc.)
            $table->decimal('price', 10, 2);                  // ราคา (THB)
            $table->decimal('original_price', 10, 2)->nullable(); // ราคาเต็มก่อนลด
            $table->string('currency', 3)->default('THB');

            // Quotas & Limits
            $table->integer('posts_limit')->default(-1);       // -1 = unlimited
            $table->integer('brands_limit')->default(1);
            $table->integer('platforms_limit')->default(9);
            $table->integer('ai_generations_limit')->default(-1);
            $table->integer('accounts_per_platform')->default(1);
            $table->integer('scheduled_posts_limit')->default(-1);
            $table->integer('team_members_limit')->default(1);

            // Features (JSON)
            $table->json('features')->nullable();              // รายละเอียด features
            $table->json('included_platforms')->nullable();    // platforms ที่รวม

            // Status & Display
            $table->boolean('is_active')->default(true);
            $table->boolean('is_featured')->default(false);    // แสดงหน้าแรก
            $table->boolean('is_popular')->default(false);     // ติดป้าย "ยอดนิยม"
            $table->integer('sort_order')->default(0);

            // Trial
            $table->boolean('has_trial')->default(false);
            $table->integer('trial_days')->default(0);

            $table->timestamps();
            $table->softDeletes();

            $table->index(['is_active', 'sort_order']);
        });

        // User Rentals - การเช่าของผู้ใช้
        Schema::create('user_rentals', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('rental_package_id')->constrained()->onDelete('cascade');

            // Rental Period
            $table->timestamp('starts_at');
            $table->timestamp('expires_at');
            $table->timestamp('cancelled_at')->nullable();

            // Status
            $table->enum('status', [
                'pending',      // รอชำระเงิน
                'active',       // ใช้งานอยู่
                'expired',      // หมดอายุ
                'cancelled',    // ยกเลิก
                'suspended'     // ระงับชั่วคราว
            ])->default('pending');

            // Payment Info
            $table->decimal('amount_paid', 10, 2);
            $table->string('currency', 3)->default('THB');
            $table->string('payment_method')->nullable();      // promptpay, bank_transfer, card, etc.
            $table->string('payment_reference')->nullable();   // Reference number

            // Usage Tracking
            $table->integer('posts_used')->default(0);
            $table->integer('ai_generations_used')->default(0);
            $table->json('usage_stats')->nullable();

            // Auto-renewal
            $table->boolean('auto_renew')->default(false);
            $table->timestamp('next_renewal_at')->nullable();

            // Metadata
            $table->json('metadata')->nullable();
            $table->text('notes')->nullable();

            $table->timestamps();
            $table->softDeletes();

            $table->index(['user_id', 'status']);
            $table->index(['expires_at', 'status']);
            $table->index('payment_reference');
        });

        // Payments - ประวัติการชำระเงิน
        Schema::create('payments', function (Blueprint $table) {
            $table->id();
            $table->uuid('uuid')->unique();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('user_rental_id')->nullable()->constrained()->onDelete('set null');

            // Payment Details
            $table->decimal('amount', 10, 2);
            $table->decimal('fee', 10, 2)->default(0);         // ค่าธรรมเนียม
            $table->decimal('net_amount', 10, 2);              // จำนวนสุทธิ
            $table->string('currency', 3)->default('THB');

            // Payment Method
            $table->enum('payment_method', [
                'promptpay',
                'bank_transfer',
                'credit_card',
                'debit_card',
                'truemoney',
                'linepay',
                'shopeepay',
                'stripe',
                'manual'
            ]);

            // Status
            $table->enum('status', [
                'pending',
                'processing',
                'completed',
                'failed',
                'refunded',
                'cancelled'
            ])->default('pending');

            // Gateway Info
            $table->string('gateway')->nullable();              // omise, 2c2p, stripe, etc.
            $table->string('gateway_reference')->nullable();    // Transaction ID from gateway
            $table->string('gateway_status')->nullable();
            $table->json('gateway_response')->nullable();

            // Thai Payment Specific
            $table->string('promptpay_qr_url')->nullable();
            $table->string('bank_account_number')->nullable();
            $table->string('bank_name')->nullable();
            $table->string('transfer_slip_url')->nullable();

            // Verification
            $table->timestamp('paid_at')->nullable();
            $table->timestamp('verified_at')->nullable();
            $table->foreignId('verified_by')->nullable()->constrained('users')->onDelete('set null');

            // Metadata
            $table->string('description')->nullable();
            $table->json('metadata')->nullable();
            $table->text('admin_notes')->nullable();

            $table->timestamps();
            $table->softDeletes();

            $table->index(['user_id', 'status']);
            $table->index(['payment_method', 'status']);
            $table->index('gateway_reference');
        });

        // Promo Codes - รหัสส่วนลด
        Schema::create('promo_codes', function (Blueprint $table) {
            $table->id();
            $table->string('code')->unique();
            $table->string('name');
            $table->text('description')->nullable();

            // Discount
            $table->enum('discount_type', ['percentage', 'fixed']);
            $table->decimal('discount_value', 10, 2);
            $table->decimal('max_discount', 10, 2)->nullable(); // สูงสุดที่ลดได้
            $table->decimal('min_purchase', 10, 2)->default(0); // ยอดขั้นต่ำ

            // Restrictions
            $table->json('applicable_packages')->nullable();    // Package IDs ที่ใช้ได้
            $table->boolean('first_purchase_only')->default(false);
            $table->integer('max_uses')->nullable();            // จำกัดการใช้ทั้งหมด
            $table->integer('max_uses_per_user')->default(1);
            $table->integer('times_used')->default(0);

            // Validity
            $table->timestamp('starts_at')->nullable();
            $table->timestamp('expires_at')->nullable();
            $table->boolean('is_active')->default(true);

            $table->timestamps();
            $table->softDeletes();

            $table->index(['code', 'is_active']);
            $table->index(['starts_at', 'expires_at']);
        });

        // Promo Code Usage - ประวัติการใช้โค้ด
        Schema::create('promo_code_usages', function (Blueprint $table) {
            $table->id();
            $table->foreignId('promo_code_id')->constrained()->onDelete('cascade');
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('payment_id')->nullable()->constrained()->onDelete('set null');
            $table->decimal('discount_amount', 10, 2);
            $table->timestamps();

            $table->index(['promo_code_id', 'user_id']);
        });

        // Invoices - ใบแจ้งหนี้/ใบเสร็จ
        Schema::create('invoices', function (Blueprint $table) {
            $table->id();
            $table->string('invoice_number')->unique();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('payment_id')->nullable()->constrained()->onDelete('set null');
            $table->foreignId('user_rental_id')->nullable()->constrained()->onDelete('set null');

            // Invoice Details
            $table->enum('type', ['invoice', 'receipt', 'tax_invoice']);
            $table->enum('status', ['draft', 'sent', 'paid', 'void'])->default('draft');

            // Amounts
            $table->decimal('subtotal', 10, 2);
            $table->decimal('discount', 10, 2)->default(0);
            $table->decimal('vat', 10, 2)->default(0);          // VAT 7%
            $table->decimal('total', 10, 2);
            $table->string('currency', 3)->default('THB');

            // Tax Invoice Info (สำหรับใบกำกับภาษี)
            $table->string('tax_id')->nullable();               // เลขประจำตัวผู้เสียภาษี
            $table->string('company_name')->nullable();
            $table->text('company_address')->nullable();
            $table->string('branch_name')->nullable();          // สาขา

            // Line Items (JSON)
            $table->json('line_items');

            // Dates
            $table->date('issue_date');
            $table->date('due_date')->nullable();
            $table->timestamp('paid_at')->nullable();

            // PDF
            $table->string('pdf_url')->nullable();

            $table->timestamps();
            $table->softDeletes();

            $table->index(['user_id', 'type']);
            $table->index('invoice_number');
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('invoices');
        Schema::dropIfExists('promo_code_usages');
        Schema::dropIfExists('promo_codes');
        Schema::dropIfExists('payments');
        Schema::dropIfExists('user_rentals');
        Schema::dropIfExists('rental_packages');
    }
};
