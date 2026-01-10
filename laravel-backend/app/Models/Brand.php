<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

/**
 * @property int $id
 * @property int $user_id
 * @property string $name
 * @property string|null $description
 * @property string|null $industry
 * @property string|null $target_audience
 * @property string|null $tone
 * @property string|null $logo_url
 * @property array|null $brand_colors
 * @property array|null $keywords
 * @property array|null $hashtags
 * @property string|null $website_url
 * @property array|null $settings
 * @property bool $is_active
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 *
 * @method static \Illuminate\Database\Eloquent\Builder|Brand where($column, $operator = null, $value = null, $boolean = 'and')
 * @method static \Illuminate\Database\Eloquent\Builder|Brand create(array $attributes = [])
 * @method static \Illuminate\Database\Eloquent\Builder|Brand find($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|Brand findOrFail($id, $columns = ['*'])
 * @method static \Illuminate\Database\Eloquent\Builder|Brand first($columns = ['*'])
 */
class Brand extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'name',
        'description',
        'industry',
        'target_audience',
        'tone',
        'logo_url',
        'brand_colors',
        'keywords',
        'hashtags',
        'website_url',
        'settings',
        'is_active',
    ];

    protected $casts = [
        'brand_colors' => 'array',
        'keywords' => 'array',
        'hashtags' => 'array',
        'settings' => 'array',
        'is_active' => 'boolean',
    ];

    // Relationships
    public function user()
    {
        return $this->belongsTo(User::class);
    }

    public function campaigns()
    {
        return $this->hasMany(Campaign::class);
    }

    public function posts()
    {
        return $this->hasMany(Post::class);
    }

    public function socialAccounts()
    {
        return $this->hasMany(SocialAccount::class);
    }

    // Helpers
    public function getFormattedHashtags(): string
    {
        return collect($this->hashtags ?? [])
            ->map(fn($tag) => "#{$tag}")
            ->implode(' ');
    }

    public function toAIContext(): array
    {
        return [
            'name' => $this->name,
            'industry' => $this->industry,
            'target_audience' => $this->target_audience,
            'tone' => $this->tone,
            'keywords' => $this->keywords,
            'hashtags' => $this->hashtags,
        ];
    }
}
