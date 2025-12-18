<?php

declare(strict_types=1);

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\UserRental;

/**
 * Notification for rental expiring soon (3 days, 1 day, today)
 */
class RentalExpiringNotification extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public UserRental $rental,
        public int $daysRemaining
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $packageName = $this->rental->package->name ?? 'แพ็กเกจ';
        $expiryDate = $this->rental->ends_at->format('d/m/Y H:i');

        $subject = match (true) {
            $this->daysRemaining <= 0 => "ด่วน! แพ็กเกจของคุณจะหมดอายุวันนี้!",
            $this->daysRemaining === 1 => "แพ็กเกจของคุณจะหมดอายุพรุ่งนี้!",
            default => "แพ็กเกจของคุณจะหมดอายุใน {$this->daysRemaining} วัน",
        };

        $urgencyLine = match (true) {
            $this->daysRemaining <= 0 => 'แพ็กเกจของคุณจะหมดอายุภายในวันนี้!',
            $this->daysRemaining === 1 => 'แพ็กเกจของคุณจะหมดอายุในวันพรุ่งนี้!',
            default => "แพ็กเกจของคุณจะหมดอายุในอีก {$this->daysRemaining} วัน",
        };

        return (new MailMessage)
            ->subject($subject)
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line($urgencyLine)
            ->line("แพ็กเกจ: {$packageName}")
            ->line("หมดอายุ: {$expiryDate}")
            ->line('ต่ออายุตอนนี้เพื่อไม่ให้บริการหยุดชะงัก!')
            ->action('ต่ออายุแพ็กเกจ', url('/pricing'))
            ->line('หรือเลือกแพ็กเกจใหม่ที่เหมาะกับคุณมากกว่า');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'rental_expiring',
            'rental_id' => $this->rental->id,
            'package_name' => $this->rental->package->name ?? 'แพ็กเกจ',
            'expires_at' => $this->rental->ends_at->toIso8601String(),
            'days_remaining' => $this->daysRemaining,
            'message' => $this->daysRemaining <= 0
                ? 'แพ็กเกจของคุณจะหมดอายุวันนี้!'
                : "แพ็กเกจของคุณจะหมดอายุใน {$this->daysRemaining} วัน",
        ];
    }
}
