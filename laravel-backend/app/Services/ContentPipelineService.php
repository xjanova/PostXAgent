<?php

declare(strict_types=1);

namespace App\Services;

use App\Models\ApiKeyPool;
use App\Models\ApiKeyPoolMember;
use App\Models\ContentPipeline;
use App\Models\PipelineRun;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Facades\Storage;
use Illuminate\Support\Str;

class ContentPipelineService
{
    private ApiKeyRotationService $keyRotation;
    private AccountRotationService $accountRotation;
    private string $aiManagerUrl;

    public function __construct(
        ApiKeyRotationService $keyRotation,
        AccountRotationService $accountRotation
    ) {
        $this->keyRotation = $keyRotation;
        $this->accountRotation = $accountRotation;
        $this->aiManagerUrl = sprintf(
            'http://%s:%s',
            config('aimanager.primary.host', 'localhost'),
            config('aimanager.primary.api_port', '5000')
        );
    }

    // ====================================================================
    // Pipeline Lifecycle
    // ====================================================================

    /**
     * Start a new pipeline run
     */
    public function startRun(ContentPipeline $pipeline): PipelineRun
    {
        $totalSteps = $this->calculateTotalSteps($pipeline);

        $run = PipelineRun::create([
            'content_pipeline_id' => $pipeline->id,
            'status' => PipelineRun::STATUS_PENDING,
            'total_steps' => $totalSteps,
            'started_at' => now(),
        ]);

        Log::info('Pipeline run started', [
            'pipeline_id' => $pipeline->id,
            'run_id' => $run->id,
            'total_steps' => $totalSteps,
        ]);

        return $run;
    }

    /**
     * Cancel a running pipeline
     */
    public function cancelRun(PipelineRun $run): void
    {
        if ($run->canCancel()) {
            $run->markCancelled();
            Log::info('Pipeline run cancelled', ['run_id' => $run->id]);
        }
    }

    // ====================================================================
    // Step 1: Story Generation (Groq / Gemini / Mistral)
    // ====================================================================

    public function executeStoryStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;
        $run->updateProgress('story', 'กำลังสร้างเรื่องราว...');

        $prompt = $this->buildStoryPrompt($pipeline);
        $story = null;
        $retries = 3;

        // Try with API key pool rotation
        $pool = $pipeline->textPool;
        if (!$pool) {
            // Fallback: try finding any active text pool
            /** @var ApiKeyPool|null $pool */
            $pool = ApiKeyPool::active()->forServiceType('text')->first();
        }

        for ($attempt = 0; $attempt < $retries; $attempt++) {
            $key = $pool ? $this->keyRotation->getNextKey($pool) : null;

            try {
                if ($key) {
                    $story = $this->callLlmApi($prompt, $key);
                    $this->keyRotation->recordSuccess($key);
                    $run->recordApiKeyUsage($key->apiKeyPool->provider ?? 'unknown', $key->label);
                } else {
                    // Fallback: call C# AI Manager content generation
                    $story = $this->callAiManagerContentGeneration($prompt, $pipeline);
                }
                break;
            } catch (\Exception $e) {
                Log::warning("Story generation attempt {$attempt} failed", [
                    'run_id' => $run->id,
                    'error' => $e->getMessage(),
                ]);

                if ($key) {
                    if ($this->isRateLimitError($e)) {
                        $key = $this->keyRotation->handleRateLimit($key);
                    } else {
                        $key = $this->keyRotation->recordFailure($key, $e->getMessage());
                    }
                }

                if ($attempt === $retries - 1) {
                    throw $e;
                }

                sleep((int) pow(2, $attempt) * 5); // Exponential backoff
            }
        }

        if (!$story) {
            throw new \RuntimeException('Story generation failed after all retries');
        }

