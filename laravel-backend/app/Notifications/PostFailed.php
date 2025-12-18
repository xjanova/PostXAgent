<?php

namespace App\Notifications;

use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Messages\MailMessage;
use Illuminate\Notifications\Notification;
use App\Models\Post;

class PostFailed extends Notification implements ShouldQueue
{
    use Queueable;

    public function __construct(
        public Post $post,
        public string $errorMessage
    ) {}

    public function via(object $notifiable): array
    {
        return ['mail', 'database'];
    }

    public function toMail(object $notifiable): MailMessage
    {
        $platformName = ucfirst($this->post->platform);

        return (new MailMessage)
            ->subject("โพสต์ของคุณบน {$platformName} ล้มเหลว")
            ->greeting('สวัสดี ' . $notifiable->name)
            ->line("โพสต์ของคุณไม่สามารถเผยแพร่ได้")
            ->line("แพลตฟอร์ม: {$platformName}")
            ->line("เนื้อหา: " . \Illuminate\Support\Str::limit($this->post->content_text, 100))
            ->line("สาเหตุ: {$this->errorMessage}")
            ->action('ดูรายละเอียดและลองใหม่', url('/posts/' . $this->post->id))
            ->line('หากปัญหายังคงอยู่ กรุณาติดต่อทีมสนับสนุน');
    }

    public function toArray(object $notifiable): array
    {
        return [
            'type' => 'post_failed',
            'post_id' => $this->post->id,
            'platform' => $this->post->platform,
            'brand_id' => $this->post->brand_id,
            'error' => $this->errorMessage,
            'message' => "โพสต์บน {$this->post->platform} ล้มเหลว: {$this->errorMessage}",
        ];
    }
}
