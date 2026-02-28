<?php

declare(strict_types=1);

namespace App\Services\Gpu;

use App\Models\GpuProviderAccount;
use InvalidArgumentException;

/**
 * Factory for creating GPU provider instances
 */
class GpuProviderFactory
{
    private array $providers = [];

    public function __construct()
    {
        // Register default providers
        $this->registerProvider(new VastAiProvider());
        $this->registerProvider(new RunPodProvider());
    }

    /**
     * Register a provider implementation
     */
    public function registerProvider(GpuProviderInterface $provider): void
    {
        $this->providers[$provider->getProviderName()] = $provider;
    }

    /**
     * Get provider by name
     */
    public function getProvider(string $providerName): GpuProviderInterface
    {
        if (!isset($this->providers[$providerName])) {
            throw new InvalidArgumentException("Unknown GPU provider: {$providerName}");
        }

        return $this->providers[$providerName];
    }

    /**
     * Get provider for a specific account
     */
    public function getProviderForAccount(GpuProviderAccount $account): GpuProviderInterface
    {
        return $this->getProvider($account->provider);
    }

    /**
     * Get all registered provider names
     */
    public function getAvailableProviders(): array
    {
        return array_keys($this->providers);
    }

    /**
     * Check if a provider is registered
     */
    public function hasProvider(string $providerName): bool
    {
        return isset($this->providers[$providerName]);
    }

    /**
     * Get all registered providers
     */
    public function getAllProviders(): array
    {
        return $this->providers;
    }
}
