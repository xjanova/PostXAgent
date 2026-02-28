<?php

declare(strict_types=1);

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\SoftDeletes;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * @property int $id
 * @property int $user_id
 * @property string $name
 * @property string|null $description
 * @property string|null $docker_image
 * @property array|null $environment_vars
 * @property string|null $setup_script
 * @property string|null $post_setup_script
 * @property array|null $required_packages
 * @property array|null $required_gpu_types
 * @property int|null $min_gpu_memory_gb
 * @property int|null $min_cpu_cores
 * @property int|null $min_ram_gb
 * @property int|null $min_disk_gb
 * @property bool $is_default
 * @property bool $is_public
 * @property \Illuminate\Support\Carbon|null $created_at
 * @property \Illuminate\Support\Carbon|null $updated_at
 * @property \Illuminate\Support\Carbon|null $deleted_at
 *
 * @mixin \Illuminate\Database\Eloquent\Builder<static>
 */
class GpuSetupTemplate extends Model
{
    use HasFactory, SoftDeletes;

    protected $fillable = [
        'user_id',
        'name',
        'description',
        'docker_image',
        'environment_vars',
        'setup_script',
        'post_setup_script',
        'required_packages',
        'required_gpu_types',
        'min_gpu_memory_gb',
        'min_cpu_cores',
        'min_ram_gb',
        'min_disk_gb',
        'is_default',
        'is_public',
    ];

    protected $casts = [
        'environment_vars' => 'array',
        'required_packages' => 'array',
        'required_gpu_types' => 'array',
        'min_gpu_memory_gb' => 'integer',
        'min_cpu_cores' => 'integer',
        'min_ram_gb' => 'integer',
        'min_disk_gb' => 'integer',
        'is_default' => 'boolean',
        'is_public' => 'boolean',
    ];

    // Relationships
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    // Query Scopes
    public function scopeForUser($query, int $userId)
    {
        return $query->where(function ($q) use ($userId) {
            $q->where('user_id', $userId)
                ->orWhere('is_public', true);
        });
    }

    public function scopeDefault($query)
    {
        return $query->where('is_default', true);
    }

    public function scopePublic($query)
    {
        return $query->where('is_public', true);
    }

    // Helpers
    public function setAsDefault(): void
    {
        // Remove default from other templates of this user
        self::where('user_id', $this->user_id)
            ->where('id', '!=', $this->id)
            ->update(['is_default' => false]);

        $this->update(['is_default' => true]);
    }

    public function getFullSetupScript(): string
    {
        $script = "#!/bin/bash\nset -e\n\n";

        // Install required packages
        if (!empty($this->required_packages)) {
            $packages = implode(' ', $this->required_packages);
            $script .= "# Install Python packages\n";
            $script .= "pip install {$packages}\n\n";
        }

        // Main setup script
        if ($this->setup_script) {
            $script .= "# Main setup script\n";
            $script .= $this->setup_script . "\n\n";
        }

        return $script;
    }

    public function meetsRequirements(array $gpuSpecs): bool
    {
        // Check GPU type
        if (!empty($this->required_gpu_types)) {
            $gpuType = $gpuSpecs['gpu_type'] ?? null;
            if (!in_array($gpuType, $this->required_gpu_types)) {
                return false;
            }
        }

        // Check GPU memory
        if ($this->min_gpu_memory_gb) {
            $gpuMemory = $gpuSpecs['gpu_memory_gb'] ?? 0;
            if ($gpuMemory < $this->min_gpu_memory_gb) {
                return false;
            }
        }

        // Check CPU cores
        if ($this->min_cpu_cores) {
            $cpuCores = $gpuSpecs['cpu_cores'] ?? 0;
            if ($cpuCores < $this->min_cpu_cores) {
                return false;
            }
        }

        // Check RAM
        if ($this->min_ram_gb) {
            $ram = $gpuSpecs['ram_gb'] ?? 0;
            if ($ram < $this->min_ram_gb) {
                return false;
            }
        }

        // Check Disk
        if ($this->min_disk_gb) {
            $disk = $gpuSpecs['disk_gb'] ?? 0;
            if ($disk < $this->min_disk_gb) {
                return false;
            }
        }

        return true;
    }

