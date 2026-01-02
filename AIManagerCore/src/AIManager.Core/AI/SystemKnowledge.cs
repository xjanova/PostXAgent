namespace AIManager.Core.AI;

/// <summary>
/// System Knowledge - ข้อมูลความรู้เกี่ยวกับระบบ PostXAgent ทั้งหมด
/// ใช้สำหรับให้ AI เข้าใจบริบทและสามารถตอบคำถามเกี่ยวกับระบบได้
/// </summary>
public static class SystemKnowledge
{
    /// <summary>
    /// ชื่อระบบ
    /// </summary>
    public const string SystemName = "PostXAgent";

    /// <summary>
    /// เวอร์ชันปัจจุบัน
    /// </summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// คำอธิบายระบบโดยย่อ
    /// </summary>
    public const string Description = "AI-Powered Brand Promotion Manager - ระบบจัดการโปรโมทแบรนด์อัตโนมัติด้วย AI";

    /// <summary>
    /// รายละเอียดระบบแบบเต็ม
    /// </summary>
    public static string GetFullSystemDescription() => @"
=== PostXAgent System Overview ===

PostXAgent คือ ระบบ AI-Powered Brand Promotion Manager สำหรับจัดการการตลาดบน Social Media แบบอัตโนมัติ
พัฒนาโดยทีม XmanStudio สำหรับตลาดประเทศไทย

--- สถาปัตยกรรมระบบ ---

┌─────────────────────────────────────────────────────────────┐
│                     Laravel Backend                          │
│                    (Web Control Panel)                       │
│                     Port: 8000                               │
└─────────────────────┬───────────────────────────────────────┘
                      │ HTTP/SignalR
                      ▼
┌─────────────────────────────────────────────────────────────┐
│              C# AI Manager Core (Windows Server)             │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │  REST API   │  │  WebSocket  │  │  SignalR    │         │
│  │  Port 5000  │  │  Port 5001  │  │  Port 5002  │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                          │                                   │
│              ┌───────────┴───────────┐                      │
│              │  Process Orchestrator │                      │
│              │    (40+ CPU Cores)    │                      │
│              └───────────┬───────────┘                      │
│                          │                                   │
│    ┌─────────┬─────────┬─┴─────────┬─────────┬─────────┐   │
│    │ FB      │ IG      │ TikTok   │ Twitter │ LINE    │   │
│    │ Worker  │ Worker  │ Worker   │ Worker  │ Worker  │   │
│    └─────────┴─────────┴──────────┴─────────┴─────────┘   │
└─────────────────────────────────────────────────────────────┘

--- เทคโนโลยีที่ใช้ ---

• AI Manager Core: C# / .NET 8.0
• AI Manager UI: WPF + Material Design (แอพเดสก์ท็อป Windows)
• Web Backend: Laravel (PHP 11.x)
• Frontend: Vue.js 3.x
• Database: MySQL/PostgreSQL 8.0+
• Cache: Redis 7.x
• Real-time: SignalR
• Web Automation: WebView2 + Selenium/Playwright

--- Social Media Platforms ที่รองรับ (9 แพลตฟอร์ม) ---

1. Facebook - Graph API, โพสต์, โฆษณา, Groups
2. Instagram - Graph API (via Facebook), Reels, Stories
3. TikTok - TikTok API, วิดีโอ, Live
4. Twitter/X - Twitter API v2, Tweets, Threads
5. LINE - Messaging API, Official Account
6. YouTube - Data API v3, วิดีโอ, Shorts
7. Threads - Threads API
8. LinkedIn - Marketing API, โพสต์, Company Pages
9. Pinterest - API v5, Pins, Boards

--- AI Providers ที่รองรับ ---

Content Generation:
1. Ollama (Free, Local) - Default สำหรับ development
2. Google Gemini (Free tier)
3. OpenAI GPT-4 (Paid)
4. Anthropic Claude (Paid)
5. HuggingFace (Free)

Image Generation:
1. Stable Diffusion (Free, Self-hosted)
2. Leonardo.ai (Free tier)
3. DALL-E 3 (Paid)

Video Generation:
1. Freepik Pikaso AI
2. Runway ML
3. Pika Labs
4. Luma AI

Music Generation:
1. Suno AI
2. Stable Audio
3. AudioCraft (Meta)
4. MusicGen
";

