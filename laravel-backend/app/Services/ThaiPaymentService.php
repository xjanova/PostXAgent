<?php

namespace App\Services;

use App\Models\User;
use App\Models\Payment;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use SimpleSoftwareIO\QrCode\Facades\QrCode;

class ThaiPaymentService
{
    protected string $promptPayNumber;
    protected string $promptPayName;
    protected array $bankAccounts;
    protected ?string $omisePublicKey;
    protected ?string $omiseSecretKey;

    public function __construct()
    {
        $this->promptPayNumber = config('payment.promptpay.number', '');
        $this->promptPayName = config('payment.promptpay.name', 'PostXAgent Co., Ltd.');
        $this->bankAccounts = config('payment.bank_accounts', []);
        $this->omisePublicKey = config('payment.omise.public_key');
        $this->omiseSecretKey = config('payment.omise.secret_key');
    }

    /**
     * Generate PromptPay QR Code
     */
    public function generatePromptPayQR(float $amount, string $reference): array
    {
        // Generate PromptPay payload following BOT standard
        $payload = $this->generatePromptPayPayload($amount);

        // Generate QR Code as SVG
        $qrCode = QrCode::format('svg')
            ->size(300)
            ->errorCorrection('M')
            ->generate($payload);

        // Also generate base64 PNG for mobile
        $qrCodePng = QrCode::format('png')
            ->size(300)
            ->errorCorrection('M')
            ->generate($payload);

        return [
            'qr_code' => base64_encode($qrCode),
            'qr_code_png' => base64_encode($qrCodePng),
            'qr_image_url' => null, // Can store to S3/public folder
            'promptpay_number' => $this->maskPromptPayNumber(),
            'promptpay_name' => $this->promptPayName,
            'amount' => $amount,
            'reference' => $reference,
            'payload' => $payload,
        ];
    }

    /**
     * Generate PromptPay EMVCo payload
     * Following Bank of Thailand PromptPay Standard
     */
    protected function generatePromptPayPayload(float $amount): string
    {
        $promptPayId = $this->promptPayNumber;

        // Remove any non-numeric characters and format
        $promptPayId = preg_replace('/[^0-9]/', '', $promptPayId);

        // Determine ID type (phone or national ID)
        $idType = strlen($promptPayId) === 13 ? '02' : '01'; // 02 = National ID, 01 = Phone

        // Format phone number (add country code if needed)
        if ($idType === '01' && strlen($promptPayId) === 10) {
            $promptPayId = '66' . substr($promptPayId, 1); // Convert to international format
        }

        // Build EMVCo QR payload
        $data = [];

        // Payload Format Indicator (ID 00)
        $data[] = '00' . '02' . '01';

        // Point of Initiation Method (ID 01) - 12 = Dynamic QR
        $data[] = '01' . '02' . '12';

        // Merchant Account Information (ID 29 - PromptPay)
        $merchantInfo = [];
        $merchantInfo[] = '00' . '16' . 'A000000677010111'; // Application ID
        $merchantInfo[] = $idType . sprintf('%02d', strlen($promptPayId)) . $promptPayId;
        $merchantInfoStr = implode('', $merchantInfo);
        $data[] = '29' . sprintf('%02d', strlen($merchantInfoStr)) . $merchantInfoStr;

        // Country Code (ID 58)
        $data[] = '58' . '02' . 'TH';

        // Transaction Currency (ID 53) - 764 = THB
        $data[] = '53' . '03' . '764';

        // Transaction Amount (ID 54)
        if ($amount > 0) {
            $amountStr = number_format($amount, 2, '.', '');
            $data[] = '54' . sprintf('%02d', strlen($amountStr)) . $amountStr;
        }

        // CRC placeholder (ID 63)
        $payload = implode('', $data) . '6304';

        // Calculate CRC16-CCITT
        $crc = $this->calculateCRC16($payload);
        $payload .= strtoupper(sprintf('%04X', $crc));

        return $payload;
    }

    /**
     * Calculate CRC16-CCITT checksum
     */
    protected function calculateCRC16(string $data): int
    {
        $crc = 0xFFFF;
        $polynomial = 0x1021;

        for ($i = 0; $i < strlen($data); $i++) {
            $crc ^= (ord($data[$i]) << 8);
            for ($j = 0; $j < 8; $j++) {
                if (($crc & 0x8000) !== 0) {
                    $crc = (($crc << 1) ^ $polynomial) & 0xFFFF;
                } else {
                    $crc = ($crc << 1) & 0xFFFF;
                }
            }
        }

        return $crc;
    }

    /**
     * Get masked PromptPay number for display
     */
    protected function maskPromptPayNumber(): string
    {
        $number = $this->promptPayNumber;
        if (strlen($number) === 10) {
            // Phone number: 08X-XXX-1234
            return substr($number, 0, 3) . '-XXX-' . substr($number, -4);
        } elseif (strlen($number) === 13) {
            // National ID: X-XXXX-XXXXX-12-3
            return 'X-XXXX-XXXXX-' . substr($number, -4, 2) . '-' . substr($number, -1);
        }
        return 'XXX-XXXX-' . substr($number, -4);
    }

