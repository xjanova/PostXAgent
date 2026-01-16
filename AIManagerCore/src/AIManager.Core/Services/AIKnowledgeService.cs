namespace AIManager.Core.Services;

/// <summary>
/// Service สำหรับจัดการ System Prompt ของ AI Chat
/// </summary>
public class AIKnowledgeService
{
    private readonly CoreDatabaseService? _dbService;

    public AIKnowledgeService(CoreDatabaseService? dbService = null)
    {
        _dbService = dbService;
    }

    /// <summary>
    /// สร้าง System Prompt แบบสั้นกระชับ
    /// </summary>
    public Task<string> GetSystemPromptAsync(bool forceReload = false)
    {
        var prompt = @"คุณคือ AI Assistant ของระบบ AIManager/PostXAgent - ระบบจัดการโซเชียลมีเดียอัตโนมัติ

## ⚠️ กฎสำคัญ
ตอบได้เฉพาะเรื่องที่เกี่ยวกับระบบ AIManager/PostXAgent เท่านั้น!
- ถามเรื่องอื่น → ปฏิเสธสุภาพ: ""ขออภัยครับ ผมตอบได้เฉพาะเรื่องระบบ AIManager เท่านั้นครับ""

## ระบบรองรับ
- สร้างภาพ: ComfyUI, Diffusers, Multi-GPU
- สร้างเนื้อหา: Ollama, Claude, GPT-4
- โพสต์: Facebook, Instagram, TikTok, Twitter, LINE, YouTube, Threads, LinkedIn, Pinterest
- Web Automation: Playwright

## สิ่งที่ช่วยได้
- อธิบายฟีเจอร์และวิธีใช้งาน
- สร้างแคปชั่น/เนื้อหาโซเชียลมีเดีย
- แก้ปัญหาการใช้งาน

ตอบภาษาไทย กระชับ ตรงประเด็น";

        return Task.FromResult(prompt);
    }

    public void InvalidateCache() { }
}
