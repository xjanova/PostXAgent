<?php

declare(strict_types=1);

namespace App\Services\Gpu;

use App\Models\GpuProviderAccount;
use App\Models\GpuInstance;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use Illuminate\Http\Client\PendingRequest;

/**
 * Vast.ai GPU provider implementation
 * API Documentation: https://docs.vast.ai/api/overview-and-quickstart
 */
class VastAiProvider implements GpuProviderInterface
{
    private const API_BASE_URL = 'https://console.vast.ai/api/v0';

    public function getProviderName(): string
    {
        return 'vast_ai';
    }

    /**
     * Get HTTP client with authentication
     */
    private function getClient(GpuProviderAccount $account): PendingRequest
    {
        return Http::baseUrl(self::API_BASE_URL)
            ->withHeaders([
                'Accept' => 'application/json',
                'Content-Type' => 'application/json',
            ])
            ->withBasicAuth($account->getDecryptedApiKey(), '');
    }

    public function validateCredentials(string $apiKey, ?string $apiSecret = null): bool
    {
        try {
            $response = Http::baseUrl(self::API_BASE_URL)
                ->withBasicAuth($apiKey, '')
                ->get('/users/current');

            return $response->successful();
        } catch (\Exception $e) {
            Log::error('Vast.ai credential validation failed', [
                'error' => $e->getMessage(),
            ]);
            return false;
        }
    }

