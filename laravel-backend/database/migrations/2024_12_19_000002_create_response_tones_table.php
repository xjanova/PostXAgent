<?php

use Illuminate\Database\Migrations\Migration;
use Illuminate\Database\Schema\Blueprint;
use Illuminate\Support\Facades\Schema;

return new class extends Migration
{
    public function up(): void
    {
        Schema::create('response_tones', function (Blueprint $table) {
            $table->id();
            $table->foreignId('user_id')->constrained()->onDelete('cascade');
            $table->foreignId('brand_id')->nullable()->constrained()->onDelete('cascade');

            $table->string('name');
            $table->string('name_th')->nullable();
            $table->text('description')->nullable();

            // Personality traits (0-100 scale)
            // {friendly: 80, formal: 20, humor: 50, emoji_usage: 70, enthusiasm: 60}
            $table->json('personality_traits');

            // Language preferences
            // {default_language: 'th', mix_languages: true, use_honorifics: true, use_particles: true}
            $table->json('language_preferences');

            // Response templates for different scenarios
            // {greeting: [...], thank_you: [...], question_response: [...], apology: [...]}
            $table->json('response_templates')->nullable();

            // Keywords that trigger special responses
            // {complaint: {response: "...", priority: 10}, praise: {response: "...", priority: 5}}
            $table->json('keyword_triggers')->nullable();

            // Restrictions
            $table->json('prohibited_words')->nullable();
            $table->json('required_elements')->nullable();

            // Custom AI instructions
            $table->text('custom_instructions')->nullable();

            // Settings
            $table->boolean('is_default')->default(false);
            $table->boolean('is_active')->default(true);
            $table->boolean('auto_reply_enabled')->default(false);
            $table->integer('reply_delay_seconds')->default(60);

            $table->timestamps();

            $table->index(['user_id', 'is_active']);
            $table->index(['brand_id', 'is_default']);
        });
    }

    public function down(): void
    {
        Schema::dropIfExists('response_tones');
    }
};
