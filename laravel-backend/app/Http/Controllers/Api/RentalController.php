<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\RentalPackage;
use App\Models\UserRental;
use App\Models\Payment;
use App\Services\RentalService;
use App\Services\ThaiPaymentService;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Auth;

class RentalController extends Controller
{
    public function __construct(
        protected RentalService $rentalService,
        protected ThaiPaymentService $paymentService
    ) {}

    /**
     * Get all available rental packages
     * GET /api/v1/rentals/packages
     */
    public function packages(Request $request): JsonResponse
    {
        $includeTrial = $request->boolean('include_trial', true);
        $packages = $this->rentalService->getAvailablePackages($includeTrial);

        return response()->json([
            'success' => true,
            'data' => $packages,
        ]);
    }

    /**
     * Get single package details
     * GET /api/v1/rentals/packages/{id}
     */
    public function packageDetail(int $id): JsonResponse
    {
        $package = RentalPackage::active()->find($id);

        if (!$package) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่พบแพ็กเกจนี้',
            ], 404);
        }

        return response()->json([
            'success' => true,
            'data' => [
                'id' => $package->id,
                'name' => $package->name,
                'name_th' => $package->name_th,
                'description' => $package->description,
                'description_th' => $package->description_th,
                'duration_type' => $package->duration_type,
                'duration_value' => $package->duration_value,
                'duration_text' => $package->duration_text,
                'price' => $package->price,
                'original_price' => $package->original_price,
                'discount_percentage' => $package->discount_percentage,
                'formatted_price' => $package->getFormattedPrice(),
                'currency' => $package->currency,
                'limits' => $package->getUsageLimits(),
                'features' => $package->features,
                'included_platforms' => $package->included_platforms,
                'is_featured' => $package->is_featured,
                'is_popular' => $package->is_popular,
                'has_trial' => $package->has_trial,
            ],
        ]);
    }

    /**
     * Get current user's rental status
     * GET /api/v1/rentals/status
     */
    public function status(): JsonResponse
    {
        $user = Auth::user();
        $usageStatus = $this->rentalService->getUsageStatus($user);

        return response()->json([
            'success' => true,
            'data' => $usageStatus,
        ]);
    }

    /**
     * Get user's rental history
     * GET /api/v1/rentals/history
     */
    public function history(Request $request): JsonResponse
    {
        $user = Auth::user();
        $limit = $request->integer('limit', 10);
        $history = $this->rentalService->getUserRentalHistory($user, $limit);

        return response()->json([
            'success' => true,
            'data' => $history,
        ]);
    }

    /**
     * Create a new rental (start checkout)
     * POST /api/v1/rentals/checkout
     */
    public function checkout(Request $request): JsonResponse
    {
        $request->validate([
            'package_id' => 'required|integer|exists:rental_packages,id',
            'payment_method' => 'required|string|in:promptpay,bank_transfer,credit_card',
            'promo_code' => 'nullable|string|max:20',
        ]);

        $user = Auth::user();
        $package = RentalPackage::active()->findOrFail($request->package_id);

        // Create rental
        $result = $this->rentalService->createRental(
            $user,
            $package,
            $request->promo_code
        );

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'message' => $result['error'],
            ], 400);
        }

        // If free (trial), no payment needed
        if (!$result['requires_payment']) {
            return response()->json([
                'success' => true,
                'data' => [
                    'rental' => $this->formatRentalResponse($result['rental']),
                    'requires_payment' => false,
                ],
                'message' => $result['message'],
            ]);
        }

        // Generate payment info based on method
        $payment = $result['payment'];
        $paymentInfo = [];

        if ($request->payment_method === 'promptpay') {
            $payment->update(['payment_method' => Payment::METHOD_PROMPTPAY]);
            $qrResult = $this->paymentService->generatePromptPayQR(
                $result['amount'],
                $payment->payment_reference
            );
            $paymentInfo = [
                'qr_code' => $qrResult['qr_code'],
                'qr_image_url' => $qrResult['qr_image_url'] ?? null,
                'promptpay_number' => $qrResult['promptpay_number'],
                'amount' => $result['amount'],
                'reference' => $payment->payment_reference,
                'expires_in_minutes' => 30,
            ];
        } elseif ($request->payment_method === 'bank_transfer') {
            $payment->update(['payment_method' => Payment::METHOD_BANK_TRANSFER]);
            $bankInfo = $this->paymentService->getBankTransferInfo();
            $paymentInfo = [
                'bank_accounts' => $bankInfo,
                'amount' => $result['amount'],
                'reference' => $payment->payment_reference,
                'instructions' => 'กรุณาโอนเงินและแนบสลิปการโอน',
            ];
        } elseif ($request->payment_method === 'credit_card') {
            $payment->update(['payment_method' => Payment::METHOD_CREDIT_CARD]);
            // Create Omise/2C2P checkout URL
            $checkoutResult = $this->paymentService->createCardCheckout(
                $result['amount'],
                $payment->payment_reference,
                $user
            );
            $paymentInfo = [
                'checkout_url' => $checkoutResult['checkout_url'],
                'amount' => $result['amount'],
                'reference' => $payment->payment_reference,
            ];
        }

        return response()->json([
            'success' => true,
            'data' => [
                'rental' => $this->formatRentalResponse($result['rental']),
                'payment' => [
                    'id' => $payment->uuid,
                    'reference' => $payment->payment_reference,
                    'amount' => $result['amount'],
                    'original_amount' => $result['original_amount'],
                    'discount_amount' => $result['discount_amount'],
                    'currency' => $package->currency,
                    'method' => $request->payment_method,
                    'status' => $payment->status,
                ],
                'payment_info' => $paymentInfo,
                'requires_payment' => true,
            ],
        ]);
    }

    /**
     * Upload transfer slip for bank transfer
     * POST /api/v1/rentals/payments/{uuid}/upload-slip
     */
    public function uploadSlip(Request $request, string $uuid): JsonResponse
    {
        $request->validate([
            'slip' => 'required|image|max:5120', // 5MB max
        ]);

        $payment = Payment::where('uuid', $uuid)
            ->where('user_id', Auth::id())
            ->where('status', Payment::STATUS_PENDING)
            ->firstOrFail();

        // Store the slip
        $path = $request->file('slip')->store('payment-slips', 'public');
        $payment->update([
            'transfer_slip_url' => $path,
            'status' => Payment::STATUS_PROCESSING,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'อัพโหลดสลิปสำเร็จ รอตรวจสอบ',
            'data' => [
                'payment_reference' => $payment->payment_reference,
                'status' => $payment->status,
            ],
        ]);
    }

    /**
     * Check payment status
     * GET /api/v1/rentals/payments/{uuid}/status
     */
    public function paymentStatus(string $uuid): JsonResponse
    {
        $payment = Payment::where('uuid', $uuid)
            ->where('user_id', Auth::id())
            ->firstOrFail();

        return response()->json([
            'success' => true,
            'data' => [
                'reference' => $payment->payment_reference,
                'status' => $payment->status,
                'status_label' => $payment->getStatusLabel(),
                'amount' => $payment->amount,
                'currency' => $payment->currency,
                'method' => $payment->payment_method,
                'method_label' => $payment->getMethodLabel(),
                'paid_at' => $payment->paid_at?->toIso8601String(),
                'rental_active' => $payment->userRental?->is_active ?? false,
            ],
        ]);
    }

    /**
     * Validate promo code
     * POST /api/v1/rentals/validate-promo
     */
    public function validatePromo(Request $request): JsonResponse
    {
        $request->validate([
            'code' => 'required|string|max:20',
            'package_id' => 'nullable|integer|exists:rental_packages,id',
        ]);

        $result = $this->rentalService->validatePromoCode(
            $request->code,
            Auth::user(),
            $request->package_id
        );

        return response()->json([
            'success' => $result['valid'],
            'data' => $result['valid'] ? $result['promo'] : null,
            'message' => $result['message'],
        ]);
    }

    /**
     * Cancel a rental
     * POST /api/v1/rentals/{id}/cancel
     */
    public function cancel(Request $request, int $id): JsonResponse
    {
        $rental = UserRental::where('id', $id)
            ->where('user_id', Auth::id())
            ->firstOrFail();

        $result = $this->rentalService->cancelRental(
            $rental,
            $request->input('reason')
        );

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'message' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'message' => $result['message'],
        ]);
    }

    /**
     * Get user's invoices
     * GET /api/v1/rentals/invoices
     */
    public function invoices(Request $request): JsonResponse
    {
        $invoices = Auth::user()->invoices()
            ->orderBy('created_at', 'desc')
            ->limit($request->integer('limit', 20))
            ->get()
            ->map(function ($invoice) {
                return [
                    'id' => $invoice->id,
                    'invoice_number' => $invoice->invoice_number,
                    'type' => $invoice->type,
                    'type_label' => $invoice->getTypeLabel(),
                    'status' => $invoice->status,
                    'status_label' => $invoice->getStatusLabel(),
                    'total' => $invoice->total,
                    'currency' => $invoice->currency,
                    'issue_date' => $invoice->issue_date->toDateString(),
                    'pdf_url' => $invoice->pdf_url,
                ];
            });

        return response()->json([
            'success' => true,
            'data' => $invoices,
        ]);
    }

    /**
     * Request tax invoice
     * POST /api/v1/rentals/invoices/{id}/request-tax
     */
    public function requestTaxInvoice(Request $request, int $id): JsonResponse
    {
        $request->validate([
            'tax_id' => 'required|string|max:20',
            'company_name' => 'required|string|max:255',
            'company_address' => 'required|string|max:500',
            'branch_name' => 'nullable|string|max:100',
        ]);

        $payment = Payment::where('id', $id)
            ->where('user_id', Auth::id())
            ->where('status', Payment::STATUS_COMPLETED)
            ->firstOrFail();

        // Create or update tax invoice
        $invoice = $payment->invoice ?? \App\Models\Invoice::createFromPayment($payment, \App\Models\Invoice::TYPE_TAX_INVOICE);

        $invoice->update([
            'type' => \App\Models\Invoice::TYPE_TAX_INVOICE,
            'tax_id' => $request->tax_id,
            'company_name' => $request->company_name,
            'company_address' => $request->company_address,
            'branch_name' => $request->branch_name,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'ขอใบกำกับภาษีสำเร็จ',
            'data' => [
                'invoice_number' => $invoice->invoice_number,
            ],
        ]);
    }

    /**
     * Get supported payment methods
     * GET /api/v1/rentals/payment-methods
     */
    public function paymentMethods(): JsonResponse
    {
        $methods = $this->paymentService->getSupportedMethods();

        return response()->json([
            'success' => true,
            'data' => $methods,
        ]);
    }

    /**
     * Handle Omise webhook
     * POST /api/v1/webhooks/omise
     */
    public function omiseWebhook(Request $request): JsonResponse
    {
        $payload = $request->getContent();
        $signature = $request->header('Omise-Signature', '');

        if (!$this->paymentService->verifyOmiseWebhook($payload, $signature)) {
            return response()->json(['error' => 'Invalid signature'], 401);
        }

        $event = json_decode($payload, true);
        $result = $this->paymentService->handleOmiseWebhook($event);

        return response()->json($result);
    }

    /**
     * Confirm payment (for manual confirmation)
     * POST /api/v1/rentals/payments/{uuid}/confirm
     */
    public function confirmPayment(Request $request, string $uuid): JsonResponse
    {
        $payment = Payment::where('uuid', $uuid)
            ->where('user_id', Auth::id())
            ->where('status', Payment::STATUS_PENDING)
            ->firstOrFail();

        // For PromptPay/bank transfer, just update status to processing
        $payment->update(['status' => Payment::STATUS_PROCESSING]);

        return response()->json([
            'success' => true,
            'message' => 'รอตรวจสอบการชำระเงิน',
            'data' => [
                'status' => $payment->status,
            ],
        ]);
    }

    /**
     * Download invoice PDF
     * GET /api/v1/rentals/invoices/{id}/download
     */
    public function downloadInvoice(int $id): JsonResponse
    {
        $invoice = \App\Models\Invoice::where('id', $id)
            ->where('user_id', Auth::id())
            ->firstOrFail();

        // For now, return the PDF URL (actual PDF generation would use a package like DomPDF)
        return response()->json([
            'success' => true,
            'data' => [
                'invoice_number' => $invoice->invoice_number,
                'pdf_url' => $invoice->pdf_url,
                'download_url' => url("/api/v1/rentals/invoices/{$id}/pdf"),
            ],
        ]);
    }

    // ==================== Admin Methods ====================

    /**
     * Get all pending payments (admin)
     * GET /api/v1/admin/rentals/payments
     */
    public function adminPayments(Request $request): JsonResponse
    {
        $status = $request->input('status', 'pending');
        $perPage = $request->integer('per_page', 20);

        $payments = Payment::with(['user', 'userRental.rentalPackage'])
            ->when($status !== 'all', fn($q) => $q->where('status', $status))
            ->orderBy('created_at', 'desc')
            ->paginate($perPage);

        return response()->json([
            'success' => true,
            'data' => $payments->map(fn($p) => [
                'id' => $p->id,
                'uuid' => $p->uuid,
                'reference' => $p->payment_reference,
                'user' => [
                    'id' => $p->user->id,
                    'name' => $p->user->name,
                    'email' => $p->user->email,
                ],
                'package' => $p->userRental?->rentalPackage?->name,
                'amount' => $p->amount,
                'currency' => $p->currency,
                'method' => $p->payment_method,
                'method_label' => $p->getMethodLabel(),
                'status' => $p->status,
                'status_label' => $p->getStatusLabel(),
                'transfer_slip_url' => $p->transfer_slip_url,
                'created_at' => $p->created_at->toIso8601String(),
            ]),
            'meta' => [
                'current_page' => $payments->currentPage(),
                'last_page' => $payments->lastPage(),
                'per_page' => $payments->perPage(),
                'total' => $payments->total(),
            ],
        ]);
    }

    /**
     * Verify payment (admin)
     * POST /api/v1/admin/rentals/payments/{uuid}/verify
     */
    public function adminVerifyPayment(Request $request, string $uuid): JsonResponse
    {
        $payment = Payment::where('uuid', $uuid)
            ->whereIn('status', [Payment::STATUS_PENDING, Payment::STATUS_PROCESSING])
            ->firstOrFail();

        $result = $this->rentalService->verifyBankTransfer(
            $payment,
            Auth::id(),
            $payment->transfer_slip_url,
            $request->input('notes')
        );

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'message' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'message' => 'ยืนยันการชำระเงินสำเร็จ',
            'data' => [
                'payment_reference' => $payment->payment_reference,
                'rental_active' => true,
            ],
        ]);
    }

    /**
     * Reject payment (admin)
     * POST /api/v1/admin/rentals/payments/{uuid}/reject
     */
    public function adminRejectPayment(Request $request, string $uuid): JsonResponse
    {
        $request->validate([
            'reason' => 'required|string|max:500',
        ]);

        $payment = Payment::where('uuid', $uuid)
            ->whereIn('status', [Payment::STATUS_PENDING, Payment::STATUS_PROCESSING])
            ->firstOrFail();

        $payment->update([
            'status' => Payment::STATUS_FAILED,
            'failed_reason' => $request->reason,
        ]);

        // Also update rental status
        if ($payment->userRental) {
            $payment->userRental->update([
                'status' => UserRental::STATUS_CANCELLED,
                'notes' => 'Payment rejected: ' . $request->reason,
            ]);
        }

        return response()->json([
            'success' => true,
            'message' => 'ปฏิเสธการชำระเงินแล้ว',
        ]);
    }

    /**
     * Get rental statistics (admin)
     * GET /api/v1/admin/rentals/stats
     */
    public function adminStats(): JsonResponse
    {
        $today = now()->startOfDay();
        $thisMonth = now()->startOfMonth();

        $stats = [
            'payments' => [
                'pending' => Payment::where('status', Payment::STATUS_PENDING)->count(),
                'processing' => Payment::where('status', Payment::STATUS_PROCESSING)->count(),
                'completed_today' => Payment::where('status', Payment::STATUS_COMPLETED)
                    ->whereDate('paid_at', $today)
                    ->count(),
                'completed_this_month' => Payment::where('status', Payment::STATUS_COMPLETED)
                    ->where('paid_at', '>=', $thisMonth)
                    ->count(),
            ],
            'revenue' => [
                'today' => Payment::where('status', Payment::STATUS_COMPLETED)
                    ->whereDate('paid_at', $today)
                    ->sum('amount'),
                'this_month' => Payment::where('status', Payment::STATUS_COMPLETED)
                    ->where('paid_at', '>=', $thisMonth)
                    ->sum('amount'),
                'total' => Payment::where('status', Payment::STATUS_COMPLETED)
                    ->sum('amount'),
            ],
            'rentals' => [
                'active' => UserRental::where('status', UserRental::STATUS_ACTIVE)
                    ->where('expires_at', '>', now())
                    ->count(),
                'expired' => UserRental::where('status', UserRental::STATUS_EXPIRED)->count(),
                'expiring_soon' => UserRental::where('status', UserRental::STATUS_ACTIVE)
                    ->whereBetween('expires_at', [now(), now()->addDays(7)])
                    ->count(),
            ],
            'packages' => RentalPackage::active()
                ->withCount(['userRentals as total_rentals'])
                ->get()
                ->map(fn($p) => [
                    'id' => $p->id,
                    'name' => $p->name,
                    'total_rentals' => $p->total_rentals,
                ]),
        ];

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }

    /**
     * Format rental for response
     */
    protected function formatRentalResponse(UserRental $rental): array
    {
        return [
            'id' => $rental->id,
            'package_name' => $rental->rentalPackage->display_name,
            'starts_at' => $rental->starts_at?->toIso8601String(),
            'expires_at' => $rental->expires_at?->toIso8601String(),
            'status' => $rental->status,
            'status_label' => $rental->getStatusLabel(),
            'is_active' => $rental->is_active,
            'days_remaining' => $rental->days_remaining,
        ];
    }
}
