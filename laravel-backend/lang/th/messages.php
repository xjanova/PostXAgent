<?php

return [
    // General
    'success' => 'สำเร็จ',
    'error' => 'เกิดข้อผิดพลาด',
    'created' => 'สร้างสำเร็จ',
    'updated' => 'อัพเดทสำเร็จ',
    'deleted' => 'ลบสำเร็จ',
    'not_found' => 'ไม่พบข้อมูล',
    'unauthorized' => 'คุณไม่มีสิทธิ์เข้าถึง',
    'forbidden' => 'ไม่ได้รับอนุญาต',
    'validation_failed' => 'ข้อมูลไม่ถูกต้อง',

    // Auth
    'auth' => [
        'login_success' => 'เข้าสู่ระบบสำเร็จ',
        'logout_success' => 'ออกจากระบบสำเร็จ',
        'register_success' => 'ลงทะเบียนสำเร็จ',
        'invalid_credentials' => 'อีเมลหรือรหัสผ่านไม่ถูกต้อง',
        'account_disabled' => 'บัญชีถูกระงับ',
        'password_reset_sent' => 'ส่งลิงก์รีเซ็ตรหัสผ่านไปยังอีเมลแล้ว',
        'password_reset_success' => 'เปลี่ยนรหัสผ่านสำเร็จ',
        'token_expired' => 'โทเค็นหมดอายุ',
        'token_invalid' => 'โทเค็นไม่ถูกต้อง',
    ],

    // Brands
    'brands' => [
        'created' => 'สร้างแบรนด์สำเร็จ',
        'updated' => 'อัพเดทแบรนด์สำเร็จ',
        'deleted' => 'ลบแบรนด์สำเร็จ',
        'not_found' => 'ไม่พบแบรนด์',
        'limit_reached' => 'ถึงจำนวนแบรนด์สูงสุดแล้ว',
    ],

    // Posts
    'posts' => [
        'created' => 'สร้างโพสต์สำเร็จ',
        'updated' => 'อัพเดทโพสต์สำเร็จ',
        'deleted' => 'ลบโพสต์สำเร็จ',
        'scheduled' => 'ตั้งเวลาโพสต์สำเร็จ',
        'published' => 'เผยแพร่โพสต์สำเร็จ',
        'publish_failed' => 'ไม่สามารถเผยแพร่โพสต์ได้',
        'not_found' => 'ไม่พบโพสต์',
        'limit_reached' => 'ถึงจำนวนโพสต์สูงสุดต่อเดือนแล้ว',
    ],

    // Campaigns
    'campaigns' => [
        'created' => 'สร้างแคมเปญสำเร็จ',
        'updated' => 'อัพเดทแคมเปญสำเร็จ',
        'deleted' => 'ลบแคมเปญสำเร็จ',
        'started' => 'เริ่มแคมเปญสำเร็จ',
        'paused' => 'หยุดแคมเปญชั่วคราวสำเร็จ',
        'stopped' => 'หยุดแคมเปญสำเร็จ',
        'not_found' => 'ไม่พบแคมเปญ',
    ],

    // Social Accounts
    'social_accounts' => [
        'connected' => 'เชื่อมต่อบัญชีสำเร็จ',
        'disconnected' => 'ยกเลิกการเชื่อมต่อสำเร็จ',
        'refreshed' => 'รีเฟรชโทเค็นสำเร็จ',
        'connection_failed' => 'ไม่สามารถเชื่อมต่อได้',
        'token_expired' => 'โทเค็นหมดอายุ กรุณาเชื่อมต่อใหม่',
        'not_found' => 'ไม่พบบัญชี Social',
    ],

    // AI Content
    'ai' => [
        'content_generated' => 'สร้างเนื้อหาสำเร็จ',
        'image_generated' => 'สร้างรูปภาพสำเร็จ',
        'generation_failed' => 'ไม่สามารถสร้างได้',
        'limit_reached' => 'ถึงจำนวนการสร้าง AI สูงสุดแล้ว',
        'provider_unavailable' => 'ผู้ให้บริการ AI ไม่พร้อมใช้งาน',
    ],

    // Rentals
    'rentals' => [
        'created' => 'สมัครแพ็กเกจสำเร็จ',
        'activated' => 'เปิดใช้งานแพ็กเกจสำเร็จ',
        'cancelled' => 'ยกเลิกแพ็กเกจสำเร็จ',
        'renewed' => 'ต่ออายุแพ็กเกจสำเร็จ',
        'expired' => 'แพ็กเกจหมดอายุแล้ว',
        'no_active_rental' => 'คุณไม่มีแพ็กเกจที่ใช้งานอยู่',
        'payment_pending' => 'รอการชำระเงิน',
        'payment_confirmed' => 'ยืนยันการชำระเงินสำเร็จ',
        'payment_rejected' => 'การชำระเงินถูกปฏิเสธ',
    ],

    // Workflows
    'workflows' => [
        'created' => 'สร้าง Workflow สำเร็จ',
        'updated' => 'อัพเดท Workflow สำเร็จ',
        'deleted' => 'ลบ Workflow สำเร็จ',
        'executed' => 'รัน Workflow สำเร็จ',
        'execution_failed' => 'ไม่สามารถรัน Workflow ได้',
        'not_found' => 'ไม่พบ Workflow',
    ],

    // Comments
    'comments' => [
        'replied' => 'ตอบกลับคอมเมนต์สำเร็จ',
        'reply_failed' => 'ไม่สามารถตอบกลับได้',
        'analyzed' => 'วิเคราะห์คอมเมนต์สำเร็จ',
        'skipped' => 'ข้ามคอมเมนต์แล้ว',
    ],

    // Roles
    'roles' => [
        'created' => 'สร้าง Role สำเร็จ',
        'updated' => 'อัพเดท Role สำเร็จ',
        'deleted' => 'ลบ Role สำเร็จ',
        'assigned' => 'กำหนด Role สำเร็จ',
        'removed' => 'ลบ Role จากผู้ใช้สำเร็จ',
        'protected' => 'ไม่สามารถแก้ไข Role นี้ได้',
    ],

    // Payments
    'payments' => [
        'approved' => 'อนุมัติการชำระเงินสำเร็จ',
        'rejected' => 'ปฏิเสธการชำระเงินสำเร็จ',
        'refunded' => 'คืนเงินสำเร็จ',
        'slip_uploaded' => 'อัพโหลดสลิปสำเร็จ',
    ],

    // AI Manager
    'ai_manager' => [
        'connected' => 'เชื่อมต่อ AI Manager สำเร็จ',
        'disconnected' => 'AI Manager ไม่ตอบสนอง',
        'started' => 'เริ่มต้น AI Manager สำเร็จ',
        'stopped' => 'หยุด AI Manager สำเร็จ',
    ],
];
