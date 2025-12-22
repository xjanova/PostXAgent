# SMS Payment Gateway - Complete Integration Guide

## ภาพรวม

ระบบ SMS Payment Gateway ของ PostXAgent ใช้วิธี **Unique Amount** เพื่อจับคู่การชำระเงินจาก SMS กับ Order ได้อัตโนมัติ 100% โดยไม่ต้องใช้ Bank API

ระบบรองรับการเชื่อมต่อ **หลายเว็บไซต์พร้อมกัน** โดยใช้แอพมือถือเครื่องเดียวเป็น Gateway กลาง

## สถาปัตยกรรมระบบ (Multi-Website Architecture)

```
┌─────────────────────────────────────────────────────────────────────┐
│                   MOBILE SMS GATEWAY APP                             │
│                    (.NET MAUI Android)                               │
│  ┌─────────────────┐     ┌─────────────────┐                        │
│  │ SMS Listener    │────▶│ AI Detection    │                        │
│  │                 │     │ + Classification│                        │
│  │ รับ SMS จาก     │     │                 │                        │
│  │ ธนาคาร          │     │ เงินเข้า/ออก/สแปม│                        │
│  └─────────────────┘     └─────────────────┘                        │
│            │                                                         │
│            │ Bank Accounts Config (บัญชีที่จะรับโอน)                 │
│            │                                                         │
│            ▼                                                         │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │              SEQUENTIAL WEBHOOK DISPATCH                       │  │
│  │  ┌─────────┐   ┌─────────┐   ┌─────────┐   ┌─────────┐       │  │
│  │  │ Site 1  │──▶│ Site 2  │──▶│ Site 3  │──▶│ Site N  │       │  │
│  │  │Priority1│   │Priority2│   │Priority3│   │PriorityN│       │  │
│  │  └────┬────┘   └────┬────┘   └────┬────┘   └────┬────┘       │  │
│  │       │             │             │             │             │  │
│  │   matched?      matched?      matched?      matched?          │  │
│  │     YES→STOP      NO→NEXT       NO→NEXT       NO→ALERT        │  │
│  └───────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐    ┌───────────────┐    ┌───────────────┐
│   Website A   │    │   Website B   │    │   Website C   │
│   PostXAgent  │    │   E-commerce  │    │   POS System  │
│               │    │               │    │               │
│ /api/sms-     │    │ /webhook/     │    │ /api/payment/ │
│ gateway/      │    │ payment       │    │ notify        │
│ webhook       │    │               │    │               │
└───────────────┘    └───────────────┘    └───────────────┘
```

## ปัญหาที่ต้องแก้

เมื่อใช้ SMS ตรวจจับการโอนเงิน จะเจอปัญหา:

```
ลูกค้า A สั่งซื้อ 100 บาท
ลูกค้า B สั่งซื้อ 100 บาท (ในเวลาใกล้เคียงกัน)

SMS เข้ามา: "โอนเข้า 100.00 บาท"

❓ จะรู้ได้อย่างไรว่าเป็นเงินของใคร?
```

## วิธีแก้: Unique Amount

ระบบจะเพิ่มสตางค์อัตโนมัติให้แต่ละ Order มียอดไม่ซ้ำกัน:

```
Order แรก  : 100 บาท → ยอดชำระ 100.00 บาท (ไม่เพิ่ม)
Order ที่ 2 : 100 บาท → ยอดชำระ 100.01 บาท (+1 สตางค์)
Order ที่ 3 : 100 บาท → ยอดชำระ 100.02 บาท (+2 สตางค์)
```

เมื่อ SMS เข้ามา:
- "โอนเข้า 100.01 บาท" → ระบบรู้ทันทีว่าเป็นของ Order ที่ 2
- "โอนเข้า 100.02 บาท" → ระบบรู้ทันทีว่าเป็นของ Order ที่ 3

---

# การใช้งาน SMS Gateway สำหรับโปรเจคอื่น

## ขั้นตอนการ Integrate

### 1. ลงทะเบียนเว็บไซต์ที่แอพมือถือ

