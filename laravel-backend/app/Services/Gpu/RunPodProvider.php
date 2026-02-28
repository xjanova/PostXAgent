<?php

declare(strict_types=1);

namespace App\Services\Gpu;

use App\Models\GpuProviderAccount;
use App\Models\GpuInstance;
use Illuminate\Support\Facades\Http;
use Illuminate\Support\Facades\Log;
use Illuminate\Http\Client\PendingRequest;

/**
 * RunPod GPU provider implementation
 * API Documentation: https://docs.runpod.io/sdks/python/apis
 * GraphQL API: https://api.runpod.io/graphql
 */
class RunPodProvider implements GpuProviderInterface
{
    private const API_BASE_URL = 'https://api.runpod.io';
    private const GRAPHQL_URL = 'https://api.runpod.io/graphql';

    public function getProviderName(): string
    {
        return 'runpod';
    }

    /**
     * Get HTTP client with authentication
     *
     * @phpstan-ignore method.unused
     */
    private function getClient(GpuProviderAccount $account): PendingRequest
    {
        return Http::baseUrl(self::API_BASE_URL)
            ->withHeaders([
                'Accept' => 'application/json',
                'Content-Type' => 'application/json',
                'Authorization' => 'Bearer ' . $account->getDecryptedApiKey(),
            ]);
    }

    /**
     * Execute GraphQL query
     */
    private function graphql(GpuProviderAccount $account, string $query, array $variables = []): array
    {
        $response = Http::withHeaders([
            'Accept' => 'application/json',
            'Content-Type' => 'application/json',
            'Authorization' => 'Bearer ' . $account->getDecryptedApiKey(),
        ])->post(self::GRAPHQL_URL, [
            'query' => $query,
            'variables' => $variables,
        ]);

        if ($response->successful()) {
            return $response->json();
        }

        throw new \Exception('GraphQL request failed: ' . $response->body());
    }

    public function validateCredentials(string $apiKey, ?string $apiSecret = null): bool
    {
        try {
            $query = '{ myself { id email } }';

            $response = Http::withHeaders([
                'Accept' => 'application/json',
                'Content-Type' => 'application/json',
                'Authorization' => 'Bearer ' . $apiKey,
            ])->post(self::GRAPHQL_URL, ['query' => $query]);

            if ($response->successful()) {
                $data = $response->json();
                return isset($data['data']['myself']['id']);
            }

            return false;
        } catch (\Exception $e) {
            Log::error('RunPod credential validation failed', [
                'error' => $e->getMessage(),
            ]);
            return false;
        }
    }

