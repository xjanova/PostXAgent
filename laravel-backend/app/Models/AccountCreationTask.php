<?php

namespace App\Models;

use Illuminate\Database\Eloquent\Factories\HasFactory;
use Illuminate\Database\Eloquent\Model;
use Illuminate\Database\Eloquent\Relations\BelongsTo;

/**
 * @property int $id
 * @property int $user_id
 * @property int $brand_id
 * @property int|null $account_pool_id
 * @property string $platform
 * @property string $status
 * @property string $current_step
 * @property int $attempts
 * @property int $max_attempts
 * @property Brand $brand
 * @property User $user
 * @property AccountPool|null $accountPool
 */
class AccountCreationTask extends Model
{
    use HasFactory;

    // Status constants
    const STATUS_PENDING = 'pending';
    const STATUS_IN_PROGRESS = 'in_progress';
    const STATUS_VERIFYING = 'verifying';
    const STATUS_COMPLETED = 'completed';
    const STATUS_FAILED = 'failed';
    const STATUS_CANCELLED = 'cancelled';

    // Step constants
    const STEP_INIT = 'init';
    const STEP_OPEN_BROWSER = 'open_browser';
    const STEP_NAVIGATE_SIGNUP = 'navigate_signup';
    const STEP_FILL_FORM = 'fill_form';
    const STEP_SUBMIT_FORM = 'submit_form';
    const STEP_WAIT_SMS = 'wait_sms';
    const STEP_ENTER_OTP = 'enter_otp';
    const STEP_COMPLETE_PROFILE = 'complete_profile';
    const STEP_SAVE_CREDENTIALS = 'save_credentials';
    const STEP_DONE = 'done';

    protected $fillable = [
        'user_id',
        'brand_id',
        'account_pool_id',
        'platform',
        'status',
        'current_step',
        'phone_number_id',
        'email_account_id',
        'proxy_server_id',
        'profile_data',
        'generated_username',
        'generated_password',
        'result_social_account_id',
        'error_message',
        'error_screenshot',
        'attempts',
        'max_attempts',
        'started_at',
        'completed_at',
        'step_log',
        'metadata',
    ];

    protected $casts = [
        'profile_data' => 'array',
        'started_at' => 'datetime',
        'completed_at' => 'datetime',
        'step_log' => 'array',
        'metadata' => 'array',
        'attempts' => 'integer',
        'max_attempts' => 'integer',
    ];

    protected $attributes = [
        'status' => self::STATUS_PENDING,
        'current_step' => self::STEP_INIT,
        'attempts' => 0,
        'max_attempts' => 3,
    ];

    public static function getStatuses(): array
    {
        return [
            self::STATUS_PENDING,
            self::STATUS_IN_PROGRESS,
            self::STATUS_VERIFYING,
            self::STATUS_COMPLETED,
            self::STATUS_FAILED,
            self::STATUS_CANCELLED,
        ];
    }

    public static function getSteps(): array
    {
        return [
            self::STEP_INIT,
            self::STEP_OPEN_BROWSER,
            self::STEP_NAVIGATE_SIGNUP,
            self::STEP_FILL_FORM,
            self::STEP_SUBMIT_FORM,
            self::STEP_WAIT_SMS,
            self::STEP_ENTER_OTP,
            self::STEP_COMPLETE_PROFILE,
            self::STEP_SAVE_CREDENTIALS,
            self::STEP_DONE,
        ];
    }

    // Relationships
    public function user(): BelongsTo
    {
        return $this->belongsTo(User::class);
    }

    public function brand(): BelongsTo
    {
        return $this->belongsTo(Brand::class);
    }

    public function accountPool(): BelongsTo
    {
        return $this->belongsTo(AccountPool::class);
    }

    public function phoneNumber(): BelongsTo
    {
        return $this->belongsTo(PhoneNumber::class);
    }

    public function emailAccount(): BelongsTo
    {
        return $this->belongsTo(EmailAccount::class);
    }

    public function proxyServer(): BelongsTo
    {
        return $this->belongsTo(ProxyServer::class);
    }

    public function resultSocialAccount(): BelongsTo
    {
        return $this->belongsTo(SocialAccount::class, 'result_social_account_id');
    }

    // Query Scopes
    public function scopePending($query)
    {
        return $query->where('status', self::STATUS_PENDING);
    }

    public function scopeInProgress($query)
    {
        return $query->whereIn('status', [self::STATUS_IN_PROGRESS, self::STATUS_VERIFYING]);
    }

    public function scopeForPlatform($query, string $platform)
    {
        return $query->where('platform', $platform);
    }

    // Helpers
    public function start(): void
    {
        $this->update([
            'status' => self::STATUS_IN_PROGRESS,
            'started_at' => now(),
            'attempts' => $this->attempts + 1,
        ]);
    }

    public function updateStep(string $step, ?string $message = null): void
    {
        $log = $this->step_log ?? [];
        $log[] = [
            'step' => $step,
            'message' => $message,
            'timestamp' => now()->toIso8601String(),
        ];

        $this->update([
            'current_step' => $step,
            'step_log' => $log,
        ]);
    }

    public function markAsVerifying(): void
    {
        $this->update(['status' => self::STATUS_VERIFYING]);
    }

    public function complete(int $socialAccountId): void
    {
        $this->update([
            'status' => self::STATUS_COMPLETED,
            'current_step' => self::STEP_DONE,
            'result_social_account_id' => $socialAccountId,
            'completed_at' => now(),
        ]);
    }

    public function fail(string $error, ?string $screenshot = null): void
    {
        $this->update([
            'status' => self::STATUS_FAILED,
            'error_message' => $error,
            'error_screenshot' => $screenshot,
            'completed_at' => now(),
        ]);
    }

    public function canRetry(): bool
    {
        return $this->status === self::STATUS_FAILED
            && $this->attempts < $this->max_attempts;
    }

    public function retry(): void
    {
        $this->update([
            'status' => self::STATUS_PENDING,
            'current_step' => self::STEP_INIT,
            'error_message' => null,
            'error_screenshot' => null,
        ]);
    }

    public function cancel(): void
    {
        $this->update([
            'status' => self::STATUS_CANCELLED,
            'completed_at' => now(),
        ]);
    }

    public function getDuration(): ?int
    {
        if (!$this->started_at) {
            return null;
        }

        $endTime = $this->completed_at ?? now();
        return $this->started_at->diffInSeconds($endTime);
    }
}
