using Newtonsoft.Json;

namespace MyPostXAgent.Core.Models;

/// <summary>
/// Workflow ที่เรียนรู้มาสำหรับแต่ละ Platform
/// Compatible with PostXAgent .mpflow format
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

    [JsonProperty("task_type")]
    public string TaskType { get; set; } = "";

    [JsonProperty("is_human_trained")]
    public bool IsHumanTrained { get; set; }

    public double GetSuccessRate()
    {
        var total = SuccessCount + FailureCount;
        return total > 0 ? (double)SuccessCount / total : 0.5;
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }

    public static LearnedWorkflow? FromJson(string json)
    {
        try
        {
            return JsonConvert.DeserializeObject<LearnedWorkflow>(json);
        }
        catch
        {
            return null;
        }
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
    public string? InputVariable { get; set; }

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

public enum StepAction
{
    Navigate,
    Click,
    DoubleClick,
    RightClick,
    Type,
    Clear,
    Select,
    Upload,
    DragDrop,
    Scroll,
    Hover,
    Wait,
    WaitForElement,
    WaitForNavigation,
    Screenshot,
    ExtractText,
    ExtractAttribute,
    AssertVisible,
    AssertText,
    ExecuteScript,
    PressKey,
    Login,
}

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
    public string? VisualHash { get; set; }

    [JsonProperty("ai_description")]
    public string? AIDescription { get; set; }

    [JsonProperty("confidence")]
    public double Confidence { get; set; } = 1.0;
}

public enum SelectorType
{
    CSS,
    XPath,
    Id,
    Name,
    ClassName,
    TagName,
    LinkText,
    PartialLinkText,
    Placeholder,
    Label,
    AriaLabel,
    TestId,
    Role,
    Text,
    Visual,
    Smart,
}

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
    Manual,
    AIObserved,
    AIGenerated,
    Imported,
    AutoRecovered,
}
