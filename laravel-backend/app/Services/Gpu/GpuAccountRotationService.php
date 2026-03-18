<?php

declare(strict_types=1);

namespace App\Services\Gpu;

use App\Models\GpuAccountPool;
use App\Models\GpuPoolMember;
use App\Models\GpuProviderAccount;
use App\Models\GpuActivityLog;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

/**
 * Service for rotating GPU provider accounts within pools
 */
class GpuAccountRotationService
{
    private const CACHE_ROUND_ROBIN_INDEX = 'gpu_rotation:rr_index:';

    public function __construct(
        private GpuProviderFactory $providerFactory
    ) {}

    /**
     * Get next available account from pool based on rotation strategy
     */
    public function getNextAccount(GpuAccountPool $pool): ?GpuPoolMember
    {
        $availableAccounts = $this->getAvailableAccounts($pool);

        if ($availableAccounts->isEmpty()) {
            Log::warning('No available GPU accounts in pool', [
                'pool_id' => $pool->id,
                'provider' => $pool->provider,
            ]);
            return null;
        }

        return match ($pool->rotation_strategy) {
            GpuAccountPool::STRATEGY_ROUND_ROBIN => $this->selectRoundRobin($pool, $availableAccounts),
            GpuAccountPool::STRATEGY_RANDOM => $this->selectRandom($availableAccounts),
            GpuAccountPool::STRATEGY_LEAST_USED => $this->selectLeastUsed($availableAccounts),
            GpuAccountPool::STRATEGY_MOST_CREDITS => $this->selectMostCredits($availableAccounts),
            GpuAccountPool::STRATEGY_PRIORITY => $this->selectByPriority($availableAccounts),
            default => $availableAccounts->first(),
        };
    }

    /**
     * Get accounts with failover support
     */
    public function getAccountsWithFailover(GpuAccountPool $pool, int $maxAccounts = 3): Collection
    {
        $availableAccounts = $this->getAvailableAccounts($pool);

        if ($availableAccounts->isEmpty()) {
            return collect();
        }

        $sorted = match ($pool->rotation_strategy) {
            GpuAccountPool::STRATEGY_PRIORITY => $availableAccounts->sortBy('priority'),
            GpuAccountPool::STRATEGY_LEAST_USED => $availableAccounts->sortBy('instances_today'),
            GpuAccountPool::STRATEGY_MOST_CREDITS => $availableAccounts->sortByDesc(fn (GpuPoolMember $m): float => (float) ($m->providerAccount->credits_balance ?? 0)),
            default => $availableAccounts->shuffle(),
        };

        return $sorted->take($maxAccounts)->values();
    }

    /**
     * Get all available accounts from a pool
     */
    public function getAvailableAccounts(GpuAccountPool $pool): Collection
    {
        $this->checkExpiredCooldowns($pool);

        return $pool->members()
            ->with(['providerAccount' => function ($query) {
                $query->where('is_active', true);
            }])
            ->where('status', GpuPoolMember::STATUS_ACTIVE)
            ->where(function ($query) {
                $query->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            })
            ->get()
            /** @phpstan-ignore argument.type */
            ->filter(function (GpuPoolMember $member) use ($pool): bool {
                /** @var GpuProviderAccount|null $account */
                $account = $member->providerAccount;
                if (!$account) {
                    return false;
                }
                return $account->credits_balance >= $pool->min_credits_required;
            });
    }

    /**
     * Record successful instance creation
     */
    public function recordSuccess(
        GpuPoolMember $member,
        float $creditsUsed = 0,
        ?array $metadata = null
    ): void {
        DB::transaction(function () use ($member, $creditsUsed) {
            $member->recordSuccess();

            if ($creditsUsed > 0) {
                $member->recordCreditsUsed($creditsUsed);
                $member->providerAccount->recordUsage($creditsUsed);
            }

            // Start cooldown if configured
            $pool = $member->accountPool;
            if ($pool && $pool->cooldown_minutes > 0) {
                $member->startCooldown($pool->cooldown_minutes);
            }
        });

        Log::info('GPU account usage recorded', [
            'account_id' => $member->gpu_provider_account_id,
            'pool_id' => $member->gpu_account_pool_id,
            'credits_used' => $creditsUsed,
        ]);
    }

