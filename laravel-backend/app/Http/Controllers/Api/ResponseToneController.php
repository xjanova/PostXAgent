<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\ResponseTone;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Auth;

class ResponseToneController extends Controller
{
    /**
     * List all response tones for user
     */
    public function index(Request $request): JsonResponse
    {
        $tones = ResponseTone::query()
            ->where(function ($query) {
                $query->whereNull('user_id') // System presets
                    ->orWhere('user_id', Auth::id());
            })
            ->when($request->input('brand_id'), fn($q, $brandId) => $q->where('brand_id', $brandId))
            ->when($request->has('is_active'), fn($q) => $q->where('is_active', $request->boolean('is_active')))
            ->orderByDesc('is_default')
            ->orderBy('name')
            ->get();

        return response()->json([
            'success' => true,
            'data' => $tones,
        ]);
    }

    /**
     * Get preset tones
     */
    public function presets(): JsonResponse
    {
        $presets = [
            [
                'name' => 'เป็นกันเอง',
                'name_en' => 'friendly',
                'description' => 'น้ำเสียงเป็นกันเอง อบอุ่น ใกล้ชิด',
                'traits' => ['friendly' => 80, 'formal' => 20, 'humor' => 50, 'emoji_usage' => 60],
            ],
            [
                'name' => 'มืออาชีพ',
                'name_en' => 'professional',
                'description' => 'น้ำเสียงสุภาพ เป็นทางการ น่าเชื่อถือ',
                'traits' => ['friendly' => 40, 'formal' => 80, 'humor' => 10, 'emoji_usage' => 20],
            ],
            [
                'name' => 'สบายๆ',
                'name_en' => 'casual',
                'description' => 'น้ำเสียงสบายๆ ไม่เป็นทางการ เหมือนคุยกับเพื่อน',
                'traits' => ['friendly' => 90, 'formal' => 10, 'humor' => 70, 'emoji_usage' => 80],
            ],
            [
                'name' => 'ห่วงใย',
                'name_en' => 'supportive',
                'description' => 'น้ำเสียงเข้าใจ ใส่ใจ ให้กำลังใจ',
                'traits' => ['friendly' => 85, 'formal' => 30, 'humor' => 20, 'emoji_usage' => 50],
            ],
            [
                'name' => 'ขายของ',
                'name_en' => 'sales',
                'description' => 'น้ำเสียงกระตือรือร้น โน้มน้าว เน้นประโยชน์',
                'traits' => ['friendly' => 70, 'formal' => 40, 'humor' => 30, 'emoji_usage' => 50],
            ],
            [
                'name' => 'ขำขัน',
                'name_en' => 'humorous',
                'description' => 'น้ำเสียงสนุกสนาน ตลกขำขัน',
                'traits' => ['friendly' => 80, 'formal' => 10, 'humor' => 90, 'emoji_usage' => 70],
            ],
        ];

        return response()->json([
            'success' => true,
            'data' => $presets,
        ]);
    }

