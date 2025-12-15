<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\AccountPool;
use App\Models\AccountPoolMember;
use App\Models\AccountStatusLog;
use App\Models\SocialAccount;
use App\Services\AccountRotationService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Validation\Rule;

class AccountPoolController extends Controller
{
    public function __construct(
        private AccountRotationService $rotationService
    ) {}

    /**
     * List all account pools for a brand
     */
    public function index(Request $request): JsonResponse
    {
        $request->validate([
            'brand_id' => 'required|exists:brands,id',
            'platform' => 'nullable|string',
        ]);

        $query = AccountPool::where('brand_id', $request->brand_id)
            ->with(['members.socialAccount'])
            ->withCount('members');

        if ($request->platform) {
            $query->where('platform', $request->platform);
        }

        $pools = $query->get()->map(function ($pool) {
            return array_merge($pool->toArray(), [
                'statistics' => $this->rotationService->getPoolStatistics($pool),
            ]);
        });

        return response()->json([
            'success' => true,
            'data' => $pools,
        ]);
    }

    /**
     * Create new account pool
     */
    public function store(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'brand_id' => 'required|exists:brands,id',
            'platform' => ['required', Rule::in(SocialAccount::getPlatforms())],
            'name' => 'required|string|max:255',
            'description' => 'nullable|string',
            'rotation_strategy' => ['nullable', Rule::in(AccountPool::getStrategies())],
            'cooldown_minutes' => 'nullable|integer|min:0|max:1440',
            'max_posts_per_day' => 'nullable|integer|min:1|max:100',
            'auto_failover' => 'nullable|boolean',
            'account_ids' => 'nullable|array',
            'account_ids.*' => 'exists:social_accounts,id',
        ]);

        // Check unique constraint
        $exists = AccountPool::where('brand_id', $validated['brand_id'])
            ->where('platform', $validated['platform'])
            ->where('name', $validated['name'])
            ->exists();

        if ($exists) {
            return response()->json([
                'success' => false,
                'error' => 'Pool with this name already exists for this brand and platform',
            ], 422);
        }

        $pool = DB::transaction(function () use ($validated) {
            $pool = AccountPool::create([
                'brand_id' => $validated['brand_id'],
                'platform' => $validated['platform'],
                'name' => $validated['name'],
                'description' => $validated['description'] ?? null,
                'rotation_strategy' => $validated['rotation_strategy'] ?? AccountPool::STRATEGY_ROUND_ROBIN,
                'cooldown_minutes' => $validated['cooldown_minutes'] ?? 30,
                'max_posts_per_day' => $validated['max_posts_per_day'] ?? 10,
                'auto_failover' => $validated['auto_failover'] ?? true,
            ]);

            // Add initial accounts if provided
            if (!empty($validated['account_ids'])) {
                foreach ($validated['account_ids'] as $index => $accountId) {
                    AccountPoolMember::create([
                        'account_pool_id' => $pool->id,
                        'social_account_id' => $accountId,
                        'priority' => $index,
                    ]);
                }
            }

            return $pool;
        });

        $pool->load('members.socialAccount');

        return response()->json([
            'success' => true,
            'data' => $pool,
            'message' => 'Account pool created successfully',
        ], 201);
    }

    /**
     * Get single pool details
     */
    public function show(AccountPool $accountPool): JsonResponse
    {
        $accountPool->load(['members.socialAccount', 'statusLogs' => function ($query) {
            $query->latest()->limit(50);
        }]);

        return response()->json([
            'success' => true,
            'data' => array_merge($accountPool->toArray(), [
                'statistics' => $this->rotationService->getPoolStatistics($accountPool),
            ]),
        ]);
    }

    /**
     * Update pool settings
     */
    public function update(Request $request, AccountPool $accountPool): JsonResponse
    {
        $validated = $request->validate([
            'name' => 'nullable|string|max:255',
            'description' => 'nullable|string',
            'rotation_strategy' => ['nullable', Rule::in(AccountPool::getStrategies())],
            'cooldown_minutes' => 'nullable|integer|min:0|max:1440',
            'max_posts_per_day' => 'nullable|integer|min:1|max:100',
            'auto_failover' => 'nullable|boolean',
            'is_active' => 'nullable|boolean',
        ]);

        $accountPool->update(array_filter($validated, fn($v) => $v !== null));

        return response()->json([
            'success' => true,
            'data' => $accountPool->fresh(['members.socialAccount']),
            'message' => 'Account pool updated successfully',
        ]);
    }

    /**
     * Delete pool
     */
    public function destroy(AccountPool $accountPool): JsonResponse
    {
        $accountPool->delete();

        return response()->json([
            'success' => true,
            'message' => 'Account pool deleted successfully',
        ]);
    }

    /**
     * Add account to pool
     */
    public function addAccount(Request $request, AccountPool $accountPool): JsonResponse
    {
        $validated = $request->validate([
            'social_account_id' => 'required|exists:social_accounts,id',
            'priority' => 'nullable|integer|min:0',
            'weight' => 'nullable|integer|min:1|max:1000',
        ]);

        // Verify account belongs to same platform
        $account = SocialAccount::findOrFail($validated['social_account_id']);
        if ($account->platform !== $accountPool->platform) {
            return response()->json([
                'success' => false,
                'error' => 'Account platform does not match pool platform',
            ], 422);
        }

        // Check if already in pool
        $exists = AccountPoolMember::where('account_pool_id', $accountPool->id)
            ->where('social_account_id', $validated['social_account_id'])
            ->exists();

        if ($exists) {
            return response()->json([
                'success' => false,
                'error' => 'Account already in this pool',
            ], 422);
        }

        $member = AccountPoolMember::create([
            'account_pool_id' => $accountPool->id,
            'social_account_id' => $validated['social_account_id'],
            'priority' => $validated['priority'] ?? $accountPool->members()->max('priority') + 1,
            'weight' => $validated['weight'] ?? 100,
        ]);

        $member->load('socialAccount');

        return response()->json([
            'success' => true,
            'data' => $member,
            'message' => 'Account added to pool successfully',
        ], 201);
    }

    /**
     * Remove account from pool
     */
    public function removeAccount(AccountPool $accountPool, int $accountId): JsonResponse
    {
        $deleted = AccountPoolMember::where('account_pool_id', $accountPool->id)
            ->where('social_account_id', $accountId)
            ->delete();

        if (!$deleted) {
            return response()->json([
                'success' => false,
                'error' => 'Account not found in this pool',
            ], 404);
        }

        return response()->json([
            'success' => true,
            'message' => 'Account removed from pool successfully',
        ]);
    }

    /**
     * Update account member settings
     */
    public function updateMember(Request $request, AccountPool $accountPool, int $accountId): JsonResponse
    {
        $validated = $request->validate([
            'priority' => 'nullable|integer|min:0',
            'weight' => 'nullable|integer|min:1|max:1000',
            'status' => ['nullable', Rule::in(AccountPoolMember::getStatuses())],
        ]);

        $member = AccountPoolMember::where('account_pool_id', $accountPool->id)
            ->where('social_account_id', $accountId)
            ->firstOrFail();

        $oldStatus = $member->status;
        $member->update(array_filter($validated, fn($v) => $v !== null));

        // Log status change if applicable
        if (isset($validated['status']) && $validated['status'] !== $oldStatus) {
            AccountStatusLog::logStatusChange(
                $accountId,
                $oldStatus,
                $validated['status'],
                'Manual status update via API',
                AccountStatusLog::TRIGGERED_BY_USER,
                $accountPool->id
            );
        }

        return response()->json([
            'success' => true,
            'data' => $member->fresh('socialAccount'),
            'message' => 'Account member updated successfully',
        ]);
    }

    /**
     * Recover account from banned/suspended status
     */
    public function recoverAccount(Request $request, AccountPool $accountPool, int $accountId): JsonResponse
    {
        $request->validate([
            'reason' => 'nullable|string|max:500',
        ]);

        $member = AccountPoolMember::where('account_pool_id', $accountPool->id)
            ->where('social_account_id', $accountId)
            ->firstOrFail();

        if ($member->status === AccountPoolMember::STATUS_ACTIVE) {
            return response()->json([
                'success' => false,
                'error' => 'Account is already active',
            ], 422);
        }

        $this->rotationService->recoverAccount(
            $member,
            $request->input('reason', 'Manual recovery via API')
        );

        return response()->json([
            'success' => true,
            'data' => $member->fresh('socialAccount'),
            'message' => 'Account recovered successfully',
        ]);
    }

    /**
     * Get pool statistics
     */
    public function statistics(AccountPool $accountPool): JsonResponse
    {
        return response()->json([
            'success' => true,
            'data' => $this->rotationService->getPoolStatistics($accountPool),
        ]);
    }

    /**
     * Get account status logs for pool
     */
    public function logs(Request $request, AccountPool $accountPool): JsonResponse
    {
        $request->validate([
            'hours' => 'nullable|integer|min:1|max:168', // max 1 week
            'event_type' => 'nullable|string',
            'account_id' => 'nullable|exists:social_accounts,id',
        ]);

        $query = AccountStatusLog::where('account_pool_id', $accountPool->id)
            ->with(['socialAccount', 'post'])
            ->latest();

        if ($request->hours) {
            $query->where('created_at', '>=', now()->subHours($request->hours));
        }

        if ($request->event_type) {
            $query->where('event_type', $request->event_type);
        }

        if ($request->account_id) {
            $query->where('social_account_id', $request->account_id);
        }

        $logs = $query->paginate(50);

        return response()->json([
            'success' => true,
            'data' => $logs,
        ]);
    }

    /**
     * Get health report for all pools
     */
    public function healthReport(Request $request): JsonResponse
    {
        $request->validate([
            'hours' => 'nullable|integer|min:1|max:168',
        ]);

        return response()->json([
            'success' => true,
            'data' => $this->rotationService->getAccountHealthReport(
                $request->input('hours', 24)
            ),
        ]);
    }

    /**
     * Reset daily post counters (admin only)
     */
    public function resetDailyCounters(): JsonResponse
    {
        $updated = $this->rotationService->resetDailyCounters();

        return response()->json([
            'success' => true,
            'message' => "Reset daily counters for {$updated} accounts",
        ]);
    }

    /**
     * Get next available account from pool (preview)
     */
    public function previewNextAccount(AccountPool $accountPool): JsonResponse
    {
        $member = $this->rotationService->getNextAccount($accountPool);

        if (!$member) {
            return response()->json([
                'success' => false,
                'error' => 'No available accounts in pool',
            ], 404);
        }

        return response()->json([
            'success' => true,
            'data' => [
                'member' => $member->load('socialAccount'),
                'available_count' => $this->rotationService->getAvailableAccounts($accountPool)->count(),
            ],
        ]);
    }
}
