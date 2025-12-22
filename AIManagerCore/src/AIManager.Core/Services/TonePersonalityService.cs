using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AIManager.Core.Services;

// ═══════════════════════════════════════════════════════════════════════════════
// TONE PERSONALITY SERVICE - Manage response personalities and tone configurations
// ═══════════════════════════════════════════════════════════════════════════════

#region Models

public class ToneConfiguration
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string NameTh { get; set; } = "";

    // Personality traits (0-100 scale)
    public int Friendly { get; set; } = 70;
    public int Formal { get; set; } = 30;
    public int Humor { get; set; } = 40;
    public int EmojiUsage { get; set; } = 50;
    public int Enthusiasm { get; set; } = 60;
    public int Empathy { get; set; } = 70;

    // Language preferences
    public string DefaultLanguage { get; set; } = "th";
    public bool MixLanguages { get; set; } = true;
    public bool UseHonorifics { get; set; } = true;
    public bool UseParticles { get; set; } = true; // Thai particles: ครับ/ค่ะ/นะ

    // Templates
    public Dictionary<string, List<string>> ResponseTemplates { get; set; } = new();
    public Dictionary<string, KeywordTrigger> KeywordTriggers { get; set; } = new();
    public List<string> ProhibitedWords { get; set; } = new();
    public List<string> RequiredElements { get; set; } = new();

    // Custom instructions
    public string? CustomInstructions { get; set; }

    // Settings
    public bool AutoReplyEnabled { get; set; }
    public int ReplyDelaySeconds { get; set; } = 60;
}

public class KeywordTrigger
{
    public string Response { get; set; } = "";
    public int Priority { get; set; }
    public string? Action { get; set; }
}

public class TonePreset
{
    public string Name { get; set; } = "";
    public string NameTh { get; set; } = "";
    public ToneConfiguration Configuration { get; set; } = new();
}

#endregion

public class TonePersonalityService
{
    private readonly ILogger<TonePersonalityService> _logger;
    private readonly Dictionary<string, TonePreset> _presets;

