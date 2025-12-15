using AIManager.Core.WebAutomation.Models;
using AIManager.Core.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace AIManager.Core.WebAutomation;

/// <summary>
/// AI-powered Element Analyzer - วิเคราะห์และค้นหา Element บนหน้าเว็บ
/// </summary>
public class AIElementAnalyzer
{
    private readonly ILogger<AIElementAnalyzer> _logger;
    private readonly ContentGeneratorService _aiService;

    public AIElementAnalyzer(ILogger<AIElementAnalyzer> logger)
    {
        _logger = logger;
        _aiService = new ContentGeneratorService();
    }

    /// <summary>
    /// วิเคราะห์ Element และสร้างคำอธิบาย
    /// </summary>
    public async Task<ElementAnalysis?> AnalyzeElement(
        RecordedElement element,
        CancellationToken ct = default)
    {
        try
        {
            // สร้าง description จาก AI
            var prompt = BuildAnalysisPrompt(element);
            var result = await _aiService.GenerateAsync(
                prompt,
                null,
                "general",
                "en",
                ct
            );

            return new ElementAnalysis
            {
                Description = result.Text,
                Confidence = CalculateElementConfidence(element),
                SuggestedSelectors = GenerateSuggestedSelectors(element),
                ElementPurpose = DetectElementPurpose(element)
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze element with AI");
            return new ElementAnalysis
            {
                Description = GenerateFallbackDescription(element),
                Confidence = CalculateElementConfidence(element),
                SuggestedSelectors = GenerateSuggestedSelectors(element),
                ElementPurpose = DetectElementPurpose(element)
            };
        }
    }

    /// <summary>
    /// ค้นหา Element ที่คล้ายกันบนหน้าใหม่
    /// </summary>
    public async Task<ElementSelector?> FindSimilarElement(
        ElementSelector originalSelector,
        string pageHtml,
        string? screenshot,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Searching for similar element on page");

        // ลอง selector เดิมก่อน
        var originalFound = CheckSelectorInHtml(originalSelector, pageHtml);
        if (originalFound)
        {
            return originalSelector;
        }

        // ใช้ AI วิเคราะห์ HTML หา element ที่คล้ายกัน
        var candidates = await FindCandidateElements(originalSelector, pageHtml, ct);

        if (candidates.Count == 0)
        {
            return null;
        }

        // เรียงตาม confidence และเลือกตัวที่ดีที่สุด
        return candidates.OrderByDescending(c => c.Confidence).FirstOrDefault();
    }

    /// <summary>
    /// สร้าง Smart Selector จาก context
    /// </summary>
    public ElementSelector CreateSmartSelector(
        string purpose,
        string pageContext,
        Dictionary<string, string>? hints = null)
    {
        var selector = new ElementSelector
        {
            Type = SelectorType.Smart,
            AIDescription = purpose
        };

        // ใช้ hints เพื่อสร้าง selector
        if (hints != null)
        {
            if (hints.TryGetValue("near_text", out var nearText))
            {
                // ค้นหา element ใกล้ text
                selector.Value = $"//*[contains(text(), '{nearText}')]/following::*[self::input or self::button or self::textarea][1]";
                selector.Type = SelectorType.XPath;
            }
            else if (hints.TryGetValue("label", out var label))
            {
                selector.Value = $"//label[contains(text(), '{label}')]/following-sibling::*[1]";
                selector.Type = SelectorType.XPath;
            }
        }

        return selector;
    }

    /// <summary>
    /// เรียนรู้ pattern ของ platform
    /// </summary>
    public PlatformPattern LearnPlatformPattern(
        string platform,
        List<RecordedStep> steps)
    {
        var pattern = new PlatformPattern
        {
            Platform = platform,
            CommonSelectors = new Dictionary<string, List<ElementSelector>>()
        };

        // วิเคราะห์ steps เพื่อหา pattern
        foreach (var step in steps.Where(s => s.Element != null))
        {
            var purpose = DetectElementPurpose(step.Element!);

            if (!pattern.CommonSelectors.ContainsKey(purpose))
            {
                pattern.CommonSelectors[purpose] = new List<ElementSelector>();
            }

            var selector = CreateSelectorFromElement(step.Element!);
            pattern.CommonSelectors[purpose].Add(selector);
        }

        // หา common patterns
        pattern.UrlPatterns = steps
            .Where(s => !string.IsNullOrEmpty(s.PageUrl))
            .Select(s => ExtractUrlPattern(s.PageUrl!))
            .Distinct()
            .ToList();

        return pattern;
    }

    private string BuildAnalysisPrompt(RecordedElement element)
    {
        return $@"Analyze this HTML element and provide a brief description of its purpose:
Tag: {element.TagName}
ID: {element.Id ?? "none"}
Classes: {element.ClassName ?? "none"}
Text: {element.TextContent?.Substring(0, Math.Min(100, element.TextContent?.Length ?? 0)) ?? "none"}
Placeholder: {element.Placeholder ?? "none"}
Type: {element.Type ?? "none"}
ARIA Label: {element.Attributes.GetValueOrDefault("aria-label", "none")}

Respond with only a short description (max 20 words) of what this element is for.";
    }

    private string GenerateFallbackDescription(RecordedElement element)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(element.TagName))
            parts.Add(element.TagName);

