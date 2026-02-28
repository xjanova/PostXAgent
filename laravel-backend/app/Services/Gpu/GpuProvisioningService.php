<?php

declare(strict_types=1);

namespace App\Services\Gpu;

use App\Models\GpuAccountPool;
use App\Models\GpuProviderAccount;
use App\Models\GpuInstance;
use App\Models\GpuSetupTemplate;
use App\Models\GpuActivityLog;
use App\Models\User;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;

/**
 * Service for provisioning and managing GPU instances
 */
class GpuProvisioningService
{
    public function __construct(
        private GpuProviderFactory $providerFactory,
        private GpuAccountRotationService $rotationService
    ) {}

    /**
     * Provision a new GPU instance using the account pool
     */
    public function provisionFromPool(
        GpuAccountPool $pool,
        array $requirements = [],
        ?GpuSetupTemplate $template = null
    ): array {
        // Get next available account
        $member = $this->rotationService->getNextAccount($pool);

        if (!$member) {
            return [
                'success' => false,
                'error' => 'No available GPU accounts in pool',
            ];
        }

        $account = $member->providerAccount;
        $provider = $this->providerFactory->getProviderForAccount($account);

        try {
            // Search for suitable offers
            $filters = $this->buildSearchFilters($requirements, $template, $pool);
            $offers = $provider->searchOffers($account, $filters);

            if (empty($offers)) {
                $this->rotationService->recordFailure($member, 'No suitable GPU offers found');
                return [
                    'success' => false,
                    'error' => 'No suitable GPU offers found matching requirements',
                ];
            }

            // Select best offer (first one after sorting)
            $offer = $offers[0];

            // Build instance config
            $config = $this->buildInstanceConfig($offer, $requirements, $template);

            // Create instance
            $result = $provider->createInstance($account, $config);

            if (!$result['success']) {
                $this->rotationService->recordFailure($member, $result['error'] ?? 'Instance creation failed');

                // Try failover if available
                if ($pool->auto_failover) {
                    $nextMember = $this->rotationService->getAccountsWithFailover($pool, 2)->skip(1)->first();
                    if ($nextMember) {
                        Log::info('Attempting failover for GPU provisioning', [
                            'from_account' => $account->id,
                            'to_account' => $nextMember->gpu_provider_account_id,
                        ]);
                        return $this->provisionWithAccount($nextMember->providerAccount, $requirements, $template, $pool);
                    }
                }

                return $result;
            }

            // Create instance record
            $instance = $this->createInstanceRecord($account, $pool, $offer, $result, $config, $template);

            // Record success
            $this->rotationService->recordSuccess($member, $offer['price_per_hour'] ?? 0);

            GpuActivityLog::logInstanceCreated($instance);

            return [
                'success' => true,
                'instance' => $instance,
                'offer' => $offer,
                'account_used' => $account->name,
            ];

        } catch (\Exception $e) {
            Log::error('GPU provisioning error', [
                'pool_id' => $pool->id,
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);

            $this->rotationService->recordFailure($member, $e->getMessage());

            return [
                'success' => false,
                'error' => $e->getMessage(),
            ];
        }
    }

    /**
     * Provision with a specific account
     */
    public function provisionWithAccount(
        GpuProviderAccount $account,
        array $requirements = [],
        ?GpuSetupTemplate $template = null,
        ?GpuAccountPool $pool = null
    ): array {
        $provider = $this->providerFactory->getProviderForAccount($account);

        try {
            $filters = $this->buildSearchFilters($requirements, $template, $pool);
            $offers = $provider->searchOffers($account, $filters);

            if (empty($offers)) {
                return [
                    'success' => false,
                    'error' => 'No suitable GPU offers found',
                ];
            }

            $offer = $offers[0];
            $config = $this->buildInstanceConfig($offer, $requirements, $template);

            $result = $provider->createInstance($account, $config);

            if (!$result['success']) {
                return $result;
            }

            $instance = $this->createInstanceRecord($account, $pool, $offer, $result, $config, $template);

            GpuActivityLog::logInstanceCreated($instance);

            return [
                'success' => true,
                'instance' => $instance,
                'offer' => $offer,
            ];

        } catch (\Exception $e) {
            Log::error('GPU provisioning error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);

            return [
                'success' => false,
                'error' => $e->getMessage(),
            ];
        }
    }

    /**
     * Search available GPU offers across all accounts in a pool
     */
    public function searchOffersFromPool(GpuAccountPool $pool, array $filters = []): array
    {
        $allOffers = [];
        $errors = [];

        foreach ($pool->members()->with('providerAccount')->get() as $member) {
            $account = $member->providerAccount;
            if (!$account || !$account->is_active) {
                continue;
            }

            try {
                $provider = $this->providerFactory->getProviderForAccount($account);
                $offers = $provider->searchOffers($account, $filters);

                foreach ($offers as $offer) {
                    $offer['account_id'] = $account->id;
                    $offer['account_name'] = $account->name;
                    $offer['account_credits'] = $account->credits_balance;
                    $allOffers[] = $offer;
                }
            } catch (\Exception $e) {
                $errors[$account->id] = $e->getMessage();
            }
        }

        // Sort by price
        usort($allOffers, fn ($a, $b) => ($a['price_per_hour'] ?? PHP_FLOAT_MAX) <=> ($b['price_per_hour'] ?? PHP_FLOAT_MAX));

        return [
            'offers' => $allOffers,
            'errors' => $errors,
        ];
    }

    /**
     * Start an instance
     */
    public function startInstance(GpuInstance $instance): array
    {
        $account = $instance->providerAccount;
        $provider = $this->providerFactory->getProviderForAccount($account);

        try {
            $instance->markAsStarting();

            $success = $provider->startInstance($account, $instance->instance_id);

            if ($success) {
                // Poll for running status
                $status = $provider->getInstanceStatus($account, $instance->instance_id);

                if ($status['success']) {
                    $instance->markAsRunning(
                        $status['ip_address'] ?? null,
                        $status['ssh_port'] ?? null
                    );
                    GpuActivityLog::logInstanceStarted($instance);
                }

                return ['success' => true, 'instance' => $instance->fresh()];
            }

            return ['success' => false, 'error' => 'Failed to start instance'];

        } catch (\Exception $e) {
            $instance->markAsError($e->getMessage());
            GpuActivityLog::logInstanceError($instance, $e->getMessage());

            return ['success' => false, 'error' => $e->getMessage()];
        }
    }

    /**
     * Stop an instance
     */
    public function stopInstance(GpuInstance $instance): array
    {
        $account = $instance->providerAccount;
        $provider = $this->providerFactory->getProviderForAccount($account);

        try {
            $instance->markAsStopping();

            $success = $provider->stopInstance($account, $instance->instance_id);

            if ($success) {
                $instance->markAsStopped();
                GpuActivityLog::logInstanceStopped($instance);

                // Deduct cost from account
                $account->recordUsage($instance->getCurrentCost());

                return ['success' => true, 'instance' => $instance->fresh()];
            }

            return ['success' => false, 'error' => 'Failed to stop instance'];

        } catch (\Exception $e) {
            $instance->markAsError($e->getMessage());
            GpuActivityLog::logInstanceError($instance, $e->getMessage());

            return ['success' => false, 'error' => $e->getMessage()];
        }
    }

    /**
     * Terminate/destroy an instance
     */
    public function terminateInstance(GpuInstance $instance): array
    {
        $account = $instance->providerAccount;
        $provider = $this->providerFactory->getProviderForAccount($account);

        try {
            $success = $provider->terminateInstance($account, $instance->instance_id);

            if ($success) {
                $instance->markAsTerminated();
                GpuActivityLog::logInstanceTerminated($instance);

                return ['success' => true, 'instance' => $instance->fresh()];
            }

            return ['success' => false, 'error' => 'Failed to terminate instance'];

        } catch (\Exception $e) {
            GpuActivityLog::logInstanceError($instance, $e->getMessage());
            return ['success' => false, 'error' => $e->getMessage()];
        }
    }

    /**
     * Sync instance status from provider
     */
    public function syncInstanceStatus(GpuInstance $instance): array
    {
        $account = $instance->providerAccount;
        $provider = $this->providerFactory->getProviderForAccount($account);

        try {
            $status = $provider->getInstanceStatus($account, $instance->instance_id);

            if (!$status['success']) {
                return $status;
            }

            $instance->update([
                'status' => $status['status'],
                'ip_address' => $status['ip_address'] ?? $instance->ip_address,
                'ssh_port' => $status['ssh_port'] ?? $instance->ssh_port,
            ]);

            // Calculate runtime if running
            if ($instance->isRunning()) {
                $instance->calculateRuntime();
            }

            return ['success' => true, 'instance' => $instance->fresh(), 'raw_status' => $status];

        } catch (\Exception $e) {
            return ['success' => false, 'error' => $e->getMessage()];
        }
    }

    /**
     * Run setup script on an instance
     */
    public function runSetup(GpuInstance $instance, ?GpuSetupTemplate $template = null): array
    {
        if (!$instance->isRunning()) {
            return ['success' => false, 'error' => 'Instance is not running'];
        }

        $template = $template ?? GpuSetupTemplate::where('user_id', $instance->user_id)
            ->where('is_default', true)
            ->first();

        if (!$template) {
            return ['success' => false, 'error' => 'No setup template available'];
        }

        try {
            $instance->updateSetupStatus(GpuInstance::SETUP_RUNNING, 'Starting setup...');
            GpuActivityLog::logSetupStarted($instance);

            $setupScript = $template->getFullSetupScript();

            // Store setup script for the instance
            $instance->update([
                'setup_script' => [
                    'template_id' => $template->id,
                    'script' => $setupScript,
                ],
            ]);

            // Note: Actual SSH execution would need to be implemented
            // This is a placeholder - you'd use phpseclib or similar
            $instance->appendSetupLog("Setup script prepared. Execute via SSH:\n" . $instance->getSshCommand());
            $instance->appendSetupLog("\nScript content saved. Run manually or use automation.");

            // For now, mark as pending manual execution
            $instance->updateSetupStatus(GpuInstance::SETUP_PENDING, 'Awaiting manual execution');

            return [
                'success' => true,
                'message' => 'Setup script prepared',
                'ssh_command' => $instance->getSshCommand(),
                'script' => $setupScript,
            ];

        } catch (\Exception $e) {
            $instance->updateSetupStatus(GpuInstance::SETUP_FAILED, 'Error: ' . $e->getMessage());
            GpuActivityLog::logSetupFailed($instance, $e->getMessage());

            return ['success' => false, 'error' => $e->getMessage()];
        }
    }

    /**
     * Get all active instances for a user
     */
    public function getActiveInstances(User $user): array
    {
        return GpuInstance::where('user_id', $user->id)
            ->active()
            ->with('providerAccount')
            ->get()
            ->toArray();
    }

    /**
     * Sync all instances for a user
     */
    public function syncAllInstances(User $user): array
    {
        $results = [];

        $instances = GpuInstance::where('user_id', $user->id)
            ->whereIn('status', [
                GpuInstance::STATUS_PENDING,
                GpuInstance::STATUS_STARTING,
                GpuInstance::STATUS_RUNNING,
            ])
            ->with('providerAccount')
            ->get();

        foreach ($instances as $instance) {
            $results[$instance->id] = $this->syncInstanceStatus($instance);
        }

        return $results;
    }

    /**
     * Terminate all instances for a pool
     */
    public function terminateAllInPool(GpuAccountPool $pool): array
    {
        $results = [];

        $instances = GpuInstance::where('gpu_account_pool_id', $pool->id)
            ->active()
            ->get();

        foreach ($instances as $instance) {
            $results[$instance->id] = $this->terminateInstance($instance);
        }

        return $results;
    }

    // Private helper methods

    private function buildSearchFilters(array $requirements, ?GpuSetupTemplate $template, ?GpuAccountPool $pool): array
    {
        $filters = [];

        // From requirements
        if (!empty($requirements['gpu_type'])) {
            $filters['gpu_type'] = $requirements['gpu_type'];
        }
        if (!empty($requirements['min_gpu_memory_gb'])) {
            $filters['min_gpu_memory_gb'] = $requirements['min_gpu_memory_gb'];
        }
        if (!empty($requirements['min_cpu_cores'])) {
            $filters['min_cpu_cores'] = $requirements['min_cpu_cores'];
        }
        if (!empty($requirements['min_ram_gb'])) {
            $filters['min_ram_gb'] = $requirements['min_ram_gb'];
        }
        if (!empty($requirements['min_disk_gb'])) {
            $filters['min_disk_gb'] = $requirements['min_disk_gb'];
        }
        if (!empty($requirements['max_price_per_hour'])) {
            $filters['max_price_per_hour'] = $requirements['max_price_per_hour'];
        }
        if (!empty($requirements['region'])) {
            $filters['region'] = $requirements['region'];
        }

        // From template
        if ($template) {
            if (!empty($template->min_gpu_memory_gb) && empty($filters['min_gpu_memory_gb'])) {
                $filters['min_gpu_memory_gb'] = $template->min_gpu_memory_gb;
            }
            if (!empty($template->min_cpu_cores) && empty($filters['min_cpu_cores'])) {
                $filters['min_cpu_cores'] = $template->min_cpu_cores;
            }
            if (!empty($template->min_ram_gb) && empty($filters['min_ram_gb'])) {
                $filters['min_ram_gb'] = $template->min_ram_gb;
            }
            if (!empty($template->min_disk_gb) && empty($filters['min_disk_gb'])) {
                $filters['min_disk_gb'] = $template->min_disk_gb;
            }
        }

        // From pool preferences
        if ($pool) {
            if (!empty($pool->max_price_per_hour) && empty($filters['max_price_per_hour'])) {
                $filters['max_price_per_hour'] = $pool->max_price_per_hour;
            }
            if (!empty($pool->preferred_regions) && empty($filters['region'])) {
                $filters['region'] = $pool->preferred_regions[0] ?? null;
            }
        }

        return $filters;
    }

    private function buildInstanceConfig(array $offer, array $requirements, ?GpuSetupTemplate $template): array
    {
        $config = [
            'offer_id' => $offer['offer_id'],
            'gpu_type_id' => $offer['gpu_type_id'] ?? $offer['offer_id'],
            'disk_gb' => $requirements['disk_gb'] ?? $template->min_disk_gb ?? 50,
            'name' => $requirements['name'] ?? 'postxagent-' . time(),
        ];

        // Docker image
        if (!empty($requirements['docker_image'])) {
            $config['docker_image'] = $requirements['docker_image'];
        } elseif ($template?->docker_image) {
            $config['docker_image'] = $template->docker_image;
        }

        // Environment variables
        $envVars = $template->environment_vars ?? [];
        if (!empty($requirements['environment_vars'])) {
            $envVars = array_merge($envVars, $requirements['environment_vars']);
        }
        if (!empty($envVars)) {
            $config['environment_vars'] = $envVars;
        }

        // Startup script
        if ($template?->setup_script) {
            $config['onstart_script'] = $template->getFullSetupScript();
        }

        return $config;
    }

    private function createInstanceRecord(
        GpuProviderAccount $account,
        ?GpuAccountPool $pool,
        array $offer,
        array $result,
        array $config,
        ?GpuSetupTemplate $template
    ): GpuInstance {
        return GpuInstance::create([
            'gpu_provider_account_id' => $account->id,
            'gpu_account_pool_id' => $pool?->id,
            'user_id' => $account->user_id,
            'provider' => $account->provider,
            'instance_id' => $result['instance_id'],
            'instance_name' => $result['instance_name'] ?? $config['name'] ?? null,
            'status' => GpuInstance::STATUS_PENDING,
            'gpu_type' => $offer['gpu_type'] ?? null,
            'gpu_count' => $offer['gpu_count'] ?? 1,
            'cpu_cores' => $offer['cpu_cores'] ?? null,
            'ram_gb' => $offer['ram_gb'] ?? null,
            'disk_gb' => $config['disk_gb'] ?? null,
            'region' => $offer['region'] ?? null,
            'price_per_hour' => $offer['price_per_hour'] ?? null,
            'docker_image' => $config['docker_image'] ?? null,
            'environment_vars' => $config['environment_vars'] ?? null,
            'setup_script' => $template ? [
                'template_id' => $template->id,
                'template_name' => $template->name,
            ] : null,
            'setup_status' => GpuInstance::SETUP_PENDING,
            'metadata' => [
                'offer' => $offer,
                'config' => $config,
                'provider_response' => $result['raw_response'] ?? null,
            ],
        ]);
    }
}
