<?php

declare(strict_types=1);

namespace App\Services;

use App\Models\AccountPool;
use App\Models\AccountPoolMember;
use App\Models\AccountStatusLog;
use App\Models\SocialAccount;
use App\Models\BackupCredential;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

class AccountRotationService
{
    // Cache keys
    const CACHE_LAST_USED_PREFIX = 'account_rotation:last_used:';
    const CACHE_ROUND_ROBIN_INDEX = 'account_rotation:rr_index:';

    /**
     * Get next available account from pool based on rotation strategy
     */
    public function getNextAccount(AccountPool $pool): ?AccountPoolMember
    {
        $availableAccounts = $this->getAvailableAccounts($pool);

        if ($availableAccounts->isEmpty()) {
            Log::warning("No available accounts in pool", [
                'pool_id' => $pool->id,
                'platform' => $pool->platform,
            ]);
            return null;
        }

        return match ($pool->rotation_strategy) {
            AccountPool::STRATEGY_ROUND_ROBIN => $this->selectRoundRobin($pool, $availableAccounts),
            AccountPool::STRATEGY_RANDOM => $this->selectRandom($availableAccounts),
            AccountPool::STRATEGY_LEAST_USED => $this->selectLeastUsed($availableAccounts),
            AccountPool::STRATEGY_PRIORITY => $this->selectByPriority($availableAccounts),
            default => $availableAccounts->first(),
        };
    }

    /**
     * Get next account with failover support
     * Returns array of accounts sorted by preference for posting
     */
    public function getAccountsWithFailover(AccountPool $pool, int $maxAccounts = 3): Collection
    {
        $availableAccounts = $this->getAvailableAccounts($pool);

        if ($availableAccounts->isEmpty()) {
            return collect();
        }

        // Sort by strategy and return multiple for failover
        $sorted = match ($pool->rotation_strategy) {
            AccountPool::STRATEGY_PRIORITY => $availableAccounts->sortBy('priority'),
            AccountPool::STRATEGY_LEAST_USED => $availableAccounts->sortBy('posts_today'),
            default => $availableAccounts->shuffle(),
        };

        return $sorted->take($maxAccounts)->values();
    }

    /**
     * Get all available accounts from a pool
     */
    public function getAvailableAccounts(AccountPool $pool): Collection
    {
        // Check and end expired cooldowns first
        $this->checkExpiredCooldowns($pool);

        return $pool->members()
            ->with(['socialAccount' => function ($query) {
                $query->where('is_active', true);
            }])
            ->where('status', AccountPoolMember::STATUS_ACTIVE)
            ->where(function ($query) {
                $query->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            })
            ->where('posts_today', '<', $pool->max_posts_per_day)
            ->get()
            ->filter(fn ($member) => $member->socialAccount !== null);
    }

    /**
     * Record successful post
     */
    public function recordSuccess(
        AccountPoolMember $member,
        ?int $postId = null,
        ?array $metadata = null
    ): void {
        DB::transaction(function () use ($member, $postId, $metadata) {
            $member->recordSuccess();

            // Start cooldown if configured
            if ($member->accountPool->cooldown_minutes > 0) {
                $member->startCooldown($member->accountPool->cooldown_minutes);

                AccountStatusLog::logCooldownStarted(
                    $member->social_account_id,
                    $member->accountPool->cooldown_minutes,
                    $member->account_pool_id
                );
            }

            // Log success
            AccountStatusLog::logSuccess(
                $member->social_account_id,
                $member->account_pool_id,
                $postId,
                $metadata
            );

            // Update round robin index
            $this->updateRoundRobinIndex($member->accountPool);
        });

        Log::info("Account post success recorded", [
            'account_id' => $member->social_account_id,
            'pool_id' => $member->account_pool_id,
            'post_id' => $postId,
        ]);
    }

    /**
     * Record failed post and handle failover
     */
    public function recordFailure(
        AccountPoolMember $member,
        string $errorMessage,
        ?int $postId = null,
        ?array $metadata = null
    ): ?AccountPoolMember {
        DB::transaction(function () use ($member, $errorMessage, $postId, $metadata) {
            $member->recordFailure($errorMessage);

            // Log failure
            AccountStatusLog::logFailure(
                $member->social_account_id,
                $errorMessage,
                $member->account_pool_id,
                $postId,
                $metadata
            );

            // Check if account should be suspended
            $this->checkAndSuspendAccount($member, $errorMessage);
        });

        Log::warning("Account post failure recorded", [
            'account_id' => $member->social_account_id,
            'pool_id' => $member->account_pool_id,
            'error' => $errorMessage,
            'consecutive_failures' => $member->consecutive_failures,
        ]);

        // Return next available account for failover if enabled
        if ($member->accountPool->auto_failover) {
            return $this->getNextAccountExcluding($member->accountPool, [$member->id]);
        }

        return null;
    }

