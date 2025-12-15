<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;

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
