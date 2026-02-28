<?php

return [
    /*
    |--------------------------------------------------------------------------
    | GPU Provider Configuration
    |--------------------------------------------------------------------------
    |
    | Configuration for GPU cloud provider integrations (Vast.ai, RunPod, etc.)
    |
    */

    // Default provider to use
    'default_provider' => env('GPU_DEFAULT_PROVIDER', 'vast_ai'),

    // Supported providers
    'providers' => [
        'vast_ai' => [
            'name' => 'Vast.ai',
            'api_base_url' => env('VAST_AI_API_URL', 'https://console.vast.ai/api/v0'),
            'enabled' => env('VAST_AI_ENABLED', true),
            'default_image' => env('VAST_AI_DEFAULT_IMAGE', 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime'),
        ],
        'runpod' => [
            'name' => 'RunPod',
            'api_base_url' => env('RUNPOD_API_URL', 'https://api.runpod.io'),
            'graphql_url' => env('RUNPOD_GRAPHQL_URL', 'https://api.runpod.io/graphql'),
            'enabled' => env('RUNPOD_ENABLED', true),
            'default_image' => env('RUNPOD_DEFAULT_IMAGE', 'runpod/pytorch:2.1.0-py3.10-cuda12.1.0-devel-ubuntu22.04'),
        ],
        'lightning_ai' => [
            'name' => 'Lightning.ai',
            'enabled' => env('LIGHTNING_AI_ENABLED', false),
        ],
        'kaggle' => [
            'name' => 'Kaggle',
            'enabled' => env('KAGGLE_ENABLED', false),
        ],
        'lambda' => [
            'name' => 'Lambda Labs',
            'enabled' => env('LAMBDA_ENABLED', false),
        ],
    ],

    // Pool defaults
    'pool' => [
        'default_rotation_strategy' => env('GPU_POOL_ROTATION_STRATEGY', 'most_credits'),
        'default_cooldown_minutes' => env('GPU_POOL_COOLDOWN_MINUTES', 60),
        'default_min_credits' => env('GPU_POOL_MIN_CREDITS', 0.50),
        'auto_failover' => env('GPU_POOL_AUTO_FAILOVER', true),
        'auto_provision' => env('GPU_POOL_AUTO_PROVISION', true),
    ],

    // Instance defaults
    'instance' => [
        'default_disk_gb' => env('GPU_DEFAULT_DISK_GB', 50),
        'default_docker_image' => env('GPU_DEFAULT_DOCKER_IMAGE', 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime'),
        'max_instances_per_user' => env('GPU_MAX_INSTANCES_PER_USER', 10),
        'auto_terminate_after_hours' => env('GPU_AUTO_TERMINATE_HOURS', null),
    ],

    // Balance monitoring
    'balance' => [
        'low_threshold' => env('GPU_LOW_BALANCE_THRESHOLD', 1.00),
        'refresh_interval_minutes' => env('GPU_BALANCE_REFRESH_MINUTES', 60),
        'notify_on_low' => env('GPU_NOTIFY_LOW_BALANCE', true),
        'notify_on_depleted' => env('GPU_NOTIFY_DEPLETED', true),
    ],

    // Search defaults
    'search' => [
        'default_max_price_per_hour' => env('GPU_DEFAULT_MAX_PRICE', null),
        'verified_hosts_only' => env('GPU_VERIFIED_HOSTS_ONLY', true),
        'min_reliability_score' => env('GPU_MIN_RELIABILITY', 0.95),
    ],

    // PostXAgent Generation Server
    'generation_server' => [
        'port' => env('GENERATION_SERVER_PORT', 5050),
        'models_dir' => env('GENERATION_SERVER_MODELS_DIR', '/workspace/models'),
        'low_vram_mode' => env('GENERATION_SERVER_LOW_VRAM', false),
    ],

    // Preferred GPU types for AI generation
    'preferred_gpu_types' => [
        'RTX_4090',
        'RTX_3090',
        'A100',
        'A6000',
        'H100',
        'L40S',
    ],

    // Minimum requirements for different workloads
    'workload_requirements' => [
        'stable_diffusion' => [
            'min_gpu_memory_gb' => 8,
            'min_ram_gb' => 16,
            'min_disk_gb' => 30,
        ],
        'sdxl' => [
            'min_gpu_memory_gb' => 16,
            'min_ram_gb' => 24,
            'min_disk_gb' => 50,
        ],
        'video_generation' => [
            'min_gpu_memory_gb' => 24,
            'min_ram_gb' => 32,
            'min_disk_gb' => 100,
        ],
        'flux' => [
            'min_gpu_memory_gb' => 24,
            'min_ram_gb' => 32,
            'min_disk_gb' => 80,
        ],
    ],
];
