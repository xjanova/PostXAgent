<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\HasMany;
use Illuminate\Database\Eloquent\SoftDeletes;

/**
 * @property int $id
 * @property string $name
 * @property string|null $description
 * @property int $posts_limit
 * @property int $accounts_limit
 * @property int $brands_limit
 * @property string $price
 * @property int $duration_days
 * @property bool $is_active
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 * @property-read \Illuminate\Database\Eloquent\Collection<int, \App\Models\UserRental> $userRentals
 */
class RentalPackage extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'name',
        'description',
        'posts_limit',
        'accounts_limit',
        'brands_limit',
        'price',
        'duration_days',
        'is_active',
    ];

    protected $casts = [
        'posts_limit' => 'integer',
        'accounts_limit' => 'integer',
        'brands_limit' => 'integer',
        'price' => 'decimal:2',
        'duration_days' => 'integer',
        'is_active' => 'boolean',
    ];

    public function userRentals(): HasMany
    {
        return $this->hasMany(UserRental::class);
    }
}
