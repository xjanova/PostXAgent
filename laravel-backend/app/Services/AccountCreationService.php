<?php

declare(strict_types=1);

namespace App\Services;

use App\Models\AccountCreationTask;
use App\Models\AccountPool;
use App\Models\AccountPoolMember;
use App\Models\Brand;
use App\Models\EmailAccount;
use App\Models\PhoneNumber;
use App\Models\ProxyServer;
use App\Models\SocialAccount;
use App\Models\User;
use Illuminate\Support\Facades\DB;
use Illuminate\Support\Facades\Log;
use Illuminate\Support\Str;

class AccountCreationService
{
    private AIManagerService $aiManager;

    // Platform signup URLs
    const SIGNUP_URLS = [
        'facebook' => 'https://www.facebook.com/r.php',
        'instagram' => 'https://www.instagram.com/accounts/emailsignup/',
        'tiktok' => 'https://www.tiktok.com/signup',
        'twitter' => 'https://twitter.com/i/flow/signup',
        'line' => 'https://account.line.biz/signup',
        'youtube' => 'https://accounts.google.com/signup',
        'threads' => 'https://www.threads.net/login',
        'linkedin' => 'https://www.linkedin.com/signup',
        'pinterest' => 'https://www.pinterest.com/signup/',
    ];

    // Platforms that require phone verification
    const PHONE_REQUIRED = ['facebook', 'instagram', 'tiktok', 'twitter', 'line'];

    // Platforms that can use email only
    const EMAIL_ONLY = ['linkedin', 'pinterest', 'youtube'];

    public function __construct(AIManagerService $aiManager)
    {
        $this->aiManager = $aiManager;
    }

    /**
     * Create a new account creation task
     */
    public function createTask(
        User $user,
        Brand $brand,
        string $platform,
        ?AccountPool $pool = null,
        ?array $profileData = null
    ): AccountCreationTask {
        // Validate platform
        if (!in_array($platform, SocialAccount::getPlatforms())) {
            throw new \InvalidArgumentException("Invalid platform: {$platform}");
        }

        // Allocate resources
        $phoneNumber = $this->allocatePhoneNumber($platform);
        $emailAccount = $this->allocateEmailAccount($platform);
        $proxyServer = $this->allocateProxy($platform);

        // Generate profile data
        $profile = $this->generateProfileData($platform, $profileData);

        return AccountCreationTask::create([
            'user_id' => $user->id,
            'brand_id' => $brand->id,
            'account_pool_id' => $pool?->id,
            'platform' => $platform,
            'phone_number_id' => $phoneNumber?->id,
            'email_account_id' => $emailAccount?->id,
            'proxy_server_id' => $proxyServer?->id,
            'profile_data' => $profile,
            'generated_username' => $profile['username'],
            'generated_password' => $profile['password'],
        ]);
    }

    /**
     * Create multiple account creation tasks
     */
    public function createBulkTasks(
        User $user,
        Brand $brand,
        string $platform,
        int $count,
        ?AccountPool $pool = null
    ): array {
        $tasks = [];

        for ($i = 0; $i < $count; $i++) {
            try {
                $tasks[] = $this->createTask($user, $brand, $platform, $pool);
            } catch (\Exception $e) {
                Log::warning("Failed to create task {$i}: " . $e->getMessage());
            }
        }

        return $tasks;
    }

    /**
     * Process a pending account creation task
     */
    public function processTask(AccountCreationTask $task): bool
    {
        try {
            $task->start();

            // Send task to AI Manager for execution
            $result = $this->aiManager->createAccount($task);

            if ($result['success']) {
                // Create the social account
                $socialAccount = $this->createSocialAccountFromResult($task, $result);

                // Add to pool if specified
                if ($task->account_pool_id) {
                    $this->addToPool($socialAccount, $task->accountPool);
                }

                // Mark resources as used
                $this->markResourcesAsUsed($task, $socialAccount);

                $task->complete($socialAccount->id);

                Log::info("Account created successfully", [
                    'task_id' => $task->id,
                    'platform' => $task->platform,
                    'account_id' => $socialAccount->id,
                ]);

                return true;
            } else {
                $task->fail($result['error'] ?? 'Unknown error', $result['screenshot'] ?? null);

                Log::warning("Account creation failed", [
                    'task_id' => $task->id,
                    'error' => $result['error'],
                ]);

                return false;
            }
        } catch (\Exception $e) {
            $task->fail($e->getMessage());

            Log::error("Account creation exception", [
                'task_id' => $task->id,
                'error' => $e->getMessage(),
            ]);

            return false;
        }
    }