    /**
     * Record failed operation and handle failover
     */
    public function recordFailure(
        GpuPoolMember $member,
        string $errorMessage,
        ?array $metadata = null
    ): ?GpuPoolMember {
        DB::transaction(function () use ($member, $errorMessage, $metadata) {
            $member->recordFailure($errorMessage);

            GpuActivityLog::logApiError(
                $member->providerAccount,
                $errorMessage,
                $metadata
            );

            $this->checkAndSuspendAccount($member, $errorMessage);
        });

        Log::warning('GPU account failure recorded', [
            'account_id' => $member->gpu_provider_account_id,
            'pool_id' => $member->gpu_account_pool_id,
            'error' => $errorMessage,
            'consecutive_failures' => $member->consecutive_failures,
        ]);

        $pool = $member->accountPool;
        if ($pool?->auto_failover) {
            $nextMember = $this->getNextAccountExcluding($pool, [$member->id]);

            if ($nextMember) {
                GpuActivityLog::logFailoverTriggered(
                    $pool,
                    $member->providerAccount,
                    $nextMember->providerAccount,
                    $errorMessage
                );
            }

            return $nextMember;
        }

        return null;
    }

    /**
     * Handle depleted credits
     */
    public function handleDepleted(GpuPoolMember $member): ?GpuPoolMember
    {
        DB::transaction(function () use ($member) {
            $member->markAsDepleted();
            $member->providerAccount->markAsDepleted();

            GpuActivityLog::logBalanceDepleted($member->providerAccount);
        });

        Log::warning('GPU account depleted', [
            'account_id' => $member->gpu_provider_account_id,
        ]);

        $pool = $member->accountPool;
        if ($pool?->auto_failover) {
            return $this->getNextAccountExcluding($pool, [$member->id]);
        }

        return null;
    }

    /**
     * Refresh balance for all accounts in a pool
     */
    public function refreshPoolBalances(GpuAccountPool $pool): array
    {
        $results = [];

        /** @var GpuPoolMember $member */
        foreach ($pool->members()->with('providerAccount')->get() as $member) {
            /** @var GpuProviderAccount|null $account */
            $account = $member->providerAccount;
            if (!$account) {
                continue;
            }

            try {
                $provider = $this->providerFactory->getProviderForAccount($account);
                $oldBalance = $account->credits_balance;
                $newBalance = $provider->getBalance($account);

                $account->updateBalance($newBalance);

                GpuActivityLog::logBalanceCheck($account, $oldBalance, $newBalance);

                // Check for low balance
                if ($account->isLowBalance()) {
                    GpuActivityLog::logBalanceLow($account);
                }

                // Update member status based on balance
                if ($newBalance < $pool->min_credits_required) {
                    $member->markAsDepleted();
                } elseif ($member->status === GpuPoolMember::STATUS_DEPLETED) {
                    $member->reactivate();
                }

                $results[$account->id] = [
                    'success' => true,
                    'old_balance' => $oldBalance,
                    'new_balance' => $newBalance,
                ];
            } catch (\Exception $e) {
                Log::error('Failed to refresh balance', [
                    'account_id' => $account->id,
                    'error' => $e->getMessage(),
                ]);
                $results[$account->id] = [
                    'success' => false,
                    'error' => $e->getMessage(),
                ];
            }
        }

        return $results;
    }

    /**
     * Reset daily counters for all pool members
     */
    public function resetDailyCounters(): int
    {
        $updated = GpuPoolMember::query()->update([
            'instances_today' => 0,
            'credits_used_today' => 0,
        ]);

        GpuProviderAccount::query()->update([
            'credits_used_today' => 0,
        ]);

        Log::info('GPU daily counters reset', ['accounts_updated' => $updated]);

        return $updated;
    }

