<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\GpuProviderAccount;
use App\Models\GpuAccountPool;
use App\Models\GpuPoolMember;
use App\Models\GpuInstance;
use App\Models\GpuSetupTemplate;
use App\Models\GpuActivityLog;
use App\Services\Gpu\GpuProviderFactory;
use App\Services\Gpu\GpuAccountRotationService;
use App\Services\Gpu\GpuProvisioningService;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Auth;
use Illuminate\Support\Facades\Validator;

class GpuProviderController extends Controller
{
    public function __construct(
        private GpuProviderFactory $providerFactory,
        private GpuAccountRotationService $rotationService,
        private GpuProvisioningService $provisioningService
    ) {}

    // ==================== Provider Accounts ====================

    /**
     * List all GPU provider accounts for the authenticated user
     */
    public function listAccounts(Request $request): JsonResponse
    {
        $accounts = GpuProviderAccount::where('user_id', Auth::id())
            ->orderBy('created_at', 'desc')
            ->get()
            ->map(fn ($a) => $this->formatAccount($a));

        return response()->json([
            'success' => true,
            'data' => $accounts,
        ]);
    }

    /**
     * Create a new GPU provider account
     */
    public function createAccount(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'provider' => 'required|string|in:' . implode(',', GpuProviderAccount::getProviders()),
            'name' => 'required|string|max:255',
            'email' => 'nullable|email|max:255',
            'api_key' => 'required|string',
            'api_secret' => 'nullable|string',
            'daily_credit_limit' => 'nullable|numeric|min:0',
            'low_balance_threshold' => 'nullable|numeric|min:0',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Validate credentials with provider
        $provider = $this->providerFactory->getProvider($request->provider);
        if (!$provider->validateCredentials($request->api_key, $request->api_secret)) {
            return response()->json([
                'success' => false,
                'error' => 'Invalid API credentials. Please check and try again.',
            ], 400);
        }

        // Create temporary account to get balance
        $tempAccount = new GpuProviderAccount([
            'provider' => $request->provider,
            'api_key' => $request->api_key,
            'api_secret' => $request->api_secret,
        ]);
        $tempAccount->setRawAttributes(['api_key' => $request->api_key], true);

        // Get initial balance
        $balance = 0;
        try {
            $balance = $provider->getBalance($tempAccount);
        } catch (\Exception $e) {
            // Continue even if balance check fails
        }

        $account = GpuProviderAccount::create([
            'user_id' => Auth::id(),
            'provider' => $request->provider,
            'name' => $request->name,
            'email' => $request->email,
            'api_key' => $request->api_key,
            'api_secret' => $request->api_secret,
            'credits_balance' => $balance,
            'daily_credit_limit' => $request->daily_credit_limit,
            'low_balance_threshold' => $request->low_balance_threshold ?? 1.00,
            'last_balance_check_at' => now(),
        ]);

        GpuActivityLog::logAccountCreated($account);

        return response()->json([
            'success' => true,
            'data' => $this->formatAccount($account),
            'message' => 'GPU provider account created successfully',
        ], 201);
    }

    /**
     * Get a specific GPU provider account
     */
    public function getAccount(int $id): JsonResponse
    {
        $account = GpuProviderAccount::where('user_id', Auth::id())
            ->findOrFail($id);

        return response()->json([
            'success' => true,
            'data' => $this->formatAccount($account),
        ]);
    }

