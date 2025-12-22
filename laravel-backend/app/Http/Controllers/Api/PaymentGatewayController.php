<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\SmsPayment;
use App\Models\PaymentOrder;
use App\Models\MobileDevice;
use App\Services\AIManagerClient;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Str;

/**
 * Payment Gateway Controller
 *
 * ระบบ Payment Gateway ที่รับข้อมูลจาก SMS Notification แทนการใช้ Bank API
 * ทำงานร่วมกับ Mobile App ที่ติดตั้งบนมือถือเพื่อรับ SMS จากธนาคาร
 */
class PaymentGatewayController extends Controller
{
    public function __construct(
        private AIManagerClient $aiManager
    ) {}

    /**
     * Get payment gateway dashboard data
     */
    public function dashboard(): JsonResponse
    {
        $today = now()->startOfDay();

        $stats = [
            'pending_payments' => SmsPayment::where('status', 'pending')->count(),
            'approved_today' => SmsPayment::where('status', 'approved')
                ->whereDate('approved_at', $today)
                ->count(),
            'total_today' => SmsPayment::whereDate('created_at', $today)
                ->where('status', 'approved')
                ->sum('amount'),
            'pending_orders' => PaymentOrder::where('status', 'pending')->count(),
            'matched_orders' => PaymentOrder::where('status', 'matched')
                ->whereDate('matched_at', $today)
                ->count(),
        ];

        $recentPayments = SmsPayment::with('matchedOrder')
            ->orderByDesc('created_at')
            ->limit(20)
            ->get();

        $connectedDevices = MobileDevice::where('is_online', true)->get();

        return response()->json([
            'success' => true,
            'data' => [
                'stats' => $stats,
                'recent_payments' => $recentPayments,
                'connected_devices' => $connectedDevices,
            ],
        ]);
    }

    /**
     * Get all SMS payments with filters
     */
    public function payments(Request $request): JsonResponse
    {
        $query = SmsPayment::with(['matchedOrder', 'device']);

        // Status filter
        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        // Bank filter
        if ($request->has('bank')) {
            $query->where('bank_name', $request->bank);
        }

        // Date range filter
        if ($request->has('from_date')) {
            $query->whereDate('created_at', '>=', $request->from_date);
        }
        if ($request->has('to_date')) {
            $query->whereDate('created_at', '<=', $request->to_date);
        }

        // Amount range filter
        if ($request->has('min_amount')) {
            $query->where('amount', '>=', $request->min_amount);
        }
        if ($request->has('max_amount')) {
            $query->where('amount', '<=', $request->max_amount);
        }

        $payments = $query->orderByDesc('created_at')
            ->paginate($request->input('per_page', 20));

        return response()->json([
            'success' => true,
            'data' => $payments,
        ]);
    }

    /**
     * Get payment details
     */
    public function showPayment(string $id): JsonResponse
    {
        $payment = SmsPayment::with(['matchedOrder', 'device', 'approvedBy'])
            ->findOrFail($id);

        return response()->json([
            'success' => true,
            'data' => $payment,
        ]);
    }