    /**
     * Handle rate limit error
     */
    public function handleRateLimit(
        AccountPoolMember $member,
        int $cooldownMinutes = 60,
        ?array $metadata = null
    ): ?AccountPoolMember {
        DB::transaction(function () use ($member, $cooldownMinutes, $metadata) {
            $member->startCooldown($cooldownMinutes);

            AccountStatusLog::logRateLimited(
                $member->social_account_id,
                $member->account_pool_id,
                $metadata
            );
        });

        Log::warning("Account rate limited", [
            'account_id' => $member->social_account_id,
            'cooldown_minutes' => $cooldownMinutes,
        ]);

        // Return next available account for failover
        if ($member->accountPool->auto_failover) {
            return $this->getNextAccountExcluding($member->accountPool, [$member->id]);
        }

        return null;
    }

    /**
     * Handle account ban
     */
    public function handleAccountBan(
        AccountPoolMember $member,
        string $reason,
        ?array $metadata = null
    ): ?AccountPoolMember {
        DB::transaction(function () use ($member, $reason, $metadata) {
            $member->markAsBanned($reason);

            AccountStatusLog::logBan(
                $member->social_account_id,
                $reason,
                $member->account_pool_id,
                $metadata
            );
        });

        Log::error("Account banned", [
            'account_id' => $member->social_account_id,
            'reason' => $reason,
        ]);

        // Return next available account for failover
        if ($member->accountPool->auto_failover) {
            return $this->getNextAccountExcluding($member->accountPool, [$member->id]);
        }

        return null;
    }

    /**
     * Recover account from banned/suspended status
     */
    public function recoverAccount(
        AccountPoolMember $member,
        string $reason = 'Manual recovery'
    ): void {
        $oldStatus = $member->status;

        DB::transaction(function () use ($member, $oldStatus, $reason) {
            $member->reactivate();

            AccountStatusLog::logStatusChange(
                $member->social_account_id,
                $oldStatus,
                AccountPoolMember::STATUS_ACTIVE,
                $reason,
                AccountStatusLog::TRIGGERED_BY_USER,
                $member->account_pool_id
            );
        });

        Log::info("Account recovered", [
            'account_id' => $member->social_account_id,
            'old_status' => $oldStatus,
        ]);
    }

    /**
     * Reset daily post counters for all accounts
     */
    public function resetDailyCounters(): int
    {
        $updated = AccountPoolMember::query()->update(['posts_today' => 0]);

        Log::info("Daily post counters reset", ['accounts_updated' => $updated]);

        return $updated;
    }

    /**
     * Get pool statistics
     */
    public function getPoolStatistics(AccountPool $pool): array
    {
        $members = $pool->members()->with('socialAccount')->get();

        $statusCounts = $members->groupBy('status')->map->count();

        return [
            'total_accounts' => $members->count(),
            'active' => $statusCounts->get(AccountPoolMember::STATUS_ACTIVE, 0),
            'cooldown' => $statusCounts->get(AccountPoolMember::STATUS_COOLDOWN, 0),
            'suspended' => $statusCounts->get(AccountPoolMember::STATUS_SUSPENDED, 0),
            'banned' => $statusCounts->get(AccountPoolMember::STATUS_BANNED, 0),
            'error' => $statusCounts->get(AccountPoolMember::STATUS_ERROR, 0),
            'posts_today' => $members->sum('posts_today'),
            'total_posts' => $members->sum('total_posts'),
            'success_rate' => $this->calculatePoolSuccessRate($members),
            'available_now' => $this->getAvailableAccounts($pool)->count(),
        ];
    }

    /**
     * Get account health report
     */
    public function getAccountHealthReport(int $hours = 24): array
    {
        $logs = AccountStatusLog::with(['socialAccount', 'accountPool'])
            ->recent($hours)
            ->get();

        return [
            'period_hours' => $hours,
            'total_events' => $logs->count(),
            'successes' => $logs->where('event_type', AccountStatusLog::EVENT_POST_SUCCESS)->count(),
            'failures' => $logs->where('event_type', AccountStatusLog::EVENT_POST_FAILED)->count(),
            'rate_limits' => $logs->where('event_type', AccountStatusLog::EVENT_RATE_LIMITED)->count(),
            'bans' => $logs->where('event_type', AccountStatusLog::EVENT_ACCOUNT_BANNED)->count(),
            'by_platform' => $logs->groupBy(fn ($log) => $log->socialAccount?->platform)
                ->map(fn ($group) => [
                    'total' => $group->count(),
                    'failures' => $group->filter(fn ($l) => in_array($l->event_type, [
                        AccountStatusLog::EVENT_POST_FAILED,
                        AccountStatusLog::EVENT_ACCOUNT_BANNED,
                    ]))->count(),
                ]),
        ];
    }

    /**
     * Backup current credentials for an account
     */
    public function backupCredentials(SocialAccount $account): void
    {
        if ($account->access_token) {
            BackupCredential::storeCredential(
                $account->id,
                BackupCredential::TYPE_ACCESS_TOKEN,
                $account->access_token,
                'Auto backup - ' . now()->format('Y-m-d H:i:s'),
                true,
                $account->token_expires_at
            );
        }

        if ($account->refresh_token) {
            BackupCredential::storeCredential(
                $account->id,
                BackupCredential::TYPE_REFRESH_TOKEN,
                $account->refresh_token,
                'Auto backup - ' . now()->format('Y-m-d H:i:s'),
                true
            );
        }

        Log::info("Credentials backed up", ['account_id' => $account->id]);
    }

