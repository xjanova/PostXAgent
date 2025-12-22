# SMS Payment Gateway - Integration Guide

## Overview

เอกสารนี้อธิบายวิธีการเชื่อมต่อระบบ Payment ของเว็บไซต์คุณกับ **SMS Gateway Mobile App** เพื่อรับการชำระเงินผ่าน SMS อัตโนมัติ โดยไม่ต้องใช้ Bank API

## สถาปัตยกรรมระบบ

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SMS Gateway Mobile App                             │
│                            (Android/.NET MAUI)                               │
│                                                                              │
│    ┌─────────────┐     ┌─────────────┐     ┌─────────────────────────┐     │
│    │ SMS Listener │────▶│ AI Classifier│────▶│ Webhook Dispatcher      │     │
│    │              │     │ (เงินเข้า/ออก)│     │ (Sequential)            │     │
│    └─────────────┘     └─────────────┘     └───────────┬─────────────┘     │
│                                                         │                    │
└─────────────────────────────────────────────────────────┼────────────────────┘
                                                          │
                          ┌───────────────────────────────┼───────────────────┐
                          │                               │                   │
                          ▼                               ▼                   ▼
              ┌───────────────────┐         ┌───────────────────┐   ┌──────────────┐
              │    Website 1       │         │    Website 2       │   │  Website 3   │
              │ (Priority: 1)      │         │ (Priority: 2)      │   │ (Priority: 3)│
              │                    │         │                    │   │              │
              │ POST /webhook      │         │ POST /webhook      │   │ POST /webhook│
              │                    │         │                    │   │              │
              │ matched: true ───▶ STOP     │ matched: false ───▶│   │              │
              │ matched: false ───▶─────────▶│                    │   │              │
              └───────────────────┘         └───────────────────┘   └──────────────┘
```

## Flow การทำงาน

1. **SMS เข้า** - Mobile App รับ SMS จากธนาคาร
2. **AI Classification** - ระบบวิเคราะห์ว่าเป็น เงินเข้า/เงินออก/SMS ขยะ
3. **เฉพาะเงินเข้า** - ถ้าเป็นเงินเข้า จะส่ง webhook ไปยังเว็บไซต์ที่กำหนด
4. **Sequential Dispatch** - ส่งทีละเว็บตามลำดับ Priority
5. **Match Found** - ถ้าเว็บใดตอบ `matched: true` หยุดส่งต่อ
6. **No Match** - ถ้าไม่มีเว็บใดตรง แจ้งเตือน Admin บน App

## การเชื่อมต่อ

### 1. สร้าง Webhook Endpoint

สร้าง endpoint ที่เว็บของคุณเพื่อรับ webhook จาก SMS Gateway:

```
POST https://your-website.com/api/sms-gateway/webhook
```

### 2. ตั้งค่า Security

Webhook ใช้ระบบ HMAC-SHA256 Signature เพื่อความปลอดภัย:

| Header | Description |
|--------|-------------|
| `X-Api-Key` | API Key ที่ได้รับจากการตั้งค่า |
| `X-Timestamp` | Unix timestamp (milliseconds) |
| `X-Signature` | HMAC-SHA256 signature |
| `X-Request-Id` | Unique request ID |

### 3. Signature Verification

```php
// PHP Example
function verifySignature(Request $request): bool
{
    $apiKey = $request->header('X-Api-Key');
    $timestamp = $request->header('X-Timestamp');
    $signature = $request->header('X-Signature');
    $payload = $request->getContent();

    // Verify API Key
    if ($apiKey !== config('sms_gateway.api_key')) {
        return false;
    }

    // Verify timestamp (not older than 5 minutes)
    $now = (int)(microtime(true) * 1000);
    if (abs($now - (int)$timestamp) > 300000) {
        return false;
    }

    // Verify signature
    $expectedSignature = base64_encode(
        hash_hmac('sha256', "{$timestamp}.{$payload}", config('sms_gateway.secret_key'), true)
    );

    return hash_equals($expectedSignature, $signature);
}
```

```javascript
// Node.js Example
const crypto = require('crypto');

