<?php

declare(strict_types=1);

namespace App\Services;

use App\Models\ApiKeyPool;
use App\Models\ApiKeyPoolMember;
use Illuminate\Support\Collection;
use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

class ApiKeyRotationService
{
    // Cache keys
    const CACHE_ROUND_ROBIN_INDEX = 'api_key_rotation:rr_index:';
    const CACHE_TTL = 86400; // 24 hours

    /**
     * Get next available API key from pool based on rotation strategy
     */
    public function getNextKey(ApiKeyPool $pool): ?ApiKeyPoolMember
    {
        $availableKeys = $this->getAvailableKeys($pool);

        if ($availableKeys->isEmpty()) {
            Log::warning('No available API keys in pool', [
                'pool_id' => $pool->id,
                'provider' => $pool->provider,
            ]);
            return null;
        }

        return match ($pool->rotation_strategy) {
            ApiKeyPool::STRATEGY_ROUND_ROBIN => $this->selectRoundRobin($pool, $availableKeys),
            ApiKeyPool::STRATEGY_RANDOM => $this->selectRandom($availableKeys),
            ApiKeyPool::STRATEGY_LEAST_USED => $this->selectLeastUsed($availableKeys),
            ApiKeyPool::STRATEGY_PRIORITY => $this->selectByPriority($availableKeys),
            default => $availableKeys->first(),
        };
    }

    /**
     * Get keys with failover support - returns sorted list for retry
     */
    public function getKeysWithFailover(ApiKeyPool $pool, int $maxKeys = 3): Collection
    {
        $available = $this->getAvailableKeys($pool);

        if ($available->isEmpty()) {
            return collect();
        }

        // เรียงตาม: success_rate สูง, consecutive_failures ต่ำ, requests_today ต่ำ
        return $available->sortBy(function (ApiKeyPoolMember $key) {
            $successRate = $key->getSuccessRate();
            return [
                -$successRate,
                $key->consecutive_failures,
                $key->requests_today,
            ];
        })->take($maxKeys)->values();
    }

    /**
     * Get all available keys (active, not in cooldown, within daily limit)
     */
    public function getAvailableKeys(ApiKeyPool $pool): Collection
    {
        return $pool->members()
            ->where('status', ApiKeyPoolMember::STATUS_ACTIVE)
            ->where(function ($query) {
                $query->whereNull('cooldown_until')
                    ->orWhere('cooldown_until', '<=', now());
            })
            ->where(function ($query) {
                $query->where('daily_limit', 0)
                    ->orWhereColumn('requests_today', '<', 'daily_limit');
            })
            ->get();
    }

    /**
     * Record successful API call
     */
    public function recordSuccess(ApiKeyPoolMember $member): void
    {
        DB::transaction(function () use ($member) {
            $member->recordSuccess();

            // Apply cooldown if configured
            $pool = $member->apiKeyPool;
            if ($pool && $pool->cooldown_seconds > 0) {
                $member->startCooldown($pool->cooldown_seconds);
            }

            Log::debug('API key request success', [
                'pool_id' => $member->api_key_pool_id,
                'key_label' => $member->label,
                'requests_today' => $member->requests_today,
            ]);
        });
    }

    /**
     * Record failed API call - returns next available key for failover
     */
    public function recordFailure(ApiKeyPoolMember $member, string $error): ?ApiKeyPoolMember
    {
        return DB::transaction(function () use ($member, $error) {
            $member->recordFailure($error);

            // Auto-suspend after 5 consecutive failures
            if ($member->consecutive_failures >= 5) {
                $member->update(['status' => ApiKeyPoolMember::STATUS_ERROR]);
                Log::warning('API key auto-suspended due to consecutive failures', [
                    'pool_id' => $member->api_key_pool_id,
                    'key_label' => $member->label,
                    'failures' => $member->consecutive_failures,
                ]);
            }

            // Return next available key for failover
            /** @var ApiKeyPool|null $pool */
            $pool = $member->apiKeyPool;
            if ($pool && $pool->auto_failover) {
                $nextKey = $this->getNextKey($pool);
                if ($nextKey && $nextKey->id !== $member->id) {
                    return $nextKey;
                }
            }

            return null;
        });
    }

    /**
     * Handle rate limit response (429)
     */
    public function handleRateLimit(ApiKeyPoolMember $member, int $cooldownSeconds = 3600): ?ApiKeyPoolMember
    {
        DB::transaction(function () use ($member, $cooldownSeconds) {
            $member->startCooldown($cooldownSeconds);

            Log::info('API key rate limited, starting cooldown', [
                'pool_id' => $member->api_key_pool_id,
                'key_label' => $member->label,
                'cooldown_seconds' => $cooldownSeconds,
            ]);
        });

        /** @var ApiKeyPool|null $pool */
        $pool = $member->apiKeyPool;
        if ($pool && $pool->auto_failover) {
            return $this->getNextKey($pool);
        }

        return null;
    }

