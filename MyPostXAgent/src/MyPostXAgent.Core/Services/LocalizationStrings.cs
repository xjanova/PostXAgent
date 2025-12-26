namespace MyPostXAgent.Core.Services;

/// <summary>
/// Localization strings for Thai/English
/// </summary>
public static class LocalizationStrings
{
    // Common
    public static class Common
    {
        public static string Save(bool isThai) => isThai ? "บันทึก" : "Save";
        public static string Cancel(bool isThai) => isThai ? "ยกเลิก" : "Cancel";
        public static string Reset(bool isThai) => isThai ? "รีเซ็ต" : "Reset";
        public static string Search(bool isThai) => isThai ? "ค้นหา..." : "Search...";
        public static string Success(bool isThai) => isThai ? "สำเร็จ" : "Success";
        public static string Error(bool isThai) => isThai ? "ข้อผิดพลาด" : "Error";
        public static string Loading(bool isThai) => isThai ? "กำลังโหลด..." : "Loading...";
        public static string Checking(bool isThai) => isThai ? "กำลังตรวจสอบ..." : "Checking...";
    }

    // AI Provider Status
    public static class AIStatus
    {
        public static string Ready(bool isThai) => isThai ? "พร้อม" : "Ready";
        public static string NotReady(bool isThai) => isThai ? "ไม่พร้อม" : "Not Ready";
        public static string NotConfigured(bool isThai) => isThai ? "ไม่ได้ตั้งค่า" : "Not Configured";
        public static string Timeout(bool isThai) => isThai ? "timeout" : "timeout";
        public static string NotRunning(bool isThai) => isThai ? "ไม่ทำงาน" : "Not Running";
        public static string NoProvider(bool isThai) => isThai ? "ไม่มี AI Provider" : "No AI Provider";
        public static string CannotCheck(bool isThai) => isThai ? "ตรวจสอบไม่ได้" : "Cannot Check";
        public static string ModelNotFound(bool isThai) => isThai ? "ไม่พบ model" : "Model Not Found";
        public static string ModelsAvailable(bool isThai, int count) =>
            isThai ? $"พร้อมใช้งาน ({count} models)" : $"Available ({count} models)";
    }

    // Settings Page
    public static class Settings
    {
        public static string Title(bool isThai) => isThai ? "ตั้งค่า API Keys และการเชื่อมต่อ" : "Configure API Keys and Connections";
        public static string AIContentGeneration(bool isThai) => isThai ? "AI Content Generation" : "AI Content Generation";
        public static string AIProviderKeys(bool isThai) => isThai ? "API Keys สำหรับสร้างเนื้อหา AI" : "API Keys for AI Content Generation";
        public static string OllamaModel(bool isThai) => isThai ? "Ollama Model" : "Ollama Model";
        public static string SaveSuccess(bool isThai) =>
            isThai ? "บันทึกการตั้งค่าสำเร็จ!\n\nAI Providers ได้รับการอัพเดทแล้ว"
                   : "Settings saved successfully!\n\nAI Providers have been updated";
        public static string ResetConfirm(bool isThai) =>
            isThai ? "ต้องการรีเซ็ตค่าทั้งหมดเป็นค่าเริ่มต้นหรือไม่?"
                   : "Do you want to reset all settings to default?";
        public static string ConfirmReset(bool isThai) => isThai ? "ยืนยันการรีเซ็ต" : "Confirm Reset";
        public static string RefreshModels(bool isThai) => isThai ? "รีเฟรชรายการ models" : "Refresh model list";
        public static string InstallHint(bool isThai) => isThai ? "💡 คำแนะนำ: ติดตั้ง model ด้วย" : "💡 Tip: Install model with";
        public static string SelectOrType(bool isThai) => isThai ? "เลือก model หรือพิมพ์เอง" : "Select model or type custom";
    }

    // Dashboard
    public static class Dashboard
    {
        public static string Title(bool isThai) => isThai ? "แดชบอร์ด" : "Dashboard";
        public static string PostsToday(bool isThai) => isThai ? "โพสต์วันนี้" : "Posts Today";
        public static string QueueCount(bool isThai) => isThai ? "คิวรอโพสต์" : "Queue";
        public static string TotalAccounts(bool isThai) => isThai ? "บัญชีทั้งหมด" : "Total Accounts";
        public static string AIStatus(bool isThai) => isThai ? "สถานะ AI" : "AI Status";
    }

    // Navigation
    public static class Nav
    {
        public static string Dashboard(bool isThai) => isThai ? "แดชบอร์ด" : "Dashboard";
        public static string ContentGenerator(bool isThai) => isThai ? "สร้างเนื้อหา AI" : "AI Content Generator";
        public static string Scheduler(bool isThai) => isThai ? "ตั้งเวลาโพสต์" : "Scheduler";
        public static string Accounts(bool isThai) => isThai ? "บัญชี Social" : "Social Accounts";
        public static string Posts(bool isThai) => isThai ? "โพสต์ทั้งหมด" : "All Posts";
        public static string Settings(bool isThai) => isThai ? "ตั้งค่า" : "Settings";
    }

    // Content Generator
    public static class ContentGen
    {
        public static string Title(bool isThai) => isThai ? "สร้างเนื้อหา AI" : "AI Content Generator";
        public static string Topic(bool isThai) => isThai ? "หัวข้อ/เนื้อหา" : "Topic/Content";
        public static string Generate(bool isThai) => isThai ? "สร้างเนื้อหา" : "Generate Content";
        public static string Generating(bool isThai) => isThai ? "กำลังสร้าง..." : "Generating...";
        public static string GeneratedContent(bool isThai) => isThai ? "เนื้อหาที่สร้าง" : "Generated Content";
        public static string Hashtags(bool isThai) => isThai ? "แฮชแท็ก" : "Hashtags";
        public static string SelectProvider(bool isThai) => isThai ? "เลือก AI Provider" : "Select AI Provider";
        public static string GenerateSuccess(bool isThai, string provider) =>
            isThai ? $"สร้างเนื้อหาสำเร็จด้วย {provider}!" : $"Content generated successfully with {provider}!";
        public static string GenerateFailed(bool isThai) =>
            isThai ? "ไม่สามารถสร้างเนื้อหาได้" : "Failed to generate content";
    }

    // Demo Mode
    public static class Demo
    {
        public static string DemoMode(bool isThai, int daysRemaining) =>
            isThai ? (daysRemaining > 0
                ? $"Demo Mode - เหลือ {daysRemaining} วัน"
                : "Demo Mode - เหลือไม่ถึง 1 วัน")
            : (daysRemaining > 0
                ? $"Demo Mode - {daysRemaining} days left"
                : "Demo Mode - Less than 1 day");
    }
}
