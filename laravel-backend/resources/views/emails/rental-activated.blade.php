@extends('emails.layout')

@section('title')
    {{ app()->getLocale() === 'th' ? 'เปิดใช้งานแพ็กเกจสำเร็จ' : 'Package Activated' }} - {{ config('app.name') }}
@endsection

@section('content')
    @if(app()->getLocale() === 'th')
        <h2>เปิดใช้งานแพ็กเกจสำเร็จ! 🎉</h2>

        <p>สวัสดี, {{ $user->name }}</p>

        <p>แพ็กเกจ <strong>{{ $rental->package->name }}</strong> ของคุณได้รับการเปิดใช้งานเรียบร้อยแล้ว</p>

        <div class="info-box success">
            <strong>รายละเอียดแพ็กเกจ</strong>
        </div>

        <table class="data-table">
            <tr>
                <th>แพ็กเกจ</th>
                <td>{{ $rental->package->name }}</td>
            </tr>
            <tr>
                <th>ราคา</th>
                <td>฿{{ number_format($rental->amount, 2) }}</td>
            </tr>
            <tr>
                <th>วันเริ่มต้น</th>
                <td>{{ $rental->starts_at->format('d/m/Y') }}</td>
            </tr>
            <tr>
                <th>วันหมดอายุ</th>
                <td>{{ $rental->expires_at->format('d/m/Y') }}</td>
            </tr>
        </table>

        <p><strong>สิทธิประโยชน์ที่คุณได้รับ:</strong></p>
        <ul>
            <li>📝 โพสต์ได้ {{ $rental->package->limits['posts'] ?? 'ไม่จำกัด' }} โพสต์/เดือน</li>
            <li>🏢 สร้างแบรนด์ได้ {{ $rental->package->limits['brands'] ?? 'ไม่จำกัด' }} แบรนด์</li>
            <li>🤖 สร้างเนื้อหา AI ได้ {{ $rental->package->limits['ai_generations'] ?? 'ไม่จำกัด' }} ครั้ง/เดือน</li>
            <li>📱 เชื่อมต่อ {{ $rental->package->limits['platforms'] ?? 9 }} แพลตฟอร์ม</li>
        </ul>

        <div class="text-center">
            <a href="{{ $dashboardUrl }}" class="btn btn-success">เริ่มใช้งานเลย</a>
        </div>

        <p class="text-small text-muted">
            แพ็กเกจจะต่ออายุอัตโนมัติก่อนวันหมดอายุ 3 วัน หากคุณเปิดใช้งานการต่ออายุอัตโนมัติ
        </p>
    @else
        <h2>Package Activated Successfully! 🎉</h2>

        <p>Hello, {{ $user->name }}</p>

        <p>Your <strong>{{ $rental->package->name }}</strong> package has been activated successfully.</p>

        <div class="info-box success">
            <strong>Package Details</strong>
        </div>

        <table class="data-table">
            <tr>
                <th>Package</th>
                <td>{{ $rental->package->name }}</td>
            </tr>
            <tr>
                <th>Price</th>
                <td>฿{{ number_format($rental->amount, 2) }}</td>
            </tr>
            <tr>
                <th>Start Date</th>
                <td>{{ $rental->starts_at->format('M d, Y') }}</td>
            </tr>
            <tr>
                <th>Expiry Date</th>
                <td>{{ $rental->expires_at->format('M d, Y') }}</td>
            </tr>
        </table>

        <p><strong>Your Benefits:</strong></p>
        <ul>
            <li>📝 {{ $rental->package->limits['posts'] ?? 'Unlimited' }} posts/month</li>
            <li>🏢 {{ $rental->package->limits['brands'] ?? 'Unlimited' }} brands</li>
            <li>🤖 {{ $rental->package->limits['ai_generations'] ?? 'Unlimited' }} AI generations/month</li>
            <li>📱 {{ $rental->package->limits['platforms'] ?? 9 }} platforms</li>
        </ul>

        <div class="text-center">
            <a href="{{ $dashboardUrl }}" class="btn btn-success">Start Using Now</a>
        </div>

        <p class="text-small text-muted">
            Your package will auto-renew 3 days before expiry if auto-renewal is enabled.
        </p>
    @endif
@endsection
