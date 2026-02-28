<?php

declare(strict_types=1);

namespace App\Services\Gpu;

use App\Models\GpuProviderAccount;
use App\Models\GpuInstance;

/**
 * Interface for GPU cloud provider implementations
 */
interface GpuProviderInterface
{
    /**
     * Get the provider name
     */
    public function getProviderName(): string;

    /**
     * Test if the API credentials are valid
     */
    public function validateCredentials(string $apiKey, ?string $apiSecret = null): bool;

    /**
     * Get the current account balance/credits
     */
    public function getBalance(GpuProviderAccount $account): float;

    /**
     * Search for available GPU offers
     *
     * @param array $filters Filters like gpu_type, min_ram, max_price, etc.
     * @return array List of available offers
     */
    public function searchOffers(GpuProviderAccount $account, array $filters = []): array;

    /**
     * Create/rent a new GPU instance
     *
     * @param array $config Instance configuration
     * @return array Instance details from provider
     */
    public function createInstance(GpuProviderAccount $account, array $config): array;

    /**
     * Start a stopped instance
     */
    public function startInstance(GpuProviderAccount $account, string $instanceId): bool;

    /**
     * Stop a running instance
     */
    public function stopInstance(GpuProviderAccount $account, string $instanceId): bool;

    /**
     * Terminate/destroy an instance
     */
    public function terminateInstance(GpuProviderAccount $account, string $instanceId): bool;

    /**
     * Get instance status and details
     */
    public function getInstanceStatus(GpuProviderAccount $account, string $instanceId): array;

    /**
     * Get all instances for the account
     */
    public function listInstances(GpuProviderAccount $account): array;

    /**
     * Execute a command on the instance via SSH
     */
    public function executeCommand(GpuInstance $instance, string $command): array;

    /**
     * Upload a file to the instance
     */
    public function uploadFile(GpuInstance $instance, string $localPath, string $remotePath): bool;

    /**
     * Get available GPU types for this provider
     */
    public function getAvailableGpuTypes(): array;

    /**
     * Get available regions for this provider
     */
    public function getAvailableRegions(): array;
}
