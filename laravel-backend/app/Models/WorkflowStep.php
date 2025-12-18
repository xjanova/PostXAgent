<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

class WorkflowStep extends Model
{
    use HasFactory;

    // Action types
    const ACTION_CLICK = 'click';
    const ACTION_TYPE = 'type';
    const ACTION_NAVIGATE = 'navigate';
    const ACTION_UPLOAD = 'upload';
    const ACTION_WAIT = 'wait';
    const ACTION_WAIT_FOR_ELEMENT = 'wait_for_element';
    const ACTION_SELECT = 'select';
    const ACTION_SCROLL = 'scroll';
    const ACTION_HOVER = 'hover';
    const ACTION_PRESS_KEY = 'press_key';
    const ACTION_EXECUTE_SCRIPT = 'execute_script';
    const ACTION_SCREENSHOT = 'screenshot';

    // Selector types
    const SELECTOR_ID = 'id';
    const SELECTOR_CSS = 'css';
    const SELECTOR_XPATH = 'xpath';
    const SELECTOR_NAME = 'name';
    const SELECTOR_TEXT = 'text';
    const SELECTOR_ARIA_LABEL = 'aria_label';
    const SELECTOR_TEST_ID = 'test_id';
    const SELECTOR_PLACEHOLDER = 'placeholder';
    const SELECTOR_SMART = 'smart';
    const SELECTOR_VISUAL = 'visual';

    protected $fillable = [
        'learned_workflow_id',
        'order',
        'action',
        'description',
        'selector_type',
        'selector_value',
        'selector_confidence',
        'ai_description',
        'alternative_selectors',
        'input_value',
        'input_variable',
        'is_optional',
        'wait_before_ms',
        'wait_after_ms',
        'timeout_ms',
        'success_condition',
        'learned_from',
        'confidence_score',
        'visual_features',
        'metadata',
    ];

    protected $casts = [
        'order' => 'integer',
        'selector_confidence' => 'decimal:4',
        'alternative_selectors' => 'array',
        'is_optional' => 'boolean',
        'wait_before_ms' => 'integer',
        'wait_after_ms' => 'integer',
        'timeout_ms' => 'integer',
        'success_condition' => 'array',
        'confidence_score' => 'decimal:4',
        'visual_features' => 'array',
        'metadata' => 'array',
    ];

    protected $attributes = [
        'is_optional' => false,
        'wait_before_ms' => 500,
        'wait_after_ms' => 500,
        'timeout_ms' => 10000,
        'confidence_score' => 0.5,
    ];

    public static function getActions(): array
    {
        return [
            self::ACTION_CLICK,
            self::ACTION_TYPE,
            self::ACTION_NAVIGATE,
            self::ACTION_UPLOAD,
            self::ACTION_WAIT,
            self::ACTION_WAIT_FOR_ELEMENT,
            self::ACTION_SELECT,
            self::ACTION_SCROLL,
            self::ACTION_HOVER,
            self::ACTION_PRESS_KEY,
            self::ACTION_EXECUTE_SCRIPT,
            self::ACTION_SCREENSHOT,
        ];
    }

    public static function getSelectorTypes(): array
    {
        return [
            self::SELECTOR_ID,
            self::SELECTOR_CSS,
            self::SELECTOR_XPATH,
            self::SELECTOR_NAME,
            self::SELECTOR_TEXT,
            self::SELECTOR_ARIA_LABEL,
            self::SELECTOR_TEST_ID,
            self::SELECTOR_PLACEHOLDER,
            self::SELECTOR_SMART,
            self::SELECTOR_VISUAL,
        ];
    }

    // Relationships
    public function workflow(): BelongsTo
    {
        return $this->belongsTo(LearnedWorkflow::class, 'learned_workflow_id');
    }

    // Helpers
    public function getSelector(): array
    {
        return [
            'type' => $this->selector_type,
            'value' => $this->selector_value,
            'confidence' => $this->selector_confidence,
            'ai_description' => $this->ai_description,
        ];
    }

    public function addAlternativeSelector(string $type, string $value, float $confidence): void
    {
        $alternatives = $this->alternative_selectors ?? [];
        $alternatives[] = [
            'type' => $type,
            'value' => $value,
            'confidence' => $confidence,
        ];
        $this->update(['alternative_selectors' => $alternatives]);
    }

    public function getBestSelector(): array
    {
        $selectors = $this->alternative_selectors ?? [];
        array_unshift($selectors, $this->getSelector());

        usort($selectors, fn($a, $b) => ($b['confidence'] ?? 0) <=> ($a['confidence'] ?? 0));

        return $selectors[0] ?? $this->getSelector();
    }

    public function resolveInputValue(array $content): ?string
    {
        if (!empty($this->input_value) && empty($this->input_variable)) {
            return $this->input_value;
        }

        if (!empty($this->input_variable)) {
            return match ($this->input_variable) {
                '{{content.text}}' => $content['text'] ?? null,
                '{{content.hashtags}}' => isset($content['hashtags'])
                    ? implode(' ', array_map(fn($h) => str_starts_with($h, '#') ? $h : "#{$h}", $content['hashtags']))
                    : null,
                '{{content.link}}' => $content['link'] ?? null,
                '{{content.location}}' => $content['location'] ?? null,
                '{{content.image}}' => $content['images'][0] ?? null,
                '{{content.video}}' => $content['videos'][0] ?? null,
                default => $this->input_value,
            };
        }

        return $this->input_value;
    }
}