    /**
     * Restore credentials from backup
     */
    public function restoreCredentials(SocialAccount $account): bool
    {
        $accessToken = BackupCredential::forAccount($account->id)
            ->ofType(BackupCredential::TYPE_ACCESS_TOKEN)
            ->primary()
            ->valid()
            ->latest()
            ->first();

        if (!$accessToken) {
            Log::warning("No valid backup credentials found", ['account_id' => $account->id]);
            return false;
        }

        $refreshToken = BackupCredential::forAccount($account->id)
            ->ofType(BackupCredential::TYPE_REFRESH_TOKEN)
            ->primary()
            ->latest()
            ->first();

        $account->update([
            'access_token' => $accessToken->getDecryptedValue(),
            'refresh_token' => $refreshToken?->getDecryptedValue(),
            'token_expires_at' => $accessToken->valid_until,
        ]);

        Log::info("Credentials restored from backup", ['account_id' => $account->id]);

        return true;
    }

    // Private helper methods

    private function selectRoundRobin(AccountPool $pool, Collection $accounts): AccountPoolMember
    {
        $cacheKey = self::CACHE_ROUND_ROBIN_INDEX . $pool->id;
        $lastIndex = Cache::get($cacheKey, -1);
        $nextIndex = ($lastIndex + 1) % $accounts->count();

        Cache::put($cacheKey, $nextIndex, now()->addDay());

        return $accounts->values()[$nextIndex];
    }

    private function selectRandom(Collection $accounts): AccountPoolMember
    {
        // Weighted random selection
        $totalWeight = $accounts->sum('weight');
        $random = random_int(0, $totalWeight);
        $cumulative = 0;

        foreach ($accounts as $account) {
            $cumulative += $account->weight;
            if ($random <= $cumulative) {
                return $account;
            }
        }

        return $accounts->last();
    }

    private function selectLeastUsed(Collection $accounts): AccountPoolMember
    {
        return $accounts->sortBy('posts_today')->sortBy('total_posts')->first();
    }

    private function selectByPriority(Collection $accounts): AccountPoolMember
    {
        return $accounts->sortBy('priority')->first();
    }

    private function updateRoundRobinIndex(AccountPool $pool): void
    {
        $cacheKey = self::CACHE_ROUND_ROBIN_INDEX . $pool->id;
        $currentIndex = Cache::get($cacheKey, 0);
        $memberCount = $pool->members()->count();

        if ($memberCount > 0) {
            Cache::put($cacheKey, ($currentIndex + 1) % $memberCount, now()->addDay());
        }
    }

    private function checkExpiredCooldowns(AccountPool $pool): void
    {
        $pool->members()
            ->where('status', AccountPoolMember::STATUS_COOLDOWN)
            ->where('cooldown_until', '<=', now())
            ->each(function (AccountPoolMember $member) {
                $member->endCooldown();

                AccountStatusLog::create([
                    'social_account_id' => $member->social_account_id,
                    'account_pool_id' => $member->account_pool_id,
                    'event_type' => AccountStatusLog::EVENT_COOLDOWN_ENDED,
                    'old_status' => AccountPoolMember::STATUS_COOLDOWN,
                    'new_status' => AccountPoolMember::STATUS_ACTIVE,
                    'message' => 'Cooldown period ended',
                    'triggered_by' => AccountStatusLog::TRIGGERED_BY_SYSTEM,
                ]);
            });
    }

    private function checkAndSuspendAccount(AccountPoolMember $member, string $errorMessage): void
    {
        // Suspend if 5 consecutive failures
        if ($member->consecutive_failures >= 5) {
            $member->markAsSuspended("Too many consecutive failures: {$errorMessage}");

            AccountStatusLog::logStatusChange(
                $member->social_account_id,
                AccountPoolMember::STATUS_ACTIVE,
                AccountPoolMember::STATUS_SUSPENDED,
                "Auto-suspended after {$member->consecutive_failures} consecutive failures",
                AccountStatusLog::TRIGGERED_BY_SYSTEM,
                $member->account_pool_id
            );
        }
    }

    private function getNextAccountExcluding(AccountPool $pool, array $excludeIds): ?AccountPoolMember
    {
        $availableAccounts = $this->getAvailableAccounts($pool)
            ->filter(fn ($member) => !in_array($member->id, $excludeIds));

        if ($availableAccounts->isEmpty()) {
            return null;
        }

        return match ($pool->rotation_strategy) {
            AccountPool::STRATEGY_ROUND_ROBIN => $availableAccounts->first(),
            AccountPool::STRATEGY_RANDOM => $this->selectRandom($availableAccounts),
            AccountPool::STRATEGY_LEAST_USED => $this->selectLeastUsed($availableAccounts),
            AccountPool::STRATEGY_PRIORITY => $this->selectByPriority($availableAccounts),
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
