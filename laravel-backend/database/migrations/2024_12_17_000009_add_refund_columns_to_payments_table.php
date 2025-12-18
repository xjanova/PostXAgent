<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     */
    public function up(): void
    {
        Schema::table('payments', function (Blueprint $table) {
            if (!Schema::hasColumn('payments', 'original_payment_id')) {
                $table->foreignId('original_payment_id')
                    ->nullable()
                    ->after('user_rental_id')
                    ->constrained('payments')
                    ->onDelete('set null');
            }

            if (!Schema::hasColumn('payments', 'type')) {
                $table->string('type')->default('payment')->after('original_payment_id');
                // Types: payment, refund
            }

            if (!Schema::hasColumn('payments', 'notes')) {
                $table->text('notes')->nullable()->after('description');
            }

            if (!Schema::hasColumn('payments', 'refund_details')) {
                $table->json('refund_details')->nullable()->after('metadata');
            }

            // Index for refund lookup
            $table->index('original_payment_id');
            $table->index('type');
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::table('payments', function (Blueprint $table) {
            $table->dropIndex(['original_payment_id']);
            $table->dropIndex(['type']);

            if (Schema::hasColumn('payments', 'original_payment_id')) {
                $table->dropForeign(['original_payment_id']);
                $table->dropColumn('original_payment_id');
            }
            if (Schema::hasColumn('payments', 'type')) {
                $table->dropColumn('type');
            }
            if (Schema::hasColumn('payments', 'notes')) {
                $table->dropColumn('notes');
            }
            if (Schema::hasColumn('payments', 'refund_details')) {
                $table->dropColumn('refund_details');
            }
        });
    }
};