ตั้งค่าเว็บไซต์ที่จะรับ webhook ในแอพมือถือ:

| ฟิลด์ | ค่า | คำอธิบาย |
|-------|-----|----------|
| Name | ชื่อเว็บไซต์ | เช่น "ร้าน ABC" |
| Webhook URL | URL ของ endpoint | เช่น `https://example.com/api/sms-gateway/webhook` |
| API Key | API Key | สำหรับ authentication |
| Secret Key | Secret Key | สำหรับ signature |
| Priority | 1-999 | ลำดับความสำคัญ (1 = สูงสุด) |
| Timeout | 30 | วินาที |

### 2. สร้าง Webhook Endpoint

สร้าง endpoint ที่รับ webhook จากแอพมือถือ:

```php
// Laravel Example: routes/api.php
Route::post('/sms-gateway/webhook', [PaymentGatewayController::class, 'smsWebhook']);
```

### 3. Webhook Request Format

แอพจะส่ง POST request มาในรูปแบบ:

#### Headers

| Header | Description |
|--------|-------------|
| `X-Api-Key` | API Key ที่ลงทะเบียนไว้ |
| `X-Timestamp` | Unix timestamp (milliseconds) |
| `X-Signature` | HMAC-SHA256 signature |
| `X-Request-Id` | Unique request ID |
| `Content-Type` | `application/json` |

#### Signature Verification

```php
function verifySignature(Request $request): bool
{
    $apiKey = $request->header('X-Api-Key');
    $timestamp = (int) $request->header('X-Timestamp');
    $signature = $request->header('X-Signature');

    // ตรวจสอบ API Key
    $secretKey = $this->getSecretKeyForApiKey($apiKey);
    if (!$secretKey) {
        return false;
    }

    // ตรวจสอบ timestamp (ไม่เกิน 5 นาที)
    $now = round(microtime(true) * 1000);
    if (abs($now - $timestamp) > 300000) {
        return false;
    }

    // ตรวจสอบ signature
    $payload = $request->getContent();
    $dataToSign = "{$timestamp}.{$payload}";
    $expectedSignature = base64_encode(
        hash_hmac('sha256', $dataToSign, $secretKey, true)
    );

    return hash_equals($expectedSignature, $signature);
}
```

#### Request Body

```json
{
    "requestId": "uuid-string",
    "event": "payment.received",
    "timestamp": 1703123456789,
    "payment": {
        "amount": 100.01,
        "currency": "THB",
        "bankName": "กสิกรไทย",
        "accountNumber": "xxx-x-xxxxx-x",
        "reference": "REF123456",
        "senderName": "นาย สมชาย ใจดี",
        "transactionTime": "2024-12-20T15:30:00Z",
        "rawSmsBody": "โอนเข้า 100.01 บาท จาก นาย สมชาย xxx",
        "confidenceScore": 0.95
    },
    "device": {
        "deviceId": "Samsung Galaxy A52",
        "deviceName": "เครื่อง SMS Gateway",
        "appVersion": "1.0.0"
    },
    "bankAccounts": {
        "isReady": true,
        "enabledCount": 3,
        "accounts": [
            {
                "bankType": "KBank",
                "bankName": "ธนาคารกสิกรไทย",
                "accountNumber": "xxx-x-12345-x",
                "accountName": "บริษัท ABC จำกัด"
            },
            {
                "bankType": "PromptPay",
                "bankName": "พร้อมเพย์",
                "accountNumber": "081-xxx-xxxx",
                "accountName": "นาย สมชาย"
            }
        ],
        "notReadyMessage": null
    },
    "gateway": {
        "isOnline": true,
        "lastSmsCheck": "2024-12-20T15:30:00Z",
        "appVersion": "1.0.0"
    }
}
```

### 4. Response Format

#### ตรวจพบ Order ที่ตรงกัน (Matched)

```json
{
    "success": true,
    "matched": true,
    "order": {
        "orderNumber": "ORD-ABC123",
        "amount": 100.01,
        "customerName": "คุณสมชาย",
        "description": "สินค้า XYZ",
        "createdAt": "2024-12-20T15:00:00Z"
    },
    "message": "จับคู่สำเร็จ"
}
```

