<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\License;
use Illuminate\Http\Request;
use Illuminate\Support\Facades\Hash;
use Illuminate\Support\Str;

/**
 * License Controller สำหรับ MyPostXAgent License Management
 */
class LicenseController extends Controller
{
    /**
     * สร้าง License Key ใหม่
     * POST /api/v1/license/generate
     */
    public function generate(Request $request)
    {
        $request->validate([
            'type' => 'required|in:monthly,yearly,lifetime',
            'quantity' => 'integer|min:1|max:100',
            'allowed_machines' => 'integer|min:1|max:10',
        ]);

        $quantity = $request->input('quantity', 1);
        $type = $request->input('type');
        $allowedMachines = $request->input('allowed_machines', 1);

        $licenses = [];

        for ($i = 0; $i < $quantity; $i++) {
            $key = $this->generateLicenseKey();

            $license = License::create([
                'license_key' => $key,
                'type' => $type,
                'status' => 'active',
                'allowed_machines' => $allowedMachines,
                'metadata' => [
                    'generated_at' => now()->toISOString(),
                    'generated_by' => $request->user()?->id ?? 'api',
                ],
            ]);

            $licenses[] = [
                'license_key' => $key,
                'type' => $type,
            ];
        }

        return response()->json([
            'success' => true,
            'data' => $licenses,
            'message' => sprintf('สร้าง %d license keys สำเร็จ', $quantity),
        ]);
    }

    /**
     * เปิดใช้งาน License
     * POST /api/v1/license/activate
     */
    public function activate(Request $request)
    {
        $request->validate([
            'license_key' => 'required|string',
            'machine_id' => 'required|string|max:255',
            'machine_fingerprint' => 'required|string|max:1024',
            'app_version' => 'string|max:50',
        ]);

        $licenseKey = strtoupper(trim($request->input('license_key')));
        $machineId = $request->input('machine_id');
        $fingerprint = $request->input('machine_fingerprint');
        $appVersion = $request->input('app_version', '1.0.0');

        // ค้นหา License
        $license = License::byKey($licenseKey)->first();

        if (!$license) {
            return response()->json([
                'success' => false,
                'error' => 'License key ไม่ถูกต้อง',
                'code' => 'INVALID_KEY',
            ], 400);
        }

        // ตรวจสอบ status
        if ($license->status === 'revoked') {
            return response()->json([
                'success' => false,
                'error' => 'License นี้ถูกยกเลิกแล้ว',
                'code' => 'REVOKED',
            ], 400);
        }

        // ตรวจสอบว่าเคย activate กับ machine อื่นหรือไม่
        if ($license->machine_id && $license->machine_id !== $machineId) {
            // ตรวจสอบจำนวน machines ที่อนุญาต
            $activatedCount = License::byKey($licenseKey)->whereNotNull('machine_id')->count();
            if ($activatedCount >= $license->allowed_machines) {
                return response()->json([
                    'success' => false,
                    'error' => 'License นี้ถูกใช้งานบนเครื่องอื่นแล้ว',
                    'code' => 'MACHINE_LIMIT',
                ], 400);
            }
        }

        // คำนวณวันหมดอายุ
        $expiresAt = match ($license->type) {
            'monthly' => now()->addDays(30),
            'yearly' => now()->addYear(),
            'lifetime' => null,
            default => now()->addDays(30),
        };

        // Update license
        $license->update([
            'machine_id' => $machineId,
            'machine_fingerprint' => Hash::make($fingerprint),
            'activated_at' => now(),
            'expires_at' => $expiresAt,
            'last_validated_at' => now(),
            'metadata' => array_merge($license->metadata ?? [], [
                'last_app_version' => $appVersion,
                'last_activation_ip' => $request->ip(),
            ]),
        ]);

        return response()->json([
            'success' => true,
            'data' => [
                'license_key' => $license->license_key,
                'type' => $license->type,
                'expires_at' => $license->expires_at?->toISOString(),
                'days_remaining' => $license->daysRemaining(),
                'machine_id' => $license->machine_id,
            ],
            'message' => 'เปิดใช้งาน License สำเร็จ',
        ]);
    }

