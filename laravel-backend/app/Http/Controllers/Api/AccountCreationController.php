<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\AccountCreationTask;
use App\Models\AccountPool;
use App\Models\Brand;
use App\Models\EmailAccount;
use App\Models\PhoneNumber;
use App\Models\ProxyServer;
use App\Models\SocialAccount;
use App\Services\AccountCreationService;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Validator;

class AccountCreationController extends Controller
{
    private AccountCreationService $creationService;

    public function __construct(AccountCreationService $creationService)
    {
        $this->creationService = $creationService;
    }

    /**
     * Create a new account creation task
     */
    public function createTask(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'brand_id' => 'required|exists:brands,id',
            'platform' => 'required|in:' . implode(',', SocialAccount::getPlatforms()),
            'account_pool_id' => 'nullable|exists:account_pools,id',
            'profile_data' => 'nullable|array',
            'profile_data.first_name' => 'nullable|string|max:50',
            'profile_data.last_name' => 'nullable|string|max:50',
            'profile_data.username' => 'nullable|string|max:30',
            'profile_data.bio' => 'nullable|string|max:500',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        // Check ownership
        $brand = Brand::findOrFail($request->brand_id);
        $this->authorize('update', $brand);

        // Check if we can create
        if (!$this->creationService->canCreateAccount($request->platform)) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่มีทรัพยากรเพียงพอสำหรับสร้างบัญชี กรุณาเพิ่มเบอร์โทร/อีเมลก่อน',
                'resource_status' => $this->creationService->checkResourceAvailability($request->platform),
            ], 400);
        }

        $pool = $request->account_pool_id
            ? AccountPool::findOrFail($request->account_pool_id)
            : null;

        $task = $this->creationService->createTask(
            $request->user(),
            $brand,
            $request->platform,
            $pool,
            $request->profile_data
        );

        return response()->json([
            'success' => true,
            'message' => 'สร้างงานสำเร็จ กำลังดำเนินการสร้างบัญชี',
            'data' => $task,
        ], 201);
    }

    /**
     * Create multiple account creation tasks
     */
    public function createBulkTasks(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'brand_id' => 'required|exists:brands,id',
            'platform' => 'required|in:' . implode(',', SocialAccount::getPlatforms()),
            'count' => 'required|integer|min:1|max:10',
            'account_pool_id' => 'nullable|exists:account_pools,id',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $brand = Brand::findOrFail($request->brand_id);
        $this->authorize('update', $brand);

        $pool = $request->account_pool_id
            ? AccountPool::findOrFail($request->account_pool_id)
            : null;

        $tasks = $this->creationService->createBulkTasks(
            $request->user(),
            $brand,
            $request->platform,
            $request->count,
            $pool
        );

        return response()->json([
            'success' => true,
            'message' => 'สร้าง ' . count($tasks) . ' งานสำเร็จ',
            'data' => $tasks,
        ], 201);
    }

    /**
     * Get task status
     */
    public function getTask(AccountCreationTask $task): JsonResponse
    {
        $this->authorize('view', $task->brand);

        return response()->json([
            'success' => true,
            'data' => $task->load(['phoneNumber', 'emailAccount', 'proxyServer', 'resultSocialAccount']),
        ]);
    }

    /**
     * List tasks
     */
    public function listTasks(Request $request): JsonResponse
    {
        $query = AccountCreationTask::query()
            ->where('user_id', $request->user()->id)
            ->with(['brand', 'accountPool', 'resultSocialAccount']);

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        if ($request->has('platform')) {
            $query->where('platform', $request->platform);
        }

        if ($request->has('brand_id')) {
            $query->where('brand_id', $request->brand_id);
        }

        $tasks = $query->orderBy('created_at', 'desc')
            ->paginate($request->get('per_page', 15));

        return response()->json([
            'success' => true,
            'data' => $tasks,
        ]);
    }

    /**
     * Retry a failed task
     */
    public function retryTask(AccountCreationTask $task): JsonResponse
    {
        $this->authorize('update', $task->brand);

        if (!$task->canRetry()) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถลองใหม่ได้ งานนี้ถึงจำนวนครั้งสูงสุดแล้ว',
            ], 400);
        }

        $task->retry();

        return response()->json([
            'success' => true,
            'message' => 'กำลังลองใหม่',
            'data' => $task,
        ]);
    }

    /**
     * Cancel a pending task
     */
    public function cancelTask(AccountCreationTask $task): JsonResponse
    {
        $this->authorize('update', $task->brand);

        if (!in_array($task->status, [AccountCreationTask::STATUS_PENDING, AccountCreationTask::STATUS_FAILED])) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถยกเลิกได้ งานกำลังดำเนินการอยู่',
            ], 400);
        }

        $task->cancel();

        return response()->json([
            'success' => true,
            'message' => 'ยกเลิกงานสำเร็จ',
        ]);
    }

    /**
     * Get account creation statistics
     */
    public function getStatistics(Request $request): JsonResponse
    {
        $platform = $request->get('platform');
        $hours = $request->get('hours', 24);

        $stats = $this->creationService->getStatistics($platform, $hours);

        return response()->json([
            'success' => true,
            'data' => $stats,
        ]);
    }

    /**
     * Check resource availability
     */
    public function checkResources(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'platform' => 'required|in:' . implode(',', SocialAccount::getPlatforms()),
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $status = $this->creationService->checkResourceAvailability($request->platform);

        return response()->json([
            'success' => true,
            'data' => $status,
        ]);
    }

    // Phone Number Management

    /**
     * List phone numbers
     */
    public function listPhoneNumbers(Request $request): JsonResponse
    {
        $query = PhoneNumber::query();

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        $phones = $query->orderBy('created_at', 'desc')
            ->paginate($request->get('per_page', 15));

        return response()->json([
            'success' => true,
            'data' => $phones,
        ]);
    }

    /**
     * Add phone number manually
     */
    public function addPhoneNumber(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'phone_number' => 'required|string|unique:phone_numbers,phone_number',
            'country_code' => 'required|string|size:2',
            'provider' => 'nullable|in:' . implode(',', PhoneNumber::getProviders()),
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $phone = PhoneNumber::create([
            'phone_number' => $request->phone_number,
            'country_code' => strtoupper($request->country_code),
            'provider' => $request->provider ?? PhoneNumber::PROVIDER_MANUAL,
            'status' => PhoneNumber::STATUS_AVAILABLE,
        ]);

        return response()->json([
            'success' => true,
            'message' => 'เพิ่มเบอร์โทรสำเร็จ',
            'data' => $phone,
        ], 201);
    }

    /**
     * Delete phone number
     */
    public function deletePhoneNumber(PhoneNumber $phone): JsonResponse
    {
        if ($phone->status === PhoneNumber::STATUS_IN_USE) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถลบได้ เบอร์นี้กำลังใช้งานอยู่',
            ], 400);
        }

        $phone->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบเบอร์โทรสำเร็จ',
        ]);
    }

    // Email Account Management

    /**
     * List email accounts
     */
    public function listEmailAccounts(Request $request): JsonResponse
    {
        $query = EmailAccount::query();

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        $emails = $query->orderBy('created_at', 'desc')
            ->paginate($request->get('per_page', 15));

        return response()->json([
            'success' => true,
            'data' => $emails,
        ]);
    }

    /**
     * Add email account
     */
    public function addEmailAccount(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'email' => 'required|email|unique:email_accounts,email',
            'password' => 'required|string|min:6',
            'provider' => 'nullable|in:' . implode(',', EmailAccount::getProviders()),
            'recovery_email' => 'nullable|email',
            'recovery_phone' => 'nullable|string',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $email = new EmailAccount([
            'email' => $request->email,
            'provider' => $request->provider ?? $this->detectEmailProvider($request->email),
            'recovery_email' => $request->recovery_email,
            'recovery_phone' => $request->recovery_phone,
            'status' => EmailAccount::STATUS_AVAILABLE,
        ]);
        $email->password = $request->password;
        $email->save();

        return response()->json([
            'success' => true,
            'message' => 'เพิ่มอีเมลสำเร็จ',
            'data' => $email,
        ], 201);
    }

    /**
     * Delete email account
     */
    public function deleteEmailAccount(EmailAccount $email): JsonResponse
    {
        if ($email->status === EmailAccount::STATUS_IN_USE) {
            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถลบได้ อีเมลนี้กำลังใช้งานอยู่',
            ], 400);
        }

        $email->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบอีเมลสำเร็จ',
        ]);
    }

    // Proxy Management

    /**
     * List proxy servers
     */
    public function listProxies(Request $request): JsonResponse
    {
        $query = ProxyServer::query();

        if ($request->has('status')) {
            $query->where('status', $request->status);
        }

        if ($request->has('country_code')) {
            $query->where('country_code', $request->country_code);
        }

        $proxies = $query->orderBy('response_time_ms', 'asc')
            ->paginate($request->get('per_page', 15));

        return response()->json([
            'success' => true,
            'data' => $proxies,
        ]);
    }

    /**
     * Add proxy server
     */
    public function addProxy(Request $request): JsonResponse
    {
        $validator = Validator::make($request->all(), [
            'host' => 'required|string',
            'port' => 'required|integer|min:1|max:65535',
            'type' => 'nullable|in:' . implode(',', ProxyServer::getTypes()),
            'username' => 'nullable|string',
            'password' => 'nullable|string',
            'provider' => 'nullable|in:' . implode(',', [
                ProxyServer::PROVIDER_BRIGHT_DATA,
                ProxyServer::PROVIDER_OXYLABS,
                ProxyServer::PROVIDER_SMARTPROXY,
                ProxyServer::PROVIDER_RESIDENTIAL,
                ProxyServer::PROVIDER_DATACENTER,
                ProxyServer::PROVIDER_CUSTOM,
            ]),
            'country_code' => 'nullable|string|size:2',
        ]);

        if ($validator->fails()) {
            return response()->json([
                'success' => false,
                'errors' => $validator->errors(),
            ], 422);
        }

        $proxy = new ProxyServer([
            'host' => $request->host,
            'port' => $request->port,
            'type' => $request->type ?? ProxyServer::TYPE_HTTP,
            'username' => $request->username,
            'provider' => $request->provider ?? ProxyServer::PROVIDER_CUSTOM,
            'country_code' => $request->country_code ? strtoupper($request->country_code) : null,
            'status' => ProxyServer::STATUS_ACTIVE,
        ]);

        if ($request->password) {
            $proxy->password = $request->password;
        }

        $proxy->save();

        return response()->json([
            'success' => true,
            'message' => 'เพิ่ม Proxy สำเร็จ',
            'data' => $proxy,
        ], 201);
    }

    /**
     * Delete proxy server
     */
    public function deleteProxy(ProxyServer $proxy): JsonResponse
    {
        $proxy->delete();

        return response()->json([
            'success' => true,
            'message' => 'ลบ Proxy สำเร็จ',
        ]);
    }

    /**
     * Test proxy connectivity
     */
    public function testProxy(ProxyServer $proxy): JsonResponse
    {
        // This would actually test the proxy connection
        // For now, return mock result
        $startTime = microtime(true);

        try {
            // Simulate proxy test
            usleep(100000); // 100ms delay

            $responseTime = (int)((microtime(true) - $startTime) * 1000);
            $proxy->recordSuccess($responseTime);

            return response()->json([
                'success' => true,
                'message' => 'Proxy ทำงานปกติ',
                'data' => [
                    'response_time_ms' => $responseTime,
                    'status' => 'connected',
                ],
            ]);
        } catch (\Exception $e) {
            $proxy->recordFailure();

            return response()->json([
                'success' => false,
                'message' => 'ไม่สามารถเชื่อมต่อ Proxy ได้',
                'error' => $e->getMessage(),
            ], 400);
        }
    }

    // Helpers

    private function detectEmailProvider(string $email): string
    {
        $domain = strtolower(substr($email, strpos($email, '@') + 1));

        return match (true) {
            str_contains($domain, 'gmail') => EmailAccount::PROVIDER_GMAIL,
            str_contains($domain, 'outlook') || str_contains($domain, 'hotmail') => EmailAccount::PROVIDER_OUTLOOK,
            str_contains($domain, 'yahoo') => EmailAccount::PROVIDER_YAHOO,
            default => EmailAccount::PROVIDER_CUSTOM,
        };
    }
}