    /**
     * Get pool statistics
     */
    public function getPoolStatistics(GpuAccountPool $pool): array
    {
        $members = $pool->members()->with('providerAccount')->get();
        $statusCounts = $members->groupBy('status')->map(fn ($items) => $items->count());

        return [
            'total_accounts' => $members->count(),
            'active' => $statusCounts->get(GpuPoolMember::STATUS_ACTIVE, 0),
            'cooldown' => $statusCounts->get(GpuPoolMember::STATUS_COOLDOWN, 0),
            'suspended' => $statusCounts->get(GpuPoolMember::STATUS_SUSPENDED, 0),
            'depleted' => $statusCounts->get(GpuPoolMember::STATUS_DEPLETED, 0),
            'error' => $statusCounts->get(GpuPoolMember::STATUS_ERROR, 0),
            /** @phpstan-ignore argument.type */
            'total_credits' => $members->sum(fn (GpuPoolMember $m): float => (float) ($m->providerAccount->credits_balance ?? 0)),
            'instances_today' => $members->sum('instances_today'),
            'total_instances' => $members->sum('total_instances'),
            'credits_used_today' => $members->sum('credits_used_today'),
            'success_rate' => $this->calculatePoolSuccessRate($members),
            'available_now' => $this->getAvailableAccounts($pool)->count(),
        ];
    }

    /**
     * Get health report for GPU accounts
     */
    public function getHealthReport(int $hours = 24): array
    {
        $logs = GpuActivityLog::recent($hours)
            ->with(['providerAccount', 'accountPool'])
            ->get();

        return [
            'period_hours' => $hours,
            'total_events' => $logs->count(),
            'instances_created' => $logs->where('event_type', GpuActivityLog::EVENT_INSTANCE_CREATED)->count(),
            'instances_terminated' => $logs->where('event_type', GpuActivityLog::EVENT_INSTANCE_TERMINATED)->count(),
            'api_errors' => $logs->where('event_type', GpuActivityLog::EVENT_API_ERROR)->count(),
            'balance_depletions' => $logs->where('event_type', GpuActivityLog::EVENT_BALANCE_DEPLETED)->count(),
            'failovers' => $logs->where('event_type', GpuActivityLog::EVENT_FAILOVER_TRIGGERED)->count(),
            'by_provider' => collect($logs->toArray())->groupBy('provider')
                ->map(fn ($group) => [
                    'total' => $group->count(),
                    'errors' => $group->filter(fn ($l) => in_array($l['event_type'] ?? '', [
                        GpuActivityLog::EVENT_API_ERROR,
                        GpuActivityLog::EVENT_INSTANCE_ERROR,
                        GpuActivityLog::EVENT_SETUP_FAILED,
                    ]))->count(),
                ]),
        ];
    }

    /**
     * Recover an account from suspended/error status
     */
    public function recoverAccount(GpuPoolMember $member, string $reason = 'Manual recovery'): void
    {
        $oldStatus = $member->status;

        DB::transaction(function () use ($member, $oldStatus, $reason) {
            $member->reactivate();

            if ($member->providerAccount->status !== GpuProviderAccount::STATUS_ACTIVE) {
                $member->providerAccount->update(['status' => GpuProviderAccount::STATUS_ACTIVE]);
            }

            GpuActivityLog::create([
                'gpu_provider_account_id' => $member->gpu_provider_account_id,
                'gpu_account_pool_id' => $member->gpu_account_pool_id,
                'user_id' => $member->providerAccount->user_id,
                'provider' => $member->providerAccount->provider,
                'event_type' => GpuActivityLog::EVENT_ACCOUNT_RECOVERED,
                'old_status' => $oldStatus,
                'new_status' => GpuPoolMember::STATUS_ACTIVE,
                'message' => $reason,
                'triggered_by' => GpuActivityLog::TRIGGERED_BY_USER,
            ]);
        });

        Log::info('GPU account recovered', [
            'account_id' => $member->gpu_provider_account_id,
            'old_status' => $oldStatus,
        ]);
    }

    // Private helper methods

    private function selectRoundRobin(GpuAccountPool $pool, Collection $accounts): GpuPoolMember
    {
        $cacheKey = self::CACHE_ROUND_ROBIN_INDEX . $pool->id;
        $lastIndex = Cache::get($cacheKey, -1);
        $nextIndex = ($lastIndex + 1) % $accounts->count();

        Cache::put($cacheKey, $nextIndex, now()->addDay());

        return $accounts->values()[$nextIndex];
    }

