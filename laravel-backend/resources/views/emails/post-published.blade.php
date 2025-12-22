@extends('emails.layout')

@section('title')
    {{ app()->getLocale() === 'th' ? 'โพสต์เผยแพร่สำเร็จ' : 'Post Published' }} - {{ config('app.name') }}
@endsection

@section('content')
    @if(app()->getLocale() === 'th')
        <h2>โพสต์เผยแพร่สำเร็จ! 🎉</h2>

        <p>สวัสดี, {{ $user->name }}</p>

        <p>โพสต์ของคุณได้รับการเผยแพร่สำเร็จไปยังแพลตฟอร์มที่เลือก</p>

        <div class="info-box success">
            <strong>รายละเอียดโพสต์</strong>
        </div>

        <table class="data-table">
            <tr>
                <th>แบรนด์</th>
                <td>{{ $post->brand->name }}</td>
            </tr>
            <tr>
                <th>แพลตฟอร์ม</th>
                <td>
                    @foreach($post->platforms as $platform)
                        <span style="display: inline-block; padding: 2px 8px; background: #e8f4fc; border-radius: 12px; font-size: 12px; margin: 2px;">
                            {{ ucfirst($platform) }}
                        </span>
                    @endforeach
                </td>
            </tr>
            <tr>
                <th>เวลาเผยแพร่</th>
                <td>{{ $post->published_at->format('d/m/Y H:i') }}</td>
            </tr>
        </table>

        <div style="background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;">
            <p style="margin: 0; color: #666; font-size: 14px;">เนื้อหา:</p>
            <p style="margin: 10px 0 0; white-space: pre-wrap;">{{ Str::limit($post->content, 300) }}</p>
        </div>

        @if($post->image_url)
        <div style="text-align: center; margin: 20px 0;">
            <img src="{{ $post->image_url }}" alt="Post Image" style="max-width: 100%; border-radius: 8px;">
        </div>
        @endif

        <div class="text-center">
            <a href="{{ $postUrl }}" class="btn btn-success">ดูโพสต์</a>
        </div>

        <p class="text-small text-muted mt-20">
            คุณสามารถดูสถิติและ engagement ได้ที่หน้า Analytics
        </p>
    @else
        <h2>Post Published Successfully! 🎉</h2>

        <p>Hello, {{ $user->name }}</p>

        <p>Your post has been published successfully to the selected platforms.</p>

        <div class="info-box success">
            <strong>Post Details</strong>
        </div>

        <table class="data-table">
            <tr>
                <th>Brand</th>
                <td>{{ $post->brand->name }}</td>
            </tr>
            <tr>
                <th>Platforms</th>
                <td>
                    @foreach($post->platforms as $platform)
                        <span style="display: inline-block; padding: 2px 8px; background: #e8f4fc; border-radius: 12px; font-size: 12px; margin: 2px;">
                            {{ ucfirst($platform) }}
                        </span>
                    @endforeach
                </td>
            </tr>
            <tr>
                <th>Published At</th>
                <td>{{ $post->published_at->format('M d, Y H:i') }}</td>
            </tr>
        </table>

        <div style="background: #f8f9fa; padding: 15px; border-radius: 8px; margin: 20px 0;">
            <p style="margin: 0; color: #666; font-size: 14px;">Content:</p>
            <p style="margin: 10px 0 0; white-space: pre-wrap;">{{ Str::limit($post->content, 300) }}</p>
        </div>

        @if($post->image_url)
        <div style="text-align: center; margin: 20px 0;">
            <img src="{{ $post->image_url }}" alt="Post Image" style="max-width: 100%; border-radius: 8px;">
        </div>
        @endif

        <div class="text-center">
            <a href="{{ $postUrl }}" class="btn btn-success">View Post</a>
        </div>

        <p class="text-small text-muted mt-20">
            You can view statistics and engagement on the Analytics page.
        </p>
    @endif
@endsection
