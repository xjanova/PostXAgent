<!DOCTYPE html>
<html lang="{{ app()->getLocale() }}">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>@yield('title', config('app.name'))</title>
    <style>
        /* Reset */
        body, table, td, p, a, li, blockquote {
            -webkit-text-size-adjust: 100%;
            -ms-text-size-adjust: 100%;
        }
        table, td {
            mso-table-lspace: 0pt;
            mso-table-rspace: 0pt;
        }
        img {
            -ms-interpolation-mode: bicubic;
        }

        /* Base */
        body {
            margin: 0;
            padding: 0;
            width: 100% !important;
            height: 100% !important;
            background-color: #f4f4f4;
            font-family: 'Sarabun', 'Segoe UI', Arial, sans-serif;
        }

        /* Container */
        .email-wrapper {
            width: 100%;
            background-color: #f4f4f4;
            padding: 20px 0;
        }

        .email-container {
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
        }

        /* Header */
        .email-header {
            background: linear-gradient(135deg, #3498db 0%, #2c3e50 100%);
            padding: 30px 40px;
            text-align: center;
        }

        .email-header img {
            max-width: 150px;
            height: auto;
        }

        .email-header h1 {
            color: #ffffff;
            margin: 15px 0 0;
            font-size: 24px;
            font-weight: 600;
        }

        /* Body */
        .email-body {
            padding: 40px;
            color: #333333;
            line-height: 1.6;
        }

        .email-body h2 {
            color: #2c3e50;
            margin: 0 0 20px;
            font-size: 22px;
        }

        .email-body p {
            margin: 0 0 15px;
            font-size: 16px;
        }

        /* Button */
        .btn {
            display: inline-block;
            padding: 14px 32px;
            background-color: #3498db;
            color: #ffffff !important;
            text-decoration: none;
            border-radius: 6px;
            font-weight: 600;
            font-size: 16px;
            margin: 20px 0;
        }

        .btn:hover {
            background-color: #2980b9;
        }

        .btn-success {
            background-color: #27ae60;
        }

        .btn-warning {
            background-color: #f39c12;
        }

        .btn-danger {
            background-color: #e74c3c;
        }

        /* Info Box */
        .info-box {
            background-color: #f8f9fa;
            border-left: 4px solid #3498db;
            padding: 15px 20px;
            margin: 20px 0;
            border-radius: 0 4px 4px 0;
        }

        .info-box.success {
            border-left-color: #27ae60;
            background-color: #e8f5e9;
        }

        .info-box.warning {
            border-left-color: #f39c12;
            background-color: #fff8e1;
        }

        .info-box.danger {
            border-left-color: #e74c3c;
            background-color: #ffebee;
        }

        /* Table */
        .data-table {
            width: 100%;
            border-collapse: collapse;
            margin: 20px 0;
        }

        .data-table th,
        .data-table td {
            padding: 12px 15px;
            text-align: left;
            border-bottom: 1px solid #e0e0e0;
        }

        .data-table th {
            background-color: #f8f9fa;
            font-weight: 600;
            color: #666;
        }

        /* Footer */
        .email-footer {
            background-color: #f8f9fa;
            padding: 30px 40px;
            text-align: center;
            border-top: 1px solid #e0e0e0;
        }

        .email-footer p {
            margin: 0 0 10px;
            font-size: 14px;
            color: #666;
        }

        .email-footer a {
            color: #3498db;
            text-decoration: none;
        }

        .social-links {
            margin: 15px 0;
        }

        .social-links a {
            display: inline-block;
            margin: 0 8px;
        }

        .social-links img {
            width: 32px;
            height: 32px;
        }

        /* Utilities */
        .text-center {
            text-align: center;
        }

        .text-muted {
            color: #999;
        }

        .text-small {
            font-size: 13px;
        }

        .mt-20 {
            margin-top: 20px;
        }

        .mb-20 {
            margin-bottom: 20px;
        }

        /* Responsive */
        @media only screen and (max-width: 600px) {
            .email-container {
                width: 100% !important;
                border-radius: 0;
            }

            .email-header,
            .email-body,
            .email-footer {
                padding: 20px;
            }

            .email-body h2 {
                font-size: 20px;
            }

            .btn {
                display: block;
                text-align: center;
            }
        }
    </style>
    @stack('styles')
</head>
<body>
    <div class="email-wrapper">
        <table class="email-container" role="presentation">
            <!-- Header -->
            <tr>
                <td class="email-header">
                    @if(config('app.logo_url'))
                        <img src="{{ config('app.logo_url') }}" alt="{{ config('app.name') }}">
                    @endif
                    <h1>{{ config('app.name') }}</h1>
                </td>
            </tr>

            <!-- Body -->
            <tr>
                <td class="email-body">
                    @yield('content')
                </td>
            </tr>

            <!-- Footer -->
            <tr>
                <td class="email-footer">
                    <p>{{ config('app.name') }} - AI-Powered Brand Promotion Manager</p>
                    <p class="text-small text-muted">
                        @if(app()->getLocale() === 'th')
                            หากคุณไม่ได้ดำเนินการนี้ กรุณาติดต่อทีมสนับสนุนของเรา
                        @else
                            If you didn't perform this action, please contact our support team.
                        @endif
                    </p>
                    <p class="text-small text-muted">
                        &copy; {{ date('Y') }} {{ config('app.name') }}. All rights reserved.
                    </p>
                </td>
            </tr>
        </table>
    </div>
</body>
</html>
