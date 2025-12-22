@extends('emails.layout')

@section('title')
    {{ app()->getLocale() === 'th' ? 'ได้รับการชำระเงินแล้ว' : 'Payment Received' }} - {{ config('app.name') }}
@endsection

@section('content')
    @if(app()->getLocale() === 'th')
        <h2>ได้รับการชำระเงินแล้ว ✅</h2>

        <p>สวัสดี, {{ $user->name }}</p>

        <p>เราได้รับการชำระเงินของคุณเรียบร้อยแล้ว และกำลังตรวจสอบความถูกต้อง</p>

        <div class="info-box">
            <strong>รายละเอียดการชำระเงิน</strong>
        </div>

        <table class="data-table">
            <tr>
                <th>หมายเลขอ้างอิง</th>
                <td>{{ $payment->reference_number }}</td>
            </tr>
            <tr>
                <th>แพ็กเกจ</th>
                <td>{{ $payment->rental->package->name }}</td>
            </tr>
            <tr>
                <th>จำนวนเงิน</th>
                <td>฿{{ number_format($payment->amount, 2) }}</td>
            </tr>
            <tr>
                <th>วิธีชำระ</th>
                <td>{{ $payment->payment_method }}</td>
            </tr>
            <tr>
                <th>วันที่ชำระ</th>
                <td>{{ $payment->created_at->format('d/m/Y H:i') }}</td>
            </tr>
            <tr>
                <th>สถานะ</th>
                <td>
                    @if($payment->status === 'pending')
                        <span style="color: #f39c12;">รอตรวจสอบ</span>
                    @elseif($payment->status === 'confirmed')
                        <span style="color: #27ae60;">ยืนยันแล้ว</span>
                    @endif
                </td>
            </tr>
        </table>

        <p><strong>ขั้นตอนถัดไป:</strong></p>
        <ol>
            <li>ทีมงานกำลังตรวจสอบการชำระเงินของคุณ</li>
            <li>คุณจะได้รับอีเมลยืนยันภายใน 1-2 ชั่วโมง</li>
            <li>หลังจากยืนยัน แพ็กเกจจะเปิดใช้งานทันที</li>
        </ol>

        <div class="text-center">
            <a href="{{ $dashboardUrl }}" class="btn">ไปที่ Dashboard</a>
        </div>

        <p class="text-small text-muted">
            หากมีข้อสงสัย กรุณาติดต่อทีมสนับสนุนพร้อมหมายเลขอ้างอิงของคุณ
        </p>
    @else
        <h2>Payment Received ✅</h2>

        <p>Hello, {{ $user->name }}</p>

        <p>We have received your payment and are currently verifying it.</p>

        <div class="info-box">
            <strong>Payment Details</strong>
        </div>

        <table class="data-table">
            <tr>
                <th>Reference Number</th>
                <td>{{ $payment->reference_number }}</td>
            </tr>
            <tr>
                <th>Package</th>
                <td>{{ $payment->rental->package->name }}</td>
            </tr>
            <tr>
                <th>Amount</th>
                <td>฿{{ number_format($payment->amount, 2) }}</td>
            </tr>
            <tr>
                <th>Payment Method</th>
                <td>{{ $payment->payment_method }}</td>
            </tr>
            <tr>
                <th>Payment Date</th>
                <td>{{ $payment->created_at->format('M d, Y H:i') }}</td>
            </tr>
            <tr>
                <th>Status</th>
                <td>
                    @if($payment->status === 'pending')
                        <span style="color: #f39c12;">Pending Verification</span>
                    @elseif($payment->status === 'confirmed')
                        <span style="color: #27ae60;">Confirmed</span>
                    @endif
                </td>
            </tr>
        </table>

        <p><strong>Next Steps:</strong></p>
        <ol>
            <li>Our team is verifying your payment</li>
            <li>You will receive confirmation email within 1-2 hours</li>
            <li>Your package will be activated immediately after confirmation</li>
        </ol>

        <div class="text-center">
            <a href="{{ $dashboardUrl }}" class="btn">Go to Dashboard</a>
        </div>

        <p class="text-small text-muted">
            If you have any questions, please contact support with your reference number.
        </p>
    @endif
@endsection
