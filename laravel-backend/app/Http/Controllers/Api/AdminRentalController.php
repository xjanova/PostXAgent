<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\Payment;
use App\Models\RentalPackage;
use App\Models\User;
use App\Models\UserRental;
use App\Notifications\RentalActivatedNotification;
use App\Services\RentalService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

/**
 * Admin Controller for Rental Management
 * จัดการระบบเช่าสำหรับ Admin
 */
class AdminRentalController extends Controller
{
    public function __construct(
        protected RentalService $rentalService
    ) {}

    // ==================== User Rental Management ====================

    /**
     * Get all user rentals with filters
     * GET /api/v1/admin/rentals
     */
    public function index(Request $request): JsonResponse
    {
        $query = UserRental::with(['user', 'rentalPackage', 'payments']);

        // Filters
        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        if ($request->has('user_id')) {
            $query->where('user_id', $request->user_id);
        }

        if ($request->has('package_id')) {
            $query->where('rental_package_id', $request->package_id);
        }

        if ($request->boolean('expiring_soon')) {
            $query->where('status', UserRental::STATUS_ACTIVE)
                ->whereBetween('expires_at', [now(), now()->addDays(7)]);
        }

        if ($request->boolean('expired')) {
            $query->where('status', UserRental::STATUS_EXPIRED);
        }

        // Search
        if ($request->has('search')) {
            $search = $request->search;
            $query->whereHas('user', function ($q) use ($search) {
                $q->where('name', 'like', "%{$search}%")
                    ->orWhere('email', 'like', "%{$search}%");
            });
        }

        // Sort
        $sortBy = $request->input('sort_by', 'created_at');
        $sortDir = $request->input('sort_dir', 'desc');
        $query->orderBy($sortBy, $sortDir);

        $perPage = $request->integer('per_page', 20);
        $rentals = $query->paginate($perPage);

        return response()->json([
            'success' => true,
            'data' => $rentals->map(fn($r) => $this->formatRentalAdmin($r)),
            'meta' => [
                'current_page' => $rentals->currentPage(),
                'last_page' => $rentals->lastPage(),
                'per_page' => $rentals->perPage(),
                'total' => $rentals->total(),
            ],
        ]);
    }

    /**
     * Get single rental details
     * GET /api/v1/admin/rentals/{id}
     */
    public function show(int $id): JsonResponse
    {
        $rental = UserRental::with([
            'user',
            'rentalPackage',
            'payments',
            'usageLogs' => fn($q) => $q->latest()->limit(50),
        ])->findOrFail($id);

        return response()->json([
            'success' => true,
            'data' => [
                ...$this->formatRentalAdmin($rental),
                'usage_history' => $rental->usageLogs->map(fn($log) => [
                    'type' => $log->type,
                    'amount' => $log->amount,
                    'description' => $log->description,
                    'created_at' => $log->created_at->toIso8601String(),
                ]),
                'payments' => $rental->payments->map(fn($p) => [
                    'id' => $p->id,
                    'uuid' => $p->uuid,
                    'amount' => $p->amount,
                    'method' => $p->payment_method,
                    'status' => $p->status,
                    'paid_at' => $p->paid_at?->toIso8601String(),
                ]),
            ],
        ]);
    }

    /**
     * Update rental (manual adjustment)
     * PUT /api/v1/admin/rentals/{id}
     */
    public function update(Request $request, int $id): JsonResponse
    {
        $request->validate([
            'status' => 'nullable|string|in:pending,active,expired,cancelled,suspended',
            'starts_at' => 'nullable|date',
            'expires_at' => 'nullable|date|after:starts_at',
            'notes' => 'nullable|string|max:1000',
            'usage_posts' => 'nullable|integer|min:0',
            'usage_ai_generations' => 'nullable|integer|min:0',
        ]);

        $rental = UserRental::findOrFail($id);
        $oldStatus = $rental->status;

        $updateData = [];

        if ($request->has('status')) {
            $updateData['status'] = $request->status;
        }
        if ($request->has('starts_at')) {
            $updateData['starts_at'] = $request->starts_at;
        }
        if ($request->has('expires_at')) {
            $updateData['expires_at'] = $request->expires_at;
        }
        if ($request->has('notes')) {
            $updateData['notes'] = $request->notes;
        }
        if ($request->has('usage_posts')) {
            $updateData['usage_posts'] = $request->usage_posts;
        }
        if ($request->has('usage_ai_generations')) {
            $updateData['usage_ai_generations'] = $request->usage_ai_generations;
        }

        $rental->update($updateData);

        Log::info('Admin updated rental', [
            'rental_id' => $rental->id,
            'admin_id' => auth()->id(),
            'changes' => $updateData,
            'old_status' => $oldStatus,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'อัพเดทข้อมูลการเช่าสำเร็จ',
            'data' => $this->formatRentalAdmin($rental->fresh()),
        ]);
    }