    /**
     * Update a GPU provider account
     */
    public function updateAccount(Request $request, int $id): JsonResponse
    {
        $account = GpuProviderAccount::where('user_id', Auth::id())
            ->findOrFail($id);

        $validator = Validator::make($request->all(), [
            'name' => 'sometimes|string|max:255',
            'email' => 'nullable|email|max:255',
            'api_key' => 'sometimes|string',
            'api_secret' => 'nullable|string',
            'daily_credit_limit' => 'nullable|numeric|min:0',
            'low_balance_threshold' => 'nullable|numeric|min:0',
            'is_active' => 'sometimes|boolean',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // If API key changed, validate it
        if ($request->has('api_key') && $request->api_key !== $account->getDecryptedApiKey()) {
            $provider = $this->providerFactory->getProviderForAccount($account);
            if (!$provider->validateCredentials($request->api_key, $request->api_secret ?? $account->getDecryptedApiSecret())) {
                return response()->json([
                    'success' => false,
                    'error' => 'Invalid API credentials',
                ], 400);
            }
        }

        $account->update($request->only([
            'name', 'email', 'api_key', 'api_secret',
            'daily_credit_limit', 'low_balance_threshold', 'is_active',
        ]));

        return response()->json([
            'success' => true,
            'data' => $this->formatAccount($account->fresh()),
            'message' => 'Account updated successfully',
        ]);
    }

    /**
     * Delete a GPU provider account
     */
    public function deleteAccount(int $id): JsonResponse
    {
        $account = GpuProviderAccount::where('user_id', Auth::id())
            ->findOrFail($id);

        // Check for active instances
        $activeInstances = $account->instances()->active()->count();
        if ($activeInstances > 0) {
            return response()->json([
                'success' => false,
                'error' => "Cannot delete account with {$activeInstances} active instances. Terminate them first.",
            ], 400);
        }

        $account->delete();

        return response()->json([
            'success' => true,
            'message' => 'Account deleted successfully',
        ]);
    }

    /**
     * Refresh balance for an account
     */
    public function refreshBalance(int $id): JsonResponse
    {
        $account = GpuProviderAccount::where('user_id', Auth::id())
            ->findOrFail($id);

        $provider = $this->providerFactory->getProviderForAccount($account);
        $oldBalance = $account->credits_balance;
        $newBalance = $provider->getBalance($account);

        $account->updateBalance($newBalance);
        GpuActivityLog::logBalanceCheck($account, $oldBalance, $newBalance);

        return response()->json([
            'success' => true,
            'data' => [
                'old_balance' => $oldBalance,
                'new_balance' => $newBalance,
                'change' => $newBalance - $oldBalance,
            ],
        ]);
    }

    // ==================== Account Pools ====================

    /**
     * List all GPU account pools
     */
    public function listPools(Request $request): JsonResponse
    {
        $pools = GpuAccountPool::where('user_id', Auth::id())
            ->with(['members.providerAccount'])
            ->orderBy('created_at', 'desc')
            ->get()
            ->map(fn ($p) => $this->formatPool($p));

        return response()->json([
            'success' => true,
            'data' => $pools,
        ]);
    }

    /**
     * Create a new GPU account pool
     */
    public function createPool(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'name' => 'required|string|max:255',
            'provider' => 'required|string',
            'description' => 'nullable|string',
            'rotation_strategy' => 'sometimes|string|in:' . implode(',', GpuAccountPool::getStrategies()),
            'cooldown_minutes' => 'sometimes|integer|min:0',
            'min_credits_required' => 'sometimes|numeric|min:0',
            'auto_failover' => 'sometimes|boolean',
            'auto_provision' => 'sometimes|boolean',
            'preferred_gpu_types' => 'nullable|array',
            'preferred_regions' => 'nullable|array',
            'max_price_per_hour' => 'nullable|numeric|min:0',
            'account_ids' => 'nullable|array',
            'account_ids.*' => 'integer|exists:gpu_provider_accounts,id',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $pool = GpuAccountPool::create([
            'user_id' => Auth::id(),
            'name' => $request->name,
            'provider' => $request->provider,
            'description' => $request->description,
            'rotation_strategy' => $request->rotation_strategy ?? GpuAccountPool::STRATEGY_MOST_CREDITS,
            'cooldown_minutes' => $request->cooldown_minutes ?? 60,
            'min_credits_required' => $request->min_credits_required ?? 0.50,
            'auto_failover' => $request->auto_failover ?? true,
            'auto_provision' => $request->auto_provision ?? true,
            'preferred_gpu_types' => $request->preferred_gpu_types,
            'preferred_regions' => $request->preferred_regions,
            'max_price_per_hour' => $request->max_price_per_hour,
        ]);

        // Add accounts to pool
        if (!empty($request->account_ids)) {
            foreach ($request->account_ids as $index => $accountId) {
                $account = GpuProviderAccount::where('user_id', Auth::id())
                    ->find($accountId);

                if ($account) {
                    GpuPoolMember::create([
                        'gpu_account_pool_id' => $pool->id,
                        'gpu_provider_account_id' => $accountId,
                        'priority' => $index,
                    ]);
                }
            }
        }

        return response()->json([
            'success' => true,
            'data' => $this->formatPool($pool->fresh(['members.providerAccount'])),
            'message' => 'Pool created successfully',
        ], 201);
    }

    /**
     * Get pool statistics
     */
    public function getPoolStatistics(int $id): JsonResponse
    {
        $pool = GpuAccountPool::where('user_id', Auth::id())
            ->findOrFail($id);

        $stats = $this->rotationService->getPoolStatistics($pool);

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }

    /**
     * Add account to pool
     */
    public function addAccountToPool(Request $request, int $poolId): JsonResponse
    {
        $pool = GpuAccountPool::where('user_id', Auth::id())
            ->findOrFail($poolId);

        $validator = Validator::make($request->all(), [
            'account_id' => 'required|integer|exists:gpu_provider_accounts,id',
            'priority' => 'sometimes|integer|min:0',
            'weight' => 'sometimes|integer|min:1|max:1000',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Verify account belongs to user
        $account = GpuProviderAccount::where('user_id', Auth::id())
            ->findOrFail($request->account_id);

        // Check if already in pool
        $existing = GpuPoolMember::where('gpu_account_pool_id', $poolId)
            ->where('gpu_provider_account_id', $request->account_id)
            ->first();

        if ($existing) {
            return response()->json([
                'success' => false,
                'error' => 'Account is already in this pool',
            ], 400);
        }

        $member = GpuPoolMember::create([
            'gpu_account_pool_id' => $poolId,
            'gpu_provider_account_id' => $request->account_id,
            'priority' => $request->priority ?? 0,
            'weight' => $request->weight ?? 100,
        ]);

        return response()->json([
            'success' => true,
            'data' => $member->load('providerAccount'),
            'message' => 'Account added to pool',
        ]);
    }

    /**
     * Remove account from pool
     */
    public function removeAccountFromPool(int $poolId, int $accountId): JsonResponse
    {
        $pool = GpuAccountPool::where('user_id', Auth::id())
            ->findOrFail($poolId);

        $member = GpuPoolMember::where('gpu_account_pool_id', $poolId)
            ->where('gpu_provider_account_id', $accountId)
            ->firstOrFail();

        $member->delete();

        return response()->json([
            'success' => true,
            'message' => 'Account removed from pool',
        ]);
    }

    /**
     * Refresh balances for all accounts in pool
     */
    public function refreshPoolBalances(int $id): JsonResponse
    {
        $pool = GpuAccountPool::where('user_id', Auth::id())
            ->findOrFail($id);

        $results = $this->rotationService->refreshPoolBalances($pool);

        return response()->json([
            'success' => true,
            'data' => $results,
        ]);
    }

    // ==================== Instances ====================

    /**
     * List all GPU instances
     */
    public function listInstances(Request $request): JsonResponse
    {
        $query = GpuInstance::where('user_id', Auth::id())
            ->with(['providerAccount', 'accountPool']);

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        if ($request->has('provider')) {
            $query->where('provider', $request->provider);
        }

        if ($request->has('pool_id')) {
            $query->where('gpu_account_pool_id', $request->pool_id);
        }

        $instances = $query->orderBy('created_at', 'desc')
            ->get()
            ->map(fn ($i) => $this->formatInstance($i));

        return response()->json([
            'success' => true,
            'data' => $instances,
        ]);
    }

    /**
     * Search available GPU offers
     */
    public function searchOffers(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'pool_id' => 'required_without:account_id|integer|exists:gpu_account_pools,id',
            'account_id' => 'required_without:pool_id|integer|exists:gpu_provider_accounts,id',
            'gpu_type' => 'nullable|string',
            'min_gpu_memory_gb' => 'nullable|integer|min:1',
            'max_price_per_hour' => 'nullable|numeric|min:0',
            'min_cpu_cores' => 'nullable|integer|min:1',
            'min_ram_gb' => 'nullable|integer|min:1',
            'region' => 'nullable|string',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $filters = $request->only([
            'gpu_type', 'min_gpu_memory_gb', 'max_price_per_hour',
            'min_cpu_cores', 'min_ram_gb', 'region',
        ]);

        if ($request->has('pool_id')) {
            $pool = GpuAccountPool::where('user_id', Auth::id())
                ->findOrFail($request->pool_id);

            $result = $this->provisioningService->searchOffersFromPool($pool, $filters);
        } else {
            $account = GpuProviderAccount::where('user_id', Auth::id())
                ->findOrFail($request->account_id);

            $provider = $this->providerFactory->getProviderForAccount($account);
            $offers = $provider->searchOffers($account, $filters);

            $result = ['offers' => $offers, 'errors' => []];
        }

        return response()->json([
            'success' => true,
            'data' => $result,
        ]);
    }

    /**
     * Provision a new GPU instance
     */
    public function provisionInstance(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'pool_id' => 'required_without:account_id|integer|exists:gpu_account_pools,id',
            'account_id' => 'required_without:pool_id|integer|exists:gpu_provider_accounts,id',
            'template_id' => 'nullable|integer|exists:gpu_setup_templates,id',
            'gpu_type' => 'nullable|string',
            'min_gpu_memory_gb' => 'nullable|integer|min:1',
            'max_price_per_hour' => 'nullable|numeric|min:0',
            'disk_gb' => 'nullable|integer|min:10',
            'docker_image' => 'nullable|string',
            'environment_vars' => 'nullable|array',
            'name' => 'nullable|string|max:255',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $requirements = $request->only([
            'gpu_type', 'min_gpu_memory_gb', 'max_price_per_hour',
            'disk_gb', 'docker_image', 'environment_vars', 'name',
        ]);

        $template = null;
        if ($request->template_id) {
            $template = GpuSetupTemplate::forUser(Auth::id())
                ->findOrFail($request->template_id);
        }

        if ($request->has('pool_id')) {
            $pool = GpuAccountPool::where('user_id', Auth::id())
                ->findOrFail($request->pool_id);

            $result = $this->provisioningService->provisionFromPool($pool, $requirements, $template);
        } else {
            $account = GpuProviderAccount::where('user_id', Auth::id())
                ->findOrFail($request->account_id);

            $result = $this->provisioningService->provisionWithAccount($account, $requirements, $template);
        }

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'data' => [
                'instance' => $this->formatInstance($result['instance']),
                'offer' => $result['offer'] ?? null,
                'account_used' => $result['account_used'] ?? null,
            ],
            'message' => 'Instance provisioning started',
        ], 201);
    }

    /**
     * Get instance details
     */
    public function getInstance(int $id): JsonResponse
    {
        $instance = GpuInstance::where('user_id', Auth::id())
            ->with(['providerAccount', 'accountPool'])
            ->findOrFail($id);

        return response()->json([
            'success' => true,
            'data' => $this->formatInstance($instance),
        ]);
    }

    /**
     * Start an instance
     */
    public function startInstance(int $id): JsonResponse
    {
        $instance = GpuInstance::where('user_id', Auth::id())
            ->findOrFail($id);

        $result = $this->provisioningService->startInstance($instance);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'data' => $this->formatInstance($result['instance']),
            'message' => 'Instance started',
        ]);
    }

