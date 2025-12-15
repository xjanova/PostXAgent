using Newtonsoft.Json;

namespace AIManager.Core.WebAutomation.Models;

/// <summary>
/// Workflow ที่เรียนรู้มาสำหรับแต่ละ Platform
/// </summary>
public class LearnedWorkflow
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("platform")]
    public string Platform { get; set; } = "";

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("version")]
    public int Version { get; set; } = 1;

    [JsonProperty("steps")]
    public List<WorkflowStep> Steps { get; set; } = new();

    [JsonProperty("success_count")]
    public int SuccessCount { get; set; }

    [JsonProperty("failure_count")]
    public int FailureCount { get; set; }

    [JsonProperty("confidence_score")]
    public double ConfidenceScore { get; set; } = 0.5;

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("last_success_at")]
    public DateTime? LastSuccessAt { get; set; }

    [JsonProperty("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonProperty("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    public double GetSuccessRate()
    {
        var total = SuccessCount + FailureCount;
        return total > 0 ? (double)SuccessCount / total : 0.5;
    }
}

/// <summary>
/// แต่ละขั้นตอนใน Workflow
/// </summary>
public class WorkflowStep
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("order")]
    public int Order { get; set; }

    [JsonProperty("action")]
    public StepAction Action { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("element_selector")]
    public ElementSelector Selector { get; set; } = new();

    [JsonProperty("input_value")]
    public string? InputValue { get; set; }

    [JsonProperty("input_variable")]
    public string? InputVariable { get; set; } // e.g., "{{content.text}}", "{{content.image}}"

    [JsonProperty("wait_before_ms")]
    public int WaitBeforeMs { get; set; } = 500;

    [JsonProperty("wait_after_ms")]
    public int WaitAfterMs { get; set; } = 500;

    [JsonProperty("timeout_ms")]
    public int TimeoutMs { get; set; } = 10000;

    [JsonProperty("retry_count")]
    public int RetryCount { get; set; } = 3;

    [JsonProperty("is_optional")]
    public bool IsOptional { get; set; }

    [JsonProperty("success_condition")]
    public SuccessCondition? SuccessCondition { get; set; }

    [JsonProperty("alternative_selectors")]
    public List<ElementSelector> AlternativeSelectors { get; set; } = new();

    [JsonProperty("confidence_score")]
    public double ConfidenceScore { get; set; } = 1.0;

    [JsonProperty("learned_from")]
    public LearnedSource LearnedFrom { get; set; } = LearnedSource.Manual;
}

/// <summary>
/// ประเภท Action ที่ทำได้
/// </summary>
public enum StepAction
{
    Navigate,           // ไปยัง URL
    Click,              // คลิก element
    DoubleClick,        // ดับเบิลคลิก
    RightClick,         // คลิกขวา
    Type,               // พิมพ์ข้อความ
    Clear,              // ล้างข้อความ
    Select,             // เลือก dropdown
    Upload,             // อัพโหลดไฟล์
    DragDrop,           // ลากวาง
    Scroll,             // เลื่อนหน้า
    Hover,              // เลื่อนเมาส์ไปวาง
    Wait,               // รอ
    WaitForElement,     // รอให้ element ปรากฏ
    WaitForNavigation,  // รอ navigation
    Screenshot,         // ถ่ายภาพหน้าจอ
    ExtractText,        // ดึงข้อความ
    ExtractAttribute,   // ดึง attribute
    AssertVisible,      // ตรวจสอบว่า element เห็น
    AssertText,         // ตรวจสอบข้อความ
    ExecuteScript,      // รัน JavaScript
    PressKey,           // กดปุ่มคีย์บอร์ด
    Login,              // Login action (special)
}

/// <summary>
/// วิธีเลือก Element บนหน้าเว็บ
/// </summary>
public class ElementSelector
{
    [JsonProperty("type")]
    public SelectorType Type { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; } = "";

    [JsonProperty("text_content")]
    public string? TextContent { get; set; }

    [JsonProperty("attributes")]
    public Dictionary<string, string>? Attributes { get; set; }

    [JsonProperty("position")]
    public ElementPosition? Position { get; set; }

    [JsonProperty("parent_selector")]
    public ElementSelector? ParentSelector { get; set; }

    [JsonProperty("visual_hash")]
    public string? VisualHash { get; set; } // สำหรับ visual matching

    [JsonProperty("ai_description")]
    public string? AIDescription { get; set; } // คำอธิบายจาก AI

    [JsonProperty("confidence")]
    public double Confidence { get; set; } = 1.0;
}

public enum SelectorType
{
    CSS,            // CSS Selector
    XPath,          // XPath
    Id,             // By ID
    Name,           // By Name
    ClassName,      // By Class
    TagName,        // By Tag
    LinkText,       // By Link Text
    PartialLinkText,// By Partial Link Text
    Placeholder,    // By Placeholder text
    Label,          // By Label text
    AriaLabel,      // By ARIA label
    TestId,         // By data-testid
    Role,           // By ARIA role
    Text,           // By text content
    Visual,         // By visual matching (AI)
    Smart,          // AI-powered smart selector
}

/// <summary>
/// ตำแหน่ง Element บนหน้าจอ
/// </summary>
public class ElementPosition
{
    [JsonProperty("x")]
    public int X { get; set; }

    [JsonProperty("y")]
    public int Y { get; set; }

    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }

    [JsonProperty("relative_to_viewport")]
    public bool RelativeToViewport { get; set; } = true;
}

