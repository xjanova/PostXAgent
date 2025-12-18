<?php

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\SocialAccount;

class AccountWarning extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public SocialAccount $account,
        public string $warningType,
        public string $warningMessage
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $platformName = ucfirst($this->account->platform);
        $accountName = $this->account->display_name ?? $this->account->platform_username;

        $subject = match ($this->warningType) {
            'token_expiring' => "Token ของบัญชี {$platformName} ใกล้หมดอายุ",
            'rate_limited' => "บัญชี {$platformName} ถูกจำกัดการใช้งาน",
            'suspended' => "บัญชี {$platformName} ถูกระงับ",
            default => "แจ้งเตือนบัญชี {$platformName}",
        };

        return (new MailMessage)
            ->subject($subject)
            ->greeting('สวัสดี ' . $notifiable->name)
            ->line("มีปัญหากับบัญชี {$accountName} ({$platformName})")
            ->line("ประเภทปัญหา: {$this->warningType}")
            ->line("รายละเอียด: {$this->warningMessage}")
            ->action('จัดการบัญชี', url('/social-accounts'))
            ->line('กรุณาดำเนินการแก้ไขเพื่อให้บริการทำงานได้ต่อเนื่อง');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'account_warning',
            'account_id' => $this->account->id,
            'platform' => $this->account->platform,
            'warning_type' => $this->warningType,
            'message' => $this->warningMessage,
        ];
    }
}
