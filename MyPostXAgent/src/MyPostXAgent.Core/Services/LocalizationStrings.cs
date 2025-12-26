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
        public static string Posts(bool isThai) => isThai ? "โพสต์" : "Posts";
        public static string Schedule(bool isThai) => isThai ? "ตั้งเวลา" : "Schedule";
        public static string Accounts(bool isThai) => isThai ? "บัญชี Social" : "Social Accounts";
        public static string AIContent(bool isThai) => isThai ? "สร้างเนื้อหา AI" : "AI Content";
        public static string VideoEditor(bool isThai) => isThai ? "วิดีโอ" : "Video Editor";
        public static string Groups(bool isThai) => isThai ? "ค้นหากลุ่ม" : "Find Groups";
        public static string Comments(bool isThai) => isThai ? "ความคิดเห็น" : "Comments";
        public static string Workflows(bool isThai) => isThai ? "เวิร์กโฟลว์" : "Workflows";
        public static string Settings(bool isThai) => isThai ? "ตั้งค่า" : "Settings";
    }

    // Window Controls
    public static class Window
    {
        public static string Minimize(bool isThai) => isThai ? "ย่อ" : "Minimize";
        public static string Maximize(bool isThai) => isThai ? "ขยาย" : "Maximize";
        public static string Close(bool isThai) => isThai ? "ปิด" : "Close";
        public static string SwitchLanguage(bool isThai) => isThai ? "สลับภาษา (Switch Language)" : "Switch Language (สลับภาษา)";
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

    // Dashboard Page
    public static class DashboardPage
    {
        public static string Overview(bool isThai) => isThai ? "ภาพรวมการทำงานของระบบ" : "System Overview";
        public static string CreateNewPost(bool isThai) => isThai ? "สร้างโพสต์ใหม่" : "Create New Post";
        public static string TotalAccounts(bool isThai) => isThai ? "บัญชีทั้งหมด" : "Total Accounts";
        public static string PostsToday(bool isThai) => isThai ? "โพสต์วันนี้" : "Posts Today";
        public static string Scheduled(bool isThai) => isThai ? "รอโพสต์" : "Scheduled";
        public static string SuccessRate(bool isThai) => isThai ? "อัตราสำเร็จ" : "Success Rate";
        public static string ThisWeek(bool isThai) => isThai ? "สัปดาห์นี้" : "This Week";
        public static string VsYesterday(bool isThai) => isThai ? "vs เมื่อวาน" : "vs Yesterday";
        public static string NextIn(bool isThai) => isThai ? "ถัดไป" : "Next In";
        public static string Excellent(bool isThai) => isThai ? "ดีเยี่ยม" : "Excellent";
        public static string QuickActions(bool isThai) => isThai ? "ดำเนินการด่วน" : "Quick Actions";
        public static string AIContentAction(bool isThai) => isThai ? "สร้างเนื้อหาอัตโนมัติ" : "Auto-generate content";
        public static string VideoEditorAction(bool isThai) => isThai ? "สร้างวิดีโอ AI" : "Create AI videos";
        public static string AddAccountAction(bool isThai) => isThai ? "เชื่อมต่อ Social" : "Connect Social";
        public static string ImportFlowAction(bool isThai) => isThai ? "นำเข้า .mpflow" : "Import .mpflow";
        public static string Platforms(bool isThai) => isThai ? "แพลตฟอร์ม" : "Platforms";
        public static string Accounts(bool isThai) => isThai ? "บัญชี" : "Accounts";
        public static string Posts(bool isThai) => isThai ? "โพสต์" : "Posts";
        public static string RecentActivity(bool isThai) => isThai ? "กิจกรรมล่าสุด" : "Recent Activity";
        public static string ViewAll(bool isThai) => isThai ? "ดูทั้งหมด" : "View All";
        public static string PostSuccess(bool isThai) => isThai ? "โพสต์สำเร็จ" : "Post Success";
        public static string AIContentReady(bool isThai) => isThai ? "AI สร้างเนื้อหาเสร็จ" : "AI Content Ready";
        public static string PostsReady(bool isThai) => isThai ? "โพสต์พร้อมใช้งาน" : "posts ready";
        public static string NewAccount(bool isThai) => isThai ? "เพิ่มบัญชีใหม่" : "New Account";
        public static string MinutesAgo(bool isThai) => isThai ? "นาทีที่แล้ว" : "minutes ago";
        public static string HourAgo(bool isThai) => isThai ? "ชั่วโมงที่แล้ว" : "hour ago";
        public static string UnlimitedUse(bool isThai) => isThai ? "ใช้งานได้ไม่จำกัด • ฟีเจอร์ครบทุกอย่าง" : "Unlimited usage • All features";
        public static string ManageLicense(bool isThai) => isThai ? "จัดการ License" : "Manage License";
    }

    // Settings Page
    public static class SettingsPage
    {
        public static string Subtitle(bool isThai) => isThai ? "ตั้งค่า API Keys และการเชื่อมต่อ" : "Configure API Keys and Connections";
        public static string Reset(bool isThai) => isThai ? "รีเซ็ต" : "Reset";
        public static string AIProviderKeysDesc(bool isThai) => isThai ? "API Keys สำหรับสร้างเนื้อหา AI" : "API Keys for AI Content Generation";
        public static string OllamaLocalDesc(bool isThai) => isThai ? "Ollama Base URL (Local)" : "Ollama Base URL (Local)";
        public static string RefreshModels(bool isThai) => isThai ? "รีเฟรชรายการ models" : "Refresh model list";
        public static string InstallHint(bool isThai) => isThai ? "💡 คำแนะนำ: ติดตั้ง model ด้วย" : "💡 Tip: Install model with";
        public static string SelectOrType(bool isThai) => isThai ? "เลือก model หรือพิมพ์เอง" : "Select model or type custom";
        public static string ImageGenDesc(bool isThai) => isThai ? "API Keys สำหรับสร้างรูปภาพ AI" : "API Keys for AI Image Generation";
        public static string VideoGenDesc(bool isThai) => isThai ? "API Keys สำหรับสร้างวิดีโอ AI" : "API Keys for AI Video Generation";
        public static string MusicGenDesc(bool isThai) => isThai ? "API Keys สำหรับสร้างเพลง AI" : "API Keys for AI Music Generation";
        public static string SocialMediaDesc(bool isThai) => isThai ? "API Keys สำหรับเชื่อมต่อ Social Media" : "API Keys for Social Media Integration";
        public static string InfoTitle(bool isThai) => isThai ? "คำแนะนำ" : "Information";
        public static string InfoLine1(bool isThai) => isThai ? "• API Keys จะถูกเก็บอย่างปลอดภัยในเครื่องของคุณเท่านั้น" : "• API Keys are stored securely on your machine only";
        public static string InfoLine2(bool isThai) => isThai ? "• หากใช้ Ollama หรือ Stable Diffusion แบบ Local ไม่จำเป็นต้องใส่ API Key" : "• If using Ollama or Stable Diffusion locally, no API key needed";
        public static string InfoLine3(bool isThai) => isThai ? "• สามารถใช้งานได้ทันทีหลังบันทึก ไม่ต้อง Restart แอพ" : "• Changes take effect immediately, no restart required";
    }

    // Content Generator Page
    public static class ContentGenPage
    {
        public static string Subtitle(bool isThai) => isThai ? "สร้างเนื้อหาโพสต์ด้วย AI อัจฉริยะ" : "Create post content with smart AI";
        public static string Clear(bool isThai) => isThai ? "ล้าง" : "Clear";
        public static string AIProviderDesc(bool isThai) => isThai ? "เลือก AI ที่ใช้สร้างเนื้อหา" : "Select AI for content generation";
        public static string OllamaFree(bool isThai) => isThai ? "Ollama (ฟรี)" : "Ollama (Free)";
        public static string ContentDetails(bool isThai) => isThai ? "รายละเอียดเนื้อหา" : "Content Details";
        public static string ContentDetailsDesc(bool isThai) => isThai ? "กำหนดหัวข้อและรูปแบบ" : "Define topic and style";
        public static string ContentType(bool isThai) => isThai ? "ประเภทเนื้อหา" : "Content Type";
        public static string Tone(bool isThai) => isThai ? "โทนเสียง" : "Tone";
        public static string TopicDescription(bool isThai) => isThai ? "หัวข้อ/คำอธิบาย" : "Topic/Description";
        public static string TopicPlaceholder(bool isThai) => isThai ? "เช่น: โปรโมทร้านกาแฟใหม่ย่านสุขุมวิท มีโปรเปิดร้านลด 50%" : "e.g.: Promote new coffee shop in Sukhumvit area with 50% grand opening discount";
        public static string KeywordsLabel(bool isThai) => isThai ? "Keywords (คั่นด้วย ,)" : "Keywords (comma-separated)";
        public static string KeywordsPlaceholder(bool isThai) => isThai ? "กาแฟ, ร้านใหม่, โปรโมชั่น" : "coffee, new shop, promotion";
        public static string HashtagsLabel(bool isThai) => isThai ? "Hashtags (คั่นด้วย ,)" : "Hashtags (comma-separated)";
        public static string PlatformsDesc(bool isThai) => isThai ? "เลือกแพลตฟอร์มที่จะโพสต์" : "Select platforms to post";
        public static string AdvancedOptions(bool isThai) => isThai ? "ตัวเลือกเพิ่มเติม" : "Advanced Options";
        public static string AdvancedOptionsDesc(bool isThai) => isThai ? "ปรับแต่งผลลัพธ์" : "Customize results";
        public static string ContentLength(bool isThai) => isThai ? "ความยาวเนื้อหา" : "Content Length";
        public static string Language(bool isThai) => isThai ? "ภาษา" : "Language";
        public static string IncludeEmojis(bool isThai) => isThai ? "ใส่ Emojis" : "Include Emojis";
        public static string IncludeCTA(bool isThai) => isThai ? "ใส่ Call-to-Action" : "Include Call-to-Action";
        public static string ContentPreview(bool isThai) => isThai ? "ตัวอย่างเนื้อหา" : "Content Preview";
        public static string Copy(bool isThai) => isThai ? "คัดลอก" : "Copy";
        public static string Regenerate(bool isThai) => isThai ? "สร้างใหม่" : "Regenerate";
        public static string Generating(bool isThai) => isThai ? "กำลังสร้างเนื้อหา..." : "Generating content...";
        public static string NoContent(bool isThai) => isThai ? "ยังไม่มีเนื้อหา" : "No content yet";
        public static string NoContentHint(bool isThai) => isThai ? "กรอกข้อมูลและกดปุ่ม 'สร้างเนื้อหา'" : "Fill in details and click 'Generate Content'";
        public static string Characters(bool isThai) => isThai ? "ตัวอักษร" : "Characters";
        public static string Words(bool isThai) => isThai ? "คำ" : "Words";
        public static string RecommendedHashtags(bool isThai) => isThai ? "Hashtags ที่แนะนำ" : "Recommended Hashtags";
        public static string SaveDraft(bool isThai) => isThai ? "บันทึก Draft" : "Save Draft";
        public static string CreatePost(bool isThai) => isThai ? "สร้างโพสต์" : "Create Post";
    }
}