    /**
     * Create a new response tone
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:100',
            'description' => 'nullable|string|max:500',
            'brand_id' => 'nullable|exists:brands,id',
            'traits' => 'required|array',
            'traits.friendly' => 'required|integer|min:0|max:100',
            'traits.formal' => 'required|integer|min:0|max:100',
            'traits.humor' => 'required|integer|min:0|max:100',
            'traits.emoji_usage' => 'required|integer|min:0|max:100',
            'language_style' => 'nullable|array',
            'language_style.use_particles' => 'boolean',
            'language_style.particle_gender' => 'in:male,female,neutral',
            'language_style.formality_level' => 'in:casual,neutral,formal',
            'custom_instructions' => 'nullable|string|max:1000',
            'example_responses' => 'nullable|array',
            'is_default' => 'boolean',
        ]);

        // If setting as default, unset other defaults
        if ($request->boolean('is_default')) {
            ResponseTone::where('user_id', Auth::id())
                ->when($request->input('brand_id'), fn($q, $brandId) => $q->where('brand_id', $brandId))
                ->update(['is_default' => false]);
        }

        $tone = ResponseTone::create([
            'user_id' => Auth::id(),
            'brand_id' => $validated['brand_id'] ?? null,
            'name' => $validated['name'],
            'description' => $validated['description'] ?? null,
            'traits' => $validated['traits'],
            'language_style' => $validated['language_style'] ?? [
                'use_particles' => true,
                'particle_gender' => 'neutral',
                'formality_level' => 'neutral',
            ],
            'custom_instructions' => $validated['custom_instructions'] ?? null,
            'example_responses' => $validated['example_responses'] ?? [],
            'is_default' => $validated['is_default'] ?? false,
            'is_active' => true,
        ]);

        return response()->json([
            'success' => true,
            'data' => $tone,
            'message' => 'สร้างโทนการตอบใหม่แล้ว',
        ], 201);
    }

    /**
     * Get a response tone
     */
    public function show(ResponseTone $responseTone): JsonResponse
    {
        // Check access
        if ($responseTone->user_id && $responseTone->user_id !== Auth::id()) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่มีสิทธิ์เข้าถึง',
            ], 403);
        }

        return response()->json([
            'success' => true,
            'data' => $responseTone,
        ]);
    }

    /**
     * Update a response tone
     */
    public function update(Request $request, ResponseTone $responseTone): JsonResponse
    {
        // Can only update own tones
        if ($responseTone->user_id !== Auth::id()) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่มีสิทธิ์แก้ไข',
            ], 403);
        }

        $validated = $request->validate([
            'name' => 'string|max:100',
            'description' => 'nullable|string|max:500',
            'traits' => 'array',
            'traits.friendly' => 'integer|min:0|max:100',
            'traits.formal' => 'integer|min:0|max:100',
            'traits.humor' => 'integer|min:0|max:100',
            'traits.emoji_usage' => 'integer|min:0|max:100',
            'language_style' => 'nullable|array',
            'custom_instructions' => 'nullable|string|max:1000',
            'example_responses' => 'nullable|array',
            'is_default' => 'boolean',
            'is_active' => 'boolean',
        ]);

        // If setting as default, unset other defaults
        if ($request->boolean('is_default')) {
            ResponseTone::where('user_id', Auth::id())
                ->where('id', '!=', $responseTone->id)
                ->when($responseTone->brand_id, fn($q) => $q->where('brand_id', $responseTone->brand_id))
                ->update(['is_default' => false]);
        }

        $responseTone->update($validated);

        return response()->json([
            'success' => true,
            'data' => $responseTone->fresh(),
            'message' => 'อัปเดตโทนการตอบแล้ว',
        ]);
    }

    /**
     * Delete a response tone
     */
    public function destroy(ResponseTone $responseTone): JsonResponse
    {
        // Can only delete own tones
        if ($responseTone->user_id !== Auth::id()) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่มีสิทธิ์ลบ',
            ], 403);
        }

        $responseTone->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบโทนการตอบแล้ว',
        ]);
    }

    /**
     * Clone a preset or existing tone
     */
    public function clone(Request $request, ResponseTone $responseTone): JsonResponse
    {
        // Check access for non-system tones
        if ($responseTone->user_id && $responseTone->user_id !== Auth::id()) {
            return response()->json([
                'success' => false,
                'error' => 'ไม่มีสิทธิ์คัดลอก',
            ], 403);
        }

        $validated = $request->validate([
            'name' => 'nullable|string|max:100',
            'brand_id' => 'nullable|exists:brands,id',
        ]);

        $newTone = $responseTone->replicate();
        $newTone->user_id = Auth::id();
        $newTone->brand_id = $validated['brand_id'] ?? null;
        $newTone->name = $validated['name'] ?? $responseTone->name . ' (สำเนา)';
        $newTone->is_default = false;
        $newTone->save();

        return response()->json([
            'success' => true,
            'data' => $newTone,
            'message' => 'คัดลอกโทนการตอบแล้ว',
        ], 201);
    }

    /**
     * Test tone with sample input
     */
    public function test(Request $request, ResponseTone $responseTone): JsonResponse
    {
        $validated = $request->validate([
            'comment' => 'required|string|max:1000',
            'context' => 'nullable|string|max:500',
        ]);

        // This would integrate with AI Manager to generate sample response
        $sampleResponse = $this->generateTestResponse($responseTone, $validated['comment'], $validated['context'] ?? '');

        return response()->json([
            'success' => true,
            'data' => [
                'input' => $validated['comment'],
                'response' => $sampleResponse,
                'tone' => $responseTone->name,
            ],
        ]);
    }

    /**
     * Generate a test response (simplified - in production would use AI)
     */
    private function generateTestResponse(ResponseTone $responseTone, string $comment, string $context): string
    {
        $traits = $responseTone->traits;
        $style = $responseTone->language_style ?? [];

        // Build a simple sample based on traits
        $greeting = $traits['friendly'] > 60 ? 'สวัสดีค่ะ/ครับ ' : '';
        $emoji = $traits['emoji_usage'] > 50 ? ' 😊' : '';

        $particle = match($style['particle_gender'] ?? 'neutral') {
            'male' => 'ครับ',
            'female' => 'ค่ะ',
            default => 'ครับ/ค่ะ',
        };

        if ($traits['formal'] > 60) {
            return "{$greeting}ขอบคุณสำหรับความคิดเห็น{$particle} เราจะนำไปปรับปรุงต่อไป{$particle}{$emoji}";
        } elseif ($traits['humor'] > 60) {
            return "{$greeting}โอ้โห ขอบคุณมากๆ เลย{$particle} ดีใจจริงๆ 555{$emoji}";
        } else {
            return "{$greeting}ขอบคุณนะ{$particle} ยินดีมากเลย{$emoji}";
        }
    }
}