    /**
     * Stop an instance
     */
    public function stopInstance(int $id): JsonResponse
    {
        $instance = GpuInstance::where('user_id', Auth::id())
            ->findOrFail($id);

        $result = $this->provisioningService->stopInstance($instance);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'data' => $this->formatInstance($result['instance']),
            'message' => 'Instance stopped',
        ]);
    }

    /**
     * Terminate an instance
     */
    public function terminateInstance(int $id): JsonResponse
    {
        $instance = GpuInstance::where('user_id', Auth::id())
            ->findOrFail($id);

        $result = $this->provisioningService->terminateInstance($instance);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'message' => 'Instance terminated',
        ]);
    }

    /**
     * Sync instance status from provider
     */
    public function syncInstance(int $id): JsonResponse
    {
        $instance = GpuInstance::where('user_id', Auth::id())
            ->findOrFail($id);

        $result = $this->provisioningService->syncInstanceStatus($instance);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'data' => $this->formatInstance($result['instance']),
        ]);
    }

    /**
     * Run setup on instance
     */
    public function runSetup(Request $request, int $id): JsonResponse
    {
        $instance = GpuInstance::where('user_id', Auth::id())
            ->findOrFail($id);

        $template = null;
        if ($request->template_id) {
            $template = GpuSetupTemplate::forUser(Auth::id())
                ->findOrFail($request->template_id);
        }

        $result = $this->provisioningService->runSetup($instance, $template);

        if (!$result['success']) {
            return response()->json([
                'success' => false,
                'error' => $result['error'],
            ], 400);
        }

        return response()->json([
            'success' => true,
            'data' => $result,
        ]);
    }

    // ==================== Templates ====================

    /**
     * List setup templates
     */
    public function listTemplates(): JsonResponse
    {
        $templates = GpuSetupTemplate::forUser(Auth::id())
            ->orderBy('is_default', 'desc')
            ->orderBy('name')
            ->get();

        return response()->json([
            'success' => true,
            'data' => $templates,
        ]);
    }

    /**
     * Create setup template
     */
    public function createTemplate(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'name' => 'required|string|max:255',
            'description' => 'nullable|string',
            'docker_image' => 'nullable|string',
            'environment_vars' => 'nullable|array',
            'setup_script' => 'nullable|string',
            'post_setup_script' => 'nullable|string',
            'required_packages' => 'nullable|array',
            'required_gpu_types' => 'nullable|array',
            'min_gpu_memory_gb' => 'nullable|integer|min:1',
            'min_cpu_cores' => 'nullable|integer|min:1',
            'min_ram_gb' => 'nullable|integer|min:1',
            'min_disk_gb' => 'nullable|integer|min:1',
            'is_default' => 'sometimes|boolean',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $template = GpuSetupTemplate::create([
            'user_id' => Auth::id(),
            ...$request->only([
                'name', 'description', 'docker_image', 'environment_vars',
                'setup_script', 'post_setup_script', 'required_packages',
                'required_gpu_types', 'min_gpu_memory_gb', 'min_cpu_cores',
                'min_ram_gb', 'min_disk_gb',
            ]),
        ]);

        if ($request->is_default) {
            $template->setAsDefault();
        }

        return response()->json([
            'success' => true,
            'data' => $template,
            'message' => 'Template created',
        ], 201);
    }

    /**
     * Create default templates for the user
     */
    public function createDefaultTemplates(): JsonResponse
    {
        $diffusers = GpuSetupTemplate::createDiffusersTemplate(Auth::id());
        $postxagent = GpuSetupTemplate::createPostXAgentTemplate(Auth::id());

        return response()->json([
            'success' => true,
            'data' => [$diffusers, $postxagent],
            'message' => 'Default templates created',
        ], 201);
    }

    // ==================== Activity Logs ====================

    /**
     * Get activity logs
     */
    public function getActivityLogs(Request $request): JsonResponse
    {
        $query = GpuActivityLog::where('user_id', Auth::id())
            ->with(['providerAccount', 'accountPool', 'instance']);

        if ($request->has('account_id')) {
            $query->where('gpu_provider_account_id', $request->account_id);
        }

        if ($request->has('pool_id')) {
            $query->where('gpu_account_pool_id', $request->pool_id);
        }

        if ($request->has('instance_id')) {
            $query->where('gpu_instance_id', $request->instance_id);
        }

        if ($request->has('event_type')) {
            $query->where('event_type', $request->event_type);
        }

        if ($request->has('hours')) {
            $query->recent((int) $request->hours);
        }

        $logs = $query->orderBy('created_at', 'desc')
            ->limit($request->limit ?? 100)
            ->get();

        return response()->json([
            'success' => true,
            'data' => $logs,
        ]);
    }

    /**
     * Get health report
     */
    public function getHealthReport(Request $request): JsonResponse
    {
        $hours = (int) ($request->hours ?? 24);
        $report = $this->rotationService->getHealthReport($hours);

        return response()->json([
            'success' => true,
            'data' => $report,
        ]);
    }

    // ==================== Helpers ====================

    private function formatAccount(GpuProviderAccount $account): array
    {
        return [
            'id' => $account->id,
            'provider' => $account->provider,
            'name' => $account->name,
            'email' => $account->email,
            'credits_balance' => $account->credits_balance,
            'credits_used_today' => $account->credits_used_today,
            'daily_credit_limit' => $account->daily_credit_limit,
            'low_balance_threshold' => $account->low_balance_threshold,
            'status' => $account->status,
            'is_active' => $account->is_active,
            'is_low_balance' => $account->isLowBalance(),
            'is_available' => $account->isAvailable(),
            'instances_created' => $account->instances_created,
            'total_gpu_hours' => $account->total_gpu_hours,
            'last_used_at' => $account->last_used_at?->toIso8601String(),
            'last_balance_check_at' => $account->last_balance_check_at?->toIso8601String(),
            'cooldown_until' => $account->cooldown_until?->toIso8601String(),
            'created_at' => $account->created_at->toIso8601String(),
        ];
    }

    private function formatPool(GpuAccountPool $pool): array
    {
        $stats = $pool->getStatistics();

        return [
            'id' => $pool->id,
            'name' => $pool->name,
            'provider' => $pool->provider,
            'description' => $pool->description,
            'rotation_strategy' => $pool->rotation_strategy,
            'cooldown_minutes' => $pool->cooldown_minutes,
            'min_credits_required' => $pool->min_credits_required,
            'auto_failover' => $pool->auto_failover,
            'auto_provision' => $pool->auto_provision,
            'preferred_gpu_types' => $pool->preferred_gpu_types,
            'preferred_regions' => $pool->preferred_regions,
            'max_price_per_hour' => $pool->max_price_per_hour,
            'is_active' => $pool->is_active,
            'statistics' => $stats,
            'members' => $pool->members->map(fn ($m) => [
                'id' => $m->id,
                'account_id' => $m->gpu_provider_account_id,
                'account_name' => $m->providerAccount?->name,
                'provider' => $m->providerAccount?->provider,
                'priority' => $m->priority,
                'weight' => $m->weight,
                'status' => $m->status,
                'credits_balance' => $m->providerAccount?->credits_balance,
                'instances_today' => $m->instances_today,
                'success_rate' => $m->getSuccessRate(),
            ]),
            'created_at' => $pool->created_at->toIso8601String(),
        ];
    }

    private function formatInstance(GpuInstance $instance): array
    {
        return [
            'id' => $instance->id,
            'provider' => $instance->provider,
            'instance_id' => $instance->instance_id,
            'instance_name' => $instance->instance_name,
            'status' => $instance->status,
            'setup_status' => $instance->setup_status,
            'gpu_type' => $instance->gpu_type,
            'gpu_count' => $instance->gpu_count,
            'cpu_cores' => $instance->cpu_cores,
            'ram_gb' => $instance->ram_gb,
            'disk_gb' => $instance->disk_gb,
            'region' => $instance->region,
            'ip_address' => $instance->ip_address,
            'ssh_port' => $instance->ssh_port,
            'ssh_command' => $instance->getSshCommand(),
            'price_per_hour' => $instance->price_per_hour,
            'current_cost' => $instance->getCurrentCost(),
            'total_cost' => $instance->total_cost,
            'runtime_minutes' => $instance->runtime_minutes,
            'docker_image' => $instance->docker_image,
            'gpu_provider_account_id' => $instance->gpu_provider_account_id,
            'gpu_account_pool_id' => $instance->gpu_account_pool_id,
            'account_name' => $instance->providerAccount?->name,
            'pool_name' => $instance->accountPool?->name,
            'started_at' => $instance->started_at?->toIso8601String(),
            'stopped_at' => $instance->stopped_at?->toIso8601String(),
            'created_at' => $instance->created_at->toIso8601String(),
        ];
    }
}