    public function getBalance(GpuProviderAccount $account): float
    {
        try {
            $response = $this->getClient($account)->get('/users/current');

            if ($response->successful()) {
                $data = $response->json();
                return (float) ($data['credit'] ?? 0);
            }

            Log::warning('Vast.ai balance check failed', [
                'account_id' => $account->id,
                'status' => $response->status(),
            ]);
            return 0;
        } catch (\Exception $e) {
            Log::error('Vast.ai balance check error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return 0;
        }
    }

    public function searchOffers(GpuProviderAccount $account, array $filters = []): array
    {
        try {
            // Build query parameters
            $query = $this->buildSearchQuery($filters);

            $response = $this->getClient($account)->get('/bundles', ['q' => $query]);

            if ($response->successful()) {
                $offers = $response->json('offers') ?? [];
                return $this->normalizeOffers($offers);
            }

            Log::warning('Vast.ai search offers failed', [
                'account_id' => $account->id,
                'status' => $response->status(),
            ]);
            return [];
        } catch (\Exception $e) {
            Log::error('Vast.ai search offers error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return [];
        }
    }

    public function createInstance(GpuProviderAccount $account, array $config): array
    {
        try {
            $offerId = $config['offer_id'] ?? throw new \InvalidArgumentException('offer_id is required');
            $image = $config['docker_image'] ?? 'pytorch/pytorch:2.1.0-cuda12.1-cudnn8-runtime';

            $payload = [
                'client_id' => 'me',
                'image' => $image,
                'disk' => $config['disk_gb'] ?? 20,
                'onstart' => $config['onstart_script'] ?? null,
                'env' => $config['environment_vars'] ?? [],
            ];

            // Add SSH public key if provided
            if (!empty($config['ssh_public_key'])) {
                $payload['ssh_key'] = $config['ssh_public_key'];
            }

            $response = $this->getClient($account)->put("/asks/{$offerId}/", $payload);

            if ($response->successful()) {
                $data = $response->json();
                return [
                    'success' => true,
                    'instance_id' => (string) $data['new_contract'],
                    'status' => 'pending',
                    'raw_response' => $data,
                ];
            }

            return [
                'success' => false,
                'error' => $response->json('msg') ?? 'Failed to create instance',
                'status_code' => $response->status(),
            ];
        } catch (\Exception $e) {
            Log::error('Vast.ai create instance error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return [
                'success' => false,
                'error' => $e->getMessage(),
            ];
        }
    }

    public function startInstance(GpuProviderAccount $account, string $instanceId): bool
    {
        try {
            $response = $this->getClient($account)->put("/instances/{$instanceId}/", [
                'state' => 'running',
            ]);

            return $response->successful();
        } catch (\Exception $e) {
            Log::error('Vast.ai start instance error', [
                'account_id' => $account->id,
                'instance_id' => $instanceId,
                'error' => $e->getMessage(),
            ]);
            return false;
        }
    }

    public function stopInstance(GpuProviderAccount $account, string $instanceId): bool
    {
        try {
            $response = $this->getClient($account)->put("/instances/{$instanceId}/", [
                'state' => 'stopped',
            ]);

            return $response->successful();
        } catch (\Exception $e) {
            Log::error('Vast.ai stop instance error', [
                'account_id' => $account->id,
                'instance_id' => $instanceId,
                'error' => $e->getMessage(),
            ]);
            return false;
        }
    }

    public function terminateInstance(GpuProviderAccount $account, string $instanceId): bool
    {
        try {
            $response = $this->getClient($account)->delete("/instances/{$instanceId}/");

            return $response->successful();
        } catch (\Exception $e) {
            Log::error('Vast.ai terminate instance error', [
                'account_id' => $account->id,
                'instance_id' => $instanceId,
                'error' => $e->getMessage(),
            ]);
            return false;
        }
    }

    public function getInstanceStatus(GpuProviderAccount $account, string $instanceId): array
    {
        try {
            $response = $this->getClient($account)->get("/instances/{$instanceId}");

            if ($response->successful()) {
                $data = $response->json();
                return $this->normalizeInstanceStatus($data);
            }

            return [
                'success' => false,
                'error' => 'Failed to get instance status',
            ];
        } catch (\Exception $e) {
            Log::error('Vast.ai get instance status error', [
                'account_id' => $account->id,
                'instance_id' => $instanceId,
                'error' => $e->getMessage(),
            ]);
            return [
                'success' => false,
                'error' => $e->getMessage(),
            ];
        }
    }

    public function listInstances(GpuProviderAccount $account): array
    {
        try {
            $response = $this->getClient($account)->get('/instances', ['owner' => 'me']);

            if ($response->successful()) {
                $instances = $response->json('instances') ?? [];
                return array_map(fn ($i) => $this->normalizeInstanceStatus($i), $instances);
            }

            return [];
        } catch (\Exception $e) {
            Log::error('Vast.ai list instances error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return [];
        }
    }

    public function executeCommand(GpuInstance $instance, string $command): array
    {
        // Vast.ai doesn't have direct command execution API
        // You need to SSH into the instance
        // This is a placeholder - actual implementation would use SSH
        return [
            'success' => false,
            'error' => 'Direct command execution not supported. Use SSH.',
            'ssh_command' => $instance->getSshCommand(),
        ];
    }

    public function uploadFile(GpuInstance $instance, string $localPath, string $remotePath): bool
    {
        // Vast.ai doesn't have direct file upload API
        // You need to use SCP/SFTP
        return false;
    }

    public function getAvailableGpuTypes(): array
    {
        return [
            'RTX_4090' => ['name' => 'NVIDIA RTX 4090', 'memory_gb' => 24],
            'RTX_3090' => ['name' => 'NVIDIA RTX 3090', 'memory_gb' => 24],
            'RTX_3080' => ['name' => 'NVIDIA RTX 3080', 'memory_gb' => 10],
            'RTX_3070' => ['name' => 'NVIDIA RTX 3070', 'memory_gb' => 8],
            'A100_PCIE' => ['name' => 'NVIDIA A100 PCIe', 'memory_gb' => 40],
            'A100_SXM4' => ['name' => 'NVIDIA A100 SXM4', 'memory_gb' => 80],
            'A6000' => ['name' => 'NVIDIA RTX A6000', 'memory_gb' => 48],
            'A5000' => ['name' => 'NVIDIA RTX A5000', 'memory_gb' => 24],
            'A4000' => ['name' => 'NVIDIA RTX A4000', 'memory_gb' => 16],
            'H100_PCIE' => ['name' => 'NVIDIA H100 PCIe', 'memory_gb' => 80],
            'H100_SXM5' => ['name' => 'NVIDIA H100 SXM5', 'memory_gb' => 80],
            'L40S' => ['name' => 'NVIDIA L40S', 'memory_gb' => 48],
        ];
    }

    public function getAvailableRegions(): array
    {
        return [
            'any' => 'Any Region',
            'NA' => 'North America',
            'EU' => 'Europe',
            'AS' => 'Asia',
        ];
    }

    /**
     * Build Vast.ai search query string
     */
    private function buildSearchQuery(array $filters): string
    {
        $query = [];

        // GPU type filter
        if (!empty($filters['gpu_type'])) {
            $query[] = "gpu_name=" . $filters['gpu_type'];
        }

        // Minimum GPU memory
        if (!empty($filters['min_gpu_memory_gb'])) {
            $query[] = "gpu_ram>=" . ($filters['min_gpu_memory_gb'] * 1024); // Convert to MB
        }

        // Maximum price per hour
        if (!empty($filters['max_price_per_hour'])) {
            $query[] = "dph_total<=" . $filters['max_price_per_hour'];
        }

        // Minimum CPU cores
        if (!empty($filters['min_cpu_cores'])) {
            $query[] = "cpu_cores>=" . $filters['min_cpu_cores'];
        }

        // Minimum RAM
        if (!empty($filters['min_ram_gb'])) {
            $query[] = "cpu_ram>=" . ($filters['min_ram_gb'] * 1024); // Convert to MB
        }

        // Minimum disk space
        if (!empty($filters['min_disk_gb'])) {
            $query[] = "disk_space>=" . $filters['min_disk_gb'];
        }

        // Region filter
        if (!empty($filters['region']) && $filters['region'] !== 'any') {
            $query[] = "geolocation=" . $filters['region'];
        }

        // Verified hosts only
        if ($filters['verified_only'] ?? true) {
            $query[] = "verified=true";
        }

        // CUDA version
        if (!empty($filters['cuda_version'])) {
            $query[] = "cuda_vers>=" . $filters['cuda_version'];
        }

        // Reliability score
        $query[] = "reliability>=0.95";

        // Order by price
        $query[] = "order=dph_total";

        return implode(' ', $query);
    }

    /**
     * Normalize offer data to a standard format
     */
    private function normalizeOffers(array $offers): array
    {
        return array_map(function ($offer) {
            return [
                'offer_id' => (string) $offer['id'],
                'provider' => 'vast_ai',
                'gpu_type' => $offer['gpu_name'] ?? 'Unknown',
                'gpu_count' => $offer['num_gpus'] ?? 1,
                'gpu_memory_gb' => round(($offer['gpu_ram'] ?? 0) / 1024, 1),
                'cpu_cores' => $offer['cpu_cores'] ?? 0,
                'ram_gb' => round(($offer['cpu_ram'] ?? 0) / 1024, 1),
                'disk_gb' => $offer['disk_space'] ?? 0,
                'price_per_hour' => $offer['dph_total'] ?? 0,
                'region' => $offer['geolocation'] ?? 'Unknown',
                'reliability' => $offer['reliability'] ?? 0,
                'verified' => $offer['verified'] ?? false,
                'cuda_version' => $offer['cuda_max_good'] ?? null,
                'internet_up_mbps' => $offer['inet_up'] ?? 0,
                'internet_down_mbps' => $offer['inet_down'] ?? 0,
                'raw_data' => $offer,
            ];
        }, $offers);
    }

    /**
     * Normalize instance status to a standard format
     */
    private function normalizeInstanceStatus(array $data): array
    {
        // Map Vast.ai status to our standard status
        $statusMap = [
            'running' => GpuInstance::STATUS_RUNNING,
            'starting' => GpuInstance::STATUS_STARTING,
            'stopped' => GpuInstance::STATUS_STOPPED,
            'stopping' => GpuInstance::STATUS_STOPPING,
            'exited' => GpuInstance::STATUS_STOPPED,
            'created' => GpuInstance::STATUS_PENDING,
            'loading' => GpuInstance::STATUS_STARTING,
        ];

        $vastStatus = strtolower($data['actual_status'] ?? $data['intended_status'] ?? 'unknown');
        $status = $statusMap[$vastStatus] ?? GpuInstance::STATUS_ERROR;

        return [
            'success' => true,
            'instance_id' => (string) $data['id'],
            'provider' => 'vast_ai',
            'status' => $status,
            'vast_status' => $vastStatus,
            'gpu_type' => $data['gpu_name'] ?? null,
            'gpu_count' => $data['num_gpus'] ?? 1,
            'cpu_cores' => $data['cpu_cores'] ?? null,
            'ram_gb' => isset($data['cpu_ram']) ? round($data['cpu_ram'] / 1024, 1) : null,
            'disk_gb' => $data['disk_space'] ?? null,
            'ip_address' => $data['public_ipaddr'] ?? null,
            'ssh_port' => $data['ssh_port'] ?? null,
            'ssh_host' => $data['ssh_host'] ?? null,
            'price_per_hour' => $data['dph_total'] ?? null,
            'docker_image' => $data['image_uuid'] ?? null,
            'start_date' => $data['start_date'] ?? null,
            'raw_data' => $data,
        ];
    }
}