    public function getBalance(GpuProviderAccount $account): float
    {
        try {
            $query = '{ myself { currentSpendPerHr creditBalance } }';
            $result = $this->graphql($account, $query);

            return (float) ($result['data']['myself']['creditBalance'] ?? 0);
        } catch (\Exception $e) {
            Log::error('RunPod balance check error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return 0;
        }
    }

    public function searchOffers(GpuProviderAccount $account, array $filters = []): array
    {
        try {
            $query = <<<'GRAPHQL'
            query GpuTypes {
                gpuTypes {
                    id
                    displayName
                    memoryInGb
                    secureCloud
                    communityCloud
                    lowestPrice(input: { gpuCount: 1 }) {
                        minimumBidPrice
                        uninterruptablePrice
                    }
                }
            }
            GRAPHQL;

            $result = $this->graphql($account, $query);
            $gpuTypes = $result['data']['gpuTypes'] ?? [];

            return $this->normalizeOffers($gpuTypes, $filters);
        } catch (\Exception $e) {
            Log::error('RunPod search offers error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return [];
        }
    }

    public function createInstance(GpuProviderAccount $account, array $config): array
    {
        try {
            $gpuTypeId = $config['gpu_type_id'] ?? throw new \InvalidArgumentException('gpu_type_id is required');
            $image = $config['docker_image'] ?? 'runpod/pytorch:2.1.0-py3.10-cuda12.1.0-devel-ubuntu22.04';

            $mutation = <<<'GRAPHQL'
            mutation CreatePod($input: PodFindAndDeployOnDemandInput!) {
                podFindAndDeployOnDemand(input: $input) {
                    id
                    name
                    imageName
                    machineId
                    machine {
                        podHostId
                    }
                }
            }
            GRAPHQL;

            $variables = [
                'input' => [
                    'cloudType' => $config['cloud_type'] ?? 'ALL',
                    'gpuCount' => $config['gpu_count'] ?? 1,
                    'volumeInGb' => $config['disk_gb'] ?? 20,
                    'containerDiskInGb' => $config['container_disk_gb'] ?? 10,
                    'gpuTypeId' => $gpuTypeId,
                    'name' => $config['name'] ?? 'postxagent-' . time(),
                    'imageName' => $image,
                    'startSsh' => true,
                    'supportPublicIp' => true,
                    'volumeMountPath' => '/workspace',
                ],
            ];

            // Add environment variables
            if (!empty($config['environment_vars'])) {
                $envVars = [];
                foreach ($config['environment_vars'] as $key => $value) {
                    $envVars[] = ['key' => $key, 'value' => $value];
                }
                $variables['input']['env'] = $envVars;
            }

            // Add startup script (docker args)
            if (!empty($config['onstart_script'])) {
                $variables['input']['dockerArgs'] = $config['onstart_script'];
            }

            $result = $this->graphql($account, $mutation, $variables);

            if (isset($result['data']['podFindAndDeployOnDemand']['id'])) {
                $pod = $result['data']['podFindAndDeployOnDemand'];
                return [
                    'success' => true,
                    'instance_id' => $pod['id'],
                    'instance_name' => $pod['name'],
                    'status' => 'pending',
                    'raw_response' => $pod,
                ];
            }

            return [
                'success' => false,
                'error' => $result['errors'][0]['message'] ?? 'Failed to create pod',
                'raw_response' => $result,
            ];
        } catch (\Exception $e) {
            Log::error('RunPod create instance error', [
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
            $mutation = <<<'GRAPHQL'
            mutation ResumePod($input: PodResumeInput!) {
                podResume(input: $input) {
                    id
                    desiredStatus
                }
            }
            GRAPHQL;

            $result = $this->graphql($account, $mutation, [
                'input' => ['podId' => $instanceId],
            ]);

            return isset($result['data']['podResume']['id']);
        } catch (\Exception $e) {
            Log::error('RunPod start instance error', [
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
            $mutation = <<<'GRAPHQL'
            mutation StopPod($input: PodStopInput!) {
                podStop(input: $input) {
                    id
                    desiredStatus
                }
            }
            GRAPHQL;

            $result = $this->graphql($account, $mutation, [
                'input' => ['podId' => $instanceId],
            ]);

            return isset($result['data']['podStop']['id']);
        } catch (\Exception $e) {
            Log::error('RunPod stop instance error', [
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
            $mutation = <<<'GRAPHQL'
            mutation TerminatePod($input: PodTerminateInput!) {
                podTerminate(input: $input)
            }
            GRAPHQL;

            $result = $this->graphql($account, $mutation, [
                'input' => ['podId' => $instanceId],
            ]);

            return $result['data']['podTerminate'] ?? false;
        } catch (\Exception $e) {
            Log::error('RunPod terminate instance error', [
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
            $query = <<<'GRAPHQL'
            query Pod($input: PodFilter!) {
                pod(input: $input) {
                    id
                    name
                    imageName
                    desiredStatus
                    lastStatusChange
                    runtime {
                        uptimeInSeconds
                        ports {
                            ip
                            isIpPublic
                            privatePort
                            publicPort
                            type
                        }
                        gpus {
                            id
                            gpuUtilPercent
                            memoryUtilPercent
                        }
                    }
                    machine {
                        gpuDisplayName
                        podHostId
                    }
                    gpuCount
                    vcpuCount
                    memoryInGb
                    volumeInGb
                    containerDiskInGb
                    costPerHr
                }
            }
            GRAPHQL;

            $result = $this->graphql($account, $query, [
                'input' => ['podId' => $instanceId],
            ]);

            if (isset($result['data']['pod'])) {
                return $this->normalizeInstanceStatus($result['data']['pod']);
            }

            return [
                'success' => false,
                'error' => 'Pod not found',
            ];
        } catch (\Exception $e) {
            Log::error('RunPod get instance status error', [
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
            $query = <<<'GRAPHQL'
            query Pods {
                myself {
                    pods {
                        id
                        name
                        imageName
                        desiredStatus
                        lastStatusChange
                        runtime {
                            uptimeInSeconds
                            ports {
                                ip
                                isIpPublic
                                privatePort
                                publicPort
                                type
                            }
                        }
                        machine {
                            gpuDisplayName
                        }
                        gpuCount
                        vcpuCount
                        memoryInGb
                        volumeInGb
                        costPerHr
                    }
                }
            }
            GRAPHQL;

            $result = $this->graphql($account, $query);
            $pods = $result['data']['myself']['pods'] ?? [];

            return array_map(fn ($pod) => $this->normalizeInstanceStatus($pod), $pods);
        } catch (\Exception $e) {
            Log::error('RunPod list instances error', [
                'account_id' => $account->id,
                'error' => $e->getMessage(),
            ]);
            return [];
        }
    }

    public function executeCommand(GpuInstance $instance, string $command): array
    {
        // RunPod doesn't have direct command execution API
        // You need to SSH into the instance or use their exec endpoint (if available)
        return [
            'success' => false,
            'error' => 'Direct command execution not supported. Use SSH.',
            'ssh_command' => $instance->getSshCommand(),
        ];
    }

    public function uploadFile(GpuInstance $instance, string $localPath, string $remotePath): bool
    {
        // RunPod doesn't have direct file upload API
        // You need to use SCP/SFTP
        return false;
    }

    public function getAvailableGpuTypes(): array
    {
        return [
            'NVIDIA GeForce RTX 4090' => ['id' => 'NVIDIA GeForce RTX 4090', 'memory_gb' => 24],
            'NVIDIA GeForce RTX 3090' => ['id' => 'NVIDIA GeForce RTX 3090', 'memory_gb' => 24],
            'NVIDIA GeForce RTX 3080' => ['id' => 'NVIDIA GeForce RTX 3080', 'memory_gb' => 10],
            'NVIDIA RTX A6000' => ['id' => 'NVIDIA RTX A6000', 'memory_gb' => 48],
            'NVIDIA RTX A5000' => ['id' => 'NVIDIA RTX A5000', 'memory_gb' => 24],
            'NVIDIA RTX A4000' => ['id' => 'NVIDIA RTX A4000', 'memory_gb' => 16],
            'NVIDIA A100-SXM4-80GB' => ['id' => 'NVIDIA A100-SXM4-80GB', 'memory_gb' => 80],
            'NVIDIA A100-PCIE-40GB' => ['id' => 'NVIDIA A100-PCIE-40GB', 'memory_gb' => 40],
            'NVIDIA H100 80GB HBM3' => ['id' => 'NVIDIA H100 80GB HBM3', 'memory_gb' => 80],
            'NVIDIA L40S' => ['id' => 'NVIDIA L40S', 'memory_gb' => 48],
        ];
    }

    public function getAvailableRegions(): array
    {
        return [
            'ALL' => 'All Regions',
            'US' => 'United States',
            'EU' => 'Europe',
            'CA' => 'Canada',
        ];
    }

    /**
     * Normalize GPU types to standard offer format
     */
    private function normalizeOffers(array $gpuTypes, array $filters = []): array
    {
        $offers = [];

        foreach ($gpuTypes as $gpu) {
            // Apply filters
            if (!empty($filters['max_price_per_hour'])) {
                $price = $gpu['lowestPrice']['uninterruptablePrice'] ?? PHP_FLOAT_MAX;
                if ($price > $filters['max_price_per_hour']) {
                    continue;
                }
            }

            if (!empty($filters['min_gpu_memory_gb'])) {
                if (($gpu['memoryInGb'] ?? 0) < $filters['min_gpu_memory_gb']) {
                    continue;
                }
            }

            $offers[] = [
                'offer_id' => $gpu['id'],
                'provider' => 'runpod',
                'gpu_type' => $gpu['displayName'] ?? $gpu['id'],
                'gpu_type_id' => $gpu['id'],
                'gpu_count' => 1,
                'gpu_memory_gb' => $gpu['memoryInGb'] ?? 0,
                'price_per_hour' => $gpu['lowestPrice']['uninterruptablePrice'] ?? null,
                'spot_price_per_hour' => $gpu['lowestPrice']['minimumBidPrice'] ?? null,
                'secure_cloud' => $gpu['secureCloud'] ?? false,
                'community_cloud' => $gpu['communityCloud'] ?? false,
                'raw_data' => $gpu,
            ];
        }

        // Sort by price
        usort($offers, fn ($a, $b) => ($a['price_per_hour'] ?? PHP_FLOAT_MAX) <=> ($b['price_per_hour'] ?? PHP_FLOAT_MAX));

        return $offers;
    }

    /**
     * Normalize instance status to a standard format
     */
    private function normalizeInstanceStatus(array $data): array
    {
        // Map RunPod status to our standard status
        $statusMap = [
            'RUNNING' => GpuInstance::STATUS_RUNNING,
            'STARTING' => GpuInstance::STATUS_STARTING,
            'STOPPED' => GpuInstance::STATUS_STOPPED,
            'STOPPING' => GpuInstance::STATUS_STOPPING,
            'EXITED' => GpuInstance::STATUS_STOPPED,
            'CREATED' => GpuInstance::STATUS_PENDING,
            'PENDING' => GpuInstance::STATUS_PENDING,
        ];

        $runpodStatus = strtoupper($data['desiredStatus'] ?? 'UNKNOWN');
        $status = $statusMap[$runpodStatus] ?? GpuInstance::STATUS_ERROR;

        // Extract SSH connection info from ports
        $sshPort = null;
        $ipAddress = null;
        if (!empty($data['runtime']['ports'])) {
            foreach ($data['runtime']['ports'] as $port) {
                if ($port['privatePort'] === 22 && $port['isIpPublic']) {
                    $sshPort = $port['publicPort'];
                    $ipAddress = $port['ip'];
                    break;
                }
            }
        }

        return [
            'success' => true,
            'instance_id' => $data['id'],
            'instance_name' => $data['name'] ?? null,
            'provider' => 'runpod',
            'status' => $status,
            'runpod_status' => $runpodStatus,
            'gpu_type' => $data['machine']['gpuDisplayName'] ?? null,
            'gpu_count' => $data['gpuCount'] ?? 1,
            'cpu_cores' => $data['vcpuCount'] ?? null,
            'ram_gb' => $data['memoryInGb'] ?? null,
            'disk_gb' => ($data['volumeInGb'] ?? 0) + ($data['containerDiskInGb'] ?? 0),
            'ip_address' => $ipAddress,
            'ssh_port' => $sshPort,
            'price_per_hour' => $data['costPerHr'] ?? null,
            'docker_image' => $data['imageName'] ?? null,
            'uptime_seconds' => $data['runtime']['uptimeInSeconds'] ?? 0,
            'raw_data' => $data,
        ];
    }
}