        if (!string.IsNullOrEmpty(element.Type))
            parts.Add($"type={element.Type}");

        if (!string.IsNullOrEmpty(element.Placeholder))
            parts.Add($"placeholder='{element.Placeholder}'");

        if (!string.IsNullOrEmpty(element.TextContent) && element.TextContent.Length <= 50)
            parts.Add($"text='{element.TextContent}'");

        return string.Join(", ", parts);
    }

    private double CalculateElementConfidence(RecordedElement element)
    {
        double confidence = 0.5;

        // ID ที่ไม่ dynamic = +0.3
        if (!string.IsNullOrEmpty(element.Id) && !IsDynamicIdentifier(element.Id))
            confidence += 0.3;

        // data-testid = +0.3
        if (element.Attributes.ContainsKey("data-testid"))
            confidence += 0.3;

        // aria-label = +0.2
        if (element.Attributes.ContainsKey("aria-label"))
            confidence += 0.2;

        // name attribute = +0.15
        if (!string.IsNullOrEmpty(element.Name))
            confidence += 0.15;

        // มี unique text = +0.1
        if (!string.IsNullOrEmpty(element.TextContent) &&
            element.TextContent.Length >= 3 &&
            element.TextContent.Length <= 50)
            confidence += 0.1;

        return Math.Min(1.0, confidence);
    }

    private List<ElementSelector> GenerateSuggestedSelectors(RecordedElement element)
    {
        var selectors = new List<ElementSelector>();

        // ID selector
        if (!string.IsNullOrEmpty(element.Id) && !IsDynamicIdentifier(element.Id))
        {
            selectors.Add(new ElementSelector
            {
                Type = SelectorType.Id,
                Value = element.Id,
                Confidence = 0.95
            });
        }

        // data-testid
        if (element.Attributes.TryGetValue("data-testid", out var testId))
        {
            selectors.Add(new ElementSelector
            {
                Type = SelectorType.TestId,
                Value = testId,
                Confidence = 0.95
            });
        }

        // aria-label
        if (element.Attributes.TryGetValue("aria-label", out var ariaLabel))
        {
            selectors.Add(new ElementSelector
            {
                Type = SelectorType.AriaLabel,
                Value = ariaLabel,
                Confidence = 0.9
            });
        }

        // CSS with multiple attributes
        var cssSelector = BuildRobustCssSelector(element);
        if (!string.IsNullOrEmpty(cssSelector))
        {
            selectors.Add(new ElementSelector
            {
                Type = SelectorType.CSS,
                Value = cssSelector,
                Confidence = 0.8
            });
        }

        // Text-based selector
        if (!string.IsNullOrEmpty(element.TextContent) && element.TextContent.Length <= 50)
        {
            selectors.Add(new ElementSelector
            {
                Type = SelectorType.Text,
                Value = element.TextContent.Trim(),
                Confidence = 0.7
            });
        }

        return selectors;
    }

    private string DetectElementPurpose(RecordedElement element)
    {
        var tag = element.TagName.ToLowerInvariant();
        var type = element.Type?.ToLowerInvariant() ?? "";
        var placeholder = element.Placeholder?.ToLowerInvariant() ?? "";
        var ariaLabel = element.Attributes.GetValueOrDefault("aria-label", "").ToLowerInvariant();
        var name = element.Name?.ToLowerInvariant() ?? "";
        var classes = element.ClassName?.ToLowerInvariant() ?? "";

        // Input types
        if (tag == "input" || tag == "textarea")
        {
            if (type == "file") return "file_upload";
            if (type == "password") return "password_input";
            if (type == "email") return "email_input";

            if (placeholder.Contains("message") || placeholder.Contains("post") ||
                name.Contains("content") || ariaLabel.Contains("compose"))
                return "content_input";

            if (placeholder.Contains("search") || name.Contains("search"))
                return "search_input";

            return "text_input";
        }

        // Buttons
        if (tag == "button" || type == "submit")
        {
            var text = element.TextContent?.ToLowerInvariant() ?? "";

            if (text.Contains("post") || text.Contains("share") || text.Contains("publish"))
                return "submit_post";

            if (text.Contains("login") || text.Contains("sign in"))
                return "login_button";

            if (text.Contains("upload"))
                return "upload_button";

            if (text.Contains("next") || text.Contains("continue"))
                return "next_button";

            return "button";
        }

        // Links
        if (tag == "a")
        {
            return "link";
        }

        // Images/Media
        if (tag == "img" || tag == "video")
        {
            return "media";
        }

        // Dropdown
        if (tag == "select")
        {
            return "dropdown";
        }

        return "unknown";
    }

    private bool CheckSelectorInHtml(ElementSelector selector, string html)
    {
        // Simple check - ในการใช้งานจริงควรใช้ HTML parser
        return selector.Type switch
        {
            SelectorType.Id => html.Contains($"id=\"{selector.Value}\"") ||
                               html.Contains($"id='{selector.Value}'"),
            SelectorType.TestId => html.Contains($"data-testid=\"{selector.Value}\""),
            SelectorType.AriaLabel => html.Contains($"aria-label=\"{selector.Value}\""),
            SelectorType.Name => html.Contains($"name=\"{selector.Value}\""),
            _ => false
        };
    }

    private async Task<List<ElementSelector>> FindCandidateElements(
        ElementSelector original,
        string pageHtml,
        CancellationToken ct)
    {
        var candidates = new List<ElementSelector>();

        // ถ้ามี AI description ใช้มันหา
        if (!string.IsNullOrEmpty(original.AIDescription))
        {
            // ใช้ AI หา element ที่ตรงกับ description
            var prompt = $@"In this HTML, find an element that matches this description: '{original.AIDescription}'
Return only the CSS selector or XPath that would select this element.
HTML snippet (first 5000 chars):
{pageHtml.Substring(0, Math.Min(5000, pageHtml.Length))}";

            try
            {
                var result = await _aiService.GenerateAsync(prompt, null, "general", "en", ct);
                var foundSelector = ParseSelectorFromAIResponse(result.Text);
                if (foundSelector != null)
                {
                    foundSelector.Confidence = 0.7;
                    candidates.Add(foundSelector);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI element search failed");
            }
        }

        // ถ้ามี text content ลองหาด้วย text
        if (!string.IsNullOrEmpty(original.TextContent))
        {
            if (pageHtml.Contains(original.TextContent))
            {
                candidates.Add(new ElementSelector
                {
                    Type = SelectorType.Text,
                    Value = original.TextContent,
                    Confidence = 0.6
                });
            }
        }

        return candidates;
    }

    private ElementSelector? ParseSelectorFromAIResponse(string response)
    {
        // Try to extract selector from AI response
        var cssMatch = Regex.Match(response, @"([.#]?[\w\-\[\]=\""'\s>:]+)");
        if (cssMatch.Success)
        {
            var value = cssMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                return new ElementSelector
                {
                    Type = value.StartsWith("//") ? SelectorType.XPath : SelectorType.CSS,
                    Value = value
                };
            }
        }
        return null;
    }

    private ElementSelector CreateSelectorFromElement(RecordedElement element)
    {
        // ใช้ logic เดียวกับ WorkflowLearningEngine
        if (!string.IsNullOrEmpty(element.Id) && !IsDynamicIdentifier(element.Id))
        {
            return new ElementSelector { Type = SelectorType.Id, Value = element.Id };
        }

        if (element.Attributes.TryGetValue("data-testid", out var testId))
        {
            return new ElementSelector { Type = SelectorType.TestId, Value = testId };
        }

        return new ElementSelector
        {
            Type = SelectorType.CSS,
            Value = element.CssSelector ?? element.TagName
        };
    }

    private string BuildRobustCssSelector(RecordedElement element)
    {
        var parts = new List<string>();

        parts.Add(element.TagName);

        if (!string.IsNullOrEmpty(element.Id) && !IsDynamicIdentifier(element.Id))
        {
            parts.Add($"#{element.Id}");
            return string.Join("", parts);
        }

        // Add stable classes
        if (!string.IsNullOrEmpty(element.ClassName))
        {
            var stableClasses = element.ClassName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(c => !IsDynamicIdentifier(c))
                .Take(2);

            foreach (var cls in stableClasses)
            {
                parts.Add($".{cls}");
            }
        }

        // Add type if input
        if (!string.IsNullOrEmpty(element.Type))
        {
            parts.Add($"[type=\"{element.Type}\"]");
        }

        // Add name if available
        if (!string.IsNullOrEmpty(element.Name))
        {
            parts.Add($"[name=\"{element.Name}\"]");
        }

        return string.Join("", parts);
    }

    private string ExtractUrlPattern(string url)
    {
        // แปลง URL เป็น pattern
        // เช่น https://facebook.com/123456/posts -> https://facebook.com/*/posts
        var pattern = Regex.Replace(url, @"/\d+", "/*");
        pattern = Regex.Replace(pattern, @"\?.*$", "");
        return pattern;
    }

    private bool IsDynamicIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value)) return true;

        // Too long = likely dynamic
        if (value.Length > 30) return true;

        // Contains GUID-like pattern
        if (Regex.IsMatch(value, @"[a-f0-9]{8}-[a-f0-9]{4}")) return true;

        // Mostly numbers
        if (value.Count(char.IsDigit) > value.Length * 0.5) return true;

        // Common dynamic prefixes
        var dynamicPrefixes = new[] { "ember", "react", "ng-", "_", "svelte", "vue-" };
        if (dynamicPrefixes.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}

/// <summary>
/// ผลการวิเคราะห์ Element
/// </summary>
public class ElementAnalysis
{
    public string Description { get; set; } = "";
    public double Confidence { get; set; }
    public List<ElementSelector> SuggestedSelectors { get; set; } = new();
    public string ElementPurpose { get; set; } = "";
}

/// <summary>
/// Pattern ของ Platform
/// </summary>
public class PlatformPattern
{
    public string Platform { get; set; } = "";
    public Dictionary<string, List<ElementSelector>> CommonSelectors { get; set; } = new();
    public List<string> UrlPatterns { get; set; } = new();
    public Dictionary<string, string> CommonXPaths { get; set; } = new();
}
