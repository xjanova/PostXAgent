<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;
use Illuminate\Database\Eloquent\SoftDeletes;

/**
 * @property int $id
 * @property int $user_id
 * @property int $rental_package_id
 * @property string $status
 * @property \Illuminate\Support\Carbon|null $starts_at
 * @property \Illuminate\Support\Carbon|null $expires_at
 * @property int $posts_used
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 * @property-read \App\Models\User $user
 * @property-read \App\Models\RentalPackage $rentalPackage
 */
class UserRental extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'rental_package_id',
        'status',
        'starts_at',
        'expires_at',
        'posts_used',
    ];

    protected $casts = [
        'starts_at' => 'datetime',
        'expires_at' => 'datetime',
        'posts_used' => 'integer',
    ];

    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function rentalPackage(): BelongsTo
    {
        return $this->belongsTo(RentalPackage::class);
    }

    public function isActive(): bool
    {
        return $this->status === 'active'
            && $this->starts_at <= now()
            && $this->expires_at >= now();
    }
}