/// <summary>
/// เงื่อนไขสำเร็จ
/// </summary>
public class SuccessCondition
{
    [JsonProperty("type")]
    public ConditionType Type { get; set; }

    [JsonProperty("selector")]
    public ElementSelector? Selector { get; set; }

    [JsonProperty("expected_text")]
    public string? ExpectedText { get; set; }

    [JsonProperty("expected_url")]
    public string? ExpectedUrl { get; set; }

    [JsonProperty("expected_attribute")]
    public KeyValuePair<string, string>? ExpectedAttribute { get; set; }
}

public enum ConditionType
{
    ElementVisible,
    ElementNotVisible,
    TextContains,
    TextEquals,
    UrlContains,
    UrlEquals,
    AttributeEquals,
    ElementCount,
    Custom
}

public enum LearnedSource
{
    Manual,         // สอนโดยผู้ใช้
    AIObserved,     // AI สังเกตจากการใช้งาน
    AIGenerated,    // AI สร้างเอง
    Imported,       // นำเข้าจากภายนอก
    AutoRecovered,  // AI แก้ไขเองเมื่อ workflow พัง
}

/// <summary>
/// Session สำหรับการสอน AI
/// </summary>
public class TeachingSession
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("platform")]
    public string Platform { get; set; } = "";

    [JsonProperty("workflow_type")]
    public string WorkflowType { get; set; } = ""; // e.g., "create_post", "login", "upload_image"

    [JsonProperty("status")]
    public TeachingStatus Status { get; set; } = TeachingStatus.Started;

    [JsonProperty("current_step")]
    public int CurrentStep { get; set; }

    [JsonProperty("recorded_steps")]
    public List<RecordedStep> RecordedSteps { get; set; } = new();

    [JsonProperty("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonProperty("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonProperty("browser_session_id")]
    public string? BrowserSessionId { get; set; }
}

public enum TeachingStatus
{
    Started,
    Recording,
    Paused,
    Reviewing,
    Completed,
    Cancelled
}

/// <summary>
/// ขั้นตอนที่บันทึกระหว่างสอน
/// </summary>
public class RecordedStep
{
    [JsonProperty("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonProperty("action")]
    public string Action { get; set; } = "";

    [JsonProperty("element")]
    public RecordedElement? Element { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }

    [JsonProperty("screenshot")]
    public string? Screenshot { get; set; } // Base64

    [JsonProperty("page_url")]
    public string? PageUrl { get; set; }

    [JsonProperty("page_title")]
    public string? PageTitle { get; set; }

    [JsonProperty("user_instruction")]
    public string? UserInstruction { get; set; }
}

/// <summary>
/// Element ที่บันทึกได้
/// </summary>
public class RecordedElement
{
    [JsonProperty("tag_name")]
    public string TagName { get; set; } = "";

    [JsonProperty("id")]
    public string? Id { get; set; }

    [JsonProperty("class_name")]
    public string? ClassName { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("placeholder")]
    public string? Placeholder { get; set; }

    [JsonProperty("text_content")]
    public string? TextContent { get; set; }

    [JsonProperty("inner_html")]
    public string? InnerHtml { get; set; }

    [JsonProperty("xpath")]
    public string? XPath { get; set; }

    [JsonProperty("css_selector")]
    public string? CssSelector { get; set; }

    [JsonProperty("attributes")]
    public Dictionary<string, string> Attributes { get; set; } = new();

    [JsonProperty("position")]
    public ElementPosition? Position { get; set; }

    [JsonProperty("computed_styles")]
    public Dictionary<string, string>? ComputedStyles { get; set; }
}

/// <summary>
/// ข้อมูลสำหรับโพสผ่าน WebView
/// </summary>
public class WebPostContent
{
    [JsonProperty("text")]
    public string Text { get; set; } = "";

    [JsonProperty("images")]
    public List<string>? ImagePaths { get; set; }

    [JsonProperty("videos")]
    public List<string>? VideoPaths { get; set; }

    [JsonProperty("link")]
    public string? Link { get; set; }

    [JsonProperty("hashtags")]
    public List<string>? Hashtags { get; set; }

    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("schedule_time")]
    public DateTime? ScheduleTime { get; set; }

    [JsonProperty("visibility")]
    public string Visibility { get; set; } = "public"; // public, friends, private

    [JsonProperty("additional_options")]
    public Dictionary<string, object>? AdditionalOptions { get; set; }
}

/// <summary>
/// Credentials สำหรับ Login ผ่าน WebView
/// </summary>
public class WebCredentials
{
    [JsonProperty("username")]
    public string? Username { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    [JsonProperty("password")]
    public string Password { get; set; } = "";

    [JsonProperty("two_factor_secret")]
    public string? TwoFactorSecret { get; set; }

    [JsonProperty("cookies")]
    public List<BrowserCookie>? Cookies { get; set; }

    [JsonProperty("local_storage")]
    public Dictionary<string, string>? LocalStorage { get; set; }
}

public class BrowserCookie
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("value")]
    public string Value { get; set; } = "";

    [JsonProperty("domain")]
    public string Domain { get; set; } = "";

    [JsonProperty("path")]
    public string Path { get; set; } = "/";

    [JsonProperty("expires")]
    public DateTime? Expires { get; set; }

    [JsonProperty("http_only")]
    public bool HttpOnly { get; set; }

    [JsonProperty("secure")]
    public bool Secure { get; set; }

    [JsonProperty("same_site")]
    public string? SameSite { get; set; }
}
