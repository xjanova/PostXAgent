namespace MyPostXAgent.Core.Models;

/// <summary>
/// Template สำเร็จรูปสำหรับ Content Generation
/// </summary>
public class PromptTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string Length { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public bool IncludeEmojis { get; set; }
    public bool IncludeCTA { get; set; }
    public string Keywords { get; set; } = string.Empty;
    public List<string> SuggestedPlatforms { get; set; } = new();
}

/// <summary>
/// Template categories
/// </summary>
public static class PromptTemplateCategories
{
    public const string Restaurant = "ร้านอาหาร";
    public const string Cafe = "คาเฟ่";
    public const string Fashion = "แฟชั่น";
    public const string Beauty = "ความงาม";
    public const string Fitness = "ฟิตเนส";
    public const string Education = "การศึกษา";
    public const string Technology = "เทคโนโลยี";
    public const string Travel = "ท่องเที่ยว";
    public const string RealEstate = "อสังหาริมทรัพย์";
    public const string Event = "อีเวนท์";
}

/// <summary>
/// Built-in prompt templates
/// </summary>
public static class BuiltInTemplates
{
    public static List<PromptTemplate> GetAll()
    {
        return new List<PromptTemplate>
        {
            // ร้านอาหาร
            new()
            {
                Name = "เมนูใหม่ร้านอาหาร",
                Description = "ประชาสัมพันธ์เมนูใหม่",
                Category = PromptTemplateCategories.Restaurant,
                Topic = "เมนูใหม่ของร้าน [ชื่อเมนู] รสชาติเด็ด ราคาไม่แพง",
                ContentType = "โพสต์โปรโมท",
                Tone = "เป็นมิตร/Friendly",
                Length = "ปานกลาง (3-5 ประโยค)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "อาหาร, เมนูใหม่, อร่อย, ราคาถูก",
                SuggestedPlatforms = new() { "Facebook", "Instagram", "LINE" }
            },
            new()
            {
                Name = "โปรโมชั่นร้านอาหาร",
                Description = "โปรโมชั่นพิเศษ",
                Category = PromptTemplateCategories.Restaurant,
                Topic = "โปรโมชั่นพิเศษ ลด 50% ทุกวันจันทร์-ศุกร์",
                ContentType = "โพสต์โปรโมท",
                Tone = "แบบเด็ก Gen Z",
                Length = "สั้น (1-2 ประโยค)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "โปรโมชั่น, ลดราคา, ประหยัด",
                SuggestedPlatforms = new() { "Facebook", "Instagram", "TikTok" }
            },

            // คาเฟ่
            new()
            {
                Name = "เครื่องดื่มใหม่คาเฟ่",
                Description = "แนะนำเครื่องดื่มใหม่",
                Category = PromptTemplateCategories.Cafe,
                Topic = "เครื่องดื่มใหม่ [ชื่อเครื่องดื่ม] หอมหวาน ถูกปาก",
                ContentType = "รีวิวสินค้า",
                Tone = "เป็นมิตร/Friendly",
                Length = "ปานกลาง (3-5 ประโยค)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "กาแฟ, คาเฟ่, เครื่องดื่ม, หวาน",
                SuggestedPlatforms = new() { "Instagram", "Facebook" }
            },
            new()
            {
                Name = "บรรยากาศคาเฟ่",
                Description = "แชร์บรรยากาศร้าน",
                Category = PromptTemplateCategories.Cafe,
                Topic = "บรรยากาศชิลล์ๆ ในร้าน เหมาะสำหรับนั่งทำงาน อ่านหนังสือ",
                ContentType = "เล่าเรื่อง/Storytelling",
                Tone = "สร้างแรงบันดาลใจ",
                Length = "ยาว (1 ย่อหน้า)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = false,
                Keywords = "คาเฟ่, บรรยากาศ, ชิล, ทำงาน",
                SuggestedPlatforms = new() { "Instagram", "Facebook" }
            },

            // แฟชั่น
            new()
            {
                Name = "สินค้าใหม่แฟชั่น",
                Description = "ประชาสัมพันธ์สินค้าใหม่",
                Category = PromptTemplateCategories.Fashion,
                Topic = "คอลเลคชั่นใหม่ล่าสุด [ชื่อคอลเลคชั่น] ทันสมัย สไตล์เกาหลี",
                ContentType = "โพสต์โปรโมท",
                Tone = "แบบเด็ก Gen Z",
                Length = "ปานกลาง (3-5 ประโยค)",
                Language = "ไทย + English (ผสม)",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "แฟชั่น, เสื้อผ้า, ทันสมัย, สไตล์",
                SuggestedPlatforms = new() { "Instagram", "TikTok", "Facebook" }
            },

            // ความงาม
            new()
            {
                Name = "รีวิวสินค้าความงาม",
                Description = "รีวิวผลิตภัณฑ์ความงาม",
                Category = PromptTemplateCategories.Beauty,
                Topic = "รีวิว [ชื่อผลิตภัณฑ์] ใช้แล้วผิวสวย กระจ่างใส",
                ContentType = "รีวิวสินค้า",
                Tone = "เป็นมิตร/Friendly",
                Length = "ยาว (1 ย่อหน้า)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "ความงาม, ผิวพรรณ, สวย, กระจ่างใส",
                SuggestedPlatforms = new() { "Instagram", "Facebook", "TikTok" }
            },

            // ฟิตเนส
            new()
            {
                Name = "Tips ออกกำลังกาย",
                Description = "เทคนิคการออกกำลังกาย",
                Category = PromptTemplateCategories.Fitness,
                Topic = "5 ท่าออกกำลังกายง่ายๆ ที่บ้าน เห็นผลใน 2 สัปดาห์",
                ContentType = "Tips & Tricks",
                Tone = "สร้างแรงบันดาลใจ",
                Length = "ยาว (1 ย่อหน้า)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "ฟิตเนส, ออกกำลังกาย, สุขภาพ, แข็งแรง",
                SuggestedPlatforms = new() { "Facebook", "Instagram", "TikTok" }
            },

            // การศึกษา
            new()
            {
                Name = "คอร์สเรียนใหม่",
                Description = "ประชาสัมพันธ์คอร์สเรียน",
                Category = PromptTemplateCategories.Education,
                Topic = "เปิดรับสมัครคอร์ส [ชื่อคอร์ส] เรียนกับผู้เชี่ยวชาญ",
                ContentType = "โพสต์โปรโมท",
                Tone = "มืออาชีพ/Professional",
                Length = "ปานกลาง (3-5 ประโยค)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "คอร์สเรียน, การศึกษา, เรียนรู้, ทักษะ",
                SuggestedPlatforms = new() { "Facebook", "LinkedIn", "Instagram" }
            },

            // เทคโนโลยี
            new()
            {
                Name = "รีวิวแก็ดเจ็ต",
                Description = "รีวิวอุปกรณ์เทคโนโลยี",
                Category = PromptTemplateCategories.Technology,
                Topic = "รีวิว [ชื่ออุปกรณ์] สเปกดี ราคาคุ้มค่า",
                ContentType = "รีวิวสินค้า",
                Tone = "มืออาชีพ/Professional",
                Length = "ยาว (1 ย่อหน้า)",
                Language = "ไทย + English (ผสม)",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "เทคโนโลยี, แก็ดเจ็ต, รีวิว, สเปก",
                SuggestedPlatforms = new() { "Facebook", "Twitter", "Instagram" }
            },

            // ท่องเที่ยว
            new()
            {
                Name = "แนะนำสถานที่ท่องเที่ยว",
                Description = "รีวิวสถานที่ท่องเที่ยว",
                Category = PromptTemplateCategories.Travel,
                Topic = "พาไปเที่ยว [ชื่อสถานที่] ทะเลสวย น้ำใส บรรยากาศดี",
                ContentType = "เล่าเรื่อง/Storytelling",
                Tone = "สร้างแรงบันดาลใจ",
                Length = "ยาวมาก (2+ ย่อหน้า)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = false,
                Keywords = "ท่องเที่ยว, เที่ยว, ทะเล, ภูเขา",
                SuggestedPlatforms = new() { "Facebook", "Instagram" }
            },

            // อีเวนท์
            new()
            {
                Name = "ประกาศอีเวนท์",
                Description = "ประชาสัมพันธ์งานอีเวนท์",
                Category = PromptTemplateCategories.Event,
                Topic = "เชิญร่วมงาน [ชื่องาน] วันที่ [วันที่] ณ [สถานที่]",
                ContentType = "ข่าวสาร/อัพเดท",
                Tone = "มืออาชีพ/Professional",
                Length = "ปานกลาง (3-5 ประโยค)",
                Language = "ไทย",
                IncludeEmojis = true,
                IncludeCTA = true,
                Keywords = "งาน, อีเวนท์, กิจกรรม, ร่วมงาน",
                SuggestedPlatforms = new() { "Facebook", "Instagram", "LINE" }
            }
        };
    }

    public static List<string> GetCategories()
    {
        return new List<string>
        {
            PromptTemplateCategories.Restaurant,
            PromptTemplateCategories.Cafe,
            PromptTemplateCategories.Fashion,
            PromptTemplateCategories.Beauty,
            PromptTemplateCategories.Fitness,
            PromptTemplateCategories.Education,
            PromptTemplateCategories.Technology,
            PromptTemplateCategories.Travel,
            PromptTemplateCategories.RealEstate,
            PromptTemplateCategories.Event
        };
    }

    public static List<PromptTemplate> GetByCategory(string category)
    {
        return GetAll().Where(t => t.Category == category).ToList();
    }
}
