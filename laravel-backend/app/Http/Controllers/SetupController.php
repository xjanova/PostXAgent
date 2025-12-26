<?php

declare(strict_types=1);

namespace App\Http\Controllers;

use App\Services\FirstRunService;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Artisan;
use Illuminate\Support\Facades\Config;
use Exception;

/**
 * Setup Wizard Controller
 * Handles the initial setup process
 */
class SetupController extends Controller
{
    public function __construct(
        private readonly FirstRunService $firstRunService
    ) {
    }

    /**
     * Show setup wizard page
     */
    public function index()
    {
        // If setup is already complete, redirect to home
        if (!$this->firstRunService->isFirstRun()) {
            return redirect('/');
        }

        return view('setup.index');
    }

    /**
     * Get setup status
     */
    public function status(): JsonResponse
    {
        return response()->json([
            'success' => true,
            'data' => [
                'is_first_run' => $this->firstRunService->isFirstRun(),
                'progress' => $this->firstRunService->getSetupProgress(),
                'state' => $this->firstRunService->getSetupState(),
            ],
        ]);
    }

    /**
     * Test database connection
     */
    public function testDatabase(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'driver' => 'required|string|in:mysql,pgsql,sqlite',
            'host' => 'required_unless:driver,sqlite|string',
            'port' => 'required_unless:driver,sqlite|integer',
            'database' => 'required|string',
            'username' => 'required_unless:driver,sqlite|string',
            'password' => 'nullable|string',
        ]);

        try {
            // Temporarily configure database connection
            Config::set('database.connections.test', [
                'driver' => $validated['driver'],
                'host' => $validated['host'] ?? null,
                'port' => $validated['port'] ?? null,
                'database' => $validated['database'],
                'username' => $validated['username'] ?? null,
                'password' => $validated['password'] ?? null,
            ]);

            // Test connection
            DB::connection('test')->getPdo();
            DB::connection('test')->disconnect();

            return response()->json([
                'success' => true,
                'message' => 'Database connection successful',
            ]);
        } catch (Exception $e) {
            return response()->json([
                'success' => false,
                'error' => 'Database connection failed: ' . $e->getMessage(),
            ], 400);
        }
    }

    /**
     * Save database configuration
     */
    public function saveDatabase(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'driver' => 'required|string|in:mysql,pgsql,sqlite',
            'host' => 'required_unless:driver,sqlite|string',
            'port' => 'required_unless:driver,sqlite|integer',
            'database' => 'required|string',
            'username' => 'required_unless:driver,sqlite|string',
            'password' => 'nullable|string',
        ]);

        try {
            // Update .env file
            $this->updateEnvFile([
                'DB_CONNECTION' => $validated['driver'],
                'DB_HOST' => $validated['host'] ?? '',
                'DB_PORT' => $validated['port'] ?? '',
                'DB_DATABASE' => $validated['database'],
                'DB_USERNAME' => $validated['username'] ?? '',
                'DB_PASSWORD' => $validated['password'] ?? '',
            ]);

            // Run migrations
            Artisan::call('migrate', ['--force' => true]);

            return response()->json([
                'success' => true,
                'message' => 'Database configured successfully',
            ]);
        } catch (Exception $e) {
            return response()->json([
                'success' => false,
                'error' => 'Failed to configure database: ' . $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Save AI Manager configuration
     */
    public function saveAIManager(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'host' => 'required|string',
            'api_port' => 'required|integer',
            'signalr_port' => 'required|integer',
        ]);

        try {
            $this->updateEnvFile([
                'AI_MANAGER_HOST' => $validated['host'],
                'AI_MANAGER_API_PORT' => $validated['api_port'],
                'AI_MANAGER_SIGNALR_PORT' => $validated['signalr_port'],
            ]);

            return response()->json([
                'success' => true,
                'message' => 'AI Manager configured successfully',
            ]);
        } catch (Exception $e) {
            return response()->json([
                'success' => false,
                'error' => 'Failed to configure AI Manager: ' . $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Save AI providers configuration
     */
    public function saveAIProviders(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'ollama_enabled' => 'boolean',
            'ollama_url' => 'nullable|string',
            'openai_enabled' => 'boolean',
            'openai_key' => 'nullable|string',
            'gemini_enabled' => 'boolean',
            'gemini_key' => 'nullable|string',
            'anthropic_enabled' => 'boolean',
            'anthropic_key' => 'nullable|string',
        ]);

        try {
            $envVars = [];

            if ($validated['ollama_enabled'] ?? false) {
                $envVars['OLLAMA_BASE_URL'] = $validated['ollama_url'] ?? 'http://localhost:11434';
            }

            if ($validated['openai_enabled'] ?? false) {
                $envVars['OPENAI_API_KEY'] = $validated['openai_key'] ?? '';
            }

            if ($validated['gemini_enabled'] ?? false) {
                $envVars['GOOGLE_API_KEY'] = $validated['gemini_key'] ?? '';
            }

            if ($validated['anthropic_enabled'] ?? false) {
                $envVars['ANTHROPIC_API_KEY'] = $validated['anthropic_key'] ?? '';
            }

            $this->updateEnvFile($envVars);

            return response()->json([
                'success' => true,
                'message' => 'AI providers configured successfully',
            ]);
        } catch (Exception $e) {
            return response()->json([
                'success' => false,
                'error' => 'Failed to configure AI providers: ' . $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Complete setup
     */
    public function complete(Request $request): JsonResponse
    {
        try {
            $metadata = $request->only(['user_email', 'company_name']);

            $this->firstRunService->markSetupCompleted($metadata);

            return response()->json([
                'success' => true,
                'message' => 'Setup completed successfully',
                'redirect' => '/',
            ]);
        } catch (Exception $e) {
            return response()->json([
                'success' => false,
                'error' => 'Failed to complete setup: ' . $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Reset setup (for testing only)
     */
    public function reset(): JsonResponse
    {
        if (!app()->environment('local')) {
            return response()->json([
                'success' => false,
                'error' => 'Setup reset is only allowed in local environment',
            ], 403);
        }

        try {
            $this->firstRunService->resetSetupState();

            return response()->json([
                'success' => true,
                'message' => 'Setup state reset successfully',
            ]);
        } catch (Exception $e) {
            return response()->json([
                'success' => false,
                'error' => 'Failed to reset setup: ' . $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Update .env file with new values
     */
    private function updateEnvFile(array $data): void
    {
        $envFile = base_path('.env');
        $envContent = file_get_contents($envFile);

        foreach ($data as $key => $value) {
            // Escape special characters in value
            $value = str_replace('"', '\"', $value);

            // Check if key exists
            if (preg_match("/^{$key}=.*/m", $envContent)) {
                // Update existing key
                $envContent = preg_replace(
                    "/^{$key}=.*/m",
                    "{$key}=\"{$value}\"",
                    $envContent
                );
            } else {
                // Add new key
                $envContent .= "\n{$key}=\"{$value}\"";
            }
        }

        file_put_contents($envFile, $envContent);

        // Reload configuration
        Artisan::call('config:clear');
    }
}