function verifySignature(req) {
    const apiKey = req.headers['x-api-key'];
    const timestamp = req.headers['x-timestamp'];
    const signature = req.headers['x-signature'];
    const payload = JSON.stringify(req.body);

    // Verify API Key
    if (apiKey !== process.env.SMS_GATEWAY_API_KEY) {
        return false;
    }

    // Verify timestamp
    const now = Date.now();
    if (Math.abs(now - parseInt(timestamp)) > 300000) {
        return false;
    }

    // Verify signature
    const expectedSignature = crypto
        .createHmac('sha256', process.env.SMS_GATEWAY_SECRET_KEY)
        .update(`${timestamp}.${payload}`)
        .digest('base64');

    return crypto.timingSafeEqual(
        Buffer.from(signature),
        Buffer.from(expectedSignature)
    );
}
```

## Webhook Payload

### Event: `connection.test`

ใช้ทดสอบการเชื่อมต่อ:

```json
{
    "requestId": "uuid-string",
    "event": "connection.test",
    "timestamp": 1703001234567,
    "payment": {
        "amount": 0,
        "bankName": "Test",
        "transactionTime": "2024-12-20T10:00:00Z"
    },
    "device": {
        "deviceId": "device-123",
        "deviceName": "Samsung Galaxy S23",
        "appVersion": "1.0.0"
    }
}
```

**Response:**
```json
{
    "success": true,
    "matched": false,
    "message": "Connection successful"
}
```

### Event: `payment.received`

เมื่อมีเงินเข้า:

```json
{
    "requestId": "uuid-string",
    "event": "payment.received",
    "timestamp": 1703001234567,
    "payment": {
        "amount": 100.01,
        "currency": "THB",
        "bankName": "กสิกรไทย",
        "accountNumber": "****1234",
        "reference": "REF123456",
        "senderName": "นาย สมชาย",
        "transactionTime": "2024-12-20T10:00:00Z",
        "rawSmsBody": "KBANK: รับโอน 100.01 บาท จาก...",
        "confidenceScore": 0.95
    },
    "device": {
        "deviceId": "device-123",
        "deviceName": "Samsung Galaxy S23",
        "appVersion": "1.0.0"
    }
}
```

## Response Format

เว็บไซต์ต้องตอบกลับในรูปแบบนี้:

### กรณีไม่มี Order ตรงกัน

```json
{
    "success": true,
    "matched": false,
    "message": "No matching order found"
}
```

**สำคัญ:** App จะส่งต่อไปเว็บถัดไป

### กรณีมี Order ตรงกัน

```json
{
    "success": true,
    "matched": true,
    "message": "Payment matched and approved",
    "order": {
        "orderNumber": "ORD-ABC123",
        "amount": 100.01,
        "customerName": "นาย สมชาย",
        "description": "สินค้า XYZ",
        "createdAt": "2024-12-20T09:55:00Z"
    }
}
```

**สำคัญ:** App จะหยุดส่งต่อทันที

### กรณี Error

```json
{
    "success": false,
    "matched": false,
    "error": "Internal server error"
}
```

## การ Implement บนเว็บไซต์

### ขั้นตอนที่ 1: สร้าง Payment Orders

ใช้ระบบ **Unique Amount** เพื่อให้ยอดไม่ซ้ำกัน:

```php
// Laravel Example
public function createOrder(Request $request)
{
    $baseAmount = $request->input('amount');

    // หายอดที่ไม่ซ้ำ
    $uniqueAmount = $this->calculateUniqueAmount($baseAmount);

    $order = PaymentOrder::create([
        'order_number' => 'ORD-' . Str::random(10),
        'base_amount' => $baseAmount,
        'amount' => $uniqueAmount['final_amount'],
        'amount_suffix' => $uniqueAmount['suffix'],
        'status' => 'pending',
        'expires_at' => now()->addHours(24),
    ]);

    return response()->json([
        'order' => $order,
        'amount_to_pay' => $uniqueAmount['final_amount'],
    ]);
}

