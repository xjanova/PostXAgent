<?php

declare(strict_types=1);

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\UserRental;

/**
 * Notification when rental has expired
 */
class RentalExpiredNotification extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public UserRental $rental
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $packageName = $this->rental->rentalPackage->name ?? 'แพ็กเกจ';

        return (new MailMessage)
            ->subject('แพ็กเกจของคุณหมดอายุแล้ว')
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line("แพ็กเกจ {$packageName} ของคุณหมดอายุแล้ว")
            ->line('คุณจะไม่สามารถใช้งานฟีเจอร์ต่างๆ ได้จนกว่าจะซื้อแพ็กเกจใหม่')
            ->line('เรามีแพ็กเกจหลากหลายให้เลือกตามความต้องการของคุณ!')
            ->action('ซื้อแพ็กเกจใหม่', url('/pricing'))
            ->line('ขอบคุณที่ใช้บริการ PostXAgent!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'rental_expired',
            'rental_id' => $this->rental->id,
            'package_name' => $this->rental->rentalPackage->name ?? 'แพ็กเกจ',
            'expired_at' => $this->rental->expires_at->toIso8601String(),
            'message' => 'แพ็กเกจของคุณหมดอายุแล้ว กรุณาซื้อแพ็กเกจใหม่',
        ];
    }
}