#### ไม่พบ Order ที่ตรงกัน (Not Matched)

```json
{
    "success": true,
    "matched": false,
    "message": "ไม่พบ Order ที่ตรงกับยอดนี้"
}
```

**สำคัญ:** เมื่อ `matched: false` แอพจะส่งต่อไปยังเว็บไซต์ถัดไปตามลำดับ Priority

#### Error Response

```json
{
    "success": false,
    "matched": false,
    "error": "invalid_signature",
    "message": "Signature ไม่ถูกต้อง"
}
```

---

## Bank Accounts Integration

### การรับข้อมูลบัญชีธนาคาร

แอพมือถือจะส่งข้อมูลบัญชีธนาคารที่ตั้งค่าไว้มาด้วยทุกครั้ง ในฟิลด์ `bankAccounts`:

```json
{
    "bankAccounts": {
        "isReady": true,
        "enabledCount": 3,
        "accounts": [
            {
                "bankType": "KBank",
                "bankName": "ธนาคารกสิกรไทย",
                "accountNumber": "xxx-x-12345-x",
                "accountName": "บริษัท ABC จำกัด"
            }
        ],
        "notReadyMessage": null
    }
}
```

### ตรวจสอบ Gateway พร้อมใช้งาน

**ก่อนแสดงหน้าชำระเงิน** ให้ตรวจสอบว่า Gateway พร้อมรับเงิน:

```php
// Event: connection.test
$response = Http::withHeaders([
    'X-Api-Key' => $apiKey,
    'X-Timestamp' => $timestamp,
    'X-Signature' => $signature,
])->post($webhookUrl, [
    'event' => 'connection.test',
    'timestamp' => $timestamp,
    'gateway' => [
        'isOnline' => true,
    ],
    'bankAccounts' => [
        'isReady' => true,
        'enabledCount' => 3,
        'accounts' => [...],
    ],
]);

if ($response->successful()) {
    $data = $response->json();

    if ($data['gateway_ready']) {
        // แสดงบัญชีให้ลูกค้าโอน
        $bankAccounts = $data['bank_accounts'];
    } else {
        // Gateway ไม่พร้อม - แสดงช่องทางชำระเงินอื่น
        showAlternativePayment();
    }
}
```

### แสดงบัญชีให้ลูกค้าโอน

```vue
<!-- Vue.js Example -->
<template>
  <div v-if="gatewayReady">
    <h3>กรุณาโอนเงินไปที่บัญชีใดบัญชีหนึ่ง:</h3>
    <div v-for="account in bankAccounts" :key="account.accountNumber" class="bank-card">
      <div class="bank-name">{{ account.bankName }}</div>
      <div class="account-number">{{ account.accountNumber }}</div>
      <div class="account-name">{{ account.accountName }}</div>
    </div>
  </div>
  <div v-else>
    <p>{{ notReadyMessage }}</p>
    <button @click="useAlternativePayment">ใช้ช่องทางอื่น</button>
  </div>
</template>
```

### ธนาคารที่รองรับ

| Bank Type | ชื่อธนาคาร | หมายเหตุ |
|-----------|------------|----------|
| KBank | กสิกรไทย | - |
| SCB | ไทยพาณิชย์ | - |
| BBL | กรุงเทพ | - |
| KTB | กรุงไทย | - |
| TTB | ทีเอ็มบีธนชาต | - |
| BAY | กรุงศรีอยุธยา | - |
| GSB | ออมสิน | - |
| BAAC | ธ.ก.ส. | - |
| PromptPay | พร้อมเพย์ | เบอร์โทร/บัตรประชาชน |
| TrueMoney | ทรูมันนี่ | e-wallet |
| LinePay | LINE Pay | e-wallet |
| ShopeePay | ShopeePay | e-wallet |

---

## Gateway Not Ready Handling

เมื่อ `bankAccounts.isReady = false` แอพจะส่ง HTTP 503:

```json
{
    "success": false,
    "matched": false,
    "error": "gateway_not_ready",
    "message": "แอพ SMS Gateway ยังไม่พร้อมรับชำระเงิน กรุณาตั้งค่าบัญชีธนาคารก่อน"
}
```

**การจัดการฝั่งเว็บไซต์:**

```php
public function handleGatewayNotReady()
{
    // 1. ซ่อนตัวเลือกโอนเงินผ่าน SMS Gateway
    // 2. แสดงช่องทางอื่น (QR Code, บัตรเครดิต, COD)
    // 3. แจ้ง Admin ให้ตรวจสอบแอพ

    return response()->json([
        'payment_methods' => [
            'sms_gateway' => ['available' => false, 'reason' => 'Gateway offline'],
            'credit_card' => ['available' => true],
            'promptpay_qr' => ['available' => true],
            'cod' => ['available' => true],
        ]
    ]);
}
```

---

## การทำงานของระบบ

### 1. สร้าง Order ใหม่

```
POST /api/v1/payment-gateway/orders
{
    "amount": 100,
    "description": "สินค้า ABC",
    "customer_name": "คุณสมชาย"
}
```

### 2. ระบบคำนวณยอดที่ไม่ซ้ำ

```php
private function calculateUniqueAmount(float $baseAmount): array
{
    // 1. หา Order ที่ยอดใกล้เคียงและยังรอชำระ
    $similarOrders = PaymentOrder::where('status', 'pending')
        ->where('expires_at', '>', now())
        ->whereBetween('base_amount', [$baseAmount - 0.50, $baseAmount + 0.50])
        ->get();

    // 2. หาสตางค์ที่ยังว่างอยู่
    $usedSuffixes = $similarOrders->pluck('amount_suffix')->toArray();

    for ($i = 1; $i <= 99; $i++) {
        $testSuffix = $i / 100; // 0.01, 0.02, ...
        if (!in_array($testSuffix, $usedSuffixes)) {
            return [
                'final_amount' => $baseAmount + $testSuffix,
                'suffix' => $testSuffix
            ];
        }
    }
}
```

### 3. Response กลับไปพร้อมคำอธิบาย

```json
{
    "success": true,
    "data": {
        "order_number": "ORD-ABC123XYZ",
        "base_amount": 100.00,
        "amount": 100.01,
        "amount_suffix": 0.01
    },
    "amount_notice": "ยอดชำระถูกปรับเป็น ฿100.01 (เพิ่ม 1 สตางค์) เพื่อให้ระบบจับคู่การชำระเงินได้อัตโนมัติ",
    "payment_instructions": {
        "amount_to_pay": 100.01,
        "amount_formatted": "฿100.01",
        "original_amount": 100,
        "added_satang": 1,
        "reason": "เพิ่มสตางค์เพื่อให้ยอดไม่ซ้ำกับ Order อื่น ระบบจะจับคู่การชำระเงินได้อัตโนมัติ"
    }
}
```

### 4. แอพมือถือรับ SMS และส่ง Webhook

```
POST https://your-website.com/api/sms-gateway/webhook
Headers:
    X-Api-Key: your-api-key
    X-Timestamp: 1703123456789
    X-Signature: base64-hmac-signature
    X-Request-Id: uuid

Body:
{
    "event": "payment.received",
    "payment": {
        "amount": 100.01,
        ...
    }
}
```

### 5. ระบบจับคู่อัตโนมัติ (Exact Match)

```php
private function autoMatchPayment(array $paymentData): ?array
{
    $amount = $paymentData['amount'];

    // ใช้ exact match เพราะยอดไม่ซ้ำกัน
    $order = PaymentOrder::where('status', 'pending')
        ->whereRaw('ABS(amount - ?) < 0.005', [$amount])
        ->first();

    if ($order) {
        // จับคู่สำเร็จ!
        $order->update(['status' => 'paid']);

        return [
            'matched' => true,
            'order' => [
                'orderNumber' => $order->order_number,
                'amount' => $order->amount,
                'customerName' => $order->customer_name,
            ]
        ];
    }

    return ['matched' => false];
}
```

