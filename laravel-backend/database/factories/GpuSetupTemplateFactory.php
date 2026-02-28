<?php

namespace Database\Factories;

use App\Models\GpuSetupTemplate;
use App\Models\User;
use Illuminate\Database\Eloquent\Factories\Factory;

/**
 * @extends \Illuminate\Database\Eloquent\Factories\Factory<\App\Models\GpuSetupTemplate>
 */
class GpuSetupTemplateFactory extends Factory
{
    protected $model = GpuSetupTemplate::class;

    /**
     * Define the model's default state.
     *
     * @return array<string, mixed>
     */
    public function definition(): array
    {
        return [
            'user_id' => User::factory(),
            'name' => fake()->words(3, true) . ' Template',
            'description' => fake()->sentence(),
            'docker_image' => 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime',
            'environment_vars' => [
                'PYTHONUNBUFFERED' => '1',
            ],
            'setup_script' => "#!/bin/bash\necho 'Setup complete'",
            'post_setup_script' => null,
            'required_packages' => ['torch', 'diffusers', 'transformers'],
            'required_gpu_types' => ['RTX_4090', 'RTX_3090', 'A100'],
            'min_gpu_memory_gb' => 16,
            'min_cpu_cores' => 4,
            'min_ram_gb' => 16,
            'min_disk_gb' => 50,
            'is_default' => false,
            'is_public' => false,
        ];
    }

    /**
     * Template set as default
     */
    public function default(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_default' => true,
        ]);
    }

    /**
     * Public template
     */
    public function public(): static
    {
        return $this->state(fn (array $attributes) => [
            'is_public' => true,
        ]);
    }

    /**
     * Template for Diffusers setup
     */
    public function diffusers(): static
    {
        return $this->state(fn (array $attributes) => [
            'name' => 'AI Image Generation (Diffusers)',
            'description' => 'Setup for Stable Diffusion / SDXL image generation',
            'required_packages' => [
                'torch', 'torchvision', 'diffusers', 'transformers',
                'accelerate', 'safetensors', 'xformers',
            ],
            'min_gpu_memory_gb' => 16,
            'min_ram_gb' => 16,
        ]);
    }

    /**
     * Template with high requirements
     */
    public function highRequirements(): static
    {
        return $this->state(fn (array $attributes) => [
            'min_gpu_memory_gb' => 48,
            'min_cpu_cores' => 16,
            'min_ram_gb' => 64,
            'min_disk_gb' => 200,
            'required_gpu_types' => ['A100', 'H100'],
        ]);
    }
}