    /// <summary>
    /// ฟีเจอร์หลักของระบบ
    /// </summary>
    public static string GetMainFeatures() => @"
=== ฟีเจอร์หลักของ PostXAgent ===

1. 🤖 AI Content Generation
   - สร้างเนื้อหาโพสต์อัตโนมัติด้วย AI
   - รองรับหลาย AI providers (Ollama, Gemini, GPT-4, Claude)
   - ปรับแต่ง tone, style ตาม brand
   - รองรับภาษาไทยและอังกฤษ

2. 🖼️ AI Image Generation
   - สร้างรูปภาพจาก prompt
   - รองรับ Stable Diffusion, Leonardo.ai, DALL-E 3
   - ปรับขนาดตาม platform

3. 🎬 AI Video & Music Generation
   - สร้างวิดีโอสั้นสำหรับ Reels, TikTok, Shorts
   - สร้างเพลงประกอบด้วย AI

4. 📅 Smart Scheduling
   - ตั้งเวลาโพสต์ล่วงหน้า
   - วิเคราะห์เวลาที่เหมาะสมที่สุด
   - จัดการ content calendar

5. 👥 Account Pool Management
   - จัดการหลาย accounts ต่อ platform
   - หมุนเวียน account อัตโนมัติ
   - ป้องกัน rate limiting

6. 🌐 Web Automation & Learning
   - สอนระบบทำงานบนเว็บไซต์ใหม่ๆ
   - บันทึก workflow อัตโนมัติ
   - AI ช่วยวิเคราะห์ elements บนหน้าเว็บ

7. 📊 Analytics & Reporting
   - Dashboard แสดงสถิติ
   - วิเคราะห์ engagement
   - รายงาน performance

8. 🔒 Security & Credentials
   - จัดการ API keys ปลอดภัย
   - เข้ารหัส credentials
   - Backup & restore

9. 🎯 Campaign Management
   - สร้างและจัดการ campaigns
   - กำหนด target audience
   - ติดตาม ROI

10. 🔄 Multi-Platform Posting
    - โพสต์ครั้งเดียวไปหลาย platforms
    - ปรับ content ตาม platform
    - ซิงค์สถานะ
";

    /// <summary>
    /// คำอธิบายหน้า Web Learning
    /// </summary>
    public static string GetWebLearningDescription() => @"
=== Web Learning Page ===

หน้า Web Learning เป็นเครื่องมือสำหรับ ""สอน"" ระบบให้รู้วิธีทำงานบนเว็บไซต์ต่างๆ

ฟีเจอร์หลัก:
• Web Browser ในตัว (WebView2)
• บันทึกการคลิก, พิมพ์, scroll
• AI วิเคราะห์ elements บนหน้าเว็บ
• สร้าง workflow อัตโนมัติ
• ทดสอบและ replay workflow

วิธีใช้งาน:
1. เลือก Platform (Facebook, Instagram, etc.)
2. เลือก Task Type (Post, Reply, Like, etc.)
3. เปิดโหมด Auto Learning
4. ทำงานบน browser ตามปกติ
5. ระบบจะบันทึกและเรียนรู้

AI Command:
• พิมพ์คำสั่งให้ AI ช่วย เช่น ""สอนวิธีโพสต์รูปบน Facebook""
• AI จะแนะนำขั้นตอนและ selectors
• สามารถถาม AI เกี่ยวกับ elements บนหน้าเว็บได้
";