---

## โครงสร้างฐานข้อมูล

### ตาราง payment_orders

| Column | Type | Description |
|--------|------|-------------|
| id | bigint | Primary key |
| order_number | varchar | เลข Order (ORD-XXXXXXXXXX) |
| **base_amount** | decimal(12,2) | ยอดเดิมที่ลูกค้าต้องการชำระ |
| **amount** | decimal(12,2) | ยอดที่ต้องชำระจริง (รวมสตางค์) |
| **amount_suffix** | decimal(4,2) | สตางค์ที่เพิ่ม (0.01, 0.02, ...) |
| status | varchar | pending, matched, paid, cancelled |
| expires_at | timestamp | เวลาหมดอายุ |
| customer_name | varchar | ชื่อลูกค้า |
| description | varchar | รายละเอียด |
| bank_channel | varchar | บัญชีที่รับโอน (เก็บหลังจับคู่) |

### ตาราง sms_payments

| Column | Type | Description |
|--------|------|-------------|
| id | bigint | Primary key |
| device_id | varchar | อุปกรณ์ที่รับ SMS |
| sms_body | text | ข้อความ SMS ต้นฉบับ |
| amount | decimal(12,2) | จำนวนเงินที่ตรวจจับได้ |
| bank_name | varchar | ชื่อธนาคาร |
| confidence | decimal(3,2) | ความมั่นใจ (0-1) |
| order_id | bigint | Order ที่จับคู่ได้ |
| status | varchar | pending, approved, rejected |

---

## Flow Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                        FRONTEND (Vue.js)                             │
│  ┌─────────────────┐     ┌─────────────────┐                        │
│  │ สร้าง Order     │────▶│ แสดงยอดชำระ     │                        │
│  │ 100 บาท        │     │ ฿100.01         │                        │
│  └─────────────────┘     │ (+1 สตางค์)     │                        │
│                          │                 │                        │
│                          │ แสดงบัญชีโอน:   │                        │
│                          │ - กสิกร xxx     │                        │
│                          │ - พร้อมเพย์ xxx │                        │
│                          └─────────────────┘                        │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        LARAVEL API                                   │
│  ┌─────────────────┐     ┌─────────────────┐                        │
│  │ calculateUnique │────▶│ PaymentOrder    │                        │
│  │ Amount()        │     │ base: 100       │                        │
│  │                 │     │ amount: 100.01  │                        │
│  │ หา suffix ว่าง  │     │ suffix: 0.01    │                        │
│  └─────────────────┘     └─────────────────┘                        │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 │ ลูกค้าโอนเงิน 100.01 บาท
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      MOBILE APP (.NET MAUI)                          │
│  ┌─────────────────┐     ┌─────────────────┐                        │
│  │ SMS Listener    │────▶│ AI Detection    │                        │
│  │                 │     │ + Classification│                        │
│  │ รับ SMS จาก     │     │                 │                        │
│  │ ธนาคาร          │     │ ตรวจจับว่าเป็น  │                        │
│  └─────────────────┘     │ การโอนเงินเข้า  │                        │
│                          └─────────────────┘                        │
│                                 │                                    │
│    Bank Accounts: [กสิกร, พร้อมเพย์, SCB]                           │
│                                 │                                    │
│                                 ▼                                    │
│  ┌───────────────────────────────────────────────────────────────┐  │
│  │              SEQUENTIAL DISPATCH                               │  │
│  │                                                                │  │
│  │  Site 1 (Priority 1) ───▶ matched? ───NO──▶ Site 2            │  │
│  │                              │                                 │  │
│  │                             YES                                │  │
│  │                              │                                 │  │
│  │                              ▼                                 │  │
│  │                        ✅ STOP                                 │  │
│  └───────────────────────────────────────────────────────────────┘  │
└────────────────────────────────┬────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────┐
│                        LARAVEL API                                   │
│  ┌─────────────────┐     ┌─────────────────┐                        │
│  │ autoMatch       │────▶│ ✅ จับคู่สำเร็จ  │                        │
│  │ Payment()       │     │                 │                        │
│  │                 │     │ 100.01 = 100.01 │                        │
│  │ Exact Match     │     │ Order #123 PAID │                        │
│  └─────────────────┘     └─────────────────┘                        │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Complete Integration Example (Laravel)