private function calculateUniqueAmount(float $baseAmount): array
{
    // หา orders ที่ยอดใกล้เคียง
    $similarOrders = PaymentOrder::where('status', 'pending')
        ->where('expires_at', '>', now())
        ->whereBetween('base_amount', [$baseAmount - 0.5, $baseAmount + 0.5])
        ->get();

    if ($similarOrders->isEmpty()) {
        return ['final_amount' => $baseAmount, 'suffix' => 0];
    }

    // หาสตางค์ที่ว่าง
    $usedSuffixes = $similarOrders->pluck('amount_suffix')->toArray();

    for ($i = 1; $i <= 99; $i++) {
        $suffix = $i / 100;
        if (!in_array($suffix, $usedSuffixes)) {
            return [
                'final_amount' => round($baseAmount + $suffix, 2),
                'suffix' => $suffix,
            ];
        }
    }

    return ['final_amount' => $baseAmount + 1, 'suffix' => 1];
}
```

### ขั้นตอนที่ 2: สร้าง Webhook Handler

```php
// Laravel Controller
public function smsWebhook(Request $request)
{
    // 1. Verify signature
    if (!$this->verifySignature($request)) {
        return response()->json([
            'success' => false,
            'matched' => false,
            'error' => 'Invalid signature',
        ], 401);
    }

    $event = $request->input('event');

    // 2. Handle test event
    if ($event === 'connection.test') {
        return response()->json([
            'success' => true,
            'matched' => false,
            'message' => 'Connection successful',
        ]);
    }

    // 3. Handle payment event
    if ($event !== 'payment.received') {
        return response()->json([
            'success' => false,
            'matched' => false,
            'error' => 'Unknown event',
        ], 400);
    }

    $paymentData = $request->input('payment');
    $amount = (float) $paymentData['amount'];

    // 4. Find matching order (EXACT MATCH)
    $order = PaymentOrder::where('status', 'pending')
        ->where('expires_at', '>', now())
        ->whereRaw('ABS(amount - ?) < 0.005', [$amount])
        ->orderBy('created_at') // Order ที่สร้างก่อนได้สิทธิ์
        ->first();

    if (!$order) {
        // ไม่มี order ตรง - ให้ app ส่งต่อเว็บถัดไป
        return response()->json([
            'success' => true,
            'matched' => false,
            'message' => 'No matching order for ' . number_format($amount, 2),
        ]);
    }

    // 5. Approve order
    DB::transaction(function () use ($order, $paymentData, $request) {
        $order->update([
            'status' => 'paid',
            'paid_at' => now(),
            'payment_data' => [
                'bank' => $paymentData['bankName'],
                'reference' => $paymentData['reference'],
                'sms_body' => $paymentData['rawSmsBody'],
                'device_id' => $request->input('device.deviceId'),
            ],
        ]);

        // Trigger your business logic
        event(new OrderPaid($order));
    });

    return response()->json([
        'success' => true,
        'matched' => true,
        'message' => 'Payment matched and approved',
        'order' => [
            'orderNumber' => $order->order_number,
            'amount' => $order->amount,
            'customerName' => $order->customer_name,
            'description' => $order->description,
            'createdAt' => $order->created_at->toISOString(),
        ],
    ]);
}
```

## ตั้งค่าบน Mobile App

### 1. เพิ่มเว็บไซต์

ในแอพ ไปที่ **เว็บไซต์ที่เชื่อมต่อ** แล้วกด **เพิ่ม**:

| Field | Value |
|-------|-------|
| ชื่อเว็บไซต์ | ร้าน ABC (ชื่อสำหรับแสดง) |
| Webhook URL | `https://your-website.com/api/sms-gateway/webhook` |
| API Key | (Generate ในแอพ) |
| Secret Key | (Generate ในแอพ) |

### 2. ทดสอบการเชื่อมต่อ

กดปุ่ม **ทดสอบ** ในแต่ละเว็บไซต์ ถ้าสำเร็จจะแสดง "เชื่อมต่อสำเร็จ"

### 3. จัดลำดับ Priority

ลากเพื่อจัดลำดับ เว็บไซต์ที่อยู่บนสุดจะถูกตรวจสอบก่อน

