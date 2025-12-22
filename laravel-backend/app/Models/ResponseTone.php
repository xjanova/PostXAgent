<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class ResponseTone extends Model
{
    use HasFactory;

    protected $fillable = [
        'user_id',
        'brand_id',
        'name',
        'name_th',
        'description',
        'personality_traits',
        'language_preferences',
        'response_templates',
        'keyword_triggers',
        'prohibited_words',
        'required_elements',
        'custom_instructions',
        'is_default',
        'is_active',
        'auto_reply_enabled',
        'reply_delay_seconds',
    ];

    protected $casts = [
        'personality_traits' => 'array',
        'language_preferences' => 'array',
        'response_templates' => 'array',
        'keyword_triggers' => 'array',
        'prohibited_words' => 'array',
        'required_elements' => 'array',
        'is_default' => 'boolean',
        'is_active' => 'boolean',
        'auto_reply_enabled' => 'boolean',
        'reply_delay_seconds' => 'integer',
    ];

    // Default personality traits
    const DEFAULT_TRAITS = [
        'friendly' => 70,      // 0-100: cold to warm
        'formal' => 30,        // 0-100: casual to formal
        'humor' => 40,         // 0-100: serious to humorous
        'emoji_usage' => 50,   // 0-100: none to heavy
        'enthusiasm' => 60,    // 0-100: calm to excited
        'empathy' => 70,       // 0-100: detached to empathetic
    ];

    // Default language preferences
    const DEFAULT_LANGUAGE = [
        'default_language' => 'th',
        'mix_languages' => true,
        'use_honorifics' => true,
        'use_particles' => true, // Thai particles like ครับ/ค่ะ/นะ
    ];

    // Preset tone templates
    const PRESET_FRIENDLY = 'friendly';
    const PRESET_PROFESSIONAL = 'professional';
    const PRESET_CASUAL = 'casual';
    const PRESET_SUPPORTIVE = 'supportive';
    const PRESET_SALES = 'sales';

    // ═══════════════════════════════════════════════════════════════
    // Relationships
    // ═══════════════════════════════════════════════════════════════

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function brand(): BelongsTo
    {
        return $this->belongsTo(Brand::class);
    }

    // ═══════════════════════════════════════════════════════════════
    // Scopes
    // ═══════════════════════════════════════════════════════════════

    public function scopeActive($query)
    {
        return $query->where('is_active', true);
    }

    public function scopeDefault($query)
    {
        return $query->where('is_default', true);
    }

    public function scopeForBrand($query, int $brandId)
    {
        return $query->where('brand_id', $brandId);
    }

    public function scopeAutoReplyEnabled($query)
    {
        return $query->where('auto_reply_enabled', true);
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    /**
     * Get trait value (0-100)
     */
    public function getTrait(string $trait): int
    {
        return $this->personality_traits[$trait] ?? self::DEFAULT_TRAITS[$trait] ?? 50;
    }

    /**
     * Get language preference
     */
    public function getLanguagePreference(string $key, $default = null)
    {
        return $this->language_preferences[$key] ?? self::DEFAULT_LANGUAGE[$key] ?? $default;
    }

    /**
     * Check if should use Thai particles (ครับ/ค่ะ)
     */
    public function shouldUseParticles(): bool
    {
        return $this->getLanguagePreference('use_particles', true);
    }

    /**
     * Get response template for scenario
     */
    public function getTemplate(string $scenario): ?array
    {
        return $this->response_templates[$scenario] ?? null;
    }

    /**
     * Check if word is prohibited
     */
    public function isProhibited(string $word): bool
    {
        $prohibited = $this->prohibited_words ?? [];
        return in_array(mb_strtolower($word), array_map('mb_strtolower', $prohibited));
    }

    /**
     * Get keyword trigger if exists
     */
    public function getKeywordTrigger(string $text): ?array
    {
        $triggers = $this->keyword_triggers ?? [];

        foreach ($triggers as $keyword => $config) {
            if (stripos($text, $keyword) !== false) {
                return $config;
            }
        }

        return null;
    }

    /**
     * Build AI system prompt based on tone settings
     */
    public function buildSystemPrompt(string $platform = 'general'): string
    {
        $traits = $this->personality_traits ?? self::DEFAULT_TRAITS;
        $lang = $this->language_preferences ?? self::DEFAULT_LANGUAGE;

        $prompt = "คุณเป็น AI ที่ตอบคอมเมนต์บน social media ในนามของแบรนด์\n\n";

        // Personality description
        $prompt .= "## บุคลิกในการตอบ:\n";

        if ($traits['friendly'] > 70) {
            $prompt .= "- เป็นมิตรและอบอุ่น\n";
        } elseif ($traits['friendly'] < 30) {
            $prompt .= "- ตอบตรงประเด็น ไม่ต้องเกริ่นมาก\n";
        }

        if ($traits['formal'] > 70) {
            $prompt .= "- ใช้ภาษาทางการ สุภาพ\n";
        } elseif ($traits['formal'] < 30) {
            $prompt .= "- ใช้ภาษาง่ายๆ เป็นกันเอง\n";
        }

        if ($traits['humor'] > 60) {
            $prompt .= "- สามารถใส่มุขตลกได้บ้างตามความเหมาะสม\n";
        }

        if ($traits['emoji_usage'] > 70) {
            $prompt .= "- ใช้ emoji ได้เยอะ\n";
        } elseif ($traits['emoji_usage'] < 20) {
            $prompt .= "- ไม่ใช้ emoji หรือใช้น้อยมาก\n";
        }

        if ($traits['empathy'] > 70) {
            $prompt .= "- แสดงความเข้าใจและใส่ใจความรู้สึกของลูกค้า\n";
        }

        // Language preferences
        $prompt .= "\n## ภาษา:\n";
        $prompt .= "- ภาษาหลัก: " . ($lang['default_language'] === 'th' ? 'ไทย' : 'อังกฤษ') . "\n";

        if ($lang['use_honorifics'] ?? true) {
            $prompt .= "- ใช้คำลงท้ายสุภาพ เช่น ครับ/ค่ะ\n";
        }

        if ($lang['mix_languages'] ?? false) {
            $prompt .= "- สามารถผสมภาษาอังกฤษได้ตามความเหมาะสม\n";
        }

        // Custom instructions
        if ($this->custom_instructions) {
            $prompt .= "\n## คำแนะนำเพิ่มเติม:\n" . $this->custom_instructions . "\n";
        }

        // Required elements
        if (!empty($this->required_elements)) {
            $prompt .= "\n## ต้องมีในคำตอบ:\n";
            foreach ($this->required_elements as $element) {
                $prompt .= "- {$element}\n";
            }
        }

        // Prohibited words
        if (!empty($this->prohibited_words)) {
            $prompt .= "\n## ห้ามใช้คำต่อไปนี้: " . implode(', ', $this->prohibited_words) . "\n";
        }

        // Platform-specific
        $prompt .= "\n## Platform: {$platform}\n";
        $prompt .= "ปรับความยาวและสไตล์ให้เหมาะกับ platform\n";

        return $prompt;
    }

    /**
     * Create preset tone configuration
     */
    public static function createPreset(string $preset, int $userId, ?int $brandId = null): self
    {
        $configs = [
            self::PRESET_FRIENDLY => [
                'name' => 'Friendly',
                'name_th' => 'เป็นมิตร',
                'personality_traits' => [
                    'friendly' => 90,
                    'formal' => 20,
                    'humor' => 50,
                    'emoji_usage' => 70,
                    'enthusiasm' => 80,
                    'empathy' => 80,
                ],
            ],
            self::PRESET_PROFESSIONAL => [
                'name' => 'Professional',
                'name_th' => 'มืออาชีพ',
                'personality_traits' => [
                    'friendly' => 50,
                    'formal' => 80,
                    'humor' => 10,
                    'emoji_usage' => 20,
                    'enthusiasm' => 40,
                    'empathy' => 60,
                ],
            ],
            self::PRESET_CASUAL => [
                'name' => 'Casual',
                'name_th' => 'สบายๆ',
                'personality_traits' => [
                    'friendly' => 80,
                    'formal' => 10,
                    'humor' => 70,
                    'emoji_usage' => 80,
                    'enthusiasm' => 70,
                    'empathy' => 60,
                ],
            ],
            self::PRESET_SUPPORTIVE => [
                'name' => 'Supportive',
                'name_th' => 'ช่วยเหลือ',
                'personality_traits' => [
                    'friendly' => 70,
                    'formal' => 50,
                    'humor' => 20,
                    'emoji_usage' => 40,
                    'enthusiasm' => 50,
                    'empathy' => 95,
                ],
            ],
            self::PRESET_SALES => [
                'name' => 'Sales',
                'name_th' => 'ขาย',
                'personality_traits' => [
                    'friendly' => 85,
                    'formal' => 40,
                    'humor' => 30,
                    'emoji_usage' => 60,
                    'enthusiasm' => 90,
                    'empathy' => 50,
                ],
            ],
        ];

        $config = $configs[$preset] ?? $configs[self::PRESET_FRIENDLY];

        return new self(array_merge($config, [
            'user_id' => $userId,
            'brand_id' => $brandId,
            'language_preferences' => self::DEFAULT_LANGUAGE,
            'is_active' => true,
        ]));
    }
}
