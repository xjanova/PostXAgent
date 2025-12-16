<?php

return [
    /*
    |--------------------------------------------------------------------------
    | Thai Payment Configuration
    |--------------------------------------------------------------------------
    |
    | Configuration for Thai payment methods including PromptPay, bank transfer,
    | and online payment gateways optimized for Thai market.
    |
    */

    // PromptPay Configuration
    'promptpay' => [
        'number' => env('PROMPTPAY_NUMBER', ''),
        'name' => env('PROMPTPAY_NAME', 'PostXAgent Co., Ltd.'),
        'enabled' => env('PROMPTPAY_ENABLED', true),
    ],

    // Bank Account Information for Manual Transfer
    'bank_accounts' => [
        [
            'bank_name' => 'ธนาคารกสิกรไทย',
            'bank_name_en' => 'Kasikorn Bank',
            'bank_code' => 'KBANK',
            'account_number' => env('KBANK_ACCOUNT_NUMBER', ''),
            'account_name' => env('BANK_ACCOUNT_NAME', 'PostXAgent Co., Ltd.'),
            'branch' => env('KBANK_BRANCH', 'สำนักงานใหญ่'),
            'enabled' => env('KBANK_ENABLED', false),
        ],
        [
            'bank_name' => 'ธนาคารไทยพาณิชย์',
            'bank_name_en' => 'Siam Commercial Bank',
            'bank_code' => 'SCB',
            'account_number' => env('SCB_ACCOUNT_NUMBER', ''),
            'account_name' => env('BANK_ACCOUNT_NAME', 'PostXAgent Co., Ltd.'),
            'branch' => env('SCB_BRANCH', 'สำนักงานใหญ่'),
            'enabled' => env('SCB_ENABLED', false),
        ],
        [
            'bank_name' => 'ธนาคารกรุงเทพ',
            'bank_name_en' => 'Bangkok Bank',
            'bank_code' => 'BBL',
            'account_number' => env('BBL_ACCOUNT_NUMBER', ''),
            'account_name' => env('BANK_ACCOUNT_NAME', 'PostXAgent Co., Ltd.'),
            'branch' => env('BBL_BRANCH', 'สำนักงานใหญ่'),
            'enabled' => env('BBL_ENABLED', false),
        ],
        [
            'bank_name' => 'ธนาคารกรุงไทย',
            'bank_name_en' => 'Krungthai Bank',
            'bank_code' => 'KTB',
            'account_number' => env('KTB_ACCOUNT_NUMBER', ''),
            'account_name' => env('BANK_ACCOUNT_NAME', 'PostXAgent Co., Ltd.'),
            'branch' => env('KTB_BRANCH', 'สำนักงานใหญ่'),
            'enabled' => env('KTB_ENABLED', false),
        ],
    ],

    // Omise Payment Gateway (Thai)
    'omise' => [
        'public_key' => env('OMISE_PUBLIC_KEY'),
        'secret_key' => env('OMISE_SECRET_KEY'),
        'webhook_secret' => env('OMISE_WEBHOOK_SECRET'),
        'enabled' => env('OMISE_ENABLED', false),
        'api_url' => 'https://api.omise.co',
    ],

    // 2C2P Payment Gateway
    '2c2p' => [
        'merchant_id' => env('2C2P_MERCHANT_ID'),
        'secret_key' => env('2C2P_SECRET_KEY'),
        'enabled' => env('2C2P_ENABLED', false),
        'api_url' => env('2C2P_API_URL', 'https://pgw.2c2p.com'),
    ],

    // TrueMoney Wallet
    'truemoney' => [
        'app_id' => env('TRUEMONEY_APP_ID'),
        'secret' => env('TRUEMONEY_SECRET'),
        'enabled' => env('TRUEMONEY_ENABLED', false),
    ],

    // LINE Pay
    'linepay' => [
        'channel_id' => env('LINEPAY_CHANNEL_ID'),
        'channel_secret' => env('LINEPAY_CHANNEL_SECRET'),
        'enabled' => env('LINEPAY_ENABLED', false),
    ],

    // Slip Verification
    'slip_verification' => [
        'provider' => env('SLIP_VERIFY_PROVIDER', 'manual'), // manual, verifyslip, openslip
        'verifyslip_api_key' => env('VERIFYSLIP_API_KEY'),
        'openslip_api_key' => env('OPENSLIP_API_KEY'),
        'auto_verify' => env('SLIP_AUTO_VERIFY', false),
    ],

    // Payment Settings
    'settings' => [
        'default_currency' => 'THB',
        'vat_rate' => 0.07, // 7% VAT
        'payment_timeout_minutes' => 30,
        'auto_expire_pending_hours' => 24,
        'require_slip_for_bank_transfer' => true,
    ],

    // Invoice Settings
    'invoice' => [
        'company_name' => env('INVOICE_COMPANY_NAME', 'PostXAgent Co., Ltd.'),
        'company_name_th' => env('INVOICE_COMPANY_NAME_TH', 'บริษัท โพสต์เอ็กซ์เอเจนท์ จำกัด'),
        'tax_id' => env('INVOICE_TAX_ID', ''),
        'address' => env('INVOICE_ADDRESS', ''),
        'address_th' => env('INVOICE_ADDRESS_TH', ''),
        'phone' => env('INVOICE_PHONE', ''),
        'email' => env('INVOICE_EMAIL', ''),
        'logo_url' => env('INVOICE_LOGO_URL'),
    ],
];