    private function selectRandom(Collection $accounts): GpuPoolMember
    {
        $totalWeight = $accounts->sum('weight');
        $random = random_int(0, max(1, $totalWeight));
        $cumulative = 0;

        foreach ($accounts as $account) {
            $cumulative += $account->weight;
            if ($random <= $cumulative) {
                return $account;
            }
        }

        return $accounts->last();
    }

    private function selectLeastUsed(Collection $accounts): GpuPoolMember
    {
        return $accounts->sortBy('instances_today')->sortBy('total_instances')->first();
    }

    private function selectMostCredits(Collection $accounts): GpuPoolMember
    {
        return $accounts->sortByDesc(fn ($m) => $m->providerAccount->credits_balance ?? 0)->first();
    }

    private function selectByPriority(Collection $accounts): GpuPoolMember
    {
        return $accounts->sortBy('priority')->first();
    }

    /** @phpstan-ignore method.unused */
    private function updateRoundRobinIndex(GpuAccountPool $pool): void
    {
        $cacheKey = self::CACHE_ROUND_ROBIN_INDEX . $pool->id;
        $currentIndex = Cache::get($cacheKey, 0);
        $memberCount = $pool->members()->count();

        if ($memberCount > 0) {
            Cache::put($cacheKey, ($currentIndex + 1) % $memberCount, now()->addDay());
        }
    }

    private function checkExpiredCooldowns(GpuAccountPool $pool): void
    {
        $pool->members()
            ->where('status', GpuPoolMember::STATUS_COOLDOWN)
            ->where('cooldown_until', '<=', now())
            /** @phpstan-ignore argument.type */
            ->each(function (GpuPoolMember $member): void {
                $member->endCooldown();
            });
    }

    private function checkAndSuspendAccount(GpuPoolMember $member, string $errorMessage): void
    {
        // Suspend if 5 consecutive failures
        if ($member->consecutive_failures >= 5) {
            $member->markAsSuspended("Too many consecutive failures: {$errorMessage}");

            GpuActivityLog::create([
                'gpu_provider_account_id' => $member->gpu_provider_account_id,
                'gpu_account_pool_id' => $member->gpu_account_pool_id,
                'user_id' => $member->providerAccount->user_id,
                'provider' => $member->providerAccount->provider,
                'event_type' => GpuActivityLog::EVENT_ACCOUNT_SUSPENDED,
                'old_status' => GpuPoolMember::STATUS_ACTIVE,
                'new_status' => GpuPoolMember::STATUS_SUSPENDED,
                'message' => "Auto-suspended after {$member->consecutive_failures} consecutive failures",
                'triggered_by' => GpuActivityLog::TRIGGERED_BY_SYSTEM,
            ]);
        }
    }

    private function getNextAccountExcluding(GpuAccountPool $pool, array $excludeIds): ?GpuPoolMember
    {
        $availableAccounts = $this->getAvailableAccounts($pool)
            ->filter(fn ($member) => !in_array($member->id, $excludeIds));

        if ($availableAccounts->isEmpty()) {
            return null;
        }

        return match ($pool->rotation_strategy) {
            GpuAccountPool::STRATEGY_ROUND_ROBIN => $availableAccounts->first(),
            GpuAccountPool::STRATEGY_RANDOM => $this->selectRandom($availableAccounts),
            GpuAccountPool::STRATEGY_LEAST_USED => $this->selectLeastUsed($availableAccounts),
            GpuAccountPool::STRATEGY_MOST_CREDITS => $this->selectMostCredits($availableAccounts),
            GpuAccountPool::STRATEGY_PRIORITY => $this->selectByPriority($availableAccounts),
            default => $availableAccounts->first(),
        };
    }

    private function calculatePoolSuccessRate(Collection $members): float
    {
        $totalSuccess = $members->sum('success_count');
        $totalFailure = $members->sum('failure_count');
        $total = $totalSuccess + $totalFailure;

        if ($total === 0) {
            return 100.0;
        }

        return round(($totalSuccess / $total) * 100, 2);
    }
}
