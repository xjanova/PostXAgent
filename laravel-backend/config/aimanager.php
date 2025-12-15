<?php

return [
    /*
    |--------------------------------------------------------------------------
    | AI Manager Connection Configuration
    |--------------------------------------------------------------------------
    |
    | Configure the connection to the AI Manager Core running on Windows Server.
    | You can configure multiple servers for load balancing and failover.
    |
    */

    // Primary AI Manager Server
    'primary' => [
        'host' => env('AI_MANAGER_HOST', 'localhost'),
        'api_port' => env('AI_MANAGER_API_PORT', 5000),
        'signalr_port' => env('AI_MANAGER_SIGNALR_PORT', 5002),
        'use_ssl' => env('AI_MANAGER_USE_SSL', false),
    ],

    // Secondary/Backup AI Manager Servers
    'servers' => [
        [
            'name' => 'primary',
            'host' => env('AI_MANAGER_HOST', 'localhost'),
            'api_port' => env('AI_MANAGER_API_PORT', 5000),
            'signalr_port' => env('AI_MANAGER_SIGNALR_PORT', 5002),
            'use_ssl' => env('AI_MANAGER_USE_SSL', false),
            'weight' => 100, // For load balancing
        ],
        // Add more servers for horizontal scaling
        // [
        //     'name' => 'secondary',
        //     'host' => env('AI_MANAGER_HOST_2', 'ai-server-2.local'),
        //     'api_port' => 5000,
        //     'signalr_port' => 5002,
        //     'use_ssl' => true,
        //     'weight' => 50,
        // ],
    ],

    // Connection settings
    'connection' => [
        'timeout' => env('AI_MANAGER_TIMEOUT', 30),
        'retry_attempts' => env('AI_MANAGER_RETRY_ATTEMPTS', 3),
        'retry_delay_ms' => env('AI_MANAGER_RETRY_DELAY', 1000),
    ],

    // Authentication
    'auth' => [
        'api_key' => env('AI_MANAGER_API_KEY', ''),
        'secret' => env('AI_MANAGER_SECRET', ''),
    ],

    // Available ports (for reference)
    'ports' => [
        'api' => 5000,           // REST API
        'websocket' => 5001,     // WebSocket
        'signalr' => 5002,       // SignalR Hub
        'grpc' => 5003,          // gRPC (optional)
    ],

    // Health check settings
    'health_check' => [
        'enabled' => env('AI_MANAGER_HEALTH_CHECK', true),
        'interval_seconds' => 30,
        'timeout_seconds' => 5,
    ],

    // Failover settings
    'failover' => [
        'enabled' => env('AI_MANAGER_FAILOVER', true),
        'auto_reconnect' => true,
        'max_downtime_seconds' => 60,
    ],
];
