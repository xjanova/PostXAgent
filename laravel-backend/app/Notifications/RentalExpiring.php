<?php

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\UserRental;

class RentalExpiring extends Notification implements ShouldQueue
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
        $packageName = $this->rental->rentalPackage->display_name ?? 'แพ็กเกจ';
        $expiryDate = $this->rental->expires_at->format('d/m/Y');

        $subject = $this->daysRemaining <= 1
            ? "แพ็กเกจของคุณจะหมดอายุวันนี้!"
            : "แพ็กเกจของคุณจะหมดอายุใน {$this->daysRemaining} วัน";

        return (new MailMessage)
            ->subject($subject)
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line("แพ็กเกจ {$packageName} ของคุณจะหมดอายุในวันที่ {$expiryDate}")
            ->line("เหลืออีก {$this->daysRemaining} วัน")
            ->line('ต่ออายุตอนนี้เพื่อไม่ให้บริการหยุดชะงัก!')
            ->action('ต่ออายุแพ็กเกจ', url('/subscription'))
            ->line('ขอบคุณที่ใช้บริการ PostXAgent!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'rental_expiring',
            'rental_id' => $this->rental->id,
            'package_name' => $this->rental->rentalPackage->display_name ?? 'แพ็กเกจ',
            'expires_at' => $this->rental->expires_at->toIso8601String(),
            'days_remaining' => $this->daysRemaining,
            'message' => "แพ็กเกจของคุณจะหมดอายุใน {$this->daysRemaining} วัน",
        ];
    }
}