    /// <summary>
    /// คำแนะนำสำหรับแต่ละ Platform
    /// </summary>
    public static string GetPlatformGuide(string platform) => platform.ToLower() switch
    {
        "facebook" => @"
=== Facebook Platform Guide ===

URLs หลัก:
• Login: https://www.facebook.com/login
• Feed: https://www.facebook.com/
• Create Post: https://www.facebook.com/ (ช่อง 'What's on your mind?')
• Groups: https://www.facebook.com/groups/
• Pages: https://www.facebook.com/pages/

Selectors สำคัญ:
• Login Email: input[name='email']
• Login Password: input[name='pass']
• Login Button: button[name='login']
• Create Post Box: div[role='textbox'], div[data-testid='post-composer']
• Photo Button: input[type='file'], div[aria-label='Photo/video']
• Post Button: div[aria-label='Post']

ข้อควรระวัง:
• Rate limit: ไม่เกิน 25 โพสต์/วัน
• รอ 2-5 วินาทีระหว่าง actions
• ตรวจสอบ captcha
• Facebook อาจเปลี่ยน UI บ่อย
",

        "instagram" => @"
=== Instagram Platform Guide ===

URLs หลัก:
• Login: https://www.instagram.com/accounts/login/
• Feed: https://www.instagram.com/
• Create: คลิก + ที่ navigation
• Profile: https://www.instagram.com/[username]/

Selectors สำคัญ:
• Login Username: input[name='username']
• Login Password: input[name='password']
• Login Button: button[type='submit']
• Create Button: svg[aria-label='New post']
• File Input: input[type='file']
• Caption Box: textarea[aria-label='Write a caption...']

ข้อควรระวัง:
• Rate limit: ไม่เกิน 10 posts/วัน
• Stories: ไม่เกิน 100/วัน
• รอ 30-60 วินาทีระหว่าง posts
• ระวัง shadowban
",

        "tiktok" => @"
=== TikTok Platform Guide ===

URLs หลัก:
• Login: https://www.tiktok.com/login
• Upload: https://www.tiktok.com/upload
• Creator Center: https://www.tiktok.com/creator-center

Selectors สำคัญ:
• Login Phone/Email: input[name='username']
• Password: input[type='password']
• Upload Button: input[type='file']
• Caption: div[contenteditable='true']
• Post Button: button[class*='upload-btn']

ข้อควรระวัง:
• ต้องใช้วิดีโอเท่านั้น (ไม่รองรับภาพนิ่ง)
• วิดีโอ 9:16 (แนวตั้ง)
• ความยาว 15 วินาที - 10 นาที
• ใช้ trending sounds เพิ่ม reach
",

        "twitter" or "x" => @"
=== Twitter/X Platform Guide ===

URLs หลัก:
• Login: https://twitter.com/login
• Home: https://twitter.com/home
• Compose: https://twitter.com/compose/tweet

Selectors สำคัญ:
• Login Username: input[autocomplete='username']
• Password: input[name='password']
• Tweet Box: div[data-testid='tweetTextarea_0']
• Tweet Button: div[data-testid='tweetButtonInline']
• Media Input: input[data-testid='fileInput']

ข้อควรระวัง:
• Character limit: 280 ตัวอักษร (Premium: 25,000)
• Rate limit: ดู API limits
• รอ 15-30 วินาทีระหว่าง tweets
• ระวัง spam detection
",

        "line" => @"
=== LINE Platform Guide ===

URLs หลัก:
• Official Account Manager: https://manager.line.biz/
• LINE for Business: https://lineforbusiness.com/

การใช้งาน:
• ส่วนใหญ่ใช้ผ่าน Messaging API
• ต้องมี Official Account
• ใช้ LINE Developers Console สำหรับ API

ข้อควรระวัง:
• ต้องมี Channel Access Token
• Message limit ตาม plan
• Rich menu ต้อง setup แยก
",

        "youtube" => @"
=== YouTube Platform Guide ===

URLs หลัก:
• Studio: https://studio.youtube.com/
• Upload: https://studio.youtube.com/channel/[id]/videos/upload

Selectors สำคัญ:
• Upload Button: input[type='file']
• Title: input[id='textbox']
• Description: div[id='description-container']
• Tags: input[aria-label='Tags']

ข้อควรระวัง:
• Video upload limit: 15 นาที (unverified), 12 ชม (verified)
• Daily upload limit: ~100 videos
• Shorts: แนวตั้ง, < 60 วินาที
",

        "linkedin" => @"
=== LinkedIn Platform Guide ===

URLs หลัก:
• Login: https://www.linkedin.com/login
• Feed: https://www.linkedin.com/feed/
• Post: คลิก 'Start a post'

Selectors สำคัญ:
• Email: input[id='username']
• Password: input[id='password']
• Post Box: div[role='textbox']
• Photo: button[aria-label='Add a photo']

ข้อควรระวัง:
• Professional tone เท่านั้น
• Rate limit: ~100 connections/week
• รอ 1-2 ชม ระหว่าง posts
",

        "threads" => @"
=== Threads Platform Guide ===

URLs หลัก:
• Login: https://www.threads.net/login
• Feed: https://www.threads.net/

ข้อมูล:
• ใช้ Instagram account login
• รองรับ text, images, videos
• Character limit: 500

ข้อควรระวัง:
• ใหม่มาก - UI อาจเปลี่ยนบ่อย
• Rate limits ไม่ชัดเจน
• Federated via ActivityPub
",

        "pinterest" => @"
=== Pinterest Platform Guide ===

URLs หลัก:
• Login: https://www.pinterest.com/login/
• Create Pin: https://www.pinterest.com/pin-creation-tool/

Selectors สำคัญ:
• Email: input[id='email']
• Password: input[id='password']
• Upload: input[type='file']
• Title: input[id='pin-draft-title']
• Description: div[data-test-id='textfield']

ข้อควรระวัง:
• Image สำคัญมาก (2:3 ratio)
• SEO-friendly descriptions
• Board organization
",

        _ => @"
=== Custom Platform ===

สำหรับ platform ที่ไม่อยู่ในรายการ:
1. เปิดหน้าเว็บใน browser
2. ใช้ Developer Tools (F12) ดู elements
3. ใช้ AI Command ถามว่า 'ช่วยหา selector สำหรับ...'
4. บันทึก workflow ด้วย Auto Learning

Tips:
• ใช้ CSS Selectors ที่มี id หรือ data-testid
• หลีกเลี่ยง class names ที่เป็น hash
• ตรวจสอบ XPath เป็นทางเลือก
"
    };

