<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * Usage Log Model
 * บันทึกประวัติการใช้งานของ UserRental
 */
class UsageLog extends Model
{
    use HasFactory;

    protected $fillable = [
        'user_rental_id',
        'user_id',
        'type',
        'amount',
        'description',
        'metadata',
    ];

    protected $casts = [
        'amount' => 'integer',
        'metadata' => 'array',
    ];

    // Log types
    const TYPE_POST = 'post';
    const TYPE_AI_GENERATION = 'ai_generation';
    const TYPE_BRAND = 'brand';
    const TYPE_PLATFORM = 'platform';
    const TYPE_TEAM_MEMBER = 'team_member';
    const TYPE_EXTENSION = 'extension';
    const TYPE_RESET = 'reset';
    const TYPE_ADJUSTMENT = 'adjustment';

    /**
     * Get the rental this log belongs to
     */
    public function userRental(): BelongsTo
    {
        return $this->belongsTo(UserRental::class);
    }

    /**
     * Get the user this log belongs to
     */
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    /**
     * Scope for specific type
     */
    public function scopeOfType($query, string $type)
    {
        return $query->where('type', $type);
    }

    /**
     * Scope for date range
     */
    public function scopeBetweenDates($query, $start, $end)
    {
        return $query->whereBetween('created_at', [$start, $end]);
    }

    /**
     * Get type label
     */
    public function getTypeLabelAttribute(): string
    {
        return match ($this->type) {
            self::TYPE_POST => 'โพสต์',
            self::TYPE_AI_GENERATION => 'สร้างเนื้อหา AI',
            self::TYPE_BRAND => 'แบรนด์',
            self::TYPE_PLATFORM => 'แพลตฟอร์ม',
            self::TYPE_TEAM_MEMBER => 'สมาชิกทีม',
            self::TYPE_EXTENSION => 'ขยายเวลา',
            self::TYPE_RESET => 'รีเซ็ต',
            self::TYPE_ADJUSTMENT => 'ปรับแก้',
            default => $this->type,
        };
    }

    /**
     * Create a post usage log
     */
    public static function logPost(UserRental $rental, string $description = null): self
    {
        return static::create([
            'user_rental_id' => $rental->id,
            'user_id' => $rental->user_id,
            'type' => self::TYPE_POST,
            'amount' => 1,
            'description' => $description ?? 'Post published',
        ]);
    }

    /**
     * Create an AI generation usage log
     */
    public static function logAIGeneration(UserRental $rental, string $description = null): self
    {
        return static::create([
            'user_rental_id' => $rental->id,
            'user_id' => $rental->user_id,
            'type' => self::TYPE_AI_GENERATION,
            'amount' => 1,
            'description' => $description ?? 'AI content generated',
        ]);
    }

    /**
     * Create an extension log
     */
    public static function logExtension(UserRental $rental, int $days, string $reason): self
    {
        return static::create([
            'user_rental_id' => $rental->id,
            'user_id' => $rental->user_id,
            'type' => self::TYPE_EXTENSION,
            'amount' => $days,
            'description' => $reason,
        ]);
    }
}
