<?php

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\Payment;

class PaymentReceived extends Notification implements ShouldQueue
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
        $currency = $this->payment->currency ?? 'THB';

        return (new MailMessage)
            ->subject('ได้รับการชำระเงินเรียบร้อยแล้ว')
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line('เราได้รับการชำระเงินของคุณเรียบร้อยแล้ว')
            ->line("จำนวนเงิน: {$amount} {$currency}")
            ->line("หมายเลขอ้างอิง: {$this->payment->uuid}")
            ->line("วันที่ชำระ: {$this->payment->created_at->format('d/m/Y H:i')}")
            ->action('ดูรายละเอียด', url('/subscription'))
            ->line('ขอบคุณที่ใช้บริการ PostXAgent!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'payment_received',
            'payment_id' => $this->payment->id,
            'payment_uuid' => $this->payment->uuid,
            'amount' => $this->payment->amount,
            'currency' => $this->payment->currency ?? 'THB',
            'message' => "ได้รับการชำระเงิน {$this->payment->amount} บาท",
        ];
    }
}