    /**
     * Handle daily quota exhaustion
     */
    public function handleQuotaExhausted(ApiKeyPoolMember $member): ?ApiKeyPoolMember
    {
        $member->markAsExhausted();

        Log::info('API key quota exhausted', [
            'pool_id' => $member->api_key_pool_id,
            'key_label' => $member->label,
            'requests_today' => $member->requests_today,
        ]);

        /** @var ApiKeyPool|null $pool */
        $pool = $member->apiKeyPool;
        if ($pool && $pool->auto_failover) {
            return $this->getNextKey($pool);
        }

        return null;
    }

    /**
     * Reset daily counters for all members - called by scheduled job
     */
    public function resetDailyCounters(): int
    {
        $count = ApiKeyPoolMember::query()
            ->where('requests_today', '>', 0)
            ->update(['requests_today' => 0]);

        // Reactivate exhausted keys
        ApiKeyPoolMember::query()
            ->where('status', ApiKeyPoolMember::STATUS_EXHAUSTED)
            ->update(['status' => ApiKeyPoolMember::STATUS_ACTIVE]);

        // Reactivate cooldown-expired keys
        ApiKeyPoolMember::query()
            ->where('status', ApiKeyPoolMember::STATUS_COOLDOWN)
            ->where('cooldown_until', '<=', now())
            ->update([
                'status' => ApiKeyPoolMember::STATUS_ACTIVE,
                'cooldown_until' => null,
            ]);

        Log::info("Reset daily API key counters", ['keys_reset' => $count]);

        return $count;
    }

    /**
     * Get pool statistics
     */
    public function getPoolStatistics(ApiKeyPool $pool): array
    {
        $members = $pool->members;
        $totalRequests = $members->sum('requests_today');
        $totalSuccess = $members->sum('success_count');
        $totalFailure = $members->sum('failure_count');

        return [
            'pool_id' => $pool->id,
            'pool_name' => $pool->name,
            'provider' => $pool->provider,
            'total_keys' => $members->count(),
            'active_keys' => $members->where('status', ApiKeyPoolMember::STATUS_ACTIVE)->count(),
            'exhausted_keys' => $members->where('status', ApiKeyPoolMember::STATUS_EXHAUSTED)->count(),
            'error_keys' => $members->where('status', ApiKeyPoolMember::STATUS_ERROR)->count(),
            'cooldown_keys' => $members->where('status', ApiKeyPoolMember::STATUS_COOLDOWN)->count(),
            'requests_today' => $totalRequests,
            'total_success' => $totalSuccess,
            'total_failure' => $totalFailure,
            'success_rate' => ($totalSuccess + $totalFailure) > 0
                ? round(($totalSuccess / ($totalSuccess + $totalFailure)) * 100, 2)
                : 100.0,
            'remaining_quota' => $pool->max_requests_per_day - $totalRequests,
        ];
    }

    // --- Private rotation strategies ---

    private function selectRoundRobin(ApiKeyPool $pool, Collection $available): ApiKeyPoolMember
    {
        $cacheKey = self::CACHE_ROUND_ROBIN_INDEX . $pool->id;
        $lastIndex = Cache::get($cacheKey, -1);
        $nextIndex = ($lastIndex + 1) % $available->count();

        Cache::put($cacheKey, $nextIndex, self::CACHE_TTL);

        /** @var ApiKeyPoolMember */
        return $available->values()->get($nextIndex);
    }

    private function selectRandom(Collection $available): ApiKeyPoolMember
    {
        // Weighted random selection
        $totalWeight = $available->sum('weight');
        if ($totalWeight === 0) {
            /** @var ApiKeyPoolMember */
            return $available->random();
        }

        $random = random_int(0, max(1, $totalWeight - 1));
        $cumulative = 0;

        foreach ($available as $key) {
            $cumulative += $key->weight;
            if ($random < $cumulative) {
                return $key;
            }
        }

        /** @var ApiKeyPoolMember */
        return $available->last();
    }

    private function selectLeastUsed(Collection $available): ApiKeyPoolMember
    {
        /** @var ApiKeyPoolMember */
        return $available->sortBy(function (ApiKeyPoolMember $key) {
            return [$key->requests_today, $key->total_requests];
        })->first();
    }

    private function selectByPriority(Collection $available): ApiKeyPoolMember
    {
        /** @var ApiKeyPoolMember */
        return $available->sortBy(function (ApiKeyPoolMember $key) {
            return [$key->priority, -$key->weight];
        })->first();
    }
}
