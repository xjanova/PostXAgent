<?php

return [

    /*
    |--------------------------------------------------------------------------
    | Third Party Services
    |--------------------------------------------------------------------------
    */

    'postmark' => [
        'token' => env('POSTMARK_TOKEN'),
    ],

    'ses' => [
        'key' => env('AWS_ACCESS_KEY_ID'),
        'secret' => env('AWS_SECRET_ACCESS_KEY'),
        'region' => env('AWS_DEFAULT_REGION', 'us-east-1'),
    ],

    'resend' => [
        'key' => env('RESEND_KEY'),
    ],

    'slack' => [
        'notifications' => [
            'bot_user_oauth_token' => env('SLACK_BOT_USER_OAUTH_TOKEN'),
            'channel' => env('SLACK_BOT_USER_DEFAULT_CHANNEL'),
        ],
    ],

    /*
    |--------------------------------------------------------------------------
    | Payment Services
    |--------------------------------------------------------------------------
    */

    'stripe' => [
        'key' => env('STRIPE_KEY'),
        'secret' => env('STRIPE_SECRET'),
        'webhook_secret' => env('STRIPE_WEBHOOK_SECRET'),
    ],

    'omise' => [
        'public_key' => env('OMISE_PUBLIC_KEY'),
        'secret_key' => env('OMISE_SECRET_KEY'),
        'webhook_secret' => env('OMISE_WEBHOOK_SECRET'),
    ],

    /*
    |--------------------------------------------------------------------------
    | Social Media Platform APIs
    |--------------------------------------------------------------------------
    */

    'facebook' => [
        'client_id' => env('FACEBOOK_APP_ID'),
        'client_secret' => env('FACEBOOK_APP_SECRET'),
        'redirect' => env('FACEBOOK_REDIRECT_URI'),
    ],

    'instagram' => [
        'client_id' => env('INSTAGRAM_CLIENT_ID'),
        'client_secret' => env('INSTAGRAM_CLIENT_SECRET'),
        'redirect' => env('INSTAGRAM_REDIRECT_URI'),
    ],

    'twitter' => [
        'client_id' => env('TWITTER_CLIENT_ID'),
        'client_secret' => env('TWITTER_CLIENT_SECRET'),
        'redirect' => env('TWITTER_REDIRECT_URI'),
        'bearer_token' => env('TWITTER_BEARER_TOKEN'),
    ],

    'tiktok' => [
        'client_key' => env('TIKTOK_CLIENT_KEY'),
        'client_secret' => env('TIKTOK_CLIENT_SECRET'),
        'redirect' => env('TIKTOK_REDIRECT_URI'),
    ],

    'line' => [
        'channel_id' => env('LINE_CHANNEL_ID'),
        'channel_secret' => env('LINE_CHANNEL_SECRET'),
        'channel_access_token' => env('LINE_CHANNEL_ACCESS_TOKEN'),
        'redirect' => env('LINE_REDIRECT_URI'),
    ],

    'google' => [
        'client_id' => env('GOOGLE_CLIENT_ID'),
        'client_secret' => env('GOOGLE_CLIENT_SECRET'),
        'redirect' => env('GOOGLE_REDIRECT_URI'),
        'api_key' => env('GOOGLE_API_KEY'),
    ],

    'youtube' => [
        'client_id' => env('YOUTUBE_CLIENT_ID', env('GOOGLE_CLIENT_ID')),
        'client_secret' => env('YOUTUBE_CLIENT_SECRET', env('GOOGLE_CLIENT_SECRET')),
        'redirect' => env('YOUTUBE_REDIRECT_URI'),
    ],

    'linkedin' => [
        'client_id' => env('LINKEDIN_CLIENT_ID'),
        'client_secret' => env('LINKEDIN_CLIENT_SECRET'),
        'redirect' => env('LINKEDIN_REDIRECT_URI'),
    ],

    'pinterest' => [
        'client_id' => env('PINTEREST_APP_ID'),
        'client_secret' => env('PINTEREST_APP_SECRET'),
        'redirect' => env('PINTEREST_REDIRECT_URI'),
    ],

    'threads' => [
        'client_id' => env('THREADS_CLIENT_ID', env('INSTAGRAM_CLIENT_ID')),
        'client_secret' => env('THREADS_CLIENT_SECRET', env('INSTAGRAM_CLIENT_SECRET')),
        'redirect' => env('THREADS_REDIRECT_URI'),
    ],

    /*
    |--------------------------------------------------------------------------
    | AI Services
    |--------------------------------------------------------------------------
    */

    'openai' => [
        'api_key' => env('OPENAI_API_KEY'),
        'organization' => env('OPENAI_ORGANIZATION'),
    ],

    'anthropic' => [
        'api_key' => env('ANTHROPIC_API_KEY'),
    ],

    'google_ai' => [
        'api_key' => env('GOOGLE_AI_API_KEY', env('GOOGLE_API_KEY')),
    ],

    'ollama' => [
        'base_url' => env('OLLAMA_BASE_URL', 'http://localhost:11434'),
        'model' => env('OLLAMA_MODEL', 'llama2'),
    ],

    /*
    |--------------------------------------------------------------------------
    | AI Manager Core (C#)
    |--------------------------------------------------------------------------
    | Connection settings for the C# AI Manager Core service
    */

    'ai_manager' => [
        'host' => env('AI_MANAGER_HOST', 'localhost'),
        'api_port' => env('AI_MANAGER_API_PORT', 5000),
        'websocket_port' => env('AI_MANAGER_WEBSOCKET_PORT', 5001),
        'signalr_port' => env('AI_MANAGER_SIGNALR_PORT', 5002),
        'api_key' => env('AI_MANAGER_API_KEY'),
    ],

    /*
    |--------------------------------------------------------------------------
    | Internal API Authentication
    |--------------------------------------------------------------------------
    | Used for C# Core and internal services to communicate with Laravel
    */

    'internal_api_key' => env('INTERNAL_API_KEY', 'change-this-in-production'),
    'internal_allowed_ips' => explode(',', env('INTERNAL_ALLOWED_IPS', '127.0.0.1,::1')),

    /*
    |--------------------------------------------------------------------------
    | SMS Gateway (Multi-Website Integration)
    |--------------------------------------------------------------------------
    | Configuration for receiving webhooks from the SMS Gateway Mobile App
    | This allows the mobile app to send payment notifications to multiple websites
    */

    'sms_gateway' => [
        'api_key' => env('SMS_GATEWAY_API_KEY'),
        'secret_key' => env('SMS_GATEWAY_SECRET_KEY'),
        'signature_tolerance' => env('SMS_GATEWAY_SIGNATURE_TOLERANCE', 300), // seconds
    ],

];
