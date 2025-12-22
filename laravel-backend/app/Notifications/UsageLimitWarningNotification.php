<?php

declare(strict_types=1);

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\UserRental;

/**
 * Notification when usage is approaching limit (80%, 90%, 100%)
 */
class UsageLimitWarningNotification extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public UserRental $rental,
        public string $usageType, // 'posts' or 'ai_generations'
        public int $used,
        public int $limit,
        public int $percentUsed
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $packageName = $this->rental->rentalPackage->name ?? 'แพ็กเกจ';
        $typeLabel = $this->usageType === 'posts' ? 'โพสต์' : 'AI Generation';
        $remaining = $this->limit - $this->used;

        $subject = match (true) {
            $this->percentUsed >= 100 => "ใช้โควต้า{$typeLabel}ครบแล้ว!",
            $this->percentUsed >= 90 => "ใช้โควต้า{$typeLabel}ไป 90% แล้ว",
            default => "ใช้โควต้า{$typeLabel}ไป 80% แล้ว",
        };

        $message = match (true) {
            $this->percentUsed >= 100 => "คุณใช้โควต้า{$typeLabel}ครบแล้ว ({$this->used}/{$this->limit})",
            $this->percentUsed >= 90 => "คุณใช้โควต้า{$typeLabel}ไป {$this->percentUsed}% แล้ว เหลืออีก {$remaining} ครั้ง",
            default => "คุณใช้โควต้า{$typeLabel}ไป {$this->percentUsed}% แล้ว เหลืออีก {$remaining} ครั้ง",
        };

        return (new MailMessage)
            ->subject($subject)
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line($message)
            ->line("แพ็กเกจปัจจุบัน: {$packageName}")
            ->line('')
            ->when($this->percentUsed >= 100, function ($mail) {
                return $mail->line('คุณจะไม่สามารถใช้งานฟีเจอร์นี้ได้จนกว่าจะอัพเกรดแพ็กเกจ');
            })
            ->action('อัพเกรดแพ็กเกจ', url('/pricing'))
            ->line('อัพเกรดตอนนี้เพื่อเพิ่มโควต้าและเข้าถึงฟีเจอร์พรีเมียม!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'usage_limit_warning',
            'rental_id' => $this->rental->id,
            'usage_type' => $this->usageType,
            'used' => $this->used,
            'limit' => $this->limit,
            'percent_used' => $this->percentUsed,
            'remaining' => $this->limit - $this->used,
            'message' => $this->percentUsed >= 100
                ? 'คุณใช้โควต้าครบแล้ว กรุณาอัพเกรดแพ็กเกจ'
                : "คุณใช้โควต้าไป {$this->percentUsed}% แล้ว",
        ];
    }
}