        $run->update(['generated_story' => $story]);
        Log::info('Story generated', ['run_id' => $run->id, 'title' => $story['title'] ?? '']);
    }

    // ====================================================================
    // Step 2: Image Generation (C# Diffusers / HuggingFace)
    // ====================================================================

    public function executeImageStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;
        $story = $run->generated_story;
        $run->updateProgress('images', 'กำลังสร้างภาพประกอบ...');

        if (!$story || empty($story['scenes'])) {
            throw new \RuntimeException('No story scenes to generate images for');
        }

        $images = [];
        $storagePath = "pipeline/{$run->id}/images";
        Storage::makeDirectory($storagePath);

        foreach ($story['scenes'] as $i => $scene) {
            $run->update(['step_message' => "กำลังสร้างภาพ scene " . ($i + 1) . "/" . count($story['scenes'])]);

            try {
                $imagePrompt = $scene['image_prompt'] ?? $scene['narration'] ?? '';
                $imageResult = $this->generateImage($imagePrompt, $pipeline);

                if ($imageResult && !empty($imageResult['image'])) {
                    $filename = "scene_{$i}.png";
                    $imagePath = "{$storagePath}/{$filename}";
                    Storage::put($imagePath, base64_decode($imageResult['image']));

                    $images[] = [
                        'scene_number' => $i,
                        'local_path' => $imagePath,
                        'provider' => $imageResult['provider'] ?? 'diffusers',
                    ];
                }
            } catch (\Exception $e) {
                Log::warning("Image generation failed for scene {$i}", [
                    'run_id' => $run->id,
                    'error' => $e->getMessage(),
                ]);
                // Continue with other scenes
            }

            // Rate limit between generations
            if ($i < count($story['scenes']) - 1) {
                sleep(2);
            }
        }

        if (empty($images)) {
            throw new \RuntimeException('No images were generated');
        }

        $run->update(['generated_images' => $images]);
        Log::info('Images generated', ['run_id' => $run->id, 'count' => count($images)]);
    }

    // ====================================================================
    // Step 3: Video Generation (SVD / Freepik / Slideshow)
    // ====================================================================

    public function executeVideoStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;
        $images = $run->generated_images;
        $run->updateProgress('video', 'กำลังสร้างวิดีโอ...');

        if (empty($images)) {
            throw new \RuntimeException('No images available for video generation');
        }

        $videoClips = [];
        $storagePath = "pipeline/{$run->id}/videos";
        Storage::makeDirectory($storagePath);

        $provider = $pipeline->video_provider;

        foreach ($images as $i => $image) {
            $run->update(['step_message' => "กำลังสร้างวิดีโอ clip " . ($i + 1) . "/" . count($images)]);

            try {
                $clipResult = match ($provider) {
                    'svd' => $this->generateVideoSvd($image, $pipeline),
                    'freepik' => $this->generateVideoFreepik($image, $pipeline, $run),
                    'slideshow' => $this->generateSlideshow($image, $pipeline),
                    default => $this->generateVideoAuto($image, $pipeline, $run),
                };

                if ($clipResult && !empty($clipResult['video_path'])) {
                    $videoClips[] = [
                        'scene_number' => $i,
                        'local_path' => $clipResult['video_path'],
                        'provider' => $clipResult['provider'] ?? $provider,
                    ];
                }
            } catch (\Exception $e) {
                Log::warning("Video generation failed for scene {$i}, falling back to slideshow", [
                    'run_id' => $run->id,
                    'error' => $e->getMessage(),
                ]);

                // Fallback to slideshow
                $clipResult = $this->generateSlideshow($image, $pipeline);
                if ($clipResult && !empty($clipResult['video_path'])) {
                    $videoClips[] = [
                        'scene_number' => $i,
                        'local_path' => $clipResult['video_path'],
                        'provider' => 'slideshow',
                    ];
                }
            }

            sleep(2);
        }

        $run->update(['generated_video_clips' => $videoClips]);
        Log::info('Video clips generated', ['run_id' => $run->id, 'count' => count($videoClips)]);
    }

    // ====================================================================
    // Step 4: Thai Voiceover (Edge TTS / Google Cloud TTS)
    // ====================================================================

    public function executeVoiceoverStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;
        $story = $run->generated_story;
        $run->updateProgress('voiceover', 'กำลังสร้างเสียงบรรยายภาษาไทย...');

        if (!$story || empty($story['scenes'])) {
            throw new \RuntimeException('No story scenes for voiceover');
        }

        // Collect narration texts (camelCase keys for C# deserialization)
        $segments = [];
        foreach ($story['scenes'] as $scene) {
            $segments[] = [
                'text' => $scene['narration'] ?? $scene['text'] ?? '',
                'sceneNumber' => $scene['scene_number'] ?? 0,
            ];
        }

        // Call C# ThaiTtsService (camelCase keys for C# deserialization)
        $response = Http::timeout(120)->post("{$this->aiManagerUrl}/api/Pipeline/generate-tts", [
            'segments' => $segments,
            'voiceId' => $pipeline->tts_voice_id,
            'provider' => $pipeline->tts_provider, // edge, google, both
            'outputDir' => storage_path("app/pipeline/{$run->id}/audio"),
        ]);

        if (!$response->successful()) {
            throw new \RuntimeException('TTS generation failed: ' . $response->body());
        }

        $ttsResult = $response->json();

        // C# returns snake_case keys in anonymous objects (audio_path, duration_seconds)
        $audioPath = $ttsResult['audio_path'] ?? null;

        // Validate TTS output
        if (empty($audioPath) || !file_exists($audioPath)) {
            throw new \RuntimeException('TTS generated empty or missing audio file');
        }

        $run->update(['generated_voiceover' => [
            'local_path' => $audioPath,
            'duration_seconds' => $ttsResult['duration_seconds'] ?? 0,
            'segments' => $ttsResult['segments'] ?? [],
            'provider' => $ttsResult['provider'] ?? 'edge',
        ]]);

        Log::info('Voiceover generated', [
            'run_id' => $run->id,
            'duration' => $ttsResult['duration_seconds'] ?? 0,
        ]);
    }

    // ====================================================================
    // Step 5: Lip Sync (SadTalker)
    // ====================================================================

    public function executeLipSyncStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;

        if (!$pipeline->enable_lip_sync) {
            $run->update(['step_message' => 'ข้าม lip sync (ปิดอยู่)']);
            Log::info('Lip sync skipped (disabled)', ['run_id' => $run->id]);
            return;
        }

        $voiceover = $run->generated_voiceover;
        if (empty($voiceover['local_path'])) {
            Log::warning('Lip sync skipped - no voiceover audio', ['run_id' => $run->id]);
            return;
        }

        $run->updateProgress('lipsync', 'กำลังซิงค์เสียงกับปากพูด...');

        // Ensure audio path is absolute
        $audioPath = $voiceover['local_path'];
        if (!str_starts_with($audioPath, '/') && !preg_match('/^[A-Z]:/i', $audioPath)) {
            $audioPath = storage_path('app/' . $audioPath);
        }

        // Ensure avatar path is absolute
        $avatarPath = $pipeline->lip_sync_avatar_path;
        if (!$avatarPath) {
            // Use first generated image as fallback avatar
            $images = $run->generated_images;
            if (!empty($images)) {
                $avatarPath = $images[0]['local_path'];
            }
        }

        if (!$avatarPath) {
            Log::warning('Lip sync skipped - no avatar image', ['run_id' => $run->id]);
            return;
        }

        if (!str_starts_with($avatarPath, '/') && !preg_match('/^[A-Z]:/i', $avatarPath)) {
            $avatarPath = storage_path('app/' . $avatarPath);
        }

        // camelCase keys for C# deserialization
        $response = Http::timeout(300)->post("{$this->aiManagerUrl}/api/Pipeline/generate-lipsync", [
            'faceImagePath' => $avatarPath,
            'audioPath' => $audioPath,
            'provider' => $pipeline->lip_sync_provider, // sadtalker, wav2lip
            'outputDir' => storage_path("app/pipeline/{$run->id}/lipsync"),
        ]);

        if (!$response->successful()) {
            Log::warning('Lip sync failed, will use original video', [
                'run_id' => $run->id,
                'error' => $response->body(),
            ]);
            return;
        }

        $lipSyncResult = $response->json();

        // C# returns snake_case keys: video_path, duration_seconds, provider
        $lipSyncVideoPath = $lipSyncResult['video_path'] ?? null;

        // Validate lipsync output
        if (empty($lipSyncVideoPath) || !file_exists($lipSyncVideoPath)) {
            Log::warning('Lip sync output file missing or empty', [
                'run_id' => $run->id,
                'path' => $lipSyncVideoPath,
            ]);
            return;
        }

        $run->update(['generated_lipsync' => [
            'local_path' => $lipSyncVideoPath,
            'duration' => $lipSyncResult['duration_seconds'] ?? 0,
            'provider' => $lipSyncResult['provider'] ?? 'sadtalker',
        ]]);

        Log::info('Lip sync generated', ['run_id' => $run->id]);
    }

    // ====================================================================
    // Step 6: Assembly (FFmpeg)
    // ====================================================================

    public function executeAssemblyStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;
        $run->updateProgress('assembly', 'กำลังประกอบวิดีโอ...');

        // Ensure all video clip paths are absolute
        $videoClips = collect($run->generated_video_clips ?? [])
            ->pluck('local_path')
            ->map(function (string $p): string {
                if (!str_starts_with($p, '/') && !preg_match('/^[A-Z]:/i', $p)) {
                    return storage_path("app/{$p}");
                }
                return $p;
            })
            ->toArray();

        $voiceover = $run->generated_voiceover;
        $lipsync = $run->generated_lipsync;
        $story = $run->generated_story;

        // Ensure voiceover path is absolute
        $voiceoverPath = $voiceover['local_path'] ?? null;
        if ($voiceoverPath && !str_starts_with($voiceoverPath, '/') && !preg_match('/^[A-Z]:/i', $voiceoverPath)) {
            $voiceoverPath = storage_path('app/' . $voiceoverPath);
        }

        // Ensure lipsync path is absolute
        $lipsyncPath = $lipsync['local_path'] ?? null;
        if ($lipsyncPath && !str_starts_with($lipsyncPath, '/') && !preg_match('/^[A-Z]:/i', $lipsyncPath)) {
            $lipsyncPath = storage_path('app/' . $lipsyncPath);
        }

        $outputDir = storage_path("app/pipeline/{$run->id}/output");
        if (!is_dir($outputDir)) {
            mkdir($outputDir, 0755, true);
        }

        // camelCase keys for C# deserialization
        $response = Http::timeout(300)->post("{$this->aiManagerUrl}/api/Pipeline/assemble-video", [
            'videoClips' => $videoClips,
            'voiceoverPath' => $voiceoverPath,
            'lipSyncVideoPath' => $lipsyncPath,
            'enableMusic' => $pipeline->enable_music,
            'musicStyle' => $pipeline->music_style,
            'musicVolume' => $pipeline->music_volume,
            'musicSource' => $pipeline->music_source,
            'aspectRatio' => $pipeline->aspect_ratio,
            'outputDir' => $outputDir,
            'title' => $story['title'] ?? 'Untitled',
        ]);

        if (!$response->successful()) {
            throw new \RuntimeException('Video assembly failed: ' . $response->body());
        }

        // C# returns snake_case keys: video_path, thumbnail_path, duration_seconds
        $assemblyResult = $response->json();

        $run->update([
            'final_video_path' => $assemblyResult['video_path'] ?? null,
            'thumbnail_path' => $assemblyResult['thumbnail_path'] ?? null,
        ]);

        Log::info('Video assembled', [
            'run_id' => $run->id,
            'video_path' => $assemblyResult['video_path'] ?? null,
        ]);
    }

    // ====================================================================
    // Step 7: Publish (TikTok / YouTube)
    // ====================================================================

    public function executePublishStep(PipelineRun $run): void
    {
        $pipeline = $run->pipeline;

        if (!$pipeline->auto_publish) {
            $run->update(['step_message' => 'ข้ามการโพสต์ (auto_publish ปิดอยู่)']);
            return;
        }

        if (!$run->final_video_path) {
            throw new \RuntimeException('No final video to publish');
        }

        $run->updateProgress('publishing', 'กำลังโพสต์เผยแพร่...');

        $story = $run->generated_story;
        $platforms = $pipeline->target_platforms;
        $results = [];

        foreach ($platforms as $platform) {
            $run->update(['step_message' => "กำลังโพสต์ไป {$platform}..."]);

            try {
                $publishResult = $this->publishToPlatform(
                    $platform,
                    $run->final_video_path,
                    $story['title'] ?? 'Untitled',
                    $story['hashtags'] ?? [],
                    $pipeline,
                    $run
                );

                $results[$platform] = $publishResult;
            } catch (\Exception $e) {
                Log::error("Publishing to {$platform} failed", [
                    'run_id' => $run->id,
                    'error' => $e->getMessage(),
                ]);
                $results[$platform] = [
                    'success' => false,
                    'error' => $e->getMessage(),
                ];
            }
        }

        $run->update(['publish_results' => $results]);
        Log::info('Publishing completed', ['run_id' => $run->id, 'results' => $results]);
    }

    // ====================================================================
    // Scheduling
    // ====================================================================

    /**
     * Check and run scheduled pipelines - called by artisan command
     */
    public function checkScheduledPipelines(): int
    {
        /** @var \Illuminate\Database\Eloquent\Collection<int, ContentPipeline> $pipelines */
        $pipelines = ContentPipeline::scheduled()->get();
        $dispatched = 0;

        /** @var ContentPipeline $pipeline */
        foreach ($pipelines as $pipeline) {
            if (!$pipeline->canRunToday()) {
                continue;
            }

            if ($pipeline->hasActiveRun()) {
                continue;
            }

            try {
                $run = $this->startRun($pipeline);
                \App\Jobs\Pipeline\RunPipelineJob::dispatch($run);

                // Update next run time
                $pipeline->update([
                    'next_run_at' => $pipeline->calculateNextRunAt(),
                ]);

                $dispatched++;
            } catch (\Exception $e) {
                Log::error('Failed to dispatch scheduled pipeline', [
                    'pipeline_id' => $pipeline->id,
                    'error' => $e->getMessage(),
                ]);
            }
        }

        return $dispatched;
    }

    // ====================================================================
    // Private Helpers
    // ====================================================================

    private function calculateTotalSteps(ContentPipeline $pipeline): int
    {
        $steps = 5; // story, images, video, voiceover, assembly
        if ($pipeline->enable_lip_sync) {
            $steps++;
        }
        if ($pipeline->auto_publish) {
            $steps++;
        }
        return $steps;
    }

    private function buildStoryPrompt(ContentPipeline $pipeline): string
    {
        if ($pipeline->content_prompt_template) {
            return $pipeline->content_prompt_template;
        }

        $type = match ($pipeline->content_type) {
            'story' => 'เรื่องเล่า/เรื่องสั้นน่าสนใจ',
            'tips' => 'เคล็ดลับและทิปส์ที่มีประโยชน์',
            'news' => 'ข่าวสารและเทรนด์ล่าสุด',
            'promotional' => 'เนื้อหาส่งเสริมการขาย',
            'educational' => 'เนื้อหาให้ความรู้',
            default => 'เนื้อหาที่น่าสนใจ',
        };

        $brandContext = '';
        if ($pipeline->brand) {
            $brandContext = "สำหรับแบรนด์: {$pipeline->brand->name}\nรายละเอียด: {$pipeline->brand->description}\n";
        }

        return <<<PROMPT
คุณเป็นนักเขียนสคริปต์วิดีโอมืออาชีพ สร้าง{$type}ภาษาไทย
{$brandContext}
สไตล์: {$pipeline->tone}
ภาษา: {$pipeline->language}

สร้างเนื้อหาสำหรับวิดีโอสั้น ({$pipeline->video_duration_seconds} วินาที) แบ่งเป็น {$pipeline->scene_count} scene

ตอบเป็น JSON เท่านั้น ในรูปแบบ:
{
    "title": "ชื่อเรื่อง",
    "scenes": [
        {
            "scene_number": 1,
            "narration": "เนื้อหาบรรยายภาษาไทยสำหรับ TTS",
            "image_prompt": "English prompt for image generation - descriptive, cinematic",
            "duration_seconds": {$pipeline->clip_duration_seconds}
        }
    ],
    "hashtags": ["#แฮชแท็ก1", "#แฮชแท็ก2"],
    "description": "คำอธิบายวิดีโอสั้นๆ สำหรับโพสต์"
}
PROMPT;
    }

    /**
     * Call LLM API (Groq/Gemini/Mistral) based on API key config
     */
    private function callLlmApi(string $prompt, ApiKeyPoolMember $key): array
    {
        /** @var ApiKeyPool|null $pool */
        $pool = $key->apiKeyPool;
        $provider = $pool ? $pool->provider : 'groq';
        $apiKey = $key->decrypted_api_key;
        $modelId = $key->model_id;
        $baseUrl = $key->base_url;

        $response = match ($provider) {
            'groq' => $this->callGroqApi($prompt, $apiKey, $modelId, $baseUrl),
            'gemini' => $this->callGeminiApi($prompt, $apiKey, $modelId),
            'mistral' => $this->callMistralApi($prompt, $apiKey, $modelId, $baseUrl),
            default => $this->callGroqApi($prompt, $apiKey, $modelId, $baseUrl),
        };

        return $response;
    }

    private function callGroqApi(string $prompt, string $apiKey, ?string $modelId = null, ?string $baseUrl = null): array
    {
        $url = ($baseUrl ?? 'https://api.groq.com/openai') . '/v1/chat/completions';

        $response = Http::withHeaders([
            'Authorization' => "Bearer {$apiKey}",
            'Content-Type' => 'application/json',
        ])->timeout(60)->post($url, [
            'model' => $modelId ?? 'llama-3.3-70b-versatile',
            'messages' => [
                ['role' => 'system', 'content' => 'You are a professional Thai content creator. Always respond in valid JSON format.'],
                ['role' => 'user', 'content' => $prompt],
            ],
            'temperature' => 0.8,
            'max_tokens' => 3000,
            'response_format' => ['type' => 'json_object'],
        ]);

        if ($response->status() === 429) {
            throw new \RuntimeException('RATE_LIMIT: ' . $response->body());
        }

        if (!$response->successful()) {
            throw new \RuntimeException('Groq API error: ' . $response->body());
        }

        $content = $response->json('choices.0.message.content', '{}');
        $parsed = json_decode($content, true);

        if (!$parsed || empty($parsed['scenes'])) {
            throw new \RuntimeException('Invalid story JSON from Groq');
        }

        return $parsed;
    }

    private function callGeminiApi(string $prompt, string $apiKey, ?string $modelId = null): array
    {
        $model = $modelId ?? 'gemini-2.0-flash-lite';
        $url = "https://generativelanguage.googleapis.com/v1beta/models/{$model}:generateContent";

        $response = Http::withHeaders([
            'x-goog-api-key' => $apiKey,
        ])->timeout(60)->post($url, [
            'contents' => [
                ['role' => 'user', 'parts' => [['text' => $prompt]]],
            ],
            'generationConfig' => [
                'temperature' => 0.8,
                'maxOutputTokens' => 3000,
                'responseMimeType' => 'application/json',
            ],
        ]);

        if ($response->status() === 429) {
            throw new \RuntimeException('RATE_LIMIT: ' . $response->body());
        }

        if (!$response->successful()) {
            throw new \RuntimeException('Gemini API error: ' . $response->body());
        }

        $content = $response->json('candidates.0.content.parts.0.text', '{}');
        $parsed = json_decode($content, true);

        if (!$parsed || empty($parsed['scenes'])) {
            throw new \RuntimeException('Invalid story JSON from Gemini');
        }

        return $parsed;
    }

    private function callMistralApi(string $prompt, string $apiKey, ?string $modelId = null, ?string $baseUrl = null): array
    {
        $url = ($baseUrl ?? 'https://api.mistral.ai') . '/v1/chat/completions';

        $response = Http::withHeaders([
            'Authorization' => "Bearer {$apiKey}",
        ])->timeout(60)->post($url, [
            'model' => $modelId ?? 'mistral-small-latest',
            'messages' => [
                ['role' => 'system', 'content' => 'You are a professional Thai content creator. Always respond in valid JSON format.'],
                ['role' => 'user', 'content' => $prompt],
            ],
            'temperature' => 0.8,
            'max_tokens' => 3000,
            'response_format' => ['type' => 'json_object'],
        ]);

        if ($response->status() === 429) {
            throw new \RuntimeException('RATE_LIMIT: ' . $response->body());
        }

        if (!$response->successful()) {
            throw new \RuntimeException('Mistral API error: ' . $response->body());
        }

        $content = $response->json('choices.0.message.content', '{}');
        $parsed = json_decode($content, true);

        if (!$parsed || empty($parsed['scenes'])) {
            throw new \RuntimeException('Invalid story JSON from Mistral');
        }

        return $parsed;
    }

    /**
     * Fallback: call C# AI Manager for content generation
     */
    private function callAiManagerContentGeneration(string $prompt, ContentPipeline $pipeline): array
    {
        $response = Http::timeout(60)->post("{$this->aiManagerUrl}/api/Tasks/generate-content", [
            'prompt' => $prompt,
            'language' => $pipeline->language,
            'format' => 'json',
        ]);

        if (!$response->successful()) {
            throw new \RuntimeException('AI Manager content generation failed');
        }

        return $response->json('data', []);
    }

    /**
     * Generate image via C# Diffusers engine
     */
    private function generateImage(string $prompt, ContentPipeline $pipeline): ?array
    {
        $diffusersPort = config('aimanager.primary.diffusers_port', '5050');
        $diffusersUrl = sprintf(
            'http://%s:%s',
            config('aimanager.primary.host', 'localhost'),
            $diffusersPort
        );

        // Style prefix for image prompt
        $styledPrompt = "{$pipeline->image_style} style, {$prompt}, high quality, detailed";

        $response = Http::timeout(120)->post("{$diffusersUrl}/generate/image", [
            'prompt' => $styledPrompt,
            'negative_prompt' => 'blurry, low quality, distorted, deformed, ugly',
            'width' => $pipeline->image_width,
            'height' => $pipeline->image_height,
            'steps' => 25,
            'guidance_scale' => 7.5,
        ]);

        if (!$response->successful()) {
            // Fallback to AI Manager image generation
            $response = Http::timeout(120)->post("{$this->aiManagerUrl}/api/MediaGeneration/generate-image", [
                'prompt' => $styledPrompt,
                'width' => $pipeline->image_width,
                'height' => $pipeline->image_height,
            ]);

            if (!$response->successful()) {
                return null;
            }
        }

        $data = $response->json();
        return [
            'image' => $data['images'][0] ?? $data['image'] ?? null,
            'provider' => $data['provider'] ?? 'diffusers',
        ];
    }

    /**
     * Generate video from image using SVD (Stable Video Diffusion)
     */
    private function generateVideoSvd(array $image, ContentPipeline $pipeline): ?array
    {
        $diffusersPort = config('aimanager.primary.diffusers_port', '5050');
        $url = sprintf(
            'http://%s:%s/generate/video',
            config('aimanager.primary.host', 'localhost'),
            $diffusersPort
        );

        $imageData = Storage::get($image['local_path']);
        $base64 = base64_encode($imageData);

        $response = Http::timeout(300)->post($url, [
            'image' => "data:image/png;base64,{$base64}",
            'num_frames' => min(25, $pipeline->clip_duration_seconds * 5),
            'fps' => 8,
        ]);

        if (!$response->successful()) {
            return null;
        }

        $data = $response->json();
        if (empty($data['frames'])) {
            return null;
        }

        // Save video
        $videoPath = "pipeline/{$image['scene_number']}/videos/clip_{$image['scene_number']}.mp4";
        // The C# side handles frame assembly to video file
        return [
            'video_path' => $data['video_path'] ?? $videoPath,
            'provider' => 'svd',
        ];
    }

    /**
     * Generate video via Freepik browser automation
     */
    private function generateVideoFreepik(array $image, ContentPipeline $pipeline, PipelineRun $run): ?array
    {
        // Ensure image path is absolute
        $imagePath = $image['local_path'];
        if (!str_starts_with($imagePath, '/') && !preg_match('/^[A-Z]:/i', $imagePath)) {
            $imagePath = storage_path('app/' . $imagePath);
        }

        // Use existing web automation system (FreepikWorker + BrowserController)
        $response = Http::timeout(300)->post("{$this->aiManagerUrl}/api/WebAutomation/execute-workflow", [
            'platform' => 'freepik',
            'workflow_type' => 'video_generation',
            'payload' => [
                'image_path' => $imagePath,
                'prompt' => $run->generated_story['scenes'][$image['scene_number']]['narration'] ?? '',
                'duration_seconds' => $pipeline->clip_duration_seconds,
            ],
        ]);

        if (!$response->successful()) {
            return null;
        }

        return [
            'video_path' => $response->json('video_path'),
            'provider' => 'freepik',
        ];
    }

    /**
     * Generate slideshow from static image (Ken Burns effect via FFmpeg)
     */
    private function generateSlideshow(array $image, ContentPipeline $pipeline): ?array
    {
        // Ensure image path is absolute
        $imagePath = $image['local_path'];
        if (!str_starts_with($imagePath, '/') && !preg_match('/^[A-Z]:/i', $imagePath)) {
            $imagePath = storage_path('app/' . $imagePath);
        }

        // camelCase keys for C# deserialization
        $response = Http::timeout(60)->post("{$this->aiManagerUrl}/api/Pipeline/create-slideshow", [
            'imagePath' => $imagePath,
            'durationSeconds' => $pipeline->clip_duration_seconds,
            'effect' => 'ken_burns', // zoom/pan effect
            'outputDir' => storage_path("app/pipeline/{$image['scene_number']}/videos"),
        ]);

        if (!$response->successful()) {
            return null;
        }

        return [
            'video_path' => $response->json('video_path'),
            'provider' => 'slideshow',
        ];
    }

    /**
     * Auto-select video provider based on availability
     */
    private function generateVideoAuto(array $image, ContentPipeline $pipeline, PipelineRun $run): ?array
    {
        // Try SVD first (local GPU)
        $result = $this->generateVideoSvd($image, $pipeline);
        if ($result) {
            return $result;
        }

        // Try Freepik browser automation
        $result = $this->generateVideoFreepik($image, $pipeline, $run);
        if ($result) {
            return $result;
        }

        // Fallback to slideshow
        return $this->generateSlideshow($image, $pipeline);
    }

    /**
     * Publish video to a specific platform
     */
    private function publishToPlatform(
        string $platform,
        string $videoPath,
        string $title,
        array $hashtags,
        ContentPipeline $pipeline,
        PipelineRun $run
    ): array {
        // Use account pool rotation for publishing
        $pool = $pipeline->publishAccountPool;
        $account = null;

        if ($pool) {
            $account = $this->accountRotation->getNextAccount($pool);
        }

        $description = ($run->generated_story['description'] ?? $title)
            . "\n\n" . implode(' ', $hashtags);

        // Ensure video path is absolute
        $absoluteVideoPath = $videoPath;
        if (!str_starts_with($absoluteVideoPath, '/') && !preg_match('/^[A-Z]:/i', $absoluteVideoPath)) {
            $absoluteVideoPath = storage_path('app/' . $absoluteVideoPath);
        }

        // Ensure thumbnail path is absolute
        $absoluteThumbnailPath = $run->thumbnail_path;
        if ($absoluteThumbnailPath && !str_starts_with($absoluteThumbnailPath, '/') && !preg_match('/^[A-Z]:/i', $absoluteThumbnailPath)) {
            $absoluteThumbnailPath = storage_path('app/' . $absoluteThumbnailPath);
        }

        // camelCase keys for C# deserialization
        $response = Http::timeout(120)->post("{$this->aiManagerUrl}/api/Pipeline/publish-video", [
            'platform' => $platform,
            'videoPath' => $absoluteVideoPath,
            'title' => $title,
            'description' => $description,
            'hashtags' => $hashtags,
            'accountId' => $account?->social_account_id,
            'thumbnailPath' => $absoluteThumbnailPath,
        ]);

        if (!$response->successful()) {
            if ($account) {
                $this->accountRotation->recordFailure($account, $response->body());
            }
            throw new \RuntimeException("Publishing to {$platform} failed: " . $response->body());
        }

        if ($account) {
            $this->accountRotation->recordSuccess($account);
        }

        return [
            'success' => true,
            'post_id' => $response->json('post_id'),
            'url' => $response->json('post_url'),
            'platform' => $platform,
        ];
    }

    private function isRateLimitError(\Exception $e): bool
    {
        $message = strtolower($e->getMessage());
        return str_contains($message, 'rate_limit')
            || str_contains($message, '429')
            || str_contains($message, 'too many requests')
            || str_contains($message, 'quota');
    }
}
