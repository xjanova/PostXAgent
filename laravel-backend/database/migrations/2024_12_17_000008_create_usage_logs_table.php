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
        Schema::create('usage_logs', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_rental_id')->constrained()->onDelete('cascade');
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->string('type'); // post, ai_generation, brand, platform, team_member, extension, reset, adjustment
            $table->integer('amount')->default(1);
            $table->string('description')->nullable();
            $table->json('metadata')->nullable();
            $table->timestamps();

            // Indexes
            $table->index(['user_rental_id', 'type']);
            $table->index(['user_id', 'created_at']);
            $table->index('type');
        });

        // Add additional usage columns to user_rentals if not exists
        Schema::table('user_rentals', function (Blueprint $table) {
            if (!Schema::hasColumn('user_rentals', 'usage_posts')) {
                $table->integer('usage_posts')->default(0)->after('ai_generations_used');
            }
            if (!Schema::hasColumn('user_rentals', 'usage_ai_generations')) {
                $table->integer('usage_ai_generations')->default(0)->after('usage_posts');
            }
            if (!Schema::hasColumn('user_rentals', 'usage_brands')) {
                $table->integer('usage_brands')->default(0)->after('usage_ai_generations');
            }
            if (!Schema::hasColumn('user_rentals', 'usage_platforms')) {
                $table->integer('usage_platforms')->default(0)->after('usage_brands');
            }
            if (!Schema::hasColumn('user_rentals', 'usage_team_members')) {
                $table->integer('usage_team_members')->default(0)->after('usage_platforms');
            }
        });
    }

    /**
     * Reverse the migrations.
     */
    public function down(): void
    {
        Schema::dropIfExists('usage_logs');

        Schema::table('user_rentals', function (Blueprint $table) {
            $columns = ['usage_posts', 'usage_ai_generations', 'usage_brands', 'usage_platforms', 'usage_team_members'];
            foreach ($columns as $column) {
                if (Schema::hasColumn('user_rentals', $column)) {
                    $table->dropColumn($column);
                }
            }
        });
    }
};