## Database Schema

```sql
-- Payment Orders Table
CREATE TABLE payment_orders (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    order_number VARCHAR(50) UNIQUE NOT NULL,
    base_amount DECIMAL(12,2) NOT NULL,    -- ยอดเดิม
    amount DECIMAL(12,2) NOT NULL,          -- ยอดที่ต้องชำระ (รวมสตางค์)
    amount_suffix DECIMAL(4,2) DEFAULT 0,   -- สตางค์ที่เพิ่ม
    status ENUM('pending', 'paid', 'cancelled', 'expired') DEFAULT 'pending',
    customer_name VARCHAR(255),
    customer_email VARCHAR(255),
    description TEXT,
    expires_at TIMESTAMP NOT NULL,
    paid_at TIMESTAMP NULL,
    payment_data JSON,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    INDEX idx_status_expires (status, expires_at),
    INDEX idx_amount (amount)
);
```

## SMS Classification

App จะแยก SMS อัตโนมัติ:

| Type | Keywords | Action |
|------|----------|--------|
| **เงินเข้า** | รับโอน, เงินเข้า, รับเงิน, credit, received | ✅ ส่ง webhook |
| **เงินออก** | โอนเงิน, หักบัญชี, จ่าย, debit, withdraw | ❌ ไม่ส่ง |
| **OTP** | OTP, รหัส, verification | ❌ ไม่ส่ง |
| **SMS ขยะ** | โปรโมชั่น, สมัคร, สินเชื่อ | ❌ ไม่ส่ง |

## Error Handling

| HTTP Code | Meaning | App Action |
|-----------|---------|------------|
| 200 + matched:false | ไม่มี order ตรง | ส่งต่อเว็บถัดไป |
| 200 + matched:true | จับคู่สำเร็จ | หยุด |
| 401 | Signature ไม่ถูกต้อง | ข้าม + Log error |
| 500 | Server error | ลองใหม่ 1 ครั้ง |
| Timeout (30s) | ไม่ตอบ | ส่งต่อเว็บถัดไป |

## Best Practices

1. **ใช้ HTTPS** - ต้องใช้ HTTPS เพื่อความปลอดภัย
2. **Unique Amount** - ใช้ระบบสตางค์เพื่อให้ยอดไม่ซ้ำ
3. **Order Expiry** - กำหนด expiry time ให้ orders (แนะนำ 24 ชม.)
4. **Exact Match** - ใช้ exact match เพราะยอดไม่ซ้ำ
5. **Idempotency** - ตรวจสอบ duplicate request ด้วย `X-Request-Id`
6. **Logging** - Log ทุก request สำหรับตรวจสอบภายหลัง

## Environment Variables

เพิ่มใน `.env`:

```env
# SMS Gateway Integration
SMS_GATEWAY_API_KEY=your_api_key_here
SMS_GATEWAY_SECRET_KEY=your_secret_key_here
SMS_GATEWAY_SIGNATURE_TOLERANCE=300
```

## Testing

### Test with cURL

```bash
# Generate test signature
TIMESTAMP=$(date +%s%3N)
PAYLOAD='{"event":"connection.test","timestamp":'$TIMESTAMP'}'
SIGNATURE=$(echo -n "${TIMESTAMP}.${PAYLOAD}" | openssl dgst -sha256 -hmac "your_secret_key" -binary | base64)

curl -X POST https://your-website.com/api/sms-gateway/webhook \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: your_api_key" \
  -H "X-Timestamp: ${TIMESTAMP}" \
  -H "X-Signature: ${SIGNATURE}" \
  -d "${PAYLOAD}"
```

## Support

หากพบปัญหาในการเชื่อมต่อ:

1. ตรวจสอบ API Key และ Secret Key ถูกต้อง
2. ตรวจสอบว่า webhook URL สามารถเข้าถึงได้จากภายนอก
3. ตรวจสอบ server time (ต้องตรงกับ NTP)
4. ดู logs ที่ทั้ง App และ Server

---

**Document Version:** 1.0.0
**Last Updated:** December 2024
**Compatible App Version:** 1.0.0+