    /// <summary>
    /// สร้าง System Prompt สำหรับ AI
    /// </summary>
    public static string GetAISystemPrompt(string platform, string taskType, string pageContext)
    {
        return $@"คุณคือ AI Assistant ของระบบ {SystemName} เวอร์ชัน {Version}

{GetFullSystemDescription()}

{GetMainFeatures()}

{GetWebLearningDescription()}

=== Platform ที่กำลังใช้งาน ===
{GetPlatformGuide(platform)}

=== Context ปัจจุบัน ===
Platform: {platform}
Task Type: {taskType}
{pageContext}

=== คำแนะนำสำหรับคุณ ===
1. ตอบเป็นภาษาไทยเสมอ ยกเว้นคำศัพท์เทคนิค
2. ให้คำแนะนำที่เฉพาะเจาะจงกับ platform ที่ใช้
3. ระบุ CSS/XPath selectors ที่แม่นยำ
4. เตือนเรื่อง rate limits และข้อควรระวัง
5. ถ้าไม่แน่ใจ ให้แนะนำให้ผู้ใช้ตรวจสอบด้วย Developer Tools (F12)
6. ตอบกระชับ ตรงประเด็น แต่ครบถ้วน

=== คำถาม/คำสั่งของผู้ใช้ ===
";
    }

    /// <summary>
    /// คำแนะนำการ troubleshoot Ollama
    /// </summary>
    public static string GetOllamaTroubleshootingGuide() => @"
=== Ollama Troubleshooting Guide ===

ถ้า Ollama แสดง 'Disconnected' หรือ 'Offline':

1. ตรวจสอบว่า Ollama กำลังทำงาน:
   • เปิด Terminal/PowerShell
   • รัน: ollama serve
   • หรือตรวจสอบ System Tray

2. ตรวจสอบ model:
   • รัน: ollama list
   • ถ้าไม่มี llama3.2 รัน: ollama pull llama3.2

3. ทดสอบ API:
   • เปิด browser ไปที่: http://localhost:11434/api/tags
   • ควรเห็น JSON response

4. ถ้ายังไม่ได้:
   • Restart Ollama service
   • ตรวจสอบ Windows Firewall
   • ลอง restart เครื่อง

5. ใช้ AI Provider อื่น:
   • ไปหน้า AI Providers
   • เลือก Google Gemini หรือ provider อื่น
";
}