    /**
     * Get bank transfer information
     */
    public function getBankTransferInfo(): array
    {
        if (empty($this->bankAccounts)) {
            // Default bank accounts
            return [
                [
                    'bank_name' => 'ธนาคารกสิกรไทย',
                    'bank_code' => 'KBANK',
                    'account_number' => 'XXX-X-XXXXX-X',
                    'account_name' => $this->promptPayName,
                    'branch' => 'สำนักงานใหญ่',
                ],
                [
                    'bank_name' => 'ธนาคารไทยพาณิชย์',
                    'bank_code' => 'SCB',
                    'account_number' => 'XXX-X-XXXXX-X',
                    'account_name' => $this->promptPayName,
                    'branch' => 'สำนักงานใหญ่',
                ],
            ];
        }

        return $this->bankAccounts;
    }

    /**
     * Create card checkout session via Omise
     */
    public function createCardCheckout(float $amount, string $reference, User $user): array
    {
        if (!$this->omiseSecretKey) {
            return [
                'success' => false,
                'error' => 'Payment gateway not configured',
                'checkout_url' => null,
            ];
        }

        try {
            // Convert to smallest currency unit (satang for THB)
            $amountInSatang = (int) ($amount * 100);

            $response = Http::withBasicAuth($this->omiseSecretKey, '')
                ->post('https://api.omise.co/charges', [
                    'amount' => $amountInSatang,
                    'currency' => 'THB',
                    'description' => "Payment for {$reference}",
                    'metadata' => [
                        'reference' => $reference,
                        'user_id' => $user->id,
                        'user_email' => $user->email,
                    ],
                    'return_uri' => config('app.url') . '/api/v1/payments/callback/omise',
                ]);

            if ($response->successful()) {
                $data = $response->json();
                return [
                    'success' => true,
                    'checkout_url' => $data['authorize_uri'] ?? null,
                    'charge_id' => $data['id'],
                    'status' => $data['status'],
                ];
            }

            Log::error('Omise charge creation failed', [
                'response' => $response->json(),
            ]);

            return [
                'success' => false,
                'error' => 'Failed to create payment',
                'checkout_url' => null,
            ];

        } catch (\Exception $e) {
            Log::error('Omise exception', ['error' => $e->getMessage()]);
            return [
                'success' => false,
                'error' => $e->getMessage(),
                'checkout_url' => null,
            ];
        }
    }

    /**
     * Verify Omise webhook
     */
    public function verifyOmiseWebhook(string $payload, string $signature): bool
    {
        $webhookSecret = config('payment.omise.webhook_secret');
        if (!$webhookSecret) {
            return false;
        }

        $expectedSignature = hash_hmac('sha256', $payload, $webhookSecret);
        return hash_equals($expectedSignature, $signature);
    }

    /**
     * Handle Omise webhook event
     */
    public function handleOmiseWebhook(array $event): array
    {
        $eventType = $event['key'] ?? '';
        $data = $event['data'] ?? [];

        Log::info('Omise webhook received', ['type' => $eventType]);

        if ($eventType === 'charge.complete') {
            $reference = $data['metadata']['reference'] ?? null;
            if ($reference) {
                $payment = Payment::where('payment_reference', $reference)->first();
                if ($payment && $payment->status === Payment::STATUS_PENDING) {
                    $payment->update([
                        'gateway' => 'omise',
                        'gateway_reference' => $data['id'],
                        'gateway_status' => $data['status'],
                        'gateway_response' => $data,
                    ]);

                    if ($data['status'] === 'successful') {
                        $payment->markAsCompleted();
                        return ['success' => true, 'message' => 'Payment completed'];
                    }
                }
            }
        }

        return ['success' => true, 'message' => 'Webhook processed'];
    }

    /**
     * Create 2C2P payment (alternative gateway)
     */
    public function create2C2PPayment(float $amount, string $reference, User $user): array
    {
        $merchantId = config('payment.2c2p.merchant_id');
        $secretKey = config('payment.2c2p.secret_key');

        if (!$merchantId || !$secretKey) {
            return [
                'success' => false,
                'error' => '2C2P not configured',
            ];
        }

        // 2C2P implementation would go here
        // This is a placeholder for the actual 2C2P API integration

        return [
            'success' => false,
            'error' => '2C2P integration pending',
        ];
    }

    /**
     * Verify slip via OCR/AI (placeholder)
     */
    public function verifyTransferSlip(string $slipPath, float $expectedAmount, string $reference): array
    {
        // This would integrate with:
        // - VerifySlip API (Thai service)
        // - OpenSlip API
        // - Custom OCR solution

        // For now, return pending manual verification
        return [
            'verified' => false,
            'requires_manual_review' => true,
            'confidence' => 0,
            'extracted_data' => null,
        ];
    }

    /**
     * Get supported payment methods
     */
    public function getSupportedMethods(): array
    {
        $methods = [
            [
                'id' => 'promptpay',
                'name' => 'PromptPay',
                'name_th' => 'พร้อมเพย์',
                'icon' => 'promptpay',
                'enabled' => !empty($this->promptPayNumber),
                'description' => 'ชำระเงินผ่าน QR Code พร้อมเพย์',
            ],
            [
                'id' => 'bank_transfer',
                'name' => 'Bank Transfer',
                'name_th' => 'โอนเงินผ่านธนาคาร',
                'icon' => 'bank',
                'enabled' => !empty($this->bankAccounts),
                'description' => 'โอนเงินผ่านบัญชีธนาคาร',
            ],
            [
                'id' => 'credit_card',
                'name' => 'Credit/Debit Card',
                'name_th' => 'บัตรเครดิต/เดบิต',
                'icon' => 'credit-card',
                'enabled' => !empty($this->omiseSecretKey),
                'description' => 'ชำระด้วยบัตรเครดิตหรือเดบิต',
            ],
        ];

        return array_filter($methods, fn($m) => $m['enabled']);
    }
}