    /**
     * Approve a pending payment
     */
    public function approvePayment(Request $request, string $id): JsonResponse
    {
        $payment = SmsPayment::findOrFail($id);

        if ($payment->status !== 'pending') {
            return response()->json([
                'success' => false,
                'message' => 'การชำระเงินนี้ไม่ได้อยู่ในสถานะรอดำเนินการ',
            ], 400);
        }

        DB::beginTransaction();
        try {
            $payment->update([
                'status' => 'approved',
                'approved_at' => now(),
                'approved_by' => $request->user()?->id,
                'approval_note' => $request->input('note'),
            ]);

            // If there's a matched order, update it
            if ($payment->order_id) {
                $order = PaymentOrder::find($payment->order_id);
                if ($order) {
                    $order->update([
                        'status' => 'paid',
                        'paid_at' => now(),
                    ]);

                    // Trigger callback if configured
                    if ($order->callback_url) {
                        $this->triggerPaymentCallback($order, $payment);
                    }
                }
            }

            DB::commit();

            // Notify via SignalR
            $this->notifyPaymentApproved($payment);

            return response()->json([
                'success' => true,
                'message' => 'อนุมัติการชำระเงินสำเร็จ',
                'data' => $payment->fresh(['matchedOrder']),
            ]);
        } catch (\Exception $e) {
            DB::rollBack();
            return response()->json([
                'success' => false,
                'message' => 'เกิดข้อผิดพลาด: ' . $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Reject a pending payment
     */
    public function rejectPayment(Request $request, string $id): JsonResponse
    {
        $request->validate([
            'reason' => 'required|string|max:500',
        ]);

        $payment = SmsPayment::findOrFail($id);

        if ($payment->status !== 'pending') {
            return response()->json([
                'success' => false,
                'message' => 'การชำระเงินนี้ไม่ได้อยู่ในสถานะรอดำเนินการ',
            ], 400);
        }

        $payment->update([
            'status' => 'rejected',
            'rejected_at' => now(),
            'rejected_by' => $request->user()?->id,
            'rejection_reason' => $request->input('reason'),
        ]);

        // Notify via SignalR
        $this->notifyPaymentRejected($payment);

        return response()->json([
            'success' => true,
            'message' => 'ปฏิเสธการชำระเงินแล้ว',
            'data' => $payment->fresh(),
        ]);
    }

    /**
     * Auto-match payment to pending orders
     *
     * ใช้ exact match เนื่องจากระบบ Unique Amount ทำให้ยอดไม่ซ้ำกัน
     */
    public function matchPayment(string $id): JsonResponse
    {
        $payment = SmsPayment::findOrFail($id);

        if ($payment->status !== 'pending' || $payment->order_id) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถจับคู่การชำระเงินนี้ได้',
            ], 400);
        }

        // ใช้ exact match เพราะระบบ Unique Amount ทำให้ยอดไม่ซ้ำกัน
        $order = PaymentOrder::where('status', 'pending')
            ->where('expires_at', '>', now())
            ->whereRaw('ABS(amount - ?) < 0.005', [$payment->amount])
            ->orderBy('created_at')
            ->first();

        if (!$order) {
            // ถ้าไม่เจอ exact match ลองหา order ที่ยอดใกล้เคียง
            $similarOrders = PaymentOrder::where('status', 'pending')
                ->where('expires_at', '>', now())
                ->whereBetween('amount', [$payment->amount - 1, $payment->amount + 1])
                ->orderBy('amount')
                ->limit(5)
                ->get();

            if ($similarOrders->isEmpty()) {
                return response()->json([
                    'success' => false,
                    'message' => 'ไม่พบ Order ที่ตรงกับจำนวนเงิน ฿' . number_format($payment->amount, 2),
                ], 404);
            }

            return response()->json([
                'success' => false,
                'message' => 'ไม่พบ Order ที่ยอดตรงกันพอดี แต่มี Order ที่ยอดใกล้เคียง',
                'similar_orders' => $similarOrders->map(fn($o) => [
                    'id' => $o->id,
                    'order_number' => $o->order_number,
                    'amount' => $o->amount,
                    'amount_formatted' => $o->amount_formatted,
                    'base_amount' => $o->base_amount,
                    'customer_name' => $o->customer_name,
                    'description' => $o->description,
                ]),
            ], 404);
        }

        $payment->update([
            'order_id' => $order->id,
        ]);

        $order->update([
            'status' => 'matched',
            'matched_at' => now(),
            'payment_id' => $payment->id,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'จับคู่ Order สำเร็จ (ยอดตรงกันพอดี)',
            'data' => [
                'payment' => $payment->fresh(),
                'order' => $order->fresh(),
            ],
        ]);
    }

    /**
     * Manually link payment to order
     */
    public function linkPaymentToOrder(Request $request, string $paymentId): JsonResponse
    {
        $request->validate([
            'order_id' => 'required|exists:payment_orders,id',
        ]);

        $payment = SmsPayment::findOrFail($paymentId);
        $order = PaymentOrder::findOrFail($request->order_id);

        if ($order->status === 'paid') {
            return response()->json([
                'success' => false,
                'message' => 'Order นี้ถูกชำระเงินแล้ว',
            ], 400);
        }

        DB::beginTransaction();
        try {
            $payment->update(['order_id' => $order->id]);
            $order->update([
                'status' => 'matched',
                'matched_at' => now(),
                'payment_id' => $payment->id,
            ]);

            DB::commit();

            return response()->json([
                'success' => true,
                'message' => 'เชื่อมโยง Payment กับ Order สำเร็จ',
                'data' => [
                    'payment' => $payment->fresh(),
                    'order' => $order->fresh(),
                ],
            ]);
        } catch (\Exception $e) {
            DB::rollBack();
            return response()->json([
                'success' => false,
                'message' => 'เกิดข้อผิดพลาด: ' . $e->getMessage(),
            ], 500);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // PAYMENT ORDERS
    // ═══════════════════════════════════════════════════════════════════════════════════

    /**
     * Get all payment orders
     */
    public function orders(Request $request): JsonResponse
    {
        $query = PaymentOrder::with('payment');

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        $orders = $query->orderByDesc('created_at')
            ->paginate($request->input('per_page', 20));

        return response()->json([
            'success' => true,
            'data' => $orders,
        ]);
    }

    /**
     * Create a new payment order
     *
     * ระบบจะเพิ่มสตางค์อัตโนมัติเพื่อให้ยอดไม่ซ้ำกัน
     * ทำให้สามารถจับคู่ SMS กับ Order ได้อย่างแม่นยำ
     */
    public function createOrder(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'amount' => 'required|numeric|min:1',
            'description' => 'required|string|max:500',
            'reference' => 'nullable|string|max:100',
            'customer_name' => 'nullable|string|max:255',
            'customer_email' => 'nullable|email|max:255',
            'customer_phone' => 'nullable|string|max:20',
            'callback_url' => 'nullable|url|max:500',
            'expires_in' => 'nullable|integer|min:5|max:1440', // minutes
            'metadata' => 'nullable|array',
        ]);

        // คำนวณยอดเงินที่ไม่ซ้ำกัน
        $uniqueAmountResult = $this->calculateUniqueAmount((float) $validated['amount']);

        $order = PaymentOrder::create([
            'order_number' => 'ORD-' . strtoupper(Str::random(10)),
            'base_amount' => $validated['amount'], // ยอดเดิม
            'amount' => $uniqueAmountResult['final_amount'], // ยอดที่ต้องชำระจริง
            'amount_suffix' => $uniqueAmountResult['suffix'], // สตางค์ที่เพิ่ม
            'description' => $validated['description'],
            'reference' => $validated['reference'] ?? null,
            'customer_name' => $validated['customer_name'] ?? null,
            'customer_email' => $validated['customer_email'] ?? null,
            'customer_phone' => $validated['customer_phone'] ?? null,
            'callback_url' => $validated['callback_url'] ?? null,
            'expires_at' => isset($validated['expires_in'])
                ? now()->addMinutes($validated['expires_in'])
                : now()->addHours(24),
            'metadata' => $validated['metadata'] ?? null,
            'status' => 'pending',
            'created_by' => $request->user()?->id,
        ]);

        // สร้างข้อความแจ้งเหตุผลการเพิ่มสตางค์
        $amountNotice = null;
        if ($uniqueAmountResult['suffix'] > 0) {
            $amountNotice = sprintf(
                'ยอดชำระถูกปรับเป็น ฿%s (เพิ่ม %.2f สตางค์) เพื่อให้ระบบจับคู่การชำระเงินได้อัตโนมัติ เนื่องจากมี %d รายการที่ยอดใกล้เคียงกันในขณะนี้',
                number_format($uniqueAmountResult['final_amount'], 2),
                $uniqueAmountResult['suffix'] * 100,
                $uniqueAmountResult['similar_orders_count']
            );
        }

        return response()->json([
            'success' => true,
            'message' => 'สร้าง Order สำเร็จ',
            'data' => $order,
            'amount_notice' => $amountNotice,
            'payment_instructions' => [
                'amount_to_pay' => $uniqueAmountResult['final_amount'],
                'amount_formatted' => '฿' . number_format($uniqueAmountResult['final_amount'], 2),
                'original_amount' => $validated['amount'],
                'added_satang' => $uniqueAmountResult['suffix'] * 100,
                'reason' => $uniqueAmountResult['suffix'] > 0
                    ? 'เพิ่มสตางค์เพื่อให้ยอดไม่ซ้ำกับ Order อื่น ระบบจะจับคู่การชำระเงินได้อัตโนมัติ'
                    : 'ยอดนี้ไม่ซ้ำกับ Order อื่น สามารถโอนได้เลย',
            ],
        ], 201);
    }

    /**
     * คำนวณยอดเงินที่ไม่ซ้ำกัน
     *
     * ระบบจะตรวจสอบ Order ที่มียอดใกล้เคียงกัน (ภายใน 1 บาท)
     * และเพิ่มสตางค์ตามลำดับเพื่อให้ยอดไม่ซ้ำกัน
     *
     * ตัวอย่าง:
     * - Order แรก 100 บาท -> 100.00 บาท
     * - Order ที่ 2 มา 100 บาท -> 100.01 บาท
     * - Order ที่ 3 มา 100 บาท -> 100.02 บาท
     */
    private function calculateUniqueAmount(float $baseAmount): array
    {
        // หา Order ที่ยังรอชำระและมียอดใกล้เคียงกัน (ภายใน 1 บาท)
        $similarOrders = PaymentOrder::where('status', 'pending')
            ->where('expires_at', '>', now()) // ยังไม่หมดอายุ
            ->whereBetween('base_amount', [$baseAmount - 0.50, $baseAmount + 0.50])
            ->orderBy('amount')
            ->get();

        if ($similarOrders->isEmpty()) {
            // ไม่มี Order ที่ยอดใกล้เคียง ใช้ยอดเดิมได้เลย
            return [
                'final_amount' => $baseAmount,
                'suffix' => 0.00,
                'similar_orders_count' => 0,
            ];
        }

        // หาสตางค์ที่ยังไม่ถูกใช้
        $usedSuffixes = $similarOrders->pluck('amount_suffix')->filter()->toArray();

        // เริ่มจาก 0.01, 0.02, ... ไปเรื่อยๆ (สูงสุด 99 สตางค์)
        $suffix = 0.00;
        for ($i = 1; $i <= 99; $i++) {
            $testSuffix = $i / 100; // 0.01, 0.02, ...
            if (!in_array($testSuffix, $usedSuffixes)) {
                $suffix = $testSuffix;
                break;
            }
        }

        // ถ้าใช้สตางค์หมดแล้ว (99 Order พร้อมกัน) ให้เพิ่ม 1 บาท
        if ($suffix === 0.00 && count($usedSuffixes) >= 99) {
            $suffix = 1.00 + (count($usedSuffixes) - 99) / 100;
        }

        return [
            'final_amount' => round($baseAmount + $suffix, 2),
            'suffix' => $suffix,
            'similar_orders_count' => $similarOrders->count(),
        ];
    }

    /**
     * Get order details
     */
    public function showOrder(string $id): JsonResponse
    {
        $order = PaymentOrder::with(['payment', 'createdBy'])->findOrFail($id);

        return response()->json([
            'success' => true,
            'data' => $order,
        ]);
    }

    /**
     * Cancel a payment order
     */
    public function cancelOrder(Request $request, string $id): JsonResponse
    {
        $order = PaymentOrder::findOrFail($id);

        if (!in_array($order->status, ['pending', 'matched'])) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถยกเลิก Order นี้ได้',
            ], 400);
        }

        $order->update([
            'status' => 'cancelled',
            'cancelled_at' => now(),
            'cancellation_reason' => $request->input('reason'),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'ยกเลิก Order สำเร็จ',
            'data' => $order->fresh(),
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // MOBILE DEVICES
    // ═══════════════════════════════════════════════════════════════════════════════════

    /**
     * Get connected mobile devices
     */
    public function devices(): JsonResponse
    {
        $devices = MobileDevice::orderByDesc('last_heartbeat')->get();

        return response()->json([
            'success' => true,
            'data' => $devices,
            'total' => $devices->count(),
            'online' => $devices->where('is_online', true)->count(),
        ]);
    }

    /**
     * Register a mobile device
     */
    public function registerDevice(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'device_id' => 'required|string|max:255',
            'device_name' => 'required|string|max:255',
            'platform' => 'required|string|in:Android,iOS',
            'app_version' => 'required|string|max:20',
            'sms_monitoring_enabled' => 'boolean',
            'auto_approve_enabled' => 'boolean',
        ]);

        $device = MobileDevice::updateOrCreate(
            ['device_id' => $validated['device_id']],
            [
                'device_name' => $validated['device_name'],
                'platform' => $validated['platform'],
                'app_version' => $validated['app_version'],
                'sms_monitoring_enabled' => $validated['sms_monitoring_enabled'] ?? false,
                'auto_approve_enabled' => $validated['auto_approve_enabled'] ?? false,
                'is_online' => true,
                'connected_at' => now(),
                'last_sync_at' => now(),
                'last_heartbeat' => now(),
            ]
        );

        return response()->json([
            'success' => true,
            'message' => 'อุปกรณ์ลงทะเบียนสำเร็จ',
            'data' => $device,
        ]);
    }

    /**
     * Device heartbeat
     */
    public function deviceHeartbeat(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'device_id' => 'required|string',
            'battery_level' => 'nullable|integer|min:0|max:100',
            'network_type' => 'nullable|string|max:50',
            'pending_payments' => 'nullable|integer|min:0',
            'total_payments_today' => 'nullable|numeric|min:0',
        ]);

        $device = MobileDevice::where('device_id', $validated['device_id'])->first();

        if (!$device) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่พบอุปกรณ์นี้',
            ], 404);
        }

        $device->update([
            'is_online' => true,
            'last_heartbeat' => now(),
            'battery_level' => $validated['battery_level'] ?? $device->battery_level,
            'network_type' => $validated['network_type'] ?? $device->network_type,
            'pending_payments' => $validated['pending_payments'] ?? $device->pending_payments,
            'total_payments_today' => $validated['total_payments_today'] ?? $device->total_payments_today,
        ]);

        return response()->json([
            'success' => true,
            'sync_status' => 'synced',
            'server_time' => now()->toISOString(),
        ]);
    }

    /**
     * Submit SMS payment from mobile device
     */
    public function submitSmsPayment(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'device_id' => 'required|string',
            'sms_sender' => 'required|string|max:50',
            'sms_body' => 'required|string',
            'sms_received_at' => 'required|date',
            'amount' => 'required|numeric|min:0.01',
            'bank_name' => 'required|string|max:100',
            'account_number' => 'nullable|string|max:50',
            'transaction_ref' => 'nullable|string|max:100',
            'confidence' => 'required|numeric|min:0|max:1',
        ]);

        // Check for duplicate
        $existing = SmsPayment::where('sms_body', $validated['sms_body'])
            ->where('device_id', $validated['device_id'])
            ->where('created_at', '>=', now()->subMinutes(5))
            ->first();

        if ($existing) {
            return response()->json([
                'success' => false,
                'message' => 'SMS นี้ถูกบันทึกแล้ว',
                'data' => $existing,
            ], 409);
        }

        // Check device
        $device = MobileDevice::where('device_id', $validated['device_id'])->first();

        // Determine if auto-approve
        $autoApprove = $device?->auto_approve_enabled && $validated['confidence'] >= 0.9;

        $payment = SmsPayment::create([
            'device_id' => $validated['device_id'],
            'sms_sender' => $validated['sms_sender'],
            'sms_body' => $validated['sms_body'],
            'sms_received_at' => $validated['sms_received_at'],
            'amount' => $validated['amount'],
            'bank_name' => $validated['bank_name'],
            'account_number' => $validated['account_number'] ?? null,
            'transaction_ref' => $validated['transaction_ref'] ?? null,
            'confidence' => $validated['confidence'],
            'status' => $autoApprove ? 'approved' : 'pending',
            'approved_at' => $autoApprove ? now() : null,
            'auto_approved' => $autoApprove,
        ]);

        // Try auto-match with pending orders
        if ($autoApprove) {
            $this->autoMatchPayment($payment);
        }

        // Notify via SignalR
        $this->notifyNewPayment($payment);

        return response()->json([
            'success' => true,
            'message' => $autoApprove ? 'ชำระเงินได้รับการอนุมัติอัตโนมัติ' : 'บันทึกการชำระเงินสำเร็จ',
            'data' => $payment,
            'auto_approved' => $autoApprove,
        ], 201);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // UNIVERSAL WEBHOOK ENDPOINT (Multi-Website SMS Gateway)
    // ═══════════════════════════════════════════════════════════════════════════════════

    /**
     * Universal SMS Webhook Endpoint
     *
     * Endpoint นี้รับ webhook จาก SMS Gateway Mobile App
     * ใช้สำหรับเชื่อมต่อหลายเว็บไซต์พร้อมกัน
     *
     * Security: API Key + Secret Key + HMAC-SHA256 Signature
     */
    public function smsWebhook(Request $request): JsonResponse
    {
        // 1. Validate signature
        $apiKey = $request->header('X-Api-Key');
        $timestamp = $request->header('X-Timestamp');
        $signature = $request->header('X-Signature');
        $requestId = $request->header('X-Request-Id');

        if (!$apiKey || !$timestamp || !$signature) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Missing authentication headers',
            ], 401);
        }

        // Verify API Key (ควรเก็บใน database หรือ config)
        $validApiKey = config('services.sms_gateway.api_key');
        $secretKey = config('services.sms_gateway.secret_key');

        if ($apiKey !== $validApiKey) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Invalid API key',
            ], 401);
        }

        // Verify timestamp (ไม่เกิน 5 นาที)
        $requestTime = (int) $timestamp;
        $now = (int) (microtime(true) * 1000);
        if (abs($now - $requestTime) > 300000) { // 5 minutes
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Request timestamp expired',
            ], 401);
        }

        // Verify HMAC signature
        $payload = $request->getContent();
        $expectedSignature = base64_encode(
            hash_hmac('sha256', "{$timestamp}.{$payload}", $secretKey, true)
        );

        if (!hash_equals($expectedSignature, $signature)) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Invalid signature',
            ], 401);
        }

        // 2. Check gateway status - is it ready to receive payments?
        $gatewayInfo = $request->input('gateway', []);
        $bankAccountsInfo = $request->input('bankAccounts', []);

        // If gateway is not ready, return early with message
        if (isset($bankAccountsInfo['isReady']) && $bankAccountsInfo['isReady'] === false) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'gateway_not_ready',
                'message' => $bankAccountsInfo['notReadyMessage'] ?? 'แอพ SMS Gateway ยังไม่พร้อม กรุณาใช้ช่องทางอื่น',
            ], 503);
        }

        // 3. Handle event
        $event = $request->input('event');

        if ($event === 'connection.test') {
            // Return bank accounts for website to cache/display
            return response()->json([
                'success' => true,
                'matched' => false,
                'message' => 'Connection successful',
                'server_time' => now()->toISOString(),
                'bank_accounts' => $bankAccountsInfo['accounts'] ?? [],
                'gateway_ready' => $bankAccountsInfo['isReady'] ?? false,
            ]);
        }

        if ($event !== 'payment.received') {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Unknown event type',
            ], 400);
        }

        // 4. Extract payment data
        $paymentData = $request->input('payment');

        if (!$paymentData || !isset($paymentData['amount'])) {
            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Invalid payment data',
            ], 400);
        }

        $amount = (float) $paymentData['amount'];

        // 4. Try to match with pending orders (Exact Match)
        $order = PaymentOrder::where('status', 'pending')
            ->where('expires_at', '>', now())
            ->whereRaw('ABS(amount - ?) < 0.005', [$amount])
            ->orderBy('created_at') // เลือก order ที่สร้างก่อน
            ->first();

        if (!$order) {
            // ไม่พบ order ที่ตรงกัน - ให้ mobile app ส่งต่อไปเว็บถัดไป
            return response()->json([
                'success' => true, // สำเร็จในแง่ที่รับ request ได้
                'matched' => false, // แต่ไม่มี order ตรง
                'message' => 'No matching order found for amount ' . number_format($amount, 2),
            ]);
        }

        // 5. บันทึก payment และ match กับ order
        DB::beginTransaction();
        try {
            $deviceInfo = $request->input('device', []);

            $payment = SmsPayment::create([
                'device_id' => $deviceInfo['deviceId'] ?? 'unknown',
                'sms_sender' => $paymentData['bankName'] ?? 'unknown',
                'sms_body' => $paymentData['rawSmsBody'] ?? '',
                'sms_received_at' => isset($paymentData['transactionTime'])
                    ? \Carbon\Carbon::parse($paymentData['transactionTime'])
                    : now(),
                'amount' => $amount,
                'bank_name' => $paymentData['bankName'] ?? 'Unknown',
                'account_number' => $paymentData['accountNumber'] ?? null,
                'transaction_ref' => $paymentData['reference'] ?? null,
                'confidence' => $paymentData['confidenceScore'] ?? 0.9,
                'status' => 'approved',
                'approved_at' => now(),
                'auto_approved' => true,
                'order_id' => $order->id,
                'webhook_request_id' => $requestId,
            ]);

            $order->update([
                'status' => 'paid',
                'matched_at' => now(),
                'paid_at' => now(),
                'payment_id' => $payment->id,
            ]);

            DB::commit();

            // Trigger callback if configured
            if ($order->callback_url) {
                $this->triggerPaymentCallback($order, $payment);
            }

            // Notify via SignalR
            $this->notifyNewPayment($payment);

            \Log::info("Webhook matched payment to order {$order->order_number} (amount: {$amount})");

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
        } catch (\Exception $e) {
            DB::rollBack();
            \Log::error("Webhook processing error: " . $e->getMessage());

            return response()->json([
                'success' => false,
                'matched' => false,
                'error' => 'Internal server error',
            ], 500);
        }
    }

    /**
     * Generate API credentials for SMS Gateway
     *
     * ใช้สำหรับสร้าง API Key และ Secret Key สำหรับเชื่อมต่อกับ Mobile App
     */
    public function generateApiCredentials(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:255',
            'description' => 'nullable|string|max:500',
        ]);

        // Generate secure keys
        $apiKey = 'sgw_' . bin2hex(random_bytes(16));
        $secretKey = bin2hex(random_bytes(32));

        // ในระบบจริงควรเก็บใน database
        // ตอนนี้แค่แสดงให้ copy ไปใช้

        return response()->json([
            'success' => true,
            'message' => 'API credentials generated successfully',
            'credentials' => [
                'name' => $validated['name'],
                'api_key' => $apiKey,
                'secret_key' => $secretKey,
                'webhook_url' => url('/api/v1/sms-gateway/webhook'),
            ],
            'instructions' => [
                'th' => 'คัดลอก API Key และ Secret Key ไปตั้งค่าในแอพมือถือ',
                'en' => 'Copy API Key and Secret Key to configure in the mobile app',
            ],
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // STATISTICS & REPORTS
    // ═══════════════════════════════════════════════════════════════════════════════════

    /**
     * Get payment statistics
     */
    public function statistics(Request $request): JsonResponse
    {
        $period = $request->input('period', 'today');

        $startDate = match($period) {
            'today' => now()->startOfDay(),
            'week' => now()->startOfWeek(),
            'month' => now()->startOfMonth(),
            'year' => now()->startOfYear(),
            default => now()->startOfDay(),
        };

        $stats = [
            'total_payments' => SmsPayment::where('created_at', '>=', $startDate)->count(),
            'approved_payments' => SmsPayment::where('status', 'approved')
                ->where('created_at', '>=', $startDate)
                ->count(),
            'pending_payments' => SmsPayment::where('status', 'pending')
                ->where('created_at', '>=', $startDate)
                ->count(),
            'rejected_payments' => SmsPayment::where('status', 'rejected')
                ->where('created_at', '>=', $startDate)
                ->count(),
            'total_amount' => SmsPayment::where('status', 'approved')
                ->where('created_at', '>=', $startDate)
                ->sum('amount'),
            'auto_approved_count' => SmsPayment::where('auto_approved', true)
                ->where('created_at', '>=', $startDate)
                ->count(),
        ];

        // By bank
        $byBank = SmsPayment::where('status', 'approved')
            ->where('created_at', '>=', $startDate)
            ->selectRaw('bank_name, COUNT(*) as count, SUM(amount) as total')
            ->groupBy('bank_name')
            ->get();

        // Daily trend (last 7 days)
        $dailyTrend = SmsPayment::where('status', 'approved')
            ->where('created_at', '>=', now()->subDays(7))
            ->selectRaw('DATE(created_at) as date, COUNT(*) as count, SUM(amount) as total')
            ->groupByRaw('DATE(created_at)')
            ->orderBy('date')
            ->get();

        return response()->json([
            'success' => true,
            'data' => [
                'summary' => $stats,
                'by_bank' => $byBank,
                'daily_trend' => $dailyTrend,
            ],
        ]);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // PRIVATE METHODS
    // ═══════════════════════════════════════════════════════════════════════════════════

    /**
     * Auto-match payment with order using exact amount matching
     *
     * เนื่องจากใช้ระบบ Unique Amount แล้ว การจับคู่จะใช้ exact match
     * ซึ่งมีความแม่นยำสูงมากเพราะยอดไม่มีทางซ้ำกัน
     */
    private function autoMatchPayment(SmsPayment $payment): void
    {
        // ใช้ exact match เพราะระบบ Unique Amount ทำให้ยอดไม่ซ้ำกัน
        // แต่เผื่อ tolerance เล็กน้อย (0.005) สำหรับ floating point precision
        $order = PaymentOrder::where('status', 'pending')
            ->where('expires_at', '>', now())
            ->whereRaw('ABS(amount - ?) < 0.005', [$payment->amount])
            ->orderBy('created_at')
            ->first();

        if ($order) {
            $payment->update(['order_id' => $order->id]);
            $order->update([
                'status' => 'paid',
                'matched_at' => now(),
                'paid_at' => now(),
                'payment_id' => $payment->id,
            ]);

            \Log::info("Auto-matched payment {$payment->id} to order {$order->id} (amount: {$payment->amount})");

            if ($order->callback_url) {
                $this->triggerPaymentCallback($order, $payment);
            }
        } else {
            \Log::info("No matching order found for payment {$payment->id} (amount: {$payment->amount})");
        }
    }

    private function triggerPaymentCallback(PaymentOrder $order, SmsPayment $payment): void
    {
        try {
            $client = new \GuzzleHttp\Client(['timeout' => 10]);
            $client->post($order->callback_url, [
                'json' => [
                    'event' => 'payment.completed',
                    'order' => $order->only(['id', 'order_number', 'amount', 'status']),
                    'payment' => $payment->only(['id', 'amount', 'bank_name', 'approved_at']),
                    'timestamp' => now()->toISOString(),
                ],
            ]);
        } catch (\Exception $e) {
            // Log but don't fail the main operation
            \Log::error("Payment callback failed for order {$order->id}: " . $e->getMessage());
        }
    }

    private function notifyNewPayment(SmsPayment $payment): void
    {
        try {
            $this->aiManager->sendSignalRMessage('PaymentReceived', [
                'payment' => $payment->toArray(),
            ]);
        } catch (\Exception $e) {
            // Silent fail - SignalR notification is not critical
        }
    }

    private function notifyPaymentApproved(SmsPayment $payment): void
    {
        try {
            $this->aiManager->sendSignalRMessage('PaymentApproved', [
                'payment' => $payment->toArray(),
            ]);
        } catch (\Exception $e) {
            // Silent fail
        }
    }

    private function notifyPaymentRejected(SmsPayment $payment): void
    {
        try {
            $this->aiManager->sendSignalRMessage('PaymentRejected', [
                'payment' => $payment->toArray(),
            ]);
        } catch (\Exception $e) {
            // Silent fail
        }
    }
}
