@extends('emails.layout')

@section('title')
    {{ app()->getLocale() === 'th' ? 'รีเซ็ตรหัสผ่าน' : 'Reset Password' }} - {{ config('app.name') }}
@endsection

@section('content')
    @if(app()->getLocale() === 'th')
        <h2>รีเซ็ตรหัสผ่าน 🔐</h2>

        <p>สวัสดี, {{ $user->name }}</p>

        <p>เราได้รับคำขอรีเซ็ตรหัสผ่านสำหรับบัญชีของคุณ</p>

        <div class="text-center">
            <a href="{{ $resetUrl }}" class="btn">รีเซ็ตรหัสผ่าน</a>
        </div>

        <div class="info-box warning">
            <strong>ลิงก์นี้จะหมดอายุใน {{ $expireMinutes }} นาที</strong>
        </div>

        <p>หากคุณไม่ได้ขอรีเซ็ตรหัสผ่าน ไม่ต้องดำเนินการใดๆ รหัสผ่านของคุณจะไม่ถูกเปลี่ยนแปลง</p>

        <p class="text-small text-muted mt-20">
            หากปุ่มด้านบนไม่ทำงาน ให้คัดลอก URL นี้ไปวางในเบราว์เซอร์:<br>
            <a href="{{ $resetUrl }}" style="word-break: break-all;">{{ $resetUrl }}</a>
        </p>
    @else
        <h2>Reset Password 🔐</h2>

        <p>Hello, {{ $user->name }}</p>

        <p>We received a request to reset the password for your account.</p>

        <div class="text-center">
            <a href="{{ $resetUrl }}" class="btn">Reset Password</a>
        </div>

        <div class="info-box warning">
            <strong>This link will expire in {{ $expireMinutes }} minutes</strong>
        </div>

        <p>If you didn't request a password reset, no action is needed. Your password will remain unchanged.</p>

        <p class="text-small text-muted mt-20">
            If the button above doesn't work, copy and paste this URL into your browser:<br>
            <a href="{{ $resetUrl }}" style="word-break: break-all;">{{ $resetUrl }}</a>
        </p>
    @endif
@endsection