    /**
     * Create a default template for AI image generation (Diffusers)
     */
    public static function createDiffusersTemplate(int $userId): self
    {
        return self::create([
            'user_id' => $userId,
            'name' => 'AI Image Generation (Diffusers)',
            'description' => 'Setup for Stable Diffusion / SDXL image generation with Diffusers library',
            'docker_image' => 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime',
            'environment_vars' => [
                'PYTHONUNBUFFERED' => '1',
                'HF_HOME' => '/workspace/huggingface',
                'TORCH_HOME' => '/workspace/torch',
            ],
            'required_packages' => [
                'torch',
                'torchvision',
                'diffusers',
                'transformers',
                'accelerate',
                'safetensors',
                'xformers',
                'fastapi',
                'uvicorn',
                'pillow',
            ],
            'setup_script' => <<<'BASH'
# Create workspace directories
mkdir -p /workspace/models /workspace/outputs /workspace/huggingface /workspace/torch

# Download common models (optional, uncomment as needed)
# python -c "from diffusers import StableDiffusionXLPipeline; StableDiffusionXLPipeline.from_pretrained('stabilityai/stable-diffusion-xl-base-1.0')"

echo "Diffusers setup complete!"
BASH,
            'required_gpu_types' => ['RTX_4090', 'RTX_3090', 'A100', 'A6000', 'H100'],
            'min_gpu_memory_gb' => 16,
            'min_cpu_cores' => 4,
            'min_ram_gb' => 16,
            'min_disk_gb' => 50,
            'is_default' => true,
            'is_public' => false,
        ]);
    }

    /**
     * Create a template for PostXAgent's generation server
     */
    public static function createPostXAgentTemplate(int $userId): self
    {
        return self::create([
            'user_id' => $userId,
            'name' => 'PostXAgent Generation Server',
            'description' => 'Full setup for PostXAgent AI generation server with all features',
            'docker_image' => 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime',
            'environment_vars' => [
                'PYTHONUNBUFFERED' => '1',
                'HF_HOME' => '/workspace/huggingface',
                'TORCH_HOME' => '/workspace/torch',
                'GENERATION_SERVER_PORT' => '5050',
                'LOW_VRAM_MODE' => 'false',
            ],
            'required_packages' => [
                'torch',
                'torchvision',
                'torchaudio',
                'diffusers',
                'transformers',
                'accelerate',
                'safetensors',
                'xformers',
                'fastapi',
                'uvicorn',
                'pillow',
                'controlnet-aux',
                'realesrgan',
                'basicsr',
            ],
            'setup_script' => <<<'BASH'
# Create workspace directories
mkdir -p /workspace/models /workspace/outputs /workspace/huggingface /workspace/torch

# Clone PostXAgent generation server
git clone https://github.com/xjanova/PostXAgent.git /workspace/PostXAgent || true

# Copy generation server script
cp /workspace/PostXAgent/AIManagerCore/src/AIManager.Core/Services/generation_server.py /workspace/

# Start the generation server
cd /workspace && python generation_server.py --port 5050 --models-dir /workspace/models &

echo "PostXAgent Generation Server setup complete!"
echo "Server running on port 5050"
BASH,
            'post_setup_script' => <<<'BASH'
# Verify server is running
sleep 10
curl -s http://localhost:5050/health || echo "Warning: Server may not be ready yet"
BASH,
            'required_gpu_types' => ['RTX_4090', 'RTX_3090', 'A100', 'A6000', 'H100', 'L40S'],
            'min_gpu_memory_gb' => 24,
            'min_cpu_cores' => 8,
            'min_ram_gb' => 32,
            'min_disk_gb' => 100,
            'is_default' => false,
            'is_public' => false,
        ]);
    }
}