    public TonePersonalityService(ILogger<TonePersonalityService> logger)
    {
        _logger = logger;
        _presets = InitializePresets();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRESET TONES
    // ═══════════════════════════════════════════════════════════════════════

    private Dictionary<string, TonePreset> InitializePresets()
    {
        return new Dictionary<string, TonePreset>
        {
            ["friendly"] = new TonePreset
            {
                Name = "Friendly",
                NameTh = "เป็นมิตร",
                Configuration = new ToneConfiguration
                {
                    Name = "Friendly",
                    NameTh = "เป็นมิตร",
                    Friendly = 90,
                    Formal = 20,
                    Humor = 50,
                    EmojiUsage = 70,
                    Enthusiasm = 80,
                    Empathy = 80,
                    ResponseTemplates = new Dictionary<string, List<string>>
                    {
                        ["greeting"] = new() { "สวัสดีค่ะ/ครับ! 😊", "ยินดีต้อนรับค่ะ/ครับ!", "สวัสดีจ้า!" },
                        ["thank_you"] = new() { "ขอบคุณมากค่ะ/ครับ! 🙏", "ขอบคุณนะคะ/ครับ!" },
                        ["apology"] = new() { "ขอโทษด้วยนะคะ/ครับ 🙏", "ต้องขออภัยด้วยค่ะ/ครับ" }
                    }
                }
            },
            ["professional"] = new TonePreset
            {
                Name = "Professional",
                NameTh = "มืออาชีพ",
                Configuration = new ToneConfiguration
                {
                    Name = "Professional",
                    NameTh = "มืออาชีพ",
                    Friendly = 50,
                    Formal = 80,
                    Humor = 10,
                    EmojiUsage = 20,
                    Enthusiasm = 40,
                    Empathy = 60,
                    UseHonorifics = true,
                    ResponseTemplates = new Dictionary<string, List<string>>
                    {
                        ["greeting"] = new() { "สวัสดีครับ/ค่ะ", "ขอบพระคุณที่ติดต่อมา" },
                        ["thank_you"] = new() { "ขอบพระคุณครับ/ค่ะ", "ขอบคุณสำหรับความไว้วางใจ" },
                        ["apology"] = new() { "ขออภัยในความไม่สะดวก", "ทางเราต้องขออภัยเป็นอย่างยิ่ง" }
                    }
                }
            },
            ["casual"] = new TonePreset
            {
                Name = "Casual",
                NameTh = "สบายๆ",
                Configuration = new ToneConfiguration
                {
                    Name = "Casual",
                    NameTh = "สบายๆ",
                    Friendly = 80,
                    Formal = 10,
                    Humor = 70,
                    EmojiUsage = 80,
                    Enthusiasm = 70,
                    Empathy = 60,
                    UseHonorifics = false,
                    UseParticles = false,
                    ResponseTemplates = new Dictionary<string, List<string>>
                    {
                        ["greeting"] = new() { "หวัดดีจ้า! 👋", "โย่! 😄", "ว่าไง!" },
                        ["thank_you"] = new() { "ขอบใจนะ! 💕", "Thanks จ้า! 🙏" },
                        ["apology"] = new() { "ขอโทษจ้า 🙏", "Sorry นะ!" }
                    }
                }
            },
            ["supportive"] = new TonePreset
            {
                Name = "Supportive",
                NameTh = "ช่วยเหลือ",
                Configuration = new ToneConfiguration
                {
                    Name = "Supportive",
                    NameTh = "ช่วยเหลือ",
                    Friendly = 70,
                    Formal = 50,
                    Humor = 20,
                    EmojiUsage = 40,
                    Enthusiasm = 50,
                    Empathy = 95,
                    ResponseTemplates = new Dictionary<string, List<string>>
                    {
                        ["greeting"] = new() { "สวัสดีค่ะ/ครับ ยินดีช่วยเหลือค่ะ/ครับ", "มีอะไรให้ช่วยไหมคะ/ครับ?" },
                        ["thank_you"] = new() { "ยินดีค่ะ/ครับ มีอะไรเพิ่มเติมสามารถถามได้เลยนะคะ/ครับ" },
                        ["apology"] = new() { "เข้าใจค่ะ/ครับ ขออภัยในความไม่สะดวก เดี๋ยวจะรีบช่วยแก้ไขให้ค่ะ/ครับ" }
                    },
                    CustomInstructions = "เน้นการช่วยเหลือและแก้ปัญหา แสดงความเข้าใจในปัญหาของลูกค้า"
                }
            },
            ["sales"] = new TonePreset
            {
                Name = "Sales",
                NameTh = "ขาย",
                Configuration = new ToneConfiguration
                {
                    Name = "Sales",
                    NameTh = "ขาย",
                    Friendly = 85,
                    Formal = 40,
                    Humor = 30,
                    EmojiUsage = 60,
                    Enthusiasm = 90,
                    Empathy = 50,
                    ResponseTemplates = new Dictionary<string, List<string>>
                    {
                        ["greeting"] = new() { "สวัสดีค่ะ/ครับ! 🎉 วันนี้มีโปรโมชั่นดีๆ มาแนะนำค่ะ/ครับ!" },
                        ["thank_you"] = new() { "ขอบคุณมากค่ะ/ครับ! 🙏 หวังว่าจะได้ให้บริการอีกนะคะ/ครับ!" }
                    },
                    RequiredElements = new List<string> { "แนะนำสินค้า/โปรโมชั่นถ้าเหมาะสม" },
                    CustomInstructions = "หาโอกาสแนะนำสินค้าหรือโปรโมชั่นอย่างเป็นธรรมชาติ ไม่ยัดเยียด"
                }
            }
        };
    }

    /// <summary>
    /// Get available presets
    /// </summary>
    public List<TonePreset> GetPresets()
    {
        return _presets.Values.ToList();
    }

    /// <summary>
    /// Get preset by name
    /// </summary>
    public TonePreset? GetPreset(string name)
    {
        return _presets.TryGetValue(name.ToLower(), out var preset) ? preset : null;
    }

    /// <summary>
    /// Convert ToneConfig to ToneConfiguration
    /// </summary>
    public ToneConfiguration FromToneConfig(Models.ToneConfig? config)
    {
        if (config == null)
        {
            return GetPreset("friendly")?.Configuration ?? new ToneConfiguration();
        }

        return new ToneConfiguration
        {
            Name = config.Name,
            Friendly = config.Friendly,
            Formal = config.Formal,
            Humor = config.Humor,
            EmojiUsage = config.EmojiUsage,
            UseParticles = config.UseParticles,
            CustomInstructions = config.CustomInstructions
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SYSTEM PROMPT BUILDING
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Build AI system prompt from tone configuration
    /// </summary>
    public string BuildSystemPrompt(ToneConfiguration tone, string platform = "general", string? brandName = null)
    {
        var prompt = "คุณเป็น AI ที่ตอบคอมเมนต์บน social media";
        if (!string.IsNullOrEmpty(brandName))
        {
            prompt += $" ในนามของแบรนด์ \"{brandName}\"";
        }
        prompt += "\n\n";

        // Personality description
        prompt += "## บุคลิกในการตอบ:\n";

        // Friendly level
        if (tone.Friendly > 70)
            prompt += "- เป็นมิตรและอบอุ่น ทักทายด้วยความยิ้มแย้ม\n";
        else if (tone.Friendly > 40)
            prompt += "- สุภาพและเป็นกันเอง\n";
        else
            prompt += "- ตอบตรงประเด็น ไม่ต้องเกริ่นมาก\n";

        // Formal level
        if (tone.Formal > 70)
            prompt += "- ใช้ภาษาทางการ สุภาพ เหมาะสมกับบริบททางธุรกิจ\n";
        else if (tone.Formal > 40)
            prompt += "- ใช้ภาษาสุภาพแต่ไม่ทางการเกินไป\n";
        else
            prompt += "- ใช้ภาษาง่ายๆ เป็นกันเอง พูดเหมือนเพื่อนคุยกัน\n";

        // Humor
        if (tone.Humor > 60)
            prompt += "- สามารถใส่มุขตลกหรือความน่ารักได้ตามความเหมาะสม\n";
        else if (tone.Humor < 20)
            prompt += "- เน้นความจริงจัง ไม่ใช้มุขตลก\n";

        // Emoji
        if (tone.EmojiUsage > 70)
            prompt += "- ใช้ emoji ได้เยอะเพื่อสร้างความน่ารักและใกล้ชิด\n";
        else if (tone.EmojiUsage > 30)
            prompt += "- ใช้ emoji ได้บ้างตามความเหมาะสม\n";
        else
            prompt += "- ไม่ใช้ emoji หรือใช้น้อยมาก\n";

        // Enthusiasm
        if (tone.Enthusiasm > 70)
            prompt += "- กระตือรือร้นและมีพลัง!\n";
        else if (tone.Enthusiasm < 30)
            prompt += "- สงบ นิ่ง ไม่ตื่นเต้นเกินไป\n";

        // Empathy
        if (tone.Empathy > 70)
            prompt += "- แสดงความเข้าใจและใส่ใจความรู้สึกของลูกค้าเสมอ\n";

        // Language preferences
        prompt += "\n## ภาษา:\n";
        prompt += $"- ภาษาหลัก: {(tone.DefaultLanguage == "th" ? "ไทย" : "อังกฤษ")}\n";

        if (tone.UseHonorifics)
            prompt += "- ใช้คำสรรพนามและคำลงท้ายที่สุภาพ\n";

        if (tone.UseParticles)
            prompt += "- ใช้คำลงท้ายภาษาไทย เช่น ครับ/ค่ะ/นะ/จ้า ตามความเหมาะสม\n";

        if (tone.MixLanguages)
            prompt += "- สามารถผสมภาษาอังกฤษได้ตามความเหมาะสม\n";

        // Platform-specific
        prompt += $"\n## Platform: {platform}\n";
        prompt += GetPlatformGuidelines(platform);

        // Required elements
        if (tone.RequiredElements.Any())
        {
            prompt += "\n## ต้องมีในคำตอบ:\n";
            foreach (var element in tone.RequiredElements)
            {
                prompt += $"- {element}\n";
            }
        }

        // Prohibited words
        if (tone.ProhibitedWords.Any())
        {
            prompt += $"\n## ห้ามใช้คำต่อไปนี้: {string.Join(", ", tone.ProhibitedWords)}\n";
        }

        // Custom instructions
        if (!string.IsNullOrEmpty(tone.CustomInstructions))
        {
            prompt += $"\n## คำแนะนำเพิ่มเติม:\n{tone.CustomInstructions}\n";
        }

        // Response guidelines
        prompt += @"

## แนวทางการตอบ:
1. อ่านและเข้าใจคอมเมนต์ให้ดีก่อนตอบ
2. ตอบให้ตรงประเด็นกับสิ่งที่ถามหรือพูดถึง
3. ถ้าเป็นคำถาม ให้ตอบให้ชัดเจน
4. ถ้าเป็นคำชม ให้ขอบคุณอย่างจริงใจ
5. ถ้าเป็นคำตำหนิ ให้รับฟังและแสดงความเข้าใจ
6. ความยาวคำตอบเหมาะสม ไม่สั้นเกินไปหรือยาวเกินไป
7. ตอบให้เป็นธรรมชาติ ไม่เหมือนหุ่นยนต์";

        return prompt;
    }

    /// <summary>
    /// Get platform-specific guidelines
    /// </summary>
    private string GetPlatformGuidelines(string platform)
    {
        return platform.ToLower() switch
        {
            "facebook" => "- Facebook: ความยาวพอดี สามารถใช้ emoji ได้ ตอบเป็นกันเอง\n",
            "instagram" => "- Instagram: สั้นกระชับ ใช้ emoji ได้มาก มีความ trendy\n",
            "twitter" or "x" => "- Twitter/X: สั้นมาก (ไม่เกิน 280 ตัวอักษร) ตรงประเด็น\n",
            "tiktok" => "- TikTok: สนุกสนาน ใช้ภาษาวัยรุ่น trendy มากๆ\n",
            "line" => "- LINE: สุภาพ ใช้ emoji/sticker ได้ ตอบรวดเร็ว\n",
            "youtube" => "- YouTube: ตอบได้ยาวขึ้น ให้ข้อมูลครบถ้วน\n",
            "linkedin" => "- LinkedIn: ทางการ มืออาชีพ ไม่ใช้ emoji มาก\n",
            _ => "- ปรับความยาวและสไตล์ให้เหมาะกับ platform\n"
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RESPONSE GENERATION HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get a random template for a scenario
    /// </summary>
    public string? GetRandomTemplate(ToneConfiguration tone, string scenario)
    {
        if (tone.ResponseTemplates.TryGetValue(scenario, out var templates) && templates.Any())
        {
            var random = new Random();
            return templates[random.Next(templates.Count)];
        }
        return null;
    }

    /// <summary>
    /// Check for keyword triggers in text
    /// </summary>
    public KeywordTrigger? CheckKeywordTriggers(ToneConfiguration tone, string text)
    {
        var lowerText = text.ToLower();

        foreach (var (keyword, trigger) in tone.KeywordTriggers)
        {
            if (lowerText.Contains(keyword.ToLower()))
            {
                return trigger;
            }
        }

        return null;
    }

    /// <summary>
    /// Add Thai particles to response based on tone
    /// </summary>
    public string AddThaiParticles(string response, ToneConfiguration tone, bool isFemale = true)
    {
        if (!tone.UseParticles) return response;

        var particle = isFemale ? "ค่ะ" : "ครับ";

        // Only add if response doesn't already end with a particle
        var existingParticles = new[] { "ครับ", "ค่ะ", "คะ", "จ้า", "นะ", "น่ะ" };
        var lastWord = response.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";

        if (existingParticles.Any(p => lastWord.EndsWith(p)))
        {
            return response;
        }

        // Add particle before emoji if present
        var emojiPattern = new System.Text.RegularExpressions.Regex(@"[\p{So}\p{Cs}]+$");
        var match = emojiPattern.Match(response);

        if (match.Success)
        {
            return response.Substring(0, match.Index).TrimEnd() + particle + " " + match.Value;
        }

        return response.TrimEnd() + particle;
    }

    /// <summary>
    /// Validate response against tone restrictions
    /// </summary>
    public (bool IsValid, List<string> Violations) ValidateResponse(string response, ToneConfiguration tone)
    {
        var violations = new List<string>();

        // Check prohibited words
        foreach (var word in tone.ProhibitedWords)
        {
            if (response.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"Contains prohibited word: {word}");
            }
        }

        // Check required elements
        foreach (var element in tone.RequiredElements)
        {
            if (!response.Contains(element, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add($"Missing required element: {element}");
            }
        }

        // Check emoji usage
        var emojiCount = CountEmojis(response);
        if (tone.EmojiUsage < 20 && emojiCount > 1)
        {
            violations.Add("Too many emojis for this tone");
        }

        return (violations.Count == 0, violations);
    }

    private int CountEmojis(string text)
    {
        var emojiPattern = new System.Text.RegularExpressions.Regex(@"[\p{So}\p{Cs}]");
        return emojiPattern.Matches(text).Count;
    }

    /// <summary>
    /// Adjust response to match tone
    /// </summary>
    public string AdjustResponseToTone(string response, ToneConfiguration tone)
    {
        var adjusted = response;

        // Remove excess emojis if needed
        if (tone.EmojiUsage < 30)
        {
            adjusted = RemoveExcessEmojis(adjusted, 1);
        }

        // Add particles if needed
        if (tone.UseParticles)
        {
            adjusted = AddThaiParticles(adjusted, tone);
        }

        return adjusted;
    }

    private string RemoveExcessEmojis(string text, int maxEmojis)
    {
        var emojiPattern = new System.Text.RegularExpressions.Regex(@"[\p{So}\p{Cs}]+");
        var matches = emojiPattern.Matches(text);

        if (matches.Count <= maxEmojis) return text;

        var result = text;
        for (int i = matches.Count - 1; i >= maxEmojis; i--)
        {
            result = result.Remove(matches[i].Index, matches[i].Length);
        }

        return result.Trim();
    }
}
