<?php

declare(strict_types=1);

namespace App\Services;

use Illuminate\Support\Facades\Cache;
use Illuminate\Support\Facades\File;
use Illuminate\Support\Facades\DB;
use Exception;

/**
 * First Run Detection Service
 * Checks if this is the first run of the application
 */
class FirstRunService
{
    private const SETUP_COMPLETE_FILE = 'setup-complete.json';
    private const CACHE_KEY = 'postxagent:setup:completed';

    /**
     * Check if this is the first run
     * Returns true if setup has NOT been completed
     */
    public function isFirstRun(): bool
    {
        // Check cache first (faster)
        $cached = Cache::get(self::CACHE_KEY);
        if ($cached !== null) {
            return !$cached;
        }

        // Check if setup file exists
        $setupFile = storage_path(self::SETUP_COMPLETE_FILE);
        if (!File::exists($setupFile)) {
            return true;
        }

        // Read setup state
        try {
            $content = File::get($setupFile);
            $state = json_decode($content, true);

            if (!$state || !isset($state['completed']) || !$state['completed']) {
                return true;
            }

            // Cache the result
            Cache::put(self::CACHE_KEY, true, now()->addDay());

            return false;
        } catch (Exception $e) {
            logger()->error('Failed to read setup state', ['error' => $e->getMessage()]);
            return true;
        }
    }

    /**
     * Mark setup as completed
     */
    public function markSetupCompleted(array $metadata = []): void
    {
        $state = [
            'completed' => true,
            'completed_at' => now()->toIso8601String(),
            'version' => config('app.version', '1.0.0'),
            'metadata' => $metadata,
        ];

        $setupFile = storage_path(self::SETUP_COMPLETE_FILE);

        try {
            File::put($setupFile, json_encode($state, JSON_PRETTY_PRINT));
            Cache::put(self::CACHE_KEY, true, now()->addDay());

            logger()->info('Setup marked as completed', $state);
        } catch (Exception $e) {
            logger()->error('Failed to mark setup as completed', ['error' => $e->getMessage()]);
            throw $e;
        }
    }

    /**
     * Reset setup state (for testing)
     */
    public function resetSetupState(): void
    {
        $setupFile = storage_path(self::SETUP_COMPLETE_FILE);

        if (File::exists($setupFile)) {
            File::delete($setupFile);
        }

        Cache::forget(self::CACHE_KEY);

        logger()->info('Setup state reset');
    }

    /**
     * Get setup state
     */
    public function getSetupState(): ?array
    {
        $setupFile = storage_path(self::SETUP_COMPLETE_FILE);

        if (!File::exists($setupFile)) {
            return null;
        }

        try {
            $content = File::get($setupFile);
            return json_decode($content, true);
        } catch (Exception $e) {
            logger()->error('Failed to get setup state', ['error' => $e->getMessage()]);
            return null;
        }
    }

    /**
     * Check if database is configured and accessible
     */
    public function isDatabaseConfigured(): bool
    {
        try {
            DB::connection()->getPdo();
            return true;
        } catch (Exception $e) {
            return false;
        }
    }

    /**
     * Check if AI Manager is configured
     */
    public function isAIManagerConfigured(): bool
    {
        $host = config('aimanager.host');
        $port = config('aimanager.api_port');

        return !empty($host) && !empty($port);
    }

    /**
     * Get setup progress
     * Returns array of completed steps
     */
    public function getSetupProgress(): array
    {
        return [
            'database' => $this->isDatabaseConfigured(),
            'ai_manager' => $this->isAIManagerConfigured(),
            'setup_completed' => !$this->isFirstRun(),
        ];
    }
}
