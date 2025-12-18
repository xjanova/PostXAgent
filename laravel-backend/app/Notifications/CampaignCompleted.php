<?php

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\Campaign;

class CampaignCompleted extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public Campaign $campaign,
        public array $summary
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $totalPosts = $this->summary['total_posts'] ?? 0;
        $totalLikes = number_format($this->summary['total_likes'] ?? 0);
        $totalEngagement = number_format($this->summary['total_engagement'] ?? 0);

        return (new MailMessage)
            ->subject("แคมเปญ '{$this->campaign->name}' เสร็จสิ้นแล้ว")
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line("แคมเปญ '{$this->campaign->name}' ได้ดำเนินการเสร็จสิ้นแล้ว")
            ->line('สรุปผลการดำเนินงาน:')
            ->line("- จำนวนโพสต์ทั้งหมด: {$totalPosts}")
            ->line("- รวมไลค์ทั้งหมด: {$totalLikes}")
            ->line("- รวม Engagement: {$totalEngagement}")
            ->action('ดูรายงานแคมเปญ', url('/campaigns/' . $this->campaign->id))
            ->line('ขอบคุณที่ใช้บริการ PostXAgent!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'campaign_completed',
            'campaign_id' => $this->campaign->id,
            'campaign_name' => $this->campaign->name,
            'summary' => $this->summary,
            'message' => "แคมเปญ '{$this->campaign->name}' เสร็จสิ้นแล้ว",
        ];
    }
}
