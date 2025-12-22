@extends('emails.layout')

@section('title')
    {{ app()->getLocale() === 'th' ? 'แพ็กเกจใกล้หมดอายุ' : 'Package Expiring Soon' }} - {{ config('app.name') }}
@endsection

@section('content')
    @if(app()->getLocale() === 'th')
        <h2>แพ็กเกจของคุณใกล้หมดอายุ ⏰</h2>

        <p>สวัสดี, {{ $user->name }}</p>

        <div class="info-box warning">
            แพ็กเกจ <strong>{{ $rental->package->name }}</strong> ของคุณจะหมดอายุใน <strong>{{ $daysLeft }} วัน</strong>
            ({{ $rental->expires_at->format('d/m/Y') }})
        </div>

        <p>เพื่อให้การใช้งานไม่สะดุด กรุณาต่ออายุแพ็กเกจก่อนวันหมดอายุ</p>

        <p><strong>หากแพ็กเกจหมดอายุ:</strong></p>
        <ul>
            <li>❌ ไม่สามารถสร้างโพสต์ใหม่ได้</li>
            <li>❌ ไม่สามารถใช้ AI สร้างเนื้อหาได้</li>
            <li>❌ การตั้งเวลาโพสต์จะถูกระงับ</li>
            <li>✅ ข้อมูลของคุณยังคงปลอดภัย</li>
        </ul>

        <div class="text-center">
            <a href="{{ $renewUrl }}" class="btn btn-warning">ต่ออายุแพ็กเกจ</a>
        </div>

        <p class="text-small text-muted mt-20">
            หากคุณเปิดใช้งานการต่ออายุอัตโนมัติ ระบบจะทำการต่ออายุให้โดยอัตโนมัติ
        </p>
    @else
        <h2>Your Package is Expiring Soon ⏰</h2>

        <p>Hello, {{ $user->name }}</p>

        <div class="info-box warning">
            Your <strong>{{ $rental->package->name }}</strong> package will expire in <strong>{{ $daysLeft }} days</strong>
            ({{ $rental->expires_at->format('M d, Y') }})
        </div>

        <p>To avoid service interruption, please renew your package before it expires.</p>

        <p><strong>If your package expires:</strong></p>
        <ul>
            <li>❌ You cannot create new posts</li>
            <li>❌ AI content generation will be disabled</li>
            <li>❌ Scheduled posts will be suspended</li>
            <li>✅ Your data will remain safe</li>
        </ul>

        <div class="text-center">
            <a href="{{ $renewUrl }}" class="btn btn-warning">Renew Package</a>
        </div>

        <p class="text-small text-muted mt-20">
            If you have auto-renewal enabled, the system will renew your package automatically.
        </p>
    @endif
@endsection