    /**
     * Extend rental period
     * POST /api/v1/admin/rentals/{id}/extend
     */
    public function extend(Request $request, int $id): JsonResponse
    {
        $request->validate([
            'days' => 'required|integer|min:1|max:365',
            'reason' => 'required|string|max:500',
            'is_free' => 'boolean',
        ]);

        $rental = UserRental::findOrFail($id);

        if (!in_array($rental->status, [UserRental::STATUS_ACTIVE, UserRental::STATUS_EXPIRED])) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถขยายเวลาสำหรับ rental นี้ได้',
            ], 400);
        }

        DB::beginTransaction();
        try {
            $baseDate = $rental->expires_at > now() ? $rental->expires_at : now();
            $newExpiresAt = $baseDate->addDays($request->days);

            $rental->update([
                'expires_at' => $newExpiresAt,
                'status' => UserRental::STATUS_ACTIVE,
                'notes' => ($rental->notes ? $rental->notes . "\n" : '') .
                    "[" . now()->format('Y-m-d H:i') . "] Extended {$request->days} days by admin: {$request->reason}",
            ]);

            // Log extension
            $rental->usageLogs()->create([
                'type' => 'extension',
                'amount' => $request->days,
                'description' => "Extended by admin: {$request->reason}",
            ]);

            DB::commit();

            Log::info('Admin extended rental', [
                'rental_id' => $rental->id,
                'admin_id' => auth()->id(),
                'days' => $request->days,
                'new_expires_at' => $newExpiresAt,
                'reason' => $request->reason,
            ]);

            return response()->json([
                'success' => true,
                'message' => "ขยายเวลาสำเร็จ {$request->days} วัน",
                'data' => [
                    'new_expires_at' => $newExpiresAt->toIso8601String(),
                    'days_remaining' => $rental->fresh()->days_remaining,
                ],
            ]);
        } catch (\Exception $e) {
            DB::rollBack();
            Log::error('Failed to extend rental', [
                'rental_id' => $id,
                'error' => $e->getMessage(),
            ]);

            return response()->json([
                'success' => false,
                'message' => 'เกิดข้อผิดพลาด กรุณาลองใหม่',
            ], 500);
        }
    }

    /**
     * Create rental manually (gift/promotional)
     * POST /api/v1/admin/rentals/create-manual
     */
    public function createManual(Request $request): JsonResponse
    {
        $request->validate([
            'user_id' => 'required|integer|exists:users,id',
            'package_id' => 'required|integer|exists:rental_packages,id',
            'duration_days' => 'nullable|integer|min:1|max:365',
            'reason' => 'required|string|max:500',
            'skip_payment' => 'boolean',
        ]);

        $user = User::findOrFail($request->user_id);
        $package = RentalPackage::findOrFail($request->package_id);

        // Check existing active rental
        $existingActive = UserRental::where('user_id', $user->id)
            ->where('status', UserRental::STATUS_ACTIVE)
            ->where('expires_at', '>', now())
            ->exists();

        if ($existingActive && !$request->boolean('allow_multiple', false)) {
            return response()->json([
                'success' => false,
                'message' => 'ผู้ใช้มี rental ที่ active อยู่แล้ว',
            ], 400);
        }

        DB::beginTransaction();
        try {
            $durationDays = $request->duration_days ?? $package->getDurationInDays();

            $rental = UserRental::create([
                'user_id' => $user->id,
                'rental_package_id' => $package->id,
                'status' => UserRental::STATUS_ACTIVE,
                'starts_at' => now(),
                'expires_at' => now()->addDays($durationDays),
                'usage_posts' => 0,
                'usage_ai_generations' => 0,
                'usage_brands' => 0,
                'usage_platforms' => 0,
                'usage_team_members' => 0,
                'notes' => "Created manually by admin: {$request->reason}",
            ]);

            // Notify user
            $user->notify(new RentalActivatedNotification($rental));

            DB::commit();

            Log::info('Admin created manual rental', [
                'rental_id' => $rental->id,
                'admin_id' => auth()->id(),
                'user_id' => $user->id,
                'package_id' => $package->id,
                'reason' => $request->reason,
            ]);

            return response()->json([
                'success' => true,
                'message' => 'สร้าง rental สำเร็จ',
                'data' => $this->formatRentalAdmin($rental),
            ]);
        } catch (\Exception $e) {
            DB::rollBack();
            Log::error('Failed to create manual rental', [
                'error' => $e->getMessage(),
            ]);

            return response()->json([
                'success' => false,
                'message' => 'เกิดข้อผิดพลาด กรุณาลองใหม่',
            ], 500);
        }
    }

    /**
     * Suspend rental
     * POST /api/v1/admin/rentals/{id}/suspend
     */
    public function suspend(Request $request, int $id): JsonResponse
    {
        $request->validate([
            'reason' => 'required|string|max:500',
        ]);

        $rental = UserRental::findOrFail($id);

        if ($rental->status !== UserRental::STATUS_ACTIVE) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถระงับ rental ที่ไม่ได้ active',
            ], 400);
        }

        $rental->update([
            'status' => UserRental::STATUS_SUSPENDED,
            'notes' => ($rental->notes ? $rental->notes . "\n" : '') .
                "[" . now()->format('Y-m-d H:i') . "] Suspended by admin: {$request->reason}",
        ]);

        Log::info('Admin suspended rental', [
            'rental_id' => $rental->id,
            'admin_id' => auth()->id(),
            'reason' => $request->reason,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'ระงับการใช้งานสำเร็จ',
        ]);
    }

    /**
     * Reactivate suspended rental
     * POST /api/v1/admin/rentals/{id}/reactivate
     */
    public function reactivate(Request $request, int $id): JsonResponse
    {
        $rental = UserRental::findOrFail($id);

        if ($rental->status !== UserRental::STATUS_SUSPENDED) {
            return response()->json([
                'success' => false,
                'message' => 'Rental นี้ไม่ได้ถูกระงับ',
            ], 400);
        }

        // Check if expired during suspension
        if ($rental->expires_at < now()) {
            return response()->json([
                'success' => false,
                'message' => 'Rental หมดอายุแล้วระหว่างการระงับ กรุณาขยายเวลาแทน',
            ], 400);
        }

        $rental->update([
            'status' => UserRental::STATUS_ACTIVE,
            'notes' => ($rental->notes ? $rental->notes . "\n" : '') .
                "[" . now()->format('Y-m-d H:i') . "] Reactivated by admin",
        ]);

        Log::info('Admin reactivated rental', [
            'rental_id' => $rental->id,
            'admin_id' => auth()->id(),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'เปิดใช้งานอีกครั้งสำเร็จ',
        ]);
    }

    // ==================== Payment & Refund Management ====================

    /**
     * Process refund
     * POST /api/v1/admin/payments/{id}/refund
     */
    public function processRefund(Request $request, int $id): JsonResponse
    {
        $request->validate([
            'amount' => 'required|numeric|min:0',
            'reason' => 'required|string|max:500',
            'refund_method' => 'required|string|in:original,bank_transfer,credit',
            'bank_account' => 'required_if:refund_method,bank_transfer|nullable|string|max:50',
            'bank_name' => 'required_if:refund_method,bank_transfer|nullable|string|max:100',
        ]);

        $payment = Payment::where('status', Payment::STATUS_COMPLETED)
            ->findOrFail($id);

        if ($request->amount > $payment->amount) {
            return response()->json([
                'success' => false,
                'message' => 'จำนวนเงินคืนมากกว่ายอดชำระเดิม',
            ], 400);
        }

        // Check for existing refunds
        $existingRefund = Payment::where('original_payment_id', $payment->id)
            ->where('type', 'refund')
            ->sum('amount');

        $availableForRefund = $payment->amount - $existingRefund;

        if ($request->amount > $availableForRefund) {
            return response()->json([
                'success' => false,
                'message' => "ยอดที่สามารถคืนได้เหลือเพียง {$availableForRefund} บาท",
            ], 400);
        }

        DB::beginTransaction();
        try {
            // Create refund record
            $refund = Payment::create([
                'uuid' => \Illuminate\Support\Str::uuid(),
                'user_id' => $payment->user_id,
                'user_rental_id' => $payment->user_rental_id,
                'original_payment_id' => $payment->id,
                'type' => 'refund',
                'payment_reference' => 'REF-' . strtoupper(\Illuminate\Support\Str::random(8)),
                'amount' => -$request->amount, // Negative amount for refund
                'currency' => $payment->currency,
                'payment_method' => $request->refund_method,
                'status' => Payment::STATUS_PROCESSING,
                'notes' => $request->reason,
                'refund_details' => json_encode([
                    'original_payment_id' => $payment->id,
                    'bank_account' => $request->bank_account,
                    'bank_name' => $request->bank_name,
                    'processed_by' => auth()->id(),
                ]),
            ]);

            // If full refund, cancel the rental
            if ($request->amount >= $payment->amount && $payment->userRental) {
                $payment->userRental->update([
                    'status' => UserRental::STATUS_CANCELLED,
                    'notes' => ($payment->userRental->notes ? $payment->userRental->notes . "\n" : '') .
                        "[" . now()->format('Y-m-d H:i') . "] Cancelled due to full refund",
                ]);
            }

            DB::commit();

            Log::info('Admin processed refund', [
                'payment_id' => $payment->id,
                'refund_id' => $refund->id,
                'amount' => $request->amount,
                'admin_id' => auth()->id(),
            ]);

            return response()->json([
                'success' => true,
                'message' => 'สร้างรายการคืนเงินสำเร็จ',
                'data' => [
                    'refund_id' => $refund->id,
                    'refund_reference' => $refund->payment_reference,
                    'amount' => abs($refund->amount),
                    'status' => 'processing',
                ],
            ]);
        } catch (\Exception $e) {
            DB::rollBack();
            Log::error('Failed to process refund', [
                'payment_id' => $id,
                'error' => $e->getMessage(),
            ]);

            return response()->json([
                'success' => false,
                'message' => 'เกิดข้อผิดพลาด กรุณาลองใหม่',
            ], 500);
        }
    }

    /**
     * Mark refund as completed
     * POST /api/v1/admin/refunds/{id}/complete
     */
    public function completeRefund(Request $request, int $id): JsonResponse
    {
        $refund = Payment::where('type', 'refund')
            ->where('status', Payment::STATUS_PROCESSING)
            ->findOrFail($id);

        $refund->update([
            'status' => Payment::STATUS_COMPLETED,
            'paid_at' => now(),
            'notes' => ($refund->notes ? $refund->notes . "\n" : '') .
                "Completed by admin at " . now()->format('Y-m-d H:i'),
        ]);

        Log::info('Admin completed refund', [
            'refund_id' => $refund->id,
            'admin_id' => auth()->id(),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'ดำเนินการคืนเงินเสร็จสิ้น',
        ]);
    }

    // ==================== Package Management ====================

    /**
     * List all packages (including inactive)
     * GET /api/v1/admin/packages
     */
    public function listPackages(Request $request): JsonResponse
    {
        $query = RentalPackage::query();

        if ($request->has('is_active')) {
            $query->where('is_active', $request->boolean('is_active'));
        }

        $packages = $query->withCount(['userRentals', 'userRentals as active_rentals_count' => function ($q) {
            $q->where('status', UserRental::STATUS_ACTIVE)
                ->where('expires_at', '>', now());
        }])->orderBy('sort_order')->get();

        return response()->json([
            'success' => true,
            'data' => $packages->map(fn($p) => [
                'id' => $p->id,
                'name' => $p->name,
                'name_th' => $p->name_th,
                'description' => $p->description,
                'price' => $p->price,
                'original_price' => $p->original_price,
                'currency' => $p->currency,
                'duration_type' => $p->duration_type,
                'duration_value' => $p->duration_value,
                'limits' => $p->getUsageLimits(),
                'features' => $p->features,
                'is_active' => $p->is_active,
                'is_featured' => $p->is_featured,
                'is_popular' => $p->is_popular,
                'sort_order' => $p->sort_order,
                'total_rentals' => $p->user_rentals_count,
                'active_rentals' => $p->active_rentals_count,
            ]),
        ]);
    }

    /**
     * Create package
     * POST /api/v1/admin/packages
     */
    public function createPackage(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'required|string|max:100',
            'name_th' => 'required|string|max:100',
            'description' => 'nullable|string|max:500',
            'description_th' => 'nullable|string|max:500',
            'price' => 'required|numeric|min:0',
            'original_price' => 'nullable|numeric|min:0',
            'currency' => 'nullable|string|max:3',
            'duration_type' => 'required|string|in:days,weeks,months,years',
            'duration_value' => 'required|integer|min:1',
            'limit_posts' => 'nullable|integer|min:-1',
            'limit_ai_generations' => 'nullable|integer|min:-1',
            'limit_brands' => 'nullable|integer|min:-1',
            'limit_platforms' => 'nullable|integer|min:-1',
            'limit_team_members' => 'nullable|integer|min:-1',
            'features' => 'nullable|array',
            'included_platforms' => 'nullable|array',
            'is_active' => 'boolean',
            'is_featured' => 'boolean',
            'is_popular' => 'boolean',
            'sort_order' => 'nullable|integer',
        ]);

        $package = RentalPackage::create([
            ...$validated,
            'currency' => $validated['currency'] ?? 'THB',
            'is_active' => $validated['is_active'] ?? true,
            'is_featured' => $validated['is_featured'] ?? false,
            'is_popular' => $validated['is_popular'] ?? false,
        ]);

        Log::info('Admin created package', [
            'package_id' => $package->id,
            'admin_id' => auth()->id(),
        ]);

        return response()->json([
            'success' => true,
            'message' => 'สร้างแพ็กเกจสำเร็จ',
            'data' => [
                'id' => $package->id,
                'name' => $package->name,
            ],
        ], 201);
    }

    /**
     * Update package
     * PUT /api/v1/admin/packages/{id}
     */
    public function updatePackage(Request $request, int $id): JsonResponse
    {
        $package = RentalPackage::findOrFail($id);

        $validated = $request->validate([
            'name' => 'nullable|string|max:100',
            'name_th' => 'nullable|string|max:100',
            'description' => 'nullable|string|max:500',
            'description_th' => 'nullable|string|max:500',
            'price' => 'nullable|numeric|min:0',
            'original_price' => 'nullable|numeric|min:0',
            'currency' => 'nullable|string|max:3',
            'duration_type' => 'nullable|string|in:days,weeks,months,years',
            'duration_value' => 'nullable|integer|min:1',
            'limit_posts' => 'nullable|integer|min:-1',
            'limit_ai_generations' => 'nullable|integer|min:-1',
            'limit_brands' => 'nullable|integer|min:-1',
            'limit_platforms' => 'nullable|integer|min:-1',
            'limit_team_members' => 'nullable|integer|min:-1',
            'features' => 'nullable|array',
            'included_platforms' => 'nullable|array',
            'is_active' => 'nullable|boolean',
            'is_featured' => 'nullable|boolean',
            'is_popular' => 'nullable|boolean',
            'sort_order' => 'nullable|integer',
        ]);

        $package->update(array_filter($validated, fn($v) => $v !== null));

        Log::info('Admin updated package', [
            'package_id' => $package->id,
            'admin_id' => auth()->id(),
            'changes' => $validated,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'อัพเดทแพ็กเกจสำเร็จ',
        ]);
    }

    /**
     * Toggle package active status
     * POST /api/v1/admin/packages/{id}/toggle-active
     */
    public function togglePackageActive(int $id): JsonResponse
    {
        $package = RentalPackage::findOrFail($id);
        $package->update(['is_active' => !$package->is_active]);

        return response()->json([
            'success' => true,
            'message' => $package->is_active ? 'เปิดใช้งานแพ็กเกจแล้ว' : 'ปิดใช้งานแพ็กเกจแล้ว',
            'data' => ['is_active' => $package->is_active],
        ]);
    }

    // ==================== Analytics & Reports ====================

    /**
     * Revenue report
     * GET /api/v1/admin/reports/revenue
     */
    public function revenueReport(Request $request): JsonResponse
    {
        $startDate = $request->input('start_date')
            ? \Carbon\Carbon::parse($request->start_date)->startOfDay()
            : now()->startOfMonth();
        $endDate = $request->input('end_date')
            ? \Carbon\Carbon::parse($request->end_date)->endOfDay()
            : now()->endOfDay();
        $groupBy = $request->input('group_by', 'day'); // day, week, month

        $payments = Payment::where('status', Payment::STATUS_COMPLETED)
            ->where('type', '!=', 'refund')
            ->whereBetween('paid_at', [$startDate, $endDate])
            ->get();

        // Group by date
        $grouped = $payments->groupBy(function ($payment) use ($groupBy) {
            return match ($groupBy) {
                'week' => $payment->paid_at->startOfWeek()->format('Y-m-d'),
                'month' => $payment->paid_at->format('Y-m'),
                default => $payment->paid_at->format('Y-m-d'),
            };
        });

        $chartData = $grouped->map(fn($items, $date) => [
            'date' => $date,
            'revenue' => $items->sum('amount'),
            'count' => $items->count(),
        ])->values();

        // By payment method
        $byMethod = $payments->groupBy('payment_method')
            ->map(fn($items, $method) => [
                'method' => $method,
                'revenue' => $items->sum('amount'),
                'count' => $items->count(),
            ])->values();

        // By package
        $byPackage = $payments->groupBy(fn($p) => $p->userRental?->rentalPackage?->name ?? 'Unknown')
            ->map(fn($items, $package) => [
                'package' => $package,
                'revenue' => $items->sum('amount'),
                'count' => $items->count(),
            ])->values();

        // Refunds in period
        $refunds = Payment::where('type', 'refund')
            ->where('status', Payment::STATUS_COMPLETED)
            ->whereBetween('paid_at', [$startDate, $endDate])
            ->sum('amount');

        return response()->json([
            'success' => true,
            'data' => [
                'period' => [
                    'start' => $startDate->toDateString(),
                    'end' => $endDate->toDateString(),
                ],
                'summary' => [
                    'total_revenue' => $payments->sum('amount'),
                    'total_transactions' => $payments->count(),
                    'average_transaction' => $payments->avg('amount') ?? 0,
                    'total_refunds' => abs($refunds),
                    'net_revenue' => $payments->sum('amount') - abs($refunds),
                ],
                'chart_data' => $chartData,
                'by_method' => $byMethod,
                'by_package' => $byPackage,
            ],
        ]);
    }

    /**
     * User activity report
     * GET /api/v1/admin/reports/users
     */
    public function userReport(Request $request): JsonResponse
    {
        $limit = $request->integer('limit', 50);

        // Top users by spending
        $topSpenders = User::select('users.*')
            ->selectRaw('SUM(payments.amount) as total_spent')
            ->join('payments', 'users.id', '=', 'payments.user_id')
            ->where('payments.status', Payment::STATUS_COMPLETED)
            ->where('payments.type', '!=', 'refund')
            ->groupBy('users.id')
            ->orderByDesc('total_spent')
            ->limit($limit)
            ->get()
            ->map(fn($u) => [
                'id' => $u->id,
                'name' => $u->name,
                'email' => $u->email,
                'total_spent' => (float) $u->total_spent,
            ]);

        // New users this month
        $newUsersThisMonth = User::where('created_at', '>=', now()->startOfMonth())->count();

        // Users with active rentals
        $activeRentalUsers = UserRental::where('status', UserRental::STATUS_ACTIVE)
            ->where('expires_at', '>', now())
            ->distinct('user_id')
            ->count('user_id');

        // Churn (users with expired rentals not renewed in 30 days)
        $churnedUsers = User::whereHas('rentals', function ($q) {
            $q->where('status', UserRental::STATUS_EXPIRED)
                ->where('expires_at', '<', now()->subDays(30));
        })->whereDoesntHave('rentals', function ($q) {
            $q->where('status', UserRental::STATUS_ACTIVE);
        })->count();

        return response()->json([
            'success' => true,
            'data' => [
                'summary' => [
                    'new_users_this_month' => $newUsersThisMonth,
                    'users_with_active_rentals' => $activeRentalUsers,
                    'churned_users' => $churnedUsers,
                ],
                'top_spenders' => $topSpenders,
            ],
        ]);
    }

    /**
     * Dashboard overview
     * GET /api/v1/admin/reports/dashboard
     */
    public function dashboard(): JsonResponse
    {
        $today = now()->startOfDay();
        $thisWeek = now()->startOfWeek();
        $thisMonth = now()->startOfMonth();

        return response()->json([
            'success' => true,
            'data' => [
                'rentals' => [
                    'active' => UserRental::where('status', UserRental::STATUS_ACTIVE)
                        ->where('expires_at', '>', now())->count(),
                    'expiring_7_days' => UserRental::where('status', UserRental::STATUS_ACTIVE)
                        ->whereBetween('expires_at', [now(), now()->addDays(7)])->count(),
                    'new_today' => UserRental::whereDate('created_at', $today)->count(),
                    'new_this_week' => UserRental::where('created_at', '>=', $thisWeek)->count(),
                    'new_this_month' => UserRental::where('created_at', '>=', $thisMonth)->count(),
                ],
                'payments' => [
                    'pending' => Payment::where('status', Payment::STATUS_PENDING)->count(),
                    'processing' => Payment::where('status', Payment::STATUS_PROCESSING)->count(),
                    'revenue_today' => Payment::where('status', Payment::STATUS_COMPLETED)
                        ->whereDate('paid_at', $today)->sum('amount'),
                    'revenue_this_week' => Payment::where('status', Payment::STATUS_COMPLETED)
                        ->where('paid_at', '>=', $thisWeek)->sum('amount'),
                    'revenue_this_month' => Payment::where('status', Payment::STATUS_COMPLETED)
                        ->where('paid_at', '>=', $thisMonth)->sum('amount'),
                ],
                'users' => [
                    'total' => User::count(),
                    'new_today' => User::whereDate('created_at', $today)->count(),
                    'new_this_month' => User::where('created_at', '>=', $thisMonth)->count(),
                ],
            ],
        ]);
    }

    // ==================== Helpers ====================

    protected function formatRentalAdmin(UserRental $rental): array
    {
        return [
            'id' => $rental->id,
            'user' => [
                'id' => $rental->user->id,
                'name' => $rental->user->name,
                'email' => $rental->user->email,
            ],
            'package' => [
                'id' => $rental->rentalPackage->id,
                'name' => $rental->rentalPackage->name,
                'name_th' => $rental->rentalPackage->name_th,
                'price' => $rental->rentalPackage->price,
            ],
            'status' => $rental->status,
            'starts_at' => $rental->starts_at?->toIso8601String(),
            'expires_at' => $rental->expires_at?->toIso8601String(),
            'days_remaining' => $rental->days_remaining,
            'is_active' => $rental->is_active,
            'usage' => [
                'posts' => $rental->usage_posts,
                'ai_generations' => $rental->usage_ai_generations,
                'brands' => $rental->usage_brands,
                'platforms' => $rental->usage_platforms,
                'team_members' => $rental->usage_team_members,
            ],
            'limits' => $rental->rentalPackage->getUsageLimits(),
            'notes' => $rental->notes,
            'created_at' => $rental->created_at->toIso8601String(),
        ];
    }
}
