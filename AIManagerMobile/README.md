# AI Manager Mobile

แอพ Android สำหรับจัดการ AI Manager Core บนมือถือ พัฒนาด้วย .NET MAUI

## คุณสมบัติ

### การเชื่อมต่อ AI Manager Core
- เชื่อมต่อผ่าน REST API และ SignalR สำหรับ real-time updates
- ดูสถานะระบบ (CPU, Memory, Workers)
- จัดการ Tasks (ดู, ยกเลิก)
- ควบคุม Workers (เริ่ม, หยุด, pause)

### การตรวจสอบ SMS
- ดักจับ SMS แจ้งเตือนจากธนาคาร
- รองรับธนาคารไทยทั้งหมด (กสิกร, SCB, กรุงเทพ, กรุงไทย, TMB, กรุงศรี, ออมสิน, ธกส)
- รองรับ PromptPay

### AI Payment Detection
- วิเคราะห์ SMS อัตโนมัติเพื่อตรวจจับการชำระเงิน
- คำนวณ Confidence Score
- สกัดข้อมูล: จำนวนเงิน, ธนาคาร, เลขบัญชี, เวลา, อ้างอิง
- อนุมัติอัตโนมัติ (ถ้าเปิดใช้งาน)

### Payment Gateway Integration
- อนุมัติการชำระเงินแบบ Manual หรือ Auto
- ส่งข้อมูลไปยัง AI Manager Core
- ไม่ต้องขอ API กับธนาคาร

## Requirements

- Visual Studio 2022 (17.8+) with .NET MAUI workload
- .NET 8.0 SDK
- Android SDK (API Level 24+)
- Android device หรือ emulator

## การติดตั้ง

### 1. ติดตั้ง .NET MAUI Workload

```bash
dotnet workload install maui
```

### 2. Clone และเปิดโปรเจค

```bash
cd D:\Code\PostXAgent\AIManagerMobile
```

เปิด `AIManagerMobile.sln` ใน Visual Studio 2022

### 3. Build และ Deploy

1. เลือก Target เป็น `Android Emulator` หรือ `Android Device`
2. กด F5 หรือ Run

## โครงสร้างโปรเจค

```
AIManagerMobile/
├── AIManagerMobile.sln
└── src/
    └── AIManager.Mobile/
        ├── Models/
        │   ├── SmsMessage.cs         # SMS และ Payment models
        │   ├── TaskModels.cs         # Task models
        │   ├── WorkerInfo.cs         # Worker status
        │   └── ApiResponse.cs        # API response models
        ├── Services/
        │   ├── IAIManagerApiService.cs
        │   ├── AIManagerApiService.cs   # REST API client
        │   ├── ISignalRService.cs
        │   ├── SignalRService.cs        # SignalR client
        │   ├── ISmsListenerService.cs
        │   ├── SmsListenerService.cs    # SMS listener
        │   ├── IPaymentDetectionService.cs
        │   ├── PaymentDetectionService.cs  # AI payment detection
        │   ├── ISettingsService.cs
        │   └── SettingsService.cs       # App settings
        ├── ViewModels/
        │   ├── BaseViewModel.cs
        │   ├── DashboardViewModel.cs
        │   ├── TasksViewModel.cs
        │   ├── WorkersViewModel.cs
        │   ├── SmsMonitorViewModel.cs
        │   ├── PaymentsViewModel.cs
        │   └── SettingsViewModel.cs
        ├── Views/
        │   ├── DashboardPage.xaml
        │   ├── TasksPage.xaml
        │   ├── WorkersPage.xaml
        │   ├── SmsMonitorPage.xaml
        │   ├── PaymentsPage.xaml
        │   └── SettingsPage.xaml
        ├── Converters/
        │   └── Converters.cs
        ├── Platforms/
        │   └── Android/
        │       ├── AndroidManifest.xml
        │       ├── MainActivity.cs
        │       ├── MainApplication.cs
        │       └── SmsListenerService.Android.cs
        ├── Resources/
        │   ├── Styles/
        │   │   ├── Colors.xaml
        │   │   └── Styles.xaml
        │   ├── AppIcon/
        │   ├── Splash/
        │   └── Fonts/
        ├── App.xaml
        ├── AppShell.xaml
        ├── MauiProgram.cs
        └── AIManager.Mobile.csproj
```

## การตั้งค่า

### เชื่อมต่อ AI Manager Core

1. เปิดแอพ ไปที่หน้า "ตั้งค่า"
2. กรอก Server Host/IP ของเครื่องที่รัน AI Manager Core
3. กรอก API Port (default: 5000) และ SignalR Port (default: 5002)
4. กด "ทดสอบการเชื่อมต่อ"
5. กด "บันทึก"

### อนุญาต SMS

แอพจะขอสิทธิ์อ่าน SMS เมื่อเปิดหน้า "ตรวจสอบ SMS"

### การอนุมัติอัตโนมัติ

1. ไปหน้า "การชำระเงิน"
2. เปิด "อนุมัติอัตโนมัติ"
3. ปรับ "ค่าความมั่นใจขั้นต่ำ" (default: 85%)

## Permissions

แอพต้องการ permissions เหล่านี้:

- `RECEIVE_SMS` - รับ SMS
- `READ_SMS` - อ่าน SMS
- `INTERNET` - เชื่อมต่อ API
- `POST_NOTIFICATIONS` - แจ้งเตือน (Android 13+)

## Theme

แอพใช้ theme เดียวกับ AI Manager Core UI:
- Primary: #7C4DFF (Purple)
- Secondary: #00BCD4 (Cyan)
- Background: #0D1117 (Dark)
- Cards: #1E1E2E

## Development Notes

### Build for Release

```bash
dotnet build -c Release -f net8.0-android
```

### Publish APK

```bash
dotnet publish -c Release -f net8.0-android
```

APK จะอยู่ที่: `bin/Release/net8.0-android/publish/`

## License

Part of PostXAgent project.