### Controller

```php
<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\PaymentOrder;
use Illuminate\Http\Request;
use Illuminate\Support\Str;

class PaymentGatewayController extends Controller
{
    private string $apiKey;
    private string $secretKey;

    public function __construct()
    {
        $this->apiKey = config('services.sms_gateway.api_key');
        $this->secretKey = config('services.sms_gateway.secret_key');
    }

    /**
     * Receive webhook from SMS Gateway app
     */
    public function smsWebhook(Request $request)
    {
        // 1. Verify signature
        if (!$this->verifySignature($request)) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'invalid_signature',
            ], 401);
        }

        $event = $request->input('event', 'payment.received');
        $bankAccountsInfo = $request->input('bankAccounts', []);

        // 2. Check gateway status
        if (isset($bankAccountsInfo['isReady']) && $bankAccountsInfo['isReady'] === false) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'gateway_not_ready',
                'message' => $bankAccountsInfo['notReadyMessage'] ?? 'Gateway not ready',
            ], 503);
        }

        // 3. Handle connection test
        if ($event === 'connection.test') {
            return response()->json([
                'success' => true,
                'message' => 'เชื่อมต่อสำเร็จ',
                'bank_accounts' => $bankAccountsInfo['accounts'] ?? [],
                'gateway_ready' => $bankAccountsInfo['isReady'] ?? false,
            ]);
        }

        // 4. Process payment
        if ($event === 'payment.received') {
            $paymentData = $request->input('payment', []);
            $result = $this->autoMatchPayment($paymentData);

            return response()->json([
                'success' => true,
                'matched' => $result['matched'],
                'order' => $result['order'] ?? null,
                'message' => $result['matched'] ? 'จับคู่สำเร็จ' : 'ไม่พบ Order ที่ตรงกัน',
            ]);
        }

        return response()->json([
            'success' => false,
            'matched' => false,
            'error' => 'unknown_event',
        ], 400);
    }

    private function verifySignature(Request $request): bool
    {
        $apiKey = $request->header('X-Api-Key');
        $timestamp = (int) $request->header('X-Timestamp');
        $signature = $request->header('X-Signature');

        if ($apiKey !== $this->apiKey) {
            return false;
        }

        // Check timestamp (5 minutes tolerance)
        $now = round(microtime(true) * 1000);
        if (abs($now - $timestamp) > 300000) {
            return false;
        }

        // Verify HMAC signature
        $payload = $request->getContent();
        $dataToSign = "{$timestamp}.{$payload}";
        $expectedSignature = base64_encode(
            hash_hmac('sha256', $dataToSign, $this->secretKey, true)
        );

        return hash_equals($expectedSignature, $signature);
    }

    private function autoMatchPayment(array $paymentData): array
    {
        $amount = $paymentData['amount'] ?? 0;

        $order = PaymentOrder::where('status', 'pending')
            ->where('expires_at', '>', now())
            ->whereRaw('ABS(amount - ?) < 0.005', [$amount])
            ->first();

        if ($order) {
            $order->update([
                'status' => 'paid',
                'paid_at' => now(),
                'bank_channel' => $paymentData['bankName'] ?? null,
            ]);

            return [
                'matched' => true,
                'order' => [
                    'orderNumber' => $order->order_number,
                    'amount' => $order->amount,
                    'customerName' => $order->customer_name,
                    'description' => $order->description,
                    'createdAt' => $order->created_at->toIso8601String(),
                ],
            ];
        }

        return ['matched' => false];
    }
}
```

### Configuration

```php
// config/services.php
return [
    'sms_gateway' => [
        'api_key' => env('SMS_GATEWAY_API_KEY'),
        'secret_key' => env('SMS_GATEWAY_SECRET_KEY'),
    ],
];
```

```env
# .env
SMS_GATEWAY_API_KEY=your-api-key-here
SMS_GATEWAY_SECRET_KEY=your-secret-key-here
```

