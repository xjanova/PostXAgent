<?php

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\Post;

class PostPublished extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public Post $post
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $platformName = ucfirst($this->post->platform);

        return (new MailMessage)
            ->subject("โพสต์ของคุณถูกเผยแพร่บน {$platformName} แล้ว")
            ->greeting('สวัสดี ' . $notifiable->name . '!')
            ->line("โพสต์ของคุณถูกเผยแพร่เรียบร้อยแล้ว")
            ->line("แพลตฟอร์ม: {$platformName}")
            ->line("เนื้อหา: " . \Illuminate\Support\Str::limit($this->post->content_text, 100))
            ->action('ดูโพสต์', $this->post->platform_url ?? url('/posts/' . $this->post->id))
            ->line('ขอบคุณที่ใช้บริการ PostXAgent!');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'post_published',
            'post_id' => $this->post->id,
            'platform' => $this->post->platform,
            'brand_id' => $this->post->brand_id,
            'message' => "โพสต์บน {$this->post->platform} ถูกเผยแพร่แล้ว",
        ];
    }
}