    /**
     * ตรวจสอบ License
     * POST /api/v1/license/validate
     */
    public function validate(Request $request)
    {
        $request->validate([
            'license_key' => 'required|string',
            'machine_id' => 'required|string',
        ]);

        $licenseKey = strtoupper(trim($request->input('license_key')));
        $machineId = $request->input('machine_id');

        $license = License::byKey($licenseKey)
            ->byMachine($machineId)
            ->first();

        if (!$license) {
            return response()->json([
                'success' => false,
                'is_valid' => false,
                'error' => 'License ไม่ถูกต้องหรือไม่ตรงกับเครื่อง',
                'code' => 'INVALID',
            ], 400);
        }

        $isValid = $license->isValid();

        // Update last validated
        $license->update([
            'last_validated_at' => now(),
        ]);

        return response()->json([
            'success' => true,
            'is_valid' => $isValid,
            'data' => [
                'license_key' => $license->license_key,
                'type' => $license->type,
                'status' => $license->status,
                'expires_at' => $license->expires_at?->toISOString(),
                'days_remaining' => $license->daysRemaining(),
                'is_expired' => $license->isExpired(),
            ],
        ]);
    }

    /**
     * เริ่มใช้งาน Demo
     * POST /api/v1/license/demo
     */
    public function startDemo(Request $request)
    {
        $request->validate([
            'machine_id' => 'required|string|max:255',
            'machine_fingerprint' => 'required|string|max:1024',
        ]);

        $machineId = $request->input('machine_id');
        $fingerprint = $request->input('machine_fingerprint');

        // ตรวจสอบว่าเคยใช้ Demo หรือยัง
        $existingDemo = License::where('type', 'demo')
            ->byMachine($machineId)
            ->first();

        if ($existingDemo) {
            if ($existingDemo->isExpired()) {
                return response()->json([
                    'success' => false,
                    'error' => 'Demo หมดอายุแล้ว ไม่สามารถใช้ Demo ซ้ำได้',
                    'code' => 'DEMO_EXPIRED',
                ], 400);
            }

            // Return existing demo info
            return response()->json([
                'success' => true,
                'data' => [
                    'type' => 'demo',
                    'expires_at' => $existingDemo->expires_at->toISOString(),
                    'days_remaining' => $existingDemo->daysRemaining(),
                    'already_started' => true,
                ],
                'message' => 'คุณกำลังใช้งาน Demo อยู่แล้ว',
            ]);
        }

        // สร้าง Demo License ใหม่
        $demoKey = 'DEMO-' . Str::upper(Str::random(12));

        $license = License::create([
            'license_key' => $demoKey,
            'machine_id' => $machineId,
            'machine_fingerprint' => Hash::make($fingerprint),
            'type' => 'demo',
            'status' => 'active',
            'activated_at' => now(),
            'expires_at' => now()->addDays(3),
            'allowed_machines' => 1,
            'metadata' => [
                'demo_started_at' => now()->toISOString(),
                'ip' => $request->ip(),
            ],
        ]);

        return response()->json([
            'success' => true,
            'data' => [
                'type' => 'demo',
                'expires_at' => $license->expires_at->toISOString(),
                'days_remaining' => 3,
                'already_started' => false,
            ],
            'message' => 'เริ่มใช้งาน Demo 3 วันสำเร็จ',
        ]);
    }

    /**
     * ตรวจสอบสถานะ Demo
     * POST /api/v1/license/demo/check
     */
    public function checkDemo(Request $request)
    {
        $request->validate([
            'machine_id' => 'required|string',
        ]);

        $machineId = $request->input('machine_id');

        $demo = License::where('type', 'demo')
            ->byMachine($machineId)
            ->first();

        if (!$demo) {
            return response()->json([
                'success' => true,
                'data' => [
                    'has_used_demo' => false,
                    'can_start_demo' => true,
                ],
            ]);
        }

        return response()->json([
            'success' => true,
            'data' => [
                'has_used_demo' => true,
                'can_start_demo' => false,
                'is_active' => $demo->isValid(),
                'expires_at' => $demo->expires_at?->toISOString(),
                'days_remaining' => $demo->daysRemaining(),
            ],
        ]);
    }

    /**
     * สร้าง License Key แบบสุ่ม
     */
    private function generateLicenseKey(): string
    {
        // Format: XXXX-XXXX-XXXX-XXXX
        $segments = [];
        for ($i = 0; $i < 4; $i++) {
            $segments[] = Str::upper(Str::random(4));
        }
        return implode('-', $segments);
    }
}