    /**
     * Get pending tasks for processing
     */
    public function getPendingTasks(int $limit = 10): \Illuminate\Database\Eloquent\Collection
    {
        return AccountCreationTask::pending()
            ->orderBy('created_at', 'asc')
            ->limit($limit)
            ->get();
    }

    /**
     * Retry failed tasks
     */
    public function retryFailedTasks(): int
    {
        $tasks = AccountCreationTask::where('status', AccountCreationTask::STATUS_FAILED)
            ->whereColumn('attempts', '<', 'max_attempts')
            ->get();

        $retried = 0;
        foreach ($tasks as $task) {
            if ($task->canRetry()) {
                // Reallocate resources if needed
                $this->reallocateResources($task);
                $task->retry();
                $retried++;
            }
        }

        return $retried;
    }

    /**
     * Get account creation statistics
     */
    public function getStatistics(?string $platform = null, int $hours = 24): array
    {
        $query = AccountCreationTask::where('created_at', '>=', now()->subHours($hours));

        if ($platform) {
            $query->where('platform', $platform);
        }

        $tasks = $query->get();

        $statusCounts = $tasks->groupBy('status')->map->count();
        $platformCounts = $tasks->groupBy('platform')->map->count();

        $completed = $tasks->where('status', AccountCreationTask::STATUS_COMPLETED);
        $failed = $tasks->where('status', AccountCreationTask::STATUS_FAILED);

        return [
            'period_hours' => $hours,
            'total' => $tasks->count(),
            'by_status' => [
                'pending' => $statusCounts->get(AccountCreationTask::STATUS_PENDING, 0),
                'in_progress' => $statusCounts->get(AccountCreationTask::STATUS_IN_PROGRESS, 0),
                'completed' => $statusCounts->get(AccountCreationTask::STATUS_COMPLETED, 0),
                'failed' => $statusCounts->get(AccountCreationTask::STATUS_FAILED, 0),
                'cancelled' => $statusCounts->get(AccountCreationTask::STATUS_CANCELLED, 0),
            ],
            'by_platform' => $platformCounts->toArray(),
            'success_rate' => $tasks->count() > 0
                ? round(($completed->count() / $tasks->count()) * 100, 2)
                : 0,
            'avg_duration_seconds' => $completed->avg(fn($t) => $t->getDuration()) ?? 0,
            'common_errors' => $failed->groupBy('error_message')
                ->map->count()
                ->sortDesc()
                ->take(5)
                ->toArray(),
        ];
    }

    /**
     * Check resource availability
     */
    public function checkResourceAvailability(string $platform): array
    {
        $needsPhone = in_array($platform, self::PHONE_REQUIRED);

        return [
            'platform' => $platform,
            'phone_required' => $needsPhone,
            'phone_available' => $needsPhone
                ? PhoneNumber::available()->forPlatform($platform)->count()
                : null,
            'email_available' => EmailAccount::available()->forPlatform($platform)->count(),
            'proxy_available' => ProxyServer::active()->notBannedFor($platform)->count(),
            'can_create' => $this->canCreateAccount($platform),
        ];
    }

    /**
     * Check if we can create an account for a platform
     */
    public function canCreateAccount(string $platform): bool
    {
        // Check phone availability
        if (in_array($platform, self::PHONE_REQUIRED)) {
            if (PhoneNumber::available()->forPlatform($platform)->count() === 0) {
                return false;
            }
        }

        // Check email availability
        if (EmailAccount::available()->forPlatform($platform)->count() === 0) {
            return false;
        }

        // Check proxy availability
        if (ProxyServer::active()->notBannedFor($platform)->count() === 0) {
            return false;
        }

        return true;
    }

    // Private methods

    private function allocatePhoneNumber(string $platform): ?PhoneNumber
    {
        if (!in_array($platform, self::PHONE_REQUIRED)) {
            return null;
        }

        $phone = PhoneNumber::available()
            ->forPlatform($platform)
            ->orderBy('created_at', 'asc')
            ->first();

        if ($phone) {
            $phone->markAsInUse($platform);
        }

        return $phone;
    }

    private function allocateEmailAccount(string $platform): ?EmailAccount
    {
        $email = EmailAccount::available()
            ->forPlatform($platform)
            ->orderBy('created_at', 'asc')
            ->first();

        if ($email) {
            $email->markAsInUse($platform);
        }

        return $email;
    }

    private function allocateProxy(string $platform): ?ProxyServer
    {
        return ProxyServer::active()
            ->notBannedFor($platform)
            ->leastUsed()
            ->first();
    }

