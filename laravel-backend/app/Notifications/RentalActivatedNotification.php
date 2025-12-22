<?php

declare(strict_types=1);

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\UserRental;

/**
 * Notification when rental is activated after payment
 */
class RentalActivatedNotification extends Notification implements ShouldQueue
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
        $expiryDate = $this->rental->expires_at->format('d/m/Y H:i');
        $postsLimit = $this->rental->rentalPackage->posts_limit ?? 'ไม่จำกัด';
        $aiLimit = $this->rental->rentalPackage->ai_generations_limit ?? 'ไม่จำกัด';

        return (new MailMessage)
            ->subject("ยินดีต้อนรับ! แพ็กเกจ {$packageName} พร้อมใช้งานแล้ว")
            ->greeting('ยินดีต้อนรับ ' . $notifiable->name . '!')
            ->line("ขอบคุณที่เลือกใช้ PostXAgent!")
            ->line("แพ็กเกจ {$packageName} ของคุณพร้อมใช้งานแล้ว")
            ->line('')
            ->line("รายละเอียดแพ็กเกจ:")
            ->line("• โพสต์: {$postsLimit} ครั้ง/รอบบิล")
            ->line("• AI Generation: {$aiLimit} ครั้ง/รอบบิล")
            ->line("• หมดอายุ: {$expiryDate}")
            ->line('')
            ->action('เริ่มใช้งาน', url('/dashboard'))
            ->line('หากมีคำถามหรือต้องการความช่วยเหลือ ติดต่อทีมซัพพอร์ตได้ตลอดเวลา');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'rental_activated',
            'rental_id' => $this->rental->id,
            'package_name' => $this->rental->rentalPackage->name ?? 'แพ็กเกจ',
            'starts_at' => $this->rental->starts_at?->toIso8601String(),
            'expires_at' => $this->rental->expires_at->toIso8601String(),
            'message' => 'แพ็กเกจของคุณพร้อมใช้งานแล้ว!',
        ];
    }
}
