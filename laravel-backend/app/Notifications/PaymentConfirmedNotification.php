<?php

declare(strict_types=1);

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\Payment;

/**
 * Notification when payment is confirmed
 */
class PaymentConfirmedNotification extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public Payment $payment
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $amount = number_format($this->payment->amount, 2);
        $method = $this->payment->payment_method_label ?? $this->payment->payment_method;
        $date = $this->payment->created_at->format('d/m/Y H:i');

        return (new MailMessage)
            ->subject('ยืนยันการชำระเงินสำเร็จ')
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line('การชำระเงินของคุณได้รับการยืนยันแล้ว')
            ->line('')
            ->line("รายละเอียดการชำระเงิน:")
            ->line("• หมายเลขอ้างอิง: {$this->payment->uuid}")
            ->line("• จำนวนเงิน: ฿{$amount}")
            ->line("• ช่องทาง: {$method}")
            ->line("• วันที่: {$date}")
            ->line('')
            ->action('ดูใบเสร็จ', url("/rentals/invoices/{$this->payment->invoice_id}"))
            ->line('ขอบคุณที่ใช้บริการ PostXAgent!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'payment_confirmed',
            'payment_id' => $this->payment->id,
            'payment_uuid' => $this->payment->uuid,
            'amount' => $this->payment->amount,
            'payment_method' => $this->payment->payment_method,
            'message' => 'การชำระเงินได้รับการยืนยันแล้ว',
        ];
    }
}
