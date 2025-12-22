<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    /**
     * Run the migrations.
     * Creates activity log table for audit logging (Spatie Activity Log)
     */
    public function up(): void
    {
        Schema::connection(config('activitylog.database_connection'))
            ->create(config('activitylog.table_name', 'activity_log'), function (Blueprint $table) {
                $table->id();
                $table->string('log_name')->nullable();
                $table->text('description');
                $table->nullableMorphs('subject', 'subject');
                $table->string('event')->nullable();
                $table->nullableMorphs('causer', 'causer');
                $table->json('properties')->nullable();
                $table->uuid('batch_uuid')->nullable();
                $table->timestamps();

                $table->index('log_name');
                $table->index(['causer_type', 'causer_id']);
                $table->index(['subject_type', 'subject_id']);
            });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::connection(config('activitylog.database_connection'))
            ->dropIfExists(config('activitylog.table_name', 'activity_log'));
    }
};
