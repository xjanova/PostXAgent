@extends('emails.layout')

@section('title')
    {{ app()->getLocale() === 'th' ? 'ยินดีต้อนรับ' : 'Welcome' }} - {{ config('app.name') }}
@endsection

@section('content')
    @if(app()->getLocale() === 'th')
        <h2>สวัสดี, {{ $user->name }}! 👋</h2>

        <p>ยินดีต้อนรับสู่ <strong>{{ config('app.name') }}</strong> - ระบบจัดการโปรโมทแบรนด์ด้วย AI ที่ทรงพลังที่สุด</p>

        <p>คุณสามารถเริ่มต้นใช้งานได้ทันทีด้วยฟีเจอร์เหล่านี้:</p>

        <ul>
            <li>🎯 สร้างและจัดการแบรนด์ของคุณ</li>
            <li>📱 เชื่อมต่อ Social Media ทั้ง 9 แพลตฟอร์ม</li>
            <li>🤖 สร้างเนื้อหาด้วย AI อัตโนมัติ</li>
            <li>📊 วิเคราะห์ผลลัพธ์และติดตามความสำเร็จ</li>
            <li>⏰ ตั้งเวลาโพสต์อัตโนมัติ</li>
        </ul>

        <div class="text-center">
            <a href="{{ $dashboardUrl }}" class="btn">เข้าสู่ Dashboard</a>
        </div>

        <div class="info-box">
            <strong>ต้องการความช่วยเหลือ?</strong><br>
            ทีมงานของเราพร้อมช่วยเหลือคุณตลอด 24 ชั่วโมง
        </div>

        <p>ขอบคุณที่เลือกใช้บริการของเรา</p>
        <p>ทีมงาน {{ config('app.name') }}</p>
    @else
        <h2>Hello, {{ $user->name }}! 👋</h2>

        <p>Welcome to <strong>{{ config('app.name') }}</strong> - the most powerful AI-powered brand promotion management system.</p>

        <p>You can start using these features right away:</p>

        <ul>
            <li>🎯 Create and manage your brands</li>
            <li>📱 Connect to all 9 social media platforms</li>
            <li>🤖 Generate content with AI automatically</li>
            <li>📊 Analyze results and track success</li>
            <li>⏰ Schedule posts automatically</li>
        </ul>

        <div class="text-center">
            <a href="{{ $dashboardUrl }}" class="btn">Go to Dashboard</a>
        </div>

        <div class="info-box">
            <strong>Need help?</strong><br>
            Our team is ready to assist you 24/7.
        </div>

        <p>Thank you for choosing our service.</p>
        <p>The {{ config('app.name') }} Team</p>
    @endif
@endsection