    private function generateProfileData(string $platform, ?array $customData = null): array
    {
        $faker = \Faker\Factory::create('th_TH');

        // Generate Thai-friendly profile
        $firstName = $customData['first_name'] ?? $faker->firstName();
        $lastName = $customData['last_name'] ?? $faker->lastName();

        $username = $customData['username'] ?? $this->generateUsername($firstName, $lastName);
        $password = $customData['password'] ?? $this->generatePassword();

        return [
            'first_name' => $firstName,
            'last_name' => $lastName,
            'display_name' => "{$firstName} {$lastName}",
            'username' => $username,
            'password' => $password,
            'birthday' => $customData['birthday'] ?? $faker->dateTimeBetween('-40 years', '-18 years')->format('Y-m-d'),
            'gender' => $customData['gender'] ?? $faker->randomElement(['male', 'female']),
            'bio' => $customData['bio'] ?? $faker->sentence(),
            'avatar_prompt' => "Professional profile photo of a Thai {$faker->randomElement(['man', 'woman'])}",
        ];
    }

    private function generateUsername(string $firstName, string $lastName): string
    {
        $base = strtolower($firstName . $lastName);
        $base = preg_replace('/[^a-z0-9]/', '', $base);

        // Add random suffix
        $suffix = rand(100, 9999);

        return substr($base, 0, 10) . $suffix;
    }

    private function generatePassword(): string
    {
        $chars = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%';
        $password = '';

        // Ensure at least one of each type
        $password .= chr(rand(97, 122)); // lowercase
        $password .= chr(rand(65, 90));  // uppercase
        $password .= chr(rand(48, 57));  // number
        $password .= $chars[rand(62, 67)]; // special

        // Add more random chars
        for ($i = 0; $i < 8; $i++) {
            $password .= $chars[rand(0, strlen($chars) - 1)];
        }

        return str_shuffle($password);
    }

    private function createSocialAccountFromResult(AccountCreationTask $task, array $result): SocialAccount
    {
        return SocialAccount::create([
            'user_id' => $task->user_id,
            'brand_id' => $task->brand_id,
            'platform' => $task->platform,
            'platform_user_id' => $result['platform_user_id'] ?? null,
            'platform_username' => $result['username'] ?? $task->generated_username,
            'display_name' => $task->profile_data['display_name'] ?? null,
            'access_token' => $result['access_token'] ?? null,
            'refresh_token' => $result['refresh_token'] ?? null,
            'token_expires_at' => isset($result['token_expires_at'])
                ? now()->addSeconds($result['token_expires_at'])
                : null,
            'profile_url' => $result['profile_url'] ?? null,
            'avatar_url' => $result['avatar_url'] ?? null,
            'metadata' => [
                'created_by' => 'auto_creation',
                'task_id' => $task->id,
                'password' => $task->generated_password, // Encrypted in SocialAccount model
                'email' => $task->emailAccount?->email,
                'phone' => $task->phoneNumber?->phone_number,
            ],
            'is_active' => true,
        ]);
    }

    private function addToPool(SocialAccount $account, AccountPool $pool): void
    {
        AccountPoolMember::create([
            'account_pool_id' => $pool->id,
            'social_account_id' => $account->id,
            'priority' => 100, // Low priority for new accounts
            'weight' => 1,
            'status' => AccountPoolMember::STATUS_ACTIVE,
            'posts_today' => 0,
            'total_posts' => 0,
            'success_count' => 0,
            'failure_count' => 0,
            'consecutive_failures' => 0,
        ]);
    }

    private function markResourcesAsUsed(AccountCreationTask $task, SocialAccount $account): void
    {
        if ($task->phoneNumber) {
            $task->phoneNumber->markAsUsed($account->id);
        }

        if ($task->emailAccount) {
            $task->emailAccount->markAsUsed($account->id);
        }
    }

    private function reallocateResources(AccountCreationTask $task): void
    {
        // Release old resources
        if ($task->phoneNumber && $task->phoneNumber->status === PhoneNumber::STATUS_IN_USE) {
            $task->phoneNumber->update(['status' => PhoneNumber::STATUS_AVAILABLE]);
        }

        if ($task->emailAccount && $task->emailAccount->status === EmailAccount::STATUS_IN_USE) {
            $task->emailAccount->update(['status' => EmailAccount::STATUS_AVAILABLE]);
        }

        // Allocate new resources
        $newPhone = $this->allocatePhoneNumber($task->platform);
        $newEmail = $this->allocateEmailAccount($task->platform);
        $newProxy = $this->allocateProxy($task->platform);

        $task->update([
            'phone_number_id' => $newPhone?->id ?? $task->phone_number_id,
            'email_account_id' => $newEmail?->id ?? $task->email_account_id,
            'proxy_server_id' => $newProxy?->id ?? $task->proxy_server_id,
        ]);
    }
}