### Migration

```php
// database/migrations/xxxx_create_payment_orders_table.php
Schema::create('payment_orders', function (Blueprint $table) {
    $table->id();
    $table->string('order_number')->unique();
    $table->decimal('base_amount', 12, 2);
    $table->decimal('amount', 12, 2);
    $table->decimal('amount_suffix', 4, 2)->default(0);
    $table->string('status')->default('pending');
    $table->timestamp('expires_at');
    $table->timestamp('paid_at')->nullable();
    $table->string('customer_name')->nullable();
    $table->string('description')->nullable();
    $table->string('bank_channel')->nullable();
    $table->timestamps();

    $table->index(['status', 'expires_at']);
    $table->index('amount');
});
```

---

## ข้อจำกัดและการจัดการ

### 1. สตางค์มีได้สูงสุด 99 ค่า (0.01 - 0.99)

หาก Order ยอดเดียวกันมากกว่า 99 รายการพร้อมกัน:
- ระบบจะเพิ่มทีละ 1 บาทแทน (1.00, 1.01, 1.02, ...)
- ในทางปฏิบัติแทบเป็นไปไม่ได้

### 2. Order หมดอายุ

- สตางค์จะถูก "ปลดล็อค" เมื่อ Order หมดอายุ
- Order ใหม่สามารถใช้สตางค์นั้นได้อีก

### 3. การยกเลิก Order

- เมื่อยกเลิก Order สตางค์นั้นจะว่างทันที
- Order ใหม่สามารถใช้ได้

### 4. Multi-Website Conflict

เมื่อหลายเว็บไซต์มี Order ยอดใกล้เคียงกัน:
- แต่ละเว็บต้องมี pool สตางค์แยกกัน
- หรือใช้ reference number เสริม

---

## ข้อดีของระบบ

| ข้อดี | รายละเอียด |
|-------|------------|
| ✅ ไม่ต้องใช้ Bank API | ใช้ SMS จากมือถือตัวเอง |
| ✅ จับคู่อัตโนมัติ 100% | ยอดไม่มีทางซ้ำ |
| ✅ ประหยัดต้นทุน | ไม่ต้องจ่ายค่า API ธนาคาร |
| ✅ ตั้งค่าง่าย | แค่ติดตั้งแอพมือถือ |
| ✅ รองรับทุกธนาคาร | ใช้ SMS ซึ่งทุกธนาคารส่ง |
| ✅ Multi-Website | 1 แอพใช้กับหลายเว็บ |
| ✅ Bank Info Sharing | เว็บรู้บัญชีที่รับโอน |

## ข้อควรระวัง

| ข้อควรระวัง | คำแนะนำ |
|------------|---------|
| ⚠️ ลูกค้าต้องโอนยอดที่แสดง | แสดงยอดใหญ่และชัดเจน |
| ⚠️ ยอดต่างจากที่สั่ง | อธิบายเหตุผลให้ลูกค้าเข้าใจ |
| ⚠️ มือถือต้องเปิดตลอด | ใช้มือถือเฉพาะสำหรับระบบนี้ |
| ⚠️ ตั้งค่าบัญชีก่อน | แอพต้องมีบัญชีอย่างน้อย 1 บัญชี |

---

## สรุป

ระบบ SMS Payment Gateway ช่วยให้:

1. **Unique Amount** - เพิ่มสตางค์ให้แต่ละ Order มียอดไม่ซ้ำกัน
2. **Exact Match** - จับคู่ SMS กับ Order ได้ทันที
3. **Multi-Website** - 1 แอพรองรับหลายเว็บไซต์
4. **Bank Accounts** - ส่งข้อมูลบัญชีไปยังเว็บไซต์
5. **Gateway Status** - ตรวจสอบความพร้อมก่อนรับชำระ
6. **ไม่ต้องใช้ Bank API** - ประหยัดต้นทุน

---

**เอกสารนี้เป็นส่วนหนึ่งของ PostXAgent**
*อัพเดตล่าสุด: December 2024*
